// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

public sealed class ReportExecutionOptions
{
    public const string SectionName = "ReportExecution";

    public int PollIntervalMilliseconds { get; init; } = 250;

    public int EnqueueDelayMilliseconds { get; init; } = 500;

    public int HeartbeatIntervalMilliseconds { get; init; } = 1000;

    public int LeaseSeconds { get; init; } = 30;

    public int ExecutionTimeoutSeconds { get; init; } = 20;

    public int QueueExpirationMinutes { get; init; } = 15;

    public int MaxAttempts { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 1;
}
