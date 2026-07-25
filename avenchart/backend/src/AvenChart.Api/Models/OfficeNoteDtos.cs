namespace AvenChart.Api.Models;

public sealed record OfficeNotesResponse(IReadOnlyList<OfficeNoteItem> Notes, int Total);

public sealed record OfficeNoteItem(
    Guid Id,
    string Body,
    string Author,
    string? GroupName,
    bool Active,
    string CreatedAt,
    string UpdatedAt);

public sealed record OfficeNoteCreateRequest(string Body);
public sealed record OfficeNoteUpdateRequest(string Body);
public sealed record OfficeNoteActivityRequest(bool Active);
