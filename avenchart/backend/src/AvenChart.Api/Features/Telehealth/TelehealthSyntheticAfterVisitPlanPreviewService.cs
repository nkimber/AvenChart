// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthSyntheticAfterVisitPlanPreviewService(
    TelehealthSyntheticAfterVisitPlanPreviewRepository repository,
    PatientPortalRepository patientPortalRepository,
    IPatientPortalIdentityAdapter patientIdentityAdapter,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthSyntheticAfterVisitPlanPreviewResponse> GetForPatientAsync(HttpContext context, Guid requestId, CancellationToken cancellationToken)
    {
        RequireConfiguredHost(context.Request.Host);
        var patient = await RequirePatientAsync(context, cancellationToken);
        return await repository.GetForPatientAsync(_options.PracticeId, patient.CanonicalId, requestId, cancellationToken) ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthSyntheticAfterVisitPlanPreviewResponse> GetForApplicantAsync(HttpContext context, Guid applicantId, Guid requestId, string applicantAccessKey, CancellationToken cancellationToken)
    {
        RequireConfiguredHost(context.Request.Host);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey));
        return await repository.GetForApplicantAsync(_options.PracticeId, _options.FacilityId, applicantId, accessKeyHash, requestId, cancellationToken) ?? throw TelehealthProblem.NotFound();
    }

    private async Task<PatientPortalSessionResponse> RequirePatientAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = await patientIdentityAdapter.ResolveSessionIdAsync(context, cancellationToken);
        if (sessionId is null) throw TelehealthProblem.Unauthorized();
        var session = await patientPortalRepository.GetCurrentSessionAsync(sessionId.Value, cancellationToken);
        if (!session.Authenticated || string.IsNullOrWhiteSpace(session.CanonicalId)) throw TelehealthProblem.Unauthorized(session.FailureReason ?? "The patient session is not active.");
        return session;
    }

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
            throw new TelehealthProblem(StatusCodes.Status404NotFound, "telehealth_practice_not_found", "Telehealth practice was not found", "This host is not configured for the synthetic telehealth practice.");
    }
}
