namespace AvenChart.Api.Models;
public sealed record ChartTrackerPatient(string PatientId,string PublicId,string DisplayName,string DateOfBirth,ChartTrackerEvent? Current);
public sealed record ChartTrackerEvent(Guid Id,string? Location,int? UserId,string? UserName,string RecordedAt);
public sealed record ChartTrackerOptions(IReadOnlyList<string> Locations,IReadOnlyList<ChartTrackerUser> Users);
public sealed record ChartTrackerUser(int Id,string DisplayName);
public sealed record ChartTrackerUpdateRequest(string? Location,int? UserId);
