// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class TherapyGroupSessionAttendanceEntity
{
    public Guid SessionId { get; set; }
    public required string PatientId { get; set; }
    public required string AttendanceStatus { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public TherapyGroupSessionEntity Session { get; set; } = null!;
    public PatientEntity Patient { get; set; } = null!;
}
