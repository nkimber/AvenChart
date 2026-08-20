// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

/// <summary>
/// Entity-oriented administration mutations split from the broader governed-configuration
/// repository. The legacy directory projection remains a SQL read model in that repository.
/// </summary>
public sealed class AdministrationDirectoryRepository(
    AvenChartDbContext dbContext,
    AdministrationRepository administrationRepository)
{
    private const string DefaultFacilityColor = "#246b73";
    private const string DefaultUserEmailDomain = "example.test";
    private static readonly HashSet<string> ValidAccessReturnValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "addonly",
            "view",
            "write",
            "wsome"
        };

    public async Task<AdministrationUserMutationResponse> CreateUserAsync(
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await BuildUserAsync(request, cancellationToken);
        dbContext.Staff.Add(user);
        await SaveUserChangesAsync(user.Username, cancellationToken);
        return new AdministrationUserMutationResponse(
            user.Id,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse?> UpdateUserAsync(
        int userId,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Staff.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var values = await BuildUserAsync(request, cancellationToken);
        user.Username = values.Username;
        user.FirstName = values.FirstName;
        user.LastName = values.LastName;
        user.Role = values.Role;
        user.Calendar = values.Calendar;
        user.FacilityId = values.FacilityId;
        user.Email = values.Email;
        user.Npi = values.Npi;
        user.Active = values.Active;
        await SaveUserChangesAsync(user.Username, cancellationToken);
        return new AdministrationUserMutationResponse(
            user.Id,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.AccessUserMemberships
            .Where(membership => membership.StaffId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        var user = await dbContext.Staff.SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.Staff.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdministrationFacilityMutationResponse> CreateFacilityAsync(
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        var facility = BuildFacility(request);
        dbContext.Facilities.Add(facility);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdministrationFacilityMutationResponse(
            facility.Id,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationFacilityMutationResponse?> UpdateFacilityAsync(
        int facilityId,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        var facility = await dbContext.Facilities.SingleOrDefaultAsync(
            candidate => candidate.Id == facilityId,
            cancellationToken);
        if (facility is null)
        {
            return null;
        }

        var values = BuildFacility(request);
        facility.Code = values.Code;
        facility.Name = values.Name;
        facility.Phone = values.Phone;
        facility.Street = values.Street;
        facility.City = values.City;
        facility.State = values.State;
        facility.PostalCode = values.PostalCode;
        facility.Color = values.Color;
        facility.Inactive = values.Inactive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdministrationFacilityMutationResponse(
            facility.Id,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteFacilityAsync(
        int facilityId,
        CancellationToken cancellationToken)
    {
        var facility = await dbContext.Facilities.SingleOrDefaultAsync(
            candidate => candidate.Id == facilityId,
            cancellationToken);
        if (facility is null)
        {
            return false;
        }

        dbContext.Facilities.Remove(facility);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdministrationAccessPermissionMutationResponse> GrantAccessGroupPermissionAsync(
        AdministrationAccessPermissionMutationRequest request,
        CancellationToken cancellationToken)
    {
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");
        var sectionValue = NormalizeAccessToken(request.SectionValue, "Permission section");
        var permissionValue = NormalizeAccessToken(request.PermissionValue, "Permission");
        var returnValue = NormalizeAccessReturnValue(request.ReturnValue);
        if (!await dbContext.AccessGroups.AsNoTracking().AnyAsync(
                group => group.Value == groupValue,
                cancellationToken))
        {
            throw new ArgumentException($"Access group '{groupValue}' was not found.");
        }

        var permissionName = await dbContext.AccessPermissions
            .AsNoTracking()
            .Where(permission =>
                permission.SectionValue == sectionValue &&
                permission.Value == permissionValue)
            .Select(permission => permission.Name)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException(
                $"Access permission '{sectionValue}:{permissionValue}' was not found.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.AccessGroupPermissions
            .Where(permission =>
                permission.GroupValue == groupValue &&
                permission.SectionValue == sectionValue &&
                permission.PermissionValue == permissionValue)
            .ExecuteDeleteAsync(cancellationToken);
        dbContext.AccessGroupPermissions.Add(new AccessGroupPermissionEntity
        {
            GroupValue = groupValue,
            SectionValue = sectionValue,
            PermissionValue = permissionValue,
            PermissionName = permissionName,
            ReturnValue = returnValue
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdministrationAccessPermissionMutationResponse(
            groupValue,
            sectionValue,
            permissionValue,
            returnValue,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessPermissionMutationResponse?> RevokeAccessGroupPermissionAsync(
        string groupValue,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken)
    {
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");
        var normalizedSection = NormalizeAccessToken(sectionValue, "Permission section");
        var normalizedPermission = NormalizeAccessToken(permissionValue, "Permission");
        var deleted = await dbContext.AccessGroupPermissions
            .Where(permission =>
                permission.GroupValue == normalizedGroup &&
                permission.SectionValue == normalizedSection &&
                permission.PermissionValue == normalizedPermission)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0
            ? null
            : new AdministrationAccessPermissionMutationResponse(
                normalizedGroup,
                normalizedSection,
                normalizedPermission,
                null,
                await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse> GrantAccessUserMembershipAsync(
        AdministrationAccessUserMembershipMutationRequest request,
        CancellationToken cancellationToken)
    {
        var userValue = NormalizeAccessToken(request.UserValue, "User");
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");
        var groupName = await dbContext.AccessGroups
            .AsNoTracking()
            .Where(group => group.Value == groupValue)
            .Select(group => group.Name)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Access group '{groupValue}' was not found.");
        var staff = await dbContext.Staff
            .AsNoTracking()
            .Where(user => user.Username.ToLower() == userValue)
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.FirstName,
                user.LastName
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"User '{userValue}' was not found.");
        var membership = await dbContext.AccessUserMemberships.SingleOrDefaultAsync(
            candidate =>
                candidate.UserValue == staff.Username &&
                candidate.GroupValue == groupValue,
            cancellationToken);
        if (membership is null)
        {
            membership = new AccessUserMembershipEntity
            {
                UserValue = staff.Username,
                UserName = $"{staff.LastName}, {staff.FirstName}",
                GroupValue = groupValue,
                GroupName = groupName,
                StaffId = staff.Id
            };
            dbContext.AccessUserMemberships.Add(membership);
        }
        else
        {
            membership.UserName = $"{staff.LastName}, {staff.FirstName}";
            membership.GroupName = groupName;
            membership.StaffId = staff.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdministrationAccessUserMembershipMutationResponse(
            staff.Username,
            groupValue,
            await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse?> RevokeAccessUserMembershipAsync(
        string userValue,
        string groupValue,
        CancellationToken cancellationToken)
    {
        var normalizedUser = NormalizeAccessToken(userValue, "User");
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");
        var deleted = await dbContext.AccessUserMemberships
            .Where(membership =>
                membership.UserValue == normalizedUser &&
                membership.GroupValue == normalizedGroup)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0
            ? null
            : new AdministrationAccessUserMembershipMutationResponse(
                normalizedUser,
                normalizedGroup,
                await administrationRepository.GetDirectoryAsync(cancellationToken));
    }

    private async Task<StaffEntity> BuildUserAsync(
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var username = NormalizeRequired(request.Username, "Username");
        var role = NormalizeRequired(request.Role, "Role").ToLowerInvariant();
        if (request.FacilityId is { } facilityId &&
            !await dbContext.Facilities.AsNoTracking().AnyAsync(
                facility => facility.Id == facilityId,
                cancellationToken))
        {
            throw new ArgumentException($"Facility '{facilityId}' was not found.");
        }

        return new StaffEntity
        {
            Username = username,
            FirstName = NormalizeRequired(request.FirstName, "First name"),
            LastName = NormalizeRequired(request.LastName, "Last name"),
            Role = role,
            Calendar = request.Calendar ?? string.Equals(
                role,
                "provider",
                StringComparison.OrdinalIgnoreCase),
            FacilityId = request.FacilityId,
            Email = NormalizeOptional(request.Email ?? $"{username}@{DefaultUserEmailDomain}"),
            Npi = NormalizeOptional(request.Npi),
            Active = request.Active ?? true
        };
    }

    private static FacilityEntity BuildFacility(AdministrationFacilityMutationRequest request) =>
        new()
        {
            Code = NormalizeRequired(request.Code, "Facility code"),
            Name = NormalizeRequired(request.Name, "Facility name"),
            Phone = NormalizeOptional(request.Phone),
            Street = NormalizeOptional(request.Street),
            City = NormalizeOptional(request.City),
            State = NormalizeOptional(request.State),
            PostalCode = NormalizeOptional(request.PostalCode),
            Color = NormalizeOptional(request.Color) ?? DefaultFacilityColor,
            Inactive = !(request.Active ?? true)
        };

    private async Task SaveUserChangesAsync(
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ArgumentException($"Username '{username}' is already in use.");
        }
    }

    private static string NormalizeAccessToken(string? value, string label) =>
        NormalizeRequired(value, label).ToLowerInvariant();

    private static string NormalizeAccessReturnValue(string? value)
    {
        var returnValue = NormalizeAccessToken(value, "Return value");
        return ValidAccessReturnValues.Contains(returnValue)
            ? returnValue
            : throw new ArgumentException($"Return value '{returnValue}' is not supported.");
    }

    private static string NormalizeRequired(string? value, string label)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{label} is required.")
            : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
