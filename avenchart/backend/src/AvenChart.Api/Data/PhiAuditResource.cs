// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Data;

public sealed record PhiAuditResource(string Type, string Id);

public static class PhiAuditResourceContext
{
    private const string ItemKey = "phiAuditResource";

    public static void Set(HttpContext httpContext, string resourceType, string? resourceId)
    {
        var normalizedId = resourceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId) || normalizedId.Length > 256)
        {
            return;
        }

        httpContext.Items[ItemKey] = new PhiAuditResource(resourceType, normalizedId);
    }

    public static PhiAuditResource? Get(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var value) && value is PhiAuditResource resource
            ? resource
            : null;
}
