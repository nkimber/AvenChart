// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Npgsql;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps governed external-laboratory source and integration inbox/outbox APIs,
/// including selected-facility grant validation and idempotent recovery flows.
/// </summary>
public static class IntegrationEndpoints
{
    public static RouteGroupBuilder MapIntegrationEndpoints(this WebApplication app)
    {
        var integrations = app.MapGroup("/api/integrations").WithTags("Integrations");
        RequireAccessPermission(integrations, "admin", "super", "write");

        integrations.MapGet("/laboratory-sources", async (
                ExternalLaboratorySourceRepository repository,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetSourcesAsync(cancellationToken)))
            .WithName("ListExternalLaboratorySources");

        integrations.MapPost("/laboratory-sources", async (
                ExternalLaboratorySourceCreateRequest request,
                ExternalLaboratorySourceRepository repository,
                AuthRepository authRepository,
                StaffAccessContextService accessContextService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    await EnsureExternalLaboratorySourceFacilityScopeAsync(session, request.FacilityIds, accessContextService, cancellationToken);
                    var source = await repository.CreateSourceAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/integrations/laboratory-sources/{source.SourceId}", source);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
                catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    return Results.Conflict(new { error = "An external laboratory source with that source ID already exists." });
                }
            })
            .WithName("CreateExternalLaboratorySource");

        integrations.MapPut("/laboratory-sources/{sourceId}/facilities", async (
                string sourceId,
                ExternalLaboratorySourceFacilityGrantUpdateRequest request,
                ExternalLaboratorySourceRepository repository,
                AuthRepository authRepository,
                StaffAccessContextService accessContextService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    await EnsureExternalLaboratorySourceFacilityScopeAsync(session, request.FacilityIds, accessContextService, cancellationToken);
                    var source = await repository.ReplaceFacilityGrantsAsync(sourceId, request, session.Username, cancellationToken);
                    return source is null ? Results.NotFound() : Results.Ok(source);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("ReplaceExternalLaboratorySourceFacilityGrants");

        integrations.MapPost("/laboratory-sources/{sourceId}/deactivate", async (
                string sourceId,
                ExternalLaboratorySourceDeactivateRequest request,
                ExternalLaboratorySourceRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var source = await repository.DeactivateSourceAsync(sourceId, request, session.Username, cancellationToken);
                    return source is null
                        ? Results.Conflict(new { error = "The external laboratory source does not exist or is already deactivated." })
                        : Results.Ok(source);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("DeactivateExternalLaboratorySource");

        integrations.MapGet("/outbox", async (
                IntegrationRepository repository,
                string? status,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetOutboxAsync(status, limit ?? 25, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["status"] = [exception.Message]
                    });
                }
            })
            .WithName("ListIntegrationOutbox");

        integrations.MapPost("/outbox", async (
                IntegrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                IntegrationOutboxQueueRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var message = await repository.QueueAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/integrations/outbox/{message.EventId}", message);
                }
                catch (IntegrationIdempotencyConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("QueueIntegrationOutbox");

        integrations.MapPost("/outbox/{eventId:guid}/dispatch", async (
                IntegrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid eventId,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var dispatch = await repository.DispatchAsync(eventId, session.Username, cancellationToken);
                return dispatch is null ? Results.NotFound() : Results.Ok(dispatch);
            })
            .WithName("DispatchIntegrationOutbox");

        integrations.MapGet("/outbox/{eventId:guid}/history", async (
                IntegrationRepository repository,
                Guid eventId,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetOutboxHistoryAsync(eventId, cancellationToken)))
            .WithName("GetIntegrationOutboxHistory");

        integrations.MapPost("/outbox/{eventId:guid}/requeue", async (
                IntegrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid eventId,
                IntegrationOutboxRecoveryRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    return Results.Ok(await repository.RequeueQuarantinedAsync(eventId, request, session.Username, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .WithName("RequeueQuarantinedIntegrationOutbox");

        integrations.MapPost("/inbox", async (
                IntegrationRepository repository,
                IntegrationInboxReceiveRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var receipt = await repository.ReceiveAsync(request, cancellationToken);
                    return receipt.Duplicate
                        ? Results.Ok(receipt)
                        : Results.Created($"/api/integrations/inbox/{receipt.InboxId}", receipt);
                }
                catch (IntegrationIdempotencyConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("ReceiveIntegrationInbox");

        integrations.MapGet("/inbox", async (IntegrationRepository repository, string? status, int? limit, CancellationToken token) =>
        {
            try { return Results.Ok(await repository.GetInboxAsync(status, limit ?? 25, token)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [exception.Message] }); }
        }).WithName("ListIntegrationInbox");

        integrations.MapGet("/inbox/{inboxId:guid}/history", async (Guid inboxId, IntegrationRepository repository, CancellationToken token) =>
            Results.Ok(await repository.GetInboxHistoryAsync(inboxId, token))).WithName("GetIntegrationInboxHistory");

        foreach (var action in new[] { "reconcile", "reject" })
            integrations.MapPost($"/inbox/{{inboxId:guid}}/{action}", async (Guid inboxId, IntegrationInboxDecisionRequest request, IntegrationRepository repository, AuthRepository authRepository, HttpContext context, CancellationToken token) =>
            {
                try { var session = await GetSessionFromHeaderAsync(authRepository, context, token); return Results.Ok(await repository.DecideInboxAsync(inboxId, action, request, session.Username, token)); }
                catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [exception.Message] }); }
                catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
            }).WithName($"{action}IntegrationInbox");


        return integrations;
    }

    private static async Task EnsureExternalLaboratorySourceFacilityScopeAsync(
        AuthSessionResponse session,
        IReadOnlyList<int>? requestedFacilityIds,
        StaffAccessContextService accessContextService,
        CancellationToken cancellationToken)
    {
        var requested = (requestedFacilityIds ?? []).Distinct().OrderBy(id => id).ToArray();
        if (requested.Length == 0 || requested.Any(id => id <= 0))
        {
            throw new ArgumentException("At least one valid facility grant is required for an external laboratory source.");
        }
        var available = await accessContextService.GetAvailableAsync(session.Username, cancellationToken);
        var permitted = available.Facilities.Select(facility => facility.FacilityId).ToHashSet();
        if (requested.Any(id => !permitted.Contains(id)))
        {
            throw new ArgumentException("A laboratory source may be granted only to facilities available to the authenticated administrator.");
        }
    }

}
