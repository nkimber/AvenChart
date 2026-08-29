// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestPracticeNetworkService(
    TelehealthApplicantRequestPracticeNetworkRepository repository,
    ITelehealthProspectivePracticeNetworkGateway gateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestPracticeNetworkResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestPracticeNetworkResponse> RunAsync(
        HttpContext httpContext,
        Guid applicantId,
        RunTelehealthApplicantRequestPracticeNetworkVerification request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestPracticeNetworkPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-practice-network-verification-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.NetworkSnapshotFingerprint,
            normalized.SyntheticDataConfirmed,
            normalized.PracticeOnlyScopeAcknowledged,
            normalized.NoGuaranteeAcknowledged);
        return ToResponse(await repository.RunAsync(
            _options.PracticeId,
            _options.PracticeDisplayName,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            normalized,
            semanticKey,
            commandFingerprint,
            ResolveNetworkAsync,
            cancellationToken));
    }

    private async ValueTask<TelehealthProspectivePracticeNetworkAdapterResult> ResolveNetworkAsync(
        TelehealthApplicantRequestPracticeNetworkCandidate candidate,
        CancellationToken cancellationToken)
    {
        var result = await gateway.CheckAsync(
            new(
                candidate.PracticeId,
                candidate.PracticeDisplayName,
                candidate.FacilityId,
                candidate.PlanKey,
                candidate.CurrentLocationStateCode,
                candidate.DateOfService,
                SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory,
                candidate.DatabaseNow.ToUniversalTime()),
            cancellationToken);
        RequireAdapterContract(result, candidate);
        return result;
    }

    private static void RequireAdapterContract(
        TelehealthProspectivePracticeNetworkAdapterResult result,
        TelehealthApplicantRequestPracticeNetworkCandidate candidate)
    {
        var checkedAt = candidate.DatabaseNow.ToUniversalTime();
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
            && IsValidOutcome(result, candidate.PlanKey);
        if (!valid)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_practice_network_adapter_contract_invalid",
                "The bounded synthetic practice-network adapter returned an invalid result.");
        }
    }

    private static bool IsValidOutcome(
        TelehealthProspectivePracticeNetworkAdapterResult result,
        string planKey) =>
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
                NetworkReference: "syn-network-harbor-mutual-hd",
                OrganizationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.OrganizationReference,
                LocationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.LocationReference,
                ServiceReference: SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceReference
            } && planKey == "harbor-mutual-hd",
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
                NetworkReference: "syn-network-pine-state-choice",
                OrganizationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.OrganizationReference,
                LocationReference: SyntheticTelehealthProspectivePracticeNetworkGateway.LocationReference,
                ServiceReference: SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceReference
            } && planKey == "pine-state-choice",
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
            } && planKey == "blue-valley-standard",
            _ => false
        };

    private static TelehealthApplicantRequestPracticeNetworkResponse ToResponse(
        TelehealthApplicantRequestPracticeNetworkRecord result)
    {
        var adapter = result.AdapterResult;
        var complete = result.VerificationId is not null;
        var evidenceExpiresAt = adapter is null
            ? result.ContextExpiresAt
            : adapter.ExpiresAt < result.ContextExpiresAt
                ? adapter.ExpiresAt
                : result.ContextExpiresAt;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            PolicyKey: TelehealthApplicantRequestPracticeNetworkPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestPracticeNetworkPolicy.PolicyVersion,
            NetworkSnapshotFingerprint: result.NetworkSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            PracticeDisplayName: result.PracticeDisplayName,
            PayerDisplayName: result.PayerDisplayName,
            ProductDisplayName: result.ProductDisplayName,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            PurposeCategory: result.PurposeCategory,
            EligibilityVerificationId: result.EligibilityVerificationId,
            EligibilityBusinessOutcome: result.EligibilityBusinessOutcome,
            EligibilityCheckedAt: result.EligibilityCheckedAt,
            EligibilityExpiresAt: result.EligibilityExpiresAt,
            VerificationReady: !complete,
            VerificationCompleted: complete,
            VerificationId: result.VerificationId,
            DateOfService: result.DateOfService,
            ServiceCategory: complete
                ? SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory
                : null,
            AdapterMode: adapter?.AdapterMode,
            CompatibilityTarget: adapter?.CompatibilityTarget,
            DatasetKey: adapter?.DatasetKey,
            DatasetVersion: adapter?.DatasetVersion,
            TransportOutcome: adapter?.TransportOutcome,
            PlanNetworkMatchStatus: adapter?.PlanNetworkMatchStatus,
            PracticeAffiliationStatus: adapter?.PracticeAffiliationStatus,
            ServiceAvailabilityStatus: adapter?.ServiceAvailabilityStatus,
            NewPatientAcceptanceStatus: adapter?.NewPatientAcceptanceStatus,
            BusinessOutcome: adapter?.BusinessOutcome,
            PracticeNetworkChecked: adapter?.PracticeNetworkChecked ?? false,
            PracticeInNetwork: adapter?.PracticeInNetwork ?? false,
            NewPatientsAccepted: adapter?.NewPatientsAccepted ?? false,
            CheckedAt: adapter?.CheckedAt,
            ExpiresAt: adapter?.ExpiresAt,
            EvidenceExpiresAt: complete ? evidenceExpiresAt : null,
            CurrentEligibilityEvidenceReusedAsContext: true,
            PracticeNetworkVerificationCreated: complete,
            RenderingPhysicianSelected: false,
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
            Direction: DirectionFor(adapter?.BusinessOutcome),
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. No payer, provider directory, clearinghouse, or other external destination was contacted.",
                "This result concerns only the configured practice, facility, plan fixture, service category, state, and date. No rendering physician has been selected or checked.",
                "Practice-level network evidence is not exact network confirmation and never guarantees coverage, payment, patient responsibility, or physician participation.",
                "No canonical coverage, selection, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action is created."
            ]);
    }

    private static string DirectionFor(string? businessOutcome) => businessOutcome switch
    {
        null => "Run the fresh synthetic practice-network check. Rendering-physician participation and every downstream gate will remain pending.",
        "PracticeInNetworkAcceptingNewPatients" => "The practice-level fixture is in network and accepting new patients. Rendering-physician network participation and later gates remain required.",
        "PracticeOutOfNetwork" => "The practice-level fixture is out of network. Do not infer coverage or advance this request without a separately authorized financial route.",
        _ => "The synthetic adapter could not determine practice-level network participation. Manual review or correction is required before any later gate."
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
