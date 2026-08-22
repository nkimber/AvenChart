// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class VitalConfiguration : IEntityTypeConfiguration<VitalEntity>
{
    public void Configure(EntityTypeBuilder<VitalEntity> entity)
    {
        entity.ToTable("vitals", table => table.ExcludeFromMigrations());
        entity.HasKey(vital => vital.Id);
        entity.Property(vital => vital.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(vital => vital.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(vital => vital.LegacyPid).HasColumnName("pid");
        entity.Property(vital => vital.EncounterNumber).HasColumnName("encounter");
        entity.Property(vital => vital.VitalDateTime)
            .HasColumnName("vital_datetime")
            .HasColumnType("timestamp without time zone");
        entity.Property(vital => vital.RecordedAt)
            .HasColumnName("recorded_at")
            .HasColumnType("timestamp without time zone");
        entity.Property(vital => vital.RecordedBy).HasColumnName("recorded_by").IsRequired();
        entity.Property(vital => vital.CorrectionOfVitalId).HasColumnName("correction_of_vital_id");
        entity.Property(vital => vital.CorrectionReason).HasColumnName("correction_reason");
        entity.Property(vital => vital.Systolic).HasColumnName("bps");
        entity.Property(vital => vital.Diastolic).HasColumnName("bpd");
        entity.Property(vital => vital.Weight).HasColumnName("weight");
        entity.Property(vital => vital.Height).HasColumnName("height");
        entity.Property(vital => vital.Temperature).HasColumnName("temperature");
        entity.Property(vital => vital.Pulse).HasColumnName("pulse");
        entity.Property(vital => vital.Respiration).HasColumnName("respiration");
        entity.Property(vital => vital.Bmi).HasColumnName("bmi");
        entity.Property(vital => vital.OxygenSaturation).HasColumnName("oxygen_saturation");
        entity.Property(vital => vital.Note).HasColumnName("note");
    }
}
