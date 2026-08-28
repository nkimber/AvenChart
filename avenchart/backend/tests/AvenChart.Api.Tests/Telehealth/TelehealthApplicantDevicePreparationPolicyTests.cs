// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantDevicePreparationPolicyTests
{
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void SnapshotIsDeterministicAndSourceBound()
    {
        var readinessId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var first = TelehealthApplicantDevicePreparationPolicy.Snapshot(
            readinessId, Fingerprint, "GA", "0199");
        var second = TelehealthApplicantDevicePreparationPolicy.Snapshot(
            readinessId, Fingerprint, "GA", "0199");
        var changed = TelehealthApplicantDevicePreparationPolicy.Snapshot(
            readinessId, Fingerprint, "CA", "0199");

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void NormalizeAcceptsPassingUnknownNetworkResult()
    {
        var result = TelehealthApplicantDevicePreparationPolicy.Normalize(Valid("unknown"));

        Assert.Equal("Unknown", result.NetworkQuality);
        Assert.True(result.BrowserSupported);
        Assert.True(result.CameraAvailable);
        Assert.True(result.MicrophoneAvailable);
        Assert.True(result.SpeakerAvailable);
    }

    [Fact]
    public void NormalizeCanonicalizesGoodNetworkResultAndFingerprint()
    {
        var result = TelehealthApplicantDevicePreparationPolicy.Normalize(
            Valid(" GOOD ") with { PreparationSnapshotFingerprint = Fingerprint.ToUpperInvariant() });

        Assert.Equal("Good", result.NetworkQuality);
        Assert.Equal(Fingerprint, result.PreparationSnapshotFingerprint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NormalizeRejectsInvalidVersion(int version)
    {
        Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantDevicePreparationPolicy.Normalize(Valid() with { ExpectedVersion = version }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void NormalizeRejectsInvalidFingerprint(string fingerprint)
    {
        Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantDevicePreparationPolicy.Normalize(
                Valid() with { PreparationSnapshotFingerprint = fingerprint }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Limited")]
    [InlineData("Excellent")]
    public void NormalizeRejectsUnavailableOrLimitedNetworkVocabulary(string networkQuality)
    {
        Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantDevicePreparationPolicy.Normalize(Valid(networkQuality)));
    }

    [Fact]
    public void NormalizeRejectsAnyMissingCapability()
    {
        var requests = new[]
        {
            Valid() with { BrowserSupported = false },
            Valid() with { CameraAvailable = false },
            Valid() with { MicrophoneAvailable = false },
            Valid() with { SpeakerAvailable = false }
        };

        Assert.All(requests, request => Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantDevicePreparationPolicy.Normalize(request)));
    }

    [Fact]
    public void NormalizeRejectsAnyMissingAcknowledgment()
    {
        var requests = new[]
        {
            Valid() with { ClientReportedResultAcknowledged = false },
            Valid() with { NoReadinessGuaranteeAcknowledged = false },
            Valid() with { RecheckBeforeConsultationAcknowledged = false }
        };

        Assert.All(requests, request => Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantDevicePreparationPolicy.Normalize(request)));
    }

    private static RecordTelehealthApplicantDevicePreparationRequest Valid(
        string networkQuality = "Good") => new(
            16,
            Fingerprint,
            true,
            true,
            true,
            true,
            networkQuality,
            true,
            true,
            true);
}
