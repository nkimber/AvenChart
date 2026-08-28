// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantAllergyInformationPolicyTests
{
    private const string Fingerprint =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Catalog_is_fixed_local_incomplete_and_not_externally_mapped()
    {
        Assert.Equal(6, SyntheticTelehealthApplicantAllergyCatalog.Items.Count);
        Assert.Equal(
            ["amoxicillin", "ibuprofen", "peanut", "shellfish", "latex", "bee-venom"],
            SyntheticTelehealthApplicantAllergyCatalog.Items.Select(item => item.CatalogKey));
        Assert.Equal(
            ["Medication", "Medication", "Food", "Food", "Environment", "Environment"],
            SyntheticTelehealthApplicantAllergyCatalog.Items.Select(item => item.Category));
        Assert.All(SyntheticTelehealthApplicantAllergyCatalog.Items, item =>
        {
            Assert.Equal(1, item.CatalogVersion);
            Assert.Equal("LOCAL_SYNTHETIC_ONLY", item.CodingSystem);
            Assert.False(item.SnomedCtMapped);
            Assert.False(item.RxNormMapped);
        });
    }

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request(expectedVersion: 0), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_allergy_information_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request(fingerprint: fingerprint), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_allergy_information_fingerprint_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_and_sorts_server_catalog_items()
    {
        var normalized = TelehealthApplicantAllergyInformationPolicy.Normalize(
            Request(items: [new(" LATEX "), new("Peanut")]),
            "ItemsToReview");

        Assert.Equal(["peanut", "latex"],
            normalized.AllergyItems.Select(item => item.CatalogKey));
    }

    [Theory]
    [InlineData("PatientReportsNone", false, "PendingClinicianConfirmationOfPatientReportedNone")]
    [InlineData("Unsure", false, "AssistedAllergyReviewRequired")]
    [InlineData("ItemsToReview", false, "ClinicianAllergyReviewRequired")]
    [InlineData("ItemsToReview", true, "AdditionalAllergyCollectionRequired")]
    public void Normalize_applies_server_owned_review_routes(
        string inventoryStatus,
        bool additional,
        string expectedRoute)
    {
        var items = inventoryStatus == "ItemsToReview" && !additional
            ? new[] { new RecordTelehealthApplicantAllergyItemRequest("amoxicillin") }
            : [];
        var normalized = TelehealthApplicantAllergyInformationPolicy.Normalize(
            Request(items, additional), inventoryStatus);

        Assert.Equal(expectedRoute, normalized.ReviewRoute);
    }

    [Fact]
    public void Items_to_review_requires_a_selection_or_additional_item_signal()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(Request(), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_allergy_information_items_required", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_item_branches_reject_catalog_items(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request([new("ibuprofen")]), inventoryStatus));

        Assert.Equal("telehealth_applicant_allergy_information_branch_invalid", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_item_branches_reject_additional_item_signal(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request(additional: true), inventoryStatus));

        Assert.Equal("telehealth_applicant_allergy_information_branch_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_unknown_catalog_items()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request([new("unknown")]), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_allergy_information_item_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_duplicate_catalog_items()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request([new("latex"), new("LATEX")]), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_allergy_information_item_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_more_items_than_the_catalog()
    {
        var items = Enumerable.Range(0, 7)
            .Select(index => new RecordTelehealthApplicantAllergyItemRequest($"unknown-{index}"))
            .ToArray();
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(Request(items), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_allergy_information_item_limit", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool reported,
        bool catalog,
        bool noReaction,
        bool verify)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantAllergyInformationPolicy.Normalize(
                Request(
                    [new("bee-venom")],
                    reportedAcknowledged: reported,
                    catalogAcknowledged: catalog,
                    noReactionAcknowledged: noReaction,
                    verificationAcknowledged: verify),
                "ItemsToReview"));

        Assert.Equal("telehealth_applicant_allergy_information_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_upstream_provenance()
    {
        var medicationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var first = TelehealthApplicantAllergyInformationPolicy.Snapshot(
            medicationId, Fingerprint, "ItemsToReview", "ClinicianMedicationReviewRequired");
        var replay = TelehealthApplicantAllergyInformationPolicy.Snapshot(
            medicationId, Fingerprint, "ItemsToReview", "ClinicianMedicationReviewRequired");
        var changed = TelehealthApplicantAllergyInformationPolicy.Snapshot(
            medicationId, Fingerprint, "Unsure", "AssistedMedicationReviewRequired");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static RecordTelehealthApplicantAllergyInformationRequest Request(
        IReadOnlyList<RecordTelehealthApplicantAllergyItemRequest>? items = null,
        bool additional = false,
        int expectedVersion = 30,
        string fingerprint = Fingerprint,
        bool reportedAcknowledged = true,
        bool catalogAcknowledged = true,
        bool noReactionAcknowledged = true,
        bool verificationAcknowledged = true) => new(
            expectedVersion,
            fingerprint,
            items,
            additional,
            reportedAcknowledged,
            catalogAcknowledged,
            noReactionAcknowledged,
            verificationAcknowledged);
}
