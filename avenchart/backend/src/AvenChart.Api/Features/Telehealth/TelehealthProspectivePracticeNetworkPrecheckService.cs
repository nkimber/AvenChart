// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectivePracticeNetworkPrecheckService(
    TelehealthProspectivePracticeNetworkPrecheckRepository repository,
    SyntheticTelehealthProspectivePracticeNetworkCatalog catalog,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectivePracticeNetworkOptionsResponse> GetOptionsAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var result = await repository.GetOptionsAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToOptionsResponse(result);
    }

    public async Task<TelehealthProspectivePracticeNetworkPrecheckResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectivePracticeNetworkPrecheckRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = catalog.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-practice-network-precheck-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.PlanKey,
            request.SyntheticDataConfirmed);
        var result = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            fingerprint,
            cancellationToken);
        return ToResponse(result);
    }

    private static TelehealthProspectivePracticeNetworkOptionsResponse ToOptionsResponse(
        TelehealthProspectivePracticeNetworkOptionsRecord result) => new(
            result.ApplicantId,
            result.ApplicantVersion,
            result.ApplicantStatus,
            result.Catalog.AdapterMode,
            result.Catalog.CatalogKey,
            result.Catalog.CatalogVersion,
            result.Catalog.EffectiveFrom,
            result.Catalog.EffectiveThrough,
            result.Catalog.Plans.Select(plan => new TelehealthProspectivePracticeNetworkOptionResponse(
                plan.PlanKey,
                plan.PayerDisplayName,
                plan.ProductDisplayName,
                plan.PracticeNetworkStatus,
                plan.Meaning)).ToArray(),
            false,
            false,
            false,
            false,
            false,
            "Choose one fictional plan for a practice-level demonstration precheck. This does not verify you or a physician.",
            [
                "NON_PRODUCTION synthetic catalog; no payer, directory, clearinghouse, or X12 270/271 transaction was contacted.",
                "A practice-level fixture result is not member eligibility, benefits, exact network confirmation, coverage, or a payment guarantee.",
                "No member ID, group number, policy number, insurance card, physician, price, or payment information is collected."
            ]);

    private static TelehealthProspectivePracticeNetworkPrecheckResponse ToResponse(
        TelehealthProspectivePracticeNetworkPrecheckRecord result) => new(
            PrecheckId: result.PrecheckId,
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            PlanKey: result.Plan.PlanKey,
            PayerDisplayName: result.Plan.PayerDisplayName,
            ProductDisplayName: result.Plan.ProductDisplayName,
            PracticeNetworkStatus: result.Plan.PracticeNetworkStatus,
            AdapterMode: result.AdapterMode,
            CatalogKey: result.CatalogKey,
            CatalogVersion: result.CatalogVersion,
            CatalogEffectiveFrom: result.CatalogEffectiveFrom,
            CatalogEffectiveThrough: result.CatalogEffectiveThrough,
            RecordedAt: result.RecordedAt,
            MemberEligibilityChecked: false,
            MemberBenefitsChecked: false,
            RenderingPhysicianNetworkChecked: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
            IdentityProofed: false,
            CanonicalPatientCreated: false,
            ChartLinked: false,
            PortalAccountCreated: false,
            ProspectiveIntakeCompleted: false,
            ConsentCreated: false,
            PracticeAccepted: false,
            CoverageRecordCreated: false,
            EstimateCreated: false,
            FinancialAcknowledgmentCreated: false,
            RequestCreated: false,
            QueueEnabled: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareEnabled: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            CommunicationEnabled: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: "The synthetic practice-level plan precheck was recorded. Individual eligibility and exact practice-and-physician network verification remain required.",
            Limitations:
            [
                "This result comes only from a deterministic NON_PRODUCTION catalog and is not an insurer, directory, clearinghouse, or X12 271 response.",
                "No member eligibility, benefits, rendering-physician participation, exact network status, coverage, estimate, or payment guarantee was established.",
                "No identity proofing, patient, chart, portal, consent, request, appointment, queue, encounter, prescribing, billing, claim, communication, integration, or care capability was created."
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
