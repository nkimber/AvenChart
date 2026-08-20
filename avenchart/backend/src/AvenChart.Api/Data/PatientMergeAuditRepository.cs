// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientMergeAuditRepository(NpgsqlDataSource dataSource)
{
    public async Task<PatientMergeAuditPlanResponse> RecordPreviewAsync(
        PatientMergeAuditPlanRequest request,
        PatientMergePreviewResponse preview,
        string username,
        CancellationToken cancellationToken)
    {
        var auditId = Guid.NewGuid();
        var plannedAt = DateTimeOffset.UtcNow;
        var rationale = Normalize(request.Rationale);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patient_merge_audit_plans (
                audit_id, target_patient_id, source_patient_id, target_legacy_pid, source_legacy_pid,
                match_score, match_reasons, rationale, planned_by, planned_at, status)
            values (
                @auditId, @targetPatientId, @sourcePatientId, @targetLegacyPid, @sourceLegacyPid,
                @matchScore, @matchReasons, @rationale, @plannedBy, @plannedAt, 'Previewed');
            """;
        command.Parameters.AddWithValue("auditId", auditId);
        command.Parameters.AddWithValue("targetPatientId", preview.TargetPatient.CanonicalId);
        command.Parameters.AddWithValue("sourcePatientId", preview.SourcePatient.CanonicalId);
        command.Parameters.AddWithValue("targetLegacyPid", preview.TargetPatient.LegacyPid);
        command.Parameters.AddWithValue("sourceLegacyPid", preview.SourcePatient.LegacyPid);
        command.Parameters.AddWithValue("matchScore", preview.MatchScore);
        command.Parameters.AddWithValue("matchReasons", preview.MatchReasons.ToArray());
        command.Parameters.AddWithValue("rationale", (object?)rationale ?? DBNull.Value);
        command.Parameters.AddWithValue("plannedBy", username);
        command.Parameters.AddWithValue("plannedAt", plannedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new PatientMergeAuditPlanResponse(
            auditId,
            plannedAt.ToString("O"),
            username,
            "Previewed",
            rationale,
            preview);
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
