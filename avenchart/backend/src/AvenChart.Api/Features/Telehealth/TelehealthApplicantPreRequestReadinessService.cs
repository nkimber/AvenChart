// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantPreRequestReadinessService(
    TelehealthApplicantPreRequestReadinessRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantPreRequestReadinessResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        return ToResponse(
            context,
            TelehealthApplicantPreRequestReadinessRepository.Snapshot(context),
            context.ReadinessAcknowledgmentId is not null,
            context.AcknowledgedAt,
            context.OverallRoute ?? TelehealthApplicantPreRequestReadinessRepository.OverallRoute(context));
    }

    public async Task<TelehealthApplicantPreRequestReadinessResponse> AcknowledgeAsync(
        HttpContext httpContext,
        Guid applicantId,
        AcknowledgeTelehealthApplicantPreRequestReadinessRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var context = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            cancellationToken);
        var snapshot = TelehealthApplicantPreRequestReadinessRepository.Snapshot(context);
        var normalized = TelehealthApplicantPreRequestReadinessPolicy.Normalize(request);
        var overallRoute = TelehealthApplicantPreRequestReadinessRepository.OverallRoute(context);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-pre-request-readiness-acknowledgment-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.PreRequestReadinessSnapshotFingerprint,
            overallRoute,
            normalized.PriorSectionsReviewedAcknowledged,
            normalized.OutstandingStepsRemainAcknowledged,
            normalized.NoRequestOrQueueCreatedAcknowledged,
            normalized.CorrectionRequiresSeparateWorkflowAcknowledged);
        var acknowledged = await repository.AcknowledgeAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(
            context with
            {
                ApplicantVersion = acknowledged.ApplicantVersion,
                ApplicantStatus = acknowledged.ApplicantStatus
            },
            snapshot,
            acknowledged: true,
            acknowledged.AcknowledgedAt,
            acknowledged.OverallRoute);
    }

    private static TelehealthApplicantPreRequestReadinessResponse ToResponse(
        TelehealthApplicantPreRequestReadinessContext context,
        TelehealthApplicantPreRequestReadinessSnapshot snapshot,
        bool acknowledged,
        DateTimeOffset? acknowledgedAt,
        string overallRoute) => new(
            ApplicantId: context.ApplicantId,
            ApplicantVersion: context.ApplicantVersion,
            ApplicantStatus: context.ApplicantStatus,
            PreRequestReadinessSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantPreRequestReadinessPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantPreRequestReadinessPolicy.PolicyVersion,
            Sections:
            [
                new("Registration", "ReceiptRecorded", "IdentityAndIntakeCompletionStillRequired"),
                new("Insurance", "ReceiptRecorded", "CoverageAndRenderingClinicianVerificationStillRequired"),
                new(
                    "CommunicationAccess",
                    "PreferencesRecorded",
                    TelehealthApplicantPreRequestReadinessPolicy.CommunicationRoute(
                        context.InterpreterRequested!.Value,
                        context.AccessibilitySupportRequested!.Value)),
                new("DevicePreparation", "ClientReportedPreparationRecorded", "PreConsultationTechnologyRecheckRequired"),
                new("ClinicalInformation", "PatientReportedSummaryConfirmed", context.ClinicalInformationSummaryRoute!)
            ],
            OverallRoute: overallRoute,
            ReadinessAcknowledged: acknowledged,
            AcknowledgedAt: acknowledgedAt,
            PriorSectionsReviewedAcknowledged: acknowledged,
            OutstandingStepsRemainAcknowledged: acknowledged,
            NoRequestOrQueueCreatedAcknowledged: acknowledged,
            CorrectionRequiresSeparateWorkflowAcknowledged: acknowledged,
            IdentityAssuranceEstablished: false,
            CoverageGuaranteed: false,
            RenderingClinicianNetworkVerified: false,
            InterpreterOrAccommodationArranged: false,
            TechnologyReady: false,
            ClinicalInformationReconciled: false,
            ClinicalIntakeCompleted: false,
            ClinicalEligibilityEstablished: false,
            LegalConsentEstablished: false,
            StaffReviewCreated: false,
            ClinicianReviewCreated: false,
            PracticeAccepted: false,
            PatientRecordChanged: false,
            RequestCreated: false,
            QueueEntered: false,
            AppointmentCreated: false,
            EncounterCreated: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: acknowledged
                ? overallRoute switch
                {
                    "AdditionalClinicalInformationRequired" =>
                        "Additional or unlisted clinical information still requires a separately authorized collection workflow. No request or review task was created.",
                    "AssistedPreRequestSupportRequired" =>
                        "A communication/access preference or uncertain clinical branch still requires separately arranged support. No request or review task was created.",
                    _ =>
                        "The five synthetic sections were acknowledged for a future separately authorized practice review. No request, queue entry, or acceptance was created."
                }
                : "Review all five coarse sections. If anything is wrong or missing, stop and use a separately authorized correction workflow.",
            Limitations:
            [
                "Synthetic demonstration only. Section states prove only that earlier bounded local receipts exist; source values and clinical details are not returned here.",
                "Acknowledgment does not establish identity, coverage, rendering-clinician network status, fulfilled support, technology readiness, reconciliation, completed intake, eligibility, consent, or practice acceptance.",
                "No staff or clinician task, telehealth request, queue entry, appointment, encounter, care, prescribing, billing, claim, integration, or external action is created."
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
