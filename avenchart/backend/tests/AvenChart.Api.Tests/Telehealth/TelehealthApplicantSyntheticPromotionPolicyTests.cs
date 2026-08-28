// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantSyntheticPromotionPolicyTests
{
    [Fact]
    public void CanonicalPatientId_IsDeterministicAndSynthetic()
    {
        var applicantId = Guid.Parse("a3ca4479-1895-4b99-9c44-8e81a802b8e4");

        Assert.Equal(
            "TH-PAT-A3CA447918954B999C448E81A802B8E4",
            TelehealthApplicantSyntheticPromotionPolicy.CanonicalPatientId(applicantId));
    }

    [Theory]
    [InlineData(false, "SyntheticPatientCreated", "SyntheticPatientPromoted")]
    [InlineData(true, "BlockedPossiblePatientMatch", "SyntheticPromotionBlockedPossibleMatch")]
    public void OutcomeAndStatus_KeepDuplicateBlockDistinct(
        bool possibleMatch,
        string expectedOutcome,
        string expectedStatus)
    {
        Assert.Equal(expectedOutcome, TelehealthApplicantSyntheticPromotionPolicy.Outcome(possibleMatch));
        Assert.Equal(expectedStatus, TelehealthApplicantSyntheticPromotionPolicy.ResultingStatus(possibleMatch));
    }

    [Fact]
    public void Contract_IsExplicitlySyntheticAndVersioned()
    {
        Assert.Equal("SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION", TelehealthApplicantSyntheticPromotionPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantSyntheticPromotionPolicy.PolicyVersion);
        Assert.Equal("AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK", TelehealthApplicantSyntheticPromotionPolicy.EvidenceType);
        Assert.Equal("PromoteAuthorizedSyntheticApplicant", TelehealthApplicantSyntheticPromotionPolicy.Command);
    }

    [Fact]
    public void NormalizeReason_TrimsValidReason()
    {
        Assert.Equal(
            "Reviewed current evidence and approved the bounded synthetic transaction.",
            TelehealthApplicantSyntheticPromotionPolicy.NormalizeReason(
                "  Reviewed current evidence and approved the bounded synthetic transaction.  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    [InlineData("unsupported\u0001control")]
    public void NormalizeReason_RejectsInvalidInput(string reason)
    {
        var error = Assert.Throws<TelehealthProblem>(
            () => TelehealthApplicantSyntheticPromotionPolicy.NormalizeReason(reason));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("telehealth_applicant_synthetic_promotion_reason_invalid", error.Code);
    }

    [Fact]
    public void NormalizeReason_RejectsOverlongInput()
    {
        var error = Assert.Throws<TelehealthProblem>(
            () => TelehealthApplicantSyntheticPromotionPolicy.NormalizeReason(new string('x', 1001)));

        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }
}
