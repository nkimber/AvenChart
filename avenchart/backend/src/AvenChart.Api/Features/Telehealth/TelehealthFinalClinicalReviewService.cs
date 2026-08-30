// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthFinalClinicalReviewService(
    TelehealthFinalClinicalReviewRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthFinalClinicalReviewWorkspaceResponse> GetWorkspaceAsync(
        AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId, CancellationToken cancellationToken)
    {
        var physician = RequirePhysician(session, accessContext, "review synthetic final clinical-review evidence");
        return await repository.GetWorkspaceAsync(_options.PracticeId, _options.FacilityId, physician, consultationId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthFinalClinicalReviewResponse> RecordAsync(
        AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId,
        RecordTelehealthFinalClinicalReviewRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var physician = RequirePhysician(session, accessContext, "record synthetic final clinical-review evidence");
        try
        {
            if (request.ExpectedDocumentationVersion < 1 || request.ExpectedDispositionVersion < 1)
            {
                throw new ArgumentException("Expected documentation and safety-disposition versions must identify current drafts.");
            }
            if (!request.DocumentationReviewed || !request.PhysicianResponsibilityConfirmed
                || !request.NoAutomaticClaimOrDeliveryConfirmed || !request.SyntheticDataConfirmed)
            {
                throw new ArgumentException("Confirm review, physician responsibility, no automatic claim or delivery, and synthetic-only effect before recording final clinical-review evidence.");
            }
            var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
            var fingerprint = TelehealthCommandFingerprint.Create(
                "record-synthetic-final-clinical-review", consultationId, request.ExpectedDocumentationVersion,
                request.ExpectedDispositionVersion, request.DocumentationReviewed, request.PhysicianResponsibilityConfirmed,
                request.NoAutomaticClaimOrDeliveryConfirmed, request.SyntheticDataConfirmed);
            return await repository.RecordAsync(_options.PracticeId, _options.FacilityId, physician, consultationId,
                request, key, fingerprint, cancellationToken) ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthFinalClinicalReviewConflictException exception)
        {
            throw TelehealthProblem.Conflict("telehealth_final_clinical_review_conflict", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_final_clinical_review_invalid", exception.Message);
        }
    }

    private int RequirePhysician(AuthSessionResponse session, StaffAccessContext accessContext, string action)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden("telehealth_physician_role_required", $"An eligible physician role is required to {action}.");
        }
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
        return session.StaffId ?? throw TelehealthProblem.Forbidden(
            "telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
    }
}
