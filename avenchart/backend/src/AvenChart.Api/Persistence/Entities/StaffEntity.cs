// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class StaffEntity
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public bool Calendar { get; set; }
    public int? FacilityId { get; set; }
    public string? Email { get; set; }
    public string? Npi { get; set; }
    public bool Active { get; set; }
}
