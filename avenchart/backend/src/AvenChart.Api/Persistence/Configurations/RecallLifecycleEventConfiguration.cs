// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class RecallLifecycleEventConfiguration : IEntityTypeConfiguration<RecallLifecycleEventEntity>
{
    public void Configure(EntityTypeBuilder<RecallLifecycleEventEntity> entity)
    {
        entity.ToTable("recall_lifecycle_events", table => table.ExcludeFromMigrations());
        entity.HasKey(item => item.EventId);
        entity.Property(item => item.EventId).HasColumnName("event_id").ValueGeneratedNever();
        entity.Property(item => item.RecallId).HasColumnName("recall_id");
        entity.Property(item => item.PreviousStatus).HasColumnName("previous_status");
        entity.Property(item => item.Status).HasColumnName("status").IsRequired();
        entity.Property(item => item.EventType).HasColumnName("event_type").IsRequired();
        entity.Property(item => item.Actor).HasColumnName("actor").IsRequired();
        entity.Property(item => item.Reason).HasColumnName("reason");
        entity.Property(item => item.OccurredAt)
            .HasColumnName("occurred_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }
}
