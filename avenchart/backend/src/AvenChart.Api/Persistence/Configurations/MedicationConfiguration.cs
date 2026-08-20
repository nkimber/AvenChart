// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class MedicationConfiguration : IEntityTypeConfiguration<MedicationEntity>
{
    public void Configure(EntityTypeBuilder<MedicationEntity> entity)
    {
        entity.ToTable("medications", table => table.ExcludeFromMigrations());
        entity.HasKey(medication => medication.Id);
        entity.Property(medication => medication.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(medication => medication.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(medication => medication.LegacyPid).HasColumnName("pid");
        entity.Property(medication => medication.Type).HasColumnName("type");
        entity.Property(medication => medication.Title).HasColumnName("title");
        entity.Property(medication => medication.Diagnosis).HasColumnName("diagnosis");
        entity.Property(medication => medication.MedicationDate).HasColumnName("medication_date");
        entity.Property(medication => medication.ModifiedDate).HasColumnName("modified_date");
        entity.Property(medication => medication.Comments).HasColumnName("comments");
        entity.Property(medication => medication.Activity).HasColumnName("activity");
        entity.Property(medication => medication.EndDate).HasColumnName("end_date");
        entity.Property(medication => medication.LifecycleVersion)
            .HasColumnName("lifecycle_version")
            .IsConcurrencyToken();
    }
}
