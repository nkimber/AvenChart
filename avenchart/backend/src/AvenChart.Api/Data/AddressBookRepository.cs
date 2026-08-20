// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

public sealed class AddressBookRepository(
    NpgsqlDataSource dataSource,
    AvenChartDbContext dbContext)
{
    public async Task<AddressBookResponse> SearchAsync(
        string? organization,
        string? firstName,
        string? lastName,
        string? specialty,
        string? npi,
        string? type,
        bool externalOnly,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with entries as (
              select -s.id as id, true as internal, s.username, ''::text organization,
                     s.first_name, s.last_name, null::text specialty, s.npi, 'internal'::text type,
                     null::text phone, null::text mobile, null::text fax, s.email,
                     null::text street, null::text city, null::text state, null::text postal_code, s.active
              from staff s
              union all
              select id, false, null, organization, first_name, last_name, specialty, npi,
                     contact_type, phone, mobile, fax, email, street, city, state, postal_code, active
              from address_book_contacts
            )
            select *, count(*) over() total
            from entries
            where active
              and (@externalOnly = false or internal = false)
              and organization ilike @organization
              and first_name ilike @firstName
              and last_name ilike @lastName
              and coalesce(specialty, '') ilike @specialty
              and coalesce(npi, '') ilike @npi
              and (@type = '' or type = @type)
            order by organization, last_name, first_name
            limit 500;
            """;
        AddFilter(command, "organization", organization);
        AddFilter(command, "firstName", firstName);
        AddFilter(command, "lastName", lastName);
        AddFilter(command, "specialty", specialty);
        AddFilter(command, "npi", npi);
        command.Parameters.AddWithValue("type", type?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("externalOnly", externalOnly);

        var entries = new List<AddressBookEntry>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            total = Convert.ToInt32(reader.GetInt64(18));
            entries.Add(new AddressBookEntry(
                reader.GetInt32(0),
                reader.GetBoolean(1),
                ReadText(reader, 2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                ReadText(reader, 6),
                ReadText(reader, 7),
                reader.GetString(8),
                ReadText(reader, 9),
                ReadText(reader, 10),
                ReadText(reader, 11),
                ReadText(reader, 12),
                ReadText(reader, 13),
                ReadText(reader, 14),
                ReadText(reader, 15),
                ReadText(reader, 16),
                reader.GetBoolean(17)));
        }

        return new AddressBookResponse(entries, total);
    }

    public async Task<AddressBookEntry?> SaveAsync(
        int? id,
        AddressBookContactRequest request,
        CancellationToken cancellationToken)
    {
        var organization = Required(request.Organization, "Organization");
        var firstName = Required(request.FirstName, "First name");
        var lastName = Required(request.LastName, "Last name");

        AddressBookContactEntity contact;
        if (id is null)
        {
            contact = new AddressBookContactEntity
            {
                Organization = organization,
                FirstName = firstName,
                LastName = lastName,
                ContactType = request.Type?.Trim() ?? "external_provider"
            };
            dbContext.AddressBookContacts.Add(contact);
        }
        else
        {
            var existingContact = await dbContext.AddressBookContacts.SingleOrDefaultAsync(
                candidate => candidate.Id == id.Value,
                cancellationToken);
            if (existingContact is null)
            {
                return null;
            }

            contact = existingContact;
        }

        contact.Organization = organization;
        contact.FirstName = firstName;
        contact.LastName = lastName;
        contact.Specialty = NormalizeOptional(request.Specialty);
        contact.Npi = NormalizeOptional(request.Npi);
        contact.ContactType = request.Type?.Trim() ?? "external_provider";
        contact.Phone = NormalizeOptional(request.Phone);
        contact.Mobile = NormalizeOptional(request.Mobile);
        contact.Fax = NormalizeOptional(request.Fax);
        contact.Email = NormalizeOptional(request.Email);
        contact.Street = NormalizeOptional(request.Street);
        contact.City = NormalizeOptional(request.City);
        contact.State = NormalizeOptional(request.State);
        contact.PostalCode = NormalizeOptional(request.PostalCode);
        contact.Active = request.Active ?? true;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToEntry(contact);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await dbContext.AddressBookContacts
            .Where(contact => contact.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    private static AddressBookEntry ToEntry(AddressBookContactEntity contact) =>
        new(
            contact.Id,
            false,
            null,
            contact.Organization,
            contact.FirstName,
            contact.LastName,
            contact.Specialty,
            contact.Npi,
            contact.ContactType,
            contact.Phone,
            contact.Mobile,
            contact.Fax,
            contact.Email,
            contact.Street,
            contact.City,
            contact.State,
            contact.PostalCode,
            contact.Active);

    private static void AddFilter(NpgsqlCommand command, string key, string? value) =>
        command.Parameters.AddWithValue(key, $"%{value?.Trim() ?? string.Empty}%");

    private static string? ReadText(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 120)
        {
            throw new ArgumentException($"{name} is required and must be 120 characters or fewer.");
        }

        return value.Trim();
    }
}
