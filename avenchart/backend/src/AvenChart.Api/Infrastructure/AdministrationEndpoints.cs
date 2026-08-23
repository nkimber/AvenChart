// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Npgsql;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps administration, access-context, catalog, and governed configuration routes as one aggregate.
/// </summary>
public static class AdministrationEndpoints
{
    public static RouteGroupBuilder MapAdministrationEndpoints(this WebApplication app)
    {
        var administration = app.MapGroup("/api/administration").WithTags("Administration");
        RequireAccessPermission(administration, "admin", "acl", "write");
        administration.MapAzureOperationsEndpoints();

        administration.MapGet("/access-context-grants/{username}", async (
                string username,
                StaffAccessContextService accessContextService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await accessContextService.GetPrincipalGrantAsync(username, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.NotFound(new { error = exception.Message });
                }
            })
            .WithName("GetStaffAccessContextGrant");

        administration.MapPut("/access-context-grants/{username}", async (
                string username,
                AuthAccessContextGrantUpdateRequest request,
                StaffAccessContextService accessContextService,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    return Results.Ok(await accessContextService.UpdatePrincipalGrantAsync(
                        username,
                        request,
                        session.Username,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["accessContextGrant"] = [exception.Message]
                    });
                }
            })
            .WithName("UpdateStaffAccessContextGrant");

        administration.MapGet("/external-identity-mappings", async (
                string? providerId,
                ExternalIdentityMappingRepository repository,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetMappingsAsync(providerId, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["providerId"] = [exception.Message]
                    });
                }
            })
            .WithName("ListExternalIdentityMappings");

        administration.MapPost("/external-identity-mappings", async (
                ExternalIdentityMappingCreateRequest request,
                ExternalIdentityMappingRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mapping = await repository.CreateAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/administration/external-identity-mappings/{mapping.MappingId}", mapping);
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
                    return Results.Conflict(new { error = "An active mapping already exists for this provider subject or local account." });
                }
            })
            .WithName("CreateExternalIdentityMapping");

        administration.MapPost("/external-identity-mappings/{mappingId:guid}/deactivate", async (
                Guid mappingId,
                ExternalIdentityMappingDeactivateRequest request,
                ExternalIdentityMappingRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mapping = await repository.DeactivateAsync(mappingId, request, session.Username, cancellationToken);
                    return mapping is null
                        ? Results.Conflict(new { error = "The mapping does not exist or is already deactivated." })
                        : Results.Ok(mapping);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("DeactivateExternalIdentityMapping");

        administration.MapGet("/patient-portal-external-identity-mappings", async (
                string? providerId,
                PatientPortalExternalIdentityMappingRepository repository,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetMappingsAsync(providerId, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["providerId"] = [exception.Message]
                    });
                }
            })
            .WithName("ListPatientPortalExternalIdentityMappings");

        administration.MapPost("/patient-portal-external-identity-mappings", async (
                PatientPortalExternalIdentityMappingCreateRequest request,
                PatientPortalExternalIdentityMappingRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mapping = await repository.CreateAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/administration/patient-portal-external-identity-mappings/{mapping.MappingId}", mapping);
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
                    return Results.Conflict(new { error = "An active portal mapping already exists for this provider subject or patient." });
                }
            })
            .WithName("CreatePatientPortalExternalIdentityMapping");

        administration.MapPost("/patient-portal-external-identity-mappings/{mappingId:guid}/deactivate", async (
                Guid mappingId,
                PatientPortalExternalIdentityMappingDeactivateRequest request,
                PatientPortalExternalIdentityMappingRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mapping = await repository.DeactivateAsync(mappingId, request, session.Username, cancellationToken);
                    return mapping is null
                        ? Results.Conflict(new { error = "The portal mapping does not exist or is already deactivated." })
                        : Results.Ok(mapping);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("DeactivatePatientPortalExternalIdentityMapping");

        return administration;
    }
}
