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

public sealed record PatientPortalAppointmentsResponse(
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
    int UpcomingAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> UpcomingAppointments,
    int PastAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> PastAppointments,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalAppointmentRequestOptionsResponse(
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
    IReadOnlyList<PatientPortalAppointmentCategoryOption> Categories,
    IReadOnlyList<PatientPortalAppointmentProviderOption> Providers,
    IReadOnlyList<PatientPortalAppointmentFacilityOption> Facilities,
    PatientPortalAppointmentRequestDefaults Defaults,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalAppointmentCategoryOption(
    int Id,
    string Name,
    string ConstantId,
    int DurationMinutes);

public sealed record PatientPortalAppointmentProviderOption(
    int Id,
    string Username,
    string DisplayName,
    int? FacilityId,
    string? FacilityName);

public sealed record PatientPortalAppointmentFacilityOption(
    int Id,
    string Name,
    string? Code);

public sealed record PatientPortalAppointmentRequestDefaults(
    int? CategoryId,
    int? ProviderId,
    int? FacilityId,
    int DurationMinutes,
    string Date,
    string StartTime);

public sealed record PatientPortalAppointmentRequest(
    int? ProviderId,
    int? FacilityId,
    int? CategoryId,
    string? Date,
    string? StartTime,
    int? DurationMinutes,
    string? Reason);

public sealed record PatientPortalAppointmentRequestResponse(
    bool Authenticated,
    bool Created,
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
    PatientPortalHomeAppointmentSummary? Appointment,
    PatientPortalAppointmentReminder? Reminder,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalAppointmentReminder(
    string Id,
    string Title,
    string Body,
    string AssignedTo,
    string Status);

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

public sealed record PatientPortalDocumentsResponse(
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
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentCategory> Categories,
    IReadOnlyList<PatientPortalDocumentItem> Documents,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalDocumentCategory(
    int CategoryId,
    string CategoryName,
    string DisplayPath,
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentItem> Documents);

public sealed record PatientPortalDocumentItem(
    int Id,
    string DocumentKey,
    int CategoryId,
    string CategoryName,
    string DisplayPath,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    string FileName,
    int? SizeBytes,
    string? StorageMethod,
    bool CanDownload);

public sealed record PatientPortalDocumentsDownloadRequest(
    IReadOnlyList<int>? DocumentIds);

public sealed record PatientPortalDocumentsDownloadPackage(
    bool Downloadable,
    string FileName,
    string ContentType,
    byte[] Content,
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentItem> Documents,
    string? FailureReason);

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
