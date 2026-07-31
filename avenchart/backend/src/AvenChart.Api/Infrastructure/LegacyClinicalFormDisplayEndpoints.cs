// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

public static class LegacyClinicalFormDisplayEndpoints
{
    public static RouteGroupBuilder MapLegacyClinicalFormDisplayEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/patients/{patientId}/legacy-snapshots", async (
                LegacyClinicalFormDisplayRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListAsync(
                        patientId,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientLegacyClinicalFormSnapshots");

        group.MapGet("/legacy-snapshots/{snapshotId:guid}", async (
                LegacyClinicalFormDisplayRepository repository,
                Guid snapshotId,
                CancellationToken cancellationToken) =>
            {
                var result = await repository.GetAsync(snapshotId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetLegacyClinicalFormSnapshot");

        group.MapGet(
                "/patients/{patientId}/legacy-migration-manifests/{stableKey}",
                async (
                    LegacyClinicalFormDisplayRepository repository,
                    AuthRepository authRepository,
                    IStaffIdentityAdapter identityAdapter,
                    HttpContext httpContext,
                    string patientId,
                    string stableKey,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var session = await identityAdapter.ResolveAsync(
                            httpContext,
                            cancellationToken);
                        if (!session.Authenticated)
                        {
                            return Results.Unauthorized();
                        }

                        var result = await repository.GetMigrationManifestAsync(
                            patientId,
                            stableKey,
                            cancellationToken);
                        if (result is null)
                        {
                            return Results.NotFound();
                        }

                        var allowedActions = new List<string>();
                        if (result.Manifest.Status == "draft"
                            && await authRepository.HasAccessPermissionAsync(
                                session.Username,
                                "admin",
                                "forms",
                                "write",
                                cancellationToken))
                        {
                            allowedActions.Add("review");
                        }

                        if (result.Manifest.Status == "in-review"
                            && !string.Equals(
                                result.Manifest.ReviewedBy,
                                session.Username,
                                StringComparison.OrdinalIgnoreCase)
                            && await authRepository.HasAccessPermissionAsync(
                                session.Username,
                                "admin",
                                "super",
                                "write",
                                cancellationToken))
                        {
                            allowedActions.Add("approve");
                            allowedActions.Add("reject");
                        }

                        return Results.Ok(result with
                        {
                            AllowedActions = allowedActions
                        });
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.BadRequest(new { error = exception.Message });
                    }
                })
            .WithName("GetPatientLegacyClinicalFormMigrationManifest");

        foreach (var action in new[] { "review", "approve", "reject" })
        {
            group.MapPost(
                    $"/legacy-migration-manifests/{{manifestId:guid}}/{action}",
                    async (
                        LegacyClinicalFormDisplayRepository repository,
                        AuthRepository authRepository,
                        IStaffIdentityAdapter identityAdapter,
                        HttpContext httpContext,
                        Guid manifestId,
                        LegacyClinicalFormMigrationManifestDecisionRequest request,
                        CancellationToken cancellationToken) =>
                    {
                        var session = await identityAdapter.ResolveAsync(
                            httpContext,
                            cancellationToken);
                        if (!session.Authenticated)
                        {
                            return Results.Unauthorized();
                        }

                        var permissionSection = "admin";
                        var permissionValue =
                            action == "review" ? "forms" : "super";
                        var authorized =
                            await authRepository.HasAccessPermissionAsync(
                                session.Username,
                                permissionSection,
                                permissionValue,
                                "write",
                                cancellationToken);
                        if (!authorized)
                        {
                            return Results.Json(
                                new
                                {
                                    error =
                                        $"User '{session.Username}' is not authorized to {action} migration manifests."
                                },
                                statusCode: StatusCodes.Status403Forbidden);
                        }

                        try
                        {
                            var result =
                                await repository
                                    .TransitionMigrationManifestAsync(
                                        manifestId,
                                        action,
                                        request,
                                        session.Username,
                                        cancellationToken);
                            return result is null
                                ? Results.NotFound()
                                : Results.Ok(result);
                        }
                        catch (
                            LegacyClinicalFormMigrationManifestConflictException
                            exception)
                        {
                            return Results.Conflict(new
                            {
                                error = exception.Message,
                                currentVersion = exception.CurrentVersion,
                                currentStatus = exception.CurrentStatus
                            });
                        }
                        catch (ArgumentException exception)
                        {
                            return Results.BadRequest(
                                new { error = exception.Message });
                        }
                    })
                .WithName(
                    $"TransitionLegacyClinicalFormMigrationManifest{char.ToUpperInvariant(action[0])}{action[1..]}");
        }

        group.MapDelete(
                "/legacy-migration-manifests/{manifestId:guid}/test-fixture",
                async (
                    LegacyClinicalFormDisplayRepository repository,
                    AuthRepository authRepository,
                    IStaffIdentityAdapter identityAdapter,
                    HttpContext httpContext,
                    Guid manifestId,
                    CancellationToken cancellationToken) =>
                {
                    var session = await identityAdapter.ResolveAsync(
                        httpContext,
                        cancellationToken);
                    if (!session.Authenticated)
                    {
                        return Results.Unauthorized();
                    }

                    var authorized =
                        await authRepository.HasAccessPermissionAsync(
                            session.Username,
                            "admin",
                            "super",
                            "write",
                            cancellationToken);
                    if (!authorized)
                    {
                        return Results.Json(
                            new
                            {
                                error =
                                    $"User '{session.Username}' is not authorized to reset migration manifest fixtures."
                            },
                            statusCode: StatusCodes.Status403Forbidden);
                    }

                    try
                    {
                        return await repository
                            .ResetMigrationManifestTestFixtureAsync(
                                manifestId,
                                cancellationToken)
                            ? Results.NoContent()
                            : Results.NotFound();
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.BadRequest(
                            new { error = exception.Message });
                    }
                })
            .WithName("ResetLegacyClinicalFormMigrationManifestTestFixture");

        return group;
    }
}
