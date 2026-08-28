// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantClinicalInformationSummaryService(
    TelehealthApplicantClinicalInformationSummaryRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantClinicalInformationSummaryResponse> GetAsync(
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
            TelehealthApplicantClinicalInformationSummaryRepository.Snapshot(context),
            context.ConfirmationId is not null,
            context.ConfirmedAt,
            context.SummaryRoute ?? TelehealthApplicantClinicalInformationSummaryRepository.SummaryRoute(context));
    }

    public async Task<TelehealthApplicantClinicalInformationSummaryResponse> ConfirmAsync(
        HttpContext httpContext,
        Guid applicantId,
        ConfirmTelehealthApplicantClinicalInformationSummaryRequest request,
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
        var snapshot = TelehealthApplicantClinicalInformationSummaryRepository.Snapshot(context);
        var normalized = TelehealthApplicantClinicalInformationSummaryPolicy.Normalize(request);
        var summaryRoute = TelehealthApplicantClinicalInformationSummaryRepository.SummaryRoute(context);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-clinical-information-summary-confirmation-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.ClinicalInformationSummarySnapshotFingerprint,
            summaryRoute,
            normalized.PatientReportedMayBeIncompleteAcknowledged,
            normalized.NotClinicallyVerifiedOrReconciledAcknowledged,
            normalized.NoIntakeCompletionOrEligibilityAcknowledged,
            normalized.CorrectionRequiresSeparateWorkflowAcknowledged);
        var confirmed = await repository.ConfirmAsync(
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
                ApplicantVersion = confirmed.ApplicantVersion,
                ApplicantStatus = confirmed.ApplicantStatus
            },
            snapshot,
            confirmed: true,
            confirmed.ConfirmedAt,
            confirmed.SummaryRoute);
    }

    private static TelehealthApplicantClinicalInformationSummaryResponse ToResponse(
        TelehealthApplicantClinicalInformationSummaryContext context,
        TelehealthApplicantClinicalInformationSummarySnapshot snapshot,
        bool confirmed,
        DateTimeOffset? confirmedAt,
        string summaryRoute) => new(
            ApplicantId: context.ApplicantId,
            ApplicantVersion: context.ApplicantVersion,
            ApplicantStatus: context.ApplicantStatus,
            ClinicalInformationSummarySnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantClinicalInformationSummaryPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantClinicalInformationSummaryPolicy.PolicyVersion,
            Categories:
            [
                new(
                    "Medications",
                    context.MedicationsStatus!,
                    context.MedicationItemCount!.Value,
                    context.AdditionalMedicationItemsReported!.Value,
                    context.MedicationReviewRoute!),
                new(
                    "AllergiesOrIntolerances",
                    context.AllergiesOrIntolerancesStatus!,
                    context.AllergyItemCount!.Value,
                    context.AdditionalAllergyItemsReported!.Value,
                    context.AllergyReviewRoute!),
                new(
                    "OtherHealthHistory",
                    context.OtherHealthHistoryStatus!,
                    context.HealthHistoryTopicCount!.Value,
                    context.AdditionalHealthHistoryTopicsReported!.Value,
                    context.HealthHistoryReviewRoute!)
            ],
            SummaryRoute: summaryRoute,
            SummaryConfirmed: confirmed,
            ConfirmedAt: confirmedAt,
            PatientReportedMayBeIncompleteAcknowledged: confirmed,
            NotClinicallyVerifiedOrReconciledAcknowledged: confirmed,
            NoIntakeCompletionOrEligibilityAcknowledged: confirmed,
            CorrectionRequiresSeparateWorkflowAcknowledged: confirmed,
            QuestionnaireResponseCreated: false,
            MedicationListReconciled: false,
            AllergyListReconciled: false,
            HealthHistoryReconciled: false,
            ConfirmedNegativeEstablished: false,
            ClinicianReviewCreated: false,
            ClinicalIntakeCompleted: false,
            ClinicalEligibilityEstablished: false,
            ClinicalTriageChanged: false,
            PatientRecordChanged: false,
            PracticeAccepted: false,
            RequestCreated: false,
            QueueEntered: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            Direction: confirmed
                ? summaryRoute switch
                {
                    "AdditionalClinicalInformationCollectionRequired" =>
                        "Additional or unlisted information still requires a separately authorized collection workflow; no task or request was created.",
                    "AssistedClinicalInformationReviewRequired" =>
                        "At least one category remains uncertain and requires separately authorized assistance and clinician reconciliation; no task or request was created.",
                    "ClinicianClinicalInformationReviewRequired" =>
                        "At least one category has patient-reported items for later clinician review and reconciliation; no task or request was created.",
                    _ =>
                        "The prior coarse inventory reports no items in all three categories. Clinician reconciliation is still required; no confirmed negative was established."
                }
                : "Review the three server-derived categories. If anything is wrong or missing, stop and use a separately authorized correction or collection workflow.",
            Limitations:
            [
                "Synthetic demonstration only. This summary contains only prior coarse patient-reported states, bounded counts, additional-item signals, and informational routes.",
                "Confirmation does not create a QuestionnaireResponse, canonical medication/allergy/problem/history record, confirmed negative, clinical verification, reconciliation, assessment, or eligibility result.",
                "No clinical-review task, completed intake, practice acceptance, request, queue entry, prescribing, care capability, integration, or external action is created."
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
