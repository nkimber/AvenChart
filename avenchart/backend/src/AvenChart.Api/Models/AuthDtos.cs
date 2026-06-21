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
    string? FailureReason);

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
