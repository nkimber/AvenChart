// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class LabOrderReferenceConfiguration : IEntityTypeConfiguration<LabOrderReferenceEntity>
{
    public void Configure(EntityTypeBuilder<LabOrderReferenceEntity> entity)
    {
        entity.ToTable("lab_orders", table => table.ExcludeFromMigrations());
        entity.HasKey(order => order.Id);
        entity.Property(order => order.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(order => order.LabId).HasColumnName("lab_id");
    }
}
