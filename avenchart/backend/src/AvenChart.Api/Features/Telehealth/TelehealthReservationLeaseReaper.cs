// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthReservationLeaseReaper(
    IServiceScopeFactory scopeFactory,
    IOptions<TelehealthOptions> options,
    ILogger<TelehealthReservationLeaseReaper> logger) : BackgroundService
{
    private static readonly TimeSpan ReapInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<TelehealthRepository>();
                await repository.ExpireLeasedReservationsAsync(
                    options.Value.PracticeId,
                    options.Value.FacilityId,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Synthetic telehealth reservation lease reconciliation failed.");
            }

            await Task.Delay(ReapInterval, stoppingToken);
        }
    }
}
