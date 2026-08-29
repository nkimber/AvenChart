// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestComplaintTriageSnapshot(
    string ComplaintCategory,
    string CurrentLocationStateCode,
    string MaskedCallbackPhone,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestComplaintTriageAssessment(
    int ExpectedRequestVersion,
    string ContextSnapshotFingerprint,
    string CurrentLocationStateCode,
    bool CurrentLocationConfirmed,
    bool CallbackNumberConfirmed,
    bool SyntheticDataConfirmed,
    SyntheticComplaintTriageAnswers Answers);

public static class TelehealthApplicantRequestComplaintTriagePolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_COMPLAINT_TRIAGE_ASSESSMENT";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string EntryRequestStatus = "SafetyScreening";
    public const int EntryRequestVersion = 3;
    public const int ResultingRequestVersion = 4;
    public const string ClinicalContentStatus = "UNAPPROVED_SYNTHETIC";
    public static readonly IReadOnlyList<string> SupportedStates = ["GA", "CA", "FL"];

    public static TelehealthApplicantRequestComplaintTriageSnapshot Snapshot(
        Guid requestId,
        Guid requestCreationId,
        Guid locationConfirmationId,
        Guid locationId,
        Guid universalSafetyReceiptId,
        Guid universalSafetyAssessmentId,
        int requestVersion,
        string complaintCategory,
        string currentLocationStateCode,
        string callbackPhoneLast4,
        DateTimeOffset universalSafetyEvaluatedAt,
        DateTimeOffset contextExpiresAt,
        DateTimeOffset applicantExpiresAt)
    {
        var effectiveExpiry = contextExpiresAt < applicantExpiresAt ? contextExpiresAt : applicantExpiresAt;
        var protocol = ProtocolFor(complaintCategory);
        return new(
            complaintCategory,
            currentLocationStateCode,
            $"***-***-{callbackPhoneLast4}",
            effectiveExpiry,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-complaint-triage-context-v1",
                requestId,
                requestCreationId,
                locationConfirmationId,
                locationId,
                universalSafetyReceiptId,
                universalSafetyAssessmentId,
                requestVersion,
                complaintCategory,
                currentLocationStateCode,
                callbackPhoneLast4,
                universalSafetyEvaluatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                effectiveExpiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                protocol.ProtocolId,
                protocol.ProtocolKey,
                SyntheticTelehealthComplaintTriageEvaluator.ProtocolVersion,
                protocol.ProtocolContentHash,
                SyntheticTelehealthComplaintTriageEvaluator.EngineVersion,
                ClinicalContentStatus));
    }

    public static NormalizedTelehealthApplicantRequestComplaintTriageAssessment Normalize(
        EvaluateTelehealthApplicantRequestComplaintTriage request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty).Trim().ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_snapshot_invalid",
                "Reload the complaint-specific triage step before continuing.");
        }

        var state = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_state_unsupported",
                "Current physical location must be Georgia, California, or Florida for this synthetic demonstration.");
        }
        if (!request.CurrentLocationConfirmed || !request.CallbackNumberConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_context_confirmation_required",
                "Confirm that the displayed current location and masked callback route remain correct.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_synthetic_confirmation_required",
                "Confirm that every complaint-triage answer is synthetic demonstration data.");
        }

        SyntheticComplaintTriageAnswers answers;
        if (request.Migraine is not null && request.Sleep is null)
        {
            answers = new(
                "migraine",
                new(
                    Parse(request.Migraine.SuddenOrWorstOnset, nameof(request.Migraine.SuddenOrWorstOnset)),
                    Parse(request.Migraine.NewNeurologicOrVisionChange, nameof(request.Migraine.NewNeurologicOrVisionChange)),
                    Parse(request.Migraine.FeverOrStiffNeck, nameof(request.Migraine.FeverOrStiffNeck)),
                    Parse(request.Migraine.RecentHeadInjury, nameof(request.Migraine.RecentHeadInjury)),
                    Parse(request.Migraine.PregnantOrPostpartum, nameof(request.Migraine.PregnantOrPostpartum)),
                    Parse(request.Migraine.CancerOrImmunocompromised, nameof(request.Migraine.CancerOrImmunocompromised)),
                    Parse(request.Migraine.KnownSimilarPattern, nameof(request.Migraine.KnownSimilarPattern)),
                    Parse(request.Migraine.PersistentVomiting, nameof(request.Migraine.PersistentVomiting))),
                null);
        }
        else if (request.Sleep is not null && request.Migraine is null)
        {
            answers = new(
                "sleep",
                null,
                new(
                    Parse(request.Sleep.SelfHarmThoughts, nameof(request.Sleep.SelfHarmThoughts)),
                    Parse(request.Sleep.ManiaOrPsychosis, nameof(request.Sleep.ManiaOrPsychosis)),
                    Parse(request.Sleep.DangerousSomnolence, nameof(request.Sleep.DangerousSomnolence)),
                    Parse(request.Sleep.WithdrawalConcern, nameof(request.Sleep.WithdrawalConcern)),
                    Parse(request.Sleep.BreathingPausesOrSevereSnoring, nameof(request.Sleep.BreathingPausesOrSevereSnoring)),
                    Parse(request.Sleep.PregnantOrComplexMedicationConcern, nameof(request.Sleep.PregnantOrComplexMedicationConcern)),
                    Parse(request.Sleep.ControlledSedativeRequest, nameof(request.Sleep.ControlledSedativeRequest)),
                    Parse(request.Sleep.UncomplicatedSleepDifficulty, nameof(request.Sleep.UncomplicatedSleepDifficulty))));
        }
        else
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_answer_set_invalid",
                "Submit exactly one complete migraine or sleep answer set. The server verifies that it matches the request.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            state,
            request.CurrentLocationConfirmed,
            request.CallbackNumberConfirmed,
            request.SyntheticDataConfirmed,
            answers);
    }

    public static string ResultingRequestStatus(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "EmergencyRedirected",
        TelehealthTriageOutcome.UrgentInPerson or TelehealthTriageOutcome.InPersonRequired =>
            "InPersonRecommended",
        TelehealthTriageOutcome.Unsupported => "Unsupported",
        TelehealthTriageOutcome.ClinicalReview => "ClinicalReview",
        TelehealthTriageOutcome.TelehealthEligible => "Intake",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string PublicDisposition(TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency => "EmergencyCareNow",
        TelehealthTriageOutcome.UrgentInPerson => "PromptInPersonCare",
        TelehealthTriageOutcome.InPersonRequired => "InPersonCareRequired",
        TelehealthTriageOutcome.Unsupported => "TelehealthServiceUnsupported",
        TelehealthTriageOutcome.ClinicalReview => "ClinicalReviewRequired",
        TelehealthTriageOutcome.TelehealthEligible => "SyntheticVideoEvaluationCandidate",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static string Direction(string complaintCategory, TelehealthTriageOutcome outcome) => outcome switch
    {
        TelehealthTriageOutcome.Emergency when complaintCategory == "sleep" =>
            "Call 911 now for immediate danger, or call or text 988 for a mental-health or suicide crisis. This application did not contact or dispatch emergency services.",
        TelehealthTriageOutcome.Emergency =>
            "Call 911 now or go to the nearest emergency department. This application did not contact or dispatch emergency services.",
        TelehealthTriageOutcome.UrgentInPerson =>
            "Seek prompt in-person medical evaluation. If symptoms become an emergency, call 911.",
        TelehealthTriageOutcome.InPersonRequired =>
            "Arrange an in-person medical evaluation. This request cannot continue toward the telehealth queue.",
        TelehealthTriageOutcome.Unsupported =>
            "This synthetic presentation is outside the demonstrated telehealth service. Contact the practice or arrange an appropriate in-person evaluation.",
        TelehealthTriageOutcome.ClinicalReview =>
            "A qualified clinical review is required before this request can continue. No reviewer or review work item has been assigned yet.",
        TelehealthTriageOutcome.TelehealthEligible =>
            "This unapproved synthetic fixture can demonstrate progression to intake. A physician may still recommend other care, and this result must not be used for real patient care.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static bool ClinicalReviewRequired(TelehealthTriageOutcome outcome) =>
        outcome == TelehealthTriageOutcome.ClinicalReview;

    public static bool TerminalForTelehealth(TelehealthTriageOutcome outcome) => outcome is
        TelehealthTriageOutcome.Emergency
        or TelehealthTriageOutcome.UrgentInPerson
        or TelehealthTriageOutcome.InPersonRequired
        or TelehealthTriageOutcome.Unsupported;

    public static bool SyntheticVideoEvaluationCandidate(TelehealthTriageOutcome outcome) =>
        outcome == TelehealthTriageOutcome.TelehealthEligible;

    public static void RequirePublicationBlocked(SyntheticComplaintTriageResult result)
    {
        if (result.ClinicalContentStatus != ClinicalContentStatus)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_publication_conflict",
                "The complaint-triage fixture is not in the required unapproved synthetic state.");
        }
    }

    public static (Guid ProtocolId, string ProtocolKey, string ProtocolContentHash) ProtocolFor(
        string complaintCategory) => complaintCategory switch
        {
            "migraine" => (
                SyntheticTelehealthComplaintTriageEvaluator.MigraineProtocolId,
                SyntheticTelehealthComplaintTriageEvaluator.MigraineProtocolKey,
                SyntheticTelehealthComplaintTriageEvaluator.MigraineProtocolContentHash),
            "sleep" => (
                SyntheticTelehealthComplaintTriageEvaluator.SleepProtocolId,
                SyntheticTelehealthComplaintTriageEvaluator.SleepProtocolKey,
                SyntheticTelehealthComplaintTriageEvaluator.SleepProtocolContentHash),
            _ => throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_category_conflict",
                "The request complaint category is not available for this synthetic fixture.")
        };

    private static SyntheticComplaintAnswer Parse(string? value, string field)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "YES" => SyntheticComplaintAnswer.Yes,
            "NO" => SyntheticComplaintAnswer.No,
            "NOTSURE" => SyntheticComplaintAnswer.NotSure,
            _ => throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_complaint_triage_answer_invalid",
                $"{field} must be answered Yes, No, or NotSure. Missing answers cannot pass.")
        };
    }
}
