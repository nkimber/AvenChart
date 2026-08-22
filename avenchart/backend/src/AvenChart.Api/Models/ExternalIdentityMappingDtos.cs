// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

/// <summary>
/// Administrator-visible binding between a provider-scoped OIDC subject and
/// the server-owned AvenChart account that owns roles and resource grants.
/// </summary>
public sealed record ExternalIdentityMappingItem(
    Guid MappingId,
    string ProviderId,
    string ExternalSubject,
    string Username,
    bool Active,
    string CreatedAt,
    string CreatedBy,
    string? DeactivatedAt,
    string? DeactivatedBy,
    string? DeactivationReason);

public sealed record ExternalIdentityMappingCreateRequest(
    string ProviderId,
    string ExternalSubject,
    string Username);

public sealed record ExternalIdentityMappingDeactivateRequest(string Reason);
