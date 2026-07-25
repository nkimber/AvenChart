namespace AvenChart.Api.Models;

public sealed record AdministrationDirectoryResponse(
    string DatasetId,
    string DatasetVersion,
    AdministrationDirectoryCounts Counts,
    IReadOnlyList<AdministrationUserItem> Users,
    IReadOnlyList<AdministrationFacilityItem> Facilities,
    AdministrationAccessControlSummary AccessControl,
    AdministrationPortalActivitySummary PortalActivity);

public sealed record AdministrationDirectoryCounts(
    int Users,
    int Providers,
    int CalendarUsers,
    int Facilities,
    int AccessGroups,
    int AccessPermissions,
    int AccessGroupPermissions,
    int AccessUserMemberships,
    int WaitingPortalAudits,
    int WaitingProfileReviews);

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
    IReadOnlyList<AdministrationAccessGroupPermissionItem> GroupPermissions,
    IReadOnlyList<AdministrationAccessUserMembershipItem> UserMemberships);

public sealed record AdministrationPortalActivitySummary(
    int WaitingAuditCount,
    int WaitingProfileReviewCount,
    IReadOnlyList<AdministrationPortalProfileReviewRequest> ProfileReviewRequests);

public sealed record AdministrationPortalProfileReviewRequest(
    string Id,
    string RequestedAt,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string FirstName,
    string MiddleName,
    string LastName,
    string PatientName,
    string Activity,
    int RequireAudit,
    string PendingAction,
    string ActionTaken,
    string Status,
    string Narrative,
    string TableAction,
    string? ActionUser,
    string? ActionTakenAt,
    string Checksum,
    PatientPortalProfileDemographics RequestedDemographics);

public sealed record AdministrationPortalProfileReviewMutationResponse(
    string Id,
    string PatientId,
    int LegacyPid,
    string Status,
    string PendingAction,
    string ActionTaken,
    string Narrative,
    string TableAction,
    string ActionUser,
    string ActionTakenAt,
    PatientPortalProfileDemographics RequestedDemographics,
    AdministrationDirectoryResponse Detail);

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

public sealed record AdministrationAccessUserMembershipItem(
    string UserValue,
    string UserName,
    string GroupValue,
    string GroupName,
    int? StaffId);

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

public sealed record AdministrationAccessUserMembershipMutationRequest(
    string UserValue,
    string GroupValue);

public sealed record AdministrationAccessUserMembershipMutationResponse(
    string UserValue,
    string GroupValue,
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

public sealed record PracticeSettingItem(
    string Key,
    string Label,
    string Value,
    string ValueType,
    string UpdatedAt,
    string UpdatedBy);

public sealed record PracticeSettingsResponse(
    IReadOnlyList<PracticeSettingItem> Settings);

public sealed record PracticeSettingUpdateRequest(string Value);

public sealed record CodingCatalogItem(string Key, string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength, string UpdatedAt, string UpdatedBy);
public sealed record CodingCatalogResponse(IReadOnlyList<CodingCatalogItem> Catalogs);
public sealed record CodingCatalogCreateRequest(string Key, string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength);
public sealed record CodingCatalogUpdateRequest(string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength);
