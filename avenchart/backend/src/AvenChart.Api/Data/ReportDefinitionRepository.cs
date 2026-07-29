using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReportDefinitionRepository(NpgsqlDataSource dataSource)
{
    private const string PolicyRevision = "local-report-definition-v2";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex StableKeyPattern = new(
        "^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ExecutableContentPattern = new(
        @"(<script\b|</script>|\{\{|\}\}|\$\{|;\s*--|\bselect\s+.+\s+from\b|\binsert\s+into\b|\bupdate\s+.+\s+set\b|\bdelete\s+from\b|\bdrop\s+(table|database)\b|\bexec(?:ute)?\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly string[] States =
        ["draft", "reviewed", "approved", "active", "suspended", "retired"];
    private static readonly string[] Sensitivities =
        ["internal", "confidential", "restricted"];
    private static readonly string[] RowPolicies =
        ["practice-wide", "facility-scoped", "patient-assigned"];
    private static readonly string[] RecipientPolicies =
        ["requesting-user", "report-owner"];
    private static readonly string[] DeliveryModes = ["local-download"];

    private static readonly IReadOnlyList<GovernedReportFamily> Families =
    [
        BuildFamily(
            "operational",
            "Operational snapshot",
            "Practice counts and operational activity summary.",
            false,
            "operational-aggregate",
            "Curated practice, patient, scheduling, clinical, billing, document, and message aggregates.",
            ["section", "name", "metric", "value"]),
        BuildFamily(
            "patients",
            "Patient list",
            "Registered patient identity and contact summary.",
            false,
            "patients",
            "Current unmerged patient registry.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "appointments",
            "Appointments",
            "Bounded scheduled-appointment activity.",
            true,
            "appointments",
            "Appointments joined to the patient registry.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "encounters",
            "Encounters",
            "Bounded clinical-encounter activity.",
            true,
            "encounters",
            "Encounters joined to the patient registry.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "referrals",
            "Referrals",
            "Bounded local referral lifecycle activity.",
            true,
            "referrals",
            "Referral records joined to the patient registry.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "chart-tracker",
            "Chart tracker",
            "Bounded chart-location handoff activity.",
            true,
            "chart-tracker-events",
            "Chart tracker events joined to patients and recording staff.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "inventory",
            "Inventory transactions",
            "Bounded immutable inventory transaction activity.",
            true,
            "inventory-transactions",
            "Inventory transactions joined to lots and items.",
            ["identifier", "subject", "date", "detail"]),
        BuildFamily(
            "clinical-forms",
            "Clinical form fields",
            "Bounded signed and amended clinical-form field facts with pinned revision evidence.",
            true,
            "clinical-form-instances",
            "Signed and amended clinical form instances joined to patients, encounters, and their pinned form revisions.",
            [
                "instance_id",
                "patient_id",
                "encounter_id",
                "form_stable_key",
                "form_name",
                "form_revision",
                "schema_hash",
                "renderer_revision",
                "instance_state",
                "instance_version",
                "content_hash",
                "clinical_date",
                "recorded_at",
                "field_path",
                "field_key",
                "field_label",
                "field_type",
                "report_column",
                "code_system",
                "unit",
                "value"
            ])
    ];

    public ReportDefinitionGovernancePolicy GetPolicy() =>
        new(
            PolicyRevision,
            RawSqlAccepted: false,
            ExecutableTemplatesAccepted: false,
            ExternalDeliveryEnabled: false,
            RowPolicyExecutionEnforced: true,
            States,
            Sensitivities,
            RowPolicies,
            RecipientPolicies,
            DeliveryModes,
            MinimumRetentionDays: 1,
            MaximumRetentionDays: 3650,
            Families,
            ProductionBlockers:
            [
                "Report owners must approve metric terminology, permitted purpose, sensitivity, row policy, and retention.",
                "REP-02 local execution enforces practice, facility, and supported patient relationships; accountable production scope, delegation, revocation, and minimum-necessary policy remain unapproved.",
                "The direct family CSV path is compatibility-only and does not produce governed definition, scope, artifact, or download evidence.",
                "Production artifact storage, encryption, key management, retention deletion, legal hold, backup, and recovery are not approved.",
                "Schedules, recipient groups, retries, escalation, and external delivery remain disabled pending REP-03 and REP-05.",
                "Metric validation fixtures require accountable data-owner review against accepted synthetic scenarios.",
                "Threat, privacy, performance, accessibility, and representative-user evidence remain release gates.",
                "The marker cleanup endpoint is test-only and must not ship in a production profile."
            ]);

    public async Task<GovernedReportDefinitionListResponse> ListAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        bool catalogOnly,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();
        if (normalizedSearch?.Length > 120)
        {
            throw new ArgumentException("Search must be 120 characters or fewer.");
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not null && !States.Contains(normalizedStatus, StringComparer.Ordinal))
        {
            throw new ArgumentException("Status must be a supported report-definition state.");
        }

        if (catalogOnly && normalizedStatus is not null && normalizedStatus != "active")
        {
            throw new ArgumentException("The accessible catalog exposes active definitions only.");
        }

        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 50);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = catalogOnly
            ? """
                select
                  d.id,
                  d.stable_key,
                  d.governance_version,
                  d.latest_revision_id,
                  latest.revision_number,
                  active.title,
                  active.owner_username,
                  active.report_family,
                  active.sensitivity,
                  active.row_policy,
                  active.retention_days,
                  active.status,
                  active.version,
                  active.revision_number,
                  active.updated_at,
                  active.updated_by,
                  false,
                  count(*) over()::integer
                from saved_report_definitions d
                join saved_report_definition_revisions active
                  on active.revision_id = d.active_revision_id
                 and active.status = 'active'
                join saved_report_definition_revisions latest
                  on latest.revision_id = d.latest_revision_id
                where (@search is null
                       or d.stable_key ilike '%' || @search || '%'
                       or active.title ilike '%' || @search || '%'
                       or active.owner_username ilike '%' || @search || '%')
                order by active.title, d.stable_key
                offset @offset limit @limit;
                """
            : """
                select
                  d.id,
                  d.stable_key,
                  d.governance_version,
                  d.latest_revision_id,
                  latest.revision_number,
                  latest.title,
                  latest.owner_username,
                  latest.report_family,
                  latest.sensitivity,
                  latest.row_policy,
                  latest.retention_days,
                  latest.status,
                  latest.version,
                  active.revision_number,
                  latest.updated_at,
                  latest.updated_by,
                  (latest.sensitivity = 'unknown'
                   or latest.row_policy = 'owner-review-required'
                   or latest.retention_days is null),
                  count(*) over()::integer
                from saved_report_definitions d
                join saved_report_definition_revisions latest
                  on latest.revision_id = d.latest_revision_id
                left join saved_report_definition_revisions active
                  on active.revision_id = d.active_revision_id
                 and active.status = 'active'
                where (@search is null
                       or d.stable_key ilike '%' || @search || '%'
                       or latest.title ilike '%' || @search || '%'
                       or latest.owner_username ilike '%' || @search || '%')
                  and (@status is null or latest.status = @status)
                order by latest.updated_at desc, d.stable_key
                offset @offset limit @limit;
                """;
        command.Parameters.Add("search", NpgsqlDbType.Text).Value =
            (object?)normalizedSearch ?? DBNull.Value;
        if (!catalogOnly)
        {
            command.Parameters.Add("status", NpgsqlDbType.Text).Value =
                (object?)normalizedStatus ?? DBNull.Value;
        }
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        var definitions = new List<GovernedReportDefinitionSummary>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            definitions.Add(ReadSummary(reader));
            total = reader.GetInt32(17);
        }

        return new(definitions, page, pageSize, total);
    }

    public async Task<GovernedReportDefinitionDetail?> GetDetailAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var header = await GetDefinitionHeaderAsync(
            connection,
            null,
            definitionId,
            lockRow: false,
            cancellationToken);
        if (header is null)
        {
            return null;
        }

        var revisions = new List<GovernedReportDefinitionRevision>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  revision_id,
                  definition_id,
                  revision_number,
                  title,
                  owner_username,
                  purpose,
                  report_family,
                  metric_dictionary::text,
                  parameter_schema::text,
                  source_datasets::text,
                  output_schema::text,
                  sensitivity,
                  row_policy,
                  retention_days,
                  allowed_recipients::text,
                  delivery_modes::text,
                  validation_fixture::text,
                  status,
                  version,
                  predecessor_revision_id,
                  created_at,
                  created_by,
                  updated_at,
                  updated_by,
                  effective_from,
                  effective_to
                from saved_report_definition_revisions
                where definition_id = @definition
                order by revision_number desc;
                """;
            command.Parameters.AddWithValue("definition", definitionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                revisions.Add(ReadRevision(reader));
            }
        }

        var events = new List<GovernedReportDefinitionEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  event_id,
                  definition_id,
                  revision_id,
                  revision_number,
                  action,
                  from_status,
                  to_status,
                  reason,
                  actor_username,
                  occurred_at,
                  snapshot_checksum
                from saved_report_definition_events
                where definition_id = @definition
                order by occurred_at desc, event_id desc;
                """;
            command.Parameters.AddWithValue("definition", definitionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    FormatInstant(reader, 9),
                    reader.GetString(10)));
            }
        }

        return new(
            header.DefinitionId,
            header.StableKey,
            header.GovernanceVersion,
            header.LatestRevisionId,
            header.ActiveRevisionId,
            revisions,
            events);
    }

    public async Task<GovernedReportDefinitionDetail> CreateAsync(
        GovernedReportDefinitionCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        RejectAdditionalProperties(request.AdditionalProperties);
        var normalized = Normalize(
            request.Title,
            request.OwnerUsername,
            request.Purpose,
            request.ReportFamily,
            request.Sensitivity,
            request.RowPolicy,
            request.RetentionDays,
            request.AllowedRecipients,
            request.DeliveryModes);
        var stableKey = NormalizeStableKey(request.StableKey);
        var reason = RequiredReason(request.Reason);
        var definitionId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RequireActiveOwnerAsync(
            connection,
            transaction,
            normalized.OwnerUsername,
            cancellationToken);

        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction;
            duplicate.CommandText =
                "select exists(select 1 from saved_report_definitions where stable_key = @key);";
            duplicate.Parameters.AddWithValue("key", stableKey);
            if (await duplicate.ExecuteScalarAsync(cancellationToken) is true)
            {
                throw new ReportDefinitionConflictException(
                    "A report definition with that stable key already exists.");
            }
        }

        await using (var definition = connection.CreateCommand())
        {
            definition.Transaction = transaction;
            definition.CommandText = """
                insert into saved_report_definitions (
                  id,
                  name,
                  report_type,
                  schedule,
                  active,
                  created_by,
                  created_at,
                  run_count,
                  stable_key,
                  latest_revision_id,
                  active_revision_id,
                  governance_version,
                  legacy_active_before_governance
                )
                values (
                  @definition,
                  @title,
                  @family,
                  'manual',
                  false,
                  @user,
                  @now,
                  0,
                  @key,
                  @revision,
                  null,
                  1,
                  null
                );
                """;
            definition.Parameters.AddWithValue("definition", definitionId);
            definition.Parameters.AddWithValue("title", normalized.Title);
            definition.Parameters.AddWithValue("family", normalized.Family.Key);
            definition.Parameters.AddWithValue("user", username);
            definition.Parameters.AddWithValue("now", now);
            definition.Parameters.AddWithValue("key", stableKey);
            definition.Parameters.AddWithValue("revision", revisionId);
            await definition.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertRevisionAsync(
            connection,
            transaction,
            definitionId,
            revisionId,
            revisionNumber: 1,
            predecessorRevisionId: null,
            normalized,
            status: "draft",
            now,
            username,
            cancellationToken);
        await WriteEventAsync(
            connection,
            transaction,
            definitionId,
            revisionId,
            revisionNumber: 1,
            action: "created",
            fromStatus: null,
            toStatus: "draft",
            reason,
            username,
            normalized,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetDetailAsync(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("The created report definition could not be loaded.");
    }

    public async Task<GovernedReportDefinitionDetail> CreateRevisionAsync(
        Guid definitionId,
        GovernedReportRevisionCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        RejectAdditionalProperties(request.AdditionalProperties);
        var normalized = Normalize(
            request.Title,
            request.OwnerUsername,
            request.Purpose,
            request.ReportFamily,
            request.Sensitivity,
            request.RowPolicy,
            request.RetentionDays,
            request.AllowedRecipients,
            request.DeliveryModes);
        var reason = RequiredReason(request.Reason);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var header = await GetDefinitionHeaderAsync(
            connection,
            transaction,
            definitionId,
            lockRow: true,
            cancellationToken)
            ?? throw new ArgumentException("The report definition was not found.");
        var latest = await GetRevisionAsync(
            connection,
            transaction,
            header.LatestRevisionId,
            lockRow: true,
            cancellationToken)
            ?? throw new InvalidOperationException("The latest report revision is missing.");

        if (request.ExpectedLatestRevisionNumber != latest.RevisionNumber)
        {
            throw new ReportDefinitionConflictException(
                $"The report definition changed after it was loaded. Latest revision is {latest.RevisionNumber}.",
                latest.Version,
                latest.Status);
        }
        var legacyReviewRequired = IsLegacyReviewRequired(latest);
        if ((latest.Status is "draft" or "reviewed" or "approved") &&
            !legacyReviewRequired)
        {
            throw new ReportDefinitionConflictException(
                "Finish or retire the open revision before creating a successor.",
                latest.Version,
                latest.Status);
        }
        if (latest.Status == "retired")
        {
            throw new ReportDefinitionConflictException(
                "A retired report definition cannot receive a successor revision.",
                latest.Version,
                latest.Status);
        }

        await RequireActiveOwnerAsync(
            connection,
            transaction,
            normalized.OwnerUsername,
            cancellationToken);
        var revisionId = Guid.NewGuid();
        var revisionNumber = latest.RevisionNumber + 1;
        await InsertRevisionAsync(
            connection,
            transaction,
            definitionId,
            revisionId,
            revisionNumber,
            latest.RevisionId,
            normalized,
            status: "draft",
            now,
            username,
            cancellationToken);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update saved_report_definitions
                set latest_revision_id = @revision,
                    governance_version = governance_version + 1
                where id = @definition;
                """;
            update.Parameters.AddWithValue("revision", revisionId);
            update.Parameters.AddWithValue("definition", definitionId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteEventAsync(
            connection,
            transaction,
            definitionId,
            revisionId,
            revisionNumber,
            "revision-created",
            null,
            "draft",
            reason,
            username,
            normalized,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("The revised report definition could not be loaded.");
    }

    public async Task<GovernedReportDefinitionDetail> TransitionAsync(
        Guid definitionId,
        string action,
        GovernedReportTransitionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        action = action.Trim().ToLowerInvariant();
        RejectAdditionalProperties(request.AdditionalProperties);
        var reason = RequiredReason(request.Reason);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var header = await GetDefinitionHeaderAsync(
            connection,
            transaction,
            definitionId,
            lockRow: true,
            cancellationToken)
            ?? throw new ArgumentException("The report definition was not found.");
        var revision = await GetRevisionAsync(
            connection,
            transaction,
            header.LatestRevisionId,
            lockRow: true,
            cancellationToken)
            ?? throw new InvalidOperationException("The latest report revision is missing.");

        if (request.ExpectedVersion != revision.Version)
        {
            throw new ReportDefinitionConflictException(
                $"The report revision changed after it was loaded. Current version is {revision.Version}.",
                revision.Version,
                revision.Status);
        }

        if (IsLegacyReviewRequired(revision) && action != "retire")
        {
            throw new ArgumentException(
                "The migrated legacy draft has unknown governance facts. Create a complete replacement revision or retire it.");
        }

        var nextStatus = GetNextStatus(action, revision.Status);
        if (action == "review" &&
            !string.Equals(revision.OwnerUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only the named report owner can complete owner review.");
        }

        if (action == "activate" &&
            header.ActiveRevisionId is Guid activeRevisionId &&
            activeRevisionId != revision.RevisionId)
        {
            var priorActive = await GetRevisionAsync(
                connection,
                transaction,
                activeRevisionId,
                lockRow: true,
                cancellationToken);
            if (priorActive is not null && priorActive.Status == "active")
            {
                await UpdateRevisionStatusAsync(
                    connection,
                    transaction,
                    priorActive.RevisionId,
                    "suspended",
                    username,
                    now,
                    setEffectiveFrom: false,
                    setEffectiveTo: true,
                    cancellationToken);
                await WriteEventAsync(
                    connection,
                    transaction,
                    definitionId,
                    priorActive.RevisionId,
                    priorActive.RevisionNumber,
                    "superseded",
                    "active",
                    "suspended",
                    $"Superseded by revision {revision.RevisionNumber}: {reason}",
                    username,
                    priorActive.Definition,
                    cancellationToken);
            }
        }

        await UpdateRevisionStatusAsync(
            connection,
            transaction,
            revision.RevisionId,
            nextStatus,
            username,
            now,
            setEffectiveFrom: nextStatus == "active",
            setEffectiveTo: nextStatus is "suspended" or "retired",
            cancellationToken);

        await using (var updateDefinition = connection.CreateCommand())
        {
            updateDefinition.Transaction = transaction;
            updateDefinition.CommandText = action switch
            {
                "activate" => """
                    update saved_report_definitions
                    set active_revision_id = @revision,
                        active = true,
                        name = @title,
                        report_type = @family,
                        schedule = 'manual',
                        governance_version = governance_version + 1
                    where id = @definition;
                    """,
                "suspend" => """
                    update saved_report_definitions
                    set active = false,
                        governance_version = governance_version + 1
                    where id = @definition;
                    """,
                "retire" => """
                    update saved_report_definitions
                    set active_revision_id = case
                          when active_revision_id = @revision then null
                          else active_revision_id
                        end,
                        active = case
                          when active_revision_id = @revision then false
                          else active
                        end,
                        governance_version = governance_version + 1
                    where id = @definition;
                    """,
                _ => """
                    update saved_report_definitions
                    set governance_version = governance_version + 1
                    where id = @definition;
                    """
            };
            updateDefinition.Parameters.AddWithValue("definition", definitionId);
            updateDefinition.Parameters.AddWithValue("revision", revision.RevisionId);
            updateDefinition.Parameters.AddWithValue("title", revision.Definition.Title);
            updateDefinition.Parameters.AddWithValue("family", revision.Definition.Family.Key);
            await updateDefinition.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteEventAsync(
            connection,
            transaction,
            definitionId,
            revision.RevisionId,
            revision.RevisionNumber,
            action,
            revision.Status,
            nextStatus,
            reason,
            username,
            revision.Definition,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(definitionId, cancellationToken)
            ?? throw new InvalidOperationException("The transitioned report definition could not be loaded.");
    }

    public async Task<bool> DeleteTestFixtureAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText =
            "select stable_key from saved_report_definitions where id = @definition for update;";
        check.Parameters.AddWithValue("definition", definitionId);
        var stableKey = await check.ExecuteScalarAsync(cancellationToken) as string;
        if (stableKey is null)
        {
            return false;
        }
        if (!stableKey.StartsWith("tmp-report-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only TMP-REPORT-* report-definition fixtures can be deleted.");
        }

        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = """
            delete from saved_report_runs where definition_id = @definition;
            delete from saved_report_definition_events where definition_id = @definition;
            update saved_report_definitions
            set latest_revision_id = null,
                active_revision_id = null
            where id = @definition;
            delete from saved_report_definition_revisions where definition_id = @definition;
            delete from saved_report_definitions where id = @definition;
            """;
        delete.Parameters.AddWithValue("definition", definitionId);
        await delete.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static GovernedReportFamily BuildFamily(
        string key,
        string name,
        string purpose,
        bool supportsDateRange,
        string datasetKey,
        string datasetDescription,
        IReadOnlyList<string> fields)
    {
        var metrics = fields
            .Select(field => new ReportMetricDefinition(
                field,
                Humanize(field),
                $"Curated {Humanize(field).ToLowerInvariant()} value for the {name} family.",
                "value",
                $"{datasetKey}.{field}"))
            .ToArray();
        var parameters = supportsDateRange
            ? new[]
            {
                new ReportParameterDefinition("from", "From date", "date", false, 366),
                new ReportParameterDefinition("to", "To date", "date", false, 366)
            }
            : [];
        return new(
            key,
            name,
            purpose,
            metrics,
            parameters,
            [new(datasetKey, datasetDescription, fields)],
            fields.Select(field => new ReportOutputFieldDefinition(
                    field,
                    Humanize(field),
                    field == "value" ? "decimal-or-text" : "string",
                    "restricted"))
                .ToArray(),
            new(
                "gold-legacy-ehr-synthetic",
                $"rep-01:{key}",
                fields,
                ExpectedRowCount: null));
    }

    private static string Humanize(string value)
    {
        var normalized = value.Replace("-", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal);
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static GovernedReportDefinitionSummary ReadSummary(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetGuid(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetInt32(10),
            reader.GetString(11),
            reader.GetInt32(12),
            reader.IsDBNull(13) ? null : reader.GetInt32(13),
            FormatInstant(reader, 14),
            reader.GetString(15),
            reader.GetBoolean(16));

    private static GovernedReportDefinitionRevision ReadRevision(NpgsqlDataReader reader)
    {
        var sensitivity = reader.GetString(11);
        var rowPolicy = reader.GetString(12);
        int? retentionDays = reader.IsDBNull(13) ? null : reader.GetInt32(13);
        return new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            Deserialize<IReadOnlyList<ReportMetricDefinition>>(reader.GetString(7)),
            Deserialize<IReadOnlyList<ReportParameterDefinition>>(reader.GetString(8)),
            Deserialize<IReadOnlyList<ReportSourceDatasetDefinition>>(reader.GetString(9)),
            Deserialize<IReadOnlyList<ReportOutputFieldDefinition>>(reader.GetString(10)),
            sensitivity,
            rowPolicy,
            retentionDays,
            Deserialize<IReadOnlyList<string>>(reader.GetString(14)),
            Deserialize<IReadOnlyList<string>>(reader.GetString(15)),
            Deserialize<ReportValidationFixture>(reader.GetString(16)),
            reader.GetString(17),
            reader.GetInt32(18),
            reader.IsDBNull(19) ? null : reader.GetGuid(19),
            FormatInstant(reader, 20),
            reader.GetString(21),
            FormatInstant(reader, 22),
            reader.GetString(23),
            reader.IsDBNull(24) ? null : FormatInstant(reader, 24),
            reader.IsDBNull(25) ? null : FormatInstant(reader, 25),
            sensitivity == "unknown" || rowPolicy == "owner-review-required" || retentionDays is null);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored report-definition JSON is invalid.");

    private static string FormatInstant(NpgsqlDataReader reader, int ordinal) =>
        reader.GetFieldValue<DateTimeOffset>(ordinal)
            .ToString("O", CultureInfo.InvariantCulture);

    private static async Task<DefinitionHeader?> GetDefinitionHeaderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid definitionId,
        bool lockRow,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              id,
              stable_key,
              governance_version,
              latest_revision_id,
              active_revision_id
            from saved_report_definitions
            where id = @definition
            """ + (lockRow ? " for update;" : ";");
        command.Parameters.AddWithValue("definition", definitionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        if (reader.IsDBNull(3))
        {
            throw new InvalidOperationException("The report definition has no latest revision.");
        }
        return new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4));
    }

    private static async Task<LockedRevision?> GetRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid revisionId,
        bool lockRow,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              revision_id,
              definition_id,
              revision_number,
              title,
              owner_username,
              purpose,
              report_family,
              metric_dictionary::text,
              parameter_schema::text,
              source_datasets::text,
              output_schema::text,
              sensitivity,
              row_policy,
              retention_days,
              allowed_recipients::text,
              delivery_modes::text,
              validation_fixture::text,
              status,
              version
            from saved_report_definition_revisions
            where revision_id = @revision
            """ + (lockRow ? " for update;" : ";");
        command.Parameters.AddWithValue("revision", revisionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var family = new GovernedReportFamily(
            reader.GetString(6),
            Families.Single(item => item.Key == reader.GetString(6)).Name,
            reader.GetString(5),
            Deserialize<IReadOnlyList<ReportMetricDefinition>>(reader.GetString(7)),
            Deserialize<IReadOnlyList<ReportParameterDefinition>>(reader.GetString(8)),
            Deserialize<IReadOnlyList<ReportSourceDatasetDefinition>>(reader.GetString(9)),
            Deserialize<IReadOnlyList<ReportOutputFieldDefinition>>(reader.GetString(10)),
            Deserialize<ReportValidationFixture>(reader.GetString(16)));
        var definition = new NormalizedDefinition(
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            family,
            reader.GetString(11),
            reader.GetString(12),
            reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
            Deserialize<IReadOnlyList<string>>(reader.GetString(14)),
            Deserialize<IReadOnlyList<string>>(reader.GetString(15)));
        return new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            definition,
            reader.GetString(17),
            reader.GetInt32(18));
    }

    private static async Task RequireActiveOwnerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string ownerUsername,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              exists(
                select 1
                from staff
                where username = @owner
                  and active
              )
              or exists(
                select 1
                from auth_accounts
                where username = @owner
                  and active
              );
            """;
        command.Parameters.AddWithValue("owner", ownerUsername);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new ArgumentException(
                "Report owner must be an active local staff or authenticated account username.");
        }
    }

    private static async Task InsertRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        Guid revisionId,
        int revisionNumber,
        Guid? predecessorRevisionId,
        NormalizedDefinition definition,
        string status,
        DateTimeOffset now,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into saved_report_definition_revisions (
              revision_id,
              definition_id,
              revision_number,
              title,
              owner_username,
              purpose,
              report_family,
              metric_dictionary,
              parameter_schema,
              source_datasets,
              output_schema,
              sensitivity,
              row_policy,
              retention_days,
              allowed_recipients,
              delivery_modes,
              validation_fixture,
              status,
              version,
              predecessor_revision_id,
              created_at,
              created_by,
              updated_at,
              updated_by
            )
            values (
              @revision,
              @definition,
              @number,
              @title,
              @owner,
              @purpose,
              @family,
              @metrics,
              @parameters,
              @sources,
              @outputs,
              @sensitivity,
              @rowPolicy,
              @retention,
              @recipients,
              @deliveryModes,
              @fixture,
              @status,
              0,
              @predecessor,
              @now,
              @user,
              @now,
              @user
            );
            """;
        command.Parameters.AddWithValue("revision", revisionId);
        command.Parameters.AddWithValue("definition", definitionId);
        command.Parameters.AddWithValue("number", revisionNumber);
        command.Parameters.AddWithValue("title", definition.Title);
        command.Parameters.AddWithValue("owner", definition.OwnerUsername);
        command.Parameters.AddWithValue("purpose", definition.Purpose);
        command.Parameters.AddWithValue("family", definition.Family.Key);
        AddJson(command, "metrics", definition.Family.MetricDictionary);
        AddJson(command, "parameters", definition.Family.ParameterSchema);
        AddJson(command, "sources", definition.Family.SourceDatasets);
        AddJson(command, "outputs", definition.Family.OutputSchema);
        command.Parameters.AddWithValue("sensitivity", definition.Sensitivity);
        command.Parameters.AddWithValue("rowPolicy", definition.RowPolicy);
        command.Parameters.AddWithValue("retention", definition.RetentionDays);
        AddJson(command, "recipients", definition.AllowedRecipients);
        AddJson(command, "deliveryModes", definition.DeliveryModes);
        AddJson(command, "fixture", definition.Family.ValidationFixture);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("predecessor", (object?)predecessorRevisionId ?? DBNull.Value);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("user", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddJson(NpgsqlCommand command, string name, object value) =>
        command.Parameters.AddWithValue(
            name,
            NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(value, JsonOptions));

    private static async Task UpdateRevisionStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid revisionId,
        string nextStatus,
        string username,
        DateTimeOffset now,
        bool setEffectiveFrom,
        bool setEffectiveTo,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update saved_report_definition_revisions
            set status = @status,
                version = version + 1,
                updated_at = @now,
                updated_by = @user,
                effective_from = case
                  when @setFrom then coalesce(effective_from, @now)
                  else effective_from
                end,
                effective_to = case
                  when @setTo then @now
                  when @status = 'active' then null
                  else effective_to
                end
            where revision_id = @revision;
            """;
        command.Parameters.AddWithValue("status", nextStatus);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("user", username);
        command.Parameters.AddWithValue("setFrom", setEffectiveFrom);
        command.Parameters.AddWithValue("setTo", setEffectiveTo);
        command.Parameters.AddWithValue("revision", revisionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        Guid revisionId,
        int revisionNumber,
        string action,
        string? fromStatus,
        string toStatus,
        string reason,
        string username,
        NormalizedDefinition definition,
        CancellationToken cancellationToken)
    {
        var snapshotChecksum = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(
                            new
                            {
                                definitionId,
                                revisionId,
                                revisionNumber,
                                definition,
                                toStatus
                            },
                            JsonOptions))))
            .ToLowerInvariant();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into saved_report_definition_events (
              event_id,
              definition_id,
              revision_id,
              revision_number,
              action,
              from_status,
              to_status,
              reason,
              actor_username,
              occurred_at,
              snapshot_checksum
            )
            values (
              @event,
              @definition,
              @revision,
              @number,
              @action,
              @fromStatus,
              @toStatus,
              @reason,
              @user,
              clock_timestamp(),
              @checksum
            );
            """;
        command.Parameters.AddWithValue("event", Guid.NewGuid());
        command.Parameters.AddWithValue("definition", definitionId);
        command.Parameters.AddWithValue("revision", revisionId);
        command.Parameters.AddWithValue("number", revisionNumber);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("fromStatus", (object?)fromStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("toStatus", toStatus);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("user", username);
        command.Parameters.AddWithValue("checksum", snapshotChecksum);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GetNextStatus(string action, string currentStatus) =>
        (action, currentStatus) switch
        {
            ("review", "draft") => "reviewed",
            ("approve", "reviewed") => "approved",
            ("activate", "approved") => "active",
            ("activate", "suspended") => "active",
            ("suspend", "active") => "suspended",
            ("retire", "draft" or "reviewed" or "approved" or "active" or "suspended") =>
                "retired",
            _ when !new[] { "review", "approve", "activate", "suspend", "retire" }
                .Contains(action, StringComparer.Ordinal) =>
                throw new ArgumentException("Unsupported report-definition action."),
            _ => throw new ReportDefinitionConflictException(
                $"A {currentStatus} report revision cannot perform {action}.",
                currentStatus: currentStatus)
        };

    private static bool IsLegacyReviewRequired(LockedRevision revision) =>
        revision.Definition.Sensitivity == "unknown" ||
        revision.Definition.RowPolicy == "owner-review-required" ||
        revision.Definition.RetentionDays == 0;

    private static NormalizedDefinition Normalize(
        string? title,
        string? ownerUsername,
        string? purpose,
        string? reportFamily,
        string? sensitivity,
        string? rowPolicy,
        int retentionDays,
        IReadOnlyList<string>? allowedRecipients,
        IReadOnlyList<string>? deliveryModes)
    {
        var normalizedTitle = RequiredText(
            title,
            "Report title is required and must be 3 to 120 characters.",
            3,
            120);
        var normalizedOwner = RequiredText(
                ownerUsername,
                "Report owner username is required and must be 80 characters or fewer.",
                1,
                80)
            .ToLowerInvariant();
        var normalizedPurpose = RequiredText(
            purpose,
            "Report purpose is required and must be 20 to 500 characters.",
            20,
            500);
        RejectExecutableContent(normalizedTitle);
        RejectExecutableContent(normalizedPurpose);

        var normalizedFamily = reportFamily?.Trim().ToLowerInvariant();
        var family = Families.SingleOrDefault(item => item.Key == normalizedFamily)
            ?? throw new ArgumentException(
                "Report family must be a supported governed family.");
        var normalizedSensitivity = NormalizeChoice(
            sensitivity,
            Sensitivities,
            "sensitivity");
        var normalizedRowPolicy = NormalizeChoice(
            rowPolicy,
            RowPolicies,
            "row policy");
        if (retentionDays is < 1 or > 3650)
        {
            throw new ArgumentException(
                "Retention must be between 1 and 3650 days.");
        }

        var recipients = NormalizeChoices(
            allowedRecipients,
            RecipientPolicies,
            "recipient policy");
        var modes = NormalizeChoices(
            deliveryModes,
            DeliveryModes,
            "delivery mode");
        return new(
            normalizedTitle,
            normalizedOwner,
            normalizedPurpose,
            family,
            normalizedSensitivity,
            normalizedRowPolicy,
            retentionDays,
            recipients,
            modes);
    }

    private static string NormalizeStableKey(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is null ||
            normalized.Length is < 3 or > 80 ||
            !StableKeyPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Stable key must be 3 to 80 lowercase letters, numbers, dots, dashes, or underscores and start with a letter.");
        }
        return normalized;
    }

    private static string RequiredReason(string? value) =>
        RequiredText(
            value,
            "A governance reason is required and must be 10 to 500 characters.",
            10,
            500);

    private static string RequiredText(
        string? value,
        string message,
        int minimum,
        int maximum)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length < minimum ||
            normalized.Length > maximum)
        {
            throw new ArgumentException(message);
        }
        return normalized;
    }

    private static string NormalizeChoice(
        string? value,
        IReadOnlyList<string> choices,
        string label)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is not null &&
               choices.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : throw new ArgumentException($"Select a supported {label}.");
    }

    private static IReadOnlyList<string> NormalizeChoices(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> choices,
        string label)
    {
        var normalized = (values ?? [])
            .Select(value => value?.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0 ||
            normalized.Any(value => !choices.Contains(value, StringComparer.Ordinal)))
        {
            throw new ArgumentException($"Select at least one supported {label}.");
        }
        return normalized;
    }

    private static void RejectExecutableContent(string value)
    {
        if (ExecutableContentPattern.IsMatch(value))
        {
            throw new ArgumentException(
                "Raw SQL and executable template content are prohibited in report definitions.");
        }
    }

    private static void RejectAdditionalProperties(
        IDictionary<string, JsonElement>? properties)
    {
        if (properties is { Count: > 0 })
        {
            throw new ArgumentException(
                $"Unknown report-definition fields are prohibited: {string.Join(", ", properties.Keys.OrderBy(key => key, StringComparer.Ordinal))}.");
        }
    }

    private sealed record DefinitionHeader(
        Guid DefinitionId,
        string StableKey,
        int GovernanceVersion,
        Guid LatestRevisionId,
        Guid? ActiveRevisionId);

    private sealed record NormalizedDefinition(
        string Title,
        string OwnerUsername,
        string Purpose,
        GovernedReportFamily Family,
        string Sensitivity,
        string RowPolicy,
        int RetentionDays,
        IReadOnlyList<string> AllowedRecipients,
        IReadOnlyList<string> DeliveryModes);

    private sealed record LockedRevision(
        Guid RevisionId,
        Guid DefinitionId,
        int RevisionNumber,
        NormalizedDefinition Definition,
        string Status,
        int Version)
    {
        public string OwnerUsername => Definition.OwnerUsername;
    }
}
