// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPracticeReviewSubmissionPolicyTests
{
    private const string Fingerprint =
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewSubmissionPolicy.Normalize(Request(expectedVersion: 0)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_practice_review_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewSubmissionPolicy.Normalize(
                Request(fingerprint: fingerprint)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_practice_review_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool patientReported,
        bool practiceDecision,
        bool noRequestOrQueue,
        bool worsening)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewSubmissionPolicy.Normalize(Request(
                patientReported,
                practiceDecision,
                noRequestOrQueue,
                worsening)));

        Assert.Equal("telehealth_applicant_practice_review_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_the_fingerprint_and_preserves_only_acknowledgments()
    {
        var normalized = TelehealthApplicantPracticeReviewSubmissionPolicy.Normalize(
            Request(fingerprint: Fingerprint.ToUpperInvariant()));

        Assert.Equal(35, normalized.ExpectedVersion);
        Assert.Equal(Fingerprint, normalized.PracticeReviewSnapshotFingerprint);
        Assert.True(normalized.PatientReportedInformationAcknowledged);
        Assert.True(normalized.PracticeMayRequestInformationOrDeclineAcknowledged);
        Assert.True(normalized.NoTelehealthRequestOrCareQueueAcknowledged);
        Assert.True(normalized.WorseningSymptomsRequireImmediateActionAcknowledged);
    }

    [Theory]
    [InlineData("AdditionalClinicalInformationRequired")]
    [InlineData("AssistedPreRequestSupportRequired")]
    [InlineData("PendingPracticePreRequestReview")]
    public void Snapshot_is_stable_for_each_server_owned_review_route(string route)
    {
        var first = Snapshot(route, "SYN-PAT-0001");
        var replay = Snapshot(route, "SYN-PAT-0001");

        Assert.Equal(first, replay);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    [Fact]
    public void Snapshot_changes_with_readiness_route_patient_or_expiry_provenance()
    {
        var baseline = Snapshot("PendingPracticePreRequestReview", "SYN-PAT-0001");
        var routeChanged = Snapshot("AssistedPreRequestSupportRequired", "SYN-PAT-0001");
        var patientChanged = Snapshot("PendingPracticePreRequestReview", "SYN-PAT-0002");
        var expiryChanged = TelehealthApplicantPracticeReviewSubmissionPolicy.Snapshot(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            35,
            Fingerprint,
            "PendingPracticePreRequestReview",
            "SYN-PAT-0001",
            DateTimeOffset.Parse("2026-09-02T00:00:00Z"));

        Assert.NotEqual(baseline, routeChanged);
        Assert.NotEqual(baseline, patientChanged);
        Assert.NotEqual(baseline, expiryChanged);
    }

    [Fact]
    public void Policy_constants_keep_practice_review_separate_from_request_and_care_queues()
    {
        Assert.Equal("SyntheticPreRequestReadinessAcknowledged",
            TelehealthApplicantPracticeReviewSubmissionPolicy.EntryStatus);
        Assert.Equal("SyntheticPracticeReviewSubmitted",
            TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus);
        Assert.Equal("PendingPracticeReview",
            TelehealthApplicantPracticeReviewSubmissionPolicy.ReviewStatus);
        Assert.DoesNotContain("REQUEST", TelehealthApplicantPracticeReviewSubmissionPolicy.EvidenceType);
    }

    private static TelehealthApplicantPracticeReviewSubmissionSnapshot Snapshot(
        string route,
        string patientId) =>
        TelehealthApplicantPracticeReviewSubmissionPolicy.Snapshot(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            35,
            Fingerprint,
            route,
            patientId,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

    private static SubmitTelehealthApplicantPracticeReviewRequest Request(
        bool patientReported = true,
        bool practiceDecision = true,
        bool noRequestOrQueue = true,
        bool worsening = true,
        int expectedVersion = 35,
        string fingerprint = Fingerprint) => new(
            expectedVersion,
            fingerprint,
            patientReported,
            practiceDecision,
            noRequestOrQueue,
            worsening);
}
