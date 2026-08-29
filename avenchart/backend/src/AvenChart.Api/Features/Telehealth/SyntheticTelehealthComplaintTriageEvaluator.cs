// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public enum SyntheticComplaintAnswer
{
    Yes,
    No,
    NotSure
}

public sealed record SyntheticMigraineComplaintTriageAnswers(
    SyntheticComplaintAnswer SuddenOrWorstOnset,
    SyntheticComplaintAnswer NewNeurologicOrVisionChange,
    SyntheticComplaintAnswer FeverOrStiffNeck,
    SyntheticComplaintAnswer RecentHeadInjury,
    SyntheticComplaintAnswer PregnantOrPostpartum,
    SyntheticComplaintAnswer CancerOrImmunocompromised,
    SyntheticComplaintAnswer KnownSimilarPattern,
    SyntheticComplaintAnswer PersistentVomiting);

public sealed record SyntheticSleepComplaintTriageAnswers(
    SyntheticComplaintAnswer SelfHarmThoughts,
    SyntheticComplaintAnswer ManiaOrPsychosis,
    SyntheticComplaintAnswer DangerousSomnolence,
    SyntheticComplaintAnswer WithdrawalConcern,
    SyntheticComplaintAnswer BreathingPausesOrSevereSnoring,
    SyntheticComplaintAnswer PregnantOrComplexMedicationConcern,
    SyntheticComplaintAnswer ControlledSedativeRequest,
    SyntheticComplaintAnswer UncomplicatedSleepDifficulty);

public sealed record SyntheticComplaintTriageAnswers(
    string ComplaintCategory,
    SyntheticMigraineComplaintTriageAnswers? Migraine,
    SyntheticSleepComplaintTriageAnswers? Sleep);

public sealed record SyntheticComplaintTriageResult(
    Guid ProtocolId,
    string ProtocolKey,
    int ProtocolVersion,
    string ProtocolContentHash,
    string EngineVersion,
    string ClinicalContentStatus,
    TelehealthTriageOutcome Outcome,
    string AnswerFingerprint,
    IReadOnlyList<string> FiredRuleCodes,
    IReadOnlyList<string> ReasonCodes);

public interface ISyntheticTelehealthComplaintTriageEvaluator
{
    SyntheticComplaintTriageResult Evaluate(SyntheticComplaintTriageAnswers answers);
}

