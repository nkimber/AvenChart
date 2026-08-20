// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AuthAccountEntity
{
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public bool Active { get; set; }
}
