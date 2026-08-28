// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthProspectivePracticeNetworkCatalogTests
{
    private readonly SyntheticTelehealthProspectivePracticeNetworkCatalog _catalog = new();

    [Fact]
    public void GetCurrent_exposes_exactly_three_versioned_nonproduction_options()
    {
        var snapshot = _catalog.GetCurrent(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("NON_PRODUCTION", snapshot.AdapterMode);
        Assert.Equal("avenchart-synthetic-prospective-practice-network-2026-08", snapshot.CatalogKey);
        Assert.Equal(1, snapshot.CatalogVersion);
        Assert.Equal(3, snapshot.Plans.Count);
        Assert.Equal(
            ["harbor-mutual-hd", "blue-valley-standard", "pine-state-choice"],
            snapshot.Plans.Select(plan => plan.PlanKey));
    }

    [Theory]
    [InlineData("harbor-mutual-hd", "Harbor Mutual", "High Deductible", "PracticeNetworkConfirmedFixture")]
    [InlineData("blue-valley-standard", "Blue Valley Health", "Standard", "NetworkUnknown")]
    [InlineData("pine-state-choice", "Pine State Choice", "Choice", "PracticeOutOfNetworkFixture")]
    public void Resolve_owns_all_labels_and_practice_level_results(
        string planKey,
        string payer,
        string product,
        string status)
    {
        var plan = _catalog.Resolve(
            planKey,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(payer, plan.PayerDisplayName);
        Assert.Equal(product, plan.ProductDisplayName);
        Assert.Equal(status, plan.PracticeNetworkStatus);
        Assert.Contains("synthetic fixture", plan.Meaning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(" Harbor-Mutual-HD ", "harbor-mutual-hd")]
    [InlineData("BLUE-VALLEY-STANDARD", "blue-valley-standard")]
    public void Normalize_accepts_only_an_opaque_normalized_key(string supplied, string expected)
    {
        var normalized = _catalog.Normalize(
            new RecordTelehealthProspectivePracticeNetworkPrecheckRequest(5, supplied, true));

        Assert.Equal(5, normalized.ExpectedVersion);
        Assert.Equal(expected, normalized.PlanKey);
    }

    [Theory]
    [InlineData(0, "harbor-mutual-hd", true, "telehealth_applicant_version_invalid")]
    [InlineData(5, "harbor-mutual-hd", false, "telehealth_synthetic_confirmation_required")]
    [InlineData(5, "", true, "telehealth_applicant_practice_network_plan_invalid")]
    [InlineData(5, "plan key", true, "telehealth_applicant_practice_network_plan_invalid")]
    [InlineData(5, "plan_with_result", true, "telehealth_applicant_practice_network_plan_invalid")]
    public void Normalize_rejects_invalid_command_content(
        int version,
        string planKey,
        bool confirmed,
        string expectedCode)
    {
        var problem = Assert.Throws<TelehealthProblem>(() => _catalog.Normalize(
            new RecordTelehealthProspectivePracticeNetworkPrecheckRequest(
                version,
                planKey,
                confirmed)));

        Assert.Equal(expectedCode, problem.Code);
    }

    [Fact]
    public void Resolve_rejects_a_well_formed_but_arbitrary_plan()
    {
        var problem = Assert.Throws<TelehealthProblem>(() => _catalog.Resolve(
            "arbitrary-plan",
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("telehealth_applicant_practice_network_plan_invalid", problem.Code);
    }

    [Theory]
    [InlineData(2026, 8, 26, 23, 59, 59)]
    [InlineData(2026, 11, 1, 0, 0, 0)]
    public void GetCurrent_fails_closed_outside_the_approved_window(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second)
    {
        var problem = Assert.Throws<TelehealthProblem>(() => _catalog.GetCurrent(
            new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)));

        Assert.Equal("telehealth_applicant_practice_network_catalog_expired", problem.Code);
        Assert.Equal(StatusCodes.Status410Gone, problem.StatusCode);
    }

    [Fact]
    public void Effective_window_includes_both_boundaries()
    {
        Assert.NotNull(_catalog.GetCurrent(SyntheticTelehealthProspectivePracticeNetworkCatalog.EffectiveFrom));
        Assert.NotNull(_catalog.GetCurrent(SyntheticTelehealthProspectivePracticeNetworkCatalog.EffectiveThrough));
    }
}
