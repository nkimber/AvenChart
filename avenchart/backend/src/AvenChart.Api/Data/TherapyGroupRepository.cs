using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class TherapyGroupRepository(NpgsqlDataSource dataSource)
{
    public async Task<TherapyGroupsResponse> GetAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select id, name, status, facilitator_id, description, capacity, created_at from therapy_groups order by created_at desc;";
        var groups = new List<TherapyGroupItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) groups.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O")));
        return new(groups);
    }

    public async Task<TherapyGroupItem> CreateAsync(TherapyGroupCreateRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim(); if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Group name is required and must be 120 characters or fewer.");
        var capacity = Math.Clamp(request.Capacity, 1, 200); var id = Guid.NewGuid(); var created = DateTimeOffset.UtcNow;
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "insert into therapy_groups (id, name, status, facilitator_id, description, capacity, created_at) values (@id, @name, 'active', @facilitator, @description, @capacity, @created);";
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("name", name); command.Parameters.AddWithValue("facilitator", (object?)request.FacilitatorId ?? DBNull.Value); command.Parameters.AddWithValue("description", (object?)request.Description?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue("capacity", capacity); command.Parameters.AddWithValue("created", created); await command.ExecuteNonQueryAsync(cancellationToken);
        return new(id, name, "active", request.FacilitatorId, request.Description?.Trim(), capacity, created.ToString("O"));
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "create table if not exists therapy_groups (id uuid primary key, name text not null, status text not null, facilitator_id integer references staff(id), description text, capacity integer not null, created_at timestamptz not null);"; await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
