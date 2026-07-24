using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

public interface IIntegrationTransport
{
    Task<IntegrationTransportResult> DeliverAsync(
        IntegrationOutboxMessage message,
        CancellationToken cancellationToken);
}
