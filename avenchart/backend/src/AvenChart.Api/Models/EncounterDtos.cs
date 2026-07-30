namespace AvenChart.Api.Models;

public sealed record EncounterSearchResponse(
    string DatasetId,
    string DatasetVersion,
    string? PatientId,
    string FromDate,
    int Limit,
    int TotalMatches,
    IReadOnlyList<EncounterListItem> Encounters);

public sealed record EncounterListItem(
    int Id,
    int Encounter,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string Date,
    string? Reason,
    string? DiagnosisCode,
    string? DiagnosisText,
    int? CategoryId,
    string? ProviderName,
    string? FacilityName,
    string? Sensitivity,
    string? ReferralSource,
    string? ExternalId,
    int? PosCode,
    bool HasVitals,
    bool HasSoapNote,
    int BillingLineCount);

public sealed record EncounterDetail(
    int Id,
    int Encounter,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    string? Sex,
    string DateOfBirth,
    string Date,
    string DateTime,
    string? Reason,
    string? DiagnosisCode,
    string? DiagnosisText,
    int? CategoryId,
    string? ProviderName,
    string? FacilityName,
    string? Sensitivity,
    string? ReferralSource,
    string? ExternalId,
    int? PosCode,
    string? BillingNote,
    string? SourceAppointmentId,
    string? ArchivedAt,
    int ArchiveVersion,
    EncounterVitals? Vitals,
    EncounterSoapNote? SoapNote,
    int BillingLineCount,
    IReadOnlyList<EncounterDiagnosisCode> DiagnosisCodes,
    IReadOnlyList<BillingLineItem> BillingLines,
    IReadOnlyList<BillingClaimItem> Claims,
    IReadOnlyList<ProcedureOrderItem> ProcedureOrders,
    IReadOnlyList<EncounterSignatureItem> Signatures,
    IReadOnlyList<EncounterAmendmentHistoryItem> AmendmentHistory,
    IReadOnlyList<EncounterDocumentAttachment> Documents);

public sealed record EncounterSignatureItem(
    int Id,
    string TableName,
    int? SignerUserId,
    string SignerUsername,
    string SignedAt,
    bool IsLock,
    string? Amendment,
    string Hash,
    string SignatureHash);

public sealed record EncounterAmendmentHistoryItem(
    int SignatureId,
    string SignerUsername,
    string SignedAt,
    bool IsLock,
    string Amendment,
    string Hash,
    string SignatureHash);

public sealed record EncounterAuditEventItem(
    Guid EventId,
    string OccurredAt,
    string Username,
    string Action,
    IReadOnlyList<string> ChangedFields);

public sealed record EncounterAuditHistoryResponse(
    int Encounter,
    int EventCount,
    IReadOnlyList<EncounterAuditEventItem> Events);

public sealed record EncounterDiagnosisCode(
    string Code,
    string? Description,
    IReadOnlyList<string> Sources,
    int BillingLineCount,
    int ProcedureOrderCount,
    IReadOnlyList<string> SupportingBillingCodes);

public sealed record EncounterDocumentAttachment(
    int Id,
    string DocumentKey,
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
    string? StorageMethod,
    string? FileName,
    string? Url,
    string? Hash,
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
    bool CanPreviewInline,
    bool CanDownload,
    bool IsScannedAttachment,
    string ScanStatus,
    string CaptureSource,
    int ScanPageCount,
    string OcrStatus,
    IReadOnlyList<EncounterDocumentLifecycleEvent> LifecycleEvents);

public sealed record EncounterDocumentLifecycleEvent(
    string Code,
    string Label,
    string? OccurredAt,
    string? Actor,
    string Detail);

public sealed record EncounterVitals(
    int? Systolic,
    int? Diastolic,
    string? BloodPressure,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? Pulse,
    int? Respiration,
    decimal? Bmi,
    int? OxygenSaturation);

public sealed record EncounterSoapNote(
    int Id,
    int Version,
    string NoteDateTime,
    string SavedAt,
    string? SavedBy,
    string EvidenceSource,
    bool IsLocked,
    string? Subjective,
    string? Objective,
    string? Assessment,
    string? Plan,
    IReadOnlyList<EncounterSoapNoteVersion> Versions);

