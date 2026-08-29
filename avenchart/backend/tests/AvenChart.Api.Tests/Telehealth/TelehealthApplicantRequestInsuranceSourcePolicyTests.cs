// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestInsuranceSourcePolicyTests
{
    [Fact]
    public void Normalizes_the_exact_bounded_confirmation()
    {
        var normalized = TelehealthApplicantRequestInsuranceSourcePolicy.Normalize(Valid() with
        {
            InsuranceSourceSnapshotFingerprint = new string('A', 64)
        });

        Assert.Equal(5, normalized.ExpectedRequestVersion);
        Assert.Equal(new string('a', 64), normalized.InsuranceSourceSnapshotFingerprint);
        Assert.All(
            new[]
            {
                normalized.PayerProductConfirmed,
                normalized.MaskedMemberDetailsConfirmed,
                normalized.SubscriberRelationshipConfirmed,
                normalized.PrimaryCoverageSourceConfirmed,
                normalized.FreshVerificationRequested,
                normalized.EvidenceLimitationsAcknowledged,
                normalized.SyntheticDataConfirmed
            },
            Assert.True);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Rejects_each_missing_confirmation(int index)
    {
        var values = Enumerable.Repeat(true, 7).ToArray();
        values[index] = false;
        var request = Valid() with
        {
            PayerProductConfirmed = values[0],
            MaskedMemberDetailsConfirmed = values[1],
            SubscriberRelationshipConfirmed = values[2],
            PrimaryCoverageSourceConfirmed = values[3],
            FreshVerificationRequested = values[4],
            EvidenceLimitationsAcknowledged = values[5],
            SyntheticDataConfirmed = values[6]
        };

        Assert.Equal(
            "telehealth_applicant_request_insurance_source_confirmations_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestInsuranceSourcePolicy.Normalize(request)).Code);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "telehealth_applicant_request_insurance_source_version_invalid")]
    [InlineData(5, "short", "telehealth_applicant_request_insurance_source_snapshot_invalid")]
    [InlineData(5, "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz",
        "telehealth_applicant_request_insurance_source_snapshot_invalid")]
    public void Rejects_invalid_version_or_snapshot(int version, string fingerprint, string expectedCode)
    {
        Assert.Equal(
            expectedCode,
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestInsuranceSourcePolicy.Normalize(Valid() with
                {
                    ExpectedRequestVersion = version,
                    InsuranceSourceSnapshotFingerprint = fingerprint
                })).Code);
    }

    [Fact]
    public void Snapshot_is_stable_masked_and_bound_to_the_source_chain()
    {
        var first = Snapshot();
        var replay = Snapshot();
        var changed = Snapshot(product: "Synthetic Silver 2");

        Assert.Equal(first, replay);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.Equal("••••1234", first.MaskedMemberId);
        Assert.Equal("••••5678", first.MaskedGroupNumber);
        Assert.DoesNotContain("SYNTHETIC-MEMBER-1234", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_uses_the_earliest_context_or_applicant_expiry()
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");

        Assert.Equal(now.AddMinutes(20), Snapshot(now.AddMinutes(20), now.AddMinutes(30)).ContextExpiresAt);
        Assert.Equal(now.AddMinutes(15), Snapshot(now.AddMinutes(25), now.AddMinutes(15)).ContextExpiresAt);
    }

    [Fact]
    public void State_machine_allows_only_the_bounded_same_status_transition()
    {
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification));
        Assert.False(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Queued));
    }

    private static ConfirmTelehealthApplicantRequestInsuranceSource Valid() => new(
        5,
        new string('a', 64),
        true,
        true,
        true,
        true,
        true,
        true,
        true);

    private static TelehealthApplicantRequestInsuranceSourceSnapshot Snapshot(
        DateTimeOffset? contextExpiresAt = null,
        DateTimeOffset? applicantExpiresAt = null,
        string product = "Synthetic Silver")
    {
        var now = DateTimeOffset.Parse("2026-08-29T12:00:00Z");
        return TelehealthApplicantRequestInsuranceSourcePolicy.Snapshot(
            Guid.Parse("45000000-0000-4000-8000-000000000001"),
            Guid.Parse("45000000-0000-4000-8000-000000000002"),
            Guid.Parse("45000000-0000-4000-8000-000000000003"),
            Guid.Parse("45000000-0000-4000-8000-000000000004"),
            Guid.Parse("45000000-0000-4000-8000-000000000005"),
            Guid.Parse("45000000-0000-4000-8000-000000000006"),
            Guid.Parse("45000000-0000-4000-8000-000000000007"),
            Guid.Parse("45000000-0000-4000-8000-000000000008"),
            Guid.Parse("45000000-0000-4000-8000-000000000009"),
            Guid.Parse("45000000-0000-4000-8000-000000000010"),
            Guid.Parse("45000000-0000-4000-8000-000000000011"),
            5,
            "synthetic-patient-45",
            new string('b', 64),
            "Synthetic Payer",
            product,
            "1234",
            "5678",
            "Self",
            "Primary",
            "EligibleBenefitsReported",
            now.AddMinutes(-30),
            now.AddMinutes(30),
            "PracticeInNetworkAcceptingNewPatients",
            now.AddMinutes(-25),
            now.AddMinutes(35),
            now.AddMinutes(-5),
            contextExpiresAt ?? now.AddMinutes(20),
            applicantExpiresAt ?? now.AddMinutes(30));
    }
}
