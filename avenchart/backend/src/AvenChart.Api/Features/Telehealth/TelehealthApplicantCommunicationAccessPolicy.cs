// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantCommunicationAccessSnapshot(
    string CurrentLocationStateCode,
    string MaskedCallbackPhone,
    string Fingerprint);

public sealed record NormalizedTelehealthApplicantCommunicationAccessReadiness(
    int ExpectedVersion,
    string ContextSnapshotFingerprint,
    string PreferredSpokenLanguage,
    bool InterpreterRequested,
    bool AccessibilitySupportRequested,
    bool CurrentLocationConfirmed,
    bool CallbackNumberConfirmed,
    bool SafeAndPrivateToCommunicateConfirmed,
    bool DisconnectionAndEmergencyPlanAcknowledged,
    bool SyntheticDataConfirmed);

public static class TelehealthApplicantCommunicationAccessPolicy
{
    public const string PolicyKey = "SYNTHETIC_COMMUNICATION_ACCESS_READINESS";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_COMMUNICATION_ACCESS_READINESS_RECEIPT";
    public const string EntryStatus = "SyntheticInsuranceDetailsConfirmed";
    public const string ResultingStatus = "SyntheticCommunicationAccessReadinessRecorded";
    public static readonly IReadOnlyList<string> SupportedLanguages = ["English", "Spanish"];

    public static TelehealthApplicantCommunicationAccessSnapshot Snapshot(
        Guid safetyEvaluationId,
        Guid insuranceHandoffConfirmationId,
        string currentLocationStateCode,
        string callbackPhone) => new(
            currentLocationStateCode,
            TelehealthProspectiveApplicantPolicy.MaskPhone(callbackPhone),
            TelehealthCommandFingerprint.Create(
                "synthetic-communication-access-context-snapshot-v1",
                safetyEvaluationId,
                insuranceHandoffConfirmationId,
                currentLocationStateCode,
                callbackPhone));

    public static NormalizedTelehealthApplicantCommunicationAccessReadiness Normalize(
        RecordTelehealthApplicantCommunicationAccessReadinessRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_communication_access_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.ContextSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_communication_access_fingerprint_invalid",
                "Reload the communication and access context before confirming it.");
        }

        var preferredLanguage = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language, request.PreferredSpokenLanguage?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (preferredLanguage is null)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_communication_access_language_invalid",
                "Choose one of the available synthetic spoken-language preferences.");
        }

        if (!request.CurrentLocationConfirmed
            || !request.CallbackNumberConfirmed
            || !request.SafeAndPrivateToCommunicateConfirmed
            || !request.DisconnectionAndEmergencyPlanAcknowledged
            || !request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_communication_access_acknowledgments_required",
                "Confirm every communication and access-readiness statement before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            preferredLanguage,
            request.InterpreterRequested,
            request.AccessibilitySupportRequested,
            request.CurrentLocationConfirmed,
            request.CallbackNumberConfirmed,
            request.SafeAndPrivateToCommunicateConfirmed,
            request.DisconnectionAndEmergencyPlanAcknowledged,
            request.SyntheticDataConfirmed);
    }
}
