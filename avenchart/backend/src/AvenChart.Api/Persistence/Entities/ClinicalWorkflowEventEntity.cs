// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class ClinicalWorkflowEventEntity
{
    public Guid EventId { get; set; }
    public required string WorkflowType { get; set; }
    public required string EntityId { get; set; }
    public string? PatientId { get; set; }
    public int WorkflowVersion { get; set; }
    public required string Action { get; set; }
    public string? FromState { get; set; }
    public required string ToState { get; set; }
    public string? FromAssignedTo { get; set; }
    public string? ToAssignedTo { get; set; }
    public required string ReasonCode { get; set; }
    public required string Reason { get; set; }
    public required string Actor { get; set; }
    public required string PolicyRevision { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
