using Microsoft.Extensions.Options;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;

namespace AvenChart.Api.Workflows;

public sealed class ReportExecutionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExecutionOptions> options,
    ILogger<ReportExecutionWorker> logger)
    : BackgroundService
{
    private readonly string workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromMilliseconds(
            options.Value.PollIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider
                    .GetRequiredService<ReportExecutionQueueRepository>();
                processed = await queue.ProcessNextAsync(workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Governed report worker {WorkerId} iteration failed.",
                    workerId);
            }

            if (!processed)
            {
                await Task.Delay(idleDelay, stoppingToken);
            }
        }
    }
}
