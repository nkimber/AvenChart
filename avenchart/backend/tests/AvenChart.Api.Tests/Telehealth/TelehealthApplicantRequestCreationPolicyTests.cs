// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestCreationPolicyTests
{
    [Fact]
    public void Policy_is_versioned_and_stops_at_created_request_state()
    {
        Assert.Equal("SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION",
            TelehealthApplicantRequestCreationPolicy.PolicyKey);
        Assert.Equal(1, TelehealthApplicantRequestCreationPolicy.PolicyVersion);
        Assert.Equal(1, TelehealthApplicantRequestCreationPolicy.AuthorizationPolicyVersion);
        Assert.Equal("SyntheticPracticeReviewAuthorized",
            TelehealthApplicantRequestCreationPolicy.EntryStatus);
        Assert.Equal("SyntheticRequestCreated",
            TelehealthApplicantRequestCreationPolicy.ResultingStatus);
        Assert.Equal("APPLICANT_CONFIRMATION_WITH_AUTHORIZED_SOURCE_PROVENANCE",
            TelehealthApplicantRequestCreationPolicy.EvidenceType);
    }

    [Fact]
    public void Normalize_accepts_current_version_and_all_confirmations()
    {
        var normalized = TelehealthApplicantRequestCreationPolicy.Normalize(new(
            25, 1, true, true, true));

        Assert.Equal(25, normalized.ExpectedApplicantVersion);
        Assert.Equal(1, normalized.AuthorizationPolicyVersion);
        Assert.True(normalized.RequestCreationConfirmed);
        Assert.True(normalized.NoQueueOrCareAcknowledged);
        Assert.True(normalized.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Normalize_rejects_each_incomplete_confirmation(
        bool create,
        bool noQueue,
        bool urgent)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestCreationPolicy.Normalize(new(
                25, 1, create, noQueue, urgent)));

        Assert.Equal("telehealth_applicant_request_creation_acknowledgments_required", problem.Code);
    }

    [Theory]
    [InlineData(0, 1, "telehealth_applicant_request_creation_version_invalid")]
    [InlineData(25, 2, "telehealth_applicant_request_creation_authorization_policy_invalid")]
    public void Normalize_rejects_invalid_versions(int applicantVersion, int policyVersion, string code)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantRequestCreationPolicy.Normalize(new(
                applicantVersion, policyVersion, true, true, true)));

        Assert.Equal(code, problem.Code);
    }
}
