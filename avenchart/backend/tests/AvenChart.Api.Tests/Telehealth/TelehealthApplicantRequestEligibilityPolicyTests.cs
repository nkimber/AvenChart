// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestEligibilityPolicyTests
{
    [Fact]
    public void Normalizes_the_exact_bounded_command()
    {
        var normalized = TelehealthApplicantRequestEligibilityPolicy.Normalize(Valid() with
        {
            EligibilitySnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(6, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.EligibilitySnapshotFingerprint);
        Assert.True(normalized.SyntheticDataConfirmed);
        Assert.True(normalized.NoGuaranteeAcknowledged);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Rejects_missing_acknowledgments(bool synthetic, bool noGuarantee)
    {
        Assert.Equal(
            "telehealth_applicant_request_eligibility_acknowledgments_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestEligibilityPolicy.Normalize(Valid() with
                {
                    SyntheticDataConfirmed = synthetic,
                    NoGuaranteeAcknowledged = noGuarantee
                })).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_eligibility_version_invalid")]
    [InlineData(6, "short", "telehealth_applicant_request_eligibility_snapshot_invalid")]
    [InlineData(6, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_eligibility_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestEligibilityPolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    EligibilitySnapshotFingerprint = fingerprint
                })).Code);
    }

    [Fact]
    public void Snapshot_is_stable_masked_and_bound_to_request_source_and_location()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changed = Snapshot(state: "CA");

        Assert.Equal(first, replay);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.Equal("••••1234", first.MaskedMemberId);
        Assert.Equal("••••5678", first.MaskedGroupNumber);
        Assert.DoesNotContain("SYN-MEMBER-1234", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_uses_the_earliest_context_or_applicant_expiry()
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");

        Assert.Equal(now.AddMinutes(20), Snapshot(now.AddMinutes(20), now.AddMinutes(30)).ContextExpiresAt);
        Assert.Equal(now.AddMinutes(15), Snapshot(now.AddMinutes(25), now.AddMinutes(15)).ContextExpiresAt);
    }

    [Fact]
    public void Constants_preserve_the_same_status_request_only_advance()
    {
        Assert.Equal("Verification", TelehealthApplicantRequestEligibilityPolicy.RequestStatus);
        Assert.Equal(6, TelehealthApplicantRequestEligibilityPolicy.EntryRequestVersion);
        Assert.Equal(7, TelehealthApplicantRequestEligibilityPolicy.ResultingRequestVersion);
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
    }

    private static RunTelehealthApplicantRequestEligibilityVerification Valid() => new(
        6,
        new string('a', 64),
        true,
        true);

    private static TelehealthApplicantRequestEligibilitySnapshot Snapshot(
        DateTimeOffset? contextExpiresAt = null,
        DateTimeOffset? applicantExpiresAt = null,
        string state = "GA")
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestEligibilityPolicy.Snapshot(
            Guid.Parse("46000000-0000-4000-8000-000000000001"),
            Guid.Parse("46000000-0000-4000-8000-000000000002"),
            Guid.Parse("46000000-0000-4000-8000-000000000003"),
            Guid.Parse("46000000-0000-4000-8000-000000000004"),
            6,
            "synthetic-patient-46",
            new string('b', 64),
            "Synthetic Payer",
            "Synthetic Silver",
            "1234",
            "5678",
            "Self",
            "Primary",
            state,
            "migraine",
            now.AddMinutes(-2),
            contextExpiresAt ?? now.AddMinutes(20),
            applicantExpiresAt ?? now.AddMinutes(30));
    }
}
