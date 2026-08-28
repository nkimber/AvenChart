// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPromotionAuthorizationPolicyTests
{
    [Theory]
    [InlineData("AuthorizedForSyntheticPromotion", "SyntheticPromotionAuthorized")]
    [InlineData("DeniedForSyntheticPromotion", "SyntheticPromotionDenied")]
    public void DecisionMapsOnlyToProspectiveGovernanceState(string decision, string expected) =>
        Assert.Equal(expected,
            TelehealthApplicantPromotionAuthorizationPolicy.ResultingStatus(decision));

    [Fact]
    public void PolicyMetadataMakesTheSyntheticBoundaryExplicit()
    {
        Assert.Equal("SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION",
            TelehealthApplicantPromotionAuthorizationPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantPromotionAuthorizationPolicy.PolicyVersion);
        Assert.Equal("COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY",
            TelehealthApplicantPromotionAuthorizationPolicy.EvidenceType);
    }

    [Fact]
    public void UnknownDecisionFailsBeforePersistence()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPromotionAuthorizationPolicy.ResultingStatus("CreatePatient"));
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_promotion_authorization_decision_invalid", problem.Code);
    }

    [Fact]
    public void ReasonIsTrimmedButRetainedAsExplicitStaffEvidence() =>
        Assert.Equal("Synthetic chain reviewed.",
            TelehealthApplicantPromotionAuthorizationPolicy.NormalizeReason(
                "  Synthetic chain reviewed.  "));

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    [InlineData("Synthetic\u0000evidence")]
    public void InvalidReasonFailsBeforePersistence(string reason)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPromotionAuthorizationPolicy.NormalizeReason(reason));
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_promotion_authorization_reason_invalid", problem.Code);
    }
}
