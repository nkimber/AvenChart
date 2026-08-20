// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class LabProviderAddressBookConfiguration : IEntityTypeConfiguration<LabProviderAddressBookEntity>
{
    public void Configure(EntityTypeBuilder<LabProviderAddressBookEntity> entity)
    {
        entity.ToTable("lab_provider_address_book", table => table.ExcludeFromMigrations());
        entity.HasKey(organization => organization.Id);
        entity.Property(organization => organization.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(organization => organization.Organization).HasColumnName("organization").IsRequired();
        entity.Property(organization => organization.Type).HasColumnName("type").IsRequired();
        entity.Property(organization => organization.Active).HasColumnName("active");
    }
}
