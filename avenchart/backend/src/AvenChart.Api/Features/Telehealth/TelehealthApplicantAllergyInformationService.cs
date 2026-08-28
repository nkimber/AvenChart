// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantAllergyInformationService(
    TelehealthApplicantAllergyInformationRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantAllergyInformationResponse> GetAsync(
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
            context.InventoryAllergiesOrIntolerancesStatus!,
            TelehealthApplicantAllergyInformationRepository.Snapshot(context),
            context.ReceiptId is not null,
            context.RecordedAt,
            state.AllergyItems,
            context.AdditionalOrUnlistedItemsReported is true,
            context.AllergyReviewRoute);
    }

    public async Task<TelehealthApplicantAllergyInformationResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantAllergyInformationRequest request,
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
        var snapshot = TelehealthApplicantAllergyInformationRepository.Snapshot(context);
        var normalized = TelehealthApplicantAllergyInformationPolicy.Normalize(
            request,
            context.InventoryAllergiesOrIntolerancesStatus!);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var normalizedItems = string.Join(
            "|",
            normalized.AllergyItems.Select(item => item.CatalogKey));
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-allergy-information-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.AllergyInformationSnapshotFingerprint,
            context.InventoryAllergiesOrIntolerancesStatus,
            normalizedItems,
            normalized.AdditionalOrUnlistedItemsReported,
            normalized.PatientReportedMayBeIncompleteAcknowledged,
            normalized.SyntheticCatalogIncompleteAcknowledged,
            normalized.NoReactionOrCriticalityCapturedAcknowledged,
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
            context.InventoryAllergiesOrIntolerancesStatus!,
            snapshot,
            true,
            recorded.RecordedAt,
            recorded.AllergyItems,
            recorded.AdditionalOrUnlistedItemsReported,
            recorded.ReviewRoute);
    }

    private static TelehealthApplicantAllergyInformationResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        string inventoryAllergiesOrIntolerancesStatus,
        TelehealthApplicantAllergyInformationSnapshot snapshot,
        bool recorded,
        DateTimeOffset? recordedAt,
        IReadOnlyList<TelehealthApplicantAllergyItemResponse> allergyItems,
        bool additionalOrUnlistedItemsReported,
        string? reviewRoute) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            InventoryAllergiesOrIntolerancesStatus: inventoryAllergiesOrIntolerancesStatus,
            AllergyInformationSnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantAllergyInformationPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantAllergyInformationPolicy.PolicyVersion,
            CatalogKey: SyntheticTelehealthApplicantAllergyCatalog.CatalogKey,
            CatalogVersion: SyntheticTelehealthApplicantAllergyCatalog.CatalogVersion,
            CodingSystem: SyntheticTelehealthApplicantAllergyCatalog.CodingSystem,
            CatalogComplete: false,
            CatalogItems: SyntheticTelehealthApplicantAllergyCatalog.Items,
            AllergyInformationRecorded: recorded,
            RecordedAt: recordedAt,
            AllergyItems: allergyItems,
            AdditionalOrUnlistedItemsReported: additionalOrUnlistedItemsReported,
            ReviewRoute: reviewRoute,
            PatientReportedMayBeIncompleteAcknowledged: recorded,
            SyntheticCatalogIncompleteAcknowledged: recorded,
            NoReactionOrCriticalityCapturedAcknowledged: recorded,
            ClinicianVerificationRequiredAcknowledged: recorded,
            AllergyIntoleranceCreated: false,
            AllergyListReconciled: false,
            ReactionAssessed: false,
            CriticalityAssessed: false,
            ContraindicationCheckPerformed: false,
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
                    "AdditionalAllergyCollectionRequired" => "Additional or unlisted allergy or intolerance information still requires a separately authorized collection and clinician verification workflow; none was created.",
                    "ClinicianAllergyReviewRequired" => "The selected patient-reported substances require clinician verification and reconciliation before care or prescribing; no review task or alert was created.",
                    "AssistedAllergyReviewRequired" => "The patient remains unsure about allergies or intolerances. A separately authorized assisted collection and clinician verification workflow is required; none was created.",
                    _ => "The patient reported no allergy or intolerance items in the prior coarse inventory. Clinician confirmation is still required; no confirmed no-known-allergy assertion was established."
                }
                : inventoryAllergiesOrIntolerancesStatus switch
                {
                    "ItemsToReview" => "Select available local synthetic substances and indicate whether additional or unlisted substances exist. Do not enter reactions or other details.",
                    "Unsure" => "Confirm that the allergy or intolerance category remains uncertain. No allergy record, alert, or clinician task will be created.",
                    _ => "Confirm the provisional patient report of no allergy or intolerance items. This does not establish a confirmed no-known-allergy assertion."
                },
            Limitations:
            [
                "Synthetic demonstration only; the local substance catalog is incomplete and has no SNOMED CT, RxNorm, NDC, UNII, or other external terminology mapping claim.",
                "No reaction, manifestation, allergy-versus-intolerance type, clinical status, verification status, severity, criticality, onset, date, note, attachment, or free text is collected.",
                "No AllergyIntolerance resource, canonical allergy list, confirmed negation, reconciliation, contraindication check, alert, clinician task, intake completion, eligibility decision, request, queue entry, prescribing, or care capability is created."
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
