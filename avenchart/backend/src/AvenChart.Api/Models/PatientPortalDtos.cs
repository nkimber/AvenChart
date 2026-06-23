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

public sealed record PatientPortalHomeSummaryResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    PatientPortalHomeMessageSummary Messages,
    int UpcomingAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> UpcomingAppointments,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalHomeMessageSummary(
    int TotalMessages,
    int NewMessages,
    int DoneMessages,
    string? LatestMessageTitle,
    string? LatestMessageDate);

public sealed record PatientPortalHomeAppointmentSummary(
    string Id,
    string Date,
    string StartTime,
    string Title,
    string? Status,
    int? CategoryId,
    string? CategoryName,
    string? ProviderName,
    string? FacilityName,
    string? Comments);
