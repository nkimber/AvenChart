// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveVisitPurposeService(
    TelehealthProspectiveVisitPurposeRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveVisitPurposeResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectiveVisitPurposeRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var purpose = TelehealthProspectiveVisitPurposePolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-visit-purpose-v1",
            applicantId,
            purpose.ExpectedVersion,
            purpose.PurposeCategory,
            request.SyntheticDataConfirmed);
        var recorded = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            purpose,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(recorded);
    }

    private static TelehealthProspectiveVisitPurposeResponse ToResponse(
        TelehealthProspectiveVisitPurposeRecord purpose) => new(
            purpose.PurposeId,
            purpose.ApplicantId,
            purpose.ApplicantVersion,
            purpose.ApplicantStatus,
            purpose.PurposeCategory,
            purpose.PurposeDisplayLabel,
            purpose.RecordedAt,
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
            "The synthetic visit purpose was classified for a later intake step. No complaint-specific clinical decision or request for care was created.",
            [
                "Migraine and sleep are demonstration navigation categories, not diagnoses or approved clinical protocols.",
                "No clinician reviewed this selection and no telehealth eligibility or treatment decision was made.",
                "No patient, chart, portal account, complete intake, coverage check, request, appointment, queue entry, or care capability was created."
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
