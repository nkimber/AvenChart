// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AccessUserMembershipConfiguration :
    IEntityTypeConfiguration<AccessUserMembershipEntity>
{
    public void Configure(EntityTypeBuilder<AccessUserMembershipEntity> entity)
    {
        entity.ToTable("access_user_memberships", table => table.ExcludeFromMigrations());
        entity.HasKey(membership => new { membership.UserValue, membership.GroupValue });
        entity.Property(membership => membership.UserValue).HasColumnName("user_value").IsRequired();
        entity.Property(membership => membership.UserName).HasColumnName("user_name").IsRequired();
        entity.Property(membership => membership.GroupValue).HasColumnName("group_value").IsRequired();
        entity.Property(membership => membership.GroupName).HasColumnName("group_name").IsRequired();
        entity.Property(membership => membership.StaffId).HasColumnName("staff_id");
    }
}
