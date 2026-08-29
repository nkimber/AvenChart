// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestUniversalSafetyService(
    TelehealthApplicantRequestUniversalSafetyRepository repository,
    ITelehealthTriageEvaluator evaluator,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestUniversalSafetyResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestUniversalSafetyResponse> AssessAsync(
        HttpContext httpContext,
        Guid applicantId,
        EvaluateTelehealthApplicantRequestUniversalSafety request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestUniversalSafetyPolicy.Normalize(request);
        var result = evaluator.Evaluate(new SyntheticTelehealthTriageAnswers(
            normalized.HasEmergencyWarning,
            normalized.SevereOrWorsening,
            normalized.RequiresHandsOnExam,
            normalized.Unsure));
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-universal-safety-assessment-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.CurrentLocationStateCode,
            normalized.CurrentLocationConfirmed,
            normalized.CallbackNumberConfirmed,
            normalized.SyntheticDataConfirmed,
            normalized.HasEmergencyWarning,
            normalized.SevereOrWorsening,
            normalized.RequiresHandsOnExam,
            normalized.Unsure,
            result.ProtocolId,
            result.ProtocolVersion,
            result.ProtocolContentHash);
        var assessment = await repository.AssessAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            result,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(assessment);
    }

    private static TelehealthApplicantRequestUniversalSafetyResponse ToResponse(
        TelehealthApplicantRequestUniversalSafetyRecord result)
    {
        var outcome = result.Outcome;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestUniversalSafetyPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestUniversalSafetyPolicy.PolicyVersion,
            ProtocolKey: SyntheticTelehealthTriageEvaluator.ProtocolKey,
            ProtocolVersion: SyntheticTelehealthTriageEvaluator.ProtocolVersion,
            ContextSnapshotFingerprint: result.ContextSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            MaskedCallbackPhone: $"***-***-{result.CallbackPhoneLast4}",
            AssessmentReady: result.AssessmentId is null,
            AssessmentCreated: result.AssessmentId is not null,
            Outcome: outcome?.ToString(),
            PublicDisposition: outcome is null
                ? null
                : TelehealthApplicantRequestUniversalSafetyPolicy.PublicDisposition(outcome.Value),
            EvaluatedAt: result.EvaluatedAt,
            UniversalSafetyPassed: outcome is not null
                && TelehealthApplicantRequestUniversalSafetyPolicy.UniversalSafetyPassed(outcome.Value),
            ComplaintSpecificTriageRequired: outcome is not null
                && TelehealthApplicantRequestUniversalSafetyPolicy
                    .ComplaintSpecificTriageRequired(outcome.Value),
            ComplaintSpecificTriageCreated: false,
            ClinicalReviewRequired: outcome is not null
                && TelehealthApplicantRequestUniversalSafetyPolicy.ClinicalReviewRequired(outcome.Value),
            ClinicalReviewCreated: false,
            TerminalForTelehealth: outcome is not null
                && TelehealthApplicantRequestUniversalSafetyPolicy.TerminalForTelehealth(outcome.Value),
            PatientContacted: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            DoctorSearchStarted: false,
            QueuePositionAssigned: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: outcome is null
                ? "Answer every universal safety question using synthetic demonstration data. Call 911 now for any emergency; this application cannot contact emergency services."
                : TelehealthApplicantRequestUniversalSafetyPolicy.Direction(outcome.Value),
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. This deterministic four-answer fixture is not approved clinical content and must not be used for real patient care.",
                "The response never returns the submitted safety answers or their answer fingerprint.",
                "Passing this universal screen is not clinical eligibility. Complaint-specific triage and any required clinical review remain separate future steps.",
                "This step creates no clinical-review work item, contact, doctor search, queue, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action."
            ]);
    }

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
