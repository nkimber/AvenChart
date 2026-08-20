// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class StaffConfiguration : IEntityTypeConfiguration<StaffEntity>
{
    public void Configure(EntityTypeBuilder<StaffEntity> entity)
    {
        entity.ToTable("staff", table => table.ExcludeFromMigrations());
        entity.HasKey(staff => staff.Id);
        entity.Property(staff => staff.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(staff => staff.FirstName).HasColumnName("first_name").IsRequired();
        entity.Property(staff => staff.LastName).HasColumnName("last_name").IsRequired();
        entity.Property(staff => staff.Active).HasColumnName("active");
    }
}
