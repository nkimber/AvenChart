using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ClinicalFormRepository(NpgsqlDataSource dataSource)
{
    public ClinicalFormPolicyResponse GetPolicy() => ClinicalFormRuntime.BuildPolicy();

    public ClinicalFormEvaluationResponse Preview(ClinicalFormPreviewRequest request)
    {
        RejectUnknownFields(request.ExtraFields);
        return ClinicalFormRuntime.Evaluate(request.Definition, request.Values);
    }

    public async Task<ClinicalFormDefinitionListResponse> ListDefinitionsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        bool catalogOnly,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = NormalizeOptionalSearch(search);
        var normalizedStatus = NormalizeStatusFilter(status);
        ValidatePage(page, pageSize);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var where = catalogOnly
            ? """
              d.effective_revision is not null
              and r.status = 'effective'
              and (r.effective_from is null or r.effective_from <= clock_timestamp())
              and (r.effective_to is null or r.effective_to > clock_timestamp())
              """
            : "(@status is null or r.status = @status)";
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"""
            select count(*)
            from clinical_form_definitions d
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = {(catalogOnly ? "d.effective_revision" : "d.latest_revision")}
            where {where}
              and (
                @search is null
                or lower(d.stable_key) like @search
                or lower(r.schema_json->>'name') like @search
                or lower(r.schema_json->>'purpose') like @search
              );
            """;
        AddNullableText(countCommand, "status", normalizedStatus);
        AddNullableText(
            countCommand,
            "search",
            normalizedSearch is null ? null : $"%{normalizedSearch}%");
        var total = Convert.ToInt32(
            await countCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
              d.definition_id,
              d.stable_key,
              d.latest_revision,
              d.effective_revision,
              r.revision,
              r.status,
              r.version,
              r.schema_json,
              r.updated_at,
              r.updated_by
            from clinical_form_definitions d
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = {(catalogOnly ? "d.effective_revision" : "d.latest_revision")}
            where {where}
              and (
                @search is null
                or lower(d.stable_key) like @search
                or lower(r.schema_json->>'name') like @search
                or lower(r.schema_json->>'purpose') like @search
              )
            order by lower(r.schema_json->>'name'), d.stable_key
            offset @offset limit @limit;
            """;
        AddNullableText(command, "status", normalizedStatus);
        AddNullableText(
            command,
            "search",
            normalizedSearch is null ? null : $"%{normalizedSearch}%");
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        var definitions = new List<ClinicalFormDefinitionSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = ClinicalFormRuntime.DeserializeSchema(reader.GetString(7));
            definitions.Add(new(
                reader.GetGuid(0),
                reader.GetString(1),
                schema.Name,
                schema.Purpose,
                schema.ContextScope,
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.GetString(5),
                reader.GetInt32(6),
                schema.SignaturePolicy,
                Iso(reader.GetFieldValue<DateTimeOffset>(8)),
                reader.GetString(9)));
        }

        return new(definitions, total, page, pageSize);
    }

    public Task<ClinicalFormDefinitionListResponse> ListCatalogAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        ListDefinitionsAsync(
            search,
            status: "effective",
            page,
            pageSize,
            catalogOnly: true,
            cancellationToken);

    public async Task<ClinicalFormDefinitionDetailResponse?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await GetDefinitionAsync(connection, definitionId, cancellationToken);
    }

    public async Task<ClinicalFormDefinitionDetailResponse> CreateDefinitionAsync(
        ClinicalFormDefinitionCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var definition = ClinicalFormRuntime.Normalize(request.Definition);
        var reason = NormalizeReason(request.Reason);
        var schemaJson = ClinicalFormRuntime.SerializeSchema(definition);
        var schemaHash = ClinicalFormRuntime.Hash(schemaJson);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var definitionCommand = connection.CreateCommand())
            {
                definitionCommand.Transaction = transaction;
                definitionCommand.CommandText = """
                    insert into clinical_form_definitions (
                      definition_id, stable_key, latest_revision, effective_revision,
                      created_at, created_by, updated_at, updated_by
                    )
                    values (@id, @key, 1, null, @now, @actor, @now, @actor);
                    """;
                definitionCommand.Parameters.AddWithValue("id", id);
                definitionCommand.Parameters.AddWithValue("key", definition.StableKey);
                definitionCommand.Parameters.AddWithValue("now", now);
                definitionCommand.Parameters.AddWithValue("actor", actor);
                await definitionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertRevisionAsync(
                connection,
                transaction,
                id,
                revision: 1,
                status: "draft",
                version: 0,
                definition,
                schemaHash,
                actor,
                predecessorRevision: null,
                now,
                cancellationToken);
            await InsertDefinitionEventAsync(
                connection,
                transaction,
                id,
                revision: 1,
                action: "created",
                fromStatus: null,
                toStatus: "draft",
                actor,
                reason,
                schemaHash,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            throw new ClinicalFormConflictException(
                $"A form definition with stable key {definition.StableKey} already exists.");
        }

        return await GetDefinitionAsync(id, cancellationToken)
               ?? throw new InvalidOperationException("The created form definition was not found.");
    }

    public async Task<ClinicalFormDefinitionDetailResponse> CreateRevisionAsync(
        Guid definitionId,
        ClinicalFormRevisionCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var definition = ClinicalFormRuntime.Normalize(request.Definition);
        var reason = NormalizeReason(request.Reason);
        var schemaJson = ClinicalFormRuntime.SerializeSchema(definition);
        var schemaHash = ClinicalFormRuntime.Hash(schemaJson);
        var now = DateTimeOffset.UtcNow;
        int nextRevision;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                select stable_key, latest_revision
                from clinical_form_definitions
                where definition_id = @id
                for update;
                """;
            lockCommand.Parameters.AddWithValue("id", definitionId);
            await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("Form definition was not found.");
            }

            var stableKey = reader.GetString(0);
            var latestRevision = reader.GetInt32(1);
            if (latestRevision != request.ExpectedLatestRevision)
            {
                throw new ClinicalFormConflictException(
                    $"The form definition changed after it was loaded. Latest revision is {latestRevision}.",
                    currentVersion: latestRevision);
            }

            if (!string.Equals(stableKey, definition.StableKey, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A successor revision cannot change the stable form key.");
            }

            nextRevision = latestRevision + 1;
        }

        await InsertRevisionAsync(
            connection,
            transaction,
            definitionId,
            nextRevision,
            "draft",
            version: 0,
            definition,
            schemaHash,
            actor,
            predecessorRevision: request.ExpectedLatestRevision,
            now,
            cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_definitions
                set latest_revision = @revision,
                    updated_at = @now,
                    updated_by = @actor
                where definition_id = @id;
                """;
            update.Parameters.AddWithValue("id", definitionId);
            update.Parameters.AddWithValue("revision", nextRevision);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("actor", actor);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertDefinitionEventAsync(
            connection,
            transaction,
            definitionId,
            nextRevision,
            "revision-created",
            null,
            "draft",
            actor,
            reason,
            schemaHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDefinitionAsync(definitionId, cancellationToken)
               ?? throw new InvalidOperationException("The revised form definition was not found.");
    }

    public async Task<ClinicalFormDefinitionDetailResponse> TransitionDefinitionAsync(
        Guid definitionId,
        string action,
        ClinicalFormDefinitionTransitionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var normalizedAction = action.Trim().ToLowerInvariant();
        var reason = NormalizeReason(request.Reason);
        var allowed = normalizedAction switch
        {
            "review" => new[] { "draft" },
            "approve" => new[] { "in-review" },
            "reject" => new[] { "in-review" },
            "activate" => new[] { "approved", "suspended" },
            "suspend" => new[] { "effective" },
            "retire" => new[]
            {
                "draft",
                "in-review",
                "approved",
                "effective",
                "suspended",
                "rejected"
            },
            _ => throw new ArgumentException("Unsupported form definition transition.")
        };
        var nextStatus = normalizedAction switch
        {
            "review" => "in-review",
            "approve" => "approved",
            "reject" => "rejected",
            "activate" => "effective",
            "suspend" => "suspended",
            "retire" => "retired",
            _ => throw new InvalidOperationException()
        };

        DateTimeOffset? effectiveFrom = normalizedAction == "activate"
            ? ParseOptionalDateTime(request.EffectiveFrom, "Effective from")
              ?? DateTimeOffset.UtcNow
            : null;
        DateTimeOffset? effectiveTo = normalizedAction == "activate"
            ? ParseOptionalDateTime(request.EffectiveTo, "Effective to")
            : null;
        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            throw new ArgumentException("Effective-to time must be after effective-from time.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        RevisionState current;
        int? priorEffectiveRevision;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                  d.latest_revision,
                  d.effective_revision,
                  r.status,
                  r.version,
                  r.schema_json,
                  r.schema_hash
                from clinical_form_definitions d
                join clinical_form_revisions r
                  on r.definition_id = d.definition_id
                 and r.revision = @revision
                where d.definition_id = @id
                for update of d, r;
                """;
            command.Parameters.AddWithValue("id", definitionId);
            command.Parameters.AddWithValue("revision", request.Revision);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("Form definition revision was not found.");
            }

            var latestRevision = reader.GetInt32(0);
            priorEffectiveRevision = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            current = new(
                request.Revision,
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5));
            if (request.Revision != latestRevision && normalizedAction != "retire")
            {
                throw new ClinicalFormConflictException(
                    $"Only the latest revision may move through {normalizedAction}. Latest revision is {latestRevision}.",
                    currentVersion: current.Version,
                    currentState: current.Status);
            }
        }

        if (current.Version != request.ExpectedVersion)
        {
            throw new ClinicalFormConflictException(
                $"The form revision changed after it was loaded. Current version is {current.Version}.",
                current.Version,
                current.Status);
        }

        if (!allowed.Contains(current.Status, StringComparer.Ordinal))
        {
            throw new ClinicalFormConflictException(
                $"Form revision {request.Revision} is {current.Status}; it cannot move through {normalizedAction}.",
                current.Version,
                current.Status);
        }

        var now = DateTimeOffset.UtcNow;
        if (normalizedAction == "activate"
            && priorEffectiveRevision is not null
            && priorEffectiveRevision != request.Revision)
        {
            await using var supersede = connection.CreateCommand();
            supersede.Transaction = transaction;
            supersede.CommandText = """
                update clinical_form_revisions
                set status = 'superseded',
                    version = version + 1,
                    effective_to = coalesce(effective_to, @now),
                    updated_at = @now,
                    updated_by = @actor
                where definition_id = @id
                  and revision = @revision
                  and status = 'effective'
                returning schema_hash;
                """;
            supersede.Parameters.AddWithValue("id", definitionId);
            supersede.Parameters.AddWithValue("revision", priorEffectiveRevision.Value);
            supersede.Parameters.AddWithValue("now", now);
            supersede.Parameters.AddWithValue("actor", actor);
            var priorHash = await supersede.ExecuteScalarAsync(cancellationToken) as string;
            if (priorHash is not null)
            {
                await InsertDefinitionEventAsync(
                    connection,
                    transaction,
                    definitionId,
                    priorEffectiveRevision.Value,
                    "superseded",
                    "effective",
                    "superseded",
                    actor,
                    reason,
                    priorHash,
                    cancellationToken);
            }
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_revisions
                set status = @status,
                    version = version + 1,
                    reviewed_by = case when @action = 'review' then @actor else reviewed_by end,
                    approved_by = case when @action = 'approve' then @actor else approved_by end,
                    effective_from = case when @action = 'activate' then @effectiveFrom else effective_from end,
                    effective_to = case
                      when @action = 'activate' then @effectiveTo
                      when @action in ('suspend', 'retire') and status = 'effective'
                        then coalesce(effective_to, @now)
                      else effective_to
                    end,
                    updated_at = @now,
                    updated_by = @actor
                where definition_id = @id
                  and revision = @revision;
                """;
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("action", normalizedAction);
            update.Parameters.AddWithValue("actor", actor);
            AddNullableTimestamp(update, "effectiveFrom", effectiveFrom);
            AddNullableTimestamp(update, "effectiveTo", effectiveTo);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("id", definitionId);
            update.Parameters.AddWithValue("revision", request.Revision);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var definitionUpdate = connection.CreateCommand())
        {
            definitionUpdate.Transaction = transaction;
            definitionUpdate.CommandText = """
                update clinical_form_definitions
                set effective_revision = case
                      when @action = 'activate' then @revision
                      when effective_revision = @revision
                        and @action in ('suspend', 'retire') then null
                      else effective_revision
                    end,
                    updated_at = @now,
                    updated_by = @actor
                where definition_id = @id;
                """;
            definitionUpdate.Parameters.AddWithValue("action", normalizedAction);
            definitionUpdate.Parameters.AddWithValue("revision", request.Revision);
            definitionUpdate.Parameters.AddWithValue("now", now);
            definitionUpdate.Parameters.AddWithValue("actor", actor);
            definitionUpdate.Parameters.AddWithValue("id", definitionId);
            await definitionUpdate.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertDefinitionEventAsync(
            connection,
            transaction,
            definitionId,
            request.Revision,
            normalizedAction,
            current.Status,
            nextStatus,
            actor,
            reason,
            current.SchemaHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDefinitionAsync(definitionId, cancellationToken)
               ?? throw new InvalidOperationException("The transitioned form definition was not found.");
    }

    public async Task<ClinicalFormInstanceListResponse> ListInstancesAsync(
        string patientId,
        int? encounterId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalPatientId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              i.instance_id,
              i.definition_id,
              i.definition_revision,
              d.stable_key,
              r.schema_json,
              i.patient_id,
              i.encounter_id,
              i.state,
              i.version,
              i.author,
              i.predecessor_instance_id,
              i.successor_instance_id,
              i.amendment_reason,
              i.created_at,
              i.updated_at,
              i.finalized_at,
              i.signed_at
            from clinical_form_instances i
            join clinical_form_definitions d on d.definition_id = i.definition_id
            join clinical_form_revisions r
              on r.definition_id = i.definition_id
             and r.revision = i.definition_revision
            where i.patient_id = @patientId
              and (@encounterId is null or i.encounter_id = @encounterId)
            order by i.updated_at desc, i.instance_id;
            """;
        command.Parameters.AddWithValue("patientId", canonicalPatientId);
        AddNullableInteger(command, "encounterId", encounterId);
        var instances = new List<ClinicalFormInstanceSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            instances.Add(ReadInstanceSummary(reader));
        }

        return new(instances, instances.Count);
    }

    public async Task<ClinicalFormInstanceDetailResponse> CreateInstanceAsync(
        string patientId,
        ClinicalFormInstanceCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var reason = NormalizeReason(request.Reason);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalPatientId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction);

        if (request.EncounterId is not null)
        {
            await ValidateEncounterAsync(
                connection,
                transaction,
                request.EncounterId.Value,
                canonicalPatientId,
                cancellationToken);
        }

        var revision = await ResolveEffectiveRevisionAsync(
            connection,
            transaction,
            request.DefinitionId,
            request.Revision,
            cancellationToken);
        var definition = ClinicalFormRuntime.DeserializeSchema(revision.SchemaJson);
        if (definition.ContextScope == "encounter" && request.EncounterId is null)
        {
            throw new ArgumentException("This form requires an encounter context.");
        }

        if (definition.ContextScope == "patient" && request.EncounterId is not null)
        {
            throw new ArgumentException("This patient-scoped form cannot be pinned to an encounter.");
        }

        var existing = await GetIdempotentInstanceAsync(
            connection,
            transaction,
            actor,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.DefinitionId != request.DefinitionId
                || existing.DefinitionRevision != revision.Revision
                || existing.PatientId != canonicalPatientId
                || existing.EncounterId != request.EncounterId)
            {
                throw new ClinicalFormConflictException(
                    "The idempotency key was already used for a different form context.");
            }

            await transaction.RollbackAsync(cancellationToken);
            return await GetInstanceAsync(existing.InstanceId, cancellationToken);
        }

        var values = request.Values ?? new Dictionary<string, JsonElement>();
        var evaluation = ClinicalFormRuntime.Evaluate(definition, values);
        var instanceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var snapshotHash = ClinicalFormRuntime.HashInstance(
            instanceId,
            revision.Revision,
            version: 0,
            state: "draft",
            evaluation.Values);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into clinical_form_instances (
                  instance_id, definition_id, definition_revision, patient_id,
                  encounter_id, state, version, author, values_json,
                  validation_json, idempotency_key, created_at, updated_at
                )
                values (
                  @instanceId, @definitionId, @revision, @patientId,
                  @encounterId, 'draft', 0, @actor, @values,
                  @validation, @idempotencyKey, @now, @now
                );
                """;
            insert.Parameters.AddWithValue("instanceId", instanceId);
            insert.Parameters.AddWithValue("definitionId", request.DefinitionId);
            insert.Parameters.AddWithValue("revision", revision.Revision);
            insert.Parameters.AddWithValue("patientId", canonicalPatientId);
            AddNullableInteger(insert, "encounterId", request.EncounterId);
            insert.Parameters.AddWithValue("actor", actor);
            AddJson(insert, "values", ClinicalFormRuntime.SerializeValues(evaluation.Values));
            AddJson(insert, "validation", ClinicalFormRuntime.SerializeEvaluation(evaluation));
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertInstanceEventAsync(
            connection,
            transaction,
            instanceId,
            version: 0,
            action: "created",
            fromState: null,
            toState: "draft",
            actor,
            reason,
            snapshotHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetInstanceAsync(instanceId, cancellationToken);
    }

    public async Task<ClinicalFormInstanceDetailResponse> GetInstanceAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await GetInstanceAsync(connection, instanceId, cancellationToken)
               ?? throw new ArgumentException("Clinical form instance was not found.");
    }

    public async Task<ClinicalFormInstanceDetailResponse> UpdateInstanceAsync(
        Guid instanceId,
        ClinicalFormInstanceUpdateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var reason = NormalizeReason(request.Reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockInstanceAsync(
            connection,
            transaction,
            instanceId,
            cancellationToken);
        EnsureVersion(current, request.ExpectedVersion);
        if (current.State != "draft")
        {
            throw new ClinicalFormConflictException(
                "Only a draft form instance can be edited.",
                current.Version,
                current.State);
        }

        if (current.Author != actor)
        {
            throw new ArgumentException("Only the draft author can edit this form instance.");
        }

        var definition = ClinicalFormRuntime.DeserializeSchema(current.SchemaJson);
        var evaluation = ClinicalFormRuntime.Evaluate(definition, request.Values);
        var nextVersion = current.Version + 1;
        var now = DateTimeOffset.UtcNow;
        var snapshotHash = ClinicalFormRuntime.HashInstance(
            instanceId,
            current.DefinitionRevision,
            nextVersion,
            current.State,
            evaluation.Values);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_instances
                set values_json = @values,
                    validation_json = @validation,
                    version = @version,
                    updated_at = @now
                where instance_id = @id;
                """;
            AddJson(update, "values", ClinicalFormRuntime.SerializeValues(evaluation.Values));
            AddJson(update, "validation", ClinicalFormRuntime.SerializeEvaluation(evaluation));
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("id", instanceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertInstanceEventAsync(
            connection,
            transaction,
            instanceId,
            nextVersion,
            "draft-saved",
            current.State,
            current.State,
            actor,
            reason,
            snapshotHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetInstanceAsync(instanceId, cancellationToken);
    }

    public async Task<ClinicalFormInstanceDetailResponse> FinalizeInstanceAsync(
        Guid instanceId,
        ClinicalFormInstanceTransitionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var reason = NormalizeReason(request.Reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockInstanceAsync(
            connection,
            transaction,
            instanceId,
            cancellationToken);
        EnsureVersion(current, request.ExpectedVersion);
        if (current.State != "draft")
        {
            throw new ClinicalFormConflictException(
                "Only a draft form instance can be finalized.",
                current.Version,
                current.State);
        }

        if (current.Author != actor)
        {
            throw new ArgumentException("Only the draft author can finalize this form instance.");
        }

        var definition = ClinicalFormRuntime.DeserializeSchema(current.SchemaJson);
        var values = ClinicalFormRuntime.DeserializeValues(current.ValuesJson);
        var evaluation = ClinicalFormRuntime.Evaluate(definition, values);
        if (!evaluation.Valid)
        {
            var errors = evaluation.Issues
                .Where(issue => issue.Severity == "error")
                .Select(issue => $"{issue.FieldKey}: {issue.Message}")
                .Take(8);
            throw new ArgumentException(
                $"The form cannot be finalized: {string.Join(" ", errors)}");
        }

        return await MoveInstanceAsync(
            connection,
            transaction,
            current,
            nextState: "ready-for-signature",
            action: "finalized",
            actor,
            reason,
            evaluation,
            finalizedAt: DateTimeOffset.UtcNow,
            signedAt: null,
            cancellationToken);
    }

    public Task<ClinicalFormInstanceDetailResponse> SignInstanceAsync(
        Guid instanceId,
        ClinicalFormInstanceTransitionRequest request,
        string actor,
        CancellationToken cancellationToken) =>
        SignOrCosignAsync(
            instanceId,
            request,
            actor,
            coSign: false,
            cancellationToken);

    public Task<ClinicalFormInstanceDetailResponse> CosignInstanceAsync(
        Guid instanceId,
        ClinicalFormInstanceTransitionRequest request,
        string actor,
        CancellationToken cancellationToken) =>
        SignOrCosignAsync(
            instanceId,
            request,
            actor,
            coSign: true,
            cancellationToken);

    public async Task<ClinicalFormInstanceDetailResponse> AmendInstanceAsync(
        Guid instanceId,
        ClinicalFormInstanceAmendRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var reason = NormalizeReason(request.Reason);
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockInstanceAsync(
            connection,
            transaction,
            instanceId,
            cancellationToken);
        EnsureVersion(current, request.ExpectedVersion);
        if (current.State != "signed")
        {
            throw new ClinicalFormConflictException(
                "Only a signed form instance can begin an amendment.",
                current.Version,
                current.State);
        }

        if (current.SuccessorInstanceId is not null)
        {
            throw new ClinicalFormConflictException(
                "This signed form already has a successor amendment.",
                current.Version,
                current.State);
        }

        var existing = await GetIdempotentInstanceAsync(
            connection,
            transaction,
            actor,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.PredecessorInstanceId != instanceId)
            {
                throw new ClinicalFormConflictException(
                    "The amendment idempotency key was already used for a different form.");
            }

            await transaction.RollbackAsync(cancellationToken);
            return await GetInstanceAsync(existing.InstanceId, cancellationToken);
        }

        var successorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var evaluation = ClinicalFormRuntime.DeserializeEvaluation(current.ValidationJson);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into clinical_form_instances (
                  instance_id, definition_id, definition_revision, patient_id,
                  encounter_id, state, version, author, values_json,
                  validation_json, idempotency_key, predecessor_instance_id,
                  amendment_reason, created_at, updated_at
                )
                values (
                  @successorId, @definitionId, @definitionRevision, @patientId,
                  @encounterId, 'draft', 0, @actor, @values,
                  @validation, @idempotencyKey, @predecessorId,
                  @reason, @now, @now
                );
                """;
            insert.Parameters.AddWithValue("successorId", successorId);
            insert.Parameters.AddWithValue("definitionId", current.DefinitionId);
            insert.Parameters.AddWithValue("definitionRevision", current.DefinitionRevision);
            insert.Parameters.AddWithValue("patientId", current.PatientId);
            AddNullableInteger(insert, "encounterId", current.EncounterId);
            insert.Parameters.AddWithValue("actor", actor);
            AddJson(insert, "values", current.ValuesJson);
            AddJson(insert, "validation", current.ValidationJson);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("predecessorId", instanceId);
            insert.Parameters.AddWithValue("reason", reason);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var predecessorNextVersion = current.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_instances
                set successor_instance_id = @successorId,
                    version = @version,
                    updated_at = @now
                where instance_id = @predecessorId;
                """;
            update.Parameters.AddWithValue("successorId", successorId);
            update.Parameters.AddWithValue("version", predecessorNextVersion);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("predecessorId", instanceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var predecessorHash = ClinicalFormRuntime.HashInstance(
            instanceId,
            current.DefinitionRevision,
            predecessorNextVersion,
            current.State,
            ClinicalFormRuntime.DeserializeValues(current.ValuesJson));
        await InsertInstanceEventAsync(
            connection,
            transaction,
            instanceId,
            predecessorNextVersion,
            "amendment-started",
            "signed",
            "signed",
            actor,
            reason,
            predecessorHash,
            cancellationToken);
        var successorHash = ClinicalFormRuntime.HashInstance(
            successorId,
            current.DefinitionRevision,
            version: 0,
            state: "draft",
            evaluation.Values);
        await InsertInstanceEventAsync(
            connection,
            transaction,
            successorId,
            version: 0,
            action: "amendment-created",
            fromState: null,
            toState: "draft",
            actor,
            reason,
            successorHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetInstanceAsync(successorId, cancellationToken);
    }

    public async Task<ClinicalFormRenderResponse> RenderInstanceAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var detail = await GetInstanceAsync(instanceId, cancellationToken);
        var contentHash = ClinicalFormRuntime.Hash(
            JsonSerializer.Serialize(new
            {
                detail.Instance.InstanceId,
                detail.Instance.DefinitionRevision,
                detail.Definition,
                detail.Values,
                detail.Signatures
            }, ClinicalFormRuntime.JsonOptions));
        return new(
            detail.Instance,
            detail.Definition,
            detail.Values,
            detail.Signatures,
            contentHash,
            Iso(DateTimeOffset.UtcNow),
            ClinicalFormRuntime.RendererVersion);
    }

    public async Task<string> ExportInstanceHtmlAsync(
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        var render = await RenderInstanceAsync(instanceId, cancellationToken);
        var builder = new StringBuilder();
        builder.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(render.Definition.Name))
            .Append("</title><style>body{font:14px Arial,sans-serif;margin:32px;color:#17211f}h1{font-size:24px}h2{font-size:18px;margin-top:24px}dl{display:grid;grid-template-columns:180px 1fr;gap:8px}dt{font-weight:700}dd{margin:0;white-space:pre-wrap}.evidence{border-top:1px solid #aaa;margin-top:28px;padding-top:12px;font-size:12px}@media print{body{margin:10mm}}</style></head><body>")
            .Append("<h1>")
            .Append(WebUtility.HtmlEncode(render.Definition.Name))
            .Append("</h1><p>")
            .Append(WebUtility.HtmlEncode(render.Definition.Purpose))
            .Append("</p>");

        foreach (var section in render.Definition.Sections.OrderBy(section => section.Sequence))
        {
            builder.Append("<section><h2>")
                .Append(WebUtility.HtmlEncode(section.Title))
                .Append("</h2><dl>");
            foreach (var field in render.Definition.Fields
                         .Where(field => field.SectionKey == section.Key)
                         .OrderBy(field => field.Sequence))
            {
                render.Values.TryGetValue(field.Key, out var value);
                builder.Append("<dt>")
                    .Append(WebUtility.HtmlEncode(field.Label))
                    .Append("</dt><dd>")
                    .Append(WebUtility.HtmlEncode(FormatValue(field, value)))
                    .Append("</dd>");
            }

            builder.Append("</dl></section>");
        }

        builder.Append("<div class=\"evidence\"><p>State: ")
            .Append(WebUtility.HtmlEncode(render.Instance.State))
            .Append(" · Definition: ")
            .Append(WebUtility.HtmlEncode(render.Definition.StableKey))
            .Append(" revision ")
            .Append(render.Instance.DefinitionRevision)
            .Append(" · Renderer: ")
            .Append(WebUtility.HtmlEncode(render.RendererVersion))
            .Append("</p><p>Content SHA-256: ")
            .Append(render.ContentHash)
            .Append("</p>");
        foreach (var signature in render.Signatures)
        {
            builder.Append("<p>")
                .Append(WebUtility.HtmlEncode(signature.Role))
                .Append(": ")
                .Append(WebUtility.HtmlEncode(signature.Signer))
                .Append(" · ")
                .Append(WebUtility.HtmlEncode(signature.SignedAt))
                .Append(" · ")
                .Append(WebUtility.HtmlEncode(signature.PolicyRevision))
                .Append("</p>");
        }

        return builder.Append("</div></body></html>").ToString();
    }

    public async Task<bool> DeleteTestFixtureAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? stableKey;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select stable_key
                from clinical_form_definitions
                where definition_id = @id
                for update;
                """;
            command.Parameters.AddWithValue("id", definitionId);
            stableKey = await command.ExecuteScalarAsync(cancellationToken) as string;
        }

        if (stableKey is null)
        {
            return false;
        }

        if (!stableKey.StartsWith("tmp.form.", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only tmp.form.* definitions may be removed by fixture cleanup.");
        }

        await using (var clearLinks = connection.CreateCommand())
        {
            clearLinks.Transaction = transaction;
            clearLinks.CommandText = """
                update clinical_form_instances
                set predecessor_instance_id = null,
                    successor_instance_id = null
                where definition_id = @id;
                """;
            clearLinks.Parameters.AddWithValue("id", definitionId);
            await clearLinks.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var sql in new[]
                 {
                     "delete from clinical_form_instance_events where instance_id in (select instance_id from clinical_form_instances where definition_id = @id);",
                     "delete from clinical_form_signatures where instance_id in (select instance_id from clinical_form_instances where definition_id = @id);",
                     "delete from clinical_form_instances where definition_id = @id;",
                     "delete from clinical_form_definition_events where definition_id = @id;",
                     "update clinical_form_definitions set effective_revision = null where definition_id = @id;",
                     "delete from clinical_form_revisions where definition_id = @id;",
                     "delete from clinical_form_definitions where definition_id = @id;"
                 })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("id", definitionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<ClinicalFormInstanceDetailResponse> SignOrCosignAsync(
        Guid instanceId,
        ClinicalFormInstanceTransitionRequest request,
        string actor,
        bool coSign,
        CancellationToken cancellationToken)
    {
        RejectUnknownFields(request.ExtraFields);
        var reason = NormalizeReason(request.Reason);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockInstanceAsync(
            connection,
            transaction,
            instanceId,
            cancellationToken);
        EnsureVersion(current, request.ExpectedVersion);
        var definition = ClinicalFormRuntime.DeserializeSchema(current.SchemaJson);
        var expectedState = coSign ? "awaiting-co-sign" : "ready-for-signature";
        if (current.State != expectedState)
        {
            throw new ClinicalFormConflictException(
                coSign
                    ? "Only a form awaiting co-signature can be co-signed."
                    : "Only a finalized form can be signed.",
                current.Version,
                current.State);
        }

        await ValidateActiveActorAsync(
            connection,
            transaction,
            actor,
            cancellationToken);
        if (!coSign && current.Author != actor)
        {
            throw new ArgumentException(
                "The local signature policy requires the form author to sign first.");
        }

        if (coSign)
        {
            if (definition.SignaturePolicy != "author-and-cosigner")
            {
                throw new ArgumentException("This form does not require a co-signer.");
            }

            if (current.Author == actor)
            {
                throw new ArgumentException("The co-signer must be distinct from the author.");
            }

            await using var priorSignature = connection.CreateCommand();
            priorSignature.Transaction = transaction;
            priorSignature.CommandText = """
                select signer
                from clinical_form_signatures
                where instance_id = @id and role = 'signer';
                """;
            priorSignature.Parameters.AddWithValue("id", instanceId);
            var signer = await priorSignature.ExecuteScalarAsync(cancellationToken) as string;
            if (signer is null)
            {
                throw new ClinicalFormConflictException(
                    "The required author signature is missing.",
                    current.Version,
                    current.State);
            }

            if (signer == actor)
            {
                throw new ArgumentException("The co-signer must be distinct from the signer.");
            }
        }

        var values = ClinicalFormRuntime.DeserializeValues(current.ValuesJson);
        var evaluation = ClinicalFormRuntime.Evaluate(definition, values);
        if (!evaluation.Valid)
        {
            throw new ArgumentException(
                "The form values no longer satisfy the pinned definition and cannot be signed.");
        }

        var nextVersion = current.Version + 1;
        var nextState = coSign || definition.SignaturePolicy == "author-only"
            ? "signed"
            : "awaiting-co-sign";
        var now = DateTimeOffset.UtcNow;
        var contentHash = ClinicalFormRuntime.HashInstance(
            instanceId,
            current.DefinitionRevision,
            nextVersion,
            nextState,
            evaluation.Values);
        await using (var signature = connection.CreateCommand())
        {
            signature.Transaction = transaction;
            signature.CommandText = """
                insert into clinical_form_signatures (
                  signature_id, instance_id, role, signer, method,
                  policy_revision, credential_context, signed_at, content_hash
                )
                values (
                  @signatureId, @instanceId, @role, @signer, 'local-attestation',
                  @policyRevision, 'active-local-auth-account', @now, @contentHash
                );
                """;
            signature.Parameters.AddWithValue("signatureId", Guid.NewGuid());
            signature.Parameters.AddWithValue("instanceId", instanceId);
            signature.Parameters.AddWithValue("role", coSign ? "co-signer" : "signer");
            signature.Parameters.AddWithValue("signer", actor);
            signature.Parameters.AddWithValue(
                "policyRevision",
                ClinicalFormRuntime.SignaturePolicyRevision);
            signature.Parameters.AddWithValue("now", now);
            signature.Parameters.AddWithValue("contentHash", contentHash);
            await signature.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_instances
                set state = @state,
                    version = @version,
                    validation_json = @validation,
                    updated_at = @now,
                    signed_at = case when @state = 'signed' then @now else signed_at end
                where instance_id = @id;
                """;
            update.Parameters.AddWithValue("state", nextState);
            update.Parameters.AddWithValue("version", nextVersion);
            AddJson(update, "validation", ClinicalFormRuntime.SerializeEvaluation(evaluation));
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("id", instanceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertInstanceEventAsync(
            connection,
            transaction,
            instanceId,
            nextVersion,
            coSign ? "co-signed" : "signed",
            current.State,
            nextState,
            actor,
            reason,
            contentHash,
            cancellationToken);

        if (nextState == "signed" && current.PredecessorInstanceId is not null)
        {
            await CompletePredecessorAmendmentAsync(
                connection,
                transaction,
                current.PredecessorInstanceId.Value,
                instanceId,
                actor,
                reason,
                now,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetInstanceAsync(instanceId, cancellationToken);
    }

    private static async Task CompletePredecessorAmendmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid predecessorId,
        Guid successorId,
        string actor,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        InstanceState predecessor;
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                select
                  i.instance_id, i.definition_id, i.definition_revision,
                  i.patient_id, i.encounter_id, i.state, i.version, i.author,
                  i.values_json, i.validation_json, i.predecessor_instance_id,
                  i.successor_instance_id, r.schema_json
                from clinical_form_instances i
                join clinical_form_revisions r
                  on r.definition_id = i.definition_id
                 and r.revision = i.definition_revision
                where i.instance_id = @id
                for update of i;
                """;
            lockCommand.Parameters.AddWithValue("id", predecessorId);
            await using var reader = await lockCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ClinicalFormConflictException(
                    "The predecessor form was not found.");
            }

            predecessor = ReadLockedInstance(reader);
        }

        if (predecessor.State != "signed"
            || predecessor.SuccessorInstanceId != successorId)
        {
            throw new ClinicalFormConflictException(
                "The predecessor form no longer accepts this successor.",
                predecessor.Version,
                predecessor.State);
        }

        var nextVersion = predecessor.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_instances
                set state = 'amended',
                    version = @version,
                    updated_at = @now
                where instance_id = @id;
                """;
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("now", now);
            update.Parameters.AddWithValue("id", predecessorId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var snapshotHash = ClinicalFormRuntime.HashInstance(
            predecessorId,
            predecessor.DefinitionRevision,
            nextVersion,
            "amended",
            ClinicalFormRuntime.DeserializeValues(predecessor.ValuesJson));
        await InsertInstanceEventAsync(
            connection,
            transaction,
            predecessorId,
            nextVersion,
            "amended-by-successor",
            "signed",
            "amended",
            actor,
            reason,
            snapshotHash,
            cancellationToken);
    }

    private async Task<ClinicalFormInstanceDetailResponse> MoveInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InstanceState current,
        string nextState,
        string action,
        string actor,
        string reason,
        ClinicalFormEvaluationResponse evaluation,
        DateTimeOffset? finalizedAt,
        DateTimeOffset? signedAt,
        CancellationToken cancellationToken)
    {
        var nextVersion = current.Version + 1;
        var now = DateTimeOffset.UtcNow;
        var snapshotHash = ClinicalFormRuntime.HashInstance(
            current.InstanceId,
            current.DefinitionRevision,
            nextVersion,
            nextState,
            evaluation.Values);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update clinical_form_instances
                set state = @state,
                    version = @version,
                    values_json = @values,
                    validation_json = @validation,
                    updated_at = @now,
                    finalized_at = coalesce(@finalizedAt, finalized_at),
                    signed_at = coalesce(@signedAt, signed_at)
                where instance_id = @id;
                """;
            update.Parameters.AddWithValue("state", nextState);
            update.Parameters.AddWithValue("version", nextVersion);
            AddJson(update, "values", ClinicalFormRuntime.SerializeValues(evaluation.Values));
            AddJson(update, "validation", ClinicalFormRuntime.SerializeEvaluation(evaluation));
            update.Parameters.AddWithValue("now", now);
            AddNullableTimestamp(update, "finalizedAt", finalizedAt);
            AddNullableTimestamp(update, "signedAt", signedAt);
            update.Parameters.AddWithValue("id", current.InstanceId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertInstanceEventAsync(
            connection,
            transaction,
            current.InstanceId,
            nextVersion,
            action,
            current.State,
            nextState,
            actor,
            reason,
            snapshotHash,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetInstanceAsync(current.InstanceId, cancellationToken);
    }

    private static async Task<ClinicalFormDefinitionDetailResponse?> GetDefinitionAsync(
        NpgsqlConnection connection,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        int latestRevision;
        int? effectiveRevision;
        string stableKey;
        await using (var definitionCommand = connection.CreateCommand())
        {
            definitionCommand.CommandText = """
                select stable_key, latest_revision, effective_revision
                from clinical_form_definitions
                where definition_id = @id;
                """;
            definitionCommand.Parameters.AddWithValue("id", definitionId);
            await using var reader =
                await definitionCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            stableKey = reader.GetString(0);
            latestRevision = reader.GetInt32(1);
            effectiveRevision = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        }

        var revisions = new List<ClinicalFormRevisionItem>();
        await using (var revisionCommand = connection.CreateCommand())
        {
            revisionCommand.CommandText = """
                select
                  definition_id, revision, status, version, schema_json,
                  renderer_version, schema_hash, author, reviewed_by, approved_by,
                  effective_from, effective_to, created_at, updated_at, updated_by,
                  predecessor_revision
                from clinical_form_revisions
                where definition_id = @id
                order by revision desc;
                """;
            revisionCommand.Parameters.AddWithValue("id", definitionId);
            await using var reader =
                await revisionCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                revisions.Add(ReadRevision(reader));
            }
        }

        var current = revisions.Single(revision => revision.Revision == latestRevision);
        var events = new List<ClinicalFormDefinitionEvent>();
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.CommandText = """
                select
                  event_id, revision, action, from_status, to_status,
                  actor, reason, occurred_at, snapshot_hash
                from clinical_form_definition_events
                where definition_id = @id
                order by event_id desc;
                """;
            eventCommand.Parameters.AddWithValue("id", definitionId);
            await using var reader =
                await eventCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
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
        }

        var summary = new ClinicalFormDefinitionSummary(
            definitionId,
            stableKey,
            current.Definition.Name,
            current.Definition.Purpose,
            current.Definition.ContextScope,
            latestRevision,
            effectiveRevision,
            current.Status,
            current.Version,
            current.Definition.SignaturePolicy,
            current.UpdatedAt,
            current.UpdatedBy);
        return new(summary, current, revisions, events);
    }

    private static ClinicalFormRevisionItem ReadRevision(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetInt32(3),
            ClinicalFormRuntime.DeserializeSchema(reader.GetString(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10)
                ? null
                : Iso(reader.GetFieldValue<DateTimeOffset>(10)),
            reader.IsDBNull(11)
                ? null
                : Iso(reader.GetFieldValue<DateTimeOffset>(11)),
            Iso(reader.GetFieldValue<DateTimeOffset>(12)),
            Iso(reader.GetFieldValue<DateTimeOffset>(13)),
            reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetInt32(15));

    private static async Task InsertRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        int revision,
        string status,
        int version,
        ClinicalFormSchemaDefinition definition,
        string schemaHash,
        string actor,
        int? predecessorRevision,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_form_revisions (
              definition_id, revision, status, version, schema_json,
              renderer_version, schema_hash, author, predecessor_revision,
              created_at, updated_at, updated_by
            )
            values (
              @id, @revision, @status, @version, @schema,
              @renderer, @hash, @actor, @predecessor,
              @now, @now, @actor
            );
            """;
        command.Parameters.AddWithValue("id", definitionId);
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("version", version);
        AddJson(command, "schema", ClinicalFormRuntime.SerializeSchema(definition));
        command.Parameters.AddWithValue("renderer", ClinicalFormRuntime.RendererVersion);
        command.Parameters.AddWithValue("hash", schemaHash);
        command.Parameters.AddWithValue("actor", actor);
        AddNullableInteger(command, "predecessor", predecessorRevision);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDefinitionEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        int revision,
        string action,
        string? fromStatus,
        string toStatus,
        string actor,
        string reason,
        string snapshotHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_form_definition_events (
              definition_id, revision, action, from_status, to_status,
              actor, reason, occurred_at, snapshot_hash
            )
            values (
              @id, @revision, @action, @fromStatus, @toStatus,
              @actor, @reason, clock_timestamp(), @hash
            );
            """;
        command.Parameters.AddWithValue("id", definitionId);
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("action", action);
        AddNullableText(command, "fromStatus", fromStatus);
        command.Parameters.AddWithValue("toStatus", toStatus);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("hash", snapshotHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertInstanceEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid instanceId,
        int version,
        string action,
        string? fromState,
        string toState,
        string actor,
        string reason,
        string snapshotHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_form_instance_events (
              instance_id, version, action, from_state, to_state,
              actor, reason, occurred_at, snapshot_hash
            )
            values (
              @id, @version, @action, @fromState, @toState,
              @actor, @reason, clock_timestamp(), @hash
            );
            """;
        command.Parameters.AddWithValue("id", instanceId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("action", action);
        AddNullableText(command, "fromState", fromState);
        command.Parameters.AddWithValue("toState", toState);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("hash", snapshotHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ClinicalFormInstanceDetailResponse?> GetInstanceAsync(
        NpgsqlConnection connection,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        ClinicalFormInstanceSummary summary;
        ClinicalFormSchemaDefinition definition;
        IReadOnlyDictionary<string, JsonElement> values;
        ClinicalFormEvaluationResponse validation;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  i.instance_id,
                  i.definition_id,
                  i.definition_revision,
                  d.stable_key,
                  r.schema_json,
                  i.patient_id,
                  i.encounter_id,
                  i.state,
                  i.version,
                  i.author,
                  i.predecessor_instance_id,
                  i.successor_instance_id,
                  i.amendment_reason,
                  i.created_at,
                  i.updated_at,
                  i.finalized_at,
                  i.signed_at,
                  i.values_json,
                  i.validation_json
                from clinical_form_instances i
                join clinical_form_definitions d on d.definition_id = i.definition_id
                join clinical_form_revisions r
                  on r.definition_id = i.definition_id
                 and r.revision = i.definition_revision
                where i.instance_id = @id;
                """;
            command.Parameters.AddWithValue("id", instanceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            summary = ReadInstanceSummary(reader);
            definition = ClinicalFormRuntime.DeserializeSchema(reader.GetString(4));
            values = ClinicalFormRuntime.DeserializeValues(reader.GetString(17));
            validation = ClinicalFormRuntime.DeserializeEvaluation(reader.GetString(18));
        }

        var signatures = new List<ClinicalFormSignatureItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  signature_id, role, signer, method, policy_revision,
                  credential_context, signed_at, content_hash
                from clinical_form_signatures
                where instance_id = @id
                order by signed_at, signature_id;
                """;
            command.Parameters.AddWithValue("id", instanceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                signatures.Add(new(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    Iso(reader.GetFieldValue<DateTimeOffset>(6)),
                    reader.GetString(7)));
            }
        }

        var events = new List<ClinicalFormInstanceEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  event_id, version, action, from_state, to_state,
                  actor, reason, occurred_at, snapshot_hash
                from clinical_form_instance_events
                where instance_id = @id
                order by event_id desc;
                """;
            command.Parameters.AddWithValue("id", instanceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
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
        }

        return new(summary, definition, values, validation, signatures, events);
    }

    private static ClinicalFormInstanceSummary ReadInstanceSummary(NpgsqlDataReader reader)
    {
        var schema = ClinicalFormRuntime.DeserializeSchema(reader.GetString(4));
        return new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            schema.Name,
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetString(9),
            schema.SignaturePolicy,
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            Iso(reader.GetFieldValue<DateTimeOffset>(13)),
            Iso(reader.GetFieldValue<DateTimeOffset>(14)),
            reader.IsDBNull(15)
                ? null
                : Iso(reader.GetFieldValue<DateTimeOffset>(15)),
            reader.IsDBNull(16)
                ? null
                : Iso(reader.GetFieldValue<DateTimeOffset>(16)));
    }

    private static async Task<InstanceState> LockInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid instanceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              i.instance_id, i.definition_id, i.definition_revision,
              i.patient_id, i.encounter_id, i.state, i.version, i.author,
              i.values_json, i.validation_json, i.predecessor_instance_id,
              i.successor_instance_id, r.schema_json
            from clinical_form_instances i
            join clinical_form_revisions r
              on r.definition_id = i.definition_id
             and r.revision = i.definition_revision
            where i.instance_id = @id
            for update of i;
            """;
        command.Parameters.AddWithValue("id", instanceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException("Clinical form instance was not found.");
        }

        return ReadLockedInstance(reader);
    }

    private static InstanceState ReadLockedInstance(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.GetString(12));

    private static void EnsureVersion(InstanceState current, int expectedVersion)
    {
        if (current.Version != expectedVersion)
        {
            throw new ClinicalFormConflictException(
                $"The form instance changed after it was loaded. Current version is {current.Version}.",
                current.Version,
                current.State);
        }
    }

    private static async Task<EffectiveRevision> ResolveEffectiveRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        int? requestedRevision,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              r.revision, r.schema_json
            from clinical_form_definitions d
            join clinical_form_revisions r
              on r.definition_id = d.definition_id
             and r.revision = d.effective_revision
            where d.definition_id = @id
              and r.status = 'effective'
              and (r.effective_from is null or r.effective_from <= clock_timestamp())
              and (r.effective_to is null or r.effective_to > clock_timestamp());
            """;
        command.Parameters.AddWithValue("id", definitionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException(
                "The selected form has no currently effective revision.");
        }

        var revision = reader.GetInt32(0);
        if (requestedRevision is not null && requestedRevision != revision)
        {
            throw new ClinicalFormConflictException(
                $"New instances must use effective revision {revision}.",
                currentVersion: revision,
                currentState: "effective");
        }

        return new(revision, reader.GetString(1));
    }

    private static async Task<IdempotentInstance?> GetIdempotentInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string actor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              instance_id, definition_id, definition_revision, patient_id,
              encounter_id, predecessor_instance_id
            from clinical_form_instances
            where author = @actor and idempotency_key = @key;
            """;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5))
            : null;
    }

    private static async Task<string> ResolvePatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient is required.");
        }

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

    private static async Task ValidateEncounterAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int encounterId,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
              select 1
              from encounters
              where encounter = @encounterId
                and patient_id = @patientId
                and archived_at is null
            );
            """;
        command.Parameters.AddWithValue("encounterId", encounterId);
        command.Parameters.AddWithValue("patientId", patientId);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException(
                "Encounter was not found, is archived, or does not belong to this patient.");
        }
    }

    private static async Task ValidateActiveActorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
              select 1
              from auth_accounts
              where lower(username) = lower(@actor)
                and active = true
            );
            """;
        command.Parameters.AddWithValue("actor", actor);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException("The signer is not an active local account.");
        }
    }

    private static string FormatValue(
        ClinicalFormFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "";
        }

        return field.Type switch
        {
            "boolean" => value.GetBoolean() ? "Yes" : "No",
            "select" or "coded" => field.Options
                .FirstOrDefault(option => option.Code == value.GetString())?.Display
                ?? value.GetString()
                ?? "",
            "multiselect" => string.Join(
                ", ",
                value.EnumerateArray().Select(item =>
                    field.Options.FirstOrDefault(
                        option => option.Code == item.GetString())?.Display
                    ?? item.GetString())),
            "measurement" => value.TryGetProperty("value", out var number)
                             && value.TryGetProperty("unit", out var unit)
                ? $"{number.GetRawText()} {unit.GetString()}"
                : value.GetRawText(),
            "repeat" => value.GetRawText(),
            _ => value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : value.GetRawText()
        };
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new ArgumentException(
                "Page must be positive and page size must be 1 to 100.");
        }
    }

    private static string? NormalizeOptionalSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var value = search.Trim().ToLowerInvariant();
        if (value.Length > 120)
        {
            throw new ArgumentException("Search must be 120 characters or fewer.");
        }

        return value;
    }

    private static string? NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var value = status.Trim().ToLowerInvariant();
        return ClinicalFormRuntime.BuildPolicy().DefinitionStates.Contains(
            value,
            StringComparer.Ordinal)
            ? value
            : throw new ArgumentException("Unsupported form definition status.");
    }

    private static string NormalizeReason(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 3 or > 1000)
        {
            throw new ArgumentException("Reason must be 3 to 1000 characters.");
        }

        return value;
    }

    private static string NormalizeIdempotencyKey(string? key)
    {
        var value = key?.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 8 or > 120
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not ('-' or '_' or '.')))
        {
            throw new ArgumentException(
                "Idempotency key must be 8 to 120 letters, numbers, dots, dashes, or underscores.");
        }

        return value;
    }

    private static DateTimeOffset? ParseOptionalDateTime(
        string? value,
        string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : throw new ArgumentException($"{label} must be an ISO 8601 date-time.");
    }

    private static void RejectUnknownFields(
        IDictionary<string, JsonElement>? extraFields)
    {
        if (extraFields is { Count: > 0 })
        {
            throw new ArgumentException(
                $"Unknown request fields are not allowed: {string.Join(", ", extraFields.Keys.Order())}.");
        }
    }

    private static void AddJson(
        NpgsqlCommand command,
        string name,
        string value) =>
        command.Parameters.Add(name, NpgsqlDbType.Jsonb).Value = value;

    private static void AddNullableText(
        NpgsqlCommand command,
        string name,
        string? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Text).Value =
            (object?)value ?? DBNull.Value;

    private static void AddNullableInteger(
        NpgsqlCommand command,
        string name,
        int? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value =
            (object?)value ?? DBNull.Value;

    private static void AddNullableTimestamp(
        NpgsqlCommand command,
        string name,
        DateTimeOffset? value) =>
        command.Parameters.Add(name, NpgsqlDbType.TimestampTz).Value =
            (object?)value ?? DBNull.Value;

    private static string Iso(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    private sealed record RevisionState(
        int Revision,
        string Status,
        int Version,
        string SchemaJson,
        string SchemaHash);

    private sealed record EffectiveRevision(
        int Revision,
        string SchemaJson);

    private sealed record IdempotentInstance(
        Guid InstanceId,
        Guid DefinitionId,
        int DefinitionRevision,
        string PatientId,
        int? EncounterId,
        Guid? PredecessorInstanceId);

    private sealed record InstanceState(
        Guid InstanceId,
        Guid DefinitionId,
        int DefinitionRevision,
        string PatientId,
        int? EncounterId,
        string State,
        int Version,
        string Author,
        string ValuesJson,
        string ValidationJson,
        Guid? PredecessorInstanceId,
        Guid? SuccessorInstanceId,
        string SchemaJson);
}
