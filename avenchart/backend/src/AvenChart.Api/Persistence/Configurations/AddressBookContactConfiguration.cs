// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AvenChart.Api.Persistence.Configurations;

public sealed class AddressBookContactConfiguration : IEntityTypeConfiguration<AddressBookContactEntity>
{
    public void Configure(EntityTypeBuilder<AddressBookContactEntity> entity)
    {
        entity.ToTable("address_book_contacts", table => table.ExcludeFromMigrations());
        entity.HasKey(contact => contact.Id);
        entity.Property(contact => contact.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(contact => contact.Organization).HasColumnName("organization").IsRequired();
        entity.Property(contact => contact.FirstName).HasColumnName("first_name").IsRequired();
        entity.Property(contact => contact.LastName).HasColumnName("last_name").IsRequired();
        entity.Property(contact => contact.Specialty).HasColumnName("specialty");
        entity.Property(contact => contact.Npi).HasColumnName("npi");
        entity.Property(contact => contact.ContactType).HasColumnName("contact_type").IsRequired();
        entity.Property(contact => contact.Phone).HasColumnName("phone");
        entity.Property(contact => contact.Mobile).HasColumnName("mobile");
        entity.Property(contact => contact.Fax).HasColumnName("fax");
        entity.Property(contact => contact.Email).HasColumnName("email");
        entity.Property(contact => contact.Street).HasColumnName("street");
        entity.Property(contact => contact.City).HasColumnName("city");
        entity.Property(contact => contact.State).HasColumnName("state");
        entity.Property(contact => contact.PostalCode).HasColumnName("postal_code");
        entity.Property(contact => contact.Active).HasColumnName("active").IsRequired();
        entity.HasIndex(contact => new { contact.Organization, contact.LastName, contact.FirstName })
            .HasDatabaseName("ix_address_book_contacts_search");
    }
}
