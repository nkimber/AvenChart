// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPracticeReviewClaimService(
    TelehealthApplicantPracticeReviewClaimRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPracticeReviewClaimResponse> ClaimAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid caseId,
        ClaimTelehealthApplicantPracticeReviewRequest request,
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
                "telehealth_practice_review_claim_version_invalid",
                "ExpectedApplicantVersion must be positive.");
        }
        if (request.InboxPolicyVersion != TelehealthApplicantPracticeReviewInboxPolicy.PolicyVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_claim_policy_version_conflict",
                "The practice-review inbox policy changed. Refresh before retrying.");
        }
        TelehealthApplicantPracticeReviewClaimPolicy.RequireAcknowledgments(
            request.NoDecisionAcknowledged,
            request.NoPatientContactAcknowledged,
            request.NoRequestOrCareQueueAcknowledged);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "claim-applicant-practice-review-v1", caseId, request.ExpectedApplicantVersion,
            request.InboxPolicyVersion, session.Username,
            request.NoDecisionAcknowledged, request.NoPatientContactAcknowledged,
            request.NoRequestOrCareQueueAcknowledged);
        var record = await repository.ClaimAsync(
            _options.PracticeId, accessContext.FacilityId, session.StaffId,
            session.Username, actorRole, caseId, request.ExpectedApplicantVersion,
            request.NoDecisionAcknowledged, request.NoPatientContactAcknowledged,
            request.NoRequestOrCareQueueAcknowledged, key, fingerprint, cancellationToken);
        return new(
            record.ClaimId,
            record.PracticeReviewCaseId,
            record.ApplicantVersion,
            record.PolicyKey,
            record.PolicyVersion,
            record.EvidenceType,
            record.AssignedAt,
            record.AssignmentExpiresAt,
            Assigned: true,
            AssignedToCurrentUser: true,
            StaffReviewWorkItemExists: true,
            StaffActionTaken: true,
            PriorityAssigned: false,
            PracticeAccepted: false,
            PracticeDeclined: false,
            PatientContacted: false,
            ClinicianReviewCreated: false,
            TelehealthRequestCreated: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Limitations:
            [
                "This 120-second synthetic claim only prevents simultaneous staff review work.",
                "No priority, decision, patient contact, response-time promise, request, or care queue was created.",
                "No clinical, appointment, encounter, prescribing, financial, integration, or external action is available."
            ]);
    }
}
