// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AccessGroupPermissionConfiguration :
    IEntityTypeConfiguration<AccessGroupPermissionEntity>
{
    public void Configure(EntityTypeBuilder<AccessGroupPermissionEntity> entity)
    {
        entity.ToTable("access_group_permissions", table => table.ExcludeFromMigrations());
        entity.HasKey(permission => new
        {
            permission.GroupValue,
            permission.SectionValue,
            permission.PermissionValue,
            permission.ReturnValue
        });
        entity.Property(permission => permission.GroupValue).HasColumnName("group_value").IsRequired();
        entity.Property(permission => permission.SectionValue).HasColumnName("section_value").IsRequired();
        entity.Property(permission => permission.PermissionValue).HasColumnName("permission_value").IsRequired();
        entity.Property(permission => permission.PermissionName).HasColumnName("permission_name").IsRequired();
        entity.Property(permission => permission.ReturnValue).HasColumnName("return_value").IsRequired();
    }
}
