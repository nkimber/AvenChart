// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class EncounterEntity
{
    public int Id { get; set; }
    public int EncounterNumber { get; set; }
    public required string PatientId { get; set; }
}
