// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;

namespace AvenChart.Api.Security;

public interface IStaffIdentityAdapter
{
    string AdapterId { get; }

    Task<AuthSessionResponse> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

public sealed class LocalDevelopmentStaffIdentityAdapter(
    AuthRepository authRepository) : IStaffIdentityAdapter
{
    public const string Id = "local-database-staff-session";

    public string AdapterId => Id;

    public async Task<AuthSessionResponse> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var header = httpContext.Request.Headers["X-Legacy EHR-Session"].ToString();
        if (!Guid.TryParse(header, out var sessionId))
        {
            return MissingSession();
        }

        return await authRepository.GetCurrentSessionAsync(
            sessionId,
            cancellationToken);
    }

    private static AuthSessionResponse MissingSession() => new(
        Authenticated: false,
        SessionId: null,
        Username: string.Empty,
        DisplayName: string.Empty,
        Role: string.Empty,
        StaffId: null,
        CreatedAt: null,
        LastSeenAt: null,
        ExpiresAt: null,
        EndedAt: null,
        FailureReason: "A valid Legacy EHR session is required.",
        SessionSource: "avenchart");
}
