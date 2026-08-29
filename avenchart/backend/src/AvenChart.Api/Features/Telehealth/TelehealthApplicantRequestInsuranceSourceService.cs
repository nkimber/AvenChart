// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestInsuranceSourceService(
    TelehealthApplicantRequestInsuranceSourceRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestInsuranceSourceResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestInsuranceSourceResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantRequestInsuranceSource request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestInsuranceSourcePolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-insurance-source-confirmation-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.InsuranceSourceSnapshotFingerprint,
            normalized.PayerProductConfirmed,
            normalized.MaskedMemberDetailsConfirmed,
            normalized.SubscriberRelationshipConfirmed,
            normalized.PrimaryCoverageSourceConfirmed,
            normalized.FreshVerificationRequested,
            normalized.EvidenceLimitationsAcknowledged,
            normalized.SyntheticDataConfirmed);
        return ToResponse(await repository.ConfirmAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken));
    }

    private static TelehealthApplicantRequestInsuranceSourceResponse ToResponse(
        TelehealthApplicantRequestInsuranceSourceRecord result)
    {
        var confirmed = result.ConfirmationId is not null;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestInsuranceSourcePolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestInsuranceSourcePolicy.PolicyVersion,
            InsuranceSourceSnapshotFingerprint: result.InsuranceSourceSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            MaskedMemberId: $"••••{result.MemberIdLast4}",
            MaskedGroupNumber: result.GroupNumberLast4 is null ? null : $"••••{result.GroupNumberLast4}",
            SubscriberRelationship: result.SubscriberRelationship,
            CoveragePriority: result.CoveragePriority,
            PreviousEligibilityBusinessOutcome: result.PreviousEligibilityBusinessOutcome,
            PreviousEligibilityCheckedAt: result.PreviousEligibilityCheckedAt,
            PreviousEligibilityExpiresAt: result.PreviousEligibilityExpiresAt,
            PreviousEligibilityEvidenceExpired: result.PreviousEligibilityExpiresAt <= result.DatabaseNow,
            PreviousPracticeNetworkBusinessOutcome: result.PreviousPracticeNetworkBusinessOutcome,
            PreviousPracticeNetworkCheckedAt: result.PreviousPracticeNetworkCheckedAt,
            PreviousPracticeNetworkExpiresAt: result.PreviousPracticeNetworkExpiresAt,
            PreviousPracticeNetworkEvidenceExpired: result.PreviousPracticeNetworkExpiresAt <= result.DatabaseNow,
            PreviousRenderingPhysicianNetworkChecked: false,
            PreviousResultReusable: false,
            SourceReady: !confirmed,
            SourceConfirmed: confirmed,
            ConfirmedAt: result.ConfirmedAt,
            ProtectedPayloadReferenced: true,
            ProtectedPayloadCopied: false,
            ProtectedPayloadDecrypted: false,
            FreshVerificationRequested: confirmed,
            CanonicalCoverageCreated: false,
            CoverageSelected: false,
            EligibilityVerificationCreated: false,
            NetworkVerificationCreated: false,
            RenderingPhysicianNetworkChecked: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
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
            Direction: confirmed
                ? "The insurance source is confirmed for this synthetic request. Fresh eligibility, practice-network, and rendering-physician verification are still pending and unavailable."
                : "Review the masked source details and historical evidence, then explicitly request a future fresh verification step.",
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. No real person or protected health information may be used.",
                "The payer, product, masked member details, subscriber relationship, and primary designation come from the earlier applicant insurance handoff; the protected member payload is referenced but never copied or decrypted here.",
                "Prior eligibility and practice-network results are historical context only. They are not reused and do not establish current eligibility, benefits, exact network status, or rendering-physician participation.",
                "Requesting fresh verification records intent only. This endpoint performs no payer, clearinghouse, pharmacy, network, or other external call.",
                "No canonical coverage, coverage selection, eligibility or network verification, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action is created."
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
