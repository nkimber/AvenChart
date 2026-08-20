// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class PatientEducationResourceConfiguration : IEntityTypeConfiguration<PatientEducationResourceEntity>
{
    public void Configure(EntityTypeBuilder<PatientEducationResourceEntity> entity)
    {
        entity.ToTable("patient_education_resources", table => table.ExcludeFromMigrations());
        entity.HasKey(resource => resource.ResourceKey);
        entity.Property(resource => resource.ResourceKey).HasColumnName("resource_key").ValueGeneratedNever();
        entity.Property(resource => resource.Title).HasColumnName("title").IsRequired();
        entity.Property(resource => resource.SearchTemplate).HasColumnName("search_template").IsRequired();
        entity.Property(resource => resource.Active).HasColumnName("active").IsRequired();
    }
}
