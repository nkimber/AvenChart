// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPreRequestReadinessSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantPreRequestReadinessAcknowledgment(
    int ExpectedVersion,
    string PreRequestReadinessSnapshotFingerprint,
    bool PriorSectionsReviewedAcknowledged,
    bool OutstandingStepsRemainAcknowledged,
    bool NoRequestOrQueueCreatedAcknowledged,
    bool CorrectionRequiresSeparateWorkflowAcknowledged);

public static class TelehealthApplicantPreRequestReadinessPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_PRE_REQUEST_READINESS_ACKNOWLEDGMENT_RECEIPT";
    public const string EntryStatus = "SyntheticClinicalInformationSummaryConfirmed";
    public const string ResultingStatus = "SyntheticPreRequestReadinessAcknowledged";

    public static TelehealthApplicantPreRequestReadinessSnapshot Snapshot(
        Guid registrationDetailsConfirmationId,
        string registrationDetailsFingerprint,
        Guid insuranceHandoffConfirmationId,
        string insuranceSnapshotFingerprint,
        Guid communicationAccessReadinessId,
        string communicationContextFingerprint,
        bool interpreterRequested,
        bool accessibilitySupportRequested,
        Guid devicePreparationId,
        string preparationSnapshotFingerprint,
        Guid clinicalInventoryId,
        string inventorySnapshotFingerprint,
        Guid clinicalInformationSummaryConfirmationId,
        string clinicalInformationSummarySnapshotFingerprint,
        string clinicalInformationSummaryRoute) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-pre-request-readiness-snapshot-v1",
                registrationDetailsConfirmationId,
                registrationDetailsFingerprint,
                insuranceHandoffConfirmationId,
                insuranceSnapshotFingerprint,
                communicationAccessReadinessId,
                communicationContextFingerprint,
                interpreterRequested,
                accessibilitySupportRequested,
                devicePreparationId,
                preparationSnapshotFingerprint,
                clinicalInventoryId,
                inventorySnapshotFingerprint,
                clinicalInformationSummaryConfirmationId,
                clinicalInformationSummarySnapshotFingerprint,
                clinicalInformationSummaryRoute));

    public static NormalizedTelehealthApplicantPreRequestReadinessAcknowledgment Normalize(
        AcknowledgeTelehealthApplicantPreRequestReadinessRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_pre_request_readiness_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.PreRequestReadinessSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_pre_request_readiness_fingerprint_invalid",
                "Reload the pre-request readiness review before acknowledging it.");
        }

        if (!request.PriorSectionsReviewedAcknowledged
            || !request.OutstandingStepsRemainAcknowledged
            || !request.NoRequestOrQueueCreatedAcknowledged
            || !request.CorrectionRequiresSeparateWorkflowAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_pre_request_readiness_acknowledgments_required",
                "Confirm every pre-request readiness limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.PriorSectionsReviewedAcknowledged,
            request.OutstandingStepsRemainAcknowledged,
            request.NoRequestOrQueueCreatedAcknowledged,
            request.CorrectionRequiresSeparateWorkflowAcknowledged);
    }

    public static string DetermineOverallRoute(
        string clinicalInformationSummaryRoute,
        bool interpreterRequested,
        bool accessibilitySupportRequested)
    {
        if (clinicalInformationSummaryRoute == "AdditionalClinicalInformationCollectionRequired")
        {
            return "AdditionalClinicalInformationRequired";
        }

        if (interpreterRequested
            || accessibilitySupportRequested
            || clinicalInformationSummaryRoute == "AssistedClinicalInformationReviewRequired")
        {
            return "AssistedPreRequestSupportRequired";
        }

        return "PendingPracticePreRequestReview";
    }

    public static string CommunicationRoute(
        bool interpreterRequested,
        bool accessibilitySupportRequested) =>
        interpreterRequested || accessibilitySupportRequested
            ? "AssistedCommunicationPlanningRequired"
            : "CommunicationReconfirmationRequired";
}
