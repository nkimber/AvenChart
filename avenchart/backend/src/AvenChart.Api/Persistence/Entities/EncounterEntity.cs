// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class EncounterEntity
{
    public int Id { get; set; }
    public int EncounterNumber { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public string? Reason { get; set; }
    public string? Sensitivity { get; set; }
    public string? ReferralSource { get; set; }
    public string? ExternalId { get; set; }
    public int? PosCode { get; set; }
    public string? BillingNote { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public int ArchiveVersion { get; set; }
    public long RowVersion { get; set; }
}
