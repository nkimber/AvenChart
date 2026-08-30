// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthPrescriptionSafetyInput(
    int ActiveMedicationCount,
    int ActiveAllergyCount,
    bool NoCurrentMedicationsConfirmed,
    bool NoKnownAllergiesConfirmed);

public sealed record TelehealthPrescriptionSafetyResult(
    string Outcome,
    string RulesetVersion,
    bool Passed,
    bool ExternalSourceContacted,
    IReadOnlyList<string> Findings);

public interface ITelehealthPrescriptionSafetyGateway
{
    TelehealthPrescriptionSafetyResult Evaluate(TelehealthPrescriptionSafetyInput input);
}

public sealed class SyntheticTelehealthPrescriptionSafetyGateway : ITelehealthPrescriptionSafetyGateway
{
    public const string RulesetVersion = "AVENCHART_SYNTHETIC_ZERO_LIST_GATE_V1";

    public TelehealthPrescriptionSafetyResult Evaluate(TelehealthPrescriptionSafetyInput input)
    {
        var findings = new List<string>();
        if (input.ActiveMedicationCount != 0 || !input.NoCurrentMedicationsConfirmed)
        {
            findings.Add("ACTIVE_OR_UNCONFIRMED_MEDICATION_LIST");
        }
        if (input.ActiveAllergyCount != 0 || !input.NoKnownAllergiesConfirmed)
        {
            findings.Add("ACTIVE_OR_UNCONFIRMED_ALLERGY_LIST");
        }

        return new TelehealthPrescriptionSafetyResult(
            findings.Count == 0 ? "SYNTHETIC_ZERO_LIST_GATE_PASSED" : "CLINICIAN_RESOLUTION_REQUIRED",
            RulesetVersion,
            findings.Count == 0,
            ExternalSourceContacted: false,
            findings);
    }
}

public sealed record TelehealthPrescriptionTransmissionPreparation(
    string AdapterMode,
    string CanonicalModelVersion,
    string TargetStandard,
    string TransitionStandard,
    string TransactionType,
    string TransmissionState,
    bool Certified,
    bool ExternalDestinationContacted);

public interface IEPrescriptionGateway
{
    TelehealthPrescriptionTransmissionPreparation PrepareNewRx();
}

public sealed class SyntheticEPrescriptionGateway : IEPrescriptionGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CanonicalModelVersion = "AVENCHART_ERX_CANONICAL_V1";
    public const string TargetStandard = "NCPDP_SCRIPT_2023011";
    public const string TransitionStandard = "NCPDP_SCRIPT_2017071_THROUGH_2027_12_31";

    public TelehealthPrescriptionTransmissionPreparation PrepareNewRx() => new(
        AdapterMode,
        CanonicalModelVersion,
        TargetStandard,
        TransitionStandard,
        TransactionType: "NewRx",
        TransmissionState: "PreparedOnly",
        Certified: false,
        ExternalDestinationContacted: false);
}
