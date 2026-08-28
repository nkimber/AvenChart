// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantClinicalInformationInventoryPolicyTests
{
    private const string Fingerprint =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(Request(expectedVersion: 0)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_inventory_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(
                Request(inventorySnapshotFingerprint: fingerprint)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_inventory_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NoKnownAllergies")]
    [InlineData("Aspirin")]
    public void Normalize_rejects_unknown_or_detailed_category_values(string? status)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(
                Request(medicationsStatus: status!)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_inventory_status_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_requires_every_limitation_acknowledgment()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(
                Request(noClinicalDetailsCapturedAcknowledged: false)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_inventory_acknowledgments_required", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone", "PatientReportsNone", "PatientReportsNone", "PendingClinicianReconciliation")]
    [InlineData("Unsure", "PatientReportsNone", "PatientReportsNone", "AssistedReviewRequired")]
    [InlineData("Unsure", "ItemsToReview", "PatientReportsNone", "DetailedCollectionRequired")]
    [InlineData("ItemsToReview", "Unsure", "ItemsToReview", "DetailedCollectionRequired")]
    public void Normalize_applies_the_server_owned_review_route_priority(
        string medications,
        string allergies,
        string history,
        string expectedRoute)
    {
        var normalized = TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(
            Request(medications, allergies, history));

        Assert.Equal(expectedRoute, normalized.ReviewRoute);
    }

    [Fact]
    public void Normalize_canonicalizes_allowed_values_without_collecting_details()
    {
        var normalized = TelehealthApplicantClinicalInformationInventoryPolicy.Normalize(
            Request(" itemsTOreview ", " unsure ", " patientreportsnone "));

        Assert.Equal("ItemsToReview", normalized.MedicationsStatus);
        Assert.Equal("Unsure", normalized.AllergiesOrIntolerancesStatus);
        Assert.Equal("PatientReportsNone", normalized.OtherHealthHistoryStatus);
        Assert.Equal(Fingerprint, normalized.InventorySnapshotFingerprint);
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_immutable_device_provenance()
    {
        var preparationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var first = TelehealthApplicantClinicalInformationInventoryPolicy.Snapshot(
            preparationId, Fingerprint, "Good");
        var replay = TelehealthApplicantClinicalInformationInventoryPolicy.Snapshot(
            preparationId, Fingerprint, "Good");
        var changed = TelehealthApplicantClinicalInformationInventoryPolicy.Snapshot(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), Fingerprint, "Good");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static RecordTelehealthApplicantClinicalInformationInventoryRequest Request(
        string medicationsStatus = "PatientReportsNone",
        string allergiesOrIntolerancesStatus = "PatientReportsNone",
        string otherHealthHistoryStatus = "PatientReportsNone",
        int expectedVersion = 28,
        string inventorySnapshotFingerprint = Fingerprint,
        bool patientReportedMayBeIncompleteAcknowledged = true,
        bool noClinicalDetailsCapturedAcknowledged = true,
        bool clinicianReconciliationRequiredAcknowledged = true) => new(
            expectedVersion,
            inventorySnapshotFingerprint,
            medicationsStatus,
            allergiesOrIntolerancesStatus,
            otherHealthHistoryStatus,
            patientReportedMayBeIncompleteAcknowledged,
            noClinicalDetailsCapturedAcknowledged,
            clinicianReconciliationRequiredAcknowledged);
}
