// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProfessionalClaimPreparationService(
    TelehealthProfessionalClaimPreparationRepository repository,
    IProfessionalClaimGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProfessionalClaimPreparationWorkspaceResponse> GetWorkspaceAsync(
        AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId, CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden("telehealth_physician_role_required", "An eligible physician role is required to review telehealth claim-preparation blockers.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        var physician = session.StaffId ?? throw TelehealthProblem.Forbidden(
            "telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
        return await repository.GetWorkspaceAsync(_options.PracticeId, _options.FacilityId, physician, consultationId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthProfessionalClaimPreparationResponse> PrepareAsync(
        AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId,
        PrepareTelehealthProfessionalClaimRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden("telehealth_physician_role_required", "An eligible physician role is required to prepare a synthetic professional-claim receipt.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId)) throw TelehealthProblem.NotFound();
        var physician = session.StaffId ?? throw TelehealthProblem.Forbidden("telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
        if (request.ExpectedDocumentationVersion < 1 || request.ExpectedDispositionVersion < 1 || request.ExpectedFinalClinicalReviewVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_claim_preparation_invalid", "Every expected source-evidence version must be positive.");
        }
        try
        {
            var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
            var fingerprint = TelehealthCommandFingerprint.Create("prepare-synthetic-professional-claim", consultationId,
                request.ExpectedDocumentationVersion, request.ExpectedDispositionVersion, request.ExpectedFinalClinicalReviewVersion,
                request.SourceEvidenceReviewed, request.SyntheticOnlyConfirmed, request.NoSubmissionConfirmed);
            var actorHash = TelehealthCommandFingerprint.Create("synthetic-professional-claim-actor", _options.PracticeId, physician);
            return await repository.PrepareAsync(_options.PracticeId, _options.FacilityId, physician, consultationId, request,
                actorHash, key, fingerprint, gateway, cancellationToken) ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthProfessionalClaimPreparationConflictException exception)
        {
            throw TelehealthProblem.Conflict("telehealth_claim_preparation_conflict", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_claim_preparation_invalid", exception.Message);
        }
    }
}
