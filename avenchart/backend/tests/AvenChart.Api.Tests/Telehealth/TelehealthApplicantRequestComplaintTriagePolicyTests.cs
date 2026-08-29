// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestComplaintTriagePolicyTests
{
    [Fact]
    public void Normalizes_exactly_one_complete_migraine_answer_set()
    {
        var normalized = TelehealthApplicantRequestComplaintTriagePolicy.Normalize(ValidMigraine());

        Assert.Equal("migraine", normalized.Answers.ComplaintCategory);
        Assert.NotNull(normalized.Answers.Migraine);
        Assert.Null(normalized.Answers.Sleep);
        Assert.Equal(SyntheticComplaintAnswer.NotSure, normalized.Answers.Migraine.RecentHeadInjury);
    }

    [Fact]
    public void Normalizes_exactly_one_complete_sleep_answer_set()
    {
        var normalized = TelehealthApplicantRequestComplaintTriagePolicy.Normalize(ValidSleep());

        Assert.Equal("sleep", normalized.Answers.ComplaintCategory);
        Assert.Null(normalized.Answers.Migraine);
        Assert.NotNull(normalized.Answers.Sleep);
    }

    [Fact]
    public void Rejects_missing_both_or_multiple_answer_sets()
    {
        var migraine = ValidMigraine();
        Assert.Equal(
            "telehealth_applicant_request_complaint_triage_answer_set_invalid",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestComplaintTriagePolicy.Normalize(
                    migraine with { Migraine = null })).Code);
        Assert.Equal(
            "telehealth_applicant_request_complaint_triage_answer_set_invalid",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestComplaintTriagePolicy.Normalize(
                    migraine with { Sleep = ValidSleep().Sleep })).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Maybe")]
    public void Rejects_missing_or_unrecognized_answers(string? value)
    {
        var request = ValidMigraine() with
        {
            Migraine = ValidMigraine().Migraine! with { SuddenOrWorstOnset = value }
        };

        Assert.Equal(
            "telehealth_applicant_request_complaint_triage_answer_invalid",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestComplaintTriagePolicy.Normalize(request)).Code);
    }

    [Theory]
    [InlineData(TelehealthTriageOutcome.Emergency, "EmergencyRedirected", "EmergencyCareNow", false, false, true)]
    [InlineData(TelehealthTriageOutcome.UrgentInPerson, "InPersonRecommended", "PromptInPersonCare", false, false, true)]
    [InlineData(TelehealthTriageOutcome.InPersonRequired, "InPersonRecommended", "InPersonCareRequired", false, false, true)]
    [InlineData(TelehealthTriageOutcome.Unsupported, "Unsupported", "TelehealthServiceUnsupported", false, false, true)]
    [InlineData(TelehealthTriageOutcome.ClinicalReview, "ClinicalReview", "ClinicalReviewRequired", false, true, false)]
    [InlineData(TelehealthTriageOutcome.TelehealthEligible, "Intake", "SyntheticVideoEvaluationCandidate", true, false, false)]
    public void Maps_every_outcome_without_conferring_downstream_authority(
        TelehealthTriageOutcome outcome,
        string status,
        string disposition,
        bool candidate,
        bool review,
        bool terminal)
    {
        Assert.Equal(status, TelehealthApplicantRequestComplaintTriagePolicy.ResultingRequestStatus(outcome));
        Assert.Equal(disposition, TelehealthApplicantRequestComplaintTriagePolicy.PublicDisposition(outcome));
        Assert.Equal(candidate, TelehealthApplicantRequestComplaintTriagePolicy.SyntheticVideoEvaluationCandidate(outcome));
        Assert.Equal(review, TelehealthApplicantRequestComplaintTriagePolicy.ClinicalReviewRequired(outcome));
        Assert.Equal(terminal, TelehealthApplicantRequestComplaintTriagePolicy.TerminalForTelehealth(outcome));
    }

    [Fact]
    public void Publication_gate_accepts_only_unapproved_synthetic_content()
    {
        var result = new SyntheticTelehealthComplaintTriageEvaluator().Evaluate(
            TelehealthApplicantRequestComplaintTriagePolicy.Normalize(ValidMigraine()).Answers);
        TelehealthApplicantRequestComplaintTriagePolicy.RequirePublicationBlocked(result);

        var altered = result with { ClinicalContentStatus = "APPROVED" };
        Assert.Equal(
            "telehealth_applicant_request_complaint_triage_publication_conflict",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestComplaintTriagePolicy.RequirePublicationBlocked(altered)).Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_changes_with_protocol_category()
    {
        var now = DateTimeOffset.Parse("2026-08-28T12:00:00Z");
        var migraine = Snapshot("migraine", now);
        var replay = Snapshot("migraine", now);
        var sleep = Snapshot("sleep", now);

        Assert.Equal(migraine, replay);
        Assert.NotEqual(migraine.Fingerprint, sleep.Fingerprint);
        Assert.Equal(64, migraine.Fingerprint.Length);
    }

    [Fact]
    public void State_machine_allows_only_the_new_bounded_safety_transitions()
    {
        foreach (var status in new[]
                 {
                     TelehealthRequestStatus.EmergencyRedirected,
                     TelehealthRequestStatus.InPersonRecommended,
                     TelehealthRequestStatus.Unsupported,
                     TelehealthRequestStatus.ClinicalReview,
                     TelehealthRequestStatus.Intake
                 })
        {
            Assert.True(TelehealthRequestStateMachine.CanTransition(
                TelehealthRequestStatus.SafetyScreening,
                status));
        }
        Assert.False(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.SafetyScreening,
            TelehealthRequestStatus.Queued));
    }

    private static TelehealthApplicantRequestComplaintTriageSnapshot Snapshot(
        string category,
        DateTimeOffset now) =>
        TelehealthApplicantRequestComplaintTriagePolicy.Snapshot(
            Guid.Parse("43000000-0000-4000-8000-000000000001"),
            Guid.Parse("43000000-0000-4000-8000-000000000002"),
            Guid.Parse("43000000-0000-4000-8000-000000000003"),
            Guid.Parse("43000000-0000-4000-8000-000000000004"),
            Guid.Parse("43000000-0000-4000-8000-000000000005"),
            Guid.Parse("43000000-0000-4000-8000-000000000006"),
            3,
            category,
            "GA",
            "0101",
            now,
            now.AddMinutes(20),
            now.AddMinutes(30));

    private static EvaluateTelehealthApplicantRequestComplaintTriage ValidMigraine() => new(
        3,
        new string('a', 64),
        "GA",
        true,
        true,
        true,
        new("No", "No", "No", "NotSure", "No", "No", "Yes", "No"),
        null);

    private static EvaluateTelehealthApplicantRequestComplaintTriage ValidSleep() => new(
        3,
        new string('b', 64),
        "CA",
        true,
        true,
        true,
        null,
        new("No", "No", "No", "No", "No", "No", "No", "Yes"));
}
