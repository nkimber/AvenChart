// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class TherapyGroupMemberEntity
{
    public Guid GroupId { get; set; }
    public required string PatientId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public TherapyGroupEntity Group { get; set; } = null!;
    public PatientEntity Patient { get; set; } = null!;
}
