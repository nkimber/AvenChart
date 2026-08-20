// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupSessionParticipantConfiguration : IEntityTypeConfiguration<TherapyGroupSessionParticipantEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupSessionParticipantEntity> entity)
    {
        entity.ToTable("therapy_group_session_participants", table => table.ExcludeFromMigrations());
        entity.HasKey(participant => new { participant.SessionId, participant.PatientId });
        entity.Property(participant => participant.SessionId).HasColumnName("session_id");
        entity.Property(participant => participant.PatientId).HasColumnName("patient_id").IsRequired();
        entity.HasOne(participant => participant.Patient)
            .WithMany()
            .HasForeignKey(participant => participant.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
