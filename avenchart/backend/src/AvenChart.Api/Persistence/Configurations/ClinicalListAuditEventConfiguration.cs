// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ClinicalListAuditEventConfiguration : IEntityTypeConfiguration<ClinicalListAuditEventEntity>
{
    public void Configure(EntityTypeBuilder<ClinicalListAuditEventEntity> entity)
    {
        entity.ToTable("clinical_list_audit_events", table => table.ExcludeFromMigrations());
        entity.HasKey(item => item.EventId);
        entity.Property(item => item.EventId).HasColumnName("event_id").ValueGeneratedNever();
        entity.Property(item => item.ResourceType).HasColumnName("resource_type").IsRequired();
        entity.Property(item => item.ResourceId).HasColumnName("resource_id").IsRequired();
        entity.Property(item => item.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(item => item.Action).HasColumnName("action").IsRequired();
        entity.Property(item => item.Actor).HasColumnName("actor").IsRequired();
        entity.Property(item => item.Reason).HasColumnName("reason");
        entity.Property(item => item.StateJson).HasColumnName("state_json").HasColumnType("jsonb").IsRequired();
        entity.Property(item => item.OccurredAt)
            .HasColumnName("occurred_at")
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }
}
