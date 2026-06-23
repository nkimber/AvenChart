namespace AvenChart.Api.Models;

public sealed record PatientPortalLoginRequest(
    string Username,
    string Password);

public sealed record PatientPortalLoginResponse(
    bool Authenticated,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string? FailureReason,
    Guid? SessionId,
    DateTimeOffset? SessionCreatedAt,
    DateTimeOffset? SessionExpiresAt,
    string SessionSource);

public sealed record PatientPortalSessionResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? EndedAt,
    string? FailureReason,
    string SessionSource);
