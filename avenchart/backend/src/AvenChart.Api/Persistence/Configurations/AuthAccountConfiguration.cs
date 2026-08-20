// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AuthAccountConfiguration : IEntityTypeConfiguration<AuthAccountEntity>
{
    public void Configure(EntityTypeBuilder<AuthAccountEntity> entity)
    {
        entity.ToTable("auth_accounts", table => table.ExcludeFromMigrations());
        entity.HasKey(account => account.Username);
        entity.Property(account => account.Username).HasColumnName("username").ValueGeneratedNever();
        entity.Property(account => account.DisplayName).HasColumnName("display_name").IsRequired();
        entity.Property(account => account.Active).HasColumnName("active");
    }
}
