// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthApplicantClinicalInformationInventoryService(
    TelehealthApplicantClinicalInformationInventoryRepository repository,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthApplicantClinicalInformationInventoryResponse> GetAsync(
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
            context.ApplicantId,
            context.ApplicantVersion,
            context.ApplicantStatus,
            TelehealthApplicantClinicalInformationInventoryRepository.Snapshot(context),
            context.InventoryId is not null,
            context.RecordedAt,
            context.MedicationsStatus,
            context.AllergiesOrIntolerancesStatus,
            context.OtherHealthHistoryStatus,
            context.ReviewRoute);
    }

    public async Task<TelehealthApplicantClinicalInformationInventoryResponse> RecordAsync(
        HttpContext httpContext,
        Guid applicantId,
        RecordTelehealthApplicantClinicalInformationInventoryRequest request,
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
        var snapshot = TelehealthApplicantClinicalInformationInventoryRepository.Snapshot(current);
        var normalized = TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(request);
        var semanticKey = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var commandFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-clinical-information-inventory-v1",
            applicantId,
            normalized.ExpectedVersion,
            normalized.InventorySnapshotFingerprint,
            normalized.MedicationsStatus,
            normalized.AllergiesOrIntolerancesStatus,
            normalized.OtherHealthHistoryStatus,
            normalized.PatientReportedMayBeIncompleteAcknowledged,
            normalized.NoClinicalDetailsCapturedAcknowledged,
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
            snapshot,
            true,
            recorded.RecordedAt,
            recorded.MedicationsStatus,
            recorded.AllergiesOrIntolerancesStatus,
            recorded.OtherHealthHistoryStatus,
            recorded.ReviewRoute);
    }

    private static TelehealthApplicantClinicalInformationInventoryResponse ToResponse(
        Guid applicantId,
        int applicantVersion,
        string applicantStatus,
        TelehealthApplicantClinicalInformationInventorySnapshot snapshot,
        bool recorded,
        DateTimeOffset? recordedAt,
        string? medicationsStatus,
        string? allergiesOrIntolerancesStatus,
        string? otherHealthHistoryStatus,
        string? reviewRoute) => new(
            ApplicantId: applicantId,
            ApplicantVersion: applicantVersion,
            ApplicantStatus: applicantStatus,
            SupportedCategoryStatuses:
                TelehealthApplicantClinicalInformationInventoryPolicy.SupportedCategoryStatuses,
            InventorySnapshotFingerprint: snapshot.Fingerprint,
            PolicyKey: TelehealthApplicantClinicalInformationInventoryPolicy.PolicyKey,
            PolicyVersion: TelehealthApplicantClinicalInformationInventoryPolicy.PolicyVersion,
            InventoryRecorded: recorded,
            RecordedAt: recordedAt,
            MedicationsStatus: medicationsStatus,
            AllergiesOrIntolerancesStatus: allergiesOrIntolerancesStatus,
            OtherHealthHistoryStatus: otherHealthHistoryStatus,
            ReviewRoute: reviewRoute,
            PatientReportedMayBeIncompleteAcknowledged: recorded,
            NoClinicalDetailsCapturedAcknowledged: recorded,
            ClinicianReconciliationRequiredAcknowledged: recorded,
            MedicationListReconciled: false,
            AllergyListReconciled: false,
            HealthHistoryReconciled: false,
            ClinicalIntakeCompleted: false,
            ClinicalEligibilityEstablished: false,
            ClinicianReviewCreated: false,
            PatientRecordChanged: false,
            RequestCreated: false,
            QueueEntered: false,
            CareAuthorized: false,
            PrescribingEnabled: false,
            Direction: recorded
                ? reviewRoute switch
                {
                    "DetailedCollectionRequired" => "One or more categories contain items to review. A separately authorized detailed collection and clinician reconciliation workflow is required; none was created.",
                    "AssistedReviewRequired" => "One or more categories are uncertain. A separately authorized assisted review and clinician reconciliation workflow is required; none was created.",
                    _ => "The patient reported no items in these three coarse categories. Clinician reconciliation is still required before care or prescribing; no no-known clinical finding was established."
                }
                : "Choose one coarse patient-reported status for each category. Do not enter names, reactions, doses, diagnoses, symptoms, dates, identifiers, or other clinical details.",
            Limitations:
            [
                "Synthetic demonstration only; no medication, substance, reaction, dose, diagnosis, symptom, procedure, narrative, date, identifier, or free text is collected.",
                "These are patient-reported category signals that may be incomplete. PatientReportsNone never establishes a clinician-reconciled no-known finding.",
                "No canonical medication, allergy, problem, or history record is read or changed, and no clinical review task, intake completion, eligibility decision, request, queue entry, prescribing, or care capability is created."
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
