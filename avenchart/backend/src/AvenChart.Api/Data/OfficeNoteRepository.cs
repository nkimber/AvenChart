using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class OfficeNoteRepository(NpgsqlDataSource dataSource)
{
    public async Task<OfficeNotesResponse> GetAsync(string activity, int offset, int limit, CancellationToken cancellationToken)
    {
        var active = activity.ToLowerInvariant() switch { "active" => true, "inactive" => false, _ => (bool?)null };
        offset = Math.Max(0, offset); limit = Math.Clamp(limit, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, body, author, group_name, active, created_at, updated_at, count(*) over() as total
            from office_notes where @hasActivity = false or active = @active
            order by created_at desc, id desc offset @offset limit @limit;
            """;
        command.Parameters.AddWithValue("hasActivity", active.HasValue);
        command.Parameters.AddWithValue("active", active ?? false);
        command.Parameters.AddWithValue("offset", offset); command.Parameters.AddWithValue("limit", limit);
        var notes = new List<OfficeNoteItem>(); var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            total = Convert.ToInt32(reader.GetInt64(7));
            notes.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4), reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetFieldValue<DateTimeOffset>(6).ToString("O")));
        }
        return new(notes, total);
    }

    public async Task<OfficeNoteItem?> CreateAsync(string? body, string author, CancellationToken cancellationToken)
    {
        var text = Normalize(body); if (text is null) return null;
        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """insert into office_notes (id, body, author, active) values (@id, @body, @author, true) returning id, body, author, group_name, active, created_at, updated_at;""";
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("body", text); command.Parameters.AddWithValue("author", author);
        return await ReadOneAsync(command, cancellationToken);
    }

    public async Task<OfficeNoteItem?> UpdateAsync(Guid id, string? body, CancellationToken cancellationToken)
    {
        var text = Normalize(body); if (text is null) return null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """update office_notes set body = @body, updated_at = now() where id = @id returning id, body, author, group_name, active, created_at, updated_at;""";
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("body", text);
        return await ReadOneAsync(command, cancellationToken);
    }

    public async Task<OfficeNoteItem?> SetActivityAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """update office_notes set active = @active, updated_at = now() where id = @id returning id, body, author, group_name, active, created_at, updated_at;""";
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("active", active);
        return await ReadOneAsync(command, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "delete from office_notes where id = @id;"; command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<OfficeNoteItem?> ReadOneAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4), reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetFieldValue<DateTimeOffset>(6).ToString("O"));
    }

    private static string? Normalize(string? body) => string.IsNullOrWhiteSpace(body) ? null : body.Trim().Length > 4000 ? null : body.Trim();
}
