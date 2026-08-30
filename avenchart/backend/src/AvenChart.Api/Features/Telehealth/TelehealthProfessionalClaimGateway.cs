// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>
/// Versioned boundary for a professional-claim clearinghouse. Implementations
/// must receive only a canonical packet; the transport is never inferred from
/// packet creation or from a successful method call.
/// </summary>
public interface IProfessionalClaimGateway
{
    Task<TelehealthProfessionalClaimGatewayReceipt> PrepareAsync(
        TelehealthProfessionalClaimPacket packet,
        CancellationToken cancellationToken);
}

public sealed record TelehealthProfessionalClaimPacket(
    Guid ClaimPreparationId,
    Guid ConsultationId,
    int EncounterId,
    string CanonicalClaimVersion,
    string SourceEvidenceHash,
    bool IsSynthetic);

public sealed record TelehealthProfessionalClaimGatewayReceipt(
    string AdapterMode,
    string AdapterName,
    string TargetStandard,
    string ClaimState,
    string CorrelationReference,
    bool TransactionCreated,
    bool ExternalDestinationContacted,
    bool SubmissionAccepted,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Development-only adapter. It models an 837P preparation handoff without
/// constructing EDI, retaining payload data, contacting a clearinghouse, or
/// representing a payer, acknowledgement, adjudication, or payment outcome.
/// </summary>
public sealed class SyntheticProfessionalClaimGateway : IProfessionalClaimGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string TargetStandard = "ASC_X12N_837P_005010X222A1";

    public Task<TelehealthProfessionalClaimGatewayReceipt> PrepareAsync(
        TelehealthProfessionalClaimPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!packet.IsSynthetic || packet.ClaimPreparationId == Guid.Empty || packet.ConsultationId == Guid.Empty
            || packet.EncounterId < 1 || !string.Equals(packet.CanonicalClaimVersion, "telehealth-claim-v1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(packet.SourceEvidenceHash))
        {
            throw new ArgumentException("Only a complete synthetic canonical claim packet may reach the non-production claim adapter.");
        }

        var correlationReference = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"avenchart-synthetic-professional-claim-v1\u001f{packet.ClaimPreparationId:D}\u001f{packet.SourceEvidenceHash}")));
        return Task.FromResult(new TelehealthProfessionalClaimGatewayReceipt(
            AdapterMode,
            nameof(SyntheticProfessionalClaimGateway),
            TargetStandard,
            ClaimState: "PreparedOnly",
            correlationReference,
            TransactionCreated: false,
            ExternalDestinationContacted: false,
            SubmissionAccepted: false,
            Limitations:
            [
                "This is a deterministic NON_PRODUCTION claim-adapter receipt, not an ASC X12 transaction.",
                "No clearinghouse, payer, pharmacy, or other external destination was contacted.",
                "PreparedOnly does not mean submitted, acknowledged, accepted, adjudicated, paid, or patient-billed."
            ]));
    }
}
