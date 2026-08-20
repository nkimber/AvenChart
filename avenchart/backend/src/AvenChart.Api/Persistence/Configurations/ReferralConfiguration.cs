// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ReferralConfiguration : IEntityTypeConfiguration<ReferralEntity>
{
    public void Configure(EntityTypeBuilder<ReferralEntity> entity)
    {
        entity.ToTable("referrals", table => table.ExcludeFromMigrations());
        entity.HasKey(referral => referral.Id);
        entity.Property(referral => referral.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(referral => referral.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(referral => referral.EncounterId).HasColumnName("encounter_id");
        entity.Property(referral => referral.Destination).HasColumnName("destination").IsRequired();
        entity.Property(referral => referral.Reason).HasColumnName("reason").IsRequired();
        entity.Property(referral => referral.Status).HasColumnName("status").IsRequired();
        entity.Property(referral => referral.ExternalReference).HasColumnName("external_reference");
        entity.Property(referral => referral.Notes).HasColumnName("notes");
        entity.Property(referral => referral.RequestedAt).HasColumnName("requested_at");
        entity.Property(referral => referral.WorkflowVersion)
            .HasColumnName("workflow_version")
            .IsConcurrencyToken();
        entity.Property(referral => referral.AssignedTo).HasColumnName("assigned_to");
        entity.Property(referral => referral.DueAt).HasColumnName("due_at");
        entity.Property(referral => referral.CreatedBy).HasColumnName("created_by");
        entity.Property(referral => referral.CreatedAt).HasColumnName("created_at");
        entity.Property(referral => referral.UpdatedAt).HasColumnName("updated_at");
        entity.HasOne(referral => referral.Patient)
            .WithMany()
            .HasForeignKey(referral => referral.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
