// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps facility-scoped referral and authorization operational queues.
/// </summary>
public static class ClinicalWorkflowEndpoints
{
    public static RouteGroupBuilder MapClinicalWorkflowEndpoints(this WebApplication app)
    {
        var clinicalWorkflows = app.MapGroup("/api/clinical-workflows").WithTags("Clinical Workflows");
        RequireAccessPermission(clinicalWorkflows, "patients", "med", "view");
        clinicalWorkflows.MapGet("/assignees", async (
                AuthorizationRepository repository,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAssigneesAsync(cancellationToken)))
            .WithName("GetClinicalWorkflowAssignees");
        clinicalWorkflows.MapGet("/referral-work-queue", async (
                ReferralRepository repository,
                HttpContext httpContext,
                string? status,
                string? assignedTo,
                bool? overdueOnly,
                string? query,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetWorkQueueAsync(
                        status,
                        assignedTo,
                        overdueOnly ?? false,
                        query,
                        limit ?? 25,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetReferralWorkQueue");

        clinicalWorkflows.MapGet("/authorization-work-queue", async (
                AuthorizationRepository repository,
                HttpContext httpContext,
                string? status,
                string? assignedTo,
                bool? overdueOnly,
                bool? expiringOnly,
                string? query,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetWorkQueueAsync(
                        status,
                        assignedTo,
                        overdueOnly ?? false,
                        expiringOnly ?? false,
                        query,
                        limit ?? 25,
                        RequireStaffAccessContext(httpContext).FacilityId,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetAuthorizationWorkQueue");

        return clinicalWorkflows;
    }
}
