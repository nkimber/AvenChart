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
