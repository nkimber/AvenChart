// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewInboxSection(
    string SectionKey,
    string ReceiptState,
    string OutstandingRoute);

public static class TelehealthApplicantPracticeReviewInboxPolicy
{
    public const string PolicyKey = "SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX";
    public const int PolicyVersion = 1;
    public const int MaximumItems = 100;

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

    public static void RequireAllowed(
        string applicantStatus,
        string caseStatus,
        string reviewRoute,
        string purposeCategory,
        string safetyOutcome,
        string clinicalInformationSummaryRoute)
    {
        if (applicantStatus != "SyntheticPracticeReviewSubmitted"
            || caseStatus != "PendingPracticeReview"
            || !ReviewRoutes.Contains(reviewRoute)
            || purposeCategory is not ("migraine" or "sleep")
            || safetyOutcome != "TelehealthEligible"
            || !ClinicalRoutes.Contains(clinicalInformationSummaryRoute))
        {
            throw new InvalidOperationException(
                "A practice-review inbox item crossed its bounded synthetic read-only policy.");
        }
    }

    public static IReadOnlyList<TelehealthApplicantPracticeReviewInboxSection> Sections(
        bool interpreterRequested,
        bool accessibilitySupportRequested,
        string clinicalInformationSummaryRoute) =>
    [
        new("Registration", "ReceiptRecorded", "IdentityAndIntakeCompletionStillRequired"),
        new("Insurance", "ReceiptRecorded", "CoverageAndRenderingClinicianVerificationStillRequired"),
        new(
            "CommunicationAccess",
            "PreferencesRecorded",
            TelehealthApplicantPreRequestReadinessPolicy.CommunicationRoute(
                interpreterRequested,
                accessibilitySupportRequested)),
        new("DevicePreparation", "ClientReportedPreparationRecorded", "PreConsultationTechnologyRecheckRequired"),
        new("ClinicalInformation", "PatientReportedSummaryConfirmed", clinicalInformationSummaryRoute)
    ];
}
