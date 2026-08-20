// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class RecallActivityConfiguration : IEntityTypeConfiguration<RecallActivityEntity>
{
    public void Configure(EntityTypeBuilder<RecallActivityEntity> entity)
    {
        entity.ToTable("recall_activity", table => table.ExcludeFromMigrations());
        entity.HasKey(activity => activity.Id);
        entity.Property(activity => activity.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(activity => activity.RecallId).HasColumnName("recall_id");
        entity.Property(activity => activity.ActivityType).HasColumnName("activity_type").IsRequired();
        entity.Property(activity => activity.Note).HasColumnName("note");
        entity.Property(activity => activity.RecordedAt)
            .HasColumnName("recorded_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }
}
