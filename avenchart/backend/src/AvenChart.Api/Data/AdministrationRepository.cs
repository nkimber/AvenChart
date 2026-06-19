using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AdministrationRepository(NpgsqlDataSource dataSource)
{
    private const string DefaultFacilityColor = "#246b73";
    private const string DefaultUserEmailDomain = "example.test";

    public async Task<AdministrationDirectoryResponse> GetDirectoryAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await GetUsersAsync(connection, cancellationToken);
        var facilities = await GetFacilitiesAsync(connection, cancellationToken);
        var accessControl = await GetAccessControlAsync(connection, cancellationToken);

        return new AdministrationDirectoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Counts: new AdministrationDirectoryCounts(
                Users: users.Count,
                Providers: users.Count(user => string.Equals(user.Role, "provider", StringComparison.OrdinalIgnoreCase)),
                CalendarUsers: users.Count(user => user.Calendar),
                Facilities: facilities.Count,
                AccessGroups: accessControl.Groups.Count,
                AccessPermissions: accessControl.Permissions.Count,
                AccessGroupPermissions: accessControl.GroupPermissions.Count),
            Users: users,
            Facilities: facilities,
            AccessControl: accessControl);
    }

    public async Task<AdministrationUserMutationResponse> CreateUserAsync(
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextStaffIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into staff
              (id, username, first_name, last_name, role, calendar, facility_id, email, npi, active)
            values
              (@id, @username, @firstName, @lastName, @role, @calendar, @facilityId, @email, @npi, @active)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddUserParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User create did not return an ID."));

        return new AdministrationUserMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse?> UpdateUserAsync(
        int userId,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update staff
            set username = @username,
                first_name = @firstName,
                last_name = @lastName,
                role = @role,
                calendar = @calendar,
                facility_id = @facilityId,
                email = @email,
                npi = @npi,
                active = @active
            where id = @userId
            returning id;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        AddUserParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationUserMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from staff
            where id = @userId;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<AdministrationFacilityMutationResponse> CreateFacilityAsync(
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextFacilityIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into facilities
              (id, code, name, phone, street, city, state, postal_code, color, inactive)
            values
              (@id, @code, @name, @phone, @street, @city, @state, @postalCode, @color, @inactive)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddFacilityParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility create did not return an ID."));

        return new AdministrationFacilityMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationFacilityMutationResponse?> UpdateFacilityAsync(
        int facilityId,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update facilities
            set code = @code,
                name = @name,
                phone = @phone,
                street = @street,
                city = @city,
                state = @state,
                postal_code = @postalCode,
                color = @color,
                inactive = @inactive
            where id = @facilityId
            returning id;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        AddFacilityParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationFacilityMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteFacilityAsync(int facilityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from facilities
            where id = @facilityId;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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
                s.active,
                s.calendar,
                s.facility_id,
                f.name as facility_name,
                s.email,
                s.npi
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
                Active: reader.GetBoolean(reader.GetOrdinal("active")),
                Calendar: reader.GetBoolean(reader.GetOrdinal("calendar")),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Email: ReadNullableString(reader, "email") ?? $"{username}@{DefaultUserEmailDomain}",
                Npi: ReadNullableString(reader, "npi") ?? (isProvider ? $"18888{id}" : null)));
        }

        return users;
    }

    private static async Task<IReadOnlyList<AdministrationFacilityItem>> GetFacilitiesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, code, name, phone, street, city, state, postal_code, color, inactive
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
                Active: !reader.GetBoolean(reader.GetOrdinal("inactive")),
                Phone: ReadNullableString(reader, "phone"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                Color: ReadNullableString(reader, "color")));
        }

        return facilities;
    }

    private static async Task<AdministrationAccessControlSummary> GetAccessControlAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var groups = new List<AdministrationAccessGroupItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    g.id,
                    g.value,
                    g.name,
                    g.parent_id,
                    count(gp.*) as permission_count
                from access_groups g
                left join access_group_permissions gp on gp.group_value = g.value
                group by g.id, g.value, g.name, g.parent_id
                order by g.id;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groups.Add(new AdministrationAccessGroupItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    ParentId: ReadNullableInt(reader, "parent_id"),
                    PermissionCount: (int)reader.GetInt64(reader.GetOrdinal("permission_count"))));
            }
        }

        var permissions = new List<AdministrationAccessPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select section_value, value, name
                from access_permissions
                order by section_value, value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new AdministrationAccessPermissionItem(
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name"))));
            }
        }

        var groupPermissions = new List<AdministrationAccessGroupPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select group_value, section_value, permission_value, permission_name, return_value
                from access_group_permissions
                order by group_value, section_value, permission_value, return_value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groupPermissions.Add(new AdministrationAccessGroupPermissionItem(
                    GroupValue: reader.GetString(reader.GetOrdinal("group_value")),
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    PermissionValue: reader.GetString(reader.GetOrdinal("permission_value")),
                    PermissionName: reader.GetString(reader.GetOrdinal("permission_name")),
                    ReturnValue: reader.GetString(reader.GetOrdinal("return_value"))));
            }
        }

        return new AdministrationAccessControlSummary(groups, permissions, groupPermissions);
    }

    private async Task<int> GetNextStaffIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from staff;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User ID allocation failed."));
    }

    private async Task<int> GetNextFacilityIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from facilities;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility ID allocation failed."));
    }

    private static void AddUserParameters(
        NpgsqlCommand command,
        AdministrationUserMutationRequest request,
        bool defaultActive)
    {
        var username = NormalizeRequired(request.Username, "Username");
        var role = NormalizeRequired(request.Role, "Role").ToLowerInvariant();
        command.Parameters.Add("username", NpgsqlDbType.Text).Value = username;
        command.Parameters.Add("firstName", NpgsqlDbType.Text).Value = NormalizeRequired(request.FirstName, "First name");
        command.Parameters.Add("lastName", NpgsqlDbType.Text).Value = NormalizeRequired(request.LastName, "Last name");
        command.Parameters.Add("role", NpgsqlDbType.Text).Value = role;
        command.Parameters.Add("calendar", NpgsqlDbType.Boolean).Value = request.Calendar ?? string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);
        AddNullableInt(command, "facilityId", request.FacilityId);
        AddNullableText(command, "email", request.Email ?? $"{username}@{DefaultUserEmailDomain}");
        AddNullableText(command, "npi", request.Npi);
        command.Parameters.Add("active", NpgsqlDbType.Boolean).Value = request.Active ?? defaultActive;
    }

    private static void AddFacilityParameters(
        NpgsqlCommand command,
        AdministrationFacilityMutationRequest request,
        bool defaultActive)
    {
        command.Parameters.Add("code", NpgsqlDbType.Text).Value = NormalizeRequired(request.Code, "Facility code");
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = NormalizeRequired(request.Name, "Facility name");
        AddNullableText(command, "phone", request.Phone);
        AddNullableText(command, "street", request.Street);
        AddNullableText(command, "city", request.City);
        AddNullableText(command, "state", request.State);
        AddNullableText(command, "postalCode", request.PostalCode);
        command.Parameters.Add("color", NpgsqlDbType.Text).Value = NormalizeOptional(request.Color) ?? DefaultFacilityColor;
        command.Parameters.Add("inactive", NpgsqlDbType.Boolean).Value = !(request.Active ?? defaultActive);
    }

    private static string NormalizeRequired(string? value, string label)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{label} is required.")
            : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = NormalizeOptional(value) is { } normalized
            ? normalized
            : DBNull.Value;
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value = value is { } integer
            ? integer
            : DBNull.Value;
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
