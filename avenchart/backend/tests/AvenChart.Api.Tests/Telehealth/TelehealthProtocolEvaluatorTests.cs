// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthProtocolEvaluatorTests
{
    private readonly SyntheticTelehealthTriageEvaluator _evaluator = new();

    [Theory]
    [InlineData(true, false, false, false, TelehealthTriageOutcome.Emergency)]
    [InlineData(false, true, false, false, TelehealthTriageOutcome.UrgentInPerson)]
    [InlineData(false, false, true, false, TelehealthTriageOutcome.InPersonRequired)]
    [InlineData(false, false, false, true, TelehealthTriageOutcome.ClinicalReview)]
    [InlineData(false, false, false, false, TelehealthTriageOutcome.TelehealthEligible)]
    public void AppliesFailClosedPriorityInDeterministicOrder(
        bool emergency,
        bool severe,
        bool handsOn,
        bool unsure,
        TelehealthTriageOutcome expected)
    {
        var result = _evaluator.Evaluate(new(emergency, severe, handsOn, unsure));
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(SyntheticTelehealthTriageEvaluator.ProtocolId, result.ProtocolId);
        Assert.Equal(64, result.AnswerFingerprint.Length);
        Assert.Equal(64, result.ProtocolContentHash.Length);
    }

    [Fact]
    public void EmergencyAlwaysWinsOverOtherwiseEligibleAnswers()
    {
        var result = _evaluator.Evaluate(new(true, true, true, true));
        Assert.Equal(TelehealthTriageOutcome.Emergency, result.Outcome);
    }
}
