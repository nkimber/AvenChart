// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Data;

/// <summary>
/// Records the final HTTP status after a Minimal API result has executed.
/// </summary>
public sealed class PhiAuditedResult(
    IResult inner,
    PhiAuditRepository auditRepository,
    AuthSessionResponse session,
    string httpMethod,
    string endpointName,
    string requiredPermission,
    StaffAccessContext accessContext) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            await inner.ExecuteAsync(httpContext);
        }
        catch
        {
            await RecordAsync(StatusCodes.Status500InternalServerError, httpContext);
            throw;
        }

        await RecordAsync(httpContext.Response.StatusCode, httpContext);
    }

    private Task RecordAsync(int responseStatus, HttpContext httpContext) => auditRepository.RecordAccessDecisionAsync(
        session,
        httpMethod,
        endpointName,
        requiredPermission,
        authorized: true,
        responseStatus: responseStatus,
        accessContext: accessContext,
        cancellationToken: CancellationToken.None,
        resource: PhiAuditResourceContext.Get(httpContext));
}
