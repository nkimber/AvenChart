// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantMedicationInformationService(
    TelehealthApplicantMedicationInformationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantMedicationInformationResponse> GetAsync(
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
            context.InventoryMedicationsStatus!,
            TelehealthApplicantMedicationInformationRepository.Snapshot(context),
            context.ReceiptId is not null,
            context.RecordedAt,
            state.MedicationItems,
            context.AdditionalOrUnlistedItemsReported is true,
            context.MedicationReviewRoute);
    }

    public async Task<TelehealthApplicantMedicationInformationResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantMedicationInformationRequest request,
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
        var snapshot = TelehealthApplicantMedicationInformationRepository.Snapshot(context);
        var normalized = TelehealthApplicantMedicationInformationPolicy.Normalize(
            request,
            context.InventoryMedicationsStatus!);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var normalizedItems = string.Join(
            "|",
            normalized.MedicationItems.Select(item =>
                $"{item.CatalogItem.CatalogKey}:{item.ReportedUseStatus}"));
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-medication-information-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.MedicationInformationSnapshotFingerprint,
            context.InventoryMedicationsStatus,
            normalizedItems,
            normalized.AdditionalOrUnlistedItemsReported,
            normalized.PatientReportedMayBeIncompleteAcknowledged,
            normalized.SyntheticCatalogIncompleteAcknowledged,
            normalized.NoDoseOrDirectionsCapturedAcknowledged,
            normalized.ClinicianReconciliationRequiredAcknowledged,
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
            context.InventoryMedicationsStatus!,
            snapshot,
            true,
            recorded.RecordedAt,
            recorded.MedicationItems,
            recorded.AdditionalOrUnlistedItemsReported,
            recorded.ReviewRoute);
    }

    private static TelehealthApplicantMedicationInformationResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        string inventoryMedicationsStatus,
        TelehealthApplicantMedicationInformationSnapshot snapshot,
        bool recorded,
        DateTimeOffset? recordedAt,
        IReadOnlyList<TelehealthApplicantMedicationItemResponse> medicationItems,
        bool additionalOrUnlistedItemsReported,
        string? reviewRoute) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            InventoryMedicationsStatus: inventoryMedicationsStatus,
            MedicationInformationSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantMedicationInformationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantMedicationInformationPolicy.PolicyVersion,
            CatalogKey: SyntheticTelehealthApplicantMedicationCatalog.CatalogKey,
            CatalogVersion: SyntheticTelehealthApplicantMedicationCatalog.CatalogVersion,
            CodingSystem: SyntheticTelehealthApplicantMedicationCatalog.CodingSystem,
            CatalogComplete: false,
            CatalogItems: SyntheticTelehealthApplicantMedicationCatalog.Items,
            SupportedReportedUseStatuses:
                TelehealthApplicantMedicationInformationPolicy.SupportedReportedUseStatuses,
            MedicationInformationRecorded: recorded,
            RecordedAt: recordedAt,
            MedicationItems: medicationItems,
            AdditionalOrUnlistedItemsReported: additionalOrUnlistedItemsReported,
            ReviewRoute: reviewRoute,
            PatientReportedMayBeIncompleteAcknowledged: recorded,
            SyntheticCatalogIncompleteAcknowledged: recorded,
            NoDoseOrDirectionsCapturedAcknowledged: recorded,
            ClinicianReconciliationRequiredAcknowledged: recorded,
            MedicationStatementCreated: false,
            MedicationRequestCreated: false,
            MedicationListReconciled: false,
            InteractionCheckPerformed: false,
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
                    "AdditionalMedicationCollectionRequired" => "Additional or unlisted medication information still requires a separately authorized collection and clinician reconciliation workflow; none was created.",
                    "ClinicianMedicationReviewRequired" => "The selected patient-reported medication information requires clinician reconciliation before care or prescribing; no review task was created.",
                    "AssistedMedicationReviewRequired" => "The patient remains unsure about medications. A separately authorized assisted collection and clinician reconciliation workflow is required; none was created.",
                    _ => "The patient reported no medication items in the prior coarse inventory. Clinician confirmation is still required; no reconciled no-known medication finding was established."
                }
                : inventoryMedicationsStatus switch
                {
                    "ItemsToReview" => "Select available local synthetic ingredients and indicate whether additional or unlisted items exist. Do not enter dose, directions, or other details.",
                    "Unsure" => "Confirm that the medication category remains uncertain. No medication detail or clinician task will be created.",
                    _ => "Confirm the provisional patient report of no medication items. This does not establish a reconciled no-known medication finding."
                },
            Limitations:
            [
                "Synthetic demonstration only; the local ingredient catalog is incomplete and has no RxNorm, NDC, or SNOMED CT mapping claim.",
                "No strength, form, dose, route, frequency, timing, indication, prescriber, pharmacy, date, note, attachment, directions, or free text is collected.",
                "No MedicationStatement, MedicationRequest, canonical medication list, interaction check, clinician task, intake completion, eligibility decision, request, queue entry, prescribing, or care capability is created."
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
