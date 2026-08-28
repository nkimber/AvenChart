// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantHealthHistoryInformationService(
    TelehealthApplicantHealthHistoryInformationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantHealthHistoryInformationResponse> GetAsync(
        HttpContext httpContext,
        Guid applicantId,
        string applicantAccessKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var state = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            TelehealthProspectiveApplicantPolicy.Hash(key),
            cancellationToken);
        var context = state.Context;
        return ToResponse(
            context.ApplicantId,
            context.ApplicantVersion,
            context.ApplicantStatus,
            context.InventoryOtherHealthHistoryStatus!,
            TelehealthApplicantHealthHistoryInformationRepository.Snapshot(context),
            context.ReceiptId is not null,
            context.RecordedAt,
            state.HealthHistoryTopics,
            context.AdditionalOrUnlistedTopicsReported is true,
            context.HealthHistoryReviewRoute);
    }

    public async Task<TelehealthApplicantHealthHistoryInformationResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantHealthHistoryInformationRequest request,
        string applicantAccessKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var key = TelehealthProspectiveApplicantPolicy.RequireAccessKey(applicantAccessKey);
        var accessKeyHash = TelehealthProspectiveApplicantPolicy.Hash(key);
        var current = await repository.GetAuthorizedAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            cancellationToken);
        var context = current.Context;
        var snapshot = TelehealthApplicantHealthHistoryInformationRepository.Snapshot(context);
        var normalized = TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
            request,
            context.InventoryOtherHealthHistoryStatus!);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var normalizedTopics = string.Join(
            "|",
            normalized.HealthHistoryTopics.Select(topic => topic.CatalogKey));
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-health-history-information-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.HealthHistoryInformationSnapshotFingerprint,
            context.InventoryOtherHealthHistoryStatus,
            normalizedTopics,
            normalized.AdditionalOrUnlistedTopicsReported,
            normalized.PatientReportedMayBeIncompleteAcknowledged,
            normalized.TopicSelectionIsNotDiagnosisAcknowledged,
            normalized.NoStatusOrTimingCapturedAcknowledged,
            normalized.ClinicianVerificationRequiredAcknowledged,
            normalized.ReviewRoute);
        var recorded = await repository.RecordAsync(
            _options.PracticeId,
            _options.FacilityId,
            applicantId,
            accessKeyHash,
            normalized,
            semanticKey,
            commandFingerprint,
            cancellationToken);
        return ToResponse(
            recorded.ApplicantId,
            recorded.ApplicantVersion,
            recorded.ApplicantStatus,
            context.InventoryOtherHealthHistoryStatus!,
            snapshot,
            true,
            recorded.RecordedAt,
            recorded.HealthHistoryTopics,
            recorded.AdditionalOrUnlistedTopicsReported,
            recorded.ReviewRoute);
    }

    private static TelehealthApplicantHealthHistoryInformationResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        string inventoryOtherHealthHistoryStatus,
        TelehealthApplicantHealthHistoryInformationSnapshot snapshot,
        bool recorded,
        DateTimeOffset? recordedAt,
        IReadOnlyList<TelehealthApplicantHealthHistoryTopicResponse> healthHistoryTopics,
        bool additionalOrUnlistedTopicsReported,
        string? reviewRoute) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            InventoryOtherHealthHistoryStatus: inventoryOtherHealthHistoryStatus,
            HealthHistoryInformationSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantHealthHistoryInformationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantHealthHistoryInformationPolicy.PolicyVersion,
            CatalogKey: SyntheticTelehealthApplicantHealthHistoryTopicCatalog.CatalogKey,
            CatalogVersion: SyntheticTelehealthApplicantHealthHistoryTopicCatalog.CatalogVersion,
            CodingSystem: SyntheticTelehealthApplicantHealthHistoryTopicCatalog.CodingSystem,
            CatalogComplete: false,
            CatalogTopics: SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items,
            HealthHistoryInformationRecorded: recorded,
            RecordedAt: recordedAt,
            HealthHistoryTopics: healthHistoryTopics,
            AdditionalOrUnlistedTopicsReported: additionalOrUnlistedTopicsReported,
            ReviewRoute: reviewRoute,
            PatientReportedMayBeIncompleteAcknowledged: recorded,
            TopicSelectionIsNotDiagnosisAcknowledged: recorded,
            NoStatusOrTimingCapturedAcknowledged: recorded,
            ClinicianVerificationRequiredAcknowledged: recorded,
            ConditionCreated: false,
            ProcedureCreated: false,
            ObservationCreated: false,
            FamilyMemberHistoryCreated: false,
            QuestionnaireResponseCreated: false,
            HealthHistoryReconciled: false,
            RiskModifierEvaluated: false,
            ClinicalTriageChanged: false,
            ClinicianReviewCreated: false,
            ClinicalIntakeCompleted: false,
            ClinicalEligibilityEstablished: false,
            PatientRecordChanged: false,
            RequestCreated: false,
            QueueEntered: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            Direction: recorded
                ? reviewRoute switch
                {
                    "AdditionalHealthHistoryCollectionRequired" => "Additional or unlisted health-history topics still require separately authorized collection and clinician verification; no workflow was created.",
                    "ClinicianHealthHistoryReviewRequired" => "The selected patient-reported topics require detailed collection and clinician verification before care; no review task or clinical finding was created.",
                    "AssistedHealthHistoryReviewRequired" => "The patient remains unsure about other health history. Separately authorized assisted collection and clinician verification are required; no workflow was created.",
                    _ => "The patient reported no other-health-history items in the prior coarse inventory. Clinician confirmation is still required; no confirmed no-history assertion was established."
                }
                : inventoryOtherHealthHistoryStatus switch
                {
                    "ItemsToReview" => "Select broad local synthetic topics that need later review and indicate whether additional or unlisted topics exist. Do not enter diagnoses or details.",
                    "Unsure" => "Confirm that other health history remains uncertain. No condition, assessment, or clinician task will be created.",
                    _ => "Confirm the provisional patient report of no other-health-history items. This does not establish a confirmed no-history assertion."
                },
            Limitations:
            [
                "Synthetic demonstration only; the local topic catalog is incomplete and has no SNOMED CT, ICD-10-CM, LOINC, FHIR, US Core, or USCDI mapping claim.",
                "A topic is only a prompt for later review. No diagnosis, condition, procedure, observation, pregnancy status, assessment, family-history finding, clinical or verification status, timing, severity, date, note, attachment, or free text is collected.",
                "No canonical problem, Condition, Procedure, Observation, FamilyMemberHistory, QuestionnaireResponse, confirmed negation, reconciliation, risk evaluation, triage change, clinician task, intake completion, eligibility decision, request, queue entry, prescribing, or care capability is created."
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
