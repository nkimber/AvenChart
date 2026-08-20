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
        entity.Property(encounter => encounter.LegacyPid).HasColumnName("pid");
        entity.Property(encounter => encounter.Reason).HasColumnName("reason");
        entity.Property(encounter => encounter.Sensitivity).HasColumnName("sensitivity");
        entity.Property(encounter => encounter.ReferralSource).HasColumnName("referral_source");
        entity.Property(encounter => encounter.ExternalId).HasColumnName("external_id");
        entity.Property(encounter => encounter.PosCode).HasColumnName("pos_code");
        entity.Property(encounter => encounter.BillingNote).HasColumnName("billing_note");
        entity.Property(encounter => encounter.ArchivedAt)
            .HasColumnName("archived_at")
            .HasColumnType("timestamp without time zone");
        entity.Property(encounter => encounter.ArchiveVersion).HasColumnName("archive_version");
        entity.Property(encounter => encounter.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
    }
}
