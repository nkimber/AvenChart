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
        int limit,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeReviewQueueStatus(status);
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
                    coalesce(sum(case when coalesce(review_status, '') = 'reviewed' then 1 else 0 end), 0) as reviewed_reports,
                    coalesce(sum(case when coalesce(review_status, '') <> 'reviewed' then 1 else 0 end), 0) as unreviewed_reports
                from lab_reports;
                """;

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
                nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
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
            where (@statusFilter = 'all')
               or (@statusFilter = 'reviewed' and coalesce(lr.review_status, '') = 'reviewed')
               or (@statusFilter = 'unreviewed' and coalesce(lr.review_status, '') <> 'reviewed')
            order by lr.report_date desc, lr.id desc, p.last_name, p.first_name, p.legacy_pid, lo.id
            limit @limit;
            """;
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
                    ProviderName: ReadNullableString(reader, "provider_name"),
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
            Limit: safeLimit,
            TotalReports: totalReports,
            ReviewedReports: reviewedReports,
            UnreviewedReports: unreviewedReports,
            Reports: reports);
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
        var providerId = (object?)request.ProviderId ?? (object?)encounter.ProviderId ?? DBNull.Value;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into lab_orders
                (id, patient_id, pid, encounter, provider_id, order_date, code, name,
                 diagnosis, order_priority, procedure_type, instructions, order_status)
            values
                (@id, @patientId, @pid, @encounter, @providerId, @orderDate, @code, @name,
                 @diagnosis, @priority, @procedureType, @instructions, @status);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("encounter", encounter.Encounter);
        command.Parameters.AddWithValue("providerId", providerId);
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
        if (tableName is not ("lab_orders" or "lab_reports" or "lab_results" or "lab_specimens") || columnName != "id")
        {
            throw new ArgumentException("Unsupported procedure id source.");
        }

        await using var command = connection.CreateCommand();
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
}
