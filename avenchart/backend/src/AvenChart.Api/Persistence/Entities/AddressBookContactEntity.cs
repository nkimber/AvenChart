// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AddressBookContactEntity
{
    public int Id { get; set; }
    public required string Organization { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Specialty { get; set; }
    public string? Npi { get; set; }
    public required string ContactType { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public bool Active { get; set; }
}
