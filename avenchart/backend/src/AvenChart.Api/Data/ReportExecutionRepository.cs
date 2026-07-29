using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Configuration;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReportExecutionRepository(
    NpgsqlDataSource dataSource,
    ReportRepository reportRepository,
    IOptions<ReportExecutionOptions> options)
{
    private const string ExecutionRevision = "local-report-execution-v3";
    private const string DefinitionRevision = "local-report-definition-v1";
    private const string ScopeRevision = "local-report-scope-v1";
    private const string QueueRevision = "local-report-queue-v1";
    private const string OperationsRevision = "local-report-operations-v1";
    private const int OperationsPollIntervalSeconds = 5;
    private const int MaximumDateSpanDays = 366;
    private const int MaximumRows = 5000;
    private const int PreviewRows = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex IdempotencyKeyPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._:-]{7,99}$",
        RegexOptions.CultureInvariant);
    private static readonly string[] RunStates =
        ["queued", "running", "completed", "failed", "cancelled", "expired"];
    private static readonly string[] ExecutableRowPolicies =
        ["practice-wide", "facility-scoped", "patient-assigned"];
    private static readonly string[] DeliveryModes = ["local-download"];
    private static readonly string[] ReportFamilies =
        ["operational", "patients", "appointments", "encounters", "referrals", "chart-tracker", "inventory"];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>>
        RowPolicyFamilySupport =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["practice-wide"] =
                    ["operational", "patients", "appointments", "encounters", "referrals", "chart-tracker", "inventory"],
                ["facility-scoped"] =
                    ["operational", "patients", "appointments", "encounters", "referrals", "chart-tracker", "inventory"],
                ["patient-assigned"] =
                    ["operational", "patients", "appointments", "encounters", "referrals", "chart-tracker"]
            };

    public async Task<GovernedReportExecutionPolicy> GetPolicyAsync(
        string actor,
        bool operatorAccess,
        CancellationToken cancellationToken)
    {
        var watermark = await GetWatermarkAsync(cancellationToken);
        var actorScope = await GetActorScopeAsync(actor, cancellationToken);
        return new(
            ExecutionRevision,
            DefinitionRevision,
            ScopeRevision,
            QueueRevision,
            watermark.DatasetId,
            watermark.DatasetVersion,
            watermark.BaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RunStates,
            ExecutableRowPolicies,
            RowPolicyFamilySupport,
            [
                "auth_accounts.staff_id",
                "staff.active and staff.facility_id",
                "facilities.inactive",
                "patients.provider_id",
                "active patient_care_teams and patient_care_team_members"
            ],
            actorScope,
            operatorAccess,
            DeliveryModes,
            MaximumDateSpanDays,
            MaximumRows,
            PreviewRows,
            DurableQueueEnabled: true,
            options.Value.EnqueueDelayMilliseconds,
            options.Value.PollIntervalMilliseconds,
            options.Value.LeaseSeconds,
            options.Value.ExecutionTimeoutSeconds,
            options.Value.QueueExpirationMinutes,
            options.Value.MaxAttempts,
            options.Value.RetryBaseDelaySeconds,
            DefinitionRetentionEnforcedLocally: true,
            RetryableFailureCodes:
            [
                "execution-timeout",
                "execution-transient",
                "worker-stopped",
                "worker-lease-expired"
            ],
            ExternalDeliveryEnabled: false,
            ArtifactStorageProductionApproved: false,
            ProductionBlockers:
            [
                "The executable staff/facility/provider/care-team mapping is a local development contract and still requires accountable production policy approval.",
                "Patient-assigned inventory execution remains denied because inventory transactions have no approved patient relationship.",
                "Only the exact synthetic dataset as-of date is executable; historical snapshots and source-version time travel are not available.",
                "The local database queue and in-process worker are not approved independent production worker infrastructure or an operational service-level contract.",
                "The local database artifact is not approved encrypted production object storage; definition retention is enforced locally without legal hold, backup, recovery, or accountable disposition approval.",
                "Only requesting-user or report-owner recipients and local download are supported; schedules and external delivery remain disabled.",
                "Metric owners have not approved the curated family semantics, validation fixtures, or cross-revision equivalence.",
                "Production identity, purpose-of-use claims, disclosure authority, monitoring, performance, and accountable acceptance remain open."
            ]);
    }

    private async Task<GovernedReportActorScope> GetActorScopeAsync(
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select staff.id,
                   staff.facility_id,
                   facility.code,
                   (
                     select count(*)
                     from patients patient
                     where patient.merged_into_patient_id is null
                       and (
                         patient.provider_id=staff.id
                         or exists (
                           select 1
                           from patient_care_teams team
                           join patient_care_team_members member
                             on member.patient_id=team.patient_id
                           where team.patient_id=patient.canonical_id
                             and team.team_status='active'
                             and member.user_id=staff.id
                             and member.status='active'
                         )
                       )
                   )::integer
            from auth_accounts account
            join staff
              on staff.id=account.staff_id
             and staff.active=true
            left join facilities facility
              on facility.id=staff.facility_id
             and facility.inactive=false
            where lower(account.username)=lower(@actor)
              and account.active=true;
            """;
        command.Parameters.AddWithValue("actor", normalizedActor);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new(normalizedActor, false, null, null, null, 0);
        }
        int? facilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
        var facilityCode = reader.IsDBNull(2) ? null : reader.GetString(2);
        return new(
            normalizedActor,
            true,
            reader.GetInt32(0),
            facilityCode is null ? null : facilityId,
            facilityCode,
            reader.GetInt32(3));
    }

    public async Task<GovernedReportPreviewResponse?> PreviewAsync(
        Guid definitionId,
        GovernedReportPreviewRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownProperties(request.AdditionalProperties);
        var context = await LoadContextAsync(
            definitionId,
            request.Purpose,
            request.RecipientUsername,
            request.DeliveryMode,
            request.AsOfDate,
            request.Parameters,
            actor,
            cancellationToken);
        if (context is null)
        {
            return null;
        }

        EnsureScopeExecutable(context.Scope);
        var csv = await reportRepository.GetGovernedFamilyCsvAsync(
            context.Definition.ReportFamily,
            context.From,
            context.To,
            context.Scope.DataScope,
            cancellationToken);
        var parsed = ParseCsv(csv);
        var checksum = Sha256(csv);
        var rows = parsed.Count <= 1
            ? []
            : parsed.Skip(1).Take(PreviewRows).Select(row => (IReadOnlyList<string>)row).ToList();

        return new(
            definitionId,
            context.Definition.RevisionId,
            context.Definition.RevisionNumber,
            context.Definition.ReportFamily,
            context.Definition.RowPolicy,
            context.Purpose,
            context.RecipientUsername,
            context.Watermark.BaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            context.Parameters,
            context.Watermark.DatasetId,
            context.Watermark.DatasetVersion,
            ExecutionRevision,
            ScopeRevision,
            context.Scope.SnapshotChecksum,
            context.Scope.FacilityId,
            context.Scope.SubjectCount,
            Math.Max(0, parsed.Count - 1),
            PreviewRows,
            parsed.Count == 0 ? [] : parsed[0],
            rows,
            checksum);
    }

    public async Task<GovernedReportRunDetail?> RunAsync(
        Guid definitionId,
        GovernedReportRunRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownProperties(request.AdditionalProperties);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var context = await LoadContextAsync(
            definitionId,
            request.Purpose,
            request.RecipientUsername,
            request.DeliveryMode,
            request.AsOfDate,
            request.Parameters,
            actor,
            cancellationToken);
        if (context is null)
        {
            return null;
        }

        var fingerprint = BuildFingerprint(context, idempotencyKey);
        var existing = await FindIdempotentRunAsync(
            actor,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(
                    existing.Value.Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ReportExecutionConflictException(
                    "The idempotency key was already used for a different report request.",
                    existing.Value.Detail.Run);
            }

            return existing.Value.Detail with
            {
                Run = existing.Value.Detail.Run with { Replay = true }
            };
        }

        var runId = $"RPT-{Guid.NewGuid():N}";
        try
        {
            await CreateQueuedRunAsync(
                runId,
                context,
                actor,
                idempotencyKey,
                fingerprint,
                cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            var concurrent = await FindIdempotentRunAsync(
                actor,
                idempotencyKey,
                cancellationToken);
            if (concurrent is null ||
                !string.Equals(
                    concurrent.Value.Fingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ReportExecutionConflictException(
                    "The idempotency key was concurrently used for a different report request.",
                    concurrent?.Detail.Run);
            }

            return concurrent.Value.Detail with
            {
                Run = concurrent.Value.Detail.Run with { Replay = true }
            };
        }

        if (!context.Scope.Executable)
        {
            await FailRunAsync(
                runId,
                actor,
                context.Scope.FailureCode!,
                context.Scope.FailureMessage!,
                cancellationToken);
            return await GetRunAsync(runId, actor, cancellationToken);
        }

        return await GetRunAsync(runId, actor, cancellationToken);
    }

    public async Task<GovernedReportRunListResponse> ListRunsAsync(
        Guid definitionId,
        string actor,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {RunSelect}
            where run.definition_id = @definition
              and (
                lower(run.ran_by) = lower(@actor)
                or lower(coalesce(run.recipient_username, '')) = lower(@actor)
              )
            order by run.ran_at desc, run.run_id desc
            offset @offset limit @limit;
            """;
        command.Parameters.AddWithValue("definition", definitionId);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        var runs = new List<GovernedReportRunItem>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(ReadRun(reader, replay: false, actor));
            total = reader.GetInt32(reader.GetOrdinal("total_count"));
        }

        return new(runs, page, pageSize, total);
    }

    public async Task<GovernedReportOperationsResponse> GetOperationsAsync(
        string actor,
        string? search,
        string? status,
        string? family,
        string? requestedBy,
        bool attentionOnly,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        var normalizedSearch = NormalizeOperationsFilter(search, "Search", 100);
        var normalizedStatus = NormalizeOperationsFilter(status, "Status", 20)
            .ToLowerInvariant();
        var normalizedFamily = NormalizeOperationsFilter(family, "Family", 30)
            .ToLowerInvariant();
        var normalizedRequester =
            NormalizeOperationsFilter(requestedBy, "Requester", 100);
        if (normalizedStatus.Length > 0 &&
            !RunStates.Contains(normalizedStatus, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Status must be one of: {string.Join(", ", RunStates)}.");
        }
        if (normalizedFamily.Length > 0 &&
            !ReportFamilies.Contains(normalizedFamily, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Family must be one of: {string.Join(", ", ReportFamilies)}.");
        }
        if (from.HasValue && to.HasValue)
        {
            if (from.Value > to.Value)
            {
                throw new ArgumentException(
                    "Operations from date must be on or before the to date.");
            }
            if (to.Value.DayNumber - from.Value.DayNumber > MaximumDateSpanDays)
            {
                throw new ArgumentException(
                    $"Operations date range cannot exceed {MaximumDateSpanDays} days.");
            }
        }

        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var generatedAt = DateTimeOffset.UtcNow;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var summary = await ReadOperationsSummaryAsync(
            connection,
            generatedAt,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {RunSelect}
            where (
                @search = ''
                or lower(run.run_id) like @search_like
                or lower(definition.stable_key) like @search_like
                or lower(coalesce(revision.title, definition.name)) like @search_like
                or lower(coalesce(run.failure_code, '')) like @search_like
              )
              and (@status = '' or run.status = @status)
              and (
                @family = ''
                or coalesce(revision.report_family, definition.report_type) = @family
              )
              and (
                @requester = ''
                or lower(run.ran_by) = lower(@requester)
              )
              and (@from_date is null or run.ran_at >= @from_date)
              and (
                @to_date is null
                or run.ran_at < (@to_date::date + interval '1 day')
              )
              and (
                not @attention_only
                or run.status = 'failed'
                or (
                  run.status = 'expired'
                  and run.failure_code = 'queue-expired'
                )
                or (
                  run.status = 'running'
                  and (
                    run.cancel_requested_at is not null
                    or run.lease_expires_at is null
                    or run.lease_expires_at <= @generated_at
                  )
                )
                or (
                  run.status = 'queued'
                  and run.queue_expires_at <= @generated_at
                )
              )
            order by run.ran_at desc, run.run_id desc
            offset @offset limit @limit;
            """;
        command.Parameters.AddWithValue("search", normalizedSearch);
        command.Parameters.AddWithValue(
            "search_like",
            $"%{normalizedSearch.ToLowerInvariant()}%");
        command.Parameters.AddWithValue("status", normalizedStatus);
        command.Parameters.AddWithValue("family", normalizedFamily);
        command.Parameters.AddWithValue("requester", normalizedRequester);
        command.Parameters.Add("from_date", NpgsqlDbType.Date).Value =
            from.HasValue ? from.Value : DBNull.Value;
        command.Parameters.Add("to_date", NpgsqlDbType.Date).Value =
            to.HasValue ? to.Value : DBNull.Value;
        command.Parameters.AddWithValue("attention_only", attentionOnly);
        command.Parameters.AddWithValue("generated_at", generatedAt);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        var runs = new List<GovernedReportRunItem>();
        var total = 0;
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(ReadRun(reader, replay: false, normalizedActor));
            total = reader.GetInt32(reader.GetOrdinal("total_count"));
        }

        var alerts = BuildOperationsAlerts(summary);
        var health = summary.OverdueLeases > 0
            ? "critical"
            : alerts.Count > 0
                ? "attention"
                : "healthy";
        return new(
            OperationsRevision,
            generatedAt.ToString("O"),
            health,
            OperationsPollIntervalSeconds,
            ProductionApproved: false,
            RunStates,
            ReportFamilies,
            [
                "failed runs",
                "queue-expired requests",
                "running requests with overdue or missing leases",
                "running requests with pending cancellation",
                "queued requests past their queue deadline"
            ],
            summary,
            alerts,
            runs,
            page,
            pageSize,
            total,
            [
                "This is a local read-only operations projection, not an approved production monitoring or alert service.",
                "Alert thresholds, notification channels, on-call ownership, escalation, and incident severity policy are not approved.",
                "Operators cannot cancel, retry, or download another requester's artifact through this surface.",
                "Production telemetry retention, protected-data minimization, export, legal hold, and audit integration remain open."
            ]);
    }

    public async Task<GovernedReportRunDetail?> GetRunAsync(
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedRunId = NormalizeRunId(runId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var run = await ReadAuthorizedRunAsync(
            connection,
            normalizedRunId,
            actor,
            cancellationToken);
        if (run is null)
        {
            return null;
        }

        return new(
            run,
            await ReadRunEventsAsync(
                connection,
                normalizedRunId,
                cancellationToken));
    }

    public async Task<GovernedReportRunDetail?> GetOperatorRunAsync(
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedRunId = NormalizeRunId(runId);
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var run = await ReadRunByIdAsync(
            connection,
            normalizedRunId,
            normalizedActor,
            cancellationToken);
        if (run is null)
        {
            return null;
        }

        return new(
            run,
            await ReadRunEventsAsync(
                connection,
                normalizedRunId,
                cancellationToken));
    }

    public async Task<GovernedReportRunDetail?> CancelAsync(
        string runId,
        GovernedReportLifecycleRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownProperties(request.AdditionalProperties);
        var normalizedRunId = NormalizeRunId(runId);
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        var reason = NormalizeLifecycleReason(request.Reason);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        string status;
        int lifecycleVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select status, lifecycle_version
                from saved_report_runs
                where run_id=@run
                  and lower(ran_by)=lower(@actor)
                for update;
                """;
            command.Parameters.AddWithValue("run", normalizedRunId);
            command.Parameters.AddWithValue("actor", normalizedActor);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            status = reader.GetString(0);
            lifecycleVersion = reader.GetInt32(1);
        }

        if (request.ExpectedLifecycleVersion != lifecycleVersion)
        {
            throw new ReportExecutionConflictException(
                $"The report lifecycle changed after it was loaded. Current version is {lifecycleVersion}.");
        }
        if (status is not ("queued" or "running"))
        {
            throw new ReportExecutionConflictException(
                $"A {status} report cannot be cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = status == "queued"
                ? """
                  update saved_report_runs
                  set status='cancelled',
                      lifecycle_version=lifecycle_version+1,
                      cancel_requested_at=@now,
                      cancel_requested_by=@actor,
                      cancel_reason=@reason,
                      finished_at=@now,
                      duration_ms=0,
                      next_attempt_at=null,
                      failure_code='cancelled-by-request',
                      failure_message=@reason,
                      failure_retryable=false,
                      result_summary=jsonb_build_object(
                        'failureCode',
                        'cancelled-by-request',
                        'message',
                        @reason::text)
                  where run_id=@run and status='queued';
                  """
                : """
                  update saved_report_runs
                  set lifecycle_version=lifecycle_version+1,
                      cancel_requested_at=@now,
                      cancel_requested_by=@actor,
                      cancel_reason=@reason
                  where run_id=@run
                    and status='running'
                    and cancel_requested_at is null;
                  """;
            command.Parameters.AddWithValue("run", normalizedRunId);
            command.Parameters.AddWithValue("actor", normalizedActor);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("now", now);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ReportExecutionConflictException(
                    "The report lifecycle changed while cancellation was requested.");
            }
        }

        await InsertEventAsync(
            connection,
            transaction,
            normalizedRunId,
            status == "queued" ? "cancelled" : "cancellation-requested",
            status,
            status == "queued" ? "cancelled" : "running",
            normalizedActor,
            reason,
            new Dictionary<string, object?>
            {
                ["requestedAt"] = now.ToString("O"),
                ["lifecycleVersion"] = lifecycleVersion + 1
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRunAsync(
            normalizedRunId,
            normalizedActor,
            cancellationToken);
    }

    public async Task<GovernedReportRunDetail?> RetryAsync(
        string runId,
        GovernedReportLifecycleRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownProperties(request.AdditionalProperties);
        var normalizedRunId = NormalizeRunId(runId);
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        var reason = NormalizeLifecycleReason(request.Reason);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        string status;
        int lifecycleVersion;
        int attemptCount;
        bool failureRetryable;
        string? failureCode;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select status,
                       lifecycle_version,
                       attempt_count,
                       coalesce(failure_retryable, false),
                       failure_code
                from saved_report_runs
                where run_id=@run
                  and lower(ran_by)=lower(@actor)
                for update;
                """;
            command.Parameters.AddWithValue("run", normalizedRunId);
            command.Parameters.AddWithValue("actor", normalizedActor);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            status = reader.GetString(0);
            lifecycleVersion = reader.GetInt32(1);
            attemptCount = reader.GetInt32(2);
            failureRetryable = reader.GetBoolean(3);
            failureCode = reader.IsDBNull(4) ? null : reader.GetString(4);
        }

        if (request.ExpectedLifecycleVersion != lifecycleVersion)
        {
            throw new ReportExecutionConflictException(
                $"The report lifecycle changed after it was loaded. Current version is {lifecycleVersion}.");
        }
        if (status != "failed")
        {
            throw new ReportExecutionConflictException(
                $"A {status} report cannot be retried.");
        }
        if (!failureRetryable)
        {
            throw new ReportExecutionConflictException(
                "This failure was classified as non-retryable.");
        }
        if (attemptCount >= 10)
        {
            throw new ReportExecutionConflictException(
                "The report reached the absolute ten-attempt safety limit.");
        }

        var nextAttemptAt = DateTimeOffset.UtcNow.AddMilliseconds(
            options.Value.EnqueueDelayMilliseconds);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update saved_report_runs
                set status='queued',
                    lifecycle_version=lifecycle_version+1,
                    max_attempts=greatest(max_attempts, attempt_count+1),
                    manual_retry_count=manual_retry_count+1,
                    next_attempt_at=@nextAttempt,
                    finished_at=null,
                    duration_ms=null,
                    failure_code=null,
                    failure_message=null,
                    failure_retryable=null,
                    result_summary='{}'::jsonb,
                    cancel_requested_at=null,
                    cancel_requested_by=null,
                    cancel_reason=null
                where run_id=@run and status='failed';
                """;
            command.Parameters.AddWithValue("run", normalizedRunId);
            command.Parameters.AddWithValue("nextAttempt", nextAttemptAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            normalizedRunId,
            "manual-retry-requested",
            "failed",
            "queued",
            normalizedActor,
            reason,
            new Dictionary<string, object?>
            {
                ["priorFailureCode"] = failureCode,
                ["priorAttemptCount"] = attemptCount,
                ["nextAttemptAt"] = nextAttemptAt.ToString("O"),
                ["lifecycleVersion"] = lifecycleVersion + 1
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRunAsync(
            normalizedRunId,
            normalizedActor,
            cancellationToken);
    }

    public async Task<GovernedReportArtifact?> DownloadAsync(
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedRunId = NormalizeRunId(runId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select artifact_file_name, artifact_content_type, artifact_content,
                   result_checksum, status
            from saved_report_runs
            where run_id = @run
              and (
                lower(ran_by) = lower(@actor)
                or lower(coalesce(recipient_username, '')) = lower(@actor)
              )
            for update;
            """;
        command.Parameters.AddWithValue("run", normalizedRunId);
        command.Parameters.AddWithValue("actor", actor);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var status = reader.GetString(4);
        if (status != "completed" ||
            reader.IsDBNull(0) ||
            reader.IsDBNull(1) ||
            reader.IsDBNull(2) ||
            reader.IsDBNull(3))
        {
            return null;
        }

        var artifact = new GovernedReportArtifact(
            reader.GetString(0),
            reader.GetString(1),
            Encoding.UTF8.GetBytes(reader.GetString(2)),
            reader.GetString(3));
        await reader.DisposeAsync();

        await InsertEventAsync(
            connection,
            transaction,
            normalizedRunId,
            "downloaded",
            "completed",
            "completed",
            actor,
            "Authorized local artifact download.",
            new Dictionary<string, object?>
            {
                ["checksum"] = artifact.Checksum,
                ["bytes"] = artifact.Content.Length
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }

    private async Task<ExecutionContext?> LoadContextAsync(
        Guid definitionId,
        string purpose,
        string recipientUsername,
        string deliveryMode,
        string asOfDate,
        IReadOnlyDictionary<string, string?>? parameters,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedActor = NormalizeUsername(actor, "Authenticated actor");
        var normalizedPurpose = NormalizeRequired(purpose, "Purpose", 500);
        var normalizedRecipient = NormalizeUsername(recipientUsername, "Recipient");
        var normalizedDelivery = NormalizeRequired(deliveryMode, "Delivery mode", 40)
            .ToLowerInvariant();
        if (!DeliveryModes.Contains(normalizedDelivery, StringComparer.Ordinal))
        {
            throw new ArgumentException("Only local-download delivery is supported.");
        }

        var watermark = await GetWatermarkAsync(cancellationToken);
        if (!DateOnly.TryParseExact(
                asOfDate?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedAsOf))
        {
            throw new ArgumentException("As-of date must use YYYY-MM-DD.");
        }
        if (parsedAsOf != watermark.BaseDate)
        {
            throw new ArgumentException(
                $"As-of date must equal the available synthetic dataset snapshot {watermark.BaseDate:yyyy-MM-dd}.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var definition = await LoadActiveDefinitionAsync(
            connection,
            definitionId,
            cancellationToken);
        if (definition is null)
        {
            return null;
        }

        if (!string.Equals(
                normalizedPurpose,
                definition.Purpose,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Run purpose must exactly match the approved active definition purpose.");
        }
        if (!definition.DeliveryModes.Contains(
                normalizedDelivery,
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The active definition does not permit the requested delivery mode.");
        }

        var requestingUserRecipient =
            string.Equals(normalizedRecipient, normalizedActor, StringComparison.OrdinalIgnoreCase) &&
            definition.AllowedRecipients.Contains("requesting-user", StringComparer.Ordinal);
        var reportOwnerRecipient =
            string.Equals(
                normalizedRecipient,
                definition.OwnerUsername,
                StringComparison.OrdinalIgnoreCase) &&
            definition.AllowedRecipients.Contains("report-owner", StringComparer.Ordinal);
        if (!requestingUserRecipient && !reportOwnerRecipient)
        {
            throw new ArgumentException(
                "Recipient must be the requesting user or report owner as permitted by the active definition.");
        }
        if (!await IsActiveAccountAsync(connection, normalizedRecipient, cancellationToken))
        {
            throw new ArgumentException("Recipient must be an active local authentication account.");
        }

        var normalizedParameters = NormalizeParameters(
            definition.ParameterSchema,
            parameters,
            parsedAsOf,
            out var from,
            out var to);
        var scope = await ResolveScopeAsync(
            connection,
            definition.RowPolicy,
            definition.ReportFamily,
            normalizedActor,
            cancellationToken);

        return new(
            definition,
            watermark,
            normalizedActor,
            normalizedPurpose,
            normalizedRecipient,
            normalizedDelivery,
            normalizedParameters,
            from,
            to,
            scope);
    }

    private static IReadOnlyDictionary<string, string?> NormalizeParameters(
        IReadOnlyList<ReportParameterDefinition> schema,
        IReadOnlyDictionary<string, string?>? supplied,
        DateOnly asOfDate,
        out DateOnly? from,
        out DateOnly? to)
    {
        var input = supplied ?? new Dictionary<string, string?>();
        var allowed = schema.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var unknown = input.Keys
            .Where(key => !allowed.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Unknown report parameter(s): {string.Join(", ", unknown)}.");
        }

        var normalized = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        from = null;
        to = null;
        foreach (var parameter in schema.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            input.TryGetValue(parameter.Key, out var raw);
            var value = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (parameter.Required && value is null)
            {
                throw new ArgumentException($"{parameter.Label} is required.");
            }
            if (parameter.Type != "date")
            {
                throw new ArgumentException(
                    $"Parameter type '{parameter.Type}' is not executable.");
            }
            if (value is not null &&
                !DateOnly.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                throw new ArgumentException(
                    $"{parameter.Label} must use YYYY-MM-DD.");
            }

            normalized[parameter.Key] = value;
        }

        if (allowed.Contains("from") &&
            normalized.TryGetValue("from", out var fromText) &&
            fromText is not null)
        {
            from = DateOnly.ParseExact(
                fromText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }
        if (allowed.Contains("to"))
        {
            if (!normalized.TryGetValue("to", out var toText) || toText is null)
            {
                to = asOfDate;
                normalized["to"] = asOfDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }
            else
            {
                to = DateOnly.ParseExact(
                    toText,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture);
            }
        }

        if (from is not null && from > asOfDate ||
            to is not null && to > asOfDate)
        {
            throw new ArgumentException("Report dates cannot be after the run as-of date.");
        }
        if (from is not null && to is not null && from > to)
        {
            throw new ArgumentException("From date cannot be after to date.");
        }
        if (from is not null &&
            to is not null &&
            to.Value.DayNumber - from.Value.DayNumber > MaximumDateSpanDays)
        {
            throw new ArgumentException(
                $"Report date range cannot exceed {MaximumDateSpanDays} days.");
        }

        return normalized;
    }

    private async Task CreateQueuedRunAsync(
        string runId,
        ExecutionContext context,
        string actor,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nextAttemptAt = now.AddMilliseconds(
            options.Value.EnqueueDelayMilliseconds);
        var queueExpiresAt = now.AddMinutes(
            options.Value.QueueExpirationMinutes);
        var parametersJson = JsonSerializer.Serialize(context.Parameters, JsonOptions);
        var watermarkJson = JsonSerializer.Serialize(
            new
            {
                context.Watermark.DatasetId,
                context.Watermark.DatasetVersion,
                baseDate = context.Watermark.BaseDate.ToString("yyyy-MM-dd"),
                generatedAt = context.Watermark.GeneratedAt.ToString("O")
            },
            JsonOptions);
        var fileName =
            $"{SanitizeFileName(context.Definition.StableKey)}-r{context.Definition.RevisionNumber}-{runId}.csv";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into saved_report_runs (
              run_id, definition_id, revision_id, revision_number, ran_at, ran_by,
              output_format, row_count, status, purpose, recipient_username,
              row_policy, normalized_parameters, as_of_date, dataset_id,
              dataset_version, execution_revision, source_watermark,
              definition_snapshot_checksum, request_fingerprint, idempotency_key,
              result_summary, artifact_content_type, artifact_file_name,
              scope_revision, scope_snapshot, scope_snapshot_checksum,
              scope_facility_id, scope_subject_count, queue_revision,
              lifecycle_version, attempt_count, max_attempts,
              manual_retry_count, next_attempt_at, queue_expires_at)
            values (
              @run, @definition, @revision, @revisionNumber, @at, @actor,
              'csv', 0, 'queued', @purpose, @recipient, @rowPolicy,
              @parameters, @asOf, @dataset, @datasetVersion, @executionRevision,
              @watermark, @definitionChecksum, @fingerprint, @idempotency,
              '{}'::jsonb, 'text/csv; charset=utf-8', @fileName,
              @scopeRevision, @scopeSnapshot, @scopeChecksum,
              @scopeFacility, @scopeSubjects, @queueRevision,
              0, 0, @maxAttempts, 0, @nextAttempt, @queueExpires);
            """;
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("definition", context.Definition.DefinitionId);
        command.Parameters.AddWithValue("revision", context.Definition.RevisionId);
        command.Parameters.AddWithValue("revisionNumber", context.Definition.RevisionNumber);
        command.Parameters.AddWithValue("at", now);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("purpose", context.Purpose);
        command.Parameters.AddWithValue("recipient", context.RecipientUsername);
        command.Parameters.AddWithValue("rowPolicy", context.Definition.RowPolicy);
        command.Parameters.Add("parameters", NpgsqlDbType.Jsonb).Value = parametersJson;
        command.Parameters.AddWithValue("asOf", context.Watermark.BaseDate);
        command.Parameters.AddWithValue("dataset", context.Watermark.DatasetId);
        command.Parameters.AddWithValue("datasetVersion", context.Watermark.DatasetVersion);
        command.Parameters.AddWithValue("executionRevision", ExecutionRevision);
        command.Parameters.Add("watermark", NpgsqlDbType.Jsonb).Value = watermarkJson;
        command.Parameters.AddWithValue(
            "definitionChecksum",
            context.Definition.SnapshotChecksum);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("idempotency", idempotencyKey);
        command.Parameters.AddWithValue("fileName", fileName);
        command.Parameters.AddWithValue("scopeRevision", ScopeRevision);
        command.Parameters.Add("scopeSnapshot", NpgsqlDbType.Jsonb).Value =
            context.Scope.SnapshotJson;
        command.Parameters.AddWithValue("scopeChecksum", context.Scope.SnapshotChecksum);
        command.Parameters.Add("scopeFacility", NpgsqlDbType.Integer).Value =
            (object?)context.Scope.FacilityId ?? DBNull.Value;
        command.Parameters.Add("scopeSubjects", NpgsqlDbType.Integer).Value =
            (object?)context.Scope.SubjectCount ?? DBNull.Value;
        command.Parameters.AddWithValue("queueRevision", QueueRevision);
        command.Parameters.AddWithValue("maxAttempts", options.Value.MaxAttempts);
        command.Parameters.AddWithValue("nextAttempt", nextAttemptAt);
        command.Parameters.AddWithValue("queueExpires", queueExpiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            "queued",
            null,
            "queued",
            actor,
            "Authorized report request accepted.",
            new Dictionary<string, object?>
            {
                ["revision"] = context.Definition.RevisionNumber,
                ["rowPolicy"] = context.Definition.RowPolicy,
                ["scopeRevision"] = ScopeRevision,
                ["scopeChecksum"] = context.Scope.SnapshotChecksum,
                ["scopeFacilityId"] = context.Scope.FacilityId,
                ["scopeSubjectCount"] = context.Scope.SubjectCount,
                ["recipient"] = context.RecipientUsername,
                ["asOfDate"] = context.Watermark.BaseDate.ToString("yyyy-MM-dd"),
                ["queueRevision"] = QueueRevision,
                ["nextAttemptAt"] = nextAttemptAt.ToString("O"),
                ["queueExpiresAt"] = queueExpiresAt.ToString("O"),
                ["maxAttempts"] = options.Value.MaxAttempts
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task FailRunAsync(
        string runId,
        string actor,
        string failureCode,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var finished = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? priorStatus;
        await using (var status = connection.CreateCommand())
        {
            status.Transaction = transaction;
            status.CommandText = """
                select status
                from saved_report_runs
                where run_id = @run
                for update;
                """;
            status.Parameters.AddWithValue("run", runId);
            priorStatus = (string?)await status.ExecuteScalarAsync(cancellationToken);
        }
        if (priorStatus is null)
        {
            throw new InvalidOperationException("The report run no longer exists.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update saved_report_runs
            set status = 'failed',
                lifecycle_version = lifecycle_version + 1,
                finished_at = @finished,
                duration_ms = case
                  when started_at is null then 0
                  else greatest(0, floor(extract(epoch from (@finished - started_at)) * 1000)::integer)
                end,
                failure_code = @code,
                failure_message = @message,
                failure_retryable = false,
                next_attempt_at = null,
                result_summary = jsonb_build_object(
                  'failureCode', @code::text,
                  'message', @message::text
                )
            where run_id = @run;
            """;
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("finished", finished);
        command.Parameters.AddWithValue("code", failureCode);
        command.Parameters.AddWithValue("message", failureMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            "failed",
            priorStatus,
            "failed",
            actor,
            "Report execution failed closed.",
            new Dictionary<string, object?>
            {
                ["failureCode"] = failureCode,
                ["message"] = failureMessage
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<(string Fingerprint, GovernedReportRunDetail Detail)?>
        FindIdempotentRunAsync(
            string actor,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select run_id, request_fingerprint
            from saved_report_runs
            where lower(ran_by) = lower(@actor)
              and idempotency_key = @key
            limit 1;
            """;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var runId = reader.GetString(0);
        var fingerprint = reader.GetString(1);
        await reader.DisposeAsync();
        var detail = await GetRunAsync(runId, actor, cancellationToken)
            ?? throw new InvalidOperationException("Idempotent report run could not be reloaded.");
        return (fingerprint, detail);
    }

    private static string NormalizeOperationsFilter(
        string? value,
        string field,
        int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{field} cannot exceed {maximumLength} characters.");
        }
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{field} cannot contain control characters.");
        }
        return normalized;
    }

    private static async Task<GovernedReportOperationsSummary>
        ReadOperationsSummaryAsync(
            NpgsqlConnection connection,
            DateTimeOffset generatedAt,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              count(*)::integer as total_runs,
              jsonb_build_object(
                'queued', count(*) filter (where status='queued'),
                'running', count(*) filter (where status='running'),
                'completed', count(*) filter (where status='completed'),
                'failed', count(*) filter (where status='failed'),
                'cancelled', count(*) filter (where status='cancelled'),
                'expired', count(*) filter (where status='expired')
              )::text as status_counts,
              count(*) filter (
                where status='queued'
                  and coalesce(next_attempt_at, ran_at) <= @generated_at
              )::integer as queued_ready,
              count(*) filter (
                where status='queued'
                  and coalesce(next_attempt_at, ran_at) > @generated_at
              )::integer as queued_delayed,
              count(*) filter (
                where status='running'
                  and lease_expires_at > @generated_at
              )::integer as running_with_lease,
              count(*) filter (
                where status='running'
                  and (
                    lease_expires_at is null
                    or lease_expires_at <= @generated_at
                  )
              )::integer as overdue_leases,
              count(*) filter (
                where status='running'
                  and cancel_requested_at is not null
              )::integer as pending_cancellations,
              count(*) filter (
                where status='failed'
                  and failure_retryable=true
              )::integer as retryable_failures,
              count(*) filter (
                where status='failed'
                  and coalesce(failure_retryable, false)=false
              )::integer as permanent_failures,
              count(*) filter (
                where status='expired'
                  and failure_code='queue-expired'
              )::integer as queue_expired,
              count(*) filter (
                where artifact_expired_at is not null
              )::integer as artifact_expired,
              count(*) filter (
                where result_checksum is not null
                  and finished_at >= @generated_at-interval '24 hours'
              )::integer as completed_last_24_hours,
              count(*) filter (
                where (
                    status='failed'
                    or (
                      status='expired'
                      and failure_code='queue-expired'
                    )
                  )
                  and finished_at >= @generated_at-interval '24 hours'
              )::integer as failed_last_24_hours,
              (
                percentile_cont(0.95) within group (order by duration_ms)
                filter (
                  where result_checksum is not null
                    and duration_ms is not null
                )
              )::integer as p95_completed_duration_ms,
              min(ran_at) filter (where status='queued') as oldest_queued_at
            from saved_report_runs;
            """;
        command.Parameters.AddWithValue("generated_at", generatedAt);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The report operations summary could not be read.");
        }

        var statusCounts =
            JsonSerializer.Deserialize<Dictionary<string, int>>(
                reader.GetString(1),
                JsonOptions) ?? [];
        return new(
            reader.GetInt32(0),
            statusCounts,
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12),
            reader.IsDBNull(13) ? null : reader.GetInt32(13),
            reader.IsDBNull(14)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(14).ToString("O"));
    }

    private static IReadOnlyList<GovernedReportOperationsAlert>
        BuildOperationsAlerts(GovernedReportOperationsSummary summary)
    {
        var alerts = new List<GovernedReportOperationsAlert>();
        if (summary.OverdueLeases > 0)
        {
            alerts.Add(new(
                "overdue-worker-lease",
                "critical",
                summary.OverdueLeases,
                "Running reports have a missing or expired worker lease.",
                null));
        }
        if (summary.PendingCancellations > 0)
        {
            alerts.Add(new(
                "pending-cancellation",
                "warning",
                summary.PendingCancellations,
                "Running reports are waiting for cancellation to settle.",
                null));
        }
        if (summary.RetryableFailures > 0)
        {
            alerts.Add(new(
                "retryable-failure",
                "warning",
                summary.RetryableFailures,
                "Requester-actionable retryable failures need review.",
                null));
        }
        if (summary.PermanentFailures > 0)
        {
            alerts.Add(new(
                "permanent-failure",
                "warning",
                summary.PermanentFailures,
                "Permanent report failures need policy or data review.",
                null));
        }
        if (summary.QueueExpired > 0)
        {
            alerts.Add(new(
                "queue-expired",
                "warning",
                summary.QueueExpired,
                "Report requests expired before a worker could start them.",
                null));
        }
        return alerts;
    }

    private async Task<GovernedReportRunItem?> ReadRunByIdAsync(
        NpgsqlConnection connection,
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {RunSelect}
            where run.run_id = @run;
            """;
        command.Parameters.AddWithValue("run", runId);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRun(reader, replay: false, actor)
            : null;
    }

    private static async Task<IReadOnlyList<GovernedReportRunEvent>>
        ReadRunEventsAsync(
            NpgsqlConnection connection,
            string runId,
            CancellationToken cancellationToken)
    {
        var events = new List<GovernedReportRunEvent>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, run_id, action, from_status, to_status,
                   actor_username, reason, occurred_at, details
            from saved_report_run_events
            where run_id = @run
            order by occurred_at, event_id;
            """;
        command.Parameters.AddWithValue("run", runId);
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),
                DeserializeJsonElements(reader.GetString(8))));
        }
        return events;
    }

    private async Task<GovernedReportRunItem?> ReadAuthorizedRunAsync(
        NpgsqlConnection connection,
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {RunSelect}
            where run.run_id = @run
              and (
                lower(run.ran_by) = lower(@actor)
                or lower(coalesce(run.recipient_username, '')) = lower(@actor)
              );
            """;
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("actor", actor);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRun(reader, replay: false, actor)
            : null;
    }

    private static GovernedReportRunItem ReadRun(
        NpgsqlDataReader reader,
        bool replay,
        string actor)
    {
        var parameters = DeserializeStringDictionary(
            reader.GetString(reader.GetOrdinal("normalized_parameters")));
        var status = reader.GetString(reader.GetOrdinal("status"));
        var artifactContent = reader.IsDBNull(reader.GetOrdinal("artifact_content"))
            ? null
            : reader.GetString(reader.GetOrdinal("artifact_content"));
        var requestedBy = reader.GetString(reader.GetOrdinal("ran_by"));
        var attemptCount = reader.GetInt32(reader.GetOrdinal("attempt_count"));
        var failureRetryable = reader.IsDBNull(
            reader.GetOrdinal("failure_retryable"))
            ? (bool?)null
            : reader.GetBoolean(reader.GetOrdinal("failure_retryable"));
        var cancelRequestedAt = ReadNullableInstant(
            reader,
            "cancel_requested_at");
        var requesterControls =
            string.Equals(requestedBy, actor, StringComparison.OrdinalIgnoreCase);
        return new(
            reader.GetString(reader.GetOrdinal("run_id")),
            reader.GetGuid(reader.GetOrdinal("definition_id")),
            reader.IsDBNull(reader.GetOrdinal("revision_id"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("revision_id")),
            reader.IsDBNull(reader.GetOrdinal("revision_number"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("revision_number")),
            reader.GetString(reader.GetOrdinal("stable_key")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetString(reader.GetOrdinal("report_family")),
            status,
            requestedBy,
            reader.GetString(reader.GetOrdinal("recipient_username")),
            reader.GetString(reader.GetOrdinal("purpose")),
            reader.GetString(reader.GetOrdinal("row_policy")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("as_of_date"))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            parameters,
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("dataset_version")),
            reader.GetString(reader.GetOrdinal("execution_revision")),
            reader.GetString(reader.GetOrdinal("scope_revision")),
            reader.GetString(reader.GetOrdinal("queue_revision")),
            reader.IsDBNull(reader.GetOrdinal("scope_snapshot_checksum"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("scope_snapshot_checksum")),
            reader.IsDBNull(reader.GetOrdinal("scope_facility_id"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("scope_facility_id")),
            reader.IsDBNull(reader.GetOrdinal("scope_subject_count"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("scope_subject_count")),
            reader.GetString(reader.GetOrdinal("definition_snapshot_checksum")),
            reader.GetInt32(reader.GetOrdinal("lifecycle_version")),
            attemptCount,
            reader.GetInt32(reader.GetOrdinal("max_attempts")),
            reader.GetInt32(reader.GetOrdinal("manual_retry_count")),
            ReadNullableInstant(reader, "next_attempt_at"),
            ReadNullableInstant(reader, "last_attempt_at"),
            ReadNullableInstant(reader, "lease_expires_at"),
            ReadNullableInstant(reader, "queue_expires_at"),
            cancelRequestedAt,
            reader.IsDBNull(reader.GetOrdinal("cancel_requested_by"))
                ? null
                : reader.GetString(reader.GetOrdinal("cancel_requested_by")),
            reader.IsDBNull(reader.GetOrdinal("cancel_reason"))
                ? null
                : reader.GetString(reader.GetOrdinal("cancel_reason")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ran_at")).ToString("O"),
            reader.IsDBNull(reader.GetOrdinal("started_at"))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("started_at")).ToString("O"),
            reader.IsDBNull(reader.GetOrdinal("finished_at"))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("finished_at")).ToString("O"),
            reader.IsDBNull(reader.GetOrdinal("duration_ms"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("duration_ms")),
            reader.GetInt32(reader.GetOrdinal("row_count")),
            reader.IsDBNull(reader.GetOrdinal("result_checksum"))
                ? null
                : reader.GetString(reader.GetOrdinal("result_checksum")),
            artifactContent is null ? 0 : Encoding.UTF8.GetByteCount(artifactContent),
            reader.IsDBNull(reader.GetOrdinal("artifact_content_type"))
                ? null
                : reader.GetString(reader.GetOrdinal("artifact_content_type")),
            reader.IsDBNull(reader.GetOrdinal("artifact_file_name"))
                ? null
                : reader.GetString(reader.GetOrdinal("artifact_file_name")),
            ReadNullableInstant(reader, "artifact_expires_at"),
            ReadNullableInstant(reader, "artifact_expired_at"),
            reader.IsDBNull(reader.GetOrdinal("failure_code"))
                ? null
                : reader.GetString(reader.GetOrdinal("failure_code")),
            reader.IsDBNull(reader.GetOrdinal("failure_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("failure_message")),
            failureRetryable,
            status == "completed" && artifactContent is not null,
            requesterControls &&
                (status is "queued" or "running") &&
                cancelRequestedAt is null,
            requesterControls &&
                status == "failed" &&
                failureRetryable == true &&
                attemptCount < 10,
            replay);
    }

    private static string? ReadNullableInstant(
        NpgsqlDataReader reader,
        string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal).ToString("O");
    }

    private async Task<ActiveDefinition?> LoadActiveDefinitionAsync(
        NpgsqlConnection connection,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              definition.id,
              definition.stable_key,
              revision.revision_id,
              revision.revision_number,
              revision.title,
              revision.owner_username,
              revision.purpose,
              revision.report_family,
              revision.parameter_schema,
              revision.row_policy,
              revision.allowed_recipients,
              revision.delivery_modes,
              coalesce(event.snapshot_checksum, '')
            from saved_report_definitions definition
            join saved_report_definition_revisions revision
              on revision.revision_id = definition.active_revision_id
             and revision.status = 'active'
            left join lateral (
              select evidence.snapshot_checksum
              from saved_report_definition_events evidence
              where evidence.revision_id = revision.revision_id
                and evidence.to_status = 'active'
              order by evidence.occurred_at desc, evidence.event_id desc
              limit 1
            ) event on true
            where definition.id = @definition;
            """;
        command.Parameters.AddWithValue("definition", definitionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            JsonSerializer.Deserialize<List<ReportParameterDefinition>>(
                reader.GetString(8),
                JsonOptions) ?? [],
            reader.GetString(9),
            JsonSerializer.Deserialize<List<string>>(
                reader.GetString(10),
                JsonOptions) ?? [],
            JsonSerializer.Deserialize<List<string>>(
                reader.GetString(11),
                JsonOptions) ?? [],
            reader.GetString(12));
    }

    private async Task<DatasetWatermark> GetWatermarkAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, generated_at, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "A dataset watermark is required for governed report execution.");
        }
        return new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetFieldValue<DateOnly>(3));
    }

    private static async Task<bool> IsActiveAccountAsync(
        NpgsqlConnection connection,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists (
              select 1
              from auth_accounts
              where lower(username) = lower(@username)
                and active = true
            );
            """;
        command.Parameters.AddWithValue("username", username);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<ScopeResolution> ResolveScopeAsync(
        NpgsqlConnection connection,
        string rowPolicy,
        string reportFamily,
        string actor,
        CancellationToken cancellationToken)
    {
        if (!RowPolicyFamilySupport.TryGetValue(rowPolicy, out var supportedFamilies))
        {
            return BuildScopeResolution(
                rowPolicy,
                reportFamily,
                actor,
                null,
                null,
                null,
                [],
                executable: false,
                "scope-policy-unavailable",
                $"Row policy '{rowPolicy}' is not supported by {ScopeRevision}.");
        }

        if (!supportedFamilies.Contains(reportFamily, StringComparer.Ordinal))
        {
            return BuildScopeResolution(
                rowPolicy,
                reportFamily,
                actor,
                null,
                null,
                null,
                [],
                executable: false,
                "scope-family-unavailable",
                $"Report family '{reportFamily}' has no approved {rowPolicy} relationship.");
        }

        if (rowPolicy == "practice-wide")
        {
            var count = await CountScopePatientsAsync(
                connection,
                facilityId: null,
                patientIds: null,
                cancellationToken);
            return BuildScopeResolution(
                rowPolicy,
                reportFamily,
                actor,
                null,
                null,
                count,
                [],
                executable: true,
                null,
                null);
        }

        int? staffId = null;
        int? facilityId = null;
        string? facilityCode = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select staff.id, staff.facility_id, facility.code
                from auth_accounts account
                join staff on staff.id=account.staff_id
                left join facilities facility
                  on facility.id=staff.facility_id
                 and facility.inactive=false
                where lower(account.username)=lower(@actor)
                  and account.active=true
                  and staff.active=true;
                """;
            command.Parameters.AddWithValue("actor", actor);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                staffId = reader.GetInt32(0);
                facilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                facilityCode = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        if (staffId is null)
        {
            return BuildScopeResolution(
                rowPolicy,
                reportFamily,
                actor,
                null,
                null,
                null,
                [],
                executable: false,
                "scope-identity-unavailable",
                "The authenticated account is not linked to an active local staff identity.");
        }

        if (rowPolicy == "facility-scoped")
        {
            if (facilityId is null || facilityCode is null)
            {
                return BuildScopeResolution(
                    rowPolicy,
                    reportFamily,
                    actor,
                    staffId,
                    facilityId,
                    null,
                    [],
                    executable: false,
                    "scope-facility-unavailable",
                    "The active staff identity is not linked to an active facility.");
            }
            var count = await CountScopePatientsAsync(
                connection,
                facilityId,
                patientIds: null,
                cancellationToken);
            return BuildScopeResolution(
                rowPolicy,
                reportFamily,
                actor,
                staffId,
                facilityId,
                count,
                [],
                executable: true,
                null,
                null,
                facilityCode);
        }

        var assignedPatientIds = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select patient.canonical_id
                from patients patient
                where patient.merged_into_patient_id is null
                  and (
                    patient.provider_id=@staff
                    or exists (
                      select 1
                      from patient_care_teams team
                      join patient_care_team_members member
                        on member.patient_id=team.patient_id
                      where team.patient_id=patient.canonical_id
                        and team.team_status='active'
                        and member.user_id=@staff
                        and member.status='active'
                    )
                  )
                order by patient.canonical_id;
                """;
            command.Parameters.AddWithValue("staff", staffId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assignedPatientIds.Add(reader.GetString(0));
            }
        }

        return BuildScopeResolution(
            rowPolicy,
            reportFamily,
            actor,
            staffId,
            facilityId,
            assignedPatientIds.Count,
            assignedPatientIds,
            executable: true,
            null,
            null,
            facilityCode);
    }

    private static async Task<int> CountScopePatientsAsync(
        NpgsqlConnection connection,
        int? facilityId,
        IReadOnlyList<string>? patientIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = patientIds is not null
            ? """
              select count(*)
              from patients
              where merged_into_patient_id is null
                and canonical_id=any(@patients);
              """
            : facilityId is not null
                ? """
                  select count(*)
                  from patients
                  where merged_into_patient_id is null
                    and facility_id=@facility;
                  """
                : """
                  select count(*)
                  from patients
                  where merged_into_patient_id is null;
                  """;
        if (patientIds is not null)
        {
            command.Parameters.Add("patients", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
                patientIds.ToArray();
        }
        if (facilityId is not null)
        {
            command.Parameters.AddWithValue("facility", facilityId.Value);
        }
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static ScopeResolution BuildScopeResolution(
        string rowPolicy,
        string reportFamily,
        string actor,
        int? staffId,
        int? facilityId,
        int? subjectCount,
        IReadOnlyList<string> patientIds,
        bool executable,
        string? failureCode,
        string? failureMessage,
        string? facilityCode = null)
    {
        var snapshot = JsonSerializer.Serialize(
            new
            {
                revision = ScopeRevision,
                rowPolicy,
                reportFamily,
                actor,
                staffId,
                facilityId,
                facilityCode,
                subjectCount,
                assignedPatientIds = patientIds,
                sources = new[]
                {
                    "auth_accounts.staff_id",
                    "staff.facility_id",
                    "patients.provider_id",
                    "patient_care_team_members.user_id"
                },
                executable,
                failureCode
            },
            JsonOptions);
        return new(
            executable,
            failureCode,
            failureMessage,
            staffId,
            facilityId,
            subjectCount,
            patientIds,
            snapshot,
            Sha256(snapshot),
            new(rowPolicy, facilityId, patientIds));
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string runId,
        string action,
        string? fromStatus,
        string toStatus,
        string actor,
        string reason,
        IReadOnlyDictionary<string, object?> details,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            values (
              @event, @run, @action, @from, @to,
              @actor, @reason, clock_timestamp(), @details);
            """;
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("from", NpgsqlDbType.Text).Value =
            (object?)fromStatus ?? DBNull.Value;
        command.Parameters.AddWithValue("to", toStatus);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.Add("details", NpgsqlDbType.Jsonb).Value =
            JsonSerializer.Serialize(details, JsonOptions);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildFingerprint(
        ExecutionContext context,
        string idempotencyKey)
    {
        var canonicalParameters = string.Join(
            "&",
            context.Parameters
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value ?? "<null>"}"));
        return Sha256(
            string.Join(
                "|",
                idempotencyKey,
                context.Definition.DefinitionId,
                context.Definition.RevisionId,
                context.Definition.RevisionNumber,
                context.Purpose,
                context.RecipientUsername.ToLowerInvariant(),
                context.DeliveryMode,
                context.Watermark.BaseDate.ToString("yyyy-MM-dd"),
                context.Scope.SnapshotChecksum,
                canonicalParameters));
    }

    private static void EnsureScopeExecutable(ScopeResolution scope)
    {
        if (!scope.Executable)
        {
            throw new ArgumentException(scope.FailureMessage);
        }
    }

    private static string NormalizeIdempotencyKey(string value)
    {
        var normalized = NormalizeRequired(value, "Idempotency key", 100);
        if (!IdempotencyKeyPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Idempotency key must be 8-100 safe letters, digits, dots, underscores, colons, or dashes.");
        }
        return normalized;
    }

    private static string NormalizeRunId(string value)
    {
        var normalized = NormalizeRequired(value, "Run ID", 40);
        if (!normalized.StartsWith("RPT-", StringComparison.Ordinal) ||
            normalized.Length != 36 ||
            !Guid.TryParseExact(normalized[4..], "N", out _))
        {
            throw new ArgumentException("Run ID is invalid.");
        }
        return normalized;
    }

    private static string NormalizeUsername(string value, string label)
    {
        var normalized = NormalizeRequired(value, label, 80);
        return normalized;
    }

    private static string NormalizeLifecycleReason(string value)
    {
        var normalized = NormalizeRequired(value, "Lifecycle reason", 500);
        if (normalized.Length < 10)
        {
            throw new ArgumentException(
                "Lifecycle reason must be at least 10 characters.");
        }
        return normalized;
    }

    private static string NormalizeRequired(string value, string label, int maximum)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximum)
        {
            throw new ArgumentException(
                $"{label} is required and must be {maximum} characters or fewer.");
        }
        return normalized;
    }

    private static void RejectUnknownProperties(
        IDictionary<string, JsonElement>? additionalProperties)
    {
        if (additionalProperties is { Count: > 0 })
        {
            throw new ArgumentException(
                $"Unknown request field(s): {string.Join(", ", additionalProperties.Keys.Order(StringComparer.Ordinal))}.");
        }
    }

    private static string BoundFailure(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "Report execution failed."
            : message.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string SanitizeFileName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ||
                           character is '-' or '_' or '.'
                ? character
                : '-');
        }
        return builder.ToString();
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static IReadOnlyDictionary<string, string?> DeserializeStringDictionary(
        string json) =>
        JsonSerializer.Deserialize<SortedDictionary<string, string?>>(
            json,
            JsonOptions) ?? new SortedDictionary<string, string?>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, JsonElement> DeserializeJsonElements(
        string json) =>
        JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>>(
            json,
            JsonOptions) ?? new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (quoted)
            {
                if (character == '"' &&
                    index + 1 < csv.Length &&
                    csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' &&
                    index + 1 < csv.Length &&
                    csv[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => value.Length > 0))
                {
                    rows.Add(row);
                }
                row = [];
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(value => value.Length > 0))
            {
                rows.Add(row);
            }
        }
        return rows;
    }

    private const string RunSelect = """
        select
          run.run_id,
          run.definition_id,
          run.revision_id,
          run.revision_number,
          definition.stable_key,
          coalesce(revision.title, definition.name) as title,
          coalesce(revision.report_family, definition.report_type) as report_family,
          run.status,
          run.ran_by,
          coalesce(run.recipient_username, run.ran_by) as recipient_username,
          coalesce(run.purpose, 'Legacy local run; purpose unavailable.') as purpose,
          coalesce(run.row_policy, 'owner-review-required') as row_policy,
          coalesce(run.as_of_date, run.ran_at::date) as as_of_date,
          run.normalized_parameters,
          coalesce(run.dataset_id, 'unknown') as dataset_id,
          coalesce(run.dataset_version, 'unknown') as dataset_version,
          run.execution_revision,
          run.scope_revision,
          run.queue_revision,
          run.scope_snapshot_checksum,
          run.scope_facility_id,
          run.scope_subject_count,
          coalesce(run.definition_snapshot_checksum, '') as definition_snapshot_checksum,
          run.lifecycle_version,
          run.attempt_count,
          run.max_attempts,
          run.manual_retry_count,
          run.next_attempt_at,
          run.last_attempt_at,
          run.lease_expires_at,
          run.queue_expires_at,
          run.cancel_requested_at,
          run.cancel_requested_by,
          run.cancel_reason,
          run.ran_at,
          run.started_at,
          run.finished_at,
          run.duration_ms,
          run.row_count,
          run.result_checksum,
          run.artifact_content,
          run.artifact_content_type,
          run.artifact_file_name,
          run.artifact_expires_at,
          run.artifact_expired_at,
          run.failure_code,
          run.failure_message,
          run.failure_retryable,
          count(*) over()::integer as total_count
        from saved_report_runs run
        join saved_report_definitions definition
          on definition.id = run.definition_id
        left join saved_report_definition_revisions revision
          on revision.revision_id = run.revision_id
        """;

    private sealed record DatasetWatermark(
        string DatasetId,
        string DatasetVersion,
        DateTimeOffset GeneratedAt,
        DateOnly BaseDate);

    private sealed record ActiveDefinition(
        Guid DefinitionId,
        string StableKey,
        Guid RevisionId,
        int RevisionNumber,
        string Title,
        string OwnerUsername,
        string Purpose,
        string ReportFamily,
        IReadOnlyList<ReportParameterDefinition> ParameterSchema,
        string RowPolicy,
        IReadOnlyList<string> AllowedRecipients,
        IReadOnlyList<string> DeliveryModes,
        string SnapshotChecksum);

    private sealed record ExecutionContext(
        ActiveDefinition Definition,
        DatasetWatermark Watermark,
        string Actor,
        string Purpose,
        string RecipientUsername,
        string DeliveryMode,
        IReadOnlyDictionary<string, string?> Parameters,
        DateOnly? From,
        DateOnly? To,
        ScopeResolution Scope);

    private sealed record ScopeResolution(
        bool Executable,
        string? FailureCode,
        string? FailureMessage,
        int? StaffId,
        int? FacilityId,
        int? SubjectCount,
        IReadOnlyList<string> PatientIds,
        string SnapshotJson,
        string SnapshotChecksum,
        GovernedReportDataScope DataScope);
}
