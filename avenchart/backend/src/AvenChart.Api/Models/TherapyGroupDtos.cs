// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
public sealed record TherapyGroupSessionAttendanceRequest(string Status, string? Note);
public sealed record TherapyGroupSessionAttendanceItem(
    Guid SessionId,
    string PatientId,
    int LegacyPid,
    string DisplayName,
    string Status,
    string? Note,
    string? RecordedAt);
public sealed record TherapyGroupSessionAttendanceResponse(
    Guid SessionId,
    IReadOnlyList<TherapyGroupSessionAttendanceItem> Attendance);
public sealed record TherapyGroupSessionEncounterRequest(int? ProviderId, int? FacilityId, int? BillingFacilityId, string? Sensitivity, string? ReferralSource, int? PosCode, string? BillingNote);
public sealed record TherapyGroupSessionEncounterItem(Guid SessionId, string PatientId, int LegacyPid, string DisplayName, int? Encounter, string Status);
public sealed record TherapyGroupSessionEncounterResponse(Guid SessionId, IReadOnlyList<TherapyGroupSessionEncounterItem> Encounters);
