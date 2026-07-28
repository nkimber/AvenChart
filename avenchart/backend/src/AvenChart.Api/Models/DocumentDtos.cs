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
    string? FileName,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    string? Hash,
    string ContentPreview);

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
    IReadOnlyList<PatientDocumentRoutingQueueItem> Items);

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
    string? Notes);

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
    string? Notes);

public sealed record EncounterDocumentMoveRequest(
    int TargetEncounter);

public sealed record EncounterDocumentMoveResponse(
    int Id,
    EncounterDetail SourceDetail,
    EncounterDetail TargetDetail);

public sealed record PatientDocumentContentReplaceRequest(
    string FileName,
    string Content);

public sealed record PatientDocumentBinaryContentReplaceRequest(
    string FileName,
    string Mimetype,
    string ContentBase64);

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
    string ReviewedBy);

public sealed record PatientDocumentMutationResponse(
    int Id,
    PatientDocumentsResponse Detail);
