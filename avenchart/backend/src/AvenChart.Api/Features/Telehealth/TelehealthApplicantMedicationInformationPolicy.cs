// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthApplicantMedicationCatalogItem(
    string CatalogKey,
    string DisplayName,
    int CatalogVersion,
    string CodingSystem,
    bool RxNormMapped,
    int SortOrder);

public sealed record TelehealthApplicantMedicationInformationSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantMedicationItem(
    SyntheticTelehealthApplicantMedicationCatalogItem CatalogItem,
    string ReportedUseStatus);

public sealed record NormalizedTelehealthApplicantMedicationInformation(
    int ExpectedVersion,
    string MedicationInformationSnapshotFingerprint,
    IReadOnlyList<NormalizedTelehealthApplicantMedicationItem> MedicationItems,
    bool AdditionalOrUnlistedItemsReported,
    bool PatientReportedMayBeIncompleteAcknowledged,
    bool SyntheticCatalogIncompleteAcknowledged,
    bool NoDoseOrDirectionsCapturedAcknowledged,
    bool ClinicianReconciliationRequiredAcknowledged,
    string ReviewRoute);

public static class SyntheticTelehealthApplicantMedicationCatalog
{
    public const string CatalogKey = "avenchart-synthetic-applicant-medication-ingredients-2026-08";
    public const int CatalogVersion = 1;
    public const string CodingSystem = "LOCAL_SYNTHETIC_ONLY";

    public static readonly IReadOnlyList<SyntheticTelehealthApplicantMedicationCatalogItem> Items =
    [
        Item("acetaminophen", "Acetaminophen", 1),
        Item("ibuprofen", "Ibuprofen", 2),
        Item("sumatriptan", "Sumatriptan", 3),
        Item("melatonin", "Melatonin", 4),
        Item("lisinopril", "Lisinopril", 5),
        Item("metformin", "Metformin", 6)
    ];

    public static SyntheticTelehealthApplicantMedicationCatalogItem? Find(string? catalogKey) =>
        Items.FirstOrDefault(item =>
            string.Equals(item.CatalogKey, catalogKey?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static SyntheticTelehealthApplicantMedicationCatalogItem Item(
        string catalogKey,
        string displayName,
        int sortOrder) => new(
            catalogKey,
            displayName,
            CatalogVersion,
            CodingSystem,
            RxNormMapped: false,
            sortOrder);
}

public static class TelehealthApplicantMedicationInformationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_MEDICATION_INFORMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_MEDICATION_INFORMATION_RECEIPT";
    public const string EntryStatus = "SyntheticClinicalInformationInventoryRecorded";
    public const string ResultingStatus = "SyntheticMedicationInformationRecorded";
    public static readonly IReadOnlyList<string> SupportedReportedUseStatuses =
        ["Taking", "NotTaking", "Unsure"];

    public static TelehealthApplicantMedicationInformationSnapshot Snapshot(
        Guid inventoryId,
        string inventorySnapshotFingerprint,
        string medicationsStatus,
        string inventoryReviewRoute) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-medication-information-snapshot-v1",
                inventoryId,
                inventorySnapshotFingerprint,
                medicationsStatus,
                inventoryReviewRoute));

    public static NormalizedTelehealthApplicantMedicationInformation Normalize(
        RecordTelehealthApplicantMedicationInformationRequest request,
        string inventoryMedicationsStatus)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_medication_information_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.MedicationInformationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_medication_information_fingerprint_invalid",
                "Reload the medication-information context before recording the result.");
        }

        if (!TelehealthApplicantClinicalInformationInventoryPolicy.SupportedCategoryStatuses
            .Contains(inventoryMedicationsStatus, StringComparer.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_medication_information_inventory_conflict",
                "The prior medication inventory status is unavailable.");
        }

        var suppliedItems = request.MedicationItems ?? [];
        if (suppliedItems.Count > SyntheticTelehealthApplicantMedicationCatalog.Items.Count)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_medication_information_item_limit",
                "The medication selection exceeds the bounded synthetic catalog.");
        }

        var normalizedItems = new List<NormalizedTelehealthApplicantMedicationItem>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suppliedItem in suppliedItems)
        {
            var catalogItem = SyntheticTelehealthApplicantMedicationCatalog.Find(
                suppliedItem.CatalogKey);
            if (catalogItem is null || !keys.Add(catalogItem.CatalogKey))
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_medication_information_item_invalid",
                    "Select each medication at most once from the current synthetic catalog.");
            }

            var useStatus = SupportedReportedUseStatuses.FirstOrDefault(value =>
                string.Equals(
                    value,
                    suppliedItem.ReportedUseStatus?.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (useStatus is null)
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_medication_information_use_status_invalid",
                    "Select Taking, NotTaking, or Unsure for every selected medication.");
            }

            normalizedItems.Add(new(catalogItem, useStatus));
        }

        normalizedItems.Sort((left, right) =>
            left.CatalogItem.SortOrder.CompareTo(right.CatalogItem.SortOrder));

        if (inventoryMedicationsStatus == "ItemsToReview")
        {
            if (normalizedItems.Count == 0 && !request.AdditionalOrUnlistedItemsReported)
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_medication_information_items_required",
                    "Select at least one catalog item or indicate that additional or unlisted items exist.");
            }
        }
        else if (normalizedItems.Count != 0 || request.AdditionalOrUnlistedItemsReported)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_medication_information_branch_invalid",
                "The medication selections do not match the prior patient-reported inventory status.");
        }

        if (!request.PatientReportedMayBeIncompleteAcknowledged
            || !request.SyntheticCatalogIncompleteAcknowledged
            || !request.NoDoseOrDirectionsCapturedAcknowledged
            || !request.ClinicianReconciliationRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_medication_information_acknowledgments_required",
                "Confirm every medication-information limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            normalizedItems,
            request.AdditionalOrUnlistedItemsReported,
            request.PatientReportedMayBeIncompleteAcknowledged,
            request.SyntheticCatalogIncompleteAcknowledged,
            request.NoDoseOrDirectionsCapturedAcknowledged,
            request.ClinicianReconciliationRequiredAcknowledged,
            DetermineReviewRoute(
                inventoryMedicationsStatus,
                request.AdditionalOrUnlistedItemsReported));
    }

    public static string DetermineReviewRoute(
        string inventoryMedicationsStatus,
        bool additionalOrUnlistedItemsReported)
    {
        if (additionalOrUnlistedItemsReported)
        {
            return "AdditionalMedicationCollectionRequired";
        }

        return inventoryMedicationsStatus switch
        {
            "ItemsToReview" => "ClinicianMedicationReviewRequired",
            "Unsure" => "AssistedMedicationReviewRequired",
            _ => "PendingClinicianConfirmationOfNone"
        };
    }
}
