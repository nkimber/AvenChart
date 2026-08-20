// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AccessUserMembershipEntity
{
    public required string UserValue { get; set; }
    public required string UserName { get; set; }
    public required string GroupValue { get; set; }
    public required string GroupName { get; set; }
    public int? StaffId { get; set; }
}
