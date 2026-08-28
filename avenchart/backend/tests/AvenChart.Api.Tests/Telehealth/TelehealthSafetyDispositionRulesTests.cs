// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class TelehealthSafetyDispositionRulesTests
{
    [Fact]
    public void ValidPhysicianAuthoredDraftIsNormalizedWithoutGeneratingContent()
    {
        var normalized = TelehealthSafetyDispositionRules.Normalize(Valid() with
        {
            FollowUpTimeframe = "  within two synthetic days  ",
            NextStepInstructions = "  Physician-authored synthetic next step.  "
        });

        Assert.Equal("within two synthetic days", normalized.FollowUpTimeframe);
        Assert.Equal("Physician-authored synthetic next step.", normalized.NextStepInstructions);
        Assert.Equal("TreatedTelehealth", normalized.DispositionCode);
    }

    [Fact]
    public void UnknownDispositionFailsClosed() =>
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { DispositionCode = "GeneratedRecommendation" }));

    [Fact]
    public void CompletedEvaluationDispositionRequiresAdequateEvaluation() =>
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { AdequateEvaluationCompleted = false }));

    [Fact]
    public void EmergencyDispositionRequiresLocationInstructionsAndFactualHandoff()
    {
        var emergency = Valid() with
        {
            DispositionCode = "EmergencyTransferRecommended",
            FollowUpOwner = "EmergencyServices"
        };
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(emergency));

        var normalized = TelehealthSafetyDispositionRules.Normalize(emergency with
        {
            LocationCallbackReconfirmed = true,
            EmergencyInstructionProvided = true,
            EmergencyHandoffStatus = "UnableToConfirm"
        });
        Assert.Equal("UnableToConfirm", normalized.EmergencyHandoffStatus);
    }

    [Fact]
    public void NonEmergencyDispositionRejectsEmergencyFacts() =>
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { EmergencyInstructionProvided = true, EmergencyHandoffStatus = "RecommendedOnly" }));

    [Fact]
    public void InterruptedDispositionRequiresContactAndSafetyAttemptSummary()
    {
        var interrupted = Valid() with
        {
            DispositionCode = "TechnicalAbort",
            AdequateEvaluationCompleted = false,
            CommunicationMethod = "NotYetCommunicated",
            CommunicationCompleted = false
        };
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(interrupted));
        Assert.Equal(
            "Synthetic callback attempted; safe next step remains physician-authored.",
            TelehealthSafetyDispositionRules.Normalize(interrupted with
            {
                ContactAttemptSummary = "Synthetic callback attempted; safe next step remains physician-authored."
            }).ContactAttemptSummary);
    }

    [Fact]
    public void CommunicationMethodMustMatchCompletionState() =>
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { CommunicationMethod = "NotYetCommunicated", CommunicationCompleted = true }));

    [Fact]
    public void SyntheticAcknowledgmentAndBoundedTextAreMandatory()
    {
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { SyntheticDataConfirmed = false }));
        Assert.Throws<ArgumentException>(() => TelehealthSafetyDispositionRules.Normalize(
            Valid() with { WarningEscalationInstructions = new string('x', 2001) }));
    }

    private static RecordTelehealthSafetyDispositionDraftRequest Valid() => new(
        ExpectedVersion: 0,
        DispositionCode: "TreatedTelehealth",
        AdequateEvaluationCompleted: true,
        FollowUpOwner: "Patient",
        FollowUpTimeframe: "within two synthetic days",
        NextStepInstructions: "Physician-authored synthetic next step.",
        WarningEscalationInstructions: "Physician-authored synthetic warning and escalation instruction.",
        CommunicationMethod: "DiscussedDuringSyntheticConsultation",
        CommunicationCompleted: true,
        LocationCallbackReconfirmed: false,
        EmergencyInstructionProvided: false,
        EmergencyHandoffStatus: null,
        ContactAttemptSummary: null,
        SyntheticDataConfirmed: true);
}
