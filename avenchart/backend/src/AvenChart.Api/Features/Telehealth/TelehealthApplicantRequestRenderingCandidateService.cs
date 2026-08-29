// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestRenderingCandidateService(
    TelehealthApplicantRequestRenderingCandidateRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestRenderingCandidateResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestRenderingCandidateResponse> SelectAsync(
        HttpContext httpContext,
        Guid applicantId,
        SelectTelehealthApplicantRequestRenderingCandidate request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestRenderingCandidatePolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-rendering-candidate-selection-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.CandidateSnapshotFingerprint,
            normalized.SyntheticDataConfirmed,
            normalized.CandidateOnlyScopeAcknowledged,
            normalized.NoAssignmentAcknowledged,
            normalized.NetworkCheckStillRequiredAcknowledged);
        return ToResponse(await repository.SelectAsync(
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

    private static TelehealthApplicantRequestRenderingCandidateResponse ToResponse(
        TelehealthApplicantRequestRenderingCandidateRecord result)
    {
        var complete = result.SelectionId is not null;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestRenderingCandidatePolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestRenderingCandidatePolicy.PolicyVersion,
            CatalogKey: TelehealthApplicantRequestRenderingCandidatePolicy.CatalogKey,
            CatalogVersion: TelehealthApplicantRequestRenderingCandidatePolicy.CatalogVersion,
            CandidateSnapshotFingerprint: result.CandidateSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            EligibilityVerificationId: result.EligibilityVerificationId,
            PracticeNetworkVerificationId: result.PracticeNetworkVerificationId,
            PracticeNetworkBusinessOutcome: result.PracticeNetworkBusinessOutcome,
            PracticeNetworkCheckedAt: result.PracticeNetworkCheckedAt,
            PracticeNetworkExpiresAt: result.PracticeNetworkExpiresAt,
            CandidateDisplayName: result.CandidateDisplayName,
            MaskedProviderReference: $"Synthetic provider ••••{result.CandidateNpi[^4..]}",
            PractitionerReference: result.Candidate.PractitionerReference,
            StateAuthorityReference: result.Candidate.StateAuthorityReference,
            ServiceCategory: result.Candidate.ServiceCategory,
            Modality: result.Candidate.Modality,
            CandidatePurpose: TelehealthApplicantRequestRenderingCandidatePolicy.CandidatePurpose,
            SelectionReady: !complete,
            SelectionCompleted: complete,
            SelectionId: result.SelectionId,
            SelectedAt: result.SelectedAt,
            CandidateSelectedForNetworkEvaluation: complete,
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
                ? "This synthetic clinician is bound only as the candidate for a future exact participation check. No clinician is assigned and no network result exists."
                : "Review the server-owned synthetic candidate and acknowledge that exact participation, assignment, availability, and care remain pending.",
            Limitations:
            [
                "NON_PRODUCTION synthetic roster only. No payer, provider directory, credentialing source, clinician, or other external destination was contacted.",
                "This candidate is selected only for a later network evaluation. It is not a clinician assignment, availability promise, licensure decision, credentialing decision, or care relationship.",
                "Practice-level evidence does not prove this candidate participates in the exact plan. Exact billing-entity and rendering-physician participation remain unchecked.",
                "No canonical coverage, financial route, operational review, contact, doctor search, queue, appointment, encounter, consent, care, integration, or external action is created."
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
