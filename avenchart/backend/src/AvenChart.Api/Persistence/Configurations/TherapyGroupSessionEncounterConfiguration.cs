// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupSessionEncounterConfiguration : IEntityTypeConfiguration<TherapyGroupSessionEncounterEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupSessionEncounterEntity> entity)
    {
        entity.ToTable("therapy_group_session_encounters", table => table.ExcludeFromMigrations());
        entity.HasKey(encounter => new { encounter.SessionId, encounter.PatientId });
        entity.Property(encounter => encounter.SessionId).HasColumnName("session_id");
        entity.Property(encounter => encounter.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(encounter => encounter.EncounterId).HasColumnName("encounter_id");
        entity.Property(encounter => encounter.CreatedAt).HasColumnName("created_at");
        entity.HasOne(encounter => encounter.Patient)
            .WithMany()
            .HasForeignKey(encounter => encounter.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
