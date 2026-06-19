using System.Data.Common;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ProcedureRepository(NpgsqlDataSource dataSource)
{
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
        var reports = await GetReportsAsync(connection, orders.Select(order => order.Id).ToArray(), cancellationToken);
        var results = await GetResultsAsync(connection, reports.Select(report => report.Id).ToArray(), cancellationToken);

        var resultsByReport = results.GroupBy(result => result.ReportId).ToDictionary(group => group.Key, group => group.Select(item => item.Result).ToList());
        var reportsByOrder = reports
            .GroupBy(report => report.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(report => report.Report with
                {
                    Results = resultsByReport.GetValueOrDefault(report.Id, [])
                }).ToList());

        return new ProcedureResultsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Orders: orders.Select(order => order.Order with
            {
                Reports = reportsByOrder.GetValueOrDefault(order.Id, [])
            }).ToList());
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
                lo.code,
                lo.name,
                lo.diagnosis,
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
                    Code: ReadNullableString(reader, "code"),
                    Name: ReadNullableString(reader, "name"),
                    Diagnosis: ReadNullableString(reader, "diagnosis"),
                    OrderStatus: ReadNullableString(reader, "order_status"),
                    Reports: [])));
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
            select id, order_id, report_date, status
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
                    ReportDate: reader.GetDateTime(reader.GetOrdinal("report_date")).ToString("yyyy-MM-dd HH:mm"),
                    Status: ReadNullableString(reader, "status"),
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

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
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

    private sealed record ProcedureReportRow(int Id, int OrderId, ProcedureReportItem Report);

    private sealed record ProcedureResultRow(int ReportId, ProcedureResultItem Result);
}
