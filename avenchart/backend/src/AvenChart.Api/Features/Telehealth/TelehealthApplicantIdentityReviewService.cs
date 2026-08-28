// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantIdentityReviewService(
    TelehealthApplicantIdentityReviewRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantIdentityReviewQueueResponse> ListAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        var result = await repository.ListAsync(_options.PracticeId, accessContext.FacilityId, cancellationToken);
        return new TelehealthApplicantIdentityReviewQueueResponse(
            _options.PracticeDisplayName,
            result.DatabaseNow,
            result.Applicants.Select(ToItem).ToArray(),
            Limitations());
    }

    public async Task<TelehealthApplicantIdentityReviewDecisionResponse> RecordAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid applicantId,
        RecordTelehealthApplicantIdentityReviewRequest request,
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
        _ = TelehealthApplicantIdentityReviewPolicy.ResultingStatus(decision);
        var reason = TelehealthApplicantIdentityReviewPolicy.NormalizeReason(request.Reason);
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_synthetic_confirmation_required",
                "Confirm that this review uses synthetic demonstration data only.");
        }
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "record-prospective-identity-review-v1", applicantId, request.ExpectedVersion,
            decision, reason, request.SyntheticDataConfirmed);
        return ToResponse(await repository.RecordAsync(
            _options.PracticeId, accessContext.FacilityId, session.StaffId, session.Username, actorRole,
            applicantId, request.ExpectedVersion, decision, reason, key, fingerprint,
            cancellationToken));
    }

    private static TelehealthApplicantIdentityReviewItemResponse ToItem(
        TelehealthApplicantIdentityReviewRecord applicant) => new(
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
        applicant.ContactVerifiedAt,
        applicant.DuplicateDisposition,
        TelehealthApplicantIdentityReviewPolicy.AllowedDecision(applicant.DuplicateDisposition),
        applicant.CreatedAt,
        applicant.ExpiresAt);

    private static TelehealthApplicantIdentityReviewDecisionResponse ToResponse(
        TelehealthApplicantIdentityDecisionRecord decision) => new(
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
        decision.IdentityProofed,
        decision.CanonicalPatientCreated,
        decision.ChartLinked,
        decision.ProspectiveIntakeCompleted,
        decision.RequestCreated,
        decision.QueueEnabled,
        Limitations());

    private static string[] Limitations() =>
    [
        "Synthetic demonstration only; no real identity evidence is processed.",
        "Contact control and duplicate disposition are not identity proofing or an identity-assurance level.",
        "The applicant remains prospective; no patient, chart, portal, request, or queue entry is created."
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
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }
}
