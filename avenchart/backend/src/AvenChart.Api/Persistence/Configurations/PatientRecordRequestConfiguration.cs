// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class PatientRecordRequestConfiguration : IEntityTypeConfiguration<PatientRecordRequestEntity>
{
    public void Configure(EntityTypeBuilder<PatientRecordRequestEntity> entity)
    {
        entity.ToTable("patient_record_requests", table => table.ExcludeFromMigrations());
        entity.HasKey(request => request.RequestId);
        entity.Property(request => request.RequestId).HasColumnName("request_id").ValueGeneratedNever();
        entity.Property(request => request.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(request => request.LegacyPid).HasColumnName("pid");
        entity.Property(request => request.RequestedAt).HasColumnName("requested_at");
        entity.Property(request => request.RequestedBy).HasColumnName("requested_by").IsRequired();
        entity.Property(request => request.CompletedAt).HasColumnName("completed_at");
        entity.Property(request => request.CompletedBy).HasColumnName("completed_by");
        entity.Property(request => request.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
        entity.HasOne(request => request.Patient)
            .WithMany()
            .HasForeignKey(request => request.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(request => new { request.PatientId, request.RequestedAt })
            .HasDatabaseName("ix_patient_record_requests_patient_history");
    }
}
