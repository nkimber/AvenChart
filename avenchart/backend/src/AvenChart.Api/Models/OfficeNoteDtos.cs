// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
