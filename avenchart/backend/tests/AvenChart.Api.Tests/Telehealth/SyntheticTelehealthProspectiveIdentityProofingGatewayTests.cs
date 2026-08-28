// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthProspectiveIdentityProofingGatewayTests
{
    private static readonly Guid ApplicantId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SyntheticTelehealthProspectiveIdentityProofingGateway _gateway = new();

    [Fact]
    public async Task CheckAsync_returns_only_the_approved_process_fixture()
    {
        var result = await _gateway.CheckAsync(Inquiry(), CancellationToken.None);

        Assert.Equal("SimulatedCompleted", result.TransportOutcome);
        Assert.Equal("FixtureReferenceAccepted", result.EvidenceCollectionStatus);
        Assert.Equal("ValidatedFixture", result.EvidenceValidationStatus);
        Assert.Equal("ValidatedFixture", result.AttributeValidationStatus);
        Assert.Equal("VerifiedFixture", result.ApplicantVerificationStatus);
        Assert.Equal("NoIndicatorFixture", result.FraudCheckStatus);
        Assert.Equal("SyntheticProofingPassed", result.BusinessOutcome);
    }

    [Fact]
    public async Task CheckAsync_emits_fixed_nonproduction_metadata_and_short_lived_opaque_references()
    {
        var result = await _gateway.CheckAsync(Inquiry(), CancellationToken.None);

        Assert.Equal("NON_PRODUCTION", result.AdapterMode);
        Assert.Equal("NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY", result.CompatibilityTarget);
        Assert.Equal("SYNTHETIC_IDENTITY_PRACTICE_STATEMENT", result.PracticeStatementKey);
        Assert.Equal("avenchart-synthetic-identity-proofing-2026-08", result.DatasetKey);
        Assert.NotEqual(Guid.Empty, result.RequestTraceToken);
        Assert.NotEqual(Guid.Empty, result.ResponseTraceToken);
        Assert.NotEqual(result.RequestTraceToken, result.ResponseTraceToken);
        Assert.StartsWith("syn-proof-session-", result.ProofingSessionReference, StringComparison.Ordinal);
        Assert.Equal($"syn-evidence-{ApplicantId:N}", result.EvidencePackageReference);
        Assert.Equal(CheckedAt, result.CheckedAt);
        Assert.Equal(CheckedAt.AddMinutes(15), result.ExpiresAt);
    }

    [Fact]
    public async Task CheckAsync_keeps_traces_nondeterministic_and_process_outcome_deterministic()
    {
        var first = await _gateway.CheckAsync(Inquiry(), CancellationToken.None);
        var second = await _gateway.CheckAsync(Inquiry(), CancellationToken.None);

        Assert.NotEqual(first.RequestTraceToken, second.RequestTraceToken);
        Assert.NotEqual(first.ResponseTraceToken, second.ResponseTraceToken);
        Assert.NotEqual(first.ProofingSessionReference, second.ProofingSessionReference);
        Assert.Equal(first.BusinessOutcome, second.BusinessOutcome);
        Assert.Equal(first.EvidenceValidationStatus, second.EvidenceValidationStatus);
    }

    [Fact]
    public async Task CheckAsync_accepts_only_the_three_approved_states()
    {
        foreach (var state in new[] { "GA", "CA", "FL" })
        {
            var result = await _gateway.CheckAsync(
                Inquiry() with { CurrentLocationStateCode = state },
                CancellationToken.None);
            Assert.Equal("SyntheticProofingPassed", result.BusinessOutcome);
        }

        var problem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(
                Inquiry() with { CurrentLocationStateCode = "NY" },
                CancellationToken.None));
        Assert.Equal("telehealth_applicant_identity_proofing_dataset_unavailable", problem.Code);
    }

    [Fact]
    public async Task CheckAsync_rejects_practice_notice_evidence_or_profile_substitution()
    {
        var inquiries = new[]
        {
            Inquiry() with { PracticeId = "another-practice" },
            Inquiry() with { PrivacyNoticeVersion = 2 },
            Inquiry() with { EvidencePackageReference = "syn-evidence-substituted" },
            Inquiry() with { ProofingProfile = "BIOMETRIC" }
        };

        foreach (var inquiry in inquiries)
        {
            var problem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
                await _gateway.CheckAsync(inquiry, CancellationToken.None));
            Assert.Equal("telehealth_applicant_identity_proofing_dataset_unavailable", problem.Code);
        }
    }

    [Fact]
    public async Task CheckAsync_honors_cancellation_before_creating_any_result()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _gateway.CheckAsync(Inquiry(), cancellation.Token));
    }

    private static TelehealthProspectiveIdentityProofingInquiry Inquiry() => new(
        ApplicantId,
        SyntheticTelehealthProspectiveIdentityProofingGateway.PracticeId,
        SyntheticTelehealthProspectiveIdentityProofingGateway.FacilityId,
        "GA",
        SyntheticTelehealthProspectiveIdentityProofingGateway.ProofingProfile,
        SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeKey,
        SyntheticTelehealthProspectiveIdentityProofingGateway.PrivacyNoticeVersion,
        $"syn-evidence-{ApplicantId:N}",
        CheckedAt);
}
