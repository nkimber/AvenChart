// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class ChartTrackerEventEntity
{
    public Guid Id { get; set; }
    public required string PatientId { get; set; }
    public string? Location { get; set; }
    public int? UserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public PatientEntity Patient { get; set; } = null!;
    public StaffEntity? User { get; set; }
}
