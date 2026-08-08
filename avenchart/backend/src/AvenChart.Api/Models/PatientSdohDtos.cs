// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record PatientSdohDomainValue(string Status, string? Notes);
public sealed record PatientSdohGeneratedGoal(string Domain, string Description, string DueDate);
public sealed record PatientSdohGeneratedIntervention(string Domain, string Description, string Reason);

public sealed record PatientSdohAssessmentRequest(
    string AssessmentDate,
    string? ScreeningTool,
    string? Assessor,
    IReadOnlyDictionary<string, PatientSdohDomainValue?> Domains,
    string? HungerQuestionOne,
    string? HungerQuestionTwo,
    string? PregnancyStatus,
    string? PregnancyEdd,
    string? PregnancyIntent,
    string? PostpartumStatus,
    string? PostpartumEnd,
    string? DisabilityStatus,
    string? DisabilityStatusNotes,
    IReadOnlyDictionary<string, string?>? DisabilityScale,
    string? Interventions);

public sealed record PatientSdohAssessmentResponse(
    Guid AssessmentId,
    string PatientId,
    int LegacyPid,
    string AssessmentDate,
    string? ScreeningTool,
    string Assessor,
    int InstrumentScore,
    string? HungerQuestionOne,
    string? HungerQuestionTwo,
    int HungerScore,
    string? PregnancyStatus,
    string? PregnancyEdd,
    string? PregnancyIntent,
    string? PostpartumStatus,
    string? PostpartumEnd,
    string? DisabilityStatus,
    string? DisabilityStatusNotes,
    IReadOnlyDictionary<string, string> DisabilityScale,
    IReadOnlyList<PatientSdohGeneratedGoal> GeneratedGoals,
    IReadOnlyList<PatientSdohGeneratedIntervention> GeneratedInterventions,
    IReadOnlyDictionary<string, PatientSdohDomainValue> Domains,
    string? Interventions,
    string CreatedAt,
    string CreatedBy,
    string UpdatedAt,
    string UpdatedBy);
