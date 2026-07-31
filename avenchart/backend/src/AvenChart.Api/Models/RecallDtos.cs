// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;
public sealed record RecallItem(Guid Id,string PatientId,string PatientName,string RecallDate,string Reason,int? ProviderId,int? FacilityId,string Status,string CreatedAt);
public sealed record RecallRequest(string PatientId,DateOnly RecallDate,string Reason,int? ProviderId,int? FacilityId);
public sealed record RecallActivityItem(Guid Id,string ActivityType,string? Note,string RecordedAt);
public sealed record RecallActivityRequest(string ActivityType,string? Note);
