// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class TherapyGroupEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public int? FacilitatorId { get; set; }
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<TherapyGroupMemberEntity> Members { get; } = new List<TherapyGroupMemberEntity>();
    public ICollection<TherapyGroupSessionEntity> Sessions { get; } = new List<TherapyGroupSessionEntity>();
}
