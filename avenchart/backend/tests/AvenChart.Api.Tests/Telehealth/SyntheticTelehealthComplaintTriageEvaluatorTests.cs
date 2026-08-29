// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthComplaintTriageEvaluatorTests
{
    private readonly SyntheticTelehealthComplaintTriageEvaluator _evaluator = new();

    [Fact]
    public void Migraine_candidate_is_eligible_only_for_a_known_pattern_without_other_rules()
    {
        var result = _evaluator.Evaluate(new("migraine", Migraine(), null));

        Assert.Equal(TelehealthTriageOutcome.TelehealthEligible, result.Outcome);
        Assert.Equal(["MIG-CANDIDATE-KNOWN-SIMILAR-PATTERN"], result.FiredRuleCodes);
        Assert.Equal(["MIGRAINE_SYNTHETIC_CANDIDATE"], result.ReasonCodes);
        Assert.Equal(SyntheticTelehealthComplaintTriageEvaluator.MigraineProtocolId, result.ProtocolId);
        Assert.Equal(SyntheticTelehealthComplaintTriageEvaluator.MigraineProtocolContentHash, result.ProtocolContentHash);
        Assert.Equal("UNAPPROVED_SYNTHETIC", result.ClinicalContentStatus);
    }

    [Theory]
    [InlineData("sudden", TelehealthTriageOutcome.Emergency, "MIG-EMERGENCY-SUDDEN-WORST")]
    [InlineData("neurologic", TelehealthTriageOutcome.Emergency, "MIG-EMERGENCY-NEURO-VISION")]
    [InlineData("fever", TelehealthTriageOutcome.UrgentInPerson, "MIG-URGENT-FEVER-STIFF-NECK")]
    [InlineData("injury", TelehealthTriageOutcome.UrgentInPerson, "MIG-URGENT-HEAD-INJURY")]
    [InlineData("vomiting", TelehealthTriageOutcome.UrgentInPerson, "MIG-URGENT-PERSISTENT-VOMITING")]
    [InlineData("pregnancy", TelehealthTriageOutcome.ClinicalReview, "MIG-REVIEW-PREGNANCY-POSTPARTUM")]
    [InlineData("cancer", TelehealthTriageOutcome.ClinicalReview, "MIG-REVIEW-CANCER-IMMUNOCOMPROMISED")]
    [InlineData("different", TelehealthTriageOutcome.ClinicalReview, "MIG-REVIEW-NEW-OR-DIFFERENT-PATTERN")]
    [InlineData("unknown", TelehealthTriageOutcome.ClinicalReview, "MIG-REVIEW-UNKNOWN-ANSWER")]
    public void Migraine_rules_map_to_the_expected_protective_outcome(
        string scenario,
        TelehealthTriageOutcome outcome,
        string firstRule)
    {
        var answers = scenario switch
        {
            "sudden" => Migraine(sudden: SyntheticComplaintAnswer.Yes),
            "neurologic" => Migraine(neurologic: SyntheticComplaintAnswer.Yes),
            "fever" => Migraine(fever: SyntheticComplaintAnswer.Yes),
            "injury" => Migraine(injury: SyntheticComplaintAnswer.Yes),
            "vomiting" => Migraine(vomiting: SyntheticComplaintAnswer.Yes),
            "pregnancy" => Migraine(pregnancy: SyntheticComplaintAnswer.Yes),
            "cancer" => Migraine(cancer: SyntheticComplaintAnswer.Yes),
            "different" => Migraine(known: SyntheticComplaintAnswer.No),
            "unknown" => Migraine(injury: SyntheticComplaintAnswer.NotSure),
            _ => throw new InvalidOperationException()
        };

        var result = _evaluator.Evaluate(new("migraine", answers, null));

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(firstRule, result.FiredRuleCodes[0]);
    }

    [Fact]
    public void Migraine_records_every_fired_rule_in_stable_priority_order()
    {
        var result = _evaluator.Evaluate(new(
            "migraine",
            Migraine(
                sudden: SyntheticComplaintAnswer.Yes,
                neurologic: SyntheticComplaintAnswer.Yes,
                fever: SyntheticComplaintAnswer.Yes,
                injury: SyntheticComplaintAnswer.NotSure,
                pregnancy: SyntheticComplaintAnswer.Yes,
                cancer: SyntheticComplaintAnswer.Yes,
                known: SyntheticComplaintAnswer.No,
                vomiting: SyntheticComplaintAnswer.Yes),
            null));

        Assert.Equal(TelehealthTriageOutcome.Emergency, result.Outcome);
        Assert.Equal(
        [
            "MIG-EMERGENCY-SUDDEN-WORST",
            "MIG-EMERGENCY-NEURO-VISION",
            "MIG-URGENT-FEVER-STIFF-NECK",
            "MIG-URGENT-PERSISTENT-VOMITING",
            "MIG-REVIEW-PREGNANCY-POSTPARTUM",
            "MIG-REVIEW-CANCER-IMMUNOCOMPROMISED",
            "MIG-REVIEW-UNKNOWN-ANSWER",
            "MIG-REVIEW-NEW-OR-DIFFERENT-PATTERN"
        ], result.FiredRuleCodes);
    }

    [Fact]
    public void Sleep_candidate_is_eligible_only_for_uncomplicated_sleep_difficulty()
    {
        var result = _evaluator.Evaluate(new("sleep", null, Sleep()));

        Assert.Equal(TelehealthTriageOutcome.TelehealthEligible, result.Outcome);
        Assert.Equal(["SLP-CANDIDATE-UNCOMPLICATED-SLEEP-DIFFICULTY"], result.FiredRuleCodes);
        Assert.Equal(SyntheticTelehealthComplaintTriageEvaluator.SleepProtocolId, result.ProtocolId);
        Assert.Equal("UNAPPROVED_SYNTHETIC", result.ClinicalContentStatus);
    }

    [Theory]
    [InlineData("selfHarm", TelehealthTriageOutcome.Emergency, "SLP-EMERGENCY-SELF-HARM")]
    [InlineData("mania", TelehealthTriageOutcome.UrgentInPerson, "SLP-URGENT-MANIA-PSYCHOSIS")]
    [InlineData("withdrawal", TelehealthTriageOutcome.UrgentInPerson, "SLP-URGENT-WITHDRAWAL")]
    [InlineData("somnolence", TelehealthTriageOutcome.UrgentInPerson, "SLP-URGENT-DANGEROUS-SOMNOLENCE")]
    [InlineData("breathing", TelehealthTriageOutcome.InPersonRequired, "SLP-INPERSON-BREATHING-DISORDER")]
    [InlineData("pregnancy", TelehealthTriageOutcome.ClinicalReview, "SLP-REVIEW-PREGNANCY-COMPLEX-MEDS")]
    [InlineData("unknown", TelehealthTriageOutcome.ClinicalReview, "SLP-REVIEW-UNKNOWN-ANSWER")]
    [InlineData("controlled", TelehealthTriageOutcome.Unsupported, "SLP-UNSUPPORTED-CONTROLLED-SEDATIVE")]
    [InlineData("complex", TelehealthTriageOutcome.ClinicalReview, "SLP-REVIEW-COMPLEX-PRESENTATION")]
    public void Sleep_rules_map_to_the_expected_protective_outcome(
        string scenario,
        TelehealthTriageOutcome outcome,
        string firstRule)
    {
        var answers = scenario switch
        {
            "selfHarm" => Sleep(selfHarm: SyntheticComplaintAnswer.Yes),
            "mania" => Sleep(mania: SyntheticComplaintAnswer.Yes),
            "withdrawal" => Sleep(withdrawal: SyntheticComplaintAnswer.Yes),
            "somnolence" => Sleep(somnolence: SyntheticComplaintAnswer.Yes),
            "breathing" => Sleep(breathing: SyntheticComplaintAnswer.Yes),
            "pregnancy" => Sleep(pregnancy: SyntheticComplaintAnswer.Yes),
            "unknown" => Sleep(breathing: SyntheticComplaintAnswer.NotSure),
            "controlled" => Sleep(controlled: SyntheticComplaintAnswer.Yes),
            "complex" => Sleep(uncomplicated: SyntheticComplaintAnswer.No),
            _ => throw new InvalidOperationException()
        };

        var result = _evaluator.Evaluate(new("sleep", null, answers));

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(firstRule, result.FiredRuleCodes[0]);
    }

    [Fact]
    public void Same_answers_replay_bit_for_bit()
    {
        var answers = new SyntheticComplaintTriageAnswers(
            "sleep",
            null,
            Sleep(breathing: SyntheticComplaintAnswer.NotSure));

        var first = _evaluator.Evaluate(answers);
        var replay = _evaluator.Evaluate(answers);

        Assert.Equal(first.ProtocolId, replay.ProtocolId);
        Assert.Equal(first.ProtocolKey, replay.ProtocolKey);
        Assert.Equal(first.ProtocolVersion, replay.ProtocolVersion);
        Assert.Equal(first.ProtocolContentHash, replay.ProtocolContentHash);
        Assert.Equal(first.EngineVersion, replay.EngineVersion);
        Assert.Equal(first.ClinicalContentStatus, replay.ClinicalContentStatus);
        Assert.Equal(first.Outcome, replay.Outcome);
        Assert.Equal(first.AnswerFingerprint, replay.AnswerFingerprint);
        Assert.Equal(first.FiredRuleCodes, replay.FiredRuleCodes);
        Assert.Equal(first.ReasonCodes, replay.ReasonCodes);
        Assert.Equal(64, first.AnswerFingerprint.Length);
        Assert.Equal(64, first.ProtocolContentHash.Length);
    }

    [Fact]
    public void Category_and_answer_set_must_match_exactly()
    {
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(new("migraine", null, Sleep())));
        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(new("sleep", Migraine(), Sleep())));
    }

    private static SyntheticMigraineComplaintTriageAnswers Migraine(
        SyntheticComplaintAnswer sudden = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer neurologic = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer fever = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer injury = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer pregnancy = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer cancer = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer known = SyntheticComplaintAnswer.Yes,
        SyntheticComplaintAnswer vomiting = SyntheticComplaintAnswer.No) =>
        new(sudden, neurologic, fever, injury, pregnancy, cancer, known, vomiting);

    private static SyntheticSleepComplaintTriageAnswers Sleep(
        SyntheticComplaintAnswer selfHarm = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer mania = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer somnolence = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer withdrawal = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer breathing = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer pregnancy = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer controlled = SyntheticComplaintAnswer.No,
        SyntheticComplaintAnswer uncomplicated = SyntheticComplaintAnswer.Yes) =>
        new(selfHarm, mania, somnolence, withdrawal, breathing, pregnancy, controlled, uncomplicated);
}
