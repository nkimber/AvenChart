// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestCreationService(
    TelehealthApplicantRequestCreationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestCreationResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var result = await repository.GetAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(result);
    }

    public async Task<TelehealthApplicantRequestCreationResponse> CreateAsync(
        HttpContext httpContext,
        Guid applicantId,
        CreateTelehealthApplicantRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestCreationPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-telehealth-request-creation-v1",
            applicantId,
            normalized.ExpectedApplicantVersion,
            normalized.AuthorizationPolicyVersion,
            normalized.RequestCreationConfirmed,
            normalized.NoQueueOrCareAcknowledged,
            normalized.UrgentOrWorseningSymptomsRequireImmediateActionAcknowledged);
        var result = await repository.CreateAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(result);
    }

    private static TelehealthApplicantRequestCreationResponse ToResponse(
        TelehealthApplicantRequestCreationRecord result) => new(
            result.ApplicantId,
            result.ApplicantVersion,
            result.ApplicantStatus,
            TelehealthApplicantRequestCreationPolicy.PolicyKey,
            TelehealthApplicantRequestCreationPolicy.PolicyVersion,
            TelehealthApplicantRequestCreationPolicy.AuthorizationPolicyVersion,
            result.RequestId is null,
            result.RequestId is not null,
            result.RequestId,
            result.RequestStatus,
            result.RequestVersion,
            result.ComplaintCategory,
            result.CreatedAt,
            result.RequestId is not null,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            result.RequestId is null
                ? "The practice authorized this synthetic applicant to create one Draft telehealth request. Confirm the boundaries to continue."
                : "The Draft request was created. It is not searching for a doctor and is not in a patient or clinician care queue.",
            [
                "Synthetic demonstration only. The request category is derived from the prior controlled visit-purpose receipt; no new complaint or clinical detail is collected here.",
                "Draft does not mean accepted, clinically eligible, covered, queued, assigned, scheduled, or under clinician review.",
                "No contact, doctor search, queue position, appointment, encounter, consent, media, care, prescribing, billing, claim, integration, or external action was created."
            ]);

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new TelehealthProblem(
                StatusCodes.Status404NotFound,
                "telehealth_practice_not_found",
                "Telehealth practice was not found",
                "This host is not configured for the synthetic telehealth practice.");
        }
    }
}
