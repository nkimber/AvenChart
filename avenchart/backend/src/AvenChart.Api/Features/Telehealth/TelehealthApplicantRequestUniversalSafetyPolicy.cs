// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestUniversalSafetySnapshot(
    string CurrentLocationStateCode,
    string MaskedCallbackPhone,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestUniversalSafetyAssessment(
    int ExpectedRequestVersion,
    string ContextSnapshotFingerprint,
    string CurrentLocationStateCode,
    bool CurrentLocationConfirmed,
    bool CallbackNumberConfirmed,
    bool SyntheticDataConfirmed,
    bool HasEmergencyWarning,
    bool SevereOrWorsening,
    bool RequiresHandsOnExam,
    bool Unsure);

public static class TelehealthApplicantRequestUniversalSafetyPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string EntryRequestStatus = "LocationConfirmed";
    public const int EntryRequestVersion = 2;
    public const int ResultingRequestVersion = 3;
    public const int ContextFreshnessMinutes = 30;
    public static readonly IReadOnlyList<string> SupportedStates = ["GA", "CA", "FL"];

    public static TelehealthApplicantRequestUniversalSafetySnapshot Snapshot(
        Guid requestId,
        Guid requestCreationId,
        Guid locationConfirmationId,
        Guid locationId,
        Guid sourceSafetyEvaluationId,
        int requestVersion,
        string currentLocationStateCode,
        string callbackPhoneLast4,
        DateTimeOffset locationConfirmedAt,
        DateTimeOffset applicantExpiresAt)
    {
        var contextExpiresAt = locationConfirmedAt.AddMinutes(ContextFreshnessMinutes) < applicantExpiresAt
            ? locationConfirmedAt.AddMinutes(ContextFreshnessMinutes)
            : applicantExpiresAt;
        var canonicalLocationTime = locationConfirmedAt.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture);
        var canonicalExpiry = contextExpiresAt.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture);
        return new(
            currentLocationStateCode,
            $"***-***-{callbackPhoneLast4}",
            contextExpiresAt,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-universal-safety-context-v1",
                requestId,
                requestCreationId,
                locationConfirmationId,
                locationId,
                sourceSafetyEvaluationId,
                requestVersion,
                currentLocationStateCode,
                callbackPhoneLast4,
                canonicalLocationTime,
                canonicalExpiry,
                SyntheticTelehealthTriageEvaluator.ProtocolId,
                SyntheticTelehealthTriageEvaluator.ProtocolVersion,
                SyntheticTelehealthTriageEvaluator.ProtocolContentHash));
    }

    public static NormalizedTelehealthApplicantRequestUniversalSafetyAssessment Normalize(
        EvaluateTelehealthApplicantRequestUniversalSafety request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_snapshot_invalid",
                "Reload the request safety step before continuing.");
        }

        var state = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_state_unsupported",
                "Current physical location must be Georgia, California, or Florida for this synthetic demonstration.");
        }

        if (!request.CurrentLocationConfirmed || !request.CallbackNumberConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_context_confirmation_required",
                "Confirm that the displayed current location and masked callback route remain correct.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_synthetic_confirmation_required",
                "Confirm that every safety answer is synthetic demonstration data.");
        }
        if (request.HasEmergencyWarning is null
            || request.SevereOrWorsening is null
            || request.RequiresHandsOnExam is null
            || request.Unsure is null)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_safety_answer_required",
                "Answer every universal safety question explicitly. Missing answers cannot pass.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            state,
            request.CurrentLocationConfirmed,
            request.CallbackNumberConfirmed,
            request.SyntheticDataConfirmed,
            request.HasEmergencyWarning.Value,
            request.SevereOrWorsening.Value,
            request.RequiresHandsOnExam.Value,
            request.Unsure.Value);
    }

    public static string ResultingRequestStatus(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "EmergencyRedirected",
        TelehealthTriageOutcome.UrgentInPerson or TelehealthTriageOutcome.InPersonRequired =>
            "InPersonRecommended",
        TelehealthTriageOutcome.ClinicalReview => "ClinicalReview",
        TelehealthTriageOutcome.TelehealthEligible => "SafetyScreening",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string PublicDisposition(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "EmergencyCareNow",
        TelehealthTriageOutcome.UrgentInPerson => "PromptInPersonCare",
        TelehealthTriageOutcome.InPersonRequired => "InPersonCareRequired",
        TelehealthTriageOutcome.ClinicalReview => "ClinicalReviewRequired",
        TelehealthTriageOutcome.TelehealthEligible => "UniversalSafetyPassed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string Direction(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency =>
            "Call 911 now or go to the nearest emergency department. This application did not contact or dispatch emergency services.",
        TelehealthTriageOutcome.UrgentInPerson =>
            "Seek prompt in-person medical evaluation. If symptoms become an emergency, call 911.",
        TelehealthTriageOutcome.InPersonRequired =>
            "Arrange an in-person medical evaluation. This request cannot continue toward the telehealth queue.",
        TelehealthTriageOutcome.ClinicalReview =>
            "A qualified clinical review is required before this request can continue. No reviewer or review work item has been assigned yet.",
        TelehealthTriageOutcome.TelehealthEligible =>
            "The universal safety screen found no stop condition in these synthetic answers. Complaint-specific triage is still required before clinical eligibility can be established.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static bool UniversalSafetyPassed(TelehealthTriageOutcome outcome) =>
        outcome == TelehealthTriageOutcome.TelehealthEligible;

    public static bool ComplaintSpecificTriageRequired(TelehealthTriageOutcome outcome) =>
        outcome == TelehealthTriageOutcome.TelehealthEligible;

    public static bool ClinicalReviewRequired(TelehealthTriageOutcome outcome) =>
        outcome == TelehealthTriageOutcome.ClinicalReview;

    public static bool TerminalForTelehealth(TelehealthTriageOutcome outcome) => outcome is
        TelehealthTriageOutcome.Emergency
        or TelehealthTriageOutcome.UrgentInPerson
        or TelehealthTriageOutcome.InPersonRequired;
}
