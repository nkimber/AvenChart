// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class EncounterAuditEventEntity
{
    public Guid EventId { get; set; }
    public int EncounterNumber { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Username { get; set; }
    public required string Action { get; set; }
    public required string ChangedFields { get; set; }
}
