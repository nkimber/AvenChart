// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class OfficeNoteConfiguration : IEntityTypeConfiguration<OfficeNoteEntity>
{
    public void Configure(EntityTypeBuilder<OfficeNoteEntity> entity)
    {
        entity.ToTable("office_notes", table => table.ExcludeFromMigrations());
        entity.HasKey(note => note.Id);
        entity.Property(note => note.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(note => note.Body).HasColumnName("body").IsRequired();
        entity.Property(note => note.Author).HasColumnName("author").IsRequired();
        entity.Property(note => note.GroupName).HasColumnName("group_name");
        entity.Property(note => note.Active).HasColumnName("active").IsRequired();
        entity.Property(note => note.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(note => note.UpdatedAt).HasColumnName("updated_at").IsRequired();
        entity.HasIndex(note => note.CreatedAt).HasDatabaseName("ix_office_notes_created_at");
        entity.HasIndex(note => new { note.Active, note.CreatedAt })
            .HasDatabaseName("ix_office_notes_active_created_at");
    }
}
