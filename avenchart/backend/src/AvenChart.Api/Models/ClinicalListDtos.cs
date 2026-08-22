// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
    IReadOnlyList<MedicationReconciliationSummary> MedicationReconciliations,
    IReadOnlyList<ImmunizationListItem> Immunizations,
    IReadOnlyList<PrescriptionListItem> Prescriptions,
    IReadOnlyList<PrescriptionDiagnosisInteractionSummary> PrescriptionDiagnosisInteractions,
    IReadOnlyList<PrescriptionRefillRequestItem> PrescriptionRefillRequests);

public sealed record ProblemListItem(
    string Id,
    string Title,
    string? Diagnosis,
    string? Date,
    string? EndDate,
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
    string? EndDate,
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

public sealed record ClinicalMedicationDeactivateRequest(
    string Comments,
    int ExpectedVersion);

public sealed record ClinicalMedicationRestoreRequest(
    string Reason,
    int ExpectedVersion);

public sealed record ClinicalMedicationUpdateRequest(
    string Title,
    string? Diagnosis,
    string Date,
    string? Comments,
    string Reason,
    int ExpectedVersion);

public sealed record ClinicalListMutationResponse(
    string Id,
    ClinicalListsResponse Detail);

public sealed record ClinicalListAuditHistoryResponse(
    string ResourceType,
    string ResourceId,
    string PatientId,
    int EventCount,
    IReadOnlyList<ClinicalListAuditEventItem> Events);

public sealed record ClinicalListAuditEventItem(
    Guid EventId,
    string Action,
    string Actor,
    string? Reason,
    string StateJson,
    string OccurredAt);

public enum ClinicalPrescriptionUpdateStatus
{
    Updated,
    Invalid,
    NotFound,
    PatientInactive,
    Conflict
}

public sealed record ClinicalPrescriptionUpdateResult(
    ClinicalPrescriptionUpdateStatus Status,
    string? CurrentVersion,
    ClinicalListMutationResponse? Mutation);

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
    string? EndDate,
    string? Comments,
    int Activity,
    int LifecycleVersion,
    int LifecycleEventCount);

public enum ClinicalMedicationLifecycleMutationStatus
{
    Updated,
    Invalid,
    NotFound,
    Conflict
}

public sealed record ClinicalMedicationLifecycleMutationResult(
    ClinicalMedicationLifecycleMutationStatus Status,
    ClinicalListMutationResponse? Mutation);

public sealed record ClinicalMedicationLifecycleHistoryResponse(
    string MedicationId,
    int CurrentVersion,
    int EventCount,
    IReadOnlyList<ClinicalMedicationLifecycleEventItem> Events);

public sealed record ClinicalMedicationLifecycleEventItem(
    long EventId,
    string Action,
    int? PreviousActivity,
    int CurrentActivity,
    string Actor,
    string? Reason,
    int ExpectedVersion,
    int ResultingVersion,
    string OccurredAt);

public sealed record MedicationDuplicateSummary(
    string NormalizedTitle,
    string DisplayTitle,
    int ActiveCount,
    IReadOnlyList<string> MedicationIds,
    string? FirstDate,
    string? LatestDate,
    IReadOnlyList<string> Diagnoses);

public sealed record MedicationReconciliationSummary(
    string NormalizedTitle,
    string DisplayTitle,
    string Status,
    int MedicationCount,
    int PrescriptionCount,
    IReadOnlyList<string> MedicationIds,
    IReadOnlyList<string> PrescriptionIds,
    IReadOnlyList<string> MedicationTitles,
    IReadOnlyList<string> PrescriptionDrugs,
    IReadOnlyList<string> Diagnoses);

public sealed record MedicationVocabularyItem(
    string RxNormCode,
    string DrugName,
    string DisplayName,
    string Form,
    string Strength,
    string Route,
    decimal? DoseAmount,
    string? DoseUnit,
    string? Frequency,
    int? DurationDays,
    string? ControlledSubstanceSchedule);

public sealed record ClinicalPharmacyDirectoryResponse(
    string DatasetId,
    string DatasetVersion,
    int PharmacyCount,
    IReadOnlyList<ClinicalPharmacyDirectoryItem> Pharmacies);

public sealed record ClinicalPharmacyDirectoryItem(
    int Id,
    string Name,
    int TransmitMethod,
    string? Email,
    int? Ncpdp,
    int? Npi);

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
    decimal? DoseAmount,
    string? DoseUnit,
    string? Frequency,
    int? DurationDays,
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
    string? ErxPayload,
    string Version);

public sealed record PrescriptionDiagnosisInteractionSummary(
    string Diagnosis,
    string Status,
    string? ProblemId,
    string? ProblemTitle,
    int PrescriptionCount,
    IReadOnlyList<string> PrescriptionIds,
    IReadOnlyList<string> Drugs);

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
    string? StaffResponse,
    string? PatientNote,
    string Body);

public sealed record PrescriptionRefillQueueResponse(
    string DatasetId,
    string DatasetVersion,
    string StatusFilter,
    string? PatientFilter,
    int TotalMatches,
    int ReturnedCount,
    PrescriptionRefillQueueCounts Counts,
    IReadOnlyList<PrescriptionRefillQueueItem> Requests);

public sealed record PrescriptionRefillQueueCounts(
    int Pending,
    int ClarificationRequested,
    int Approved,
    int Denied,
    int Completed,
    int Total);

public sealed record PrescriptionRefillQueueItem(
    int MessageId,
    int ThreadId,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string PortalUsername,
    string PrescriptionId,
    string Drug,
    string? Dosage,
    string? Quantity,
    string? Route,
    int CurrentRefills,
    string RequestDate,
    string Status,
    string? PatientNote,
    string? StaffResponse,
    string UpdatedAt,
    string UpdatedBy);

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
    int? Encounter,
    bool EnteredInError);

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
    decimal? DoseAmount,
    string? DoseUnit,
    string? Frequency,
    int? DurationDays,
    string? Route,
    int Refills,
    string Note,
    string Diagnosis);

public sealed record ClinicalPrescriptionDeactivateRequest(
    string EndDate,
    string Note);

public sealed record ClinicalPrescriptionUpdateRequest(
    string ExpectedVersion,
    string StartDate,
    string Dosage,
    string Quantity,
    decimal? DoseAmount,
    string? DoseUnit,
    string? Frequency,
    int? DurationDays,
    string? Route,
    int Refills,
    string? Diagnosis,
    string? Note,
    string EditReason);

public sealed record ClinicalPrescriptionRefillRequest(
    string RefillDate,
    int AdditionalRefills,
    string Note);

public sealed record ClinicalPrescriptionRefillApprovalRequest(
    string RefillDate,
    int AdditionalRefills,
    string Note);

public sealed record ClinicalPrescriptionRefillDecisionRequest(
    string Action,
    string Response);

public sealed record ClinicalPrescriptionRefillDecisionResponse(
    int MessageId,
    string PrescriptionId,
    string Status,
    string StaffResponse);

public sealed record ClinicalPrescriptionPharmacyRouteRequest(
    int PharmacyId,
    string SentAt,
    string Note);
