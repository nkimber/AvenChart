// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ChartTrackerLocationConfiguration : IEntityTypeConfiguration<ChartTrackerLocationEntity>
{
    public void Configure(EntityTypeBuilder<ChartTrackerLocationEntity> entity)
    {
        entity.ToTable("chart_tracker_locations", table => table.ExcludeFromMigrations());
        entity.HasKey(location => location.Name);
        entity.Property(location => location.Name).HasColumnName("name").ValueGeneratedNever();
        entity.Property(location => location.Position).HasColumnName("position");
        entity.Property(location => location.Active).HasColumnName("active");
    }
}
