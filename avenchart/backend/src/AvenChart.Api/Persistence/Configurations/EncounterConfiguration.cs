// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class EncounterConfiguration : IEntityTypeConfiguration<EncounterEntity>
{
    public void Configure(EntityTypeBuilder<EncounterEntity> entity)
    {
        entity.ToTable("encounters", table => table.ExcludeFromMigrations());
        entity.HasKey(encounter => encounter.Id);
        entity.Property(encounter => encounter.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(encounter => encounter.EncounterNumber).HasColumnName("encounter");
        entity.Property(encounter => encounter.PatientId).HasColumnName("patient_id").IsRequired();
    }
}
