// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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

        if (string.Equals(message.Destination, "local://recoverable", StringComparison.OrdinalIgnoreCase)
            && message.RecoveryCount > 0)
        {
            return Task.FromResult(new IntegrationTransportResult(
                Delivered: true,
                Outcome: "delivered-after-recovery",
                ExternalReference: $"LOCAL-RECOVERED-{message.EventId:N}",
                Error: null,
                RetryAfter: null));
        }

        var error = message.Destination switch
        {
            "local://retry" => "Deterministic local transport requested one retry.",
            "local://recoverable" => "Deterministic local transport requires explicit quarantine recovery.",
            _ => "The local integration transport only accepts local://success, local://retry, local://recoverable, or local://failure destinations."
        };
        return Task.FromResult(new IntegrationTransportResult(
            Delivered: false,
            Outcome: "retry-scheduled",
            ExternalReference: null,
            Error: error,
            RetryAfter: TimeSpan.FromSeconds(1)));
    }
}
