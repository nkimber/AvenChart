// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

public sealed class PatientSdohRepository(AvenChartDbContext dbContext)
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

    public async Task<IReadOnlyList<PatientSdohAssessmentResponse>> GetAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var assessments = await dbContext.PatientSdohAssessments
            .AsNoTracking()
            .Where(assessment => assessment.PatientId == patient.CanonicalId)
            .OrderByDescending(assessment => assessment.AssessmentDate)
            .ThenByDescending(assessment => assessment.UpdatedAt)
            .ThenByDescending(assessment => assessment.AssessmentId)
            .ToListAsync(cancellationToken);
        return assessments.Select(ToResponse).ToList();
    }

    public async Task<PatientSdohAssessmentResponse> CreateAsync(
        string patientId,
        PatientSdohAssessmentRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var normalized = Normalize(request, username);
        var now = DateTimeOffset.UtcNow;
        var assessment = new PatientSdohAssessmentEntity
        {
            AssessmentId = Guid.NewGuid(),
            PatientId = patient.CanonicalId,
            LegacyPid = patient.LegacyPid,
            CreatedAt = now,
            CreatedBy = username,
            UpdatedAt = now,
            UpdatedBy = username,
            RowVersion = 1,
            DomainsJson = "{}",
            DisabilityScaleJson = "{}",
            Assessor = username
        };
        Apply(assessment, normalized, username);
        dbContext.PatientSdohAssessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(assessment);
    }

    public async Task<PatientSdohAssessmentResponse> UpdateAsync(
        string patientId,
        Guid assessmentId,
        PatientSdohAssessmentRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var patient = await ResolvePatientAsync(patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        var assessment = await dbContext.PatientSdohAssessments.SingleOrDefaultAsync(
            candidate =>
                candidate.AssessmentId == assessmentId &&
                candidate.PatientId == patient.CanonicalId,
            cancellationToken);
        if (assessment is null)
        {
            throw new ArgumentException("The SDOH assessment does not exist for this patient.");
        }

        Apply(assessment, Normalize(request, username), username);
        assessment.RowVersion++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException("The SDOH assessment changed before this update could be saved.");
        }

        return ToResponse(assessment);
    }

    private static void Apply(
        PatientSdohAssessmentEntity entity,
        NormalizedAssessment assessment,
        string username)
    {
        entity.AssessmentDate = assessment.AssessmentDate;
        entity.ScreeningTool = assessment.ScreeningTool;
        entity.Assessor = assessment.Assessor;
        entity.InstrumentScore = assessment.InstrumentScore;
        entity.HungerQuestionOne = assessment.HungerQuestionOne;
        entity.HungerQuestionTwo = assessment.HungerQuestionTwo;
        entity.HungerScore = assessment.HungerScore;
        entity.PregnancyStatus = assessment.PregnancyStatus;
        entity.PregnancyEstimatedDueDate = assessment.PregnancyEdd;
        entity.PregnancyIntent = assessment.PregnancyIntent;
        entity.PostpartumStatus = assessment.PostpartumStatus;
        entity.PostpartumEnd = assessment.PostpartumEnd;
        entity.DisabilityStatus = assessment.DisabilityStatus;
        entity.DisabilityStatusNotes = assessment.DisabilityStatusNotes;
        entity.DisabilityScaleJson = JsonSerializer.Serialize(assessment.DisabilityScale);
        entity.DomainsJson = JsonSerializer.Serialize(assessment.Domains);
        entity.Interventions = assessment.Interventions;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = username;
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

    private async Task<PatientIdentity?> ResolvePatientAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var normalized = patientId.Trim();
        var normalizedLower = normalized.ToLowerInvariant();
        var hasLegacyPid = int.TryParse(normalized, out var legacyPid);
        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient =>
                patient.MergedIntoPatientId == null &&
                (patient.CanonicalId.ToLower() == normalizedLower ||
                 patient.PublicId.ToLower() == normalizedLower ||
                 (hasLegacyPid && patient.LegacyPid == legacyPid)))
            .Select(patient => new PatientIdentity(patient.CanonicalId, patient.LegacyPid))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static PatientSdohAssessmentResponse ToResponse(PatientSdohAssessmentEntity assessment)
    {
        var disabilityScale = JsonSerializer.Deserialize<Dictionary<string, string>>(assessment.DisabilityScaleJson) ?? [];
        var domains = JsonSerializer.Deserialize<Dictionary<string, PatientSdohDomainValue>>(assessment.DomainsJson) ?? [];
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
            assessment.AssessmentId,
            assessment.PatientId,
            assessment.LegacyPid,
            assessment.AssessmentDate.ToString("yyyy-MM-dd"),
            assessment.ScreeningTool,
            assessment.Assessor,
            assessment.InstrumentScore,
            assessment.HungerQuestionOne,
            assessment.HungerQuestionTwo,
            assessment.HungerScore,
            assessment.PregnancyStatus,
            assessment.PregnancyEstimatedDueDate?.ToString("yyyy-MM-dd"),
            assessment.PregnancyIntent,
            assessment.PostpartumStatus,
            assessment.PostpartumEnd?.ToString("yyyy-MM-dd"),
            assessment.DisabilityStatus,
            assessment.DisabilityStatusNotes,
            disabilityScale,
            generatedGoals,
            generatedInterventions,
            domains,
            assessment.Interventions,
            assessment.CreatedAt.ToString("O"),
            assessment.CreatedBy,
            assessment.UpdatedAt.ToString("O"),
            assessment.UpdatedBy);
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
