// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<PatientEntity>
{
    public void Configure(EntityTypeBuilder<PatientEntity> entity)
    {
        entity.ToTable("patients", table => table.ExcludeFromMigrations());
        entity.HasKey(patient => patient.CanonicalId);
        entity.Property(patient => patient.CanonicalId).HasColumnName("canonical_id").ValueGeneratedNever();
        entity.Property(patient => patient.LegacyPid).HasColumnName("legacy_pid");
        entity.Property(patient => patient.PublicId).HasColumnName("pubpid").IsRequired();
        entity.Property(patient => patient.FirstName).HasColumnName("first_name").IsRequired();
        entity.Property(patient => patient.LastName).HasColumnName("last_name").IsRequired();
        entity.Property(patient => patient.PreferredName).HasColumnName("preferred_name");
        entity.Property(patient => patient.DateOfBirth).HasColumnName("date_of_birth");
        entity.Property(patient => patient.ProviderId).HasColumnName("provider_id");
        entity.Property(patient => patient.MergedIntoPatientId).HasColumnName("merged_into_patient_id");
    }
}
