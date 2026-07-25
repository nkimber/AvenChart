namespace AvenChart.Api.Models;

public sealed record TherapyGroupItem(
    Guid Id,
    string Name,
    string Status,
    int? FacilitatorId,
    string? Description,
    int Capacity,
    string CreatedAt);

public sealed record TherapyGroupCreateRequest(
    string Name,
    int? FacilitatorId,
    string? Description,
    int Capacity);

public sealed record TherapyGroupsResponse(IReadOnlyList<TherapyGroupItem> Groups);

public sealed record TherapyGroupMemberRequest(string PatientId);
public sealed record TherapyGroupMemberItem(Guid GroupId, string PatientId, int LegacyPid, string DisplayName, string JoinedAt);

public sealed record TherapyGroupSessionCreateRequest(string StartsAt, int DurationMinutes, string? Topic);
public sealed record TherapyGroupSessionStatusRequest(string Status);
public sealed record TherapyGroupSessionItem(Guid Id, Guid GroupId, string StartsAt, int DurationMinutes, string? Topic, string Status, string CreatedAt);
