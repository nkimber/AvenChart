// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ClinicalWorkflowEventConfiguration : IEntityTypeConfiguration<ClinicalWorkflowEventEntity>
{
    public void Configure(EntityTypeBuilder<ClinicalWorkflowEventEntity> entity)
    {
        entity.ToTable("clinical_workflow_events", table => table.ExcludeFromMigrations());
        entity.HasKey(workflowEvent => workflowEvent.EventId);
        entity.Property(workflowEvent => workflowEvent.EventId).HasColumnName("event_id").ValueGeneratedNever();
        entity.Property(workflowEvent => workflowEvent.WorkflowType).HasColumnName("workflow_type").IsRequired();
        entity.Property(workflowEvent => workflowEvent.EntityId).HasColumnName("entity_id").IsRequired();
        entity.Property(workflowEvent => workflowEvent.PatientId).HasColumnName("patient_id");
        entity.Property(workflowEvent => workflowEvent.WorkflowVersion).HasColumnName("workflow_version");
        entity.Property(workflowEvent => workflowEvent.Action).HasColumnName("action").IsRequired();
        entity.Property(workflowEvent => workflowEvent.FromState).HasColumnName("from_state");
        entity.Property(workflowEvent => workflowEvent.ToState).HasColumnName("to_state").IsRequired();
        entity.Property(workflowEvent => workflowEvent.FromAssignedTo).HasColumnName("from_assigned_to");
        entity.Property(workflowEvent => workflowEvent.ToAssignedTo).HasColumnName("to_assigned_to");
        entity.Property(workflowEvent => workflowEvent.ReasonCode).HasColumnName("reason_code").IsRequired();
        entity.Property(workflowEvent => workflowEvent.Reason).HasColumnName("reason").IsRequired();
        entity.Property(workflowEvent => workflowEvent.Actor).HasColumnName("actor").IsRequired();
        entity.Property(workflowEvent => workflowEvent.PolicyRevision).HasColumnName("policy_revision").IsRequired();
        entity.Property(workflowEvent => workflowEvent.OccurredAt).HasColumnName("occurred_at");
    }
}
