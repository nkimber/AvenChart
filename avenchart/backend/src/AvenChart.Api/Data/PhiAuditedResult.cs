// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
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
    string requiredPermission) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            await inner.ExecuteAsync(httpContext);
        }
        catch
        {
            await RecordAsync(StatusCodes.Status500InternalServerError);
            throw;
        }

        await RecordAsync(httpContext.Response.StatusCode);
    }

    private Task RecordAsync(int responseStatus) => auditRepository.RecordAccessDecisionAsync(
        session,
        httpMethod,
        endpointName,
        requiredPermission,
        authorized: true,
        responseStatus: responseStatus,
        CancellationToken.None);
}
