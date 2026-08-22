// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record PhiAccessAuditEventItem(
    Guid AuditId,
    DateTimeOffset OccurredAt,
    string Username,
    string HttpMethod,
    string EndpointName,
    string RequiredPermission,
    string? ResourceType,
    string? ResourceId,
    int? FacilityId,
    string? FacilityCode,
    string? PurposeOfUse,
    bool Authorized,
    int ResponseStatus);

public sealed record PhiAccessAuditResponse(
    int TotalEvents,
    int AuthorizedEvents,
    int DeniedEvents,
    IReadOnlyList<PhiAccessAuditEventItem> Events);
