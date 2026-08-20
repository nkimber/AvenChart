// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvenChart.Api.Data;

/// <summary>
/// EF-backed mutations for the procedure order catalog and lab-provider directory.
/// Procedure queues, compendium imports, and clinical result workflows remain SQL-backed.
/// </summary>
public sealed class ProcedureDirectoryRepository(
    AvenChartDbContext dbContext,
    ProcedureRepository procedureRepository)
{
    public async Task<ProcedureOrderCatalogMutationResponse?> CreateOrderCatalogItemAsync(
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken)
    {
        var values = NormalizeOrderCatalogMutation(request);
        if (values is null || !await IsValidOrderCatalogContextAsync(values.Value, cancellationToken))
        {
            return null;
        }

        var item = new LabOrderCatalogEntity
        {
            Name = values.Value.Name,
            ItemType = values.Value.ItemType
        };
        ApplyOrderCatalogValues(item, values.Value);
        dbContext.LabOrderCatalog.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcedureOrderCatalogMutationResponse(
            item.Id,
            await procedureRepository.GetOrderCatalogAsync(cancellationToken));
    }

    public async Task<ProcedureOrderCatalogMutationResponse?> UpdateOrderCatalogItemAsync(
        int id,
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        var values = NormalizeOrderCatalogMutation(request);
        if (values is null || !await IsValidOrderCatalogContextAsync(values.Value, cancellationToken))
        {
            return null;
        }

        var item = await dbContext.LabOrderCatalog.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (item is null)
        {
            return null;
        }

        ApplyOrderCatalogValues(item, values.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcedureOrderCatalogMutationResponse(
            item.Id,
            await procedureRepository.GetOrderCatalogAsync(cancellationToken));
    }

    public async Task<bool> DeleteOrderCatalogItemAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        return await dbContext.LabOrderCatalog
            .Where(item => item.Id == id)
            .Where(item => !dbContext.LabOrderCatalog.Any(child => child.ParentId == item.Id))
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<ProcedureLabProviderMutationResponse?> CreateLabProviderAsync(
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        var identity = await ResolveLabProviderIdentityAsync(request, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var provider = new LabProviderEntity
        {
            Name = identity.Value.Name,
            Protocol = NormalizeLabProviderProtocol(request.Protocol),
            Usage = NormalizeLabProviderUsage(request.Usage),
            Direction = NormalizeLabProviderDirection(request.Direction),
            SendApplicationId = NormalizeText(request.SendApplicationId) ?? string.Empty,
            SendFacilityId = NormalizeText(request.SendFacilityId) ?? string.Empty,
            ReceiveApplicationId = NormalizeText(request.ReceiveApplicationId) ?? string.Empty,
            ReceiveFacilityId = NormalizeText(request.ReceiveFacilityId) ?? string.Empty,
            RemoteHost = NormalizeText(request.RemoteHost) ?? string.Empty,
            Login = NormalizeText(request.Login) ?? string.Empty,
            Password = NormalizeText(request.Password) ?? string.Empty,
            OrdersPath = NormalizeText(request.OrdersPath) ?? string.Empty,
            ResultsPath = NormalizeText(request.ResultsPath) ?? string.Empty
        };
        ApplyLabProviderValues(provider, request, identity.Value);
        dbContext.LabProviders.Add(provider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcedureLabProviderMutationResponse(
            provider.Id,
            await procedureRepository.GetLabProvidersAsync(includeInactive: true, cancellationToken));
    }

    public async Task<ProcedureLabProviderMutationResponse?> UpdateLabProviderAsync(
        int id,
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        var identity = await ResolveLabProviderIdentityAsync(request, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var provider = await dbContext.LabProviders.SingleOrDefaultAsync(
            candidate => candidate.Id == id,
            cancellationToken);
        if (provider is null)
        {
            return null;
        }

        ApplyLabProviderValues(provider, request, identity.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcedureLabProviderMutationResponse(
            provider.Id,
            await procedureRepository.GetLabProvidersAsync(includeInactive: true, cancellationToken));
    }

    public async Task<bool> DeleteLabProviderAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        return await dbContext.LabProviders
            .Where(provider => provider.Id == id)
            .Where(provider => !dbContext.LabOrderReferences.Any(order => order.LabId == provider.Id))
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<ProcedureLabProviderAddressBookMutationResponse?> CreateLabProviderAddressBookOrganizationAsync(
        ProcedureLabProviderAddressBookMutationRequest request,
        CancellationToken cancellationToken)
    {
        var organizationName = NormalizeText(request.Organization);
        if (organizationName is null)
        {
            return null;
        }

        var organization = new LabProviderAddressBookEntity
        {
            Organization = organizationName,
            Type = NormalizeLabProviderAddressBookType(request.Type),
            Active = request.Active
        };
        dbContext.LabProviderAddressBook.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ProcedureLabProviderAddressBookMutationResponse(
            organization.Id,
            await procedureRepository.GetLabProviderAddressBookAsync(cancellationToken));
    }

    public async Task<bool> DeleteLabProviderAddressBookOrganizationAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return false;
        }

        return await dbContext.LabProviderAddressBook
            .Where(organization => organization.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private async Task<bool> IsValidOrderCatalogContextAsync(
        OrderCatalogMutationValues values,
        CancellationToken cancellationToken)
    {
        if (values.ParentId is { } parentId
            && !await dbContext.LabOrderCatalog.AsNoTracking().AnyAsync(
                item => item.Id == parentId && item.ItemType == "grp",
                cancellationToken))
        {
            return false;
        }

        return values.LabId is not { } labId
            || await dbContext.LabProviders.AsNoTracking().AnyAsync(
                provider => provider.Id == labId,
                cancellationToken);
    }

    private async Task<(string Name, int? LabDirectorId)?> ResolveLabProviderIdentityAsync(
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.LabDirectorId is > 0)
        {
            var organization = await dbContext.LabProviderAddressBook
                .AsNoTracking()
                .Where(candidate => candidate.Id == request.LabDirectorId.Value)
                .Where(candidate => EF.Functions.Like(candidate.Type, "ord_%"))
                .Select(candidate => candidate.Organization)
                .SingleOrDefaultAsync(cancellationToken);
            return NormalizeText(organization) is { } name
                ? (name, request.LabDirectorId.Value)
                : null;
        }

        return NormalizeText(request.Name) is { } providerName
            ? (providerName, null)
            : null;
    }

    private static void ApplyOrderCatalogValues(
        LabOrderCatalogEntity item,
        OrderCatalogMutationValues values)
    {
        item.ParentId = values.ParentId;
        item.LabId = values.LabId;
        item.Code = values.Code;
        item.Name = values.Name;
        item.ItemType = values.ItemType;
        item.ProcedureTypeName = values.ProcedureTypeName;
        item.Description = values.Description;
        item.Specimen = values.Specimen;
        item.StandardCode = values.StandardCode;
        item.Sequence = values.Sequence;
        item.Active = values.Active;
    }

    private static void ApplyLabProviderValues(
        LabProviderEntity provider,
        ProcedureLabProviderMutationRequest request,
        (string Name, int? LabDirectorId) identity)
    {
        provider.Name = identity.Name;
        provider.LabDirectorId = identity.LabDirectorId;
        provider.Npi = NormalizeText(request.Npi);
        provider.Protocol = NormalizeLabProviderProtocol(request.Protocol);
        provider.Usage = NormalizeLabProviderUsage(request.Usage);
        provider.Direction = NormalizeLabProviderDirection(request.Direction);
        provider.SendApplicationId = NormalizeText(request.SendApplicationId) ?? string.Empty;
        provider.SendFacilityId = NormalizeText(request.SendFacilityId) ?? string.Empty;
        provider.ReceiveApplicationId = NormalizeText(request.ReceiveApplicationId) ?? string.Empty;
        provider.ReceiveFacilityId = NormalizeText(request.ReceiveFacilityId) ?? string.Empty;
        provider.RemoteHost = NormalizeText(request.RemoteHost) ?? string.Empty;
        provider.Login = NormalizeText(request.Login) ?? string.Empty;
        provider.Password = NormalizeText(request.Password) ?? string.Empty;
        provider.OrdersPath = NormalizeText(request.OrdersPath) ?? string.Empty;
        provider.ResultsPath = NormalizeText(request.ResultsPath) ?? string.Empty;
        provider.Notes = NormalizeText(request.Notes);
        provider.Active = request.Active;
    }

    private static OrderCatalogMutationValues? NormalizeOrderCatalogMutation(
        ProcedureOrderCatalogMutationRequest request)
    {
        var name = NormalizeText(request.Name);
        if (name is null)
        {
            return null;
        }

        var itemType = NormalizeText(request.ItemType)?.ToLowerInvariant() switch
        {
            "grp" => "grp",
            "ord" => "ord",
            _ => "ord"
        };
        var parentId = request.ParentId is > 0 ? request.ParentId : null;
        var labId = request.LabId is > 0 ? request.LabId : null;
        var code = NormalizeText(request.Code);
        if (itemType == "ord" && (parentId is null || labId is null || code is null))
        {
            return null;
        }

        return new OrderCatalogMutationValues(
            parentId,
            labId,
            name,
            code,
            itemType,
            NormalizeText(request.ProcedureTypeName) ?? (itemType == "ord" ? "laboratory" : null),
            NormalizeText(request.Description),
            NormalizeText(request.Specimen),
            NormalizeText(request.StandardCode),
            request.Sequence ?? 0,
            request.Active);
    }

    private static string NormalizeLabProviderProtocol(string? protocol) =>
        NormalizeText(protocol)?.ToUpperInvariant() ?? "DL";

    private static string NormalizeLabProviderUsage(string? usage) =>
        NormalizeText(usage)?.ToUpperInvariant() switch
        {
            "P" => "P",
            "T" => "T",
            "Q" => "Q",
            _ => "D"
        };

    private static string NormalizeLabProviderDirection(string? direction) =>
        NormalizeText(direction)?.ToUpperInvariant() == "R" ? "R" : "B";

    private static string NormalizeLabProviderAddressBookType(string? type)
    {
        var normalized = NormalizeText(type)?.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            || !normalized.StartsWith("ord_", StringComparison.Ordinal)
            ? "ord_lab"
            : normalized;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private readonly record struct OrderCatalogMutationValues(
        int? ParentId,
        int? LabId,
        string Name,
        string? Code,
        string ItemType,
        string? ProcedureTypeName,
        string? Description,
        string? Specimen,
        string? StandardCode,
        int Sequence,
        bool Active);
}
