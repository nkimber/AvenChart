namespace AvenChart.Api.Models;

public sealed record PatientDocumentsResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    int Count,
    int ActiveCount,
    int ArchivedCount,
    bool IncludesArchived,
    IReadOnlyList<PatientDocumentItem> Documents);

public sealed record PatientDocumentCategoryOptionsResponse(
    string DatasetId,
    string DatasetVersion,
    int MaxFileSizeBytes,
    IReadOnlyList<PatientDocumentCategoryOption> Categories);

public sealed record PatientDocumentCategoryOption(
    int Id,
    string Name);

public sealed record PatientDocumentItem(
    int Id,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    int CategoryId,
    string CategoryName,
    string Name,
    string DocDate,
    string UploadedAt,
    string RevisionAt,
    int CurrentVersion,
    string VersionLabel,
    string VersionStatus,
    int VersionHistoryCount,
    bool HasPriorVersions,
    string? RevisionHash,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    int? Encounter,
    string? StorageMethod,
    string? FileName,
    string? Url,
    string? Hash,
    string? DocumentationOf,
    string? Notes,
    int Deleted,
    string? ArchiveStateActor,
    string? ArchiveStateAt,
    int ArchiveEventCount,
    string ReviewStatus,
    string? ReviewedBy,
    string? ReviewedAt,
    string? ContentPreview,
    string PreviewKind,
    string PreviewStatus,
    string ThumbnailLabel,
    string ThumbnailText,
    string? ThumbnailDataUri,
    bool CanPreviewInline,
    bool CanDownload,
    bool IsScannedAttachment,
    string ScanStatus,
    string CaptureSource,
    int ScanPageCount,
    string OcrStatus,
    IReadOnlyList<PatientDocumentLifecycleEvent> LifecycleEvents);

public sealed record PatientDocumentLifecycleEvent(
    string Code,
    string Label,
    string? OccurredAt,
    string? Actor,
    string Detail);

public sealed record PatientDocumentVersionItem(
    int Version,
    string VersionLabel,
    string VersionStatus,
    string CapturedAt,
    string? RevisionActor,
    string? RevisionReason,
    string RevisionAt,
    string? FileName,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    string? Hash,
    string ContentPreview,
    bool CanDownload);

public sealed record PatientDocumentVersionHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Name,
    int CurrentVersion,
    int VersionCount,
    IReadOnlyList<PatientDocumentVersionItem> Versions);

public sealed record PatientDocumentVersionContentResponse(
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Name,
    int Version,
    string VersionLabel,
    string VersionStatus,
    string RevisionAt,
    string? RevisionActor,
    string? RevisionReason,
    string FileName,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    string? Hash,
    string Content,
    string? ContentBase64,
    bool IsBinary);

public sealed record PatientDocumentReviewEvent(
    Guid EventId,
    string FromStatus,
    string ToStatus,
    string Action,
    string Reason,
    string Actor,
    string OccurredAt,
    int DocumentVersion,
    string? ContentHash);

public sealed record PatientDocumentReviewHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Name,
    string CurrentStatus,
    string? CurrentReviewer,
    string? CurrentReviewedAt,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientDocumentReviewEvent> Events);

public sealed record PatientDocumentArchiveEvent(
    Guid EventId,
    string Action,
    bool FromArchived,
    bool ToArchived,
    string Reason,
    string Actor,
    string OccurredAt,
    int DocumentVersion,
    string ReviewStatus,
    string? ContentHash);

public sealed record PatientDocumentArchiveHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Name,
    bool CurrentArchived,
    string? CurrentStateActor,
    string? CurrentStateAt,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientDocumentArchiveEvent> Events);

public sealed record PatientDocumentOcrQueueResponse(
    string DatasetId,
    string DatasetVersion,
    int Count,
    IReadOnlyList<PatientDocumentOcrQueueItem> Items);

