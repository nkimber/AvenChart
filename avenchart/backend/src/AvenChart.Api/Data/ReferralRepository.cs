// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using AvenChart.Api.Workflows;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

public sealed class ReferralRepository(
    NpgsqlDataSource dataSource,
    AvenChartDbContext dbContext)
{
    public async Task<IReadOnlyList<ReferralItem>> GetAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var canonicalId = await ResolvePatientIdAsync(patientId, cancellationToken);
        var referrals = await dbContext.Referrals
            .AsNoTracking()
            .Where(referral => referral.PatientId == canonicalId)
            .OrderByDescending(referral => referral.RequestedAt)
            .ThenByDescending(referral => referral.CreatedAt)
            .ToListAsync(cancellationToken);
        var displays = await ReadAssigneeDisplaysAsync(referrals, cancellationToken);
        return referrals.Select(referral => ToItem(referral, displays)).ToList();
    }

    public async Task<ReferralItem?> GetByIdAsync(
        string patientId,
        Guid referralId,
        CancellationToken cancellationToken)
    {
        var canonicalId = await ResolvePatientIdAsync(patientId, cancellationToken);
        var referral = await dbContext.Referrals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == referralId && candidate.PatientId == canonicalId,
                cancellationToken);
        return referral is null ? null : await ToItemAsync(referral, cancellationToken);
    }

    // This is intentionally a SQL read model: filtered aggregate counts and the prioritized,
    // joined queue projection are clearer and more efficient as one database-specific query.
    public async Task<ReferralWorkQueueResponse> GetWorkQueueAsync(
        string? status,
        string? assignedTo,
        bool overdueOnly,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = status?.Trim().ToLowerInvariant() is "all" or null or ""
            ? null
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not null &&
            normalizedStatus is not ("draft" or "sent" or "received" or "closed" or "cancelled"))
        {
            throw new ArgumentException(
                "Referral queue status must be draft, sent, received, closed, cancelled, or all.");
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
        count.Parameters.AddWithValue("status", (object?)normalizedStatus ?? DBNull.Value);
        count.Parameters.AddWithValue("assignedTo", (object?)assigneeFilter ?? DBNull.Value);
        count.Parameters.AddWithValue("overdueOnly", overdueOnly);
        count.Parameters.AddWithValue("query", (object?)queryFilter ?? DBNull.Value);
        int total;
        int activeCount;
        int overdueCount;
        await using (var reader = await count.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            total = Convert.ToInt32(reader.GetValue(0));
            activeCount = Convert.ToInt32(reader.GetValue(1));
            overdueCount = Convert.ToInt32(reader.GetValue(2));
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select r.id, r.patient_id, r.encounter_id, r.destination, r.reason, r.status,
              r.external_reference, r.notes, r.requested_at, r.workflow_version,
              coalesce(r.assigned_to, r.created_by, 'admin') as assigned_to,
              coalesce(a.display_name, r.assigned_to, r.created_by, 'Unassigned') as assigned_display_name,
              r.due_at, coalesce(r.created_by, 'legacy') as created_by, r.created_at, r.updated_at,
              trim(concat(p.last_name, ', ', p.first_name)) as patient_display_name, p.pubpid,
              (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()) as is_overdue
            from referrals r
            join patients p on p.canonical_id = r.patient_id
            left join auth_accounts a on lower(a.username) = lower(r.assigned_to)
            where (@status::text is null or r.status = @status::text)
              and (@assignedTo::text is null or lower(coalesce(r.assigned_to, r.created_by, 'admin')) = @assignedTo::text)
              and (@overdueOnly = false or (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()))
              and (@query::text is null or lower(r.destination) like '%' || @query::text || '%' or lower(r.reason) like '%' || @query::text || '%' or lower(p.canonical_id) like '%' || @query::text || '%' or lower(p.pubpid) like '%' || @query::text || '%' or lower(concat(p.last_name, ', ', p.first_name)) like '%' || @query::text || '%')
            order by (r.status in ('draft', 'sent', 'received') and r.due_at is not null and r.due_at < now()) desc,
              r.due_at nulls last, r.requested_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("status", (object?)normalizedStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("assignedTo", (object?)assigneeFilter ?? DBNull.Value);
        command.Parameters.AddWithValue("overdueOnly", overdueOnly);
        command.Parameters.AddWithValue("query", (object?)queryFilter ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", safeLimit);
        var items = new List<ReferralWorkQueueItem>();
        await using var itemReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await itemReader.ReadAsync(cancellationToken))
        {
            var statusValue = itemReader.GetString(5);
            var referral = new ReferralItem(
                itemReader.GetGuid(0),
                itemReader.GetString(1),
                itemReader.IsDBNull(2) ? null : itemReader.GetInt32(2),
                itemReader.GetString(3),
                itemReader.GetString(4),
                statusValue,
                ReadNullableString(itemReader, 6),
                ReadNullableString(itemReader, 7),
                itemReader.GetFieldValue<DateTimeOffset>(8).ToString("O"),
                itemReader.GetInt32(9),
                itemReader.GetString(10),
                itemReader.GetString(11),
                itemReader.IsDBNull(12) ? null : itemReader.GetFieldValue<DateTimeOffset>(12).ToString("O"),
                itemReader.GetString(13),
                ClinicalWorkflowPolicyCatalog.Revision,
                itemReader.GetFieldValue<DateTimeOffset>(14).ToString("O"),
                itemReader.GetFieldValue<DateTimeOffset>(15).ToString("O"),
                ClinicalWorkflowPolicyCatalog.GetAvailableReferralTransitions(statusValue));
            items.Add(new ReferralWorkQueueItem(
                referral,
                itemReader.GetString(16),
                itemReader.GetString(17),
                itemReader.GetBoolean(18)));
        }

        return new ReferralWorkQueueResponse(total, activeCount, overdueCount, items);
    }

    public async Task<ReferralItem> CreateAsync(
        string patientId,
        ReferralCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var destination = RequireText(request.Destination, "Referral destination", 240);
        var reason = RequireText(request.Reason, "Referral reason", 1000);
        var workflowReason = ClinicalWorkflowPolicyCatalog.RequireReason(
            request.WorkflowReason ?? "Referral draft created.");
        if (!TryParse(request.RequestedAt, out var requestedAt) ||
            !TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException(
                "Requested and responsibility due dates must be valid ISO dates or date-times.");
        }

        if (dueAt is not null && dueAt.Value.UtcDateTime.Date < requestedAt.UtcDateTime.Date)
        {
            throw new ArgumentException("Responsibility due date cannot be before the requested date.");
        }

        var assignedTo = TrimToNull(request.AssignedTo) ?? actor;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(patientId, cancellationToken);
        await ValidateAssigneeAsync(assignedTo, cancellationToken);
        await ValidateEncounterAsync(canonicalId, request.EncounterId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var referral = new ReferralEntity
        {
            Id = Guid.NewGuid(),
            PatientId = canonicalId,
            EncounterId = request.EncounterId,
            Destination = destination,
            Reason = reason,
            Status = "draft",
            ExternalReference = TrimToNull(request.ExternalReference),
            Notes = TrimToNull(request.Notes),
            RequestedAt = requestedAt,
            WorkflowVersion = 1,
            AssignedTo = assignedTo,
            DueAt = dueAt,
            CreatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Referrals.Add(referral);
        dbContext.ClinicalWorkflowEvents.Add(CreateEvent(
            referral,
            "created",
            null,
            "draft",
            null,
            assignedTo,
            "referral-draft-created",
            workflowReason,
            actor,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToItemAsync(referral, cancellationToken);
    }

    public async Task<ReferralItem> UpdateStatusAsync(
        string patientId,
        Guid referralId,
        ReferralStatusRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var nextState = request.Status?.Trim().ToLowerInvariant();
        if (nextState is not ("sent" or "received" or "closed" or "cancelled"))
        {
            throw new ArgumentException("Referral status must be sent, received, closed, or cancelled.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(patientId, cancellationToken);
        var referral = await dbContext.Referrals.SingleOrDefaultAsync(
            candidate => candidate.Id == referralId && candidate.PatientId == canonicalId,
            cancellationToken)
            ?? throw new ArgumentException("Referral was not found.");
        await EnsureEncounterIsUnlockedAsync(referral.EncounterId, cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, referral.WorkflowVersion);
        var transition = ClinicalWorkflowPolicyCatalog.RequireReferralTransition(
            referral.Status,
            nextState,
            request.ReasonCode,
            request.Reason);
        var priorStatus = referral.Status;
        var priorVersion = referral.WorkflowVersion;
        var now = DateTimeOffset.UtcNow;
        referral.Status = nextState;
        referral.WorkflowVersion++;
        referral.UpdatedAt = now;
        dbContext.ClinicalWorkflowEvents.Add(CreateEvent(
            referral,
            transition.Action,
            priorStatus,
            nextState,
            referral.AssignedTo,
            referral.AssignedTo,
            transition.ReasonCode,
            request.Reason.Trim(),
            actor,
            now));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ClinicalWorkflowVersionConflictException(request.ExpectedVersion, priorVersion);
        }

        return await ToItemAsync(referral, cancellationToken);
    }

    public async Task<ReferralItem> UpdateAssignmentAsync(
        string patientId,
        Guid referralId,
        ReferralAssignmentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var assignedTo = RequireText(request.AssignedTo, "Responsible staff user", 80);
        if (!TryParseNullable(request.DueAt, out var dueAt))
        {
            throw new ArgumentException(
                "Responsibility due date must be a valid ISO date or date-time.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientIdAsync(patientId, cancellationToken);
        var referral = await dbContext.Referrals.SingleOrDefaultAsync(
            candidate => candidate.Id == referralId && candidate.PatientId == canonicalId,
            cancellationToken)
            ?? throw new ArgumentException("Referral was not found.");
        await EnsureEncounterIsUnlockedAsync(referral.EncounterId, cancellationToken);
        RequireExpectedVersion(request.ExpectedVersion, referral.WorkflowVersion);
        ClinicalWorkflowPolicyCatalog.RequireAssignmentChange(
            referral.Status,
            request.ReasonCode,
            request.Reason);
        await ValidateAssigneeAsync(assignedTo, cancellationToken);
        if (dueAt is not null && dueAt.Value.UtcDateTime.Date < DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Responsibility due date cannot be in the past.");
        }

        if (string.Equals(referral.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase) &&
            Nullable.Equals(referral.DueAt, dueAt))
        {
            throw new ArgumentException(
                "Responsibility assignment must change the assignee or due date.");
        }

        var priorAssignedTo = referral.AssignedTo;
        var priorVersion = referral.WorkflowVersion;
        var now = DateTimeOffset.UtcNow;
        referral.AssignedTo = assignedTo;
        referral.DueAt = dueAt;
        referral.WorkflowVersion++;
        referral.UpdatedAt = now;
        dbContext.ClinicalWorkflowEvents.Add(CreateEvent(
            referral,
            "reassigned",
            referral.Status,
            referral.Status,
            priorAssignedTo,
            assignedTo,
            ClinicalWorkflowPolicyCatalog.ResponsibilityTransferReasonCode,
            request.Reason.Trim(),
            actor,
            now));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ClinicalWorkflowVersionConflictException(request.ExpectedVersion, priorVersion);
        }

        return await ToItemAsync(referral, cancellationToken);
    }

    public async Task<ReferralWorkflowHistoryResponse?> GetHistoryAsync(
        string patientId,
        Guid referralId,
        CancellationToken cancellationToken)
    {
        var referral = await GetByIdAsync(patientId, referralId, cancellationToken);
        if (referral is null)
        {
            return null;
        }

        var entityId = referralId.ToString();
        var eventEntities = await dbContext.ClinicalWorkflowEvents
            .AsNoTracking()
            .Where(workflowEvent =>
                workflowEvent.WorkflowType == ClinicalWorkflowPolicyCatalog.PatientReferralWorkflow &&
                workflowEvent.EntityId == entityId)
            .OrderByDescending(workflowEvent => workflowEvent.WorkflowVersion)
            .ThenByDescending(workflowEvent => workflowEvent.OccurredAt)
            .ToListAsync(cancellationToken);
        var events = eventEntities.Select(workflowEvent => new ReferralWorkflowEvent(
            workflowEvent.EventId,
            workflowEvent.WorkflowVersion,
            workflowEvent.Action,
            workflowEvent.FromState,
            workflowEvent.ToState,
            workflowEvent.FromAssignedTo,
            workflowEvent.ToAssignedTo,
            workflowEvent.ReasonCode,
            workflowEvent.Reason,
            workflowEvent.Actor,
            workflowEvent.PolicyRevision,
            workflowEvent.OccurredAt.ToString("O"))).ToList();
        return new ReferralWorkflowHistoryResponse(referral, events.Count, events);
    }

    private async Task<string> ResolvePatientIdAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var normalized = TrimToNull(patientId)?.ToLowerInvariant()
            ?? throw new ArgumentException("Patient was not found.");
        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient =>
                patient.CanonicalId.ToLower() == normalized ||
                patient.PublicId.ToLower() == normalized)
            .Select(patient => patient.CanonicalId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException("Patient was not found.");
    }

    private async Task ValidateEncounterAsync(
        string patientId,
        int? encounterId,
        CancellationToken cancellationToken)
    {
        if (encounterId is null)
        {
            return;
        }

        var normalizedPatientId = patientId.ToLowerInvariant();
        var belongsToPatient = await dbContext.Encounters
            .AsNoTracking()
            .AnyAsync(
                encounter =>
                    encounter.EncounterNumber == encounterId.Value &&
                    encounter.PatientId.ToLower() == normalizedPatientId,
                cancellationToken);
        if (!belongsToPatient)
        {
            throw new ArgumentException("Referral encounter does not belong to this patient.");
        }

        await EnsureEncounterIsUnlockedAsync(encounterId, cancellationToken);
    }

    private async Task EnsureEncounterIsUnlockedAsync(
        int? encounterId,
        CancellationToken cancellationToken)
    {
        if (encounterId is null)
        {
            return;
        }

        if (await dbContext.EncounterSignatures.AsNoTracking().AnyAsync(
                signature => signature.EncounterNumber == encounterId.Value && signature.IsLock,
                cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }
    }

    private async Task ValidateAssigneeAsync(
        string assignedTo,
        CancellationToken cancellationToken)
    {
        var normalized = assignedTo.ToLowerInvariant();
        if (!await dbContext.AuthAccounts.AsNoTracking().AnyAsync(
                account => account.Active && account.Username.ToLower() == normalized,
                cancellationToken))
        {
            throw new ArgumentException("Responsible staff user must be an active account.");
        }
    }

    private async Task<ReferralItem> ToItemAsync(
        ReferralEntity referral,
        CancellationToken cancellationToken)
    {
        var displays = await ReadAssigneeDisplaysAsync([referral], cancellationToken);
        return ToItem(referral, displays);
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadAssigneeDisplaysAsync(
        IEnumerable<ReferralEntity> referrals,
        CancellationToken cancellationToken)
    {
        var usernames = referrals
            .Select(EffectiveAssignee)
            .Select(username => username.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (usernames.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var accounts = await dbContext.AuthAccounts
            .AsNoTracking()
            .Where(account => usernames.Contains(account.Username.ToLower()))
            .Select(account => new { account.Username, account.DisplayName })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(
            account => account.Username,
            account => account.DisplayName,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ReferralItem ToItem(
        ReferralEntity referral,
        IReadOnlyDictionary<string, string> assigneeDisplays)
    {
        var status = referral.Status;
        var assignedTo = EffectiveAssignee(referral);
        var assignedDisplayName = assigneeDisplays.TryGetValue(assignedTo, out var displayName)
            ? displayName
            : assignedTo;
        return new ReferralItem(
            referral.Id,
            referral.PatientId,
            referral.EncounterId,
            referral.Destination,
            referral.Reason,
            status,
            referral.ExternalReference,
            referral.Notes,
            referral.RequestedAt.ToString("O"),
            referral.WorkflowVersion,
            assignedTo,
            assignedDisplayName,
            referral.DueAt?.ToString("O"),
            TrimToNull(referral.CreatedBy) ?? "legacy",
            ClinicalWorkflowPolicyCatalog.Revision,
            referral.CreatedAt.ToString("O"),
            referral.UpdatedAt.ToString("O"),
            ClinicalWorkflowPolicyCatalog.GetAvailableReferralTransitions(status));
    }

    private static ClinicalWorkflowEventEntity CreateEvent(
        ReferralEntity referral,
        string action,
        string? fromState,
        string toState,
        string? fromAssignedTo,
        string? toAssignedTo,
        string reasonCode,
        string reason,
        string actor,
        DateTimeOffset occurredAt) =>
        new()
        {
            EventId = Guid.NewGuid(),
            WorkflowType = ClinicalWorkflowPolicyCatalog.PatientReferralWorkflow,
            EntityId = referral.Id.ToString(),
            PatientId = referral.PatientId,
            WorkflowVersion = referral.WorkflowVersion,
            Action = action,
            FromState = fromState,
            ToState = toState,
            FromAssignedTo = fromAssignedTo,
            ToAssignedTo = toAssignedTo,
            ReasonCode = reasonCode,
            Reason = reason,
            Actor = actor,
            PolicyRevision = ClinicalWorkflowPolicyCatalog.Revision,
            OccurredAt = occurredAt
        };

    private static string EffectiveAssignee(ReferralEntity referral) =>
        TrimToNull(referral.AssignedTo) ?? TrimToNull(referral.CreatedBy) ?? "admin";

    private static void RequireExpectedVersion(int expectedVersion, int currentVersion)
    {
        if (expectedVersion != currentVersion)
        {
            throw new ClinicalWorkflowVersionConflictException(expectedVersion, currentVersion);
        }
    }

    private static string RequireText(string? value, string label, int maximumLength)
    {
        var normalized = TrimToNull(value);
        return normalized is null || normalized.Length > maximumLength
            ? throw new ArgumentException(
                $"{label} is required and must be {maximumLength} characters or fewer.")
            : normalized;
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

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
