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
