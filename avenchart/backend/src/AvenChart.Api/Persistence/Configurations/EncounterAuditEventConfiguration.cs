// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class EncounterAuditEventConfiguration :
    IEntityTypeConfiguration<EncounterAuditEventEntity>
{
    public void Configure(EntityTypeBuilder<EncounterAuditEventEntity> entity)
    {
        entity.ToTable("encounter_audit_events", table => table.ExcludeFromMigrations());
        entity.HasKey(auditEvent => auditEvent.EventId);
        entity.Property(auditEvent => auditEvent.EventId).HasColumnName("event_id").ValueGeneratedNever();
        entity.Property(auditEvent => auditEvent.EncounterNumber).HasColumnName("encounter");
        entity.Property(auditEvent => auditEvent.OccurredAt).HasColumnName("occurred_at");
        entity.Property(auditEvent => auditEvent.Username).HasColumnName("username").IsRequired();
        entity.Property(auditEvent => auditEvent.Action).HasColumnName("action").IsRequired();
        entity.Property(auditEvent => auditEvent.ChangedFields).HasColumnName("changed_fields").IsRequired();
    }
}
