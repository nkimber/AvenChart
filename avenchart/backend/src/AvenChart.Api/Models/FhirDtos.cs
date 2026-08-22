// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record FhirCapabilityStatement(
    string ResourceType,
    string Url,
    string Version,
    string Name,
    string Status,
    bool Experimental,
    string Date,
    string Publisher,
    string Kind,
    string FhirVersion,
    FhirSoftware Software,
    FhirImplementation Implementation,
    IReadOnlyList<string> Format,
    IReadOnlyList<FhirCapabilityResource> Rest);

public sealed record FhirSoftware(string Name, string Version);
public sealed record FhirImplementation(string Description, string Url);

public sealed record FhirCapabilityResource(
    string Mode,
    IReadOnlyList<FhirCapabilityInteraction>? Interaction,
    IReadOnlyList<FhirResourceCapability> Resource);

public sealed record FhirCapabilityInteraction(string Code);

public sealed record FhirResourceCapability(
    string Type,
    IReadOnlyList<FhirCapabilityInteraction> Interaction,
    IReadOnlyList<FhirSearchParameter> SearchParam);

public sealed record FhirSearchParameter(string Name, string Type);

public sealed record FhirPatientResource(
    string ResourceType,
    string Id,
    IReadOnlyList<FhirIdentifier> Identifier,
    IReadOnlyList<FhirHumanName> Name,
    string? Gender,
    string BirthDate,
    IReadOnlyList<FhirContactPoint>? Telecom,
    IReadOnlyList<FhirAddress>? Address);

public sealed record FhirIdentifier(string System, string Value);
public sealed record FhirHumanName(string Use, string Family, IReadOnlyList<string> Given);
public sealed record FhirContactPoint(string System, string Value, string Use);
public sealed record FhirAddress(string Use, IReadOnlyList<string> Line, string? City, string? State, string? PostalCode);

public sealed record FhirSearchBundle(
    string ResourceType,
    string Type,
    int Total,
    IReadOnlyList<FhirBundleLink> Link,
    IReadOnlyList<FhirSearchEntry>? Entry);

public sealed record FhirSearchEntry(string FullUrl, FhirPatientResource Resource);
public sealed record FhirBundleLink(string Relation, string Url);

public sealed record FhirEncounterResource(
    string ResourceType,
    string Id,
    string Status,
    FhirCoding Class,
    FhirReference Subject,
    FhirPeriod Period,
    IReadOnlyList<FhirCodeableConcept>? ReasonCode);
public sealed record FhirReference(string Reference);
public sealed record FhirPeriod(string Start);
public sealed record FhirEncounterBundle(
    string ResourceType,
    string Type,
    int Total,
    IReadOnlyList<FhirBundleLink> Link,
    IReadOnlyList<FhirEncounterSearchEntry>? Entry);
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
    IReadOnlyList<FhirObservationReferenceRange>? ReferenceRange,
    IReadOnlyList<FhirCodeableConcept>? Interpretation);

public sealed record FhirCodeableConcept(IReadOnlyList<FhirCoding>? Coding, string? Text);
public sealed record FhirCoding(string System, string Code, string? Display);
public sealed record FhirQuantity(decimal Value, string? Unit);
public sealed record FhirObservationReferenceRange(string? Text);
public sealed record FhirObservationBundle(
    string ResourceType,
    string Type,
    int Total,
    IReadOnlyList<FhirBundleLink> Link,
    IReadOnlyList<FhirObservationSearchEntry>? Entry);
public sealed record FhirObservationSearchEntry(string FullUrl, FhirObservationResource Resource);

public sealed record FhirOperationOutcome(string ResourceType, IReadOnlyList<FhirOperationOutcomeIssue> Issue);
public sealed record FhirOperationOutcomeIssue(string Severity, string Code, string Diagnostics);
