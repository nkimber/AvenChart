// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthApplicantRequestOperationalReviewSubmissionCommand(
    int ExpectedRequestVersion,
    string SubmissionSnapshotFingerprint,
    bool SyntheticEvidenceAcknowledged,
    bool NoCoverageGuaranteeAcknowledged,
    bool PracticeReviewPendingAcknowledged,
    bool NoCareRelationshipAcknowledged);

public static class TelehealthApplicantRequestOperationalReviewSubmissionPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION";
    public const string SourceMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "AVENCHART_SYNTHETIC_OPERATIONAL_REVIEW_V1";
    public const string BusinessOutcome = "SyntheticRequestSubmittedForOperationalReview";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string EntryRequestStatus = "Verification";
    public const int EntryRequestVersion = 11;
    public const string ResultingRequestStatus = "OperationalReview";
    public const int ResultingRequestVersion = 12;

    public static NormalizedTelehealthApplicantRequestOperationalReviewSubmissionCommand Normalize(
        SubmitTelehealthApplicantRequestForOperationalReview request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_operational_review_submission_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.SubmissionSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_operational_review_submission_snapshot_invalid",
                "Reload the operational-review submission before continuing.");
        }

        if (!request.SyntheticEvidenceAcknowledged
            || !request.NoCoverageGuaranteeAcknowledged
            || !request.PracticeReviewPendingAcknowledged
            || !request.NoCareRelationshipAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_operational_review_submission_acknowledgments_required",
                "Confirm the synthetic-evidence, no-guarantee, pending-review, and no-care-relationship statements before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticEvidenceAcknowledged,
            request.NoCoverageGuaranteeAcknowledged,
            request.PracticeReviewPendingAcknowledged,
            request.NoCareRelationshipAcknowledged);
    }

    public static string SnapshotFingerprint(
        Guid applicantId,
        Guid requestId,
        Guid participationEvaluationId,
        int requestVersion,
        string practiceId,
        int facilityId,
        string canonicalPatientId,
        string practiceDisplayName,
        string payerDisplayName,
        string productDisplayName,
        string currentLocationStateCode,
        string purposeCategory,
        DateOnly dateOfService,
        int candidateStaffId,
        string candidateDisplayName,
        string candidateNpiLast4,
        string serviceCategory,
        string modality,
        string evaluationSnapshotFingerprint,
        DateTimeOffset evaluatedAt,
        DateTimeOffset resultValidThrough) =>
        TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-operational-review-submission-v1",
            applicantId,
            requestId,
            participationEvaluationId,
            requestVersion,
            practiceId,
            facilityId,
            canonicalPatientId,
            practiceDisplayName,
            payerDisplayName,
            productDisplayName,
            currentLocationStateCode,
            purposeCategory,
            dateOfService.ToString("O", CultureInfo.InvariantCulture),
            candidateStaffId,
            candidateDisplayName,
            candidateNpiLast4,
            serviceCategory,
            modality,
            evaluationSnapshotFingerprint,
            evaluatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            resultValidThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            SourceMode,
            CompatibilityTarget,
            BusinessOutcome);
}
