// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestComplaintTriageService(
    TelehealthApplicantRequestComplaintTriageRepository repository,
    ISyntheticTelehealthComplaintTriageEvaluator evaluator,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestComplaintTriageResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        return ToResponse(await repository.GetAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken));
    }

    public async Task<TelehealthApplicantRequestComplaintTriageResponse> AssessAsync(
        HttpContext httpContext,
        Guid applicantId,
        EvaluateTelehealthApplicantRequestComplaintTriage request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestComplaintTriagePolicy.Normalize(request);
        var result = evaluator.Evaluate(normalized.Answers);
        TelehealthApplicantRequestComplaintTriagePolicy.RequirePublicationBlocked(result);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-complaint-triage-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.CurrentLocationStateCode,
            normalized.CurrentLocationConfirmed,
            normalized.CallbackNumberConfirmed,
            normalized.SyntheticDataConfirmed,
            normalized.Answers.ComplaintCategory,
            result.AnswerFingerprint,
            result.ProtocolId,
            result.ProtocolVersion,
            result.ProtocolContentHash,
            result.EngineVersion,
            result.ClinicalContentStatus);
        return ToResponse(await repository.AssessAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            result,
            semanticKey,
            commandFingerprint,
            cancellationToken));
    }

    private static TelehealthApplicantRequestComplaintTriageResponse ToResponse(
        TelehealthApplicantRequestComplaintTriageRecord result)
    {
        var outcome = result.Outcome;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            ComplaintCategory: result.ComplaintCategory,
            PolicyKey: TelehealthApplicantRequestComplaintTriagePolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestComplaintTriagePolicy.PolicyVersion,
            ProtocolKey: result.ProtocolKey,
            ProtocolVersion: SyntheticTelehealthComplaintTriageEvaluator.ProtocolVersion,
            EngineVersion: SyntheticTelehealthComplaintTriageEvaluator.EngineVersion,
            ClinicalContentStatus: TelehealthApplicantRequestComplaintTriagePolicy.ClinicalContentStatus,
            MedicalDirectorApprovalRequired: true,
            MedicalDirectorApprovalRecorded: false,
            ClinicalGoldenCasePackApproved: false,
            ProductionPublicationAllowed: false,
            ContextSnapshotFingerprint: result.ContextSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            MaskedCallbackPhone: $"***-***-{result.CallbackPhoneLast4}",
            AssessmentReady: result.AssessmentId is null,
            AssessmentCreated: result.AssessmentId is not null,
            Outcome: outcome?.ToString(),
            PublicDisposition: outcome is null
                ? null
                : TelehealthApplicantRequestComplaintTriagePolicy.PublicDisposition(outcome.Value),
            EvaluatedAt: result.EvaluatedAt,
            SyntheticVideoEvaluationCandidate: outcome is not null
                && TelehealthApplicantRequestComplaintTriagePolicy
                    .SyntheticVideoEvaluationCandidate(outcome.Value),
            ClinicalReviewRequired: outcome is not null
                && TelehealthApplicantRequestComplaintTriagePolicy.ClinicalReviewRequired(outcome.Value),
            ClinicalReviewCreated: false,
            TerminalForTelehealth: outcome is not null
                && TelehealthApplicantRequestComplaintTriagePolicy.TerminalForTelehealth(outcome.Value),
            IntakeSnapshotCreated: false,
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
                ? "Answer every complaint-specific question using synthetic demonstration data. Call 911 now for an emergency; call or text 988 for a mental-health or suicide crisis."
                : TelehealthApplicantRequestComplaintTriagePolicy.Direction(
                    result.ComplaintCategory,
                    outcome.Value),
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. This complaint fixture is unapproved clinical content and must not be used for real patient care.",
                "A named medical director, approved golden-case pack, independent under-triage review, and exact publication decision are required before production use.",
                "The response never returns submitted answers, the answer fingerprint, fired rules, or reason codes.",
                "A synthetic eligible result only demonstrates the Intake workflow state. It is not diagnosis, practice acceptance, care authorization, or guaranteed treatment.",
                "This step creates no clinical-review work item, intake snapshot, contact, doctor search, queue, appointment, encounter, consent, media, care, prescribing, financial, integration, or external action."
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
