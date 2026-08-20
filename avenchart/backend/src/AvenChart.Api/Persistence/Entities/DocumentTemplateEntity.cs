// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class DocumentTemplateEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Content { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long RowVersion { get; set; }
    public ICollection<DocumentTemplateBinaryVersionEntity> BinaryVersions { get; } =
        new List<DocumentTemplateBinaryVersionEntity>();
    public ICollection<DocumentTemplateEventEntity> Events { get; } =
        new List<DocumentTemplateEventEntity>();
}
