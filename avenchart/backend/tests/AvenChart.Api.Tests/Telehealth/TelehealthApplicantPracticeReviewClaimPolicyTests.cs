// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantPracticeReviewClaimPolicyTests
{
    [Fact]
    public void Policy_is_short_lived_synthetic_and_versioned()
    {
        Assert.Equal("SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM",
            TelehealthApplicantPracticeReviewClaimPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantPracticeReviewClaimPolicy.PolicyVersion);
        Assert.Equal("PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT",
            TelehealthApplicantPracticeReviewClaimPolicy.EvidenceType);
        Assert.Equal(120, TelehealthApplicantPracticeReviewClaimPolicy.LeaseSeconds);
    }

    [Fact]
    public void RequireAcknowledgments_accepts_all_three_independent_limits()
    {
        TelehealthApplicantPracticeReviewClaimPolicy.RequireAcknowledgments(true, true, true);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void RequireAcknowledgments_rejects_every_incomplete_combination(
        bool noDecision,
        bool noContact,
        bool noRequestOrQueue)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantPracticeReviewClaimPolicy.RequireAcknowledgments(
                noDecision, noContact, noRequestOrQueue));

        Assert.Equal("telehealth_practice_review_claim_acknowledgments_required", problem.Code);
    }
}
