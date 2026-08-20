// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class LabProviderAddressBookEntity
{
    public int Id { get; set; }
    public required string Organization { get; set; }
    public required string Type { get; set; }
    public bool Active { get; set; }
}
