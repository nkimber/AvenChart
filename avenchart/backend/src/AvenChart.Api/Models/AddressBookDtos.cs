// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record AddressBookEntry(int Id, bool IsInternal, string? Username, string Organization, string FirstName, string LastName, string? Specialty, string? Npi, string Type, string? Phone, string? Mobile, string? Fax, string? Email, string? Street, string? City, string? State, string? PostalCode, bool Active);
public sealed record AddressBookResponse(IReadOnlyList<AddressBookEntry> Entries, int Total);
public sealed record AddressBookContactRequest(string Organization, string FirstName, string LastName, string? Specialty, string? Npi, string? Type, string? Phone, string? Mobile, string? Fax, string? Email, string? Street, string? City, string? State, string? PostalCode, bool? Active);
