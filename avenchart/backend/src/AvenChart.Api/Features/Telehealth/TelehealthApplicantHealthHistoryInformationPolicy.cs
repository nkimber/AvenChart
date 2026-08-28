// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record SyntheticTelehealthApplicantHealthHistoryTopic(
    string CatalogKey,
    string DisplayName,
    string Category,
    int CatalogVersion,
    string CodingSystem,
    bool SnomedCtMapped,
    bool Icd10CmMapped,
    bool LoincMapped,
    int SortOrder);

public sealed record TelehealthApplicantHealthHistoryInformationSnapshot(string Fingerprint);

public sealed record NormalizedTelehealthApplicantHealthHistoryInformation(
    int ExpectedVersion,
    string HealthHistoryInformationSnapshotFingerprint,
    IReadOnlyList<SyntheticTelehealthApplicantHealthHistoryTopic> HealthHistoryTopics,
    bool AdditionalOrUnlistedTopicsReported,
    bool PatientReportedMayBeIncompleteAcknowledged,
    bool TopicSelectionIsNotDiagnosisAcknowledged,
    bool NoStatusOrTimingCapturedAcknowledged,
    bool ClinicianVerificationRequiredAcknowledged,
    string ReviewRoute);

public static class SyntheticTelehealthApplicantHealthHistoryTopicCatalog
{
    public const string CatalogKey = "avenchart-synthetic-applicant-health-history-topics-2026-08";
    public const int CatalogVersion = 1;
    public const string CodingSystem = "LOCAL_SYNTHETIC_ONLY";

    public static readonly IReadOnlyList<SyntheticTelehealthApplicantHealthHistoryTopic> Items =
    [
        Item("ongoing-health-conditions", "Ongoing health conditions", "ConditionOrConcern", 1),
        Item("prior-surgery-or-hospital-stay", "Prior surgeries or hospital stays", "ProcedureOrHospitalization", 2),
        Item("pregnancy-or-postpartum-information", "Pregnancy or postpartum information", "HealthStatus", 3),
        Item("immune-system-or-active-cancer-treatment", "Immune system or active cancer treatment information", "RiskContext", 4),
        Item("behavioral-health-or-substance-use", "Behavioral health or substance use information", "SensitiveHistory", 5),
        Item("family-health-history", "Family health history", "FamilyHistory", 6)
    ];

    public static SyntheticTelehealthApplicantHealthHistoryTopic? Find(string? catalogKey) =>
        Items.FirstOrDefault(item =>
            string.Equals(item.CatalogKey, catalogKey?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static SyntheticTelehealthApplicantHealthHistoryTopic Item(
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
            Icd10CmMapped: false,
            LoincMapped: false,
            sortOrder);
}

public static class TelehealthApplicantHealthHistoryInformationPolicy
{
    public const string PolicyKey = "SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION";
    public const int PolicyVersion = 1;
    public const string EvidenceType = "PROMOTED_PATIENT_HEALTH_HISTORY_INFORMATION_RECEIPT";
    public const string EntryStatus = "SyntheticAllergyInformationRecorded";
    public const string ResultingStatus = "SyntheticHealthHistoryInformationRecorded";

    public static TelehealthApplicantHealthHistoryInformationSnapshot Snapshot(
        Guid allergyInformationId,
        string allergyInformationSnapshotFingerprint,
        Guid medicationInformationId,
        string medicationInformationSnapshotFingerprint,
        string otherHealthHistoryStatus,
        string allergyReviewRoute) => new(
            TelehealthCommandFingerprint.Create(
                "synthetic-applicant-health-history-information-snapshot-v1",
                allergyInformationId,
                allergyInformationSnapshotFingerprint,
                medicationInformationId,
                medicationInformationSnapshotFingerprint,
                otherHealthHistoryStatus,
                allergyReviewRoute));

    public static NormalizedTelehealthApplicantHealthHistoryInformation Normalize(
        RecordTelehealthApplicantHealthHistoryInformationRequest request,
        string inventoryOtherHealthHistoryStatus)
    {
        if (request.ExpectedVersion <= 0)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_health_history_information_version_invalid",
                "ExpectedVersion must be a positive applicant version.");
        }

        var fingerprint = (request.HealthHistoryInformationSnapshotFingerprint ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_health_history_information_fingerprint_invalid",
                "Reload the health-history information context before recording the result.");
        }

        if (!TelehealthApplicantClinicalInformationInventoryPolicy.SupportedCategoryStatuses
            .Contains(inventoryOtherHealthHistoryStatus, StringComparer.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_health_history_information_inventory_conflict",
                "The prior other-health-history inventory status is unavailable.");
        }

        var suppliedTopics = request.HealthHistoryTopics ?? [];
        if (suppliedTopics.Count > SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items.Count)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_health_history_information_topic_limit",
                "The topic selection exceeds the bounded synthetic catalog.");
        }

        var normalizedTopics = new List<SyntheticTelehealthApplicantHealthHistoryTopic>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suppliedTopic in suppliedTopics)
        {
            var catalogTopic = SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Find(
                suppliedTopic.CatalogKey);
            if (catalogTopic is null || !keys.Add(catalogTopic.CatalogKey))
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_health_history_information_topic_invalid",
                    "Select each topic at most once from the current synthetic catalog.");
            }

            normalizedTopics.Add(catalogTopic);
        }

        normalizedTopics.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));

        if (inventoryOtherHealthHistoryStatus == "ItemsToReview")
        {
            if (normalizedTopics.Count == 0 && !request.AdditionalOrUnlistedTopicsReported)
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_health_history_information_topics_required",
                    "Select at least one catalog topic or indicate that additional or unlisted topics exist.");
            }
        }
        else if (normalizedTopics.Count != 0 || request.AdditionalOrUnlistedTopicsReported)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_health_history_information_branch_invalid",
                "The topic selections do not match the prior patient-reported inventory status.");
        }

        if (!request.PatientReportedMayBeIncompleteAcknowledged
            || !request.TopicSelectionIsNotDiagnosisAcknowledged
            || !request.NoStatusOrTimingCapturedAcknowledged
            || !request.ClinicianVerificationRequiredAcknowledged)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_applicant_health_history_information_acknowledgments_required",
                "Confirm every health-history information limitation before continuing.");
        }

        return new(
            request.ExpectedVersion,
            fingerprint,
            normalizedTopics,
            request.AdditionalOrUnlistedTopicsReported,
            request.PatientReportedMayBeIncompleteAcknowledged,
            request.TopicSelectionIsNotDiagnosisAcknowledged,
            request.NoStatusOrTimingCapturedAcknowledged,
            request.ClinicianVerificationRequiredAcknowledged,
            DetermineReviewRoute(
                inventoryOtherHealthHistoryStatus,
                request.AdditionalOrUnlistedTopicsReported));
    }

    public static string DetermineReviewRoute(
        string inventoryOtherHealthHistoryStatus,
        bool additionalOrUnlistedTopicsReported)
    {
        if (additionalOrUnlistedTopicsReported)
        {
            return "AdditionalHealthHistoryCollectionRequired";
        }

        return inventoryOtherHealthHistoryStatus switch
        {
            "ItemsToReview" => "ClinicianHealthHistoryReviewRequired",
            "Unsure" => "AssistedHealthHistoryReviewRequired",
            _ => "PendingClinicianConfirmationOfPatientReportedNone"
        };
    }
}
