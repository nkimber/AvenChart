// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

/// <summary>
/// An immutable clinical-list mutation record. This preserves the authoritative
/// post-mutation state for list resources that do not have their own versioned
/// lifecycle aggregate.
/// </summary>
public sealed class ClinicalListAuditEventEntity
{
    public Guid EventId { get; set; }
    public required string ResourceType { get; set; }
    public required string ResourceId { get; set; }
    public required string PatientId { get; set; }
    public required string Action { get; set; }
    public required string Actor { get; set; }
    public string? Reason { get; set; }
    public required string StateJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
