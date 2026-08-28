// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantMedicationInformationPolicyTests
{
    private const string Fingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Catalog_is_fixed_local_incomplete_and_not_rxnorm_mapped()
    {
        Assert.Equal(6, SyntheticTelehealthApplicantMedicationCatalog.Items.Count);
        Assert.Equal(
            ["acetaminophen", "ibuprofen", "sumatriptan", "melatonin", "lisinopril", "metformin"],
            SyntheticTelehealthApplicantMedicationCatalog.Items.Select(item => item.CatalogKey));
        Assert.All(SyntheticTelehealthApplicantMedicationCatalog.Items, item =>
        {
            Assert.Equal(1, item.CatalogVersion);
            Assert.Equal("LOCAL_SYNTHETIC_ONLY", item.CodingSystem);
            Assert.False(item.RxNormMapped);
        });
    }

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request(expectedVersion: 0), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_medication_information_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request(fingerprint: fingerprint), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_medication_information_fingerprint_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_and_sorts_server_catalog_items()
    {
        var normalized = TelehealthApplicantMedicationInformationPolicy.Normalize(
            Request(items:
            [
                new(" MELATONIN ", " unsure "),
                new("Sumatriptan", "taking")
            ]),
            "ItemsToReview");

        Assert.Equal(["sumatriptan", "melatonin"],
            normalized.MedicationItems.Select(item => item.CatalogItem.CatalogKey));
        Assert.Equal(["Taking", "Unsure"],
            normalized.MedicationItems.Select(item => item.ReportedUseStatus));
    }

    [Theory]
    [InlineData("PatientReportsNone", false, "PendingClinicianConfirmationOfNone")]
    [InlineData("Unsure", false, "AssistedMedicationReviewRequired")]
    [InlineData("ItemsToReview", false, "ClinicianMedicationReviewRequired")]
    [InlineData("ItemsToReview", true, "AdditionalMedicationCollectionRequired")]
    public void Normalize_applies_server_owned_review_routes(
        string inventoryStatus,
        bool additional,
        string expectedRoute)
    {
        var items = inventoryStatus == "ItemsToReview" && !additional
            ? new[] { new RecordTelehealthApplicantMedicationItemRequest("ibuprofen", "Taking") }
            : [];
        var normalized = TelehealthApplicantMedicationInformationPolicy.Normalize(
            Request(items, additional), inventoryStatus);

        Assert.Equal(expectedRoute, normalized.ReviewRoute);
    }

    [Fact]
    public void Items_to_review_requires_a_selection_or_additional_item_signal()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(Request(), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_medication_information_items_required", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_item_branches_reject_catalog_items(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request([new("ibuprofen", "Taking")]), inventoryStatus));

        Assert.Equal("telehealth_applicant_medication_information_branch_invalid", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_item_branches_reject_additional_item_signal(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request(additional: true), inventoryStatus));

        Assert.Equal("telehealth_applicant_medication_information_branch_invalid", problem.Code);
    }

    [Theory]
    [InlineData("unknown", "Taking")]
    [InlineData("ibuprofen", "Daily")]
    public void Normalize_rejects_unknown_catalog_or_use_values(string key, string useStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request([new(key, useStatus)]), "ItemsToReview"));

        Assert.Contains(problem.Code,
            new[]
            {
                "telehealth_applicant_medication_information_item_invalid",
                "telehealth_applicant_medication_information_use_status_invalid"
            });
    }

    [Fact]
    public void Normalize_rejects_duplicate_catalog_items()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request([new("ibuprofen", "Taking"), new("IBUPROFEN", "Unsure")]),
                "ItemsToReview"));

        Assert.Equal("telehealth_applicant_medication_information_item_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_more_items_than_the_catalog()
    {
        var items = Enumerable.Range(0, 7)
            .Select(index => new RecordTelehealthApplicantMedicationItemRequest($"unknown-{index}", "Taking"))
            .ToArray();
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(Request(items), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_medication_information_item_limit", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool reported,
        bool catalog,
        bool noDose,
        bool reconcile)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantMedicationInformationPolicy.Normalize(
                Request(
                    [new("metformin", "NotTaking")],
                    reportedAcknowledged: reported,
                    catalogAcknowledged: catalog,
                    noDoseAcknowledged: noDose,
                    reconciliationAcknowledged: reconcile),
                "ItemsToReview"));

        Assert.Equal("telehealth_applicant_medication_information_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_inventory_provenance()
    {
        var inventoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var first = TelehealthApplicantMedicationInformationPolicy.Snapshot(
            inventoryId, Fingerprint, "ItemsToReview", "DetailedCollectionRequired");
        var replay = TelehealthApplicantMedicationInformationPolicy.Snapshot(
            inventoryId, Fingerprint, "ItemsToReview", "DetailedCollectionRequired");
        var changed = TelehealthApplicantMedicationInformationPolicy.Snapshot(
            inventoryId, Fingerprint, "Unsure", "AssistedReviewRequired");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static RecordTelehealthApplicantMedicationInformationRequest Request(
        IReadOnlyList<RecordTelehealthApplicantMedicationItemRequest>? items = null,
        bool additional = false,
        int expectedVersion = 29,
        string fingerprint = Fingerprint,
        bool reportedAcknowledged = true,
        bool catalogAcknowledged = true,
        bool noDoseAcknowledged = true,
        bool reconciliationAcknowledged = true) => new(
            expectedVersion,
            fingerprint,
            items,
            additional,
            reportedAcknowledged,
            catalogAcknowledged,
            noDoseAcknowledged,
            reconciliationAcknowledged);
}
