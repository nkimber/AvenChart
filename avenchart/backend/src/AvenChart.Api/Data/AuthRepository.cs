using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AuthRepository(NpgsqlDataSource dataSource)
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    public async Task<AuthLoginResponse> LoginAsync(
        AuthLoginRequest request,
        string? sourceIp,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            var failed = Failed(username);
            await RecordLoginAuditAsync(connection, username, success: false, sourceIp, failed.FailureReason, cancellationToken);
            return failed;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select username, display_name, role, staff_id, password_salt, password_hash
            from auth_accounts
            where lower(username) = lower(@username)
              and active = true
            limit 1;
            """;
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            var failed = Failed(username);
            await RecordLoginAuditAsync(connection, username, success: false, sourceIp, failed.FailureReason, cancellationToken);
            return failed;
        }

        var storedUsername = reader.GetString(reader.GetOrdinal("username"));
        var displayName = reader.GetString(reader.GetOrdinal("display_name"));
        var role = reader.GetString(reader.GetOrdinal("role"));
        int? staffId = reader.IsDBNull(reader.GetOrdinal("staff_id")) ? null : reader.GetInt32(reader.GetOrdinal("staff_id"));
        var storedHash = reader.GetString(reader.GetOrdinal("password_hash"));
        var computedHash = HashPassword(
            reader.GetString(reader.GetOrdinal("password_salt")),
            request.Password);
        await reader.DisposeAsync();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedHash),
                Encoding.UTF8.GetBytes(computedHash)))
        {
            var failed = Failed(username);
            await RecordLoginAuditAsync(connection, username, success: false, sourceIp, failed.FailureReason, cancellationToken);
            return failed;
        }

        var succeeded = new AuthLoginResponse(
            Authenticated: true,
            Username: storedUsername,
            DisplayName: displayName,
            Role: role,
            StaffId: staffId,
            FailureReason: null);
        await RecordLoginAuditAsync(connection, storedUsername, success: true, sourceIp, null, cancellationToken);
        return succeeded;
    }

    public async Task<AuthAuditResponse> GetLoginAuditAsync(int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            select
              count(*)::int as total_events,
              count(*) filter (where success)::int as successful_logins,
              count(*) filter (where not success)::int as failed_logins
            from auth_audit_events
            where event = 'login';
            """;

        var totalEvents = 0;
        var successfulLogins = 0;
        var failedLogins = 0;
        await using (var countReader = await countCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await countReader.ReadAsync(cancellationToken))
            {
                totalEvents = countReader.GetInt32(countReader.GetOrdinal("total_events"));
                successfulLogins = countReader.GetInt32(countReader.GetOrdinal("successful_logins"));
                failedLogins = countReader.GetInt32(countReader.GetOrdinal("failed_logins"));
            }
        }

        await using var eventsCommand = connection.CreateCommand();
        eventsCommand.CommandText = """
            select id, occurred_at, event, username, success, source_ip, comment, failure_reason, log_source
            from auth_audit_events
            where event = 'login'
            order by id desc
            limit @limit;
            """;
        eventsCommand.Parameters.AddWithValue("limit", safeLimit);

        var events = new List<AuthAuditEventItem>();
        await using var reader = await eventsCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new AuthAuditEventItem(
                Id: reader.GetInt64(reader.GetOrdinal("id")),
                OccurredAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at")),
                Event: reader.GetString(reader.GetOrdinal("event")),
                Username: reader.GetString(reader.GetOrdinal("username")),
                Success: reader.GetBoolean(reader.GetOrdinal("success")),
                SourceIp: reader.IsDBNull(reader.GetOrdinal("source_ip")) ? null : reader.GetString(reader.GetOrdinal("source_ip")),
                Comment: reader.GetString(reader.GetOrdinal("comment")),
                FailureReason: reader.IsDBNull(reader.GetOrdinal("failure_reason")) ? null : reader.GetString(reader.GetOrdinal("failure_reason")),
                LogSource: reader.GetString(reader.GetOrdinal("log_source"))));
        }

        return new AuthAuditResponse(totalEvents, successfulLogins, failedLogins, events);
    }

    private static AuthLoginResponse Failed(string username)
    {
        return new AuthLoginResponse(
            Authenticated: false,
            Username: username,
            DisplayName: string.Empty,
            Role: string.Empty,
            StaffId: null,
            FailureReason: InvalidCredentialsMessage);
    }

    private static async Task RecordLoginAuditAsync(
        NpgsqlConnection connection,
        string username,
        bool success,
        string? sourceIp,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var auditUsername = string.IsNullOrWhiteSpace(username) ? "(blank)" : username;
        var source = string.IsNullOrWhiteSpace(sourceIp) ? "unknown" : sourceIp;
        var comment = success ? $"success: {source}" : $"failure: {source}. user password incorrect";

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into auth_audit_events (occurred_at, event, username, success, source_ip, comment, failure_reason, log_source)
            values (now(), 'login', @username, @success, @source_ip, @comment, @failure_reason, 'avenchart');
            """;
        command.Parameters.AddWithValue("username", auditUsername);
        command.Parameters.AddWithValue("success", success);
        command.Parameters.AddWithValue("source_ip", source);
        command.Parameters.AddWithValue("comment", comment);
        command.Parameters.AddWithValue("failure_reason", (object?)failureReason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string HashPassword(string salt, string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
