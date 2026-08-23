// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Microsoft.AspNetCore.Mvc;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps inventory, controlled-substance, valuation, and reconciliation routes
/// so retained stock and attestation contracts remain one visible aggregate.
/// </summary>
public static class InventoryEndpoints
{
    public static RouteGroupBuilder MapInventoryEndpoints(this WebApplication app)
    {
        var inventory = app.MapGroup("/api/inventory").WithTags("Inventory");
        RequireAccessPermission(inventory, "inventory", "reporting", "view");

        inventory.MapGet("/", async (
                InventoryRepository repository,
                CancellationToken cancellationToken) =>
            {
                return Results.Ok(await repository.GetInventoryAsync(cancellationToken));
            })
            .WithName("GetInventory");

        inventory.MapGet("/cost-policies", async (InventoryCostPolicyRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
            .WithName("GetInventoryCostPolicies")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapGet("/receipt-cost-layers", async (int? lotId, int? limit, InventoryRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetReceiptCostLayersAsync(lotId, limit.GetValueOrDefault(50), cancellationToken)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReceiptCostLayers"] = [exception.Message] }); }
        })
            .WithName("GetInventoryReceiptCostLayers")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapGet("/receipt-cost-layers/{layerId:guid}/applications", async (Guid layerId, InventoryRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetReceiptCostLayerApplicationsAsync(layerId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReceiptCostLayer"] = [exception.Message] }); }
        })
            .WithName("GetInventoryReceiptCostLayerApplications")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapGet("/valuation-runs", async (int? limit, InventoryValuationRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetRunsAsync(limit ?? 30, cancellationToken)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryValuationRun"] = [exception.Message] }); }
        })
            .WithName("GetInventoryValuationRuns")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapGet("/valuation-runs/{runId:guid}", async (Guid runId, InventoryValuationRepository repository, CancellationToken cancellationToken) =>
        {
            var result = await repository.GetDetailAsync(runId, cancellationToken);
            return result is null ? Results.NotFound(new { error = "The inventory valuation run was not found." }) : Results.Ok(result);
        })
            .WithName("GetInventoryValuationRun")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapPost("/valuation-runs", async (InventoryValuationRunCreateRequest request, InventoryValuationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var result = await repository.CreateAsync(request, session.Username, cancellationToken);
                return Results.Created($"/api/inventory/valuation-runs/{result.Run.RunId}", result);
            }
            catch (InventoryValuationPolicyMissingException exception) { return Results.Conflict(new { error = exception.Message, code = "inventory_cost_policy_missing" }); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryValuationRun"] = [exception.Message] }); }
        })
            .WithName("CreateInventoryValuationRun")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/cost-policy-change-requests", async (InventoryCostPolicyChangeRequestCreateRequest request, InventoryCostPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/cost-policy-change-requests/{created.Request.RequestId}", created); } catch (InventoryCostPolicyChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryCostPolicy"] = [exception.Message] }); } })
            .WithName("CreateInventoryCostPolicyChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/cost-policy-change-requests/{requestId:guid}", async (Guid requestId, InventoryCostPolicyRepository repository, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
            .WithName("GetInventoryCostPolicyChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
            inventory.MapPost($"/cost-policy-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryCostPolicyChangeRequestDecisionRequest request, InventoryCostPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
            { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); } catch (InventoryCostPolicyChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
                .WithName($"TransitionInventoryCostPolicyChangeRequest{action}")
                .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/accounting-integration-decision", async (InventoryAccountingIntegrationRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
            .WithName("GetInventoryAccountingIntegrationDecision")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        inventory.MapPost("/accounting-integration-change-requests", async (InventoryAccountingIntegrationChangeRequestCreateRequest request, InventoryAccountingIntegrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/accounting-integration-change-requests/{created.Request.RequestId}", created); }
            catch (InventoryAccountingIntegrationConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryAccountingIntegration"] = [exception.Message] }); }
        })
            .WithName("CreateInventoryAccountingIntegrationChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapGet("/accounting-integration-change-requests/{requestId:guid}", async (Guid requestId, InventoryAccountingIntegrationRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
        })
            .WithName("GetInventoryAccountingIntegrationChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
            inventory.MapPost($"/accounting-integration-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryAccountingIntegrationChangeRequestDecisionRequest request, InventoryAccountingIntegrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); }
                catch (InventoryAccountingIntegrationConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
                catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
            })
                .WithName($"TransitionInventoryAccountingIntegrationChangeRequest{action}")
                .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapGet("/replenishment-policies", async (InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
            .WithName("GetInventoryReplenishmentPolicies")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        inventory.MapGet("/replenishment-recommendations", async (InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetRecommendationsAsync(cancellationToken)))
            .WithName("GetInventoryReplenishmentRecommendations")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        inventory.MapPost("/replenishment-policy-change-requests", async (InventoryReplenishmentPolicyChangeRequestCreateRequest request, InventoryReplenishmentPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/replenishment-policy-change-requests/{created.Request.RequestId}", created); }
            catch (InventoryReplenishmentPolicyConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReplenishmentPolicy"] = [exception.Message] }); }
        })
            .WithName("CreateInventoryReplenishmentPolicyChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapGet("/replenishment-policy-change-requests/{requestId:guid}", async (Guid requestId, InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
        })
            .WithName("GetInventoryReplenishmentPolicyChangeRequest")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
            inventory.MapPost($"/replenishment-policy-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryReplenishmentPolicyChangeRequestDecisionRequest request, InventoryReplenishmentPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); }
                catch (InventoryReplenishmentPolicyConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
                catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
            })
                .WithName($"TransitionInventoryReplenishmentPolicyChangeRequest{action}")
                .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapGet("/medication-catalog", async (InventoryRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetMedicationCatalogAsync(cancellationToken)))
            .WithName("GetInventoryMedicationCatalog")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapGet("/controlled-substances", async (InventoryRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetControlledSubstanceCatalogAsync(cancellationToken)))
            .WithName("GetInventoryControlledSubstanceCatalog")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapPost("/controlled-custody-movements", async (InventoryControlledCustodyMovementRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var movement = await repository.CreateControlledCustodyMovementAsync(request, session.Username, cancellationToken);
                return Results.Created($"/api/inventory/controlled-custody-movements/{movement.Event.EventId}", movement);
            }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyMovement"] = [exception.Message] });
            }
        })
            .WithName("CreateInventoryControlledCustodyMovement")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapPost("/controlled-custody-movement-attestations", async (InventoryControlledCustodyMovementRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var attestation = await repository.CreateControlledCustodyMovementAttestationAsync(request, session.Username, cancellationToken);
                return Results.Created($"/api/inventory/controlled-custody-movement-attestations/{attestation.AttestationId}", attestation);
            }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyAttestation"] = [exception.Message] }); }
        })
            .WithName("RequestInventoryControlledCustodyMovementAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapGet("/controlled-custody-movement-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            return Results.Ok(await repository.GetPendingControlledAttestationsAsync("custody_movement", session.Username, cancellationToken));
        })
            .WithName("GetPendingInventoryControlledCustodyMovementAttestations")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapPost("/controlled-custody-movement-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "custody_movement", session.Username, cancellationToken)); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyAttestation"] = [exception.Message] }); }
        })
            .WithName("ApproveInventoryControlledCustodyMovementAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapGet("/controlled-custody-lots/{lotId:int}/history", async (int lotId, InventoryRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetControlledCustodyLotHistoryAsync(lotId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
        })
            .WithName("GetInventoryControlledCustodyLotHistory")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapPost("/controlled-count-sessions", async (InventoryControlledCountSessionCreateRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var count = await repository.CreateControlledCountSessionAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-sessions/{count.SessionId}", count); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
            .WithName("CreateInventoryControlledCountSession")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/controlled-count-sessions/{sessionId:guid}", async (Guid sessionId, InventoryRepository repository, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetControlledCountSessionAsync(sessionId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
            .WithName("GetInventoryControlledCountSession")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapGet("/controlled-count-sessions", async (int? limit, InventoryRepository repository, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetControlledCountSessionsAsync(limit ?? 30, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
            .WithName("GetInventoryControlledCountSessions")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapPost("/controlled-count-sessions/{sessionId:guid}/submission-attestations", async (Guid sessionId, InventoryControlledCountSubmitRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var attestation = await repository.CreateControlledCountSubmissionAttestationAsync(sessionId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-attestations/{attestation.AttestationId}", attestation); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCountAttestation"] = [exception.Message] }); } })
            .WithName("RequestInventoryControlledCountSubmissionAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/controlled-count-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.GetPendingControlledAttestationsAsync("count_submit", session.Username, cancellationToken)); })
            .WithName("GetPendingInventoryControlledCountAttestations")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapPost("/controlled-count-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "count_submit", session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCountAttestation"] = [exception.Message] }); } })
            .WithName("ApproveInventoryControlledCountAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/controlled-count-sessions/{sessionId:guid}/submit", async (Guid sessionId, InventoryControlledCountSubmitRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitControlledCountSessionAsync(sessionId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
            .WithName("SubmitInventoryControlledCountSession")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPut("/controlled-count-discrepancies/{discrepancyId:guid}/investigation", async (Guid discrepancyId, InventoryControlledDiscrepancyInvestigationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.InvestigateControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
            .WithName("InvestigateInventoryControlledCountDiscrepancy")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/correction-attestations", async (Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var attestation = await repository.CreateControlledDiscrepancyCorrectionAttestationAsync(discrepancyId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-attestations/{attestation.AttestationId}", attestation); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancyAttestation"] = [exception.Message] }); } })
            .WithName("RequestInventoryControlledDiscrepancyCorrectionAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/controlled-count-discrepancy-correction-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.GetPendingControlledAttestationsAsync("discrepancy_correction", session.Username, cancellationToken)); })
            .WithName("GetPendingInventoryControlledDiscrepancyCorrectionAttestations")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

        inventory.MapPost("/controlled-count-discrepancy-correction-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "discrepancy_correction", session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancyAttestation"] = [exception.Message] }); } })
            .WithName("ApproveInventoryControlledDiscrepancyCorrectionAttestation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/corrections", async (Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var correction = await repository.CorrectControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-custody-movements/{correction.Event.EventId}", correction); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
            .WithName("CorrectInventoryControlledCountDiscrepancy")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/close", async (Guid discrepancyId, InventoryControlledDiscrepancyCloseRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CloseControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
            .WithName("CloseInventoryControlledCountDiscrepancy")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/controlled-locations", async (InventoryControlledLocationMutationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/inventory/controlled-locations", await repository.CreateControlledLocationAsync(request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledLocation"] = [exception.Message] }); } })
            .WithName("CreateInventoryControlledLocation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapPut("/items/{itemId:int}/controlled-classification", async (int itemId, InventoryControlledSubstanceClassificationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateControlledSubstanceClassificationAsync(itemId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledClassification"] = [exception.Message] }); } })
            .WithName("UpdateInventoryControlledSubstanceClassification")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapGet("/items/{itemId:int}/controlled-classification/history", async (int itemId, InventoryRepository repository, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetControlledSubstanceClassificationHistoryAsync(itemId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
            .WithName("GetInventoryControlledSubstanceClassificationHistory")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapPut("/items/{itemId:int}/medication-link", async (
                int itemId,
                InventoryMedicationLinkUpdateRequest request,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var link = await repository.UpdateMedicationLinkAsync(itemId, request, session.Username, cancellationToken);
                    return link is null ? Results.NotFound() : Results.Ok(link);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryMedicationLink"] = [exception.Message] });
                }
            })
            .WithName("UpdateInventoryMedicationLink")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapGet("/items/{itemId:int}/medication-link/history", async (int itemId, InventoryRepository repository, CancellationToken cancellationToken) =>
        { try { return Results.Ok(await repository.GetMedicationLinkHistoryAsync(itemId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
            .WithName("GetInventoryMedicationLinkHistory")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapDelete("/items/{itemId:int}/medication-link", async (int itemId, [FromBody] InventoryMedicationLinkUnlinkRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UnlinkMedicationAsync(itemId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryMedicationLink"] = [exception.Message] }); } })
            .WithName("UnlinkInventoryMedicationLink")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapPost("/prescription-dispensations", async (
                InventoryPrescriptionDispenseRequest request,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var dispense = await repository.DispensePrescriptionAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/inventory/prescription-dispensations/{dispense.Sale.SaleId}", dispense);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPrescriptionDispense"] = [exception.Message] });
                }
            })
            .WithName("DispenseInventoryPrescription")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

        inventory.MapPost("/transactions", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryTransactionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.CreateTransactionAsync(request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/transactions/{mutation.Transaction.TransactionId}", mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = [exception.Message]
                    });
                }
            })
            .WithName("CreateInventoryTransaction")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapPost("/transfers", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryTransferCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.CreateTransferAsync(request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/transfers/{mutation.TransferId}", mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inventoryTransfer"] = [exception.Message]
                    });
                }
            })
            .WithName("CreateInventoryTransfer")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "transfers", "write"));

        inventory.MapGet("/vendors", async (
                InventoryRepository repository,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetVendorsAsync(cancellationToken)))
            .WithName("GetInventoryVendors")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        inventory.MapPost("/vendors", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryVendorCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var vendor = await repository.CreateVendorAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/inventory/vendors/{vendor.VendorId}", vendor);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryVendor"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryVendor")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapGet("/purchase-requisitions", async (InventoryRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetPurchaseRequisitionsAsync(cancellationToken)))
            .WithName("GetInventoryPurchaseRequisitions")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

        inventory.MapPost("/purchase-requisitions", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryPurchaseRequisitionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var requisition = await repository.CreatePurchaseRequisitionAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/inventory/purchase-requisitions/{requisition!.RequisitionId}", requisition);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryPurchaseRequisition")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapPost("/purchase-requisitions/{requisitionId:guid}/submit", async (
                Guid requisitionId,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var requisition = await repository.SubmitPurchaseRequisitionAsync(requisitionId, session.Username, cancellationToken);
                    return requisition is null ? Results.NotFound() : Results.Ok(requisition);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
                }
            })
            .WithName("SubmitInventoryPurchaseRequisition")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapPost("/purchase-requisitions/{requisitionId:guid}/decisions/{decision}", async (
                Guid requisitionId,
                string decision,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryPurchaseRequisitionDecisionRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase) && !string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var requisition = await repository.DecidePurchaseRequisitionAsync(requisitionId, string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase), request, session.Username, cancellationToken);
                    return requisition is null ? Results.NotFound() : Results.Ok(requisition);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
                }
            })
            .WithName("DecideInventoryPurchaseRequisition")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapPost("/purchase-receipts", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryPurchaseReceiptCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var receipt = await repository.CreatePurchaseReceiptAsync(request, session.Username, cancellationToken);
                    return Results.Created($"/api/inventory/purchase-receipts/{receipt.ReceiptId}", receipt);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseReceipt"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryPurchaseReceipt")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapPost("/returns", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryTransactionCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.CreateTransactionAsync(request with { TransactionType = "return" }, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/returns/{mutation.Transaction.TransactionId}", mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReturn"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryReturn")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

        inventory.MapPost("/patient-sales", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryPatientSaleCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var sale = await repository.CreatePatientSaleAsync(request, session.Username, cancellationToken);
                    return sale is null ? Results.NotFound() : Results.Created($"/api/inventory/patient-sales/{sale.SaleId}", sale);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPatientSale"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryPatientSale")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

        inventory.MapPost("/patient-sales/allocate", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, InventoryPatientSaleAllocationCreateRequest request, CancellationToken cancellationToken) =>
            {
                try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/inventory/patient-sales/allocate", await repository.CreatePatientSaleAllocationAsync(request, session.Username, cancellationToken)); }
                catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPatientSaleAllocation"] = [exception.Message] }); }
            })
            .WithName("AllocateInventoryPatientSale")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

        inventory.MapPut("/lots/{lotId:int}", async (
                int lotId,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryLotMetadataUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateLotMetadataAsync(lotId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryLot"] = [exception.Message] });
                }
            })
            .WithName("UpdateInventoryLotMetadata")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

        inventory.MapGet("/lots/{lotId:int}/metadata-history", async (
                int lotId,
                InventoryRepository repository,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetLotMetadataHistoryAsync(lotId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetInventoryLotMetadataHistory")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

        inventory.MapPost("/lots/{lotId:int}/destructions", async (
                int lotId,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryLotDestructionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var destruction = await repository.DestroyLotAsync(lotId, request, session.Username, cancellationToken);
                    return destruction is null
                        ? Results.NotFound()
                        : Results.Created($"/api/inventory/lots/{lotId}/destructions/{destruction.DestructionId}", destruction);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryLotDestruction"] = [exception.Message] });
                }
            })
            .WithName("DestroyInventoryLot")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "destruction", "write"));

        inventory.MapPost("/lots/{lotId:int}/expiry-dispositions", async (
                int lotId,
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryExpiryDispositionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var disposition = await repository.CreateExpiryDispositionAsync(lotId, request, session.Username, cancellationToken);
                    return disposition is null ? Results.NotFound() : Results.Created($"/api/inventory/lots/{lotId}/expiry-dispositions/{disposition.DispositionId}", disposition);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryExpiryDisposition"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryExpiryDisposition")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "destruction", "write"));

        inventory.MapPost("/count-reconciliations", async (
                InventoryRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                InventoryCountReconciliationCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var reconciliation = await repository.CreateCountReconciliationAsync(request, session.Username, cancellationToken);
                    return reconciliation is null
                        ? Results.NotFound()
                        : Results.Created($"/api/inventory/count-reconciliations/{reconciliation.ReconciliationId}", reconciliation);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryCount"] = [exception.Message] });
                }
            })
            .WithName("CreateInventoryCountReconciliation")
            .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

        inventory.MapGet("/activity", async (
                InventoryRepository repository,
                DateOnly? from,
                DateOnly? to,
                int? facilityId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetActivityReportAsync(from, to, facilityId, cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inventoryActivity"] = [exception.Message]
                    });
                }
            })
            .WithName("GetInventoryActivityReport");

        inventory.MapGet("/activity/export", async (
                InventoryRepository repository,
                DateOnly? from,
                DateOnly? to,
                int? facilityId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var csv = await repository.GetActivityReportCsvAsync(from, to, facilityId, cancellationToken);
                    return Results.File(Encoding.UTF8.GetBytes(csv), contentType: "text/csv", fileDownloadName: "avenchart-inventory-activity.csv");
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["inventoryActivity"] = [exception.Message]
                    });
                }
            })
            .WithName("ExportInventoryActivityReport");

        app.MapBillingEndpoints();

        return inventory;
    }
}
