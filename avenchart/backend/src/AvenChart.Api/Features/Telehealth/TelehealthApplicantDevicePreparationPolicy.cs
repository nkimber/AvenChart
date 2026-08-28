// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantDevicePreparationSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantDevicePreparation(
    int ExpectedVersion,
    string PreparationSnapshotFingerprint,
    bool BrowserSupported,
    bool CameraAvailable,
    bool MicrophoneAvailable,
    bool SpeakerAvailable,
    string NetworkQuality,
    bool ClientReportedResultAcknowledged,
    bool NoReadinessGuaranteeAcknowledged,
    bool RecheckBeforeConsultationAcknowledged);

public static class TelehealthApplicantDevicePreparationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_DEVICE_PREPARATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT";
    public const string EntryStatus = "SyntheticCommunicationAccessReadinessRecorded";
    public const string ResultingStatus = "SyntheticDevicePreparationRecorded";
    public static readonly IReadOnlyList<string> SupportedNetworkQualities = ["Unknown", "Good"];

    public static TelehealthApplicantDevicePreparationSnapshot Snapshot(
        Guid communicationAccessReadinessId,
        string communicationContextFingerprint,
        string currentLocationStateCode,
        string callbackPhoneLast4) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-device-preparation-snapshot-v1",
                communicationAccessReadinessId,
                communicationContextFingerprint,
                currentLocationStateCode,
                callbackPhoneLast4));

    public static NormalizedTelehealthApplicantDevicePreparation Normalize(
        RecordTelehealthApplicantDevicePreparationRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_device_preparation_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.PreparationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_device_preparation_fingerprint_invalid",
                "Reload the device-preparation context before recording the result.");
        }

        if (!request.BrowserSupported
            || !request.CameraAvailable
            || !request.MicrophoneAvailable
            || !request.SpeakerAvailable)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_device_preparation_capabilities_required",
                "The secure browser, camera, microphone, and speaker checks must all pass before recording preparation.");
        }

        var networkQuality = SupportedNetworkQualities.FirstOrDefault(value =>
            string.Equals(value, request.NetworkQuality?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (networkQuality is null)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_device_preparation_network_invalid",
                "The coarse network result must be Unknown or Good; a limited result must be retried on another connection.");
        }

        if (!request.ClientReportedResultAcknowledged
            || !request.NoReadinessGuaranteeAcknowledged
            || !request.RecheckBeforeConsultationAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_device_preparation_acknowledgments_required",
                "Confirm every device-preparation limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            request.BrowserSupported,
            request.CameraAvailable,
            request.MicrophoneAvailable,
            request.SpeakerAvailable,
            networkQuality,
            request.ClientReportedResultAcknowledged,
            request.NoReadinessGuaranteeAcknowledged,
            request.RecheckBeforeConsultationAcknowledged);
    }
}
