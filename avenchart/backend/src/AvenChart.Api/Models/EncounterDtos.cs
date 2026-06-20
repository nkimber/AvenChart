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
    EncounterVitals? Vitals,
    EncounterSoapNote? SoapNote,
    int BillingLineCount,
    IReadOnlyList<BillingLineItem> BillingLines,
    IReadOnlyList<EncounterDocumentAttachment> Documents);

public sealed record EncounterDocumentAttachment(
    int Id,
    string DocumentKey,
    int CategoryId,
    string CategoryName,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    int? SizeBytes,
    int? Pages,
    string? StorageMethod,
    string? FileName,
    string? Url,
    string? Hash,
    string? Notes,
    string? ContentPreview,
    string PreviewKind,
    string PreviewStatus,
    string ThumbnailLabel,
    string ThumbnailText,
    bool CanPreviewInline,
    bool CanDownload);

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
    string? Subjective,
    string? Objective,
    string? Assessment,
    string? Plan);

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
    string? BillingNote);

public sealed record EncounterUpdateRequest(
    string Reason,
    string? Sensitivity,
    string? ReferralSource,
    string? ExternalId,
    int? PosCode,
    string? BillingNote);

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
    string? Plan);

public sealed record EncounterFormMutationResponse(
    int Id,
    EncounterDetail Detail);
