// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthProspectiveEligibilityGatewayTests
{
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SyntheticTelehealthProspectiveEligibilityGateway _gateway = new();

    [Theory]
    [InlineData("harbor-mutual-hd", "SYN-HM-1001", "SimulatedAccepted", "Matched", "Active", "Reported", "EligibleBenefitsReported", true, true, true)]
    [InlineData("blue-valley-standard", "SYN-BV-2002", "SimulatedAccepted", "Matched", "Inactive", "NotReported", "CoverageInactive", true, true, false)]
    [InlineData("pine-state-choice", "SYN-PS-3003", "SimulatedAccepted", "NotMatched", "Unknown", "NotReported", "SubscriberNotFound", false, true, false)]
    [InlineData("harbor-mutual-hd", "SYN-HM-9999", "SimulatedUnavailable", "Unknown", "Unknown", "Unknown", "UnableToDetermine", false, false, false)]
    public async Task CheckAsync_maps_only_the_four_approved_normalized_outcomes(
        string planKey,
        string memberId,
        string transport,
        string match,
        string eligibility,
        string benefits,
        string business,
        bool matched,
        bool eligibilityChecked,
        bool benefitsChecked)
    {
        var result = await _gateway.CheckAsync(Inquiry(planKey, memberId), CancellationToken.None);

        Assert.Equal(transport, result.TransportOutcome);
        Assert.Equal(match, result.MemberMatchStatus);
        Assert.Equal(eligibility, result.EligibilityStatus);
        Assert.Equal(benefits, result.BenefitInformationStatus);
        Assert.Equal(business, result.BusinessOutcome);
        Assert.Equal(matched, result.MemberMatched);
        Assert.Equal(eligibilityChecked, result.MemberEligibilityChecked);
        Assert.Equal(benefitsChecked, result.MemberBenefitsChecked);
    }

    [Fact]
    public async Task CheckAsync_emits_fixed_nonproduction_metadata_and_short_lived_opaque_traces()
    {
        var result = await _gateway.CheckAsync(
            Inquiry("harbor-mutual-hd", "SYN-HM-1001"),
            CancellationToken.None);

        Assert.Equal("NON_PRODUCTION", result.AdapterMode);
        Assert.Equal("ASC_X12N_270_271_005010X279A1", result.CompatibilityTarget);
        Assert.Equal("avenchart-synthetic-prospective-eligibility-2026-08", result.DatasetKey);
        Assert.Equal(1, result.DatasetVersion);
        Assert.Equal(SyntheticTelehealthProspectiveEligibilityGateway.DatasetEffectiveFrom, result.DatasetEffectiveFrom);
        Assert.Equal(SyntheticTelehealthProspectiveEligibilityGateway.DatasetEffectiveThrough, result.DatasetEffectiveThrough);
        Assert.NotEqual(Guid.Empty, result.InquiryTraceToken);
        Assert.NotEqual(Guid.Empty, result.ResponseTraceToken);
        Assert.NotEqual(result.InquiryTraceToken, result.ResponseTraceToken);
        Assert.Equal(CheckedAt, result.CheckedAt);
        Assert.Equal(CheckedAt.AddMinutes(15), result.ExpiresAt);
    }

    [Fact]
    public async Task CheckAsync_keeps_trace_tokens_nondeterministic_and_outcomes_deterministic()
    {
        var inquiry = Inquiry("harbor-mutual-hd", "SYN-HM-1001");

        var first = await _gateway.CheckAsync(inquiry, CancellationToken.None);
        var second = await _gateway.CheckAsync(inquiry, CancellationToken.None);

        Assert.NotEqual(first.InquiryTraceToken, second.InquiryTraceToken);
        Assert.NotEqual(first.ResponseTraceToken, second.ResponseTraceToken);
        Assert.Equal(first.BusinessOutcome, second.BusinessOutcome);
        Assert.Equal(first.EligibilityStatus, second.EligibilityStatus);
        Assert.Equal(first.BenefitInformationStatus, second.BenefitInformationStatus);
    }

    [Theory]
    [InlineData(2026, 8, 26)]
    [InlineData(2026, 11, 1)]
    public async Task CheckAsync_fails_closed_outside_the_approved_dataset_window(
        int year,
        int month,
        int day)
    {
        var inquiry = Inquiry("harbor-mutual-hd", "SYN-HM-1001") with
        {
            DateOfService = new DateOnly(year, month, day)
        };

        var problem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(inquiry, CancellationToken.None));

        Assert.Equal("telehealth_applicant_eligibility_dataset_unavailable", problem.Code);
    }

    [Fact]
    public async Task CheckAsync_rejects_a_nonstandard_service_category()
    {
        var inquiry = Inquiry("harbor-mutual-hd", "SYN-HM-1001") with
        {
            ServiceCategory = "EmergencyDepartment"
        };

        var problem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(inquiry, CancellationToken.None));

        Assert.Equal("telehealth_applicant_eligibility_dataset_unavailable", problem.Code);
    }

    private static TelehealthProspectiveEligibilityInquiry Inquiry(string planKey, string memberId) => new(
        planKey,
        memberId,
        "SYN-GROUP-01",
        "Self",
        "Taylor",
        "Applicant",
        new DateOnly(1985, 4, 5),
        new DateOnly(2026, 8, 27),
        SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory,
        CheckedAt);
}
