// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record FhirCapabilityStatement(
    string ResourceType,
    string Status,
    string Date,
    string Kind,
    string FhirVersion,
    string Format,
    IReadOnlyList<FhirCapabilityResource> Rest);

public sealed record FhirCapabilityResource(
    string Mode,
    IReadOnlyList<FhirCapabilityInteraction> Interaction,
    IReadOnlyList<FhirPatientCapability> Resource);

public sealed record FhirCapabilityInteraction(string Code);

public sealed record FhirPatientCapability(
    string Type,
    IReadOnlyList<FhirCapabilityInteraction> Interaction,
    IReadOnlyList<string> SearchParam);

public sealed record FhirPatientResource(
    string ResourceType,
    string Id,
    IReadOnlyList<FhirIdentifier> Identifier,
    IReadOnlyList<FhirHumanName> Name,
    string? Gender,
    string BirthDate,
    IReadOnlyList<FhirContactPoint> Telecom,
    IReadOnlyList<FhirAddress> Address);

public sealed record FhirIdentifier(string System, string Value);
public sealed record FhirHumanName(string Use, string Family, IReadOnlyList<string> Given);
public sealed record FhirContactPoint(string System, string Value, string Use);
public sealed record FhirAddress(string Use, IReadOnlyList<string> Line, string? City, string? State, string? PostalCode);

public sealed record FhirSearchBundle(
    string ResourceType,
    string Type,
    int Total,
    IReadOnlyList<FhirSearchEntry> Entry);

public sealed record FhirSearchEntry(string FullUrl, FhirPatientResource Resource);

public sealed record FhirEncounterResource(string ResourceType, string Id, string Status, FhirReference Subject, FhirPeriod Period, string? Reason);
public sealed record FhirReference(string Reference);
public sealed record FhirPeriod(string Start);
public sealed record FhirEncounterBundle(string ResourceType, string Type, int Total, IReadOnlyList<FhirEncounterSearchEntry> Entry);
public sealed record FhirEncounterSearchEntry(string FullUrl, FhirEncounterResource Resource);

public sealed record FhirObservationResource(
    string ResourceType,
    string Id,
    string Status,
    IReadOnlyList<FhirCodeableConcept> Category,
    FhirCodeableConcept Code,
    FhirReference Subject,
    string EffectiveDateTime,
    FhirQuantity? ValueQuantity,
    string? ValueString,
    IReadOnlyList<FhirObservationReferenceRange> ReferenceRange,
    IReadOnlyList<FhirCodeableConcept> Interpretation);

public sealed record FhirCodeableConcept(IReadOnlyList<FhirCoding> Coding, string? Text);
public sealed record FhirCoding(string System, string Code, string? Display);
public sealed record FhirQuantity(decimal Value, string? Unit);
public sealed record FhirObservationReferenceRange(string? Text);
public sealed record FhirObservationBundle(string ResourceType, string Type, int Total, IReadOnlyList<FhirObservationSearchEntry> Entry);
public sealed record FhirObservationSearchEntry(string FullUrl, FhirObservationResource Resource);
