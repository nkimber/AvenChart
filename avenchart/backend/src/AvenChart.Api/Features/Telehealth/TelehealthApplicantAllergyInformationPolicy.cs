// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthApplicantAllergyCatalogItem(
    string CatalogKey,
    string DisplayName,
    string Category,
    int CatalogVersion,
    string CodingSystem,
    bool SnomedCtMapped,
    bool RxNormMapped,
    int SortOrder);

public sealed record TelehealthApplicantAllergyInformationSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantAllergyInformation(
    int ExpectedVersion,
    string AllergyInformationSnapshotFingerprint,
    IReadOnlyList<SyntheticTelehealthApplicantAllergyCatalogItem> AllergyItems,
    bool AdditionalOrUnlistedItemsReported,
    bool PatientReportedMayBeIncompleteAcknowledged,
    bool SyntheticCatalogIncompleteAcknowledged,
    bool NoReactionOrCriticalityCapturedAcknowledged,
    bool ClinicianVerificationRequiredAcknowledged,
    string ReviewRoute);

public static class SyntheticTelehealthApplicantAllergyCatalog
{
    public const string CatalogKey = "avenchart-synthetic-applicant-allergy-substances-2026-08";
    public const int CatalogVersion = 1;
    public const string CodingSystem = "LOCAL_SYNTHETIC_ONLY";

    public static readonly IReadOnlyList<SyntheticTelehealthApplicantAllergyCatalogItem> Items =
    [
        Item("amoxicillin", "Amoxicillin", "Medication", 1),
        Item("ibuprofen", "Ibuprofen", "Medication", 2),
        Item("peanut", "Peanut", "Food", 3),
        Item("shellfish", "Shellfish", "Food", 4),
        Item("latex", "Latex", "Environment", 5),
        Item("bee-venom", "Bee venom", "Environment", 6)
    ];

    public static SyntheticTelehealthApplicantAllergyCatalogItem? Find(string? catalogKey) =>
        Items.FirstOrDefault(item =>
            string.Equals(item.CatalogKey, catalogKey?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static SyntheticTelehealthApplicantAllergyCatalogItem Item(
        string catalogKey,
        string displayName,
        string category,
        int sortOrder) => new(
            catalogKey,
            displayName,
            category,
            CatalogVersion,
            CodingSystem,
            SnomedCtMapped: false,
            RxNormMapped: false,
            sortOrder);
}

public static class TelehealthApplicantAllergyInformationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_ALLERGY_INFORMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_ALLERGY_INFORMATION_RECEIPT";
    public const string EntryStatus = "SyntheticMedicationInformationRecorded";
    public const string ResultingStatus = "SyntheticAllergyInformationRecorded";

    public static TelehealthApplicantAllergyInformationSnapshot Snapshot(
        Guid medicationInformationId,
        string medicationInformationSnapshotFingerprint,
        string allergiesOrIntolerancesStatus,
        string medicationReviewRoute) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-allergy-information-snapshot-v1",
                medicationInformationId,
                medicationInformationSnapshotFingerprint,
                allergiesOrIntolerancesStatus,
                medicationReviewRoute));

    public static NormalizedTelehealthApplicantAllergyInformation Normalize(
        RecordTelehealthApplicantAllergyInformationRequest request,
        string inventoryAllergiesOrIntolerancesStatus)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_allergy_information_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.AllergyInformationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_allergy_information_fingerprint_invalid",
                "Reload the allergy-information context before recording the result.");
        }

        if (!TelehealthApplicantClinicalInformationInventoryPolicy.SupportedCategoryStatuses
            .Contains(inventoryAllergiesOrIntolerancesStatus, StringComparer.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_allergy_information_inventory_conflict",
                "The prior allergy or intolerance inventory status is unavailable.");
        }

        var suppliedItems = request.AllergyItems ?? [];
        if (suppliedItems.Count > SyntheticTelehealthApplicantAllergyCatalog.Items.Count)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_allergy_information_item_limit",
                "The substance selection exceeds the bounded synthetic catalog.");
        }

        var normalizedItems = new List<SyntheticTelehealthApplicantAllergyCatalogItem>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suppliedItem in suppliedItems)
        {
            var catalogItem = SyntheticTelehealthApplicantAllergyCatalog.Find(
                suppliedItem.CatalogKey);
            if (catalogItem is null || !keys.Add(catalogItem.CatalogKey))
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_allergy_information_item_invalid",
                    "Select each substance at most once from the current synthetic catalog.");
            }

            normalizedItems.Add(catalogItem);
        }

        normalizedItems.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));

        if (inventoryAllergiesOrIntolerancesStatus == "ItemsToReview")
        {
            if (normalizedItems.Count == 0 && !request.AdditionalOrUnlistedItemsReported)
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_allergy_information_items_required",
                    "Select at least one catalog substance or indicate that additional or unlisted substances exist.");
            }
        }
        else if (normalizedItems.Count != 0 || request.AdditionalOrUnlistedItemsReported)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_allergy_information_branch_invalid",
                "The substance selections do not match the prior patient-reported inventory status.");
        }

        if (!request.PatientReportedMayBeIncompleteAcknowledged
            || !request.SyntheticCatalogIncompleteAcknowledged
            || !request.NoReactionOrCriticalityCapturedAcknowledged
            || !request.ClinicianVerificationRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_allergy_information_acknowledgments_required",
                "Confirm every allergy-information limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            normalizedItems,
            request.AdditionalOrUnlistedItemsReported,
            request.PatientReportedMayBeIncompleteAcknowledged,
            request.SyntheticCatalogIncompleteAcknowledged,
            request.NoReactionOrCriticalityCapturedAcknowledged,
            request.ClinicianVerificationRequiredAcknowledged,
            DetermineReviewRoute(
                inventoryAllergiesOrIntolerancesStatus,
                request.AdditionalOrUnlistedItemsReported));
    }

    public static string DetermineReviewRoute(
        string inventoryAllergiesOrIntolerancesStatus,
        bool additionalOrUnlistedItemsReported)
    {
        if (additionalOrUnlistedItemsReported)
        {
            return "AdditionalAllergyCollectionRequired";
        }

        return inventoryAllergiesOrIntolerancesStatus switch
        {
            "ItemsToReview" => "ClinicianAllergyReviewRequired",
            "Unsure" => "AssistedAllergyReviewRequired",
            _ => "PendingClinicianConfirmationOfPatientReportedNone"
        };
    }
}
