// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPromotionAuthorizationService(
    TelehealthApplicantPromotionAuthorizationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPromotionAuthorizationQueueResponse> ListAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        var result = await repository.ListAsync(
            _options.PracticeId, accessContext.FacilityId, cancellationToken);
        return new(
            _options.PracticeDisplayName,
            result.DatabaseNow,
            result.Applicants.Select(ToItem).ToArray(),
            Limitations());
    }

    public async Task<TelehealthApplicantPromotionAuthorizationDecisionResponse> RecordAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid applicantId,
        RecordTelehealthApplicantPromotionAuthorizationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        var actorRole = string.Equals(session.Role, "frontdesk", StringComparison.OrdinalIgnoreCase)
            ? "frontdesk"
            : "administrator";
        if (actorRole == "frontdesk" && session.StaffId is null)
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "An authenticated front-desk identity must be bound to an active staff record.");
        }
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        var decision = request.Decision?.Trim() ?? string.Empty;
        _ = TelehealthApplicantPromotionAuthorizationPolicy.ResultingStatus(decision);
        var reason = TelehealthApplicantPromotionAuthorizationPolicy.NormalizeReason(request.Reason);
        if (!request.NoneAssuranceAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_none_assurance_acknowledgment_required",
                "Acknowledge that assurance remains None and identity was not proved.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_synthetic_confirmation_required",
                "Confirm that this authorization uses synthetic demonstration data only.");
        }
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "record-prospective-promotion-authorization-v1", applicantId,
            request.ExpectedVersion, decision, reason,
            request.NoneAssuranceAcknowledged, request.SyntheticDataConfirmed);
        return ToResponse(await repository.RecordAsync(
            _options.PracticeId, accessContext.FacilityId, session.StaffId,
            session.Username, actorRole, applicantId, request.ExpectedVersion,
            decision, reason, request.NoneAssuranceAcknowledged,
            request.SyntheticDataConfirmed, key, fingerprint, cancellationToken));
    }

    private static TelehealthApplicantPromotionAuthorizationItemResponse ToItem(
        TelehealthApplicantPromotionAuthorizationCandidate applicant) => new(
        applicant.ApplicantId,
        applicant.Version,
        applicant.Status,
        applicant.LegalFirstName,
        applicant.LegalLastName,
        applicant.DateOfBirth,
        TelehealthProspectiveApplicantPolicy.MaskEmail(applicant.Email),
        TelehealthProspectiveApplicantPolicy.MaskPhone(applicant.Phone),
        applicant.ResidenceStateCode,
        applicant.PostalCode,
        applicant.PlanKey,
        applicant.PayerDisplayName,
        applicant.ProductDisplayName,
        applicant.EligibilityStatus,
        applicant.BenefitInformationStatus,
        applicant.EligibilityBusinessOutcome,
        applicant.NetworkBusinessOutcome,
        applicant.ProofingMethod,
        applicant.TransportOutcome,
        applicant.EvidenceCollectionStatus,
        applicant.EvidenceValidationStatus,
        applicant.AttributeValidationStatus,
        applicant.ApplicantVerificationStatus,
        applicant.FraudCheckStatus,
        applicant.ProofingBusinessOutcome,
        applicant.AssuranceLevelAchieved,
        applicant.IdentityProofed,
        applicant.ProofingCheckedAt,
        applicant.ProofingExpiresAt,
        applicant.CreatedAt,
        applicant.ApplicantExpiresAt,
        TelehealthApplicantPromotionAuthorizationPolicy.Decisions);

    private static TelehealthApplicantPromotionAuthorizationDecisionResponse ToResponse(
        TelehealthApplicantPromotionAuthorizationDecisionRecord decision) => new(
        decision.DecisionId,
        decision.ApplicantId,
        decision.ApplicantVersion,
        decision.ApplicantStatus,
        decision.Decision,
        decision.Reason,
        decision.PolicyKey,
        decision.PolicyVersion,
        decision.EvidenceType,
        decision.DecidedAt,
        decision.NoneAssuranceAcknowledged,
        decision.RealIdentityProofed,
        decision.CanonicalPatientCreated,
        decision.ChartLinked,
        decision.PortalAccountCreated,
        decision.ProspectiveIntakeCompleted,
        decision.ConsentCreated,
        decision.PracticeAccepted,
        decision.RequestCreated,
        decision.QueueEnabled,
        Limitations());

    private static string[] Limitations() =>
    [
        "Synthetic demonstration only; assurance remains None and identity was not proved.",
        "Authorization permits only a separately gated future synthetic promotion exercise.",
        "The applicant remains prospective; no patient, chart, portal, consent, request, queue, or care capability is created."
    ];

    private static void RequireAdministrator(AuthSessionResponse session)
    {
        if (!TelehealthAuthorizationPolicy.IsAdministratorRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_administrator_role_required",
                "An authorized practice administrator is required for this action.");
        }
    }

    private void RequireConfiguredFacility(StaffAccessContext accessContext)
    {
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(
            accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }
}
