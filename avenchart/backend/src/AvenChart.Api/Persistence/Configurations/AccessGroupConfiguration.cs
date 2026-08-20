// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AccessGroupConfiguration : IEntityTypeConfiguration<AccessGroupEntity>
{
    public void Configure(EntityTypeBuilder<AccessGroupEntity> entity)
    {
        entity.ToTable("access_groups", table => table.ExcludeFromMigrations());
        entity.HasKey(group => group.Id);
        entity.Property(group => group.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(group => group.Value).HasColumnName("value").IsRequired();
        entity.Property(group => group.Name).HasColumnName("name").IsRequired();
    }
}
