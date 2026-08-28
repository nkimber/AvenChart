// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public interface ITelehealthTriageEvaluator
{
    TelehealthTriageResult Evaluate(SyntheticTelehealthTriageAnswers answers);
}

public sealed class SyntheticTelehealthTriageEvaluator : ITelehealthTriageEvaluator
{
    public static readonly Guid ProtocolId = Guid.Parse("8df3224f-8cc6-4a1e-b070-657ad2f71f80");
    public const string ProtocolKey = "synthetic-universal-safety";
    public const int ProtocolVersion = 1;
    public static readonly string ProtocolContentHash = TelehealthCommandFingerprint.Create(
        ProtocolKey,
        ProtocolVersion,
        "emergency>urgent>in-person>clinical-review>eligible");

    public TelehealthTriageResult Evaluate(SyntheticTelehealthTriageAnswers answers)
    {
        var outcome = answers.HasEmergencyWarning
            ? TelehealthTriageOutcome.Emergency
            : answers.SevereOrWorsening
                ? TelehealthTriageOutcome.UrgentInPerson
                : answers.RequiresHandsOnExam
                    ? TelehealthTriageOutcome.InPersonRequired
                    : answers.Unsure
                        ? TelehealthTriageOutcome.ClinicalReview
                        : TelehealthTriageOutcome.TelehealthEligible;

        return new TelehealthTriageResult(
            ProtocolId,
            ProtocolKey,
            ProtocolVersion,
            ProtocolContentHash,
            outcome,
            TelehealthCommandFingerprint.Create(
                answers.HasEmergencyWarning,
                answers.SevereOrWorsening,
                answers.RequiresHandsOnExam,
                answers.Unsure));
    }
}
