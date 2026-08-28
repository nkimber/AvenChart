// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantIdentityReviewPolicyTests
{
    [Theory]
    [InlineData("NoCandidate", "ApprovedForProspectiveIntake")]
    [InlineData("PossibleMatchManualReview", "ManualReviewRequired")]
    public void DuplicateEvidenceDeterminesTheOnlyAllowedDecision(string disposition, string expected) =>
        Assert.Equal(expected, TelehealthApplicantIdentityReviewPolicy.AllowedDecision(disposition));

    [Fact]
    public void UnknownDuplicateEvidenceFailsClosed()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantIdentityReviewPolicy.AllowedDecision("CandidateSelected"));
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("telehealth_applicant_identity_review_evidence_invalid", problem.Code);
    }

    [Theory]
    [InlineData("ApprovedForProspectiveIntake", "IdentityReviewApproved")]
    [InlineData("ManualReviewRequired", "ManualReviewRequired")]
    public void DecisionMapsOnlyToProspectiveTerminalReviewState(string decision, string expected) =>
        Assert.Equal(expected, TelehealthApplicantIdentityReviewPolicy.ResultingStatus(decision));

    [Fact]
    public void ReasonIsTrimmedButRetainedAsExplicitStaffEvidence() =>
        Assert.Equal("Synthetic evidence reviewed.",
            TelehealthApplicantIdentityReviewPolicy.NormalizeReason("  Synthetic evidence reviewed.  "));

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    [InlineData("Synthetic\u0000evidence")]
    public void InvalidReasonFailsBeforePersistence(string reason)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantIdentityReviewPolicy.NormalizeReason(reason));
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_identity_review_reason_invalid", problem.Code);
    }
}
