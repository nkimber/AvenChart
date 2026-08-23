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

    public int MaximumPoolSize { get; init; } = 100;

    public int KeepAliveSeconds { get; init; } = 30;
}
