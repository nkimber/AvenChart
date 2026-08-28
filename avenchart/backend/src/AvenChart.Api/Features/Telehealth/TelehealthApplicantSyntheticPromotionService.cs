// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantSyntheticPromotionService(
    TelehealthApplicantSyntheticPromotionRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantSyntheticPromotionQueueResponse> ListAsync(
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

    public async Task<TelehealthApplicantSyntheticPromotionResponse> ExecuteAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid applicantId,
        ExecuteTelehealthApplicantSyntheticPromotionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "ExpectedVersion must be positive.");
        }
        if (!string.Equals(
            request.Command?.Trim(),
            TelehealthApplicantSyntheticPromotionPolicy.Command,
            StringComparison.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_synthetic_promotion_command_invalid",
                $"Command must be {TelehealthApplicantSyntheticPromotionPolicy.Command}.");
        }
        var reason = TelehealthApplicantSyntheticPromotionPolicy.NormalizeReason(request.Reason);
        if (!request.CanonicalPatientCreationAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_canonical_patient_creation_acknowledgment_required",
                "Acknowledge that a no-match outcome creates a canonical synthetic patient shell.");
        }
        if (!request.NoPortalNoCareAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_no_portal_no_care_acknowledgment_required",
                "Acknowledge that promotion creates no portal, request, queue, or care capability.");
        }
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "execute-prospective-synthetic-promotion-v1", applicantId,
            request.ExpectedVersion, TelehealthApplicantSyntheticPromotionPolicy.Command,
            reason, request.CanonicalPatientCreationAcknowledged,
            request.NoPortalNoCareAcknowledged);
        return ToResponse(await repository.ExecuteAsync(
            _options.PracticeId, accessContext.FacilityId, session.StaffId,
            session.Username, applicantId, request.ExpectedVersion, reason,
            request.CanonicalPatientCreationAcknowledged,
            request.NoPortalNoCareAcknowledged, key, fingerprint, cancellationToken));
    }

    private static TelehealthApplicantSyntheticPromotionItemResponse ToItem(
        TelehealthApplicantSyntheticPromotionCandidate applicant) => new(
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
        applicant.AuthorizationDecision,
        applicant.AuthorizedAt,
        applicant.AssuranceLevelAchieved,
        applicant.IdentityProofed,
        applicant.ApplicantExpiresAt,
        applicant.ProofingExpiresAt,
        TelehealthApplicantSyntheticPromotionPolicy.Command);

    private static TelehealthApplicantSyntheticPromotionResponse ToResponse(
        TelehealthApplicantSyntheticPromotionRecord promotion) => new(
        promotion.PromotionId,
        promotion.ApplicantId,
        promotion.ApplicantVersion,
        promotion.ApplicantStatus,
        promotion.Outcome,
        promotion.PossibleMatchDetected,
        promotion.CanonicalPatientCreated,
        promotion.PolicyKey,
        promotion.PolicyVersion,
        promotion.EvidenceType,
        promotion.ExecutedAt,
        promotion.PortalAccountCreated,
        promotion.ProspectiveIntakeCompleted,
        promotion.ConsentCreated,
        promotion.PracticeAccepted,
        promotion.InsuranceCreated,
        promotion.RequestCreated,
        promotion.QueueEnabled,
        promotion.CareEnabled,
        Limitations());

    private static string[] Limitations() =>
    [
        "Synthetic demonstration only; assurance remains None and identity was not proved.",
        "A newly detected possible match blocks creation without identifying or linking any patient.",
        "A created shell has no portal, completed intake, consent, coverage, request, queue, or care capability."
    ];

    private static void RequireAdministrator(AuthSessionResponse session)
    {
        if (string.Equals(session.Role, "frontdesk", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(session.Role)
            || !session.Role.Contains("admin", StringComparison.OrdinalIgnoreCase))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_canonical_promotion_administrator_required",
                "An authorized practice administrator is required to execute synthetic patient promotion.");
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
