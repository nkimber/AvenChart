namespace AvenChart.Api.Models;
public sealed record TrackAnythingItem(int Id, int? ParentId, string Name, string? Description, int Position, bool Active);
public sealed record TrackAnythingResponse(IReadOnlyList<TrackAnythingItem> Items);
public sealed record TrackAnythingRequest(int? ParentId, string Name, string? Description, int Position, bool? Active);
