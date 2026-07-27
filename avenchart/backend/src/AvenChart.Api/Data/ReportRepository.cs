using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReportRepository(NpgsqlDataSource dataSource)
{
    private static readonly IReadOnlyList<ReportFamilyItem> Families = [new("operational", "Operational snapshot", "Practice counts and activity summary.", false), new("patients", "Patient list", "Registered patient demographics.", false), new("appointments", "Appointments", "Scheduled appointment activity.", true), new("encounters", "Encounters", "Clinical encounter activity.", true), new("referrals", "Referrals", "Local referral lifecycle activity.", true), new("chart-tracker", "Chart tracker", "Recorded chart-location handoffs.", true), new("inventory", "Inventory transactions", "Immutable inventory transaction activity.", true)];
    public IReadOnlyList<ReportFamilyItem> GetFamilies() => Families;

    public async Task<ControlledInventoryReportResponse> RunControlledInventoryReportAsync(ControlledInventoryReportRequest request, string username, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (asOf < new DateOnly(2000, 1, 1) || asOf > today)
        {
            throw new ArgumentException("Controlled report as-of date must be between 2000 and today.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var lines = new List<ControlledInventoryReportLine>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                with deltas as (
                  select lot_id, sum(quantity_delta) quantity
                  from inventory_controlled_custody_events
                  where occurred_at::date <= @asOf
                  group by lot_id
                  union all
                  select counterparty_lot_id, sum(quantity)
                  from inventory_controlled_custody_events
                  where action = 'transfer'
                    and occurred_at::date <= @asOf
                    and counterparty_lot_id is not null
                  group by counterparty_lot_id
                ), balances as (
                  select lot_id, sum(quantity) quantity
                  from deltas
                  group by lot_id
                )
                select l.lot_id, i.item_code, i.controlled_schedule, f.code,
                       cl.location_code, l.lot_number, b.quantity
                from balances b
                join inventory_lots l on l.lot_id = b.lot_id
                join inventory_items i on i.item_id = l.item_id
                join facilities f on f.id = l.facility_id
                join inventory_controlled_locations cl on cl.location_id = l.controlled_location_id
                where i.controlled_schedule is not null
                  and b.quantity <> 0
                  and (@locationId is null or cl.location_id = @locationId)
                order by i.item_code, cl.location_code, l.lot_number;
                """;
            command.Parameters.AddWithValue("asOf", asOf);
            command.Parameters.AddWithValue("locationId", (object?)request.LocationId ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDecimal(6)));
            }
        }

        var payload = string.Join("\n", lines.Select(line =>
            $"{line.LotId}|{line.ItemCode}|{line.ScheduleCode}|{line.FacilityCode}|{line.LocationCode}|{line.LotNumber}|{line.QuantityOnHand.ToString(CultureInfo.InvariantCulture)}"));
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var runId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                insert into inventory_controlled_report_runs (
                  run_id, report_key, as_of_date, location_id, requested_by,
                  requested_at, row_count, result_checksum)
                values (
                  @runId, 'as_of_inventory', @asOf, @locationId, @requestedBy,
                  @requestedAt, @rowCount, @resultChecksum);
                """;
            insert.Parameters.AddWithValue("runId", runId);
            insert.Parameters.AddWithValue("asOf", asOf);
            insert.Parameters.AddWithValue("locationId", (object?)request.LocationId ?? DBNull.Value);
            insert.Parameters.AddWithValue("requestedBy", username);
            insert.Parameters.AddWithValue("requestedAt", requestedAt);
            insert.Parameters.AddWithValue("rowCount", lines.Count);
            insert.Parameters.AddWithValue("resultChecksum", checksum);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return new(
            new(
                runId,
                "as_of_inventory",
                asOf.ToString("yyyy-MM-dd"),
                request.LocationId,
                username,
                requestedAt.ToString("O"),
                lines.Count,
                checksum),
            lines);
    }
    public async Task<SavedReportDefinitionsResponse> GetSavedDefinitionsAsync(CancellationToken cancellationToken)
    {
        await EnsureSavedReportSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, name, report_type, schedule, active, created_by, created_at, last_run_at, run_count
            from saved_report_definitions order by created_at desc, name;
            """;
        var definitions = new List<SavedReportDefinitionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            definitions.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O"), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7).ToString("O"), reader.GetInt32(8)));
        }
        return new(definitions);
    }

    public async Task<SavedReportDefinitionItem> CreateSavedDefinitionAsync(SavedReportDefinitionRequest request, string username, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Report name is required and must be 120 characters or fewer.");
        var schedule = request.Schedule?.Trim().ToLowerInvariant();
        if (schedule is not ("manual" or "daily" or "weekly")) throw new ArgumentException("Schedule must be manual, daily, or weekly.");
        var reportType = string.IsNullOrWhiteSpace(request.ReportType) ? "operational" : request.ReportType.Trim().ToLowerInvariant(); if (!Families.Any(f => f.Key == reportType)) throw new ArgumentException("Report type must be a supported operational report family.");
        await EnsureSavedReportSchemaAsync(cancellationToken);
        var id = Guid.NewGuid(); var createdAt = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into saved_report_definitions (id, name, report_type, schedule, active, created_by, created_at)
            values (@id, @name, @reportType, @schedule, @active, @createdBy, @createdAt);
            """;
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("name", name); command.Parameters.AddWithValue("reportType", reportType); command.Parameters.AddWithValue("schedule", schedule); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("createdBy", username); command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new(id, name, reportType, schedule, request.Active, username, createdAt.ToString("O"), null, 0);
    }

    public async Task<SavedReportRunResponse?> RunSavedDefinitionAsync(Guid definitionId, string username, CancellationToken cancellationToken)
    {
        await EnsureSavedReportSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var definitionCommand = connection.CreateCommand(); definitionCommand.Transaction = transaction;
        definitionCommand.CommandText = "select report_type, active from saved_report_definitions where id = @id for update;"; definitionCommand.Parameters.AddWithValue("id", definitionId);
        await using var reader = await definitionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(1)) return null;
        var reportType = reader.GetString(0); await reader.DisposeAsync();
        var runId = $"RPT-{Guid.NewGuid():N}"; var ranAt = DateTimeOffset.UtcNow;
        await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = "insert into saved_report_runs (run_id, definition_id, ran_at, ran_by, output_format, row_count) values (@runId, @id, @ranAt, @ranBy, 'csv', 0); update saved_report_definitions set last_run_at = @ranAt, run_count = run_count + 1 where id = @id;";
        insert.Parameters.AddWithValue("runId", runId); insert.Parameters.AddWithValue("id", definitionId); insert.Parameters.AddWithValue("ranAt", ranAt); insert.Parameters.AddWithValue("ranBy", username);
        await insert.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(definitionId, runId, ranAt.ToString("O"), username, reportType, "csv", 0);
    }

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
        AppendCsvRow(builder, "Counts", "Patient Documents", "Total", report.Counts.PatientDocuments);
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

    public async Task<string> GetFamilyCsvAsync(string family, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var key = family.Trim().ToLowerInvariant(); if (!Families.Any(item => item.Key == key)) throw new ArgumentException("Unsupported report family."); if (from is not null && to is not null && from > to) throw new ArgumentException("From date cannot be after to date."); if (key == "operational") return await GetOperationalReportsCsvAsync(cancellationToken);
        await EnsureSavedReportSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = key switch
        {
            "patients" => "select p.canonical_id, trim(concat(p.last_name, ', ', p.first_name)), p.date_of_birth::text, coalesce(p.phone_cell,p.phone_home,p.email,'') from patients p where p.merged_into_patient_id is null order by p.last_name,p.first_name limit 5000;",
            "appointments" => "select a.id::text, p.pubpid, a.appointment_date::text, concat(coalesce(a.title,''),' | ',coalesce(a.status,'')) from appointments a join patients p on p.legacy_pid=a.pid where (@from is null or a.appointment_date>=@from) and (@to is null or a.appointment_date<=@to) order by a.appointment_date,a.start_time limit 5000;",
            "encounters" => "select e.encounter::text, p.pubpid, e.encounter_date::text, coalesce(e.reason,'') from encounters e join patients p on p.legacy_pid=e.pid where (@from is null or e.encounter_date>=@from) and (@to is null or e.encounter_date<=@to) order by e.encounter_date desc,e.encounter desc limit 5000;",
            "referrals" => "select r.id::text, p.pubpid, r.requested_at::date::text, concat(r.destination,' | ',r.status) from referrals r join patients p on p.canonical_id=r.patient_id where (@from is null or r.requested_at::date>=@from) and (@to is null or r.requested_at::date<=@to) order by r.requested_at desc limit 5000;",
            "chart-tracker" => "select e.id::text, p.pubpid, e.recorded_at::date::text, coalesce(e.location, trim(concat(s.first_name,' ',s.last_name)),'') from chart_tracker_events e join patients p on p.canonical_id=e.patient_id left join staff s on s.id=e.user_id where (@from is null or e.recorded_at::date>=@from) and (@to is null or e.recorded_at::date<=@to) order by e.recorded_at desc limit 5000;",
            "inventory" => "select t.transaction_id::text, i.item_code, t.occurred_at::date::text, concat(t.transaction_type,' | ',t.quantity_delta::text,' | ',coalesce(t.reason,'')) from inventory_transactions t join inventory_lots l on l.lot_id=t.lot_id join inventory_items i on i.item_id=l.item_id where (@from is null or t.occurred_at::date>=@from) and (@to is null or t.occurred_at::date<=@to) order by t.occurred_at desc limit 5000;",
            _ => throw new ArgumentException("Unsupported report family.")
        };
        command.Parameters.Add("from", NpgsqlDbType.Date).Value = (object?)from ?? DBNull.Value; command.Parameters.Add("to", NpgsqlDbType.Date).Value = (object?)to ?? DBNull.Value;
        var csv = new StringBuilder(); AppendCsvRow(csv, "Identifier", "Subject", "Date", "Detail"); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) AppendCsvRow(csv, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)); return csv.ToString();
    }

    private async Task EnsureSavedReportSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists saved_report_definitions (id uuid primary key, name text not null, report_type text not null, schedule text not null, active boolean not null default true, created_by text not null, created_at timestamptz not null, last_run_at timestamptz, run_count integer not null default 0);
            create table if not exists saved_report_runs (run_id text primary key, definition_id uuid not null references saved_report_definitions(id), ran_at timestamptz not null, ran_by text not null, output_format text not null, row_count integer not null);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
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
              (select count(*) from patient_documents where deleted = 0) as patient_documents,
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
            return new OperationalReportCounts(0, 0, 0, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0, 0, 0);
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
            PatientDocuments: ReadCount(reader, "patient_documents"),
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
