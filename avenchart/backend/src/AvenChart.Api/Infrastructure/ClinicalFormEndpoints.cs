// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps clinical form definition, rendering, instance, transition, and export routes as one aggregate.
/// </summary>
public static class ClinicalFormEndpoints
{
    public static RouteGroupBuilder MapClinicalFormEndpoints(this WebApplication app)
    {
        var formEngine = app.MapGroup("/api/form-engine").WithTags("Clinical Form Engine");
        RequireAccessPermission(formEngine, "patients", "demo", "view");
        formEngine.MapLegacyClinicalFormDisplayEndpoints();
        formEngine.MapClinicalFormOptionListEndpoints();

        formEngine.MapGet("/policy", (ClinicalFormRepository repository) =>
                Results.Ok(repository.GetPolicy()))
            .WithName("GetClinicalFormPolicy");

        formEngine.MapPost("/preview", (
                ClinicalFormRepository repository,
                ClinicalFormPreviewRequest request) =>
            {
                try
                {
                    return Results.Ok(repository.Preview(request));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("PreviewClinicalFormDefinition")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        formEngine.MapGet("/catalog", async (
                ClinicalFormRepository repository,
                string? search,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListCatalogAsync(
                        search,
                        page ?? 1,
                        pageSize ?? 20,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetClinicalFormCatalog");

        formEngine.MapGet("/definitions", async (
                ClinicalFormRepository repository,
                string? search,
                string? status,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListDefinitionsAsync(
                        search,
                        status,
                        page ?? 1,
                        pageSize ?? 20,
                        catalogOnly: false,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetClinicalFormDefinitions")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        formEngine.MapPost("/definitions", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                ClinicalFormDefinitionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var created = await repository.CreateDefinitionAsync(
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/form-engine/definitions/{created.Definition.DefinitionId}",
                        created);
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateClinicalFormDefinition")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        formEngine.MapGet("/definitions/{definitionId:guid}", async (
                ClinicalFormRepository repository,
                Guid definitionId,
                CancellationToken cancellationToken) =>
            {
                var result = await repository.GetDefinitionAsync(
                    definitionId,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetClinicalFormDefinition")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        formEngine.MapPost("/definitions/{definitionId:guid}/revisions", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                ClinicalFormRevisionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var created = await repository.CreateRevisionAsync(
                        definitionId,
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/form-engine/definitions/{definitionId}",
                        created);
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateClinicalFormRevision")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        foreach (var action in new[]
                 {
                     "review",
                     "approve",
                     "reject",
                     "activate",
                     "suspend",
                     "retire"
                 })
        {
            formEngine.MapPost(
                    $"/definitions/{{definitionId:guid}}/{action}",
                    async (
                        ClinicalFormRepository repository,
                        AuthRepository authRepository,
                        HttpContext httpContext,
                        Guid definitionId,
                        ClinicalFormDefinitionTransitionRequest request,
                        CancellationToken cancellationToken) =>
                    {
                        try
                        {
                            var session = await GetSessionFromHeaderAsync(
                                authRepository,
                                httpContext,
                                cancellationToken);
                            return Results.Ok(await repository.TransitionDefinitionAsync(
                                definitionId,
                                action,
                                request,
                                session.Username,
                                cancellationToken));
                        }
                        catch (ClinicalFormConflictException exception)
                        {
                            return Results.Conflict(new
                            {
                                error = exception.Message,
                                currentVersion = exception.CurrentVersion,
                                currentState = exception.CurrentState
                            });
                        }
                        catch (ArgumentException exception)
                        {
                            return Results.BadRequest(new { error = exception.Message });
                        }
                    })
                .WithName($"TransitionClinicalFormDefinition{action}")
                .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));
        }

        formEngine.MapDelete("/definitions/{definitionId:guid}/test-fixture", async (
                ClinicalFormRepository repository,
                Guid definitionId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return await repository.DeleteTestFixtureAsync(
                        definitionId,
                        cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("DeleteClinicalFormTestFixture")
            .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

        formEngine.MapGet("/patients/{patientId}/instances", async (
                ClinicalFormRepository repository,
                string patientId,
                int? encounterId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListInstancesAsync(
                        patientId,
                        encounterId,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientClinicalFormInstances");

        formEngine.MapPost("/patients/{patientId}/instances", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string patientId,
                ClinicalFormInstanceCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var created = await repository.CreateInstanceAsync(
                        patientId,
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/form-engine/instances/{created.Instance.InstanceId}",
                        created);
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreatePatientClinicalFormInstance")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        formEngine.MapGet("/instances/{instanceId:guid}", async (
                ClinicalFormRepository repository,
                Guid instanceId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetInstanceAsync(
                        instanceId,
                        cancellationToken));
                }
                catch (ArgumentException)
                {
                    return Results.NotFound();
                }
            })
            .WithName("GetClinicalFormInstance");

        formEngine.MapPut("/instances/{instanceId:guid}", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid instanceId,
                ClinicalFormInstanceUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.UpdateInstanceAsync(
                        instanceId,
                        request,
                        session.Username,
                        cancellationToken));
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdateClinicalFormInstance")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        foreach (var action in new[] { "finalize", "sign", "cosign" })
        {
            formEngine.MapPost(
                    $"/instances/{{instanceId:guid}}/{action}",
                    async (
                        ClinicalFormRepository repository,
                        AuthRepository authRepository,
                        HttpContext httpContext,
                        Guid instanceId,
                        ClinicalFormInstanceTransitionRequest request,
                        CancellationToken cancellationToken) =>
                    {
                        try
                        {
                            var session = await GetSessionFromHeaderAsync(
                                authRepository,
                                httpContext,
                                cancellationToken);
                            var result = action switch
                            {
                                "finalize" => await repository.FinalizeInstanceAsync(
                                    instanceId,
                                    request,
                                    session.Username,
                                    cancellationToken),
                                "sign" => await repository.SignInstanceAsync(
                                    instanceId,
                                    request,
                                    session.Username,
                                    cancellationToken),
                                _ => await repository.CosignInstanceAsync(
                                    instanceId,
                                    request,
                                    session.Username,
                                    cancellationToken)
                            };
                            return Results.Ok(result);
                        }
                        catch (ClinicalFormConflictException exception)
                        {
                            return Results.Conflict(new
                            {
                                error = exception.Message,
                                currentVersion = exception.CurrentVersion,
                                currentState = exception.CurrentState
                            });
                        }
                        catch (ArgumentException exception)
                        {
                            return Results.BadRequest(new { error = exception.Message });
                        }
                    })
                .WithName($"TransitionClinicalFormInstance{action}")
                .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
        }

        formEngine.MapPost("/instances/{instanceId:guid}/amend", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid instanceId,
                ClinicalFormInstanceAmendRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Created(
                        "/api/form-engine/instances",
                        await repository.AmendInstanceAsync(
                            instanceId,
                            request,
                            session.Username,
                            cancellationToken));
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("AmendClinicalFormInstance")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        formEngine.MapGet("/instances/{instanceId:guid}/render", async (
                ClinicalFormRepository repository,
                Guid instanceId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.RenderInstanceAsync(
                        instanceId,
                        cancellationToken));
                }
                catch (ArgumentException)
                {
                    return Results.NotFound();
                }
            })
            .WithName("RenderClinicalFormInstance");

        formEngine.MapGet("/instances/{instanceId:guid}/field-dictionary", async (
                ClinicalFormRepository repository,
                Guid instanceId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetInstanceFieldDictionaryAsync(
                        instanceId,
                        cancellationToken));
                }
                catch (ArgumentException)
                {
                    return Results.NotFound();
                }
            })
            .WithName("GetClinicalFormInstanceFieldDictionary");

        formEngine.MapGet("/instances/{instanceId:guid}/structured-export", async (
                ClinicalFormRepository repository,
                Guid instanceId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ExportInstanceStructuredAsync(
                        instanceId,
                        cancellationToken));
                }
                catch (ArgumentException)
                {
                    return Results.NotFound();
                }
            })
            .WithName("ExportClinicalFormInstanceStructured");

        formEngine.MapGet("/instances/{instanceId:guid}/export", async (
                ClinicalFormRepository repository,
                Guid instanceId,
                string? locale,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Content(
                        await repository.ExportInstanceHtmlAsync(
                            instanceId,
                            locale,
                            cancellationToken),
                        "text/html",
                        Encoding.UTF8);
                }
                catch (ArgumentException)
                {
                    return Results.NotFound();
                }
            })
            .WithName("ExportClinicalFormInstance");

        return formEngine;
    }
}
