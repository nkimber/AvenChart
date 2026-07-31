// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record PatientRecordRequestResponse(
    Guid RequestId,
    string PatientId,
    int LegacyPid,
    string Status,
    string RequestedAt,
    string RequestedBy,
    string? CompletedAt,
    string? CompletedBy);
