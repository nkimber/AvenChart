namespace AvenChart.Api.Models;

public sealed record PatientRecordRequestResponse(
    Guid RequestId,
    string PatientId,
    int LegacyPid,
    string Status,
    string RequestedAt,
    string RequestedBy,
    string? CompletedAt,
    string? CompletedBy);
