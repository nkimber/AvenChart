// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPracticeReviewInboxPolicyTests
{
    [Fact]
    public void Policy_is_bounded_to_one_hundred_read_only_items()
    {
        Assert.Equal("SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX",
            TelehealthApplicantPracticeReviewInboxPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantPracticeReviewInboxPolicy.PolicyVersion);
        Assert.Equal(100, TelehealthApplicantPracticeReviewInboxPolicy.MaximumItems);
    }

    [Theory]
    [InlineData("AdditionalClinicalInformationRequired", "migraine", "AdditionalClinicalInformationCollectionRequired")]
    [InlineData("AssistedPreRequestSupportRequired", "sleep", "AssistedClinicalInformationReviewRequired")]
    [InlineData("PendingPracticePreRequestReview", "migraine", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("PendingPracticePreRequestReview", "sleep", "PendingClinicianReconciliationOfPatientReportedNone")]
    public void RequireAllowed_accepts_only_the_bounded_pending_projection(
        string reviewRoute,
        string purpose,
        string clinicalRoute)
    {
        TelehealthApplicantPracticeReviewInboxPolicy.RequireAllowed(
            "SyntheticPracticeReviewSubmitted",
            "PendingPracticeReview",
            reviewRoute,
            purpose,
            "TelehealthEligible",
            clinicalRoute);
    }

    [Theory]
    [InlineData("SyntheticPreRequestReadinessAcknowledged", "PendingPracticeReview", "PendingPracticePreRequestReview", "migraine", "TelehealthEligible", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("SyntheticPracticeReviewSubmitted", "Accepted", "PendingPracticePreRequestReview", "migraine", "TelehealthEligible", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PatientQueue", "migraine", "TelehealthEligible", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PendingPracticePreRequestReview", "cancer", "TelehealthEligible", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PendingPracticePreRequestReview", "migraine", "ClinicalReview", "ClinicianClinicalInformationReviewRequired")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PendingPracticePreRequestReview", "migraine", "TelehealthEligible", "DiagnosisConfirmed")]
    public void RequireAllowed_rejects_any_state_route_purpose_safety_or_clinical_drift(
        string applicantStatus,
        string caseStatus,
        string reviewRoute,
        string purpose,
        string safetyOutcome,
        string clinicalRoute)
    {
        Assert.Throws<InvalidOperationException>(() =>
            TelehealthApplicantPracticeReviewInboxPolicy.RequireAllowed(
                applicantStatus,
                caseStatus,
                reviewRoute,
                purpose,
                safetyOutcome,
                clinicalRoute));
    }

    [Fact]
    public void Sections_return_exactly_five_coarse_server_owned_states()
    {
        var sections = TelehealthApplicantPracticeReviewInboxPolicy.Sections(
            interpreterRequested: false,
            accessibilitySupportRequested: false,
            clinicalInformationSummaryRoute: "ClinicianClinicalInformationReviewRequired");

        Assert.Equal(5, sections.Count);
        Assert.Equal(
            ["Registration", "Insurance", "CommunicationAccess", "DevicePreparation", "ClinicalInformation"],
            sections.Select(section => section.SectionKey));
        Assert.Equal("CommunicationReconfirmationRequired", sections[2].OutstandingRoute);
        Assert.Equal("ClinicianClinicalInformationReviewRequired", sections[4].OutstandingRoute);
    }

    [Fact]
    public void Sections_route_requested_support_without_claiming_it_was_arranged()
    {
        var sections = TelehealthApplicantPracticeReviewInboxPolicy.Sections(
            interpreterRequested: true,
            accessibilitySupportRequested: false,
            clinicalInformationSummaryRoute: "AssistedClinicalInformationReviewRequired");

        Assert.Equal("AssistedCommunicationPlanningRequired", sections[2].OutstandingRoute);
        Assert.DoesNotContain(sections, section => section.ReceiptState.Contains("Arranged", StringComparison.Ordinal));
    }
}
