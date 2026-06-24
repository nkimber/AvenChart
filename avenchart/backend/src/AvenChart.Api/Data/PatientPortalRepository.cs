using System.IO.Compression;
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
        var messages = await GetMessageSummaryAsync(connection, session.PortalUsername, cancellationToken);
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

    public async Task<PatientPortalAppointmentsResponse> GetAppointmentsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyAppointments(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var upcomingAppointments = await GetPortalAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            PortalAppointmentWindow.Upcoming,
            cancellationToken);
        var pastAppointments = await GetPortalAppointmentsAsync(
            connection,
            session.LegacyPid.Value,
            metadata.BaseDate,
            PortalAppointmentWindow.Past,
            cancellationToken);

        return new PatientPortalAppointmentsResponse(
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
            UpcomingAppointmentCount: upcomingAppointments.TotalCount,
            UpcomingAppointments: upcomingAppointments.Items,
            PastAppointmentCount: pastAppointments.TotalCount,
            PastAppointments: pastAppointments.Items,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalAppointmentRequestResponse> RequestAppointmentAsync(
        Guid sessionId,
        PatientPortalAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return AppointmentRequestFailure(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);

        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var appointmentStart)
            || request.DurationMinutes is null
            || request.DurationMinutes <= 0)
        {
            return AppointmentRequestFailure(session, "Appointment request needs a valid date, start time, and duration.", metadata);
        }

        if (appointmentDate < metadata.BaseDate)
        {
            return AppointmentRequestFailure(session, "Appointment request date must be today or later.", metadata);
        }

        var categoryId = request.CategoryId ?? 9;
        var title = GetPortalAppointmentRequestTitle(categoryId);
        var reason = NormalizeText(request.Reason);
        var appointmentId = $"APPT-PORTAL-{Guid.NewGuid():N}";
        var reminderId = $"MSG-PORTAL-APPT-{Guid.NewGuid():N}";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with patient_match as (
                select canonical_id, legacy_pid, provider_id, facility_id
                from patients
                where canonical_id = @patient_id
                limit 1
            ),
            provider_match as (
                select id, username, trim(concat(first_name, ' ', last_name)) as provider_name
                from staff
                where active = true
                  and id = coalesce(@provider_id, (select provider_id from patient_match))
                limit 1
            ),
            facility_match as (
                select id, name
                from facilities
                where inactive = false
                  and id = coalesce(@facility_id, (select facility_id from patient_match))
                limit 1
            ),
            inserted as (
                insert into appointments (
                    id,
                    patient_id,
                    pid,
                    provider_id,
                    facility_id,
                    billing_location_id,
                    appointment_date,
                    start_time,
                    duration_minutes,
                    category_id,
                    title,
                    status,
                    comments,
                    recurrence_type
                )
                select
                    @appointment_id,
                    patient_match.canonical_id,
                    patient_match.legacy_pid,
                    provider_match.id,
                    coalesce(facility_match.id, patient_match.facility_id),
                    coalesce(facility_match.id, patient_match.facility_id),
                    @appointment_date,
                    @start_time,
                    @duration_minutes,
                    @category_id,
                    @title,
                    '^',
                    @reason,
                    0
                from patient_match
                join provider_match on true
                left join facility_match on true
                returning *
            )
            select
                inserted.id,
                inserted.appointment_date,
                inserted.start_time,
                inserted.title,
                inserted.status,
                inserted.category_id,
                inserted.comments,
                provider_match.username as provider_username,
                provider_match.provider_name,
                facility_match.name as facility_name
            from inserted
            join provider_match on provider_match.id = inserted.provider_id
            left join facility_match on facility_match.id = inserted.facility_id;
            """;
        command.Parameters.AddWithValue("patient_id", session.CanonicalId);
        command.Parameters.Add("provider_id", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.Add("facility_id", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.AddWithValue("appointment_id", appointmentId);
        command.Parameters.Add("appointment_date", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("start_time", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.AddWithValue("duration_minutes", request.DurationMinutes.Value);
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason ?? (object)DBNull.Value;

        PatientPortalHomeAppointmentSummary? appointment = null;
        var assignedTo = string.Empty;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return AppointmentRequestFailure(session, "Appointment request could not be created for the selected provider and facility.", metadata);
            }

            assignedTo = ReadNullableString(reader, "provider_username") ?? string.Empty;
            appointment = new PatientPortalHomeAppointmentSummary(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")).ToString("yyyy-MM-dd"),
                StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")).ToString("HH:mm"),
                Title: ReadNullableString(reader, "title") ?? title,
                Status: ReadNullableString(reader, "status"),
                CategoryId: ReadNullableInt(reader, "category_id"),
                CategoryName: GetAppointmentCategoryName(ReadNullableInt(reader, "category_id")),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Comments: ReadNullableString(reader, "comments"));
        }

        if (appointment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AppointmentRequestFailure(session, "Appointment request could not be created.", metadata);
        }

        var reminderBody = BuildPortalAppointmentReminderBody(session, appointment, reason);
        await using var reminderCommand = connection.CreateCommand();
        reminderCommand.Transaction = transaction;
        reminderCommand.CommandText = """
            insert into messages (
                id,
                patient_id,
                pid,
                message_date,
                title,
                body,
                status,
                assigned_to,
                portal_relation,
                is_encrypted,
                deleted,
                activity
            )
            values (
                @id,
                @patient_id,
                @pid,
                current_date,
                'Patient Reminders',
                @body,
                'New',
                @assigned_to,
                @portal_relation,
                false,
                0,
                1
            );
            """;
        reminderCommand.Parameters.AddWithValue("id", reminderId);
        reminderCommand.Parameters.AddWithValue("patient_id", session.CanonicalId);
        reminderCommand.Parameters.AddWithValue("pid", session.LegacyPid.Value);
        reminderCommand.Parameters.AddWithValue("body", reminderBody);
        reminderCommand.Parameters.AddWithValue("assigned_to", assignedTo);
        reminderCommand.Parameters.AddWithValue("portal_relation", $"portal:appointment-request:{appointment.Id}");
        await reminderCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PatientPortalAppointmentRequestResponse(
            Authenticated: true,
            Created: true,
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
            Appointment: appointment,
            Reminder: new PatientPortalAppointmentReminder(
                Id: reminderId,
                Title: "Patient Reminders",
                Body: reminderBody,
                AssignedTo: assignedTo,
                Status: "New"),
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalMessagesResponse> GetMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyMessages(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var messages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.Inbox,
            cancellationToken);
        var sentMessages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.Sent,
            cancellationToken);
        var allMessages = await GetPortalMessagesAsync(
            connection,
            session.PortalUsername,
            PortalMessageFolder.All,
            cancellationToken);

        return new PatientPortalMessagesResponse(
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
            MessageCount: messages.Count,
            Messages: messages,
            SentMessageCount: sentMessages.Count,
            SentMessages: sentMessages,
            AllMessageCount: allMessages.Count,
            AllMessages: allMessages,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalDocumentsResponse> GetDocumentsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return EmptyDocuments(session, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var documents = await GetPortalDocumentsAsync(connection, session.LegacyPid.Value, cancellationToken);

        return BuildDocumentsResponse(session, metadata, documents);
    }

    public async Task<PatientPortalDocumentsDownloadPackage> DownloadDocumentsAsync(
        Guid sessionId,
        PatientPortalDocumentsDownloadRequest request,
        CancellationToken cancellationToken)
    {
        var selectedDocumentIds = (request.DocumentIds ?? Array.Empty<int>())
            .Where(documentId => documentId > 0)
            .Distinct()
            .ToArray();
        if (selectedDocumentIds.Length == 0)
        {
            return DownloadFailure("Select at least one patient document to download.");
        }

        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return DownloadFailure(session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var documents = await GetPortalDownloadDocumentsAsync(
            connection,
            session.LegacyPid.Value,
            selectedDocumentIds,
            cancellationToken);
        if (documents.Count == 0)
        {
            return DownloadFailure("Selected patient documents were not found in the signed-in portal account.");
        }

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents)
            {
                var entry = archive.CreateEntry(BuildZipEntryName(document.Item, usedEntryNames), CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(document.Content, cancellationToken);
            }
        }

        return new PatientPortalDocumentsDownloadPackage(
            Downloadable: true,
            FileName: "patient_documents.zip",
            ContentType: "application/zip",
            Content: stream.ToArray(),
            DocumentCount: documents.Count,
            Documents: documents.Select(document => document.Item).ToArray(),
            FailureReason: null);
    }

    public async Task<PatientPortalMessageThreadResponse> GetMessageThreadAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ThreadFailure(session, messageId.ToString(), 0, session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var metadata = await GetMetadataAsync(connection, cancellationToken);
        var anchor = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (anchor is null)
        {
            return ThreadFailure(
                session,
                messageId.ToString(),
                0,
                "Secure message was not found in the signed-in portal mailbox.",
                metadata);
        }

        var threadId = ResolvePortalThreadId(anchor.Item, messageId);
        var threadMessages = await GetPortalMessageThreadAsync(
            connection,
            session.PortalUsername,
            messageId,
            threadId,
            cancellationToken);

        return new PatientPortalMessageThreadResponse(
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
            MessageId: anchor.Item.Id,
            ThreadId: threadId,
            AnchorMessage: anchor.Item,
            ThreadMessageCount: threadMessages.Count,
            ThreadMessages: threadMessages,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalComposeMessageResponse> ComposeMessageAsync(
        Guid sessionId,
        PatientPortalComposeMessageRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ComposeFailure(session, session.FailureReason ?? "Session is not active.");
        }

        var recipientId = NormalizeText(request.RecipientId) ?? "admin";
        var title = NormalizeText(request.Title);
        var body = NormalizeText(request.Body);
        if (string.IsNullOrWhiteSpace(title))
        {
            return ComposeFailure(session, "Secure message title is required.", recipientId);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return ComposeFailure(session, "Secure message body is required.", recipientId);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var recipientName = await GetRecipientDisplayNameAsync(connection, recipientId, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var nextId = await GetNextPortalMailboxIdAsync(connection, transaction, cancellationToken);
        var sentMessage = new PatientPortalMessageItem(
            Id: nextId.ToString(),
            Date: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            Title: title,
            Body: body,
            Status: "New",
            AssignedTo: recipientId,
            SenderId: session.PortalUsername,
            SenderName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            MailChain: nextId,
            ReplyMailChain: nextId,
            PortalRelation: "portal:composed",
            IsEncrypted: false);
        var recipientMessage = sentMessage with
        {
            Id = (nextId + 1).ToString(),
            MailChain = nextId + 1,
            SenderName = session.DisplayName,
            RecipientName = recipientName
        };

        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            sentMessage,
            owner: session.PortalUsername,
            userValue: session.PortalUsername,
            mailChain: nextId,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            recipientMessage,
            owner: recipientId,
            userValue: session.PortalUsername,
            mailChain: nextId + 1,
            replyMailChain: nextId,
            transaction: transaction,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalComposeMessageResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            SentMessage: sentMessage,
            RecipientMessage: recipientMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalReplyMessageResponse> ReplyToMessageAsync(
        Guid sessionId,
        int messageId,
        PatientPortalReplyMessageRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ReplyFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        var body = NormalizeText(request.Body);
        if (string.IsNullOrWhiteSpace(body))
        {
            return ReplyFailure(session, messageId.ToString(), "Secure message reply body is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var original = await GetPortalInboxMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (original is null)
        {
            return ReplyFailure(session, messageId.ToString(), "Secure message was not found in the signed-in portal inbox.");
        }

        var recipientId = NormalizeText(original.Item.SenderId)
            ?? NormalizeText(original.Item.AssignedTo)
            ?? "admin";
        var recipientName = NormalizeText(original.Item.SenderName)
            ?? await GetRecipientDisplayNameAsync(connection, recipientId, cancellationToken);
        var replyThreadId = original.Item.ReplyMailChain > 0
            ? original.Item.ReplyMailChain
            : original.Item.MailChain > 0
                ? original.Item.MailChain
                : messageId;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var nextId = await GetNextPortalMailboxIdAsync(connection, transaction, cancellationToken);
        var messageDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var sentMessage = new PatientPortalMessageItem(
            Id: nextId.ToString(),
            Date: messageDate,
            Title: original.Item.Title,
            Body: body,
            Status: "New",
            AssignedTo: recipientId,
            SenderId: session.PortalUsername,
            SenderName: session.DisplayName,
            RecipientId: recipientId,
            RecipientName: recipientName,
            MailChain: nextId,
            ReplyMailChain: replyThreadId,
            PortalRelation: "portal:reply",
            IsEncrypted: false);
        var recipientMessage = sentMessage with
        {
            Id = (nextId + 1).ToString(),
            MailChain = nextId + 1
        };

        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            sentMessage,
            owner: session.PortalUsername,
            userValue: session.PortalUsername,
            mailChain: nextId,
            replyMailChain: replyThreadId,
            transaction: transaction,
            cancellationToken: cancellationToken);
        await InsertPortalMailboxMessageAsync(
            connection,
            session,
            recipientMessage,
            owner: recipientId,
            userValue: session.PortalUsername,
            mailChain: nextId + 1,
            replyMailChain: replyThreadId,
            transaction: transaction,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalReplyMessageResponse(
            Authenticated: true,
            Created: true,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            OriginalMessageId: original.Item.Id,
            OriginalMessage: original.Item,
            SentMessage: sentMessage,
            RecipientMessage: recipientMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalReadMessageResponse> MarkMessageReadAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ReadFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var message = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (message is null)
        {
            return ReadFailure(
                session,
                messageId.ToString(),
                "Secure message was not found in the signed-in portal mailbox.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            update portal_mailbox_messages
            set message_status = 'Read',
                activity = 1
            where id = @message_id
              and owner = @portal_username
              and deleted = 0
            returning id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted;
            """;
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);

        PatientPortalMessageItem? readMessage = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                readMessage = ReadPortalMessageItem(reader);
            }
        }

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalReadMessageResponse(
            Authenticated: true,
            MarkedRead: readMessage is not null,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageId: message.Item.Id,
            Message: readMessage,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: readMessage is null ? "Secure message was not marked read." : null,
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalDeleteMessageResponse> DeleteMessageAsync(
        Guid sessionId,
        int messageId,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return DeleteFailure(session, messageId.ToString(), session.FailureReason ?? "Session is not active.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var message = await GetPortalOwnedMessageAsync(connection, session.PortalUsername, messageId, cancellationToken);
        if (message is null)
        {
            return DeleteFailure(
                session,
                messageId.ToString(),
                "Secure message was not found in the signed-in portal mailbox.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with updated as (
              update portal_mailbox_messages
              set message_status = 'Delete',
                  activity = 1,
                  deleted = 1
              where owner = @portal_username
                and (mail_chain = @message_id or id = @message_id)
              returning id, message_date, title, body, message_status, assigned_to, portal_relation,
                mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            )
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from updated
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        var deletedMessages = new List<PatientPortalMessageItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                deletedMessages.Add(ReadPortalMessageItem(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalDeleteMessageResponse(
            Authenticated: true,
            Deleted: deletedMessages.Count > 0,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageId: message.Item.Id,
            DeletedMessage: deletedMessages.FirstOrDefault(),
            DeletedMessageCount: deletedMessages.Count,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: deletedMessages.Count > 0 ? null : "Secure message was not archived.",
            SessionSource: session.SessionSource);
    }

    public async Task<PatientPortalArchiveMessagesResponse> ArchiveMessagesAsync(
        Guid sessionId,
        PatientPortalArchiveMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var session = await GetCurrentSessionAsync(sessionId, cancellationToken);
        var requestedMessageIds = (request.MessageIds ?? Array.Empty<int>())
            .Where(messageId => messageId > 0)
            .Distinct()
            .ToArray();
        if (!session.Authenticated || session.LegacyPid is null)
        {
            return ArchiveMessagesFailure(
                session,
                requestedMessageIds.Select(messageId => messageId.ToString()).ToArray(),
                session.FailureReason ?? "Session is not active.");
        }

        if (requestedMessageIds.Length == 0)
        {
            return ArchiveMessagesFailure(session, Array.Empty<string>(), "Select at least one secure message to archive.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with selected as (
              select distinct case when mail_chain > 0 then mail_chain else id end as archive_id
              from portal_mailbox_messages
              where deleted = 0
                and owner = @portal_username
                and (sender_id = @portal_username or recipient_id = @portal_username)
                and (id = any(@message_ids) or mail_chain = any(@message_ids))
            ),
            updated as (
              update portal_mailbox_messages messages
              set message_status = 'Delete',
                  activity = 1,
                  deleted = 1
              from selected
              where messages.owner = @portal_username
                and (messages.mail_chain = selected.archive_id or messages.id = selected.archive_id)
              returning messages.id, messages.message_date, messages.title, messages.body, messages.message_status,
                messages.assigned_to, messages.portal_relation, messages.mail_chain, messages.sender_id,
                messages.sender_name, messages.recipient_id, messages.recipient_name, messages.reply_mail_chain,
                messages.is_encrypted
            )
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from updated
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", session.PortalUsername);
        command.Parameters.Add("message_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = requestedMessageIds;

        var archivedMessages = new List<PatientPortalMessageItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                archivedMessages.Add(ReadPortalMessageItem(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var sentCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Sent, cancellationToken);
        var inboxCount = await GetPortalMessageCountAsync(connection, session.PortalUsername, PortalMessageFolder.Inbox, cancellationToken);

        return new PatientPortalArchiveMessagesResponse(
            Authenticated: true,
            Archived: archivedMessages.Count > 0,
            SessionId: session.SessionId,
            Username: session.Username,
            PortalUsername: session.PortalUsername,
            CanonicalId: session.CanonicalId,
            LegacyPid: session.LegacyPid,
            Pubpid: session.Pubpid,
            DisplayName: session.DisplayName,
            MessageIds: requestedMessageIds.Select(messageId => messageId.ToString()).ToArray(),
            ArchivedMessages: archivedMessages,
            ArchivedMessageCount: archivedMessages.Count,
            MessageCount: inboxCount,
            SentMessageCount: sentCount,
            FailureReason: archivedMessages.Count > 0 ? null : "Selected secure messages were not archived.",
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

    private static PatientPortalAppointmentsResponse EmptyAppointments(
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
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        PastAppointmentCount: 0,
        PastAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentsResponse MissingSessionAppointments(string reason) => new(
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
        UpcomingAppointmentCount: 0,
        UpcomingAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        PastAppointmentCount: 0,
        PastAppointments: Array.Empty<PatientPortalHomeAppointmentSummary>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestResponse AppointmentRequestFailure(
        PatientPortalSessionResponse session,
        string reason,
        DatasetMetadata? metadata = null) => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: metadata?.DatasetId ?? "unseeded",
        DatasetVersion: metadata?.DatasetVersion ?? "unknown",
        AsOfDate: (metadata?.BaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        Appointment: null,
        Reminder: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalAppointmentRequestResponse MissingSessionAppointmentRequest(string reason) => new(
        Authenticated: false,
        Created: false,
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
        Appointment: null,
        Reminder: null,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessagesResponse EmptyMessages(
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
        MessageCount: 0,
        Messages: Array.Empty<PatientPortalMessageItem>(),
        SentMessageCount: 0,
        SentMessages: Array.Empty<PatientPortalMessageItem>(),
        AllMessageCount: 0,
        AllMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessagesResponse MissingSessionMessages(string reason) => new(
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
        MessageCount: 0,
        Messages: Array.Empty<PatientPortalMessageItem>(),
        SentMessageCount: 0,
        SentMessages: Array.Empty<PatientPortalMessageItem>(),
        AllMessageCount: 0,
        AllMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDocumentsResponse EmptyDocuments(
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
        DocumentCount: 0,
        Categories: Array.Empty<PatientPortalDocumentCategory>(),
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDocumentsResponse MissingSessionDocuments(string reason) => new(
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
        DocumentCount: 0,
        Categories: Array.Empty<PatientPortalDocumentCategory>(),
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

    public static PatientPortalMessagesResponse MissingSessionHeaderMessages() =>
        MissingSessionMessages("Patient portal session header was not supplied.");

    public static PatientPortalDocumentsResponse MissingSessionHeaderDocuments() =>
        MissingSessionDocuments("Patient portal session header was not supplied.");

    public static PatientPortalDocumentsDownloadPackage MissingSessionHeaderDocumentsDownload() =>
        DownloadFailure("Patient portal session header was not supplied.");

    public static PatientPortalMessageThreadResponse MissingSessionHeaderMessageThread(string messageId) =>
        new(
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
            MessageId: messageId,
            ThreadId: 0,
            AnchorMessage: null,
            ThreadMessageCount: 0,
            ThreadMessages: Array.Empty<PatientPortalMessageItem>(),
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalHomeSummaryResponse MissingSessionHeaderHomeSummary() =>
        MissingSessionHomeSummary("Patient portal session header was not supplied.");

    public static PatientPortalAppointmentsResponse MissingSessionHeaderAppointments() =>
        MissingSessionAppointments("Patient portal session header was not supplied.");

    public static PatientPortalAppointmentRequestResponse MissingSessionHeaderAppointmentRequest() =>
        MissingSessionAppointmentRequest("Patient portal session header was not supplied.");

    public static PatientPortalComposeMessageResponse MissingSessionHeaderComposeMessage() =>
        new(
            Authenticated: false,
            Created: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            RecipientId: string.Empty,
            RecipientName: string.Empty,
            SentMessage: null,
            RecipientMessage: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalReplyMessageResponse MissingSessionHeaderReplyMessage(string messageId) =>
        new(
            Authenticated: false,
            Created: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            OriginalMessageId: messageId,
            OriginalMessage: null,
            SentMessage: null,
            RecipientMessage: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalReadMessageResponse MissingSessionHeaderReadMessage(string messageId) =>
        new(
            Authenticated: false,
            MarkedRead: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageId: messageId,
            Message: null,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalDeleteMessageResponse MissingSessionHeaderDeleteMessage(string messageId) =>
        new(
            Authenticated: false,
            Deleted: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageId: messageId,
            DeletedMessage: null,
            DeletedMessageCount: 0,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    public static PatientPortalArchiveMessagesResponse MissingSessionHeaderArchiveMessages() =>
        new(
            Authenticated: false,
            Archived: false,
            SessionId: null,
            Username: string.Empty,
            PortalUsername: string.Empty,
            CanonicalId: string.Empty,
            LegacyPid: null,
            Pubpid: string.Empty,
            DisplayName: string.Empty,
            MessageIds: Array.Empty<string>(),
            ArchivedMessages: Array.Empty<PatientPortalMessageItem>(),
            ArchivedMessageCount: 0,
            MessageCount: 0,
            SentMessageCount: 0,
            FailureReason: "Patient portal session header was not supplied.",
            SessionSource: SessionSource);

    private static PatientPortalComposeMessageResponse ComposeFailure(
        PatientPortalSessionResponse session,
        string reason,
        string recipientId = "") => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        RecipientId: recipientId,
        RecipientName: string.Empty,
        SentMessage: null,
        RecipientMessage: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalReplyMessageResponse ReplyFailure(
        PatientPortalSessionResponse session,
        string originalMessageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        Created: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        OriginalMessageId: originalMessageId,
        OriginalMessage: null,
        SentMessage: null,
        RecipientMessage: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalReadMessageResponse ReadFailure(
        PatientPortalSessionResponse session,
        string messageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        MarkedRead: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageId: messageId,
        Message: null,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalDeleteMessageResponse DeleteFailure(
        PatientPortalSessionResponse session,
        string messageId,
        string reason) => new(
        Authenticated: session.Authenticated,
        Deleted: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageId: messageId,
        DeletedMessage: null,
        DeletedMessageCount: 0,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalArchiveMessagesResponse ArchiveMessagesFailure(
        PatientPortalSessionResponse session,
        IReadOnlyList<string> messageIds,
        string reason) => new(
        Authenticated: session.Authenticated,
        Archived: false,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        MessageIds: messageIds,
        ArchivedMessages: Array.Empty<PatientPortalMessageItem>(),
        ArchivedMessageCount: 0,
        MessageCount: 0,
        SentMessageCount: 0,
        FailureReason: reason,
        SessionSource: SessionSource);

    private static PatientPortalMessageThreadResponse ThreadFailure(
        PatientPortalSessionResponse session,
        string messageId,
        int threadId,
        string reason,
        DatasetMetadata? metadata = null) => new(
        Authenticated: session.Authenticated,
        SessionId: session.SessionId,
        Username: session.Username,
        PortalUsername: session.PortalUsername,
        CanonicalId: session.CanonicalId,
        LegacyPid: session.LegacyPid,
        Pubpid: session.Pubpid,
        DisplayName: session.DisplayName,
        DatasetId: metadata?.DatasetId ?? "unseeded",
        DatasetVersion: metadata?.DatasetVersion ?? "unknown",
        AsOfDate: (metadata?.BaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd"),
        MessageId: messageId,
        ThreadId: threadId,
        AnchorMessage: null,
        ThreadMessageCount: 0,
        ThreadMessages: Array.Empty<PatientPortalMessageItem>(),
        FailureReason: reason,
        SessionSource: SessionSource);

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

    private static PatientPortalDocumentsResponse BuildDocumentsResponse(
        PatientPortalSessionResponse session,
        DatasetMetadata metadata,
        IReadOnlyList<PatientPortalDocumentItem> documents)
    {
        var categories = documents
            .GroupBy(document => new { document.CategoryId, document.CategoryName, document.DisplayPath })
            .OrderBy(group => group.Key.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PatientPortalDocumentCategory(
                CategoryId: group.Key.CategoryId,
                CategoryName: group.Key.CategoryName,
                DisplayPath: group.Key.DisplayPath,
                DocumentCount: group.Count(),
                Documents: group.OrderByDescending(document => document.DocDate).ThenByDescending(document => document.Id).ToArray()))
            .ToArray();

        return new PatientPortalDocumentsResponse(
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
            DocumentCount: documents.Count,
            Categories: categories,
            Documents: documents,
            FailureReason: null,
            SessionSource: session.SessionSource);
    }

    private static PatientPortalDocumentsDownloadPackage DownloadFailure(string reason) => new(
        Downloadable: false,
        FileName: "patient_documents.zip",
        ContentType: "application/zip",
        Content: Array.Empty<byte>(),
        DocumentCount: 0,
        Documents: Array.Empty<PatientPortalDocumentItem>(),
        FailureReason: reason);

    private static async Task<IReadOnlyList<PatientPortalDocumentItem>> GetPortalDocumentsAsync(
        NpgsqlConnection connection,
        int pid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, storage_method
            from patient_documents
            where pid = @pid and deleted = 0
            order by category_name, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);

        var documents = new List<PatientPortalDocumentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(ReadPortalDocumentItem(reader));
        }

        return documents;
    }

    private static async Task<IReadOnlyList<PortalDownloadDocumentRow>> GetPortalDownloadDocumentsAsync(
        NpgsqlConnection connection,
        int pid,
        IReadOnlyList<int> documentIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, storage_method, coalesce(content, '') as content, content_bytes
            from patient_documents
            where pid = @pid
              and deleted = 0
              and coalesce(storage_method, 'database') <> 'web_url'
              and id = any(@document_ids)
            order by category_name, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.Add("document_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = documentIds.ToArray();

        var documents = new List<PortalDownloadDocumentRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = ReadPortalDocumentItem(reader);
            var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
            var contentBytes = reader.IsDBNull(contentBytesOrdinal)
                ? Encoding.UTF8.GetBytes(reader.GetString(reader.GetOrdinal("content")))
                : (byte[])reader.GetValue(contentBytesOrdinal);

            documents.Add(new PortalDownloadDocumentRow(item, contentBytes));
        }

        return documents;
    }

    private static PatientPortalDocumentItem ReadPortalDocumentItem(NpgsqlDataReader reader)
    {
        var name = reader.GetString(reader.GetOrdinal("name"));
        var mimetype = ReadNullableString(reader, "mimetype");
        var storageMethod = ReadNullableString(reader, "storage_method");
        var categoryName = reader.GetString(reader.GetOrdinal("category_name"));
        var fileName = ReadNullableString(reader, "file_name") ?? BuildPortalDocumentFileName(name, mimetype);

        return new PatientPortalDocumentItem(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
            CategoryName: categoryName,
            DisplayPath: BuildPortalDocumentDisplayPath(categoryName),
            Name: name,
            DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
            UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
            Mimetype: mimetype,
            FileName: fileName,
            SizeBytes: ReadNullableInt(reader, "size_bytes"),
            StorageMethod: storageMethod,
            CanDownload: !string.Equals(storageMethod, "web_url", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPortalDocumentDisplayPath(string categoryName)
    {
        var trimmed = categoryName.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Unfiled" : trimmed;
    }

    private static string BuildPortalDocumentFileName(string name, string? mimetype)
    {
        var extension = mimetype switch
        {
            "application/pdf" => ".pdf",
            "application/xml" => ".xml",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "text/uri-list" => ".url",
            _ => ".txt"
        };
        var sanitized = SanitizeZipPathSegment(name);
        return sanitized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? sanitized : $"{sanitized}{extension}";
    }

    private static string BuildZipEntryName(PatientPortalDocumentItem document, HashSet<string> usedEntryNames)
    {
        var folder = SanitizeZipPathSegment(document.DisplayPath);
        var fileName = SanitizeZipPathSegment(document.FileName);
        var candidate = $"{folder}/{fileName}";
        if (usedEntryNames.Add(candidate))
        {
            return candidate;
        }

        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var index = 2;
        while (true)
        {
            candidate = $"{folder}/{baseName}-{index}{extension}";
            if (usedEntryNames.Add(candidate))
            {
                return candidate;
            }

            index += 1;
        }
    }

    private static string SanitizeZipPathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Trim().Length);
        foreach (var character in value.Trim())
        {
            builder.Append(character is '/' or '\\' || invalidCharacters.Contains(character) ? '-' : character);
        }

        var sanitized = builder.ToString().Trim('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }

    private static async Task<PatientPortalHomeMessageSummary> GetMessageSummaryAsync(
        NpgsqlConnection connection,
        string portalUsername,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              count(*)::int as total_messages,
              count(*) filter (where message_status = 'New')::int as new_messages,
              count(*) filter (where message_status = 'Done')::int as done_messages,
              (
                select title
                from portal_mailbox_messages latest
                where latest.deleted = 0
                  and latest.owner = @portal_username
                  and latest.recipient_id = @portal_username
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_title,
              (
                select message_date
                from portal_mailbox_messages latest
                where latest.deleted = 0
                  and latest.owner = @portal_username
                  and latest.recipient_id = @portal_username
                order by latest.message_date desc, latest.id desc
                limit 1
              ) as latest_message_date
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and recipient_id = @portal_username;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

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

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> GetPortalMessagesAsync(
        NpgsqlConnection connection,
        string portalUsername,
        PortalMessageFolder folder,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var folderPredicate = GetPortalMessageFolderPredicate(folder);
        command.CommandText = $"""
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0 and {folderPredicate}
            order by message_date desc, id desc;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var messages = new List<PatientPortalMessageItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadPortalMessageItem(reader));
        }

        return messages;
    }

    private static async Task<PortalMailboxMessageRow?> GetPortalOwnedMessageAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and (sender_id = @portal_username or recipient_id = @portal_username)
              and id = @message_id
            limit 1;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PortalMailboxMessageRow(ReadPortalMessageItem(reader))
            : null;
    }

    private static async Task<IReadOnlyList<PatientPortalMessageItem>> GetPortalMessageThreadAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        int threadId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and (sender_id = @portal_username or recipient_id = @portal_username)
              and (reply_mail_chain = @thread_id or mail_chain = @thread_id or id = @message_id)
            order by message_date asc, id asc;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("thread_id", NpgsqlDbType.Integer).Value = threadId;
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        var messages = new List<PatientPortalMessageItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadPortalMessageItem(reader));
        }

        return messages;
    }

    private static async Task<PortalMailboxMessageRow?> GetPortalInboxMessageAsync(
        NpgsqlConnection connection,
        string portalUsername,
        int messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, message_date, title, body, message_status, assigned_to, portal_relation,
              mail_chain, sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain, is_encrypted
            from portal_mailbox_messages
            where deleted = 0
              and owner = @portal_username
              and recipient_id = @portal_username
              and id = @message_id
            limit 1;
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);
        command.Parameters.Add("message_id", NpgsqlDbType.Integer).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var item = ReadPortalMessageItem(reader);

        return new PortalMailboxMessageRow(item);
    }

    private static PatientPortalMessageItem ReadPortalMessageItem(NpgsqlDataReader reader) => new(
        Id: reader.GetInt32(reader.GetOrdinal("id")).ToString(),
        Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("message_date")).ToString("yyyy-MM-dd"),
        Title: ReadNullableString(reader, "title") ?? string.Empty,
        Body: ReadNullableString(reader, "body") ?? string.Empty,
        Status: ReadNullableString(reader, "message_status") ?? string.Empty,
        AssignedTo: ReadNullableString(reader, "assigned_to") ?? string.Empty,
        SenderId: ReadNullableString(reader, "sender_id") ?? string.Empty,
        SenderName: ReadNullableString(reader, "sender_name") ?? string.Empty,
        RecipientId: ReadNullableString(reader, "recipient_id") ?? string.Empty,
        RecipientName: ReadNullableString(reader, "recipient_name") ?? string.Empty,
        MailChain: reader.GetInt32(reader.GetOrdinal("mail_chain")),
        ReplyMailChain: reader.GetInt32(reader.GetOrdinal("reply_mail_chain")),
        PortalRelation: ReadNullableString(reader, "portal_relation"),
        IsEncrypted: reader.GetBoolean(reader.GetOrdinal("is_encrypted")));

    private static int ResolvePortalThreadId(PatientPortalMessageItem message, int fallbackMessageId)
    {
        if (message.ReplyMailChain > 0)
        {
            return message.ReplyMailChain;
        }

        return message.MailChain > 0 ? message.MailChain : fallbackMessageId;
    }

    private static async Task<int> GetNextPortalMailboxIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select greatest(coalesce(max(id), 9390000) + 1, 9390001)
            from portal_mailbox_messages;
            """;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task<int> GetPortalMessageCountAsync(
        NpgsqlConnection connection,
        string portalUsername,
        PortalMessageFolder folder,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var folderPredicate = GetPortalMessageFolderPredicate(folder);
        command.CommandText = $"""
            select count(*)::int
            from portal_mailbox_messages
            where deleted = 0 and {folderPredicate};
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static string GetPortalMessageFolderPredicate(PortalMessageFolder folder) => folder switch
    {
        PortalMessageFolder.Sent => "owner = @portal_username and sender_id = @portal_username",
        PortalMessageFolder.All => "owner = @portal_username",
        _ => "owner = @portal_username and recipient_id = @portal_username"
    };

    private static async Task InsertPortalMailboxMessageAsync(
        NpgsqlConnection connection,
        PatientPortalSessionResponse session,
        PatientPortalMessageItem message,
        string owner,
        string userValue,
        int mailChain,
        int replyMailChain,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into portal_mailbox_messages (
              id, patient_id, pid, message_date, body, owner, user_value, group_name,
              activity, authorized, title, assigned_to, message_status, portal_relation, mail_chain,
              sender_id, sender_name, recipient_id, recipient_name, reply_mail_chain,
              is_encrypted, deleted
            )
            values (
              @id, @patient_id, @pid, @message_date, @body, @owner, @user_value, 'Default',
              1, 1, @title, @assigned_to, @message_status, @portal_relation, @mail_chain,
              @sender_id, @sender_name, @recipient_id, @recipient_name, @reply_mail_chain,
              @is_encrypted, 0
            );
            """;
        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = int.Parse(message.Id);
        command.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = session.CanonicalId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = session.LegacyPid ?? 0;
        command.Parameters.Add("message_date", NpgsqlDbType.Date).Value = DateOnly.Parse(message.Date);
        command.Parameters.Add("body", NpgsqlDbType.Text).Value = message.Body;
        command.Parameters.Add("owner", NpgsqlDbType.Text).Value = owner;
        command.Parameters.Add("user_value", NpgsqlDbType.Text).Value = userValue;
        command.Parameters.Add("title", NpgsqlDbType.Text).Value = message.Title;
        command.Parameters.Add("assigned_to", NpgsqlDbType.Text).Value = message.AssignedTo;
        command.Parameters.Add("message_status", NpgsqlDbType.Text).Value = message.Status;
        command.Parameters.Add("portal_relation", NpgsqlDbType.Text).Value = message.PortalRelation is { } relation ? (object)relation : DBNull.Value;
        command.Parameters.Add("mail_chain", NpgsqlDbType.Integer).Value = mailChain;
        command.Parameters.Add("sender_id", NpgsqlDbType.Text).Value = message.SenderId;
        command.Parameters.Add("sender_name", NpgsqlDbType.Text).Value = message.SenderName;
        command.Parameters.Add("recipient_id", NpgsqlDbType.Text).Value = message.RecipientId;
        command.Parameters.Add("recipient_name", NpgsqlDbType.Text).Value = message.RecipientName;
        command.Parameters.Add("reply_mail_chain", NpgsqlDbType.Integer).Value = replyMailChain;
        command.Parameters.Add("is_encrypted", NpgsqlDbType.Boolean).Value = message.IsEncrypted;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> GetRecipientDisplayNameAsync(
        NpgsqlConnection connection,
        string recipientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select coalesce(
              (select trim(first_name || ' ' || last_name) from staff where username = @recipient_id limit 1),
              (select display_name from auth_accounts where username = @recipient_id limit 1),
              @recipient_id
            ) as display_name;
            """;
        command.Parameters.Add("recipient_id", NpgsqlDbType.Text).Value = recipientId;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value?.ToString() ?? recipientId;
    }

    private static async Task<AppointmentSummaryRows> GetUpcomingAppointmentsAsync(
        NpgsqlConnection connection,
        int pid,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
        => await GetPortalAppointmentsAsync(
            connection,
            pid,
            asOfDate,
            PortalAppointmentWindow.Upcoming,
            cancellationToken);

    private static async Task<AppointmentSummaryRows> GetPortalAppointmentsAsync(
        NpgsqlConnection connection,
        int pid,
        DateOnly asOfDate,
        PortalAppointmentWindow window,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = window == PortalAppointmentWindow.Upcoming
            ? """
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
            """
            : """
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
              and a.appointment_date < @as_of_date
            order by a.appointment_date desc, a.start_time desc, a.id desc
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

    private static string GetPortalAppointmentRequestTitle(int categoryId) => categoryId switch
    {
        9 => "Established Patient",
        10 => "New Patient",
        13 => "Preventive Care",
        _ => GetAppointmentCategoryName(categoryId) ?? "Office Visit"
    };

    private static string BuildPortalAppointmentReminderBody(
        PatientPortalSessionResponse session,
        PatientPortalHomeAppointmentSummary appointment,
        string? reason)
    {
        var appointmentTime = appointment.StartTime.Length == 5
            ? $"{appointment.StartTime}:00"
            : appointment.StartTime;
        var body = new StringBuilder()
            .Append("A New Appointment request was received from portal patient ")
            .Append(session.DisplayName)
            .Append(" regarding appointment dated ")
            .Append(appointment.Date)
            .Append(' ')
            .Append(appointmentTime)
            .Append('.');

        if (!string.IsNullOrWhiteSpace(reason))
        {
            body.Append(" Reason ").Append(reason).Append('.');
        }

        body.Append(" Use Portal Dashboard to confirm with patient.");
        return body.ToString();
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string HashPassword(string salt, string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private enum PortalMessageFolder
    {
        Inbox,
        Sent,
        All
    }

    private enum PortalAppointmentWindow
    {
        Upcoming,
        Past
    }

    private sealed record PortalDownloadDocumentRow(PatientPortalDocumentItem Item, byte[] Content);

    private sealed record PortalMailboxMessageRow(PatientPortalMessageItem Item);

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
