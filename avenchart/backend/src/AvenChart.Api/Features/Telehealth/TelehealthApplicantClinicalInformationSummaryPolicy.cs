// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantClinicalInformationSummarySnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantClinicalInformationSummaryConfirmation(
    int ExpectedVersion,
    string ClinicalInformationSummarySnapshotFingerprint,
    bool PatientReportedMayBeIncompleteAcknowledged,
    bool NotClinicallyVerifiedOrReconciledAcknowledged,
    bool NoIntakeCompletionOrEligibilityAcknowledged,
    bool CorrectionRequiresSeparateWorkflowAcknowledged);

public static class TelehealthApplicantClinicalInformationSummaryPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_CLINICAL_INFORMATION_SUMMARY_CONFIRMATION_RECEIPT";
    public const string EntryStatus = "SyntheticHealthHistoryInformationRecorded";
    public const string ResultingStatus = "SyntheticClinicalInformationSummaryConfirmed";

    public static TelehealthApplicantClinicalInformationSummarySnapshot Snapshot(
        Guid clinicalInventoryId,
        string medicationsStatus,
        string allergiesStatus,
        string otherHealthHistoryStatus,
        Guid medicationInformationId,
        string medicationInformationSnapshotFingerprint,
        int medicationItemCount,
        bool additionalMedicationItemsReported,
        string medicationReviewRoute,
        Guid allergyInformationId,
        string allergyInformationSnapshotFingerprint,
        int allergyItemCount,
        bool additionalAllergyItemsReported,
        string allergyReviewRoute,
        Guid healthHistoryInformationId,
        string healthHistoryInformationSnapshotFingerprint,
        int healthHistoryTopicCount,
        bool additionalHealthHistoryTopicsReported,
        string healthHistoryReviewRoute) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-clinical-information-summary-snapshot-v1",
                clinicalInventoryId,
                medicationsStatus,
                allergiesStatus,
                otherHealthHistoryStatus,
                medicationInformationId,
                medicationInformationSnapshotFingerprint,
                medicationItemCount,
                additionalMedicationItemsReported,
                medicationReviewRoute,
                allergyInformationId,
                allergyInformationSnapshotFingerprint,
                allergyItemCount,
                additionalAllergyItemsReported,
                allergyReviewRoute,
                healthHistoryInformationId,
                healthHistoryInformationSnapshotFingerprint,
                healthHistoryTopicCount,
                additionalHealthHistoryTopicsReported,
                healthHistoryReviewRoute));

    public static NormalizedTelehealthApplicantClinicalInformationSummaryConfirmation Normalize(
        ConfirmTelehealthApplicantClinicalInformationSummaryRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_summary_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.ClinicalInformationSummarySnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_summary_fingerprint_invalid",
                "Reload the clinical-information summary before confirming it.");
        }

        if (!request.PatientReportedMayBeIncompleteAcknowledged
            || !request.NotClinicallyVerifiedOrReconciledAcknowledged
            || !request.NoIntakeCompletionOrEligibilityAcknowledged
            || !request.CorrectionRequiresSeparateWorkflowAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_summary_acknowledgments_required",
                "Confirm every clinical-information summary limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.PatientReportedMayBeIncompleteAcknowledged,
            request.NotClinicallyVerifiedOrReconciledAcknowledged,
            request.NoIntakeCompletionOrEligibilityAcknowledged,
            request.CorrectionRequiresSeparateWorkflowAcknowledged);
    }

    public static string DetermineSummaryRoute(
        string medicationsStatus,
        string allergiesStatus,
        string otherHealthHistoryStatus,
        bool additionalMedicationItemsReported,
        bool additionalAllergyItemsReported,
        bool additionalHealthHistoryTopicsReported)
    {
        if (additionalMedicationItemsReported
            || additionalAllergyItemsReported
            || additionalHealthHistoryTopicsReported)
        {
            return "AdditionalClinicalInformationCollectionRequired";
        }

        var statuses = new[] { medicationsStatus, allergiesStatus, otherHealthHistoryStatus };
        if (statuses.Contains("Unsure", StringComparer.Ordinal))
        {
            return "AssistedClinicalInformationReviewRequired";
        }

        return statuses.Contains("ItemsToReview", StringComparer.Ordinal)
            ? "ClinicianClinicalInformationReviewRequired"
            : "PendingClinicianReconciliationOfPatientReportedNone";
    }
}
