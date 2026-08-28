// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectiveSafetyTriageService(
    TelehealthProspectiveSafetyTriageRepository repository,
    ITelehealthTriageEvaluator evaluator,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectiveSafetyTriageResponse> EvaluateAsync(
        HttpContext httpContext,
        Guid applicantId,
        EvaluateTelehealthProspectiveSafetyTriageRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthProspectiveSafetyTriagePolicy.Normalize(
            request, _options.SupportedStates);
        var result = evaluator.Evaluate(new SyntheticTelehealthTriageAnswers(
            normalized.HasEmergencyWarning,
            normalized.SevereOrWorsening,
            normalized.RequiresHandsOnExam,
            normalized.Unsure));
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-safety-triage-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.CurrentLocationStateCode,
            normalized.HasEmergencyWarning,
            normalized.SevereOrWorsening,
            normalized.RequiresHandsOnExam,
            normalized.Unsure,
            result.ProtocolId,
            result.ProtocolVersion,
            result.ProtocolContentHash,
            request.SyntheticDataConfirmed);
        var evaluation = await repository.EvaluateAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            result,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(evaluation);
    }

    private static TelehealthProspectiveSafetyTriageResponse ToResponse(
        TelehealthProspectiveSafetyTriageRecord evaluation) => new(
            evaluation.EvaluationId,
            evaluation.ApplicantId,
            evaluation.ApplicantVersion,
            evaluation.ApplicantStatus,
            TelehealthProspectiveSafetyTriagePolicy.PublicDisposition(evaluation.Outcome),
            evaluation.CurrentLocationStateCode,
            evaluation.ProtocolKey,
            evaluation.ProtocolVersion,
            evaluation.EvaluatedAt,
            evaluation.Outcome == TelehealthTriageOutcome.TelehealthEligible,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            TelehealthProspectiveSafetyTriagePolicy.Direction(evaluation.Outcome),
            [
                "A deterministic synthetic universal screen evaluated only the four displayed answers.",
                "No clinician reviewed these answers and no diagnosis or treatment decision was made.",
                "No patient, chart, portal account, complete intake, coverage check, request, appointment, or queue entry was created."
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
