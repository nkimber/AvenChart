// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;

namespace AvenChart.Api.Workflows;

public sealed class ReportExecutionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReportExecutionOptions> options,
    ILogger<ReportExecutionWorker> logger,
    TimeProvider timeProvider)
    : BackgroundService
{
    private readonly string workerId =
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = new ReportWorkerSchedule(options.Value);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            var failed = false;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider
                    .GetRequiredService<ReportExecutionQueueRepository>();
                var maintain = schedule.MaintenanceDue(timeProvider.GetUtcNow());
                processed = await queue.ProcessNextAsync(workerId, stoppingToken, maintain);
                schedule.Succeeded(timeProvider.GetUtcNow(), maintain);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                failed = true;
                logger.LogError(
                    exception,
                    "Governed report worker {WorkerId} iteration failed.",
                    workerId);
            }

            if (!processed)
            {
                await Task.Delay(schedule.NextDelay(failed), timeProvider, stoppingToken);
            }
        }
    }
}

public sealed class ReportWorkerSchedule(ReportExecutionOptions options)
{
    private DateTimeOffset nextMaintenance = DateTimeOffset.MinValue;
    private int consecutiveFailures;

    public bool MaintenanceDue(DateTimeOffset now) => now >= nextMaintenance;

    public void Succeeded(DateTimeOffset now, bool performedMaintenance)
    {
        consecutiveFailures = 0;
        if (performedMaintenance)
            nextMaintenance = now.AddSeconds(options.MaintenanceIntervalSeconds);
    }

    public TimeSpan NextDelay(bool failed)
    {
        if (!failed) return TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds);
        consecutiveFailures = Math.Min(consecutiveFailures + 1, 20);
        return TimeSpan.FromMilliseconds(Math.Min(
            options.FailureBackoffMaximumSeconds * 1000d,
            options.PollIntervalMilliseconds * Math.Pow(2, consecutiveFailures - 1)));
    }
}
