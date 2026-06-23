using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientPortalRepository(NpgsqlDataSource dataSource)
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";
    private const string SessionSource = "avenchart-portal";

    public async Task<PatientPortalLoginResponse> LoginAsync(
        PatientPortalLoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              p.canonical_id,
              p.legacy_pid,
              p.pubpid,
              p.first_name,
              p.last_name,
              p.portal_enabled,
              ppa.portal_username,
              ppa.portal_login_username,
              ppa.password_salt,
              ppa.password_hash,
              ppa.password_status,
              ppa.one_time_token
            from patient_portal_accounts ppa
            join patients p on p.canonical_id = ppa.patient_id
            where lower(ppa.portal_login_username) = lower(@username)
            limit 1;
            """;
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        var account = ReadPortalAccount(reader);
        await reader.DisposeAsync();

        if (string.IsNullOrWhiteSpace(account.PortalUsername)
            || string.IsNullOrWhiteSpace(account.PortalLoginUsername)
            || string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        if (!string.IsNullOrWhiteSpace(account.OneTimeToken))
        {
            return Failed(username, "One-time reset pending.");
        }

        if (account.PasswordStatus != 1)
        {
            return Failed(username, "Patient portal account is pending password setup.");
        }

        if (!account.PortalEnabled)
        {
            return Failed(username, "Patient portal access is disabled.");
        }

        var computedHash = HashPassword(account.PasswordSalt, request.Password);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(account.PasswordHash),
                Encoding.UTF8.GetBytes(computedHash)))
        {
            return Failed(username, InvalidCredentialsMessage);
        }

        var session = await CreateSessionAsync(connection, account, cancellationToken);
        return new PatientPortalLoginResponse(
            Authenticated: true,
            Username: account.PortalLoginUsername,
            PortalUsername: account.PortalUsername,
            CanonicalId: account.CanonicalId,
            LegacyPid: account.LegacyPid,
            Pubpid: account.Pubpid,
            DisplayName: account.DisplayName,
            FailureReason: null,
            SessionId: session.SessionId,
            SessionCreatedAt: session.CreatedAt,
            SessionExpiresAt: session.ExpiresAt,
            SessionSource: SessionSource);
    }

    public async Task<PatientPortalSessionResponse> GetCurrentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_portal_sessions
            set last_seen_at = now()
            where id = @session_id
              and ended_at is null
              and expires_at > now()
            returning id, patient_id, pid, portal_username, portal_login_username, created_at, last_seen_at, expires_at, ended_at, session_source;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return InactiveSession(sessionId, "Session is not active.");
        }

        var row = ReadSessionRow(reader);
        await reader.DisposeAsync();

        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = """
            select pubpid, first_name, last_name
            from patients
            where canonical_id = @patient_id
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patient_id", row.CanonicalId);

        await using var patientReader = await patientCommand.ExecuteReaderAsync(cancellationToken);
        if (!await patientReader.ReadAsync(cancellationToken))
        {
            return row.ToResponse(authenticated: false, "Session patient was not found.", string.Empty, string.Empty);
        }

        var pubpid = patientReader.GetString(patientReader.GetOrdinal("pubpid"));
        var displayName = $"{patientReader.GetString(patientReader.GetOrdinal("last_name"))}, {patientReader.GetString(patientReader.GetOrdinal("first_name"))}";
        return row.ToResponse(authenticated: true, null, pubpid, displayName);
    }

    private static PatientPortalLoginResponse Failed(string username, string reason) => new(
        Authenticated: false,
        Username: username,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        FailureReason: reason,
        SessionId: null,
        SessionCreatedAt: null,
        SessionExpiresAt: null,
        SessionSource: SessionSource);

    private static async Task<PatientPortalSessionRow> CreateSessionAsync(
        NpgsqlConnection connection,
        PatientPortalAccountRow account,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patient_portal_sessions
              (id, patient_id, pid, portal_username, portal_login_username, created_at, last_seen_at, expires_at, session_source)
            values
              (@id, @patient_id, @pid, @portal_username, @portal_login_username, now(), now(), now() + interval '8 hours', @session_source)
            returning id, created_at, expires_at;
            """;
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("patient_id", account.CanonicalId);
        command.Parameters.AddWithValue("pid", account.LegacyPid);
        command.Parameters.AddWithValue("portal_username", account.PortalUsername);
        command.Parameters.AddWithValue("portal_login_username", account.PortalLoginUsername);
        command.Parameters.AddWithValue("session_source", SessionSource);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Patient portal session could not be created.");
        }

        return new PatientPortalSessionRow(
            SessionId: reader.GetGuid(reader.GetOrdinal("id")),
            CreatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            ExpiresAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")));
    }

    private static PatientPortalAccountRow ReadPortalAccount(NpgsqlDataReader reader)
    {
        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        return new PatientPortalAccountRow(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            DisplayName: $"{lastName}, {firstName}",
            PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")),
            PortalUsername: ReadNullableString(reader, "portal_username") ?? string.Empty,
            PortalLoginUsername: ReadNullableString(reader, "portal_login_username") ?? string.Empty,
            PasswordSalt: reader.GetString(reader.GetOrdinal("password_salt")),
            PasswordHash: reader.GetString(reader.GetOrdinal("password_hash")),
            PasswordStatus: reader.GetInt32(reader.GetOrdinal("password_status")),
            OneTimeToken: ReadNullableString(reader, "one_time_token"));
    }

    private static PatientPortalSessionReadRow ReadSessionRow(NpgsqlDataReader reader) => new(
        SessionId: reader.GetGuid(reader.GetOrdinal("id")),
        Username: reader.GetString(reader.GetOrdinal("portal_login_username")),
        PortalUsername: reader.GetString(reader.GetOrdinal("portal_username")),
        CanonicalId: reader.GetString(reader.GetOrdinal("patient_id")),
        LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
        CreatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
        LastSeenAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_seen_at")),
        ExpiresAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
        EndedAt: reader.IsDBNull(reader.GetOrdinal("ended_at"))
            ? null
            : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ended_at")),
        SessionSource: reader.GetString(reader.GetOrdinal("session_source")));

    private static PatientPortalSessionResponse InactiveSession(Guid sessionId, string reason) => new(
        Authenticated: false,
        SessionId: sessionId,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        CreatedAt: null,
        LastSeenAt: null,
        ExpiresAt: null,
        EndedAt: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static string? ReadNullableString(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string HashPassword(string salt, string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record PatientPortalAccountRow(
        string CanonicalId,
        int LegacyPid,
        string Pubpid,
        string DisplayName,
        bool PortalEnabled,
        string PortalUsername,
        string PortalLoginUsername,
        string PasswordSalt,
        string PasswordHash,
        int PasswordStatus,
        string? OneTimeToken);

    private sealed record PatientPortalSessionRow(
        Guid SessionId,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);

    private sealed record PatientPortalSessionReadRow(
        Guid SessionId,
        string Username,
        string PortalUsername,
        string CanonicalId,
        int LegacyPid,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSeenAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? EndedAt,
        string SessionSource)
    {
        public PatientPortalSessionResponse ToResponse(
            bool authenticated,
            string? failureReason,
            string pubpid,
            string displayName) => new(
            Authenticated: authenticated,
            SessionId: SessionId,
            Username: Username,
            PortalUsername: PortalUsername,
            CanonicalId: CanonicalId,
            LegacyPid: LegacyPid,
            Pubpid: pubpid,
            DisplayName: displayName,
            CreatedAt: CreatedAt,
            LastSeenAt: LastSeenAt,
            ExpiresAt: ExpiresAt,
            EndedAt: EndedAt,
            FailureReason: failureReason,
            SessionSource: SessionSource);
    }
}
