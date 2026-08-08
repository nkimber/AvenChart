// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace AvenChart.Api.Infrastructure;

public sealed class PostgresReadinessHealthCheck(
    NpgsqlDataSource dataSource,
    ILogger<PostgresReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL connection is ready.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PostgreSQL readiness check failed.");
            return HealthCheckResult.Unhealthy("PostgreSQL connection is unavailable.");
        }
    }
}
