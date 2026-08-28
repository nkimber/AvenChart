// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPracticeReviewPacketPolicyTests
{
    [Fact]
    public void Policy_is_versioned_and_read_only()
    {
        Assert.Equal("SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET",
            TelehealthApplicantPracticeReviewPacketPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantPracticeReviewPacketPolicy.PolicyVersion);
    }

    [Fact]
    public void RequireAllowed_accepts_the_bounded_operational_projection()
    {
        TelehealthApplicantPracticeReviewPacketPolicy.RequireAllowed(ValidRecord());
    }

    [Theory]
    [InlineData("SyntheticPreRequestReadinessAcknowledged", "PendingPracticeReview", "PendingPracticePreRequestReview", "migraine", "TelehealthEligible")]
    [InlineData("SyntheticPracticeReviewSubmitted", "Accepted", "PendingPracticePreRequestReview", "migraine", "TelehealthEligible")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PatientQueue", "migraine", "TelehealthEligible")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PendingPracticePreRequestReview", "cancer", "TelehealthEligible")]
    [InlineData("SyntheticPracticeReviewSubmitted", "PendingPracticeReview", "PendingPracticePreRequestReview", "migraine", "DiagnosisConfirmed")]
    public void RequireAllowed_rejects_state_route_purpose_or_safety_drift(
        string applicantStatus,
        string caseStatus,
        string reviewRoute,
        string purpose,
        string safetyOutcome)
    {
        var record = ValidRecord() with
        {
            ApplicantStatus = applicantStatus,
            CaseStatus = caseStatus,
            ReviewRoute = reviewRoute,
            PurposeCategory = purpose,
            SafetyOutcome = safetyOutcome
        };

        Assert.Throws<InvalidOperationException>(() =>
            TelehealthApplicantPracticeReviewPacketPolicy.RequireAllowed(record));
    }

    [Theory]
    [InlineData("Unknown", "PracticeInNetworkAcceptingNewPatients", false)]
    [InlineData("EligibleBenefitsReported", "RenderingClinicianInNetwork", false)]
    [InlineData("EligibleBenefitsReported", "PracticeInNetworkAcceptingNewPatients", true)]
    public void RequireAllowed_rejects_insurance_assurance_drift(
        string eligibility,
        string network,
        bool renderingPhysicianChecked)
    {
        var record = ValidRecord() with
        {
            EligibilityBusinessOutcome = eligibility,
            PracticeNetworkBusinessOutcome = network,
            RenderingPhysicianNetworkChecked = renderingPhysicianChecked
        };

        Assert.Throws<InvalidOperationException>(() =>
            TelehealthApplicantPracticeReviewPacketPolicy.RequireAllowed(record));
    }

    [Theory]
    [InlineData(false, true, true, true, true, "Good")]
    [InlineData(true, false, true, true, true, "Good")]
    [InlineData(true, true, false, true, true, "Good")]
    [InlineData(true, true, true, false, true, "Good")]
    [InlineData(true, true, true, true, false, "Good")]
    [InlineData(true, true, true, true, true, "Excellent")]
    public void RequireAllowed_rejects_device_or_communication_overstatement(
        bool browser,
        bool camera,
        bool microphone,
        bool speaker,
        bool safePrivate,
        string networkQuality)
    {
        var record = ValidRecord() with
        {
            BrowserSupported = browser,
            CameraAvailable = camera,
            MicrophoneAvailable = microphone,
            SpeakerAvailable = speaker,
            SafePrivateCommunicationConfirmed = safePrivate,
            NetworkQuality = networkQuality
        };

        Assert.Throws<InvalidOperationException>(() =>
            TelehealthApplicantPracticeReviewPacketPolicy.RequireAllowed(record));
    }

    private static TelehealthApplicantPracticeReviewPacketRecord ValidRecord()
    {
        var now = DateTimeOffset.Parse("2026-08-28T12:00:00Z");
        return new(
            PracticeReviewCaseId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ApplicantVersion: 31,
            ApplicantStatus: "SyntheticPracticeReviewSubmitted",
            CaseStatus: "PendingPracticeReview",
            LegalFirstName: "Avery",
            LegalLastName: "Example",
            DateOfBirth: new DateOnly(1990, 1, 1),
            Email: "avery@example.invalid",
            Phone: "4045550100",
            ResidenceStateCode: "GA",
            PostalCode: "30303",
            PurposeCategory: "migraine",
            PurposeDisplayLabel: "Migraine or recurring headache",
            SafetyOutcome: "TelehealthEligible",
            ReviewRoute: "PendingPracticePreRequestReview",
            SubmittedAt: now.AddMinutes(-5),
            AssignmentExpiresAt: now.AddMinutes(2),
            PayerDisplayName: "Harbor Mutual",
            ProductDisplayName: "Harbor HD",
            MemberIdLast4: "A123",
            GroupNumberLast4: "B456",
            SubscriberRelationship: "Self",
            CoveragePriority: "Primary",
            EligibilityBusinessOutcome: "EligibleBenefitsReported",
            EligibilityCheckedAt: now.AddHours(-1),
            EligibilityExpiresAt: now.AddHours(1),
            PracticeNetworkBusinessOutcome: "PracticeInNetworkAcceptingNewPatients",
            PracticeNetworkCheckedAt: now.AddMinutes(-50),
            PracticeNetworkExpiresAt: now.AddHours(1),
            RenderingPhysicianNetworkChecked: false,
            PreferredSpokenLanguage: "English",
            InterpreterRequested: false,
            AccessibilitySupportRequested: false,
            SafePrivateCommunicationConfirmed: true,
            BrowserSupported: true,
            CameraAvailable: true,
            MicrophoneAvailable: true,
            SpeakerAvailable: true,
            NetworkQuality: "Good",
            ClinicalInformationSummaryRoute: "ClinicianClinicalInformationReviewRequired",
            RegistrationConfirmedAt: now.AddHours(-1),
            InsuranceConfirmedAt: now.AddMinutes(-55),
            CommunicationRecordedAt: now.AddMinutes(-45),
            DeviceRecordedAt: now.AddMinutes(-40),
            ClinicalSummaryConfirmedAt: now.AddMinutes(-10),
            DatabaseNow: now);
    }
}
