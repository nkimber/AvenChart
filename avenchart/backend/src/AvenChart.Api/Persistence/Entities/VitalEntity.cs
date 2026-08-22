// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class VitalEntity
{
    public int Id { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public int? EncounterNumber { get; set; }
    public DateTime VitalDateTime { get; set; }
    public DateTime RecordedAt { get; set; }
    public required string RecordedBy { get; set; }
    public int? CorrectionOfVitalId { get; set; }
    public string? CorrectionReason { get; set; }
    public int? Systolic { get; set; }
    public int? Diastolic { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Height { get; set; }
    public decimal? Temperature { get; set; }
    public int? Pulse { get; set; }
    public int? Respiration { get; set; }
    public decimal? Bmi { get; set; }
    public int? OxygenSaturation { get; set; }
    public string? Note { get; set; }
}
