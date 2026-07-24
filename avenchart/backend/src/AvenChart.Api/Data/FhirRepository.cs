using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class FhirRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;

    public async Task<FhirPatientResource?> GetPatientAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PatientSelectSql + " where p.canonical_id = @id or p.pubpid = @id limit 1;";
        command.Parameters.AddWithValue("id", id.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
    }

    public async Task<FhirSearchBundle> SearchPatientsAsync(string? name, string? identifier, int? count, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(count ?? 20, 1, MaximumSearchLimit);
        var normalizedName = name?.Trim();
        var normalizedIdentifier = identifier?.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"select count(*) from patients p where {SearchPredicate};";
        AddSearchParameters(countCommand, normalizedName, normalizedIdentifier, limit);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = PatientSelectSql + $" where {SearchPredicate} order by p.last_name, p.first_name, p.canonical_id limit @limit;";
        AddSearchParameters(command, normalizedName, normalizedIdentifier, limit);
        var entries = new List<FhirSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var patient = ReadPatient(reader);
            entries.Add(new FhirSearchEntry($"Patient/{patient.Id}", patient));
        }
        return new FhirSearchBundle("Bundle", "searchset", total, entries);
    }

    public async Task<FhirEncounterResource?> GetEncounterAsync(int encounterId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = EncounterSelectSql + " where e.encounter = @encounter limit 1;";
        command.Parameters.AddWithValue("encounter", encounterId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEncounter(reader) : null;
    }

    public async Task<FhirEncounterBundle> SearchEncountersAsync(string? subject, int? count, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(count ?? 20, 1, MaximumSearchLimit);
        var normalizedSubject = subject?.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "select count(*) from encounters e join patients p on p.legacy_pid = e.pid where (@subject is null or p.canonical_id = @subject or p.pubpid = @subject);";
        countCommand.Parameters.AddWithValue("subject", (object?)normalizedSubject ?? DBNull.Value);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var command = connection.CreateCommand();
        command.CommandText = EncounterSelectSql + " where (@subject is null or p.canonical_id = @subject or p.pubpid = @subject) order by e.encounter_date desc, e.encounter desc limit @limit;";
        command.Parameters.AddWithValue("subject", (object?)normalizedSubject ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);
        var entries = new List<FhirEncounterSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { var encounter = ReadEncounter(reader); entries.Add(new FhirEncounterSearchEntry($"Encounter/{encounter.Id}", encounter)); }
        return new FhirEncounterBundle("Bundle", "searchset", total, entries);
    }

    private const string SearchPredicate = """
        (@name is null or lower(concat(p.first_name, ' ', p.last_name)) like '%' || lower(@name) || '%')
        and (@identifier is null or p.canonical_id = @identifier or p.pubpid = @identifier)
        """;

    private const string PatientSelectSql = """
        select p.canonical_id, p.pubpid, p.first_name, p.last_name, p.preferred_name, p.sex, p.date_of_birth,
          p.phone, p.phone_home, p.phone_cell, p.email, p.street, p.city, p.state, p.postal_code
        from patients p
        """;

    private const string EncounterSelectSql = """
        select e.encounter, p.canonical_id, e.encounter_date, e.reason
        from encounters e join patients p on p.legacy_pid = e.pid
        """;

    private static void AddSearchParameters(NpgsqlCommand command, string? name, string? identifier, int limit)
    {
        command.Parameters.AddWithValue("name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("identifier", (object?)identifier ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);
    }

    private static FhirPatientResource ReadPatient(NpgsqlDataReader reader)
    {
        var id = reader.GetString(0);
        var telecom = new List<FhirContactPoint>();
        AddTelecom(telecom, "phone", ReadNullableString(reader, 7), "mobile");
        AddTelecom(telecom, "phone", ReadNullableString(reader, 8), "home");
        AddTelecom(telecom, "phone", ReadNullableString(reader, 9), "mobile");
        AddTelecom(telecom, "email", ReadNullableString(reader, 10), "home");
        var street = ReadNullableString(reader, 11);
        IReadOnlyList<FhirAddress> address = string.IsNullOrWhiteSpace(street)
            ? []
            : [new FhirAddress("home", [street], ReadNullableString(reader, 12), ReadNullableString(reader, 13), ReadNullableString(reader, 14))];
        var preferred = ReadNullableString(reader, 4);
        IReadOnlyList<string> given = string.IsNullOrWhiteSpace(preferred)
            ? [reader.GetString(2)]
            : [reader.GetString(2), preferred];
        return new FhirPatientResource(
            "Patient",
            id,
            [new FhirIdentifier("urn:legacy-ehr:canonical-id", id), new FhirIdentifier("urn:legacy-ehr:pubpid", reader.GetString(1))],
            [new FhirHumanName("official", reader.GetString(3), given)],
            ToFhirGender(ReadNullableString(reader, 5)),
            reader.GetFieldValue<DateOnly>(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            telecom,
            address);
    }

    private static FhirEncounterResource ReadEncounter(NpgsqlDataReader reader) => new(
        "Encounter", reader.GetInt32(0).ToString(CultureInfo.InvariantCulture), "finished",
        new FhirReference($"Patient/{reader.GetString(1)}"),
        new FhirPeriod(reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        ReadNullableString(reader, 3));

    private static void AddTelecom(ICollection<FhirContactPoint> telecom, string system, string? value, string use)
    {
        if (!string.IsNullOrWhiteSpace(value)) telecom.Add(new FhirContactPoint(system, value, use));
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? ToFhirGender(string? sex) => sex?.Trim().ToLowerInvariant() switch
    {
        "male" or "m" => "male",
        "female" or "f" => "female",
        "other" or "o" => "other",
        _ => "unknown",
    };
}
