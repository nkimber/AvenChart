// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupSessionConfiguration : IEntityTypeConfiguration<TherapyGroupSessionEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupSessionEntity> entity)
    {
        entity.ToTable("therapy_group_sessions", table => table.ExcludeFromMigrations());
        entity.HasKey(session => session.Id);
        entity.Property(session => session.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(session => session.GroupId).HasColumnName("group_id");
        entity.Property(session => session.StartsAt).HasColumnName("starts_at");
        entity.Property(session => session.DurationMinutes).HasColumnName("duration_minutes");
        entity.Property(session => session.Topic).HasColumnName("topic");
        entity.Property(session => session.Status)
            .HasColumnName("status")
            .IsRequired()
            .IsConcurrencyToken();
        entity.Property(session => session.CreatedAt).HasColumnName("created_at");
        entity.HasOne(session => session.Group)
            .WithMany(group => group.Sessions)
            .HasForeignKey(session => session.GroupId);
    }
}
