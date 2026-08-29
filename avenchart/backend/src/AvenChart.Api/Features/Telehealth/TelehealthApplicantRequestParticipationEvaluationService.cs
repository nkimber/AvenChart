// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestParticipationEvaluationService(
    TelehealthApplicantRequestParticipationEvaluationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestParticipationEvaluationResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        return ToResponse(await repository.GetAsync(
            _options.PracticeId,
            _options.PracticeDisplayName,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken));
    }

    public async Task<TelehealthApplicantRequestParticipationEvaluationResponse> EvaluateAsync(
        HttpContext httpContext,
        Guid applicantId,
        EvaluateTelehealthApplicantRequestParticipation request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestParticipationEvaluationPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-participation-evaluation-command-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.EvaluationSnapshotFingerprint,
            normalized.SyntheticDataConfirmed,
            normalized.ExactTupleScopeAcknowledged,
            normalized.NoCoverageGuaranteeAcknowledged,
            normalized.RealVerificationStillRequiredAcknowledged);
        return ToResponse(await repository.EvaluateAsync(
            _options.PracticeId,
            _options.PracticeDisplayName,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken));
    }

    private static TelehealthApplicantRequestParticipationEvaluationResponse ToResponse(
        TelehealthApplicantRequestParticipationEvaluationRecord result)
    {
        var complete = result.EvaluationId is not null;
        var rule = result.Rule;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestParticipationEvaluationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestParticipationEvaluationPolicy.PolicyVersion,
            CatalogKey: TelehealthApplicantRequestParticipationEvaluationPolicy.CatalogKey,
            CatalogVersion: TelehealthApplicantRequestParticipationEvaluationPolicy.CatalogVersion,
            SourceMode: rule.SourceMode,
            CompatibilityTarget: rule.CompatibilityTarget,
            EvaluationScope: rule.EvaluationScope,
            EvaluationSnapshotFingerprint: result.EvaluationSnapshotFingerprint,
            ResultValidThrough: result.ResultValidThrough,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            DateOfService: result.DateOfService,
            EligibilityVerificationId: result.EligibilityVerificationId,
            PracticeNetworkVerificationId: result.PracticeNetworkVerificationId,
            CandidateSelectionId: result.CandidateSelectionId,
            ParticipationContextConfirmationId: result.ParticipationContextConfirmationId,
            CandidateDisplayName: result.CandidateDisplayName,
            MaskedProviderReference: $"Synthetic provider ••••{result.CandidateNpi[^4..]}",
            MaskedBillingProviderReference: "Synthetic billing provider ••••8800",
            ServiceCategory: rule.ServiceCategory,
            Modality: rule.Modality,
            EffectiveFrom: rule.EffectiveFrom,
            EffectiveThrough: rule.EffectiveThrough,
            EvaluationReady: !complete,
            EvaluationCompleted: complete,
            EvaluationId: result.EvaluationId,
            EvaluatedAt: result.EvaluatedAt,
            BusinessOutcome: complete ? rule.BusinessOutcome : null,
            SyntheticParticipationEvaluated: complete,
            SyntheticBillingEntityInNetwork: complete && rule.SyntheticBillingEntityInNetwork,
            SyntheticRenderingProviderInNetwork: complete && rule.SyntheticRenderingProviderInNetwork,
            SyntheticPlanNetworkMatched: complete && rule.SyntheticPlanNetworkMatched,
            SyntheticServiceLocationMatched: complete && rule.SyntheticServiceLocationMatched,
            SyntheticNewPatientsAccepted: complete && rule.SyntheticNewPatientsAccepted,
            SyntheticExactNetworkMatched: complete && rule.SyntheticExactNetworkMatched,
            RealStateAuthorityVerified: false,
            RealCredentialingVerified: false,
            RenderingPhysicianAssigned: false,
            RenderingPhysicianNetworkChecked: false,
            ExactNetworkConfirmed: false,
            CanonicalCoverageCreated: false,
            CoverageSelected: false,
            CoverageVerified: false,
            FinancialRouteCreated: false,
            OperationalReviewCreated: false,
            PracticeAccepted: false,
            PatientContacted: false,
            PatientCareQueueEntered: false,
            ClinicianQueueEntered: false,
            DoctorSearchStarted: false,
            QueuePositionAssigned: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            ConsentCreated: false,
            CareAuthorized: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: complete
                ? "The exact tuple matched the synthetic catalog. Real payer or directory participation, coverage, assignment, and care remain unverified."
                : "Review the exact synthetic tuple and request its non-production participation evaluation.",
            Limitations:
            [
                "NON_PRODUCTION synthetic evaluation only. No payer, provider directory, licensing board, credentialing source, clinician, or external destination was contacted.",
                "The result applies only to the fixed billing entity, rendering provider, network, location, service, modality, state, date, and new-patient tuple shown.",
                "An NPI is an identifier. It does not prove licensure, credentialing, participation, availability, or a care relationship.",
                "A synthetic catalog match is not a coverage, benefits, payment, price, appointment, or treatment guarantee.",
                "Real authority, credentialing, payer/directory participation, canonical coverage, operational review, contact, doctor search, queue, appointment, encounter, consent, and care remain closed."
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
