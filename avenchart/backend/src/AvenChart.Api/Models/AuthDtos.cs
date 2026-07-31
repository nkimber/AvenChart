// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record AuthLoginRequest(
    string Username,
    string Password);

public sealed record AuthLoginResponse(
    bool Authenticated,
    string Username,
    string DisplayName,
    string Role,
    int? StaffId,
    string? FailureReason,
    Guid? SessionId,
    DateTimeOffset? SessionCreatedAt,
    DateTimeOffset? SessionExpiresAt);

public sealed record AuthSessionRequest(
    Guid SessionId);

public sealed record AuthSessionResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string DisplayName,
    string Role,
    int? StaffId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? EndedAt,
    string? FailureReason,
    string SessionSource);

public sealed record AuthAuthorizationFailureResponse(
    bool Authenticated,
    bool Authorized,
    Guid? SessionId,
    string Username,
    string Role,
    string RequiredSection,
    string RequiredPermission,
    string RequiredReturnValue,
    string? FailureReason,
    string SessionSource);

public sealed record AuthAuditEventItem(
    long Id,
    DateTimeOffset OccurredAt,
    string Event,
    string Username,
    bool Success,
    string? SourceIp,
    string Comment,
    string? FailureReason,
    string LogSource);

public sealed record AuthAuditResponse(
    int TotalEvents,
    int SuccessfulLogins,
    int FailedLogins,
    IReadOnlyList<AuthAuditEventItem> Events);

public sealed record AuthSessionAuditItem(
    string Username,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt,
    bool Active,
    string SessionSource);

public sealed record AuthActivityAuditResponse(
    int TotalEvents,
    int SuccessfulLogins,
    int FailedLogins,
    int ActiveSessions,
    int EndedSessions,
    IReadOnlyList<AuthAuditEventItem> Events,
    IReadOnlyList<AuthSessionAuditItem> Sessions);
