// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantClinicalInformationSummaryPolicyTests
{
    private const string Fingerprint =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationSummaryPolicy.Normalize(Request(expectedVersion: 0)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_summary_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationSummaryPolicy.Normalize(
                Request(fingerprint: fingerprint)));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_clinical_information_summary_fingerprint_invalid", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool patientReported,
        bool notVerified,
        bool notComplete,
        bool correction)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantClinicalInformationSummaryPolicy.Normalize(Request(
                patientReportedAcknowledged: patientReported,
                notVerifiedAcknowledged: notVerified,
                notCompleteAcknowledged: notComplete,
                correctionAcknowledged: correction)));

        Assert.Equal(
            "telehealth_applicant_clinical_information_summary_acknowledgments_required",
            problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_the_fingerprint_without_accepting_summary_content()
    {
        var normalized = TelehealthApplicantClinicalInformationSummaryPolicy.Normalize(
            Request(fingerprint: Fingerprint.ToUpperInvariant()));

        Assert.Equal(33, normalized.ExpectedVersion);
        Assert.Equal(Fingerprint, normalized.ClinicalInformationSummarySnapshotFingerprint);
    }

    [Theory]
    [InlineData("PatientReportsNone", "PatientReportsNone", "PatientReportsNone",
        false, false, false, "PendingClinicianReconciliationOfPatientReportedNone")]
    [InlineData("ItemsToReview", "PatientReportsNone", "PatientReportsNone",
        false, false, false, "ClinicianClinicalInformationReviewRequired")]
    [InlineData("PatientReportsNone", "Unsure", "PatientReportsNone",
        false, false, false, "AssistedClinicalInformationReviewRequired")]
    [InlineData("Unsure", "ItemsToReview", "PatientReportsNone",
        false, false, false, "AssistedClinicalInformationReviewRequired")]
    [InlineData("PatientReportsNone", "PatientReportsNone", "PatientReportsNone",
        true, false, false, "AdditionalClinicalInformationCollectionRequired")]
    [InlineData("Unsure", "Unsure", "Unsure",
        false, true, false, "AdditionalClinicalInformationCollectionRequired")]
    [InlineData("ItemsToReview", "ItemsToReview", "ItemsToReview",
        false, false, true, "AdditionalClinicalInformationCollectionRequired")]
    public void Route_priority_is_server_owned(
        string medications,
        string allergies,
        string history,
        bool additionalMedications,
        bool additionalAllergies,
        bool additionalHistory,
        string expected)
    {
        Assert.Equal(expected,
            TelehealthApplicantClinicalInformationSummaryPolicy.DetermineSummaryRoute(
                medications,
                allergies,
                history,
                additionalMedications,
                additionalAllergies,
                additionalHistory));
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_source_provenance()
    {
        var first = Snapshot(medicationItemCount: 1);
        var replay = Snapshot(medicationItemCount: 1);
        var changed = Snapshot(medicationItemCount: 2);

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static TelehealthApplicantClinicalInformationSummarySnapshot Snapshot(
        int medicationItemCount) =>
        TelehealthApplicantClinicalInformationSummaryPolicy.Snapshot(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "ItemsToReview",
            "Unsure",
            "PatientReportsNone",
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Fingerprint,
            medicationItemCount,
            false,
            "ClinicianMedicationReviewRequired",
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Fingerprint,
            0,
            false,
            "AssistedAllergyReviewRequired",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            Fingerprint,
            0,
            false,
            "PendingClinicianConfirmationOfPatientReportedNone");

    private static ConfirmTelehealthApplicantClinicalInformationSummaryRequest Request(
        int expectedVersion = 33,
        string fingerprint = Fingerprint,
        bool patientReportedAcknowledged = true,
        bool notVerifiedAcknowledged = true,
        bool notCompleteAcknowledged = true,
        bool correctionAcknowledged = true) => new(
            expectedVersion,
            fingerprint,
            patientReportedAcknowledged,
            notVerifiedAcknowledged,
            notCompleteAcknowledged,
            correctionAcknowledged);
}
