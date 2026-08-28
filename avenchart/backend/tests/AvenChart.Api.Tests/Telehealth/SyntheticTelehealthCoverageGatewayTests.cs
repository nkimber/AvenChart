// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthCoverageGatewayTests
{
    private readonly SyntheticTelehealthCoverageGateway _gateway = new();

    [Fact]
    public void KeepsEligibilityAndExactNetworkAsSeparateConfirmedEvidence()
    {
        var result = _gateway.Verify(Input("primary", "Harbor Mutual", "High Deductible"));

        Assert.Equal("NON_PRODUCTION", result.AdapterMode);
        Assert.Equal(TelehealthEligibilityStatus.Active, result.EligibilityStatus);
        Assert.Equal(TelehealthNetworkStatus.ConfirmedInNetwork, result.NetworkStatus);
        Assert.Equal(TelehealthFinancialRoute.ConfirmedInNetwork, result.FinancialRoute);
        Assert.NotEqual(result.EligibilitySource, result.NetworkSource);
        Assert.Contains(result.Limitations, item => item.Contains("not a guarantee", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(64, result.InputFingerprint.Length);
    }

    [Fact]
    public void ActiveCoverageDoesNotImplyNetworkParticipation()
    {
        var result = _gateway.Verify(Input("secondary", "Blue Valley Health", "Premier PPO"));

        Assert.Equal(TelehealthEligibilityStatus.Active, result.EligibilityStatus);
        Assert.Equal(TelehealthNetworkStatus.Unknown, result.NetworkStatus);
        Assert.Equal(TelehealthFinancialRoute.CoverageActiveNetworkPending, result.FinancialRoute);
    }

    [Fact]
    public void UnsupportedFixtureFailsClosedAndIsDeterministic()
    {
        var input = Input("primary", "Unmapped Synthetic Payer", "Unknown Product");

        var first = _gateway.Verify(input);
        var second = _gateway.Verify(input);

        Assert.Equal(TelehealthEligibilityStatus.Unknown, first.EligibilityStatus);
        Assert.Equal(TelehealthNetworkStatus.Unknown, first.NetworkStatus);
        Assert.Equal(TelehealthFinancialRoute.UnableToVerify, first.FinancialRoute);
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Equal(first.EligibilityStatus, second.EligibilityStatus);
        Assert.Equal(first.NetworkStatus, second.NetworkStatus);
        Assert.Equal(first.FinancialRoute, second.FinancialRoute);
        Assert.Equal(first.Limitations, second.Limitations);
    }

    [Fact]
    public void AcknowledgmentIsVersionedSyntheticAndHasNoLegalEffect()
    {
        var response = SyntheticTelehealthAcknowledgment.ToResponse();

        Assert.False(response.LegalEffect);
        Assert.Equal(64, response.ContentHash.Length);
        Assert.Contains(response.Statements, item => item.Contains("not a production", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkflowStatusDoesNotChangeTheSemanticCoverageInputFingerprint()
    {
        var verification = Input("primary", "Harbor Mutual", "High Deductible");
        var operationalReview = verification with { RequestStatus = "OperationalReview" };

        Assert.Equal(
            _gateway.Verify(verification).InputFingerprint,
            _gateway.Verify(operationalReview).InputFingerprint);
    }

    private static TelehealthCoverageGatewayInput Input(string type, string provider, string planName) => new(
        "avenchart-synthetic-practice",
        10,
        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        "MOD-PAT-0012",
        "CA",
        "migraine",
        "Verification",
        "INS-MOD-PAT-0012-P",
        type,
        provider,
        planName,
        new string('a', 64));
}
