namespace AvenChart.Api.Models;

public sealed record PatientMessagesResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    bool PortalEnabled,
    IReadOnlyList<PatientMessageItem> Messages);

public sealed record PatientMessageItem(
    string Id,
    string? Date,
    string? Title,
    string? Body,
    string? Status,
    string? AssignedTo,
    string? PortalRelation,
    bool IsEncrypted,
    int? UpdatedBy,
    string? UpdatedAt,
    int Deleted);

public sealed record PatientMessageCreateRequest(
    string PatientId,
    string Title,
    string Body,
    string AssignedTo);

public sealed record PatientMessageStatusUpdateRequest(
    string Status,
    string Body);

public sealed record PatientMessageContentUpdateRequest(
    string Title,
    string Body);

public sealed record PatientMessageAssignmentUpdateRequest(
    string AssignedTo);

public sealed record PatientMessageReplyRequest(
    string Body,
    string AssignedTo);

public sealed record PatientMessageMutationResponse(
    string Id,
    PatientMessagesResponse Detail);

public sealed record StaffMessageInboxCounts(
    int Total,
    int Unread,
    int AssignedToMe,
    int Unassigned);

public sealed record StaffMessageInboxItem(
    string Id,
    string PatientId,
    string Pubpid,
    string PatientDisplayName,
    string? Date,
    string Subject,
    string Preview,
    string Status,
    string? AssignedTo,
    string Priority,
    int AgeDays,
    bool Unread,
    string? PortalRelation,
    string? UpdatedAt);

public sealed record StaffMessageInboxResponse(
    string DatasetId,
    string DatasetVersion,
    int Total,
    int Offset,
    int Limit,
    StaffMessageInboxCounts Counts,
    IReadOnlyList<StaffMessageInboxItem> Items);

public sealed record StaffMessageInboxQuery(
    string? Status,
    string? Assignment,
    string? Patient,
    string? Subject,
    string? Priority,
    string? Owner,
    int? MinimumAgeDays,
    int? MaximumAgeDays,
    int Offset,
    int Limit);
