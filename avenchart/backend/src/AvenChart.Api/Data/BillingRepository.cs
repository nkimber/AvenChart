using System.Data.Common;
using Npgsql;
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
            select id, encounter, billing_date, code_type, code, code_text, fee, justify
            from billing
            where pid = @pid
              and encounter = any(@encounters)
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
                CodeText: ReadNullableString(reader, "code_text"),
                Fee: ReadNullableDecimal(reader, "fee"),
                Justify: ReadNullableString(reader, "justify")));
        }

        return items;
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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record BillingPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName);
}