public sealed record PatientDocumentOcrQueueItem(
    int Id,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    int CategoryId,
    string CategoryName,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    string? FileName,
    int? Pages,
    int? Encounter,
    string CaptureSource,
    int ScanPageCount,
    string OcrStatus,
    string QueueStatus,
    string Priority,
    string? Notes);

public sealed record PatientDocumentRoutingQueueResponse(
    string DatasetId,
    string DatasetVersion,
    int Count,
    int TotalCount,
    int ReturnedCount,
    int Offset,
    int Limit,
    string StatusFilter,
    PatientDocumentRoutingQueueCounts Counts,
    IReadOnlyList<PatientDocumentRoutingQueueItem> Items);

public sealed record PatientDocumentRoutingQueueCounts(
    int Active,
    int Pending,
    int InProgress,
    int Unassigned,
    int HighPriority,
    int Overdue,
    int Completed);

public sealed record PatientDocumentRoutingQueueItem(
    int Id,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    int CategoryId,
    string CategoryName,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    string? FileName,
    int? Encounter,
    string ReviewStatus,
    string QueueStatus,
    string RouteDestination,
    string Priority,
    string RoutingReason,
    int TaskVersion,
    bool Inferred,
    string? AssignedTo,
    string? AssignedDisplayName,
    string RoutedAt,
    string DueAt,
    int AgeHours,
    bool IsOverdue,
    string? CompletedBy,
    string? CompletedAt,
    string? CompletionNote,
    string? Notes);

public sealed record PatientDocumentRoutingAssignee(
    int? StaffId,
    string Username,
    string DisplayName,
    string Role);

public sealed record PatientDocumentRoutingAssigneesResponse(
    string DatasetId,
    string DatasetVersion,
    int Count,
    IReadOnlyList<PatientDocumentRoutingAssignee> Assignees);

public sealed record PatientDocumentRoutingMutationRequest(
    string Destination,
    string Priority,
    string? AssignedTo,
    string Reason,
    string? DueAt,
    int ExpectedTaskVersion);

public sealed record PatientDocumentRoutingCompleteRequest(
    string Reason,
    int ExpectedTaskVersion);

public sealed record PatientDocumentRoutingMutationResponse(
    int DocumentId,
    int TaskVersion,
    string Status,
    string? AssignedTo,
    string Destination,
    string Priority,
    string DueAt);

public sealed record PatientDocumentRoutingEvent(
    Guid EventId,
    string Action,
    string FromStatus,
    string ToStatus,
    string? FromDestination,
    string ToDestination,
    string? FromPriority,
    string ToPriority,
    string? FromAssignedTo,
    string? ToAssignedTo,
    string Reason,
    string Actor,
    string OccurredAt,
    string DueAt,
    int TaskVersion,
    int DocumentVersion,
    string ReviewStatus,
    string? ContentHash);

public sealed record PatientDocumentRoutingHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Name,
    int CurrentTaskVersion,
    string CurrentStatus,
    string? CurrentAssignedTo,
    string? CurrentDestination,
    string? CurrentPriority,
    string? CurrentDueAt,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientDocumentRoutingEvent> Events);

public sealed record PatientDocumentRetentionPolicyResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int Count,
    int EligibleCount,
    IReadOnlyList<PatientDocumentRetentionPolicyItem> Items);

public sealed record PatientDocumentRetentionPolicyItem(
    int Id,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    int CategoryId,
    string CategoryName,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    string? FileName,
    int? Encounter,
    string RetentionClass,
    int RetentionYears,
    string RetainUntil,
    string DispositionStatus,
    string PolicyBasis,
    string? Notes);

public sealed record PatientDocumentOcrCompleteRequest(
    string ExtractedText,
    string CompletedBy);

public sealed record PatientDocumentOcrCompleteResponse(
    int Id,
    string OcrStatus,
    string CompletedBy,
    string CompletedAt,
    PatientDocumentContentResponse Document,
    PatientDocumentOcrQueueResponse Queue);

public sealed record PatientDocumentRetentionDispositionRequest(
    string DisposedBy,
    string Reason);

