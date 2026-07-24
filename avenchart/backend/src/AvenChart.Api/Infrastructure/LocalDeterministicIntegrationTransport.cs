using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

public sealed class LocalDeterministicIntegrationTransport(
    ILogger<LocalDeterministicIntegrationTransport> logger) : IIntegrationTransport
{
    public Task<IntegrationTransportResult> DeliverAsync(
        IntegrationOutboxMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Dispatching integration event {EventId} to local destination {Destination} on attempt {AttemptCount}",
            message.EventId,
            message.Destination,
            message.AttemptCount);

        if (string.Equals(message.Destination, "local://success", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new IntegrationTransportResult(
                Delivered: true,
                Outcome: "delivered",
                ExternalReference: $"LOCAL-{message.EventId:N}",
                Error: null,
                RetryAfter: null));
        }

        if (string.Equals(message.Destination, "local://retry", StringComparison.OrdinalIgnoreCase)
            && message.AttemptCount > 1)
        {
            return Task.FromResult(new IntegrationTransportResult(
                Delivered: true,
                Outcome: "delivered-after-retry",
                ExternalReference: $"LOCAL-RETRY-{message.EventId:N}",
                Error: null,
                RetryAfter: null));
        }

        var error = string.Equals(message.Destination, "local://retry", StringComparison.OrdinalIgnoreCase)
            ? "Deterministic local transport requested one retry."
            : "The local integration transport only accepts local://success, local://retry, or local://failure destinations.";
        return Task.FromResult(new IntegrationTransportResult(
            Delivered: false,
            Outcome: "retry-scheduled",
            ExternalReference: null,
            Error: error,
            RetryAfter: TimeSpan.FromSeconds(1)));
    }
}
