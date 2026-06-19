namespace AvenChart.Api.Models;

public sealed record PatientMessagesResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    bool PortalEnabled,
    IReadOnlyList<PatientMessageItem> Messages);

public sealed record PatientMessageItem(
    string Id,
    string? Date,
    string? Title,
    string? Body,
    string? Status);
