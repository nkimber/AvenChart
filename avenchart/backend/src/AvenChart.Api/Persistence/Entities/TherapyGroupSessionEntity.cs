// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class TherapyGroupSessionEntity
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? Topic { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TherapyGroupEntity Group { get; set; } = null!;
    public ICollection<TherapyGroupSessionAttendanceEntity> Attendance { get; } = new List<TherapyGroupSessionAttendanceEntity>();
}
