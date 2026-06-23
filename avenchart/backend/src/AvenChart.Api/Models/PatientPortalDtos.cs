namespace AvenChart.Api.Models;

public sealed record PatientPortalLoginRequest(
    string Username,
    string Password);

public sealed record PatientPortalLoginResponse(
    bool Authenticated,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string? FailureReason,
    Guid? SessionId,
    DateTimeOffset? SessionCreatedAt,
    DateTimeOffset? SessionExpiresAt,
    string SessionSource);

public sealed record PatientPortalSessionResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? EndedAt,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalHomeSummaryResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    PatientPortalHomeMessageSummary Messages,
    int UpcomingAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> UpcomingAppointments,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalHomeMessageSummary(
    int TotalMessages,
    int NewMessages,
    int DoneMessages,
    string? LatestMessageTitle,
    string? LatestMessageDate);

public sealed record PatientPortalHomeAppointmentSummary(
    string Id,
    string Date,
    string StartTime,
    string Title,
    string? Status,
    int? CategoryId,
    string? CategoryName,
    string? ProviderName,
    string? FacilityName,
    string? Comments);

public sealed record PatientPortalMessagesResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int MessageCount,
    IReadOnlyList<PatientPortalMessageItem> Messages,
    int SentMessageCount,
    IReadOnlyList<PatientPortalMessageItem> SentMessages,
    int AllMessageCount,
    IReadOnlyList<PatientPortalMessageItem> AllMessages,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageThreadResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    string MessageId,
    int ThreadId,
    PatientPortalMessageItem? AnchorMessage,
    int ThreadMessageCount,
    IReadOnlyList<PatientPortalMessageItem> ThreadMessages,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageItem(
    string Id,
    string Date,
    string Title,
    string Body,
    string Status,
    string AssignedTo,
    string SenderId,
    string SenderName,
    string RecipientId,
    string RecipientName,
    int MailChain,
    int ReplyMailChain,
    string? PortalRelation,
    bool IsEncrypted);

public sealed record PatientPortalComposeMessageRequest(
    string? RecipientId,
    string? Title,
    string? Body);

public sealed record PatientPortalComposeMessageResponse(
    bool Authenticated,
    bool Created,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string RecipientId,
    string RecipientName,
    PatientPortalMessageItem? SentMessage,
    PatientPortalMessageItem? RecipientMessage,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalReplyMessageRequest(
    string? Body);

public sealed record PatientPortalReplyMessageResponse(
    bool Authenticated,
    bool Created,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string OriginalMessageId,
    PatientPortalMessageItem? OriginalMessage,
    PatientPortalMessageItem? SentMessage,
    PatientPortalMessageItem? RecipientMessage,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalReadMessageResponse(
    bool Authenticated,
    bool MarkedRead,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string MessageId,
    PatientPortalMessageItem? Message,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalDeleteMessageResponse(
    bool Authenticated,
    bool Deleted,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string MessageId,
    PatientPortalMessageItem? DeletedMessage,
    int DeletedMessageCount,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalArchiveMessagesRequest(
    IReadOnlyList<int>? MessageIds);

public sealed record PatientPortalArchiveMessagesResponse(
    bool Authenticated,
    bool Archived,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    IReadOnlyList<string> MessageIds,
    IReadOnlyList<PatientPortalMessageItem> ArchivedMessages,
    int ArchivedMessageCount,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);
