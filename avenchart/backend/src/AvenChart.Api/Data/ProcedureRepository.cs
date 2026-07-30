using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ProcedureReportReviewConflictException(
    int expectedVersion,
    int currentVersion,
    string currentStatus)
    : Exception($"The report review changed after it was loaded (expected version {expectedVersion}; current version {currentVersion}).")
{
    public int ExpectedVersion { get; } = expectedVersion;
    public int CurrentVersion { get; } = currentVersion;
    public string CurrentStatus { get; } = currentStatus;
}

public sealed class ProcedureRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumReviewQueueLimit = 100;

    public async Task<ProcedureResultsResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureProcedureResultVersionTableAsync(connection, cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var orders = await GetOrdersAsync(connection, patient.LegacyPid, cancellationToken);
        var specimens = await GetSpecimensAsync(connection, orders.Select(order => order.Id).ToArray(), cancellationToken);
        var reports = await GetReportsAsync(connection, orders.Select(order => order.Id).ToArray(), cancellationToken);
        var results = await GetResultsAsync(connection, reports.Select(report => report.Id).ToArray(), cancellationToken);

        var specimensByOrder = specimens
            .GroupBy(specimen => specimen.OrderId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Specimen).ToList());
        var resultsByReport = results.GroupBy(result => result.ReportId).ToDictionary(group => group.Key, group => group.Select(item => item.Result).ToList());
        var reportsByOrder = reports
            .GroupBy(report => report.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(report => report.Report with
                {
                    Results = resultsByReport.GetValueOrDefault(report.Id, [])
                }).ToList());

        var procedureOrders = orders.Select(order => order.Order with
        {
            Specimens = specimensByOrder.GetValueOrDefault(order.Id, []),
            Reports = reportsByOrder.GetValueOrDefault(order.Id, [])
        }).ToList();

        return new ProcedureResultsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Counts: BuildCounts(procedureOrders, metadata.BaseDate),
            Orders: procedureOrders);
    }

    public async Task<ProcedureReportReviewQueueResponse> GetReportReviewQueueAsync(
        string? status,
        string? patientId,
        int? providerId,
        int? labId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int limit,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeReviewQueueStatus(status);
        var normalizedPatient = NormalizeText(patientId)?.ToLowerInvariant();
        var safeLimit = Math.Clamp(limit, 1, MaximumReviewQueueLimit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        int totalReports;
        int reviewedReports;
        int unreviewedReports;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                select
                    count(*) as total_reports,
                    coalesce(sum(case when coalesce(lr.review_status, '') = 'reviewed' then 1 else 0 end), 0) as reviewed_reports,
                    coalesce(sum(case when coalesce(lr.review_status, '') <> 'reviewed' then 1 else 0 end), 0) as unreviewed_reports
                from lab_reports lr
                inner join lab_orders lo on lo.id = lr.order_id
                inner join patients p on p.legacy_pid = lo.pid
                where (@patientFilter is null
                       or lower(p.canonical_id) = @patientFilter
                       or lower(p.pubpid) = @patientFilter
                       or p.legacy_pid::text = @patientFilter)
                  and (@fromDate is null or lo.order_date >= @fromDate)
                  and (@toDate is null or lo.order_date <= @toDate)
                  and (@providerFilter is null or lo.provider_id = @providerFilter)
                  and (@labFilter is null or lo.lab_id = @labFilter);
                """;
            AddReviewQueueFilterParameters(countCommand, normalizedPatient, providerId, labId, fromDate, toDate);

            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalReports = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("total_reports")));
            reviewedReports = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("reviewed_reports")));
            unreviewedReports = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("unreviewed_reports")));
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                lr.id as report_id,
                lo.id as order_id,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                trim(concat(p.last_name, ', ', p.first_name)) as patient_display_name,
                lo.order_date,
                lo.provider_id,
                nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                lo.lab_id,
                lp.name as lab_name,
                lo.code as procedure_code,
                lo.name as procedure_name,
                lr.report_date,
                lr.status as report_status,
                coalesce(lr.review_status, '') as review_status,
                lr.reviewed_by,
                lr.reviewed_at,
                lr.review_version,
                (select count(*) from lab_report_review_events lrrh where lrrh.report_id = lr.id) as review_history_count,
                lr.specimen_number,
                lr.notes
            from lab_reports lr
            inner join lab_orders lo on lo.id = lr.order_id
            inner join patients p on p.legacy_pid = lo.pid
            left join staff s on s.id = lo.provider_id
            left join lab_providers lp on lp.id = lo.lab_id
            where (@patientFilter is null
                   or lower(p.canonical_id) = @patientFilter
                   or lower(p.pubpid) = @patientFilter
                   or p.legacy_pid::text = @patientFilter)
              and (@fromDate is null or lo.order_date >= @fromDate)
              and (@toDate is null or lo.order_date <= @toDate)
              and (@providerFilter is null or lo.provider_id = @providerFilter)
              and (@labFilter is null or lo.lab_id = @labFilter)
              and ((@statusFilter = 'all')
               or (@statusFilter = 'reviewed' and coalesce(lr.review_status, '') = 'reviewed')
               or (@statusFilter = 'unreviewed' and coalesce(lr.review_status, '') <> 'reviewed'))
            order by lr.report_date desc, lr.id desc, p.last_name, p.first_name, p.legacy_pid, lo.id
            limit @limit;
            """;
        AddReviewQueueFilterParameters(command, normalizedPatient, providerId, labId, fromDate, toDate);
        command.Parameters.AddWithValue("statusFilter", normalizedStatus);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = safeLimit;

        var reports = new List<ProcedureReportReviewQueueItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                reports.Add(new ProcedureReportReviewQueueItem(
                    ReportId: reader.GetInt32(reader.GetOrdinal("report_id")),
                    OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                    OrderDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("order_date")).ToString("yyyy-MM-dd"),
                    ProviderId: ReadNullableInt(reader, "provider_id"),
                    ProviderName: ReadNullableString(reader, "provider_name"),
                    LabId: ReadNullableInt(reader, "lab_id"),
                    LabName: ReadNullableString(reader, "lab_name"),
                    ProcedureCode: ReadNullableString(reader, "procedure_code"),
                    ProcedureName: ReadNullableString(reader, "procedure_name"),
                    ReportDate: reader.GetDateTime(reader.GetOrdinal("report_date")).ToString("yyyy-MM-dd HH:mm"),
                    ReportStatus: ReadNullableString(reader, "report_status"),
                    ReviewStatus: ReadNullableString(reader, "review_status"),
                    ReviewedBy: ReadNullableString(reader, "reviewed_by"),
                    ReviewedAt: ReadNullableDateTime(reader, "reviewed_at"),
                    ReviewVersion: reader.GetInt32(reader.GetOrdinal("review_version")),
                    ReviewHistoryCount: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("review_history_count"))),
                    SpecimenNumber: ReadNullableString(reader, "specimen_number"),
                    Notes: ReadNullableString(reader, "notes")));
            }
        }

        return new ProcedureReportReviewQueueResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            StatusFilter: normalizedStatus,
            PatientFilter: normalizedPatient,
            ProviderFilter: providerId,
            LabFilter: labId,
            FromDate: fromDate?.ToString("yyyy-MM-dd"),
            ToDate: toDate?.ToString("yyyy-MM-dd"),
            Limit: safeLimit,
            TotalReports: totalReports,
            ReviewedReports: reviewedReports,
            UnreviewedReports: unreviewedReports,
            Reports: reports);
    }

    public async Task<CriticalLabResultQueueResponse> GetCriticalLabResultQueueAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select lres.id as result_id, lr.id as report_id, p.canonical_id as patient_id,
                   trim(concat(p.last_name, ', ', p.first_name)) as patient_display_name,
                   lres.code, lres.text, lres.result, lres.units, lres.abnormal, lres.result_date,
                   coalesce(ack.status, 'open') as acknowledgement_status,
                   coalesce(ack.version, 1) as acknowledgement_version,
                   ack.acknowledged_by, ack.acknowledged_at
            from lab_results lres
            inner join lab_reports lr on lr.id = lres.report_id
            inner join lab_orders lo on lo.id = lr.order_id
            inner join patients p on p.legacy_pid = lo.pid
            left join critical_lab_result_acknowledgements ack on ack.result_id = lres.id
            where lower(coalesce(lres.abnormal, '')) in ('critical', 'panic', 'hh', 'll')
              and coalesce(ack.status, 'open') = 'open'
            order by lres.result_date desc, lres.id desc;
            """;
        var results = new List<CriticalLabResultQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CriticalLabResultQueueItem(
                reader.GetInt32(reader.GetOrdinal("result_id")), reader.GetInt32(reader.GetOrdinal("report_id")),
                reader.GetString(reader.GetOrdinal("patient_id")), reader.GetString(reader.GetOrdinal("patient_display_name")),
                ReadNullableString(reader, "code"), ReadNullableString(reader, "text"), ReadNullableString(reader, "result"),
                ReadNullableString(reader, "units"), ReadNullableString(reader, "abnormal"),
                reader.GetDateTime(reader.GetOrdinal("result_date")).ToString("yyyy-MM-dd HH:mm"),
                reader.GetString(reader.GetOrdinal("acknowledgement_status")), reader.GetInt32(reader.GetOrdinal("acknowledgement_version")),
                ReadNullableString(reader, "acknowledged_by"), ReadNullableDateTime(reader, "acknowledged_at")));
        }
        return new CriticalLabResultQueueResponse(results.Count, results);
    }

    public async Task<bool> AcknowledgeCriticalLabResultAsync(
        int resultId, CriticalLabResultAcknowledgementRequest request, string actor, CancellationToken cancellationToken)
    {
        if (resultId <= 0 || request.ExpectedVersion <= 0 || !HasBoundedReason(request.Reason) || string.IsNullOrWhiteSpace(actor)) return false;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into critical_lab_result_acknowledgements (result_id, status, version, acknowledged_by, acknowledged_at, acknowledgement_reason)
            select @resultId, 'acknowledged', 2, @actor, @occurredAt, @reason
            where exists (select 1 from lab_results where id = @resultId and lower(coalesce(abnormal, '')) in ('critical', 'panic', 'hh', 'll'))
            on conflict (result_id) do update set status = 'acknowledged', version = critical_lab_result_acknowledgements.version + 1, acknowledged_by = excluded.acknowledged_by, acknowledged_at = excluded.acknowledged_at, acknowledgement_reason = excluded.acknowledgement_reason
            where critical_lab_result_acknowledgements.status = 'open' and critical_lab_result_acknowledgements.version = @expectedVersion
            returning version;
            """;
        var occurredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        command.Parameters.AddWithValue("resultId", resultId); command.Parameters.AddWithValue("actor", actor.Trim()); command.Parameters.AddWithValue("reason", request.Reason.Trim()); command.Parameters.AddWithValue("expectedVersion", request.ExpectedVersion); command.Parameters.AddWithValue("occurredAt", occurredAt);
        var version = await command.ExecuteScalarAsync(cancellationToken);
        if (version is null) return false;
        await using var history = connection.CreateCommand(); history.Transaction = transaction;
        history.CommandText = "insert into critical_lab_result_acknowledgement_events (result_id, action, previous_status, current_status, actor, reason, expected_version, resulting_version, occurred_at) values (@resultId, 'acknowledged', 'open', 'acknowledged', @actor, @reason, @expectedVersion, @resultingVersion, @occurredAt);";
        history.Parameters.AddWithValue("resultId", resultId); history.Parameters.AddWithValue("actor", actor.Trim()); history.Parameters.AddWithValue("reason", request.Reason.Trim()); history.Parameters.AddWithValue("expectedVersion", request.ExpectedVersion); history.Parameters.AddWithValue("resultingVersion", Convert.ToInt32(version)); history.Parameters.AddWithValue("occurredAt", occurredAt); await history.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken); return true;
    }

    public async Task<ProcedureReportReviewHistoryResponse?> GetReportReviewHistoryAsync(
        int reportId,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select lr.id as report_id, lr.review_version,
                   event.id as event_id, event.action, event.previous_status, event.current_status,
                   event.assigned_to, event.actor, event.reason, event.expected_version,
                   event.resulting_version, event.occurred_at
            from lab_reports lr
            left join lab_report_review_events event on event.report_id = lr.id
            where lr.id = @reportId
            order by event.occurred_at desc nulls last, event.id desc nulls last;
            """;
        command.Parameters.AddWithValue("reportId", reportId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var version = reader.GetInt32(reader.GetOrdinal("review_version"));
        var events = new List<ProcedureReportReviewEventItem>();
        do
        {
            if (reader.IsDBNull(reader.GetOrdinal("event_id")))
            {
                continue;
            }

            events.Add(new ProcedureReportReviewEventItem(
                EventId: reader.GetInt64(reader.GetOrdinal("event_id")),
                Action: reader.GetString(reader.GetOrdinal("action")),
                PreviousStatus: ReadNullableString(reader, "previous_status"),
                CurrentStatus: reader.GetString(reader.GetOrdinal("current_status")),
                AssignedTo: ReadNullableString(reader, "assigned_to"),
                Actor: reader.GetString(reader.GetOrdinal("actor")),
                Reason: ReadNullableString(reader, "reason"),
                ExpectedVersion: reader.GetInt32(reader.GetOrdinal("expected_version")),
                ResultingVersion: reader.GetInt32(reader.GetOrdinal("resulting_version")),
                OccurredAt: reader.GetDateTime(reader.GetOrdinal("occurred_at")).ToString("yyyy-MM-dd HH:mm")));
        }
        while (await reader.ReadAsync(cancellationToken));

        return new ProcedureReportReviewHistoryResponse(reportId, version, events);
    }

    public async Task<ProcedureOrderQueueResponse> GetOrderQueueAsync(
        string? status,
        string? patientId,
        int? providerId,
        int? labId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int limit,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeOrderQueueStatus(status);
        var normalizedPatient = NormalizeText(patientId)?.ToLowerInvariant();
        var safeLimit = Math.Clamp(limit, 1, MaximumReviewQueueLimit);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        int totalOrders;
        int readyToSendOrders;
        int transmittedPendingOrders;
        int reportedOrders;
        int scheduledOrders;
        int completedOrders;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                with order_queue as (
                    select
                        lo.id,
                        lo.order_date,
                        coalesce(lo.order_status, '') as order_status,
                        lo.date_transmitted,
                        (
                            select count(*)
                            from lab_reports lr
                            where lr.order_id = lo.id
                        ) as report_count
                    from lab_orders lo
                    inner join patients p on p.legacy_pid = lo.pid
                    where (@patientFilter is null
                           or lower(p.canonical_id) = @patientFilter
                           or lower(p.pubpid) = @patientFilter
                           or p.legacy_pid::text = @patientFilter)
                      and (@fromDate is null or lo.order_date >= @fromDate)
                      and (@toDate is null or lo.order_date <= @toDate)
                      and (@providerFilter is null or lo.provider_id = @providerFilter)
                      and (@labFilter is null or lo.lab_id = @labFilter)
                )
                select
                    count(*)::int as total_orders,
                    coalesce(sum(case when report_count = 0 and date_transmitted is null then 1 else 0 end), 0)::int as ready_to_send_orders,
                    coalesce(sum(case when report_count = 0 and date_transmitted is not null then 1 else 0 end), 0)::int as transmitted_pending_orders,
                    coalesce(sum(case when report_count > 0 then 1 else 0 end), 0)::int as reported_orders,
                    coalesce(sum(case when order_status = 'scheduled' then 1 else 0 end), 0)::int as scheduled_orders,
                    coalesce(sum(case when order_status = 'complete' then 1 else 0 end), 0)::int as completed_orders
                from order_queue;
                """;
            AddReviewQueueFilterParameters(countCommand, normalizedPatient, providerId, labId, fromDate, toDate);

            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalOrders = reader.GetInt32(reader.GetOrdinal("total_orders"));
            readyToSendOrders = reader.GetInt32(reader.GetOrdinal("ready_to_send_orders"));
            transmittedPendingOrders = reader.GetInt32(reader.GetOrdinal("transmitted_pending_orders"));
            reportedOrders = reader.GetInt32(reader.GetOrdinal("reported_orders"));
            scheduledOrders = reader.GetInt32(reader.GetOrdinal("scheduled_orders"));
            completedOrders = reader.GetInt32(reader.GetOrdinal("completed_orders"));
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            with order_queue as (
                select
                    lo.id as order_id,
                    p.canonical_id as patient_id,
                    p.legacy_pid,
                    p.pubpid,
                    trim(concat(p.last_name, ', ', p.first_name)) as patient_display_name,
                    lo.encounter,
                    lo.order_date,
                    lo.provider_id,
                    nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                    lo.lab_id,
                    lp.name as lab_name,
                    lo.code as procedure_code,
                    lo.name as procedure_name,
                    lo.procedure_type,
                    lo.order_priority,
                    lo.order_status,
                    lo.date_transmitted,
                    coalesce((
                        select count(*)
                        from lab_reports lr
                        where lr.order_id = lo.id
                    ), 0)::int as report_count,
                    coalesce((
                        select count(*)
                        from lab_reports lr
                        inner join lab_results lres on lres.report_id = lr.id
                        where lr.order_id = lo.id
                    ), 0)::int as result_count,
                    coalesce((
                        select count(*)
                        from lab_specimens ls
                        where ls.order_id = lo.id
                    ), 0)::int as specimen_count,
                    lo.instructions
                from lab_orders lo
                inner join patients p on p.legacy_pid = lo.pid
                left join staff s on s.id = lo.provider_id
                left join lab_providers lp on lp.id = lo.lab_id
                where (@patientFilter is null
                       or lower(p.canonical_id) = @patientFilter
                       or lower(p.pubpid) = @patientFilter
                       or p.legacy_pid::text = @patientFilter)
                  and (@fromDate is null or lo.order_date >= @fromDate)
                  and (@toDate is null or lo.order_date <= @toDate)
                  and (@providerFilter is null or lo.provider_id = @providerFilter)
                  and (@labFilter is null or lo.lab_id = @labFilter)
            )
            select *
            from order_queue
            where ((@statusFilter = 'all')
               or (@statusFilter = 'ready-to-send' and report_count = 0 and date_transmitted is null)
               or (@statusFilter = 'transmitted-pending' and report_count = 0 and date_transmitted is not null)
               or (@statusFilter = 'reported' and report_count > 0)
               or (@statusFilter = 'scheduled' and coalesce(order_status, '') = 'scheduled')
               or (@statusFilter = 'completed' and coalesce(order_status, '') = 'complete'))
            order by order_date desc, order_id desc, patient_display_name, legacy_pid
            limit @limit;
            """;
        AddReviewQueueFilterParameters(command, normalizedPatient, providerId, labId, fromDate, toDate);
        command.Parameters.AddWithValue("statusFilter", normalizedStatus);
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = safeLimit;

        var orders = new List<ProcedureOrderQueueItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var reportCount = reader.GetInt32(reader.GetOrdinal("report_count"));
                var transmittedAt = ReadNullableDateTime(reader, "date_transmitted");
                var queueState = reportCount > 0
                    ? "reported"
                    : transmittedAt is null
                        ? "ready-to-send"
                        : "transmitted-pending";

                orders.Add(new ProcedureOrderQueueItem(
                    OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                    EncounterId: ReadNullableInt(reader, "encounter"),
                    OrderDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("order_date")).ToString("yyyy-MM-dd"),
                    ProviderId: ReadNullableInt(reader, "provider_id"),
                    ProviderName: ReadNullableString(reader, "provider_name"),
                    LabId: ReadNullableInt(reader, "lab_id"),
                    LabName: ReadNullableString(reader, "lab_name"),
                    ProcedureCode: ReadNullableString(reader, "procedure_code"),
                    ProcedureName: ReadNullableString(reader, "procedure_name"),
                    ProcedureType: ReadNullableString(reader, "procedure_type"),
                    OrderPriority: ReadNullableString(reader, "order_priority"),
                    OrderStatus: ReadNullableString(reader, "order_status"),
                    DateTransmitted: transmittedAt,
                    ReportCount: reportCount,
                    ResultCount: reader.GetInt32(reader.GetOrdinal("result_count")),
                    SpecimenCount: reader.GetInt32(reader.GetOrdinal("specimen_count")),
                    CanTransmit: reportCount == 0 && transmittedAt is null,
                    QueueState: queueState,
                    Instructions: ReadNullableString(reader, "instructions")));
            }
        }

        return new ProcedureOrderQueueResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            StatusFilter: normalizedStatus,
            PatientFilter: normalizedPatient,
            ProviderFilter: providerId,
            LabFilter: labId,
            FromDate: fromDate?.ToString("yyyy-MM-dd"),
            ToDate: toDate?.ToString("yyyy-MM-dd"),
            Limit: safeLimit,
            TotalOrders: totalOrders,
            ReadyToSendOrders: readyToSendOrders,
            TransmittedPendingOrders: transmittedPendingOrders,
            ReportedOrders: reportedOrders,
            ScheduledOrders: scheduledOrders,
            CompletedOrders: completedOrders,
            Orders: orders);
    }

    public async Task<ProcedureLabProviderDirectoryResponse> GetLabProvidersAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        int totalProviders;
        int activeProviders;
        int inactiveProviders;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                select
                    count(*) as total_providers,
                    coalesce(sum(case when active then 1 else 0 end), 0) as active_providers,
                    coalesce(sum(case when not active then 1 else 0 end), 0) as inactive_providers
                from lab_providers;
                """;

            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalProviders = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("total_providers")));
            activeProviders = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("active_providers")));
            inactiveProviders = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("inactive_providers")));
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                lp.id,
                lp.name,
                lp.lab_director_id,
                abo.organization as lab_director_name,
                abo.type as lab_director_type,
                lp.npi,
                coalesce(nullif(trim(lp.protocol), ''), 'DL') as protocol,
                coalesce(nullif(trim(lp.usage), ''), 'D') as usage,
                coalesce(nullif(trim(lp.direction), ''), 'B') as direction,
                lp.send_app_id,
                lp.send_fac_id,
                lp.recv_app_id,
                lp.recv_fac_id,
                lp.remote_host,
                lp.login,
                lp.password,
                lp.orders_path,
                lp.results_path,
                lp.notes,
                lp.active,
                count(distinct lo.id)::int as order_count,
                count(distinct lr.id)::int as report_count,
                count(distinct case when lo.order_date > @baseDate then lo.id end)::int as future_order_count
            from lab_providers lp
            left join lab_provider_address_book abo on abo.id = lp.lab_director_id
            left join lab_orders lo on lo.lab_id = lp.id
            left join lab_reports lr on lr.order_id = lo.id
            where (@includeInactive or lp.active)
            group by lp.id, lp.name, lp.lab_director_id, abo.organization, abo.type, lp.npi, lp.protocol, lp.usage, lp.direction, lp.send_app_id, lp.send_fac_id,
                lp.recv_app_id, lp.recv_fac_id, lp.remote_host, lp.login, lp.password, lp.orders_path, lp.results_path,
                lp.notes, lp.active
            order by lp.name, lp.id;
            """;
        command.Parameters.Add("includeInactive", NpgsqlDbType.Boolean).Value = includeInactive;
        command.Parameters.Add("baseDate", NpgsqlDbType.Date).Value = metadata.BaseDate;

        var providers = new List<ProcedureLabProviderItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                providers.Add(new ProcedureLabProviderItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    LabDirectorId: ReadNullableInt(reader, "lab_director_id"),
                    LabDirectorName: ReadNullableString(reader, "lab_director_name"),
                    LabDirectorType: ReadNullableString(reader, "lab_director_type"),
                    Npi: ReadNullableString(reader, "npi"),
                    Protocol: ReadNullableString(reader, "protocol"),
                    Usage: ReadNullableString(reader, "usage"),
                    Direction: ReadNullableString(reader, "direction"),
                    Notes: ReadNullableString(reader, "notes"),
                    Active: reader.GetBoolean(reader.GetOrdinal("active")),
                    OrderCount: reader.GetInt32(reader.GetOrdinal("order_count")),
                    ReportCount: reader.GetInt32(reader.GetOrdinal("report_count")),
                    FutureOrderCount: reader.GetInt32(reader.GetOrdinal("future_order_count"))));
            }
        }

        return new ProcedureLabProviderDirectoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            IncludeInactive: includeInactive,
            TotalProviders: totalProviders,
            ActiveProviders: activeProviders,
            InactiveProviders: inactiveProviders,
            Providers: providers);
    }

    public async Task<ProcedureOrderCatalogResponse> GetOrderCatalogAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                loc.id,
                loc.parent_id,
                loc.lab_id,
                lp.name as lab_name,
                loc.name,
                loc.code,
                loc.item_type,
                loc.procedure_type_name,
                loc.description,
                loc.specimen,
                loc.standard_code,
                loc.seq,
                loc.active,
                (
                    select count(*)::int
                    from lab_order_catalog child
                    where child.parent_id = loc.id
                ) as child_count
            from lab_order_catalog loc
            left join lab_providers lp on lp.id = loc.lab_id
            order by
                case
                    when loc.parent_id is null then 0
                    when loc.item_type = 'grp' then 1
                    else 2
                end,
                loc.parent_id nulls first,
                loc.seq,
                loc.name,
                loc.id;
            """;

        var items = new List<ProcedureOrderCatalogItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new ProcedureOrderCatalogItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    ParentId: ReadNullableInt(reader, "parent_id"),
                    LabId: ReadNullableInt(reader, "lab_id"),
                    LabName: ReadNullableString(reader, "lab_name"),
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    Code: ReadNullableString(reader, "code"),
                    ItemType: reader.GetString(reader.GetOrdinal("item_type")),
                    ProcedureTypeName: ReadNullableString(reader, "procedure_type_name"),
                    Description: ReadNullableString(reader, "description"),
                    Specimen: ReadNullableString(reader, "specimen"),
                    StandardCode: ReadNullableString(reader, "standard_code"),
                    Sequence: reader.GetInt32(reader.GetOrdinal("seq")),
                    Active: reader.GetBoolean(reader.GetOrdinal("active")),
                    ChildCount: reader.GetInt32(reader.GetOrdinal("child_count"))));
            }
        }

        var groupCount = items.Count(item => string.Equals(item.ItemType, "grp", StringComparison.OrdinalIgnoreCase));
        var orderCount = items.Count(item => string.Equals(item.ItemType, "ord", StringComparison.OrdinalIgnoreCase));
        var labProviderCount = items
            .Where(item => item.LabId is not null && string.Equals(item.ItemType, "ord", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.LabId!.Value)
            .Distinct()
            .Count();

        return new ProcedureOrderCatalogResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            TotalItems: items.Count,
            GroupCount: groupCount,
            OrderCount: orderCount,
            LabProviderCount: labProviderCount,
            Items: items);
    }

    public async Task<ProcedureOrderCatalogMutationResponse?> CreateOrderCatalogItemAsync(
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeOrderCatalogMutation(request);
        if (normalized is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await IsValidOrderCatalogContextAsync(connection, normalized.Value, cancellationToken))
        {
            return null;
        }

        var id = await GetNextIntIdAsync(connection, "lab_order_catalog", "id", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_order_catalog
                (id, parent_id, lab_id, code, name, item_type, procedure_type_name, description, specimen, standard_code, seq, active)
            values
                (@id, @parentId, @labId, @code, @name, @itemType, @procedureTypeName, @description, @specimen, @standardCode, @sequence, @active);
            """;
        command.Parameters.AddWithValue("id", id);
        AddOrderCatalogMutationParameters(command, normalized.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new ProcedureOrderCatalogMutationResponse(
            Id: id,
            Catalog: await GetOrderCatalogAsync(cancellationToken));
    }

    public async Task<ProcedureOrderCatalogMutationResponse?> UpdateOrderCatalogItemAsync(
        int id,
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        var normalized = NormalizeOrderCatalogMutation(request);
        if (normalized is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await IsValidOrderCatalogContextAsync(connection, normalized.Value, cancellationToken))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            update lab_order_catalog
            set parent_id = @parentId,
                lab_id = @labId,
                code = @code,
                name = @name,
                item_type = @itemType,
                procedure_type_name = @procedureTypeName,
                description = @description,
                specimen = @specimen,
                standard_code = @standardCode,
                seq = @sequence,
                active = @active
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", id);
        AddOrderCatalogMutationParameters(command, normalized.Value);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return null;
        }

        return new ProcedureOrderCatalogMutationResponse(
            Id: id,
            Catalog: await GetOrderCatalogAsync(cancellationToken));
    }

    public async Task<bool> DeleteOrderCatalogItemAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from lab_order_catalog loc
            where loc.id = @id
              and not exists (
                  select 1
                  from lab_order_catalog child
                  where child.parent_id = loc.id
              );
            """;
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ProcedureOrderCatalogImportResponse?> ImportOrderCatalogCompendiumAsync(
        ProcedureOrderCatalogImportRequest request,
        CancellationToken cancellationToken)
    {
        var import = NormalizeOrderCatalogImport(request);
        if (import is null || import.Rows.Count == 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await IsValidOrderCatalogImportContextAsync(
                connection,
                transaction,
                import.ParentId,
                import.LabId,
                cancellationToken))
        {
            return null;
        }

        var deactivatedOrderCount = await DeactivateOrderCatalogChildrenAsync(
            connection,
            transaction,
            import.ParentId,
            "ord",
            cancellationToken);

        var importedItems = new List<ProcedureOrderCatalogImportItem>();
        var createdOrderCount = 0;
        var updatedOrderCount = 0;
        var reactivatedOrderCount = 0;
        var createdResultCount = 0;
        var updatedResultCount = 0;
        var reactivatedResultCount = 0;
        var resultCount = 0;
        var resultParentsCleared = new HashSet<int>();

        foreach (var row in import.Rows)
        {
            var orderMutation = await UpsertImportedOrderCatalogItemAsync(
                connection,
                transaction,
                import.ParentId,
                import.LabId,
                row.OrderCode,
                row.OrderName,
                "ord",
                cancellationToken);

            if (orderMutation.Created)
            {
                createdOrderCount += 1;
            }
            else
            {
                updatedOrderCount += 1;
            }

            if (orderMutation.Reactivated)
            {
                reactivatedOrderCount += 1;
            }

            importedItems.Add(new ProcedureOrderCatalogImportItem(
                Id: orderMutation.Id,
                ParentId: import.ParentId,
                Code: row.OrderCode,
                Name: row.OrderName,
                ItemType: "ord",
                Created: orderMutation.Created,
                Reactivated: orderMutation.Reactivated));

            if (resultParentsCleared.Add(orderMutation.Id))
            {
                await DeactivateOrderCatalogChildrenAsync(
                    connection,
                    transaction,
                    orderMutation.Id,
                    "res",
                    cancellationToken);
            }

            if (import.VendorFormat != "pathgroup" || row.ResultCode is null || row.ResultName is null)
            {
                continue;
            }

            var resultMutation = await UpsertImportedOrderCatalogItemAsync(
                connection,
                transaction,
                orderMutation.Id,
                import.LabId,
                row.ResultCode,
                row.ResultName,
                "res",
                cancellationToken);
            resultCount += 1;

            if (resultMutation.Created)
            {
                createdResultCount += 1;
            }
            else
            {
                updatedResultCount += 1;
            }

            if (resultMutation.Reactivated)
            {
                reactivatedResultCount += 1;
            }

            importedItems.Add(new ProcedureOrderCatalogImportItem(
                Id: resultMutation.Id,
                ParentId: orderMutation.Id,
                Code: row.ResultCode,
                Name: row.ResultName,
                ItemType: "res",
                Created: resultMutation.Created,
                Reactivated: resultMutation.Reactivated));
        }

        await transaction.CommitAsync(cancellationToken);

        return new ProcedureOrderCatalogImportResponse(
            VendorFormat: import.VendorFormat,
            ParentId: import.ParentId,
            LabId: import.LabId,
            ImportedOrderCount: import.Rows.Count,
            CreatedOrderCount: createdOrderCount,
            UpdatedOrderCount: updatedOrderCount,
            ReactivatedOrderCount: reactivatedOrderCount,
            DeactivatedOrderCount: deactivatedOrderCount,
            ImportedResultCount: resultCount,
            CreatedResultCount: createdResultCount,
            UpdatedResultCount: updatedResultCount,
            ReactivatedResultCount: reactivatedResultCount,
            ImportedItems: importedItems,
            Catalog: await GetOrderCatalogAsync(cancellationToken));
    }

    public async Task<ProcedureLabProviderMutationResponse?> CreateLabProviderAsync(
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var resolvedProvider = await ResolveLabProviderNameAsync(connection, request, cancellationToken);
        if (resolvedProvider is null)
        {
            return null;
        }

        var id = await GetNextIntIdAsync(connection, "lab_providers", "id", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_providers
                (id, name, lab_director_id, npi, protocol, usage, direction, send_app_id, send_fac_id, recv_app_id, recv_fac_id,
                 remote_host, login, password, orders_path, results_path, notes, active)
            values
                (@id, @name, @labDirectorId, @npi, @protocol, @usage, @direction, @sendAppId, @sendFacId, @recvAppId, @recvFacId,
                 @remoteHost, @login, @password, @ordersPath, @resultsPath, @notes, @active);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", resolvedProvider.Value.Name);
        command.Parameters.Add("labDirectorId", NpgsqlDbType.Integer).Value =
            resolvedProvider.Value.LabDirectorId is { } labDirectorId ? labDirectorId : DBNull.Value;
        command.Parameters.Add("npi", NpgsqlDbType.Text).Value = NormalizeText(request.Npi) is { } npi ? npi : DBNull.Value;
        command.Parameters.AddWithValue("protocol", NormalizeLabProviderProtocol(request.Protocol));
        AddLabProviderConfigurationParameters(command, request);
        command.Parameters.AddWithValue("active", request.Active);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new ProcedureLabProviderMutationResponse(
            Id: id,
            Directory: await GetLabProvidersAsync(includeInactive: true, cancellationToken));
    }

    public async Task<ProcedureLabProviderMutationResponse?> UpdateLabProviderAsync(
        int id,
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var resolvedProvider = await ResolveLabProviderNameAsync(connection, request, cancellationToken);
        if (resolvedProvider is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            update lab_providers
            set name = @name,
                lab_director_id = @labDirectorId,
                npi = @npi,
                protocol = @protocol,
                usage = @usage,
                direction = @direction,
                send_app_id = @sendAppId,
                send_fac_id = @sendFacId,
                recv_app_id = @recvAppId,
                recv_fac_id = @recvFacId,
                remote_host = @remoteHost,
                login = @login,
                password = @password,
                orders_path = @ordersPath,
                results_path = @resultsPath,
                notes = @notes,
                active = @active
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", resolvedProvider.Value.Name);
        command.Parameters.Add("labDirectorId", NpgsqlDbType.Integer).Value =
            resolvedProvider.Value.LabDirectorId is { } labDirectorId ? labDirectorId : DBNull.Value;
        command.Parameters.Add("npi", NpgsqlDbType.Text).Value = NormalizeText(request.Npi) is { } npi ? npi : DBNull.Value;
        command.Parameters.AddWithValue("protocol", NormalizeLabProviderProtocol(request.Protocol));
        AddLabProviderConfigurationParameters(command, request);
        command.Parameters.AddWithValue("active", request.Active);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            return null;
        }

        return new ProcedureLabProviderMutationResponse(
            Id: id,
            Directory: await GetLabProvidersAsync(includeInactive: true, cancellationToken));
    }

    public async Task<bool> DeleteLabProviderAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from lab_providers lp
            where lp.id = @id
              and not exists (
                  select 1
                  from lab_orders lo
                  where lo.lab_id = lp.id
              );
            """;
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ProcedureLabProviderAddressBookResponse> GetLabProviderAddressBookAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, organization, type, active
            from lab_provider_address_book
            where type like 'ord_%'
            order by organization, id;
            """;

        var organizations = new List<ProcedureLabProviderAddressBookItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            organizations.Add(new ProcedureLabProviderAddressBookItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Organization: reader.GetString(reader.GetOrdinal("organization")),
                Type: reader.GetString(reader.GetOrdinal("type")),
                Active: reader.GetBoolean(reader.GetOrdinal("active"))));
        }

        return new ProcedureLabProviderAddressBookResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Organizations: organizations);
    }

    public async Task<ProcedureLabProviderAddressBookMutationResponse?> CreateLabProviderAddressBookOrganizationAsync(
        ProcedureLabProviderAddressBookMutationRequest request,
        CancellationToken cancellationToken)
    {
        var organization = NormalizeText(request.Organization);
        if (organization is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = await GetNextIntIdAsync(connection, "lab_provider_address_book", "id", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_provider_address_book (id, organization, type, active)
            values (@id, @organization, @type, @active);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("organization", organization);
        command.Parameters.AddWithValue("type", NormalizeLabProviderAddressBookType(request.Type));
        command.Parameters.AddWithValue("active", request.Active);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new ProcedureLabProviderAddressBookMutationResponse(
            Id: id,
            AddressBook: await GetLabProviderAddressBookAsync(cancellationToken));
    }

    public async Task<bool> DeleteLabProviderAddressBookOrganizationAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from lab_provider_address_book
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ProcedureMutationResponse?> CreateOrderAsync(
        ProcedureOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Priority)
            || string.IsNullOrWhiteSpace(request.Status)
            || string.IsNullOrWhiteSpace(request.ProcedureCode)
            || string.IsNullOrWhiteSpace(request.ProcedureName)
            || string.IsNullOrWhiteSpace(request.ProcedureType)
            || string.IsNullOrWhiteSpace(request.Diagnosis)
            || request.EncounterId <= 0
            || !TryReadDate(request.DateOrdered, out var orderDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, request.EncounterId, cancellationToken);
        if (encounter is null)
        {
            return null;
        }

        if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        var id = await GetNextIntIdAsync(connection, "lab_orders", "id", cancellationToken);
        var providerId = request.ProviderId ?? encounter.ProviderId;
        var labId = request.LabId;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_orders
                (id, patient_id, pid, encounter, provider_id, lab_id, order_date, code, name,
                 diagnosis, order_priority, procedure_type, instructions, order_status)
            values
                (@id, @patientId, @pid, @encounter, @providerId, @labId, @orderDate, @code, @name,
                 @diagnosis, @priority, @procedureType, @instructions, @status);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("encounter", encounter.Encounter);
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = providerId is null ? DBNull.Value : providerId.Value;
        command.Parameters.Add("labId", NpgsqlDbType.Integer).Value = labId is null ? DBNull.Value : labId.Value;
        command.Parameters.Add("orderDate", NpgsqlDbType.Date).Value = orderDate;
        command.Parameters.AddWithValue("code", request.ProcedureCode.Trim());
        command.Parameters.AddWithValue("name", request.ProcedureName.Trim());
        command.Parameters.AddWithValue("diagnosis", request.Diagnosis.Trim());
        command.Parameters.AddWithValue("priority", request.Priority.Trim());
        command.Parameters.AddWithValue("procedureType", request.ProcedureType.Trim());
        command.Parameters.AddWithValue("instructions", request.Instructions?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("status", request.Status.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(id, detail);
    }

    public async Task<ProcedureMutationResponse?> UpdateOrderStatusAsync(
        int orderId,
        ProcedureOrderStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(request.Status))
        {
            return null;
        }

        await using (var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var order = await GetOrderMutationContextAsync(lockConnection, orderId, cancellationToken);
            if (order is null) return null;
            if (await IsEncounterLockedAsync(lockConnection, order.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update lab_orders
                set order_status = @status
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.AddWithValue("status", request.Status.Trim());
            var result = await command.ExecuteScalarAsync(cancellationToken);
            patientId = result as string;
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(orderId, detail);
    }

    public async Task<ProcedureMutationResponse?> TransmitOrderAsync(
        int orderId,
        ProcedureOrderTransmitRequest request,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0)
        {
            return null;
        }

        await using (var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var order = await GetOrderMutationContextAsync(lockConnection, orderId, cancellationToken);
            if (order is null) return null;
            if (await IsEncounterLockedAsync(lockConnection, order.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }
        }

        var transmittedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        if (!string.IsNullOrWhiteSpace(request.TransmittedAt))
        {
            if (!TryReadDateTime(request.TransmittedAt, out transmittedAt))
            {
                return null;
            }

            transmittedAt = DateTime.SpecifyKind(transmittedAt, DateTimeKind.Unspecified);
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update lab_orders lo
                set date_transmitted = @transmittedAt
                where lo.id = @id
                  and lo.date_transmitted is null
                  and not exists (
                      select 1
                      from lab_reports lr
                      where lr.order_id = lo.id
                  )
                returning lo.patient_id;
                """;
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.Add("transmittedAt", NpgsqlDbType.Timestamp).Value = transmittedAt;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            patientId = result as string;
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(orderId, detail);
    }

    public async Task<ProcedureMutationResponse?> UpdateOrderAsync(
        int orderId,
        ProcedureOrderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0
            || string.IsNullOrWhiteSpace(request.Priority)
            || string.IsNullOrWhiteSpace(request.Status)
            || string.IsNullOrWhiteSpace(request.ProcedureCode)
            || string.IsNullOrWhiteSpace(request.ProcedureName)
            || string.IsNullOrWhiteSpace(request.ProcedureType)
            || string.IsNullOrWhiteSpace(request.Diagnosis)
            || !TryReadDate(request.DateOrdered, out var orderDate))
        {
            return null;
        }

        await using (var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var order = await GetOrderMutationContextAsync(lockConnection, orderId, cancellationToken);
            if (order is null) return null;
            if (await IsEncounterLockedAsync(lockConnection, order.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update lab_orders
                set order_date = @orderDate,
                    code = @code,
                    name = @name,
                    diagnosis = @diagnosis,
                    order_priority = @priority,
                    procedure_type = @procedureType,
                    instructions = @instructions,
                    order_status = @status
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", orderId);
            command.Parameters.Add("orderDate", NpgsqlDbType.Date).Value = orderDate;
            command.Parameters.AddWithValue("code", request.ProcedureCode.Trim());
            command.Parameters.AddWithValue("name", request.ProcedureName.Trim());
            command.Parameters.AddWithValue("diagnosis", request.Diagnosis.Trim());
            command.Parameters.AddWithValue("priority", request.Priority.Trim());
            command.Parameters.AddWithValue("procedureType", request.ProcedureType.Trim());
            command.Parameters.AddWithValue("instructions", request.Instructions?.Trim() ?? string.Empty);
            command.Parameters.AddWithValue("status", request.Status.Trim());
            var result = await command.ExecuteScalarAsync(cancellationToken);
            patientId = result as string;
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(orderId, detail);
    }

    public async Task<ProcedureMutationResponse?> CreateReportAsync(
        ProcedureReportCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrderId <= 0
            || string.IsNullOrWhiteSpace(request.ReportStatus)
            || string.IsNullOrWhiteSpace(request.ReviewStatus)
            || string.IsNullOrWhiteSpace(request.SpecimenNumber)
            || !TryReadDateTime(request.DateCollected, out var collectedDate)
            || !TryReadDateTime(request.DateReport, out var reportDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var order = await GetOrderMutationContextAsync(connection, request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var id = await GetNextIntIdAsync(connection, "lab_reports", "id", cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_reports
                (id, order_id, date_collected, report_date, specimen_number, status, review_status, reviewed_by, reviewed_at, notes)
            values
                (@id, @orderId, @dateCollected, @reportDate, @specimenNumber, @status, 'received', null, null, @notes);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("orderId", order.Id);
        command.Parameters.Add("dateCollected", NpgsqlDbType.Timestamp).Value = collectedDate;
        command.Parameters.Add("reportDate", NpgsqlDbType.Timestamp).Value = reportDate;
        command.Parameters.AddWithValue("specimenNumber", request.SpecimenNumber.Trim());
        command.Parameters.AddWithValue("status", request.ReportStatus.Trim());
        command.Parameters.AddWithValue("notes", request.Notes?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(order.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(id, detail);
    }

    public async Task<ProcedureMutationResponse?> UpdateReportAsync(
        int reportId,
        ProcedureReportUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || string.IsNullOrWhiteSpace(request.ReportStatus)
            || string.IsNullOrWhiteSpace(request.ReviewStatus)
            || string.IsNullOrWhiteSpace(request.SpecimenNumber)
            || !TryReadDateTime(request.DateCollected, out var collectedDate)
            || !TryReadDateTime(request.DateReport, out var reportDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var report = await GetReportMutationContextAsync(connection, reportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            update lab_reports
            set date_collected = @dateCollected,
                report_date = @reportDate,
                specimen_number = @specimenNumber,
                status = @status,
                notes = @notes
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", report.Id);
        command.Parameters.Add("dateCollected", NpgsqlDbType.Timestamp).Value = collectedDate;
        command.Parameters.Add("reportDate", NpgsqlDbType.Timestamp).Value = reportDate;
        command.Parameters.AddWithValue("specimenNumber", request.SpecimenNumber.Trim());
        command.Parameters.AddWithValue("status", request.ReportStatus.Trim());
        command.Parameters.AddWithValue("notes", request.Notes?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(report.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(report.Id, detail);
    }

    public async Task<ProcedureMutationResponse?> SignReportAsync(
        int reportId,
        ProcedureReportSignRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || request.ExpectedReviewVersion <= 0
            || !HasBoundedReason(request.Reason)
            || string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        return await TransitionReportReviewAsync(
            reportId,
            request.ExpectedReviewVersion,
            "reviewed",
            "signed",
            actor.Trim(),
            actor.Trim(),
            request.Reason.Trim(),
            ["received", "assigned"],
            cancellationToken);
    }

    public async Task<ProcedureMutationResponse?> AssignReportReviewerAsync(
        int reportId,
        ProcedureReportReviewAssignmentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || string.IsNullOrWhiteSpace(request.AssignedTo)
            || request.ExpectedReviewVersion <= 0
            || !HasBoundedReason(request.Reason)
            || string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        return await TransitionReportReviewAsync(
            reportId,
            request.ExpectedReviewVersion,
            "assigned",
            "assigned",
            request.AssignedTo.Trim(),
            actor.Trim(),
            request.Reason.Trim(),
            ["received", "assigned"],
            cancellationToken);
    }

    public async Task<ProcedureMutationResponse?> ReopenReportReviewAsync(
        int reportId,
        ProcedureReportReviewDecisionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || request.ExpectedReviewVersion <= 0
            || !HasBoundedReason(request.Reason)
            || string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        return await TransitionReportReviewAsync(
            reportId,
            request.ExpectedReviewVersion,
            "received",
            "reopened",
            null,
            actor.Trim(),
            request.Reason.Trim(),
            ["reviewed", "denied"],
            cancellationToken);
    }

    public async Task<ProcedureMutationResponse?> DenyReportReviewAsync(
        int reportId,
        ProcedureReportReviewDecisionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || request.ExpectedReviewVersion <= 0
            || !HasBoundedReason(request.Reason)
            || string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        return await TransitionReportReviewAsync(
            reportId,
            request.ExpectedReviewVersion,
            "denied",
            "denied",
            actor.Trim(),
            actor.Trim(),
            request.Reason.Trim(),
            ["received", "assigned"],
            cancellationToken);
    }

    public async Task<ProcedureReportBulkSignResponse?> BulkSignReportsAsync(
        ProcedureReportBulkSignRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var requested = (request.Reports ?? Array.Empty<ProcedureReportBulkSignItem>())
            .Where(item => item.ReportId > 0 && item.ExpectedReviewVersion > 0)
            .GroupBy(item => item.ReportId)
            .Select(group => group.Single())
            .OrderBy(item => item.ReportId)
            .ToArray();

        if (requested.Length == 0
            || requested.Length != (request.Reports?.Count ?? 0)
            || !HasBoundedReason(request.Reason)
            || string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var contexts = await GetReportReviewContextsAsync(
            connection,
            transaction,
            requested.Select(item => item.ReportId).ToArray(),
            cancellationToken);
        if (contexts.Count != requested.Length)
        {
            throw new ArgumentException("One or more selected reports no longer exist.");
        }

        foreach (var item in requested)
        {
            var context = contexts[item.ReportId];
            if (context.ReviewVersion != item.ExpectedReviewVersion)
            {
                throw new ProcedureReportReviewConflictException(
                    item.ExpectedReviewVersion,
                    context.ReviewVersion,
                    context.ReviewStatus);
            }

            if (context.ReviewStatus is not ("received" or "assigned"))
            {
                throw new ArgumentException($"Report {item.ReportId} cannot be signed from {context.ReviewStatus}.");
            }

            await ApplyReviewTransitionAsync(
                connection,
                transaction,
                context,
                "reviewed",
                "signed",
                actor.Trim(),
                actor.Trim(),
                request.Reason.Trim(),
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return new ProcedureReportBulkSignResponse(
            RequestedCount: requested.Length,
            SignedCount: requested.Length,
            SignedReportIds: requested.Select(item => item.ReportId).ToArray(),
            ReviewedBy: actor.Trim(),
            ReviewedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));
    }

    public async Task<ProcedureMutationResponse?> CreateSpecimenAsync(
        ProcedureSpecimenCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrderId <= 0
            || (string.IsNullOrWhiteSpace(request.SpecimenIdentifier) && string.IsNullOrWhiteSpace(request.AccessionIdentifier))
            || !TryReadDateTime(request.CollectedDate, out var collectedDate)
            || request.VolumeValue < 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var order = await GetOrderMutationContextAsync(connection, request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var id = await GetNextIntIdAsync(connection, "lab_specimens", "id", cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_specimens
                (id, order_id, specimen_identifier, accession_identifier, specimen_type_code, specimen_type,
                 collection_method_code, collection_method, specimen_location_code, specimen_location,
                 collected_date, volume_value, volume_unit, condition_code, specimen_condition, comments)
            values
                (@id, @orderId, @specimenIdentifier, @accessionIdentifier, @specimenTypeCode, @specimenType,
                 @collectionMethodCode, @collectionMethod, @specimenLocationCode, @specimenLocation,
                 @collectedDate, @volumeValue, @volumeUnit, @conditionCode, @specimenCondition, @comments);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("orderId", order.Id);
        command.Parameters.AddWithValue("specimenIdentifier", NormalizeText(request.SpecimenIdentifier) ?? string.Empty);
        command.Parameters.AddWithValue("accessionIdentifier", NormalizeText(request.AccessionIdentifier) ?? string.Empty);
        command.Parameters.AddWithValue("specimenTypeCode", NormalizeText(request.SpecimenTypeCode) ?? string.Empty);
        command.Parameters.AddWithValue("specimenType", NormalizeText(request.SpecimenType) ?? string.Empty);
        command.Parameters.AddWithValue("collectionMethodCode", NormalizeText(request.CollectionMethodCode) ?? string.Empty);
        command.Parameters.AddWithValue("collectionMethod", NormalizeText(request.CollectionMethod) ?? string.Empty);
        command.Parameters.AddWithValue("specimenLocationCode", NormalizeText(request.SpecimenLocationCode) ?? string.Empty);
        command.Parameters.AddWithValue("specimenLocation", NormalizeText(request.SpecimenLocation) ?? string.Empty);
        command.Parameters.Add("collectedDate", NpgsqlDbType.Timestamp).Value = collectedDate;
        command.Parameters.Add("volumeValue", NpgsqlDbType.Numeric).Value = request.VolumeValue is null ? DBNull.Value : request.VolumeValue.Value;
        command.Parameters.AddWithValue("volumeUnit", NormalizeText(request.VolumeUnit) ?? string.Empty);
        command.Parameters.AddWithValue("conditionCode", NormalizeText(request.ConditionCode) ?? string.Empty);
        command.Parameters.AddWithValue("specimenCondition", NormalizeText(request.SpecimenCondition) ?? string.Empty);
        command.Parameters.AddWithValue("comments", NormalizeText(request.Comments) ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(order.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(id, detail);
    }

    public async Task<ProcedureMutationResponse?> CreateResultAsync(
        ProcedureResultCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ReportId <= 0
            || string.IsNullOrWhiteSpace(request.ResultCode)
            || string.IsNullOrWhiteSpace(request.ResultText)
            || string.IsNullOrWhiteSpace(request.Result)
            || string.IsNullOrWhiteSpace(request.Status)
            || !TryReadDateTime(request.DateTime, out var resultDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var report = await GetReportMutationContextAsync(connection, request.ReportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        var id = await GetNextIntIdAsync(connection, "lab_results", "id", cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_results
                (id, report_id, code, text, units, result, range, abnormal, result_date, result_status)
            values
                (@id, @reportId, @code, @text, @units, @result, @range, @abnormal, @resultDate, @status);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("reportId", report.Id);
        command.Parameters.AddWithValue("code", request.ResultCode.Trim());
        command.Parameters.AddWithValue("text", request.ResultText.Trim());
        command.Parameters.AddWithValue("units", request.Units?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("result", request.Result.Trim());
        command.Parameters.AddWithValue("range", request.Range?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("abnormal", request.Abnormal?.Trim() ?? string.Empty);
        command.Parameters.Add("resultDate", NpgsqlDbType.Timestamp).Value = resultDate;
        command.Parameters.AddWithValue("status", request.Status.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(report.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(id, detail);
    }

    public async Task<ProcedureMutationResponse?> UpdateResultAsync(
        int resultId,
        ProcedureResultUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (resultId <= 0
            || string.IsNullOrWhiteSpace(request.ResultCode)
            || string.IsNullOrWhiteSpace(request.ResultText)
            || string.IsNullOrWhiteSpace(request.Result)
            || string.IsNullOrWhiteSpace(request.Status)
            || !TryReadDateTime(request.DateTime, out var resultDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureProcedureResultVersionTableAsync(connection, cancellationToken);
        var result = await GetResultMutationContextAsync(connection, resultId, cancellationToken);
        if (result is null)
        {
            return null;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SnapshotCurrentProcedureResultVersionAsync(connection, transaction, result.Id, cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update lab_results
            set code = @code,
                text = @text,
                units = @units,
                result = @result,
                range = @range,
                abnormal = @abnormal,
                result_date = @resultDate,
                result_status = @status
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", result.Id);
        command.Parameters.AddWithValue("code", request.ResultCode.Trim());
        command.Parameters.AddWithValue("text", request.ResultText.Trim());
        command.Parameters.AddWithValue("units", request.Units?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("result", request.Result.Trim());
        command.Parameters.AddWithValue("range", request.Range?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("abnormal", request.Abnormal?.Trim() ?? string.Empty);
        command.Parameters.Add("resultDate", NpgsqlDbType.Timestamp).Value = resultDate;
        command.Parameters.AddWithValue("status", request.Status.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await GetForPatientAsync(result.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(result.Id, detail);
    }

    public async Task<bool> DeleteOrderCascadeAsync(int orderId, CancellationToken cancellationToken)
    {
        if (orderId <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var order = await GetOrderMutationContextAsync(connection, orderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var specimenCommand = connection.CreateCommand())
        {
            specimenCommand.Transaction = transaction;
            specimenCommand.CommandText = "delete from lab_specimens where order_id = @orderId;";
            specimenCommand.Parameters.AddWithValue("orderId", order.Id);
            await specimenCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var resultCommand = connection.CreateCommand())
        {
            resultCommand.Transaction = transaction;
            resultCommand.CommandText = """
                delete from lab_results
                where report_id in (
                    select id
                    from lab_reports
                    where order_id = @orderId
                );
                """;
            resultCommand.Parameters.AddWithValue("orderId", order.Id);
            await resultCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var reportCommand = connection.CreateCommand())
        {
            reportCommand.Transaction = transaction;
            reportCommand.CommandText = "delete from lab_reports where order_id = @orderId;";
            reportCommand.Parameters.AddWithValue("orderId", order.Id);
            await reportCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var orderCommand = connection.CreateCommand())
        {
            orderCommand.Transaction = transaction;
            orderCommand.CommandText = "delete from lab_orders where id = @orderId;";
            orderCommand.Parameters.AddWithValue("orderId", order.Id);
            await orderCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<ProcedurePatient?> GetPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");

        return new ProcedurePatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<IReadOnlyList<ProcedureOrderRow>> GetOrdersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                lo.id,
                lo.encounter,
                nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                lo.order_date,
                lo.order_priority,
                lo.code,
                lo.name,
                lo.procedure_type,
                lo.diagnosis,
                lo.instructions,
                lo.order_status
            from lab_orders lo
            left join staff s on s.id = lo.provider_id
            where lo.pid = @pid
            order by lo.order_date desc, lo.id desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var rows = new List<ProcedureOrderRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            rows.Add(new ProcedureOrderRow(
                Id: id,
                Order: new ProcedureOrderItem(
                    Id: id,
                    Encounter: ReadNullableInt(reader, "encounter"),
                    ProviderName: ReadNullableString(reader, "provider_name"),
                    OrderDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("order_date")).ToString("yyyy-MM-dd"),
                    OrderPriority: ReadNullableString(reader, "order_priority"),
                    Code: ReadNullableString(reader, "code"),
                    Name: ReadNullableString(reader, "name"),
                    ProcedureType: ReadNullableString(reader, "procedure_type"),
                    Diagnosis: ReadNullableString(reader, "diagnosis"),
                    Instructions: ReadNullableString(reader, "instructions"),
                    OrderStatus: ReadNullableString(reader, "order_status"),
                    Specimens: [],
                    Reports: [])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProcedureSpecimenRow>> GetSpecimensAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_id, specimen_identifier, accession_identifier, specimen_type_code, specimen_type,
                   collection_method_code, collection_method, specimen_location_code, specimen_location,
                   collected_date, volume_value, volume_unit, condition_code, specimen_condition, comments
            from lab_specimens
            where order_id = any(@orderIds)
            order by collected_date desc, id desc;
            """;
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        var rows = new List<ProcedureSpecimenRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProcedureSpecimenRow(
                OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                Specimen: new ProcedureSpecimenItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    SpecimenIdentifier: ReadNullableString(reader, "specimen_identifier"),
                    AccessionIdentifier: ReadNullableString(reader, "accession_identifier"),
                    SpecimenTypeCode: ReadNullableString(reader, "specimen_type_code"),
                    SpecimenType: ReadNullableString(reader, "specimen_type"),
                    CollectionMethodCode: ReadNullableString(reader, "collection_method_code"),
                    CollectionMethod: ReadNullableString(reader, "collection_method"),
                    SpecimenLocationCode: ReadNullableString(reader, "specimen_location_code"),
                    SpecimenLocation: ReadNullableString(reader, "specimen_location"),
                    CollectedDate: reader.GetDateTime(reader.GetOrdinal("collected_date")).ToString("yyyy-MM-dd HH:mm"),
                    VolumeValue: ReadNullableDecimal(reader, "volume_value"),
                    VolumeUnit: ReadNullableString(reader, "volume_unit"),
                    ConditionCode: ReadNullableString(reader, "condition_code"),
                    SpecimenCondition: ReadNullableString(reader, "specimen_condition"),
                    Comments: ReadNullableString(reader, "comments"))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProcedureReportRow>> GetReportsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_id, date_collected, report_date, specimen_number, status, review_status, reviewed_by, reviewed_at,
                   review_version,
                   (select count(*) from lab_report_review_events lrrh where lrrh.report_id = lab_reports.id) as review_history_count,
                   notes
            from lab_reports
            where order_id = any(@orderIds)
            order by report_date desc, id desc;
            """;
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        var rows = new List<ProcedureReportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            rows.Add(new ProcedureReportRow(
                Id: id,
                OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                Report: new ProcedureReportItem(
                    Id: id,
                    DateCollected: reader.GetDateTime(reader.GetOrdinal("date_collected")).ToString("yyyy-MM-dd HH:mm"),
                    ReportDate: reader.GetDateTime(reader.GetOrdinal("report_date")).ToString("yyyy-MM-dd HH:mm"),
                    SpecimenNumber: ReadNullableString(reader, "specimen_number"),
                    Status: ReadNullableString(reader, "status"),
                    ReviewStatus: ReadNullableString(reader, "review_status"),
                    ReviewedBy: ReadNullableString(reader, "reviewed_by"),
                    ReviewedAt: ReadNullableDateTime(reader, "reviewed_at"),
                    ReviewVersion: reader.GetInt32(reader.GetOrdinal("review_version")),
                    ReviewHistoryCount: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("review_history_count"))),
                    Notes: ReadNullableString(reader, "notes"),
                    Results: [])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProcedureResultRow>> GetResultsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> reportIds,
        CancellationToken cancellationToken)
    {
        if (reportIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, report_id, code, text, units, result, range, abnormal, result_date, result_status,
                   (select count(*) from procedure_result_versions v where v.result_id = lab_results.id) as prior_version_count
            from lab_results
            where report_id = any(@reportIds)
            order by id;
            """;
        command.Parameters.AddWithValue("reportIds", reportIds.ToArray());

        var rows = new List<ProcedureResultRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var baseRows = new List<ProcedureResultBaseRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            baseRows.Add(new ProcedureResultBaseRow(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                ReportId: reader.GetInt32(reader.GetOrdinal("report_id")),
                Code: ReadNullableString(reader, "code"),
                Text: ReadNullableString(reader, "text"),
                Units: ReadNullableString(reader, "units"),
                Result: ReadNullableString(reader, "result"),
                Range: ReadNullableString(reader, "range"),
                Abnormal: ReadNullableString(reader, "abnormal"),
                ResultDate: reader.GetDateTime(reader.GetOrdinal("result_date")),
                ResultStatus: ReadNullableString(reader, "result_status"),
                PriorVersionCount: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("prior_version_count")))));
        }
        await reader.DisposeAsync();

        var historyByResult = await GetProcedureResultVersionHistoryAsync(connection, baseRows.Select(row => row.Id).ToArray(), cancellationToken);
        foreach (var row in baseRows)
        {
            var currentVersion = row.PriorVersionCount + 1;
            var currentHistory = new ProcedureResultVersionItem(
                Version: currentVersion,
                VersionLabel: $"Version {currentVersion}",
                VersionStatus: "Current version",
                CapturedAt: row.ResultDate.ToString("yyyy-MM-dd HH:mm"),
                Code: row.Code,
                Text: row.Text,
                Units: row.Units,
                Result: row.Result,
                Range: row.Range,
                Abnormal: row.Abnormal,
                ResultDate: row.ResultDate.ToString("yyyy-MM-dd HH:mm"),
                ResultStatus: row.ResultStatus);
            var versionHistory = new List<ProcedureResultVersionItem> { currentHistory };
            versionHistory.AddRange(historyByResult.GetValueOrDefault(row.Id, []));
            rows.Add(new ProcedureResultRow(
                ReportId: row.ReportId,
                Result: new ProcedureResultItem(
                    Id: row.Id,
                    Code: row.Code,
                    Text: row.Text,
                    Units: row.Units,
                    Result: row.Result,
                    Range: row.Range,
                    Abnormal: row.Abnormal,
                    ResultDate: row.ResultDate.ToString("yyyy-MM-dd HH:mm"),
                    ResultStatus: row.ResultStatus,
                    CurrentVersion: currentVersion,
                    VersionLabel: $"Version {currentVersion}",
                    VersionHistoryCount: currentVersion,
                    HasPriorVersions: row.PriorVersionCount > 0,
                    VersionHistory: versionHistory)));
        }

        return rows;
    }

    private static async Task EnsureProcedureResultVersionTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists procedure_result_versions (
              id bigserial primary key,
              result_id integer not null references lab_results(id) on delete cascade,
              version_no integer not null,
              captured_at timestamp not null,
              code text,
              text text,
              units text,
              result text,
              range text,
              abnormal text,
              result_date timestamp,
              result_status text,
              unique (result_id, version_no)
            );

            create index if not exists idx_procedure_result_versions_result
              on procedure_result_versions (result_id, version_no desc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SnapshotCurrentProcedureResultVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int resultId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into procedure_result_versions (
              result_id, version_no, captured_at, code, text, units, result, range, abnormal, result_date, result_status
            )
            select
              lr.id,
              coalesce((select max(v.version_no) from procedure_result_versions v where v.result_id = lr.id), 0) + 1,
              current_timestamp,
              lr.code,
              lr.text,
              lr.units,
              lr.result,
              lr.range,
              lr.abnormal,
              lr.result_date,
              lr.result_status
            from lab_results lr
            where lr.id = @resultId;
            """;
        command.Parameters.AddWithValue("resultId", resultId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<int, List<ProcedureResultVersionItem>>> GetProcedureResultVersionHistoryAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> resultIds,
        CancellationToken cancellationToken)
    {
        if (resultIds.Count == 0)
        {
            return new Dictionary<int, List<ProcedureResultVersionItem>>();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select result_id, version_no, captured_at, code, text, units, result, range, abnormal, result_date, result_status
            from procedure_result_versions
            where result_id = any(@resultIds)
            order by result_id, version_no desc;
            """;
        command.Parameters.AddWithValue("resultIds", resultIds.ToArray());

        var rows = new Dictionary<int, List<ProcedureResultVersionItem>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var resultId = reader.GetInt32(reader.GetOrdinal("result_id"));
            var version = reader.GetInt32(reader.GetOrdinal("version_no"));
            var resultRows = rows.GetValueOrDefault(resultId) ?? [];
            var resultDate = reader.IsDBNull(reader.GetOrdinal("result_date"))
                ? string.Empty
                : reader.GetDateTime(reader.GetOrdinal("result_date")).ToString("yyyy-MM-dd HH:mm");
            resultRows.Add(new ProcedureResultVersionItem(
                Version: version,
                VersionLabel: $"Version {version}",
                VersionStatus: "Prior version",
                CapturedAt: reader.GetDateTime(reader.GetOrdinal("captured_at")).ToString("yyyy-MM-dd HH:mm"),
                Code: ReadNullableString(reader, "code"),
                Text: ReadNullableString(reader, "text"),
                Units: ReadNullableString(reader, "units"),
                Result: ReadNullableString(reader, "result"),
                Range: ReadNullableString(reader, "range"),
                Abnormal: ReadNullableString(reader, "abnormal"),
                ResultDate: resultDate,
                ResultStatus: ReadNullableString(reader, "result_status")));
            rows[resultId] = resultRows;
        }

        return rows;
    }

    private async Task<ProcedureMutationResponse?> TransitionReportReviewAsync(
        int reportId,
        int expectedVersion,
        string targetStatus,
        string action,
        string? assignedTo,
        string actor,
        string reason,
        IReadOnlyCollection<string> allowedStatuses,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var contexts = await GetReportReviewContextsAsync(connection, transaction, [reportId], cancellationToken);
        if (!contexts.TryGetValue(reportId, out var context))
        {
            return null;
        }

        if (context.ReviewVersion != expectedVersion)
        {
            throw new ProcedureReportReviewConflictException(expectedVersion, context.ReviewVersion, context.ReviewStatus);
        }

        if (!allowedStatuses.Contains(context.ReviewStatus, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Report {reportId} cannot transition from {context.ReviewStatus} to {targetStatus}.");
        }

        await ApplyReviewTransitionAsync(
            connection,
            transaction,
            context,
            targetStatus,
            action,
            assignedTo,
            actor,
            reason,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await GetForPatientAsync(context.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(reportId, detail);
    }

    private static async Task<Dictionary<int, ProcedureReportReviewContext>> GetReportReviewContextsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<int> reportIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select lr.id, lo.patient_id, coalesce(lr.review_status, 'received') as review_status, lr.review_version
            from lab_reports lr
            inner join lab_orders lo on lo.id = lr.order_id
            where lr.id = any(@reportIds)
            order by lr.id
            for update;
            """;
        command.Parameters.Add("reportIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = reportIds.ToArray();

        var contexts = new Dictionary<int, ProcedureReportReviewContext>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            contexts[id] = new ProcedureReportReviewContext(
                id,
                reader.GetString(reader.GetOrdinal("patient_id")),
                reader.GetString(reader.GetOrdinal("review_status")),
                reader.GetInt32(reader.GetOrdinal("review_version")));
        }
        return contexts;
    }

    private static async Task ApplyReviewTransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProcedureReportReviewContext context,
        string targetStatus,
        string action,
        string? assignedTo,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        var occurredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var resultingVersion = context.ReviewVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update lab_reports
                set review_status = @reviewStatus,
                    reviewed_by = @reviewedBy,
                    reviewed_at = @reviewedAt,
                    review_version = @reviewVersion
                where id = @id and review_version = @expectedVersion;
                """;
            update.Parameters.AddWithValue("id", context.Id);
            update.Parameters.AddWithValue("reviewStatus", targetStatus);
            update.Parameters.AddWithValue("reviewedBy", (object?)assignedTo ?? DBNull.Value);
            update.Parameters.AddWithValue("reviewedAt", assignedTo is null ? DBNull.Value : occurredAt);
            update.Parameters.AddWithValue("reviewVersion", resultingVersion);
            update.Parameters.AddWithValue("expectedVersion", context.ReviewVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ProcedureReportReviewConflictException(context.ReviewVersion, context.ReviewVersion, context.ReviewStatus);
            }
        }

        await using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = """
            insert into lab_report_review_events
                (report_id, action, previous_status, current_status, assigned_to, actor, reason, expected_version, resulting_version, occurred_at)
            values
                (@reportId, @action, @previousStatus, @currentStatus, @assignedTo, @actor, @reason, @expectedVersion, @resultingVersion, @occurredAt);
            """;
        history.Parameters.AddWithValue("reportId", context.Id);
        history.Parameters.AddWithValue("action", action);
        history.Parameters.AddWithValue("previousStatus", context.ReviewStatus);
        history.Parameters.AddWithValue("currentStatus", targetStatus);
        history.Parameters.AddWithValue("assignedTo", (object?)assignedTo ?? DBNull.Value);
        history.Parameters.AddWithValue("actor", actor);
        history.Parameters.AddWithValue("reason", reason);
        history.Parameters.AddWithValue("expectedVersion", context.ReviewVersion);
        history.Parameters.AddWithValue("resultingVersion", resultingVersion);
        history.Parameters.AddWithValue("occurredAt", occurredAt);
        await history.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool HasBoundedReason(string? reason)
    {
        var length = reason?.Trim().Length ?? 0;
        return length is >= 1 and <= 500;
    }

    private static async Task<ProcedureEncounterMutationContext?> GetEncounterForPatientAsync(
        NpgsqlConnection connection,
        int legacyPid,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select encounter, provider_id
            from encounters
            where pid = @pid and encounter = @encounter
            limit 1;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounter", encounter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProcedureEncounterMutationContext(
            Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
            ProviderId: ReadNullableInt(reader, "provider_id"));
    }

    private static async Task<bool> IsEncounterLockedAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from encounter_signatures where encounter = @encounter and is_lock);";
        command.Parameters.AddWithValue("encounter", encounter);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<ProcedureOrderMutationContext?> GetOrderMutationContextAsync(
        NpgsqlConnection connection,
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, patient_id, pid, encounter
            from lab_orders
            where id = @id
            limit 1;
            """;
        command.Parameters.AddWithValue("id", orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProcedureOrderMutationContext(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            Encounter: reader.GetInt32(reader.GetOrdinal("encounter")));
    }

    private static async Task<ProcedureReportMutationContext?> GetReportMutationContextAsync(
        NpgsqlConnection connection,
        int reportId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select lr.id, lo.patient_id, lo.pid
            from lab_reports lr
            inner join lab_orders lo on lo.id = lr.order_id
            where lr.id = @id
            limit 1;
            """;
        command.Parameters.AddWithValue("id", reportId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProcedureReportMutationContext(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")));
    }

    private static async Task<ProcedureResultMutationContext?> GetResultMutationContextAsync(
        NpgsqlConnection connection,
        int resultId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select lrs.id, lo.patient_id, lo.pid
            from lab_results lrs
            inner join lab_reports lr on lr.id = lrs.report_id
            inner join lab_orders lo on lo.id = lr.order_id
            where lrs.id = @id
            limit 1;
            """;
        command.Parameters.AddWithValue("id", resultId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ProcedureResultMutationContext(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")));
    }

    private static async Task<int> GetNextIntIdAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        return await GetNextIntIdAsync(connection, tableName, columnName, null, cancellationToken);
    }

    private static async Task<int> GetNextIntIdAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (tableName is not ("lab_orders" or "lab_reports" or "lab_results" or "lab_specimens" or "lab_providers" or "lab_provider_address_book" or "lab_order_catalog")
            || columnName != "id")
        {
            throw new ArgumentException("Unsupported procedure id source.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select coalesce(max({columnName}), 0) + 1 from {tableName};";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static bool TryReadDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParse(value, out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return false;
    }

    private static bool TryReadDateTime(string value, out DateTime dateTime)
    {
        return DateTime.TryParse(value, out dateTime);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeLabProviderProtocol(string? protocol)
    {
        return NormalizeText(protocol)?.ToUpperInvariant() ?? "DL";
    }

    private static string NormalizeLabProviderUsage(string? usage)
    {
        return NormalizeText(usage)?.ToUpperInvariant() switch
        {
            "P" => "P",
            "T" => "T",
            "Q" => "Q",
            _ => "D"
        };
    }

    private static string NormalizeLabProviderDirection(string? direction)
    {
        return NormalizeText(direction)?.ToUpperInvariant() == "R" ? "R" : "B";
    }

    private static string NormalizeLabProviderAddressBookType(string? type)
    {
        var normalized = NormalizeText(type)?.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith("ord_", StringComparison.Ordinal)
            ? "ord_lab"
            : normalized;
    }

    private static OrderCatalogMutationValues? NormalizeOrderCatalogMutation(
        ProcedureOrderCatalogMutationRequest request)
    {
        var name = NormalizeText(request.Name);
        if (name is null)
        {
            return null;
        }

        var itemType = NormalizeText(request.ItemType)?.ToLowerInvariant() switch
        {
            "grp" => "grp",
            "ord" => "ord",
            _ => "ord"
        };
        var parentId = request.ParentId is > 0 ? request.ParentId : null;
        var labId = request.LabId is > 0 ? request.LabId : null;
        var code = NormalizeText(request.Code);

        if (itemType == "ord" && (parentId is null || labId is null || code is null))
        {
            return null;
        }

        return new OrderCatalogMutationValues(
            ParentId: parentId,
            LabId: labId,
            Name: name,
            Code: code,
            ItemType: itemType,
            ProcedureTypeName: NormalizeText(request.ProcedureTypeName) ?? (itemType == "ord" ? "laboratory" : null),
            Description: NormalizeText(request.Description),
            Specimen: NormalizeText(request.Specimen),
            StandardCode: NormalizeText(request.StandardCode),
            Sequence: request.Sequence ?? 0,
            Active: request.Active);
    }

    private static async Task<bool> IsValidOrderCatalogContextAsync(
        NpgsqlConnection connection,
        OrderCatalogMutationValues values,
        CancellationToken cancellationToken)
    {
        if (values.ParentId is { } parentId)
        {
            await using var parentCommand = connection.CreateCommand();
            parentCommand.CommandText = """
                select exists (
                    select 1
                    from lab_order_catalog
                    where id = @parentId
                      and item_type = 'grp'
                );
                """;
            parentCommand.Parameters.AddWithValue("parentId", parentId);
            if (!Convert.ToBoolean(await parentCommand.ExecuteScalarAsync(cancellationToken)))
            {
                return false;
            }
        }

        if (values.LabId is { } labId)
        {
            await using var labCommand = connection.CreateCommand();
            labCommand.CommandText = "select exists (select 1 from lab_providers where id = @labId);";
            labCommand.Parameters.AddWithValue("labId", labId);
            if (!Convert.ToBoolean(await labCommand.ExecuteScalarAsync(cancellationToken)))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddOrderCatalogMutationParameters(
        NpgsqlCommand command,
        OrderCatalogMutationValues values)
    {
        command.Parameters.Add("parentId", NpgsqlDbType.Integer).Value = values.ParentId is { } parentId ? parentId : DBNull.Value;
        command.Parameters.Add("labId", NpgsqlDbType.Integer).Value = values.LabId is { } labId ? labId : DBNull.Value;
        command.Parameters.Add("code", NpgsqlDbType.Text).Value = values.Code is { } code ? code : DBNull.Value;
        command.Parameters.AddWithValue("name", values.Name);
        command.Parameters.AddWithValue("itemType", values.ItemType);
        command.Parameters.Add("procedureTypeName", NpgsqlDbType.Text).Value =
            values.ProcedureTypeName is { } procedureTypeName ? procedureTypeName : DBNull.Value;
        command.Parameters.Add("description", NpgsqlDbType.Text).Value =
            values.Description is { } description ? description : DBNull.Value;
        command.Parameters.Add("specimen", NpgsqlDbType.Text).Value = values.Specimen is { } specimen ? specimen : DBNull.Value;
        command.Parameters.Add("standardCode", NpgsqlDbType.Text).Value =
            values.StandardCode is { } standardCode ? standardCode : DBNull.Value;
        command.Parameters.AddWithValue("sequence", values.Sequence);
        command.Parameters.AddWithValue("active", values.Active);
    }

    private static OrderCatalogImportValues? NormalizeOrderCatalogImport(ProcedureOrderCatalogImportRequest request)
    {
        var vendorFormat = NormalizeText(request.VendorFormat)?.ToLowerInvariant() switch
        {
            "pathgroup" or "pathgroup-labs" => "pathgroup",
            "ympg-dpmg" or "ypmg" or "dpmg" or "yosemite" or "diagnostic-pathology" => "ympg-dpmg",
            _ => null
        };
        if (vendorFormat is null || request.ParentId <= 0 || request.LabId <= 0)
        {
            return null;
        }

        var rows = ParseOrderCatalogCsv(request.CsvText, vendorFormat);
        return new OrderCatalogImportValues(
            VendorFormat: vendorFormat,
            ParentId: request.ParentId,
            LabId: request.LabId,
            Rows: rows);
    }

    private static IReadOnlyList<OrderCatalogCompendiumRow> ParseOrderCatalogCsv(string? csvText, string vendorFormat)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return [];
        }

        var rows = new List<OrderCatalogCompendiumRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var columns in ParseCsv(csvText))
        {
            if (columns.Length == 0)
            {
                continue;
            }

            var orderCode = NormalizeText(columns.ElementAtOrDefault(0));
            if (orderCode is null || string.Equals(orderCode, "Order Code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var orderName = NormalizeText(columns.ElementAtOrDefault(1));
            if (orderName is null)
            {
                continue;
            }

            var resultCode = vendorFormat == "pathgroup" ? NormalizeText(columns.ElementAtOrDefault(2)) : null;
            var resultName = vendorFormat == "pathgroup" ? NormalizeText(columns.ElementAtOrDefault(3)) : null;
            if (vendorFormat == "pathgroup" && columns.Length < 4)
            {
                continue;
            }

            var dedupeKey = $"{orderCode}|{resultCode ?? string.Empty}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            rows.Add(new OrderCatalogCompendiumRow(
                OrderCode: orderCode,
                OrderName: orderName,
                ResultCode: resultCode,
                ResultName: resultName));
        }

        return rows;
    }

    private static IReadOnlyList<string[]> ParseCsv(string csvText)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvText.Length; index += 1)
        {
            var current = csvText[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < csvText.Length && csvText[index + 1] == '"')
                    {
                        field.Append('"');
                        index += 1;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (current == '"')
            {
                inQuotes = true;
                continue;
            }

            if (current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (current is '\r' or '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    rows.Add(row.ToArray());
                }

                row.Clear();
                if (current == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index += 1;
                }

                continue;
            }

            field.Append(current);
        }

        row.Add(field.ToString());
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static async Task<bool> IsValidOrderCatalogImportContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int parentId,
        int labId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                exists (
                    select 1
                    from lab_order_catalog
                    where id = @parentId
                      and item_type = 'grp'
                ) as has_parent,
                exists (
                    select 1
                    from lab_providers
                    where id = @labId
                ) as has_lab;
            """;
        command.Parameters.AddWithValue("parentId", parentId);
        command.Parameters.AddWithValue("labId", labId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return reader.GetBoolean(reader.GetOrdinal("has_parent"))
            && reader.GetBoolean(reader.GetOrdinal("has_lab"));
    }

    private static async Task<int> DeactivateOrderCatalogChildrenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int parentId,
        string itemType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update lab_order_catalog
            set active = false
            where parent_id = @parentId
              and item_type = @itemType
              and active = true;
            """;
        command.Parameters.AddWithValue("parentId", parentId);
        command.Parameters.AddWithValue("itemType", itemType);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OrderCatalogImportMutation> UpsertImportedOrderCatalogItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int parentId,
        int labId,
        string code,
        string name,
        string itemType,
        CancellationToken cancellationToken)
    {
        var existing = await GetImportedOrderCatalogItemAsync(
            connection,
            transaction,
            parentId,
            code,
            itemType,
            cancellationToken);
        if (existing is null)
        {
            var id = await GetNextIntIdAsync(connection, "lab_order_catalog", "id", transaction, cancellationToken);
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into lab_order_catalog
                    (id, parent_id, lab_id, code, name, item_type, procedure_type_name, description, specimen, standard_code, seq, active)
                values
                    (@id, @parentId, @labId, @code, @name, @itemType, @procedureTypeName, null, null, null, 0, true);
                """;
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("parentId", parentId);
            insert.Parameters.AddWithValue("labId", labId);
            insert.Parameters.AddWithValue("code", code);
            insert.Parameters.AddWithValue("name", name);
            insert.Parameters.AddWithValue("itemType", itemType);
            insert.Parameters.Add("procedureTypeName", NpgsqlDbType.Text).Value = itemType == "ord" ? "laboratory" : DBNull.Value;
            await insert.ExecuteNonQueryAsync(cancellationToken);
            return new OrderCatalogImportMutation(id, Created: true, Reactivated: false);
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            update lab_order_catalog
            set parent_id = @parentId,
                lab_id = @labId,
                code = @code,
                name = @name,
                item_type = @itemType,
                active = true
            where id = @id;
            """;
        update.Parameters.AddWithValue("id", existing.Id);
        update.Parameters.AddWithValue("parentId", parentId);
        update.Parameters.AddWithValue("labId", labId);
        update.Parameters.AddWithValue("code", code);
        update.Parameters.AddWithValue("name", name);
        update.Parameters.AddWithValue("itemType", itemType);
        await update.ExecuteNonQueryAsync(cancellationToken);

        return new OrderCatalogImportMutation(existing.Id, Created: false, Reactivated: !existing.Active);
    }

    private static async Task<ExistingCatalogImportItem?> GetImportedOrderCatalogItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int parentId,
        string code,
        string itemType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, active
            from lab_order_catalog
            where parent_id = @parentId
              and code = @code
              and item_type = @itemType
            order by id desc
            limit 1;
            """;
        command.Parameters.AddWithValue("parentId", parentId);
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("itemType", itemType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExistingCatalogImportItem(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            Active: reader.GetBoolean(reader.GetOrdinal("active")));
    }

    private static async Task<(string Name, int? LabDirectorId)?> ResolveLabProviderNameAsync(
        NpgsqlConnection connection,
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LabDirectorId is > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select organization
                from lab_provider_address_book
                where id = @id and type like 'ord_%'
                limit 1;
                """;
            command.Parameters.AddWithValue("id", request.LabDirectorId.Value);
            var organization = NormalizeText(await command.ExecuteScalarAsync(cancellationToken) as string);
            return organization is null ? null : (organization, request.LabDirectorId.Value);
        }

        var name = NormalizeText(request.Name);
        return name is null ? null : (name, null);
    }

    private static void AddLabProviderConfigurationParameters(
        NpgsqlCommand command,
        ProcedureLabProviderMutationRequest request)
    {
        command.Parameters.AddWithValue("usage", NormalizeLabProviderUsage(request.Usage));
        command.Parameters.AddWithValue("direction", NormalizeLabProviderDirection(request.Direction));
        command.Parameters.AddWithValue("sendAppId", NormalizeText(request.SendApplicationId) ?? string.Empty);
        command.Parameters.AddWithValue("sendFacId", NormalizeText(request.SendFacilityId) ?? string.Empty);
        command.Parameters.AddWithValue("recvAppId", NormalizeText(request.ReceiveApplicationId) ?? string.Empty);
        command.Parameters.AddWithValue("recvFacId", NormalizeText(request.ReceiveFacilityId) ?? string.Empty);
        command.Parameters.AddWithValue("remoteHost", NormalizeText(request.RemoteHost) ?? string.Empty);
        command.Parameters.AddWithValue("login", NormalizeText(request.Login) ?? string.Empty);
        command.Parameters.AddWithValue("password", NormalizeText(request.Password) ?? string.Empty);
        command.Parameters.AddWithValue("ordersPath", NormalizeText(request.OrdersPath) ?? string.Empty);
        command.Parameters.AddWithValue("resultsPath", NormalizeText(request.ResultsPath) ?? string.Empty);
        command.Parameters.Add("notes", NpgsqlDbType.Text).Value = NormalizeText(request.Notes) is { } notes ? notes : DBNull.Value;
    }

    private static string NormalizeReviewQueueStatus(string? status)
    {
        var normalized = NormalizeText(status)?.ToLowerInvariant();
        return normalized switch
        {
            "all" => "all",
            "reviewed" => "reviewed",
            "received" or "received-unreviewed" or "pending" or "unreviewed" => "unreviewed",
            _ => "unreviewed"
        };
    }

    private static string NormalizeOrderQueueStatus(string? status)
    {
        var normalized = NormalizeText(status)?.ToLowerInvariant();
        return normalized switch
        {
            "all" => "all",
            "reported" or "with-reports" or "reports" => "reported",
            "transmitted" or "sent" or "sent-pending" or "transmitted-pending" => "transmitted-pending",
            "scheduled" => "scheduled",
            "complete" or "completed" => "completed",
            "ready" or "ready-to-send" or "unsent" or "untransmitted" or "pending" or "reportless" => "ready-to-send",
            _ => "ready-to-send"
        };
    }

    private static void AddReviewQueueFilterParameters(
        NpgsqlCommand command,
        string? patientFilter,
        int? providerFilter,
        int? labFilter,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        command.Parameters.Add("patientFilter", NpgsqlDbType.Text).Value = patientFilter is null ? DBNull.Value : patientFilter;
        command.Parameters.Add("providerFilter", NpgsqlDbType.Integer).Value = providerFilter is null ? DBNull.Value : providerFilter.Value;
        command.Parameters.Add("labFilter", NpgsqlDbType.Integer).Value = labFilter is null ? DBNull.Value : labFilter.Value;
        command.Parameters.Add("fromDate", NpgsqlDbType.Date).Value = fromDate is null ? DBNull.Value : fromDate;
        command.Parameters.Add("toDate", NpgsqlDbType.Date).Value = toDate is null ? DBNull.Value : toDate;
    }

    private static ProcedureOrderCounts BuildCounts(IReadOnlyList<ProcedureOrderItem> orders, DateOnly baseDate)
    {
        var specimens = orders.Sum(order => order.Specimens.Count);
        var reports = orders.Sum(order => order.Reports.Count);
        var results = orders.Sum(order => order.Reports.Sum(report => report.Results.Count));
        var finalResults = orders.Sum(order =>
            order.Reports.Sum(report =>
                report.Results.Count(result => string.Equals(result.ResultStatus, "final", StringComparison.OrdinalIgnoreCase))));

        return new ProcedureOrderCounts(
            Orders: orders.Count,
            CompletedOrders: orders.Count(order => string.Equals(order.OrderStatus, "complete", StringComparison.OrdinalIgnoreCase)),
            ScheduledOrders: orders.Count(order => string.Equals(order.OrderStatus, "scheduled", StringComparison.OrdinalIgnoreCase)),
            ReportlessOrders: orders.Count(order => order.Reports.Count == 0),
            FutureScheduledOrders: orders.Count(order =>
                string.Equals(order.OrderStatus, "scheduled", StringComparison.OrdinalIgnoreCase)
                && DateOnly.TryParse(order.OrderDate, out var orderDate)
                && orderDate > baseDate),
            Specimens: specimens,
            Reports: reports,
            Results: results,
            FinalResults: finalResults);
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm");
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record ProcedurePatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName);

    private sealed record ProcedureOrderRow(int Id, ProcedureOrderItem Order);

    private sealed record ProcedureSpecimenRow(int OrderId, ProcedureSpecimenItem Specimen);

    private sealed record ProcedureReportRow(int Id, int OrderId, ProcedureReportItem Report);

    private sealed record ProcedureResultRow(int ReportId, ProcedureResultItem Result);
    private sealed record ProcedureResultBaseRow(
        int Id,
        int ReportId,
        string? Code,
        string? Text,
        string? Units,
        string? Result,
        string? Range,
        string? Abnormal,
        DateTime ResultDate,
        string? ResultStatus,
        int PriorVersionCount);

    private sealed record ProcedureEncounterMutationContext(int Encounter, int? ProviderId);

    private sealed record ProcedureOrderMutationContext(int Id, string PatientId, int LegacyPid, int Encounter);

    private sealed record ProcedureReportMutationContext(int Id, string PatientId, int LegacyPid);

    private sealed record ProcedureReportReviewContext(
        int Id,
        string PatientId,
        string ReviewStatus,
        int ReviewVersion);

    private sealed record ProcedureResultMutationContext(int Id, string PatientId, int LegacyPid);

    private readonly record struct OrderCatalogMutationValues(
        int? ParentId,
        int? LabId,
        string Name,
        string? Code,
        string ItemType,
        string? ProcedureTypeName,
        string? Description,
        string? Specimen,
        string? StandardCode,
        int Sequence,
        bool Active);

    private sealed record OrderCatalogImportValues(
        string VendorFormat,
        int ParentId,
        int LabId,
        IReadOnlyList<OrderCatalogCompendiumRow> Rows);

    private sealed record OrderCatalogCompendiumRow(
        string OrderCode,
        string OrderName,
        string? ResultCode,
        string? ResultName);

    private sealed record ExistingCatalogImportItem(int Id, bool Active);

    private sealed record OrderCatalogImportMutation(int Id, bool Created, bool Reactivated);
}
