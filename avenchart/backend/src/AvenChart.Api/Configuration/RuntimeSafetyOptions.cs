// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

public sealed class RuntimeSafetyOptions
{
    public const string SectionName = "RuntimeSafety";

    public bool RequireHttps { get; init; }

    /// <summary>
    /// Explicit addresses of reverse proxies permitted to supply client address
    /// and scheme headers. An empty list means the application does not trust
    /// forwarded headers from any peer.
    /// </summary>
    public string[] TrustedProxyAddresses { get; init; } = [];

    /// <summary>
    /// Maximum number of trusted reverse proxies between AvenChart and a client.
    /// </summary>
    public int ForwardedHeaderLimit { get; init; } = 1;

    /// <summary>
    /// Shared, durable directory for ASP.NET Core data-protection keys.
    /// Production requires this, an application name, and a certificate.
    /// </summary>
    public string? DataProtectionKeyRingPath { get; init; }

    /// <summary>
    /// Stable discriminator shared only by instances of the same deployment.
    /// </summary>
    public string? DataProtectionApplicationName { get; init; }

    /// <summary>
    /// Path to a PFX certificate used to encrypt persisted data-protection keys.
    /// The certificate password is supplied separately from configuration secrets.
    /// </summary>
    public string? DataProtectionCertificatePath { get; init; }

    public string? DataProtectionCertificatePassword { get; init; }

    public int RateLimitPermitLimit { get; init; } = 120;

    public int RateLimitWindowSeconds { get; init; } = 60;

    public int RateLimitQueueLimit { get; init; } = 0;
}
