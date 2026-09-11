// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

/// <summary>
/// Explicit PostgreSQL connection and command boundaries for request and worker data access.
/// </summary>
public sealed class DatabaseConnectionOptions
{
    public const string SectionName = "DatabaseConnection";

    public int ConnectionTimeoutSeconds { get; init; } = 15;

    public int CommandTimeoutSeconds { get; init; } = 30;

    public int CancellationTimeoutMilliseconds { get; init; } = 2000;

    public int MinimumPoolSize { get; init; }

    public int MaximumPoolSize { get; init; } = 15;

    public int KeepAliveSeconds { get; init; } = 30;

    public string BuildConnectionString(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        // Both settings are ceilings. Never enlarge a deployment's explicit pool.
        builder.MaxPoolSize = Math.Min(builder.MaxPoolSize, MaximumPoolSize);
        builder.MinPoolSize = Math.Max(builder.MinPoolSize, MinimumPoolSize);
        if (builder.MinPoolSize > builder.MaxPoolSize)
            throw new InvalidOperationException("The minimum database pool size exceeds the effective maximum.");
        builder.Timeout = ConnectionTimeoutSeconds;
        builder.CommandTimeout = CommandTimeoutSeconds;
        builder.CancellationTimeout = CancellationTimeoutMilliseconds;
        builder.KeepAlive = KeepAliveSeconds;
        return builder.ConnectionString;
    }
}
