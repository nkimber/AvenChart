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
            FailureReason: null,
            SessionSource: session.SessionSource);
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
        FailureReason: reason,
        SessionSource: SessionSource);

    public static PatientPortalMessagesResponse MissingSessionHeaderMessages() =>
        MissingSessionMessages("Patient portal session header was not supplied.");

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
        var folderPredicate = folder == PortalMessageFolder.Sent
            ? "owner = @portal_username and sender_id = @portal_username"
            : "owner = @portal_username and recipient_id = @portal_username";
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
        var folderPredicate = folder == PortalMessageFolder.Sent
            ? "owner = @portal_username and sender_id = @portal_username"
            : "owner = @portal_username and recipient_id = @portal_username";
        command.CommandText = $"""
            select count(*)::int
            from portal_mailbox_messages
            where deleted = 0 and {folderPredicate};
            """;
        command.Parameters.AddWithValue("portal_username", portalUsername);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

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
        Sent
    }

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
