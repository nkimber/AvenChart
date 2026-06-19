namespace AvenChart.Api.Models;

public sealed record AppointmentSearchResponse(
    string DatasetId,
    string DatasetVersion,
    string? PatientId,
    string? FromDate,
    int Limit,
    int TotalMatches,
    IReadOnlyList<AppointmentListItem> Appointments);

public sealed record AppointmentListItem(
    string Id,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string Date,
    string StartTime,
    int DurationMinutes,
    string Title,
    string? Status,
    string? Room,
    int? CategoryId,
    string? ProviderName,
    string? FacilityName);

public sealed record AppointmentDetail(
    string Id,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    string? Sex,
    string DateOfBirth,
    string Date,
    string StartTime,
    int DurationMinutes,
    string Title,
    string? Status,
    string? Room,
    int? CategoryId,
    string? ProviderName,
    string? FacilityName,
    string? PatientPurpose);
