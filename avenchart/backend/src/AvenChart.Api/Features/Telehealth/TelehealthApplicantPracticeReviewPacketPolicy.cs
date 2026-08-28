// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthApplicantPracticeReviewPacketPolicy
{
    public const string PolicyKey = "SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET";
    public const int PolicyVersion = 1;

    private static readonly HashSet<string> ReviewRoutes =
    [
        "AdditionalClinicalInformationRequired",
        "AssistedPreRequestSupportRequired",
        "PendingPracticePreRequestReview"
    ];

    private static readonly HashSet<string> ClinicalRoutes =
    [
        "AdditionalClinicalInformationCollectionRequired",
        "AssistedClinicalInformationReviewRequired",
        "ClinicianClinicalInformationReviewRequired",
        "PendingClinicianReconciliationOfPatientReportedNone"
    ];

    public static void RequireAllowed(TelehealthApplicantPracticeReviewPacketRecord record)
    {
        if (record.ApplicantStatus != "SyntheticPracticeReviewSubmitted"
            || record.CaseStatus != "PendingPracticeReview"
            || !ReviewRoutes.Contains(record.ReviewRoute)
            || record.PurposeCategory is not ("migraine" or "sleep")
            || record.SafetyOutcome != "TelehealthEligible"
            || !ClinicalRoutes.Contains(record.ClinicalInformationSummaryRoute)
            || record.EligibilityBusinessOutcome != "EligibleBenefitsReported"
            || record.PracticeNetworkBusinessOutcome != "PracticeInNetworkAcceptingNewPatients"
            || record.RenderingPhysicianNetworkChecked
            || record.SubscriberRelationship is not ("Self" or "Spouse" or "Parent" or "Other")
            || record.CoveragePriority != "Primary"
            || record.PreferredSpokenLanguage is not ("English" or "Spanish")
            || record.NetworkQuality is not ("Unknown" or "Good")
            || !record.BrowserSupported
            || !record.CameraAvailable
            || !record.MicrophoneAvailable
            || !record.SpeakerAvailable
            || !record.SafePrivateCommunicationConfirmed)
        {
            throw new InvalidOperationException(
                "A claimant-bound practice-review packet crossed its bounded synthetic policy.");
        }
    }
}
