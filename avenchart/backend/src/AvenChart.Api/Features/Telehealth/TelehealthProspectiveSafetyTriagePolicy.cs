// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthProspectiveSafetyTriage(
    int ExpectedVersion,
    string CurrentLocationStateCode,
    bool HasEmergencyWarning,
    bool SevereOrWorsening,
    bool RequiresHandsOnExam,
    bool Unsure);

public static class TelehealthProspectiveSafetyTriagePolicy
{
    public static NormalizedTelehealthProspectiveSafetyTriage Normalize(
        EvaluateTelehealthProspectiveSafetyTriageRequest request,
        IReadOnlyCollection<string> supportedStates)
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
                "Confirm that every safety-screen value is synthetic demonstration data.");
        }
        if (request.CurrentLocationConfirmed is not true)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_location_confirmation_required",
                "Explicitly confirm the synthetic applicant's current physical location.");
        }

        var stateCode = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!supportedStates.Contains(stateCode, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_location_not_supported",
                "Current physical location must be one of the configured synthetic service states.");
        }

        if (request.HasEmergencyWarning is null
            || request.SevereOrWorsening is null
            || request.RequiresHandsOnExam is null
            || request.Unsure is null)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_safety_answer_required",
                "Answer every universal safety question explicitly. Missing answers cannot pass.");
        }

        return new NormalizedTelehealthProspectiveSafetyTriage(
            request.ExpectedVersion,
            stateCode,
            request.HasEmergencyWarning.Value,
            request.SevereOrWorsening.Value,
            request.RequiresHandsOnExam.Value,
            request.Unsure.Value);
    }

    public static string ResultingStatus(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "SafetyEmergencyRedirect",
        TelehealthTriageOutcome.UrgentInPerson or TelehealthTriageOutcome.InPersonRequired =>
            "SafetyInPersonRequired",
        TelehealthTriageOutcome.ClinicalReview => "SafetyClinicalReviewRequired",
        TelehealthTriageOutcome.TelehealthEligible => "SafetyScreenPassed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string PublicDisposition(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "EmergencyCareNow",
        TelehealthTriageOutcome.UrgentInPerson => "PromptInPersonCare",
        TelehealthTriageOutcome.InPersonRequired => "InPersonCareRequired",
        TelehealthTriageOutcome.ClinicalReview => "ClinicalReviewRequired",
        TelehealthTriageOutcome.TelehealthEligible => "ContinueProspectiveIntake",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string Direction(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency =>
            "Call 911 now or go to the nearest emergency department. No telehealth request was created.",
        TelehealthTriageOutcome.UrgentInPerson =>
            "Seek prompt in-person medical evaluation. If symptoms become an emergency, call 911.",
        TelehealthTriageOutcome.InPersonRequired =>
            "Arrange an in-person medical evaluation. This synthetic screen cannot continue toward telehealth.",
        TelehealthTriageOutcome.ClinicalReview =>
            "An uncertain answer cannot pass automatically. A separately authorized clinical-review workflow would be required.",
        TelehealthTriageOutcome.TelehealthEligible =>
            "The universal safety screen found no stop condition in these synthetic answers. Later identity, complaint-specific triage, consent, coverage, and practice gates are still required and are not yet available.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
