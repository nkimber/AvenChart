// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

public sealed class PatientEducationRepository(AvenChartDbContext dbContext)
{
    public async Task<PatientEducationResponse> GetAsync(CancellationToken cancellationToken)
    {
        var resources = await dbContext.PatientEducationResources
            .AsNoTracking()
            .Where(resource => resource.Active)
            .OrderBy(resource => resource.Title)
            .Select(resource => new PatientEducationResource(
                resource.ResourceKey,
                resource.Title,
                resource.SearchTemplate,
                resource.Active))
            .ToListAsync(cancellationToken);
        return new PatientEducationResponse(resources);
    }

    public async Task<PatientEducationSearchResponse?> SearchAsync(
        PatientEducationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var searchText = request.SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        var searchTemplate = await dbContext.PatientEducationResources
            .AsNoTracking()
            .Where(resource => resource.ResourceKey == request.ResourceKey && resource.Active)
            .Select(resource => resource.SearchTemplate)
            .SingleOrDefaultAsync(cancellationToken);
        var destination = searchTemplate?.Replace("[%]", Uri.EscapeDataString(searchText));
        if (destination is null ||
            !searchTemplate!.Contains("[%]", StringComparison.Ordinal) ||
            !Uri.TryCreate(destination, UriKind.Absolute, out var url) ||
            url.Scheme != "https")
        {
            return null;
        }

        return new PatientEducationSearchResponse(request.ResourceKey, searchText, destination);
    }
}
