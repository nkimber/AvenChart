// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
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

    /// <summary>
    /// Enforces selected-facility ownership for direct encounter, appointment,
    /// and document routes, while recording the resource context used by the
    /// enclosing PHI audit filter.
    /// </summary>
    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> ClinicalResourceFacilityScopeFilter()
    {
        return async (context, next) =>
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            var accessContext = RequireStaffAccessContext(context.HttpContext);
            var accessContextService = context.HttpContext.RequestServices
                .GetRequiredService<StaffAccessContextService>();

            if (routeValues.TryGetValue("documentId", out var documentRouteValue)
                && int.TryParse(documentRouteValue?.ToString(), out var documentId))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "Document", documentId.ToString());
                var allowed = await accessContextService.CanAccessDocumentAsync(
                    documentId,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("encounter", out var encounterRouteValue)
                && int.TryParse(encounterRouteValue?.ToString(), out var encounter))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "Encounter", encounter.ToString());
                var allowed = await accessContextService.CanAccessEncounterAsync(
                    encounter,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("appointmentId", out var appointmentRouteValue))
            {
                var appointmentId = appointmentRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "Appointment", appointmentId);
                var allowed = await accessContextService.CanAccessAppointmentAsync(
                    appointmentId,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
                return allowed ? await next(context) : Results.NotFound();
            }

            return await next(context);
        };
    }

    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> MessageFacilityScopeFilter()
    {
        return async (context, next) =>
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            var accessContext = RequireStaffAccessContext(context.HttpContext);
            var accessContextService = context.HttpContext.RequestServices
                .GetRequiredService<StaffAccessContextService>();

            if (routeValues.TryGetValue("patientId", out var patientRouteValue))
            {
                var patientId = patientRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "Patient", patientId);
                var allowed = await accessContextService.CanAccessPatientAsync(
                    patientId,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("messageId", out var messageRouteValue))
            {
                var messageId = messageRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "Message", messageId);
                var allowed = await accessContextService.CanAccessMessageAsync(
                    messageId,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
                return allowed ? await next(context) : Results.NotFound();
            }

            return await next(context);
        };
    }

    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> ClinicalListFacilityScopeFilter()
    {
        return async (context, next) =>
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            var accessContext = RequireStaffAccessContext(context.HttpContext);
            var accessContextService = context.HttpContext.RequestServices
                .GetRequiredService<StaffAccessContextService>();
            var cancellationToken = context.HttpContext.RequestAborted;

            if (routeValues.TryGetValue("patientId", out var patientRouteValue))
            {
                var patientId = patientRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "Patient", patientId);
                var allowed = await accessContextService.CanAccessPatientAsync(
                    patientId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            (string? ResourceType, string? ResourceId) clinicalResource = routeValues.TryGetValue("allergyId", out var allergyId) ? ("Allergy", allergyId?.ToString())
                : routeValues.TryGetValue("problemId", out var problemId) ? ("Problem", problemId?.ToString())
                : routeValues.TryGetValue("medicationId", out var medicationId) ? ("Medication", medicationId?.ToString())
                : routeValues.TryGetValue("prescriptionId", out var prescriptionId) ? ("Prescription", prescriptionId?.ToString())
                : routeValues.TryGetValue("immunizationId", out var immunizationId) ? ("Immunization", immunizationId?.ToString())
                : routeValues.TryGetValue("immunizationKey", out var immunizationKey) ? ("ImmunizationKey", immunizationKey?.ToString())
                : (null, null);
            if (clinicalResource.ResourceType is not null)
            {
                PhiAuditResourceContext.Set(context.HttpContext, clinicalResource.ResourceType, clinicalResource.ResourceId);
                var allowed = await accessContextService.CanAccessClinicalListResourceAsync(
                    clinicalResource.ResourceType,
                    clinicalResource.ResourceId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("messageId", out var messageRouteValue))
            {
                var messageId = messageRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "PrescriptionRefillRequest", messageId);
                var allowed = await accessContextService.CanAccessPrescriptionRefillRequestAsync(
                    messageId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            return await next(context);
        };
    }

    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> ProcedureFacilityScopeFilter()
    {
        return async (context, next) =>
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            var accessContext = RequireStaffAccessContext(context.HttpContext);
            var accessContextService = context.HttpContext.RequestServices
                .GetRequiredService<StaffAccessContextService>();
            var cancellationToken = context.HttpContext.RequestAborted;

            if (routeValues.TryGetValue("patientId", out var patientRouteValue))
            {
                var patientId = patientRouteValue?.ToString();
                PhiAuditResourceContext.Set(context.HttpContext, "Patient", patientId);
                var allowed = await accessContextService.CanAccessPatientAsync(
                    patientId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("orderId", out var orderRouteValue)
                && int.TryParse(orderRouteValue?.ToString(), out var orderId))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryOrder", orderId.ToString(CultureInfo.InvariantCulture));
                var allowed = await accessContextService.CanAccessLaboratoryOrderAsync(
                    orderId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("reportId", out var reportRouteValue)
                && int.TryParse(reportRouteValue?.ToString(), out var reportId))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryReport", reportId.ToString(CultureInfo.InvariantCulture));
                var allowed = await accessContextService.CanAccessLaboratoryReportAsync(
                    reportId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("resultId", out var resultRouteValue)
                && int.TryParse(resultRouteValue?.ToString(), out var resultId))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryResult", resultId.ToString(CultureInfo.InvariantCulture));
                var allowed = await accessContextService.CanAccessLaboratoryResultAsync(
                    resultId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            if (routeValues.TryGetValue("specimenId", out var specimenRouteValue)
                && int.TryParse(specimenRouteValue?.ToString(), out var specimenId))
            {
                PhiAuditResourceContext.Set(context.HttpContext, "LaboratorySpecimen", specimenId.ToString(CultureInfo.InvariantCulture));
                var allowed = await accessContextService.CanAccessLaboratorySpecimenAsync(
                    specimenId,
                    accessContext.FacilityId,
                    cancellationToken);
                return allowed ? await next(context) : Results.NotFound();
            }

            return await next(context);
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
