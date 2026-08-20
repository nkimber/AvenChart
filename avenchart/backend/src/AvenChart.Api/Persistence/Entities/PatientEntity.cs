// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class PatientEntity
{
    public required string CanonicalId { get; set; }
    public int LegacyPid { get; set; }
    public required string PublicId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? PreferredName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int? ProviderId { get; set; }
    public string? MergedIntoPatientId { get; set; }
}
