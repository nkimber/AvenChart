// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class PatientEducationResourceEntity
{
    public required string ResourceKey { get; set; }
    public required string Title { get; set; }
    public required string SearchTemplate { get; set; }
    public bool Active { get; set; }
}
