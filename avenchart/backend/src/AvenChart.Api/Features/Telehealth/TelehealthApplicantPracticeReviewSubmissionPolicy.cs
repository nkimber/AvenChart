// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewSubmissionSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantPracticeReviewSubmission(
    int ExpectedVersion,
    string PracticeReviewSnapshotFingerprint,
    bool PatientReportedInformationAcknowledged,
    bool PracticeMayRequestInformationOrDeclineAcknowledged,
    bool NoTelehealthRequestOrCareQueueAcknowledged,
    bool WorseningSymptomsRequireImmediateActionAcknowledged);

public static class TelehealthApplicantPracticeReviewSubmissionPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_PRACTICE_REVIEW_SUBMISSION_RECEIPT";
    public const string EntryStatus = "SyntheticPreRequestReadinessAcknowledged";
    public const string ResultingStatus = "SyntheticPracticeReviewSubmitted";
    public const string ReviewStatus = "PendingPracticeReview";

    public static TelehealthApplicantPracticeReviewSubmissionSnapshot Snapshot(
        Guid applicantId,
        Guid readinessAcknowledgmentId,
        int readinessApplicantVersion,
        string readinessSnapshotFingerprint,
        string overallRoute,
        string canonicalPatientId,
        DateTimeOffset applicantExpiresAt) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-practice-review-submission-snapshot-v1",
                applicantId,
                readinessAcknowledgmentId,
                readinessApplicantVersion,
                readinessSnapshotFingerprint,
                overallRoute,
                canonicalPatientId,
                applicantExpiresAt));

    public static NormalizedTelehealthApplicantPracticeReviewSubmission Normalize(
        SubmitTelehealthApplicantPracticeReviewRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_practice_review_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.PracticeReviewSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_practice_review_fingerprint_invalid",
                "Reload the practice review submission before continuing.");
        }

        if (!request.PatientReportedInformationAcknowledged
            || !request.PracticeMayRequestInformationOrDeclineAcknowledged
            || !request.NoTelehealthRequestOrCareQueueAcknowledged
            || !request.WorseningSymptomsRequireImmediateActionAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_practice_review_acknowledgments_required",
                "Confirm every practice review submission limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.PatientReportedInformationAcknowledged,
            request.PracticeMayRequestInformationOrDeclineAcknowledged,
            request.NoTelehealthRequestOrCareQueueAcknowledged,
            request.WorseningSymptomsRequireImmediateActionAcknowledged);
    }
}
