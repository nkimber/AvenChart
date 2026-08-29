// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestOperationalReviewSubmissionService(
    TelehealthApplicantRequestOperationalReviewSubmissionRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestOperationalReviewSubmissionResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestOperationalReviewSubmissionResponse> SubmitAsync(
        HttpContext httpContext,
        Guid applicantId,
        SubmitTelehealthApplicantRequestForOperationalReview request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestOperationalReviewSubmissionPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-operational-review-submission-command-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.SubmissionSnapshotFingerprint,
            normalized.SyntheticEvidenceAcknowledged,
            normalized.NoCoverageGuaranteeAcknowledged,
            normalized.PracticeReviewPendingAcknowledged,
            normalized.NoCareRelationshipAcknowledged);
        return ToResponse(await repository.SubmitAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken));
    }

    private static TelehealthApplicantRequestOperationalReviewSubmissionResponse ToResponse(
        TelehealthApplicantRequestOperationalReviewSubmissionRecord result)
    {
        var complete = result.SubmittedAt is not null;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestOperationalReviewSubmissionPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestOperationalReviewSubmissionPolicy.PolicyVersion,
            SourceMode: TelehealthApplicantRequestOperationalReviewSubmissionPolicy.SourceMode,
            CompatibilityTarget: TelehealthApplicantRequestOperationalReviewSubmissionPolicy.CompatibilityTarget,
            SubmissionSnapshotFingerprint: result.SubmissionSnapshotFingerprint,
            ResultValidThrough: result.ResultValidThrough,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            DateOfService: result.DateOfService,
            CandidateDisplayName: result.CandidateDisplayName,
            MaskedProviderReference: $"Synthetic provider ••••{result.CandidateNpiLast4}",
            MaskedBillingProviderReference: "Synthetic billing provider ••••8800",
            ServiceCategory: result.ServiceCategory,
            Modality: result.Modality,
            SubmissionReady: !complete,
            SubmissionCompleted: complete,
            SubmittedAt: result.SubmittedAt,
            BusinessOutcome: complete ? TelehealthApplicantRequestOperationalReviewSubmissionPolicy.BusinessOutcome : null,
            SyntheticAutomatedChecksComplete: complete,
            OperationalReviewCreated: complete,
            RealStateAuthorityVerified: false,
            RealCredentialingVerified: false,
            RenderingPhysicianAssigned: false,
            RenderingPhysicianNetworkChecked: false,
            ExactNetworkConfirmed: false,
            CanonicalCoverageCreated: false,
            CoverageSelected: false,
            CoverageVerified: false,
            FinancialRouteCreated: false,
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
                ? "Submitted for practice review. The practice has not accepted the request, and insurance or payment is not guaranteed."
                : "Review the bounded synthetic evidence and submit the request for practice operational review.",
            Limitations: [
                "NON_PRODUCTION synthetic evidence only. No payer, provider directory, licensing board, credentialing source, clinician, or external destination was contacted.",
                "Operational review means only that practice staff may review the request; it is not acceptance, coverage verification, or authorization for care.",
                "Insurance coverage, benefits, payment, price, clinician availability, and treatment are not guaranteed.",
                "No care relationship, contact, queue position, appointment, encounter, consent, prescription, claim, or external action was created."
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
