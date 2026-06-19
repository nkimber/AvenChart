namespace AvenChart.Api.Models;

public sealed record PatientBillingResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    IReadOnlyList<BillingEncounterItem> Encounters);

public sealed record BillingEncounterItem(
    int Id,
    int Encounter,
    string Date,
    string? Reason,
    string? DiagnosisCode,
    string? DiagnosisText,
    string? ProviderName,
    string? FacilityName,
    decimal TotalFee,
    IReadOnlyList<BillingLineItem> Lines);

public sealed record BillingLineItem(
    string Id,
    int Encounter,
    string BillingDate,
    string? CodeType,
    string? Code,
    string? CodeText,
    decimal? Fee,
    string? Justify,
    int Units,
    int Billed,
    int Activity);

public sealed record BillingLineCreateRequest(
    string PatientId,
    int? ProviderId,
    int Encounter,
    string BillingDate,
    string CodeType,
    string Code,
    string CodeText,
    decimal Fee,
    int Units,
    string Justify);

public sealed record BillingLineUpdateRequest(
    string CodeText,
    decimal Fee,
    int Units,
    string Justify);

public sealed record BillingLineStatusUpdateRequest(
    int Billed,
    int Activity);

public sealed record BillingLineMutationResponse(
    string Id,
    PatientBillingResponse Detail);
