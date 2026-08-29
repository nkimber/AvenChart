// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestEligibilitySnapshot(
    string PayerDisplayName,
    string ProductDisplayName,
    string MaskedMemberId,
    string? MaskedGroupNumber,
    string SubscriberRelationship,
    string CoveragePriority,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestEligibilityCommand(
    int ExpectedRequestVersion,
    string EligibilitySnapshotFingerprint,
    bool SyntheticDataConfirmed,
    bool NoGuaranteeAcknowledged);

public static class TelehealthApplicantRequestEligibilityPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 6;
    public const int ResultingRequestVersion = 7;

    public static NormalizedTelehealthApplicantRequestEligibilityCommand Normalize(
        RunTelehealthApplicantRequestEligibilityVerification request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_eligibility_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.EligibilitySnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_eligibility_snapshot_invalid",
                "Reload the eligibility verification step before continuing.");
        }

        if (!request.SyntheticDataConfirmed || !request.NoGuaranteeAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_eligibility_acknowledgments_required",
                "Confirm both the synthetic-data and no-guarantee statements before running verification.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.SyntheticDataConfirmed,
            request.NoGuaranteeAcknowledged);
    }

    public static TelehealthApplicantRequestEligibilitySnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid insuranceSourceConfirmationId,
        Guid memberInsuranceDetailsId,
        int requestVersion,
        string canonicalPatientId,
        string insuranceSourceSnapshotFingerprint,
        string payerDisplayName,
        string productDisplayName,
        string memberIdLast4,
        string? groupNumberLast4,
        string subscriberRelationship,
        string coveragePriority,
        string currentLocationStateCode,
        string purposeCategory,
        DateTimeOffset sourceConfirmedAt,
        DateTimeOffset contextExpiresAt,
        DateTimeOffset applicantExpiresAt)
    {
        var effectiveExpiry = contextExpiresAt < applicantExpiresAt ? contextExpiresAt : applicantExpiresAt;
        return new(
            payerDisplayName,
            productDisplayName,
            $"••••{memberIdLast4}",
            groupNumberLast4 is null ? null : $"••••{groupNumberLast4}",
            subscriberRelationship,
            coveragePriority,
            currentLocationStateCode,
            purposeCategory,
            effectiveExpiry,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-eligibility-context-v1",
                applicantId,
                requestId,
                insuranceSourceConfirmationId,
                memberInsuranceDetailsId,
                requestVersion,
                canonicalPatientId,
                insuranceSourceSnapshotFingerprint,
                payerDisplayName,
                productDisplayName,
                memberIdLast4,
                groupNumberLast4 ?? string.Empty,
                subscriberRelationship,
                coveragePriority,
                currentLocationStateCode,
                purposeCategory,
                sourceConfirmedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                effectiveExpiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }
}
