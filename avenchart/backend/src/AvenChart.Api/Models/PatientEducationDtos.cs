// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;
public sealed record PatientEducationResource(string Key,string Title,string SearchTemplate,bool Active);
public sealed record PatientEducationResponse(IReadOnlyList<PatientEducationResource> Resources);
public sealed record PatientEducationSearchRequest(string ResourceKey,string SearchText);
public sealed record PatientEducationSearchResponse(string ResourceKey,string SearchText,string Url);
