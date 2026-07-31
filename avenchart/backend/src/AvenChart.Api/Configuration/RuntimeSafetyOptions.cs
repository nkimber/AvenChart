// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

public sealed class RuntimeSafetyOptions
{
    public const string SectionName = "RuntimeSafety";

    public bool RequireHttps { get; init; }

    public int RateLimitPermitLimit { get; init; } = 120;

    public int RateLimitWindowSeconds { get; init; } = 60;

    public int RateLimitQueueLimit { get; init; } = 0;
}
