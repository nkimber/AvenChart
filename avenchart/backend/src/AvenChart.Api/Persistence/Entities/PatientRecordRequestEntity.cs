// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Persistence.Entities;

public sealed class PatientRecordRequestEntity
{
    public Guid RequestId { get; set; }
    public required string PatientId { get; set; }
    public int LegacyPid { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public required string RequestedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public long RowVersion { get; set; }
    public PatientEntity Patient { get; set; } = null!;
}
