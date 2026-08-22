// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AvenChart.Api.Security;

/// <summary>
/// Resolves a request to a server-owned portal-session identifier. Local
/// sessions and validated OIDC bearers therefore feed the existing patient
/// binding rather than allowing a portal route to accept a patient identifier.
/// </summary>
public interface IPatientPortalIdentityAdapter
{
    Task<Guid?> ResolveSessionIdAsync(HttpContext httpContext, CancellationToken cancellationToken);
}

public sealed class LocalPatientPortalIdentityAdapter : IPatientPortalIdentityAdapter
{
    public Task<Guid?> ResolveSessionIdAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var header = httpContext.Request.Headers[PatientPortalIdentityAdapterHelpers.SessionHeader].ToString();
        return Task.FromResult(Guid.TryParse(header, out var sessionId) ? (Guid?)sessionId : null);
    }
}

/// <summary>
/// Vendor-neutral OIDC portal adapter. It validates a bearer through discovery
/// and JWKS, then maps only its provider-scoped subject to a governed portal
/// patient identity; patient, role, and display claims in the bearer are never
/// used as AvenChart authorization data.
/// </summary>
public sealed class OidcPatientPortalIdentityAdapter(
    PatientPortalRepository repository,
    IOptions<IdentityProviderOptions> options) : IPatientPortalIdentityAdapter
{
    private readonly IdentityProviderOptions _options = options.Value;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager = CreateConfigurationManager(options.Value);

    public async Task<Guid?> ResolveSessionIdAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var token = OidcIdentityAdapterHelpers.ReadBearerToken(httpContext);
        if (token is null) return null;
        try
        {
            var metadata = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var validation = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = metadata.SigningKeys,
                ValidateIssuer = true,
                ValidIssuer = string.IsNullOrWhiteSpace(_options.Issuer) ? metadata.Issuer : _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            });
            return !validation.IsValid || validation.ClaimsIdentity is null
                ? null
                : await PatientPortalIdentityAdapterHelpers.ResolveValidatedSessionAsync(
                    repository,
                    validation.ClaimsIdentity,
                    _options.SubjectClaim,
                    _options.ProviderId,
                    token,
                    cancellationToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(IdentityProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority)) throw new InvalidOperationException("IdentityProvider:Authority is required when IdentityProvider:Mode is oidc.");
        var authority = options.Authority.TrimEnd('/');
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata });
    }
}

/// <summary>Development-only portal adapter backed by the first-party test IdP.</summary>
public sealed class TestOidcPatientPortalIdentityAdapter(
    PatientPortalRepository repository,
    TestIdentityProviderService testIdentityProvider,
    IOptions<IdentityProviderOptions> options) : IPatientPortalIdentityAdapter
{
    private readonly IdentityProviderOptions _options = options.Value;

    public async Task<Guid?> ResolveSessionIdAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var token = OidcIdentityAdapterHelpers.ReadBearerToken(httpContext);
        if (token is null) return null;
        try
        {
            var validation = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = testIdentityProvider.SigningKey,
                ValidateIssuer = true,
                ValidIssuer = _options.TestIssuer,
                ValidateAudience = true,
                ValidAudience = _options.TestAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            });
            return !validation.IsValid || validation.ClaimsIdentity is null
                ? null
                : await PatientPortalIdentityAdapterHelpers.ResolveValidatedSessionAsync(
                    repository,
                    validation.ClaimsIdentity,
                    "sub",
                    "test-oidc",
                    token,
                    cancellationToken);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}

internal static class PatientPortalIdentityAdapterHelpers
{
    public const string SessionHeader = "X-AvenChart-Patient-Portal-Session";

    public static async Task<Guid?> ResolveValidatedSessionAsync(
        PatientPortalRepository repository,
        System.Security.Claims.ClaimsIdentity identity,
        string subjectClaim,
        string providerId,
        string token,
        CancellationToken cancellationToken)
    {
        var subject = identity.FindFirst(subjectClaim)?.Value;
        var expires = identity.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || !long.TryParse(expires, out var seconds)) return null;
        return await repository.ResolveExternalSessionAsync(
            providerId,
            subject,
            DateTimeOffset.FromUnixTimeSeconds(seconds),
            SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            cancellationToken);
    }
}
