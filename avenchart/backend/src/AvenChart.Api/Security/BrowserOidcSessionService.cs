// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AvenChart.Api.Security;

/// <summary>
/// Implements the browser-facing, authorization-code plus PKCE boundary for
/// OIDC. Tokens pass through this service only long enough to validate and
/// establish a short-lived server session; they are never stored in browser
/// storage, database rows, or application logs.
/// </summary>
public sealed class BrowserOidcSessionService(
    AuthRepository authRepository,
    PatientPortalRepository patientPortalRepository,
    TestIdentityProviderService testIdentityProvider,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<IdentityProviderOptions> options)
{
    public const string StartPath = "/api/auth/oidc/start";
    public const string CallbackPath = "/api/auth/oidc/callback";
    public const string StaffAudience = "staff";
    public const string PortalAudience = "portal";

    private const string CorrelationCookie = "AvenChart.Oidc.Correlation";
    private const string StaffSessionCookie = "AvenChart.Oidc.StaffSession";
    private const string PortalSessionCookie = "AvenChart.Oidc.PortalSession";
    private const string StaffCsrfCookie = "AvenChart.Oidc.StaffCsrf";
    private const string PortalCsrfCookie = "AvenChart.Oidc.PortalCsrf";
    private readonly IdentityProviderOptions _options = options.Value;
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("AvenChart.BrowserOidc.State.v1");
    private readonly IDataProtector _csrfProtector = dataProtectionProvider.CreateProtector("AvenChart.BrowserOidc.Csrf.v1");
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager =
        options.Value.IsOidc ? CreateConfigurationManager(options.Value) : null;

    public BrowserOidcConfigurationResponse GetConfiguration()
    {
        var enabled = _options.BrowserBffEnabled;
        return new BrowserOidcConfigurationResponse(
            _options.Mode,
            enabled,
            enabled ? null : _options.IsLocal
                ? "Local AvenChart credentials are active."
                : "Browser single sign-on is not configured for this deployment.",
            enabled ? [StaffAudience, PortalAudience] : [],
            StartPath,
            CallbackPath,
            _options.BrowserClientId,
            _options.BrowserScopes);
    }

    public async Task<string> StartAsync(
        HttpContext context,
        string audience,
        string returnUrl,
        CancellationToken cancellationToken)
    {
        EnsureBrowserSignInEnabled();
        var normalizedAudience = NormalizeAudience(audience);
        var returnUri = ValidateReturnUri(returnUrl);
        var metadata = await GetMetadataAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(metadata.AuthorizationEndpoint))
        {
            throw new BrowserOidcException("The configured identity provider does not advertise an authorization endpoint.");
        }

        var now = DateTimeOffset.UtcNow;
        var correlation = CreateRandomValue(32);
        var verifier = CreateRandomValue(48);
        var protectedState = ProtectState(new BrowserOidcState(
            correlation,
            verifier,
            normalizedAudience,
            returnUri.ToString(),
            now.AddSeconds(_options.BrowserStateLifetimeSeconds)));
        context.Response.Cookies.Append(
            CorrelationCookie,
            correlation,
            CreateCookieOptions(context, "/api/auth/oidc", now.AddSeconds(_options.BrowserStateLifetimeSeconds)));

        var authorizationParameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.BrowserClientId,
            ["redirect_uri"] = GetCallbackUrl(context),
            ["scope"] = _options.BrowserScopes,
            ["state"] = protectedState,
            ["code_challenge"] = CreateCodeChallenge(verifier),
            ["code_challenge_method"] = "S256",
        };
        return QueryHelpers.AddQueryString(metadata.AuthorizationEndpoint, authorizationParameters);
    }

    public async Task<string> CompleteAsync(
        HttpContext context,
        string? code,
        string? state,
        string? providerError,
        CancellationToken cancellationToken)
    {
        var oidcState = ReadState(state);
        context.Response.Cookies.Delete(CorrelationCookie, CreateCookieOptions(context, "/api/auth/oidc", DateTimeOffset.UnixEpoch));
        var suppliedCorrelation = context.Request.Cookies[CorrelationCookie];
        if (string.IsNullOrWhiteSpace(suppliedCorrelation)
            || !FixedTimeEquals(suppliedCorrelation, oidcState.Correlation))
        {
            throw new BrowserOidcException("Single sign-on correlation could not be verified. Start sign-in again.");
        }
        if (oidcState.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new BrowserOidcException("Single sign-on sign-in expired. Start again.");
        }
        if (!string.IsNullOrWhiteSpace(providerError))
        {
            return AddCallbackResult(oidcState.ReturnUrl, oidcState.Audience, false, "The identity provider did not complete sign-in.");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BrowserOidcException("The identity provider did not return an authorization code.");
        }

        var token = await ExchangeAuthorizationCodeAsync(code, oidcState.CodeVerifier, context, cancellationToken);
        var accessToken = token.AccessToken ?? throw new BrowserOidcException("The identity provider did not return a usable bearer token.");
        var identity = await ValidateTokenAsync(accessToken, cancellationToken);
        var subject = identity.Principal.FindFirst(_options.IsTestOidc ? "sub" : _options.SubjectClaim)?.Value
            ?? identity.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new BrowserOidcException("The identity provider token does not contain the configured subject claim.");
        }

        if (oidcState.Audience == StaffAudience)
        {
            var source = BrowserStaffSessionSource;
            var mapped = await authRepository.ResolveExternalPrincipalAsync(
                _options.EffectiveProviderId,
                subject,
                source,
                identity.ExpiresAt,
                cancellationToken);
            if (!mapped.Authenticated)
            {
                throw new BrowserOidcException("The external identity is not mapped to an active AvenChart staff account.");
            }
            var session = await authRepository.CreateBrowserOidcSessionAsync(
                mapped,
                identity.ExpiresAt,
                source,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            IssueSessionCookies(context, StaffAudience, session.SessionId!.Value, session.ExpiresAt!.Value);
        }
        else
        {
            var sessionId = await patientPortalRepository.ResolveExternalSessionAsync(
                _options.EffectiveProviderId,
                subject,
                identity.ExpiresAt,
                SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)),
                cancellationToken);
            if (sessionId is null)
            {
                throw new BrowserOidcException("The external identity is not mapped to an active patient portal account.");
            }
            var session = await patientPortalRepository.GetCurrentSessionAsync(sessionId.Value, cancellationToken);
            if (!session.Authenticated || session.ExpiresAt is null)
            {
                throw new BrowserOidcException("The patient portal session could not be established.");
            }
            IssueSessionCookies(context, PortalAudience, sessionId.Value, session.ExpiresAt.Value);
        }

        return AddCallbackResult(oidcState.ReturnUrl, oidcState.Audience, true, null);
    }

    public async Task<AuthSessionResponse?> ResolveBrowserStaffSessionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadSessionId(context, StaffAudience, out var sessionId)) return null;
        var session = await authRepository.GetBrowserOidcSessionAsync(sessionId, BrowserStaffSessionSource, cancellationToken);
        return session.Authenticated ? session : null;
    }

    public async Task<Guid?> ResolveBrowserPortalSessionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadSessionId(context, PortalAudience, out var sessionId)) return null;
        var session = await patientPortalRepository.GetCurrentSessionAsync(sessionId, cancellationToken);
        return session.Authenticated && string.Equals(session.SessionSource, BrowserPortalSessionSource, StringComparison.Ordinal)
            ? sessionId
            : null;
    }

    public bool IsBrowserSessionRequest(HttpContext context, string audience) =>
        _options.BrowserBffEnabled && context.Request.Cookies.ContainsKey(SessionCookieName(NormalizeAudience(audience)));

    public bool TryGetCsrfToken(HttpContext context, string audience, out string token)
    {
        token = string.Empty;
        if (!_options.BrowserBffEnabled || !TryReadSessionId(context, audience, out var sessionId)) return false;
        var protectedValue = context.Request.Cookies[CsrfCookieName(NormalizeAudience(audience))];
        if (string.IsNullOrWhiteSpace(protectedValue)) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<BrowserCsrfPayload>(_csrfProtector.Unprotect(protectedValue));
            if (payload is null || payload.SessionId != sessionId || payload.ExpiresAt <= DateTimeOffset.UtcNow) return false;
            token = payload.Token;
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public bool HasValidBrowserCsrf(HttpContext context)
    {
        var supplied = context.Request.Headers["X-AvenChart-CSRF"].ToString();
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        return (TryGetCsrfToken(context, StaffAudience, out var staffToken) && FixedTimeEquals(staffToken, supplied))
            || (TryGetCsrfToken(context, PortalAudience, out var portalToken) && FixedTimeEquals(portalToken, supplied));
    }

    public bool IsAllowedBrowserOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        return _options.BrowserAllowedOrigins.Any(allowed =>
            string.Equals(NormalizeOrigin(allowed), NormalizeOrigin(origin), StringComparison.OrdinalIgnoreCase));
    }

    public void ClearBrowserSessionCookies(HttpContext context, string audience)
    {
        var normalizedAudience = NormalizeAudience(audience);
        context.Response.Cookies.Delete(SessionCookieName(normalizedAudience), CreateCookieOptions(context, "/api", DateTimeOffset.UnixEpoch));
        context.Response.Cookies.Delete(CsrfCookieName(normalizedAudience), CreateCookieOptions(context, "/api", DateTimeOffset.UnixEpoch));
    }

    private string BrowserStaffSessionSource => $"oidc-browser:{_options.EffectiveProviderId.Trim().ToLowerInvariant()}";
    private string BrowserPortalSessionSource => $"oidc:{_options.EffectiveProviderId.Trim().ToLowerInvariant()}";

    private async Task<OidcProviderMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        if (_options.IsTestOidc)
        {
            var issuer = _options.TestIssuer.TrimEnd('/');
            return new OidcProviderMetadata($"{issuer}/authorize", $"{issuer}/token");
        }
        var authority = _options.IsTestOidc ? _options.TestIssuer : _options.Authority;
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new BrowserOidcException("The identity provider authority is not configured.");
        }
        var client = httpClientFactory.CreateClient("browser-oidc");
        using var response = await client.GetAsync($"{authority.TrimEnd('/')}/.well-known/openid-configuration", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BrowserOidcException("The identity provider discovery document could not be loaded.");
        }
        var metadata = await response.Content.ReadFromJsonAsync<OidcProviderMetadata>(cancellationToken: cancellationToken);
        return metadata is null || string.IsNullOrWhiteSpace(metadata.TokenEndpoint)
            ? throw new BrowserOidcException("The identity provider discovery document is incomplete.")
            : metadata;
    }

    private async Task<OidcTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string verifier,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (_options.IsTestOidc)
        {
            var issued = testIdentityProvider.ExchangeAuthorizationCode(code, _options.BrowserClientId, GetCallbackUrl(context), verifier);
            return issued is null
                ? throw new BrowserOidcException("The identity provider did not accept the authorization code.")
                : new OidcTokenResponse(issued.AccessToken, issued.TokenType);
        }
        var metadata = await GetMetadataAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, metadata.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = GetCallbackUrl(context),
                ["client_id"] = _options.BrowserClientId,
                ["code_verifier"] = verifier,
            })
        };
        using var response = await httpClientFactory.CreateClient("browser-oidc").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new BrowserOidcException("The identity provider did not accept the authorization code.");
        }
        var token = await response.Content.ReadFromJsonAsync<OidcTokenResponse>(cancellationToken: cancellationToken);
        return token is null || string.IsNullOrWhiteSpace(token.AccessToken)
            || !string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? throw new BrowserOidcException("The identity provider did not return a usable bearer token.")
            : token;
    }

    private async Task<ValidatedOidcIdentity> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            TokenValidationResult validation;
            if (_options.IsTestOidc)
            {
                validation = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
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
            }
            else
            {
                var metadata = await _configurationManager!.GetConfigurationAsync(cancellationToken);
                validation = await new JwtSecurityTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
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
            }
            if (!validation.IsValid || validation.ClaimsIdentity is null)
            {
                throw new BrowserOidcException("The identity provider token is invalid.");
            }
            var expires = validation.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Exp)?.Value
                ?? validation.ClaimsIdentity.FindFirst(ClaimTypes.Expiration)?.Value;
            if (!long.TryParse(expires, out var expirySeconds))
            {
                throw new BrowserOidcException("The identity provider token does not contain an expiry.");
            }
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new BrowserOidcException("The identity provider token is expired.");
            }
            return new ValidatedOidcIdentity(validation.ClaimsIdentity, expiresAt);
        }
        catch (BrowserOidcException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SecurityTokenException or InvalidOperationException or IOException)
        {
            throw new BrowserOidcException("The identity provider token is invalid.");
        }
    }

    private BrowserOidcState ReadState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) throw new BrowserOidcException("Single sign-on state is missing.");
        try
        {
            var unprotected = _stateProtector.Unprotect(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(state)));
            var value = JsonSerializer.Deserialize<BrowserOidcState>(unprotected);
            if (value is null || string.IsNullOrWhiteSpace(value.Correlation) || string.IsNullOrWhiteSpace(value.CodeVerifier))
            {
                throw new BrowserOidcException("Single sign-on state is invalid.");
            }
            _ = NormalizeAudience(value.Audience);
            _ = ValidateReturnUri(value.ReturnUrl);
            return value;
        }
        catch (BrowserOidcException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            throw new BrowserOidcException("Single sign-on state is invalid.");
        }
    }

    private string ProtectState(BrowserOidcState state) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(_stateProtector.Protect(JsonSerializer.Serialize(state))));

    private void IssueSessionCookies(HttpContext context, string audience, Guid sessionId, DateTimeOffset expiresAt)
    {
        var csrfToken = CreateRandomValue(32);
        var normalizedAudience = NormalizeAudience(audience);
        context.Response.Cookies.Append(
            SessionCookieName(normalizedAudience),
            sessionId.ToString("D"),
            CreateCookieOptions(context, "/api", expiresAt));
        context.Response.Cookies.Append(
            CsrfCookieName(normalizedAudience),
            _csrfProtector.Protect(JsonSerializer.Serialize(new BrowserCsrfPayload(sessionId, csrfToken, expiresAt))),
            CreateCookieOptions(context, "/api", expiresAt));
    }

    private bool TryReadSessionId(HttpContext context, string audience, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        return _options.BrowserBffEnabled
            && Guid.TryParse(context.Request.Cookies[SessionCookieName(NormalizeAudience(audience))], out sessionId);
    }

    private static string SessionCookieName(string audience) => audience == StaffAudience ? StaffSessionCookie : PortalSessionCookie;
    private static string CsrfCookieName(string audience) => audience == StaffAudience ? StaffCsrfCookie : PortalCsrfCookie;

    private CookieOptions CreateCookieOptions(HttpContext context, string path, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        Secure = context.Request.IsHttps,
        SameSite = context.Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
        Path = path,
        Expires = expiresAt,
    };

    private Uri ValidateReturnUri(string returnUrl)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            || !string.IsNullOrWhiteSpace(uri.UserInfo)
            || !_options.BrowserAllowedOrigins.Any(allowed => string.Equals(
                NormalizeOrigin(allowed),
                NormalizeOrigin(uri.GetLeftPart(UriPartial.Authority)),
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new BrowserOidcException("The requested single sign-on return URL is not allowed.");
        }
        return uri;
    }

    private string GetCallbackUrl(HttpContext context) =>
        string.IsNullOrWhiteSpace(_options.BrowserCallbackUrl)
            ? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{CallbackPath}"
            : _options.BrowserCallbackUrl;

    private void EnsureBrowserSignInEnabled()
    {
        if (!_options.BrowserBffEnabled)
        {
            throw new BrowserOidcException("Browser single sign-on is not configured for this deployment.");
        }
    }

    private static string NormalizeAudience(string audience) => audience.Trim().ToLowerInvariant() switch
    {
        StaffAudience => StaffAudience,
        PortalAudience => PortalAudience,
        _ => throw new BrowserOidcException("The requested single sign-on audience is not supported."),
    };

    private static string NormalizeOrigin(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority).TrimEnd('/')
            : string.Empty;
    }

    private static string CreateRandomValue(int bytes) => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));
    private static string CreateCodeChallenge(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string AddCallbackResult(string returnUrl, string audience, bool success, string? reason) =>
        QueryHelpers.AddQueryString(returnUrl, new Dictionary<string, string?>
        {
            ["oidc"] = success ? "complete" : "failed",
            ["audience"] = audience,
            ["reason"] = reason,
        });

    private static ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(IdentityProviderOptions options)
    {
        var authority = options.Authority!.TrimEnd('/');
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = options.RequireHttpsMetadata });
    }

    private sealed record BrowserOidcState(
        string Correlation,
        string CodeVerifier,
        string Audience,
        string ReturnUrl,
        DateTimeOffset ExpiresAt);

    private sealed record BrowserCsrfPayload(Guid SessionId, string Token, DateTimeOffset ExpiresAt);
    private sealed record OidcProviderMetadata(
        [property: JsonPropertyName("authorization_endpoint")] string? AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string? TokenEndpoint);
    private sealed record OidcTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType);
    private sealed record ValidatedOidcIdentity(ClaimsIdentity Principal, DateTimeOffset ExpiresAt);
}

public sealed class BrowserOidcException(string message) : Exception(message);
