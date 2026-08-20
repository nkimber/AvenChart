// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class OfficeNoteEntity
{
    public Guid Id { get; set; }

    public required string Body { get; set; }

    public required string Author { get; set; }

    public string? GroupName { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
