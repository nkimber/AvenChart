using System.Data.Common;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AdministrationRepository(NpgsqlDataSource dataSource)
{
    public async Task<AdministrationDirectoryResponse> GetDirectoryAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await GetUsersAsync(connection, cancellationToken);
        var facilities = await GetFacilitiesAsync(connection, cancellationToken);

        return new AdministrationDirectoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Counts: new AdministrationDirectoryCounts(
                Users: users.Count,
                Providers: users.Count(user => string.Equals(user.Role, "provider", StringComparison.OrdinalIgnoreCase)),
                CalendarUsers: users.Count(user => user.Calendar),
                Facilities: facilities.Count),
            Users: users,
            Facilities: facilities);
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<IReadOnlyList<AdministrationUserItem>> GetUsersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                s.id,
                s.username,
                s.first_name,
                s.last_name,
                s.role,
                s.calendar,
                s.facility_id,
                f.name as facility_name
            from staff s
            left join facilities f on f.id = s.facility_id
            order by s.id;
            """;

        var users = new List<AdministrationUserItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            var username = reader.GetString(reader.GetOrdinal("username"));
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var role = reader.GetString(reader.GetOrdinal("role"));
            var isProvider = string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);

            users.Add(new AdministrationUserItem(
                Id: id,
                Username: username,
                FirstName: firstName,
                LastName: lastName,
                DisplayName: $"{lastName}, {firstName}",
                Role: role,
                Authorized: isProvider,
                Active: true,
                Calendar: reader.GetBoolean(reader.GetOrdinal("calendar")),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Email: $"{username}@example.test",
                Npi: isProvider ? $"18888{id}" : null));
        }

        return users;
    }

    private static async Task<IReadOnlyList<AdministrationFacilityItem>> GetFacilitiesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, code, name, phone, street, city, state, postal_code, color
            from facilities
            order by id;
            """;

        var facilities = new List<AdministrationFacilityItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facilities.Add(new AdministrationFacilityItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Code: reader.GetString(reader.GetOrdinal("code")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                Phone: ReadNullableString(reader, "phone"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                Color: ReadNullableString(reader, "color")));
        }

        return facilities;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);
}
