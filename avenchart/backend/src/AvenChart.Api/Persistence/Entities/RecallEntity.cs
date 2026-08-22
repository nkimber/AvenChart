// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class RecallEntity
{
    public Guid Id { get; set; }
    public required string PatientId { get; set; }
    public DateOnly RecallDate { get; set; }
    public required string Reason { get; set; }
    public int? ProviderId { get; set; }
    public int? FacilityId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? ClosureReason { get; set; }
    public PatientEntity Patient { get; set; } = null!;
    public ICollection<RecallActivityEntity> Activities { get; } = new List<RecallActivityEntity>();
    public ICollection<RecallLifecycleEventEntity> LifecycleEvents { get; } = new List<RecallLifecycleEventEntity>();
}
