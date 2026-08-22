// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record ExternalLaboratorySourceCreateRequest(
    string SourceId,
    string DisplayName,
    string ApiKey);

public sealed record ExternalLaboratorySourceDeactivateRequest(string Reason);

public sealed record ExternalLaboratorySourceItem(
    string SourceId,
    string DisplayName,
    bool Active,
    string CreatedAt,
    string CreatedBy,
    string? DeactivatedAt,
    string? DeactivatedBy,
    string? DeactivationReason);

public sealed record ExternalLaboratorySourceAuthentication(
    string SourceId,
    string DisplayName);
