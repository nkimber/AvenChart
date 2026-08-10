// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

public sealed class AzureOperationsOptions
{
    public const string SectionName = "AzureOperations";

    public bool Enabled { get; init; } = true;

    public bool AllowPlanExecution { get; init; } = true;

    public bool AllowDeploymentExecution { get; init; }

    public string AzureCliPath { get; init; } = "az";

    public string? RepositoryRoot { get; init; }

    public int CommandTimeoutMinutes { get; init; } = 30;

    public int MigrationTimeoutMinutes { get; init; } = 30;

    public int AccessGrantMinutes { get; init; } = 15;

    public int UnlockMaximumFailures { get; init; } = 5;

    public int UnlockFailureWindowMinutes { get; init; } = 15;

    public int UnlockLockoutMinutes { get; init; } = 15;

    public int AccessCodeHashIterations { get; init; } = 310_000;
}
