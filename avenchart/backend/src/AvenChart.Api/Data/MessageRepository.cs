using System.Data.Common;
using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class MessageRepository(NpgsqlDataSource dataSource)
{
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
                (id, patient_id, pid, message_date, title, body, status, assigned_to, portal_relation, is_encrypted, deleted, activity)
            values
                (@id, @patientId, @pid, @messageDate, @title, @body, 'New', @assignedTo, null, false, 0, 1);
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
                    body = @body
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
                    body = @body
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageId)
            || string.IsNullOrWhiteSpace(request.AssignedTo))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update messages
                set assigned_to = @assignedTo
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", messageId);
            command.Parameters.AddWithValue("assignedTo", request.AssignedTo.Trim());
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
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
                    assigned_to = @assignedTo
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
                    activity = 0
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
            select id, message_date, title, body, status, assigned_to, portal_relation, is_encrypted, deleted
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
                Deleted: reader.GetInt32(reader.GetOrdinal("deleted"))));
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

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record MessagePatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName,
        bool PortalEnabled);
}
