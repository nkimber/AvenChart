using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class MessageRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumStaffMessageAttachmentBytes = 4 * 1024 * 1024;
    public async Task<StaffMessageInboxResponse> GetInboxAsync(
        string currentUsername,
        StaffMessageInboxQuery query,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var offset = Math.Max(0, query.Offset);
        var limit = Math.Clamp(query.Limit, 1, 100);
        var conditions = new List<string> { "m.deleted = 0", "m.activity = 1" };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("lower(coalesce(m.status, '')) = lower(@status)");
            command.Parameters.AddWithValue("status", query.Status.Trim());
        }

        switch (query.Assignment?.Trim().ToLowerInvariant())
        {
            case "mine":
                conditions.Add("lower(coalesce(m.assigned_to, '')) = lower(@currentUsername)");
                break;
            case "unassigned":
                conditions.Add("nullif(trim(coalesce(m.assigned_to, '')), '') is null");
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.Owner))
        {
            conditions.Add("lower(coalesce(m.assigned_to, '')) = lower(@owner)");
            command.Parameters.AddWithValue("owner", query.Owner.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Patient))
        {
            conditions.Add("""
                (
                    lower(p.canonical_id) like lower(@patient)
                    or lower(p.pubpid) like lower(@patient)
                    or lower(concat_ws(' ', p.first_name, p.last_name, p.preferred_name)) like lower(@patient)
                )
                """);
            command.Parameters.AddWithValue("patient", $"%{query.Patient.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            conditions.Add("lower(coalesce(m.title, '')) like lower(@subject)");
            command.Parameters.AddWithValue("subject", $"%{query.Subject.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            conditions.Add("""
                case
                    when lower(coalesce(m.title, '')) like '%urgent%'
                      or lower(coalesce(m.title, '')) like '%critical%'
                    then 'urgent'
                    else 'normal'
                end = lower(@priority)
                """);
            command.Parameters.AddWithValue("priority", query.Priority.Trim());
        }

        if (query.MinimumAgeDays is >= 0)
        {
            conditions.Add("current_date - coalesce(m.message_date, current_date) >= @minimumAgeDays");
            command.Parameters.AddWithValue("minimumAgeDays", query.MinimumAgeDays.Value);
        }

        if (query.MaximumAgeDays is >= 0)
        {
            conditions.Add("current_date - coalesce(m.message_date, current_date) <= @maximumAgeDays");
            command.Parameters.AddWithValue("maximumAgeDays", query.MaximumAgeDays.Value);
        }

        command.CommandText = $"""
            select
                count(*) over()::int as total_count,
                m.id,
                p.canonical_id,
                p.pubpid,
                case
                    when nullif(trim(coalesce(p.preferred_name, '')), '') is null
                    then concat(p.last_name, ', ', p.first_name)
                    else concat(p.last_name, ', ', p.first_name, ' (', p.preferred_name, ')')
                end as patient_display_name,
                m.message_date,
                coalesce(nullif(trim(m.title), ''), '(no subject)') as subject,
                left(regexp_replace(coalesce(m.body, ''), E'\\s+', ' ', 'g'), 160) as preview,
                coalesce(nullif(trim(m.status), ''), 'Unknown') as status,
                nullif(trim(coalesce(m.assigned_to, '')), '') as assigned_to,
                case
                    when lower(coalesce(m.title, '')) like '%urgent%'
                      or lower(coalesce(m.title, '')) like '%critical%'
                    then 'urgent'
                    else 'normal'
                end as priority,
                current_date - coalesce(m.message_date, current_date) as age_days,
                lower(coalesce(m.status, '')) = 'new' as unread,
                m.portal_relation,
                coalesce(m.updated_at, m.message_date::timestamp) as updated_at
            from messages m
            join patients p on p.legacy_pid = m.pid
            where {string.Join(" and ", conditions)}
            order by unread desc, coalesce(m.updated_at, m.message_date::timestamp) desc, m.id desc
            offset @offset
            limit @limit;
            """;
        command.Parameters.AddWithValue("currentUsername", currentUsername);
        command.Parameters.AddWithValue("offset", offset);
        command.Parameters.AddWithValue("limit", limit);

        var items = new List<StaffMessageInboxItem>();
        var total = 0;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                total = reader.GetInt32(reader.GetOrdinal("total_count"));
                items.Add(new StaffMessageInboxItem(
                    Id: reader.GetString(reader.GetOrdinal("id")),
                    PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                    Date: ReadNullableDate(reader, "message_date"),
                    Subject: reader.GetString(reader.GetOrdinal("subject")),
                    Preview: reader.GetString(reader.GetOrdinal("preview")),
                    Status: reader.GetString(reader.GetOrdinal("status")),
                    AssignedTo: ReadNullableString(reader, "assigned_to"),
                    Priority: reader.GetString(reader.GetOrdinal("priority")),
                    AgeDays: reader.GetInt32(reader.GetOrdinal("age_days")),
                    Unread: reader.GetBoolean(reader.GetOrdinal("unread")),
                    PortalRelation: ReadNullableString(reader, "portal_relation"),
                    UpdatedAt: ReadNullableTimestamp(reader, "updated_at")));
            }
        }

        await using var countsCommand = connection.CreateCommand();
        countsCommand.CommandText = """
            select
                count(*)::int as total,
                count(*) filter (where lower(coalesce(status, '')) = 'new')::int as unread,
                count(*) filter (where lower(coalesce(assigned_to, '')) = lower(@currentUsername))::int as assigned_to_me,
                count(*) filter (where nullif(trim(coalesce(assigned_to, '')), '') is null)::int as unassigned
            from messages
            where deleted = 0 and activity = 1;
            """;
        countsCommand.Parameters.AddWithValue("currentUsername", currentUsername);
        await using var countsReader = await countsCommand.ExecuteReaderAsync(cancellationToken);
        await countsReader.ReadAsync(cancellationToken);
        var counts = new StaffMessageInboxCounts(
            Total: countsReader.GetInt32(countsReader.GetOrdinal("total")),
            Unread: countsReader.GetInt32(countsReader.GetOrdinal("unread")),
            AssignedToMe: countsReader.GetInt32(countsReader.GetOrdinal("assigned_to_me")),
            Unassigned: countsReader.GetInt32(countsReader.GetOrdinal("unassigned")));

        return new StaffMessageInboxResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Total: total,
            Offset: offset,
            Limit: limit,
            Counts: counts,
            Items: items);
    }

    public async Task<PatientMessagesResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var messages = await GetMessagesAsync(connection, patient.LegacyPid, cancellationToken);

        return new PatientMessagesResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            PortalEnabled: patient.PortalEnabled,
            Messages: messages);
    }

    public async Task<PatientMessageMutationResponse?> CreateAsync(
        PatientMessageCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var id = $"MSG-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into messages
                (id, patient_id, pid, message_date, title, body, status, assigned_to, portal_relation, is_encrypted, updated_by, updated_at, deleted, activity)
            values
                (@id, @patientId, @pid, @messageDate, @title, @body, 'New', @assignedTo, null, false, null, null, 0, 1);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("messageDate", DateOnly.FromDateTime(DateTime.UtcNow));
        command.Parameters.AddWithValue("title", request.Title.Trim());
        command.Parameters.AddWithValue("body", request.Body.Trim());
        command.Parameters.AddWithValue("assignedTo", NullableText(request.AssignedTo));
        await command.ExecuteNonQueryAsync(cancellationToken);

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(id, detail);
    }

    public async Task<PatientMessageMutationResponse?> UpdateStatusAsync(
        string messageId,
        PatientMessageStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId)
            || string.IsNullOrWhiteSpace(request.Status)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update messages
                set status = @status,
                    body = @body,
                    updated_by = 1,
                    updated_at = now()
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", messageId);
            command.Parameters.AddWithValue("status", request.Status.Trim());
            command.Parameters.AddWithValue("body", request.Body.Trim());
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<PatientMessageMutationResponse?> UpdateContentAsync(
        string messageId,
        PatientMessageContentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId)
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update messages
                set title = @title,
                    body = @body,
                    updated_by = 1,
                    updated_at = now()
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", messageId);
            command.Parameters.AddWithValue("title", request.Title.Trim());
            command.Parameters.AddWithValue("body", request.Body.Trim());
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<PatientMessageMutationResponse?> UpdateAssignmentAsync(
        string messageId,
        PatientMessageAssignmentUpdateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException("The expected assignment version must be zero or greater.");
        }

        var assignedTo = NormalizeOptionalText(request.AssignedTo);
        var reason = NormalizeOptionalText(request.Reason);
        if (reason is { Length: > 500 })
        {
            throw new ArgumentException("The assignment reason must be 500 characters or fewer.");
        }

        string? patientId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            MessageAssignmentState? current;
            await using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    select patient_id, nullif(trim(coalesce(assigned_to, '')), ''), assignment_version
                    from messages
                    where id = @id and deleted = 0
                    for update;
                    """;
                read.Parameters.AddWithValue("id", messageId);
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                current = await reader.ReadAsync(cancellationToken)
                    ? new MessageAssignmentState(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.GetInt32(2))
                    : null;
            }

            if (current is null)
            {
                return null;
            }

            if (current.Version != request.ExpectedVersion)
            {
                throw new PatientMessageAssignmentVersionConflictException(
                    request.ExpectedVersion,
                    current.Version);
            }

            if (string.Equals(current.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The selected assignment is already current.");
            }

            if (current.AssignedTo is not null && string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A reason is required when reassigning or unassigning a message.");
            }

            if (assignedTo is not null)
            {
                await EnsureActiveAssigneeAsync(connection, transaction, assignedTo, cancellationToken);
            }

            var actorStaffId = await GetActiveStaffIdAsync(connection, transaction, actor, cancellationToken);
            var action = current.AssignedTo is null
                ? "assigned"
                : assignedTo is null
                    ? "unassigned"
                    : "reassigned";
            var nextVersion = current.Version + 1;

            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    update messages
                    set assigned_to = @assignedTo,
                        assignment_version = @assignmentVersion,
                        updated_by = @updatedBy,
                        updated_at = now()
                    where id = @id and deleted = 0;
                    """;
                update.Parameters.AddWithValue("id", messageId);
                update.Parameters.AddWithValue("assignedTo", (object?)assignedTo ?? DBNull.Value);
                update.Parameters.AddWithValue("assignmentVersion", nextVersion);
                update.Parameters.AddWithValue("updatedBy", (object?)actorStaffId ?? DBNull.Value);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var writeEvent = connection.CreateCommand())
            {
                writeEvent.Transaction = transaction;
                writeEvent.CommandText = """
                    insert into message_assignment_events
                        (message_id, patient_id, action, previous_assigned_to, assigned_to, reason, actor, assignment_version, occurred_at)
                    values
                        (@messageId, @patientId, @action, @previousAssignedTo, @assignedTo, @reason, @actor, @assignmentVersion, now());
                    """;
                writeEvent.Parameters.AddWithValue("messageId", messageId);
                writeEvent.Parameters.AddWithValue("patientId", current.PatientId);
                writeEvent.Parameters.AddWithValue("action", action);
                writeEvent.Parameters.AddWithValue("previousAssignedTo", (object?)current.AssignedTo ?? DBNull.Value);
                writeEvent.Parameters.AddWithValue("assignedTo", (object?)assignedTo ?? DBNull.Value);
                writeEvent.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
                writeEvent.Parameters.AddWithValue("actor", actor);
                writeEvent.Parameters.AddWithValue("assignmentVersion", nextVersion);
                await writeEvent.ExecuteNonQueryAsync(cancellationToken);
            }

            patientId = current.PatientId;
            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<PatientMessageAssignmentHistoryResponse?> GetAssignmentHistoryAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        int? currentVersion;
        await using (var message = connection.CreateCommand())
        {
            message.CommandText = """
                select assignment_version
                from messages
                where id = @id and deleted = 0;
                """;
            message.Parameters.AddWithValue("id", messageId);
            currentVersion = (int?)await message.ExecuteScalarAsync(cancellationToken);
        }

        if (currentVersion is null)
        {
            return null;
        }

        await using var history = connection.CreateCommand();
        history.CommandText = """
            select event_id, action, previous_assigned_to, assigned_to, reason, actor, occurred_at, assignment_version
            from message_assignment_events
            where message_id = @messageId
            order by occurred_at desc, event_id desc;
            """;
        history.Parameters.AddWithValue("messageId", messageId);
        var events = new List<PatientMessageAssignmentEvent>();
        await using var reader = await history.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PatientMessageAssignmentEvent(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6).ToString("O"),
                reader.GetInt32(7)));
        }

        return new PatientMessageAssignmentHistoryResponse(messageId, currentVersion.Value, events);
    }

    public async Task<PatientMessageMutationResponse?> ForwardAsync(
        string messageId,
        PatientMessageForwardRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(request.AssignedTo))
        {
            return null;
        }

        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException("The expected assignment version must be zero or greater.");
        }

        var assignedTo = request.AssignedTo.Trim();
        var note = NormalizeOptionalText(request.Note);
        if (note is { Length: > 500 })
        {
            throw new ArgumentException("The forwarding note must be 500 characters or fewer.");
        }

        string? patientId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            MessageAssignmentState? current;
            await using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    select patient_id, nullif(trim(coalesce(assigned_to, '')), ''), assignment_version
                    from messages
                    where id = @id and deleted = 0
                    for update;
                    """;
                read.Parameters.AddWithValue("id", messageId);
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                current = await reader.ReadAsync(cancellationToken)
                    ? new MessageAssignmentState(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.GetInt32(2))
                    : null;
            }

            if (current is null)
            {
                return null;
            }

            if (current.Version != request.ExpectedVersion)
            {
                throw new PatientMessageAssignmentVersionConflictException(request.ExpectedVersion, current.Version);
            }

            if (string.Equals(current.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Choose a different active staff recipient to forward this message.");
            }

            await EnsureActiveAssigneeAsync(connection, transaction, assignedTo, cancellationToken);
            var actorStaffId = await GetActiveStaffIdAsync(connection, transaction, actor, cancellationToken);
            var nextVersion = current.Version + 1;
            var forwardLine = $"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} ({actor} to {assignedTo}) {note ?? "Forwarded"}";

            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    update messages
                    set body = concat(coalesce(body, ''), case when coalesce(body, '') = '' then '' else E'\n' end, @forwardLine),
                        assigned_to = @assignedTo,
                        assignment_version = @assignmentVersion,
                        updated_by = @updatedBy,
                        updated_at = now()
                    where id = @id and deleted = 0;
                    """;
                update.Parameters.AddWithValue("id", messageId);
                update.Parameters.AddWithValue("forwardLine", forwardLine);
                update.Parameters.AddWithValue("assignedTo", assignedTo);
                update.Parameters.AddWithValue("assignmentVersion", nextVersion);
                update.Parameters.AddWithValue("updatedBy", (object?)actorStaffId ?? DBNull.Value);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var writeEvent = connection.CreateCommand())
            {
                writeEvent.Transaction = transaction;
                writeEvent.CommandText = """
                    insert into message_assignment_events
                        (message_id, patient_id, action, previous_assigned_to, assigned_to, reason, actor, assignment_version, occurred_at)
                    values
                        (@messageId, @patientId, 'forwarded', @previousAssignedTo, @assignedTo, @reason, @actor, @assignmentVersion, now());
                    """;
                writeEvent.Parameters.AddWithValue("messageId", messageId);
                writeEvent.Parameters.AddWithValue("patientId", current.PatientId);
                writeEvent.Parameters.AddWithValue("previousAssignedTo", (object?)current.AssignedTo ?? DBNull.Value);
                writeEvent.Parameters.AddWithValue("assignedTo", assignedTo);
                writeEvent.Parameters.AddWithValue("reason", (object?)note ?? DBNull.Value);
                writeEvent.Parameters.AddWithValue("actor", actor);
                writeEvent.Parameters.AddWithValue("assignmentVersion", nextVersion);
                await writeEvent.ExecuteNonQueryAsync(cancellationToken);
            }

            patientId = current.PatientId;
            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<PatientMessageMutationResponse?> ReplyAsync(
        string messageId,
        PatientMessageReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId)
            || string.IsNullOrWhiteSpace(request.Body)
            || string.IsNullOrWhiteSpace(request.AssignedTo))
        {
            return null;
        }

        var assignedTo = request.AssignedTo.Trim();
        var replyLine = $"{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (admin to {assignedTo}) {request.Body.Trim()}";
        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update messages
                set body = concat(coalesce(body, ''), E'\n', @replyLine),
                    assigned_to = @assignedTo,
                    updated_by = 1,
                    updated_at = now()
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", messageId);
            command.Parameters.AddWithValue("replyLine", replyLine);
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<StaffMessageAttachmentItem?> AddAttachmentAsync(string messageId, StaffMessageAttachmentSubmission submission, string actor, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(NormalizeOptionalText(submission.FileName) ?? string.Empty);
        var contentType = NormalizeOptionalText(submission.ContentType)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(fileName) || fileName.Length > 180
            || contentType is not ("application/pdf" or "image/png" or "image/jpeg" or "text/plain")
            || string.IsNullOrWhiteSpace(submission.ContentBase64))
        {
            throw new ArgumentException("Attachments require a safe PDF, PNG, JPEG, or plain-text file name, content type, and content.");
        }
        byte[] content;
        try { content = Convert.FromBase64String(submission.ContentBase64); }
        catch (FormatException) { throw new ArgumentException("Attachment content is not valid base64 data."); }
        if (content.Length is 0 or > MaximumStaffMessageAttachmentBytes)
            throw new ArgumentException("Each attachment must be between 1 byte and 4 MiB.");

        var id = Guid.NewGuid();
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into staff_message_attachments (id, message_id, patient_id, file_name, content_type, size_bytes, sha256, content, uploaded_by)
            select @id, m.id, m.patient_id, @fileName, @contentType, @sizeBytes, @sha256, @content, @actor
            from messages m where m.id = @messageId and m.deleted = 0
            returning uploaded_at;
            """;
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("messageId", messageId);
        command.Parameters.AddWithValue("fileName", fileName); command.Parameters.AddWithValue("contentType", contentType);
        command.Parameters.AddWithValue("sizeBytes", content.Length); command.Parameters.AddWithValue("sha256", sha256);
        command.Parameters.AddWithValue("content", content); command.Parameters.AddWithValue("actor", actor);
        var uploadedAt = await command.ExecuteScalarAsync(cancellationToken);
        return uploadedAt is DateTime timestamp
            ? new StaffMessageAttachmentItem(id.ToString(), fileName, contentType, content.Length, sha256, actor, timestamp.ToString("O"))
            : null;
    }

    public async Task<IReadOnlyList<StaffMessageAttachmentItem>?> GetAttachmentsAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """select id, file_name, content_type, size_bytes, sha256, uploaded_by, uploaded_at from staff_message_attachments where message_id=@messageId order by uploaded_at, id;""";
        command.Parameters.AddWithValue("messageId", messageId);
        var result = new List<StaffMessageAttachmentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0).ToString(), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O")));
        return result;
    }

    public async Task<StaffMessageAttachmentDownload> DownloadAttachmentAsync(string messageId, Guid attachmentId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """select file_name, content_type, content from staff_message_attachments where id=@id and message_id=@messageId;""";
        command.Parameters.AddWithValue("id", attachmentId); command.Parameters.AddWithValue("messageId", messageId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(true, reader.GetString(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2), null) : new(false, "", "application/octet-stream", [], "Attachment was not found for this message.");
    }

    public async Task<PatientMessageMutationResponse?> SoftDeleteAsync(string messageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update messages
                set deleted = 1,
                    activity = 0,
                    updated_by = 1,
                    updated_at = now()
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", messageId);
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientMessageMutationResponse(messageId, detail);
    }

    public async Task<bool> DeleteAsync(string messageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from messages
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", messageId);
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

    private static async Task<MessagePatient?> GetPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name, portal_enabled
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");

        return new MessagePatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})",
            PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")));
    }

    private static async Task<IReadOnlyList<PatientMessageItem>> GetMessagesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, message_date, title, body, status, assigned_to, portal_relation, is_encrypted,
                updated_by, updated_at, deleted, assignment_version
            from messages
            where pid = @pid and deleted = 0
            order by message_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PatientMessageItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientMessageItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Date: ReadNullableDate(reader, "message_date"),
                Title: ReadNullableString(reader, "title"),
                Body: ReadNullableString(reader, "body"),
                Status: ReadNullableString(reader, "status"),
                AssignedTo: ReadNullableString(reader, "assigned_to"),
                PortalRelation: ReadNullableString(reader, "portal_relation"),
                IsEncrypted: reader.GetBoolean(reader.GetOrdinal("is_encrypted")),
                UpdatedBy: ReadNullableInt32(reader, "updated_by"),
                UpdatedAt: ReadNullableTimestamp(reader, "updated_at"),
                Deleted: reader.GetInt32(reader.GetOrdinal("deleted")),
                AssignmentVersion: reader.GetInt32(reader.GetOrdinal("assignment_version"))));
        }

        return items;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private static int? ReadNullableInt32(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string? ReadNullableTimestamp(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTime>(ordinal).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private static async Task EnsureActiveAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from auth_accounts
                where active = true and lower(username) = lower(@username));
            """;
        command.Parameters.AddWithValue("username", username);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException("The assignment target must be an active staff user.");
        }
    }

    private static async Task<int?> GetActiveStaffIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select staff_id
            from auth_accounts
            where active = true and lower(username) = lower(@username)
            limit 1;
            """;
        command.Parameters.AddWithValue("username", username);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private sealed record MessageAssignmentState(string PatientId, string? AssignedTo, int Version);

    private sealed record MessagePatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName,
        bool PortalEnabled);
}

public sealed class PatientMessageAssignmentVersionConflictException(
    int expectedVersion,
    int currentVersion)
    : Exception(
        $"The message assignment changed after it was loaded. Expected version {expectedVersion}; current version is {currentVersion}.")
{
    public int ExpectedVersion { get; } = expectedVersion;

    public int CurrentVersion { get; } = currentVersion;
}
