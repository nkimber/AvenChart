using System.Data.Common;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ClinicalListRepository(NpgsqlDataSource dataSource)
{
    public async Task<ClinicalListsResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var problems = await GetProblemsAsync(connection, patient.LegacyPid, cancellationToken);
        var allergies = await GetAllergiesAsync(connection, patient.LegacyPid, cancellationToken);
        var medications = await GetMedicationsAsync(connection, patient.LegacyPid, cancellationToken);
        var prescriptions = await GetPrescriptionsAsync(connection, patient.LegacyPid, cancellationToken);

        return new ClinicalListsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Problems: problems,
            Allergies: allergies,
            Medications: medications,
            Prescriptions: prescriptions);
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

    private static async Task<ClinicalListPatient?> GetPatientAsync(
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

        return new ClinicalListPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<IReadOnlyList<ProblemListItem>> GetProblemsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, diagnosis, problem_date, comments
            from problems
            where pid = @pid
            order by problem_date desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<ProblemListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ProblemListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                Date: ReadNullableDate(reader, "problem_date"),
                Comments: ReadNullableString(reader, "comments")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<AllergyListItem>> GetAllergiesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, reaction, severity, allergy_date, comments
            from allergies
            where pid = @pid
            order by allergy_date desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<AllergyListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AllergyListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Reaction: ReadNullableString(reader, "reaction"),
                Severity: ReadNullableString(reader, "severity"),
                Date: ReadNullableDate(reader, "allergy_date"),
                Comments: ReadNullableString(reader, "comments")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<MedicationListItem>> GetMedicationsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, diagnosis, medication_date, comments
            from medications
            where pid = @pid
            order by medication_date desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<MedicationListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MedicationListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                Date: ReadNullableDate(reader, "medication_date"),
                Comments: ReadNullableString(reader, "comments")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PrescriptionListItem>> GetPrescriptionsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                pr.id,
                pr.drug,
                pr.dosage,
                pr.route,
                pr.diagnosis,
                pr.start_date,
                pr.encounter,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name
            from prescriptions pr
            left join staff s on s.id = pr.provider_id
            where pr.pid = @pid
            order by pr.start_date desc, pr.id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PrescriptionListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PrescriptionListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                Dosage: ReadNullableString(reader, "dosage"),
                Route: ReadNullableString(reader, "route"),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                StartDate: ReadNullableDate(reader, "start_date"),
                Encounter: ReadNullableInt(reader, "encounter"),
                ProviderName: ReadNullableString(reader, "provider_name")));
        }

        return items;
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

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record ClinicalListPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName);
}
