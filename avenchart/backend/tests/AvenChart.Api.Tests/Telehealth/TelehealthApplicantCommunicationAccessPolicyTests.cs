// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantCommunicationAccessPolicyTests
{
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void SnapshotMasksCallbackAndBindsSourceEvidence()
    {
        var evaluationId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var handoffId = Guid.Parse("20000000-0000-4000-8000-000000000002");

        var first = TelehealthApplicantCommunicationAccessPolicy.Snapshot(
            evaluationId, handoffId, "GA", "404-555-0199");
        var same = TelehealthApplicantCommunicationAccessPolicy.Snapshot(
            evaluationId, handoffId, "GA", "404-555-0199");
        var changed = TelehealthApplicantCommunicationAccessPolicy.Snapshot(
            evaluationId, handoffId, "FL", "404-555-0199");

        Assert.Equal("(***) ***-0199", first.MaskedCallbackPhone);
        Assert.Equal(first.Fingerprint, same.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
        Assert.Equal(64, first.Fingerprint.Length);
    }

    [Theory]
    [InlineData("English", "English")]
    [InlineData(" english ", "English")]
    [InlineData("SPANISH", "Spanish")]
    public void NormalizeCanonicalizesAllowlistedLanguage(string supplied, string expected)
    {
        var result = TelehealthApplicantCommunicationAccessPolicy.Normalize(Valid(supplied));

        Assert.Equal(expected, result.PreferredSpokenLanguage);
        Assert.True(result.SafeAndPrivateToCommunicateConfirmed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NormalizeRejectsInvalidVersion(int version)
    {
        var request = Valid() with { ExpectedVersion = version };
        Assert.Throws<TelehealthProblem>(() => TelehealthApplicantCommunicationAccessPolicy.Normalize(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void NormalizeRejectsInvalidFingerprint(string fingerprint)
    {
        var request = Valid() with { ContextSnapshotFingerprint = fingerprint };
        Assert.Throws<TelehealthProblem>(() => TelehealthApplicantCommunicationAccessPolicy.Normalize(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("French")]
    [InlineData("English,Spanish")]
    public void NormalizeRejectsLanguageOutsideFixedCatalog(string language)
    {
        Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantCommunicationAccessPolicy.Normalize(Valid(language)));
    }

    [Fact]
    public void NormalizeRejectsAnyMissingRequiredAffirmation()
    {
        var requests = new[]
        {
            Valid() with { CurrentLocationConfirmed = false },
            Valid() with { CallbackNumberConfirmed = false },
            Valid() with { SafeAndPrivateToCommunicateConfirmed = false },
            Valid() with { DisconnectionAndEmergencyPlanAcknowledged = false },
            Valid() with { SyntheticDataConfirmed = false }
        };

        Assert.All(requests, request => Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantCommunicationAccessPolicy.Normalize(request)));
    }

    [Fact]
    public void NormalizePreservesBooleanPreferenceIndicators()
    {
        var result = TelehealthApplicantCommunicationAccessPolicy.Normalize(Valid() with
        {
            InterpreterRequested = true,
            AccessibilitySupportRequested = true
        });

        Assert.True(result.InterpreterRequested);
        Assert.True(result.AccessibilitySupportRequested);
    }

    private static RecordTelehealthApplicantCommunicationAccessReadinessRequest Valid(
        string language = "English") => new(
            15,
            Fingerprint,
            language,
            false,
            false,
            true,
            true,
            true,
            true,
            true);
}