public sealed record PatientDocumentRetentionDispositionResponse(
    int Id,
    string DispositionStatus,
    string DisposedBy,
    string DisposedAt,
    string RetainUntil,
    PatientDocumentsResponse Detail,
    PatientDocumentRetentionPolicyResponse Policy);

public sealed record PatientDocumentCreateRequest(
    string PatientId,
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string Content,
    string? Notes);

public sealed record PatientDocumentBinaryCreateRequest(
    string PatientId,
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string FileName,
    string Mimetype,
    string ContentBase64,
    string? Notes);

public sealed record PatientDocumentScannerCaptureRequest(
    string PatientId,
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string CaptureSource,
    int PageCount,
    string CapturedBy,
    string? Notes);

public sealed record PatientDocumentExternalLinkCreateRequest(
    string PatientId,
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string Url,
    string? Notes);

public sealed record PatientDocumentMetadataUpdateRequest(
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string? Notes,
    string? Reason = null);

public sealed record PatientDocumentMetadataHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    int DocumentId,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    int CurrentCategoryId,
    string CurrentCategoryName,
    string CurrentName,
    string CurrentDocDate,
    int? CurrentEncounter,
    string? CurrentNotes,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientDocumentMetadataHistoryItem> Events);

public sealed record PatientDocumentMetadataHistoryItem(
    Guid EventId,
    IReadOnlyList<string> ChangedFields,
    int FromCategoryId,
    string FromCategoryName,
    int ToCategoryId,
    string ToCategoryName,
    string FromName,
    string ToName,
    string FromDocDate,
    string ToDocDate,
    int? FromEncounter,
    int? ToEncounter,
    string? FromNotes,
    string? ToNotes,
    string Reason,
    string Actor,
    string OccurredAt);

public sealed record EncounterDocumentMoveRequest(
    int TargetEncounter,
    string? Reason = null);

public sealed record EncounterDocumentMoveResponse(
    int Id,
    EncounterDetail SourceDetail,
    EncounterDetail TargetDetail);

public sealed record PatientDocumentContentReplaceRequest(
    string FileName,
    string Content,
    string? Reason = null,
    int? ExpectedVersion = null);

public sealed record PatientDocumentBinaryContentReplaceRequest(
    string FileName,
    string Mimetype,
    string ContentBase64,
    string? Reason = null,
    int? ExpectedVersion = null);

public sealed record PatientDocumentContentResponse(
    int Id,
    string DocumentKey,
    string PatientId,
    int LegacyPid,
    int CategoryId,
    string CategoryName,
    string Name,
    string FileName,
    string DocDate,
    string UploadedAt,
    string RevisionAt,
    int CurrentVersion,
    string VersionLabel,
    string VersionStatus,
    int VersionHistoryCount,
    bool HasPriorVersions,
    string? RevisionHash,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    int? Encounter,
    string? StorageMethod,
    string? Url,
    string? Hash,
    string? DocumentationOf,
    string? Notes,
    string ReviewStatus,
    string? ReviewedBy,
    string? ReviewedAt,
    string Content,
    string? ContentBase64,
    bool IsBinary,
    string PreviewKind,
    string PreviewStatus,
    string ThumbnailLabel,
    string ThumbnailText,
    bool CanPreviewInline,
    bool CanDownload,
    bool IsScannedAttachment,
    string ScanStatus,
    string CaptureSource,
    int ScanPageCount,
    string OcrStatus,
    IReadOnlyList<PatientDocumentLifecycleEvent> LifecycleEvents,
    IReadOnlyList<PatientDocumentVersionItem> VersionHistory);

public sealed record PatientDocumentSignRequest(
    string ReviewStatus,
    string? ReviewedBy = null,
    string? Reason = null,
    string? ExpectedReviewStatus = null);

public sealed record PatientDocumentArchiveRequest(
    string? Reason = null,
    bool? ExpectedArchived = null);

public sealed record PatientDocumentMutationResponse(
    int Id,
    PatientDocumentsResponse Detail);
