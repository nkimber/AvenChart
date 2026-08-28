// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPracticeReviewAuthorizationService(
    TelehealthApplicantPracticeReviewAuthorizationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPracticeReviewAuthorizationResponse> AuthorizeAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid caseId,
        AuthorizeTelehealthApplicantPracticeReviewRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsAdministratorRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_administrator_role_required",
                "An authorized practice administrator is required for this action.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.ApplicantNotFound();
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
        if (request.ExpectedApplicantVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_practice_review_authorization_version_invalid",
                "ExpectedApplicantVersion must be positive.");
        }
        if (request.PacketPolicyVersion != TelehealthApplicantPracticeReviewPacketPolicy.PolicyVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_authorization_packet_version_conflict",
                "The operational packet policy changed. Reload the packet before retrying.");
        }
        TelehealthApplicantPracticeReviewAuthorizationPolicy.RequireCommand(
            request.Decision,
            request.RationaleCode,
            request.NoClinicalEligibilityAcknowledged,
            request.NoCoverageGuaranteeAcknowledged,
            request.NoRequestOrQueueAcknowledged);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "authorize-applicant-practice-review-v1", caseId,
            request.ExpectedApplicantVersion, request.PacketPolicyVersion,
            session.Username, TelehealthApplicantPracticeReviewAuthorizationPolicy.Decision,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.RationaleCode,
            request.NoClinicalEligibilityAcknowledged,
            request.NoCoverageGuaranteeAcknowledged,
            request.NoRequestOrQueueAcknowledged);
        var record = await repository.AuthorizeAsync(
            _options.PracticeId, accessContext.FacilityId, session.StaffId,
            session.Username, actorRole, caseId, request.ExpectedApplicantVersion,
            request.NoClinicalEligibilityAcknowledged,
            request.NoCoverageGuaranteeAcknowledged,
            request.NoRequestOrQueueAcknowledged,
            key, fingerprint, cancellationToken);
        return new(
            record.PracticeReviewCaseId,
            record.ApplicantVersion,
            record.ApplicantStatus,
            record.Decision,
            record.RationaleCode,
            record.PolicyKey,
            record.PolicyVersion,
            record.EvidenceType,
            record.DecidedAt,
            RequestCreationAuthorized: true,
            PracticeAccepted: false,
            PatientContacted: false,
            ClinicianReviewCreated: false,
            TelehealthRequestCreated: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Limitations:
            [
                "This positive-only synthetic decision authorizes only a separately gated future request-creation step.",
                "It is not clinical eligibility, a coverage guarantee, practice acceptance, or patient contact.",
                "No request, queue, appointment, encounter, consent, care, prescribing, financial, integration, or external action was created."
            ]);
    }
}