public sealed record EncounterSoapNoteVersion(
    int Id,
    int Version,
    int? SupersedesNoteId,
    string NoteDateTime,
    string SavedAt,
    string? SavedBy,
    string EvidenceSource,
    string? Subjective,
    string? Objective,
    string? Assessment,
    string? Plan);

public sealed record EncounterSoapNoteTemplateOption(
    string TemplateId,
    string Name,
    string Category,
    string Description,
    string Subjective,
    string Objective,
    string Assessment,
    string Plan,
    bool IsDefault);

public sealed record EncounterSoapNoteTemplateCatalogResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    IReadOnlyList<EncounterSoapNoteTemplateOption> Templates);

public sealed record EncounterCreateRequest(
    string PatientId,
    int? ProviderId,
    string DateTime,
    string Reason,
    int? FacilityId,
    int? BillingFacilityId,
    string? Sensitivity,
    string? ReferralSource,
    string? ExternalId,
    int? PosCode,
    string? BillingNote,
    string? SourceAppointmentId);

public sealed record EncounterUpdateRequest(
    string Reason,
    string? Sensitivity,
    string? ReferralSource,
    string? ExternalId,
    int? PosCode,
    string? BillingNote);

public sealed record EncounterArchiveRequest(string Reason, int ExpectedArchiveVersion);

public sealed record EncounterVitalsCreateRequest(
    string DateTime,
    int? Systolic,
    int? Diastolic,
    decimal? Weight,
    decimal? Height,
    decimal? Temperature,
    int? Pulse,
    int? Respiration,
    int? OxygenSaturation,
    string? Note);

public sealed record EncounterSoapNoteCreateRequest(
    string DateTime,
    string? Subjective,
    string? Objective,
    string? Assessment,
    string? Plan,
    int? ExpectedVersion);

public sealed record EncounterFormMutationResponse(
    int Id,
    EncounterDetail Detail);

public sealed class EncounterSoapNoteConflictException(
    string message,
    int currentVersion,
    bool isLocked) : Exception(message)
{
    public int CurrentVersion { get; } = currentVersion;

    public bool IsLocked { get; } = isLocked;
}

public sealed record EncounterLayoutFormOption(string Key, string Title, string Value, bool IsDefault);
public sealed record EncounterLayoutFormField(string Key, string GroupKey, string Label, string FieldType, bool Required, int MaxLength, string DefaultValue, IReadOnlyList<EncounterLayoutFormOption> Options);
public sealed record EncounterLayoutFormGroup(string Key, string Title, IReadOnlyList<EncounterLayoutFormField> Fields);
public sealed record EncounterLayoutFormRecord(Guid RecordId, int Revision, string SavedAt, string SavedBy, IReadOnlyDictionary<string, string> Values);
public sealed record EncounterLayoutFormCatalogItem(string Key, string Title);
public sealed record EncounterLayoutFormCatalogResponse(int Encounter, IReadOnlyList<EncounterLayoutFormCatalogItem> Forms);
public sealed record EncounterLayoutFormResponse(int Encounter, string LayoutKey, string Title, IReadOnlyList<EncounterLayoutFormGroup> Groups, EncounterLayoutFormRecord? LatestRecord);
public sealed record EncounterLayoutFormSaveRequest(IReadOnlyDictionary<string, string?> Values);

public sealed record EncounterSignRequest(
    string SignerUsername,
    string SignedAt,
    bool IsLock,
    string? Amendment);

public sealed record EncounterSignatureMutationResponse(
    int Id,
    EncounterDetail Detail);

public sealed record EncounterDocumentCreateRequest(
    int CategoryId,
    string Name,
    string DocDate,
    string Content,
    string? Notes);

public sealed record EncounterBinaryDocumentCreateRequest(
    int CategoryId,
    string Name,
    string DocDate,
    string FileName,
    string Mimetype,
    string ContentBase64,
    string? Notes);

public sealed record EncounterExternalLinkDocumentCreateRequest(
    int CategoryId,
    string Name,
    string DocDate,
    string Url,
    string? Notes);

public sealed record EncounterDocumentMutationResponse(
    int Id,
    EncounterDetail Detail);
