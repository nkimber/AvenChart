// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class ImmunizationConfiguration : IEntityTypeConfiguration<ImmunizationEntity>
{
    public void Configure(EntityTypeBuilder<ImmunizationEntity> entity)
    {
        entity.ToTable("immunizations", table => table.ExcludeFromMigrations());
        entity.HasKey(immunization => immunization.Id);
        entity.Property(immunization => immunization.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(immunization => immunization.Key).HasColumnName("key").IsRequired();
        entity.Property(immunization => immunization.PatientId).HasColumnName("patient_id").IsRequired();
        entity.Property(immunization => immunization.LegacyPid).HasColumnName("pid");
        entity.Property(immunization => immunization.Encounter).HasColumnName("encounter");
        entity.Property(immunization => immunization.ImmunizationId).HasColumnName("immunization_id");
        entity.Property(immunization => immunization.CvxCode).HasColumnName("cvx_code");
        entity.Property(immunization => immunization.Vaccine).HasColumnName("vaccine");
        entity.Property(immunization => immunization.AdministeredAt)
            .HasColumnName("administered_at")
            .HasColumnType("timestamp without time zone");
        entity.Property(immunization => immunization.Manufacturer).HasColumnName("manufacturer");
        entity.Property(immunization => immunization.LotNumber).HasColumnName("lot_number");
        entity.Property(immunization => immunization.AdministeredById).HasColumnName("administered_by_id");
        entity.Property(immunization => immunization.AdministeredBy).HasColumnName("administered_by");
        entity.Property(immunization => immunization.EducationDate).HasColumnName("education_date");
        entity.Property(immunization => immunization.VisDate).HasColumnName("vis_date");
        entity.Property(immunization => immunization.AmountAdministered).HasColumnName("amount_administered");
        entity.Property(immunization => immunization.AmountAdministeredUnit).HasColumnName("amount_administered_unit");
        entity.Property(immunization => immunization.ExpirationDate).HasColumnName("expiration_date");
        entity.Property(immunization => immunization.Route).HasColumnName("route");
        entity.Property(immunization => immunization.AdministrationSite).HasColumnName("administration_site");
        entity.Property(immunization => immunization.CompletionStatus).HasColumnName("completion_status");
        entity.Property(immunization => immunization.InformationSource).HasColumnName("information_source");
        entity.Property(immunization => immunization.Note).HasColumnName("note");
        entity.Property(immunization => immunization.AddedErroneously).HasColumnName("added_erroneously");
    }
}
