// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthEncounterFinalizationService(
    TelehealthEncounterFinalizationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthEncounterFinalizationResponse> FinalizeAsync(
        AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId,
        FinalizeTelehealthEncounterRequest request, CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
            throw TelehealthProblem.Forbidden("telehealth_physician_role_required", "An eligible physician role is required to finalize a synthetic telehealth encounter.");
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
            throw TelehealthProblem.NotFound();
        if (request.ExpectedDocumentationVersion < 1 || request.ExpectedDispositionVersion < 1 || request.ExpectedFinalClinicalReviewVersion < 1)
            throw TelehealthProblem.BadRequest("telehealth_finalization_invalid", "Current documentation, disposition, and final-review versions are required.");
        var physician = session.StaffId ?? throw TelehealthProblem.Forbidden("telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
        try
        {
            return await repository.FinalizeAsync(_options.PracticeId, _options.FacilityId, physician, consultationId,
                request, session.Username, cancellationToken) ?? throw TelehealthProblem.NotFound();
        }
        catch (TelehealthEncounterFinalizationConflictException exception)
        {
            throw TelehealthProblem.Conflict("telehealth_finalization_conflict", exception.Message);
        }
        catch (ArgumentException exception)
        {
            throw TelehealthProblem.BadRequest("telehealth_finalization_invalid", exception.Message);
        }
    }
}
