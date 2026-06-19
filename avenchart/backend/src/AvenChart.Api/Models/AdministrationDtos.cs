namespace AvenChart.Api.Models;

public sealed record AdministrationDirectoryResponse(
    string DatasetId,
    string DatasetVersion,
    AdministrationDirectoryCounts Counts,
    IReadOnlyList<AdministrationUserItem> Users,
    IReadOnlyList<AdministrationFacilityItem> Facilities,
    AdministrationAccessControlSummary AccessControl);

public sealed record AdministrationDirectoryCounts(
    int Users,
    int Providers,
    int CalendarUsers,
    int Facilities,
    int AccessGroups,
    int AccessPermissions,
    int AccessGroupPermissions);

public sealed record AdministrationUserItem(
    int Id,
    string Username,
    string FirstName,
    string LastName,
    string DisplayName,
    string Role,
    bool Authorized,
    bool Active,
    bool Calendar,
    int? FacilityId,
    string? FacilityName,
    string? Email,
    string? Npi);

public sealed record AdministrationFacilityItem(
    int Id,
    string Code,
    string Name,
    bool Active,
    string? Phone,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? Color);

public sealed record AdministrationAccessControlSummary(
    IReadOnlyList<AdministrationAccessGroupItem> Groups,
    IReadOnlyList<AdministrationAccessPermissionItem> Permissions,
    IReadOnlyList<AdministrationAccessGroupPermissionItem> GroupPermissions);

public sealed record AdministrationAccessGroupItem(
    int Id,
    string Value,
    string Name,
    int? ParentId,
    int PermissionCount);

public sealed record AdministrationAccessPermissionItem(
    string SectionValue,
    string Value,
    string Name);

public sealed record AdministrationAccessGroupPermissionItem(
    string GroupValue,
    string SectionValue,
    string PermissionValue,
    string PermissionName,
    string ReturnValue);

public sealed record AdministrationAccessPermissionMutationRequest(
    string GroupValue,
    string SectionValue,
    string PermissionValue,
    string ReturnValue);

public sealed record AdministrationAccessPermissionMutationResponse(
    string GroupValue,
    string SectionValue,
    string PermissionValue,
    string? ReturnValue,
    AdministrationDirectoryResponse Detail);

public sealed record AdministrationFacilityMutationRequest(
    string Code,
    string Name,
    string? Phone,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? Color,
    bool? Active);

public sealed record AdministrationFacilityMutationResponse(
    int Id,
    AdministrationDirectoryResponse Detail);

public sealed record AdministrationUserMutationRequest(
    string Username,
    string FirstName,
    string LastName,
    string Role,
    bool? Calendar,
    int? FacilityId,
    string? Email,
    string? Npi,
    bool? Active);

public sealed record AdministrationUserMutationResponse(
    int Id,
    AdministrationDirectoryResponse Detail);
