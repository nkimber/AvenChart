// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class LabOrderCatalogConfiguration : IEntityTypeConfiguration<LabOrderCatalogEntity>
{
    public void Configure(EntityTypeBuilder<LabOrderCatalogEntity> entity)
    {
        entity.ToTable("lab_order_catalog", table => table.ExcludeFromMigrations());
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.ParentId).HasColumnName("parent_id");
        entity.Property(item => item.LabId).HasColumnName("lab_id");
        entity.Property(item => item.Code).HasColumnName("code");
        entity.Property(item => item.Name).HasColumnName("name").IsRequired();
        entity.Property(item => item.ItemType).HasColumnName("item_type").IsRequired();
        entity.Property(item => item.ProcedureTypeName).HasColumnName("procedure_type_name");
        entity.Property(item => item.Description).HasColumnName("description");
        entity.Property(item => item.Specimen).HasColumnName("specimen");
        entity.Property(item => item.StandardCode).HasColumnName("standard_code");
        entity.Property(item => item.Sequence).HasColumnName("seq");
        entity.Property(item => item.Active).HasColumnName("active");
        entity.HasOne<LabProviderEntity>()
            .WithMany()
            .HasForeignKey(item => item.LabId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
