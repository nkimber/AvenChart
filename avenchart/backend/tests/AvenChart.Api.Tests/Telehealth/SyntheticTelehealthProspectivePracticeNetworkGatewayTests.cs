// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthProspectivePracticeNetworkGatewayTests
{
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly SyntheticTelehealthProspectivePracticeNetworkGateway _gateway = new();

    [Theory]
    [InlineData("harbor-mutual-hd", "SimulatedAvailable", "Matched", "InNetwork", "Included", "Accepting", "PracticeInNetworkAcceptingNewPatients", true, true, true)]
    [InlineData("blue-valley-standard", "SimulatedUnavailable", "Unknown", "Unknown", "Unknown", "Unknown", "UnableToDetermine", false, false, false)]
    [InlineData("pine-state-choice", "SimulatedAvailable", "Matched", "OutOfNetwork", "Excluded", "Unknown", "PracticeOutOfNetwork", true, false, false)]
    public async Task CheckAsync_maps_only_the_three_approved_directory_outcomes(
        string planKey,
        string transport,
        string planMatch,
        string affiliation,
        string service,
        string newPatient,
        string business,
        bool checkedNetwork,
        bool inNetwork,
        bool accepting)
    {
        var result = await _gateway.CheckAsync(Inquiry(planKey), CancellationToken.None);

        Assert.Equal(transport, result.TransportOutcome);
        Assert.Equal(planMatch, result.PlanNetworkMatchStatus);
        Assert.Equal(affiliation, result.PracticeAffiliationStatus);
        Assert.Equal(service, result.ServiceAvailabilityStatus);
        Assert.Equal(newPatient, result.NewPatientAcceptanceStatus);
        Assert.Equal(business, result.BusinessOutcome);
        Assert.Equal(checkedNetwork, result.PracticeNetworkChecked);
        Assert.Equal(inNetwork, result.PracticeInNetwork);
        Assert.Equal(accepting, result.NewPatientsAccepted);
    }

    [Fact]
    public async Task CheckAsync_emits_fixed_nonproduction_plan_net_metadata_and_short_lived_traces()
    {
        var result = await _gateway.CheckAsync(Inquiry("harbor-mutual-hd"), CancellationToken.None);

        Assert.Equal("NON_PRODUCTION", result.AdapterMode);
        Assert.Equal("HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0", result.CompatibilityTarget);
        Assert.Equal("avenchart-synthetic-practice-network-directory-2026-08", result.DatasetKey);
        Assert.Equal(1, result.DatasetVersion);
        Assert.Equal(SyntheticTelehealthProspectivePracticeNetworkGateway.SourceLastUpdatedAt, result.SourceLastUpdatedAt);
        Assert.NotEqual(Guid.Empty, result.RequestTraceToken);
        Assert.NotEqual(Guid.Empty, result.ResponseTraceToken);
        Assert.NotEqual(result.RequestTraceToken, result.ResponseTraceToken);
        Assert.Equal(CheckedAt, result.CheckedAt);
        Assert.Equal(CheckedAt.AddMinutes(15), result.ExpiresAt);
    }

    [Fact]
    public async Task CheckAsync_uses_no_member_or_rendering_physician_input_and_keeps_outcomes_deterministic()
    {
        var inquiry = Inquiry("harbor-mutual-hd");

        var first = await _gateway.CheckAsync(inquiry, CancellationToken.None);
        var second = await _gateway.CheckAsync(inquiry, CancellationToken.None);

        Assert.NotEqual(first.RequestTraceToken, second.RequestTraceToken);
        Assert.NotEqual(first.ResponseTraceToken, second.ResponseTraceToken);
        Assert.Equal(first.BusinessOutcome, second.BusinessOutcome);
        Assert.Equal(first.PracticeAffiliationStatus, second.PracticeAffiliationStatus);
        Assert.DoesNotContain(
            typeof(TelehealthProspectivePracticeNetworkInquiry).GetProperties(),
            property => property.Name.Contains("Member", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Subscriber", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Physician", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(2026, 8, 26)]
    [InlineData(2026, 11, 1)]
    public async Task CheckAsync_fails_closed_outside_the_approved_dataset_window(
        int year,
        int month,
        int day)
    {
        var inquiry = Inquiry("harbor-mutual-hd") with
        {
            DateOfService = new DateOnly(year, month, day)
        };

        var problem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(inquiry, CancellationToken.None));

        Assert.Equal("telehealth_applicant_practice_network_dataset_unavailable", problem.Code);
    }

    [Fact]
    public async Task CheckAsync_rejects_client_like_practice_or_service_substitution()
    {
        var practiceProblem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(
                Inquiry("harbor-mutual-hd") with { PracticeId = "another-practice" },
                CancellationToken.None));
        var serviceProblem = await Assert.ThrowsAsync<TelehealthProblem>(async () =>
            await _gateway.CheckAsync(
                Inquiry("harbor-mutual-hd") with { ServiceCategory = "EmergencyDepartment" },
                CancellationToken.None));

        Assert.Equal("telehealth_applicant_practice_network_dataset_unavailable", practiceProblem.Code);
        Assert.Equal("telehealth_applicant_practice_network_dataset_unavailable", serviceProblem.Code);
    }

    private static TelehealthProspectivePracticeNetworkInquiry Inquiry(string planKey) => new(
        SyntheticTelehealthProspectivePracticeNetworkGateway.PracticeId,
        "AvenChart Synthetic Practice",
        SyntheticTelehealthProspectivePracticeNetworkGateway.FacilityId,
        planKey,
        "GA",
        new DateOnly(2026, 8, 27),
        SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory,
        CheckedAt);
}
