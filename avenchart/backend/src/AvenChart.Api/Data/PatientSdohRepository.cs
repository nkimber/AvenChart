using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientSdohRepository(NpgsqlDataSource dataSource)
{
    private static readonly HashSet<string> SupportedDomainKeys = new(StringComparer.Ordinal)
    {
        "food_insecurity", "housing_instability", "transportation_insecurity", "utilities_insecurity",
        "interpersonal_safety", "financial_strain", "social_isolation", "childcare_needs", "digital_access",
        "disability_status", "employment_status", "education_level", "caregiver_status", "veteran_status",
        "pregnancy_status", "postpartum_status"
    };

    private static readonly HashSet<string> PositiveStatuses = new(StringComparer.Ordinal)
    {
        "yes", "at_risk", "positive", "often", "sometimes", "yes_med", "yes_nonmed",
        "already_off", "very_hard", "hard", "somewhat_hard"
    };

    public async Task<IReadOnlyList<PatientSdohAssessmentResponse>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                   domains, interventions, created_at, created_by, updated_at, updated_by
            from patient_sdoh_assessments
            where patient_id = @patientId
            order by assessment_date desc, updated_at desc, assessment_id desc;
            """;
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assessments = new List<PatientSdohAssessmentResponse>();
        while (await reader.ReadAsync(cancellationToken)) assessments.Add(ToResponse(reader));
        return assessments;
    }

    public async Task<PatientSdohAssessmentResponse> CreateAsync(string patientId, PatientSdohAssessmentRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var normalized = Normalize(request, username);
        var assessmentId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patient_sdoh_assessments (
                assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                domains, interventions, created_at, created_by, updated_at, updated_by)
            values (
                @assessmentId, @patientId, @pid, @assessmentDate, @screeningTool, @assessor, @instrumentScore,
                @domains, @interventions, now(), @username, now(), @username)
            returning assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                      domains, interventions, created_at, created_by, updated_at, updated_by;
            """;
        AddMutationParameters(command, assessmentId, patient, normalized, username);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("The SDOH assessment could not be created.");
        return ToResponse(reader);
    }

    public async Task<PatientSdohAssessmentResponse> UpdateAsync(string patientId, Guid assessmentId, PatientSdohAssessmentRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var normalized = Normalize(request, username);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_sdoh_assessments
            set assessment_date = @assessmentDate, screening_tool = @screeningTool, assessor = @assessor,
                instrument_score = @instrumentScore, domains = @domains, interventions = @interventions,
                updated_at = now(), updated_by = @username
            where assessment_id = @assessmentId and patient_id = @patientId
            returning assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                      domains, interventions, created_at, created_by, updated_at, updated_by;
            """;
        AddMutationParameters(command, assessmentId, patient, normalized, username);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The SDOH assessment does not exist for this patient.");
        return ToResponse(reader);
    }

    private static void AddMutationParameters(NpgsqlCommand command, Guid assessmentId, PatientIdentity patient, NormalizedAssessment assessment, string username)
    {
        command.Parameters.AddWithValue("assessmentId", assessmentId);
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("assessmentDate", assessment.AssessmentDate);
        command.Parameters.AddWithValue("screeningTool", (object?)assessment.ScreeningTool ?? DBNull.Value);
        command.Parameters.AddWithValue("assessor", assessment.Assessor);
        command.Parameters.AddWithValue("instrumentScore", assessment.InstrumentScore);
        command.Parameters.Add("domains", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(assessment.Domains);
        command.Parameters.AddWithValue("interventions", (object?)assessment.Interventions ?? DBNull.Value);
        command.Parameters.AddWithValue("username", username);
    }

    private static NormalizedAssessment Normalize(PatientSdohAssessmentRequest request, string username)
    {
        if (!DateOnly.TryParse(request.AssessmentDate, out var assessmentDate)) throw new ArgumentException("Assessment date is required.");
        var domains = new Dictionary<string, PatientSdohDomainValue>(StringComparer.Ordinal);
        foreach (var (key, value) in request.Domains)
        {
            if (!SupportedDomainKeys.Contains(key)) throw new ArgumentException($"Unsupported SDOH domain '{key}'.");
            if (value is null) continue;
            var status = NormalizeText(value.Status, 64);
            var notes = NormalizeText(value.Notes, 2000);
            if (status is null && notes is null) continue;
            if (status is not null && !status.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')) throw new ArgumentException($"SDOH status for '{key}' is invalid.");
            domains[key] = new PatientSdohDomainValue(status ?? string.Empty, notes);
        }

        return new NormalizedAssessment(
            assessmentDate,
            NormalizeText(request.ScreeningTool, 120),
            NormalizeText(request.Assessor, 120) ?? username,
            domains,
            NormalizeText(request.Interventions, 4000),
            domains.Values.Count(value => PositiveStatuses.Contains(value.Status)));
    }

    private static async Task<PatientIdentity?> ResolvePatientAsync(NpgsqlConnection connection, string patientId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid
            from patients
            where (lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId)
              and merged_into_patient_id is null;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new PatientIdentity(reader.GetString(0), reader.GetInt32(1)) : null;
    }

    private static PatientSdohAssessmentResponse ToResponse(NpgsqlDataReader reader)
    {
        var domains = JsonSerializer.Deserialize<Dictionary<string, PatientSdohDomainValue>>(reader.GetString(7)) ?? [];
        return new(
            reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetFieldValue<DateOnly>(3).ToString("yyyy-MM-dd"),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt32(6), domains,
            reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11).ToString("O"), reader.GetString(12));
    }

    private static string? NormalizeText(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maximumLength) throw new ArgumentException($"Value exceeds {maximumLength} characters.");
        return normalized;
    }

    private sealed record PatientIdentity(string CanonicalId, int LegacyPid);
    private sealed record NormalizedAssessment(DateOnly AssessmentDate, string? ScreeningTool, string Assessor, IReadOnlyDictionary<string, PatientSdohDomainValue> Domains, string? Interventions, int InstrumentScore);
}
