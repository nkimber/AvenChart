// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Microsoft.AspNetCore.Mvc;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected laboratory and procedure operations. Orders, specimens,
/// reports, results, and clinical follow-up retain one facility-scoped boundary.
/// </summary>
public static class ProcedureEndpoints
{
    public static RouteGroupBuilder MapProcedureEndpoints(this WebApplication app)
    {
        var procedures = app.MapGroup("/api/procedures").WithTags("Procedures");
        RequireAccessPermission(procedures, "patients", "lab", "view");
        procedures.AddEndpointFilter(ProcedureFacilityScopeFilter());

        procedures.MapGet("/lab-provider-address-book", async (
                ProcedureRepository repository,
                CancellationToken cancellationToken) =>
            {
                var addressBook = await repository.GetLabProviderAddressBookAsync(cancellationToken);
                return Results.Ok(addressBook);
            })
            .WithName("GetProcedureLabProviderAddressBook");

        procedures.MapPost("/lab-provider-address-book", async (
                ProcedureDirectoryRepository repository,
                ProcedureLabProviderAddressBookMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateLabProviderAddressBookOrganizationAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest(new { error = "Procedure lab provider address-book organization is required." })
                    : Results.Created($"/api/procedures/lab-provider-address-book/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureLabProviderAddressBookOrganization")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapDelete("/lab-provider-address-book/{organizationId:int}", async (
                ProcedureDirectoryRepository repository,
                int organizationId,
                CancellationToken cancellationToken) =>
            {
                var deleted = await repository.DeleteLabProviderAddressBookOrganizationAsync(organizationId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteProcedureLabProviderAddressBookOrganization")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapGet("/lab-providers", async (
                ProcedureRepository repository,
                bool? includeInactive,
                CancellationToken cancellationToken) =>
            {
                var directory = await repository.GetLabProvidersAsync(includeInactive ?? false, cancellationToken);
                return Results.Ok(directory);
            })
            .WithName("GetProcedureLabProviders");

        procedures.MapPost("/lab-providers", async (
                ProcedureDirectoryRepository repository,
                ProcedureLabProviderMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateLabProviderAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest(new { error = "Procedure lab provider name or valid address-book organization is required." })
                    : Results.Created($"/api/procedures/lab-providers/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureLabProvider")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/lab-providers/{providerId:int}", async (
                ProcedureDirectoryRepository repository,
                int providerId,
                ProcedureLabProviderMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.UpdateLabProviderAsync(providerId, request, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("UpdateProcedureLabProvider")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapDelete("/lab-providers/{providerId:int}", async (
                ProcedureDirectoryRepository repository,
                int providerId,
                CancellationToken cancellationToken) =>
            {
                var deleted = await repository.DeleteLabProviderAsync(providerId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteProcedureLabProvider")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapGet("/order-catalog", async (
                ProcedureRepository repository,
                CancellationToken cancellationToken) =>
            {
                var catalog = await repository.GetOrderCatalogAsync(cancellationToken);
                return Results.Ok(catalog);
            })
            .WithName("GetProcedureOrderCatalog");

        procedures.MapPost("/order-catalog", async (
                ProcedureDirectoryRepository repository,
                ProcedureOrderCatalogMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateOrderCatalogItemAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest(new { error = "Procedure order catalog item requires a valid name, type, parent, lab, and code." })
                    : Results.Created($"/api/procedures/order-catalog/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureOrderCatalogItem")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/order-catalog/import-compendium", async (
                ProcedureRepository repository,
                ProcedureOrderCatalogImportRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var import = await repository.ImportOrderCatalogCompendiumAsync(request, cancellationToken);
                    return import is null
                        ? Results.BadRequest(new { error = "Procedure order catalog compendium import requires a valid vendor format, group, lab, and CSV payload." })
                        : Results.Ok(import);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["csvText"] = [exception.Message] });
                }
            })
            .WithName("ImportProcedureOrderCatalogCompendium")
            .WithMetadata(new RequestSizeLimitAttribute(ProcedureRepository.MaximumOrderCatalogImportRequestBytes))
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/order-catalog/{itemId:int}", async (
                ProcedureDirectoryRepository repository,
                int itemId,
                ProcedureOrderCatalogMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.UpdateOrderCatalogItemAsync(itemId, request, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("UpdateProcedureOrderCatalogItem")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapDelete("/order-catalog/{itemId:int}", async (
                ProcedureDirectoryRepository repository,
                int itemId,
                CancellationToken cancellationToken) =>
            {
                var deleted = await repository.DeleteOrderCatalogItemAsync(itemId, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteProcedureOrderCatalogItem")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapGet("/report-review-queue", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                string? status,
                string? patientId,
                int? providerId,
                int? labId,
                DateOnly? fromDate,
                DateOnly? toDate,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var queue = await repository.GetReportReviewQueueAsync(
                    status,
                    patientId,
                    providerId,
                    labId,
                    fromDate,
                    toDate,
                    limit ?? 25,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return Results.Ok(queue);
            })
            .WithName("GetProcedureReportReviewQueue");

        procedures.MapGet("/critical-result-queue", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetCriticalLabResultQueueAsync(
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)))
            .WithName("GetCriticalLabResultQueue")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

        procedures.MapPut("/results/{resultId:int}/critical-acknowledgement", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultAcknowledgementRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.AcknowledgeCriticalLabResultAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { accepted = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("AcceptCriticalLabResultFollowUp")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/results/{resultId:int}/critical-follow-up/ownership", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultFollowUpOwnershipRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.TransferCriticalLabResultFollowUpAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { updated = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("TransferCriticalLabResultFollowUpOwnership")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/results/{resultId:int}/critical-follow-up/communications", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultFollowUpCommunicationRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.RecordCriticalLabResultCommunicationAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { recorded = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RecordCriticalLabResultCommunication")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/results/{resultId:int}/critical-follow-up/clinical-actions", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultFollowUpActionRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.RecordCriticalLabResultClinicalActionAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { recorded = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RecordCriticalLabResultClinicalAction")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/results/{resultId:int}/critical-follow-up/escalations", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultFollowUpEscalationRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.EscalateCriticalLabResultFollowUpAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { escalated = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("EscalateCriticalLabResultFollowUp")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/results/{resultId:int}/critical-follow-up/closure", async (
                ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
                int resultId, CriticalLabResultFollowUpClosureRequest request, CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                try
                {
                    return await repository.CloseCriticalLabResultFollowUpAsync(
                            resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                        ? Results.Ok(new { closed = true })
                        : Results.NotFound();
                }
                catch (CriticalLabResultFollowUpConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CloseCriticalLabResultFollowUp")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapGet("/results/{resultId:int}/critical-follow-up/history", async (
                ProcedureRepository repository, HttpContext httpContext, int resultId, CancellationToken cancellationToken) =>
            (await repository.GetCriticalLabResultFollowUpHistoryAsync(
                resultId, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)) is { } history
                ? Results.Ok(history)
                : Results.NotFound())
            .WithName("GetCriticalLabResultFollowUpHistory")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

        procedures.MapGet("/reports/{reportId:int}/review-history", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                int reportId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetReportReviewHistoryAsync(
                    reportId,
                    cancellationToken,
                    RequireStaffAccessContext(httpContext).FacilityId);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetProcedureReportReviewHistory");

        procedures.MapGet("/order-queue", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                string? status,
                string? patientId,
                int? providerId,
                int? labId,
                DateOnly? fromDate,
                DateOnly? toDate,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var queue = await repository.GetOrderQueueAsync(
                    status,
                    patientId,
                    providerId,
                    labId,
                    fromDate,
                    toDate,
                    limit.GetValueOrDefault(50),
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return Results.Ok(queue);
            })
            .WithName("GetProcedureOrderQueue");

        procedures.MapGet("/{patientId}", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                var procedureResults = await repository.GetForPatientAsync(
                    patientId,
                    cancellationToken,
                    RequireStaffAccessContext(httpContext).FacilityId);
                return procedureResults is null ? Results.NotFound() : Results.Ok(procedureResults);
            })
            .WithName("GetProcedureResultsForPatient");

        procedures.MapPost("/orders", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                ProcedureOrderCreateRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.CreateOrderAsync(
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure order could not be created from the supplied patient, encounter, and order details.")
                        : Results.Created($"/api/procedures/orders/{mutation.Id}", mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("CreateProcedureOrder")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

        procedures.MapGet("/orders/{orderId:int}/history", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                int orderId,
                CancellationToken cancellationToken) =>
            (await repository.GetOrderMutationHistoryAsync(
                orderId,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)) is { } history
                ? Results.Ok(history)
                : Results.NotFound())
            .WithName("GetProcedureOrderMutationHistory")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

        procedures.MapPut("/orders/{orderId:int}/status", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int orderId,
                ProcedureOrderStatusUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateOrderStatusAsync(
                        orderId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("UpdateProcedureOrderStatus")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/orders/{orderId:int}/transmit", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int orderId,
                ProcedureOrderTransmitRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.TransmitOrderAsync(
                        orderId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure order could not be marked transmitted from the supplied order state.")
                        : Results.Ok(mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("TransmitProcedureOrder")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/orders/{orderId:int}", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int orderId,
                ProcedureOrderUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateOrderAsync(
                        orderId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure order could not be updated from the supplied order details.")
                        : Results.Ok(mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("UpdateProcedureOrder")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPost("/reports", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                ProcedureReportCreateRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.CreateReportAsync(
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Procedure report could not be created from the supplied order and report details.")
                    : Results.Created($"/api/procedures/reports/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureReport")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

        procedures.MapPut("/reports/{reportId:int}", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int reportId,
                ProcedureReportUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.UpdateReportAsync(
                    reportId,
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Procedure report could not be updated from the supplied report details.")
                    : Results.Ok(mutation);
            })
            .WithName("UpdateProcedureReport")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/reports/{reportId:int}/sign", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int reportId,
                ProcedureReportSignRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.SignReportAsync(
                        reportId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure report could not be signed from the supplied review details.")
                        : Results.Ok(mutation);
                }
                catch (ProcedureReportReviewConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            })
            .WithName("SignProcedureReport")
            .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

        procedures.MapPut("/reports/{reportId:int}/deny-review", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int reportId,
                ProcedureReportReviewDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.DenyReportReviewAsync(
                        reportId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure report review could not be denied from the supplied details.")
                        : Results.Ok(mutation);
                }
                catch (ProcedureReportReviewConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            })
            .WithName("DenyProcedureReportReview")
            .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

        procedures.MapPut("/reports/{reportId:int}/review-assignment", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int reportId,
                ProcedureReportReviewAssignmentRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.AssignReportReviewerAsync(
                        reportId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure report reviewer assignment could not be saved from the supplied details.")
                        : Results.Ok(mutation);
                }
                catch (ProcedureReportReviewConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            })
            .WithName("AssignProcedureReportReviewer")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/reports/{reportId:int}/reopen-review", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int reportId,
                ProcedureReportReviewDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.ReopenReportReviewAsync(
                        reportId,
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure report review could not be reopened.")
                        : Results.Ok(mutation);
                }
                catch (ProcedureReportReviewConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            })
            .WithName("ReopenProcedureReportReview")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapPut("/reports/bulk-sign", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                ProcedureReportBulkSignRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.BulkSignReportsAsync(
                        request,
                        session.Username,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Procedure reports could not be bulk signed from the supplied review details.")
                        : Results.Ok(mutation);
                }
                catch (ProcedureReportReviewConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
                }
                catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            })
            .WithName("BulkSignProcedureReports")
            .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

        procedures.MapPost("/specimens", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                ProcedureSpecimenCreateRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.CreateSpecimenAsync(
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Procedure specimen could not be created from the supplied order and specimen details.")
                    : Results.Created($"/api/procedures/specimens/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureSpecimen")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

        procedures.MapPut("/specimens/{specimenId:int}/lifecycle", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int specimenId,
                ProcedureSpecimenLifecycleTransitionRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.TransitionSpecimenLifecycleAsync(
                    specimenId,
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.Conflict(new { error = "The specimen lifecycle transition is no longer valid at the supplied version." })
                    : Results.Ok(mutation);
            })
            .WithName("TransitionProcedureSpecimenLifecycle")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapGet("/specimens/{specimenId:int}/lifecycle-history", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                int specimenId,
                CancellationToken cancellationToken) =>
            (await repository.GetSpecimenLifecycleHistoryAsync(
                specimenId,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)) is { } history
                ? Results.Ok(history)
                : Results.NotFound())
            .WithName("GetProcedureSpecimenLifecycleHistory")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

        procedures.MapPost("/results", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                ProcedureResultCreateRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.CreateResultAsync(
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Procedure result could not be created from the supplied report and result details.")
                    : Results.Created($"/api/procedures/results/{mutation.Id}", mutation);
            })
            .WithName("CreateProcedureResult")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

        procedures.MapGet("/results/{resultId:int}/history", async (
                ProcedureRepository repository,
                HttpContext httpContext,
                int resultId,
                CancellationToken cancellationToken) =>
            (await repository.GetResultMutationHistoryAsync(
                resultId,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)) is { } history
                ? Results.Ok(history)
                : Results.NotFound())
            .WithName("GetProcedureResultMutationHistory")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

        procedures.MapPut("/results/{resultId:int}", async (
                ProcedureRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int resultId,
                ProcedureResultUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(
                    authRepository,
                    httpContext,
                    cancellationToken);
                var mutation = await repository.UpdateResultAsync(
                    resultId,
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("UpdateProcedureResult")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

        procedures.MapDelete("/orders/{orderId:int}", async (
                ProcedureRepository repository,
                int orderId,
                CancellationToken cancellationToken) =>
            {
                var deletion = await repository.DeleteOrderCascadeAsync(
                    orderId,
                    cancellationToken);
                return deletion switch
                {
                    ProcedureRepository.ProcedureOrderDeletionDisposition.NotFound =>
                        Results.NotFound(),
                    _ => Results.Conflict(new
                    {
                        error = "Laboratory orders are retained to preserve specimens, results, acknowledgements, and audit evidence. Use an approved cancellation workflow when one is available."
                    })
                };
            })
            .WithName("RejectProcedureOrderCascadeDeletion")
            .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));


        return procedures;
    }
}
