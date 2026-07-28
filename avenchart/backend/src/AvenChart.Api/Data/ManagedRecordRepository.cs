using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ManagedRecordConflictException(int currentVersion, string currentState)
    : Exception($"The managed record intake is now at version {currentVersion} in state {currentState}.")
{
    public int CurrentVersion { get; } = currentVersion;
    public string CurrentState { get; } = currentState;
}

public sealed class ManagedRecordIdempotencyConflictException()
    : Exception("The idempotency key is already associated with a different managed record request.");

public sealed class ManagedRecordRepository(NpgsqlDataSource dataSource)
{
    public const string PolicyRevision = "local-record-control-v1";
    public const int MaxFileSizeBytes = 25 * 1024 * 1024;

    private static readonly string[] AcceptedMediaTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain"
    ];

    private static readonly string[] RecordClasses =
    [
        "clinical-record",
        "correspondence",
        "identity",
        "financial",
        "administrative"
    ];

    private static readonly string[] SourceTypes =
    [
        "file-upload",
        "scanner-capture",
        "external-import",
        "generated-output"
    ];

    private static readonly string[] SensitivityLevels =
    [
        "standard",
        "restricted",
        "highly-sensitive"
    ];

    private static readonly string[] States =
    [
        "captured",
        "quarantined",
        "scanning",
        "failed",
        "available"
    ];

    private static readonly IReadOnlyDictionary<int, string> Categories =
        new Dictionary<int, string>
        {
            [2] = "Lab Report",
            [3] = "Medical Record",
            [4] = "Patient Information",
            [5] = "Patient ID card",
            [6] = "Advance Directive",
            [13] = "CCDA",
            [29] = "Reviewed",
            [31] = "Invoices"
        };

    public ManagedRecordPolicyResponse GetPolicy() =>
        new(
            Revision: PolicyRevision,
            LifecycleState: "local-foundation-owner-gated",
            MaxFileSizeBytes: MaxFileSizeBytes,
            AcceptedMediaTypes: AcceptedMediaTypes,
            RecordClasses: RecordClasses,
            SourceTypes: SourceTypes,
            SensitivityLevels: SensitivityLevels,
            States: States,
            StorageAdapter: new ManagedRecordAdapterStatus(
                "local-database-record-intake",
                "local-adapter-active",
                "Quarantined bytes remain outside patient_documents until release."),
            ValidationAdapter: new ManagedRecordAdapterStatus(
                "local-structural-validator",
                "local-adapter-active",
                "Rechecks size, media type, and SHA-256 only; it is not an anti-malware engine."),
            AntiMalwareVerified: false,
            EnvironmentBoundary:
                "Local synthetic structural-validation workflow. Release proves lifecycle enforcement, not production malware safety.",
            ProductionBlockers:
            [
                "Select approved encrypted object storage, key management, and backup behavior.",
                "Select an anti-malware engine and signature/update/failure policy.",
                "Define conversion and OCR adapters, queues, retry limits, and dead-letter ownership.",
                "Approve record classes, sensitivity handling, language, facility, and author requirements.",
                "Approve quarantine reviewer roles, separation of duties, and rejected-content disposition.",
                "Prove corrupt/missing object detection, recovery, monitoring, and incident escalation.",
                "Migrate or explicitly classify legacy compatibility uploads under the managed boundary."
            ]);

    public async Task<ManagedRecordListResponse> ListAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var normalizedPatientId = RequireText(patientId, "Patient is required.", 80);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, null, normalizedPatientId, cancellationToken);
        if (patient is null)
        {
            throw new ArgumentException("Patient was not found.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"{BaseSelect} where i.patient_id = @patientId order by i.updated_at desc, i.intake_id;";
        command.Parameters.AddWithValue("patientId", patient.Value.PatientId);
        var items = await ReadItemsAsync(command, cancellationToken);
        return BuildList(patient.Value.PatientId, items);
    }

    public async Task<ManagedRecordMutationResponse> CreateAsync(
        ManagedRecordCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedActor = RequireText(actor, "Authenticated actor is required.", 120);
        var input = ValidateCreateRequest(request);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var existing = await FindByIdempotencyAsync(
            connection,
            null,
            normalizedActor,
            input.IdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Value.Fingerprint, input.Fingerprint, StringComparison.Ordinal))
            {
                throw new ManagedRecordIdempotencyConflictException();
            }

            var replay = await GetAsync(connection, null, existing.Value.IntakeId, cancellationToken)
                ?? throw new InvalidOperationException("The idempotent record intake could not be reloaded.");
            return new ManagedRecordMutationResponse(true, replay with { IdempotentReplay = true });
        }

        var patient = await GetPatientAsync(connection, null, input.PatientId, cancellationToken)
            ?? throw new ArgumentException("Patient was not found.");
        if (input.Encounter is { } encounter
            && !await EncounterBelongsToPatientAsync(
                connection,
                null,
                patient.PatientId,
                encounter,
                cancellationToken))
        {
            throw new ArgumentException("Encounter does not belong to the selected patient.");
        }

        if (input.FacilityId is { } facilityId
            && !await ActiveFacilityExistsAsync(connection, null, facilityId, cancellationToken))
        {
            throw new ArgumentException("Facility must identify an active local facility.");
        }

        var intakeId = Guid.NewGuid();
        var storageReference = $"record-intake/{intakeId:N}/content/1";
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into managed_record_intakes (
                      intake_id, patient_id, legacy_pid, idempotency_key, request_fingerprint,
                      category_id, category_name, title, service_date, encounter,
                      record_class, source_type, author_name, facility_id, sensitivity, language_tag,
                      file_name, media_type, size_bytes, content_sha256, storage_reference, content_bytes,
                      state, workflow_version, availability_status, validation_status,
                      created_by, updated_by, last_reason)
                    values (
                      @intakeId, @patientId, @legacyPid, @idempotencyKey, @fingerprint,
                      @categoryId, @categoryName, @title, @serviceDate, @encounter,
                      @recordClass, @sourceType, @authorName, @facilityId, @sensitivity, @languageTag,
                      @fileName, @mediaType, @sizeBytes, @checksum, @storageReference, @contentBytes,
                      'captured', 0, 'withheld', 'pending',
                      @actor, @actor, @reason);
                    """;
                AddCreateParameters(insert, intakeId, patient, input, storageReference, normalizedActor);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertEventAsync(
                connection,
                transaction,
                intakeId,
                action: "captured",
                fromState: null,
                toState: "captured",
                fromRecordClass: null,
                toRecordClass: input.RecordClass,
                fromSensitivity: null,
                toSensitivity: input.Sensitivity,
                reason: input.Reason,
                actor: normalizedActor,
                workflowVersion: 0,
                validationStatus: "pending",
                checksum: input.Checksum,
                documentId: null,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            var raced = await FindByIdempotencyAsync(
                connection,
                null,
                normalizedActor,
                input.IdempotencyKey,
                cancellationToken);
            if (raced is null || !string.Equals(raced.Value.Fingerprint, input.Fingerprint, StringComparison.Ordinal))
            {
                throw new ManagedRecordIdempotencyConflictException();
            }

            var replay = await GetAsync(connection, null, raced.Value.IntakeId, cancellationToken)
                ?? throw new InvalidOperationException("The raced idempotent record intake could not be reloaded.");
            return new ManagedRecordMutationResponse(true, replay with { IdempotentReplay = true });
        }

        var created = await GetAsync(connection, null, intakeId, cancellationToken)
            ?? throw new InvalidOperationException("The managed record intake could not be reloaded.");
        return new ManagedRecordMutationResponse(false, created);
    }

    public async Task<ManagedRecordItem?> UpdateClassificationAsync(
        Guid intakeId,
        ManagedRecordClassificationUpdateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedActor = RequireText(actor, "Authenticated actor is required.", 120);
        var recordClass = NormalizeChoice(request.RecordClass, RecordClasses, "record class");
        var sourceType = NormalizeChoice(request.SourceType, SourceTypes, "source type");
        var authorName = RequireText(request.AuthorName, "Author/originator is required.", 200);
        var sensitivity = NormalizeChoice(request.Sensitivity, SensitivityLevels, "sensitivity");
        var languageTag = NormalizeLanguage(request.LanguageTag);
        var reason = RequireText(request.Reason, "Classification change reason is required.", 500);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetForUpdateAsync(connection, transaction, intakeId, cancellationToken);
        if (current is null)
        {
            return null;
        }
        EnsureVersion(current, request.ExpectedVersion);
        if (current.State is "scanning" or "available" or "rejected")
        {
            throw new ArgumentException("Classification can change only while captured, quarantined, or failed.");
        }
        if (request.FacilityId is { } facilityId
            && !await ActiveFacilityExistsAsync(connection, transaction, facilityId, cancellationToken))
        {
            throw new ArgumentException("Facility must identify an active local facility.");
        }

        if (current.RecordClass == recordClass
            && current.SourceType == sourceType
            && current.AuthorName == authorName
            && current.FacilityId == request.FacilityId
            && current.Sensitivity == sensitivity
            && current.LanguageTag == languageTag)
        {
            throw new ArgumentException("Classification update must change at least one field.");
        }

        var nextVersion = current.WorkflowVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update managed_record_intakes
                set record_class = @recordClass,
                    source_type = @sourceType,
                    author_name = @authorName,
                    facility_id = @facilityId,
                    sensitivity = @sensitivity,
                    language_tag = @languageTag,
                    workflow_version = @version,
                    updated_by = @actor,
                    updated_at = now(),
                    last_reason = @reason
                where intake_id = @intakeId;
                """;
            update.Parameters.AddWithValue("recordClass", recordClass);
            update.Parameters.AddWithValue("sourceType", sourceType);
            update.Parameters.AddWithValue("authorName", authorName);
            AddNullableInt(update, "facilityId", request.FacilityId);
            update.Parameters.AddWithValue("sensitivity", sensitivity);
            update.Parameters.AddWithValue("languageTag", languageTag);
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("actor", normalizedActor);
            update.Parameters.AddWithValue("reason", reason);
            update.Parameters.AddWithValue("intakeId", intakeId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            intakeId,
            "reclassified",
            current.State,
            current.State,
            current.RecordClass,
            recordClass,
            current.Sensitivity,
            sensitivity,
            reason,
            normalizedActor,
            nextVersion,
            current.ValidationStatus,
            current.ContentChecksumSha256,
            current.DocumentId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(connection, null, intakeId, cancellationToken);
    }

    public async Task<ManagedRecordItem?> ActAsync(
        Guid intakeId,
        string action,
        ManagedRecordActionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedAction = RequireText(action, "Action is required.", 30).ToLowerInvariant();
        var normalizedActor = RequireText(actor, "Authenticated actor is required.", 120);
        var reason = RequireText(request.Reason, "Workflow reason is required.", 500);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetForUpdateAsync(connection, transaction, intakeId, cancellationToken);
        if (current is null)
        {
            return null;
        }
        EnsureVersion(current, request.ExpectedVersion);

        var transition = TransitionFor(current.State, normalizedAction);
        var nextVersion = current.WorkflowVersion + 1;
        int? documentId = current.DocumentId;
        var validationStatus = transition.ValidationStatus;
        var availabilityStatus = transition.AvailabilityStatus;
        string? failureReason = normalizedAction == "fail" ? reason : null;
        var eventAction = normalizedAction;

        if (normalizedAction == "release")
        {
            var actualChecksum = Sha256(current.ContentBytes);
            if (!string.Equals(actualChecksum, current.ContentChecksumSha256, StringComparison.Ordinal))
            {
                transition = new Transition("failed", "failed", "unavailable");
                validationStatus = transition.ValidationStatus;
                availabilityStatus = transition.AvailabilityStatus;
                failureReason = "Stored content checksum no longer matches the captured SHA-256.";
                eventAction = "integrity-failed";
            }
            else
            {
                documentId = await ReleaseDocumentAsync(
                    connection,
                    transaction,
                    current,
                    intakeId,
                    normalizedActor,
                    cancellationToken);
            }
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update managed_record_intakes
                set document_id = @documentId,
                    state = @state,
                    workflow_version = @version,
                    availability_status = @availabilityStatus,
                    validation_status = @validationStatus,
                    failure_reason = @failureReason,
                    updated_by = @actor,
                    updated_at = now(),
                    last_reason = @reason
                where intake_id = @intakeId;
                """;
            AddNullableInt(update, "documentId", documentId);
            update.Parameters.AddWithValue("state", transition.State);
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("availabilityStatus", availabilityStatus);
            update.Parameters.AddWithValue("validationStatus", validationStatus);
            AddNullableText(update, "failureReason", failureReason);
            update.Parameters.AddWithValue("actor", normalizedActor);
            update.Parameters.AddWithValue("reason", reason);
            update.Parameters.AddWithValue("intakeId", intakeId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            intakeId,
            eventAction,
            current.State,
            transition.State,
            current.RecordClass,
            current.RecordClass,
            current.Sensitivity,
            current.Sensitivity,
            failureReason ?? reason,
            normalizedActor,
            nextVersion,
            validationStatus,
            current.ContentChecksumSha256,
            documentId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(connection, null, intakeId, cancellationToken);
    }

    public async Task<ManagedRecordHistoryResponse?> GetHistoryAsync(
        Guid intakeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var current = await GetAsync(connection, null, intakeId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, action, from_state, to_state, from_record_class, to_record_class,
              from_sensitivity, to_sensitivity, reason, actor, occurred_at, workflow_version,
              validation_status, content_version, content_sha256, document_id
            from managed_record_intake_events
            where intake_id = @intakeId
            order by occurred_at desc, event_id desc;
            """;
        command.Parameters.AddWithValue("intakeId", intakeId);
        var events = new List<ManagedRecordEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ManagedRecordEvent(
                reader.GetGuid(reader.GetOrdinal("event_id")),
                reader.GetString(reader.GetOrdinal("action")),
                ReadNullableString(reader, "from_state"),
                reader.GetString(reader.GetOrdinal("to_state")),
                ReadNullableString(reader, "from_record_class"),
                reader.GetString(reader.GetOrdinal("to_record_class")),
                ReadNullableString(reader, "from_sensitivity"),
                reader.GetString(reader.GetOrdinal("to_sensitivity")),
                reader.GetString(reader.GetOrdinal("reason")),
                reader.GetString(reader.GetOrdinal("actor")),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at")).ToString("O"),
                reader.GetInt32(reader.GetOrdinal("workflow_version")),
                reader.GetString(reader.GetOrdinal("validation_status")),
                reader.GetInt32(reader.GetOrdinal("content_version")),
                reader.GetString(reader.GetOrdinal("content_sha256")),
                ReadNullableInt32(reader, "document_id")));
        }

        return new ManagedRecordHistoryResponse(
            PolicyRevision,
            intakeId,
            current.State,
            current.WorkflowVersion,
            events.Count,
            events);
    }

    public async Task<bool> DeleteTestFixtureAsync(
        Guid intakeId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetForUpdateAsync(connection, transaction, intakeId, cancellationToken);
        if (current is null)
        {
            return false;
        }
        if (!current.Title.StartsWith("TMP-RECORD-", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only TMP-RECORD-* managed intake fixtures can be deleted.");
        }

        await using var deleteIntake = connection.CreateCommand();
        deleteIntake.Transaction = transaction;
        deleteIntake.CommandText = "delete from managed_record_intakes where intake_id = @intakeId;";
        deleteIntake.Parameters.AddWithValue("intakeId", intakeId);
        await deleteIntake.ExecuteNonQueryAsync(cancellationToken);

        if (current.DocumentId is { } documentId)
        {
            await using var deleteDocument = connection.CreateCommand();
            deleteDocument.Transaction = transaction;
            deleteDocument.CommandText = "delete from patient_documents where id = @documentId;";
            deleteDocument.Parameters.AddWithValue("documentId", documentId);
            await deleteDocument.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static ValidatedCreate ValidateCreateRequest(ManagedRecordCreateRequest request)
    {
        var patientId = RequireText(request.PatientId, "Patient is required.", 80);
        var title = RequireText(request.Title, "Record title is required.", 255);
        if (!DateOnly.TryParseExact(
                request.ServiceDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var serviceDate))
        {
            throw new ArgumentException("Service date must use YYYY-MM-DD.");
        }
        if (!Categories.ContainsKey(request.CategoryId))
        {
            throw new ArgumentException("Filing category is not supported by the managed record policy.");
        }
        var categoryId = request.CategoryId;
        var recordClass = NormalizeChoice(request.RecordClass, RecordClasses, "record class");
        var sourceType = NormalizeChoice(request.SourceType, SourceTypes, "source type");
        var authorName = RequireText(request.AuthorName, "Author/originator is required.", 200);
        var sensitivity = NormalizeChoice(request.Sensitivity, SensitivityLevels, "sensitivity");
        var languageTag = NormalizeLanguage(request.LanguageTag);
        var fileName = SanitizeFileName(RequireText(request.FileName, "File name is required.", 255));
        var mediaType = NormalizeChoice(request.MediaType, AcceptedMediaTypes, "media type");
        var expectedChecksum = RequireText(
            request.ExpectedChecksumSha256,
            "Expected SHA-256 checksum is required.",
            64).ToLowerInvariant();
        if (!Regex.IsMatch(expectedChecksum, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Expected checksum must be a lowercase SHA-256 value.");
        }
        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(
                RequireText(request.ContentBase64, "File content is required.", MaxFileSizeBytes * 2));
        }
        catch (FormatException)
        {
            throw new ArgumentException("File content must be valid base64.");
        }
        if (contentBytes.Length == 0 || contentBytes.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size must be between 1 and {MaxFileSizeBytes} bytes.");
        }
        var checksum = Sha256(contentBytes);
        if (!string.Equals(checksum, expectedChecksum, StringComparison.Ordinal))
        {
            throw new ArgumentException("Expected SHA-256 does not match the uploaded bytes.");
        }
        var idempotencyKey = RequireText(request.IdempotencyKey, "Idempotency key is required.", 120);
        var reason = RequireText(request.Reason, "Capture reason is required.", 500);
        var fingerprint = Sha256(Encoding.UTF8.GetBytes(string.Join(
            "\n",
            patientId,
            categoryId,
            title,
            serviceDate.ToString("yyyy-MM-dd"),
            request.Encounter?.ToString() ?? string.Empty,
            recordClass,
            sourceType,
            authorName,
            request.FacilityId?.ToString() ?? string.Empty,
            sensitivity,
            languageTag,
            fileName,
            mediaType,
            checksum,
            reason)));
        return new ValidatedCreate(
            patientId,
            categoryId,
            Categories[categoryId],
            title,
            serviceDate,
            request.Encounter,
            recordClass,
            sourceType,
            authorName,
            request.FacilityId,
            sensitivity,
            languageTag,
            fileName,
            mediaType,
            contentBytes,
            checksum,
            idempotencyKey,
            fingerprint,
            reason);
    }

    private static Transition TransitionFor(string state, string action) =>
        (state, action) switch
        {
            ("captured", "quarantine") => new("quarantined", "queued", "withheld"),
            ("quarantined", "start") => new("scanning", "running", "withheld"),
            ("scanning", "fail") => new("failed", "failed", "unavailable"),
            ("failed", "retry") => new("quarantined", "queued", "withheld"),
            ("scanning", "release") => new("available", "locally-validated", "available"),
            _ => throw new ArgumentException($"Action '{action}' is not valid while the intake is {state}.")
        };

    private static IReadOnlyList<string> AvailableActions(string state) =>
        state switch
        {
            "captured" => ["quarantine", "reclassify"],
            "quarantined" => ["start", "reclassify"],
            "scanning" => ["fail", "release"],
            "failed" => ["retry", "reclassify"],
            _ => []
        };

    private static async Task<int> ReleaseDocumentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ManagedRecordRow current,
        Guid intakeId,
        string actor,
        CancellationToken cancellationToken)
    {
        await using (var advisory = connection.CreateCommand())
        {
            advisory.Transaction = transaction;
            advisory.CommandText = "select pg_advisory_xact_lock(9000000);";
            await advisory.ExecuteScalarAsync(cancellationToken);
        }

        int documentId;
        await using (var idCommand = connection.CreateCommand())
        {
            idCommand.Transaction = transaction;
            idCommand.CommandText = """
                select greatest(coalesce(max(id), 8999999) + 1, 9000000)
                from patient_documents;
                """;
            documentId = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
        }

        var documentKey = $"DOC-RECORD-{intakeId:N}";
        var notes =
            $"Managed record intake {intakeId}; local structural validation only; anti-malware not verified; "
            + $"class={current.RecordClass}; sensitivity={current.Sensitivity}; source={current.SourceType}; "
            + $"author={current.AuthorName}; language={current.LanguageTag}; released by {actor}.";
        var preview = $"Managed record: {current.FileName} ({current.MediaType})";

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            insert into patient_documents (
              id, document_key, patient_id, pid, category_id, category_name, name, doc_date,
              uploaded_at, mimetype, file_name, size_bytes, pages, encounter, storage_method,
              url, hash, documentation_of, notes, review_status, content, content_bytes, deleted)
            values (
              @id, @documentKey, @patientId, @legacyPid, @categoryId, @categoryName, @title,
              @serviceDate, now(), @mediaType, @fileName, @sizeBytes, @pages, @encounter,
              'database', @url, @checksum, @documentationOf, @notes, 'pending', @content,
              @contentBytes, 0);
            """;
        insert.Parameters.AddWithValue("id", documentId);
        insert.Parameters.AddWithValue("documentKey", documentKey);
        insert.Parameters.AddWithValue("patientId", current.PatientId);
        insert.Parameters.AddWithValue("legacyPid", current.LegacyPid);
        insert.Parameters.AddWithValue("categoryId", current.CategoryId);
        insert.Parameters.AddWithValue("categoryName", current.CategoryName);
        insert.Parameters.AddWithValue("title", current.Title);
        insert.Parameters.AddWithValue("serviceDate", DateOnly.Parse(current.ServiceDate));
        insert.Parameters.AddWithValue("mediaType", current.MediaType);
        insert.Parameters.AddWithValue("fileName", current.FileName);
        insert.Parameters.AddWithValue("sizeBytes", current.SizeBytes);
        insert.Parameters.AddWithValue(
            "pages",
            string.Equals(current.MediaType, "application/pdf", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        AddNullableInt(insert, "encounter", current.Encounter);
        insert.Parameters.AddWithValue("url", $"modern://managed-records/{intakeId:N}/{current.FileName}");
        insert.Parameters.AddWithValue("checksum", current.ContentChecksumSha256);
        insert.Parameters.AddWithValue("documentationOf", notes);
        insert.Parameters.AddWithValue("notes", notes);
        insert.Parameters.AddWithValue("content", preview);
        insert.Parameters.Add("contentBytes", NpgsqlTypes.NpgsqlDbType.Bytea).Value = current.ContentBytes;
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return documentId;
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid intakeId,
        string action,
        string? fromState,
        string toState,
        string? fromRecordClass,
        string toRecordClass,
        string? fromSensitivity,
        string toSensitivity,
        string reason,
        string actor,
        int workflowVersion,
        string validationStatus,
        string checksum,
        int? documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into managed_record_intake_events (
              event_id, intake_id, action, from_state, to_state,
              from_record_class, to_record_class, from_sensitivity, to_sensitivity,
              reason, actor, workflow_version, validation_status,
              content_version, content_sha256, document_id)
            values (
              @eventId, @intakeId, @action, @fromState, @toState,
              @fromRecordClass, @toRecordClass, @fromSensitivity, @toSensitivity,
              @reason, @actor, @workflowVersion, @validationStatus,
              1, @checksum, @documentId);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("intakeId", intakeId);
        command.Parameters.AddWithValue("action", action);
        AddNullableText(command, "fromState", fromState);
        command.Parameters.AddWithValue("toState", toState);
        AddNullableText(command, "fromRecordClass", fromRecordClass);
        command.Parameters.AddWithValue("toRecordClass", toRecordClass);
        AddNullableText(command, "fromSensitivity", fromSensitivity);
        command.Parameters.AddWithValue("toSensitivity", toSensitivity);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("workflowVersion", workflowVersion);
        command.Parameters.AddWithValue("validationStatus", validationStatus);
        command.Parameters.AddWithValue("checksum", checksum);
        AddNullableInt(command, "documentId", documentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string PatientId, int LegacyPid)?> GetPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetInt32(1))
            : null;
    }

    private static async Task<bool> EncounterBelongsToPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string patientId,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1 from encounters
              where encounter = @encounter and patient_id = @patientId);
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("patientId", patientId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> ActiveFacilityExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from facilities where id = @id and active);";
        command.Parameters.AddWithValue("id", facilityId);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<(Guid IntakeId, string Fingerprint)?> FindByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string actor,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select intake_id, request_fingerprint
            from managed_record_intakes
            where created_by = @actor and idempotency_key = @idempotencyKey
            limit 1;
            """;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task<ManagedRecordRow?> GetForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid intakeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"{BaseSelectWithContent} where i.intake_id = @intakeId for update of i;";
        command.Parameters.AddWithValue("intakeId", intakeId);
        var items = await ReadRowsAsync(command, cancellationToken);
        return items.SingleOrDefault();
    }

    private static async Task<ManagedRecordItem?> GetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid intakeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"{BaseSelect} where i.intake_id = @intakeId;";
        command.Parameters.AddWithValue("intakeId", intakeId);
        var items = await ReadItemsAsync(command, cancellationToken);
        return items.SingleOrDefault();
    }

    private static async Task<List<ManagedRecordItem>> ReadItemsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken) =>
        (await ReadRowsAsync(command, cancellationToken)).Select(ToItem).ToList();

    private static async Task<List<ManagedRecordRow>> ReadRowsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<ManagedRecordRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ManagedRecordRow(
                reader.GetGuid(reader.GetOrdinal("intake_id")),
                ReadNullableInt32(reader, "document_id"),
                reader.GetString(reader.GetOrdinal("patient_id")),
                reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                reader.GetInt32(reader.GetOrdinal("category_id")),
                reader.GetString(reader.GetOrdinal("category_name")),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetFieldValue<DateOnly>(reader.GetOrdinal("service_date")).ToString("yyyy-MM-dd"),
                ReadNullableInt32(reader, "encounter"),
                reader.GetString(reader.GetOrdinal("record_class")),
                reader.GetString(reader.GetOrdinal("source_type")),
                reader.GetString(reader.GetOrdinal("author_name")),
                ReadNullableInt32(reader, "facility_id"),
                ReadNullableString(reader, "facility_name"),
                reader.GetString(reader.GetOrdinal("sensitivity")),
                reader.GetString(reader.GetOrdinal("language_tag")),
                reader.GetString(reader.GetOrdinal("file_name")),
                reader.GetString(reader.GetOrdinal("media_type")),
                reader.GetInt32(reader.GetOrdinal("size_bytes")),
                reader.GetInt32(reader.GetOrdinal("content_version")),
                reader.GetString(reader.GetOrdinal("content_sha256")),
                reader.GetString(reader.GetOrdinal("storage_adapter")),
                reader.GetString(reader.GetOrdinal("storage_reference")),
                (byte[])reader.GetValue(reader.GetOrdinal("content_bytes")),
                reader.GetString(reader.GetOrdinal("state")),
                reader.GetInt32(reader.GetOrdinal("workflow_version")),
                reader.GetString(reader.GetOrdinal("availability_status")),
                reader.GetString(reader.GetOrdinal("validation_status")),
                reader.GetString(reader.GetOrdinal("validation_adapter")),
                reader.GetBoolean(reader.GetOrdinal("anti_malware_verified")),
                ReadNullableString(reader, "failure_reason"),
                reader.GetString(reader.GetOrdinal("updated_by")),
                reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")).ToString("O"),
                reader.GetString(reader.GetOrdinal("last_reason"))));
        }
        return items;
    }

    private static ManagedRecordItem ToItem(ManagedRecordRow row) =>
        new(
            row.IntakeId,
            row.DocumentId,
            row.PatientId,
            row.LegacyPid,
            row.CategoryId,
            row.CategoryName,
            row.Title,
            row.ServiceDate,
            row.Encounter,
            row.RecordClass,
            row.SourceType,
            row.AuthorName,
            row.FacilityId,
            row.FacilityName,
            row.Sensitivity,
            row.LanguageTag,
            row.FileName,
            row.MediaType,
            row.SizeBytes,
            row.ContentVersion,
            row.ContentChecksumSha256,
            row.StorageAdapter,
            row.StorageReference,
            row.State,
            row.WorkflowVersion,
            row.AvailabilityStatus,
            row.ValidationStatus,
            row.ValidationAdapter,
            row.AntiMalwareVerified,
            row.FailureReason,
            row.LastActor,
            row.LastActionAt,
            row.LastReason,
            false,
            AvailableActions(row.State));

    private static ManagedRecordListResponse BuildList(
        string patientId,
        IReadOnlyList<ManagedRecordItem> items) =>
        new(
            PolicyRevision,
            patientId,
            items.Count,
            new ManagedRecordCounts(
                items.Count(item => item.State == "captured"),
                items.Count(item => item.State == "quarantined"),
                items.Count(item => item.State == "scanning"),
                items.Count(item => item.State == "failed"),
                items.Count(item => item.State == "available"),
                items.Count(item => item.AvailabilityStatus != "available")),
            items);

    private static void EnsureVersion(ManagedRecordRow current, int expectedVersion)
    {
        if (expectedVersion != current.WorkflowVersion)
        {
            throw new ManagedRecordConflictException(current.WorkflowVersion, current.State);
        }
    }

    private static string NormalizeChoice(
        string value,
        IReadOnlyList<string> accepted,
        string label)
    {
        var normalized = RequireText(value, $"{label} is required.", 150).ToLowerInvariant();
        if (!accepted.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"{label} must be one of: {string.Join(", ", accepted)}.");
        }
        return normalized;
    }

    private static string NormalizeLanguage(string value)
    {
        var normalized = RequireText(value, "Language tag is required.", 35);
        if (!Regex.IsMatch(
                normalized,
                "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Language must be a bounded BCP 47-style tag such as en-US.");
        }
        return normalized;
    }

    private static string RequireText(string? value, string message, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new ArgumentException(message);
        }
        return normalized;
    }

    private static string SanitizeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(safe) ? "record.bin" : safe;
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AddCreateParameters(
        NpgsqlCommand command,
        Guid intakeId,
        (string PatientId, int LegacyPid) patient,
        ValidatedCreate input,
        string storageReference,
        string actor)
    {
        command.Parameters.AddWithValue("intakeId", intakeId);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("legacyPid", patient.LegacyPid);
        command.Parameters.AddWithValue("idempotencyKey", input.IdempotencyKey);
        command.Parameters.AddWithValue("fingerprint", input.Fingerprint);
        command.Parameters.AddWithValue("categoryId", input.CategoryId);
        command.Parameters.AddWithValue("categoryName", input.CategoryName);
        command.Parameters.AddWithValue("title", input.Title);
        command.Parameters.AddWithValue("serviceDate", input.ServiceDate);
        AddNullableInt(command, "encounter", input.Encounter);
        command.Parameters.AddWithValue("recordClass", input.RecordClass);
        command.Parameters.AddWithValue("sourceType", input.SourceType);
        command.Parameters.AddWithValue("authorName", input.AuthorName);
        AddNullableInt(command, "facilityId", input.FacilityId);
        command.Parameters.AddWithValue("sensitivity", input.Sensitivity);
        command.Parameters.AddWithValue("languageTag", input.LanguageTag);
        command.Parameters.AddWithValue("fileName", input.FileName);
        command.Parameters.AddWithValue("mediaType", input.MediaType);
        command.Parameters.AddWithValue("sizeBytes", input.ContentBytes.Length);
        command.Parameters.AddWithValue("checksum", input.Checksum);
        command.Parameters.AddWithValue("storageReference", storageReference);
        command.Parameters.Add("contentBytes", NpgsqlTypes.NpgsqlDbType.Bytea).Value = input.ContentBytes;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", input.Reason);
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlTypes.NpgsqlDbType.Integer);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlTypes.NpgsqlDbType.Text);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    private static int? ReadNullableInt32(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private const string BaseSelect = """
        select i.intake_id, i.document_id, i.patient_id, i.legacy_pid,
          i.category_id, i.category_name, i.title, i.service_date, i.encounter,
          i.record_class, i.source_type, i.author_name, i.facility_id, f.name as facility_name,
          i.sensitivity, i.language_tag, i.file_name, i.media_type, i.size_bytes,
          i.content_version, i.content_sha256, i.storage_adapter, i.storage_reference,
          ''::bytea as content_bytes, i.state, i.workflow_version, i.availability_status,
          i.validation_status, i.validation_adapter, i.anti_malware_verified,
          i.failure_reason, i.updated_by, i.updated_at, i.last_reason
        from managed_record_intakes i
        left join facilities f on f.id = i.facility_id
        """;

    private static readonly string BaseSelectWithContent =
        BaseSelect.Replace("''::bytea as content_bytes", "i.content_bytes", StringComparison.Ordinal);

    private sealed record Transition(
        string State,
        string ValidationStatus,
        string AvailabilityStatus);

    private sealed record ValidatedCreate(
        string PatientId,
        int CategoryId,
        string CategoryName,
        string Title,
        DateOnly ServiceDate,
        int? Encounter,
        string RecordClass,
        string SourceType,
        string AuthorName,
        int? FacilityId,
        string Sensitivity,
        string LanguageTag,
        string FileName,
        string MediaType,
        byte[] ContentBytes,
        string Checksum,
        string IdempotencyKey,
        string Fingerprint,
        string Reason);

    private sealed record ManagedRecordRow(
        Guid IntakeId,
        int? DocumentId,
        string PatientId,
        int LegacyPid,
        int CategoryId,
        string CategoryName,
        string Title,
        string ServiceDate,
        int? Encounter,
        string RecordClass,
        string SourceType,
        string AuthorName,
        int? FacilityId,
        string? FacilityName,
        string Sensitivity,
        string LanguageTag,
        string FileName,
        string MediaType,
        int SizeBytes,
        int ContentVersion,
        string ContentChecksumSha256,
        string StorageAdapter,
        string StorageReference,
        byte[] ContentBytes,
        string State,
        int WorkflowVersion,
        string AvailabilityStatus,
        string ValidationStatus,
        string ValidationAdapter,
        bool AntiMalwareVerified,
        string? FailureReason,
        string LastActor,
        string LastActionAt,
        string LastReason);
}
