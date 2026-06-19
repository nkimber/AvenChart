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
    string? Comments);

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
    string? Route,
    string? Diagnosis,
    string? StartDate,
    int? Encounter,
    string? ProviderName);
