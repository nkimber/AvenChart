// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthProspectivePracticeNetworkService(
    TelehealthProspectivePracticeNetworkRepository repository,
    ITelehealthProspectivePracticeNetworkGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthProspectivePracticeNetworkResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthProspectivePracticeNetworkRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_version_invalid",
                "Expected applicant version must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_practice_network_synthetic_acknowledgment_required",
                "Confirm that this is a NON_PRODUCTION synthetic practice-network check.");
        }

        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-synthetic-practice-network-v1",
            applicantId,
            request.ExpectedVersion,
            request.SyntheticDataConfirmed);
        var result = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            _options.PracticeDisplayName,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            request.ExpectedVersion,
            semanticKey,
            fingerprint,
            ResolveNetworkAsync,
            cancellationToken);
        return ToResponse(result);
    }

    private async ValueTask<TelehealthProspectivePracticeNetworkAdapterResult> ResolveNetworkAsync(
        TelehealthProspectivePracticeNetworkCandidate candidate,
        CancellationToken cancellationToken)
    {
        var checkedAt = candidate.DatabaseNow.ToUniversalTime();
        var result = await gateway.CheckAsync(
            new(
                _options.PracticeId,
                _options.PracticeDisplayName,
                _options.FacilityId,
                candidate.PlanKey,
                candidate.CurrentLocationStateCode,
                DateOnly.FromDateTime(checkedAt.UtcDateTime),
                SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory,
                checkedAt),
            cancellationToken);
        RequireAdapterContract(result, checkedAt);
        return result;
    }

    private static void RequireAdapterContract(
        TelehealthProspectivePracticeNetworkAdapterResult result,
        DateTimeOffset checkedAt)
    {
        var valid = result.AdapterMode == SyntheticTelehealthProspectivePracticeNetworkGateway.AdapterMode
            && result.CompatibilityTarget == SyntheticTelehealthProspectivePracticeNetworkGateway.CompatibilityTarget
            && result.DatasetKey == SyntheticTelehealthProspectivePracticeNetworkGateway.DatasetKey
            && result.DatasetVersion == SyntheticTelehealthProspectivePracticeNetworkGateway.DatasetVersion
            && result.DatasetEffectiveFrom == SyntheticTelehealthProspectivePracticeNetworkGateway.DatasetEffectiveFrom
            && result.DatasetEffectiveThrough == SyntheticTelehealthProspectivePracticeNetworkGateway.DatasetEffectiveThrough
            && result.SourceLastUpdatedAt == SyntheticTelehealthProspectivePracticeNetworkGateway.SourceLastUpdatedAt
            && result.RequestTraceToken != Guid.Empty
            && result.ResponseTraceToken != Guid.Empty
            && result.RequestTraceToken != result.ResponseTraceToken
            && result.CheckedAt == checkedAt
            && result.ExpiresAt == checkedAt.AddMinutes(15)
            && IsValidOutcome(result);
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_network_adapter_contract_invalid",
                "The bounded synthetic practice-network adapter returned an invalid result.");
        }
    }

    private static bool IsValidOutcome(TelehealthProspectivePracticeNetworkAdapterResult result) =>
        result.BusinessOutcome switch
        {
            "PracticeInNetworkAcceptingNewPatients" => result is
            {
                TransportOutcome: "SimulatedAvailable",
                PlanNetworkMatchStatus: "Matched",
                PracticeAffiliationStatus: "InNetwork",
                ServiceAvailabilityStatus: "Included",
                NewPatientAcceptanceStatus: "Accepting",
                PracticeNetworkChecked: true,
                PracticeInNetwork: true,
                NewPatientsAccepted: true,
                NetworkReference: not null,
                OrganizationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.OrganizationReference,
                LocationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.LocationReference,
                ServiceReference: SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceReference
            },
            "PracticeOutOfNetwork" => result is
            {
                TransportOutcome: "SimulatedAvailable",
                PlanNetworkMatchStatus: "Matched",
                PracticeAffiliationStatus: "OutOfNetwork",
                ServiceAvailabilityStatus: "Excluded",
                NewPatientAcceptanceStatus: "Unknown",
                PracticeNetworkChecked: true,
                PracticeInNetwork: false,
                NewPatientsAccepted: false,
                NetworkReference: not null,
                OrganizationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.OrganizationReference,
                LocationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.LocationReference,
                ServiceReference: SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceReference
            },
            "UnableToDetermine" => result is
            {
                TransportOutcome: "SimulatedUnavailable",
                PlanNetworkMatchStatus: "Unknown",
                PracticeAffiliationStatus: "Unknown",
                ServiceAvailabilityStatus: "Unknown",
                NewPatientAcceptanceStatus: "Unknown",
                PracticeNetworkChecked: false,
                PracticeInNetwork: false,
                NewPatientsAccepted: false,
                NetworkReference: null,
                OrganizationReference: null,
                LocationReference: null,
                ServiceReference: null
            },
            _ => false
        };

    private static TelehealthProspectivePracticeNetworkResponse ToResponse(
        TelehealthProspectivePracticeNetworkRecord result)
    {
        var adapter = result.AdapterResult;
        return new(
            NetworkDeterminationId: result.NetworkDeterminationId,
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            PlanKey: result.PlanKey,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            PracticeDisplayName: result.PracticeDisplayName,
            DateOfService: result.DateOfService,
            ServiceCategory: result.ServiceCategory,
            EligibilityStatus: result.EligibilityStatus,
            BenefitInformationStatus: result.BenefitInformationStatus,
            EligibilityBusinessOutcome: result.EligibilityBusinessOutcome,
            EligibilityCheckedAt: result.EligibilityCheckedAt,
            EligibilityExpiresAt: result.EligibilityExpiresAt,
            AdapterMode: adapter.AdapterMode,
            CompatibilityTarget: adapter.CompatibilityTarget,
            DatasetKey: adapter.DatasetKey,
            DatasetVersion: adapter.DatasetVersion,
            DatasetEffectiveFrom: adapter.DatasetEffectiveFrom,
            DatasetEffectiveThrough: adapter.DatasetEffectiveThrough,
            SourceLastUpdatedAt: adapter.SourceLastUpdatedAt,
            RequestTraceToken: adapter.RequestTraceToken,
            ResponseTraceToken: adapter.ResponseTraceToken,
            TransportOutcome: adapter.TransportOutcome,
            PlanNetworkMatchStatus: adapter.PlanNetworkMatchStatus,
            PracticeAffiliationStatus: adapter.PracticeAffiliationStatus,
            ServiceAvailabilityStatus: adapter.ServiceAvailabilityStatus,
            NewPatientAcceptanceStatus: adapter.NewPatientAcceptanceStatus,
            BusinessOutcome: adapter.BusinessOutcome,
            PracticeNetworkChecked: adapter.PracticeNetworkChecked,
            PracticeInNetwork: adapter.PracticeInNetwork,
            NewPatientsAccepted: adapter.NewPatientsAccepted,
            NetworkReference: adapter.NetworkReference,
            OrganizationReference: adapter.OrganizationReference,
            LocationReference: adapter.LocationReference,
            ServiceReference: adapter.ServiceReference,
            CheckedAt: adapter.CheckedAt,
            ExpiresAt: adapter.ExpiresAt,
            RecordedAt: result.RecordedAt,
            FhirResourceCreated: false,
            LiveDirectoryQueried: false,
            RenderingPhysicianNetworkChecked: false,
            ExactNetworkConfirmed: false,
            CoverageVerified: false,
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
            Direction: DirectionFor(adapter.BusinessOutcome),
            Limitations:
            [
                "NON_PRODUCTION fixture only. No payer, provider-directory, clearinghouse, FHIR endpoint, or external service was contacted, and no FHIR resource or bundle was created.",
                "The result applies only to the configured synthetic practice, facility, plan, state, date, and professional telehealth service. Rendering-physician participation remains unchecked.",
                "Practice network metadata is separate from member eligibility and never guarantees coverage, payment, price, capacity, appointment availability, practice acceptance, or care."
            ]);
    }

    private static string DirectionFor(string businessOutcome) => businessOutcome switch
    {
        "PracticeInNetworkAcceptingNewPatients" =>
            "The synthetic directory fixture reports this practice, facility, and service as in network and accepting new patients. Rendering-physician participation and every later intake gate remain required.",
        "PracticeOutOfNetwork" =>
            "The synthetic directory fixture reports the practice as out of network for this plan and service. No self-pay choice, estimate, request, or care action was created.",
        _ =>
            "The synthetic directory adapter could not determine practice participation. Do not infer network status, coverage, payment, availability, or acceptance."
    };

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
