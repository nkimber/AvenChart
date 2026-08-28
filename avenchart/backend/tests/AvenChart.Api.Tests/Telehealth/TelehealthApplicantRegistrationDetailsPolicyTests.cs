// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRegistrationDetailsPolicyTests
{
    [Fact]
    public void Snapshot_ReturnsOnlyBoundedDisplayFieldsAndDeterministicFingerprint()
    {
        var first = TelehealthApplicantRegistrationDetailsPolicy.Snapshot(
            "Avery", "Example", new DateOnly(1990, 3, 14),
            "avery@example.test", "+14045550199", "GA", "30303");
        var second = TelehealthApplicantRegistrationDetailsPolicy.Snapshot(
            "Avery", "Example", new DateOnly(1990, 3, 14),
            "avery@example.test", "+14045550199", "GA", "30303");

        Assert.Equal(first, second);
        Assert.Equal("a•••@example.test", first.MaskedEmail);
        Assert.Equal("(***) ***-0199", first.MaskedPhone);
        Assert.Equal(64, first.Fingerprint.Length);
        Assert.DoesNotContain("avery@example.test", first.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+14045550199", first.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_IsNoEditAndNoAssurance()
    {
        Assert.Equal(
            "SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION",
            TelehealthApplicantRegistrationDetailsPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantRegistrationDetailsPolicy.PolicyVersion);
        Assert.Equal(
            "PROMOTED_PATIENT_MINIMUM_DETAILS_NO_EDIT_CONFIRMATION",
            TelehealthApplicantRegistrationDetailsPolicy.EvidenceType);
        Assert.Equal(
            "SyntheticMinimumRegistrationDetailsConfirmed",
            TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus);
    }

    [Fact]
    public void Normalize_AcceptsExactFingerprintAndAllAffirmations()
    {
        var normalized = TelehealthApplicantRegistrationDetailsPolicy.Normalize(Valid());

        Assert.Equal(14, normalized.ExpectedVersion);
        Assert.Equal(new string('a', 64), normalized.DetailsFingerprint);
        Assert.True(normalized.NoCorrectionsNeededConfirmed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_RejectsInvalidExpectedVersion(int version)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRegistrationDetailsPolicy.Normalize(
                Valid() with { ExpectedVersion = version }));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_registration_details_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_RejectsInvalidFingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRegistrationDetailsPolicy.Normalize(
                Valid() with { DetailsFingerprint = fingerprint }));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_registration_details_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("contact")]
    [InlineData("residence")]
    [InlineData("correction")]
    [InlineData("synthetic")]
    public void Normalize_RejectsEachMissingAffirmation(string field)
    {
        var request = field switch
        {
            "name" => Valid() with { LegalNameAndBirthDateConfirmed = false },
            "contact" => Valid() with { ContactChannelsConfirmed = false },
            "residence" => Valid() with { ResidenceRegionConfirmed = false },
            "correction" => Valid() with { NoCorrectionsNeededConfirmed = false },
            _ => Valid() with { SyntheticDataConfirmed = false }
        };

        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRegistrationDetailsPolicy.Normalize(request));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(
            "telehealth_applicant_registration_details_acknowledgments_required",
            problem.Code);
    }

    private static ConfirmTelehealthApplicantRegistrationDetailsRequest Valid() => new(
        14,
        new string('a', 64),
        true,
        true,
        true,
        true,
        true);
}
