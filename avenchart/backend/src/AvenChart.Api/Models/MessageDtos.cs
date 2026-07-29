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
    int Deleted,
    int AssignmentVersion);

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
    string? AssignedTo,
    int ExpectedVersion,
    string? Reason);

public sealed record PatientMessageForwardRequest(
    string AssignedTo,
    int ExpectedVersion,
    string? Note);

public sealed record PatientMessageCorrectionRequest(
    string Correction,
    string Reason);

public sealed record PatientMessageCorrectionEvent(
    long EventId,
    string Correction,
    string Reason,
    string Actor,
    string OccurredAt);

public sealed record PatientMessageCorrectionHistoryResponse(
    string MessageId,
    IReadOnlyList<PatientMessageCorrectionEvent> Events);

public sealed record PatientMessageArchiveRequest(string Reason);

public sealed record PatientMessageRetentionEvent(long EventId, string Action, string Reason, string Actor, string OccurredAt);

public sealed record PatientMessageRetentionHistoryResponse(string MessageId, IReadOnlyList<PatientMessageRetentionEvent> Events);

public sealed record StaffMessageAttachmentSubmission(string? FileName, string? ContentType, string? ContentBase64);

public sealed record StaffMessageAttachmentItem(string Id, string FileName, string ContentType, int SizeBytes, string Sha256, string UploadedBy, string UploadedAt);

public sealed record StaffMessageAttachmentDownload(bool Downloadable, string FileName, string ContentType, byte[] Content, string? FailureReason);

public sealed record PatientMessageAssignmentEvent(
    long EventId,
    string Action,
    string? PreviousAssignedTo,
    string? AssignedTo,
    string? Reason,
    string Actor,
    string OccurredAt,
    int AssignmentVersion);

public sealed record PatientMessageAssignmentHistoryResponse(
    string MessageId,
    int CurrentVersion,
    IReadOnlyList<PatientMessageAssignmentEvent> Events);

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
