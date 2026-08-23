// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using AvenChart.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps the staff authentication API, including local development sign-in and
/// browser OIDC session handling. The development test identity provider is
/// mapped separately by host composition so it cannot be mistaken for a
/// production authentication surface.
/// </summary>
public static class StaffAuthenticationEndpoints
{
    public static RouteGroupBuilder MapStaffAuthenticationEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapGet("/oidc/browser-configuration", (BrowserOidcSessionService browserOidcSessions) =>
                Results.Ok(browserOidcSessions.GetConfiguration()))
            .WithName("GetBrowserOidcConfiguration");

        auth.MapGet("/oidc/start", async (
                string audience,
                string returnUrl,
                BrowserOidcSessionService browserOidcSessions,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Redirect(await browserOidcSessions.StartAsync(httpContext, audience, returnUrl, cancellationToken));
                }
                catch (BrowserOidcException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("StartBrowserOidcSignIn");

        auth.MapGet("/oidc/callback", async (
                string? code,
                string? state,
                string? error,
                BrowserOidcSessionService browserOidcSessions,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Redirect(await browserOidcSessions.CompleteAsync(
                        httpContext,
                        code,
                        state,
                        error,
                        cancellationToken));
                }
                catch (BrowserOidcException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CompleteBrowserOidcSignIn");

        auth.MapPost("/login", async (
                AuthRepository repository,
                StaffAccessContextService accessContextService,
                IOptions<IdentityProviderOptions> identityProviderOptions,
                AuthLoginRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!identityProviderOptions.Value.IsLocal)
                {
                    return Results.NotFound();
                }

                var response = await repository.LoginAsync(
                    request,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString(),
                    cancellationToken);
                if (response.Authenticated)
                {
                    response = response with
                    {
                        AccessContext = await accessContextService.GetAvailableAsync(response.Username, cancellationToken)
                    };
                }
                return Results.Ok(response);
            })
            .WithName("Login");

        auth.MapGet("/session", async (
                AuthRepository repository,
                BrowserOidcSessionService browserOidcSessions,
                StaffAccessContextService accessContextService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await EndpointAccessPolicies.GetSessionFromHeaderAsync(repository, httpContext, cancellationToken);
                if (session.Authenticated)
                {
                    session = session with
                    {
                        AccessContext = await accessContextService.GetAvailableAsync(session.Username, cancellationToken)
                    };
                }
                if (session.Authenticated && browserOidcSessions.TryGetCsrfToken(
                        httpContext,
                        BrowserOidcSessionService.StaffAudience,
                        out var csrfToken))
                {
                    httpContext.Response.Headers["X-AvenChart-CSRF"] = csrfToken;
                }
                return Results.Ok(session);
            })
            .WithName("GetCurrentSession");

        auth.MapGet("/access-context", async (
                AuthRepository repository,
                StaffAccessContextService accessContextService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await EndpointAccessPolicies.GetSessionFromHeaderAsync(repository, httpContext, cancellationToken);
                return !session.Authenticated
                    ? Results.Json(session, statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(await accessContextService.GetAvailableAsync(session.Username, cancellationToken));
            })
            .WithName("GetStaffAccessContext");

        auth.MapPost("/logout", async (
                AuthRepository repository,
                BrowserOidcSessionService browserOidcSessions,
                AuthSessionRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var browserSession = await browserOidcSessions.ResolveBrowserStaffSessionAsync(httpContext, cancellationToken);
                var response = browserSession is not null
                    ? await repository.LogoutAsync(browserSession.SessionId!.Value, cancellationToken)
                    : await repository.LogoutAsync(request.SessionId, cancellationToken);
                if (browserSession is not null)
                {
                    browserOidcSessions.ClearBrowserSessionCookies(httpContext, BrowserOidcSessionService.StaffAudience);
                }
                return Results.Ok(response);
            })
            .WithName("Logout");

        auth.MapGet("/login-audit", async (
                AuthRepository repository,
                HttpContext httpContext,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var session = await EndpointAccessPolicies.GetSessionFromHeaderAsync(repository, httpContext, cancellationToken);
                if (!session.Authenticated)
                {
                    return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Ok(await repository.GetLoginAuditAsync(limit ?? 10, cancellationToken));
            })
            .WithName("GetLoginAudit")
            .AddEndpointFilter(EndpointAccessPolicies.AccessPermissionFilter("admin", "super", "view"));

        auth.MapGet("/activity-audit", async (
                AuthRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAuthenticationActivityAuditAsync(limit ?? 25, cancellationToken)))
            .WithName("GetAuthenticationActivityAudit")
            .AddEndpointFilter(EndpointAccessPolicies.AccessPermissionFilter("admin", "super", "view"));

        return auth;
    }
}
