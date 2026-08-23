// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps managed-record intake, classification, history, and retention routes as one aggregate.
/// </summary>
public static class ManagedRecordEndpoints
{
    public static RouteGroupBuilder MapManagedRecordEndpoints(this WebApplication app)
    {
        var records = app.MapGroup("/api/records").WithTags("Records");
        RequireAccessPermission(records, "patients", "docs", "view");

        records.MapGet("/policy", (ManagedRecordRepository repository) =>
                Results.Ok(repository.GetPolicy()))
            .WithName("GetManagedRecordPolicy");

        records.MapGet("/", async (
                ManagedRecordRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListAsync(patientId, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetManagedRecordIntakes");

        records.MapPost("/", async (
                ManagedRecordRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                ManagedRecordCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.CreateAsync(request, session.Username, cancellationToken);
                    return result.IdempotentReplay
                        ? Results.Ok(result)
                        : Results.Created($"/api/records/{result.Intake.IntakeId}", result);
                }
                catch (ManagedRecordIdempotencyConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateManagedRecordIntake")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        records.MapPut("/{intakeId:guid}/classification", async (
                ManagedRecordRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid intakeId,
                ManagedRecordClassificationUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.UpdateClassificationAsync(intakeId, request, session.Username, cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ManagedRecordConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdateManagedRecordClassification")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        records.MapPost("/{intakeId:guid}/{action}", async (
                ManagedRecordRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid intakeId,
                string action,
                ManagedRecordActionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.ActAsync(intakeId, action, request, session.Username, cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ManagedRecordConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("ActOnManagedRecordIntake")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        records.MapGet("/{intakeId:guid}/history", async (
                ManagedRecordRepository repository,
                Guid intakeId,
                CancellationToken cancellationToken) =>
            {
                var result = await repository.GetHistoryAsync(intakeId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetManagedRecordHistory");

        records.MapDelete("/{intakeId:guid}/test-fixture", async (
                ManagedRecordRepository repository,
                Guid intakeId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return await repository.DeleteTestFixtureAsync(intakeId, cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("DeleteManagedRecordTestFixture")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        return records;
    }
}
