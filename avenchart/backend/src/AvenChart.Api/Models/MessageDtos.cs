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
    string? Status,
    string? AssignedTo,
    int Deleted);

public sealed record PatientMessageCreateRequest(
    string PatientId,
    string Title,
    string Body,
    string AssignedTo);

public sealed record PatientMessageStatusUpdateRequest(
    string Status,
    string Body);

public sealed record PatientMessageContentUpdateRequest(
    string Title,
    string Body);

public sealed record PatientMessageAssignmentUpdateRequest(
    string AssignedTo);

public sealed record PatientMessageMutationResponse(
    string Id,
    PatientMessagesResponse Detail);
