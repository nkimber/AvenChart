namespace AvenChart.Api.Models;

public sealed record FlowBoardResponse(
    string DatasetId,
    string DatasetVersion,
    string Date,
    IReadOnlyList<FlowBoardLane> Lanes);

public sealed record FlowBoardLane(
    string Key,
    string Label,
    IReadOnlyList<FlowBoardItem> Items);

public sealed record FlowBoardItem(
    string AppointmentId,
    string PatientId,
    string PatientDisplayName,
    string StartTime,
    string Title,
    string? Room,
    string? ProviderName,
    string? FacilityName,
    string? AppointmentStatus,
    string FlowStatus);
