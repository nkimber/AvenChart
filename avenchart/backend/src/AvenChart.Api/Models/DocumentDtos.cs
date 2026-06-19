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
    string? Url,
    string? Hash,
    string? DocumentationOf,
    string? Notes,
    string? ContentPreview);

public sealed record PatientDocumentCreateRequest(
    string PatientId,
    int CategoryId,
    string Name,
    string DocDate,
    int? Encounter,
    string Content,
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
    string Content);

public sealed record PatientDocumentMutationResponse(
    int Id,
    PatientDocumentsResponse Detail);
