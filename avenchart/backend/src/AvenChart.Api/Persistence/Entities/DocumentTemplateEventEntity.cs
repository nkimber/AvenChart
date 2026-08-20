// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class DocumentTemplateEventEntity
{
    public long EventId { get; set; }
    public Guid TemplateId { get; set; }
    public required string Action { get; set; }
    public required string Summary { get; set; }
    public Guid? BinaryVersionId { get; set; }
    public long? PatientDocumentId { get; set; }
    public string? PatientId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Username { get; set; }
    public DocumentTemplateEntity Template { get; set; } = null!;
}
