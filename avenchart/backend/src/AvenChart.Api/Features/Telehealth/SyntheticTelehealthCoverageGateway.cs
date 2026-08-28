// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public interface ITelehealthCoverageGateway
{
    TelehealthCoverageGatewayResult Verify(TelehealthCoverageGatewayInput input);
}

public sealed class SyntheticTelehealthCoverageGateway : ITelehealthCoverageGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string EvidenceKey = "avenchart-synthetic-coverage-2026-08";
    public const int EvidenceVersion = 1;

    public TelehealthCoverageGatewayResult Verify(TelehealthCoverageGatewayInput input)
    {
        var inputFingerprint = TelehealthCommandFingerprint.Create(
            EvidenceKey,
            EvidenceVersion,
            input.PracticeId,
            input.FacilityId,
            input.RequestId,
            input.PatientId,
            input.StateCode,
            input.ComplaintCategory,
            input.CoverageRecordId,
            input.CoverageType,
            input.Provider,
            input.PlanName,
            input.CoverageFingerprint);

        if (string.Equals(input.Provider, "Harbor Mutual", StringComparison.Ordinal)
            && string.Equals(input.PlanName, "High Deductible", StringComparison.Ordinal)
            && string.Equals(input.CoverageType, "primary", StringComparison.Ordinal))
        {
            return new(
                AdapterMode,
                TelehealthEligibilityStatus.Active,
                TelehealthNetworkStatus.ConfirmedInNetwork,
                TelehealthFinancialRoute.ConfirmedInNetwork,
                "SYNTHETIC_X12_271_FIXTURE",
                "SYNTHETIC_EXACT_CONTRACT_ROSTER",
                EvidenceKey,
                EvidenceVersion,
                inputFingerprint,
                [
                    "Synthetic fixture only; no payer or provider directory was contacted.",
                    "This is not a guarantee of coverage, payment, benefits, or patient responsibility."
                ]);
        }

        if (string.Equals(input.Provider, "Blue Valley Health", StringComparison.Ordinal))
        {
            return new(
                AdapterMode,
                TelehealthEligibilityStatus.Active,
                TelehealthNetworkStatus.Unknown,
                TelehealthFinancialRoute.CoverageActiveNetworkPending,
                "SYNTHETIC_X12_271_FIXTURE",
                "SYNTHETIC_NETWORK_SOURCE_UNAVAILABLE",
                EvidenceKey,
                EvidenceVersion,
                inputFingerprint,
                [
                    "Synthetic coverage appears active, but exact network participation is unknown.",
                    "Active coverage is not proof that this practice or physician is in network.",
                    "No payer or provider directory was contacted."
                ]);
        }

        return new(
            AdapterMode,
            TelehealthEligibilityStatus.Unknown,
            TelehealthNetworkStatus.Unknown,
            TelehealthFinancialRoute.UnableToVerify,
            "SYNTHETIC_ELIGIBILITY_SOURCE_UNAVAILABLE",
            "SYNTHETIC_NETWORK_SOURCE_UNAVAILABLE",
            EvidenceKey,
            EvidenceVersion,
            inputFingerprint,
            [
                "The deterministic fixture has no result for this synthetic coverage record.",
                "Unknown must not be represented as covered or in network."
            ]);
    }
}

public static class SyntheticTelehealthAcknowledgment
{
    public const string Kind = "SyntheticDemonstrationAcknowledgment";
    public const string PackageKey = "avenchart-synthetic-readiness-acknowledgment";
    public const int PackageVersion = 1;
    public const string Title = "Synthetic telehealth demonstration acknowledgment";

    public static readonly IReadOnlyList<string> Statements =
    [
        "This environment is a demonstration and cannot provide patient care.",
        "I will use only synthetic information and will not enter real symptoms or personal data.",
        "Coverage and network results are deterministic fixtures, not payer responses or guarantees of payment.",
        "This acknowledgment is not a production telehealth treatment consent."
    ];

    public static readonly string ContentHash = TelehealthCommandFingerprint.Create(
        Kind,
        PackageKey,
        PackageVersion,
        string.Join('\u001f', Statements));

    public static TelehealthAcknowledgmentPackageResponse ToResponse() => new(
        Kind,
        PackageKey,
        PackageVersion,
        ContentHash,
        Title,
        Statements,
        false);
}
