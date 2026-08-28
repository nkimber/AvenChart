// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPreRequestReadinessPolicyTests
{
    private const string Fingerprint =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPreRequestReadinessPolicy.Normalize(Request(expectedVersion: 0)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_pre_request_readiness_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPreRequestReadinessPolicy.Normalize(Request(fingerprint: fingerprint)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_pre_request_readiness_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool sections,
        bool outstanding,
        bool noRequest,
        bool correction)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPreRequestReadinessPolicy.Normalize(Request(
                sectionsAcknowledged: sections,
                outstandingAcknowledged: outstanding,
                noRequestAcknowledged: noRequest,
                correctionAcknowledged: correction)));

        Assert.Equal(
            "telehealth_applicant_pre_request_readiness_acknowledgments_required",
            problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_the_fingerprint_without_accepting_section_content()
    {
        var normalized = TelehealthApplicantPreRequestReadinessPolicy.Normalize(
            Request(fingerprint: Fingerprint.ToUpperInvariant()));

        Assert.Equal(34, normalized.ExpectedVersion);
        Assert.Equal(Fingerprint, normalized.PreRequestReadinessSnapshotFingerprint);
    }

    [Theory]
    [InlineData("AdditionalClinicalInformationCollectionRequired", false, false,
        "AdditionalClinicalInformationRequired")]
    [InlineData("AdditionalClinicalInformationCollectionRequired", true, true,
        "AdditionalClinicalInformationRequired")]
    [InlineData("AssistedClinicalInformationReviewRequired", false, false,
        "AssistedPreRequestSupportRequired")]
    [InlineData("ClinicianClinicalInformationReviewRequired", true, false,
        "AssistedPreRequestSupportRequired")]
    [InlineData("PendingClinicianReconciliationOfPatientReportedNone", false, true,
        "AssistedPreRequestSupportRequired")]
    [InlineData("ClinicianClinicalInformationReviewRequired", false, false,
        "PendingPracticePreRequestReview")]
    [InlineData("PendingClinicianReconciliationOfPatientReportedNone", false, false,
        "PendingPracticePreRequestReview")]
    public void Overall_route_priority_is_server_owned(
        string summaryRoute,
        bool interpreterRequested,
        bool accessibilityRequested,
        string expected)
    {
        Assert.Equal(
            expected,
            TelehealthApplicantPreRequestReadinessPolicy.DetermineOverallRoute(
                summaryRoute,
                interpreterRequested,
                accessibilityRequested));
    }

    [Theory]
    [InlineData(false, false, "CommunicationReconfirmationRequired")]
    [InlineData(true, false, "AssistedCommunicationPlanningRequired")]
    [InlineData(false, true, "AssistedCommunicationPlanningRequired")]
    [InlineData(true, true, "AssistedCommunicationPlanningRequired")]
    public void Communication_route_is_bounded(
        bool interpreterRequested,
        bool accessibilityRequested,
        string expected)
    {
        Assert.Equal(
            expected,
            TelehealthApplicantPreRequestReadinessPolicy.CommunicationRoute(
                interpreterRequested,
                accessibilityRequested));
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_source_provenance()
    {
        var first = Snapshot(interpreterRequested: false);
        var replay = Snapshot(interpreterRequested: false);
        var changed = Snapshot(interpreterRequested: true);

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static TelehealthApplicantPreRequestReadinessSnapshot Snapshot(
        bool interpreterRequested) =>
        TelehealthApplicantPreRequestReadinessPolicy.Snapshot(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Fingerprint,
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Fingerprint,
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Fingerprint,
            interpreterRequested,
            false,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            Fingerprint,
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Fingerprint,
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            Fingerprint,
            "ClinicianClinicalInformationReviewRequired");

    private static AcknowledgeTelehealthApplicantPreRequestReadinessRequest Request(
        int expectedVersion = 34,
        string fingerprint = Fingerprint,
        bool sectionsAcknowledged = true,
        bool outstandingAcknowledged = true,
        bool noRequestAcknowledged = true,
        bool correctionAcknowledged = true) => new(
            expectedVersion,
            fingerprint,
            sectionsAcknowledged,
            outstandingAcknowledged,
            noRequestAcknowledged,
            correctionAcknowledged);
}
