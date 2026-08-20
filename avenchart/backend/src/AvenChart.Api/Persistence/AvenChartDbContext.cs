// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Persistence;

/// <summary>
/// Database-first EF Core mapping for incrementally adopted persistence slices.
/// The versioned SQL migration catalog remains the sole schema authority.
/// </summary>
public sealed class AvenChartDbContext(DbContextOptions<AvenChartDbContext> options)
    : DbContext(options)
{
    public DbSet<OfficeNoteEntity> OfficeNotes => Set<OfficeNoteEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OfficeNoteEntity>(entity =>
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
        });
    }
}
