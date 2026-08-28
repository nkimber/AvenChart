// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantInsuranceHandoffPolicyTests
{
    [Fact]
    public void Snapshot_ReturnsOnlyMaskedIdentifiersAndDeterministicFingerprint()
    {
        var first = Snapshot();
        var second = Snapshot();

        Assert.Equal(first, second);
        Assert.Equal("••••A123", first.MemberIdMask);
        Assert.Equal("••••G456", first.GroupNumberMask);
        Assert.False(first.RenderingPhysicianNetworkChecked);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.DoesNotContain("SYN-MEMBER-A123", first.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SYN-GROUP-G456", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_IsNoEditAndDoesNotClaimCoverage()
    {
        Assert.Equal(
            "SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION",
            TelehealthApplicantInsuranceHandoffPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantInsuranceHandoffPolicy.PolicyVersion);
        Assert.Equal(
            "PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION",
            TelehealthApplicantInsuranceHandoffPolicy.EvidenceType);
        Assert.Equal(
            "SyntheticInsuranceDetailsConfirmed",
            TelehealthApplicantInsuranceHandoffPolicy.ResultingStatus);
    }

    [Fact]
    public void Normalize_AcceptsExactFingerprintAndAllAffirmations()
    {
        var normalized = TelehealthApplicantInsuranceHandoffPolicy.Normalize(Valid());

        Assert.Equal(15, normalized.ExpectedVersion);
        Assert.Equal(new string('b', 64), normalized.InsuranceSnapshotFingerprint);
        Assert.True(normalized.EvidenceLimitationsAcknowledged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_RejectsInvalidExpectedVersion(int version)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantInsuranceHandoffPolicy.Normalize(
                Valid() with { ExpectedVersion = version }));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_insurance_handoff_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_RejectsInvalidFingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantInsuranceHandoffPolicy.Normalize(
                Valid() with { InsuranceSnapshotFingerprint = fingerprint }));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_insurance_handoff_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData("payer")]
    [InlineData("member")]
    [InlineData("subscriber")]
    [InlineData("limitations")]
    [InlineData("synthetic")]
    public void Normalize_RejectsEachMissingAffirmation(string field)
    {
        var request = field switch
        {
            "payer" => Valid() with { PayerAndProductConfirmed = false },
            "member" => Valid() with { MaskedMemberDetailsConfirmed = false },
            "subscriber" => Valid() with { SubscriberRelationshipConfirmed = false },
            "limitations" => Valid() with { EvidenceLimitationsAcknowledged = false },
            _ => Valid() with { SyntheticDataConfirmed = false }
        };

        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantInsuranceHandoffPolicy.Normalize(request));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(
            "telehealth_applicant_insurance_handoff_acknowledgments_required",
            problem.Code);
    }

    private static TelehealthApplicantInsuranceHandoffSnapshot Snapshot() =>
        TelehealthApplicantInsuranceHandoffPolicy.Snapshot(
            Guid.Parse("20000000-0000-4000-8000-000000000001"),
            Guid.Parse("20000000-0000-4000-8000-000000000002"),
            Guid.Parse("20000000-0000-4000-8000-000000000003"),
            "Harbor Mutual",
            "High Deductible",
            "A123",
            "G456",
            "Self",
            "Primary",
            "EligibleBenefitsReported",
            DateTimeOffset.Parse("2026-08-28T01:00:00Z"),
            DateTimeOffset.Parse("2026-08-28T01:15:00Z"),
            "PracticeInNetworkAcceptingNewPatients",
            DateTimeOffset.Parse("2026-08-28T01:01:00Z"),
            DateTimeOffset.Parse("2026-08-28T01:16:00Z"),
            false);

    private static ConfirmTelehealthApplicantInsuranceHandoffRequest Valid() => new(
        15,
        new string('b', 64),
        true,
        true,
        true,
        true,
        true);
}
