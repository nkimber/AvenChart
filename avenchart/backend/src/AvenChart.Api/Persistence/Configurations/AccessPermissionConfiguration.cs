// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AccessPermissionConfiguration : IEntityTypeConfiguration<AccessPermissionEntity>
{
    public void Configure(EntityTypeBuilder<AccessPermissionEntity> entity)
    {
        entity.ToTable("access_permissions", table => table.ExcludeFromMigrations());
        entity.HasKey(permission => new { permission.SectionValue, permission.Value });
        entity.Property(permission => permission.SectionValue).HasColumnName("section_value").IsRequired();
        entity.Property(permission => permission.Value).HasColumnName("value").IsRequired();
        entity.Property(permission => permission.Name).HasColumnName("name").IsRequired();
    }
}
