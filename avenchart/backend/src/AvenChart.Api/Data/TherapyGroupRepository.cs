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

    public async Task<IReadOnlyList<TherapyGroupMemberItem>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select m.group_id, p.canonical_id, p.legacy_pid,
              coalesce(nullif(trim(concat_ws(' ', p.preferred_name, p.last_name)), ''), p.canonical_id), m.joined_at
            from therapy_group_members m
            inner join patients p on p.canonical_id = m.patient_id
            where m.group_id = @groupId
            order by m.joined_at;
            """;
        command.Parameters.AddWithValue("groupId", groupId);
        var members = new List<TherapyGroupMemberItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            members.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4).ToString("O")));
        return members;
    }

    public async Task<TherapyGroupMemberItem> AddMemberAsync(Guid groupId, TherapyGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var patientId = request.PatientId?.Trim();
        if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("Patient identifier is required.");
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var groupCommand = connection.CreateCommand();
        groupCommand.Transaction = transaction;
        groupCommand.CommandText = "select status, capacity, (select count(*) from therapy_group_members where group_id = @groupId) from therapy_groups where id = @groupId for update;";
        groupCommand.Parameters.AddWithValue("groupId", groupId);
        await using var groupReader = await groupCommand.ExecuteReaderAsync(cancellationToken);
        if (!await groupReader.ReadAsync(cancellationToken)) throw new ArgumentException("Therapy group was not found.");
        var status = groupReader.GetString(0); var capacity = groupReader.GetInt32(1); var currentCount = groupReader.GetInt64(2);
        await groupReader.DisposeAsync();
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Members can only be added to an active therapy group.");
        if (currentCount >= capacity) throw new ArgumentException("The therapy group is at capacity.");

        await using var patientCommand = connection.CreateCommand();
        patientCommand.Transaction = transaction;
        patientCommand.CommandText = """
            select canonical_id, legacy_pid,
              coalesce(nullif(trim(concat_ws(' ', preferred_name, last_name)), ''), canonical_id)
            from patients
            where lower(canonical_id) = lower(@patientId) or lower(pubpid) = lower(@patientId)
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patientId", patientId);
        await using var patientReader = await patientCommand.ExecuteReaderAsync(cancellationToken);
        if (!await patientReader.ReadAsync(cancellationToken)) throw new ArgumentException("Patient was not found.");
        var canonicalPatientId = patientReader.GetString(0); var legacyPid = patientReader.GetInt32(1); var displayName = patientReader.GetString(2);
        await patientReader.DisposeAsync();

        var joinedAt = DateTimeOffset.UtcNow;
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = "insert into therapy_group_members (group_id, patient_id, joined_at) values (@groupId, @patientId, @joinedAt);";
        insertCommand.Parameters.AddWithValue("groupId", groupId); insertCommand.Parameters.AddWithValue("patientId", canonicalPatientId); insertCommand.Parameters.AddWithValue("joinedAt", joinedAt);
        try { await insertCommand.ExecuteNonQueryAsync(cancellationToken); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) { throw new ArgumentException("Patient is already a member of this therapy group."); }
        await transaction.CommitAsync(cancellationToken);
        return new(groupId, canonicalPatientId, legacyPid, displayName, joinedAt.ToString("O"));
    }

    public async Task<IReadOnlyList<TherapyGroupSessionItem>> GetSessionsAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select id, group_id, starts_at, duration_minutes, topic, status, created_at from therapy_group_sessions where group_id = @groupId order by starts_at desc;";
        command.Parameters.AddWithValue("groupId", groupId);
        var sessions = new List<TherapyGroupSessionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            sessions.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2).ToString("O"), reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O")));
        return sessions;
    }

    public async Task<TherapyGroupSessionItem> CreateSessionAsync(Guid groupId, TherapyGroupSessionCreateRequest request, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(request.StartsAt, out var startsAt)) throw new ArgumentException("A valid session start date and time is required.");
        if (request.DurationMinutes is < 15 or > 480) throw new ArgumentException("Session duration must be between 15 and 480 minutes.");
        var topic = request.Topic?.Trim(); if (topic?.Length > 400) throw new ArgumentException("Session topic must be 400 characters or fewer.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var groupCommand = connection.CreateCommand(); groupCommand.CommandText = "select status from therapy_groups where id = @groupId;"; groupCommand.Parameters.AddWithValue("groupId", groupId);
        var status = await groupCommand.ExecuteScalarAsync(cancellationToken) as string;
        if (status is null) throw new ArgumentException("Therapy group was not found.");
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Sessions can only be scheduled for an active therapy group.");
        var id = Guid.NewGuid(); var createdAt = DateTimeOffset.UtcNow;
        await using var command = connection.CreateCommand();
        command.CommandText = "insert into therapy_group_sessions (id, group_id, starts_at, duration_minutes, topic, status, created_at) values (@id, @groupId, @startsAt, @duration, @topic, 'scheduled', @createdAt);";
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("groupId", groupId); command.Parameters.AddWithValue("startsAt", startsAt); command.Parameters.AddWithValue("duration", request.DurationMinutes); command.Parameters.AddWithValue("topic", (object?)topic ?? DBNull.Value); command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new(id, groupId, startsAt.ToString("O"), request.DurationMinutes, topic, "scheduled", createdAt.ToString("O"));
    }

    public async Task<TherapyGroupSessionItem> UpdateSessionStatusAsync(Guid groupId, Guid sessionId, TherapyGroupSessionStatusRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("completed" or "cancelled")) throw new ArgumentException("Session status must be completed or cancelled.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            update therapy_group_sessions set status = @status
            where id = @sessionId and group_id = @groupId and status = 'scheduled'
            returning id, group_id, starts_at, duration_minutes, topic, status, created_at;
            """;
        command.Parameters.AddWithValue("status", status); command.Parameters.AddWithValue("sessionId", sessionId); command.Parameters.AddWithValue("groupId", groupId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Scheduled therapy-group session was not found.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2).ToString("O"), reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O"));
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists therapy_groups (id uuid primary key, name text not null, status text not null, facilitator_id integer references staff(id), description text, capacity integer not null, created_at timestamptz not null);
            create table if not exists therapy_group_members (group_id uuid not null references therapy_groups(id), patient_id text not null references patients(canonical_id), joined_at timestamptz not null, primary key (group_id, patient_id));
            create table if not exists therapy_group_sessions (id uuid primary key, group_id uuid not null references therapy_groups(id), starts_at timestamptz not null, duration_minutes integer not null, topic text, status text not null, created_at timestamptz not null);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
