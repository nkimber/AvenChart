// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class DocumentTemplateBinaryVersionConfiguration :
    IEntityTypeConfiguration<DocumentTemplateBinaryVersionEntity>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateBinaryVersionEntity> entity)
    {
        entity.ToTable("document_template_binary_versions", table => table.ExcludeFromMigrations());
        entity.HasKey(version => version.Id);
        entity.Property(version => version.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(version => version.TemplateId).HasColumnName("template_id");
        entity.Property(version => version.Version).HasColumnName("version");
        entity.Property(version => version.FileName).HasColumnName("file_name").IsRequired();
        entity.Property(version => version.Mimetype).HasColumnName("mimetype").IsRequired();
        entity.Property(version => version.SizeBytes).HasColumnName("size_bytes");
        entity.Property(version => version.Sha256).HasColumnName("sha256").IsRequired();
        entity.Property(version => version.Content).HasColumnName("content").IsRequired();
        entity.Property(version => version.CreatedAt).HasColumnName("created_at");
        entity.HasOne(version => version.Template)
            .WithMany(template => template.BinaryVersions)
            .HasForeignKey(version => version.TemplateId);
    }
}
