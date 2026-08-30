// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticProfessionalClaimGatewayTests
{
    [Fact]
    public async Task ReturnsDeterministicPreparedOnlyReceiptWithoutTransport()
    {
        var gateway = new SyntheticProfessionalClaimGateway();
        var packet = Packet();

        var first = await gateway.PrepareAsync(packet, CancellationToken.None);
        var replay = await gateway.PrepareAsync(packet, CancellationToken.None);

        Assert.Equal("NON_PRODUCTION", first.AdapterMode);
        Assert.Equal("ASC_X12N_837P_005010X222A1", first.TargetStandard);
        Assert.Equal("PreparedOnly", first.ClaimState);
        Assert.Equal(first.CorrelationReference, replay.CorrelationReference);
        Assert.Equal(64, first.CorrelationReference.Length);
        Assert.False(first.TransactionCreated);
        Assert.False(first.ExternalDestinationContacted);
        Assert.False(first.SubmissionAccepted);
        Assert.Contains(first.Limitations, item => item.Contains("not an ASC X12 transaction", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectsAnyNonSyntheticOrIncompletePacket()
    {
        var gateway = new SyntheticProfessionalClaimGateway();

        await Assert.ThrowsAsync<ArgumentException>(() => gateway.PrepareAsync(
            Packet() with { IsSynthetic = false }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gateway.PrepareAsync(
            Packet() with { SourceEvidenceHash = string.Empty }, CancellationToken.None));
    }

    private static TelehealthProfessionalClaimPacket Packet() => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        17,
        "telehealth-claim-v1",
        new string('c', 64),
        IsSynthetic: true);
}
