using System.Globalization;
using System.Text.Json;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class LegacyClinicalFormDisplayRepository(NpgsqlDataSource dataSource)
{
    public const int ListLimit = 100;
    public static readonly Guid ClinicNoteManifestId =
        Guid.Parse("90f00000-0000-4000-a000-000000000001");

    public async Task<LegacyClinicalFormSnapshotListResponse> ListAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalPatientId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
              s.snapshot_id,
              s.source_system,
              s.source_baseline_version,
              s.extraction_revision,
              s.source_schema,
              s.source_table,
              s.source_row_id,
              s.source_revision,
              s.source_form_key,
              s.patient_id,
              s.encounter_id,
              s.source_active,
              s.source_recorded_at,
              s.captured_at,
              s.adapter_revision,
              s.target_definition_revision,
              s.raw_values::text,
              s.raw_sha256,
              d.definition_id,
              r.schema_json::text,
              r.schema_hash,
              r.renderer_version,
              count(*) over()::integer
            from legacy_clinical_form_snapshots s
            join clinical_form_definitions d
              on d.stable_key = s.source_form_key
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = s.target_definition_revision
            where s.patient_id = @patientId
            order by s.source_recorded_at desc nulls last, s.snapshot_id
            limit {ListLimit};
            """;
        command.Parameters.AddWithValue("patientId", canonicalPatientId);

        var snapshots = new List<LegacyClinicalFormSnapshotSummary>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            total = reader.GetInt32(22);
            snapshots.Add(BuildDetail(row).Snapshot);
        }

        return new(snapshots, total, snapshots.Count, ListLimit);
    }

    public async Task<LegacyClinicalFormSnapshotDetailResponse?> GetAsync(
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              s.snapshot_id,
              s.source_system,
              s.source_baseline_version,
              s.extraction_revision,
              s.source_schema,
              s.source_table,
              s.source_row_id,
              s.source_revision,
              s.source_form_key,
              s.patient_id,
              s.encounter_id,
              s.source_active,
              s.source_recorded_at,
              s.captured_at,
              s.adapter_revision,
              s.target_definition_revision,
              s.raw_values::text,
              s.raw_sha256,
              d.definition_id,
              r.schema_json::text,
              r.schema_hash,
              r.renderer_version
            from legacy_clinical_form_snapshots s
            join clinical_form_definitions d
              on d.stable_key = s.source_form_key
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = s.target_definition_revision
            where s.snapshot_id = @snapshotId;
            """;
        command.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? BuildDetail(ReadRow(reader))
            : null;
    }

    public async Task<LegacyClinicalFormMigrationManifestResponse?> GetMigrationManifestAsync(
        string patientId,
        string stableKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
        {
            throw new ArgumentException("Form stable key is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalPatientId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken);
        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = """
            select
              m.manifest_id,
              m.stable_key,
              m.source_system,
              m.source_baseline_version,
              m.extraction_revision,
              m.source_schema,
              m.source_table,
              m.target_definition_revision,
              r.schema_hash,
              r.renderer_version,
              m.manifest_revision,
              m.version,
              m.status,
              m.contract_json::text,
              m.blockers_json::text,
              m.manifest_sha256,
              m.production_approved,
              m.execution_enabled,
              m.reviewed_by,
              m.reviewed_at,
              m.approved_by,
              m.approved_at,
              m.decision_reason,
              m.created_at,
              m.updated_at,
              m.updated_by
            from clinical_form_migration_manifests m
            join clinical_form_definitions d
              on d.stable_key = m.stable_key
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = m.target_definition_revision
            where m.stable_key = @stableKey
            order by m.manifest_revision desc
            limit 1;
            """;
        manifestCommand.Parameters.AddWithValue("stableKey", stableKey.Trim());
        await using var manifestReader =
            await manifestCommand.ExecuteReaderAsync(cancellationToken);
        if (!await manifestReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var contractJson = manifestReader.GetString(13);
        var storedManifestHash = manifestReader.GetString(15);
        if (!string.Equals(
                ClinicalFormRuntime.Hash(contractJson),
                storedManifestHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Clinical form migration manifest {manifestReader.GetGuid(0)} failed its stored SHA-256 check.");
        }

        using var contractDocument = JsonDocument.Parse(contractJson);
        var blockers = JsonSerializer.Deserialize<List<string>>(
                           manifestReader.GetString(14))
                       ?? [];
        var manifest = new LegacyClinicalFormMigrationManifest(
            manifestReader.GetGuid(0),
            manifestReader.GetString(1),
            manifestReader.GetString(2),
            manifestReader.GetString(3),
            manifestReader.GetString(4),
            manifestReader.GetString(5),
            manifestReader.GetString(6),
            manifestReader.GetInt32(7),
            manifestReader.GetString(8),
            manifestReader.GetString(9),
            manifestReader.GetInt32(10),
            manifestReader.GetInt32(11),
            manifestReader.GetString(12),
            contractDocument.RootElement.Clone(),
            blockers,
            storedManifestHash,
            manifestReader.GetBoolean(16),
            manifestReader.GetBoolean(17),
            manifestReader.IsDBNull(18) ? null : manifestReader.GetString(18),
            manifestReader.IsDBNull(19)
                ? null
                : Iso(manifestReader.GetFieldValue<DateTimeOffset>(19)),
            manifestReader.IsDBNull(20) ? null : manifestReader.GetString(20),
            manifestReader.IsDBNull(21)
                ? null
                : Iso(manifestReader.GetFieldValue<DateTimeOffset>(21)),
            manifestReader.IsDBNull(22) ? null : manifestReader.GetString(22),
            Iso(manifestReader.GetFieldValue<DateTimeOffset>(23)),
            Iso(manifestReader.GetFieldValue<DateTimeOffset>(24)),
            manifestReader.GetString(25));
        await manifestReader.DisposeAsync();

        await using var snapshotCommand = connection.CreateCommand();
        snapshotCommand.CommandText = $"""
            select
              s.snapshot_id,
              s.source_system,
              s.source_baseline_version,
              s.extraction_revision,
              s.source_schema,
              s.source_table,
              s.source_row_id,
              s.source_revision,
              s.source_form_key,
              s.patient_id,
              s.encounter_id,
              s.source_active,
              s.source_recorded_at,
              s.captured_at,
              s.adapter_revision,
              s.target_definition_revision,
              s.raw_values::text,
              s.raw_sha256,
              d.definition_id,
              r.schema_json::text,
              r.schema_hash,
              r.renderer_version
            from legacy_clinical_form_snapshots s
            join clinical_form_definitions d
              on d.stable_key = s.source_form_key
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = s.target_definition_revision
            where s.patient_id = @patientId
              and s.source_form_key = @stableKey
            order by s.source_recorded_at, s.snapshot_id
            limit {ListLimit};
            """;
        snapshotCommand.Parameters.AddWithValue("patientId", canonicalPatientId);
        snapshotCommand.Parameters.AddWithValue("stableKey", stableKey.Trim());
        var details = new List<LegacyClinicalFormSnapshotDetailResponse>();
        await using var snapshotReader =
            await snapshotCommand.ExecuteReaderAsync(cancellationToken);
        while (await snapshotReader.ReadAsync(cancellationToken))
        {
            details.Add(BuildDetail(ReadRow(snapshotReader)));
        }
        await snapshotReader.DisposeAsync();

        var rows = details.Select(detail =>
        {
            var reasons = new List<string>();
            if (!detail.Snapshot.SourceActive)
            {
                reasons.Add("The source row is inactive.");
            }

            if (detail.UnmappedFacts.Count > 0)
            {
                reasons.Add(
                    $"{detail.UnmappedFacts.Count} source fact(s) are unmapped.");
            }

            return new LegacyClinicalFormMigrationRowDisposition(
                detail.Snapshot.SnapshotId,
                detail.Snapshot.SourceRowId,
                detail.Snapshot.SourceActive,
                detail.UnmappedFacts.Count,
                reasons.Count == 0 ? "eligible-for-review" : "blocked",
                reasons);
        }).ToList();
        var sourceDigest = ClinicalFormRuntime.Hash(string.Join(
            "\n",
            details
                .OrderBy(detail => detail.Snapshot.SnapshotId)
                .Select(detail =>
                    $"{detail.Snapshot.SnapshotId:D}:{detail.Snapshot.RawSha256}")));
        var reconciliation = new LegacyClinicalFormMigrationReconciliation(
            details.Count,
            details.Count(detail => detail.Snapshot.SourceActive),
            details.Count(detail => !detail.Snapshot.SourceActive),
            details.Count(detail => detail.UnmappedFacts.Count == 0),
            details.Count(detail => detail.UnmappedFacts.Count > 0),
            rows.Count(row => row.Disposition == "eligible-for-review"),
            rows.Count(row => row.Disposition == "blocked"),
            GovernedInstancesCreated: 0,
            sourceDigest,
            rows);
        var events = await GetManifestEventsAsync(
            connection,
            manifest.ManifestId,
            cancellationToken);
        return new(
            manifest,
            canonicalPatientId,
            reconciliation,
            events,
            AllowedActions: []);
    }

    public async Task<LegacyClinicalFormMigrationManifestDecisionResponse?>
        TransitionMigrationManifestAsync(
            Guid manifestId,
            string action,
            LegacyClinicalFormMigrationManifestDecisionRequest request,
            string actor,
            CancellationToken cancellationToken)
    {
        if (request.ExtraFields is { Count: > 0 })
        {
            throw new ArgumentException(
                $"Unsupported request field(s): {string.Join(", ", request.ExtraFields.Keys.OrderBy(key => key, StringComparer.Ordinal))}.");
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        if (normalizedAction is not ("review" or "approve" or "reject"))
        {
            throw new ArgumentException("Unsupported migration manifest decision.");
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 500)
        {
            throw new ArgumentException(
                "Decision reason must be between 10 and 500 characters.");
        }

        var normalizedActor = actor.Trim();
        if (normalizedActor.Length == 0)
        {
            throw new ArgumentException("Decision actor is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        int currentVersion;
        string currentStatus;
        string? reviewedBy;
        bool productionApproved;
        bool executionEnabled;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select
                  version,
                  status,
                  reviewed_by,
                  production_approved,
                  execution_enabled
                from clinical_form_migration_manifests
                where manifest_id = @manifestId
                for update;
                """;
            select.Parameters.AddWithValue("manifestId", manifestId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            currentVersion = reader.GetInt32(0);
            currentStatus = reader.GetString(1);
            reviewedBy = reader.IsDBNull(2) ? null : reader.GetString(2);
            productionApproved = reader.GetBoolean(3);
            executionEnabled = reader.GetBoolean(4);
        }

        if (productionApproved || executionEnabled)
        {
            throw new InvalidOperationException(
                "The local manifest governance route cannot operate on a production-approved or execution-enabled manifest.");
        }

        if (request.ExpectedVersion != currentVersion)
        {
            throw new LegacyClinicalFormMigrationManifestConflictException(
                $"The migration manifest changed after it was loaded. Current version is {currentVersion}.",
                currentVersion,
                currentStatus);
        }

        var expectedStatus = normalizedAction == "review" ? "draft" : "in-review";
        if (!string.Equals(currentStatus, expectedStatus, StringComparison.Ordinal))
        {
            throw new LegacyClinicalFormMigrationManifestConflictException(
                $"The migration manifest is {currentStatus}; it cannot move through {normalizedAction}.",
                currentVersion,
                currentStatus);
        }

        if (normalizedAction is "approve" or "reject"
            && string.Equals(
                reviewedBy,
                normalizedActor,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new LegacyClinicalFormMigrationManifestConflictException(
                "The manifest reviewer cannot approve or reject their own review.",
                currentVersion,
                currentStatus);
        }

        var nextStatus = normalizedAction switch
        {
            "review" => "in-review",
            "approve" => "locally-approved",
            "reject" => "rejected",
            _ => throw new InvalidOperationException()
        };
        var nextVersion = currentVersion + 1;
        var occurredAt = DateTimeOffset.UtcNow;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_migration_manifests
                set status = @nextStatus,
                    version = @nextVersion,
                    reviewed_by = case
                      when @action = 'review' then @actor
                      else reviewed_by
                    end,
                    reviewed_at = case
                      when @action = 'review' then @occurredAt
                      else reviewed_at
                    end,
                    approved_by = case
                      when @action = 'approve' then @actor
                      when @action = 'reject' then null
                      else approved_by
                    end,
                    approved_at = case
                      when @action = 'approve' then @occurredAt
                      when @action = 'reject' then null
                      else approved_at
                    end,
                    decision_reason = @reason,
                    updated_at = @occurredAt,
                    updated_by = @actor
                where manifest_id = @manifestId;
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("action", normalizedAction);
            update.Parameters.AddWithValue("actor", normalizedActor);
            update.Parameters.AddWithValue("occurredAt", occurredAt);
            update.Parameters.AddWithValue("reason", reason);
            update.Parameters.AddWithValue("manifestId", manifestId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        long eventId;
        DateTimeOffset storedOccurredAt;
        string snapshotSha256;
        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.Transaction = transaction;
            insertEvent.CommandText = """
                insert into clinical_form_migration_manifest_events (
                  manifest_id,
                  version,
                  action,
                  from_status,
                  to_status,
                  actor,
                  reason,
                  occurred_at,
                  snapshot_sha256
                )
                select
                  @manifestId,
                  @version,
                  @action,
                  @fromStatus,
                  @toStatus,
                  @actor,
                  @reason,
                  @occurredAt,
                  encode(sha256(convert_to(
                    jsonb_build_object(
                      'manifestId', @manifestId,
                      'version', @version,
                      'action', @action,
                      'fromStatus', @fromStatus,
                      'toStatus', @toStatus,
                      'actor', @actor,
                      'reason', @reason,
                      'occurredAt', @occurredAt
                    )::text,
                    'utf8'
                  )), 'hex')
                returning event_id, occurred_at, snapshot_sha256;
                """;
            insertEvent.Parameters.AddWithValue("manifestId", manifestId);
            insertEvent.Parameters.AddWithValue("version", nextVersion);
            insertEvent.Parameters.AddWithValue("action", normalizedAction);
            insertEvent.Parameters.AddWithValue("fromStatus", currentStatus);
            insertEvent.Parameters.AddWithValue("toStatus", nextStatus);
            insertEvent.Parameters.AddWithValue("actor", normalizedActor);
            insertEvent.Parameters.AddWithValue("reason", reason);
            insertEvent.Parameters.AddWithValue("occurredAt", occurredAt);
            await using var reader =
                await insertEvent.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "The migration manifest decision event was not recorded.");
            }

            eventId = reader.GetInt64(0);
            storedOccurredAt = reader.GetFieldValue<DateTimeOffset>(1);
            snapshotSha256 = reader.GetString(2);
        }

        await transaction.CommitAsync(cancellationToken);
        var decision = new LegacyClinicalFormMigrationManifestEvent(
            eventId,
            nextVersion,
            normalizedAction,
            currentStatus,
            nextStatus,
            normalizedActor,
            reason,
            Iso(storedOccurredAt),
            snapshotSha256);
        return new(
            manifestId,
            nextVersion,
            nextStatus,
            ProductionApproved: false,
            ExecutionEnabled: false,
            decision);
    }

    public async Task<bool> ResetMigrationManifestTestFixtureAsync(
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        if (manifestId != ClinicNoteManifestId)
        {
            throw new ArgumentException(
                "Only the deterministic Clinic Note manifest fixture can be reset.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var deleteEvents = connection.CreateCommand())
        {
            deleteEvents.Transaction = transaction;
            deleteEvents.CommandText = """
                delete from clinical_form_migration_manifest_events
                where manifest_id = @manifestId
                  and version > 1;
                """;
            deleteEvents.Parameters.AddWithValue("manifestId", manifestId);
            await deleteEvents.ExecuteNonQueryAsync(cancellationToken);
        }

        int updated;
        await using (var reset = connection.CreateCommand())
        {
            reset.Transaction = transaction;
            reset.CommandText = """
                update clinical_form_migration_manifests
                set status = 'draft',
                    version = 1,
                    reviewed_by = null,
                    reviewed_at = null,
                    approved_by = null,
                    approved_at = null,
                    decision_reason = null,
                    updated_at = created_at,
                    updated_by = 'seed'
                where manifest_id = @manifestId
                  and production_approved = false
                  and execution_enabled = false;
                """;
            reset.Parameters.AddWithValue("manifestId", manifestId);
            updated = await reset.ExecuteNonQueryAsync(cancellationToken);
        }

        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<IReadOnlyList<LegacyClinicalFormMigrationManifestEvent>>
        GetManifestEventsAsync(
            NpgsqlConnection connection,
            Guid manifestId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              e.event_id,
              e.version,
              e.action,
              e.from_status,
              e.to_status,
              e.actor,
              e.reason,
              e.occurred_at,
              e.snapshot_sha256,
              e.snapshot_sha256 = encode(sha256(convert_to(
                jsonb_build_object(
                  'manifestId', e.manifest_id,
                  'version', e.version,
                  'action', e.action,
                  'fromStatus', e.from_status,
                  'toStatus', e.to_status,
                  'actor', e.actor,
                  'reason', e.reason,
                  'occurredAt', e.occurred_at
                )::text,
                'utf8'
              )), 'hex') as hash_valid
            from clinical_form_migration_manifest_events e
            where e.manifest_id = @manifestId
            order by e.version;
            """;
        command.Parameters.AddWithValue("manifestId", manifestId);
        var events = new List<LegacyClinicalFormMigrationManifestEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.GetBoolean(9))
            {
                throw new InvalidOperationException(
                    $"Clinical form migration manifest event {reader.GetInt64(0)} failed its stored SHA-256 check.");
            }

            events.Add(new(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                Iso(reader.GetFieldValue<DateTimeOffset>(7)),
                reader.GetString(8)));
        }

        return events;
    }

    private static LegacyClinicalFormSnapshotDetailResponse BuildDetail(
        SnapshotRow row)
    {
        if (!string.Equals(
                ClinicalFormRuntime.Hash(row.RawValuesJson),
                row.RawSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Legacy clinical form snapshot {row.SnapshotId} failed its stored SHA-256 check.");
        }

        var schema = ClinicalFormRuntime.DeserializeSchema(row.SchemaJson);
        var targetFields = schema.Fields.ToDictionary(
            field => field.Key,
            StringComparer.Ordinal);
        var values = ClinicalFormRuntime.DeserializeValues(row.RawValuesJson);
        var fields = new List<LegacyClinicalFormDisplayField>();
        var unmapped = new List<LegacyClinicalFormUnmappedFact>();
        HashSet<string> expectedSourceFields;
        if (string.Equals(
                row.StableKey,
                "legacy.clinicnote",
                StringComparison.Ordinal)
            && string.Equals(
                row.AdapterRevision,
                "local-legacy-clinic-note-display-v1",
                StringComparison.Ordinal))
        {
            AddTextField("history", "history");
            AddTextField("examination", "examination");
            AddTextField("plan", "plan");
            AddFollowUpField();
            AddTextField("followup_timing", "follow_up_timing");
            expectedSourceFields = new(
                [
                    "history",
                    "examination",
                    "plan",
                    "followup_required",
                    "followup_timing"
                ],
                StringComparer.Ordinal);
        }
        else if (string.Equals(
                     row.StableKey,
                     "legacy.clinicalinstructions",
                     StringComparison.Ordinal)
                 && string.Equals(
                     row.AdapterRevision,
                     "local-legacy-clinical-instructions-display-v1",
                     StringComparison.Ordinal))
        {
            AddTextField("instruction", "instruction");
            expectedSourceFields = new(
                ["instruction"],
                StringComparer.Ordinal);
        }
        else
        {
            throw new InvalidOperationException(
                $"Legacy clinical form snapshot {row.SnapshotId} has no supported display adapter.");
        }

        foreach (var extra in values
                     .Where(pair => !expectedSourceFields.Contains(pair.Key))
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var reason = "The source field is not mapped by this adapter revision.";
            fields.Add(new(
                extra.Key,
                null,
                extra.Key,
                extra.Value,
                DisplayRaw(extra.Value),
                "unmapped",
                reason));
            unmapped.Add(new(extra.Key, extra.Value, reason));
        }

        var summary = new LegacyClinicalFormSnapshotSummary(
            row.SnapshotId,
            row.SourceSystem,
            row.SourceBaselineVersion,
            row.ExtractionRevision,
            row.SourceTable,
            row.SourceRowId,
            row.SourceRevision,
            row.StableKey,
            schema.Name,
            row.PatientId,
            row.EncounterId,
            row.SourceActive,
            row.SourceRecordedAt is null ? null : Iso(row.SourceRecordedAt.Value),
            Iso(row.CapturedAt),
            row.RawSha256,
            row.AdapterRevision,
            row.TargetDefinitionRevision,
            row.SchemaHash,
            unmapped.Count,
            ReadOnly: true,
            Converted: false);
        return new(
            summary,
            row.SourceSchema,
            row.DefinitionId,
            row.RendererVersion,
            values,
            fields,
            unmapped,
            MigrationApproved: false,
            GovernedInstanceId: null);

        void AddTextField(string sourceKey, string targetKey)
        {
            var target = targetFields[targetKey];
            if (!values.TryGetValue(sourceKey, out var sourceValue))
            {
                AddMissing(sourceKey, targetKey, target.Label);
                return;
            }

            fields.Add(new(
                sourceKey,
                targetKey,
                target.Label,
                sourceValue,
                DisplayRaw(sourceValue),
                "exact",
                "The source value is displayed without transformation."));
        }

        void AddFollowUpField()
        {
            const string sourceKey = "followup_required";
            const string targetKey = "follow_up_status";
            var target = targetFields[targetKey];
            if (!values.TryGetValue(sourceKey, out var sourceValue))
            {
                AddMissing(sourceKey, targetKey, target.Label);
                return;
            }

            var legacyCode = sourceValue.ValueKind == JsonValueKind.Number
                             && sourceValue.TryGetInt32(out var numericCode)
                ? numericCode.ToString(CultureInfo.InvariantCulture)
                : sourceValue.ValueKind == JsonValueKind.String
                    ? sourceValue.GetString()
                    : null;
            var mapping = legacyCode switch
            {
                "0" => ("none_required", "None required"),
                "1" => ("required_in", "Required in"),
                "2" => ("pending_investigation", "Pending investigation"),
                _ => ((string Code, string Display)?)null
            };
            if (mapping is not null)
            {
                fields.Add(new(
                    sourceKey,
                    targetKey,
                    target.Label,
                    sourceValue,
                    mapping.Value.Display,
                    "normalized",
                    $"Legacy code {legacyCode} maps to target option {mapping.Value.Code}."));
                return;
            }

            var reason =
                $"Legacy follow-up code {DisplayRaw(sourceValue)} is not mapped by this adapter revision.";
            fields.Add(new(
                sourceKey,
                targetKey,
                target.Label,
                sourceValue,
                DisplayRaw(sourceValue),
                "unmapped",
                reason));
            unmapped.Add(new(sourceKey, sourceValue, reason));
        }

        void AddMissing(string sourceKey, string targetKey, string label)
        {
            var sourceValue = JsonSerializer.SerializeToElement<object?>(null);
            const string reason = "The expected source field is absent from the captured snapshot.";
            fields.Add(new(
                sourceKey,
                targetKey,
                label,
                sourceValue,
                "Not present",
                "unmapped",
                reason));
            unmapped.Add(new(sourceKey, sourceValue, reason));
        }
    }

    private static string DisplayRaw(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => "Not recorded",
            JsonValueKind.String when string.IsNullOrWhiteSpace(value.GetString()) =>
                "Not recorded",
            JsonValueKind.String => value.GetString() ?? "Not recorded",
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            _ => value.GetRawText()
        };

    private static SnapshotRow ReadRow(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetInt32(10),
            reader.GetBoolean(11),
            reader.IsDBNull(12)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetFieldValue<DateTimeOffset>(13),
            reader.GetString(14),
            reader.GetInt32(15),
            reader.GetString(16),
            reader.GetString(17),
            reader.GetGuid(18),
            reader.GetString(19),
            reader.GetString(20),
            reader.GetString(21));

    private static async Task<string> ResolvePatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient is required.");
        }

        await using var command = connection.CreateCommand();
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

    private static string Iso(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private sealed record SnapshotRow(
        Guid SnapshotId,
        string SourceSystem,
        string SourceBaselineVersion,
        string ExtractionRevision,
        string SourceSchema,
        string SourceTable,
        string SourceRowId,
        string SourceRevision,
        string StableKey,
        string PatientId,
        int EncounterId,
        bool SourceActive,
        DateTimeOffset? SourceRecordedAt,
        DateTimeOffset CapturedAt,
        string AdapterRevision,
        int TargetDefinitionRevision,
        string RawValuesJson,
        string RawSha256,
        Guid DefinitionId,
        string SchemaJson,
        string SchemaHash,
        string RendererVersion);
}
