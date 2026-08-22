// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class RecallLifecycleEventEntity
{
    public Guid EventId { get; set; }
    public Guid RecallId { get; set; }
    public string? PreviousStatus { get; set; }
    public required string Status { get; set; }
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public RecallEntity Recall { get; set; } = null!;
}
