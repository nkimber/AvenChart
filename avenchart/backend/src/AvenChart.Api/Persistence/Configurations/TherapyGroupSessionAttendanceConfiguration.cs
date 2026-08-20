// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class TherapyGroupSessionAttendanceConfiguration : IEntityTypeConfiguration<TherapyGroupSessionAttendanceEntity>
{
    public void Configure(EntityTypeBuilder<TherapyGroupSessionAttendanceEntity> entity)
    {
        entity.ToTable("therapy_group_session_attendance", table => table.ExcludeFromMigrations());
        entity.HasKey(attendance => new { attendance.SessionId, attendance.PatientId });
        entity.Property(attendance => attendance.SessionId).HasColumnName("session_id");
        entity.Property(attendance => attendance.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(attendance => attendance.AttendanceStatus).HasColumnName("attendance_status").IsRequired();
        entity.Property(attendance => attendance.Note).HasColumnName("note");
        entity.Property(attendance => attendance.RecordedAt).HasColumnName("recorded_at");
        entity.HasOne(attendance => attendance.Session)
            .WithMany(session => session.Attendance)
            .HasForeignKey(attendance => attendance.SessionId);
        entity.HasOne(attendance => attendance.Patient)
            .WithMany()
            .HasForeignKey(attendance => attendance.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
