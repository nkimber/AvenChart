// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestInsuranceSourceSnapshot(
    string PayerDisplayName,
    string ProductDisplayName,
    string MaskedMemberId,
    string? MaskedGroupNumber,
    string SubscriberRelationship,
    string CoveragePriority,
    string PreviousEligibilityBusinessOutcome,
    DateTimeOffset PreviousEligibilityCheckedAt,
    DateTimeOffset PreviousEligibilityExpiresAt,
    string PreviousPracticeNetworkBusinessOutcome,
    DateTimeOffset PreviousPracticeNetworkCheckedAt,
    DateTimeOffset PreviousPracticeNetworkExpiresAt,
    DateTimeOffset ContextExpiresAt,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantRequestInsuranceSourceConfirmation(
    int ExpectedRequestVersion,
    string InsuranceSourceSnapshotFingerprint,
    bool PayerProductConfirmed,
    bool MaskedMemberDetailsConfirmed,
    bool SubscriberRelationshipConfirmed,
    bool PrimaryCoverageSourceConfirmed,
    bool FreshVerificationRequested,
    bool EvidenceLimitationsAcknowledged,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantRequestInsuranceSourcePolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION";
    public const string ApplicantStatus = "SyntheticRequestCreated";
    public const int ApplicantVersion = 26;
    public const string RequestStatus = "Verification";
    public const int EntryRequestVersion = 5;
    public const int ResultingRequestVersion = 6;

    public static NormalizedTelehealthApplicantRequestInsuranceSourceConfirmation Normalize(
        ConfirmTelehealthApplicantRequestInsuranceSource request)
    {
        if (request.ExpectedRequestVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_insurance_source_version_invalid",
                "ExpectedRequestVersion must be positive.");
        }

        var fingerprint = (request.InsuranceSourceSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_insurance_source_snapshot_invalid",
                "Reload the insurance-source confirmation step before continuing.");
        }

        if (!request.PayerProductConfirmed
            || !request.MaskedMemberDetailsConfirmed
            || !request.SubscriberRelationshipConfirmed
            || !request.PrimaryCoverageSourceConfirmed
            || !request.FreshVerificationRequested
            || !request.EvidenceLimitationsAcknowledged
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_request_insurance_source_confirmations_required",
                "All seven insurance-source confirmations must be explicitly accepted.");
        }

        return new(
            request.ExpectedRequestVersion,
            fingerprint,
            request.PayerProductConfirmed,
            request.MaskedMemberDetailsConfirmed,
            request.SubscriberRelationshipConfirmed,
            request.PrimaryCoverageSourceConfirmed,
            request.FreshVerificationRequested,
            request.EvidenceLimitationsAcknowledged,
            request.SyntheticDataConfirmed);
    }

    public static TelehealthApplicantRequestInsuranceSourceSnapshot Snapshot(
        Guid applicantId,
        Guid requestId,
        Guid requestIntakeReceiptId,
        Guid requestCreationId,
        Guid insuranceHandoffConfirmationId,
        Guid memberInsuranceDetailsId,
        Guid eligibilityResultId,
        Guid networkDeterminationId,
        Guid promotionId,
        Guid practiceReviewCaseId,
        Guid practiceReviewAuthorizationId,
        int requestVersion,
        string canonicalPatientId,
        string sourceInsuranceSnapshotFingerprint,
        string payerDisplayName,
        string productDisplayName,
        string memberIdLast4,
        string? groupNumberLast4,
        string subscriberRelationship,
        string coveragePriority,
        string previousEligibilityBusinessOutcome,
        DateTimeOffset previousEligibilityCheckedAt,
        DateTimeOffset previousEligibilityExpiresAt,
        string previousPracticeNetworkBusinessOutcome,
        DateTimeOffset previousPracticeNetworkCheckedAt,
        DateTimeOffset previousPracticeNetworkExpiresAt,
        DateTimeOffset requestIntakeCapturedAt,
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
            previousEligibilityBusinessOutcome,
            previousEligibilityCheckedAt,
            previousEligibilityExpiresAt,
            previousPracticeNetworkBusinessOutcome,
            previousPracticeNetworkCheckedAt,
            previousPracticeNetworkExpiresAt,
            effectiveExpiry,
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-request-insurance-source-context-v1",
                applicantId,
                requestId,
                requestIntakeReceiptId,
                requestCreationId,
                insuranceHandoffConfirmationId,
                memberInsuranceDetailsId,
                eligibilityResultId,
                networkDeterminationId,
                promotionId,
                practiceReviewCaseId,
                practiceReviewAuthorizationId,
                requestVersion,
                canonicalPatientId,
                sourceInsuranceSnapshotFingerprint,
                payerDisplayName,
                productDisplayName,
                memberIdLast4,
                groupNumberLast4 ?? string.Empty,
                subscriberRelationship,
                coveragePriority,
                previousEligibilityBusinessOutcome,
                previousEligibilityCheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                previousEligibilityExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                previousPracticeNetworkBusinessOutcome,
                previousPracticeNetworkCheckedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                previousPracticeNetworkExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                requestIntakeCapturedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                effectiveExpiry.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }
}
