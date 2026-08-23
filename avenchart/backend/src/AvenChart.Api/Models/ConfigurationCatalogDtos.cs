// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record ConfigurationCatalogItem(string Key, string Family, string Classification, string Authority, string Validation, string MutationState);
public sealed record ConfigurationCatalogResponse(IReadOnlyList<ConfigurationCatalogItem> Settings);
