// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestOperationalReviewSubmissionPolicyTests
{
    [Fact]
    public void Normalizes_the_exact_acknowledgment_only_command()
    {
        var normalized = TelehealthApplicantRequestOperationalReviewSubmissionPolicy.Normalize(
            Valid() with { SubmissionSnapshotFingerprint = new string('A', 64) });

        Assert.Equal(11, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.SubmissionSnapshotFingerprint);
        Assert.True(normalized.SyntheticEvidenceAcknowledged);
        Assert.True(normalized.NoCoverageGuaranteeAcknowledged);
        Assert.True(normalized.PracticeReviewPendingAcknowledged);
        Assert.True(normalized.NoCareRelationshipAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    public void Rejects_each_missing_acknowledgment(
        bool syntheticEvidence,
        bool noGuarantee,
        bool reviewPending,
        bool noCareRelationship)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestOperationalReviewSubmissionPolicy.Normalize(Valid() with
            {
                SyntheticEvidenceAcknowledged = syntheticEvidence,
                NoCoverageGuaranteeAcknowledged = noGuarantee,
                PracticeReviewPendingAcknowledged = reviewPending,
                NoCareRelationshipAcknowledged = noCareRelationship
            }));

        Assert.Equal(
            "telehealth_applicant_request_operational_review_submission_acknowledgments_required",
            problem.Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_operational_review_submission_version_invalid")]
    [InlineData(11, "short",
        "telehealth_applicant_request_operational_review_submission_snapshot_invalid")]
    [InlineData(11, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_operational_review_submission_snapshot_invalid")]
    public void Rejects_an_invalid_version_or_snapshot(int version, string snapshot, string expectedCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestOperationalReviewSubmissionPolicy.Normalize(Valid() with
            {
                ExpectedRequestVersion = version,
                SubmissionSnapshotFingerprint = snapshot
            }));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_bound_to_every_operational_input()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changedEvaluation = Snapshot(evaluationId:
            Guid.Parse("51000000-0000-4000-8000-000000000099"));
        var changedEvidence = Snapshot(evaluationFingerprint: new string('b', 64));

        Assert.Equal(first, replay);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, changedEvaluation);
        Assert.NotEqual(first, changedEvidence);
    }

    [Fact]
    public void Constants_preserve_the_review_boundary()
    {
        Assert.Equal("Verification", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestStatus);
        Assert.Equal(11, TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestVersion);
        Assert.Equal("OperationalReview", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestStatus);
        Assert.Equal(12, TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestVersion);
        Assert.Equal("NON_PRODUCTION", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.SourceMode);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.OperationalReview));
    }

    private static SubmitTelehealthApplicantRequestForOperationalReview Valid() => new(
        11,
        new string('a', 64),
        true,
        true,
        true,
        true);

    private static string Snapshot(Guid? evaluationId = null, string? evaluationFingerprint = null) =>
        TelehealthApplicantRequestOperationalReviewSubmissionPolicy.SnapshotFingerprint(
            Guid.Parse("51000000-0000-4000-8000-000000000001"),
            Guid.Parse("51000000-0000-4000-8000-000000000002"),
            evaluationId ?? Guid.Parse("51000000-0000-4000-8000-000000000003"),
            11,
            "avenchart-synthetic-practice",
            10,
            "synthetic-patient-51",
            "AvenChart Synthetic Practice",
            "Harbor Mutual",
            "High Deductible",
            "GA",
            "migraine",
            new DateOnly(2026, 8, 29),
            101,
            "Alex Walker",
            "8101",
            "ProfessionalTelehealthConsultation",
            "RealTimeAudioVideo",
            evaluationFingerprint ?? new string('a', 64),
            DateTimeOffset.Parse("2026-08-29T12:00:00Z"),
            DateTimeOffset.Parse("2026-08-29T12:09:00Z"));
}
