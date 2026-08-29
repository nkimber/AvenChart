// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestQueueAuthorizationPolicyTests
{
    [Fact]
    public void Normalize_AcceptsExactPositiveCommand()
    {
        var normalized = TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(Valid());

        Assert.Equal(12, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.AuthorizationSnapshotFingerprint);
        Assert.True(normalized.SyntheticEvidenceReviewed);
        Assert.True(normalized.NoCoverageGuaranteeAcknowledged);
        Assert.True(normalized.PracticeAcceptsForQueueAcknowledged);
        Assert.True(normalized.QueueNotCareAcknowledged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_RejectsNonPositiveVersion(int version)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(
                Valid() with { ExpectedRequestVersion = version }));

        Assert.Equal("telehealth_applicant_request_queue_authorization_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Normalize_RejectsMalformedSnapshot(string snapshot)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(
                Valid() with { AuthorizationSnapshotFingerprint = snapshot }));

        Assert.Equal("telehealth_applicant_request_queue_authorization_snapshot_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_CanonicalizesSnapshotCaseAndWhitespace()
    {
        var normalized = TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(
            Valid() with { AuthorizationSnapshotFingerprint = $"  {new string('A', 64)}  " });

        Assert.Equal(new string('a', 64), normalized.AuthorizationSnapshotFingerprint);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_RequiresEveryAcknowledgment(
        bool evidence,
        bool noCoverage,
        bool practiceAccepts,
        bool queueNotCare)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(Valid() with
            {
                SyntheticEvidenceReviewed = evidence,
                NoCoverageGuaranteeAcknowledged = noCoverage,
                PracticeAcceptsForQueueAcknowledged = practiceAccepts,
                QueueNotCareAcknowledged = queueNotCare
            }));

        Assert.Equal("telehealth_applicant_request_queue_authorization_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Contract_UsesDedicatedVersionedQueueBoundary()
    {
        Assert.Equal("OperationalReview", TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestStatus);
        Assert.Equal(12, TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestVersion);
        Assert.Equal("Queued", TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestStatus);
        Assert.Equal(13, TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestVersion);
        Assert.Equal("NON_PRODUCTION", TelehealthApplicantRequestQueueAuthorizationPolicy.SourceMode);
        Assert.Equal("AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1", TelehealthApplicantRequestQueueAuthorizationPolicy.CompatibilityTarget);
        Assert.Equal("SyntheticRequestAuthorizedToQueue", TelehealthApplicantRequestQueueAuthorizationPolicy.BusinessOutcome);
    }

    [Fact]
    public void Snapshot_ChangesWhenSourceBindingChanges()
    {
        var first = Snapshot("migraine");
        var second = Snapshot("sleep");

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
    }

    private static AuthorizeTelehealthApplicantRequestToQueue Valid() => new(
        12, new string('a', 64), true, true, true, true);

    private static string Snapshot(string purpose) =>
        TelehealthApplicantRequestQueueAuthorizationPolicy.SnapshotFingerprint(
            Guid.Parse("12000000-0000-4000-8000-000000000012"),
            Guid.Parse("51000000-0000-4000-8000-000000000051"),
            Guid.Parse("26000000-0000-4000-8000-000000000026"),
            12,
            "avenchart-synthetic-practice",
            10,
            "synthetic-patient",
            new string('b', 64),
            "AvenChart Synthetic Practice",
            "Harbor Mutual",
            "Synthetic Choice",
            "GA",
            purpose,
            new DateOnly(2026, 8, 29),
            99,
            "Dr Synthetic",
            "1234",
            "ProfessionalTelehealthConsultation",
            "RealTimeAudioVideo",
            DateTimeOffset.Parse("2026-08-29T12:00:00Z"),
            DateTimeOffset.Parse("2026-08-29T13:00:00Z"));
}
