// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplateEntity>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateEntity> entity)
    {
        entity.ToTable("document_templates", table => table.ExcludeFromMigrations());
        entity.HasKey(template => template.Id);
        entity.Property(template => template.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(template => template.Name).HasColumnName("name").IsRequired();
        entity.Property(template => template.Content).HasColumnName("content").IsRequired();
        entity.Property(template => template.Active).HasColumnName("active");
        entity.Property(template => template.CreatedAt).HasColumnName("created_at");
        entity.Property(template => template.UpdatedAt).HasColumnName("updated_at");
        entity.Property(template => template.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
    }
}
