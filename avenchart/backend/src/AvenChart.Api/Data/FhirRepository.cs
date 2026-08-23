// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class FhirRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;

    public async Task<FhirPatientResource?> GetPatientAsync(
        string id,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = PatientSelectSql + " where p.facility_id = @facility and (p.canonical_id = @id or p.pubpid = @id) limit 1;";
        command.Parameters.AddWithValue("id", id.Trim());
        AddFacilityParameter(command, facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
    }

    public async Task<FhirSearchBundle> SearchPatientsAsync(
        string? name,
        string? identifier,
        int? count,
        int? page,
        string fhirBaseUrl,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var searchPage = ResolveSearchPage(count, page);
        var normalizedName = name?.Trim();
        var normalizedIdentifier = identifier?.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"select count(*) from patients p where p.facility_id = @facility and ({SearchPredicate});";
        AddPatientSearchParameters(countCommand, normalizedName, normalizedIdentifier, searchPage);
        AddFacilityParameter(countCommand, facilityId);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = PatientSelectSql + $" where p.facility_id = @facility and ({SearchPredicate}) order by p.last_name, p.first_name, p.canonical_id limit @limit offset @offset;";
        AddPatientSearchParameters(command, normalizedName, normalizedIdentifier, searchPage);
        AddFacilityParameter(command, facilityId);
        var entries = new List<FhirSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var patient = ReadPatient(reader);
            entries.Add(new FhirSearchEntry(BuildResourceUrl(fhirBaseUrl, "Patient", patient.Id), patient));
        }
        return new FhirSearchBundle(
            "Bundle",
            "searchset",
            total,
            BuildSearchLinks(fhirBaseUrl, "Patient", [("name", normalizedName), ("identifier", normalizedIdentifier)], searchPage, total),
            entries.Count == 0 ? null : entries);
    }

    public async Task<FhirEncounterResource?> GetEncounterAsync(
        int encounterId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = EncounterSelectSql + " where e.encounter = @encounter and p.facility_id = @facility limit 1;";
        command.Parameters.AddWithValue("encounter", encounterId);
        AddFacilityParameter(command, facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEncounter(reader) : null;
    }

    public async Task<FhirEncounterBundle> SearchEncountersAsync(
        string? subject,
        int? count,
        int? page,
        string fhirBaseUrl,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var searchPage = ResolveSearchPage(count, page);
        var normalizedSubject = NormalizePatientReference(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "select count(*) from encounters e join patients p on p.legacy_pid = e.pid where p.facility_id = @facility and (@subject is null or p.canonical_id = @subject or p.pubpid = @subject);";
        AddSubjectParameter(countCommand, normalizedSubject);
        AddFacilityParameter(countCommand, facilityId);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var command = connection.CreateCommand();
        command.CommandText = EncounterSelectSql + " where p.facility_id = @facility and (@subject is null or p.canonical_id = @subject or p.pubpid = @subject) order by e.encounter_date desc, e.encounter desc limit @limit offset @offset;";
        AddSubjectParameter(command, normalizedSubject);
        AddFacilityParameter(command, facilityId);
        AddSearchPageParameters(command, searchPage);
        var entries = new List<FhirEncounterSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var encounter = ReadEncounter(reader);
            entries.Add(new FhirEncounterSearchEntry(BuildResourceUrl(fhirBaseUrl, "Encounter", encounter.Id), encounter));
        }
        return new FhirEncounterBundle(
            "Bundle",
            "searchset",
            total,
            BuildSearchLinks(fhirBaseUrl, "Encounter", [("subject", normalizedSubject)], searchPage, total),
            entries.Count == 0 ? null : entries);
    }

    public async Task<FhirObservationResource?> GetObservationAsync(
        int observationId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ObservationSelectSql + " where lrs.id = @id and p.facility_id = @facility limit 1;";
        command.Parameters.AddWithValue("id", observationId);
        AddFacilityParameter(command, facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadObservation(reader) : null;
    }

    public async Task<FhirObservationBundle> SearchObservationsAsync(
        string? subject,
        int? count,
        int? page,
        string fhirBaseUrl,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var searchPage = ResolveSearchPage(count, page);
        var normalizedSubject = NormalizePatientReference(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            select count(*)
            from lab_results lrs
            inner join lab_reports lr on lr.id = lrs.report_id
            inner join lab_orders lo on lo.id = lr.order_id
            inner join patients p on p.legacy_pid = lo.pid
            where p.facility_id = @facility
              and (@subject is null or p.canonical_id = @subject or p.pubpid = @subject);
            """;
        AddSubjectParameter(countCommand, normalizedSubject);
        AddFacilityParameter(countCommand, facilityId);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = ObservationSelectSql + """
             where p.facility_id = @facility
               and (@subject is null or p.canonical_id = @subject or p.pubpid = @subject)
             order by lrs.result_date desc, lrs.id desc
             limit @limit offset @offset;
            """;
        AddSubjectParameter(command, normalizedSubject);
        AddFacilityParameter(command, facilityId);
        AddSearchPageParameters(command, searchPage);
        var entries = new List<FhirObservationSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var observation = ReadObservation(reader);
            entries.Add(new FhirObservationSearchEntry(BuildResourceUrl(fhirBaseUrl, "Observation", observation.Id), observation));
        }
        return new FhirObservationBundle(
            "Bundle",
            "searchset",
            total,
            BuildSearchLinks(fhirBaseUrl, "Observation", [("subject", normalizedSubject)], searchPage, total),
            entries.Count == 0 ? null : entries);
    }

    public async Task<FhirObservationBundle> SearchSdohObservationsAsync(
        string? subject,
        int? count,
        int? page,
        string fhirBaseUrl,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var searchPage = ResolveSearchPage(count, page);
        var normalizedSubject = NormalizePatientReference(subject);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            select count(*)
            from patient_sdoh_assessments assessment
            inner join patients p on p.canonical_id = assessment.patient_id
            cross join lateral jsonb_each(assessment.domains) domain
            where p.facility_id = @facility
              and (@subject is null
                   or assessment.patient_id = @subject
                   or assessment.patient_id in (select canonical_id from patients where pubpid = @subject))
              and coalesce(
                    nullif(trim(domain.value ->> 'status'), ''),
                    nullif(trim(domain.value ->> 'Status'), '')) is not null;
            """;
        AddSubjectParameter(countCommand, normalizedSubject);
        AddFacilityParameter(countCommand, facilityId);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select assessment.assessment_id::text,
                   assessment.patient_id,
                   assessment.assessment_date,
                   domain.key,
                   coalesce(domain.value ->> 'status', domain.value ->> 'Status') as status,
                   coalesce(domain.value ->> 'notes', domain.value ->> 'Notes') as notes
            from patient_sdoh_assessments assessment
            inner join patients p on p.canonical_id = assessment.patient_id
            cross join lateral jsonb_each(assessment.domains) domain
            where p.facility_id = @facility
              and (@subject is null
                   or assessment.patient_id = @subject
                   or assessment.patient_id in (select canonical_id from patients where pubpid = @subject))
              and coalesce(
                    nullif(trim(domain.value ->> 'status'), ''),
                    nullif(trim(domain.value ->> 'Status'), '')) is not null
            order by assessment.assessment_date desc, assessment.updated_at desc, assessment.assessment_id desc, domain.key
            limit @limit offset @offset;
            """;
        AddSubjectParameter(command, normalizedSubject);
        AddFacilityParameter(command, facilityId);
        AddSearchPageParameters(command, searchPage);
        var entries = new List<FhirObservationSearchEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var assessmentId = reader.GetString(0);
            var patientId = reader.GetString(1);
            var effectiveDate = reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var domain = reader.GetString(3);
            var status = reader.GetString(4);
            var notes = ReadNullableString(reader, 5);
            var observation = new FhirObservationResource(
                "Observation", $"sdoh-{assessmentId}-{domain}", "final",
                [new FhirCodeableConcept([new FhirCoding("http://terminology.hl7.org/CodeSystem/observation-category", "social-history", "Social History")], "Social History")],
                new FhirCodeableConcept([new FhirCoding("urn:avenchart:sdoh-domain", domain, ToSdohDomainDisplay(domain))], ToSdohDomainDisplay(domain)),
                new FhirReference($"Patient/{patientId}"), $"{effectiveDate}T00:00:00", null, status,
                string.IsNullOrWhiteSpace(notes) ? null : [new FhirObservationReferenceRange(notes)], null);
            entries.Add(new FhirObservationSearchEntry(BuildResourceUrl(fhirBaseUrl, "Observation", observation.Id), observation));
        }
        return new FhirObservationBundle(
            "Bundle",
            "searchset",
            total,
            BuildSearchLinks(fhirBaseUrl, "Observation/sdoh", [("subject", normalizedSubject)], searchPage, total),
            entries.Count == 0 ? null : entries);
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

    private static void AddPatientSearchParameters(
        NpgsqlCommand command,
        string? name,
        string? identifier,
        FhirSearchPage searchPage)
    {
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = (object?)name ?? DBNull.Value;
        command.Parameters.Add("identifier", NpgsqlDbType.Text).Value = (object?)identifier ?? DBNull.Value;
        AddSearchPageParameters(command, searchPage);
    }

    private static void AddSubjectParameter(NpgsqlCommand command, string? subject)
    {
        command.Parameters.Add("subject", NpgsqlDbType.Text).Value = (object?)subject ?? DBNull.Value;
    }

    private static void AddFacilityParameter(NpgsqlCommand command, int facilityId)
    {
        if (facilityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        }

        command.Parameters.AddWithValue("facility", facilityId);
    }

    private static void AddSearchPageParameters(NpgsqlCommand command, FhirSearchPage searchPage)
    {
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = searchPage.Limit;
        command.Parameters.Add("offset", NpgsqlDbType.Integer).Value = searchPage.Offset;
    }

    private static FhirSearchPage ResolveSearchPage(int? count, int? page)
    {
        var limit = Math.Clamp(count ?? 20, 1, MaximumSearchLimit);
        var maximumPage = Math.Max(1, int.MaxValue / limit);
        var number = Math.Clamp(page ?? 1, 1, maximumPage);
        return new FhirSearchPage(limit, number, (number - 1) * limit);
    }

    private static IReadOnlyList<FhirBundleLink> BuildSearchLinks(
        string fhirBaseUrl,
        string resourcePath,
        IReadOnlyList<(string Name, string? Value)> searchParameters,
        FhirSearchPage searchPage,
        int total)
    {
        var links = new List<FhirBundleLink>();
        var baseResourceUrl = $"{fhirBaseUrl.TrimEnd('/')}/{resourcePath}";
        links.Add(new FhirBundleLink(
            "self",
            BuildSearchUrl(baseResourceUrl, searchParameters, searchPage.Limit, searchPage.Page)));

        if (searchPage.Page > 1)
        {
            links.Add(new FhirBundleLink(
                "previous",
                BuildSearchUrl(baseResourceUrl, searchParameters, searchPage.Limit, searchPage.Page - 1)));
        }

        if ((long)searchPage.Offset + searchPage.Limit < total)
        {
            links.Add(new FhirBundleLink(
                "next",
                BuildSearchUrl(baseResourceUrl, searchParameters, searchPage.Limit, searchPage.Page + 1)));
        }

        return links;
    }

    private static string BuildSearchUrl(
        string baseResourceUrl,
        IReadOnlyList<(string Name, string? Value)> searchParameters,
        int count,
        int page)
    {
        var parameters = searchParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value!)}")
            .Append($"_count={count.ToString(CultureInfo.InvariantCulture)}")
            .Append($"page={page.ToString(CultureInfo.InvariantCulture)}");
        return $"{baseResourceUrl}?{string.Join("&", parameters)}";
    }

    private static string BuildResourceUrl(string fhirBaseUrl, string resourceType, string id) =>
        $"{fhirBaseUrl.TrimEnd('/')}/{resourceType}/{Uri.EscapeDataString(id)}";

    private sealed record FhirSearchPage(int Limit, int Page, int Offset);

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
            [new FhirIdentifier("urn:avenchart:canonical-id", id), new FhirIdentifier("urn:avenchart:pubpid", reader.GetString(1))],
            [new FhirHumanName("official", reader.GetString(3), given)],
            ToFhirGender(ReadNullableString(reader, 5)),
            reader.GetFieldValue<DateOnly>(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            telecom.Count == 0 ? null : telecom,
            address.Count == 0 ? null : address);
    }

    private static FhirEncounterResource ReadEncounter(NpgsqlDataReader reader)
    {
        var reason = ReadNullableString(reader, 3);
        return new FhirEncounterResource(
            "Encounter",
            reader.GetInt32(0).ToString(CultureInfo.InvariantCulture),
            "finished",
            new FhirCoding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB", "ambulatory"),
            new FhirReference($"Patient/{reader.GetString(1)}"),
            new FhirPeriod(reader.GetFieldValue<DateOnly>(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            string.IsNullOrWhiteSpace(reason) ? null : [new FhirCodeableConcept(null, reason)]);
    }

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
        IReadOnlyList<FhirCoding>? coding = string.IsNullOrWhiteSpace(code)
            ? null
            : [new FhirCoding("urn:avenchart:procedure-result", code, text)];
        IReadOnlyList<FhirCodeableConcept>? interpretation = string.IsNullOrWhiteSpace(abnormal)
            ? null
            : [new FhirCodeableConcept([new FhirCoding("urn:avenchart:abnormal-flag", abnormal, abnormal)], abnormal)];
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
            string.IsNullOrWhiteSpace(referenceRange) ? null : [new FhirObservationReferenceRange(referenceRange)],
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
