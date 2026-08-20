// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class DocumentTemplateEventConfiguration :
    IEntityTypeConfiguration<DocumentTemplateEventEntity>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateEventEntity> entity)
    {
        entity.ToTable("document_template_events", table => table.ExcludeFromMigrations());
        entity.HasKey(templateEvent => templateEvent.EventId);
        entity.Property(templateEvent => templateEvent.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedOnAdd();
        entity.Property(templateEvent => templateEvent.TemplateId).HasColumnName("template_id");
        entity.Property(templateEvent => templateEvent.Action).HasColumnName("action").IsRequired();
        entity.Property(templateEvent => templateEvent.Summary).HasColumnName("summary").IsRequired();
        entity.Property(templateEvent => templateEvent.BinaryVersionId).HasColumnName("binary_version_id");
        entity.Property(templateEvent => templateEvent.PatientDocumentId).HasColumnName("patient_document_id");
        entity.Property(templateEvent => templateEvent.PatientId).HasColumnName("patient_id");
        entity.Property(templateEvent => templateEvent.OccurredAt).HasColumnName("occurred_at");
        entity.Property(templateEvent => templateEvent.Username).HasColumnName("username").IsRequired();
        entity.HasOne(templateEvent => templateEvent.Template)
            .WithMany(template => template.Events)
            .HasForeignKey(templateEvent => templateEvent.TemplateId);
    }
}
