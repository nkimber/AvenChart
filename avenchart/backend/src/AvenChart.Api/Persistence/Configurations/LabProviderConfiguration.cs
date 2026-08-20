// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class LabProviderConfiguration : IEntityTypeConfiguration<LabProviderEntity>
{
    public void Configure(EntityTypeBuilder<LabProviderEntity> entity)
    {
        entity.ToTable("lab_providers", table => table.ExcludeFromMigrations());
        entity.HasKey(provider => provider.Id);
        entity.Property(provider => provider.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(provider => provider.Name).HasColumnName("name").IsRequired();
        entity.Property(provider => provider.LabDirectorId).HasColumnName("lab_director_id");
        entity.Property(provider => provider.Npi).HasColumnName("npi");
        entity.Property(provider => provider.Protocol).HasColumnName("protocol").IsRequired();
        entity.Property(provider => provider.Usage).HasColumnName("usage").IsRequired();
        entity.Property(provider => provider.Direction).HasColumnName("direction").IsRequired();
        entity.Property(provider => provider.SendApplicationId).HasColumnName("send_app_id").IsRequired();
        entity.Property(provider => provider.SendFacilityId).HasColumnName("send_fac_id").IsRequired();
        entity.Property(provider => provider.ReceiveApplicationId).HasColumnName("recv_app_id").IsRequired();
        entity.Property(provider => provider.ReceiveFacilityId).HasColumnName("recv_fac_id").IsRequired();
        entity.Property(provider => provider.RemoteHost).HasColumnName("remote_host").IsRequired();
        entity.Property(provider => provider.Login).HasColumnName("login").IsRequired();
        entity.Property(provider => provider.Password).HasColumnName("password").IsRequired();
        entity.Property(provider => provider.OrdersPath).HasColumnName("orders_path").IsRequired();
        entity.Property(provider => provider.ResultsPath).HasColumnName("results_path").IsRequired();
        entity.Property(provider => provider.Notes).HasColumnName("notes");
        entity.Property(provider => provider.Active).HasColumnName("active");
        entity.HasOne<LabProviderAddressBookEntity>()
            .WithMany()
            .HasForeignKey(provider => provider.LabDirectorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
