// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Configuration;

/// <summary>
/// Selects a staff identity boundary without coupling the API to a particular
/// commercial provider.  OIDC providers such as Auth0, Okta, Entra ID, and
/// Keycloak expose the same discovery/JWKS contract used by the OIDC mode.
/// </summary>
public sealed class IdentityProviderOptions
{
    public const string SectionName = "IdentityProvider";

    public string Mode { get; init; } = "local";
    public string ProviderId { get; init; } = "local-development";
    public string? Authority { get; init; }
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public string SubjectClaim { get; init; } = "sub";
    public bool RequireHttpsMetadata { get; init; } = true;
    public int ClockSkewSeconds { get; init; } = 60;
    /// <summary>
    /// Public OIDC client identifier used only by the modern browser BFF
    /// authorization-code flow. A confidential client secret is deliberately
    /// not accepted from the browser or configuration catalog.
    /// </summary>
    public string BrowserClientId { get; init; } = "avenchart-modern-ui";
    public string BrowserScopes { get; init; } = "openid profile";
    public IReadOnlyList<string> BrowserAllowedOrigins { get; init; } = [];
    public string? BrowserCallbackUrl { get; init; }
    public bool EnableBrowserBff { get; init; }
    public int BrowserStateLifetimeSeconds { get; init; } = 300;
    public string TestIssuer { get; init; } = "http://localhost:5001/api/test-idp";
    public string TestAudience { get; init; } = "avenchart-api";
    public int TestTokenLifetimeMinutes { get; init; } = 15;

    public bool IsLocal => string.Equals(Mode, "local", StringComparison.OrdinalIgnoreCase);
    public bool IsOidc => string.Equals(Mode, "oidc", StringComparison.OrdinalIgnoreCase);
    public bool IsTestOidc => string.Equals(Mode, "test-oidc", StringComparison.OrdinalIgnoreCase);
    public bool BrowserBffEnabled => !IsLocal
        && (IsTestOidc || EnableBrowserBff)
        && !string.IsNullOrWhiteSpace(BrowserClientId)
        && BrowserAllowedOrigins.Any(origin => !string.IsNullOrWhiteSpace(origin));
    public string EffectiveProviderId => IsTestOidc ? "test-oidc" : ProviderId;
    public string EffectiveIssuer => IsTestOidc ? TestIssuer : Issuer ?? Authority ?? string.Empty;
}
