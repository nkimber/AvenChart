// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProfessionalClaimPreparationService(
    TelehealthProfessionalClaimPreparationRepository repository,
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
}
