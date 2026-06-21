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
        string? userAgent,
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
            FailureReason: null,
            SessionId: null,
            SessionCreatedAt: null,
            SessionExpiresAt: null);
        var session = await CreateSessionAsync(
            connection,
            storedUsername,
            displayName,
            role,
            staffId,
            sourceIp,
            userAgent,
            cancellationToken);
        await RecordLoginAuditAsync(connection, storedUsername, success: true, sourceIp, null, cancellationToken);
        return succeeded with
        {
            SessionId = session.SessionId,
            SessionCreatedAt = session.CreatedAt,
            SessionExpiresAt = session.ExpiresAt
        };
    }

    public async Task<AuthSessionResponse> GetCurrentSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var session = await GetSessionAsync(connection, sessionId, touchActiveSession: true, cancellationToken);
        return session?.ToResponse() ?? InactiveSession(sessionId, "Session was not found.");
    }

    public async Task<AuthSessionResponse> LogoutAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update auth_sessions
            set ended_at = coalesce(ended_at, now()),
                last_seen_at = now()
            where id = @session_id
            returning id, username, display_name, role, staff_id, created_at, last_seen_at, expires_at, ended_at, session_source;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return InactiveSession(sessionId, "Session was not found.");
        }

        var session = ReadSession(reader, forceInactive: true);
        return session.ToResponse();
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
            FailureReason: InvalidCredentialsMessage,
            SessionId: null,
            SessionCreatedAt: null,
            SessionExpiresAt: null);
    }

    private static async Task<AuthSessionRow> CreateSessionAsync(
        NpgsqlConnection connection,
        string username,
        string displayName,
        string role,
        int? staffId,
        string? sourceIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var source = string.IsNullOrWhiteSpace(sourceIp) ? "unknown" : sourceIp;
        var agent = string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into auth_sessions
              (id, username, display_name, role, staff_id, created_at, last_seen_at, expires_at, source_ip, user_agent, session_source)
            values
              (@id, @username, @display_name, @role, @staff_id, now(), now(), now() + interval '8 hours', @source_ip, @user_agent, 'avenchart')
            returning id, username, display_name, role, staff_id, created_at, last_seen_at, expires_at, ended_at, session_source;
            """;
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("staff_id", (object?)staffId ?? DBNull.Value);
        command.Parameters.AddWithValue("source_ip", source);
        command.Parameters.AddWithValue("user_agent", agent);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Authentication session could not be created.");
        }

        return ReadSession(reader);
    }

    private static async Task<AuthSessionRow?> GetSessionAsync(
        NpgsqlConnection connection,
        Guid sessionId,
        bool touchActiveSession,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, username, display_name, role, staff_id, created_at, last_seen_at, expires_at, ended_at, session_source
            from auth_sessions
            where id = @session_id
            limit 1;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);

        AuthSessionRow? session = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                session = ReadSession(reader);
            }
        }

        if (session is null || !touchActiveSession || !session.Authenticated)
        {
            return session;
        }

        await using var touchCommand = connection.CreateCommand();
        touchCommand.CommandText = """
            update auth_sessions
            set last_seen_at = now()
            where id = @session_id
              and ended_at is null
              and expires_at > now()
            returning id, username, display_name, role, staff_id, created_at, last_seen_at, expires_at, ended_at, session_source;
            """;
        touchCommand.Parameters.AddWithValue("session_id", sessionId);

        await using var touchReader = await touchCommand.ExecuteReaderAsync(cancellationToken);
        return await touchReader.ReadAsync(cancellationToken)
            ? ReadSession(touchReader)
            : session with { Authenticated = false, FailureReason = "Session is not active." };
    }

    private static AuthSessionRow ReadSession(NpgsqlDataReader reader, bool forceInactive = false)
    {
        var endedAt = reader.IsDBNull(reader.GetOrdinal("ended_at"))
            ? (DateTimeOffset?)null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ended_at"));
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at"));
        var authenticated = !forceInactive && endedAt is null && expiresAt > DateTimeOffset.UtcNow;

        return new AuthSessionRow(
            SessionId: reader.GetGuid(reader.GetOrdinal("id")),
            Username: reader.GetString(reader.GetOrdinal("username")),
            DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
            Role: reader.GetString(reader.GetOrdinal("role")),
            StaffId: reader.IsDBNull(reader.GetOrdinal("staff_id")) ? null : reader.GetInt32(reader.GetOrdinal("staff_id")),
            CreatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            LastSeenAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_seen_at")),
            ExpiresAt: expiresAt,
            EndedAt: endedAt,
            SessionSource: reader.GetString(reader.GetOrdinal("session_source")),
            Authenticated: authenticated,
            FailureReason: authenticated ? null : "Session is not active.");
    }

    private static AuthSessionResponse InactiveSession(Guid sessionId, string failureReason)
    {
        return new AuthSessionResponse(
            Authenticated: false,
            SessionId: sessionId,
            Username: string.Empty,
            DisplayName: string.Empty,
            Role: string.Empty,
            StaffId: null,
            CreatedAt: null,
            LastSeenAt: null,
            ExpiresAt: null,
            EndedAt: null,
            FailureReason: failureReason,
            SessionSource: "avenchart");
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

    private sealed record AuthSessionRow(
        Guid SessionId,
        string Username,
        string DisplayName,
        string Role,
        int? StaffId,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSeenAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? EndedAt,
        string SessionSource,
        bool Authenticated,
        string? FailureReason)
    {
        public AuthSessionResponse ToResponse() => new(
            Authenticated,
            SessionId,
            Username,
            DisplayName,
            Role,
            StaffId,
            CreatedAt,
            LastSeenAt,
            ExpiresAt,
            EndedAt,
            FailureReason,
            SessionSource);
    }
}
