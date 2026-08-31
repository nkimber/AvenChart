// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthSyntheticVisitClosureService(TelehealthSyntheticVisitClosureRepository repository, IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;
    public async Task<TelehealthSyntheticVisitClosureResponse> CloseAsync(AuthSessionResponse session, StaffAccessContext access, Guid consultationId, CloseSyntheticTelehealthVisitRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role)) throw TelehealthProblem.Forbidden("telehealth_physician_role_required", "An eligible physician role is required to close a synthetic telehealth visit.");
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(access.FacilityId, _options.FacilityId)) throw TelehealthProblem.NotFound();
        var physician = session.StaffId ?? throw TelehealthProblem.Forbidden("telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
        if (request.ExpectedConsultationVersion < 1) throw TelehealthProblem.BadRequest("telehealth_visit_closure_invalid", "ExpectedConsultationVersion must be positive.");
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey); var fingerprint = TelehealthCommandFingerprint.Create("close-synthetic-telehealth-visit", consultationId, request.ExpectedConsultationVersion, request.EncounterLockReviewed, request.SyntheticClosureConfirmed);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{_options.PracticeId}:{physician}")));
        try { return await repository.CloseAsync(_options.PracticeId, _options.FacilityId, physician, consultationId, request, hash, key, fingerprint, cancellationToken) ?? throw TelehealthProblem.NotFound(); }
        catch (TelehealthSyntheticVisitClosureConflictException exception) { throw TelehealthProblem.Conflict("telehealth_visit_closure_conflict", exception.Message); }
        catch (ArgumentException exception) { throw TelehealthProblem.BadRequest("telehealth_visit_closure_invalid", exception.Message); }
    }
}
