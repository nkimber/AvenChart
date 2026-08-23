// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps governed report discovery, execution, review, and download operations.
/// Report scope and retained execution evidence remain explicit at this boundary.
/// </summary>
public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports").WithTags("Reports");
        RequireAccessPermission(reports, "patients", "pat_rep", "view");

        reports.MapGet("/operational", async (
                ReportRepository repository,
                CancellationToken cancellationToken) =>
            {
                var report = await repository.GetOperationalReportsAsync(cancellationToken);
                return Results.Ok(report);
            })
            .WithName("GetOperationalReports");

        reports.MapPost("/controlled-inventory/as-of", async (
                ControlledInventoryReportRequest request,
                ReportRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var report = await repository.RunControlledInventoryReportAsync(request, session.Username, cancellationToken);
                    return Results.Created("/api/reports/controlled-inventory/as-of", report);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] });
                }
            })
            .WithName("RunControlledInventoryAsOfReport")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        reports.MapPost("/controlled-inventory/activity", async (
                ControlledInventoryActivityReportRequest request,
                ReportRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var report = await repository.RunControlledInventoryActivityReportAsync(request, session.Username, cancellationToken);
                    return Results.Created("/api/reports/controlled-inventory/activity", report);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] });
                }
            })
            .WithName("RunControlledInventoryActivityReport")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        reports.MapGet("/controlled-inventory/as-of/{runId:guid}/export", async (
            Guid runId,
            ReportRepository repository,
            AuthRepository authRepository,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var csv = await repository.ExportControlledInventoryRunCsvAsync(
                runId,
                session.Username,
                cancellationToken);
            return csv is null
                ? Results.NotFound()
                : Results.File(
                    Encoding.UTF8.GetBytes(csv),
                    "text/csv",
                    $"avenchart-controlled-inventory-{runId}.csv");
        }).WithName("ExportControlledInventoryAsOfRun")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        reports.MapGet("/controlled-inventory/activity/{runId:guid}/export", async (
            Guid runId,
            ReportRepository repository,
            AuthRepository authRepository,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var csv = await repository.ExportControlledInventoryActivityRunCsvAsync(
                runId,
                session.Username,
                cancellationToken);
            return csv is null
                ? Results.NotFound()
                : Results.File(
                    Encoding.UTF8.GetBytes(csv),
                    "text/csv",
                    $"avenchart-controlled-activity-{runId}.csv");
        }).WithName("ExportControlledInventoryActivityRun")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        reports.MapPost("/controlled-inventory/count-variance", async (ControlledCountVarianceReportRequest request, ReportRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/reports/controlled-inventory/count-variance", await repository.RunControlledCountVarianceReportAsync(request, session.Username, cancellationToken)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] }); }
        }).WithName("RunControlledCountVarianceReport").AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        reports.MapGet("/controlled-inventory/count-variance/{runId:guid}/export", async (
            Guid runId,
            ReportRepository repository,
            AuthRepository authRepository,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var csv = await repository.ExportControlledCountVarianceRunCsvAsync(
                runId,
                session.Username,
                cancellationToken);
            return csv is null
                ? Results.NotFound()
                : Results.File(
                    Encoding.UTF8.GetBytes(csv),
                    "text/csv",
                    $"avenchart-controlled-count-variance-{runId}.csv");
        }).WithName("ExportControlledCountVarianceRun")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        reports.MapGet("/operational/export", () =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Compatibility report export retired",
                    detail: "Use governed report execution to create a scoped, purpose-bound, auditable download."))
            .WithName("ExportOperationalReports");

        reports.MapGet("/families", (ReportRepository repository) => Results.Ok(repository.GetFamilies())).WithName("GetReportFamilies");
        reports.MapGet("/families/{family}/export", () =>
            Results.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Compatibility report export retired",
                detail: "Use governed report execution to create a scoped, purpose-bound, auditable download."))
            .WithName("ExportReportFamily");

        reports.MapGet("/definition-policy", (ReportDefinitionRepository repository) =>
                Results.Ok(repository.GetPolicy()))
            .WithName("GetReportDefinitionGovernancePolicy");

        reports.MapGet("/execution-policy", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(
                    authRepository,
                    httpContext,
                    cancellationToken);
                var operatorAccess = await authRepository.HasAccessPermissionAsync(
                    session.Username,
                    "patients",
                    "pat_rep",
                    "write",
                    cancellationToken);
                return Results.Ok(await repository.GetPolicyAsync(
                    session.Username,
                    operatorAccess,
                    cancellationToken));
            })
            .WithName("GetGovernedReportExecutionPolicy");

        reports.MapGet("/operations/runs", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string? search,
                string? status,
                string? family,
                string? requestedBy,
                bool? attentionOnly,
                DateOnly? from,
                DateOnly? to,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.GetOperationsAsync(
                        session.Username,
                        search,
                        status,
                        family,
                        requestedBy,
                        attentionOnly ?? false,
                        from,
                        to,
                        page ?? 1,
                        pageSize ?? 20,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetGovernedReportOperations")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        reports.MapGet("/operations/runs/{runId}", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string runId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var run = await repository.GetOperatorRunAsync(
                        runId,
                        session.Username,
                        cancellationToken);
                    return run is null ? Results.NotFound() : Results.Ok(run);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetGovernedReportOperationsRun")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        reports.MapGet("/catalog", async (
                ReportDefinitionRepository repository,
                string? search,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListAsync(
                        search,
                        status: "active",
                        page ?? 1,
                        pageSize ?? 20,
                        catalogOnly: true,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetGovernedReportCatalog");

        reports.MapGet("/definitions", async (
                ReportDefinitionRepository repository,
                string? search,
                string? status,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListAsync(
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
            .WithName("GetGovernedReportDefinitions");

        reports.MapPost("/definitions", async (
                ReportDefinitionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                GovernedReportDefinitionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var result = await repository.CreateAsync(
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/reports/definitions/{result.DefinitionId}",
                        result);
                }
                catch (ReportDefinitionConflictException exception)
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
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateGovernedReportDefinition")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        reports.MapGet("/definitions/{definitionId:guid}", async (
                ReportDefinitionRepository repository,
                Guid definitionId,
                CancellationToken cancellationToken) =>
            {
                var result = await repository.GetDetailAsync(definitionId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetGovernedReportDefinition");

        reports.MapPost("/definitions/{definitionId:guid}/revisions", async (
                ReportDefinitionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                GovernedReportRevisionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Created(
                        $"/api/reports/definitions/{definitionId}",
                        await repository.CreateRevisionAsync(
                            definitionId,
                            request,
                            session.Username,
                            cancellationToken));
                }
                catch (ReportDefinitionConflictException exception)
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
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateGovernedReportDefinitionRevision")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        foreach (var action in new[] { "review", "approve", "activate", "suspend", "retire" })
        {
            reports.MapPost($"/definitions/{{definitionId:guid}}/{action}", async (
                    ReportDefinitionRepository repository,
                    AuthRepository authRepository,
                    HttpContext httpContext,
                    Guid definitionId,
                    GovernedReportTransitionRequest request,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var session = await GetSessionFromHeaderAsync(
                            authRepository,
                            httpContext,
                            cancellationToken);
                        return Results.Ok(await repository.TransitionAsync(
                            definitionId,
                            action,
                            request,
                            session.Username,
                            cancellationToken));
                    }
                    catch (ReportDefinitionConflictException exception)
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
                        return Results.BadRequest(new { error = exception.Message });
                    }
                })
                .WithName($"TransitionGovernedReportDefinition{action}")
                .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));
        }

        reports.MapDelete("/definitions/{definitionId:guid}/test-fixture", async (
                ReportDefinitionRepository repository,
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
            .WithName("DeleteGovernedReportDefinitionTestFixture")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));


        reports.MapPost("/definitions/{definitionId:guid}/preview", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                GovernedReportPreviewRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var preview = await repository.PreviewAsync(
                        definitionId,
                        request,
                        session.Username,
                        cancellationToken);
                    return preview is null ? Results.NotFound() : Results.Ok(preview);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("PreviewGovernedReportDefinition")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapPost("/definitions/{definitionId:guid}/run", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                GovernedReportRunRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var run = await repository.RunAsync(
                        definitionId,
                        request,
                        session.Username,
                        cancellationToken);
                    return run is null
                        ? Results.NotFound()
                        : Results.Created($"/api/reports/runs/{run.Run.RunId}", run);
                }
                catch (ReportExecutionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        existingRun = exception.ExistingRun
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RunGovernedReportDefinition")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapGet("/definitions/{definitionId:guid}/runs", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                int? page,
                int? pageSize,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(
                    authRepository,
                    httpContext,
                    cancellationToken);
                return Results.Ok(await repository.ListRunsAsync(
                    definitionId,
                    session.Username,
                    page ?? 1,
                    pageSize ?? 20,
                    cancellationToken));
            })
            .WithName("GetGovernedReportRuns")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapGet("/runs/{runId}", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string runId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var run = await repository.GetRunAsync(
                        runId,
                        session.Username,
                        cancellationToken);
                    return run is null ? Results.NotFound() : Results.Ok(run);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetGovernedReportRun")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapPost("/runs/{runId}/cancel", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string runId,
                GovernedReportLifecycleRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var run = await repository.CancelAsync(
                        runId,
                        request,
                        session.Username,
                        cancellationToken);
                    return run is null ? Results.NotFound() : Results.Ok(run);
                }
                catch (ReportExecutionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        existingRun = exception.ExistingRun
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CancelGovernedReportRun")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapPost("/runs/{runId}/retry", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string runId,
                GovernedReportLifecycleRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var run = await repository.RetryAsync(
                        runId,
                        request,
                        session.Username,
                        cancellationToken);
                    return run is null ? Results.NotFound() : Results.Ok(run);
                }
                catch (ReportExecutionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        existingRun = exception.ExistingRun
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RetryGovernedReportRun")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        reports.MapGet("/runs/{runId}/download", async (
                ReportExecutionRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string runId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var artifact = await repository.DownloadAsync(
                        runId,
                        session.Username,
                        cancellationToken);
                    return artifact is null
                        ? Results.NotFound()
                        : Results.File(
                            artifact.Content,
                            artifact.ContentType,
                            artifact.FileName);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("DownloadGovernedReportRun")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        return reports;
    }
}
