// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectivePracticeNetworkInquiry(
    string PracticeId,
    string PracticeDisplayName,
    int FacilityId,
    string PlanKey,
    string CurrentLocationStateCode,
    DateOnly DateOfService,
    string ServiceCategory,
    DateTimeOffset CheckedAt);

public sealed record TelehealthProspectivePracticeNetworkAdapterResult(
    string AdapterMode,
    string CompatibilityTarget,
    string DatasetKey,
    int DatasetVersion,
    DateTimeOffset DatasetEffectiveFrom,
    DateTimeOffset DatasetEffectiveThrough,
    DateTimeOffset SourceLastUpdatedAt,
    Guid RequestTraceToken,
    Guid ResponseTraceToken,
    string TransportOutcome,
    string PlanNetworkMatchStatus,
    string PracticeAffiliationStatus,
    string ServiceAvailabilityStatus,
    string NewPatientAcceptanceStatus,
    string BusinessOutcome,
    bool PracticeNetworkChecked,
    bool PracticeInNetwork,
    bool NewPatientsAccepted,
    string? NetworkReference,
    string? OrganizationReference,
    string? LocationReference,
    string? ServiceReference,
    DateTimeOffset CheckedAt,
    DateTimeOffset ExpiresAt);

public interface ITelehealthProspectivePracticeNetworkGateway
{
    ValueTask<TelehealthProspectivePracticeNetworkAdapterResult> CheckAsync(
        TelehealthProspectivePracticeNetworkInquiry inquiry,
        CancellationToken cancellationToken);
}

public sealed class SyntheticTelehealthProspectivePracticeNetworkGateway
    : ITelehealthProspectivePracticeNetworkGateway
{
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CompatibilityTarget = "HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0";
    public const string DatasetKey = "avenchart-synthetic-practice-network-directory-2026-08";
    public const int DatasetVersion = 1;
    public const string PracticeId = "avenchart-synthetic-practice";
    public const int FacilityId = 10;
    public const string ServiceCategory = "ProfessionalTelehealthConsultation";
    public const string OrganizationReference = "syn-org-avenchart-practice";
    public const string LocationReference = "syn-location-main-telehealth";
    public const string ServiceReference = "syn-service-professional-telehealth";

    public static readonly DateTimeOffset DatasetEffectiveFrom =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DatasetEffectiveThrough =
        new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);
    public static readonly DateTimeOffset SourceLastUpdatedAt =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

    public ValueTask<TelehealthProspectivePracticeNetworkAdapterResult> CheckAsync(
        TelehealthProspectivePracticeNetworkInquiry inquiry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedInquiry(inquiry);

        var outcome = inquiry.PlanKey switch
        {
            "harbor-mutual-hd" => new SyntheticOutcome(
                "SimulatedAvailable",
                "Matched",
                "InNetwork",
                "Included",
                "Accepting",
                "PracticeInNetworkAcceptingNewPatients",
                true,
                true,
                true,
                "syn-network-harbor-mutual-hd",
                OrganizationReference,
                LocationReference,
                ServiceReference),
            "pine-state-choice" => new SyntheticOutcome(
                "SimulatedAvailable",
                "Matched",
                "OutOfNetwork",
                "Excluded",
                "Unknown",
                "PracticeOutOfNetwork",
                true,
                false,
                false,
                "syn-network-pine-state-choice",
                OrganizationReference,
                LocationReference,
                ServiceReference),
            "blue-valley-standard" => new SyntheticOutcome(
                "SimulatedUnavailable",
                "Unknown",
                "Unknown",
                "Unknown",
                "Unknown",
                "UnableToDetermine",
                false,
                false,
                false,
                null,
                null,
                null,
                null),
            _ => throw DatasetUnavailable()
        };

        return ValueTask.FromResult(new TelehealthProspectivePracticeNetworkAdapterResult(
            AdapterMode,
            CompatibilityTarget,
            DatasetKey,
            DatasetVersion,
            DatasetEffectiveFrom,
            DatasetEffectiveThrough,
            SourceLastUpdatedAt,
            Guid.NewGuid(),
            Guid.NewGuid(),
            outcome.TransportOutcome,
            outcome.PlanNetworkMatchStatus,
            outcome.PracticeAffiliationStatus,
            outcome.ServiceAvailabilityStatus,
            outcome.NewPatientAcceptanceStatus,
            outcome.BusinessOutcome,
            outcome.PracticeNetworkChecked,
            outcome.PracticeInNetwork,
            outcome.NewPatientsAccepted,
            outcome.NetworkReference,
            outcome.OrganizationReference,
            outcome.LocationReference,
            outcome.ServiceReference,
            inquiry.CheckedAt,
            inquiry.CheckedAt.AddMinutes(15)));
    }

    private static void RequireSupportedInquiry(
        TelehealthProspectivePracticeNetworkInquiry inquiry)
    {
        var effectiveFrom = DateOnly.FromDateTime(DatasetEffectiveFrom.UtcDateTime);
        var effectiveThrough = DateOnly.FromDateTime(DatasetEffectiveThrough.UtcDateTime);
        if (inquiry.PracticeId != PracticeId
            || inquiry.FacilityId != FacilityId
            || string.IsNullOrWhiteSpace(inquiry.PracticeDisplayName)
            || inquiry.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || inquiry.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || inquiry.ServiceCategory != ServiceCategory
            || inquiry.DateOfService < effectiveFrom
            || inquiry.DateOfService > effectiveThrough
            || inquiry.CheckedAt.Offset != TimeSpan.Zero)
        {
            throw DatasetUnavailable();
        }
    }

    private static TelehealthProblem DatasetUnavailable() =>
        TelehealthProblem.Conflict(
            "telehealth_applicant_practice_network_dataset_unavailable",
            "The bounded synthetic practice-network dataset is not available for this inquiry.");

    private sealed record SyntheticOutcome(
        string TransportOutcome,
        string PlanNetworkMatchStatus,
        string PracticeAffiliationStatus,
        string ServiceAvailabilityStatus,
        string NewPatientAcceptanceStatus,
        string BusinessOutcome,
        bool PracticeNetworkChecked,
        bool PracticeInNetwork,
        bool NewPatientsAccepted,
        string? NetworkReference,
        string? OrganizationReference,
        string? LocationReference,
        string? ServiceReference);
}
