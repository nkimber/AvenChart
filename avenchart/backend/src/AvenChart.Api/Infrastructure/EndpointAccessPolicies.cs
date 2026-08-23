// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Owns the staff request boundary shared by feature endpoint groups. Keeping
/// it outside host composition makes the authentication, permission, selected
/// facility/purpose, and executed-outcome audit contract explicit and reusable.
/// </summary>
public static class EndpointAccessPolicies
{
    public static StaffAccessContext RequireStaffAccessContext(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(StaffAccessContextService.HttpContextItemKey, out var value)
            && value is StaffAccessContext accessContext
            ? accessContext
            : throw new InvalidOperationException("The endpoint requires a resolved staff access context.");

    public static Task<bool> CanAccessSelectedFacilityPatientAsync(
        HttpContext httpContext,
        string? patientId,
        CancellationToken cancellationToken) =>
        httpContext.RequestServices.GetRequiredService<StaffAccessContextService>()
            .CanAccessPatientAsync(
                patientId,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);

    public static void RequireAccessPermission(
        RouteGroupBuilder group,
        string sectionValue,
        string permissionValue,
        string returnValue) =>
        group.AddEndpointFilter(AccessPermissionFilter(sectionValue, permissionValue, returnValue));

    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> AccessPermissionFilter(
        string sectionValue,
        string permissionValue,
        string returnValue)
    {
        var policy = AuthorizationPolicyCatalog.Require(
            sectionValue,
            permissionValue,
            returnValue);
        return async (context, next) =>
        {
            var repository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
            var phiAuditRepository = context.HttpContext.RequestServices.GetRequiredService<PhiAuditRepository>();
            var accessContextService = context.HttpContext.RequestServices.GetRequiredService<StaffAccessContextService>();
            var session = await GetSessionFromHeaderAsync(repository, context.HttpContext, context.HttpContext.RequestAborted);
            if (!session.Authenticated)
            {
                return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
            }

            var authorized = await repository.HasAccessPermissionAsync(
                session.Username,
                sectionValue,
                permissionValue,
                returnValue,
                context.HttpContext.RequestAborted);
            if (!authorized)
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                    authorized: false,
                    responseStatus: StatusCodes.Status403Forbidden,
                    accessContext: null,
                    cancellationToken: context.HttpContext.RequestAborted);
                return Results.Json(new AuthAuthorizationFailureResponse(
                    Authenticated: true,
                    Authorized: false,
                    SessionId: session.SessionId,
                    Username: session.Username,
                    Role: session.Role,
                    RequiredSection: sectionValue,
                    RequiredPermission: permissionValue,
                    RequiredReturnValue: returnValue,
                    FailureReason: $"User '{session.Username}' is not authorized for {sectionValue}:{permissionValue} {returnValue}.",
                    SessionSource: session.SessionSource), statusCode: StatusCodes.Status403Forbidden);
            }

            var accessContext = await accessContextService.ResolveAsync(
                session,
                context.HttpContext,
                context.HttpContext.RequestAborted);
            if (!accessContext.Authorized)
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                    authorized: false,
                    responseStatus: StatusCodes.Status403Forbidden,
                    accessContext: accessContext.Context,
                    cancellationToken: context.HttpContext.RequestAborted);
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Staff access context is not authorized",
                    detail: accessContext.FailureReason,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "staff_access_context_required",
                        ["facilityHeader"] = StaffAccessContextService.FacilityHeader,
                        ["purposeHeader"] = StaffAccessContextService.PurposeHeader
                    });
            }

            context.HttpContext.Items[StaffAccessContextService.HttpContextItemKey] = accessContext.Context!;

            try
            {
                var result = await next(context);
                var endpointName = context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched";
                var requiredPermission = $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}";
                if (result is IResult httpResult)
                {
                    return new PhiAuditedResult(
                        httpResult,
                        phiAuditRepository,
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        requiredPermission,
                        accessContext.Context!);
                }

                context.HttpContext.Response.OnStarting(async () =>
                {
                    await phiAuditRepository.RecordAccessDecisionAsync(
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        requiredPermission,
                        authorized: true,
                        responseStatus: context.HttpContext.Response.StatusCode,
                        accessContext: accessContext.Context,
                        cancellationToken: CancellationToken.None);
                });
                return result;
            }
            catch
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                    authorized: true,
                    responseStatus: StatusCodes.Status500InternalServerError,
                    accessContext: accessContext.Context,
                    cancellationToken: context.HttpContext.RequestAborted);
                throw;
            }
        };
    }

    /// <summary>
    /// Permits an encounter signature by an administrator with the <c>auth_a</c>
    /// capability or by the clinician assigned to that encounter with the
    /// narrower <c>auth</c> capability.  The own-encounter branch includes the
    /// selected facility in its resource query; it never turns an ACL grant
    /// into broad facility access.
    /// </summary>
    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> EncounterSigningPermissionFilter()
    {
        var anyEncounterPolicy = AuthorizationPolicyCatalog.Require("encounters", "auth_a", "write");
        var ownEncounterPolicy = AuthorizationPolicyCatalog.Require("encounters", "auth", "write");
        var requiredPermission = $"{anyEncounterPolicy.PolicyId}|{ownEncounterPolicy.PolicyId}.assigned-provider";

        return async (context, next) =>
        {
            var repository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
            var phiAuditRepository = context.HttpContext.RequestServices.GetRequiredService<PhiAuditRepository>();
            var accessContextService = context.HttpContext.RequestServices.GetRequiredService<StaffAccessContextService>();
            var cancellationToken = context.HttpContext.RequestAborted;
            var session = await GetSessionFromHeaderAsync(repository, context.HttpContext, cancellationToken);
            if (!session.Authenticated)
            {
                return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
            }

            var accessContext = await accessContextService.ResolveAsync(session, context.HttpContext, cancellationToken);
            if (!accessContext.Authorized)
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    requiredPermission,
                    authorized: false,
                    responseStatus: StatusCodes.Status403Forbidden,
                    accessContext: accessContext.Context,
                    cancellationToken: cancellationToken);
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Staff access context is not authorized",
                    detail: accessContext.FailureReason,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "staff_access_context_required",
                        ["facilityHeader"] = StaffAccessContextService.FacilityHeader,
                        ["purposeHeader"] = StaffAccessContextService.PurposeHeader
                    });
            }

            var maySignAnyEncounter = await repository.HasAccessPermissionAsync(
                session.Username,
                "encounters",
                "auth_a",
                "write",
                cancellationToken);
            var maySignAssignedEncounter = false;
            if (!maySignAnyEncounter
                && context.HttpContext.Request.RouteValues.TryGetValue("encounter", out var encounterRouteValue)
                && int.TryParse(encounterRouteValue?.ToString(), out var encounter))
            {
                var hasOwnEncounterCapability = await repository.HasAccessPermissionAsync(
                    session.Username,
                    "encounters",
                    "auth",
                    "write",
                    cancellationToken);
                maySignAssignedEncounter = hasOwnEncounterCapability
                    && await accessContextService.CanAccessAssignedEncounterAsync(
                        encounter,
                        session.Username,
                        accessContext.Context!.FacilityId,
                        cancellationToken);
            }

            if (!maySignAnyEncounter && !maySignAssignedEncounter)
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    requiredPermission,
                    authorized: false,
                    responseStatus: StatusCodes.Status403Forbidden,
                    accessContext: accessContext.Context,
                    cancellationToken: cancellationToken);
                return Results.Json(new AuthAuthorizationFailureResponse(
                    Authenticated: true,
                    Authorized: false,
                    SessionId: session.SessionId,
                    Username: session.Username,
                    Role: session.Role,
                    RequiredSection: "encounters",
                    RequiredPermission: "auth_a or assigned-provider auth",
                    RequiredReturnValue: "write",
                    FailureReason: $"User '{session.Username}' is not authorized to sign this encounter.",
                    SessionSource: session.SessionSource), statusCode: StatusCodes.Status403Forbidden);
            }

            context.HttpContext.Items[StaffAccessContextService.HttpContextItemKey] = accessContext.Context!;
            try
            {
                var result = await next(context);
                var endpointName = context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched";
                if (result is IResult httpResult)
                {
                    return new PhiAuditedResult(
                        httpResult,
                        phiAuditRepository,
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        requiredPermission,
                        accessContext.Context!);
                }

                context.HttpContext.Response.OnStarting(async () =>
                {
                    await phiAuditRepository.RecordAccessDecisionAsync(
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        requiredPermission,
                        authorized: true,
                        responseStatus: context.HttpContext.Response.StatusCode,
                        accessContext: accessContext.Context,
                        cancellationToken: CancellationToken.None);
                });
                return result;
            }
            catch
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    requiredPermission,
                    authorized: true,
                    responseStatus: StatusCodes.Status500InternalServerError,
                    accessContext: accessContext.Context,
                    cancellationToken: cancellationToken);
                throw;
            }
        };
    }

    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> StaffAccessContextFilter(
        string requiredPermission)
    {
        return async (context, next) =>
        {
            var repository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
            var phiAuditRepository = context.HttpContext.RequestServices.GetRequiredService<PhiAuditRepository>();
            var accessContextService = context.HttpContext.RequestServices.GetRequiredService<StaffAccessContextService>();
            var session = await GetSessionFromHeaderAsync(repository, context.HttpContext, context.HttpContext.RequestAborted);
            if (!session.Authenticated)
            {
                return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
            }

            var accessContext = await accessContextService.ResolveAsync(
                session,
                context.HttpContext,
                context.HttpContext.RequestAborted);
            if (!accessContext.Authorized)
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    $"{requiredPermission}@{AuthorizationPolicyCatalog.Revision}",
                    authorized: false,
                    responseStatus: StatusCodes.Status403Forbidden,
                    accessContext: null,
                    cancellationToken: context.HttpContext.RequestAborted);
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Staff access context is not authorized",
                    detail: accessContext.FailureReason,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "staff_access_context_required",
                        ["facilityHeader"] = StaffAccessContextService.FacilityHeader,
                        ["purposeHeader"] = StaffAccessContextService.PurposeHeader
                    });
            }

            context.HttpContext.Items[StaffAccessContextService.HttpContextItemKey] = accessContext.Context!;
            try
            {
                var result = await next(context);
                var endpointName = context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched";
                var qualifiedPermission = $"{requiredPermission}@{AuthorizationPolicyCatalog.Revision}";
                if (result is IResult httpResult)
                {
                    return new PhiAuditedResult(
                        httpResult,
                        phiAuditRepository,
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        qualifiedPermission,
                        accessContext.Context!);
                }

                context.HttpContext.Response.OnStarting(async () =>
                {
                    await phiAuditRepository.RecordAccessDecisionAsync(
                        session,
                        context.HttpContext.Request.Method,
                        endpointName,
                        qualifiedPermission,
                        authorized: true,
                        responseStatus: context.HttpContext.Response.StatusCode,
                        accessContext: accessContext.Context,
                        cancellationToken: CancellationToken.None);
                });
                return result;
            }
            catch
            {
                await phiAuditRepository.RecordAccessDecisionAsync(
                    session,
                    context.HttpContext.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                    $"{requiredPermission}@{AuthorizationPolicyCatalog.Revision}",
                    authorized: true,
                    responseStatus: StatusCodes.Status500InternalServerError,
                    accessContext: accessContext.Context,
                    cancellationToken: context.HttpContext.RequestAborted);
                throw;
            }
        };
    }

    public static async Task<AuthSessionResponse> GetSessionFromHeaderAsync(
        AuthRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        _ = repository;
        var adapter = httpContext.RequestServices.GetRequiredService<IStaffIdentityAdapter>();
        return await adapter.ResolveAsync(httpContext, cancellationToken);
    }
}
