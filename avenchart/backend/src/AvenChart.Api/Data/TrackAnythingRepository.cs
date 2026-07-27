using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class TrackAnythingRepository(NpgsqlDataSource dataSource)
{
    public async Task<TrackAnythingResponse> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select id,parent_id,name,description,position,active from track_anything_types order by coalesce(parent_id,0),position,active desc,name;";
        var items = new List<TrackAnythingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadItem(reader));
        return new TrackAnythingResponse(items);
    }

    public async Task<TrackAnythingItem?> SaveAsync(int? id, TrackAnythingRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (request.ParentId is not null && !await ExistsAsync(connection, request.ParentId.Value, cancellationToken)) throw new ArgumentException("Parent track does not exist.");
        if (id is not null && request.ParentId == id) throw new ArgumentException("A track cannot be its own parent.");
        await using var command = connection.CreateCommand();
        command.CommandText = id is null
            ? "insert into track_anything_types(parent_id,name,description,position,active) values(@parent,@name,@description,@position,@active) returning id,parent_id,name,description,position,active;"
            : "update track_anything_types set parent_id=@parent,name=@name,description=@description,position=@position,active=@active where id=@id returning id,parent_id,name,description,position,active;";
        if (id is not null) command.Parameters.AddWithValue("id", id.Value);
        command.Parameters.AddWithValue("parent", (object?)request.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(request.Description) ? DBNull.Value : request.Description.Trim());
        command.Parameters.AddWithValue("position", request.Position);
        command.Parameters.AddWithValue("active", request.Active ?? true);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadItem(reader) : null;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from track_anything_types where id=@id;";
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<TrackAnythingEncounterCatalog?> GetEncounterCatalogAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await EncounterExistsAsync(connection, encounter, cancellationToken)) return null;
        var definitions = await GetDefinitionsAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select record_id,encounter,track_type_id,track_name,created_at,created_by from encounter_track_records where encounter=@encounter order by created_at desc,record_id desc;";
        command.Parameters.AddWithValue("encounter", encounter);
        var records = new List<TrackAnythingEncounterRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) records.Add(ReadRecord(reader));
        return new TrackAnythingEncounterCatalog(encounter, definitions, records);
    }

    public async Task<TrackAnythingEncounterRecord?> CreateEncounterRecordAsync(int encounter, TrackAnythingEncounterRecordCreateRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await EncounterExistsAsync(connection, encounter, cancellationToken, transaction)) return null;
        await using var typeCommand = connection.CreateCommand();
        typeCommand.Transaction = transaction;
        typeCommand.CommandText = "select name from track_anything_types where id=@id and parent_id is null and active=true;";
        typeCommand.Parameters.AddWithValue("id", request.TrackTypeId);
        var trackName = await typeCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (trackName is null) throw new ArgumentException("Select an active top-level track.");
        var recordId = Guid.NewGuid();
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = "insert into encounter_track_records(record_id,encounter,track_type_id,track_name,created_at,created_by) values(@id,@encounter,@type,@name,now(),@user) returning record_id,encounter,track_type_id,track_name,created_at,created_by;";
        insertCommand.Parameters.AddWithValue("id", recordId);
        insertCommand.Parameters.AddWithValue("encounter", encounter);
        insertCommand.Parameters.AddWithValue("type", request.TrackTypeId);
        insertCommand.Parameters.AddWithValue("name", trackName);
        insertCommand.Parameters.AddWithValue("user", username);
        await using var reader = await insertCommand.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var result = ReadRecord(reader);
        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<TrackAnythingEncounterRecordDetail?> GetEncounterRecordAsync(int encounter, Guid recordId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var record = await GetRecordAsync(connection, encounter, recordId, cancellationToken);
        if (record is null) return null;
        var items = await GetDirectItemsAsync(connection, record.TrackTypeId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select r.reading_id,r.recorded_at,r.recorded_by,v.item_type_id,v.item_name,v.value from encounter_track_readings r join encounter_track_reading_values v on v.reading_id=r.reading_id where r.record_id=@record order by r.recorded_at desc,r.reading_id desc,v.item_name;";
        command.Parameters.AddWithValue("record", recordId);
        var readings = new Dictionary<Guid, (DateTimeOffset RecordedAt, string RecordedBy, List<TrackAnythingReadingValue> Values)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var readingId = reader.GetGuid(0);
            if (!readings.TryGetValue(readingId, out var reading)) reading = (reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), []);
            reading.Values.Add(new(reader.GetInt32(3), reader.GetString(4), reader.GetString(5)));
            readings[readingId] = reading;
        }
        return new TrackAnythingEncounterRecordDetail(record, items, readings.Select(pair => new TrackAnythingReading(pair.Key, pair.Value.RecordedAt.ToString("O"), pair.Value.RecordedBy, pair.Value.Values)).ToList());
    }

    public async Task<TrackAnythingReading?> AddReadingAsync(int encounter, Guid recordId, TrackAnythingReadingCreateRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var record = await GetRecordAsync(connection, encounter, recordId, cancellationToken, transaction);
        if (record is null) return null;
        var activeItems = await GetDirectItemsAsync(connection, record.TrackTypeId, cancellationToken, transaction);
        var submitted = request.Values ?? [];
        if (submitted.Count != activeItems.Count || submitted.Select(value => value.ItemTypeId).Distinct().Count() != submitted.Count || submitted.Any(value => activeItems.All(item => item.Id != value.ItemTypeId))) throw new ArgumentException("A reading must include every active item for the selected track exactly once.");
        var values = submitted.ToDictionary(value => value.ItemTypeId, value => (value.Value ?? string.Empty).Trim());
        if (values.Values.Any(value => value.Length > 1000)) throw new ArgumentException("Track values must be 1,000 characters or fewer.");
        if (values.Values.All(string.IsNullOrWhiteSpace)) throw new ArgumentException("Enter a value for at least one item before saving the reading.");
        var readingId = Guid.NewGuid();
        var recordedAt = request.RecordedAt ?? DateTimeOffset.UtcNow;
        await using (var insertReading = connection.CreateCommand())
        {
            insertReading.Transaction = transaction;
            insertReading.CommandText = "insert into encounter_track_readings(reading_id,record_id,recorded_at,recorded_by) values(@id,@record,@at,@user);";
            insertReading.Parameters.AddWithValue("id", readingId);
            insertReading.Parameters.AddWithValue("record", recordId);
            insertReading.Parameters.AddWithValue("at", recordedAt);
            insertReading.Parameters.AddWithValue("user", username);
            await insertReading.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var item in activeItems)
        {
            await using var insertValue = connection.CreateCommand();
            insertValue.Transaction = transaction;
            insertValue.CommandText = "insert into encounter_track_reading_values(reading_id,item_type_id,item_name,value) values(@reading,@item,@name,@value);";
            insertValue.Parameters.AddWithValue("reading", readingId);
            insertValue.Parameters.AddWithValue("item", item.Id);
            insertValue.Parameters.AddWithValue("name", item.Name);
            insertValue.Parameters.AddWithValue("value", values[item.Id]);
            await insertValue.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new TrackAnythingReading(readingId, recordedAt.ToString("O"), username, activeItems.Select(item => new TrackAnythingReadingValue(item.Id, item.Name, values[item.Id])).ToList());
    }

    private static async Task<List<TrackAnythingDefinition>> GetDefinitionsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select id,parent_id,name,description,position,active from track_anything_types where active=true order by coalesce(parent_id,0),position,name;";
        var allItems = new List<TrackAnythingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) allItems.Add(ReadItem(reader));
        return allItems.Where(item => item.ParentId is null).Select(track => new TrackAnythingDefinition(track.Id, track.Name, track.Description, allItems.Where(item => item.ParentId == track.Id).ToList())).ToList();
    }

    private static async Task<List<TrackAnythingItem>> GetDirectItemsAsync(NpgsqlConnection connection, int trackTypeId, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id,parent_id,name,description,position,active from track_anything_types where parent_id=@parent and active=true order by position,name;";
        command.Parameters.AddWithValue("parent", trackTypeId);
        var items = new List<TrackAnythingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadItem(reader));
        return items;
    }

    private static async Task<TrackAnythingEncounterRecord?> GetRecordAsync(NpgsqlConnection connection, int encounter, Guid recordId, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select record_id,encounter,track_type_id,track_name,created_at,created_by from encounter_track_records where encounter=@encounter and record_id=@record;";
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("record", recordId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    private static async Task<bool> EncounterExistsAsync(NpgsqlConnection connection, int encounter, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from encounters where encounter=@encounter);";
        command.Parameters.AddWithValue("encounter", encounter);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, int id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from track_anything_types where id=@id);";
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static TrackAnythingItem ReadItem(NpgsqlDataReader reader) => new(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5));
    private static TrackAnythingEncounterRecord ReadRecord(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4).ToString("O"), reader.GetString(5));
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 120 ? throw new ArgumentException("Name is required and must be 120 characters or fewer.") : value.Trim();
}
