// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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

    private static readonly HashSet<string> DisabilityQuestionKeys = new(StringComparer.Ordinal)
    {
        "walk_climb", "seeing", "hearing", "cognitive", "dressing_bathing", "errands"
    };

    private static readonly HashSet<string> GoalPositiveStatuses = new(StringComparer.Ordinal)
    {
        "yes", "positive", "present", "high", "at_risk", "often", "sometimes", "frequently", "severe", "moderate"
    };

    private static readonly IReadOnlyDictionary<string, string> GoalDomainLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["food_insecurity"] = "Food insecurity",
        ["housing_instability"] = "Housing instability",
        ["transportation_insecurity"] = "Transportation insecurity",
        ["utilities_insecurity"] = "Utilities insecurity",
        ["interpersonal_safety"] = "Interpersonal safety concern",
        ["financial_strain"] = "Financial resource strain",
        ["social_isolation"] = "Social isolation",
        ["childcare_needs"] = "Childcare need",
        ["digital_access"] = "Digital access barrier"
    };

    private static readonly IReadOnlyDictionary<string, (string Description, string Reason)> InterventionGuidance = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["food_insecurity"] = ("Assistance with application for food pantry program", "Food insecurity risk"),
        ["housing_instability"] = ("Referral to local housing assistance resources", "Housing instability risk"),
        ["transportation_insecurity"] = ("Arrange transportation for appointments (medical or social services)", "Transportation barrier present"),
        ["utilities_insecurity"] = ("Referral to utility bill assistance program", "Utility shutoff risk"),
        ["financial_strain"] = ("Referral to financial counseling / benefits navigator", "Financial strain"),
        ["social_isolation"] = ("Referral to community/social connection programs", "Loneliness / social isolation"),
        ["childcare_needs"] = ("Provide childcare resources and referral", "Childcare needs present"),
        ["digital_access"] = ("Assist with device/internet access and digital literacy", "Limited digital access"),
        ["interpersonal_safety"] = ("Provide IPV resources and safety planning; social work referral", "IPV risk present")
    };

    public async Task<IReadOnlyList<PatientSdohAssessmentResponse>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                   hunger_q1, hunger_q2, hunger_score, pregnancy_status, pregnancy_edd, pregnancy_intent, postpartum_status, postpartum_end,
                   disability_status, disability_status_notes, disability_scale,
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
                hunger_q1, hunger_q2, hunger_score, pregnancy_status, pregnancy_edd, pregnancy_intent, postpartum_status, postpartum_end,
                disability_status, disability_status_notes, disability_scale,
                domains, interventions, created_at, created_by, updated_at, updated_by)
            values (
                @assessmentId, @patientId, @pid, @assessmentDate, @screeningTool, @assessor, @instrumentScore,
                @hungerQuestionOne, @hungerQuestionTwo, @hungerScore, @pregnancyStatus, @pregnancyEdd, @pregnancyIntent, @postpartumStatus, @postpartumEnd,
                @disabilityStatus, @disabilityStatusNotes, @disabilityScale,
                @domains, @interventions, now(), @username, now(), @username)
            returning assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                      hunger_q1, hunger_q2, hunger_score, pregnancy_status, pregnancy_edd, pregnancy_intent, postpartum_status, postpartum_end,
                      disability_status, disability_status_notes, disability_scale,
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
                instrument_score = @instrumentScore, hunger_q1 = @hungerQuestionOne, hunger_q2 = @hungerQuestionTwo,
                hunger_score = @hungerScore, pregnancy_status = @pregnancyStatus, pregnancy_edd = @pregnancyEdd,
                pregnancy_intent = @pregnancyIntent, postpartum_status = @postpartumStatus, postpartum_end = @postpartumEnd,
                disability_status = @disabilityStatus, disability_status_notes = @disabilityStatusNotes, disability_scale = @disabilityScale,
                domains = @domains, interventions = @interventions,
                updated_at = now(), updated_by = @username
            where assessment_id = @assessmentId and patient_id = @patientId
            returning assessment_id, patient_id, pid, assessment_date, screening_tool, assessor, instrument_score,
                      hunger_q1, hunger_q2, hunger_score, pregnancy_status, pregnancy_edd, pregnancy_intent, postpartum_status, postpartum_end,
                      disability_status, disability_status_notes, disability_scale,
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
        command.Parameters.AddWithValue("hungerQuestionOne", (object?)assessment.HungerQuestionOne ?? DBNull.Value);
        command.Parameters.AddWithValue("hungerQuestionTwo", (object?)assessment.HungerQuestionTwo ?? DBNull.Value);
        command.Parameters.AddWithValue("hungerScore", assessment.HungerScore);
        command.Parameters.AddWithValue("pregnancyStatus", (object?)assessment.PregnancyStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("pregnancyEdd", (object?)assessment.PregnancyEdd ?? DBNull.Value);
        command.Parameters.AddWithValue("pregnancyIntent", (object?)assessment.PregnancyIntent ?? DBNull.Value);
        command.Parameters.AddWithValue("postpartumStatus", (object?)assessment.PostpartumStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("postpartumEnd", (object?)assessment.PostpartumEnd ?? DBNull.Value);
        command.Parameters.AddWithValue("disabilityStatus", (object?)assessment.DisabilityStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("disabilityStatusNotes", (object?)assessment.DisabilityStatusNotes ?? DBNull.Value);
        command.Parameters.Add("disabilityScale", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(assessment.DisabilityScale);
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

        var hungerQuestionOne = NormalizeHungerAnswer(request.HungerQuestionOne);
        var hungerQuestionTwo = NormalizeHungerAnswer(request.HungerQuestionTwo);
        var hungerScore = CountHungerRisk(hungerQuestionOne) + CountHungerRisk(hungerQuestionTwo);
        if (hungerScore > 0)
        {
            domains["food_insecurity"] = new PatientSdohDomainValue("at_risk", domains.GetValueOrDefault("food_insecurity")?.Notes);
        }
        else if (hungerQuestionOne is not null && hungerQuestionTwo is not null)
        {
            domains["food_insecurity"] = new PatientSdohDomainValue("no_risk", domains.GetValueOrDefault("food_insecurity")?.Notes);
        }

        var pregnancyStatus = NormalizeOption(request.PregnancyStatus, "Pregnancy status", ["pregnant", "not_pregnant", "possible", "unconfirmed"]);
        var pregnancyEdd = NormalizeDate(request.PregnancyEdd, "Estimated due date");
        var pregnancyIntent = NormalizeOption(request.PregnancyIntent, "Pregnancy intention", ["not_sure", "ambivalent", "no_desire", "wants_pregnancy"]);
        var postpartumStatus = NormalizeOption(request.PostpartumStatus, "Postpartum status", ["postpartum"]);
        var postpartumEnd = NormalizeDate(request.PostpartumEnd, "Postpartum end date");
        var disabilityStatus = NormalizeOption(request.DisabilityStatus, "Disability status", ["im_safe", "im_vulnerable", "im_at_risk", "im_in_crisis"]);
        var disabilityScale = NormalizeDisabilityScale(request.DisabilityScale);

        return new NormalizedAssessment(
            assessmentDate,
            NormalizeText(request.ScreeningTool, 120),
            NormalizeText(request.Assessor, 120) ?? username,
            domains,
            NormalizeText(request.Interventions, 4000),
            domains.Values.Count(value => PositiveStatuses.Contains(value.Status)) + disabilityScale.Values.Count(value => value == "yes"),
            hungerQuestionOne,
            hungerQuestionTwo,
            hungerScore,
            pregnancyStatus,
            pregnancyEdd,
            pregnancyIntent,
            postpartumStatus,
            postpartumEnd,
            disabilityStatus,
            NormalizeText(request.DisabilityStatusNotes, 2000),
            disabilityScale);
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
        var disabilityScale = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(17)) ?? [];
        var domains = JsonSerializer.Deserialize<Dictionary<string, PatientSdohDomainValue>>(reader.GetString(18)) ?? [];
        var goalDueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90).ToString("yyyy-MM-dd");
        var generatedGoals = GoalDomainLabels
            .Where(pair => domains.TryGetValue(pair.Key, out var value) && GoalPositiveStatuses.Contains(value.Status))
            .Select(pair => new PatientSdohGeneratedGoal(pair.Key, $"Improve {pair.Value}", goalDueDate))
            .ToArray();
        var generatedInterventions = InterventionGuidance
            .Where(pair => domains.TryGetValue(pair.Key, out var value) && value.Status is "present" or "at_risk" or "yes")
            .Select(pair => new PatientSdohGeneratedIntervention(pair.Key, pair.Value.Description, pair.Value.Reason))
            .ToArray();
        return new(
            reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetFieldValue<DateOnly>(3).ToString("yyyy-MM-dd"),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11).ToString("yyyy-MM-dd"),
            reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14).ToString("yyyy-MM-dd"),
            reader.IsDBNull(15) ? null : reader.GetString(15), reader.IsDBNull(16) ? null : reader.GetString(16), disabilityScale,
            generatedGoals,
            generatedInterventions,
            domains,
            reader.IsDBNull(19) ? null : reader.GetString(19), reader.GetFieldValue<DateTimeOffset>(20).ToString("O"), reader.GetString(21),
            reader.GetFieldValue<DateTimeOffset>(22).ToString("O"), reader.GetString(23));
    }

    private static string? NormalizeText(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maximumLength) throw new ArgumentException($"Value exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? NormalizeHungerAnswer(string? value)
    {
        var normalized = NormalizeText(value, 16);
        if (normalized is not null && normalized is not ("LA28397-0" or "LA28398-8" or "LA6729-3")) throw new ArgumentException("Hunger Vital Signs answers are invalid.");
        return normalized;
    }

    private static int CountHungerRisk(string? answer) => answer is "LA28397-0" or "LA28398-8" ? 1 : 0;

    private static string? NormalizeOption(string? value, string label, params string[] allowedValues)
    {
        var normalized = NormalizeText(value, 64);
        if (normalized is not null && !allowedValues.Contains(normalized)) throw new ArgumentException($"{label} is invalid.");
        return normalized;
    }

    private static DateOnly? NormalizeDate(string? value, string label)
    {
        var normalized = NormalizeText(value, 10);
        if (normalized is null) return null;
        if (!DateOnly.TryParse(normalized, out var date)) throw new ArgumentException($"{label} is invalid.");
        return date;
    }

    private static IReadOnlyDictionary<string, string> NormalizeDisabilityScale(IReadOnlyDictionary<string, string?>? scale)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in scale ?? new Dictionary<string, string?>())
        {
            if (!DisabilityQuestionKeys.Contains(key)) throw new ArgumentException($"Unsupported disability question '{key}'.");
            var answer = NormalizeOption(value, "Disability question answer", ["yes", "no", "declined"]);
            if (answer is not null) normalized[key] = answer;
        }
        return normalized;
    }

    private sealed record PatientIdentity(string CanonicalId, int LegacyPid);
    private sealed record NormalizedAssessment(DateOnly AssessmentDate, string? ScreeningTool, string Assessor, IReadOnlyDictionary<string, PatientSdohDomainValue> Domains, string? Interventions, int InstrumentScore, string? HungerQuestionOne, string? HungerQuestionTwo, int HungerScore, string? PregnancyStatus, DateOnly? PregnancyEdd, string? PregnancyIntent, string? PostpartumStatus, DateOnly? PostpartumEnd, string? DisabilityStatus, string? DisabilityStatusNotes, IReadOnlyDictionary<string, string> DisabilityScale);
}
