// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Defines the server-enforced critical-result follow-up transitions.  Detailed
/// clinical timing remains an operating-policy decision; the system requires a
/// selected due time rather than inventing one.
/// </summary>
public static class CriticalLabResultFollowUpLifecycle
{
    public static bool AllowsAction(string? currentStatus, string? action) =>
        (currentStatus?.Trim().ToLowerInvariant(), action?.Trim().ToLowerInvariant()) switch
        {
            ("open", "accepted") => true,
            ("accepted" or "actioned", "ownership-transferred") => true,
            ("accepted" or "actioned", "communication-recorded") => true,
            ("accepted" or "actioned", "clinical-action-recorded") => true,
            ("accepted" or "actioned", "escalated") => true,
            ("actioned", "closed") => true,
            _ => false
        };
}
