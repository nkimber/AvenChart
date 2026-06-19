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
    string? BillingNote,
    EncounterVitals? Vitals,
    EncounterSoapNote? SoapNote,
    int BillingLineCount);

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
    string? BillingNote);

public sealed record EncounterUpdateRequest(
    string Reason,
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
