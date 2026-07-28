namespace AvenChart.Api.Models;

public sealed record ClinicalWorkflowTransitionOption(
    string Action,
    string FromState,
    string ToState,
    string ReasonCode,
    string Label,
    bool RequiresAuthorizationNumber);

public sealed record ClinicalWorkflowAssignee(
    int? StaffId,
    string Username,
    string DisplayName,
    string Role);

public sealed record ClinicalWorkflowAssigneesResponse(
    string PolicyRevision,
    int Count,
    IReadOnlyList<ClinicalWorkflowAssignee> Assignees);

public sealed record AuthorizationItem(
    Guid Id,
    string PatientId,
    Guid? ReferralId,
    string Payer,
    string Service,
    string Status,
    string? AuthorizationNumber,
    string RequestedAt,
    string? ExpiresAt,
    int WorkflowVersion,
    string AssignedTo,
    string AssignedDisplayName,
    string? DueAt,
    string CreatedBy,
    string PolicyRevision,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<ClinicalWorkflowTransitionOption> AvailableTransitions);

public sealed record AuthorizationCreateRequest(
    Guid? ReferralId,
    string Payer,
    string Service,
    string? RequestedAt,
    string? ExpiresAt,
    string? AssignedTo,
    string? DueAt,
    string? Reason);

public sealed record AuthorizationStatusRequest(
    string Status,
    string? AuthorizationNumber,
    int ExpectedVersion,
    string ReasonCode,
    string Reason);

public sealed record AuthorizationAssignmentRequest(
    string AssignedTo,
    string? DueAt,
    int ExpectedVersion,
    string ReasonCode,
    string Reason);

public sealed record AuthorizationWorkflowEvent(
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

public sealed record AuthorizationWorkflowHistoryResponse(
    AuthorizationItem Authorization,
    int Total,
    IReadOnlyList<AuthorizationWorkflowEvent> Events);
