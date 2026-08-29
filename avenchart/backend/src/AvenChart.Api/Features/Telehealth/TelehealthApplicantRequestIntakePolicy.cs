// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestIntakeSnapshot(
    string ComplaintCategory,
    string ComplaintSummary,
    string CurrentLocationStateCode,
    string MaskedCallbackPhone,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestIntakeConfirmation(
    int ExpectedRequestVersion,
    string ContextSnapshotFingerprint,
    string CurrentLocationStateCode,
    string SymptomDuration,
    bool CurrentLocationConfirmed,
    bool CallbackNumberConfirmed,
    bool PriorInformationReviewed,
    bool InsuranceLimitationsAcknowledged,
    bool PendingConsentAcknowledged,
    bool PendingVerificationAcknowledged,
    bool ComplaintResultAcknowledged,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantRequestIntakePolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string EntryRequestStatus = "Intake";
    public const int EntryRequestVersion = 4;
    public const string ResultingRequestStatus = "Verification";
    public const int ResultingRequestVersion = 5;
    public const string ClinicalContentStatus = "UNAPPROVED_SYNTHETIC";
    public static readonly IReadOnlyList<string> SupportedStates = ["GA", "CA", "FL"];
    public static readonly IReadOnlyList<string> SupportedSymptomDurations =
        ["less-than-day", "1-3-days", "4-14-days", "more-than-14-days"];

    public static NormalizedTelehealthApplicantRequestIntakeConfirmation Normalize(
        ConfirmTelehealthApplicantRequestIntake request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_intake_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty).Trim().ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_intake_snapshot_invalid",
                "Reload the intake confirmation step before continuing.");
        }

        var state = request.CurrentLocationStateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_intake_state_unsupported",
                "Current physical location must be Georgia, California, or Florida for this synthetic demonstration.");
        }

        var duration = request.SymptomDuration?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedSymptomDurations.Contains(duration, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_intake_duration_invalid",
                "Choose one supported symptom-duration range.");
        }

        if (!request.CurrentLocationConfirmed
            || !request.CallbackNumberConfirmed
            || !request.PriorInformationReviewed
            || !request.InsuranceLimitationsAcknowledged
            || !request.PendingConsentAcknowledged
            || !request.PendingVerificationAcknowledged
            || !request.ComplaintResultAcknowledged
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_intake_confirmations_required",
                "All eight intake confirmations must be explicitly accepted.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            state,
            duration,
            request.CurrentLocationConfirmed,
            request.CallbackNumberConfirmed,
            request.PriorInformationReviewed,
            request.InsuranceLimitationsAcknowledged,
            request.PendingConsentAcknowledged,
            request.PendingVerificationAcknowledged,
            request.ComplaintResultAcknowledged,
            request.SyntheticDataConfirmed);
    }

    public static TelehealthApplicantRequestIntakeSnapshot Snapshot(
        Guid requestId,
        Guid requestCreationId,
        Guid locationConfirmationId,
        Guid locationId,
        Guid universalSafetyReceiptId,
        Guid complaintTriageReceiptId,
        Guid complaintTriageAssessmentId,
        Guid promotionId,
        Guid practiceReviewCaseId,
        Guid practiceReviewAuthorizationId,
        int requestVersion,
        string complaintCategory,
        string complaintOutcome,
        string currentLocationStateCode,
        string callbackPhoneLast4,
        DateTimeOffset locationConfirmedAt,
        DateTimeOffset complaintEvaluatedAt,
        DateTimeOffset contextExpiresAt,
        DateTimeOffset applicantExpiresAt,
        string protocolKey,
        int protocolVersion,
        string protocolContentHash,
        string clinicalContentStatus,
        bool medicalDirectorApprovalRecorded,
        bool clinicalGoldenCasePackApproved,
        bool productionPublicationAllowed,
        string complaintContextFingerprint,
        string complaintCommandFingerprint)
    {
        var effectiveExpiry = contextExpiresAt < applicantExpiresAt ? contextExpiresAt : applicantExpiresAt;
        var summary = ComplaintSummary(complaintCategory);
        return new(
            complaintCategory,
            summary,
            currentLocationStateCode,
            $"***-***-{callbackPhoneLast4}",
            effectiveExpiry,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-intake-context-v1",
                requestId,
                requestCreationId,
                locationConfirmationId,
                locationId,
                universalSafetyReceiptId,
                complaintTriageReceiptId,
                complaintTriageAssessmentId,
                promotionId,
                practiceReviewCaseId,
                practiceReviewAuthorizationId,
                requestVersion,
                complaintCategory,
                complaintOutcome,
                summary,
                currentLocationStateCode,
                callbackPhoneLast4,
                locationConfirmedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                complaintEvaluatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                effectiveExpiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                protocolKey,
                protocolVersion,
                protocolContentHash,
                clinicalContentStatus,
                medicalDirectorApprovalRecorded,
                clinicalGoldenCasePackApproved,
                productionPublicationAllowed,
                complaintContextFingerprint,
                complaintCommandFingerprint));
    }

    public static string ComplaintSummary(string complaintCategory) => complaintCategory switch
    {
        "migraine" => "Synthetic migraine intake demonstration",
        "sleep" => "Synthetic sleep intake demonstration",
        _ => throw TelehealthProblem.Conflict(
            "telehealth_applicant_request_intake_category_conflict",
            "The request complaint category is not available for this synthetic intake fixture.")
    };
}
