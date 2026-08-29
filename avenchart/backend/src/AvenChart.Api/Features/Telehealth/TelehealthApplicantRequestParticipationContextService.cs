// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestParticipationContextService(
    TelehealthApplicantRequestParticipationContextRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestParticipationContextResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestParticipationContextResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantRequestParticipationContext request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestParticipationContextPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-participation-context-confirmation-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.SyntheticDataConfirmed,
            normalized.NpiNotCredentialAcknowledged,
            normalized.RealAuthorityNotVerifiedAcknowledged,
            normalized.ExactParticipationStillRequiredAcknowledged);
        return ToResponse(await repository.ConfirmAsync(
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

    private static TelehealthApplicantRequestParticipationContextResponse ToResponse(
        TelehealthApplicantRequestParticipationContextRecord result)
    {
        var complete = result.ConfirmationId is not null;
        var context = result.ParticipationContext;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestParticipationContextPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestParticipationContextPolicy.PolicyVersion,
            CatalogKey: TelehealthApplicantRequestParticipationContextPolicy.CatalogKey,
            CatalogVersion: TelehealthApplicantRequestParticipationContextPolicy.CatalogVersion,
            ContextSnapshotFingerprint: result.ContextSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            EligibilityVerificationId: result.EligibilityVerificationId,
            PracticeNetworkVerificationId: result.PracticeNetworkVerificationId,
            CandidateSelectionId: result.CandidateSelectionId,
            CandidateDisplayName: result.CandidateDisplayName,
            MaskedProviderReference: $"Synthetic provider ••••{result.CandidateNpi[^4..]}",
            MaskedBillingProviderReference: "Synthetic billing provider ••••8800",
            AuthorityJurisdiction: context.StateCode,
            AuthorityKind: context.AuthorityKind,
            AuthorityContextStatus: context.AuthorityFixtureStatus,
            PractitionerRoleContextStatus: context.RoleFixtureStatus,
            OrganizationAffiliationContextStatus: context.AffiliationFixtureStatus,
            BillingContractContextStatus: context.ContractFixtureStatus,
            ServiceCategory: context.ServiceCategory,
            Modality: context.Modality,
            EffectiveFrom: context.EffectiveFrom,
            EffectiveThrough: context.EffectiveThrough,
            ContextPurpose: TelehealthApplicantRequestParticipationContextPolicy.ContextPurpose,
            ConfirmationReady: !complete,
            ConfirmationCompleted: complete,
            ConfirmationId: result.ConfirmationId,
            ConfirmedAt: result.ConfirmedAt,
            ParticipationEvaluationContextConfirmed: complete,
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
                ? "The synthetic prerequisite context is fixed for a future exact participation evaluation. No real authority, credential, participation, assignment, or coverage result exists."
                : "Review the server-owned synthetic prerequisite context and acknowledge that real authority, credentialing, and exact participation remain unverified.",
            Limitations:
            [
                "NON_PRODUCTION synthetic context only. No payer, provider directory, licensing board, credentialing source, clinician, or other external destination was contacted.",
                "An NPI is an identifier. It does not prove licensure, credentialing, participation, availability, or a care relationship.",
                "Synthetic authority, role, affiliation, and contract fixture states are prerequisites only; they are not real verification results.",
                "Exact billing-entity and rendering-provider participation, canonical coverage, financial routing, operational review, contact, doctor search, queue, appointment, encounter, consent, and care remain closed."
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
