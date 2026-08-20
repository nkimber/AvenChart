// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class LabOrderReferenceEntity
{
    public int Id { get; set; }
    public int? LabId { get; set; }
}
