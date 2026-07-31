// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using AvenChart.Api.Models;
using AvenChart.Api.Workflows;

namespace AvenChart.Api.Data;

public sealed class ReferralRepository(NpgsqlDataSource dataSource)
{
    private const string ReferralProjection = """
        select r.id, r.patient_id, r.encounter_id, r.destination, r.reason, r.status,
          r.external_reference, r.notes, r.requested_at, r.workflow_version,
          coalesce(r.assigned_to, r.created_by, 'admin') as assigned_to,
          coalesce(a.display_name, r.assigned_to, r.created_by, 'Unassigned') as assigned_display_name,
          r.due_at, coalesce(r.created_by, 'legacy') as created_by,
          r.created_at, r.updated_at
        from referrals r
        left join auth_accounts a on lower(a.username) = lower(r.assigned_to)
        """;

    public async Task<IReadOnlyList<ReferralItem>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, null, patientId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {ReferralProjection}
            where r.patient_id = @patientId
            order by r.requested_at desc, r.created_at desc;
            """;
        command.Parameters.AddWithValue("patientId", canonicalId);
        return await ReadItemsAsync(command, cancellationToken);
    }

    public async Task<ReferralItem?> GetByIdAsync(string patientId, Guid referralId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, null, patientId, cancellationToken);
        return await ReadReferralAsync(connection, null, canonicalId, referralId, false, cancellationToken);
    }

    public async Task<ReferralWorkQueueResponse> GetWorkQueueAsync(
        string? status,
        string? assignedTo,
        bool overdueOnly,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = status?.Trim().ToLowerInvariant() is "all" or null or "" ? null : status!.Trim().ToLowerInvariant();
        if (normalizedStatus is not null && normalizedStatus is not ("draft" or "sent" or "received" or "closed" or "cancelled"))
        {
            throw new ArgumentException("Referral queue status must be draft, sent, received, closed, cancelled, or all.");
        }

        var assigneeFilter = TrimToNull(assignedTo)?.ToLowerInvariant();
        var queryFilter = TrimToNull(query)?.ToLowerInvariant();
        var safeLimit = Math.Clamp(limit, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = """
            select count(*) as total,
              count(*) filter (where r.status in ('draft', 'sent', 'received')) as active_count,
              count(*) filter (where r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()) as overdue_count
            from referrals r join patients p on p.canonical_id = r.patient_id
            where (@status::text is null or r.status = @status::text)
              and (@assignedTo::text is null or lower(coalesce(r.assigned_to, r.created_by, 'admin')) = @assignedTo::text)
              and (@overdueOnly = false or (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()))
              and (@query::text is null or lower(r.destination) like '%' || @query::text || '%' or lower(r.reason) like '%' || @query::text || '%' or lower(p.canonical_id) like '%' || @query::text || '%' or lower(p.pubpid) like '%' || @query::text || '%' or lower(concat(p.last_name, ', ', p.first_name)) like '%' || @query::text || '%');
            """;
        count.Parameters.AddWithValue("status", (object?)normalizedStatus ?? DBNull.Value); count.Parameters.AddWithValue("assignedTo", (object?)assigneeFilter ?? DBNull.Value); count.Parameters.AddWithValue("overdueOnly", overdueOnly); count.Parameters.AddWithValue("query", (object?)queryFilter ?? DBNull.Value);
        int total; int activeCount; int overdueCount;
        await using (var reader = await count.ExecuteReaderAsync(cancellationToken)) { await reader.ReadAsync(cancellationToken); total = Convert.ToInt32(reader.GetValue(0)); activeCount = Convert.ToInt32(reader.GetValue(1)); overdueCount = Convert.ToInt32(reader.GetValue(2)); }
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select r.id, r.patient_id, r.encounter_id, r.destination, r.reason, r.status, r.external_reference, r.notes, r.requested_at, r.workflow_version, coalesce(r.assigned_to, r.created_by, 'admin') as assigned_to, coalesce(a.display_name, r.assigned_to, r.created_by, 'Unassigned') as assigned_display_name, r.due_at, coalesce(r.created_by, 'legacy') as created_by, r.created_at, r.updated_at, trim(concat(p.last_name, ', ', p.first_name)) as patient_display_name, p.pubpid, (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()) as is_overdue
            from referrals r join patients p on p.canonical_id = r.patient_id left join auth_accounts a on lower(a.username) = lower(r.assigned_to)
            where (@status::text is null or r.status = @status::text) and (@assignedTo::text is null or lower(coalesce(r.assigned_to, r.created_by, 'admin')) = @assignedTo::text) and (@overdueOnly = false or (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now())) and (@query::text is null or lower(r.destination) like '%' || @query::text || '%' or lower(r.reason) like '%' || @query::text || '%' or lower(p.canonical_id) like '%' || @query::text || '%' or lower(p.pubpid) like '%' || @query::text || '%' or lower(concat(p.last_name, ', ', p.first_name)) like '%' || @query::text || '%')
            order by (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()) desc, r.due_at nulls last, r.requested_at desc limit @limit;
            """;
        command.Parameters.AddWithValue("status", (object?)normalizedStatus ?? DBNull.Value); command.Parameters.AddWithValue("assignedTo", (object?)assigneeFilter ?? DBNull.Value); command.Parameters.AddWithValue("overdueOnly", overdueOnly); command.Parameters.AddWithValue("query", (object?)queryFilter ?? DBNull.Value); command.Parameters.AddWithValue("limit", safeLimit);
        var items = new List<ReferralWorkQueueItem>();
        await using var itemReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await itemReader.ReadAsync(cancellationToken))
        {
            var statusValue = itemReader.GetString(5);
            var referral = new ReferralItem(itemReader.GetGuid(0), itemReader.GetString(1), itemReader.IsDBNull(2) ? null : itemReader.GetInt32(2), itemReader.GetString(3), itemReader.GetString(4), statusValue, ReadNullableString(itemReader, 6), ReadNullableString(itemReader, 7), itemReader.GetFieldValue<DateTimeOffset>(8).ToString("O"), itemReader.GetInt32(9), itemReader.GetString(10), itemReader.GetString(11), itemReader.IsDBNull(12) ? null : itemReader.GetFieldValue<DateTimeOffset>(12).ToString("O"), itemReader.GetString(13), ClinicalWorkflowPolicyCatalog.Revision, itemReader.GetFieldValue<DateTimeOffset>(14).ToString("O"), itemReader.GetFieldValue<DateTimeOffset>(15).ToString("O"), ClinicalWorkflowPolicyCatalog.GetAvailableReferralTransitions(statusValue));
            items.Add(new ReferralWorkQueueItem(referral, itemReader.GetString(16), itemReader.GetString(17), itemReader.GetBoolean(18)));
        }
        return new ReferralWorkQueueResponse(total, activeCount, overdueCount, items);
    }

    public async Task<ReferralItem> CreateAsync(string patientId, ReferralCreateRequest request, string actor, CancellationToken cancellationToken)
    {
        var destination = RequireText(request.Destination, "Referral destination", 240);
        var reason = RequireText(request.Reason, "Referral reason", 1000);
        var workflowReason = ClinicalWorkflowPolicyCatalog.RequireReason(request.WorkflowReason ?? "Referral draft created.");
        if (!TryParse(request.RequestedAt, out var requestedAt) || !TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException("Requested and responsibility due dates must be valid ISO dates or date-times.");
        }

        if (dueAt is not null && dueAt.Value.UtcDateTime.Date < requestedAt.UtcDateTime.Date)
        {
            throw new ArgumentException("Responsibility due date cannot be before the requested date.");
        }

        var assignedTo = TrimToNull(request.AssignedTo) ?? actor;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, transaction, patientId, cancellationToken);
        await ValidateAssigneeAsync(connection, transaction, assignedTo, cancellationToken);
        await ValidateEncounterAsync(connection, transaction, canonicalId, request.EncounterId, cancellationToken);

        var referralId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into referrals (
                  id, patient_id, encounter_id, destination, reason, status,
                  external_reference, notes, requested_at, workflow_version,
                  assigned_to, due_at, created_by, created_at, updated_at)
                values (
                  @id, @patientId, @encounterId, @destination, @reason, 'draft',
                  @externalReference, @notes, @requestedAt, 1,
                  @assignedTo, @dueAt, @actor, @now, @now);
                """;
            command.Parameters.AddWithValue("id", referralId);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue("encounterId", (object?)request.EncounterId ?? DBNull.Value);
            command.Parameters.AddWithValue("destination", destination);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("externalReference", (object?)TrimToNull(request.ExternalReference) ?? DBNull.Value);
            command.Parameters.AddWithValue("notes", (object?)TrimToNull(request.Notes) ?? DBNull.Value);
            command.Parameters.AddWithValue("requestedAt", requestedAt);
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            command.Parameters.AddWithValue("dueAt", (object?)dueAt ?? DBNull.Value);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(connection, transaction, referralId, canonicalId, 1, "created", null, "draft", null, assignedTo, "referral-draft-created", workflowReason, actor, now, cancellationToken);
        var created = await ReadReferralAsync(connection, transaction, canonicalId, referralId, false, cancellationToken)
            ?? throw new InvalidOperationException("The referral draft was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<ReferralItem> UpdateStatusAsync(string patientId, Guid referralId, ReferralStatusRequest request, string actor, CancellationToken cancellationToken)
    {
        var nextState = request.Status?.Trim().ToLowerInvariant();
        if (nextState is not ("sent" or "received" or "closed" or "cancelled"))
        {
            throw new ArgumentException("Referral status must be sent, received, closed, or cancelled.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, transaction, patientId, cancellationToken);
        var current = await ReadReferralAsync(connection, transaction, canonicalId, referralId, true, cancellationToken)
            ?? throw new ArgumentException("Referral was not found.");
        await EnsureEncounterIsUnlockedAsync(connection, transaction, current.EncounterId, cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, current.WorkflowVersion);
        var transition = ClinicalWorkflowPolicyCatalog.RequireReferralTransition(current.Status, nextState, request.ReasonCode, request.Reason);
        var nextVersion = current.WorkflowVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update referrals
                set status = @status, workflow_version = @workflowVersion, updated_at = @now
                where id = @id and patient_id = @patientId and workflow_version = @expectedVersion;
                """;
            command.Parameters.AddWithValue("status", nextState);
            command.Parameters.AddWithValue("workflowVersion", nextVersion);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("id", referralId);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue("expectedVersion", current.WorkflowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ClinicalWorkflowVersionConflictException(request.ExpectedVersion, current.WorkflowVersion);
            }
        }

        await InsertEventAsync(connection, transaction, referralId, canonicalId, nextVersion, transition.Action, current.Status, nextState, current.AssignedTo, current.AssignedTo, transition.ReasonCode, request.Reason.Trim(), actor, now, cancellationToken);
        var updated = await ReadReferralAsync(connection, transaction, canonicalId, referralId, false, cancellationToken)
            ?? throw new InvalidOperationException("The updated referral was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<ReferralItem> UpdateAssignmentAsync(string patientId, Guid referralId, ReferralAssignmentRequest request, string actor, CancellationToken cancellationToken)
    {
        var assignedTo = RequireText(request.AssignedTo, "Responsible staff user", 80);
        if (!TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException("Responsibility due date must be a valid ISO date or date-time.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, transaction, patientId, cancellationToken);
        var current = await ReadReferralAsync(connection, transaction, canonicalId, referralId, true, cancellationToken)
            ?? throw new ArgumentException("Referral was not found.");
        await EnsureEncounterIsUnlockedAsync(connection, transaction, current.EncounterId, cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, current.WorkflowVersion);
        ClinicalWorkflowPolicyCatalog.RequireAssignmentChange(current.Status, request.ReasonCode, request.Reason);
        await ValidateAssigneeAsync(connection, transaction, assignedTo, cancellationToken);
        if (dueAt is not null && dueAt.Value.UtcDateTime.Date < DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Responsibility due date cannot be in the past.");
        }

        if (string.Equals(current.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase) && Nullable.Equals(current.DueAt, dueAt))
        {
            throw new ArgumentException("Responsibility assignment must change the assignee or due date.");
        }

        var nextVersion = current.WorkflowVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update referrals
                set assigned_to = @assignedTo, due_at = @dueAt,
                    workflow_version = @workflowVersion, updated_at = @now
                where id = @id and patient_id = @patientId and workflow_version = @expectedVersion;
                """;
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            command.Parameters.AddWithValue("dueAt", (object?)dueAt ?? DBNull.Value);
            command.Parameters.AddWithValue("workflowVersion", nextVersion);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("id", referralId);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue("expectedVersion", current.WorkflowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ClinicalWorkflowVersionConflictException(request.ExpectedVersion, current.WorkflowVersion);
            }
        }

        await InsertEventAsync(connection, transaction, referralId, canonicalId, nextVersion, "reassigned", current.Status, current.Status, current.AssignedTo, assignedTo, ClinicalWorkflowPolicyCatalog.ResponsibilityTransferReasonCode, request.Reason.Trim(), actor, now, cancellationToken);
        var updated = await ReadReferralAsync(connection, transaction, canonicalId, referralId, false, cancellationToken)
            ?? throw new InvalidOperationException("The reassigned referral was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<ReferralWorkflowHistoryResponse?> GetHistoryAsync(string patientId, Guid referralId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(connection, null, patientId, cancellationToken);
        var referral = await ReadReferralAsync(connection, null, canonicalId, referralId, false, cancellationToken);
        if (referral is null) return null;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, workflow_version, action, from_state, to_state, from_assigned_to, to_assigned_to,
              reason_code, reason, actor, policy_revision, occurred_at
            from clinical_workflow_events
            where workflow_type = @workflowType and entity_id = @entityId
            order by workflow_version desc, occurred_at desc;
            """;
        command.Parameters.AddWithValue("workflowType", ClinicalWorkflowPolicyCatalog.PatientReferralWorkflow);
        command.Parameters.AddWithValue("entityId", referralId.ToString());
        var events = new List<ReferralWorkflowEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ReferralWorkflowEvent(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), ReadNullableString(reader, 3), reader.GetString(4), ReadNullableString(reader, 5), ReadNullableString(reader, 6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11).ToString("O")));
        }
        return new ReferralWorkflowHistoryResponse(referral, events.Count, events);
    }

    private static async Task<ReferralItem?> ReadReferralAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string patientId, Guid referralId, bool lockRow, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            {ReferralProjection}
            where r.id = @id and r.patient_id = @patientId
            {(lockRow ? "for update of r" : string.Empty)};
            """;
        command.Parameters.AddWithValue("id", referralId);
        command.Parameters.AddWithValue("patientId", patientId);
        return (await ReadItemsAsync(command, cancellationToken)).SingleOrDefault();
    }

    private static async Task<IReadOnlyList<ReferralItem>> ReadItemsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var referrals = new List<ReferralItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader.GetString(reader.GetOrdinal("status"));
            referrals.Add(new ReferralItem(reader.GetGuid(reader.GetOrdinal("id")), reader.GetString(reader.GetOrdinal("patient_id")), reader.IsDBNull(reader.GetOrdinal("encounter_id")) ? null : reader.GetInt32(reader.GetOrdinal("encounter_id")), reader.GetString(reader.GetOrdinal("destination")), reader.GetString(reader.GetOrdinal("reason")), status, ReadNullableString(reader, "external_reference"), ReadNullableString(reader, "notes"), reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("requested_at")).ToString("O"), reader.GetInt32(reader.GetOrdinal("workflow_version")), reader.GetString(reader.GetOrdinal("assigned_to")), reader.GetString(reader.GetOrdinal("assigned_display_name")), ReadNullableDateTimeOffset(reader, "due_at")?.ToString("O"), reader.GetString(reader.GetOrdinal("created_by")), ClinicalWorkflowPolicyCatalog.Revision, reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")).ToString("O"), reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")).ToString("O"), ClinicalWorkflowPolicyCatalog.GetAvailableReferralTransitions(status)));
        }
        return referrals;
    }

    private static async Task InsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid referralId, string patientId, int workflowVersion, string action, string? fromState, string toState, string? fromAssignedTo, string? toAssignedTo, string reasonCode, string reason, string actor, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_workflow_events (event_id, workflow_type, entity_id, patient_id, workflow_version, action, from_state, to_state, from_assigned_to, to_assigned_to, reason_code, reason, actor, policy_revision, occurred_at)
            values (@eventId, @workflowType, @entityId, @patientId, @workflowVersion, @action, @fromState, @toState, @fromAssignedTo, @toAssignedTo, @reasonCode, @reason, @actor, @policyRevision, @occurredAt);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid()); command.Parameters.AddWithValue("workflowType", ClinicalWorkflowPolicyCatalog.PatientReferralWorkflow); command.Parameters.AddWithValue("entityId", referralId.ToString()); command.Parameters.AddWithValue("patientId", patientId); command.Parameters.AddWithValue("workflowVersion", workflowVersion); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("fromState", (object?)fromState ?? DBNull.Value); command.Parameters.AddWithValue("toState", toState); command.Parameters.AddWithValue("fromAssignedTo", (object?)fromAssignedTo ?? DBNull.Value); command.Parameters.AddWithValue("toAssignedTo", (object?)toAssignedTo ?? DBNull.Value); command.Parameters.AddWithValue("reasonCode", reasonCode); command.Parameters.AddWithValue("reason", reason); command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("policyRevision", ClinicalWorkflowPolicyCatalog.Revision); command.Parameters.AddWithValue("occurredAt", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ValidateEncounterAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string patientId, int? encounterId, CancellationToken cancellationToken)
    {
        if (encounterId is null) return;
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select count(*) from encounters where encounter = @encounterId and lower(patient_id) = lower(@patientId);";
        command.Parameters.AddWithValue("encounterId", encounterId.Value); command.Parameters.AddWithValue("patientId", patientId);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0) == 0) throw new ArgumentException("Referral encounter does not belong to this patient.");
        await EnsureEncounterIsUnlockedAsync(connection, transaction, encounterId, cancellationToken);
    }

    private static async Task EnsureEncounterIsUnlockedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int? encounterId, CancellationToken cancellationToken)
    {
        if (encounterId is null) return;
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select count(*) from encounter_signatures where encounter = @encounter and is_lock;";
        command.Parameters.AddWithValue("encounter", encounterId.Value);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0) > 0) throw new EncounterLockConflictException("This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
    }

    private static async Task ValidateAssigneeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string assignedTo, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select count(*) from auth_accounts where active = true and lower(username) = lower(@assignedTo);";
        command.Parameters.AddWithValue("assignedTo", assignedTo);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0) == 0) throw new ArgumentException("Responsible staff user must be an active account.");
    }

    private static async Task<string> ResolvePatientIdAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string patientId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select canonical_id from patients where lower(canonical_id) = lower(@patientId) or lower(pubpid) = lower(@patientId) limit 1;";
        command.Parameters.AddWithValue("patientId", patientId.Trim());
        return await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new ArgumentException("Patient was not found.");
    }

    private static void RequireExpectedVersion(int expectedVersion, int currentVersion) { if (expectedVersion != currentVersion) throw new ClinicalWorkflowVersionConflictException(expectedVersion, currentVersion); }
    private static string RequireText(string? value, string label, int maximumLength) { var normalized = TrimToNull(value); return normalized is null || normalized.Length > maximumLength ? throw new ArgumentException($"{label} is required and must be {maximumLength} characters or fewer.") : normalized; }
    private static bool TryParse(string? value, out DateTimeOffset result) { if (string.IsNullOrWhiteSpace(value)) { result = DateTimeOffset.UtcNow; return true; } return DateTimeOffset.TryParse(value, out result); }
    private static bool TryParseNullable(string? value, out DateTimeOffset? result) { if (string.IsNullOrWhiteSpace(value)) { result = null; return true; } if (DateTimeOffset.TryParse(value, out var parsed)) { result = parsed; return true; } result = null; return false; }
    private static string? ReadNullableString(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));
    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static DateTimeOffset? ReadNullableDateTimeOffset(NpgsqlDataReader reader, string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
