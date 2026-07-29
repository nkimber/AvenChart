using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed record GovernedReportDataScope(
    string RowPolicy,
    int? FacilityId,
    IReadOnlyList<string> PatientIds);

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
        var artifact = JsonSerializer.Serialize(lines);
        var runId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                insert into inventory_controlled_report_runs (
                  run_id, report_key, as_of_date, location_id, result_artifact, requested_by,
                  requested_at, row_count, result_checksum)
                values (
                  @runId, 'as_of_inventory', @asOf, @locationId, @artifact, @requestedBy,
                  @requestedAt, @rowCount, @resultChecksum);
                """;
            insert.Parameters.AddWithValue("runId", runId);
            insert.Parameters.AddWithValue("asOf", asOf);
            insert.Parameters.AddWithValue("locationId", (object?)request.LocationId ?? DBNull.Value);
            insert.Parameters.AddWithValue("artifact", NpgsqlDbType.Jsonb, artifact);
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

    public async Task<ControlledInventoryActivityReportResponse> RunControlledInventoryActivityReportAsync(
        ControlledInventoryActivityReportRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var reportType = request.ReportType?.Trim().ToLowerInvariant();
        if (reportType is not ("movement" or "waste" or "patient-dispense"))
        {
            throw new ArgumentException("Controlled activity report type must be movement, waste, or patient-dispense.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = request.ToDate ?? today;
        var fromDate = request.FromDate ?? toDate;
        if (fromDate < new DateOnly(2000, 1, 1) || toDate > today || fromDate > toDate || toDate.DayNumber - fromDate.DayNumber > 366)
        {
            throw new ArgumentException("Controlled activity report dates must be ordered dates between 2000 and today with a maximum 366-day range.");
        }

        if (request.LocationId == Guid.Empty)
        {
            throw new ArgumentException("Controlled activity report location must be a valid secure location.");
        }

        var patientId = string.IsNullOrWhiteSpace(request.PatientId) ? null : request.PatientId.Trim();
        if (patientId is { Length: > 128 })
        {
            throw new ArgumentException("Controlled activity report patient identifier must be 128 characters or fewer.");
        }

        if (patientId is not null && reportType != "patient-dispense")
        {
            throw new ArgumentException("A patient filter is available only for the patient-dispense report.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var lines = new List<ControlledInventoryActivityReportLine>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select e.event_id, e.action, e.lot_id, i.item_code, i.controlled_schedule,
                       f.code, l.lot_number, source.location_code as source_location_code,
                       destination.location_code as destination_location_code,
                       e.patient_id, e.encounter, e.quantity, e.quantity_delta, e.reason,
                       e.related_event_id, e.performed_by, e.occurred_at,
                       e.witness_username, e.witnessed_at
                from inventory_controlled_custody_events e
                join inventory_lots l on l.lot_id = e.lot_id
                join inventory_items i on i.item_id = l.item_id
                join facilities f on f.id = l.facility_id
                left join inventory_controlled_locations source on source.location_id = e.source_location_id
                left join inventory_controlled_locations destination on destination.location_id = e.destination_location_id
                where e.occurred_at::date >= @fromDate
                  and e.occurred_at::date <= @toDate
                  and (@locationId is null or e.source_location_id = @locationId or e.destination_location_id = @locationId)
                  and (@patientId is null or e.patient_id = @patientId)
                  and ((@reportType = 'movement' and e.action in ('receipt', 'transfer', 'return', 'correction'))
                    or (@reportType = 'waste' and e.action = 'waste')
                    or (@reportType = 'patient-dispense' and e.action in ('dispense', 'administration')))
                order by e.occurred_at, e.event_id;
                """;
            command.Parameters.AddWithValue("fromDate", NpgsqlDbType.Date, fromDate);
            command.Parameters.AddWithValue("toDate", NpgsqlDbType.Date, toDate);
            command.Parameters.AddWithValue("locationId", NpgsqlDbType.Uuid, (object?)request.LocationId ?? DBNull.Value);
            command.Parameters.AddWithValue("patientId", NpgsqlDbType.Text, (object?)patientId ?? DBNull.Value);
            command.Parameters.AddWithValue("reportType", NpgsqlDbType.Text, reportType);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    ReadNullableString(reader, "source_location_code"),
                    ReadNullableString(reader, "destination_location_code"),
                    ReadNullableString(reader, "patient_id"),
                    reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12),
                    reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetGuid(14),
                    reader.GetString(15),
                    reader.GetFieldValue<DateTimeOffset>(16).ToString("O"),
                    ReadNullableString(reader, "witness_username"),
                    reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18).ToString("O")));
            }
        }

        var filterEvidence = JsonSerializer.Serialize(new
        {
            reportType,
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            request.LocationId,
            patientId
        });
        var payload = string.Join("\n", new[] { filterEvidence }.Concat(lines.Select(line =>
            $"{line.EventId}|{line.Action}|{line.LotId}|{line.ItemCode}|{line.ScheduleCode}|{line.FacilityCode}|{line.LotNumber}|{line.SourceLocationCode}|{line.DestinationLocationCode}|{line.PatientId}|{line.Encounter}|{line.Quantity.ToString(CultureInfo.InvariantCulture)}|{line.QuantityDelta.ToString(CultureInfo.InvariantCulture)}|{line.Reason}|{line.RelatedEventId}|{line.PerformedBy}|{line.OccurredAt}|{line.WitnessUsername}|{line.WitnessedAt}")));
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var artifact = JsonSerializer.Serialize(lines);
        var runId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;

        await using (var insert = connection.CreateCommand())
        {
                insert.CommandText = """
                insert into inventory_controlled_report_runs (
                  run_id, report_key, as_of_date, date_from, location_id, input_filters,
                  result_artifact, requested_by, requested_at, row_count, result_checksum)
                values (
                  @runId, 'custody_activity', @toDate, @fromDate, @locationId, @inputFilters, @artifact,
                  @requestedBy, @requestedAt, @rowCount, @resultChecksum);
                """;
            insert.Parameters.AddWithValue("runId", runId);
            insert.Parameters.AddWithValue("toDate", toDate);
            insert.Parameters.AddWithValue("fromDate", fromDate);
            insert.Parameters.AddWithValue("locationId", (object?)request.LocationId ?? DBNull.Value);
            insert.Parameters.AddWithValue("inputFilters", NpgsqlDbType.Jsonb, filterEvidence);
            insert.Parameters.AddWithValue("artifact", NpgsqlDbType.Jsonb, artifact);
            insert.Parameters.AddWithValue("requestedBy", username);
            insert.Parameters.AddWithValue("requestedAt", requestedAt);
            insert.Parameters.AddWithValue("rowCount", lines.Count);
            insert.Parameters.AddWithValue("resultChecksum", checksum);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return new(
            new(
                runId,
                "custody_activity",
                reportType,
                fromDate.ToString("yyyy-MM-dd"),
                toDate.ToString("yyyy-MM-dd"),
                request.LocationId,
                patientId,
                username,
                requestedAt.ToString("O"),
                lines.Count,
                checksum),
            lines);
    }

    public async Task<string?> ExportControlledInventoryRunCsvAsync(Guid runId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await GetControlledReportArtifactAsync(connection, runId, "as_of_inventory", cancellationToken);
        if (result is null)
        {
            return null;
        }

        var lines = JsonSerializer.Deserialize<List<ControlledInventoryReportLine>>(result.Value.Artifact) ?? [];
        var csv = new StringBuilder("Lot ID,Item Code,Schedule,Facility,Location,Lot Number,Quantity On Hand\n");
        foreach (var line in lines)
        {
            AppendCsvRow(
                csv,
                line.LotId,
                line.ItemCode,
                line.ScheduleCode,
                line.FacilityCode,
                line.LocationCode,
                line.LotNumber,
                line.QuantityOnHand);
        }

        await RecordControlledReportExportAsync(connection, runId, username, result.Value.Checksum, cancellationToken);

        return csv.ToString();
    }

    public async Task<string?> ExportControlledInventoryActivityRunCsvAsync(Guid runId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await GetControlledReportArtifactAsync(connection, runId, "custody_activity", cancellationToken);
        if (result is null)
        {
            return null;
        }

        var lines = JsonSerializer.Deserialize<List<ControlledInventoryActivityReportLine>>(result.Value.Artifact) ?? [];
        var csv = new StringBuilder("Event ID,Action,Lot ID,Item Code,Schedule,Facility,Lot Number,Source Location,Destination Location,Patient ID,Encounter,Quantity,Quantity Delta,Reason,Related Event ID,Performed By,Occurred At,Witness Username,Witnessed At\n");
        foreach (var line in lines)
        {
            AppendCsvRow(csv, line.EventId, line.Action, line.LotId, line.ItemCode, line.ScheduleCode,
                line.FacilityCode, line.LotNumber, line.SourceLocationCode ?? string.Empty,
                line.DestinationLocationCode ?? string.Empty, line.PatientId ?? string.Empty,
                line.Encounter?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, line.Quantity,
                line.QuantityDelta, line.Reason, line.RelatedEventId?.ToString() ?? string.Empty,
                line.PerformedBy, line.OccurredAt, line.WitnessUsername ?? string.Empty,
                line.WitnessedAt ?? string.Empty);
        }

        await RecordControlledReportExportAsync(connection, runId, username, result.Value.Checksum, cancellationToken);
        return csv.ToString();
    }

    public async Task<string?> ExportControlledCountVarianceRunCsvAsync(Guid runId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var result = await GetControlledReportArtifactAsync(connection, runId, "count_variance", cancellationToken);
        if (result is null)
        {
            return null;
        }

        var lines = JsonSerializer.Deserialize<List<ControlledCountVarianceReportLine>>(result.Value.Artifact) ?? [];
        var csv = new StringBuilder("Session ID,Discrepancy ID,Location,Count Type,Session Status,Lot ID,Item Code,Lot Number,Expected Quantity,Observed Quantity,Variance Quantity,Discrepancy Status,Correction Event ID,Started At,Submitted At\n");
        foreach (var line in lines)
        {
            AppendCsvRow(csv, line.SessionId, line.DiscrepancyId, line.LocationCode, line.CountType,
                line.SessionStatus, line.LotId, line.ItemCode, line.LotNumber, line.ExpectedQuantity,
                line.ObservedQuantity, line.VarianceQuantity, line.DiscrepancyStatus,
                line.CorrectionEventId?.ToString() ?? string.Empty, line.StartedAt,
                line.SubmittedAt ?? string.Empty);
        }

        await RecordControlledReportExportAsync(connection, runId, username, result.Value.Checksum, cancellationToken);
        return csv.ToString();
    }

    public async Task<ControlledCountVarianceReportResponse> RunControlledCountVarianceReportAsync(ControlledCountVarianceReportRequest request, string username, CancellationToken cancellationToken)
    {
        var today=DateOnly.FromDateTime(DateTime.UtcNow); var toDate=request.ToDate??today; var fromDate=request.FromDate??toDate;
        if(fromDate<new DateOnly(2000,1,1)||toDate>today||fromDate>toDate||toDate.DayNumber-fromDate.DayNumber>366) throw new ArgumentException("Controlled count variance dates must be ordered dates between 2000 and today with a maximum 366-day range.");
        if(request.LocationId==Guid.Empty) throw new ArgumentException("Controlled count variance location must be a valid secure location.");
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); var lines=new List<ControlledCountVarianceReportLine>();
        await using(var command=connection.CreateCommand()) { command.CommandText="""
            select s.session_id,d.discrepancy_id,l.location_code,s.count_type,s.status,c.lot_id,i.item_code,lot.lot_number,c.expected_quantity,c.observed_quantity,c.variance_quantity,d.status,d.correction_event_id,s.started_at,s.submitted_at
            from inventory_controlled_count_discrepancies d join inventory_controlled_count_sessions s on s.session_id=d.session_id join inventory_controlled_count_lines c on c.line_id=d.line_id join inventory_controlled_locations l on l.location_id=s.location_id join inventory_lots lot on lot.lot_id=c.lot_id join inventory_items i on i.item_id=lot.item_id
            where s.submitted_at::date>=@fromDate and s.submitted_at::date<=@toDate and (@locationId is null or s.location_id=@locationId)
            order by s.submitted_at,d.discrepancy_id;
            """;
            command.Parameters.AddWithValue("fromDate",NpgsqlDbType.Date,fromDate); command.Parameters.AddWithValue("toDate",NpgsqlDbType.Date,toDate); command.Parameters.AddWithValue("locationId",NpgsqlDbType.Uuid,(object?)request.LocationId??DBNull.Value);
            await using var reader=await command.ExecuteReaderAsync(cancellationToken); while(await reader.ReadAsync(cancellationToken)) lines.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetInt32(5),reader.GetString(6),reader.GetString(7),reader.GetDecimal(8),reader.GetDecimal(9),reader.GetDecimal(10),reader.GetString(11),reader.IsDBNull(12)?null:reader.GetGuid(12),reader.GetFieldValue<DateTimeOffset>(13).ToString("O"),reader.IsDBNull(14)?null:reader.GetFieldValue<DateTimeOffset>(14).ToString("O"))); }
        var filters=JsonSerializer.Serialize(new { fromDate=fromDate.ToString("yyyy-MM-dd"),toDate=toDate.ToString("yyyy-MM-dd"),request.LocationId }); var payload=string.Join("\n",new[]{filters}.Concat(lines.Select(x=>$"{x.SessionId}|{x.DiscrepancyId}|{x.LotId}|{x.VarianceQuantity}|{x.DiscrepancyStatus}|{x.CorrectionEventId}"))); var checksum=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant(); var artifact=JsonSerializer.Serialize(lines); var runId=Guid.NewGuid(); var requestedAt=DateTimeOffset.UtcNow;
        await using(var insert=connection.CreateCommand()){insert.CommandText="insert into inventory_controlled_report_runs(run_id,report_key,as_of_date,date_from,location_id,input_filters,result_artifact,requested_by,requested_at,row_count,result_checksum) values(@id,'count_variance',@to,@from,@location,@filters,@artifact,@user,@at,@count,@checksum);";insert.Parameters.AddWithValue("id",runId);insert.Parameters.AddWithValue("to",toDate);insert.Parameters.AddWithValue("from",fromDate);insert.Parameters.AddWithValue("location",(object?)request.LocationId??DBNull.Value);insert.Parameters.AddWithValue("filters",NpgsqlDbType.Jsonb,filters);insert.Parameters.AddWithValue("artifact",NpgsqlDbType.Jsonb,artifact);insert.Parameters.AddWithValue("user",username);insert.Parameters.AddWithValue("at",requestedAt);insert.Parameters.AddWithValue("count",lines.Count);insert.Parameters.AddWithValue("checksum",checksum);await insert.ExecuteNonQueryAsync(cancellationToken);}
        return new(new(runId,"count_variance",fromDate.ToString("yyyy-MM-dd"),toDate.ToString("yyyy-MM-dd"),request.LocationId,username,requestedAt.ToString("O"),lines.Count,checksum),lines);
    }

    private static async Task<(string Artifact, string Checksum)?> GetControlledReportArtifactAsync(
        NpgsqlConnection connection,
        Guid runId,
        string reportKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select result_artifact, result_checksum
            from inventory_controlled_report_runs
            where run_id = @id and report_key = @reportKey;
            """;
        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("reportKey", reportKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0)
            ? (reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task RecordControlledReportExportAsync(
        NpgsqlConnection connection,
        Guid runId,
        string username,
        string checksum,
        CancellationToken cancellationToken)
    {
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            insert into inventory_controlled_report_exports (
              export_id, run_id, exported_by, exported_at, format, result_checksum)
            values (@export, @run, @user, now(), 'csv', @checksum);
            """;
        insert.Parameters.AddWithValue("export", Guid.NewGuid());
        insert.Parameters.AddWithValue("run", runId);
        insert.Parameters.AddWithValue("user", username);
        insert.Parameters.AddWithValue("checksum", checksum);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SavedReportDefinitionsResponse> GetSavedDefinitionsAsync(CancellationToken cancellationToken)
    {
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

    public async Task<string> GetGovernedFamilyCsvAsync(
        string family,
        DateOnly? from,
        DateOnly? to,
        GovernedReportDataScope scope,
        CancellationToken cancellationToken)
    {
        var key = family.Trim().ToLowerInvariant();
        if (!Families.Any(item => item.Key == key))
        {
            throw new ArgumentException("Unsupported report family.");
        }
        if (from is not null && to is not null && from > to)
        {
            throw new ArgumentException("From date cannot be after to date.");
        }
        if (scope.RowPolicy == "practice-wide")
        {
            return await GetFamilyCsvAsync(key, from, to, cancellationToken);
        }
        if (scope.RowPolicy is not ("facility-scoped" or "patient-assigned"))
        {
            throw new ArgumentException("Unsupported governed report row policy.");
        }
        if (scope.RowPolicy == "facility-scoped" && scope.FacilityId is null)
        {
            throw new ArgumentException("Facility-scoped execution requires a pinned facility.");
        }
        if (scope.RowPolicy == "patient-assigned" && key == "inventory")
        {
            throw new ArgumentException(
                "Inventory transactions do not have an approved patient-assignment relationship.");
        }
        if (key == "operational")
        {
            return await GetScopedOperationalReportsCsvAsync(
                from,
                to,
                scope,
                cancellationToken);
        }

        var scopePredicate = scope.RowPolicy == "facility-scoped"
            ? key switch
            {
                "patients" => "p.facility_id = @facility",
                "appointments" => "a.facility_id = @facility",
                "encounters" => "e.facility_id = @facility",
                "referrals" => "p.facility_id = @facility",
                "chart-tracker" => "p.facility_id = @facility",
                "inventory" => "l.facility_id = @facility",
                _ => throw new ArgumentException("Unsupported report family.")
            }
            : "p.canonical_id = any(@patientIds)";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = key switch
        {
            "patients" => $"""
                select p.canonical_id,
                       trim(concat(p.last_name, ', ', p.first_name)),
                       p.date_of_birth::text,
                       coalesce(p.phone_cell,p.phone_home,p.email,'')
                from patients p
                where p.merged_into_patient_id is null
                  and {scopePredicate}
                order by p.last_name,p.first_name
                limit 5000;
                """,
            "appointments" => $"""
                select a.id::text,
                       p.pubpid,
                       a.appointment_date::text,
                       concat(coalesce(a.title,''),' | ',coalesce(a.status,''))
                from appointments a
                join patients p on p.legacy_pid=a.pid
                where (@from is null or a.appointment_date>=@from)
                  and (@to is null or a.appointment_date<=@to)
                  and {scopePredicate}
                order by a.appointment_date,a.start_time
                limit 5000;
                """,
            "encounters" => $"""
                select e.encounter::text,
                       p.pubpid,
                       e.encounter_date::text,
                       coalesce(e.reason,'')
                from encounters e
                join patients p on p.legacy_pid=e.pid
                where (@from is null or e.encounter_date>=@from)
                  and (@to is null or e.encounter_date<=@to)
                  and {scopePredicate}
                order by e.encounter_date desc,e.encounter desc
                limit 5000;
                """,
            "referrals" => $"""
                select r.id::text,
                       p.pubpid,
                       r.requested_at::date::text,
                       concat(r.destination,' | ',r.status)
                from referrals r
                join patients p on p.canonical_id=r.patient_id
                where (@from is null or r.requested_at::date>=@from)
                  and (@to is null or r.requested_at::date<=@to)
                  and {scopePredicate}
                order by r.requested_at desc
                limit 5000;
                """,
            "chart-tracker" => $"""
                select event.id::text,
                       p.pubpid,
                       event.recorded_at::date::text,
                       coalesce(event.location, trim(concat(staff.first_name,' ',staff.last_name)),'')
                from chart_tracker_events event
                join patients p on p.canonical_id=event.patient_id
                left join staff on staff.id=event.user_id
                where (@from is null or event.recorded_at::date>=@from)
                  and (@to is null or event.recorded_at::date<=@to)
                  and {scopePredicate}
                order by event.recorded_at desc
                limit 5000;
                """,
            "inventory" => $"""
                select tx.transaction_id::text,
                       item.item_code,
                       tx.occurred_at::date::text,
                       concat(tx.transaction_type,' | ',tx.quantity_delta::text,' | ',coalesce(tx.reason,''))
                from inventory_transactions tx
                join inventory_lots l on l.lot_id=tx.lot_id
                join inventory_items item on item.item_id=l.item_id
                where (@from is null or tx.occurred_at::date>=@from)
                  and (@to is null or tx.occurred_at::date<=@to)
                  and {scopePredicate}
                order by tx.occurred_at desc
                limit 5000;
                """,
            _ => throw new ArgumentException("Unsupported report family.")
        };
        AddGovernedScopeParameters(command, from, to, scope);
        var csv = new StringBuilder();
        AppendCsvRow(csv, "Identifier", "Subject", "Date", "Detail");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            AppendCsvRow(
                csv,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3));
        }
        return csv.ToString();
    }

    private async Task<string> GetScopedOperationalReportsCsvAsync(
        DateOnly? from,
        DateOnly? to,
        GovernedReportDataScope scope,
        CancellationToken cancellationToken)
    {
        var patientPredicate = scope.RowPolicy == "facility-scoped"
            ? "patient.facility_id = @facility"
            : "patient.canonical_id = any(@patientIds)";
        var appointmentPredicate = scope.RowPolicy == "facility-scoped"
            ? "appointment.facility_id = @facility"
            : "patient.canonical_id = any(@patientIds)";
        var encounterPredicate = scope.RowPolicy == "facility-scoped"
            ? "encounter.facility_id = @facility"
            : "patient.canonical_id = any(@patientIds)";
        var inventoryRow = scope.RowPolicy == "facility-scoped"
            ? """
              union all
              select 'Scoped activity','Inventory transactions','Rows',count(*)::text
              from inventory_transactions tx
              join inventory_lots lot on lot.lot_id=tx.lot_id
              where (@from is null or tx.occurred_at::date>=@from)
                and (@to is null or tx.occurred_at::date<=@to)
                and lot.facility_id=@facility
              """
            : string.Empty;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select 'Scoped activity','Patients','Rows',count(*)::text
            from patients patient
            where patient.merged_into_patient_id is null
              and {patientPredicate}
            union all
            select 'Scoped activity','Appointments','Rows',count(*)::text
            from appointments appointment
            join patients patient on patient.legacy_pid=appointment.pid
            where (@from is null or appointment.appointment_date>=@from)
              and (@to is null or appointment.appointment_date<=@to)
              and {appointmentPredicate}
            union all
            select 'Scoped activity','Encounters','Rows',count(*)::text
            from encounters encounter
            join patients patient on patient.legacy_pid=encounter.pid
            where (@from is null or encounter.encounter_date>=@from)
              and (@to is null or encounter.encounter_date<=@to)
              and {encounterPredicate}
            union all
            select 'Scoped activity','Referrals','Rows',count(*)::text
            from referrals referral
            join patients patient on patient.canonical_id=referral.patient_id
            where (@from is null or referral.requested_at::date>=@from)
              and (@to is null or referral.requested_at::date<=@to)
              and {patientPredicate}
            union all
            select 'Scoped activity','Chart tracker','Rows',count(*)::text
            from chart_tracker_events event
            join patients patient on patient.canonical_id=event.patient_id
            where (@from is null or event.recorded_at::date>=@from)
              and (@to is null or event.recorded_at::date<=@to)
              and {patientPredicate}
            {inventoryRow}
            order by 2;
            """;
        AddGovernedScopeParameters(command, from, to, scope);
        var csv = new StringBuilder();
        AppendCsvRow(csv, "Section", "Name", "Metric", "Value");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            AppendCsvRow(
                csv,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3));
        }
        return csv.ToString();
    }

    private static void AddGovernedScopeParameters(
        NpgsqlCommand command,
        DateOnly? from,
        DateOnly? to,
        GovernedReportDataScope scope)
    {
        command.Parameters.Add("from", NpgsqlDbType.Date).Value =
            (object?)from ?? DBNull.Value;
        command.Parameters.Add("to", NpgsqlDbType.Date).Value =
            (object?)to ?? DBNull.Value;
        if (scope.RowPolicy == "facility-scoped")
        {
            command.Parameters.AddWithValue("facility", scope.FacilityId!.Value);
        }
        else
        {
            command.Parameters.Add("patientIds", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
                scope.PatientIds.ToArray();
        }
    }

    public async Task<string> GetFamilyCsvAsync(string family, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var key = family.Trim().ToLowerInvariant(); if (!Families.Any(item => item.Key == key)) throw new ArgumentException("Unsupported report family."); if (from is not null && to is not null && from > to) throw new ArgumentException("From date cannot be after to date."); if (key == "operational") return await GetOperationalReportsCsvAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
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
