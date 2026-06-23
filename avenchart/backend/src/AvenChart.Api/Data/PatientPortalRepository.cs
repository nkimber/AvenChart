using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
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

    public async Task<PatientPortalSessionResponse> EndSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patient_portal_sessions
            set ended_at = coalesce(ended_at, now()),
                last_seen_at = now()
            where id = @session_id
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
        return row.ToResponse(authenticated: false, "Session is not active.", pubpid, displayName);
    }

    public async Task<PatientPortalHomeSummaryResponse> GetHomeSummaryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyHomeSummary(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var messages = await GetMessageSummaryAsync(connection, session.LegacyPid.Value, cancellationToken);
        var appointments = await GetUpcomingAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            cancellationToken);

        return new PatientPortalHomeSummaryResponse(
            Authenticated: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Messages: messages,
            UpcomingAppointmentCount: appointments.TotalCount,
            UpcomingAppointments: appointments.Items,
            FailureReason: null,
            SessionSource: session.SessionSource);
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

    private static PatientPortalHomeSummaryResponse EmptyHomeSummary(
        PatientPortalSessionResponse session,
        string reason) => new(
        Authenticated: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Messages: new PatientPortalHomeMessageSummary(0, 0, 0, null, null),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalHomeSummaryResponse MissingSessionHomeSummary(string reason) => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        PortalUsername: string.Empty,
        CanonicalId: string.Empty,
        LegacyPid: null,
        Pubpid: string.Empty,
        DisplayName: string.Empty,
        DatasetId: "unseeded",
        DatasetVersion: "unknown",
        AsOfDate: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        Messages: new PatientPortalHomeMessageSummary(0, 0, 0, null, null),
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    public static PatientPortalHomeSummaryResponse MissingSessionHeaderHomeSummary() =>
        MissingSessionHomeSummary("Patient portal session header was not supplied.");

    private static async Task<DatasetMetadata> GetMetadataAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
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

    private static async Task<PatientPortalHomeMessageSummary> GetMessageSummaryAsync(
        NpgsqlConnection connection,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              count(*)::int as total_messages,
              count(*) filter (where status = 'New')::int as new_messages,
              count(*) filter (where status = 'Done')::int as done_messages,
              (
                select title
                from messages latest
                where latest.pid = @pid and latest.deleted = 0
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_title,
              (
                select message_date
                from messages latest
                where latest.pid = @pid and latest.deleted = 0
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_date
            from messages
            where pid = @pid and deleted = 0;
            """;
        command.Parameters.AddWithValue("pid", pid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PatientPortalHomeMessageSummary(0, 0, 0, null, null);
        }

        return new PatientPortalHomeMessageSummary(
            TotalMessages: reader.GetInt32(reader.GetOrdinal("total_messages")),
            NewMessages: reader.GetInt32(reader.GetOrdinal("new_messages")),
            DoneMessages: reader.GetInt32(reader.GetOrdinal("done_messages")),
            LatestMessageTitle: ReadNullableString(reader, "latest_message_title"),
            LatestMessageDate: ReadNullableDate(reader, "latest_message_date"));
    }

    private static async Task<AppointmentSummaryRows> GetUpcomingAppointmentsAsync(
        NpgsqlConnection connection,
        int pid,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              a.id,
              a.appointment_date,
              a.start_time,
              coalesce(a.title, 'Appointment') as title,
              a.status,
              a.category_id,
              trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
              f.name as facility_name,
              a.comments,
              count(*) over ()::int as total_count
            from appointments a
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where a.pid = @pid
              and a.appointment_date >= @as_of_date
            order by a.appointment_date, a.start_time, a.id
            limit 10;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.Add("as_of_date", NpgsqlDbType.Date).Value = asOfDate;

        var items = new List<PatientPortalHomeAppointmentSummary>();
        var totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));
            var categoryId = ReadNullableInt(reader, "category_id");
            items.Add(new PatientPortalHomeAppointmentSummary(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")).ToString("yyyy-MM-dd"),
                StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")).ToString("HH:mm"),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Status: ReadNullableString(reader, "status"),
                CategoryId: categoryId,
                CategoryName: GetAppointmentCategoryName(categoryId),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Comments: ReadNullableString(reader, "comments")));
        }

        return new AppointmentSummaryRows(totalCount, items);
    }

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

    private static int? ReadNullableInt(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableDate(NpgsqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private static string? GetAppointmentCategoryName(int? categoryId) => categoryId switch
    {
        9 => "Established Patient",
        10 => "New Patient",
        13 => "Preventive Care Services",
        null => null,
        _ => $"Category {categoryId.Value}"
    };

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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record AppointmentSummaryRows(
        int TotalCount,
        IReadOnlyList<PatientPortalHomeAppointmentSummary> Items);

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
