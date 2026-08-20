// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class FacilityConfiguration : IEntityTypeConfiguration<FacilityEntity>
{
    public void Configure(EntityTypeBuilder<FacilityEntity> entity)
    {
        entity.ToTable("facilities", table => table.ExcludeFromMigrations());
        entity.HasKey(facility => facility.Id);
        entity.Property(facility => facility.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(facility => facility.Code).HasColumnName("code").IsRequired();
        entity.Property(facility => facility.Name).HasColumnName("name").IsRequired();
        entity.Property(facility => facility.Phone).HasColumnName("phone");
        entity.Property(facility => facility.Street).HasColumnName("street");
        entity.Property(facility => facility.City).HasColumnName("city");
        entity.Property(facility => facility.State).HasColumnName("state");
        entity.Property(facility => facility.PostalCode).HasColumnName("postal_code");
        entity.Property(facility => facility.Color).HasColumnName("color");
        entity.Property(facility => facility.Inactive).HasColumnName("inactive");
    }
}
