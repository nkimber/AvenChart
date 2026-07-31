// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientMergeExecutionRepository(NpgsqlDataSource dataSource)
{
    private static readonly MergeTable[] SupportedTables =
    [
        new("allergies", "id", true, true),
        new("appointments", "id", true, true),
        new("billing", "id", false, true),
        new("claims", "id", true, true),
        new("clinical_notes", "id", true, true),
        new("encounter_signatures", "id", true, true),
        new("encounters", "id", true, true),
        new("immunizations", "id", true, true),
        new("insurance_records", "id", true, true),
        new("lab_orders", "id", true, true),
        new("medications", "id", true, true),
        new("messages", "id", true, true),
        new("patient_documents", "id", true, true),
        new("patient_employers", "patient_id", true, true),
        new("patient_histories", "patient_id", true, true),
        new("patient_portal_accounts", "patient_id", true, true),
        new("patient_portal_message_audit_events", "id", true, true),
        new("patient_portal_profile_change_requests", "id", true, true),
        new("patient_portal_report_audit_events", "id", true, true),
        new("patient_portal_sessions", "id", true, true),
        new("patient_related_contacts", "contact_id", true, true),
        new("patient_reminders", "id", false, true),
        new("payment_activities", "id", true, true),
        new("payment_sessions", "id", true, true),
        new("portal_mailbox_messages", "id", true, true),
        new("prescription_audit_events", "event_id", true, true),
        new("prescriptions", "id", true, true),
        new("problems", "id", true, true),
        new("vitals", "id", true, true)
    ];

    private static readonly HashSet<string> SupportedTableNames = SupportedTables
        .Select(table => table.Name)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly string[] OneToOneTables =
    [
        "patient_employers",
        "patient_histories",
        "patient_portal_accounts"
    ];

    public async Task<PatientMergeExecutionResponse> ExecuteAsync(
        Guid auditId,
        string username,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var audit = await GetAuditAsync(connection, transaction, auditId, cancellationToken)
            ?? throw new InvalidOperationException("The merge review audit does not exist.");

        if (!string.Equals(audit.Status, "Previewed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A merge review audit can only be executed once while it is Previewed.");
        }

        var target = await GetPatientAsync(connection, transaction, audit.TargetPatientId, cancellationToken)
            ?? throw new InvalidOperationException("The merge target no longer exists.");
        var source = await GetPatientAsync(connection, transaction, audit.SourcePatientId, cancellationToken)
            ?? throw new InvalidOperationException("The merge source no longer exists.");

        if (target.MergedIntoPatientId is not null || source.MergedIntoPatientId is not null)
        {
            throw new InvalidOperationException("Merged patient records cannot be used as a merge source or target.");
        }

        var blockers = await GetBlockersAsync(connection, transaction, source, target, cancellationToken);
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException($"The merge is blocked: {string.Join("; ", blockers)}");
        }

        var executionId = Guid.NewGuid();
        var executedAt = DateTimeOffset.UtcNow;
        await InsertExecutionAsync(connection, transaction, executionId, audit, username, executedAt, cancellationToken);

        var movedRecords = new List<PatientMergeExecutionTableCount>();
        foreach (var table in SupportedTables)
        {
            var recordIds = await GetSourceRecordIdsAsync(connection, transaction, table, source, cancellationToken);
            if (recordIds.Count == 0)
            {
                continue;
            }

            await InsertManifestRowsAsync(connection, transaction, executionId, table.Name, recordIds, cancellationToken);
            await MoveRowsAsync(connection, transaction, table, recordIds, target, cancellationToken);
            movedRecords.Add(new PatientMergeExecutionTableCount(table.Name, recordIds.Count));
        }

        await SetSourceMergeStateAsync(connection, transaction, source, target, username, executedAt, false, cancellationToken);
        await SetAuditStatusAsync(connection, transaction, auditId, "Executed", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(executionId, auditId, "Executed", executedAt, username, target, source, movedRecords);
    }

    public async Task<PatientMergeExecutionResponse> RollbackAsync(
        Guid executionId,
        string username,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var execution = await GetExecutionAsync(connection, transaction, executionId, cancellationToken)
            ?? throw new InvalidOperationException("The merge execution does not exist.");
        if (!string.Equals(execution.Status, "Executed", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only an executed merge can be rolled back.");
        }

        var target = await GetPatientAsync(connection, transaction, execution.TargetPatientId, cancellationToken)
            ?? throw new InvalidOperationException("The merge target no longer exists.");
        var source = await GetPatientAsync(connection, transaction, execution.SourcePatientId, cancellationToken)
            ?? throw new InvalidOperationException("The merge source no longer exists.");
        if (!string.Equals(source.MergedIntoPatientId, target.CanonicalId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The source patient is no longer in the expected merged state.");
        }

        var manifest = await GetManifestAsync(connection, transaction, executionId, cancellationToken);
        var restoredRecords = new List<PatientMergeExecutionTableCount>();
        foreach (var table in SupportedTables)
        {
            if (!manifest.TryGetValue(table.Name, out var recordIds) || recordIds.Length == 0)
            {
                continue;
            }

            await MoveRowsAsync(connection, transaction, table, recordIds, source, cancellationToken);
            restoredRecords.Add(new PatientMergeExecutionTableCount(table.Name, recordIds.Length));
        }

        var rolledBackAt = DateTimeOffset.UtcNow;
        await SetSourceMergeStateAsync(connection, transaction, source, target, username, rolledBackAt, true, cancellationToken);
        await SetExecutionStatusAsync(connection, transaction, executionId, "RolledBack", username, rolledBackAt, cancellationToken);
        await SetAuditStatusAsync(connection, transaction, execution.AuditId, "RolledBack", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(executionId, execution.AuditId, "RolledBack", rolledBackAt, username, target, source, restoredRecords);
    }

    private static async Task<List<string>> GetBlockersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PatientIdentity source,
        PatientIdentity target,
        CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        foreach (var tableName in OneToOneTables)
        {
            var sourceCount = await CountRowsAsync(connection, transaction, tableName, true, true, source, cancellationToken);
            var targetCount = await CountRowsAsync(connection, transaction, tableName, true, true, target, cancellationToken);
            if (sourceCount > 0 && targetCount > 0)
            {
                blockers.Add($"both patients have a {tableName} record");
            }
        }

        var careTeamRows = await CountRowsAsync(connection, transaction, "patient_care_teams", true, true, source, cancellationToken);
        if (careTeamRows > 0)
        {
            blockers.Add("the source patient has care-team records, which need dedicated member reconciliation");
        }

        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.Transaction = transaction;
        columnsCommand.CommandText = """
            select table_name,
                   bool_or(column_name = 'patient_id') as has_patient_id,
                   bool_or(column_name = 'pid') as has_pid
            from information_schema.columns
            where table_schema = current_schema()
              and column_name in ('patient_id', 'pid')
            group by table_name;
            """;
        var discovered = new List<(string TableName, bool HasPatientId, bool HasPid)>();
        {
            await using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                discovered.Add((reader.GetString(0), reader.GetBoolean(1), reader.GetBoolean(2)));
            }
        }

        // Npgsql permits one active command per connection. Dispose the schema reader
        // before counting dependencies using the same transactional connection.
        foreach (var table in discovered.Where(table => !SupportedTableNames.Contains(table.TableName) && table.TableName != "patient_care_teams" && table.TableName != "patient_care_team_members"))
        {
            var count = await CountRowsAsync(connection, transaction, table.TableName, table.HasPatientId, table.HasPid, source, cancellationToken);
            if (count > 0)
            {
                blockers.Add($"unsupported source dependency in {table.TableName}");
            }
        }

        return blockers;
    }

    private static async Task<int> CountRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        bool hasPatientId,
        bool hasPid,
        PatientIdentity patient,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select count(*) from {QuoteIdentifier(tableName)} where {BuildSourcePredicate(hasPatientId, hasPid)};";
        AddPatientParameters(command, patient);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<List<string>> GetSourceRecordIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MergeTable table,
        PatientIdentity source,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select {QuoteIdentifier(table.PrimaryKey)}::text from {QuoteIdentifier(table.Name)} where {BuildSourcePredicate(table.HasPatientId, table.HasPid)} order by {QuoteIdentifier(table.PrimaryKey)};";
        AddPatientParameters(command, source);
        var ids = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static async Task InsertManifestRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        string tableName,
        IReadOnlyList<string> recordIds,
        CancellationToken cancellationToken)
    {
        foreach (var recordId in recordIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_merge_execution_manifest_rows (execution_id, table_name, record_id)
                values (@executionId, @tableName, @recordId);
                """;
            command.Parameters.AddWithValue("executionId", executionId);
            command.Parameters.AddWithValue("tableName", tableName);
            command.Parameters.AddWithValue("recordId", recordId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task MoveRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MergeTable table,
        IReadOnlyList<string> recordIds,
        PatientIdentity destination,
        CancellationToken cancellationToken)
    {
        var assignments = new List<string>();
        if (table.HasPatientId)
        {
            assignments.Add("patient_id = @patientId");
        }

        if (table.HasPid)
        {
            assignments.Add("pid = @legacyPid");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"update {QuoteIdentifier(table.Name)} set {string.Join(", ", assignments)} where {QuoteIdentifier(table.PrimaryKey)}::text = any(@recordIds);";
        command.Parameters.AddWithValue("patientId", destination.CanonicalId);
        command.Parameters.AddWithValue("legacyPid", destination.LegacyPid);
        command.Parameters.AddWithValue("recordIds", NpgsqlDbType.Array | NpgsqlDbType.Text, recordIds.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, string[]>> GetManifestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select table_name, array_agg(record_id order by record_id)
            from patient_merge_execution_manifest_rows
            where execution_id = @executionId
            group by table_name;
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        var results = new Dictionary<string, string[]>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results[reader.GetString(0)] = reader.GetFieldValue<string[]>(1);
        }

        return results;
    }

    private static async Task<MergeAudit?> GetAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid auditId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select audit_id, target_patient_id, source_patient_id, status
            from patient_merge_audit_plans
            where audit_id = @auditId
            for update;
            """;
        command.Parameters.AddWithValue("auditId", auditId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MergeAudit(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    private static async Task<MergeExecution?> GetExecutionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select execution_id, audit_id, target_patient_id, source_patient_id, status
            from patient_merge_executions
            where execution_id = @executionId
            for update;
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MergeExecution(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4))
            : null;
    }

    private static async Task<PatientIdentity?> GetPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid, merged_into_patient_id
            from patients
            where lower(canonical_id) = lower(@patientId)
            for update;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PatientIdentity(reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static async Task InsertExecutionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        MergeAudit audit,
        string username,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_merge_executions (
                execution_id, audit_id, target_patient_id, source_patient_id, executed_by, executed_at, status)
            values (@executionId, @auditId, @targetPatientId, @sourcePatientId, @executedBy, @executedAt, 'Executed');
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        command.Parameters.AddWithValue("auditId", audit.AuditId);
        command.Parameters.AddWithValue("targetPatientId", audit.TargetPatientId);
        command.Parameters.AddWithValue("sourcePatientId", audit.SourcePatientId);
        command.Parameters.AddWithValue("executedBy", username);
        command.Parameters.AddWithValue("executedAt", executedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetSourceMergeStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PatientIdentity source,
        PatientIdentity target,
        string username,
        DateTimeOffset changedAt,
        bool rollback,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = rollback
            ? "update patients set merged_into_patient_id = null, merged_at = null, merged_by = null where canonical_id = @sourcePatientId;"
            : "update patients set merged_into_patient_id = @targetPatientId, merged_at = @changedAt, merged_by = @username where canonical_id = @sourcePatientId;";
        command.Parameters.AddWithValue("sourcePatientId", source.CanonicalId);
        command.Parameters.AddWithValue("targetPatientId", target.CanonicalId);
        command.Parameters.AddWithValue("changedAt", changedAt);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetAuditStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid auditId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update patient_merge_audit_plans set status = @status where audit_id = @auditId;";
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("auditId", auditId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetExecutionStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid executionId,
        string status,
        string username,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update patient_merge_executions
            set status = @status, rolled_back_by = @username, rolled_back_at = @changedAt
            where execution_id = @executionId;
            """;
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("changedAt", changedAt);
        command.Parameters.AddWithValue("executionId", executionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            alter table patients add column if not exists merged_into_patient_id text references patients(canonical_id);
            alter table patients add column if not exists merged_at timestamptz;
            alter table patients add column if not exists merged_by text;

            create table if not exists patient_merge_executions (
                execution_id uuid primary key,
                audit_id uuid not null references patient_merge_audit_plans(audit_id),
                target_patient_id text not null references patients(canonical_id),
                source_patient_id text not null references patients(canonical_id),
                executed_by text not null,
                executed_at timestamptz not null,
                rolled_back_by text,
                rolled_back_at timestamptz,
                status text not null
            );

            create table if not exists patient_merge_execution_manifest_rows (
                execution_id uuid not null references patient_merge_executions(execution_id),
                table_name text not null,
                record_id text not null,
                primary key (execution_id, table_name, record_id)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PatientMergeExecutionResponse ToResponse(
        Guid executionId,
        Guid auditId,
        string status,
        DateTimeOffset changedAt,
        string username,
        PatientIdentity target,
        PatientIdentity source,
        IReadOnlyList<PatientMergeExecutionTableCount> movedRecords) =>
        new(
            executionId,
            auditId,
            status,
            changedAt.ToString("O"),
            username,
            target.CanonicalId,
            source.CanonicalId,
            movedRecords,
            new[]
            {
                "Execution is atomic and records every moved primary key in an immutable manifest.",
                "One-to-one account, employer, and history conflicts and care-team records block execution.",
                "Rollback restores only the manifest-recorded rows and reactivates the source patient."
            });

    private static string BuildSourcePredicate(bool hasPatientId, bool hasPid) =>
        hasPatientId && hasPid
            ? "patient_id = @patientId or pid = @legacyPid"
            : hasPatientId
                ? "patient_id = @patientId"
                : "pid = @legacyPid";

    private static void AddPatientParameters(NpgsqlCommand command, PatientIdentity patient)
    {
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        command.Parameters.AddWithValue("legacyPid", patient.LegacyPid);
    }

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed record MergeTable(string Name, string PrimaryKey, bool HasPatientId, bool HasPid);
    private sealed record MergeAudit(Guid AuditId, string TargetPatientId, string SourcePatientId, string Status);
    private sealed record MergeExecution(Guid ExecutionId, Guid AuditId, string TargetPatientId, string SourcePatientId, string Status);
    private sealed record PatientIdentity(string CanonicalId, int LegacyPid, string? MergedIntoPatientId);
}
