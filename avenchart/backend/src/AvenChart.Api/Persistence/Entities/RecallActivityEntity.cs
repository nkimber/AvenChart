// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class RecallActivityEntity
{
    public Guid Id { get; set; }
    public Guid RecallId { get; set; }
    public required string ActivityType { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public RecallEntity Recall { get; set; } = null!;
}
