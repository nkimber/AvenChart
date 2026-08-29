// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthApplicantRequestIntakePolicyTests
{
    [Theory]
    [InlineData("less-than-day")]
    [InlineData("1-3-days")]
    [InlineData("4-14-days")]
    [InlineData("more-than-14-days")]
    public void Normalizes_each_controlled_duration(string duration)
    {
        var normalized = TelehealthApplicantRequestIntakePolicy.Normalize(Valid() with
        {
            SymptomDuration = duration.ToUpperInvariant()
        });

        Assert.Equal(duration, normalized.SymptomDuration);
        Assert.Equal("GA", normalized.CurrentLocationStateCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Rejects_each_missing_confirmation(int index)
    {
        var values = Enumerable.Repeat(true, 8).ToArray();
        values[index] = false;
        var request = Valid() with
        {
            CurrentLocationConfirmed = values[0],
            CallbackNumberConfirmed = values[1],
            PriorInformationReviewed = values[2],
            InsuranceLimitationsAcknowledged = values[3],
            PendingConsentAcknowledged = values[4],
            PendingVerificationAcknowledged = values[5],
            ComplaintResultAcknowledged = values[6],
            SyntheticDataConfirmed = values[7]
        };

        Assert.Equal(
            "telehealth_applicant_request_intake_confirmations_required",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestIntakePolicy.Normalize(request)).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("today")]
    [InlineData("1-30-days")]
    public void Rejects_uncontrolled_duration(string duration)
    {
        Assert.Equal(
            "telehealth_applicant_request_intake_duration_invalid",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestIntakePolicy.Normalize(Valid() with
                {
                    SymptomDuration = duration
                })).Code);
    }

    [Fact]
    public void Derives_fixed_summary_for_each_category()
    {
        Assert.Equal(
            "Synthetic migraine intake demonstration",
            TelehealthApplicantRequestIntakePolicy.ComplaintSummary("migraine"));
        Assert.Equal(
            "Synthetic sleep intake demonstration",
            TelehealthApplicantRequestIntakePolicy.ComplaintSummary("sleep"));
        Assert.Equal(
            "telehealth_applicant_request_intake_category_conflict",
            Assert.Throws<TelehealthProblem>(() =>
                TelehealthApplicantRequestIntakePolicy.ComplaintSummary("other")).Code);
    }

    [Fact]
    public void Snapshot_is_stable_and_binds_governing_category()
    {
        var migraine = Snapshot("migraine");
        var replay = Snapshot("migraine");
        var sleep = Snapshot("sleep");

        Assert.Equal(migraine, replay);
        Assert.NotEqual(migraine.Fingerprint, sleep.Fingerprint);
        Assert.Equal(64, migraine.Fingerprint.Length);
        Assert.Equal("Synthetic migraine intake demonstration", migraine.ComplaintSummary);
    }

    [Fact]
    public void State_machine_allows_only_the_bounded_intake_transition()
    {
        Assert.True(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Intake,
            TelehealthRequestStatus.Verification));
        Assert.False(TelehealthRequestStateMachine.CanTransition(
            TelehealthRequestStatus.Intake,
            TelehealthRequestStatus.Queued));
    }

    private static ConfirmTelehealthApplicantRequestIntake Valid() => new(
        4,
        new string('a', 64),
        "ga",
        "1-3-days",
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        true);

    private static TelehealthApplicantRequestIntakeSnapshot Snapshot(string category)
    {
        var now = DateTimeOffset.Parse("2026-08-28T12:00:00Z");
        return TelehealthApplicantRequestIntakePolicy.Snapshot(
            Guid.Parse("44000000-0000-4000-8000-000000000001"),
            Guid.Parse("44000000-0000-4000-8000-000000000002"),
            Guid.Parse("44000000-0000-4000-8000-000000000003"),
            Guid.Parse("44000000-0000-4000-8000-000000000004"),
            Guid.Parse("44000000-0000-4000-8000-000000000005"),
            Guid.Parse("44000000-0000-4000-8000-000000000006"),
            Guid.Parse("44000000-0000-4000-8000-000000000007"),
            Guid.Parse("44000000-0000-4000-8000-000000000008"),
            Guid.Parse("44000000-0000-4000-8000-000000000009"),
            Guid.Parse("44000000-0000-4000-8000-000000000010"),
            4,
            category,
            "TelehealthEligible",
            "GA",
            "0101",
            now,
            now.AddMinutes(1),
            now.AddMinutes(20),
            now.AddMinutes(30),
            category == "migraine" ? "synthetic-migraine-complaint-triage" : "synthetic-sleep-complaint-triage",
            1,
            new string('b', 64),
            "UNAPPROVED_SYNTHETIC",
            false,
            false,
            false,
            new string('c', 64),
            new string('d', 64));
    }
}
