// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record NormalizedTelehealthApplicantRequestQueueAuthorizationCommand(
    int ExpectedRequestVersion,
    string AuthorizationSnapshotFingerprint,
    bool SyntheticEvidenceReviewed,
    bool NoCoverageGuaranteeAcknowledged,
    bool PracticeAcceptsForQueueAcknowledged,
    bool QueueNotCareAcknowledged);

public static class TelehealthApplicantRequestQueueAuthorizationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_QUEUE_AUTHORIZATION";
    public const string SourceMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1";
    public const string BusinessOutcome = "SyntheticRequestAuthorizedToQueue";
    public const string EntryRequestStatus = "OperationalReview";
    public const int EntryRequestVersion = 12;
    public const string ResultingRequestStatus = "Queued";
    public const int ResultingRequestVersion = 13;

    public static NormalizedTelehealthApplicantRequestQueueAuthorizationCommand Normalize(
        AuthorizeTelehealthApplicantRequestToQueue request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_queue_authorization_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.AuthorizationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_queue_authorization_snapshot_invalid",
                "Reload the queue-authorization review before continuing.");
        }

        if (!request.SyntheticEvidenceReviewed
            || !request.NoCoverageGuaranteeAcknowledged
            || !request.PracticeAcceptsForQueueAcknowledged
            || !request.QueueNotCareAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_queue_authorization_acknowledgments_required",
                "Confirm the synthetic evidence, no-guarantee, practice-acceptance, and queue-is-not-care statements before continuing.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticEvidenceReviewed,
            request.NoCoverageGuaranteeAcknowledged,
            request.PracticeAcceptsForQueueAcknowledged,
            request.QueueNotCareAcknowledged);
    }

    public static string SnapshotFingerprint(
        Guid requestId,
        Guid submissionId,
        Guid applicantId,
        int requestVersion,
        string practiceId,
        int facilityId,
        string canonicalPatientId,
        string submissionSnapshotFingerprint,
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
        DateTimeOffset submittedAt,
        DateTimeOffset resultValidThrough) =>
        TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-queue-authorization-v1",
            requestId,
            submissionId,
            applicantId,
            requestVersion,
            practiceId,
            facilityId,
            canonicalPatientId,
            submissionSnapshotFingerprint,
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
            submittedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            resultValidThrough.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            SourceMode,
            CompatibilityTarget,
            BusinessOutcome);
}
