using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReportExecutionRepository(
    NpgsqlDataSource dataSource,
    ReportRepository reportRepository)
{
    private const string ExecutionRevision = "local-report-execution-v1";
    private const string DefinitionRevision = "local-report-definition-v1";
    private const int MaximumDateSpanDays = 366;
    private const int MaximumRows = 5000;
    private const int PreviewRows = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex IdempotencyKeyPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._:-]{7,99}$",
        RegexOptions.CultureInvariant);
    private static readonly string[] RunStates =
        ["queued", "running", "completed", "failed", "cancelled", "expired"];
    private static readonly string[] ExecutableRowPolicies = ["practice-wide"];
    private static readonly string[] DeliveryModes = ["local-download"];

    public async Task<GovernedReportExecutionPolicy> GetPolicyAsync(
        CancellationToken cancellationToken)
    {
        var watermark = await GetWatermarkAsync(cancellationToken);
        return new(
            ExecutionRevision,
            DefinitionRevision,
            watermark.DatasetId,
            watermark.DatasetVersion,
            watermark.BaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            RunStates,
            ExecutableRowPolicies,
            DeliveryModes,
            MaximumDateSpanDays,
            MaximumRows,
            PreviewRows,
            ExternalDeliveryEnabled: false,
            ArtifactStorageProductionApproved: false,
            ProductionBlockers:
            [
                "Facility-scoped and patient-assigned definitions fail closed until authoritative staff scope and coverage policy are approved.",
                "Only the exact synthetic dataset as-of date is executable; historical snapshots and source-version time travel are not available.",
                "The local database artifact is not approved encrypted production object storage and has no legal-hold or retention-disposition service.",
                "Only requesting-user or report-owner recipients and local download are supported; schedules and external delivery remain disabled.",
                "Metric owners have not approved the curated family semantics, validation fixtures, or cross-revision equivalence.",
                "Production identity, purpose-of-use claims, disclosure authority, monitoring, recovery, performance, and accountable acceptance remain open."
            ]);
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

        EnsureExecutableRowPolicy(context.Definition.RowPolicy);
        var csv = await reportRepository.GetFamilyCsvAsync(
            context.Definition.ReportFamily,
            context.From,
            context.To,
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
        await CreateQueuedRunAsync(
            runId,
            context,
            actor,
            idempotencyKey,
            fingerprint,
            cancellationToken);

        if (!ExecutableRowPolicies.Contains(
                context.Definition.RowPolicy,
                StringComparer.Ordinal))
        {
            await FailRunAsync(
                runId,
                actor,
                "scope-policy-unavailable",
                $"Row policy '{context.Definition.RowPolicy}' is not executable until authoritative staff scope is approved.",
                cancellationToken);
            return await GetRunAsync(runId, actor, cancellationToken);
        }

        await MarkRunningAsync(runId, actor, cancellationToken);
        try
        {
            var started = DateTimeOffset.UtcNow;
            var csv = await reportRepository.GetFamilyCsvAsync(
                context.Definition.ReportFamily,
                context.From,
                context.To,
                cancellationToken);
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
            await CompleteRunAsync(
                runId,
                actor,
                csv,
                rowCount,
                bytes,
                checksum,
                duration,
                finished,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await FailRunAsync(
                runId,
                actor,
                "execution-failed",
                BoundFailure(exception.Message),
                cancellationToken);
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
            runs.Add(ReadRun(reader, replay: false));
            total = reader.GetInt32(reader.GetOrdinal("total_count"));
        }

        return new(runs, page, pageSize, total);
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

        var events = new List<GovernedReportRunEvent>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, run_id, action, from_status, to_status,
                   actor_username, reason, occurred_at, details
            from saved_report_run_events
            where run_id = @run
            order by occurred_at, event_id;
            """;
        command.Parameters.AddWithValue("run", normalizedRunId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

        return new(run, events);
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

        return new(
            definition,
            watermark,
            normalizedActor,
            normalizedPurpose,
            normalizedRecipient,
            normalizedDelivery,
            normalizedParameters,
            from,
            to);
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
              result_summary, artifact_content_type, artifact_file_name)
            values (
              @run, @definition, @revision, @revisionNumber, @at, @actor,
              'csv', 0, 'queued', @purpose, @recipient, @rowPolicy,
              @parameters, @asOf, @dataset, @datasetVersion, @executionRevision,
              @watermark, @definitionChecksum, @fingerprint, @idempotency,
              '{}'::jsonb, 'text/csv; charset=utf-8', @fileName);
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
                ["recipient"] = context.RecipientUsername,
                ["asOfDate"] = context.Watermark.BaseDate.ToString("yyyy-MM-dd")
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkRunningAsync(
        string runId,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update saved_report_runs
            set status = 'running', started_at = now()
            where run_id = @run and status = 'queued';
            """;
        command.Parameters.AddWithValue("run", runId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The queued report could not start.");
        }
        await InsertEventAsync(
            connection,
            transaction,
            runId,
            "started",
            "queued",
            "running",
            actor,
            "Synchronous local report execution started.",
            new Dictionary<string, object?>(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CompleteRunAsync(
        string runId,
        string actor,
        string csv,
        int rowCount,
        int bytes,
        string checksum,
        int durationMs,
        DateTimeOffset finished,
        CancellationToken cancellationToken)
    {
        var summary = JsonSerializer.Serialize(
            new { rowCount, bytes, checksum },
            JsonOptions);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update saved_report_runs
            set status = 'completed',
                finished_at = @finished,
                duration_ms = @duration,
                row_count = @rows,
                result_checksum = @checksum,
                result_summary = @summary,
                artifact_content = @artifact,
                failure_code = null,
                failure_message = null
            where run_id = @run and status = 'running';

            update saved_report_definitions definition
            set last_run_at = @finished,
                run_count = definition.run_count + 1
            from saved_report_runs run
            where run.run_id = @run
              and definition.id = run.definition_id;
            """;
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("finished", finished);
        command.Parameters.AddWithValue("duration", durationMs);
        command.Parameters.AddWithValue("rows", rowCount);
        command.Parameters.AddWithValue("checksum", checksum);
        command.Parameters.Add("summary", NpgsqlDbType.Jsonb).Value = summary;
        command.Parameters.AddWithValue("artifact", csv);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await InsertEventAsync(
            connection,
            transaction,
            runId,
            "completed",
            "running",
            "completed",
            actor,
            "Report artifact completed with reproducibility evidence.",
            new Dictionary<string, object?>
            {
                ["rowCount"] = rowCount,
                ["bytes"] = bytes,
                ["checksum"] = checksum,
                ["durationMs"] = durationMs
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
                finished_at = @finished,
                duration_ms = case
                  when started_at is null then 0
                  else greatest(0, floor(extract(epoch from (@finished - started_at)) * 1000)::integer)
                end,
                failure_code = @code,
                failure_message = @message,
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
            ? ReadRun(reader, replay: false)
            : null;
    }

    private static GovernedReportRunItem ReadRun(
        NpgsqlDataReader reader,
        bool replay)
    {
        var parameters = DeserializeStringDictionary(
            reader.GetString(reader.GetOrdinal("normalized_parameters")));
        var status = reader.GetString(reader.GetOrdinal("status"));
        var artifactContent = reader.IsDBNull(reader.GetOrdinal("artifact_content"))
            ? null
            : reader.GetString(reader.GetOrdinal("artifact_content"));
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
            reader.GetString(reader.GetOrdinal("ran_by")),
            reader.GetString(reader.GetOrdinal("recipient_username")),
            reader.GetString(reader.GetOrdinal("purpose")),
            reader.GetString(reader.GetOrdinal("row_policy")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("as_of_date"))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            parameters,
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("dataset_version")),
            reader.GetString(reader.GetOrdinal("execution_revision")),
            reader.GetString(reader.GetOrdinal("definition_snapshot_checksum")),
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
            reader.IsDBNull(reader.GetOrdinal("failure_code"))
                ? null
                : reader.GetString(reader.GetOrdinal("failure_code")),
            reader.IsDBNull(reader.GetOrdinal("failure_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("failure_message")),
            status == "completed" && artifactContent is not null,
            replay);
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
                canonicalParameters));
    }

    private static void EnsureExecutableRowPolicy(string rowPolicy)
    {
        if (!ExecutableRowPolicies.Contains(rowPolicy, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Row policy '{rowPolicy}' fails closed because authoritative staff scope is not available.");
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
          coalesce(run.definition_snapshot_checksum, '') as definition_snapshot_checksum,
          run.ran_at,
          run.started_at,
          run.finished_at,
          run.duration_ms,
          run.row_count,
          run.result_checksum,
          run.artifact_content,
          run.artifact_content_type,
          run.artifact_file_name,
          run.failure_code,
          run.failure_message,
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
        DateOnly? To);
}
