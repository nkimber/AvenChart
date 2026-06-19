namespace AvenChart.Api.Models;

public sealed record AdministrationDirectoryResponse(
    string DatasetId,
    string DatasetVersion,
    AdministrationDirectoryCounts Counts,
    IReadOnlyList<AdministrationUserItem> Users,
    IReadOnlyList<AdministrationFacilityItem> Facilities);

public sealed record AdministrationDirectoryCounts(
    int Users,
    int Providers,
    int CalendarUsers,
    int Facilities);

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
