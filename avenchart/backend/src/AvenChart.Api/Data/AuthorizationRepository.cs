using Npgsql;
using AvenChart.Api.Models;
using AvenChart.Api.Workflows;

namespace AvenChart.Api.Data;

public sealed class AuthorizationRepository(NpgsqlDataSource dataSource)
{
    private const string FixturePrefix = "TMP-CLIN-AUTH-";

    private const string AuthorizationProjection = """
        select a.id, a.patient_id, a.referral_id, a.payer, a.service, a.status,
          a.authorization_number, a.requested_at, a.expires_at,
          a.workflow_version,
          coalesce(a.assigned_to, a.created_by, 'admin') as assigned_to,
          coalesce(aa.display_name, a.assigned_to, a.created_by, 'Unassigned') as assigned_display_name,
          a.due_at,
          coalesce(a.created_by, 'legacy') as created_by,
          a.created_at, a.updated_at
        from authorizations a
        left join auth_accounts aa on lower(aa.username) = lower(a.assigned_to)
        """;

    public async Task<IReadOnlyList<AuthorizationItem>> GetAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            null,
            patientId,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {AuthorizationProjection}
            where a.patient_id = @patientId
            order by a.requested_at desc, a.created_at desc;
            """;
        command.Parameters.AddWithValue("patientId", canonicalId);
        return await ReadItemsAsync(command, cancellationToken);
    }

    public async Task<AuthorizationItem?> GetByIdAsync(
        string patientId,
        Guid authorizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            null,
            patientId,
            cancellationToken);
        return await ReadAuthorizationAsync(
            connection,
            null,
            canonicalId,
            authorizationId,
            lockRow: false,
            cancellationToken);
    }

    public async Task<ClinicalWorkflowAssigneesResponse> GetAssigneesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select staff_id, username, display_name, role
            from auth_accounts
            where active = true
            order by display_name, username;
            """;
        var assignees = new List<ClinicalWorkflowAssignee>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assignees.Add(new ClinicalWorkflowAssignee(
                reader.IsDBNull(reader.GetOrdinal("staff_id"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("staff_id")),
                reader.GetString(reader.GetOrdinal("username")),
                reader.GetString(reader.GetOrdinal("display_name")),
                reader.GetString(reader.GetOrdinal("role"))));
        }

