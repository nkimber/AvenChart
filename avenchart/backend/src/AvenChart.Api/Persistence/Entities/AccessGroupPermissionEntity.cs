// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AccessGroupPermissionEntity
{
    public required string GroupValue { get; set; }
    public required string SectionValue { get; set; }
    public required string PermissionValue { get; set; }
    public required string PermissionName { get; set; }
    public required string ReturnValue { get; set; }
}
