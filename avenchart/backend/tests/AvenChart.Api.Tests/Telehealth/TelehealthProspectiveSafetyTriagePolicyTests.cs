// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthProspectiveSafetyTriagePolicyTests
{
    private static readonly string[] SupportedStates = ["GA", "CA", "FL"];

    [Fact]
    public void NormalizeRequiresExplicitAnswersAndCurrentLocation()
    {
        var normalized = TelehealthProspectiveSafetyTriagePolicy.Normalize(
            Valid() with { CurrentLocationStateCode = " ca " }, SupportedStates);

        Assert.Equal("CA", normalized.CurrentLocationStateCode);
        Assert.False(normalized.HasEmergencyWarning);
        Assert.False(normalized.SevereOrWorsening);
        Assert.False(normalized.RequiresHandsOnExam);
        Assert.False(normalized.Unsure);
    }

    [Theory]
    [InlineData("emergency")]
    [InlineData("severe")]
    [InlineData("handsOn")]
    [InlineData("unsure")]
    public void MissingSafetyAnswerFailsClosed(string field)
    {
        var request = field switch
        {
            "emergency" => Valid() with { HasEmergencyWarning = null },
            "severe" => Valid() with { SevereOrWorsening = null },
            "handsOn" => Valid() with { RequiresHandsOnExam = null },
            _ => Valid() with { Unsure = null }
        };

        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveSafetyTriagePolicy.Normalize(request, SupportedStates));
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("telehealth_applicant_safety_answer_required", problem.Code);
    }

    [Fact]
    public void ResidenceCannotSubstituteForUnsupportedCurrentLocation()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveSafetyTriagePolicy.Normalize(
                Valid() with { CurrentLocationStateCode = "NY" }, SupportedStates));
        Assert.Equal("telehealth_applicant_location_not_supported", problem.Code);
    }

    [Fact]
    public void LocationMustBeAffirmativelyConfirmed()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveSafetyTriagePolicy.Normalize(
                Valid() with { CurrentLocationConfirmed = false }, SupportedStates));
        Assert.Equal("telehealth_applicant_location_confirmation_required", problem.Code);
    }

    [Fact]
    public void SyntheticBoundaryMustBeAffirmativelyConfirmed()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthProspectiveSafetyTriagePolicy.Normalize(
                Valid() with { SyntheticDataConfirmed = false }, SupportedStates));
        Assert.Equal("telehealth_synthetic_confirmation_required", problem.Code);
    }

    [Theory]
    [InlineData(TelehealthTriageOutcome.Emergency, "SafetyEmergencyRedirect", "EmergencyCareNow")]
    [InlineData(TelehealthTriageOutcome.UrgentInPerson, "SafetyInPersonRequired", "PromptInPersonCare")]
    [InlineData(TelehealthTriageOutcome.InPersonRequired, "SafetyInPersonRequired", "InPersonCareRequired")]
    [InlineData(TelehealthTriageOutcome.ClinicalReview, "SafetyClinicalReviewRequired", "ClinicalReviewRequired")]
    [InlineData(TelehealthTriageOutcome.TelehealthEligible, "SafetyScreenPassed", "ContinueProspectiveIntake")]
    public void OutcomeMapsToBoundedProspectiveStateAndPublicDisposition(
        TelehealthTriageOutcome outcome,
        string status,
        string disposition)
    {
        Assert.Equal(status, TelehealthProspectiveSafetyTriagePolicy.ResultingStatus(outcome));
        Assert.Equal(disposition, TelehealthProspectiveSafetyTriagePolicy.PublicDisposition(outcome));
    }

    private static EvaluateTelehealthProspectiveSafetyTriageRequest Valid() => new(
        3,
        "GA",
        true,
        false,
        false,
        false,
        false,
        true);
}
