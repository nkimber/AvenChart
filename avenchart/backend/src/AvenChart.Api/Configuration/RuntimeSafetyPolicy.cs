// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace AvenChart.Api.Configuration;

public static class RuntimeSafetyPolicy
{
    public static bool HasValidTrustedProxyAddresses(IEnumerable<string>? addresses) =>
        (addresses ?? [])
        .All(address => !string.IsNullOrWhiteSpace(address) && IPAddress.TryParse(address, out _));

    public static IReadOnlyList<IPAddress> ParseTrustedProxyAddresses(IEnumerable<string>? addresses) =>
        (addresses ?? [])
        .Select(address => IPAddress.Parse(address))
        .Distinct()
        .ToArray();

    public static bool HasCompleteDataProtectionConfiguration(RuntimeSafetyOptions options)
    {
        var values = new[]
        {
            options.DataProtectionKeyRingPath,
            options.DataProtectionApplicationName,
            options.DataProtectionCertificatePath
        };
        var configuredCount = values.Count(value => !string.IsNullOrWhiteSpace(value));
        return configuredCount is 0 or 3;
    }

    public static bool HasProductionDataProtectionConfiguration(RuntimeSafetyOptions options) =>
        HasCompleteDataProtectionConfiguration(options)
        && !string.IsNullOrWhiteSpace(options.DataProtectionKeyRingPath)
        && !string.IsNullOrWhiteSpace(options.DataProtectionApplicationName)
        && !string.IsNullOrWhiteSpace(options.DataProtectionCertificatePath)
        && Path.IsPathFullyQualified(options.DataProtectionKeyRingPath)
        && Path.IsPathFullyQualified(options.DataProtectionCertificatePath);

    public static bool HasExplicitAllowedHosts(string? allowedHosts)
    {
        var hosts = (allowedHosts ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        return hosts.Length > 0
            && hosts.All(host => !host.Contains('*', StringComparison.Ordinal));
    }

    public static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions options,
        IReadOnlyCollection<IPAddress> trustedProxies,
        int forwardLimit)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = forwardLimit;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var trustedProxy in trustedProxies)
        {
            options.KnownProxies.Add(trustedProxy);
        }
    }
}
