// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantInsuranceHandoffSnapshot(
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdMask,
    string? GroupNumberMask,
    string SubscriberRelationship,
    string CoveragePriority,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    string PracticeNetworkBusinessOutcome,
    DateTimeOffset PracticeNetworkCheckedAt,
    DateTimeOffset PracticeNetworkExpiresAt,
    bool RenderingPhysicianNetworkChecked,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantInsuranceHandoffConfirmation(
    int ExpectedVersion,
    string InsuranceSnapshotFingerprint,
    bool PayerAndProductConfirmed,
    bool MaskedMemberDetailsConfirmed,
    bool SubscriberRelationshipConfirmed,
    bool EvidenceLimitationsAcknowledged,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantInsuranceHandoffPolicy
{
    public const string PolicyKey = "SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_INSURANCE_HANDOFF_NO_EDIT_CONFIRMATION";
    public const string ResultingStatus = "SyntheticInsuranceDetailsConfirmed";

    public static TelehealthApplicantInsuranceHandoffSnapshot Snapshot(
        Guid memberInsuranceDetailsId,
        Guid eligibilityResultId,
        Guid practiceNetworkDeterminationId,
        string payerDisplayName,
        string productDisplayName,
        string memberIdLast4,
        string? groupNumberLast4,
        string subscriberRelationship,
        string coveragePriority,
        string eligibilityBusinessOutcome,
        DateTimeOffset eligibilityCheckedAt,
        DateTimeOffset eligibilityExpiresAt,
        string practiceNetworkBusinessOutcome,
        DateTimeOffset practiceNetworkCheckedAt,
        DateTimeOffset practiceNetworkExpiresAt,
        bool renderingPhysicianNetworkChecked) => new(
            payerDisplayName,
            productDisplayName,
            MaskLastFour(memberIdLast4),
            groupNumberLast4 is null ? null : MaskLastFour(groupNumberLast4),
            subscriberRelationship,
            coveragePriority,
            eligibilityBusinessOutcome,
            eligibilityCheckedAt,
            eligibilityExpiresAt,
            practiceNetworkBusinessOutcome,
            practiceNetworkCheckedAt,
            practiceNetworkExpiresAt,
            renderingPhysicianNetworkChecked,
            TelehealthCommandFingerprint.Create(
                "synthetic-insurance-handoff-snapshot-v1",
                memberInsuranceDetailsId,
                eligibilityResultId,
                practiceNetworkDeterminationId,
                payerDisplayName,
                productDisplayName,
                memberIdLast4,
                groupNumberLast4,
                subscriberRelationship,
                coveragePriority,
                eligibilityBusinessOutcome,
                eligibilityCheckedAt,
                eligibilityExpiresAt,
                practiceNetworkBusinessOutcome,
                practiceNetworkCheckedAt,
                practiceNetworkExpiresAt,
                renderingPhysicianNetworkChecked));

    public static NormalizedTelehealthApplicantInsuranceHandoffConfirmation Normalize(
        ConfirmTelehealthApplicantInsuranceHandoffRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_insurance_handoff_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.InsuranceSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_insurance_handoff_fingerprint_invalid",
                "Reload the insurance handoff before confirming it.");
        }

        if (!request.PayerAndProductConfirmed
            || !request.MaskedMemberDetailsConfirmed
            || !request.SubscriberRelationshipConfirmed
            || !request.EvidenceLimitationsAcknowledged
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_insurance_handoff_acknowledgments_required",
                "Confirm every insurance handoff statement before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.PayerAndProductConfirmed,
            request.MaskedMemberDetailsConfirmed,
            request.SubscriberRelationshipConfirmed,
            request.EvidenceLimitationsAcknowledged,
            request.SyntheticDataConfirmed);
    }

    private static string MaskLastFour(string lastFour) => $"••••{lastFour}";
}
