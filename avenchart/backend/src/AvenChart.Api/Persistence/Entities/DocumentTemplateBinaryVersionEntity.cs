// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class DocumentTemplateBinaryVersionEntity
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int Version { get; set; }
    public required string FileName { get; set; }
    public required string Mimetype { get; set; }
    public int SizeBytes { get; set; }
    public required string Sha256 { get; set; }
    public required byte[] Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DocumentTemplateEntity Template { get; set; } = null!;
}
