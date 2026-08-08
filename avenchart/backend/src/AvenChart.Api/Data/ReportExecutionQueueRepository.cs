// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Configuration;

namespace AvenChart.Api.Data;

public sealed class ReportExecutionQueueRepository(
    NpgsqlDataSource dataSource,
    ReportRepository reportRepository,
    IOptions<ReportExecutionOptions> options,
    ILogger<ReportExecutionQueueRepository> logger)
{
    private const int MaximumRows = 5000;
    private const string QueueRevision = "local-report-queue-v1";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<bool> ProcessNextAsync(
        string workerId,
        CancellationToken stoppingToken)
    {
        var maintained = await MaintainAsync(stoppingToken);
        var claim = await ClaimNextAsync(workerId, stoppingToken);
        if (claim is null)
        {
            return maintained;
        }

        await ExecuteAsync(claim, workerId, stoppingToken);
        return true;
    }

    private async Task<bool> MaintainAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        var changed = 0;

        changed += await ExecuteMaintenanceAsync(
            connection,
            transaction,
            """
            with candidates as (
              select run_id
              from saved_report_runs
              where status='running'
                and cancel_requested_at is not null
                and (lease_expires_at is null or lease_expires_at <= now())
              order by lease_expires_at nulls first, run_id
              for update skip locked
              limit 25
            ),
            transitioned as (
              update saved_report_runs run
              set status='cancelled',
                  lifecycle_version=run.lifecycle_version+1,
                  finished_at=now(),
                  duration_ms=case
                    when run.last_attempt_at is null then 0
                    else greatest(
                      0,
                      floor(extract(epoch from (now()-run.last_attempt_at))*1000)::integer)
                  end,
                  lease_owner=null,
                  lease_expires_at=null,
                  last_heartbeat_at=null,
                  next_attempt_at=null,
                  failure_code='cancelled-by-request',
                  failure_message=coalesce(
                    run.cancel_reason,
                    'The report was cancelled by an authorized requester.'),
                  failure_retryable=false,
                  result_summary=jsonb_build_object(
                    'failureCode',
                    'cancelled-by-request',
                    'message',
                    coalesce(
                      run.cancel_reason,
                      'The report was cancelled by an authorized requester.'))
              from candidates
              where run.run_id=candidates.run_id
              returning run.run_id,
                        run.ran_by,
                        run.cancel_requested_by,
                        run.cancel_reason
            )
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            select gen_random_uuid(),
                   run_id,
                   'cancelled',
                   'running',
                   'cancelled',
                   coalesce(cancel_requested_by, ran_by),
                   coalesce(
                     cancel_reason,
                     'The report was cancelled by an authorized requester.'),
                   now(),
                   jsonb_build_object('recoveredAfterLeaseExpiry', true)
            from transitioned;
            """,
            cancellationToken);

        changed += await ExecuteMaintenanceAsync(
            connection,
            transaction,
            """
            with candidates as (
              select run_id
              from saved_report_runs
              where status='running'
                and cancel_requested_at is null
                and lease_expires_at <= now()
                and attempt_count >= max_attempts
              order by lease_expires_at, run_id
              for update skip locked
              limit 25
            ),
            transitioned as (
              update saved_report_runs run
              set status='failed',
                  lifecycle_version=run.lifecycle_version+1,
                  finished_at=now(),
                  duration_ms=case
                    when run.last_attempt_at is null then 0
                    else greatest(
                      0,
                      floor(extract(epoch from (now()-run.last_attempt_at))*1000)::integer)
                  end,
                  lease_owner=null,
                  lease_expires_at=null,
                  last_heartbeat_at=null,
                  next_attempt_at=null,
                  failure_code='worker-lease-exhausted',
                  failure_message='The worker lease expired after the maximum attempt count.',
                  failure_retryable=false,
                  result_summary=jsonb_build_object(
                    'failureCode',
                    'worker-lease-exhausted',
                    'message',
                    'The worker lease expired after the maximum attempt count.')
              from candidates
              where run.run_id=candidates.run_id
              returning run.run_id, run.attempt_count, run.max_attempts
            )
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            select gen_random_uuid(),
                   run_id,
                   'failed',
                   'running',
                   'failed',
                   'report-worker',
                   'The expired worker lease exhausted the permitted attempts.',
                   now(),
                   jsonb_build_object(
                     'failureCode',
                     'worker-lease-exhausted',
                     'attemptCount',
                     attempt_count,
                     'maxAttempts',
                     max_attempts)
            from transitioned;
            """,
            cancellationToken);

        changed += await ExecuteMaintenanceAsync(
            connection,
            transaction,
            """
            with candidates as (
              select run_id
              from saved_report_runs
              where status='running'
                and cancel_requested_at is null
                and lease_expires_at <= now()
                and attempt_count < max_attempts
              order by lease_expires_at, run_id
              for update skip locked
              limit 25
            ),
            transitioned as (
              update saved_report_runs run
              set status='queued',
                  lifecycle_version=run.lifecycle_version+1,
                  next_attempt_at=now(),
                  lease_owner=null,
                  lease_expires_at=null,
                  last_heartbeat_at=null,
                  failure_code='worker-lease-expired',
                  failure_message='The prior worker lease expired; the durable request was recovered.',
                  failure_retryable=true,
                  result_summary=jsonb_build_object(
                    'failureCode',
                    'worker-lease-expired',
                    'message',
                    'The prior worker lease expired; the durable request was recovered.')
              from candidates
              where run.run_id=candidates.run_id
              returning run.run_id, run.attempt_count, run.max_attempts
            )
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            select gen_random_uuid(),
                   run_id,
                   'lease-recovered',
                   'running',
                   'queued',
                   'report-worker',
                   'An expired worker lease was recovered for another attempt.',
                   now(),
                   jsonb_build_object(
                     'failureCode',
                     'worker-lease-expired',
                     'attemptCount',
                     attempt_count,
                     'maxAttempts',
                     max_attempts)
            from transitioned;
            """,
            cancellationToken);

        changed += await ExecuteMaintenanceAsync(
            connection,
            transaction,
            """
            with candidates as (
              select run_id
              from saved_report_runs
              where status='queued'
                and queue_expires_at <= now()
              order by queue_expires_at, run_id
              for update skip locked
              limit 25
            ),
            transitioned as (
              update saved_report_runs run
              set status='expired',
                  lifecycle_version=run.lifecycle_version+1,
                  finished_at=now(),
                  duration_ms=0,
                  next_attempt_at=null,
                  failure_code='queue-expired',
                  failure_message='The report request expired before a worker could start it.',
                  failure_retryable=false,
                  result_summary=jsonb_build_object(
                    'failureCode',
                    'queue-expired',
                    'message',
                    'The report request expired before a worker could start it.')
              from candidates
              where run.run_id=candidates.run_id
              returning run.run_id
            )
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            select gen_random_uuid(),
                   run_id,
                   'expired',
                   'queued',
                   'expired',
                   'report-worker',
                   'The durable request exceeded its queue lifetime.',
                   now(),
                   jsonb_build_object('failureCode', 'queue-expired')
            from transitioned;
            """,
            cancellationToken);

        changed += await ExecuteMaintenanceAsync(
            connection,
            transaction,
            """
            with candidates as (
              select run_id
              from saved_report_runs
              where status='completed'
                and artifact_content is not null
                and artifact_expires_at <= now()
              order by artifact_expires_at, run_id
              for update skip locked
              limit 25
            ),
            transitioned as (
              update saved_report_runs run
              set status='expired',
                  lifecycle_version=run.lifecycle_version+1,
                  artifact_content=null,
                  artifact_expired_at=now()
              from candidates
              where run.run_id=candidates.run_id
              returning run.run_id,
                        run.result_checksum,
                        run.artifact_file_name,
                        run.artifact_expires_at
            )
            insert into saved_report_run_events (
              event_id, run_id, action, from_status, to_status,
              actor_username, reason, occurred_at, details)
            select gen_random_uuid(),
                   run_id,
                   'artifact-expired',
                   'completed',
                   'expired',
                   'report-worker',
                   'The local artifact reached the definition retention deadline.',
                   now(),
                   jsonb_build_object(
                     'checksum',
                     result_checksum,
                     'fileName',
                     artifact_file_name,
                     'expiredAt',
                     artifact_expires_at)
            from transitioned;
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return changed > 0;
    }

    private static async Task<int> ExecuteMaintenanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<ClaimedReportRun?> ClaimNextAsync(
        string workerId,
        CancellationToken cancellationToken)
    {
        var configured = options.Value;
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        string? runId = null;
        var attemptCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                with candidate as (
                  select run_id
                  from saved_report_runs
                  where status='queued'
                    and coalesce(next_attempt_at, ran_at) <= now()
                    and (queue_expires_at is null or queue_expires_at > now())
                    and cancel_requested_at is null
                  order by coalesce(next_attempt_at, ran_at), ran_at, run_id
                  for update skip locked
                  limit 1
                )
                update saved_report_runs run
                set status='running',
                    lifecycle_version=run.lifecycle_version+1,
                    attempt_count=run.attempt_count+1,
                    started_at=coalesce(run.started_at, now()),
                    last_attempt_at=now(),
                    lease_owner=@worker,
                    lease_expires_at=now()+(@leaseSeconds*interval '1 second'),
                    last_heartbeat_at=now()
                from candidate
                where run.run_id=candidate.run_id
                returning run.run_id, run.attempt_count;
                """;
            command.Parameters.AddWithValue("worker", workerId);
            command.Parameters.AddWithValue(
                "leaseSeconds",
                configured.LeaseSeconds);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                runId = reader.GetString(0);
                attemptCount = reader.GetInt32(1);
            }
        }
        if (runId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            attemptCount == 1 ? "started" : "retry-started",
            "queued",
            "running",
            workerId,
            attemptCount == 1
                ? "Durable local report execution started."
                : "A durable local report retry started.",
            new Dictionary<string, object?>
            {
                ["attemptCount"] = attemptCount,
                ["leaseSeconds"] = configured.LeaseSeconds,
                ["queueRevision"] = QueueRevision
            },
            cancellationToken);

        ClaimedReportRun claim;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                  run.run_id,
                  run.ran_by,
                  revision.report_family,
                  run.row_policy,
                  run.normalized_parameters,
                  run.as_of_date,
                  run.scope_snapshot,
                  run.scope_facility_id,
                  run.dataset_id,
                  run.dataset_version,
                  revision.retention_days,
                  (
                    definition.active_revision_id=run.revision_id
                    and revision.status='active'
                  ) as active_revision_available,
                  exists (
                    select 1
                    from dataset_metadata dataset
                    where dataset.dataset_id=run.dataset_id
                      and dataset.version=run.dataset_version
                      and dataset.base_date=run.as_of_date
                  ) as source_snapshot_available
                from saved_report_runs run
                join saved_report_definitions definition
                  on definition.id=run.definition_id
                join saved_report_definition_revisions revision
                  on revision.revision_id=run.revision_id
                where run.run_id=@run
                  and run.status='running'
                  and run.lease_owner=@worker;
                """;
            command.Parameters.AddWithValue("run", runId);
            command.Parameters.AddWithValue("worker", workerId);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "The claimed report execution context is unavailable.");
            }

            claim = new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DeserializeStringDictionary(reader.GetString(4)),
                reader.GetFieldValue<DateOnly>(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetBoolean(11),
                reader.GetBoolean(12));
        }

        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    private async Task ExecuteAsync(
        ClaimedReportRun claim,
        string workerId,
        CancellationToken stoppingToken)
    {
        if (!claim.ActiveRevisionAvailable)
        {
            await TransitionFailureAsync(
                claim.RunId,
                workerId,
                "definition-revision-unavailable",
                "The pinned definition revision is no longer active.",
                retryable: false,
                CancellationToken.None);
            return;
        }

        if (!claim.SourceSnapshotAvailable)
        {
            await TransitionFailureAsync(
                claim.RunId,
                workerId,
                "source-snapshot-unavailable",
                $"Dataset {claim.DatasetId}@{claim.DatasetVersion} is no longer available for the pinned as-of date.",
                retryable: false,
                CancellationToken.None);
            return;
        }

        GovernedReportDataScope scope;
        try
        {
            scope = BuildDataScope(claim);
        }
        catch (Exception exception)
        {
            await TransitionFailureAsync(
                claim.RunId,
                workerId,
                "scope-snapshot-invalid",
                BoundFailure(exception.Message),
                retryable: false,
                CancellationToken.None);
            return;
        }

        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        executionCancellation.CancelAfter(
            TimeSpan.FromSeconds(options.Value.ExecutionTimeoutSeconds));
        using var monitorCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var cancellationState = new WorkerCancellationState();
        var monitor = MonitorAsync(
            claim.RunId,
            workerId,
            executionCancellation,
            cancellationState,
            monitorCancellation.Token);

        try
        {
            var from = ReadDateParameter(claim.Parameters, "from");
            var to = ReadDateParameter(claim.Parameters, "to");
            var started = DateTimeOffset.UtcNow;
            var csv = await reportRepository.GetGovernedFamilyCsvAsync(
                claim.ReportFamily,
                from,
                to,
                scope,
                executionCancellation.Token);
            var rowCount = Math.Max(0, ParseCsv(csv).Count - 1);
            if (rowCount > MaximumRows)
            {
                throw new InvalidOperationException(
                    $"The result exceeded the {MaximumRows}-row execution limit.");
            }

            var checksum = Sha256(csv);
            var bytes = Encoding.UTF8.GetByteCount(csv);
            var finished = DateTimeOffset.UtcNow;
            var duration = Math.Max(
                0,
                Convert.ToInt32((finished - started).TotalMilliseconds));
            await CompleteAsync(
                claim,
                workerId,
                csv,
                rowCount,
                bytes,
                checksum,
                duration,
                finished,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            if (cancellationState.Value == WorkerCancellationCause.Requested)
            {
                await TransitionCancellationAsync(
                    claim.RunId,
                    workerId,
                    CancellationToken.None);
            }
            else if (cancellationState.Value == WorkerCancellationCause.LeaseLost)
            {
                logger.LogWarning(
                    "Governed report {RunId} stopped after worker {WorkerId} lost its lease.",
                    claim.RunId,
                    workerId);
            }
            else if (stoppingToken.IsCancellationRequested)
            {
                await TransitionFailureAsync(
                    claim.RunId,
                    workerId,
                    "worker-stopped",
                    "The worker stopped before the report completed.",
                    retryable: true,
                    CancellationToken.None);
            }
            else
            {
                await TransitionFailureAsync(
                    claim.RunId,
                    workerId,
                    "execution-timeout",
                    $"The report exceeded the {options.Value.ExecutionTimeoutSeconds}-second execution timeout.",
                    retryable: true,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            await TransitionFailureAsync(
                claim.RunId,
                workerId,
                IsRetryable(exception)
                    ? "execution-transient"
                    : "execution-failed",
                BoundFailure(exception.Message),
                IsRetryable(exception),
                CancellationToken.None);
        }
        finally
        {
            monitorCancellation.Cancel();
            try
            {
                await monitor;
            }
            catch (OperationCanceledException)
            {
                // The monitor is expected to stop when execution reaches a terminal path.
            }
        }
    }

    private async Task MonitorAsync(
        string runId,
        string workerId,
        CancellationTokenSource executionCancellation,
        WorkerCancellationState state,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(
            options.Value.HeartbeatIntervalMilliseconds);
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            var outcome = await RenewLeaseAsync(
                runId,
                workerId,
                cancellationToken);
            if (outcome == LeaseRenewalOutcome.Active)
            {
                continue;
            }

            state.TrySet(
                outcome == LeaseRenewalOutcome.CancellationRequested
                    ? WorkerCancellationCause.Requested
                    : WorkerCancellationCause.LeaseLost);
            executionCancellation.Cancel();
            return;
        }
    }

    private async Task<LeaseRenewalOutcome> RenewLeaseAsync(
        string runId,
        string workerId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update saved_report_runs
            set lease_expires_at=now()+(@leaseSeconds*interval '1 second'),
                last_heartbeat_at=now()
            where run_id=@run
              and status='running'
              and lease_owner=@worker
            returning cancel_requested_at is not null;
            """;
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("worker", workerId);
        command.Parameters.AddWithValue(
            "leaseSeconds",
            options.Value.LeaseSeconds);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            true => LeaseRenewalOutcome.CancellationRequested,
            false => LeaseRenewalOutcome.Active,
            _ => LeaseRenewalOutcome.LeaseLost
        };
    }

    private async Task<bool> CompleteAsync(
        ClaimedReportRun claim,
        string workerId,
        string csv,
        int rowCount,
        int bytes,
        string checksum,
        int durationMs,
        DateTimeOffset finished,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        CancelSnapshot? cancellation;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select cancel_requested_at,
                       cancel_requested_by,
                       cancel_reason
                from saved_report_runs
                where run_id=@run
                  and status='running'
                  and lease_owner=@worker
                for update;
                """;
            command.Parameters.AddWithValue("run", claim.RunId);
            command.Parameters.AddWithValue("worker", workerId);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            cancellation = reader.IsDBNull(0)
                ? null
                : new(
                    reader.GetFieldValue<DateTimeOffset>(0),
                    reader.IsDBNull(1) ? claim.RequestedBy : reader.GetString(1),
                    reader.IsDBNull(2)
                        ? "The report was cancelled by an authorized requester."
                        : reader.GetString(2));
        }

        if (cancellation is not null)
        {
            await CancelLockedAsync(
                connection,
                transaction,
                claim.RunId,
                cancellation,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        var summary = JsonSerializer.Serialize(
            new { rowCount, bytes, checksum },
            JsonOptions);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update saved_report_runs
                set status='completed',
                    lifecycle_version=lifecycle_version+1,
                    finished_at=@finished,
                    duration_ms=@duration,
                    row_count=@rows,
                    result_checksum=@checksum,
                    result_summary=@summary,
                    artifact_content=@artifact,
                    artifact_expires_at=@finished+(@retentionDays*interval '1 day'),
                    artifact_expired_at=null,
                    lease_owner=null,
                    lease_expires_at=null,
                    last_heartbeat_at=null,
                    next_attempt_at=null,
                    failure_code=null,
                    failure_message=null,
                    failure_retryable=null
                where run_id=@run
                  and status='running'
                  and lease_owner=@worker;

                update saved_report_definitions definition
                set last_run_at=@finished,
                    run_count=definition.run_count+1
                from saved_report_runs run
                where run.run_id=@run
                  and run.status='completed'
                  and definition.id=run.definition_id;
                """;
            command.Parameters.AddWithValue("run", claim.RunId);
            command.Parameters.AddWithValue("worker", workerId);
            command.Parameters.AddWithValue("finished", finished);
            command.Parameters.AddWithValue("duration", durationMs);
            command.Parameters.AddWithValue("rows", rowCount);
            command.Parameters.AddWithValue("checksum", checksum);
            command.Parameters.Add("summary", NpgsqlDbType.Jsonb).Value = summary;
            command.Parameters.AddWithValue("artifact", csv);
            command.Parameters.AddWithValue(
                "retentionDays",
                claim.RetentionDays);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            claim.RunId,
            "completed",
            "running",
            "completed",
            workerId,
            "The durable report worker completed the local artifact.",
            new Dictionary<string, object?>
            {
                ["rowCount"] = rowCount,
                ["bytes"] = bytes,
                ["checksum"] = checksum,
                ["durationMs"] = durationMs,
                ["retentionDays"] = claim.RetentionDays
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task TransitionCancellationAsync(
        string runId,
        string workerId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        CancelSnapshot? cancellation;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select cancel_requested_at,
                       cancel_requested_by,
                       cancel_reason
                from saved_report_runs
                where run_id=@run
                  and status='running'
                  and lease_owner=@worker
                for update;
                """;
            command.Parameters.AddWithValue("run", runId);
            command.Parameters.AddWithValue("worker", workerId);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                reader.IsDBNull(0))
            {
                return;
            }

            cancellation = new(
                reader.GetFieldValue<DateTimeOffset>(0),
                reader.IsDBNull(1) ? "report-worker" : reader.GetString(1),
                reader.IsDBNull(2)
                    ? "The report was cancelled by an authorized requester."
                    : reader.GetString(2));
        }

        await CancelLockedAsync(
            connection,
            transaction,
            runId,
            cancellation,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CancelLockedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string runId,
        CancelSnapshot cancellation,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update saved_report_runs
                set status='cancelled',
                    lifecycle_version=lifecycle_version+1,
                    finished_at=now(),
                    duration_ms=case
                      when last_attempt_at is null then 0
                      else greatest(
                        0,
                        floor(extract(epoch from (now()-last_attempt_at))*1000)::integer)
                    end,
                    lease_owner=null,
                    lease_expires_at=null,
                    last_heartbeat_at=null,
                    next_attempt_at=null,
                    failure_code='cancelled-by-request',
                    failure_message=@reason,
                    failure_retryable=false,
                    result_summary=jsonb_build_object(
                      'failureCode',
                      'cancelled-by-request',
                      'message',
                      @reason::text)
                where run_id=@run and status='running';
                """;
            command.Parameters.AddWithValue("run", runId);
            command.Parameters.AddWithValue("reason", cancellation.Reason);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            "cancelled",
            "running",
            "cancelled",
            cancellation.RequestedBy,
            cancellation.Reason,
            new Dictionary<string, object?>
            {
                ["requestedAt"] = cancellation.RequestedAt.ToString("O")
            },
            cancellationToken);
    }

    private async Task TransitionFailureAsync(
        string runId,
        string workerId,
        string failureCode,
        string failureMessage,
        bool retryable,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        FailureSnapshot? current;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select attempt_count,
                       max_attempts,
                       queue_expires_at,
                       cancel_requested_at,
                       cancel_requested_by,
                       cancel_reason
                from saved_report_runs
                where run_id=@run
                  and status='running'
                  and lease_owner=@worker
                for update;
                """;
            command.Parameters.AddWithValue("run", runId);
            command.Parameters.AddWithValue("worker", workerId);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return;
            }

            current = new(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.IsDBNull(2)
                    ? null
                    : reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3)
                    ? null
                    : new CancelSnapshot(
                        reader.GetFieldValue<DateTimeOffset>(3),
                        reader.IsDBNull(4) ? "report-worker" : reader.GetString(4),
                        reader.IsDBNull(5)
                            ? "The report was cancelled by an authorized requester."
                            : reader.GetString(5)));
        }

        if (current.Cancellation is not null)
        {
            await CancelLockedAsync(
                connection,
                transaction,
                runId,
                current.Cancellation,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var canRetry = retryable &&
            current.AttemptCount < current.MaxAttempts &&
            (current.QueueExpiresAt is null || current.QueueExpiresAt > now);
        var nextAttempt = canRetry
            ? now.AddSeconds(Math.Min(
                60,
                options.Value.RetryBaseDelaySeconds *
                Math.Pow(2, Math.Max(0, current.AttemptCount - 1))))
            : (DateTimeOffset?)null;
        var boundedMessage = BoundFailure(failureMessage);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = canRetry
                ? """
                  update saved_report_runs
                  set status='queued',
                      lifecycle_version=lifecycle_version+1,
                      next_attempt_at=@nextAttempt,
                      lease_owner=null,
                      lease_expires_at=null,
                      last_heartbeat_at=null,
                      failure_code=@code,
                      failure_message=@message,
                      failure_retryable=true,
                      result_summary=jsonb_build_object(
                        'failureCode',
                        @code::text,
                        'message',
                        @message::text,
                        'retryScheduledAt',
                        @nextAttempt::timestamptz)
                  where run_id=@run
                    and status='running'
                    and lease_owner=@worker;
                  """
                : """
                  update saved_report_runs
                  set status='failed',
                      lifecycle_version=lifecycle_version+1,
                      finished_at=now(),
                      duration_ms=case
                        when last_attempt_at is null then 0
                        else greatest(
                          0,
                          floor(extract(epoch from (now()-last_attempt_at))*1000)::integer)
                      end,
                      next_attempt_at=null,
                      lease_owner=null,
                      lease_expires_at=null,
                      last_heartbeat_at=null,
                      failure_code=@code,
                      failure_message=@message,
                      failure_retryable=@retryable,
                      result_summary=jsonb_build_object(
                        'failureCode',
                        @code::text,
                        'message',
                        @message::text)
                  where run_id=@run
                    and status='running'
                    and lease_owner=@worker;
                  """;
            command.Parameters.AddWithValue("run", runId);
            command.Parameters.AddWithValue("worker", workerId);
            command.Parameters.AddWithValue("code", failureCode);
            command.Parameters.AddWithValue("message", boundedMessage);
            command.Parameters.AddWithValue("retryable", retryable);
            if (canRetry)
            {
                command.Parameters.AddWithValue("nextAttempt", nextAttempt!.Value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            canRetry ? "retry-scheduled" : "failed",
            "running",
            canRetry ? "queued" : "failed",
            workerId,
            canRetry
                ? "A retryable failure was classified and another attempt was scheduled."
                : "The durable report execution failed closed.",
            new Dictionary<string, object?>
            {
                ["failureCode"] = failureCode,
                ["message"] = boundedMessage,
                ["retryable"] = retryable,
                ["attemptCount"] = current.AttemptCount,
                ["maxAttempts"] = current.MaxAttempts,
                ["nextAttemptAt"] = nextAttempt?.ToString("O")
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static GovernedReportDataScope BuildDataScope(
        ClaimedReportRun claim)
    {
        var patientIds = new List<string>();
        using var snapshot = JsonDocument.Parse(claim.ScopeSnapshot);
        if (snapshot.RootElement.TryGetProperty(
                "assignedPatientIds",
                out var assigned) &&
            assigned.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in assigned.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    patientIds.Add(item.GetString()!);
                }
            }
        }

        if (claim.RowPolicy == "facility-scoped" &&
            claim.ScopeFacilityId is null)
        {
            throw new InvalidOperationException(
                "The pinned facility scope is unavailable.");
        }
        if (claim.RowPolicy == "patient-assigned" &&
            !snapshot.RootElement.TryGetProperty(
                "assignedPatientIds",
                out _))
        {
            throw new InvalidOperationException(
                "The pinned patient-assignment scope is unavailable.");
        }

        return new(
            claim.RowPolicy,
            claim.ScopeFacilityId,
            patientIds);
    }

    private static DateOnly? ReadDateParameter(
        IReadOnlyDictionary<string, string?> parameters,
        string key)
    {
        if (!parameters.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.ParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);
    }

    private static bool IsRetryable(Exception exception) =>
        exception is TimeoutException or IOException ||
        exception is NpgsqlException { IsTransient: true };

    private static string BoundFailure(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message)
            ? "Report execution failed."
            : message.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static IReadOnlyDictionary<string, string?> DeserializeStringDictionary(
        string json) =>
        JsonSerializer.Deserialize<SortedDictionary<string, string?>>(
            json,
            JsonOptions) ??
        new SortedDictionary<string, string?>(StringComparer.Ordinal);

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

    private sealed record ClaimedReportRun(
        string RunId,
        string RequestedBy,
        string ReportFamily,
        string RowPolicy,
        IReadOnlyDictionary<string, string?> Parameters,
        DateOnly AsOfDate,
        string ScopeSnapshot,
        int? ScopeFacilityId,
        string DatasetId,
        string DatasetVersion,
        int RetentionDays,
        bool ActiveRevisionAvailable,
        bool SourceSnapshotAvailable);

    private sealed record CancelSnapshot(
        DateTimeOffset RequestedAt,
        string RequestedBy,
        string Reason);

    private sealed record FailureSnapshot(
        int AttemptCount,
        int MaxAttempts,
        DateTimeOffset? QueueExpiresAt,
        CancelSnapshot? Cancellation);

    private sealed class WorkerCancellationState
    {
        private int value;

        public WorkerCancellationCause Value =>
            (WorkerCancellationCause)Volatile.Read(ref value);

        public void TrySet(WorkerCancellationCause cause) =>
            Interlocked.CompareExchange(ref value, (int)cause, 0);
    }

    private enum WorkerCancellationCause
    {
        None,
        Requested,
        LeaseLost
    }

    private enum LeaseRenewalOutcome
    {
        Active,
        CancellationRequested,
        LeaseLost
    }
}
