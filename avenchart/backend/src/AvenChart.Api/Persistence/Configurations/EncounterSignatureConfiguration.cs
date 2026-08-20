// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class EncounterSignatureConfiguration : IEntityTypeConfiguration<EncounterSignatureEntity>
{
    public void Configure(EntityTypeBuilder<EncounterSignatureEntity> entity)
    {
        entity.ToTable("encounter_signatures", table => table.ExcludeFromMigrations());
        entity.HasKey(signature => signature.Id);
        entity.Property(signature => signature.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(signature => signature.EncounterNumber).HasColumnName("encounter");
        entity.Property(signature => signature.IsLock).HasColumnName("is_lock");
    }
}
