using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class IntegrationRepository(
    NpgsqlDataSource dataSource,
    IIntegrationTransport transport)
{
    private static readonly HashSet<string> KnownStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "queued", "dispatching", "retry-scheduled", "delivered", "quarantined"
    };

    private const int MaximumAutomaticAttempts = 3;
    private static readonly TimeSpan DispatchLease = TimeSpan.FromMinutes(5);

    public async Task<IntegrationOutboxMessage> QueueAsync(
        IntegrationOutboxQueueRequest request,
        CancellationToken cancellationToken)
    {
        ValidateQueueRequest(request);
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into integration_outbox (
              event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, created_at, updated_at
            ) values (
              @event_id, @idempotency_key, @event_type, @aggregate_type, @aggregate_id, @destination, @payload,
              'queued', 0, @now, @now, @now
            )
            on conflict (idempotency_key) do update
            set updated_at = integration_outbox.updated_at
            returning event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("idempotency_key", (object?)NormalizeOptional(request.IdempotencyKey) ?? DBNull.Value);
        command.Parameters.AddWithValue("event_type", request.EventType.Trim());
        command.Parameters.AddWithValue("aggregate_type", request.AggregateType.Trim());
        command.Parameters.AddWithValue("aggregate_id", request.AggregateId.Trim());
        command.Parameters.AddWithValue("destination", request.Destination.Trim());
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = request.Payload.GetRawText();
        command.Parameters.AddWithValue("now", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Integration outbox event could not be queued.");
        }

        return ReadOutboxMessage(reader);
    }

    public async Task<IReadOnlyList<IntegrationOutboxMessage>> GetOutboxAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeOptional(status);
        if (normalizedStatus is not null && !KnownStatuses.Contains(normalizedStatus))
        {
            throw new ArgumentException("The integration outbox status is not recognized.", nameof(status));
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at
            from integration_outbox
            where (@status is null or status = @status)
            order by created_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("status", (object?)normalizedStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", boundedLimit);

        var messages = new List<IntegrationOutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadOutboxMessage(reader));
        }

        return messages;
    }

    public async Task<IntegrationDispatchResponse?> DispatchAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var claimed = await ClaimOutboxMessageAsync(eventId, cancellationToken);
        if (claimed is null)
        {
            return null;
        }

        if (!string.Equals(claimed.Status, "dispatching", StringComparison.OrdinalIgnoreCase))
        {
            return new IntegrationDispatchResponse(claimed, Dispatched: false, Outcome: claimed.Status);
        }

        var result = await transport.DeliverAsync(claimed, cancellationToken);
        var completed = await CompleteDispatchAsync(claimed, result, cancellationToken);
        return new IntegrationDispatchResponse(completed, Dispatched: result.Delivered, Outcome: result.Outcome);
    }

    public async Task<IntegrationOutboxMessage> RequeueQuarantinedAsync(
        Guid eventId,
        IntegrationOutboxRecoveryRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("A recovery reason of 500 characters or fewer is required.");
        }

        if (request.ExpectedAttemptCount < MaximumAutomaticAttempts)
        {
            throw new ArgumentException("The expected attempt count is invalid for a quarantined integration event.");
        }

        var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update integration_outbox
            set status = 'queued', available_at = @now, locked_at = null,
              quarantined_at = null, quarantined_by = null, recovery_count = recovery_count + 1,
              updated_at = @now
            where event_id = @event_id and status = 'quarantined' and attempt_count = @attempt_count
            returning event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("attempt_count", request.ExpectedAttemptCount);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The integration event is not quarantined at the expected attempt count.");
        }

        var requeued = ReadOutboxMessage(reader);
        await reader.DisposeAsync();
        await WriteOutboxEventAsync(connection, transaction, eventId, "requeued", reason, actor, requeued.AttemptCount, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return requeued;
    }

    public async Task<IntegrationInboxReceipt> ReceiveAsync(
        IntegrationInboxReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Source)
            || string.IsNullOrWhiteSpace(request.SourceMessageId)
            || string.IsNullOrWhiteSpace(request.MessageType)
            || request.Payload.ValueKind is JsonValueKind.Undefined)
        {
            throw new ArgumentException("Source, source message ID, message type, and payload are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var inboxId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into integration_inbox (
              inbox_id, source, source_message_id, message_type, payload, status, received_at
            ) values (
              @inbox_id, @source, @source_message_id, @message_type, @payload, 'received', @received_at
            )
            on conflict (source, source_message_id) do nothing
            returning inbox_id, source, source_message_id, status, received_at;
            """;
        command.Parameters.AddWithValue("inbox_id", inboxId);
        command.Parameters.AddWithValue("source", request.Source.Trim());
        command.Parameters.AddWithValue("source_message_id", request.SourceMessageId.Trim());
        command.Parameters.AddWithValue("message_type", request.MessageType.Trim());
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = request.Payload.GetRawText();
        command.Parameters.AddWithValue("received_at", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new IntegrationInboxReceipt(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), Duplicate: false, reader.GetFieldValue<DateTimeOffset>(4));
        }

        await reader.DisposeAsync();
        await using var existing = connection.CreateCommand();
        existing.CommandText = """
            select inbox_id, source, source_message_id, status, received_at
            from integration_inbox
            where source = @source and source_message_id = @source_message_id;
            """;
        existing.Parameters.AddWithValue("source", request.Source.Trim());
        existing.Parameters.AddWithValue("source_message_id", request.SourceMessageId.Trim());
        await using var existingReader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await existingReader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Integration inbox duplicate lookup failed.");
        }

        return new IntegrationInboxReceipt(
            existingReader.GetGuid(0), existingReader.GetString(1), existingReader.GetString(2), existingReader.GetString(3), Duplicate: true, existingReader.GetFieldValue<DateTimeOffset>(4));
    }

    private async Task<IntegrationOutboxMessage?> ClaimOutboxMessageAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RecoverExpiredDispatchLeaseAsync(connection, transaction, eventId, now, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update integration_outbox
            set status = 'dispatching', attempt_count = attempt_count + 1, locked_at = @now,
              last_attempt_at = @now, updated_at = @now, last_error = null
            where event_id = @event_id
              and status in ('queued', 'retry-scheduled')
              and available_at <= @now
            returning event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var claimed = ReadOutboxMessage(reader);
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return claimed;
        }

        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return await GetOutboxMessageAsync(connection, eventId, cancellationToken);
    }

    private static async Task RecoverExpiredDispatchLeaseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update integration_outbox
            set status = 'retry-scheduled', available_at = @now, locked_at = null,
              last_error = 'The prior local dispatch claim exceeded its five-minute lease.', updated_at = @now
            where event_id = @event_id and status = 'dispatching' and locked_at <= @lease_expired_at
            returning attempt_count;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease_expired_at", now.Subtract(DispatchLease));
        var attemptCount = await command.ExecuteScalarAsync(cancellationToken);
        if (attemptCount is int recoveredAttemptCount)
        {
            await WriteOutboxEventAsync(
                connection,
                transaction,
                eventId,
                "lease-recovered",
                "The prior local dispatch claim exceeded its five-minute lease.",
                "local-dispatch-lease-recovery",
                recoveredAttemptCount,
                now,
                cancellationToken);
        }
    }

    private async Task<IntegrationOutboxMessage> CompleteDispatchAsync(
        IntegrationOutboxMessage message,
        IntegrationTransportResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var quarantine = !result.Delivered && message.AttemptCount >= MaximumAutomaticAttempts;
        var status = result.Delivered ? "delivered" : quarantine ? "quarantined" : "retry-scheduled";
        var availableAt = result.Delivered || quarantine ? message.AvailableAt : now.Add(result.RetryAfter ?? TimeSpan.FromMinutes(1));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update integration_outbox
            set status = @status, available_at = @available_at, locked_at = null,
              delivered_at = @delivered_at, external_reference = @external_reference,
              last_error = @last_error, quarantined_at = @quarantined_at,
              quarantined_by = @quarantined_by, updated_at = @now
            where event_id = @event_id and status = 'dispatching' and attempt_count = @attempt_count
            returning event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("available_at", availableAt);
        command.Parameters.AddWithValue("delivered_at", result.Delivered ? now : DBNull.Value);
        command.Parameters.AddWithValue("external_reference", (object?)result.ExternalReference ?? DBNull.Value);
        command.Parameters.AddWithValue("last_error", (object?)result.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("quarantined_at", quarantine ? now : DBNull.Value);
        command.Parameters.AddWithValue("quarantined_by", quarantine ? "local-dispatch" : DBNull.Value);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("event_id", message.EventId);
        command.Parameters.AddWithValue("attempt_count", message.AttemptCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Integration outbox dispatch completion was lost.");
        }

        var completed = ReadOutboxMessage(reader);
        await reader.DisposeAsync();
        if (quarantine)
        {
            await WriteOutboxEventAsync(connection, transaction, message.EventId, "quarantined", result.Error ?? "Dispatch attempts exhausted.", "local-dispatch", completed.AttemptCount, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return completed;
    }

    private static async Task<IntegrationOutboxMessage?> GetOutboxMessageAsync(
        NpgsqlConnection connection,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, idempotency_key, event_type, aggregate_type, aggregate_id, destination, payload,
              status, attempt_count, available_at, locked_at, last_attempt_at, delivered_at,
              external_reference, last_error, quarantined_at, quarantined_by, recovery_count, created_at, updated_at
            from integration_outbox where event_id = @event_id;
            """;
        command.Parameters.AddWithValue("event_id", eventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOutboxMessage(reader) : null;
    }

    private static IntegrationOutboxMessage ReadOutboxMessage(NpgsqlDataReader reader)
    {
        var payload = JsonDocument.Parse(reader.GetString(6)).RootElement.Clone();
        return new IntegrationOutboxMessage(
            reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), payload, reader.GetString(7), reader.GetInt32(8),
            reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15), reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.GetInt32(17), reader.GetFieldValue<DateTimeOffset>(18), reader.GetFieldValue<DateTimeOffset>(19));
    }

    private static async Task WriteOutboxEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid eventId,
        string action,
        string reason,
        string actor,
        int attemptCount,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into integration_outbox_events (event_log_id, event_id, action, reason, actor, attempt_count, occurred_at)
            values (@event_log_id, @event_id, @action, @reason, @actor, @attempt_count, @occurred_at);
            """;
        command.Parameters.AddWithValue("event_log_id", Guid.NewGuid());
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("attempt_count", attemptCount);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateQueueRequest(IntegrationOutboxQueueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType)
            || string.IsNullOrWhiteSpace(request.AggregateType)
            || string.IsNullOrWhiteSpace(request.AggregateId)
            || string.IsNullOrWhiteSpace(request.Destination)
            || request.Payload.ValueKind is JsonValueKind.Undefined)
        {
            throw new ArgumentException("Event type, aggregate type, aggregate ID, destination, and payload are required.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
