// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupConfiguration : IEntityTypeConfiguration<TherapyGroupEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupEntity> entity)
    {
        entity.ToTable("therapy_groups", table => table.ExcludeFromMigrations());
        entity.HasKey(group => group.Id);
        entity.Property(group => group.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(group => group.Name).HasColumnName("name").IsRequired();
        entity.Property(group => group.Status).HasColumnName("status").IsRequired();
        entity.Property(group => group.FacilitatorId).HasColumnName("facilitator_id");
        entity.Property(group => group.Description).HasColumnName("description");
        entity.Property(group => group.Capacity).HasColumnName("capacity");
        entity.Property(group => group.CreatedAt).HasColumnName("created_at");
    }
}
