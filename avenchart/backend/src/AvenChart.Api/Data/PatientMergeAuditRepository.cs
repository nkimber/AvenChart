// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
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
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var target = await GetSnapshotAsync(connection, transaction, preview.TargetPatient.CanonicalId, cancellationToken)
            ?? throw new InvalidOperationException("The merge target no longer exists.");
        var source = await GetSnapshotAsync(connection, transaction, preview.SourcePatient.CanonicalId, cancellationToken)
            ?? throw new InvalidOperationException("The merge source no longer exists.");

        if (target.LegacyPid != preview.TargetPatient.LegacyPid || source.LegacyPid != preview.SourcePatient.LegacyPid)
        {
            throw new InvalidOperationException("The merge preview no longer matches the selected patient records. Refresh the preview before recording review evidence.");
        }

        var targetRecordFingerprint = await PatientMergeExecutionRepository.GetRecordFingerprintAsync(
            connection, transaction, target.CanonicalId, target.LegacyPid, cancellationToken);
        var sourceRecordFingerprint = await PatientMergeExecutionRepository.GetRecordFingerprintAsync(
            connection, transaction, source.CanonicalId, source.LegacyPid, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_merge_audit_plans (
                audit_id, target_patient_id, source_patient_id, target_legacy_pid, source_legacy_pid,
                match_score, match_reasons, rationale, planned_by, planned_at, status,
                target_administration_version, source_administration_version,
                target_record_fingerprint, source_record_fingerprint)
            values (
                @auditId, @targetPatientId, @sourcePatientId, @targetLegacyPid, @sourceLegacyPid,
                @matchScore, @matchReasons, @rationale, @plannedBy, @plannedAt, 'Previewed',
                @targetAdministrationVersion, @sourceAdministrationVersion,
                @targetRecordFingerprint, @sourceRecordFingerprint);
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
        command.Parameters.AddWithValue("targetAdministrationVersion", target.AdministrationVersion);
        command.Parameters.AddWithValue("sourceAdministrationVersion", source.AdministrationVersion);
        command.Parameters.AddWithValue("targetRecordFingerprint", targetRecordFingerprint);
        command.Parameters.AddWithValue("sourceRecordFingerprint", sourceRecordFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

    private static async Task<PatientSnapshot?> GetSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid, administration_version
            from patients
            where lower(canonical_id) = lower(@patientId)
            for share;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PatientSnapshot(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2))
            : null;
    }

    private sealed record PatientSnapshot(string CanonicalId, int LegacyPid, long AdministrationVersion);
}
