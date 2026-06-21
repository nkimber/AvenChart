using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ProcedureRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumReviewQueueLimit = 100;

    public async Task<ProcedureResultsResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
                    SendApplicationId: ReadNullableString(reader, "send_app_id"),
                    SendFacilityId: ReadNullableString(reader, "send_fac_id"),
                    ReceiveApplicationId: ReadNullableString(reader, "recv_app_id"),
                    ReceiveFacilityId: ReadNullableString(reader, "recv_fac_id"),
                    RemoteHost: ReadNullableString(reader, "remote_host"),
                    Login: ReadNullableString(reader, "login"),
                    Password: ReadNullableString(reader, "password"),
                    OrdersPath: ReadNullableString(reader, "orders_path"),
                    ResultsPath: ReadNullableString(reader, "results_path"),
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
                (@id, @orderId, @dateCollected, @reportDate, @specimenNumber, @status, @reviewStatus, null, null, @notes);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("orderId", order.Id);
        command.Parameters.Add("dateCollected", NpgsqlDbType.Timestamp).Value = collectedDate;
        command.Parameters.Add("reportDate", NpgsqlDbType.Timestamp).Value = reportDate;
        command.Parameters.AddWithValue("specimenNumber", request.SpecimenNumber.Trim());
        command.Parameters.AddWithValue("status", request.ReportStatus.Trim());
        command.Parameters.AddWithValue("reviewStatus", request.ReviewStatus.Trim());
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
                review_status = @reviewStatus,
                reviewed_by = case when @reviewStatus = 'reviewed' then reviewed_by else null end,
                reviewed_at = case when @reviewStatus = 'reviewed' then reviewed_at else null end,
                notes = @notes
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", report.Id);
        command.Parameters.Add("dateCollected", NpgsqlDbType.Timestamp).Value = collectedDate;
        command.Parameters.Add("reportDate", NpgsqlDbType.Timestamp).Value = reportDate;
        command.Parameters.AddWithValue("specimenNumber", request.SpecimenNumber.Trim());
        command.Parameters.AddWithValue("status", request.ReportStatus.Trim());
        command.Parameters.AddWithValue("reviewStatus", request.ReviewStatus.Trim());
        command.Parameters.AddWithValue("notes", request.Notes?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(report.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(report.Id, detail);
    }

    public async Task<ProcedureMutationResponse?> SignReportAsync(
        int reportId,
        ProcedureReportSignRequest request,
        CancellationToken cancellationToken)
    {
        if (reportId <= 0
            || string.IsNullOrWhiteSpace(request.ReviewedBy)
            || !TryReadDateTime(request.ReviewedAt, out var reviewedAt))
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
            set review_status = 'reviewed',
                reviewed_by = @reviewedBy,
                reviewed_at = @reviewedAt
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", report.Id);
        command.Parameters.AddWithValue("reviewedBy", request.ReviewedBy.Trim());
        command.Parameters.Add("reviewedAt", NpgsqlDbType.Timestamp).Value = reviewedAt;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(report.PatientId, cancellationToken);
        return detail is null ? null : new ProcedureMutationResponse(report.Id, detail);
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
        var result = await GetResultMutationContextAsync(connection, resultId, cancellationToken);
        if (result is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
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
            select id, order_id, date_collected, report_date, specimen_number, status, review_status, reviewed_by, reviewed_at, notes
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
            select id, report_id, code, text, units, result, range, abnormal, result_date, result_status
            from lab_results
            where report_id = any(@reportIds)
            order by id;
            """;
        command.Parameters.AddWithValue("reportIds", reportIds.ToArray());

        var rows = new List<ProcedureResultRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProcedureResultRow(
                ReportId: reader.GetInt32(reader.GetOrdinal("report_id")),
                Result: new ProcedureResultItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Code: ReadNullableString(reader, "code"),
                    Text: ReadNullableString(reader, "text"),
                    Units: ReadNullableString(reader, "units"),
                    Result: ReadNullableString(reader, "result"),
                    Range: ReadNullableString(reader, "range"),
                    Abnormal: ReadNullableString(reader, "abnormal"),
                    ResultDate: reader.GetDateTime(reader.GetOrdinal("result_date")).ToString("yyyy-MM-dd HH:mm"),
                    ResultStatus: ReadNullableString(reader, "result_status"))));
        }

        return rows;
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

    private static async Task<ProcedureOrderMutationContext?> GetOrderMutationContextAsync(
        NpgsqlConnection connection,
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, patient_id, pid
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
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")));
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

    private sealed record ProcedureEncounterMutationContext(int Encounter, int? ProviderId);

    private sealed record ProcedureOrderMutationContext(int Id, string PatientId, int LegacyPid);

    private sealed record ProcedureReportMutationContext(int Id, string PatientId, int LegacyPid);

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