        return new ClinicalWorkflowAssigneesResponse(
            ClinicalWorkflowPolicyCatalog.Revision,
            assignees.Count,
            assignees);
    }

    public async Task<AuthorizationItem> CreateAsync(
        string patientId,
        AuthorizationCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var payer = RequireText(request.Payer, "Payer", 240);
        var service = RequireText(request.Service, "Service", 500);
        var creationReason = ClinicalWorkflowPolicyCatalog.RequireReason(request.Reason);
        if (!TryParse(request.RequestedAt, out var requestedAt)
            || !TryParseNullable(request.ExpiresAt, out var expiresAt)
            || !TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException(
                "Requested, expiry, and responsibility due dates must be valid ISO dates or date-times.");
        }

        if (expiresAt is not null
            && expiresAt.Value.UtcDateTime.Date < requestedAt.UtcDateTime.Date)
        {
            throw new ArgumentException("Expiry cannot be before the requested date.");
        }

        if (dueAt is not null
            && dueAt.Value.UtcDateTime.Date < requestedAt.UtcDateTime.Date)
        {
            throw new ArgumentException(
                "Responsibility due date cannot be before the requested date.");
        }

        var assignedTo = TrimToNull(request.AssignedTo) ?? actor;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        await ValidateAssigneeAsync(
            connection,
            transaction,
            assignedTo,
            cancellationToken);
        if (request.ReferralId is not null)
        {
            await ValidateReferralAsync(
                connection,
                transaction,
                canonicalId,
                request.ReferralId.Value,
                cancellationToken);
            await EnsureReferralEncounterIsUnlockedAsync(
                connection,
                transaction,
                request.ReferralId,
                cancellationToken);
        }

        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into authorizations (
                  id, patient_id, referral_id, payer, service, status,
                  authorization_number, requested_at, expires_at,
                  workflow_version, assigned_to, due_at, created_by,
                  created_at, updated_at)
                values (
                  @id, @patientId, @referralId, @payer, @service, 'draft',
                  null, @requestedAt, @expiresAt,
                  1, @assignedTo, @dueAt, @actor,
                  @now, @now);
                """;
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue(
                "referralId",
                (object?)request.ReferralId ?? DBNull.Value);
            command.Parameters.AddWithValue("payer", payer);
            command.Parameters.AddWithValue("service", service);
            command.Parameters.AddWithValue("requestedAt", requestedAt);
            command.Parameters.AddWithValue("expiresAt", (object?)expiresAt ?? DBNull.Value);
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            command.Parameters.AddWithValue("dueAt", (object?)dueAt ?? DBNull.Value);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            id,
            canonicalId,
            workflowVersion: 1,
            action: "created",
            fromState: null,
            toState: "draft",
            fromAssignedTo: null,
            toAssignedTo: assignedTo,
            reasonCode: "authorization-draft-created",
            reason: creationReason,
            actor,
            now,
            cancellationToken);
        var created = await ReadAuthorizationAsync(
                connection,
                transaction,
                canonicalId,
                id,
                lockRow: false,
                cancellationToken)
            ?? throw new InvalidOperationException("The authorization draft was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<AuthorizationItem> UpdateStatusAsync(
        string patientId,
        Guid authorizationId,
        AuthorizationStatusRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var nextState = request.Status?.Trim().ToLowerInvariant();
        if (nextState is not ("submitted" or "approved" or "denied" or "expired" or "cancelled"))
        {
            throw new ArgumentException(
                "Authorization status must be submitted, approved, denied, expired, or cancelled.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        var current = await ReadAuthorizationAsync(
                connection,
                transaction,
                canonicalId,
                authorizationId,
                lockRow: true,
                cancellationToken)
            ?? throw new ArgumentException("Authorization was not found.");
        await EnsureReferralEncounterIsUnlockedAsync(
            connection,
            transaction,
            current.ReferralId,
            cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, current.WorkflowVersion);
        var transition = ClinicalWorkflowPolicyCatalog.RequireAuthorizationTransition(
            current.Status,
            nextState,
            request.ReasonCode,
            request.Reason);
        var authorizationNumber = TrimToNull(request.AuthorizationNumber);
        if (transition.RequiresAuthorizationNumber && authorizationNumber is null)
        {
            throw new ArgumentException("An approval requires an authorization number.");
        }

        if (authorizationNumber?.Length > 120)
        {
            throw new ArgumentException(
                "Authorization number must be 120 characters or fewer.");
        }

        var nextVersion = current.WorkflowVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update authorizations
                set status = @status,
                  authorization_number = case
                    when @status = 'approved' then @authorizationNumber
                    else authorization_number
                  end,
                  workflow_version = @workflowVersion,
                  updated_at = @now
                where id = @id and patient_id = @patientId
                  and workflow_version = @expectedVersion;
                """;
            command.Parameters.AddWithValue("status", nextState);
            command.Parameters.AddWithValue(
                "authorizationNumber",
                (object?)authorizationNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("workflowVersion", nextVersion);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("id", authorizationId);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue("expectedVersion", current.WorkflowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ClinicalWorkflowVersionConflictException(
                    request.ExpectedVersion,
                    current.WorkflowVersion);
            }
        }

        await InsertEventAsync(
            connection,
            transaction,
            authorizationId,
            canonicalId,
            nextVersion,
            transition.Action,
            current.Status,
            nextState,
            current.AssignedTo,
            current.AssignedTo,
            transition.ReasonCode,
            request.Reason.Trim(),
            actor,
            now,
            cancellationToken);
        var updated = await ReadAuthorizationAsync(
                connection,
                transaction,
                canonicalId,
                authorizationId,
                lockRow: false,
                cancellationToken)
            ?? throw new InvalidOperationException("The updated authorization was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<AuthorizationItem> UpdateAssignmentAsync(
        string patientId,
        Guid authorizationId,
        AuthorizationAssignmentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var assignedTo = RequireText(request.AssignedTo, "Responsible staff user", 80);
        if (!TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException(
                "Responsibility due date must be a valid ISO date or date-time.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        var current = await ReadAuthorizationAsync(
                connection,
                transaction,
                canonicalId,
                authorizationId,
                lockRow: true,
                cancellationToken)
            ?? throw new ArgumentException("Authorization was not found.");
        await EnsureReferralEncounterIsUnlockedAsync(
            connection,
            transaction,
            current.ReferralId,
            cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, current.WorkflowVersion);
        ClinicalWorkflowPolicyCatalog.RequireAssignmentChange(
            current.Status,
            request.ReasonCode,
            request.Reason);
        await ValidateAssigneeAsync(
            connection,
            transaction,
            assignedTo,
            cancellationToken);
        if (dueAt is not null && dueAt < DateTimeOffset.UtcNow.Date)
        {
            throw new ArgumentException(
                "Responsibility due date cannot be in the past.");
        }

        var sameAssignee = string.Equals(
            current.AssignedTo,
            assignedTo,
            StringComparison.OrdinalIgnoreCase);
        var sameDueAt = DateTimeOffsetEquals(current.DueAt, dueAt);
        if (sameAssignee && sameDueAt)
        {
            throw new ArgumentException(
                "Responsibility assignment must change the assignee or due date.");
        }

        var nextVersion = current.WorkflowVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update authorizations
                set assigned_to = @assignedTo,
                  due_at = @dueAt,
                  workflow_version = @workflowVersion,
                  updated_at = @now
                where id = @id and patient_id = @patientId
                  and workflow_version = @expectedVersion;
                """;
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            command.Parameters.AddWithValue("dueAt", (object?)dueAt ?? DBNull.Value);
            command.Parameters.AddWithValue("workflowVersion", nextVersion);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("id", authorizationId);
            command.Parameters.AddWithValue("patientId", canonicalId);
            command.Parameters.AddWithValue("expectedVersion", current.WorkflowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ClinicalWorkflowVersionConflictException(
                    request.ExpectedVersion,
                    current.WorkflowVersion);
            }
        }

        await InsertEventAsync(
            connection,
            transaction,
            authorizationId,
            canonicalId,
            nextVersion,
            action: "reassigned",
            fromState: current.Status,
            toState: current.Status,
            fromAssignedTo: current.AssignedTo,
            toAssignedTo: assignedTo,
            reasonCode: ClinicalWorkflowPolicyCatalog.ResponsibilityTransferReasonCode,
            reason: request.Reason.Trim(),
            actor,
            now,
            cancellationToken);
        var updated = await ReadAuthorizationAsync(
                connection,
                transaction,
                canonicalId,
                authorizationId,
                lockRow: false,
                cancellationToken)
            ?? throw new InvalidOperationException("The reassigned authorization was not returned.");
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<AuthorizationWorkflowHistoryResponse?> GetHistoryAsync(
        string patientId,
        Guid authorizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            null,
            patientId,
            cancellationToken);
        var authorization = await ReadAuthorizationAsync(
            connection,
            null,
            canonicalId,
            authorizationId,
            lockRow: false,
            cancellationToken);
        if (authorization is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, workflow_version, action, from_state, to_state,
              from_assigned_to, to_assigned_to, reason_code, reason,
              actor, policy_revision, occurred_at
            from clinical_workflow_events
            where workflow_type = @workflowType and entity_id = @entityId
            order by workflow_version desc, occurred_at desc;
            """;
        command.Parameters.AddWithValue(
            "workflowType",
            ClinicalWorkflowPolicyCatalog.PatientAuthorizationWorkflow);
        command.Parameters.AddWithValue("entityId", authorizationId.ToString());
        var events = new List<AuthorizationWorkflowEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new AuthorizationWorkflowEvent(
                reader.GetGuid(reader.GetOrdinal("event_id")),
                reader.GetInt32(reader.GetOrdinal("workflow_version")),
                reader.GetString(reader.GetOrdinal("action")),
                ReadNullableString(reader, "from_state"),
                reader.GetString(reader.GetOrdinal("to_state")),
                ReadNullableString(reader, "from_assigned_to"),
                ReadNullableString(reader, "to_assigned_to"),
                reader.GetString(reader.GetOrdinal("reason_code")),
                reader.GetString(reader.GetOrdinal("reason")),
                reader.GetString(reader.GetOrdinal("actor")),
                reader.GetString(reader.GetOrdinal("policy_revision")),
                reader.GetFieldValue<DateTimeOffset>(
                        reader.GetOrdinal("occurred_at"))
                    .ToString("O")));
        }

        return new AuthorizationWorkflowHistoryResponse(
            authorization,
            events.Count,
            events);
    }

    public async Task<bool> DeleteFixtureAsync(
        string patientId,
        Guid authorizationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        string? payer;
        string? service;
        Guid? referralId;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select payer, service, referral_id
                from authorizations
                where id = @id and patient_id = @patientId
                for update;
                """;
            select.Parameters.AddWithValue("id", authorizationId);
            select.Parameters.AddWithValue("patientId", canonicalId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            payer = reader.GetString(0);
            service = reader.GetString(1);
            referralId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
        }

        if (!payer.StartsWith(FixturePrefix, StringComparison.Ordinal)
            || !service.StartsWith(FixturePrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Only {FixturePrefix} authorization fixtures can be removed.");
        }

        await EnsureReferralEncounterIsUnlockedAsync(
            connection,
            transaction,
            referralId,
            cancellationToken);

        await using (var deleteEvents = connection.CreateCommand())
        {
            deleteEvents.Transaction = transaction;
            deleteEvents.CommandText = """
                delete from clinical_workflow_events
                where workflow_type = @workflowType and entity_id = @entityId;
                """;
            deleteEvents.Parameters.AddWithValue(
                "workflowType",
                ClinicalWorkflowPolicyCatalog.PatientAuthorizationWorkflow);
            deleteEvents.Parameters.AddWithValue("entityId", authorizationId.ToString());
            await deleteEvents.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteAuthorization = connection.CreateCommand())
        {
            deleteAuthorization.Transaction = transaction;
            deleteAuthorization.CommandText = """
                delete from authorizations
                where id = @id and patient_id = @patientId;
                """;
            deleteAuthorization.Parameters.AddWithValue("id", authorizationId);
            deleteAuthorization.Parameters.AddWithValue("patientId", canonicalId);
            await deleteAuthorization.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<AuthorizationItem?> ReadAuthorizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string patientId,
        Guid authorizationId,
        bool lockRow,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            {AuthorizationProjection}
            where a.id = @id and a.patient_id = @patientId
            {(lockRow ? "for update of a" : string.Empty)};
            """;
        command.Parameters.AddWithValue("id", authorizationId);
        command.Parameters.AddWithValue("patientId", patientId);
        return (await ReadItemsAsync(command, cancellationToken)).SingleOrDefault();
    }

    private static async Task<IReadOnlyList<AuthorizationItem>> ReadItemsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var values = new List<AuthorizationItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader.GetString(reader.GetOrdinal("status"));
            values.Add(new AuthorizationItem(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("patient_id")),
                reader.IsDBNull(reader.GetOrdinal("referral_id"))
                    ? null
                    : reader.GetGuid(reader.GetOrdinal("referral_id")),
                reader.GetString(reader.GetOrdinal("payer")),
                reader.GetString(reader.GetOrdinal("service")),
                status,
                ReadNullableString(reader, "authorization_number"),
                reader.GetFieldValue<DateTimeOffset>(
                        reader.GetOrdinal("requested_at"))
                    .ToString("O"),
                ReadNullableDateTimeOffset(reader, "expires_at")?.ToString("O"),
                reader.GetInt32(reader.GetOrdinal("workflow_version")),
                reader.GetString(reader.GetOrdinal("assigned_to")),
                reader.GetString(reader.GetOrdinal("assigned_display_name")),
                ReadNullableDateTimeOffset(reader, "due_at")?.ToString("O"),
                reader.GetString(reader.GetOrdinal("created_by")),
                ClinicalWorkflowPolicyCatalog.Revision,
                reader.GetFieldValue<DateTimeOffset>(
                        reader.GetOrdinal("created_at"))
                    .ToString("O"),
                reader.GetFieldValue<DateTimeOffset>(
                        reader.GetOrdinal("updated_at"))
                    .ToString("O"),
                ClinicalWorkflowPolicyCatalog.GetAvailableAuthorizationTransitions(
                    status)));
        }

        return values;
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid authorizationId,
        string patientId,
        int workflowVersion,
        string action,
        string? fromState,
        string toState,
        string? fromAssignedTo,
        string? toAssignedTo,
        string reasonCode,
        string reason,
        string actor,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_workflow_events (
              event_id, workflow_type, entity_id, patient_id, workflow_version,
              action, from_state, to_state, from_assigned_to, to_assigned_to,
              reason_code, reason, actor, policy_revision, occurred_at)
            values (
              @eventId, @workflowType, @entityId, @patientId, @workflowVersion,
              @action, @fromState, @toState, @fromAssignedTo, @toAssignedTo,
              @reasonCode, @reason, @actor, @policyRevision, @occurredAt);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue(
            "workflowType",
            ClinicalWorkflowPolicyCatalog.PatientAuthorizationWorkflow);
        command.Parameters.AddWithValue("entityId", authorizationId.ToString());
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("workflowVersion", workflowVersion);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("fromState", (object?)fromState ?? DBNull.Value);
        command.Parameters.AddWithValue("toState", toState);
        command.Parameters.AddWithValue(
            "fromAssignedTo",
            (object?)fromAssignedTo ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "toAssignedTo",
            (object?)toAssignedTo ?? DBNull.Value);
        command.Parameters.AddWithValue("reasonCode", reasonCode);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue(
            "policyRevision",
            ClinicalWorkflowPolicyCatalog.Revision);
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ValidateReferralAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        Guid referralId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1 from referrals
              where id = @referralId and patient_id = @patientId
            );
            """;
        command.Parameters.AddWithValue("referralId", referralId);
        command.Parameters.AddWithValue("patientId", patientId);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException(
                "Authorization referral does not belong to this patient.");
        }
    }

    private static async Task EnsureReferralEncounterIsUnlockedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? referralId,
        CancellationToken cancellationToken)
    {
        if (referralId is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)
            from referrals r
            join encounter_signatures s on s.encounter = r.encounter_id
            where r.id = @referralId and s.is_lock;
            """;
        command.Parameters.AddWithValue("referralId", referralId.Value);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0) > 0)
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }
    }

    private static async Task ValidateAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string assignedTo,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1 from auth_accounts
              where active = true and lower(username) = lower(@assignedTo)
            );
            """;
        command.Parameters.AddWithValue("assignedTo", assignedTo);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException(
                "Responsible staff user must be an active account.");
        }
    }

    private static async Task<string> ResolvePatientIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id
            from patients
            where lower(canonical_id) = lower(@patientId)
              or lower(pubpid) = lower(@patientId)
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId.Trim());
        return await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new ArgumentException("Patient was not found.");
    }

    private static void RequireExpectedVersion(int expectedVersion, int currentVersion)
    {
        if (expectedVersion != currentVersion)
        {
            throw new ClinicalWorkflowVersionConflictException(
                expectedVersion,
                currentVersion);
        }
    }

    private static string RequireText(string? value, string label, int maximumLength)
    {
        var normalized = TrimToNull(value);
        if (normalized is null || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{label} is required and must be {maximumLength} characters or fewer.");
        }

        return normalized;
    }

    private static bool TryParse(string? value, out DateTimeOffset result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = DateTimeOffset.UtcNow;
            return true;
        }

        return DateTimeOffset.TryParse(value, out result);
    }

    private static bool TryParseNullable(string? value, out DateTimeOffset? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ReadNullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(
        NpgsqlDataReader reader,
        string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static bool DateTimeOffsetEquals(string? current, DateTimeOffset? proposed)
    {
        if (current is null)
        {
            return proposed is null;
        }

        return DateTimeOffset.TryParse(current, out var parsed)
            && proposed.HasValue
            && parsed.ToUniversalTime() == proposed.Value.ToUniversalTime();
    }
}
