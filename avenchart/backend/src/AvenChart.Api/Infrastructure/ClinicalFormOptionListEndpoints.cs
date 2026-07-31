// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

public static class ClinicalFormOptionListEndpoints
{
    public static RouteGroupBuilder MapClinicalFormOptionListEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/option-lists", async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                IStaffIdentityAdapter identityAdapter,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var identity = await identityAdapter.ResolveAsync(
                    httpContext,
                    cancellationToken);
                if (!identity.Authenticated)
                {
                    return Results.Unauthorized();
                }

                if (!await authRepository.HasAccessPermissionAsync(
                        identity.Username,
                        "admin",
                        "acl",
                        "write",
                        cancellationToken))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                return Results.Ok(
                    await repository.ListOptionListsAsync(cancellationToken));
            })
            .WithName("GetClinicalFormOptionLists");

        return group;
    }
}
