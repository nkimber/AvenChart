// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantDevicePreparationService(
    TelehealthApplicantDevicePreparationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantDevicePreparationResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(
            context.ApplicantId,
            context.ApplicantVersion,
            context.ApplicantStatus,
            TelehealthApplicantDevicePreparationRepository.Snapshot(context),
            context.PreparationId is not null,
            context.RecordedAt,
            context.BrowserSupported,
            context.CameraAvailable,
            context.MicrophoneAvailable,
            context.SpeakerAvailable,
            context.NetworkQuality);
    }

    public async Task<TelehealthApplicantDevicePreparationResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantDevicePreparationRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var current = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            cancellationToken);
        var snapshot = TelehealthApplicantDevicePreparationRepository.Snapshot(current);
        var normalized = TelehealthApplicantDevicePreparationPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-device-preparation-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.PreparationSnapshotFingerprint,
            normalized.BrowserSupported,
            normalized.CameraAvailable,
            normalized.MicrophoneAvailable,
            normalized.SpeakerAvailable,
            normalized.NetworkQuality,
            normalized.ClientReportedResultAcknowledged,
            normalized.NoReadinessGuaranteeAcknowledged,
            normalized.RecheckBeforeConsultationAcknowledged);
        var recorded = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(
            recorded.ApplicantId,
            recorded.ApplicantVersion,
            recorded.ApplicantStatus,
            snapshot,
            true,
            recorded.RecordedAt,
            recorded.BrowserSupported,
            recorded.CameraAvailable,
            recorded.MicrophoneAvailable,
            recorded.SpeakerAvailable,
            recorded.NetworkQuality);
    }

    private static TelehealthApplicantDevicePreparationResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantDevicePreparationSnapshot snapshot,
        bool recorded,
        DateTimeOffset? recordedAt,
        bool? browserSupported,
        bool? cameraAvailable,
        bool? microphoneAvailable,
        bool? speakerAvailable,
        string? networkQuality) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            SupportedNetworkQualities: TelehealthApplicantDevicePreparationPolicy.SupportedNetworkQualities,
            PreparationSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantDevicePreparationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantDevicePreparationPolicy.PolicyVersion,
            PreparationRecorded: recorded,
            RecordedAt: recordedAt,
            BrowserSupported: browserSupported,
            CameraAvailable: cameraAvailable,
            MicrophoneAvailable: microphoneAvailable,
            SpeakerAvailable: speakerAvailable,
            NetworkQuality: networkQuality,
            ClientReportedResultAcknowledged: recorded,
            NoReadinessGuaranteeAcknowledged: recorded,
            RecheckBeforeConsultationAcknowledged: recorded,
            TechnologyReady: false,
            WaitingRoomCreated: false,
            MediaSessionCreated: false,
            CommunicationStarted: false,
            SupportArrangementCompleted: false,
            PatientRecordChanged: false,
            IntakeCompleted: false,
            LegalConsentEstablished: false,
            PracticeAccepted: false,
            RequestCreated: false,
            QueueEntered: false,
            CareAuthorized: false,
            Direction: recorded
                ? "The synthetic client-reported device-preparation receipt was recorded. Technology readiness, support, media, intake, consent, acceptance, request, queue, and care gates remain closed."
                : "Run the local device check. The browser briefly requests camera and microphone access and immediately stops test tracks; a passing result is preparation only and must be checked again before a consultation.",
            Limitations:
            [
                "Synthetic demonstration only; no media, device identifier, user agent, IP address, ICE candidate, SDP, codec, recording, transcript, or precise network measurement is sent or stored.",
                "This is a client-reported point-in-time browser check, not device certification, technology readiness, connection availability, or a guarantee that a future call will work.",
                "No waiting room, grant, media session, support arrangement, patient change, complete intake, consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created."
            ]);

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new TelehealthProblem(
                StatusCodes.Status404NotFound,
                "telehealth_practice_not_found",
                "Telehealth practice was not found",
                "This host is not configured for the synthetic telehealth practice.");
        }
    }
}
