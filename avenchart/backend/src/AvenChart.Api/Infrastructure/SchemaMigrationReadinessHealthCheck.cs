// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AvenChart.Api.Infrastructure;

public sealed class SchemaMigrationReadinessHealthCheck(SchemaMigrationState state) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await state.ValidateAsync(true, cancellationToken);
        return result.IsReady
            ? HealthCheckResult.Healthy(result.Description, result.Details)
            : HealthCheckResult.Unhealthy(result.Description, data: result.Details);
    }
}
