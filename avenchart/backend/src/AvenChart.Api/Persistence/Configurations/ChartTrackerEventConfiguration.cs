// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ChartTrackerEventConfiguration : IEntityTypeConfiguration<ChartTrackerEventEntity>
{
    public void Configure(EntityTypeBuilder<ChartTrackerEventEntity> entity)
    {
        entity.ToTable("chart_tracker_events", table => table.ExcludeFromMigrations());
        entity.HasKey(trackerEvent => trackerEvent.Id);
        entity.Property(trackerEvent => trackerEvent.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(trackerEvent => trackerEvent.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(trackerEvent => trackerEvent.Location).HasColumnName("location");
        entity.Property(trackerEvent => trackerEvent.UserId).HasColumnName("user_id");
        entity.Property(trackerEvent => trackerEvent.RecordedAt)
            .HasColumnName("recorded_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        entity.HasOne(trackerEvent => trackerEvent.Patient)
            .WithMany()
            .HasForeignKey(trackerEvent => trackerEvent.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(trackerEvent => trackerEvent.User)
            .WithMany()
            .HasForeignKey(trackerEvent => trackerEvent.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(trackerEvent => new { trackerEvent.PatientId, trackerEvent.RecordedAt })
            .HasDatabaseName("idx_chart_tracker_events_patient_recorded");
    }
}
