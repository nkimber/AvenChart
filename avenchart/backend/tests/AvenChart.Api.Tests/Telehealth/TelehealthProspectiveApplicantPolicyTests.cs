// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthProspectiveApplicantPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 27);
    private static readonly string[] States = ["GA", "CA", "FL"];

    [Fact]
    public void NormalizesOnlyMinimumAdultSyntheticApplicantData()
    {
        var result = TelehealthProspectiveApplicantPolicy.Normalize(
            ValidRequest() with
            {
                LegalFirstName = "  Avery   Anne ",
                Email = " AVERY@EXAMPLE.TEST ",
                Phone = "(404) 555-0199",
                ResidenceStateCode = "ga"
            },
            States,
            Today);

        Assert.Equal("Avery Anne", result.LegalFirstName);
        Assert.Equal("avery@example.test", result.Email);
        Assert.Equal("+14045550199", result.Phone);
        Assert.Equal("GA", result.ResidenceStateCode);
    }

    [Theory]
    [InlineData("2008-08-28", "telehealth_applicant_adult_required")]
    [InlineData("not-a-date", "telehealth_applicant_birth_date_invalid")]
    public void RejectsMinorOrInvalidBirthDate(string dateOfBirth, string expectedCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveApplicantPolicy.Normalize(
                ValidRequest() with { DateOfBirth = dateOfBirth }, States, Today));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Theory]
    [InlineData("NY", "30301", "telehealth_applicant_state_unsupported")]
    [InlineData("GA", "3030", "telehealth_applicant_postal_code_invalid")]
    public void RejectsUnsupportedResidenceOrPostalCode(
        string state,
        string postalCode,
        string expectedCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveApplicantPolicy.Normalize(
                ValidRequest() with { ResidenceStateCode = state, PostalCode = postalCode },
                States,
                Today));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Fact]
    public void RequiresExplicitSyntheticAcknowledgment()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveApplicantPolicy.Normalize(
                ValidRequest() with { SyntheticDataConfirmed = false }, States, Today));

        Assert.Equal("telehealth_applicant_synthetic_acknowledgment_required", problem.Code);
    }

    [Fact]
    public void AccessKeyIsHighEntropyAndHashComparisonIsConstantTime()
    {
        var key = new string('a', 64);
        var hash = TelehealthProspectiveApplicantPolicy.Hash(
            TelehealthProspectiveApplicantPolicy.RequireAccessKey(key));

        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(key, hash, StringComparison.Ordinal);
        Assert.True(TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(hash, hash));
        Assert.False(TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(
            hash, TelehealthProspectiveApplicantPolicy.Hash(new string('b', 64))));
        Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveApplicantPolicy.RequireAccessKey("too-short"));
    }

    [Fact]
    public void PublicContactProjectionIsMasked()
    {
        Assert.Equal("a•••@example.test", TelehealthProspectiveApplicantPolicy.MaskEmail("avery@example.test"));
        Assert.Equal("(***) ***-0199", TelehealthProspectiveApplicantPolicy.MaskPhone("+14045550199"));
    }

    private static CreateTelehealthProspectiveApplicantRequest ValidRequest() => new(
        "Avery",
        "Example",
        "1990-03-14",
        "avery@example.test",
        "404-555-0199",
        "GA",
        "30301",
        true);
}

