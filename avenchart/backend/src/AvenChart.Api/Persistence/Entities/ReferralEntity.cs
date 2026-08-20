// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class ReferralEntity
{
    public Guid Id { get; set; }
    public required string PatientId { get; set; }
    public int? EncounterId { get; set; }
    public required string Destination { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int WorkflowVersion { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public PatientEntity Patient { get; set; } = null!;
}
