// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ProblemConfiguration : IEntityTypeConfiguration<ProblemEntity>
{
    public void Configure(EntityTypeBuilder<ProblemEntity> entity)
    {
        entity.ToTable("problems", table => table.ExcludeFromMigrations());
        entity.HasKey(problem => problem.Id);
        entity.Property(problem => problem.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(problem => problem.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(problem => problem.LegacyPid).HasColumnName("pid");
        entity.Property(problem => problem.Type).HasColumnName("type");
        entity.Property(problem => problem.Title).HasColumnName("title");
        entity.Property(problem => problem.Diagnosis).HasColumnName("diagnosis");
        entity.Property(problem => problem.ProblemDate).HasColumnName("problem_date");
        entity.Property(problem => problem.Comments).HasColumnName("comments");
        entity.Property(problem => problem.Activity).HasColumnName("activity");
        entity.Property(problem => problem.EndDate).HasColumnName("end_date");
    }
}
