// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class MedicationLifecycleEventEntity
{
    public long Id { get; set; }
    public required string MedicationId { get; set; }
    public required string Action { get; set; }
    public int? PreviousActivity { get; set; }
    public int CurrentActivity { get; set; }
    public required string Actor { get; set; }
    public string? Reason { get; set; }
    public int ExpectedVersion { get; set; }
    public int ResultingVersion { get; set; }
    public DateTime OccurredAt { get; set; }
}
