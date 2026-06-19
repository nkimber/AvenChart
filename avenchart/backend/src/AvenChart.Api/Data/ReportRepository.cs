using System.Data.Common;
using System.Globalization;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReportRepository(NpgsqlDataSource dataSource)
{
    public async Task<OperationalReportsResponse> GetOperationalReportsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var header = await GetReportHeaderAsync(connection, cancellationToken);
        var counts = await GetCountsAsync(connection, header.BaseDate, cancellationToken);
        var providers = await GetProviderActivityAsync(connection, cancellationToken);
        var facilities = await GetFacilityActivityAsync(connection, cancellationToken);
        var conditions = await GetClinicalConditionsAsync(connection, cancellationToken);

        return new OperationalReportsResponse(
            DatasetId: header.DatasetId,
            DatasetVersion: header.DatasetVersion,
            AsOfDate: header.BaseDate.ToString("yyyy-MM-dd"),
            CurrentYear: header.BaseDate.Year,
            Counts: counts,
            ProviderActivity: providers,
            FacilityActivity: facilities,
            ClinicalConditions: conditions);
    }

    public async Task<string> GetOperationalReportsCsvAsync(CancellationToken cancellationToken)
    {
        var report = await GetOperationalReportsAsync(cancellationToken);
        var builder = new StringBuilder();
        AppendCsvRow(builder, "Section", "Name", "Metric", "Value");

        AppendCsvRow(builder, "Counts", "Patients", "Total", report.Counts.Patients);
        AppendCsvRow(builder, "Counts", "Portal Patients", "Total", report.Counts.PortalPatients);
        AppendCsvRow(builder, "Counts", "Appointments", "Total", report.Counts.Appointments);
        AppendCsvRow(builder, "Counts", "Future Appointments", "Total", report.Counts.FutureAppointments);
        AppendCsvRow(builder, "Counts", "Current Year Appointments", "Total", report.Counts.CurrentYearAppointments);
        AppendCsvRow(builder, "Counts", "Encounters", "Total", report.Counts.Encounters);
        AppendCsvRow(builder, "Counts", "Current Year Encounters", "Total", report.Counts.CurrentYearEncounters);
        AppendCsvRow(builder, "Counts", "Billing Lines", "Total", report.Counts.BillingLines);
        AppendCsvRow(builder, "Counts", "Billing Total", "USD", report.Counts.BillingTotal);
        AppendCsvRow(builder, "Counts", "Lab Reports", "Total", report.Counts.LabReports);
        AppendCsvRow(builder, "Counts", "Messages", "Total", report.Counts.Messages);
        AppendCsvRow(builder, "Counts", "New Messages", "Total", report.Counts.NewMessages);
        AppendCsvRow(builder, "Counts", "Done Messages", "Total", report.Counts.DoneMessages);
        AppendCsvRow(builder, "Counts", "Facilities", "Total", report.Counts.Facilities);
        AppendCsvRow(builder, "Counts", "Providers", "Total", report.Counts.Providers);

        foreach (var provider in report.ProviderActivity)
        {
            AppendCsvRow(builder, "Provider Activity", provider.Username, "Display Name", provider.DisplayName);
            AppendCsvRow(builder, "Provider Activity", provider.Username, "Encounters", provider.Encounters);
            AppendCsvRow(builder, "Provider Activity", provider.Username, "Billing Lines", provider.BillingLines);
            AppendCsvRow(builder, "Provider Activity", provider.Username, "Billing Total", provider.BillingTotal);
        }

        foreach (var facility in report.FacilityActivity)
        {
            AppendCsvRow(builder, "Facility Activity", facility.Code, "Name", facility.Name);
            AppendCsvRow(builder, "Facility Activity", facility.Code, "Appointments", facility.Appointments);
            AppendCsvRow(builder, "Facility Activity", facility.Code, "Encounters", facility.Encounters);
            AppendCsvRow(builder, "Facility Activity", facility.Code, "Billing Lines", facility.BillingLines);
            AppendCsvRow(builder, "Facility Activity", facility.Code, "Billing Total", facility.BillingTotal);
        }

        foreach (var condition in report.ClinicalConditions)
        {
            var key = string.IsNullOrWhiteSpace(condition.Diagnosis) ? condition.Title : condition.Diagnosis;
            AppendCsvRow(builder, "Clinical Conditions", key, "Title", condition.Title);
            AppendCsvRow(builder, "Clinical Conditions", key, "Patients", condition.Patients);
        }

        return builder.ToString();
    }

    private static async Task<ReportHeader> GetReportHeaderAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
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
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return new ReportHeader("unseeded", "unknown", today);
        }

        return new ReportHeader(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<OperationalReportCounts> GetCountsAsync(
        NpgsqlConnection connection,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              (select count(*) from patients) as patients,
              (select count(*) from patients where portal_enabled) as portal_patients,
              (select count(*) from appointments) as appointments,
              (select count(*) from appointments where appointment_date > @asOfDate) as future_appointments,
              (select count(*) from appointments where appointment_date >= @yearStart and appointment_date < @nextYear) as current_year_appointments,
              (select count(*) from encounters) as encounters,
              (select count(*) from encounters where encounter_date >= @yearStart and encounter_date < @nextYear) as current_year_encounters,
              (select count(*) from billing) as billing_lines,
              (select coalesce(sum(fee), 0) from billing) as billing_total,
              (select count(*) from lab_reports) as lab_reports,
              (select count(*) from messages) as messages,
              (select count(*) from messages where status = 'New') as new_messages,
              (select count(*) from messages where status = 'Done') as done_messages,
              (select count(*) from facilities) as facilities,
              (select count(*) from staff where role = 'provider') as providers;
            """;
        command.Parameters.AddWithValue("asOfDate", asOfDate);
        command.Parameters.AddWithValue("yearStart", new DateOnly(asOfDate.Year, 1, 1));
        command.Parameters.AddWithValue("nextYear", new DateOnly(asOfDate.Year + 1, 1, 1));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new OperationalReportCounts(0, 0, 0, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0, 0);
        }

        return new OperationalReportCounts(
            Patients: ReadCount(reader, "patients"),
            PortalPatients: ReadCount(reader, "portal_patients"),
            Appointments: ReadCount(reader, "appointments"),
            FutureAppointments: ReadCount(reader, "future_appointments"),
            CurrentYearAppointments: ReadCount(reader, "current_year_appointments"),
            Encounters: ReadCount(reader, "encounters"),
            CurrentYearEncounters: ReadCount(reader, "current_year_encounters"),
            BillingLines: ReadCount(reader, "billing_lines"),
            BillingTotal: reader.GetDecimal(reader.GetOrdinal("billing_total")),
            LabReports: ReadCount(reader, "lab_reports"),
            Messages: ReadCount(reader, "messages"),
            NewMessages: ReadCount(reader, "new_messages"),
            DoneMessages: ReadCount(reader, "done_messages"),
            Facilities: ReadCount(reader, "facilities"),
            Providers: ReadCount(reader, "providers"));
    }

    private static async Task<IReadOnlyList<ProviderActivityReportItem>> GetProviderActivityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with provider_encounters as (
              select provider_id, count(*) as encounters
              from encounters
              group by provider_id
            ),
            provider_billing as (
              select provider_id, count(*) as billing_lines, coalesce(sum(fee), 0) as billing_total
              from billing
              group by provider_id
            )
            select s.username, s.first_name, s.last_name,
              coalesce(pe.encounters, 0) as encounters,
              coalesce(pb.billing_lines, 0) as billing_lines,
              coalesce(pb.billing_total, 0) as billing_total
            from staff s
            left join provider_encounters pe on pe.provider_id = s.id
            left join provider_billing pb on pb.provider_id = s.id
            where s.role = 'provider'
            order by encounters desc, billing_total desc, s.id
            limit 8;
            """;

        var items = new List<ProviderActivityReportItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            items.Add(new ProviderActivityReportItem(
                Username: reader.GetString(reader.GetOrdinal("username")),
                FirstName: firstName,
                LastName: lastName,
                DisplayName: $"{lastName}, {firstName}",
                Encounters: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("encounters"))),
                BillingLines: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("billing_lines"))),
                BillingTotal: reader.GetDecimal(reader.GetOrdinal("billing_total"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<FacilityActivityReportItem>> GetFacilityActivityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with facility_appointments as (
              select facility_id, count(*) as appointments
              from appointments
              group by facility_id
            ),
            facility_encounters as (
              select facility_id, count(*) as encounters
              from encounters
              group by facility_id
            ),
            facility_billing as (
              select e.facility_id, count(b.*) as billing_lines, coalesce(sum(b.fee), 0) as billing_total
              from billing b
              inner join encounters e on e.encounter = b.encounter
              group by e.facility_id
            )
            select f.code, f.name,
              coalesce(fa.appointments, 0) as appointments,
              coalesce(fe.encounters, 0) as encounters,
              coalesce(fb.billing_lines, 0) as billing_lines,
              coalesce(fb.billing_total, 0) as billing_total
            from facilities f
            left join facility_appointments fa on fa.facility_id = f.id
            left join facility_encounters fe on fe.facility_id = f.id
            left join facility_billing fb on fb.facility_id = f.id
            order by f.id;
            """;

        var items = new List<FacilityActivityReportItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new FacilityActivityReportItem(
                Code: reader.GetString(reader.GetOrdinal("code")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                Appointments: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("appointments"))),
                Encounters: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("encounters"))),
                BillingLines: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("billing_lines"))),
                BillingTotal: reader.GetDecimal(reader.GetOrdinal("billing_total"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<ClinicalConditionReportItem>> GetClinicalConditionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select title, coalesce(diagnosis, '') as diagnosis, count(*) as patients
            from problems
            group by title, diagnosis
            order by patients desc, title
            limit 8;
            """;

        var items = new List<ClinicalConditionReportItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ClinicalConditionReportItem(
                Title: ReadNullableString(reader, "title") ?? "Unspecified condition",
                Diagnosis: reader.GetString(reader.GetOrdinal("diagnosis")),
                Patients: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("patients")))));
        }

        return items;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int ReadCount(DbDataReader reader, string columnName)
    {
        return Convert.ToInt32(reader.GetInt64(reader.GetOrdinal(columnName)));
    }

    private static void AppendCsvRow(StringBuilder builder, params object[] values)
    {
        builder.AppendJoin(',', values.Select(FormatCsvValue));
        builder.AppendLine();
    }

    private static string FormatCsvValue(object value)
    {
        var text = value switch
        {
            decimal decimalValue => decimalValue.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            null => string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private sealed record ReportHeader(string DatasetId, string DatasetVersion, DateOnly BaseDate);
}
