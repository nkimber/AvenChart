// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantCommunicationAccessService(
    TelehealthApplicantCommunicationAccessRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantCommunicationAccessReadinessResponse> GetAsync(
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
            TelehealthApplicantCommunicationAccessRepository.Snapshot(context),
            context.ReadinessId is not null,
            context.PreferredSpokenLanguage,
            context.InterpreterRequested,
            context.AccessibilitySupportRequested,
            context.RecordedAt);
    }

    public async Task<TelehealthApplicantCommunicationAccessReadinessResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantCommunicationAccessReadinessRequest request,
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
        var snapshot = TelehealthApplicantCommunicationAccessRepository.Snapshot(current);
        var normalized = TelehealthApplicantCommunicationAccessPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-communication-access-readiness-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.PreferredSpokenLanguage,
            normalized.InterpreterRequested,
            normalized.AccessibilitySupportRequested,
            normalized.CurrentLocationConfirmed,
            normalized.CallbackNumberConfirmed,
            normalized.SafeAndPrivateToCommunicateConfirmed,
            normalized.DisconnectionAndEmergencyPlanAcknowledged,
            normalized.SyntheticDataConfirmed);
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
            recorded.PreferredSpokenLanguage,
            recorded.InterpreterRequested,
            recorded.AccessibilitySupportRequested,
            recorded.RecordedAt);
    }

    private static TelehealthApplicantCommunicationAccessReadinessResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantCommunicationAccessSnapshot snapshot,
        bool recorded,
        string? preferredSpokenLanguage,
        bool? interpreterRequested,
        bool? accessibilitySupportRequested,
        DateTimeOffset? recordedAt) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            CurrentLocationStateCode: snapshot.CurrentLocationStateCode,
            MaskedCallbackPhone: snapshot.MaskedCallbackPhone,
            SupportedSpokenLanguages: TelehealthApplicantCommunicationAccessPolicy.SupportedLanguages,
            PreferredSpokenLanguage: preferredSpokenLanguage,
            InterpreterRequested: interpreterRequested,
            AccessibilitySupportRequested: accessibilitySupportRequested,
            ContextSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantCommunicationAccessPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantCommunicationAccessPolicy.PolicyVersion,
            ReadinessRecorded: recorded,
            RecordedAt: recordedAt,
            SafeAndPrivateToCommunicateConfirmed: recorded,
            InterpreterAssigned: false,
            AccessibilityAccommodationArranged: false,
            CommunicationArrangementCompleted: false,
            SupportRequestCreated: false,
            TechnologyReadinessCompleted: false,
            PatientRecordChanged: false,
            IntakeCompleted: false,
            LegalConsentEstablished: false,
            PracticeAccepted: false,
            RequestCreated: false,
            QueueEnabled: false,
            CareEnabled: false,
            Direction: recorded
                ? "The synthetic communication/access-readiness receipt was recorded. Preferences are not arrangements; technology, intake, consent, acceptance, request, queue, and care gates remain closed."
                : "Confirm the masked location/callback context and record only bounded synthetic language and support preferences. If the context is wrong or you cannot communicate safely and privately, do not continue.",
            Limitations:
            [
                "Synthetic demonstration only; no interpreter, accessibility service, communication provider, emergency contact, or practice staff member was contacted.",
                "A language or support preference does not mean an interpreter, accommodation, translated content, or multi-party connection has been arranged.",
                "This receipt creates no patient change, portal access, complete intake, consent, technology readiness, practice acceptance, request, queue entry, appointment, encounter, or care capability."
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
