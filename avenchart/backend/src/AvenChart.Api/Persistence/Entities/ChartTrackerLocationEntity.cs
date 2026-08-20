// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class ChartTrackerLocationEntity
{
    public required string Name { get; set; }
    public int Position { get; set; }
    public bool Active { get; set; }
}
