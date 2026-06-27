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
    IReadOnlyList<MedicationDuplicateSummary> MedicationDuplicates,
    IReadOnlyList<ImmunizationListItem> Immunizations,
    IReadOnlyList<PrescriptionListItem> Prescriptions,
    IReadOnlyList<PrescriptionRefillRequestItem> PrescriptionRefillRequests);

public sealed record ProblemListItem(
    string Id,
    string Title,
    string? Diagnosis,
    string? Date,
    string? Comments,
    int Activity);

public sealed record ClinicalProblemCreateRequest(
    string PatientId,
    string Title,
    string DateTime,
    string? Diagnosis,
    string Comments);

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

public sealed record ClinicalPrescriptionPharmacyRouteResponse(
    string Id,
    bool Routed,
    string? FailureReason,
    ClinicalListsResponse Detail);

public sealed record ClinicalPrescriptionAuditHistoryResponse(
    string PrescriptionId,
    int EventCount,
    IReadOnlyList<ClinicalPrescriptionAuditEventItem> Events);

public sealed record ClinicalPrescriptionAuditEventItem(
    string EventId,
    string PrescriptionId,
    string Action,
    string OccurredAt,
    string Actor,
    string? Detail,
    int? BeforeRefills,
    int? AfterRefills,
    int? PharmacyId,
    string? PharmacyName,
    string? FailureReason);

public sealed record MedicationListItem(
    string Id,
    string Title,
    string? Diagnosis,
    string? Date,
    string? Comments,
    int Activity);

public sealed record MedicationDuplicateSummary(
    string NormalizedTitle,
    string DisplayTitle,
    int ActiveCount,
    IReadOnlyList<string> MedicationIds,
    string? FirstDate,
    string? LatestDate,
    IReadOnlyList<string> Diagnoses);

public sealed record ClinicalMedicationCreateRequest(
    string PatientId,
    string Title,
    string DateTime,
    string? Diagnosis,
    string Comments);

public sealed record PrescriptionListItem(
    string Id,
    string Drug,
    string? Dosage,
    string? Quantity,
    string? Route,
    string? RxNormCode,
    string? ControlledSubstanceSchedule,
    bool ControlledSubstanceReviewRequired,
    string? ControlledSubstanceReason,
    string? Diagnosis,
    string? StartDate,
    string? EndDate,
    int Refills,
    int Active,
    string? Note,
    int? Encounter,
    string? ProviderName,
    int? PharmacyId,
    string? PharmacyName,
    int? PharmacyNcpdp,
    int ErxUploaded,
    string? ErxSentAt,
    string? ErxPayload);

public sealed record PrescriptionRefillRequestItem(
    int MessageId,
    string Title,
    string RequestDate,
    string PatientDisplayName,
    string PortalUsername,
    string PrescriptionId,
    string Drug,
    string? Dosage,
    string? Quantity,
    string? Route,
    int CurrentRefills,
    string Status,
    string? PatientNote,
    string Body);

public sealed record ImmunizationListItem(
    int Id,
    string Key,
    int? ImmunizationId,
    string? CvxCode,
    string Vaccine,
    string? AdministeredAt,
    string? Manufacturer,
    string? LotNumber,
    string? AdministeredBy,
    string? EducationDate,
    string? VisDate,
    decimal? AmountAdministered,
    string? AmountAdministeredUnit,
    string? ExpirationDate,
    string? Route,
    string? AdministrationSite,
    string? CompletionStatus,
    string? InformationSource,
    string? Note,
    int? Encounter);

public sealed record ClinicalImmunizationCreateRequest(
    string PatientId,
    int? Encounter,
    int? ImmunizationId,
    string? CvxCode,
    string Vaccine,
    string AdministeredAt,
    string? Manufacturer,
    string? LotNumber,
    int? AdministeredById,
    string? AdministeredBy,
    string? EducationDate,
    string? VisDate,
    decimal? AmountAdministered,
    string? AmountAdministeredUnit,
    string? ExpirationDate,
    string? Route,
    string? AdministrationSite,
    string? CompletionStatus,
    string? InformationSource,
    string? Note);

public sealed record ClinicalImmunizationErrorRequest(string Note);

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

public sealed record ClinicalPrescriptionRefillRequest(
    string RefillDate,
    int AdditionalRefills,
    string Note);

public sealed record ClinicalPrescriptionRefillApprovalRequest(
    string RefillDate,
    int AdditionalRefills,
    string Note);

public sealed record ClinicalPrescriptionPharmacyRouteRequest(
    int PharmacyId,
    string SentAt,
    string Note);
