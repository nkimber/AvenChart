// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class ImmunizationEntity
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public int? Encounter { get; set; }
    public int? ImmunizationId { get; set; }
    public string? CvxCode { get; set; }
    public string? Vaccine { get; set; }
    public DateTime? AdministeredAt { get; set; }
    public string? Manufacturer { get; set; }
    public string? LotNumber { get; set; }
    public int? AdministeredById { get; set; }
    public string? AdministeredBy { get; set; }
    public DateOnly? EducationDate { get; set; }
    public DateOnly? VisDate { get; set; }
    public decimal? AmountAdministered { get; set; }
    public string? AmountAdministeredUnit { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Route { get; set; }
    public string? AdministrationSite { get; set; }
    public string? CompletionStatus { get; set; }
    public string? InformationSource { get; set; }
    public string? Note { get; set; }
    public int AddedErroneously { get; set; }
}
