// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class RecallConfiguration : IEntityTypeConfiguration<RecallEntity>
{
    public void Configure(EntityTypeBuilder<RecallEntity> entity)
    {
        entity.ToTable("recalls", table => table.ExcludeFromMigrations());
        entity.HasKey(recall => recall.Id);
        entity.Property(recall => recall.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(recall => recall.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(recall => recall.RecallDate).HasColumnName("recall_date");
        entity.Property(recall => recall.Reason).HasColumnName("reason").IsRequired();
        entity.Property(recall => recall.ProviderId).HasColumnName("provider_id");
        entity.Property(recall => recall.FacilityId).HasColumnName("facility_id");
        entity.Property(recall => recall.Status).HasColumnName("status").IsRequired();
        entity.Property(recall => recall.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
        entity.Property(recall => recall.ClosedAt).HasColumnName("closed_at");
        entity.Property(recall => recall.ClosedBy).HasColumnName("closed_by");
        entity.Property(recall => recall.ClosureReason).HasColumnName("closure_reason");
        entity.HasOne(recall => recall.Patient)
            .WithMany()
            .HasForeignKey(recall => recall.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasMany(recall => recall.Activities)
            .WithOne(activity => activity.Recall)
            .HasForeignKey(activity => activity.RecallId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(recall => recall.LifecycleEvents)
            .WithOne(lifecycleEvent => lifecycleEvent.Recall)
            .HasForeignKey(lifecycleEvent => lifecycleEvent.RecallId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
