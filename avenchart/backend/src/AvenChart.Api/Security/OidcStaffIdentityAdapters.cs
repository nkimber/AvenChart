// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AvenChart.Api.Security;

/// <summary>
/// Vendor-neutral OpenID Connect bearer-token adapter.  Discovery and JWKS
/// validation let a selected standards-conformant provider (for example Auth0,
/// Okta, Entra ID, or Keycloak) supply identity without changing API code.
/// </summary>
public sealed class OidcStaffIdentityAdapter(
    AuthRepository authRepository,
    IOptions<IdentityProviderOptions> options) : IStaffIdentityAdapter
{
    public const string Id = "oidc-discovery-jwks";
    private readonly IdentityProviderOptions _options = options.Value;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager = CreateConfigurationManager(options.Value);

    public string AdapterId => Id;

    public async Task<AuthSessionResponse> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var token = OidcIdentityAdapterHelpers.ReadBearerToken(httpContext);
        if (token is null) return OidcIdentityAdapterHelpers.MissingSession("A bearer token is required.");
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
            if (!validation.IsValid || validation.ClaimsIdentity is null) return OidcIdentityAdapterHelpers.MissingSession("The bearer token is invalid.");
            return await OidcIdentityAdapterHelpers.ResolveValidatedIdentityAsync(
                authRepository,
                validation.ClaimsIdentity,
                _options.SubjectClaim,
                _options.ProviderId,
                $"oidc:{_options.ProviderId}",
                cancellationToken);
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or IOException)
        {
            return OidcIdentityAdapterHelpers.MissingSession("The bearer token is invalid.");
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

/// <summary>
/// Development-only OIDC adapter backed by the first-party ephemeral test IdP.
/// It uses the same RS256 issuer/audience/lifetime validation path as external
/// OIDC, but never becomes a production identity source.
/// </summary>
public sealed class TestOidcStaffIdentityAdapter(
    AuthRepository authRepository,
    TestIdentityProviderService testIdentityProvider,
    IOptions<IdentityProviderOptions> options) : IStaffIdentityAdapter
{
    public const string Id = "first-party-test-oidc";
    private readonly IdentityProviderOptions _options = options.Value;

    public string AdapterId => Id;

    public async Task<AuthSessionResponse> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var token = OidcIdentityAdapterHelpers.ReadBearerToken(httpContext);
        if (token is null) return OidcIdentityAdapterHelpers.MissingSession("A bearer token is required.");
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
            if (!validation.IsValid || validation.ClaimsIdentity is null) return OidcIdentityAdapterHelpers.MissingSession("The bearer token is invalid.");
            return await OidcIdentityAdapterHelpers.ResolveValidatedIdentityAsync(
                authRepository,
                validation.ClaimsIdentity,
                "sub",
                "test-oidc",
                "test-oidc",
                cancellationToken);
        }
        catch (SecurityTokenException)
        {
            return OidcIdentityAdapterHelpers.MissingSession("The bearer token is invalid.");
        }
    }
}

/// <summary>Issues short-lived RS256 tokens and discovery metadata only for the development test IdP.</summary>
public sealed class TestIdentityProviderService
{
    private readonly IdentityProviderOptions _options;
    private readonly RSA _rsa;
    public RsaSecurityKey SigningKey { get; }

    public TestIdentityProviderService(IOptions<IdentityProviderOptions> options)
    {
        _options = options.Value;
        _rsa = RSA.Create(2048);
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = "avenchart-test-idp-rs256" };
    }

    public TestIdentityTokenResponse Issue(string username, string displayName)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.TestTokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.TestIssuer,
            Audience = _options.TestAudience,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Name, displayName),
            ]),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
        };
        var token = new JwtSecurityTokenHandler().CreateToken(descriptor);
        return new TestIdentityTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", Math.Max(1, (int)(expires - now).TotalSeconds), _options.TestIssuer, _options.TestAudience);
    }

    public object GetJwks()
    {
        var parameters = _rsa.ExportParameters(false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = SigningKey.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!),
                }
            }
        };
    }
}

public sealed record TestIdentityProviderTokenRequest(string Username, string Password);
public sealed record TestIdentityTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string Issuer, string Audience);

internal static class OidcIdentityAdapterHelpers
{
    public static string? ReadBearerToken(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && value.Length > 7 ? value[7..].Trim() : null;
    }

    public static async Task<AuthSessionResponse> ResolveValidatedIdentityAsync(
        AuthRepository repository,
        ClaimsIdentity identity,
        string subjectClaim,
        string providerId,
        string source,
        CancellationToken cancellationToken)
    {
        var subject = identity.FindFirst(subjectClaim)?.Value;
        var expires = identity.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || !long.TryParse(expires, out var seconds)) return MissingSession("The bearer token does not include a valid subject and expiry.");
        return await repository.ResolveExternalPrincipalAsync(providerId, subject, source, DateTimeOffset.FromUnixTimeSeconds(seconds), cancellationToken);
    }

    public static AuthSessionResponse MissingSession(string reason) => new(false, null, string.Empty, string.Empty, string.Empty, null, null, null, null, null, reason, "external-oidc");
}
