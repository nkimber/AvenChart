// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AllergyConfiguration : IEntityTypeConfiguration<AllergyEntity>
{
    public void Configure(EntityTypeBuilder<AllergyEntity> entity)
    {
        entity.ToTable("allergies", table => table.ExcludeFromMigrations());
        entity.HasKey(allergy => allergy.Id);
        entity.Property(allergy => allergy.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(allergy => allergy.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(allergy => allergy.LegacyPid).HasColumnName("pid");
        entity.Property(allergy => allergy.Type).HasColumnName("type");
        entity.Property(allergy => allergy.Title).HasColumnName("title");
        entity.Property(allergy => allergy.Reaction).HasColumnName("reaction");
        entity.Property(allergy => allergy.Severity).HasColumnName("severity");
        entity.Property(allergy => allergy.AllergyDate).HasColumnName("allergy_date");
        entity.Property(allergy => allergy.Comments).HasColumnName("comments");
        entity.Property(allergy => allergy.Activity).HasColumnName("activity");
        entity.Property(allergy => allergy.EndDate).HasColumnName("end_date");
        entity.Property(allergy => allergy.ListOptionId).HasColumnName("list_option_id");
    }
}
