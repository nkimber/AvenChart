using System.Text.Json;

namespace AvenChart.Api.Models;

public sealed record IntegrationOutboxQueueRequest(
    string EventType,
    string AggregateType,
    string AggregateId,
    string Destination,
    JsonElement Payload,
    string? IdempotencyKey);

public sealed record IntegrationOutboxMessage(
    Guid EventId,
    string? IdempotencyKey,
    string EventType,
    string AggregateType,
    string AggregateId,
    string Destination,
    JsonElement Payload,
    string Status,
    int AttemptCount,
    DateTimeOffset AvailableAt,
    DateTimeOffset? LockedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? DeliveredAt,
    string? ExternalReference,
    string? LastError,
    DateTimeOffset? QuarantinedAt,
    string? QuarantinedBy,
    int RecoveryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record IntegrationDispatchResponse(
    IntegrationOutboxMessage Message,
    bool Dispatched,
    string Outcome);

public sealed record IntegrationOutboxRecoveryRequest(
    string Reason,
    int ExpectedAttemptCount);

public sealed record IntegrationInboxReceiveRequest(
    string Source,
    string SourceMessageId,
    string MessageType,
    JsonElement Payload);

public sealed record IntegrationInboxReceipt(
    Guid InboxId,
    string Source,
    string SourceMessageId,
    string Status,
    bool Duplicate,
    DateTimeOffset ReceivedAt);

public sealed record IntegrationTransportResult(
    bool Delivered,
    string Outcome,
    string? ExternalReference,
    string? Error,
    TimeSpan? RetryAfter);
