// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestLocationService(
    TelehealthApplicantRequestLocationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestLocationResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var result = await repository.GetAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(result);
    }

    public async Task<TelehealthApplicantRequestLocationResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantRequestLocation request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestLocationPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-location-confirmation-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.CurrentLocationStateCode,
            normalized.CurrentLocationConfirmed,
            normalized.CallbackNumberConfirmed,
            normalized.ChangedLocationRequiresRestartAcknowledged,
            normalized.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
        var result = await repository.ConfirmAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(result);
    }

    private static TelehealthApplicantRequestLocationResponse ToResponse(
        TelehealthApplicantRequestLocationRecord result) => new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestLocationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestLocationPolicy.PolicyVersion,
            ContextSnapshotFingerprint: result.ContextSnapshotFingerprint,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            MaskedCallbackPhone: $"***-***-{result.CallbackPhoneLast4}",
            ConfirmationReady: !result.LocationConfirmed,
            LocationConfirmed: result.LocationConfirmed,
            ConfirmedAt: result.ConfirmedAt,
            TriageAssessmentCreated: false,
            ClinicalReviewCreated: false,
            PatientContacted: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            DoctorSearchStarted: false,
            QueuePositionAssigned: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: result.LocationConfirmed
                ? "The request location and masked callback route are confirmed. No triage result or downstream care workflow was created."
                : "Confirm that the displayed state is your current physical location and that the masked callback route remains correct. If either changed, stop and restart or request review.",
            Limitations:
            [
                "Synthetic demonstration only. The callback route is masked and cannot be edited here.",
                "A changed location cannot continue because state-specific notice, network, eligibility, and readiness evidence may need to be rebuilt.",
                "This step creates no triage result, clinical review, contact, doctor search, queue, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action."
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
