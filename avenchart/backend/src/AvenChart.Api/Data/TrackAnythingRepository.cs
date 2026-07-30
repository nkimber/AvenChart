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
        var isLocked = await IsEncounterLockedAsync(connection, encounter, cancellationToken);
        var definitions = await GetDefinitionsAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select record_id,encounter,track_type_id,track_name,created_at,created_by from encounter_track_records where encounter=@encounter order by created_at desc,record_id desc;";
        command.Parameters.AddWithValue("encounter", encounter);
        var records = new List<TrackAnythingEncounterRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) records.Add(ReadRecord(reader));
        return new TrackAnythingEncounterCatalog(encounter, definitions, records, isLocked);
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
        command.CommandText = "select r.reading_id,r.recorded_at,r.recorded_by,r.updated_at,r.updated_by,v.item_type_id,v.item_name,v.value from encounter_track_readings r join encounter_track_reading_values v on v.reading_id=r.reading_id where r.record_id=@record order by r.recorded_at desc,r.reading_id desc,v.item_name;";
        command.Parameters.AddWithValue("record", recordId);
        var readings = new Dictionary<Guid, (DateTimeOffset RecordedAt, string RecordedBy, DateTimeOffset? UpdatedAt, string? UpdatedBy, List<TrackAnythingReadingValue> Values)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var readingId = reader.GetGuid(0);
            if (!readings.TryGetValue(readingId, out var reading)) reading = (reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetString(4), []);
            reading.Values.Add(new(reader.GetInt32(5), reader.GetString(6), reader.GetString(7)));
            readings[readingId] = reading;
        }
        return new TrackAnythingEncounterRecordDetail(record, items, readings.Select(pair => new TrackAnythingReading(pair.Key, pair.Value.RecordedAt.ToString("O"), pair.Value.RecordedBy, pair.Value.UpdatedAt?.ToString("O"), pair.Value.UpdatedBy, pair.Value.Values)).ToList());
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
        return new TrackAnythingReading(readingId, recordedAt.ToString("O"), username, null, null, activeItems.Select(item => new TrackAnythingReadingValue(item.Id, item.Name, values[item.Id])).ToList());
    }

    public async Task<TrackAnythingReading?> UpdateReadingAsync(int encounter, Guid recordId, Guid readingId, TrackAnythingReadingUpdateRequest request, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await GetRecordAsync(connection, encounter, recordId, cancellationToken, transaction) is null) return null;
        if (!await ReadingBelongsToRecordAsync(connection, recordId, readingId, cancellationToken, transaction)) return null;
        var existingItems = new Dictionary<int, string>();
        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = "select item_type_id,item_name from encounter_track_reading_values where reading_id=@reading;";
            existingCommand.Parameters.AddWithValue("reading", readingId);
            await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) existingItems[reader.GetInt32(0)] = reader.GetString(1);
        }
        if (existingItems.Count == 0) return null;
        var submitted = request.Values ?? [];
        if (submitted.Count != existingItems.Count || submitted.Select(value => value.ItemTypeId).Distinct().Count() != submitted.Count || submitted.Any(value => !existingItems.ContainsKey(value.ItemTypeId))) throw new ArgumentException("An edited reading must include every originally captured item exactly once.");
        var values = submitted.ToDictionary(value => value.ItemTypeId, value => (value.Value ?? string.Empty).Trim());
        if (values.Values.Any(value => value.Length > 1000)) throw new ArgumentException("Track values must be 1,000 characters or fewer.");
        if (values.Values.All(string.IsNullOrWhiteSpace)) throw new ArgumentException("Enter a value for at least one item before saving the reading.");
        string recordedBy;
        DateTimeOffset updatedAt;
        await using (var updateReading = connection.CreateCommand())
        {
            updateReading.Transaction = transaction;
            updateReading.CommandText = "update encounter_track_readings set recorded_at=@at,updated_at=now(),updated_by=@user where reading_id=@reading returning recorded_by,updated_at;";
            updateReading.Parameters.AddWithValue("at", request.RecordedAt);
            updateReading.Parameters.AddWithValue("user", username);
            updateReading.Parameters.AddWithValue("reading", readingId);
            await using var reader = await updateReading.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            recordedBy = reader.GetString(0);
            updatedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }
        foreach (var pair in values)
        {
            await using var updateValue = connection.CreateCommand();
            updateValue.Transaction = transaction;
            updateValue.CommandText = "update encounter_track_reading_values set value=@value where reading_id=@reading and item_type_id=@item;";
            updateValue.Parameters.AddWithValue("value", pair.Value);
            updateValue.Parameters.AddWithValue("reading", readingId);
            updateValue.Parameters.AddWithValue("item", pair.Key);
            await updateValue.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new TrackAnythingReading(readingId, request.RecordedAt.ToString("O"), recordedBy, updatedAt.ToString("O"), username, existingItems.Select(pair => new TrackAnythingReadingValue(pair.Key, pair.Value, values[pair.Key])).ToList());
    }

    public async Task<TrackAnythingPatientHistoryResponse?> GetPatientHistoryAsync(string patientId, CancellationToken cancellationToken)
    {
        var normalizedPatientId = string.IsNullOrWhiteSpace(patientId) ? string.Empty : patientId.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var patientCommand = connection.CreateCommand())
        {
            patientCommand.CommandText = "select canonical_id from patients where canonical_id=@patient;";
            patientCommand.Parameters.AddWithValue("patient", normalizedPatientId);
            if (await patientCommand.ExecuteScalarAsync(cancellationToken) is not string canonicalId) return null;
            normalizedPatientId = canonicalId;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select r.track_type_id,r.track_name,r.record_id,r.encounter,e.encounter_date,
                   reading.reading_id,reading.recorded_at,reading.recorded_by,reading.updated_at,reading.updated_by,
                   value.item_type_id,value.item_name,value.value
            from encounter_track_records r
            join encounters e on e.encounter=r.encounter
            join encounter_track_readings reading on reading.record_id=r.record_id
            join encounter_track_reading_values value on value.reading_id=reading.reading_id
            where e.patient_id=@patient
            order by r.track_type_id,e.encounter_date desc,r.encounter desc,reading.recorded_at desc,reading.reading_id desc,value.item_name;
            """;
        command.Parameters.AddWithValue("patient", normalizedPatientId);

        var tracks = new Dictionary<int, PatientTrackAccumulator>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var trackTypeId = reader.GetInt32(0);
            var track = tracks.TryGetValue(trackTypeId, out var existingTrack)
                ? existingTrack
                : tracks[trackTypeId] = new PatientTrackAccumulator(reader.GetString(1));
            var recordId = reader.GetGuid(2);
            var encounter = track.GetOrAddEncounter(recordId, reader.GetInt32(3), reader.GetFieldValue<DateOnly>(4).ToString("yyyy-MM-dd"), reader.GetString(1));
            var readingId = reader.GetGuid(5);
            var reading = encounter.GetOrAddReading(readingId, reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetString(9));
            reading.Values.Add(new TrackAnythingReadingValue(reader.GetInt32(10), reader.GetString(11), reader.GetString(12)));
        }

        return new TrackAnythingPatientHistoryResponse(normalizedPatientId, tracks.Select(track => track.Value.ToDto(track.Key)).ToList());
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

    private static async Task<bool> IsEncounterLockedAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from encounter_signatures where encounter=@encounter and is_lock);";
        command.Parameters.AddWithValue("encounter", encounter);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> ReadingBelongsToRecordAsync(NpgsqlConnection connection, Guid recordId, Guid readingId, CancellationToken cancellationToken, NpgsqlTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from encounter_track_readings where reading_id=@reading and record_id=@record);";
        command.Parameters.AddWithValue("reading", readingId);
        command.Parameters.AddWithValue("record", recordId);
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

    private sealed class PatientTrackAccumulator(string trackName)
    {
        private readonly Dictionary<Guid, PatientEncounterAccumulator> encounters = [];

        public PatientEncounterAccumulator GetOrAddEncounter(Guid recordId, int encounter, string encounterDate, string historicalTrackName) =>
            encounters.TryGetValue(recordId, out var item) ? item : encounters[recordId] = new PatientEncounterAccumulator(recordId, encounter, encounterDate, historicalTrackName);

        public TrackAnythingPatientTrackHistory ToDto(int trackTypeId) => new(trackTypeId, trackName, encounters.Values.Select(encounter => encounter.ToDto()).ToList());
    }

    private sealed class PatientEncounterAccumulator(Guid recordId, int encounter, string encounterDate, string trackName)
    {
        private readonly Dictionary<Guid, PatientReadingAccumulator> readings = [];

        public PatientReadingAccumulator GetOrAddReading(Guid readingId, DateTimeOffset recordedAt, string recordedBy, DateTimeOffset? updatedAt, string? updatedBy) =>
            readings.TryGetValue(readingId, out var item) ? item : readings[readingId] = new PatientReadingAccumulator(recordedAt, recordedBy, updatedAt, updatedBy);

        public TrackAnythingPatientHistoryEncounter ToDto() => new(recordId, encounter, encounterDate, trackName, readings.Select(reading => reading.Value.ToDto(reading.Key)).ToList());
    }

    private sealed class PatientReadingAccumulator(DateTimeOffset recordedAt, string recordedBy, DateTimeOffset? updatedAt, string? updatedBy)
    {
        public List<TrackAnythingReadingValue> Values { get; } = [];
        public TrackAnythingPatientHistoryReading ToDto(Guid readingId) => new(readingId, recordedAt.ToString("O"), recordedBy, updatedAt?.ToString("O"), updatedBy, Values);
    }
}
