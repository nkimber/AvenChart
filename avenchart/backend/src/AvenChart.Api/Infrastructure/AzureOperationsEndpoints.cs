// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

public static class AzureOperationsEndpoints
{
    public static RouteGroupBuilder MapAzureOperationsEndpoints(this RouteGroupBuilder administration)
    {
        var operations = administration.MapGroup("/azure-operations").WithTags("Azure Deployment Operations");
        operations.AddEndpointFilter<AzureOperationsEnabledFilter>();

        operations.MapPost("/access/unlock", async (
            AzureOperationsUnlockRequest request,
            AzureOperationsAccessService access,
            IStaffIdentityAdapter identity,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                var session = await identity.ResolveAsync(context, cancellationToken);
                return Results.Ok(await access.UnlockAsync(session, request.Code, context, cancellationToken));
            }
            catch (AzureOperationsAccessLockedException exception)
            {
                var retryAfter = Math.Max(1, (int)Math.Ceiling((exception.LockedUntil - DateTimeOffset.UtcNow).TotalSeconds));
                context.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Json(new
                {
                    error = "operations_access_locked",
                    detail = "Too many incorrect code attempts. Try again after the temporary lockout.",
                    lockedUntil = exception.LockedUntil
                }, statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (AzureOperationsAccessDeniedException exception)
            {
                return Results.Json(new { error = "operations_access_denied", detail = exception.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }).WithName("UnlockAzureOperations");

        var protectedOperations = operations.MapGroup(string.Empty);
        protectedOperations.AddEndpointFilter<AzureOperationsAccessFilter>();

        protectedOperations.MapPost("/access/change-code", async (
            AzureOperationsChangeCodeRequest request,
            AzureOperationsAccessService access,
            IStaffIdentityAdapter identity,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await identity.ResolveAsync(context, cancellationToken);
                return Results.Ok(await access.ChangeCodeAsync(session, request, context, cancellationToken));
            }
            catch (AzureOperationsAccessLockedException exception)
            {
                var retryAfter = Math.Max(1, (int)Math.Ceiling((exception.LockedUntil - DateTimeOffset.UtcNow).TotalSeconds));
                context.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Json(new { error = "operations_access_locked", detail = exception.Message, lockedUntil = exception.LockedUntil },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (AzureOperationsAccessDeniedException exception)
            {
                return Results.Json(new { error = "operations_access_denied", detail = exception.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AzureOperationsAccessConflictException exception)
            {
                return Results.Conflict(new { error = "operations_access_conflict", detail = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newCode"] = [exception.Message] });
            }
        }).WithMetadata(new AzureOperationsBootstrapAccessAllowed())
          .WithName("ChangeAzureOperationsAccessCode");

        protectedOperations.MapPost("/access/lock", async (
            AzureOperationsAccessService access,
            IStaffIdentityAdapter identity,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var session = await identity.ResolveAsync(context, cancellationToken);
            var token = context.Request.Headers[AzureOperationsAccessService.AccessHeader].ToString();
            await access.LockAsync(session, token, cancellationToken);
            return Results.NoContent();
        }).WithMetadata(new AzureOperationsBootstrapAccessAllowed())
          .WithName("LockAzureOperations");

        protectedOperations.MapGet("/capabilities", async (AzureOperationsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCapabilitiesAsync(cancellationToken))).WithName("GetAzureOperationsCapabilities");

        protectedOperations.MapPost("/assess", (AzureDeploymentProfileDocument document) =>
            Results.Ok(AzureDeploymentProfilePolicy.Assess(document))).WithName("AssessAzureDeploymentProfile");

        protectedOperations.MapGet("/profiles", async (AzureOperationsRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetProfilesAsync(cancellationToken))).WithName("GetAzureDeploymentProfiles");

        protectedOperations.MapGet("/profiles/{profileId:guid}", async (Guid profileId, AzureOperationsRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetProfileAsync(profileId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment profile not found", exception.Message, 404)); }
        }).WithName("GetAzureDeploymentProfile");

        protectedOperations.MapPost("/profiles", async (AzureDeploymentProfileCreateRequest request, AzureOperationsRepository repository, IStaffIdentityAdapter identity, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await identity.ResolveAsync(context, cancellationToken);
                var created = await repository.CreateProfileAsync(request, session.Username, cancellationToken);
                return Results.Created($"/api/administration/azure-operations/profiles/{created.ProfileId}", created);
            }
            catch (AzureDeploymentProfileValidationException exception) { return Validation(exception.Assessment); }
            catch (AzureDeploymentProfileConflictException exception) { return Results.Conflict(Problem("Azure deployment profile conflict", exception.Message, 409)); }
            catch (ArgumentException exception) { return Results.BadRequest(Problem("Invalid Azure deployment profile", exception.Message, 400)); }
        }).WithName("CreateAzureDeploymentProfile");

        protectedOperations.MapPut("/profiles/{profileId:guid}", async (Guid profileId, AzureDeploymentProfileUpdateRequest request, AzureOperationsRepository repository, IStaffIdentityAdapter identity, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await identity.ResolveAsync(context, cancellationToken);
                return Results.Ok(await repository.UpdateProfileAsync(profileId, request, session.Username, cancellationToken));
            }
            catch (AzureDeploymentProfileValidationException exception) { return Validation(exception.Assessment); }
            catch (AzureDeploymentProfileConflictException exception) { return Results.Conflict(Problem("Azure deployment profile conflict", exception.Message, 409)); }
            catch (ArgumentException exception) { return Results.BadRequest(Problem("Invalid Azure deployment profile", exception.Message, 400)); }
        }).WithName("UpdateAzureDeploymentProfile");

        protectedOperations.MapDelete("/profiles/{profileId:guid}", async (Guid profileId, int expectedVersion, AzureOperationsRepository repository, IStaffIdentityAdapter identity, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await identity.ResolveAsync(context, cancellationToken);
                await repository.ArchiveProfileAsync(profileId, expectedVersion, session.Username, cancellationToken);
                return Results.NoContent();
            }
            catch (AzureDeploymentProfileConflictException exception) { return Results.Conflict(Problem("Azure deployment profile conflict", exception.Message, 409)); }
        }).WithName("ArchiveAzureDeploymentProfile");

        protectedOperations.MapGet("/profiles/{profileId:guid}/history", async (Guid profileId, AzureOperationsRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetProfileHistoryAsync(profileId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment profile not found", exception.Message, 404)); }
        }).WithName("GetAzureDeploymentProfileHistory");

        protectedOperations.MapPost("/profiles/{profileId:guid}/validate-access", async (Guid profileId, AzureOperationsRepository repository, AzureOperationsService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var profile = await repository.GetProfileAsync(profileId, cancellationToken);
                return Results.Ok(await service.ValidateAccessAsync(profile.Document, cancellationToken));
            }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment profile not found", exception.Message, 404)); }
        }).WithName("ValidateAzureDeploymentAccess");

        protectedOperations.MapGet("/profiles/{profileId:guid}/health", async (Guid profileId, AzureOperationsRepository repository, AzureOperationsService service, CancellationToken cancellationToken) =>
        {
            try
            {
                var profile = await repository.GetProfileAsync(profileId, cancellationToken);
                return Results.Ok(await service.GetHealthAsync(profile.Document, cancellationToken));
            }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment profile not found", exception.Message, 404)); }
        }).WithName("GetAzureDeploymentHealth");

        protectedOperations.MapGet("/executions", async (Guid? profileId, int? limit, AzureOperationsRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetExecutionsAsync(profileId, limit ?? 30, cancellationToken))).WithName("GetAzureDeploymentExecutions");

        protectedOperations.MapGet("/executions/{executionId:guid}", async (Guid executionId, AzureOperationsRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetExecutionAsync(executionId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment execution not found", exception.Message, 404)); }
        }).WithName("GetAzureDeploymentExecution");

        MapExecutionStart(protectedOperations, "plan", "PLAN");
        MapExecutionStart(protectedOperations, "verify", "VERIFY");
        MapExecutionStart(protectedOperations, "deploy", null);
        MapExecutionStart(protectedOperations, "rollback", null);

        protectedOperations.MapPost("/executions/{executionId:guid}/cancel", async (Guid executionId, AzureOperationsRepository repository, AzureDeploymentCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            try
            {
                await repository.RequestCancellationAsync(executionId, cancellationToken);
                coordinator.Cancel(executionId);
                return Results.Accepted($"/api/administration/azure-operations/executions/{executionId}");
            }
            catch (AzureDeploymentProfileConflictException exception) { return Results.Conflict(Problem("Azure operation cannot be cancelled", exception.Message, 409)); }
        }).WithName("CancelAzureDeploymentExecution");

        return administration;
    }

    private static void MapExecutionStart(RouteGroupBuilder operations, string kind, string? fixedConfirmation)
    {
        operations.MapPost($"/profiles/{{profileId:guid}}/{kind}", async (Guid profileId, AzureDeploymentExecutionStartRequest request, AzureOperationsRepository repository, AzureDeploymentCoordinator coordinator, IStaffIdentityAdapter identity, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var profile = await repository.GetProfileAsync(profileId, cancellationToken);
                var expectedConfirmation = fixedConfirmation ?? (kind == "deploy" ? $"DEPLOY {profile.Document.ResourceGroupName}" : $"ROLLBACK {profile.Document.ContainerAppName}");
                if (!string.Equals(request.Confirmation.Trim(), expectedConfirmation, StringComparison.Ordinal))
                    return Results.BadRequest(Problem("Confirmation did not match", $"Enter exactly: {expectedConfirmation}", 400));
                var session = await identity.ResolveAsync(context, cancellationToken);
                var execution = await repository.CreateExecutionAsync(profileId, kind, request.ExpectedProfileVersion, session.Username, cancellationToken);
                coordinator.Queue(execution.ExecutionId);
                return Results.Accepted($"/api/administration/azure-operations/executions/{execution.ExecutionId}", execution);
            }
            catch (AzureDeploymentProfileValidationException exception) { return Validation(exception.Assessment); }
            catch (AzureDeploymentProfileConflictException exception) { return Results.Conflict(Problem("Azure operation conflict", exception.Message, 409)); }
            catch (ArgumentException exception) { return Results.NotFound(Problem("Azure deployment profile not found", exception.Message, 404)); }
        }).WithName($"StartAzure{char.ToUpperInvariant(kind[0])}{kind[1..]}Execution");
    }

    private static IResult Validation(AzureDeploymentProfileAssessment assessment)
    {
        var errors = assessment.Issues.Where(issue => issue.Severity == "error")
            .GroupBy(issue => issue.Field)
            .ToDictionary(group => group.Key, group => group.Select(issue => issue.Message).ToArray());
        return Results.ValidationProblem(errors, statusCode: 400, title: "Azure deployment profile validation failed", extensions: new Dictionary<string, object?> { ["assessment"] = assessment });
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Problem(string title, string detail, int status) => new() { Title = title, Detail = detail, Status = status };
}