public sealed class SyntheticTelehealthComplaintTriageEvaluator
    : ISyntheticTelehealthComplaintTriageEvaluator
{
    public const string EngineVersion = "synthetic-complaint-triage-engine-v1";
    public const string ClinicalContentStatus = "UNAPPROVED_SYNTHETIC";
    public const int ProtocolVersion = 1;
    public static readonly Guid MigraineProtocolId =
        Guid.Parse("a37cd238-3dc3-44d9-9a94-8cfcf63e8601");
    public static readonly Guid SleepProtocolId =
        Guid.Parse("b8928aa9-26cc-4b9b-8b7b-825332ae0f02");
    public const string MigraineProtocolKey = "synthetic-migraine-complaint-triage";
    public const string SleepProtocolKey = "synthetic-sleep-complaint-triage";

    private static readonly string[] MigraineRuleOrder =
    [
        "MIG-EMERGENCY-SUDDEN-WORST",
        "MIG-EMERGENCY-NEURO-VISION",
        "MIG-URGENT-FEVER-STIFF-NECK",
        "MIG-URGENT-HEAD-INJURY",
        "MIG-URGENT-PERSISTENT-VOMITING",
        "MIG-REVIEW-PREGNANCY-POSTPARTUM",
        "MIG-REVIEW-CANCER-IMMUNOCOMPROMISED",
        "MIG-REVIEW-UNKNOWN-ANSWER",
        "MIG-REVIEW-NEW-OR-DIFFERENT-PATTERN",
        "MIG-CANDIDATE-KNOWN-SIMILAR-PATTERN"
    ];

    private static readonly string[] SleepRuleOrder =
    [
        "SLP-EMERGENCY-SELF-HARM",
        "SLP-URGENT-MANIA-PSYCHOSIS",
        "SLP-URGENT-WITHDRAWAL",
        "SLP-URGENT-DANGEROUS-SOMNOLENCE",
        "SLP-INPERSON-BREATHING-DISORDER",
        "SLP-REVIEW-PREGNANCY-COMPLEX-MEDS",
        "SLP-REVIEW-UNKNOWN-ANSWER",
        "SLP-UNSUPPORTED-CONTROLLED-SEDATIVE",
        "SLP-REVIEW-COMPLEX-PRESENTATION",
        "SLP-CANDIDATE-UNCOMPLICATED-SLEEP-DIFFICULTY"
    ];

    public static readonly string MigraineProtocolContentHash =
        TelehealthCommandFingerprint.Create(
            MigraineProtocolKey,
            ProtocolVersion,
            EngineVersion,
            ClinicalContentStatus,
            string.Join('>', MigraineRuleOrder));

    public static readonly string SleepProtocolContentHash =
        TelehealthCommandFingerprint.Create(
            SleepProtocolKey,
            ProtocolVersion,
            EngineVersion,
            ClinicalContentStatus,
            string.Join('>', SleepRuleOrder));

    public SyntheticComplaintTriageResult Evaluate(SyntheticComplaintTriageAnswers answers) =>
        answers.ComplaintCategory switch
        {
            "migraine" when answers.Migraine is not null && answers.Sleep is null =>
                EvaluateMigraine(answers.Migraine),
            "sleep" when answers.Sleep is not null && answers.Migraine is null =>
                EvaluateSleep(answers.Sleep),
            _ => throw new ArgumentException(
                "Exactly one complaint-specific answer set must match the complaint category.",
                nameof(answers))
        };

    private static SyntheticComplaintTriageResult EvaluateMigraine(
        SyntheticMigraineComplaintTriageAnswers answers)
    {
        var rules = new List<(string Rule, string Reason, TelehealthTriageOutcome Outcome)>();
        AddIfYes(rules, answers.SuddenOrWorstOnset,
            "MIG-EMERGENCY-SUDDEN-WORST", "MIGRAINE_SUDDEN_OR_WORST_WARNING", TelehealthTriageOutcome.Emergency);
        AddIfYes(rules, answers.NewNeurologicOrVisionChange,
            "MIG-EMERGENCY-NEURO-VISION", "MIGRAINE_NEUROLOGIC_OR_VISION_WARNING", TelehealthTriageOutcome.Emergency);
        AddIfYes(rules, answers.FeverOrStiffNeck,
            "MIG-URGENT-FEVER-STIFF-NECK", "MIGRAINE_FEVER_OR_STIFF_NECK", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.RecentHeadInjury,
            "MIG-URGENT-HEAD-INJURY", "MIGRAINE_RECENT_HEAD_INJURY", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.PersistentVomiting,
            "MIG-URGENT-PERSISTENT-VOMITING", "MIGRAINE_PERSISTENT_VOMITING", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.PregnantOrPostpartum,
            "MIG-REVIEW-PREGNANCY-POSTPARTUM", "MIGRAINE_PREGNANCY_OR_POSTPARTUM", TelehealthTriageOutcome.ClinicalReview);
        AddIfYes(rules, answers.CancerOrImmunocompromised,
            "MIG-REVIEW-CANCER-IMMUNOCOMPROMISED", "MIGRAINE_CANCER_OR_IMMUNOCOMPROMISED", TelehealthTriageOutcome.ClinicalReview);

        if (ContainsNotSure(
                answers.SuddenOrWorstOnset,
                answers.NewNeurologicOrVisionChange,
                answers.FeverOrStiffNeck,
                answers.RecentHeadInjury,
                answers.PregnantOrPostpartum,
                answers.CancerOrImmunocompromised,
                answers.KnownSimilarPattern,
                answers.PersistentVomiting))
        {
            rules.Add(("MIG-REVIEW-UNKNOWN-ANSWER", "MIGRAINE_ANSWER_UNCERTAIN", TelehealthTriageOutcome.ClinicalReview));
        }
        if (answers.KnownSimilarPattern == SyntheticComplaintAnswer.No)
        {
            rules.Add(("MIG-REVIEW-NEW-OR-DIFFERENT-PATTERN", "MIGRAINE_PATTERN_NOT_ESTABLISHED", TelehealthTriageOutcome.ClinicalReview));
        }
        if (rules.Count == 0 && answers.KnownSimilarPattern == SyntheticComplaintAnswer.Yes)
        {
            rules.Add(("MIG-CANDIDATE-KNOWN-SIMILAR-PATTERN", "MIGRAINE_SYNTHETIC_CANDIDATE", TelehealthTriageOutcome.TelehealthEligible));
        }

        return Result(
            MigraineProtocolId,
            MigraineProtocolKey,
            MigraineProtocolContentHash,
            rules,
            answers.SuddenOrWorstOnset,
            answers.NewNeurologicOrVisionChange,
            answers.FeverOrStiffNeck,
            answers.RecentHeadInjury,
            answers.PregnantOrPostpartum,
            answers.CancerOrImmunocompromised,
            answers.KnownSimilarPattern,
            answers.PersistentVomiting);
    }

    private static SyntheticComplaintTriageResult EvaluateSleep(
        SyntheticSleepComplaintTriageAnswers answers)
    {
        var rules = new List<(string Rule, string Reason, TelehealthTriageOutcome Outcome)>();
        AddIfYes(rules, answers.SelfHarmThoughts,
            "SLP-EMERGENCY-SELF-HARM", "SLEEP_SELF_HARM_WARNING", TelehealthTriageOutcome.Emergency);
        AddIfYes(rules, answers.ManiaOrPsychosis,
            "SLP-URGENT-MANIA-PSYCHOSIS", "SLEEP_MANIA_OR_PSYCHOSIS_WARNING", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.WithdrawalConcern,
            "SLP-URGENT-WITHDRAWAL", "SLEEP_WITHDRAWAL_WARNING", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.DangerousSomnolence,
            "SLP-URGENT-DANGEROUS-SOMNOLENCE", "SLEEP_DANGEROUS_SOMNOLENCE", TelehealthTriageOutcome.UrgentInPerson);
        AddIfYes(rules, answers.BreathingPausesOrSevereSnoring,
            "SLP-INPERSON-BREATHING-DISORDER", "SLEEP_BREATHING_DISORDER_WARNING", TelehealthTriageOutcome.InPersonRequired);
        AddIfYes(rules, answers.PregnantOrComplexMedicationConcern,
            "SLP-REVIEW-PREGNANCY-COMPLEX-MEDS", "SLEEP_PREGNANCY_OR_COMPLEX_MEDICATION", TelehealthTriageOutcome.ClinicalReview);

        if (ContainsNotSure(
                answers.SelfHarmThoughts,
                answers.ManiaOrPsychosis,
                answers.DangerousSomnolence,
                answers.WithdrawalConcern,
                answers.BreathingPausesOrSevereSnoring,
                answers.PregnantOrComplexMedicationConcern,
                answers.ControlledSedativeRequest,
                answers.UncomplicatedSleepDifficulty))
        {
            rules.Add(("SLP-REVIEW-UNKNOWN-ANSWER", "SLEEP_ANSWER_UNCERTAIN", TelehealthTriageOutcome.ClinicalReview));
        }
        AddIfYes(rules, answers.ControlledSedativeRequest,
            "SLP-UNSUPPORTED-CONTROLLED-SEDATIVE", "SLEEP_CONTROLLED_SEDATIVE_OUT_OF_SCOPE", TelehealthTriageOutcome.Unsupported);
        if (answers.UncomplicatedSleepDifficulty == SyntheticComplaintAnswer.No)
        {
            rules.Add(("SLP-REVIEW-COMPLEX-PRESENTATION", "SLEEP_UNCOMPLICATED_PRESENTATION_NOT_CONFIRMED", TelehealthTriageOutcome.ClinicalReview));
        }
        if (rules.Count == 0 && answers.UncomplicatedSleepDifficulty == SyntheticComplaintAnswer.Yes)
        {
            rules.Add(("SLP-CANDIDATE-UNCOMPLICATED-SLEEP-DIFFICULTY", "SLEEP_SYNTHETIC_CANDIDATE", TelehealthTriageOutcome.TelehealthEligible));
        }

        return Result(
            SleepProtocolId,
            SleepProtocolKey,
            SleepProtocolContentHash,
            rules,
            answers.SelfHarmThoughts,
            answers.ManiaOrPsychosis,
            answers.DangerousSomnolence,
            answers.WithdrawalConcern,
            answers.BreathingPausesOrSevereSnoring,
            answers.PregnantOrComplexMedicationConcern,
            answers.ControlledSedativeRequest,
            answers.UncomplicatedSleepDifficulty);
    }

    private static void AddIfYes(
        ICollection<(string Rule, string Reason, TelehealthTriageOutcome Outcome)> rules,
        SyntheticComplaintAnswer answer,
        string rule,
        string reason,
        TelehealthTriageOutcome outcome)
    {
        if (answer == SyntheticComplaintAnswer.Yes)
        {
            rules.Add((rule, reason, outcome));
        }
    }

    private static bool ContainsNotSure(params SyntheticComplaintAnswer[] answers) =>
        answers.Contains(SyntheticComplaintAnswer.NotSure);

    private static SyntheticComplaintTriageResult Result(
        Guid protocolId,
        string protocolKey,
        string protocolContentHash,
        IReadOnlyList<(string Rule, string Reason, TelehealthTriageOutcome Outcome)> rules,
        params SyntheticComplaintAnswer[] answers)
    {
        if (rules.Count == 0)
        {
            throw new InvalidOperationException("The synthetic complaint fixture produced no rule evidence.");
        }

        return new(
            protocolId,
            protocolKey,
            ProtocolVersion,
            protocolContentHash,
            EngineVersion,
            ClinicalContentStatus,
            rules[0].Outcome,
            TelehealthCommandFingerprint.Create(answers.Cast<object?>().ToArray()),
            rules.Select(rule => rule.Rule).ToArray(),
            rules.Select(rule => rule.Reason).ToArray());
    }
}
