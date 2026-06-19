using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class BillingRepository(NpgsqlDataSource dataSource)
{
    public async Task<PatientBillingResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var encounters = await GetBillingEncountersAsync(connection, patient.LegacyPid, cancellationToken);
        var lines = await GetBillingLinesAsync(connection, patient.LegacyPid, encounters.Select(encounter => encounter.Encounter).ToArray(), cancellationToken);
        var linesByEncounter = lines.GroupBy(line => line.Encounter).ToDictionary(group => group.Key, group => group.ToList());

        return new PatientBillingResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Encounters: encounters.Select(encounter =>
            {
                var encounterLines = linesByEncounter.GetValueOrDefault(encounter.Encounter, []);
                return encounter with
                {
                    TotalFee = encounterLines.Sum(line => line.Fee ?? 0m),
                    Lines = encounterLines
                };
            }).ToList());
    }

    public async Task<BillingLineMutationResponse?> CreateLineAsync(
        BillingLineCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.CodeType)
            || string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || string.IsNullOrWhiteSpace(request.Justify)
            || request.Encounter <= 0
            || request.Fee < 0
            || request.Units <= 0
            || !TryReadDate(request.BillingDate, out var billingDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, request.Encounter, cancellationToken);
        if (encounter is null)
        {
            return null;
        }

        var id = $"BILL-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into billing
                (id, pid, provider_id, encounter, billing_date, code_type, code, code_text,
                 modifier, fee, justify, units, billed, activity)
            values
                (@id, @pid, @providerId, @encounter, @billingDate, @codeType, @code, @codeText,
                 @modifier, @fee, @justify, @units, 0, 1);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("providerId", request.ProviderId ?? encounter.ProviderId);
        command.Parameters.AddWithValue("encounter", encounter.Encounter);
        command.Parameters.Add("billingDate", NpgsqlDbType.Date).Value = billingDate;
        command.Parameters.AddWithValue("codeType", request.CodeType.Trim());
        command.Parameters.AddWithValue("code", request.Code.Trim());
        command.Parameters.AddWithValue("codeText", request.CodeText.Trim());
        command.Parameters.AddWithValue("modifier", NormalizeText(request.Modifier) ?? string.Empty);
        command.Parameters.AddWithValue("fee", request.Fee);
        command.Parameters.AddWithValue("justify", request.Justify.Trim());
        command.Parameters.AddWithValue("units", request.Units);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var billing = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(id, billing);
    }

    public async Task<BillingLineMutationResponse?> UpdateLineStatusAsync(
        string billingLineId,
        BillingLineStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId)
            || !IsBinaryStatus(request.Billed)
            || !IsBinaryStatus(request.Activity))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update billing
                set billed = @billed,
                    activity = @activity
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", billingLineId);
            command.Parameters.AddWithValue("billed", request.Billed);
            command.Parameters.AddWithValue("activity", request.Activity);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<BillingLineMutationResponse?> UpdateLineAsync(
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || string.IsNullOrWhiteSpace(request.Justify)
            || request.Fee < 0
            || request.Units <= 0)
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update billing
                set code_text = @codeText,
                    modifier = @modifier,
                    fee = @fee,
                    units = @units,
                    justify = @justify
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", billingLineId);
            command.Parameters.AddWithValue("codeText", request.CodeText.Trim());
            command.Parameters.AddWithValue("modifier", NormalizeText(request.Modifier) ?? string.Empty);
            command.Parameters.AddWithValue("fee", request.Fee);
            command.Parameters.AddWithValue("units", request.Units);
            command.Parameters.AddWithValue("justify", request.Justify.Trim());
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<bool> DeleteLineAsync(string billingLineId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from billing
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", billingLineId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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

    private static async Task<BillingPatient?> GetPatientAsync(
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

        return new BillingPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<IReadOnlyList<BillingEncounterItem>> GetBillingEncountersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                e.id,
                e.encounter,
                e.encounter_date,
                e.reason,
                e.diagnosis_code,
                e.diagnosis_text,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name
            from encounters e
            left join staff s on s.id = e.provider_id
            left join facilities f on f.id = e.facility_id
            where e.pid = @pid
              and exists (select 1 from billing b where b.pid = e.pid and b.encounter = e.encounter)
            order by e.encounter_date desc, e.encounter desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<BillingEncounterItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingEncounterItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("encounter_date")).ToString("yyyy-MM-dd"),
                Reason: ReadNullableString(reader, "reason"),
                DiagnosisCode: ReadNullableString(reader, "diagnosis_code"),
                DiagnosisText: ReadNullableString(reader, "diagnosis_text"),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                TotalFee: 0m,
                Lines: []));
        }

        return items;
    }

    private static async Task<IReadOnlyList<BillingLineItem>> GetBillingLinesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        IReadOnlyList<int> encounters,
        CancellationToken cancellationToken)
    {
        if (encounters.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, billing_date, code_type, code, modifier, code_text, fee, justify, units, billed, activity
            from billing
            where pid = @pid
              and encounter = any(@encounters)
              and activity = 1
            order by encounter desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounters", encounters.ToArray());

        var items = new List<BillingLineItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingLineItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                BillingDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("billing_date")).ToString("yyyy-MM-dd"),
                CodeType: ReadNullableString(reader, "code_type"),
                Code: ReadNullableString(reader, "code"),
                Modifier: ReadNullableString(reader, "modifier"),
                CodeText: ReadNullableString(reader, "code_text"),
                Fee: ReadNullableDecimal(reader, "fee"),
                Justify: ReadNullableString(reader, "justify"),
                Units: ReadInt(reader, "units"),
                Billed: ReadInt(reader, "billed"),
                Activity: ReadInt(reader, "activity")));
        }

        return items;
    }

    private static async Task<BillingEncounterMutationContext?> GetEncounterForPatientAsync(
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

        return new BillingEncounterMutationContext(
            Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
            ProviderId: reader.GetInt32(reader.GetOrdinal("provider_id")));
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        return reader.GetInt32(reader.GetOrdinal(columnName));
    }

    private static bool TryReadDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", out date)
            || DateOnly.TryParse(value, out date);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsBinaryStatus(int value)
    {
        return value is 0 or 1;
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record BillingPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName);

    private sealed record BillingEncounterMutationContext(
        int Encounter,
        int ProviderId);
}
