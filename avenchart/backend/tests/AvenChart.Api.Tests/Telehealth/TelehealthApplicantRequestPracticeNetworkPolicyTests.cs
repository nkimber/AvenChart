// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestPracticeNetworkPolicyTests
{
    [Fact]
    public void Normalizes_the_exact_bounded_command()
    {
        var normalized = TelehealthApplicantRequestPracticeNetworkPolicy.Normalize(Valid() with
        {
            NetworkSnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(7, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.NetworkSnapshotFingerprint);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.True(normalized.PracticeOnlyScopeAcknowledged);
        Assert.True(normalized.NoGuaranteeAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Rejects_missing_acknowledgments(bool synthetic, bool practiceOnly, bool noGuarantee)
    {
        Assert.Equal(
            "telehealth_applicant_request_practice_network_acknowledgments_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestPracticeNetworkPolicy.Normalize(Valid() with
                {
                    SyntheticDataConfirmed = synthetic,
                    PracticeOnlyScopeAcknowledged = practiceOnly,
                    NoGuaranteeAcknowledged = noGuarantee
                })).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_practice_network_version_invalid")]
    [InlineData(7, "short", "telehealth_applicant_request_practice_network_snapshot_invalid")]
    [InlineData(7, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_practice_network_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestPracticeNetworkPolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    NetworkSnapshotFingerprint = fingerprint
                })).Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_bound_to_practice_plan_location_and_eligibility()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changedState = Snapshot(state: "CA");
        var changedPractice = Snapshot(practiceDisplayName: "Different Synthetic Practice");

        Assert.Equal(first, replay);
        Assert.NotEqual(first.Fingerprint, changedState.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changedPractice.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.DoesNotContain("member", first.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Snapshot_uses_the_earliest_eligibility_or_applicant_expiry()
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");

        Assert.Equal(now.AddMinutes(12), Snapshot(now.AddMinutes(12), now.AddMinutes(30)).ContextExpiresAt);
        Assert.Equal(now.AddMinutes(8), Snapshot(now.AddMinutes(20), now.AddMinutes(8)).ContextExpiresAt);
    }

    [Fact]
    public void Constants_preserve_the_same_status_request_only_advance()
    {
        Assert.Equal("Verification", TelehealthApplicantRequestPracticeNetworkPolicy.RequestStatus);
        Assert.Equal(7, TelehealthApplicantRequestPracticeNetworkPolicy.EntryRequestVersion);
        Assert.Equal(8, TelehealthApplicantRequestPracticeNetworkPolicy.ResultingRequestVersion);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
    }

    private static RunTelehealthApplicantRequestPracticeNetworkVerification Valid() => new(
        7,
        new string('a', 64),
        true,
        true,
        true);

    private static TelehealthApplicantRequestPracticeNetworkSnapshot Snapshot(
        DateTimeOffset? eligibilityExpiresAt = null,
        DateTimeOffset? applicantExpiresAt = null,
        string state = "GA",
        string practiceDisplayName = "AvenChart Synthetic Practice")
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestPracticeNetworkPolicy.Snapshot(
            Guid.Parse("47000000-0000-4000-8000-000000000001"),
            Guid.Parse("47000000-0000-4000-8000-000000000002"),
            Guid.Parse("47000000-0000-4000-8000-000000000003"),
            7,
            "synthetic-patient-47",
            "avenchart-synthetic-practice",
            10,
            practiceDisplayName,
            "harbor-mutual-hd",
            "Harbor Mutual",
            "High Deductible",
            state,
            "migraine",
            "EligibleBenefitsReported",
            now.AddMinutes(-3),
            eligibilityExpiresAt ?? now.AddMinutes(12),
            applicantExpiresAt ?? now.AddMinutes(30));
    }
}
