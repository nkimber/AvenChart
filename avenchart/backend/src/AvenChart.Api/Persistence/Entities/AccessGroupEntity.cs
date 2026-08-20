// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AccessGroupEntity
{
    public int Id { get; set; }
    public required string Value { get; set; }
    public required string Name { get; set; }
}
