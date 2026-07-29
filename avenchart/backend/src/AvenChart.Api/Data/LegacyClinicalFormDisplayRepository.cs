using System.Globalization;
using System.Text.Json;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class LegacyClinicalFormDisplayRepository(NpgsqlDataSource dataSource)
{
    public const int ListLimit = 100;

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
              m.created_at
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

        var contractJson = manifestReader.GetString(12);
        var storedManifestHash = manifestReader.GetString(14);
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
                           manifestReader.GetString(13))
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
            manifestReader.GetString(11),
            contractDocument.RootElement.Clone(),
            blockers,
            storedManifestHash,
            manifestReader.GetBoolean(15),
            manifestReader.GetBoolean(16),
            manifestReader.IsDBNull(17) ? null : manifestReader.GetString(17),
            manifestReader.IsDBNull(18)
                ? null
                : Iso(manifestReader.GetFieldValue<DateTimeOffset>(18)),
            manifestReader.IsDBNull(19) ? null : manifestReader.GetString(19),
            manifestReader.IsDBNull(20)
                ? null
                : Iso(manifestReader.GetFieldValue<DateTimeOffset>(20)),
            manifestReader.IsDBNull(21) ? null : manifestReader.GetString(21),
            Iso(manifestReader.GetFieldValue<DateTimeOffset>(22)));
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
        return new(manifest, canonicalPatientId, reconciliation);
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

        if (!string.Equals(
                row.StableKey,
                "legacy.clinicnote",
                StringComparison.Ordinal)
            || !string.Equals(
                row.AdapterRevision,
                "local-legacy-clinic-note-display-v1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Legacy clinical form snapshot {row.SnapshotId} has no supported display adapter.");
        }

        var schema = ClinicalFormRuntime.DeserializeSchema(row.SchemaJson);
        var targetFields = schema.Fields.ToDictionary(
            field => field.Key,
            StringComparer.Ordinal);
        var values = ClinicalFormRuntime.DeserializeValues(row.RawValuesJson);
        var fields = new List<LegacyClinicalFormDisplayField>();
        var unmapped = new List<LegacyClinicalFormUnmappedFact>();

        AddTextField("history", "history");
        AddTextField("examination", "examination");
        AddTextField("plan", "plan");
        AddFollowUpField();
        AddTextField("followup_timing", "follow_up_timing");

        var expectedSourceFields = new HashSet<string>(
            ["history", "examination", "plan", "followup_required", "followup_timing"],
            StringComparer.Ordinal);
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
