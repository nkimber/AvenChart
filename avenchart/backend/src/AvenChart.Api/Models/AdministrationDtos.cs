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
public sealed record PracticeSettingRevision(long RevisionId, string Value, string? PriorValue, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record PracticeSettingHistoryResponse(PracticeSettingItem Setting, IReadOnlyList<PracticeSettingRevision> Revisions);
public sealed record PracticeSettingChangeRequestCreateRequest(string Value, string Reason);
public sealed record PracticeSettingChangeRequestDecisionRequest(string? Note, int? ExpectedVersion = null);
public sealed record PracticeSettingChangeRequestItem(
    Guid RequestId,
    string SettingKey,
    string ProposedValue,
    string BaselineValue,
    string BaselineUpdatedAt,
    string Reason,
    string Status,
    int Version,
    string CreatedAt,
    string CreatedBy,
    string UpdatedAt,
    string UpdatedBy);
public sealed record PracticeSettingChangeRequestEvent(long EventId, string Action, string? Note, string OccurredAt, string Username);
public sealed record PracticeSettingChangeRequestCounts(int Draft, int Submitted, int Approved, int Rejected, int Activated, int Cancelled);
public sealed record PracticeSettingChangeRequestsResponse(
    IReadOnlyList<PracticeSettingChangeRequestItem> Requests,
    int Total,
    int Returned,
    int Offset,
    int Limit,
    string Status,
    string? SettingKey,
    PracticeSettingChangeRequestCounts Counts);
public sealed record PracticeSettingChangeRequestDetailResponse(
    PracticeSettingChangeRequestItem Request,
    PracticeSettingItem Setting,
    IReadOnlyList<PracticeSettingChangeRequestEvent> Events);
public sealed class PracticeSettingChangeRequestConflictException(string message) : Exception(message);

public sealed record CodingCatalogItem(string Key, string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength, string UpdatedAt, string UpdatedBy);
public sealed record CodingCatalogResponse(IReadOnlyList<CodingCatalogItem> Catalogs);
public sealed record CodingCatalogCreateRequest(string Key, string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength);
public sealed record CodingCatalogUpdateRequest(string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength);
public sealed record CodingCatalogRevision(long RevisionId, string DisplayName, int Sequence, bool Active, bool ClaimEnabled, bool FeeEnabled, int ModifierLength, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record CodingCatalogHistoryResponse(CodingCatalogItem Catalog, IReadOnlyList<CodingCatalogRevision> Revisions);

public sealed record FormLayoutItem(string Key, string Title, string Mapping, int Sequence, bool Active, string UpdatedAt, string UpdatedBy);
public sealed record FormLayoutGroupItem(string Key, string Title, int Sequence, bool Active, string UpdatedAt, string UpdatedBy);
public sealed record FormLayoutFieldItem(string Key, string GroupKey, string Label, string FieldType, int Sequence, bool Required, bool Active, int MaxLength, string ListId, string DefaultValue, string UpdatedAt, string UpdatedBy);
public sealed record FormLayoutCatalogResponse(IReadOnlyList<FormLayoutItem> Layouts);
public sealed record FormLayoutDetailResponse(FormLayoutItem Layout, IReadOnlyList<FormLayoutGroupItem> Groups, IReadOnlyList<FormLayoutFieldItem> Fields);
public sealed record FormLayoutMutationRequest(string Title, string Mapping, int Sequence, bool Active);
public sealed record FormLayoutRevision(long RevisionId, string Title, string Mapping, int Sequence, bool Active, int GroupCount, int FieldCount, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record FormLayoutHistoryResponse(FormLayoutDetailResponse Detail, IReadOnlyList<FormLayoutRevision> Revisions);
public sealed record FormLayoutGroupMutationRequest(string Title, int Sequence, bool Active);
public sealed record FormLayoutFieldMutationRequest(string GroupKey, string Label, string FieldType, int Sequence, bool Required, bool Active, int MaxLength, string? ListId, string? DefaultValue);
public sealed record FormOptionListItem(string Key, string Title, bool Active, int OptionCount, string UpdatedAt, string UpdatedBy);
public sealed record FormOptionValueItem(string Key, string Title, int Sequence, bool IsDefault, bool Active, string Value, string UpdatedAt, string UpdatedBy);
public sealed record FormOptionListCatalogResponse(IReadOnlyList<FormOptionListItem> Lists);
public sealed record FormOptionListDetailResponse(FormOptionListItem List, IReadOnlyList<FormOptionValueItem> Options);
public sealed record FormOptionListRevision(long RevisionId, string Title, bool Active, int OptionCount, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record FormOptionListHistoryResponse(FormOptionListDetailResponse Detail, IReadOnlyList<FormOptionListRevision> Revisions);
public sealed record FormOptionListMutationRequest(string Title, bool Active);
public sealed record FormOptionValueMutationRequest(string Title, int Sequence, bool IsDefault, bool Active, string? Value);
public sealed record ClinicalAlertRuleItem(string Key, string Title, string TriggerType, string TargetType, string Severity, string Message, int Sequence, bool Active, string UpdatedAt, string UpdatedBy);
public sealed record ClinicalAlertRulesResponse(IReadOnlyList<ClinicalAlertRuleItem> Rules);
public sealed record ClinicalAlertRuleRevision(long RevisionId, string Title, string TriggerType, string TargetType, string Severity, string Message, int Sequence, bool Active, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record ClinicalAlertRuleHistoryResponse(ClinicalAlertRuleItem Rule, IReadOnlyList<ClinicalAlertRuleRevision> Revisions);
public sealed record ClinicalAlertRuleMutationRequest(string Title, string TriggerType, string TargetType, string Severity, string Message, int Sequence, bool Active);
public sealed record ModuleCatalogItem(string Key, string DisplayName, string Category, string Status, string Description, bool CanChangeStatus, string UpdatedAt, string UpdatedBy);
public sealed record ModuleCatalogResponse(IReadOnlyList<ModuleCatalogItem> Modules);
public sealed record ModuleCatalogRevision(long RevisionId, string DisplayName, string Category, string Status, string Description, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record ModuleCatalogHistoryResponse(ModuleCatalogItem Module, IReadOnlyList<ModuleCatalogRevision> Revisions);
public sealed record ModuleCatalogStatusUpdateRequest(string Status);
public sealed record ApiClientRegistryItem(string Key, string DisplayName, string RedirectUri, string Scopes, bool Active, string UpdatedAt, string UpdatedBy);
public sealed record ApiClientRegistryResponse(IReadOnlyList<ApiClientRegistryItem> Clients);
public sealed record ApiClientRegistryRevision(long RevisionId, string DisplayName, string RedirectUri, string Scopes, bool Active, string Action, long? RestoredFromRevisionId, string OccurredAt, string Username);
public sealed record ApiClientRegistryHistoryResponse(ApiClientRegistryItem Client, IReadOnlyList<ApiClientRegistryRevision> Revisions);
public sealed record ApiClientRegistryMutationRequest(string DisplayName, string RedirectUri, string Scopes, bool Active);
