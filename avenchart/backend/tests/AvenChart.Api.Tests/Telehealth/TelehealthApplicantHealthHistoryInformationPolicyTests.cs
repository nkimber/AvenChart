// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantHealthHistoryInformationPolicyTests
{
    private const string Fingerprint =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Catalog_is_fixed_local_incomplete_and_not_externally_mapped()
    {
        Assert.Equal(6, SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items.Count);
        Assert.Equal(
            ["ongoing-health-conditions", "prior-surgery-or-hospital-stay",
                "pregnancy-or-postpartum-information", "immune-system-or-active-cancer-treatment",
                "behavioral-health-or-substance-use", "family-health-history"],
            SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items.Select(topic => topic.CatalogKey));
        Assert.Equal(
            ["ConditionOrConcern", "ProcedureOrHospitalization", "HealthStatus", "RiskContext",
                "SensitiveHistory", "FamilyHistory"],
            SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items.Select(topic => topic.Category));
        Assert.All(SyntheticTelehealthApplicantHealthHistoryTopicCatalog.Items, topic =>
        {
            Assert.Equal(1, topic.CatalogVersion);
            Assert.Equal("LOCAL_SYNTHETIC_ONLY", topic.CodingSystem);
            Assert.False(topic.SnomedCtMapped);
            Assert.False(topic.Icd10CmMapped);
            Assert.False(topic.LoincMapped);
        });
    }

    [Fact]
    public void Normalize_requires_a_positive_version()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request(expectedVersion: 0), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_health_history_information_version_invalid", problem.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Normalize_rejects_an_invalid_snapshot_fingerprint(string fingerprint)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request(fingerprint: fingerprint), "ItemsToReview"));

        Assert.Equal(400, problem.StatusCode);
        Assert.Equal("telehealth_applicant_health_history_information_fingerprint_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_canonicalizes_and_sorts_server_catalog_topics()
    {
        var normalized = TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
            Request(topics: [new(" FAMILY-HEALTH-HISTORY "), new("Ongoing-Health-Conditions")]),
            "ItemsToReview");

        Assert.Equal(["ongoing-health-conditions", "family-health-history"],
            normalized.HealthHistoryTopics.Select(topic => topic.CatalogKey));
    }

    [Theory]
    [InlineData("PatientReportsNone", false, "PendingClinicianConfirmationOfPatientReportedNone")]
    [InlineData("Unsure", false, "AssistedHealthHistoryReviewRequired")]
    [InlineData("ItemsToReview", false, "ClinicianHealthHistoryReviewRequired")]
    [InlineData("ItemsToReview", true, "AdditionalHealthHistoryCollectionRequired")]
    public void Normalize_applies_server_owned_review_routes(
        string inventoryStatus,
        bool additional,
        string expectedRoute)
    {
        var topics = inventoryStatus == "ItemsToReview" && !additional
            ? new[] { new RecordTelehealthApplicantHealthHistoryTopicRequest("ongoing-health-conditions") }
            : [];
        var normalized = TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
            Request(topics, additional), inventoryStatus);

        Assert.Equal(expectedRoute, normalized.ReviewRoute);
    }

    [Fact]
    public void Topics_to_review_requires_a_selection_or_additional_topic_signal()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(Request(), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_health_history_information_topics_required", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_topic_branches_reject_catalog_topics(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request([new("family-health-history")]), inventoryStatus));

        Assert.Equal("telehealth_applicant_health_history_information_branch_invalid", problem.Code);
    }

    [Theory]
    [InlineData("PatientReportsNone")]
    [InlineData("Unsure")]
    public void Non_topic_branches_reject_additional_topic_signal(string inventoryStatus)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request(additional: true), inventoryStatus));

        Assert.Equal("telehealth_applicant_health_history_information_branch_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_unknown_catalog_topics()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request([new("unknown")]), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_health_history_information_topic_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_duplicate_catalog_topics()
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request([new("family-health-history"), new("FAMILY-HEALTH-HISTORY")]), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_health_history_information_topic_invalid", problem.Code);
    }

    [Fact]
    public void Normalize_rejects_more_topics_than_the_catalog()
    {
        var topics = Enumerable.Range(0, 7)
            .Select(index => new RecordTelehealthApplicantHealthHistoryTopicRequest($"unknown-{index}"))
            .ToArray();
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(Request(topics), "ItemsToReview"));

        Assert.Equal("telehealth_applicant_health_history_information_topic_limit", problem.Code);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void Normalize_requires_all_four_acknowledgments(
        bool reported,
        bool topicNotDiagnosis,
        bool noStatusOrTiming,
        bool verify)
    {
        var problem = Assert.Throws<TelehealthProblem>(() =>
            TelehealthApplicantHealthHistoryInformationPolicy.Normalize(
                Request(
                    [new("pregnancy-or-postpartum-information")],
                    reportedAcknowledged: reported,
                    topicNotDiagnosisAcknowledged: topicNotDiagnosis,
                    noStatusOrTimingAcknowledged: noStatusOrTiming,
                    verificationAcknowledged: verify),
                "ItemsToReview"));

        Assert.Equal("telehealth_applicant_health_history_information_acknowledgments_required", problem.Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_upstream_provenance()
    {
        var allergyId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var medicationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var first = TelehealthApplicantHealthHistoryInformationPolicy.Snapshot(
            allergyId, Fingerprint, medicationId, Fingerprint,
            "ItemsToReview", "ClinicianAllergyReviewRequired");
        var replay = TelehealthApplicantHealthHistoryInformationPolicy.Snapshot(
            allergyId, Fingerprint, medicationId, Fingerprint,
            "ItemsToReview", "ClinicianAllergyReviewRequired");
        var changed = TelehealthApplicantHealthHistoryInformationPolicy.Snapshot(
            allergyId, Fingerprint, medicationId, Fingerprint,
            "Unsure", "AssistedAllergyReviewRequired");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first.Fingerprint);
    }

    private static RecordTelehealthApplicantHealthHistoryInformationRequest Request(
        IReadOnlyList<RecordTelehealthApplicantHealthHistoryTopicRequest>? topics = null,
        bool additional = false,
        int expectedVersion = 30,
        string fingerprint = Fingerprint,
        bool reportedAcknowledged = true,
        bool topicNotDiagnosisAcknowledged = true,
        bool noStatusOrTimingAcknowledged = true,
        bool verificationAcknowledged = true) => new(
            expectedVersion,
            fingerprint,
            topics,
            additional,
            reportedAcknowledged,
            topicNotDiagnosisAcknowledged,
            noStatusOrTimingAcknowledged,
            verificationAcknowledged);
}

