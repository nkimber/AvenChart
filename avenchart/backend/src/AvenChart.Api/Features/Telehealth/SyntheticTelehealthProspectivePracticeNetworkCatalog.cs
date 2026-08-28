// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthProspectivePracticeNetworkPrecheck(
    int ExpectedVersion,
    string PlanKey);

public sealed record SyntheticTelehealthProspectivePracticeNetworkPlan(
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeNetworkStatus,
    string Meaning);

public sealed record SyntheticTelehealthProspectivePracticeNetworkCatalogSnapshot(
    string AdapterMode,
    string CatalogKey,
    int CatalogVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveThrough,
    IReadOnlyList<SyntheticTelehealthProspectivePracticeNetworkPlan> Plans);

public sealed class SyntheticTelehealthProspectivePracticeNetworkCatalog
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CatalogKey = "avenchart-synthetic-prospective-practice-network-2026-08";
    public const int CatalogVersion = 1;
    public static readonly DateTimeOffset EffectiveFrom =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset EffectiveThrough =
        new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);

    private static readonly IReadOnlyList<SyntheticTelehealthProspectivePracticeNetworkPlan> Plans =
    [
        new(
            "harbor-mutual-hd",
            "Harbor Mutual",
            "High Deductible",
            "PracticeNetworkConfirmedFixture",
            "The synthetic fixture says the practice participates for this plan, state, and visit category. It does not check the member or rendering physician."),
        new(
            "blue-valley-standard",
            "Blue Valley Health",
            "Standard",
            "NetworkUnknown",
            "The synthetic fixture has no authoritative practice-plan participation result. Treat network status as unknown."),
        new(
            "pine-state-choice",
            "Pine State Choice",
            "Choice",
            "PracticeOutOfNetworkFixture",
            "The synthetic fixture says the practice does not participate for this plan. No self-pay choice or estimate is created.")
    ];

    public NormalizedTelehealthProspectivePracticeNetworkPrecheck Normalize(
        RecordTelehealthProspectivePracticeNetworkPrecheckRequest request)
    {
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_synthetic_confirmation_required",
                "Confirm that the selected plan is fictional synthetic demonstration data.");
        }

        var planKey = request.PlanKey?.Trim().ToLowerInvariant() ?? string.Empty;
        if (planKey.Length is < 3 or > 80
            || planKey.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            throw InvalidPlan();
        }

        return new(request.ExpectedVersion, planKey);
    }

    public SyntheticTelehealthProspectivePracticeNetworkCatalogSnapshot GetCurrent(
        DateTimeOffset at)
    {
        RequireEffective(at);
        return new(
            AdapterMode,
            CatalogKey,
            CatalogVersion,
            EffectiveFrom,
            EffectiveThrough,
            Plans);
    }

    public SyntheticTelehealthProspectivePracticeNetworkPlan Resolve(
        string planKey,
        DateTimeOffset at)
    {
        RequireEffective(at);
        return Plans.FirstOrDefault(plan =>
            string.Equals(plan.PlanKey, planKey, StringComparison.Ordinal))
            ?? throw InvalidPlan();
    }

    private static void RequireEffective(DateTimeOffset at)
    {
        if (at < EffectiveFrom || at > EffectiveThrough)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_practice_network_catalog_expired",
                "The approved synthetic practice-network catalog is outside its effective window. Start again after a new catalog is approved.");
        }
    }

    private static TelehealthProblem InvalidPlan() => TelehealthProblem.BadRequest(
        "telehealth_applicant_practice_network_plan_invalid",
        "Choose one of the supported synthetic plan options.");
}
