// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class TherapyGroupSessionParticipantEntity
{
    public Guid SessionId { get; set; }
    public required string PatientId { get; set; }
    public PatientEntity Patient { get; set; } = null!;
}
