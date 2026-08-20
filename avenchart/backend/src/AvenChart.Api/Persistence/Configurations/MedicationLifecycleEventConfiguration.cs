// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class MedicationLifecycleEventConfiguration : IEntityTypeConfiguration<MedicationLifecycleEventEntity>
{
    public void Configure(EntityTypeBuilder<MedicationLifecycleEventEntity> entity)
    {
        entity.ToTable("medication_list_lifecycle_events", table => table.ExcludeFromMigrations());
        entity.HasKey(@event => @event.Id);
        entity.Property(@event => @event.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(@event => @event.MedicationId).HasColumnName("medication_id").IsRequired();
        entity.Property(@event => @event.Action).HasColumnName("action").IsRequired();
        entity.Property(@event => @event.PreviousActivity).HasColumnName("previous_activity");
        entity.Property(@event => @event.CurrentActivity).HasColumnName("current_activity");
        entity.Property(@event => @event.Actor).HasColumnName("actor").IsRequired();
        entity.Property(@event => @event.Reason).HasColumnName("reason");
        entity.Property(@event => @event.ExpectedVersion).HasColumnName("expected_version");
        entity.Property(@event => @event.ResultingVersion).HasColumnName("resulting_version");
        entity.Property(@event => @event.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp without time zone");
        entity.HasOne<MedicationEntity>()
            .WithMany()
            .HasForeignKey(@event => @event.MedicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
