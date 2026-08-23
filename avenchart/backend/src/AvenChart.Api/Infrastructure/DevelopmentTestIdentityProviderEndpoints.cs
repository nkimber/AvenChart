// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps the development-only first-party OIDC test identity-provider endpoints.
/// </summary>
public static class DevelopmentTestIdentityProviderEndpoints
{
    public static void MapDevelopmentTestIdentityProviderEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            var testIdentityProvider = app.MapGroup("/api/test-idp").WithTags("Development Test Identity Provider");
            testIdentityProvider.MapGet("/.well-known/openid-configuration", (
                    IOptions<IdentityProviderOptions> options,
                    HttpContext httpContext) =>
                {
                    var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/api/test-idp";
                    return Results.Ok(new
                    {
                        issuer = options.Value.TestIssuer,
                        authorization_endpoint = $"{baseUrl}/authorize",
                        token_endpoint = $"{baseUrl}/token",
                        jwks_uri = $"{baseUrl}/jwks",
                        response_types_supported = new[] { "code" },
                        grant_types_supported = new[] { "authorization_code" },
                        code_challenge_methods_supported = new[] { "S256" },
                        subject_types_supported = new[] { "public" },
                        id_token_signing_alg_values_supported = new[] { "RS256" },
                    });
                })
                .WithName("GetDevelopmentTestIdentityProviderConfiguration");
            testIdentityProvider.MapGet("/jwks", (TestIdentityProviderService provider) => Results.Ok(provider.GetJwks()))
                .WithName("GetDevelopmentTestIdentityProviderJwks");
            testIdentityProvider.MapGet("/authorize", (
                    string? client_id,
                    string? redirect_uri,
                    string? state,
                    string? code_challenge,
                    string? code_challenge_method,
                    string? scope,
                    IOptions<IdentityProviderOptions> options,
                    HttpContext httpContext) =>
                {
                    if (!TryCreateDevelopmentTestOidcAuthorizationRequest(
                            client_id,
                            redirect_uri,
                            state,
                            code_challenge,
                            code_challenge_method,
                            scope,
                            options.Value,
                            httpContext,
                            out var authorizationRequest))
                    {
                        return Results.BadRequest(new { error = "The development test IdP authorization request is invalid." });
                    }
                    return Results.Content(BuildDevelopmentTestOidcAuthorizationPage(authorizationRequest), "text/html; charset=utf-8");
                })
                .WithName("AuthorizeDevelopmentTestIdentity");
            testIdentityProvider.MapPost("/authorize", async (
                    HttpRequest request,
                    AuthRepository repository,
                    TestIdentityProviderService provider,
                    IOptions<IdentityProviderOptions> options,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new { error = "The development test IdP authorization form is required." });
                    }
                    var form = await request.ReadFormAsync(cancellationToken);
                    if (!TryCreateDevelopmentTestOidcAuthorizationRequest(
                            form["client_id"],
                            form["redirect_uri"],
                            form["state"],
                            form["code_challenge"],
                            form["code_challenge_method"],
                            form["scope"],
                            options.Value,
                            httpContext,
                            out var authorizationRequest))
                    {
                        return Results.BadRequest(new { error = "The development test IdP authorization request is invalid." });
                    }
                    var login = await repository.LoginAsync(
                        new AuthLoginRequest(form["username"].ToString(), form["password"].ToString()),
                        httpContext.Connection.RemoteIpAddress?.ToString(),
                        httpContext.Request.Headers.UserAgent.ToString(),
                        cancellationToken);
                    if (!login.Authenticated)
                    {
                        return Results.Unauthorized();
                    }
                    var authorizationCode = provider.IssueAuthorizationCode(
                        login.Username,
                        login.DisplayName,
                        authorizationRequest.ClientId,
                        authorizationRequest.RedirectUri,
                        authorizationRequest.CodeChallenge);
                    return Results.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                        authorizationRequest.RedirectUri,
                        new Dictionary<string, string?>
                        {
                            ["code"] = authorizationCode,
                            ["state"] = authorizationRequest.State,
                        }));
                })
                .WithName("CompleteDevelopmentTestIdentityAuthorization");
            testIdentityProvider.MapPost("/token", async (
                    HttpRequest request,
                    AuthRepository repository,
                    TestIdentityProviderService provider,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    if (request.HasFormContentType)
                    {
                        var form = await request.ReadFormAsync(cancellationToken);
                        if (!string.Equals(form["grant_type"], "authorization_code", StringComparison.Ordinal))
                        {
                            return Results.BadRequest(new { error = "unsupported_grant_type" });
                        }
                        var issued = provider.ExchangeAuthorizationCode(
                            form["code"].ToString(),
                            form["client_id"].ToString(),
                            form["redirect_uri"].ToString(),
                            form["code_verifier"].ToString());
                        return issued is null
                            ? Results.BadRequest(new { error = "invalid_grant" })
                            : Results.Ok(new
                            {
                                access_token = issued.AccessToken,
                                token_type = issued.TokenType,
                                expires_in = issued.ExpiresIn,
                            });
                    }

                    var credentialRequest = await request.ReadFromJsonAsync<TestIdentityProviderTokenRequest>(cancellationToken);
                    if (credentialRequest is null)
                    {
                        return Results.BadRequest(new { error = "The development test identity token request is required." });
                    }
                    var login = await repository.LoginAsync(
                        new AuthLoginRequest(credentialRequest.Username, credentialRequest.Password),
                        httpContext.Connection.RemoteIpAddress?.ToString(),
                        httpContext.Request.Headers.UserAgent.ToString(),
                        cancellationToken);
                    return login.Authenticated
                        ? Results.Ok(provider.Issue(login.Username, login.DisplayName))
                        : Results.Unauthorized();
                })
                .WithName("IssueDevelopmentTestIdentityToken");
        }

    }

    static bool TryCreateDevelopmentTestOidcAuthorizationRequest(
        string? clientId,
        string? redirectUri,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? scope,
        IdentityProviderOptions options,
        HttpContext httpContext,
        out TestIdentityProviderAuthorizationRequest request)
    {
        request = default!;
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(redirectUri)
            || string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(codeChallenge)
            || !string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal)
            || !string.Equals(clientId, options.BrowserClientId, StringComparison.Ordinal)
            || codeChallenge.Length is < 43 or > 128
            || !codeChallenge.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            return false;
        }

        var expectedCallback = string.IsNullOrWhiteSpace(options.BrowserCallbackUrl)
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}{BrowserOidcSessionService.CallbackPath}"
            : options.BrowserCallbackUrl;
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var requestedCallback)
            || !Uri.TryCreate(expectedCallback, UriKind.Absolute, out var configuredCallback)
            || !string.Equals(requestedCallback.ToString(), configuredCallback.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        request = new TestIdentityProviderAuthorizationRequest(
            clientId!,
            redirectUri!,
            state!,
            codeChallenge!,
            codeChallengeMethod!,
            scope);
        return true;
    }

    static string BuildDevelopmentTestOidcAuthorizationPage(TestIdentityProviderAuthorizationRequest request)
    {
        static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        return $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AvenChart development test identity provider</title>
            </head>
            <body>
              <main>
                <h1>Development test identity provider</h1>
                <p>This non-production page issues a short-lived token only for the configured AvenChart test client.</p>
                <form method="post" action="/api/test-idp/authorize">
                  <input type="hidden" name="client_id" value="{Encode(request.ClientId)}">
                  <input type="hidden" name="redirect_uri" value="{Encode(request.RedirectUri)}">
                  <input type="hidden" name="state" value="{Encode(request.State)}">
                  <input type="hidden" name="code_challenge" value="{Encode(request.CodeChallenge)}">
                  <input type="hidden" name="code_challenge_method" value="{Encode(request.CodeChallengeMethod)}">
                  <input type="hidden" name="scope" value="{Encode(request.Scope)}">
                  <p><label>Username <input name="username" autocomplete="username" required></label></p>
                  <p><label>Password <input name="password" type="password" autocomplete="current-password" required></label></p>
                  <button type="submit">Continue</button>
                </form>
              </main>
            </body>
            </html>
            """;
    }
}
