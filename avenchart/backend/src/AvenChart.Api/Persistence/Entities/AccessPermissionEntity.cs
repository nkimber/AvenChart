// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AccessPermissionEntity
{
    public required string SectionValue { get; set; }
    public required string Value { get; set; }
    public required string Name { get; set; }
}
