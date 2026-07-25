using System.Globalization;
using System.Text.Json;
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

    public async Task<FhirObservationResource?> GetObservationAsync(int observationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ObservationSelectSql + " where lrs.id = @id limit 1;";
        command.Parameters.AddWithValue("id", observationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadObservation(reader) : null;
    }

    public async Task<FhirObservationBundle> SearchObservationsAsync(string? subject, int? count, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(count ?? 20, 1, MaximumSearchLimit);
        var normalizedSubject = NormalizePatientReference(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            select count(*)
            from lab_results lrs
            inner join lab_reports lr on lr.id = lrs.report_id
            inner join lab_orders lo on lo.id = lr.order_id
            inner join patients p on p.legacy_pid = lo.pid
            where (@subject is null or p.canonical_id = @subject or p.pubpid = @subject);
            """;
        countCommand.Parameters.AddWithValue("subject", (object?)normalizedSubject ?? DBNull.Value);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = ObservationSelectSql + """
             where (@subject is null or p.canonical_id = @subject or p.pubpid = @subject)
             order by lrs.result_date desc, lrs.id desc
             limit @limit;
            """;
        command.Parameters.AddWithValue("subject", (object?)normalizedSubject ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);
        var entries = new List<FhirObservationSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var observation = ReadObservation(reader);
            entries.Add(new FhirObservationSearchEntry($"Observation/{observation.Id}", observation));
        }
        return new FhirObservationBundle("Bundle", "searchset", total, entries);
    }

    public async Task<FhirObservationBundle> SearchSdohObservationsAsync(string? subject, int? count, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(count ?? 20, 1, MaximumSearchLimit);
        var normalizedSubject = NormalizePatientReference(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select assessment_id::text, patient_id, assessment_date, domains::text
            from patient_sdoh_assessments
            where (@subject is null or patient_id = @subject or patient_id in (select canonical_id from patients where pubpid = @subject))
            order by assessment_date desc, updated_at desc, assessment_id desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("subject", (object?)normalizedSubject ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);
        var entries = new List<FhirObservationSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var assessmentId = reader.GetString(0);
            var patientId = reader.GetString(1);
            var effectiveDate = reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var domains = JsonSerializer.Deserialize<Dictionary<string, PatientSdohDomainValue>>(reader.GetString(3)) ?? [];
            foreach (var (domain, value) in domains.Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Status)))
            {
                var observation = new FhirObservationResource(
                    "Observation", $"sdoh-{assessmentId}-{domain}", "final",
                    [new FhirCodeableConcept([new FhirCoding("http://terminology.hl7.org/CodeSystem/observation-category", "social-history", "Social History")], "Social History")],
                    new FhirCodeableConcept([new FhirCoding("urn:legacy-ehr:sdoh-domain", domain, ToSdohDomainDisplay(domain))], ToSdohDomainDisplay(domain)),
                    new FhirReference($"Patient/{patientId}"), $"{effectiveDate}T00:00:00", null, value.Status,
                    string.IsNullOrWhiteSpace(value.Notes) ? [] : [new FhirObservationReferenceRange(value.Notes)], []);
                entries.Add(new FhirObservationSearchEntry($"Observation/{observation.Id}", observation));
            }
        }
        return new FhirObservationBundle("Bundle", "searchset", entries.Count, entries);
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

    private const string ObservationSelectSql = """
        select lrs.id, p.canonical_id, lrs.result_status, lrs.code, lrs.text, lrs.result,
               lrs.units, lrs.range, lrs.abnormal, lrs.result_date
        from lab_results lrs
        inner join lab_reports lr on lr.id = lrs.report_id
        inner join lab_orders lo on lo.id = lr.order_id
        inner join patients p on p.legacy_pid = lo.pid
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

    private static FhirObservationResource ReadObservation(NpgsqlDataReader reader)
    {
        var result = ReadNullableString(reader, 5);
        var unit = ReadNullableString(reader, 6);
        var code = ReadNullableString(reader, 3);
        var text = ReadNullableString(reader, 4);
        var valueQuantity = decimal.TryParse(result, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericResult)
            ? new FhirQuantity(numericResult, unit)
            : null;
        var referenceRange = ReadNullableString(reader, 7);
        var abnormal = ReadNullableString(reader, 8);
        IReadOnlyList<FhirCoding> coding = string.IsNullOrWhiteSpace(code)
            ? []
            : [new FhirCoding("urn:legacy-ehr:procedure-result", code, text)];
        IReadOnlyList<FhirCodeableConcept> interpretation = string.IsNullOrWhiteSpace(abnormal)
            ? []
            : [new FhirCodeableConcept([new FhirCoding("urn:legacy-ehr:abnormal-flag", abnormal, abnormal)], abnormal)];
        return new FhirObservationResource(
            "Observation",
            reader.GetInt32(0).ToString(CultureInfo.InvariantCulture),
            ToFhirObservationStatus(ReadNullableString(reader, 2)),
            [new FhirCodeableConcept([new FhirCoding("http://terminology.hl7.org/CodeSystem/observation-category", "laboratory", "Laboratory")], "Laboratory")],
            new FhirCodeableConcept(coding, text ?? code ?? "Laboratory result"),
            new FhirReference($"Patient/{reader.GetString(1)}"),
            reader.GetDateTime(9).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            valueQuantity,
            valueQuantity is null ? result : null,
            string.IsNullOrWhiteSpace(referenceRange) ? [] : [new FhirObservationReferenceRange(referenceRange)],
            interpretation);
    }

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

    private static string ToFhirObservationStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "final" or "completed" or "reviewed" => "final",
        "preliminary" or "prelim" => "preliminary",
        "corrected" or "amended" => "corrected",
        "cancelled" or "canceled" => "cancelled",
        "entered-in-error" => "entered-in-error",
        _ => "unknown",
    };

    private static string? NormalizePatientReference(string? subject)
    {
        var normalized = subject?.Trim();
        return normalized?.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase) is true
            ? normalized["Patient/".Length..]
            : normalized;
    }

    private static string ToSdohDomainDisplay(string domain) => domain.Replace('_', ' ') switch
    {
        "food insecurity" => "Food insecurity",
        "housing instability" => "Housing instability",
        "transportation insecurity" => "Transportation insecurity",
        "utilities insecurity" => "Utilities insecurity",
        "interpersonal safety" => "Interpersonal safety",
        "financial strain" => "Financial strain",
        "social isolation" => "Social isolation",
        "childcare needs" => "Childcare needs",
        "digital access" => "Digital access",
        _ => domain.Replace('_', ' ')
    };
}
