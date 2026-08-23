// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Microsoft.Extensions.Options;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected billing, claims, payments, statements, and retained financial
/// evidence operations without coupling the aggregate to host composition.
/// </summary>
public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this WebApplication app)
    {
        var billing = app.MapGroup("/api/billing").WithTags("Billing");
        RequireAccessPermission(billing, "acct", "bill", "view");

        billing.MapGet("/statements/batch", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var statementBatch = await repository.GetStatementBatchAsync(limit ?? 10, cancellationToken);
                return Results.Ok(statementBatch);
            })
            .WithName("GetBillingStatementBatch");

        billing.MapGet("/statements/batch/package.zip", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var package = await repository.GetStatementBatchPackageAsync(limit ?? 10, cancellationToken);
                return Results.File(
                    package.Content,
                    contentType: "application/zip",
                    fileDownloadName: package.FileName);
            })
            .WithName("DownloadBillingStatementBatchPackage");

        billing.MapPost("/statements/batch/delivery-manifest", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var delivery = await repository.PrepareStatementBatchDeliveryAsync(limit ?? 10, cancellationToken);
                return Results.Ok(delivery);
            })
            .WithName("PrepareBillingStatementBatchDeliveryManifest");

        billing.MapPost("/statements/batch/dispatch", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var dispatch = await repository.DispatchStatementBatchDeliveryAsync(limit ?? 10, cancellationToken);
                return Results.Ok(dispatch);
            })
            .WithName("DispatchBillingStatementBatchDelivery")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapGet("/statements/batch/dispatch-history", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetStatementDeliveryAuditHistoryAsync(limit ?? 10, cancellationToken);
                return Results.Ok(history);
            })
            .WithName("GetBillingStatementDeliveryAuditHistory");

        billing.MapPost("/statements/batch/portal-delivery", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var delivery = await repository.DeliverStatementBatchToPortalAsync(limit ?? 10, cancellationToken);
                return Results.Ok(delivery);
            })
            .WithName("DeliverBillingStatementBatchToPortal")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/statements/batch/email-outbox", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var outbox = await repository.QueueStatementBatchEmailOutboxAsync(limit ?? 10, cancellationToken);
                return Results.Ok(outbox);
            })
            .WithName("QueueBillingStatementBatchEmailOutbox")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapGet("/collections/work-queue", async (
                BillingRepository repository,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var workQueue = await repository.GetCollectionsWorkQueueAsync(limit ?? 10, cancellationToken);
                return Results.Ok(workQueue);
            })
            .WithName("GetBillingCollectionsWorkQueue");

        billing.MapPost("/collections/follow-ups", async (
                BillingRepository repository,
                CollectionsFollowUpCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateCollectionsFollowUpAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Collections follow-up could not be created from the supplied patient and account state.")
                    : Results.Created($"/api/messages/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingCollectionsFollowUp")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapGet("/{patientId}", async (
                BillingRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                var patientBilling = await repository.GetForPatientAsync(patientId, cancellationToken);
                return patientBilling is null ? Results.NotFound() : Results.Ok(patientBilling);
            })
            .WithName("GetBillingForPatient");

        billing.MapGet("/{patientId}/statement.pdf", async (
                BillingRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                var export = await repository.GetStatementPdfAsync(patientId, cancellationToken);
                return export is null
                    ? Results.NotFound()
                    : Results.File(
                        export.Value.Content,
                        contentType: "application/pdf",
                        fileDownloadName: export.Value.FileName);
            })
            .WithName("DownloadBillingStatementPdf");

        billing.MapGet("/charge-templates/{templateId}", (
                BillingRepository repository,
                string templateId) =>
            {
                var template = repository.GetChargeTemplate(templateId);
                return template is null ? Results.NotFound() : Results.Ok(template);
            })
            .WithName("GetBillingChargeTemplate")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "view"));

        billing.MapPost("/lines", async (
                BillingRepository repository,
                BillingLineCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.CreateLineAsync(request, cancellationToken);
                    return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/lines/{mutation.Id}", mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("CreateBillingLine")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPut("/lines/{billingLineId}", async (
                BillingRepository repository,
                string billingLineId,
                BillingLineUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.UpdateLineAsync(billingLineId, request, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("UpdateBillingLine")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPut("/lines/{billingLineId}/status", async (
                BillingRepository repository,
                string billingLineId,
                BillingLineStatusUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var mutation = await repository.UpdateLineStatusAsync(billingLineId, request, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (EncounterLockConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
                }
            })
            .WithName("UpdateBillingLineStatus")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapDelete("/lines/{billingLineId}", (string billingLineId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Billing-line deletion is not available",
                    detail: "Financial evidence is retained. Use the line-status workflow to deactivate a line instead."))
            .WithName("RetireBillingLineDeletion")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims", async (
                BillingRepository repository,
                BillingClaimCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateClaimAsync(request, cancellationToken);
                return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/claims/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingClaimStatus")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPut("/claims/{claimId}/status", async (
                BillingRepository repository,
                string claimId,
                BillingClaimStatusUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.UpdateClaimStatusAsync(claimId, request, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("UpdateBillingClaimStatus")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/scrub", async (
                BillingRepository repository,
                string claimId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.ScrubClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("ScrubBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/generate", async (
                BillingRepository repository,
                string claimId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.GenerateClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("GenerateBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/resubmit", async (
                BillingRepository repository,
                string claimId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.ResubmitClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("ResubmitBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/deny", async (
                BillingRepository repository,
                string claimId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.DenyClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("DenyBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/clear", async (
                BillingRepository repository,
                string claimId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.ClearClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("ClearBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/claims/{claimId}/adjudicate", async (
                BillingRepository repository,
                string claimId,
                IOptions<RuntimeSafetyOptions> runtimeSafety,
                CancellationToken cancellationToken) =>
            {
                if (RuntimeSafetyPolicy.GetSyntheticFinancialMutationBlocker(runtimeSafety.Value) is { } blocker)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Generated claim adjudication is disabled",
                        detail: blocker);
                }

                var mutation = await repository.AdjudicateClaimAsync(claimId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("AdjudicateBillingClaim")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapDelete("/claims/{claimId}", (string claimId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Claim deletion is not available",
                    detail: "Financial evidence is retained. Use a governed claim-status transition instead."))
            .WithName("RetireBillingClaimDeletion")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/payments/patient-payments", async (
                BillingRepository repository,
                BillingPatientPaymentCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreatePatientPaymentAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Patient payment could not be posted for the supplied patient and encounter.")
                    : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingPatientPayment")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/payments/patient-refunds", async (
                BillingRepository repository,
                BillingPatientRefundCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreatePatientRefundAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Patient refund could not be posted for the supplied patient and encounter.")
                    : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingPatientRefund")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/payments/insurance-payments", async (
                BillingRepository repository,
                BillingInsurancePaymentCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateInsurancePaymentAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Insurance payment could not be posted for the supplied patient, encounter, and payer.")
                    : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingInsurancePayment")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/payments/insurance-reversals", async (
                BillingRepository repository,
                BillingInsuranceReversalCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateInsuranceReversalAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Insurance reversal could not be posted for the supplied patient, encounter, and payer.")
                    : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingInsuranceReversal")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/payments/adjustment-reversals", async (
                BillingRepository repository,
                BillingAdjustmentReversalCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateAdjustmentReversalAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Adjustment reversal could not be posted for the supplied patient, encounter, and payer.")
                    : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
            })
            .WithName("CreateBillingAdjustmentReversal")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapPost("/eob-batches/import", async (
                BillingRepository repository,
                BillingEobBatchImportRequest request,
                IOptions<RuntimeSafetyOptions> runtimeSafety,
                CancellationToken cancellationToken) =>
            {
                if (RuntimeSafetyPolicy.GetSyntheticFinancialMutationBlocker(runtimeSafety.Value) is { } blocker)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Generated EOB import is disabled",
                        detail: blocker);
                }

                var mutation = await repository.ImportEobBatchAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("EOB batch could not be imported for the supplied patient.")
                    : Results.Created("/api/billing/eob-batches/import", mutation);
            })
            .WithName("ImportBillingEobBatch")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapGet("/payments/{activityId}/receipt.pdf", async (
                BillingRepository repository,
                string activityId,
                CancellationToken cancellationToken) =>
            {
                var export = await repository.GetPaymentReceiptPdfAsync(activityId, cancellationToken);
                return export is null
                    ? Results.NotFound()
                    : Results.File(
                        export.Value.Content,
                        contentType: "application/pdf",
                        fileDownloadName: export.Value.FileName);
            })
            .WithName("DownloadBillingPaymentReceiptPdf");

        billing.MapPut("/payments/{activityId}/void", async (
                BillingRepository repository,
                string activityId,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.VoidPaymentAsync(activityId, cancellationToken);
                return mutation is null ? Results.NotFound() : Results.Ok(mutation);
            })
            .WithName("VoidBillingPaymentPosting")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

        billing.MapDelete("/payments/{activityId}", (string activityId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Payment deletion is not available",
                    detail: "Financial evidence is retained. Use the payment void workflow instead."))
            .WithName("RetireBillingPaymentDeletion")
            .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));


        return billing;
    }
}
