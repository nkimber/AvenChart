// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class PatientSdohAssessmentEntity
{
    public Guid AssessmentId { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public DateOnly AssessmentDate { get; set; }
    public string? ScreeningTool { get; set; }
    public required string Assessor { get; set; }
    public int InstrumentScore { get; set; }
    public string? HungerQuestionOne { get; set; }
    public string? HungerQuestionTwo { get; set; }
    public int HungerScore { get; set; }
    public string? PregnancyStatus { get; set; }
    public DateOnly? PregnancyEstimatedDueDate { get; set; }
    public string? PregnancyIntent { get; set; }
    public string? PostpartumStatus { get; set; }
    public DateOnly? PostpartumEnd { get; set; }
    public string? DisabilityStatus { get; set; }
    public string? DisabilityStatusNotes { get; set; }
    public required string DisabilityScaleJson { get; set; }
    public required string DomainsJson { get; set; }
    public string? Interventions { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string UpdatedBy { get; set; }
    public long RowVersion { get; set; }
    public PatientEntity Patient { get; set; } = null!;
}
