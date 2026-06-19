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
    string? ContentPreview);

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
    bool IsBinary);

public sealed record PatientDocumentSignRequest(
    string ReviewStatus,
    string ReviewedBy);

public sealed record PatientDocumentMutationResponse(
    int Id,
    PatientDocumentsResponse Detail);
