// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class PatientSdohAssessmentConfiguration : IEntityTypeConfiguration<PatientSdohAssessmentEntity>
{
    public void Configure(EntityTypeBuilder<PatientSdohAssessmentEntity> entity)
    {
        entity.ToTable("patient_sdoh_assessments", table => table.ExcludeFromMigrations());
        entity.HasKey(assessment => assessment.AssessmentId);
        entity.Property(assessment => assessment.AssessmentId).HasColumnName("assessment_id").ValueGeneratedNever();
        entity.Property(assessment => assessment.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(assessment => assessment.LegacyPid).HasColumnName("pid");
        entity.Property(assessment => assessment.AssessmentDate).HasColumnName("assessment_date");
        entity.Property(assessment => assessment.ScreeningTool).HasColumnName("screening_tool");
        entity.Property(assessment => assessment.Assessor).HasColumnName("assessor").IsRequired();
        entity.Property(assessment => assessment.InstrumentScore).HasColumnName("instrument_score");
        entity.Property(assessment => assessment.HungerQuestionOne).HasColumnName("hunger_q1");
        entity.Property(assessment => assessment.HungerQuestionTwo).HasColumnName("hunger_q2");
        entity.Property(assessment => assessment.HungerScore).HasColumnName("hunger_score");
        entity.Property(assessment => assessment.PregnancyStatus).HasColumnName("pregnancy_status");
        entity.Property(assessment => assessment.PregnancyEstimatedDueDate).HasColumnName("pregnancy_edd");
        entity.Property(assessment => assessment.PregnancyIntent).HasColumnName("pregnancy_intent");
        entity.Property(assessment => assessment.PostpartumStatus).HasColumnName("postpartum_status");
        entity.Property(assessment => assessment.PostpartumEnd).HasColumnName("postpartum_end");
        entity.Property(assessment => assessment.DisabilityStatus).HasColumnName("disability_status");
        entity.Property(assessment => assessment.DisabilityStatusNotes).HasColumnName("disability_status_notes");
        entity.Property(assessment => assessment.DisabilityScaleJson)
            .HasColumnName("disability_scale")
            .HasColumnType("jsonb")
            .IsRequired();
        entity.Property(assessment => assessment.DomainsJson)
            .HasColumnName("domains")
            .HasColumnType("jsonb")
            .IsRequired();
        entity.Property(assessment => assessment.Interventions).HasColumnName("interventions");
        entity.Property(assessment => assessment.CreatedAt).HasColumnName("created_at");
        entity.Property(assessment => assessment.CreatedBy).HasColumnName("created_by").IsRequired();
        entity.Property(assessment => assessment.UpdatedAt).HasColumnName("updated_at");
        entity.Property(assessment => assessment.UpdatedBy).HasColumnName("updated_by").IsRequired();
        entity.Property(assessment => assessment.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
        entity.HasOne(assessment => assessment.Patient)
            .WithMany()
            .HasForeignKey(assessment => assessment.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(assessment => new { assessment.PatientId, assessment.AssessmentDate, assessment.UpdatedAt })
            .HasDatabaseName("ix_patient_sdoh_assessments_patient_history");
    }
}
