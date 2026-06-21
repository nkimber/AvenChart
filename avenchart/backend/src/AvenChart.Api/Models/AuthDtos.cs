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
