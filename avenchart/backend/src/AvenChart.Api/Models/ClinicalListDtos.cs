namespace AvenChart.Api.Models;

public sealed record ClinicalListsResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    IReadOnlyList<ProblemListItem> Problems,
    IReadOnlyList<AllergyListItem> Allergies,
    IReadOnlyList<MedicationListItem> Medications,
    IReadOnlyList<PrescriptionListItem> Prescriptions);

public sealed record ProblemListItem(
    string Id,
    string Title,
    string? Diagnosis,
    string? Date,
    string? Comments);

public sealed record AllergyListItem(
    string Id,
    string Title,
    string? Reaction,
    string? Severity,
    string? Date,
    string? Comments,
    int Activity,
    string? ListOptionId);

public sealed record ClinicalAllergyCreateRequest(
    string PatientId,
    string Title,
    string DateTime,
    string Comments,
    string Reaction,
    string Severity,
    string? ListOptionId);

public sealed record ClinicalListDeactivateRequest(string Comments);

public sealed record ClinicalListMutationResponse(
    string Id,
    ClinicalListsResponse Detail);

public sealed record MedicationListItem(
    string Id,
    string Title,
    string? Diagnosis,
    string? Date,
    string? Comments);

public sealed record PrescriptionListItem(
    string Id,
    string Drug,
    string? Dosage,
    string? Quantity,
    string? Route,
    string? RxNormCode,
    string? Diagnosis,
    string? StartDate,
    string? EndDate,
    int Refills,
    int Active,
    string? Note,
    int? Encounter,
    string? ProviderName);

public sealed record ClinicalPrescriptionCreateRequest(
    string PatientId,
    int? ProviderId,
    string StartDate,
    string Drug,
    string? RxNormCode,
    string Dosage,
    string Quantity,
    string? Route,
    int Refills,
    string Note,
    string Diagnosis);

public sealed record ClinicalPrescriptionDeactivateRequest(
    string EndDate,
    string Note);
