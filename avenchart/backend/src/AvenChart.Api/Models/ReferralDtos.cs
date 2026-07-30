namespace AvenChart.Api.Models;

public sealed record ReferralItem(
    Guid Id,
    string PatientId,
    int? EncounterId,
    string Destination,
    string Reason,
    string Status,
    string? ExternalReference,
    string? Notes,
    string RequestedAt,
    int WorkflowVersion,
    string AssignedTo,
    string AssignedDisplayName,
    string? DueAt,
    string CreatedBy,
    string PolicyRevision,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<ClinicalWorkflowTransitionOption> AvailableTransitions);

public sealed record ReferralCreateRequest(
    int? EncounterId,
    string Destination,
    string Reason,
    string? ExternalReference,
    string? Notes,
    string? RequestedAt,
    string? AssignedTo,
    string? DueAt,
    string? WorkflowReason);

public sealed record ReferralStatusRequest(
    string Status,
    int ExpectedVersion,
    string ReasonCode,
    string Reason);

public sealed record ReferralAssignmentRequest(
    string AssignedTo,
    string? DueAt,
    int ExpectedVersion,
    string ReasonCode,
    string Reason);

public sealed record ReferralWorkflowEvent(
    Guid EventId,
    int WorkflowVersion,
    string Action,
    string? FromState,
    string ToState,
    string? FromAssignedTo,
    string? ToAssignedTo,
    string ReasonCode,
    string Reason,
    string Actor,
    string PolicyRevision,
    string OccurredAt);

public sealed record ReferralWorkflowHistoryResponse(
    ReferralItem Referral,
    int Total,
    IReadOnlyList<ReferralWorkflowEvent> Events);

public sealed record ReferralWorkQueueItem(
    ReferralItem Referral,
    string PatientDisplayName,
    string Pubpid,
    bool IsOverdue);

public sealed record ReferralWorkQueueResponse(
    int Total,
    int ActiveCount,
    int OverdueCount,
    IReadOnlyList<ReferralWorkQueueItem> Items);
