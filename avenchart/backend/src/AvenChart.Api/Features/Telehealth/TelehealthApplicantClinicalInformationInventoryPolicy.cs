// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantClinicalInformationInventorySnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantClinicalInformationInventory(
    int ExpectedVersion,
    string InventorySnapshotFingerprint,
    string MedicationsStatus,
    string AllergiesOrIntolerancesStatus,
    string OtherHealthHistoryStatus,
    bool PatientReportedMayBeIncompleteAcknowledged,
    bool NoClinicalDetailsCapturedAcknowledged,
    bool ClinicianReconciliationRequiredAcknowledged,
    string ReviewRoute);

public static class TelehealthApplicantClinicalInformationInventoryPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT";
    public const string EntryStatus = "SyntheticDevicePreparationRecorded";
    public const string ResultingStatus = "SyntheticClinicalInformationInventoryRecorded";
    public static readonly IReadOnlyList<string> SupportedCategoryStatuses =
        ["PatientReportsNone", "ItemsToReview", "Unsure"];

    public static TelehealthApplicantClinicalInformationInventorySnapshot Snapshot(
        Guid devicePreparationId,
        string preparationSnapshotFingerprint,
        string networkQuality) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-clinical-information-inventory-snapshot-v1",
                devicePreparationId,
                preparationSnapshotFingerprint,
                networkQuality));

    public static NormalizedTelehealthApplicantClinicalInformationInventory Normalize(
        RecordTelehealthApplicantClinicalInformationInventoryRequest request)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_inventory_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.InventorySnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_inventory_fingerprint_invalid",
                "Reload the clinical-information inventory context before recording the result.");
        }

        var medications = RequireCategoryStatus(request.MedicationsStatus, "medications");
        var allergies = RequireCategoryStatus(request.AllergiesOrIntolerancesStatus, "allergies or intolerances");
        var history = RequireCategoryStatus(request.OtherHealthHistoryStatus, "other health history");

        if (!request.PatientReportedMayBeIncompleteAcknowledged
            || !request.NoClinicalDetailsCapturedAcknowledged
            || !request.ClinicianReconciliationRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_inventory_acknowledgments_required",
                "Confirm every clinical-information inventory limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            medications,
            allergies,
            history,
            request.PatientReportedMayBeIncompleteAcknowledged,
            request.NoClinicalDetailsCapturedAcknowledged,
            request.ClinicianReconciliationRequiredAcknowledged,
            DetermineReviewRoute(medications, allergies, history));
    }

    public static string DetermineReviewRoute(params string[] statuses)
    {
        if (statuses.Contains("ItemsToReview", StringComparer.Ordinal))
        {
            return "DetailedCollectionRequired";
        }

        return statuses.Contains("Unsure", StringComparer.Ordinal)
            ? "AssistedReviewRequired"
            : "PendingClinicianReconciliation";
    }

    private static string RequireCategoryStatus(string? supplied, string category)
    {
        var normalized = SupportedCategoryStatuses.FirstOrDefault(value =>
            string.Equals(value, supplied?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_clinical_information_inventory_status_invalid",
                $"Select PatientReportsNone, ItemsToReview, or Unsure for {category}.");
        }

        return normalized;
    }
}
