// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestQueueAuthorizationService(
    TelehealthApplicantRequestQueueAuthorizationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestQueueAuthorizationResponse> GetAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var actorRole = RequireAdministrator(session, accessContext);
        return ToResponse(await repository.GetAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            session.StaffId,
            actorRole,
            requestId,
            cancellationToken));
    }

    public async Task<TelehealthApplicantRequestQueueAuthorizationResponse> AuthorizeAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid requestId,
        AuthorizeTelehealthApplicantRequestToQueue request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var actorRole = RequireAdministrator(session, accessContext);
        var normalized = TelehealthApplicantRequestQueueAuthorizationPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-queue-authorization-command-v1",
            requestId,
            normalized.ExpectedRequestVersion,
            normalized.AuthorizationSnapshotFingerprint,
            session.Username,
            normalized.SyntheticEvidenceReviewed,
            normalized.NoCoverageGuaranteeAcknowledged,
            normalized.PracticeAcceptsForQueueAcknowledged,
            normalized.QueueNotCareAcknowledged);
        return ToResponse(await repository.AuthorizeAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            session.StaffId,
            session.Username,
            actorRole,
            requestId,
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken));
    }

    private string RequireAdministrator(AuthSessionResponse session, StaffAccessContext accessContext)
    {
        if (!TelehealthAuthorizationPolicy.IsAdministratorRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_administrator_role_required",
                "An authorized practice administrator is required for this action.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var actorRole = string.Equals(session.Role, "frontdesk", StringComparison.OrdinalIgnoreCase)
            ? "frontdesk"
            : "administrator";
        if (actorRole == "frontdesk" && session.StaffId is null)
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "An authenticated front-desk identity must be bound to an active staff record.");
        }
        return actorRole;
    }

    private static TelehealthApplicantRequestQueueAuthorizationResponse ToResponse(
        TelehealthApplicantRequestQueueAuthorizationRecord result)
    {
        var complete = result.AuthorizedAt is not null;
        return new(
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestQueueAuthorizationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestQueueAuthorizationPolicy.PolicyVersion,
            SourceMode: TelehealthApplicantRequestQueueAuthorizationPolicy.SourceMode,
            CompatibilityTarget: TelehealthApplicantRequestQueueAuthorizationPolicy.CompatibilityTarget,
            AuthorizationSnapshotFingerprint: result.AuthorizationSnapshotFingerprint,
            ResultValidThrough: result.ResultValidThrough,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            DateOfService: result.DateOfService,
            CandidateDisplayName: result.CandidateDisplayName,
            MaskedProviderReference: $"Synthetic provider ••••{result.CandidateNpiLast4}",
            MaskedBillingProviderReference: "Synthetic billing provider ••••8800",
            ServiceCategory: result.ServiceCategory,
            Modality: result.Modality,
            AuthorizationReady: !complete,
            AuthorizationCompleted: complete,
            AuthorizedAt: result.AuthorizedAt,
            BusinessOutcome: complete ? TelehealthApplicantRequestQueueAuthorizationPolicy.BusinessOutcome : null,
            SyntheticEvidenceReviewed: complete,
            PracticeAccepted: complete,
            PatientCareQueueEntered: complete,
            ClinicianQueueEntered: complete,
            DoctorSearchStarted: complete,
            AppointmentCreated: complete,
            RealStateAuthorityVerified: false,
            RealCredentialingVerified: false,
            RenderingPhysicianAssigned: false,
            RenderingPhysicianNetworkChecked: false,
            ExactNetworkConfirmed: false,
            CanonicalCoverageCreated: false,
            CoverageSelected: false,
            CoverageVerified: false,
            FinancialRouteCreated: false,
            PatientContacted: false,
            QueuePositionAssigned: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: complete
                ? "Accepted into the synthetic clinician work queue. No clinician is assigned, and insurance, payment, wait time, or treatment is not guaranteed."
                : "Review the bounded evidence and explicitly accept this request into the synthetic clinician work queue.",
            Limitations: [
                "NON_PRODUCTION synthetic evidence only. No payer, provider directory, licensing board, credentialing source, clinician, or external destination was contacted.",
                "Queue acceptance creates an unassigned scheduling shell and internal work item; it is not clinician assignment, consent, an encounter, or authorization for care.",
                "Insurance coverage, benefits, payment, price, clinician availability, queue position, wait time, and treatment are not guaranteed.",
                "No patient contact, prescription, claim, integration, or external action was performed."
            ]);
    }
}
