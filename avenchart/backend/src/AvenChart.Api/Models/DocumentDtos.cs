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
    IReadOnlyList<PatientDocumentLifecycleEvent> LifecycleEvents);

public sealed record PatientDocumentLifecycleEvent(
    string Code,
    string Label,
    string? OccurredAt,
    string? Actor,
    string Detail);

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
    IReadOnlyList<PatientDocumentLifecycleEvent> LifecycleEvents);

public sealed record PatientDocumentSignRequest(
    string ReviewStatus,
    string ReviewedBy);

public sealed record PatientDocumentMutationResponse(
    int Id,
    PatientDocumentsResponse Detail);
