// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record PatientDisclosurePolicyResponse(
    string Revision,
    string LifecycleState,
    IReadOnlyList<PatientDisclosureOption> AuthorityTypes,
    IReadOnlyList<PatientDisclosureOption> VerificationMethods,
    IReadOnlyList<PatientDisclosureScopeOption> Scopes,
    PatientEmergencyAccessState EmergencyAccess,
    IReadOnlyList<string> Boundaries);

public sealed record PatientDisclosureOption(
    string Value,
    string Label);

public sealed record PatientDisclosureScopeOption(
    string Key,
    string Label,
    string Description);

public sealed record PatientEmergencyAccessState(
    bool Enabled,
    string State,
    string Reason,
    IReadOnlyList<string> RequiredDecisions);

public sealed record PatientDisclosureAuthorityCreateRequest(
    string AuthorityType,
    string? ProxyName,
    string? ProxyRelationship,
    string Purpose,
    string Recipient,
    IReadOnlyList<string> ScopeKeys,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset ExpiresAt,
    string VerificationMethod,
    string VerificationReference,
    string Reason);

public sealed record PatientDisclosureAuthorityTransitionRequest(
    int ExpectedVersion,
    string Reason);

public sealed record PatientDisclosureAuthorityResponse(
    Guid AuthorityId,
    string PatientId,
    string AuthorityType,
    string? ProxyName,
    string? ProxyRelationship,
    string Purpose,
    string Recipient,
    IReadOnlyList<string> ScopeKeys,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset ExpiresAt,
    string VerificationMethod,
    string VerificationReference,
    string PolicyRevision,
    string Status,
    string EffectiveStatus,
    int Version,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    IReadOnlyList<string> AllowedActions);

public sealed record PatientDisclosureAuthorityEventResponse(
    long EventId,
    Guid AuthorityId,
    string Action,
    string? FromStatus,
    string ToStatus,
    int Version,
    string Reason,
    DateTimeOffset OccurredAt,
    string Username,
    string PolicyRevision);

public sealed record PatientDisclosureRequestCreateRequest(
    Guid AuthorityId,
    string Purpose,
    string Recipient,
    IReadOnlyList<string> ScopeKeys,
    string Reason);

public sealed record PatientDisclosureDecisionRequest(
    string Action,
    int ExpectedVersion,
    string Reason);

public sealed record PatientDisclosureRequestResponse(
    Guid RequestId,
    string PatientId,
    Guid AuthorityId,
    string Purpose,
    string Recipient,
    IReadOnlyList<string> ScopeKeys,
    string Status,
    int Version,
    string PolicyRevision,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    DateTimeOffset? DecidedAt,
    string? DecidedBy,
    string? DecisionReason,
    string AuthorityEffectiveStatus,
    int AuthorityVersion,
    IReadOnlyList<string> AllowedActions);

public sealed record PatientDisclosureRequestEventResponse(
    long EventId,
    Guid RequestId,
    string Action,
    string? FromStatus,
    string ToStatus,
    int Version,
    string Reason,
    DateTimeOffset OccurredAt,
    string Username,
    Guid AuthorityId,
    int AuthorityVersion,
    string AuthorityEffectiveStatus,
    string PolicyRevision);

public sealed class PatientDisclosureConcurrencyException(
    string message,
    int expectedVersion,
    int currentVersion) : InvalidOperationException(message)
{
    public int ExpectedVersion { get; } = expectedVersion;

    public int CurrentVersion { get; } = currentVersion;
}
