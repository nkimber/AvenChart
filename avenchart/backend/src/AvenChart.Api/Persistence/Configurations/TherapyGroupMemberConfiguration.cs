// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupMemberConfiguration : IEntityTypeConfiguration<TherapyGroupMemberEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupMemberEntity> entity)
    {
        entity.ToTable("therapy_group_members", table => table.ExcludeFromMigrations());
        entity.HasKey(member => new { member.GroupId, member.PatientId });
        entity.Property(member => member.GroupId).HasColumnName("group_id");
        entity.Property(member => member.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(member => member.JoinedAt).HasColumnName("joined_at");
        entity.HasOne(member => member.Group)
            .WithMany(group => group.Members)
            .HasForeignKey(member => member.GroupId);
        entity.HasOne(member => member.Patient)
            .WithMany()
            .HasForeignKey(member => member.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
