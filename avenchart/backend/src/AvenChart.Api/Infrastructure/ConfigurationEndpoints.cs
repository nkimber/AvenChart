// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Experience;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps delegated and administrator configuration, governance, policy, and access-control routes.
/// </summary>
public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this WebApplication app, RouteGroupBuilder administration)
    {
        var delegatedConfiguration = app.MapGroup("/api/configuration-delegation").WithTags("Configuration delegation");
        delegatedConfiguration.AddEndpointFilter(StaffAccessContextFilter("delegated-configuration"));
        delegatedConfiguration.MapPost("/practice-settings/{key}/change-requests", async (string key, PracticeSettingChangeRequestCreateRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            if (!session.Authenticated) return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
            try { var response = await repository.CreateDelegatedPracticeSettingChangeRequestAsync(key, request, session.Username, cancellationToken); return Results.Created($"/api/administration/practice-setting-change-requests/{response.Request.RequestId}", response); }
            catch (UnauthorizedAccessException exception) { return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden); }
            catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateDelegatedPracticeSettingChangeRequest");
        delegatedConfiguration.MapPost("/practice-setting-change-requests/{requestId:guid}/submit", async (Guid requestId, PracticeSettingChangeRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            if (!session.Authenticated) return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
            try { return Results.Ok(await repository.SubmitDelegatedPracticeSettingChangeRequestAsync(requestId, request, session.Username, cancellationToken)); }
            catch (UnauthorizedAccessException exception) { return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden); }
            catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("SubmitDelegatedPracticeSettingChangeRequest");

        administration.MapGet("/experience-baseline", () =>
            Results.Ok(ExperienceBaselineCatalog.Build()))
            .WithName("GetExperienceBaseline");

        administration.MapGet("/identity-provider/readiness", () =>
            Results.Ok(IdentityProviderCatalog.Build(
                app.Services.GetRequiredService<IOptions<IdentityProviderOptions>>().Value,
                app.Environment.IsDevelopment())))
            .WithName("GetIdentityProviderReadiness");

        administration.MapGet("/configuration-catalog", () => Results.Ok(new ConfigurationCatalogResponse([
            new("practice.identity", "Practice identity and contact", "Local implemented", "Practice administrator", "Required non-blank practice name", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
            new("practice.default-facility", "Default facility", "Local implemented", "Practice administrator", "Must reference a positive facility identifier", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
            new("practice.locale-timezone", "Locale and time zone", "Local implemented", "Practice and operations owners", "Supported IANA or Windows time-zone identifier", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
            new("coding.catalogs", "Coding catalogs", "Local implemented", "Practice administrator", "Unique key/order, bounded modifiers, immutable historical key", "Create, edit, and activation state enabled"),
            new("forms.option-lists", "Form option lists", "Local implemented", "Practice administrator", "Ordered option key, label, value, default, and activation metadata", "Create, edit, and activation state enabled"),
            new("scheduling.defaults", "Appointment defaults", "Owner-gated", "Operations owner", "Facility/provider compatibility and bounded values", "No mutable source selected"),
            new("clinical.templates", "Clinical forms and templates", "Clinical-governed", "Clinical owner", "Versioned content and activation date", "No mutable source selected"),
            new("integrations.secrets", "Security and integration settings", "Deployment-only", "Security and operations owners", "Environment validation; never return secrets", "Excluded from application API")
        ]))).WithName("GetConfigurationCatalog");

        administration.MapGet("/runtime-diagnostics", (RuntimeDiagnostics diagnostics) =>
            Results.Ok(diagnostics.GetSnapshot()))
            .WithName("GetRuntimeDiagnostics");
        administration.MapGet("/authorization-policy-catalog", (
                string? query,
                string? gap,
                int? offset,
                int? limit) =>
            {
                try
                {
                    return Results.Ok(AuthorizationPolicyCatalog.Search(
                        query,
                        gap,
                        offset ?? 0,
                        limit ?? 8));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["authorizationPolicies"] = [exception.Message],
                    });
                }
            })
            .WithName("GetAuthorizationPolicyCatalog");

        administration.MapGet("/practice-settings", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetPracticeSettingsAsync(cancellationToken))).WithName("GetPracticeSettings");
        administration.MapGet("/practice-settings/registry", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetPracticeSettingRegistryAsync(cancellationToken))).WithName("GetPracticeSettingRegistry");
        administration.MapPost("/configuration-packages/export", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            return Results.Ok(await repository.ExportConfigurationPackageAsync(session.Username, cancellationToken));
        }).WithName("ExportConfigurationPackage");
        administration.MapPost("/configuration-packages/dry-run", async (ConfigurationPackageDryRunRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            return Results.Ok(await repository.DryRunConfigurationPackageAsync(request, session.Username, cancellationToken));
        }).WithName("DryRunConfigurationPackage");
        administration.MapGet("/configuration-package-import-requests", async (string? status, string? kind, int? offset, int? limit, AdministrationRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetConfigurationPackageImportRequestsAsync(status, kind, offset ?? 0, limit ?? 12, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("GetConfigurationPackageImportRequests");
        administration.MapGet("/configuration-package-import-requests/{requestId:guid}", async (Guid requestId, AdministrationRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetConfigurationPackageImportRequestAsync(requestId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
        }).WithName("GetConfigurationPackageImportRequest");
        administration.MapPost("/configuration-package-import-requests", async (ConfigurationPackageImportRequestCreateRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var response = await repository.CreateConfigurationPackageImportRequestAsync(request, session.Username, cancellationToken);
                return Results.Created($"/api/administration/configuration-package-import-requests/{response.Request.RequestId}", response);
            }
            catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateConfigurationPackageImportRequest");
        administration.MapPost("/configuration-package-import-requests/{requestId:guid}/compensating-rollback", async (Guid requestId, ConfigurationPackageImportRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var response = await repository.CreateConfigurationPackageCompensatingRollbackAsync(requestId, request.Note ?? string.Empty, session.Username, cancellationToken);
                return Results.Created($"/api/administration/configuration-package-import-requests/{response.Request.RequestId}", response);
            }
            catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateConfigurationPackageCompensatingRollback");
        administration.MapPost("/configuration-package-import-requests/{requestId:guid}/{action}", async (Guid requestId, string action, ConfigurationPackageImportRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var response = action switch
                {
                    "submit" => await repository.SubmitConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
                    "approve" => await repository.ApproveConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
                    "reject" => await repository.RejectConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
                    "activate" => await repository.ActivateConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
                    "cancel" => await repository.CancelConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
                    _ => throw new ArgumentException("The requested import-request action is not supported."),
                };
                return Results.Ok(response);
            }
            catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("TransitionConfigurationPackageImportRequest");
        administration.MapGet("/practice-setting-delegations", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetPracticeSettingDelegationsAsync(cancellationToken))).WithName("GetPracticeSettingDelegations");
        administration.MapPost("/practice-setting-delegations", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, PracticeSettingDelegationCreateRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/administration/practice-setting-delegations", await repository.GrantPracticeSettingDelegationAsync(request, session.Username, cancellationToken)); } catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GrantPracticeSettingDelegation");
        administration.MapPost("/practice-setting-delegations/{delegationId:guid}/revoke", async (Guid delegationId, PracticeSettingChangeRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RevokePracticeSettingDelegationAsync(delegationId, request.Note, session.Username, cancellationToken)); } catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RevokePracticeSettingDelegation");
        administration.MapGet("/practice-settings/effective", async (AdministrationRepository repository, int? facilityId, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetEffectivePracticeSettingsAsync(facilityId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("GetEffectivePracticeSettings");
        administration.MapGet("/practice-settings/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetPracticeSettingHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingHistory");
        administration.MapGet("/practice-setting-change-requests", async (
                AdministrationRepository repository,
                string? settingKey,
                string? status,
                int? offset,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetPracticeSettingChangeRequestsAsync(
                        settingKey,
                        status,
                        offset ?? 0,
                        limit ?? 8,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequests"] = [exception.Message],
                    });
                }
            })
            .WithName("GetPracticeSettingChangeRequests");
        administration.MapGet("/practice-setting-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetPracticeSettingChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingChangeRequest");
        administration.MapGet("/practice-setting-change-requests/{requestId:guid}/impact-preview", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetPracticeSettingChangeRequestImpactPreviewAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingChangeRequestImpactPreview");
        administration.MapPost("/practice-settings/{key}/change-requests", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string key,
                PracticeSettingChangeRequestCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var response = await repository.CreatePracticeSettingChangeRequestAsync(
                        key,
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/administration/practice-setting-change-requests/{response.Request.RequestId}",
                        response);
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting change request conflicts with current state",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("CreatePracticeSettingChangeRequest");

        administration.MapPost("/practice-setting-change-requests/{requestId:guid}/submit", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid requestId,
                PracticeSettingChangeRequestDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.SubmitPracticeSettingChangeRequestAsync(
                        requestId,
                        request.Note,
                        request.ExpectedVersion,
                        session.Username,
                        cancellationToken));
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting change request is stale",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("SubmitPracticeSettingChangeRequest");

        administration.MapPost("/practice-setting-change-requests/{requestId:guid}/approve", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid requestId,
                PracticeSettingChangeRequestDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.ApprovePracticeSettingChangeRequestAsync(
                        requestId,
                        request.Note,
                        request.ExpectedVersion,
                        session.Username,
                        cancellationToken));
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting change request is stale",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("ApprovePracticeSettingChangeRequest");

        administration.MapPost("/practice-setting-change-requests/{requestId:guid}/reject", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid requestId,
                PracticeSettingChangeRequestDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.RejectPracticeSettingChangeRequestAsync(
                        requestId,
                        request.Note,
                        request.ExpectedVersion,
                        session.Username,
                        cancellationToken));
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting change request is stale",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("RejectPracticeSettingChangeRequest");

        administration.MapPost("/practice-setting-change-requests/{requestId:guid}/activate", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid requestId,
                PracticeSettingChangeRequestDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.ActivatePracticeSettingChangeRequestAsync(
                        requestId,
                        request.Note,
                        request.ExpectedVersion,
                        session.Username,
                        cancellationToken));
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting activation is stale",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("ActivatePracticeSettingChangeRequest");

        administration.MapPost("/practice-setting-change-requests/{requestId:guid}/cancel", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid requestId,
                PracticeSettingChangeRequestDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.CancelPracticeSettingChangeRequestAsync(
                        requestId,
                        request.Note,
                        request.ExpectedVersion,
                        session.Username,
                        cancellationToken));
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting change request is stale",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("CancelPracticeSettingChangeRequest");

        administration.MapDelete("/practice-setting-change-requests/{requestId:guid}/test-fixture", async (
                AdministrationRepository repository,
                Guid requestId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return await repository.DeletePracticeSettingChangeRequestTestFixtureAsync(
                        requestId,
                        cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (PracticeSettingChangeRequestConflictException exception)
                {
                    return Results.Problem(
                        title: "Practice-setting fixture is still active",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["changeRequest"] = [exception.Message],
                    });
                }
            })
            .WithName("DeletePracticeSettingChangeRequestTestFixture");

        administration.MapGet("/coding-catalogs", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetCodingCatalogsAsync(cancellationToken))).WithName("GetCodingCatalogs");
        administration.MapGet("/coding-catalogs/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetCodingCatalogHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetCodingCatalogHistory");
        administration.MapPost("/coding-catalogs", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CodingCatalogCreateRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/administration/coding-catalogs/" + request.Key, await repository.CreateCodingCatalogAsync(request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateCodingCatalog");
        administration.MapPut("/coding-catalogs/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, CodingCatalogUpdateRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateCodingCatalogAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpdateCodingCatalog");
        administration.MapPost("/coding-catalogs/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackCodingCatalogAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackCodingCatalog");
        administration.MapGet("/coding-catalog-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetCodingCatalogChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetCodingCatalogChangeRequests");
        administration.MapPost("/coding-catalog-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CodingCatalogChangeRequestCreateRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateCodingCatalogChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/coding-catalog-change-requests/{created.Request.RequestId}", created); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateCodingCatalogChangeRequest");
        administration.MapGet("/coding-catalog-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetCodingCatalogChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetCodingCatalogChangeRequest");
        administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitCodingCatalogChangeRequest");
        administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveCodingCatalogChangeRequest");
        administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectCodingCatalogChangeRequest");
        administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateCodingCatalogChangeRequest");
        administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelCodingCatalogChangeRequest");

        administration.MapGet("/form-layouts", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetFormLayoutsAsync(cancellationToken))).WithName("GetFormLayouts");
        administration.MapGet("/form-layouts/{key}", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayout");
        administration.MapGet("/form-layouts/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayoutHistory");
        administration.MapPut("/form-layouts/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, FormLayoutMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayout");
        administration.MapPut("/form-layouts/{layoutKey}/groups/{groupKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string layoutKey, string groupKey, FormLayoutGroupMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutGroupAsync(layoutKey, groupKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayoutGroup");
        administration.MapPut("/form-layouts/{layoutKey}/fields/{fieldKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string layoutKey, string fieldKey, FormLayoutFieldMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutFieldAsync(layoutKey, fieldKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayoutField");
        administration.MapPost("/form-layouts/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackFormLayoutAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackFormLayout");

        administration.MapGet("/form-layout-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetFormLayoutChangeRequests");
        administration.MapPost("/form-layout-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, FormLayoutChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateFormLayoutChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/form-layout-change-requests/{created.Request.RequestId}", created); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateFormLayoutChangeRequest");
        administration.MapGet("/form-layout-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayoutChangeRequest");
        administration.MapPost("/form-layout-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitFormLayoutChangeRequest");
        administration.MapPost("/form-layout-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveFormLayoutChangeRequest");
        administration.MapPost("/form-layout-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectFormLayoutChangeRequest");
        administration.MapPost("/form-layout-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateFormLayoutChangeRequest");
        administration.MapPost("/form-layout-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelFormLayoutChangeRequest");

        administration.MapGet("/form-option-lists", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetFormOptionListsAsync(cancellationToken))).WithName("GetFormOptionLists");
        administration.MapGet("/form-option-lists/{key}", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionList");
        administration.MapGet("/form-option-lists/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionListHistory");
        administration.MapPut("/form-option-lists/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, FormOptionListMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormOptionListAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormOptionList");
        administration.MapPut("/form-option-lists/{listKey}/options/{optionKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string listKey, string optionKey, FormOptionValueMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormOptionValueAsync(listKey, optionKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormOptionValue");
        administration.MapPost("/form-option-lists/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackFormOptionListAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackFormOptionList");

        administration.MapGet("/form-option-list-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetFormOptionListChangeRequests");
        administration.MapPost("/form-option-list-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, FormOptionListChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateFormOptionListChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/form-option-list-change-requests/{created.Request.RequestId}", created); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateFormOptionListChangeRequest");
        administration.MapGet("/form-option-list-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionListChangeRequest");
        administration.MapPost("/form-option-list-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitFormOptionListChangeRequest");
        administration.MapPost("/form-option-list-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveFormOptionListChangeRequest");
        administration.MapPost("/form-option-list-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectFormOptionListChangeRequest");
        administration.MapPost("/form-option-list-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateFormOptionListChangeRequest");
        administration.MapPost("/form-option-list-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelFormOptionListChangeRequest");

        administration.MapGet("/clinical-alert-rules", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetClinicalAlertRulesAsync(cancellationToken))).WithName("GetClinicalAlertRules");
        administration.MapGet("/clinical-alert-rules/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleHistory");
        administration.MapPost("/clinical-alert-rule-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ClinicalAlertRuleChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateClinicalAlertRuleChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/clinical-alert-rule-change-requests/{created.Request.RequestId}", created); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateClinicalAlertRuleChangeRequest");
        administration.MapGet("/clinical-alert-rule-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleChangeRequestsAsync(status, offset ?? 0, limit ?? 50, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleChangeRequests");
        administration.MapGet("/clinical-alert-rule-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleChangeRequest");
        administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitClinicalAlertRuleChangeRequest");
        administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveClinicalAlertRuleChangeRequest");
        administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectClinicalAlertRuleChangeRequest");
        administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateClinicalAlertRuleChangeRequest");
        administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelClinicalAlertRuleChangeRequest");
        administration.MapGet("/modules", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetModuleCatalogAsync(cancellationToken))).WithName("GetModuleCatalog");
        administration.MapGet("/modules/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleCatalogHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetModuleCatalogHistory");
        administration.MapPost("/module-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ModuleChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateModuleChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/module-change-requests/{created.Request.RequestId}", created); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateModuleChangeRequest");
        administration.MapGet("/module-change-requests", async (AdministrationRepository repository, string? status, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleChangeRequestsAsync(status, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetModuleChangeRequests");
        administration.MapGet("/module-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetModuleChangeRequest");
        administration.MapPost("/module-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitModuleChangeRequest");
        administration.MapPost("/module-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveModuleChangeRequest");
        administration.MapPost("/module-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectModuleChangeRequest");
        administration.MapPost("/module-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateModuleChangeRequest");
        administration.MapPost("/module-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelModuleChangeRequest");
        administration.MapPut("/modules/{key}/status", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ModuleCatalogStatusUpdateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateModuleCatalogStatusAsync(key, request.Status, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpdateModuleCatalogStatus");
        administration.MapPost("/modules/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackModuleCatalogAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackModuleCatalog");
        administration.MapGet("/api-clients", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetApiClientsAsync(cancellationToken))).WithName("GetApiClients");
        administration.MapGet("/api-clients/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientRegistryHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetApiClientRegistryHistory");
        administration.MapPut("/api-clients/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ApiClientRegistryMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertApiClientAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertApiClient");
        administration.MapPost("/api-clients/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackApiClientRegistryAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackApiClientRegistry");
        administration.MapPost("/api-client-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ApiClientChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateApiClientChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/api-client-change-requests/{created.Request.RequestId}", created); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateApiClientChangeRequest");
        administration.MapGet("/api-client-change-requests", async (AdministrationRepository repository, string? status, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientChangeRequestsAsync(status, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetApiClientChangeRequests");
        administration.MapGet("/api-client-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetApiClientChangeRequest");
        administration.MapPost("/api-client-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitApiClientChangeRequest");
        administration.MapPost("/api-client-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveApiClientChangeRequest");
        administration.MapPost("/api-client-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectApiClientChangeRequest");
        administration.MapPost("/api-client-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateApiClientChangeRequest");
        administration.MapPost("/api-client-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelApiClientChangeRequest");
        administration.MapPut("/clinical-alert-rules/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ClinicalAlertRuleMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertClinicalAlertRuleAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertClinicalAlertRule");
        administration.MapPost("/clinical-alert-rules/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackClinicalAlertRuleAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackClinicalAlertRule");

        administration.MapPut("/practice-settings/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, PracticeSettingUpdateRequest request, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdatePracticeSettingAsync(key, request.Value, session.Username, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("UpdatePracticeSetting");
        administration.MapPost("/practice-settings/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackPracticeSettingAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackPracticeSetting");

        administration.MapGet("/directory", async (
                AdministrationRepository repository,
                CancellationToken cancellationToken) =>
            {
                var directory = await repository.GetDirectoryAsync(cancellationToken);
                return Results.Ok(directory);
            })
            .WithName("GetAdministrationDirectory");

        administration.MapGet("/audit/phi", async (
                PhiAuditRepository repository,
                int? limit,
                string? username,
                DateOnly? from,
                DateOnly? to,
                CancellationToken cancellationToken) =>
            {
                try { return Results.Ok(await repository.GetRecentAsync(limit ?? 50, username, from, to, cancellationToken)); }
                catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["audit"] = [exception.Message] }); }
            })
            .WithName("GetPhiAccessAudit");

        administration.MapGet("/audit/phi/export", async (
                PhiAuditRepository repository,
                int? limit,
                string? username,
                DateOnly? from,
                DateOnly? to,
                CancellationToken cancellationToken) =>
            {
                try { return Results.File(Encoding.UTF8.GetBytes(await repository.GetCsvAsync(limit ?? 200, username, from, to, cancellationToken)), "text/csv", "avenchart-phi-access-audit.csv"); }
                catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["audit"] = [exception.Message] }); }
            })
            .WithName("ExportPhiAccessAudit");

        administration.MapPut("/portal-activity/profile-reviews/{requestId:long}/accept", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                long requestId,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.AcceptPortalProfileReviewAsync(
                    requestId,
                    session.Username,
                    cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("AcceptAdministrationPortalProfileReview");

        administration.MapPut("/portal-activity/profile-reviews/{requestId:long}/revert", async (
                AdministrationRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                long requestId,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.RevertPortalProfileReviewAsync(
                    requestId,
                    session.Username,
                    cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("RevertAdministrationPortalProfileReview");

        administration.MapPost("/users", async (
                AdministrationDirectoryRepository repository,
                AdministrationUserMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.CreateUserAsync(request, cancellationToken);
                    return Results.Created($"/api/administration/users/{mutation.Id}", mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateAdministrationUser");

        administration.MapPut("/users/{userId:int}", async (
                AdministrationDirectoryRepository repository,
                int userId,
                AdministrationUserMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.UpdateUserAsync(userId, request, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdateAdministrationUser");

        administration.MapDelete("/users/{userId:int}", async (
                AdministrationDirectoryRepository repository,
                int userId,
                CancellationToken cancellationToken) =>
            {
                var deleted = await repository.DeleteUserAsync(userId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteAdministrationUser");

        administration.MapPost("/facilities", async (
                AdministrationDirectoryRepository repository,
                AdministrationFacilityMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.CreateFacilityAsync(request, cancellationToken);
                    return Results.Created($"/api/administration/facilities/{mutation.Id}", mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateAdministrationFacility");

        administration.MapPut("/facilities/{facilityId:int}", async (
                AdministrationDirectoryRepository repository,
                int facilityId,
                AdministrationFacilityMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.UpdateFacilityAsync(facilityId, request, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdateAdministrationFacility");

        administration.MapDelete("/facilities/{facilityId:int}", async (
                AdministrationDirectoryRepository repository,
                int facilityId,
                CancellationToken cancellationToken) =>
            {
                var deleted = await repository.DeleteFacilityAsync(facilityId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteAdministrationFacility");

        administration.MapPut("/access-control/group-permissions", async (
                AdministrationDirectoryRepository repository,
                AdministrationAccessPermissionMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.GrantAccessGroupPermissionAsync(request, cancellationToken);
                    return Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GrantAdministrationAccessGroupPermission");

        administration.MapDelete("/access-control/group-permissions/{groupValue}/{sectionValue}/{permissionValue}", async (
                AdministrationDirectoryRepository repository,
                string groupValue,
                string sectionValue,
                string permissionValue,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.RevokeAccessGroupPermissionAsync(
                        groupValue,
                        sectionValue,
                        permissionValue,
                        cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RevokeAdministrationAccessGroupPermission");

        administration.MapPut("/access-control/user-memberships", async (
                AdministrationDirectoryRepository repository,
                AdministrationAccessUserMembershipMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.GrantAccessUserMembershipAsync(request, cancellationToken);
                    return Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GrantAdministrationAccessUserMembership");

        administration.MapDelete("/access-control/user-memberships/{userValue}/{groupValue}", async (
                AdministrationDirectoryRepository repository,
                string userValue,
                string groupValue,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.RevokeAccessUserMembershipAsync(userValue, groupValue, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RevokeAdministrationAccessUserMembership");

    }
}
