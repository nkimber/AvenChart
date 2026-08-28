// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPracticeReviewAuthorizationPolicyTests
{
    [Fact]
    public void Policy_is_positive_only_synthetic_and_versioned()
    {
        Assert.Equal("SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION",
            TelehealthApplicantPracticeReviewAuthorizationPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantPracticeReviewAuthorizationPolicy.PolicyVersion);
        Assert.Equal("CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY",
            TelehealthApplicantPracticeReviewAuthorizationPolicy.EvidenceType);
        Assert.Equal("AuthorizedForSyntheticRequestCreation",
            TelehealthApplicantPracticeReviewAuthorizationPolicy.Decision);
        Assert.Equal("OperationalPrerequisitesReviewed",
            TelehealthApplicantPracticeReviewAuthorizationPolicy.RationaleCode);
        Assert.Equal("SyntheticPracticeReviewAuthorized",
            TelehealthApplicantPracticeReviewAuthorizationPolicy.ResultingApplicantStatus);
    }

    [Fact]
    public void RequireCommand_accepts_only_the_controlled_decision_and_all_limits()
    {
        TelehealthApplicantPracticeReviewAuthorizationPolicy.RequireCommand(
            "AuthorizedForSyntheticRequestCreation",
            "OperationalPrerequisitesReviewed",
            true, true, true);
    }

    [Theory]
    [InlineData("Declined", "OperationalPrerequisitesReviewed")]
    [InlineData("AuthorizedForSyntheticRequestCreation", "ClinicalEligibilityConfirmed")]
    [InlineData("", "OperationalPrerequisitesReviewed")]
    public void RequireCommand_rejects_unapproved_decisions_or_rationales(
        string decision,
        string rationale)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewAuthorizationPolicy.RequireCommand(
                decision, rationale, true, true, true));

        Assert.Equal("telehealth_practice_review_authorization_decision_invalid", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void RequireCommand_rejects_each_incomplete_acknowledgment_set(
        bool noClinical,
        bool noCoverage,
        bool noRequestOrQueue)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewAuthorizationPolicy.RequireCommand(
                "AuthorizedForSyntheticRequestCreation",
                "OperationalPrerequisitesReviewed",
                noClinical, noCoverage, noRequestOrQueue));

        Assert.Equal("telehealth_practice_review_authorization_acknowledgments_required", problem.Code);
    }
}
