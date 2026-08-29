// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantRequestIntakeService(
    TelehealthApplicantRequestIntakeRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantRequestIntakeResponse> GetAsync(
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

    public async Task<TelehealthApplicantRequestIntakeResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantRequestIntake request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var normalized = TelehealthApplicantRequestIntakePolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "synthetic-applicant-request-intake-confirmation-v1",
            applicantId,
            normalized.ExpectedRequestVersion,
            normalized.ContextSnapshotFingerprint,
            normalized.CurrentLocationStateCode,
            normalized.SymptomDuration,
            normalized.CurrentLocationConfirmed,
            normalized.CallbackNumberConfirmed,
            normalized.PriorInformationReviewed,
            normalized.InsuranceLimitationsAcknowledged,
            normalized.PendingConsentAcknowledged,
            normalized.PendingVerificationAcknowledged,
            normalized.ComplaintResultAcknowledged,
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

    private static TelehealthApplicantRequestIntakeResponse ToResponse(
        TelehealthApplicantRequestIntakeRecord result)
    {
        var created = result.ReceiptId is not null;
        return new(
            ApplicantId: result.ApplicantId,
            ApplicantVersion: result.ApplicantVersion,
            ApplicantStatus: result.ApplicantStatus,
            RequestId: result.RequestId,
            RequestVersion: result.RequestVersion,
            RequestStatus: result.RequestStatus,
            ComplaintCategory: result.ComplaintCategory,
            ComplaintDisplayLabel: result.ComplaintCategory == "migraine"
                ? "Migraine"
                : "Sleep difficulty",
            PolicyKey: TelehealthApplicantRequestIntakePolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantRequestIntakePolicy.PolicyVersion,
            ClinicalContentStatus: TelehealthApplicantRequestIntakePolicy.ClinicalContentStatus,
            MedicalDirectorApprovalRequired: true,
            MedicalDirectorApprovalRecorded: false,
            ClinicalGoldenCasePackApproved: false,
            ProductionPublicationAllowed: false,
            ContextSnapshotFingerprint: result.ContextSnapshotFingerprint,
            ContextExpiresAt: result.ContextExpiresAt,
            CurrentLocationStateCode: result.CurrentLocationStateCode,
            MaskedCallbackPhone: $"***-***-{result.CallbackPhoneLast4}",
            SupportedSymptomDurations: TelehealthApplicantRequestIntakePolicy.SupportedSymptomDurations,
            Sections:
            [
                new("registration", "Confirmed", "No patient-record mutation"),
                new("insurance", "Applicant handoff confirmed", "Canonical coverage and current verification pending"),
                new("communication", "Recorded", "No patient contact performed"),
                new("device", "Recorded", "No media session created"),
                new("clinical-information", "Summary confirmed", "No diagnosis or treatment authorized"),
                new("practice-review", "Request creation authorized", "Operational review and acceptance pending"),
                new("location", "Confirmed", "Must remain current through this step"),
                new("universal-safety", "Passed synthetic fixture", "Clinical publication remains blocked"),
                new("complaint-triage", "Synthetic candidate", "Not diagnosis or guaranteed treatment")
            ],
            SnapshotReady: !created,
            SnapshotCreated: created,
            SymptomDuration: result.SymptomDuration,
            CapturedAt: result.CapturedAt,
            VerificationPending: true,
            ConsentPending: true,
            CoverageRecordCreated: false,
            CoverageVerified: false,
            ExactNetworkConfirmed: false,
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
            PrescribingEnabled: false,
            BillingEnabled: false,
            ClaimCreated: false,
            IntegrationEnabled: false,
            ExternalCallPerformed: false,
            Direction: created
                ? "The synthetic intake snapshot is recorded. Verification, consent, coverage, exact network, financial, operational review, queueing, and care are still pending and unavailable."
                : "Choose one duration range and review all eight confirmations. Correct changed information or restart if the location or callback route is no longer current.",
            Limitations:
            [
                "NON_PRODUCTION synthetic demonstration only. No real person or protected health information may be used.",
                "The complaint fixture is unapproved clinical content; medical-director approval, golden-case review, and production publication remain false.",
                "The server derives a fixed synthetic summary. This endpoint accepts no free-text complaint, diagnosis, treatment request, clinical answer, rule, reason, or outcome override.",
                "Verification status does not mean identity verified, consented, covered, in network, accepted, queued, scheduled, assigned, diagnosed, or under care.",
                "No canonical coverage, payer or network check, financial route, operational work item, contact, queue, appointment, encounter, media, care, prescription, claim, integration, or external action is created."
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
