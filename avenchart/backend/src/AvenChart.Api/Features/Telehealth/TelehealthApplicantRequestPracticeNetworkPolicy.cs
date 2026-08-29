// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestPracticeNetworkSnapshot(
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestPracticeNetworkCommand(
    int ExpectedRequestVersion,
    string NetworkSnapshotFingerprint,
    bool SyntheticDataConfirmed,
    bool PracticeOnlyScopeAcknowledged,
    bool NoGuaranteeAcknowledged);

public static class TelehealthApplicantRequestPracticeNetworkPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 7;
    public const int ResultingRequestVersion = 8;

    public static NormalizedTelehealthApplicantRequestPracticeNetworkCommand Normalize(
        RunTelehealthApplicantRequestPracticeNetworkVerification request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_practice_network_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.NetworkSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_practice_network_snapshot_invalid",
                "Reload the practice-network verification step before continuing.");
        }

        if (!request.SyntheticDataConfirmed
            || !request.PracticeOnlyScopeAcknowledged
            || !request.NoGuaranteeAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_practice_network_acknowledgments_required",
                "Confirm the synthetic-data, practice-only, and no-guarantee statements before verification.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticDataConfirmed,
            request.PracticeOnlyScopeAcknowledged,
            request.NoGuaranteeAcknowledged);
    }

    public static TelehealthApplicantRequestPracticeNetworkSnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid eligibilityVerificationId,
        int requestVersion,
        string canonicalPatientId,
        string practiceId,
        int facilityId,
        string practiceDisplayName,
        string planKey,
        string payerDisplayName,
        string productDisplayName,
        string currentLocationStateCode,
        string purposeCategory,
        string eligibilityBusinessOutcome,
        DateTimeOffset eligibilityCheckedAt,
        DateTimeOffset eligibilityExpiresAt,
        DateTimeOffset applicantExpiresAt)
    {
        var effectiveExpiry = eligibilityExpiresAt < applicantExpiresAt
            ? eligibilityExpiresAt
            : applicantExpiresAt;
        return new(
            practiceDisplayName,
            payerDisplayName,
            productDisplayName,
            currentLocationStateCode,
            purposeCategory,
            eligibilityBusinessOutcome,
            eligibilityCheckedAt,
            eligibilityExpiresAt,
            effectiveExpiry,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-practice-network-context-v1",
                applicantId,
                requestId,
                eligibilityVerificationId,
                requestVersion,
                canonicalPatientId,
                practiceId,
                facilityId,
                practiceDisplayName,
                planKey,
                payerDisplayName,
                productDisplayName,
                currentLocationStateCode,
                purposeCategory,
                eligibilityBusinessOutcome,
                eligibilityCheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                eligibilityExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                effectiveExpiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }
}
