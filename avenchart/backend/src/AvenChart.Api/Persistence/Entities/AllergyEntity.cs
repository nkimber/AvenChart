// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class AllergyEntity
{
    public required string Id { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Reaction { get; set; }
    public string? Severity { get; set; }
    public DateOnly? AllergyDate { get; set; }
    public string? Comments { get; set; }
    public int Activity { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? ListOptionId { get; set; }
}
