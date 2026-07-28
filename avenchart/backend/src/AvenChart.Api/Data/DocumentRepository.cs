using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class DocumentVersionConflictException(int currentVersion)
    : Exception($"The document is now at version {currentVersion}.")
{
    public int CurrentVersion { get; } = currentVersion;
}

public sealed class DocumentReviewConflictException(
    string currentStatus,
    string message)
    : Exception(message)
{
    public string CurrentStatus { get; } = currentStatus;
}

public sealed class DocumentArchiveConflictException(
    bool currentArchived,
    string message)
    : Exception(message)
{
    public bool CurrentArchived { get; } = currentArchived;
}

public sealed class DocumentRoutingConflictException(
    int currentTaskVersion,
    string currentStatus,
    string message)
    : Exception(message)
{
    public int CurrentTaskVersion { get; } = currentTaskVersion;
    public string CurrentStatus { get; } = currentStatus;
}

public sealed class DocumentOcrConflictException(
    int currentTaskVersion,
    string currentStatus,
    string message)
    : Exception(message)
{
    public int CurrentTaskVersion { get; } = currentTaskVersion;
    public string CurrentStatus { get; } = currentStatus;
}

public sealed class DocumentRepository(NpgsqlDataSource dataSource)
{
    private const int MaxInlineThumbnailBytes = 262_144;
    public const int MaxOcrExtractedTextCharacters = 262_144;
    public const int MaxBinaryDocumentBytes = 25 * 1024 * 1024;
    private static readonly SemaphoreSlim DocumentMetadataSchemaGate = new(1, 1);
    private static readonly SemaphoreSlim DocumentVersionSchemaGate = new(1, 1);

    private static readonly IReadOnlyList<PatientDocumentCategoryOption> CategoryOptions =
    [
        new(2, "Lab Report"),
        new(3, "Medical Record"),
        new(4, "Patient Information"),
        new(5, "Patient ID card"),
        new(6, "Advance Directive"),
        new(13, "CCDA"),
        new(29, "Reviewed"),
        new(31, "Invoices")
    ];

    public async Task<PatientDocumentsResponse?> GetForPatientAsync(
        string patientId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var documents = await GetDocumentsAsync(connection, patient.PatientId, includeArchived, cancellationToken);
        var (activeCount, archivedCount) = await GetDocumentCountsAsync(
            connection,
            patient.PatientId,
            cancellationToken);

        return new PatientDocumentsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Count: documents.Count,
            ActiveCount: activeCount,
            ArchivedCount: archivedCount,
            IncludesArchived: includeArchived,
            Documents: documents);
    }

    public async Task<PatientDocumentCategoryOptionsResponse> GetCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        return new PatientDocumentCategoryOptionsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            MaxFileSizeBytes: MaxBinaryDocumentBytes,
            Categories: CategoryOptions);
    }

    public async Task<PatientDocumentOcrQueueResponse> GetOcrQueueAsync(
        CancellationToken cancellationToken,
        string? patientId = null,
        string? status = null,
        string? priority = null,
        string? query = null,
        int offset = 0,
        int limit = 1_000)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeOcrStatusFilter(status);
        var normalizedPriority = NormalizeOcrPriorityFilter(priority);
        var normalizedQuery = NormalizeText(query);
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, 1_000);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select d.id, d.document_key, d.patient_id, d.pid, p.pubpid, p.first_name, p.last_name, p.preferred_name,
              d.category_id, d.category_name, d.name, d.doc_date, d.uploaded_at, d.mimetype, d.file_name, d.pages,
              d.encounter, d.storage_method, d.notes, coalesce(d.review_status, 'pending') as review_status,
              coalesce((select count(*) from patient_document_versions v where v.document_id = d.id), 0) + 1
                as document_version,
              t.task_version, t.status as task_status, t.priority as task_priority,
              t.extracted_text, t.failure_reason,
              t.started_by, t.started_at, t.completed_by, t.completed_at,
              t.failed_by, t.failed_at, t.updated_at,
              case
                when d.content_bytes is not null then left(coalesce(d.content, ''), 260)
                else left(regexp_replace(coalesce(d.content, ''), E'[\\r\\n]+', ' ', 'g'), 260)
              end as content_preview
            from patient_documents d
            join patients p on p.canonical_id = d.patient_id
            left join patient_document_ocr_tasks t on t.document_id = d.id
            where d.deleted = 0
              and (@patientId is null
                   or lower(d.patient_id) = lower(@patientId)
                   or lower(p.pubpid) = lower(@patientId)
                   or d.pid::text = @patientId)
            order by d.uploaded_at, d.id;
            """;
        var patientParameter = command.Parameters.Add("patientId", NpgsqlTypes.NpgsqlDbType.Text);
        patientParameter.Value = string.IsNullOrWhiteSpace(patientId) ? DBNull.Value : patientId.Trim();

        var items = new List<PatientDocumentOcrQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(reader.GetOrdinal("name"));
            var fileName = ReadNullableString(reader, "file_name");
            var mimetype = ReadNullableString(reader, "mimetype");
            var notes = ReadNullableString(reader, "notes");
            var pages = ReadNullableInt32(reader, "pages");
            var scanReadiness = BuildScanReadiness(
                name,
                fileName,
                mimetype,
                pages,
                ReadNullableString(reader, "storage_method"),
                notes,
                ReadNullableString(reader, "content_preview"));

            if (!scanReadiness.IsScannedAttachment)
            {
                continue;
            }

            var inferred = reader.IsDBNull(reader.GetOrdinal("task_version"));
            var taskStatus = inferred
                ? NormalizeInferredOcrStatus(scanReadiness.OcrStatus)
                : reader.GetString(reader.GetOrdinal("task_status"));
            if (taskStatus is null)
            {
                continue;
            }

            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var preferredName = ReadNullableString(reader, "preferred_name");
            var scanPageCount = scanReadiness.ScanPageCount;
            var queueStatus = OcrQueueStatus(taskStatus);
            var ocrStatus = OcrStatusLabel(taskStatus);
            var taskPriority = inferred
                ? scanPageCount >= 5 ? "High" : "Standard"
                : reader.GetString(reader.GetOrdinal("task_priority"));
            var uploadedAt = new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("uploaded_at")),
                DateTimeKind.Utc));
            var lastUpdatedAt = inferred
                ? uploadedAt
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at"));
            var extractedText = ReadNullableString(reader, "extracted_text");
            items.Add(new PatientDocumentOcrQueueItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                PatientDisplayName: string.IsNullOrWhiteSpace(preferredName)
                    ? $"{lastName}, {firstName}"
                    : $"{lastName}, {firstName} ({preferredName})",
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
                Name: name,
                DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
                UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                Mimetype: mimetype,
                FileName: fileName,
                Pages: pages,
                Encounter: ReadNullableInt32(reader, "encounter"),
                CaptureSource: scanReadiness.CaptureSource,
                ScanPageCount: scanPageCount,
                OcrStatus: ocrStatus,
                QueueStatus: queueStatus,
                Priority: taskPriority,
                TaskVersion: inferred ? 0 : reader.GetInt32(reader.GetOrdinal("task_version")),
                Inferred: inferred,
                AgeHours: Math.Max(0, (int)Math.Floor((now - lastUpdatedAt).TotalHours)),
                LastUpdatedAt: lastUpdatedAt.ToString("O"),
                StartedBy: ReadNullableString(reader, "started_by"),
                StartedAt: ReadNullableDateTimeOffset(reader, "started_at")?.ToString("O"),
                CompletedBy: ReadNullableString(reader, "completed_by"),
                CompletedAt: ReadNullableDateTimeOffset(reader, "completed_at")?.ToString("O"),
                FailedBy: ReadNullableString(reader, "failed_by"),
                FailedAt: ReadNullableDateTimeOffset(reader, "failed_at")?.ToString("O"),
                FailureReason: ReadNullableString(reader, "failure_reason"),
                ExtractedTextLength: extractedText?.Length ?? 0,
                ExtractedTextPreview: BuildOcrTextPreview(extractedText),
                DocumentVersion: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("document_version"))),
                ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                Notes: notes));
        }

        var counts = new PatientDocumentOcrQueueCounts(
            Active: items.Count(item => item.QueueStatus != "OCR complete"),
            Queued: items.Count(item => item.QueueStatus == "Ready for OCR"),
            Running: items.Count(item => item.QueueStatus == "OCR running"),
            Failed: items.Count(item => item.QueueStatus == "OCR failed"),
            HighPriority: items.Count(item => item.QueueStatus != "OCR complete"
                && item.Priority == "High"),
            Completed: items.Count(item => item.QueueStatus == "OCR complete"));
        var filtered = items
            .Where(item => normalizedStatus switch
            {
                "active" => item.QueueStatus != "OCR complete",
                "queued" => item.QueueStatus == "Ready for OCR",
                "running" => item.QueueStatus == "OCR running",
                "failed" => item.QueueStatus == "OCR failed",
                "completed" => item.QueueStatus == "OCR complete",
                _ => true
            })
            .Where(item => normalizedPriority is null || item.Priority == normalizedPriority)
            .Where(item => normalizedQuery is null
                || ContainsIgnoreCase(item.Name, normalizedQuery)
                || ContainsIgnoreCase(item.PatientDisplayName, normalizedQuery)
                || ContainsIgnoreCase(item.Pubpid, normalizedQuery)
                || ContainsIgnoreCase(item.CategoryName, normalizedQuery)
                || ContainsIgnoreCase(item.CaptureSource, normalizedQuery)
                || ContainsIgnoreCase(item.ExtractedTextPreview, normalizedQuery)
                || ContainsIgnoreCase(item.FailureReason, normalizedQuery))
            .ToList();
        var page = filtered.Skip(offset).Take(limit).ToList();

        return new PatientDocumentOcrQueueResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Count: filtered.Count,
            TotalCount: filtered.Count,
            ReturnedCount: page.Count,
            Offset: offset,
            Limit: limit,
            StatusFilter: normalizedStatus,
            Counts: counts,
            Items: page);
    }

    public async Task<PatientDocumentRoutingQueueResponse> GetRoutingQueueAsync(
        CancellationToken cancellationToken,
        string? patientId = null,
        string? status = null,
        string? priority = null,
        string? assignedTo = null,
        int? minimumAgeHours = null,
        string? query = null,
        int offset = 0,
        int limit = 50)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeRoutingStatusFilter(status);
        var normalizedPriority = NormalizeRoutingPriorityFilter(priority);
        var normalizedAssignee = NormalizeText(assignedTo);
        var normalizedQuery = NormalizeText(query);
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, 100);
        var normalizedMinimumAgeHours = Math.Clamp(minimumAgeHours ?? 0, 0, 87_600);
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select d.id, d.document_key, d.patient_id, d.pid, p.pubpid, p.first_name, p.last_name, p.preferred_name,
              d.category_id, d.category_name, d.name, d.doc_date, d.uploaded_at, d.mimetype, d.file_name,
              d.encounter, d.notes, coalesce(d.review_status, 'pending') as review_status,
              t.task_version, t.status as task_status, t.destination, t.priority, t.assigned_to,
              t.routing_reason, t.routed_at, t.due_at, t.completed_by, t.completed_at, t.completion_note,
              a.display_name as assignee_display_name
            from patient_documents d
            join patients p on p.canonical_id = d.patient_id
            left join patient_document_routing_tasks t on t.document_id = d.id
            left join auth_accounts a on lower(a.username) = lower(t.assigned_to)
            where d.deleted = 0
              and (t.document_id is not null or lower(coalesce(d.review_status, 'pending')) = 'pending')
              and (@patientId is null
                   or lower(d.patient_id) = lower(@patientId)
                   or lower(p.pubpid) = lower(@patientId)
                   or d.pid::text = @patientId)
            order by coalesce(t.due_at, d.uploaded_at + interval '3 days'), d.uploaded_at, d.id;
            """;
        var patientParameter = command.Parameters.Add("patientId", NpgsqlTypes.NpgsqlDbType.Text);
        patientParameter.Value = string.IsNullOrWhiteSpace(patientId) ? DBNull.Value : patientId.Trim();

        var allItems = new List<PatientDocumentRoutingQueueItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var preferredName = ReadNullableString(reader, "preferred_name");
            var patientDisplayName = string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})";
            var categoryName = reader.GetString(reader.GetOrdinal("category_name"));
            var notes = ReadNullableString(reader, "notes");
            var inferred = reader.IsDBNull(reader.GetOrdinal("task_version"));
            var routeDestination = inferred
                ? ExtractTaggedValue(notes, "Route to") ?? BuildRouteDestination(categoryName)
                : reader.GetString(reader.GetOrdinal("destination"));
            var routePriority = inferred
                ? ExtractTaggedValue(notes, "Routing priority") ?? BuildRoutingPriority(categoryName, notes)
                : reader.GetString(reader.GetOrdinal("priority"));
            var taskStatus = inferred
                ? "pending"
                : reader.GetString(reader.GetOrdinal("task_status"));
            var uploadedAt = DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("uploaded_at")),
                DateTimeKind.Utc);
            var routedAt = inferred
                ? new DateTimeOffset(uploadedAt)
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("routed_at"));
            var dueAt = inferred
                ? routedAt.AddHours(string.Equals(routePriority, "High", StringComparison.OrdinalIgnoreCase) ? 24 : 72)
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("due_at"));
            var ageHours = Math.Max(0, (int)Math.Floor((now - routedAt).TotalHours));
            var assignedUsername = ReadNullableString(reader, "assigned_to");
            var assignedDisplayName = assignedUsername is null
                ? null
                : ReadNullableString(reader, "assignee_display_name") is { } displayName
                    ? $"{displayName} ({assignedUsername})"
                    : assignedUsername;
            var queueStatus = inferred
                ? "Awaiting review"
                : taskStatus switch
                {
                    "pending" => "Awaiting assignment",
                    "in_progress" => "In progress",
                    "completed" => "Completed",
                    _ => taskStatus
                };

            allItems.Add(new PatientDocumentRoutingQueueItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                PatientDisplayName: patientDisplayName,
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: categoryName,
                Name: reader.GetString(reader.GetOrdinal("name")),
                DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
                UploadedAt: uploadedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                Mimetype: ReadNullableString(reader, "mimetype"),
                FileName: ReadNullableString(reader, "file_name"),
                Encounter: ReadNullableInt32(reader, "encounter"),
                ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                QueueStatus: queueStatus,
                RouteDestination: routeDestination,
                Priority: routePriority,
                RoutingReason: inferred
                    ? $"Pending {categoryName} review"
                    : reader.GetString(reader.GetOrdinal("routing_reason")),
                TaskVersion: inferred ? 0 : reader.GetInt32(reader.GetOrdinal("task_version")),
                Inferred: inferred,
                AssignedTo: assignedUsername,
                AssignedDisplayName: assignedDisplayName,
                RoutedAt: routedAt.ToString("O"),
                DueAt: dueAt.ToString("O"),
                AgeHours: ageHours,
                IsOverdue: taskStatus != "completed" && dueAt < now,
                CompletedBy: ReadNullableString(reader, "completed_by"),
                CompletedAt: ReadNullableDateTimeOffset(reader, "completed_at")?.ToString("O"),
                CompletionNote: ReadNullableString(reader, "completion_note"),
                Notes: notes));
        }

        var counts = new PatientDocumentRoutingQueueCounts(
            Active: allItems.Count(item => item.QueueStatus != "Completed"),
            Pending: allItems.Count(item => item.TaskVersion == 0 || item.QueueStatus == "Awaiting assignment"),
            InProgress: allItems.Count(item => item.QueueStatus == "In progress"),
            Unassigned: allItems.Count(item => item.QueueStatus != "Completed" && item.AssignedTo is null),
            HighPriority: allItems.Count(item => item.QueueStatus != "Completed"
                && string.Equals(item.Priority, "High", StringComparison.OrdinalIgnoreCase)),
            Overdue: allItems.Count(item => item.IsOverdue),
            Completed: allItems.Count(item => item.QueueStatus == "Completed"));

        var filtered = allItems
            .Where(item => normalizedStatus switch
            {
                "active" => item.QueueStatus != "Completed",
                "pending" => item.TaskVersion == 0 || item.QueueStatus == "Awaiting assignment",
                "in_progress" => item.QueueStatus == "In progress",
                "completed" => item.QueueStatus == "Completed",
                _ => true
            })
            .Where(item => normalizedPriority is null
                || string.Equals(item.Priority, normalizedPriority, StringComparison.OrdinalIgnoreCase))
            .Where(item => normalizedAssignee is null
                || (string.Equals(normalizedAssignee, "unassigned", StringComparison.OrdinalIgnoreCase)
                    ? item.AssignedTo is null
                    : string.Equals(item.AssignedTo, normalizedAssignee, StringComparison.OrdinalIgnoreCase)))
            .Where(item => item.AgeHours >= normalizedMinimumAgeHours)
            .Where(item => normalizedQuery is null
                || ContainsIgnoreCase(item.Name, normalizedQuery)
                || ContainsIgnoreCase(item.PatientDisplayName, normalizedQuery)
                || ContainsIgnoreCase(item.Pubpid, normalizedQuery)
                || ContainsIgnoreCase(item.CategoryName, normalizedQuery)
                || ContainsIgnoreCase(item.RouteDestination, normalizedQuery)
                || ContainsIgnoreCase(item.AssignedDisplayName, normalizedQuery))
            .ToList();
        var page = filtered.Skip(offset).Take(limit).ToList();

        return new PatientDocumentRoutingQueueResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Count: filtered.Count,
            TotalCount: filtered.Count,
            ReturnedCount: page.Count,
            Offset: offset,
            Limit: limit,
            StatusFilter: normalizedStatus,
            Counts: counts,
            Items: page);
    }

    public async Task<PatientDocumentRoutingAssigneesResponse> GetRoutingAssigneesAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select staff_id, username, display_name, role
            from auth_accounts
            where active = true
            order by display_name, username;
            """;
        var assignees = new List<PatientDocumentRoutingAssignee>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assignees.Add(new PatientDocumentRoutingAssignee(
                StaffId: ReadNullableInt32(reader, "staff_id"),
                Username: reader.GetString(reader.GetOrdinal("username")),
                DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
                Role: reader.GetString(reader.GetOrdinal("role"))));
        }

        return new PatientDocumentRoutingAssigneesResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Count: assignees.Count,
            Assignees: assignees);
    }

    public async Task<PatientDocumentRoutingHistoryResponse?> GetRoutingHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);

        string documentKey;
        string patientId;
        int legacyPid;
        string name;
        string categoryName;
        string? notes;
        DateTimeOffset uploadedAt;
        int currentTaskVersion;
        string currentStatus;
        string? currentAssignedTo;
        string? currentDestination;
        string? currentPriority;
        string? currentDueAt;

        await using (var current = connection.CreateCommand())
        {
            current.CommandText = """
                select d.document_key, d.patient_id, d.pid, d.name, d.category_name, d.notes, d.uploaded_at,
                  t.task_version, t.status, t.assigned_to, t.destination, t.priority, t.due_at
                from patient_documents d
                left join patient_document_routing_tasks t on t.document_id = d.id
                where d.id = @documentId;
                """;
            current.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await current.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            documentKey = reader.GetString(reader.GetOrdinal("document_key"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            name = reader.GetString(reader.GetOrdinal("name"));
            categoryName = reader.GetString(reader.GetOrdinal("category_name"));
            notes = ReadNullableString(reader, "notes");
            uploadedAt = new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("uploaded_at")),
                DateTimeKind.Utc));
            var inferred = reader.IsDBNull(reader.GetOrdinal("task_version"));
            currentTaskVersion = inferred ? 0 : reader.GetInt32(reader.GetOrdinal("task_version"));
            currentStatus = inferred ? "pending" : reader.GetString(reader.GetOrdinal("status"));
            currentAssignedTo = ReadNullableString(reader, "assigned_to");
            currentDestination = inferred
                ? ExtractTaggedValue(notes, "Route to") ?? BuildRouteDestination(categoryName)
                : reader.GetString(reader.GetOrdinal("destination"));
            currentPriority = inferred
                ? ExtractTaggedValue(notes, "Routing priority") ?? BuildRoutingPriority(categoryName, notes)
                : reader.GetString(reader.GetOrdinal("priority"));
            currentDueAt = inferred
                ? uploadedAt.AddHours(string.Equals(currentPriority, "High", StringComparison.OrdinalIgnoreCase) ? 24 : 72).ToString("O")
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("due_at")).ToString("O");
        }

        var events = new List<PatientDocumentRoutingEvent>();
        var eventCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  count(*) over() as event_count,
                  event_id, action, from_status, to_status,
                  from_destination, to_destination, from_priority, to_priority,
                  from_assigned_to, to_assigned_to, reason, actor, occurred_at,
                  due_at, task_version, document_version, review_status, content_hash
                from patient_document_routing_events
                where document_id = @documentId
                order by occurred_at desc, event_id desc
                limit 100;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
                events.Add(new PatientDocumentRoutingEvent(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    Action: reader.GetString(reader.GetOrdinal("action")),
                    FromStatus: reader.GetString(reader.GetOrdinal("from_status")),
                    ToStatus: reader.GetString(reader.GetOrdinal("to_status")),
                    FromDestination: ReadNullableString(reader, "from_destination"),
                    ToDestination: reader.GetString(reader.GetOrdinal("to_destination")),
                    FromPriority: ReadNullableString(reader, "from_priority"),
                    ToPriority: reader.GetString(reader.GetOrdinal("to_priority")),
                    FromAssignedTo: ReadNullableString(reader, "from_assigned_to"),
                    ToAssignedTo: ReadNullableString(reader, "to_assigned_to"),
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at")).ToString("O"),
                    DueAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("due_at")).ToString("O"),
                    TaskVersion: reader.GetInt32(reader.GetOrdinal("task_version")),
                    DocumentVersion: reader.GetInt32(reader.GetOrdinal("document_version")),
                    ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                    ContentHash: ReadNullableString(reader, "content_hash")));
            }
        }

        return new PatientDocumentRoutingHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            CurrentTaskVersion: currentTaskVersion,
            CurrentStatus: currentStatus,
            CurrentAssignedTo: currentAssignedTo,
            CurrentDestination: currentDestination,
            CurrentPriority: currentPriority,
            CurrentDueAt: currentDueAt,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: 100,
            Events: events);
    }

    public async Task<PatientDocumentRoutingMutationResponse?> RouteDocumentAsync(
        int documentId,
        PatientDocumentRoutingMutationRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var destination = NormalizeRequiredRoutingText(request.Destination, "A routing destination", 100);
        var routePriority = NormalizeRoutingPriority(request.Priority);
        var reason = NormalizeRequiredRoutingText(request.Reason, "A routing reason", 250);
        var assignedTo = NormalizeText(request.AssignedTo);
        var dueAt = ParseRoutingDueAt(request.DueAt)
            ?? DateTimeOffset.UtcNow.AddHours(routePriority == "High" ? 24 : 72);
        if (dueAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("The routing due time must be in the future.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetRoutingDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (document.Archived)
        {
            throw new ArgumentException("Archived documents must be restored before they can be routed.");
        }

        await ValidateRoutingAssigneeAsync(
            connection,
            transaction,
            assignedTo,
            cancellationToken);
        var currentTask = await GetRoutingTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = currentTask?.Status ?? "pending";
        if (request.ExpectedTaskVersion != currentVersion)
        {
            throw new DocumentRoutingConflictException(
                currentVersion,
                currentStatus,
                $"The routing task is now at version {currentVersion} with status {currentStatus}.");
        }

        var nextStatus = assignedTo is null ? "pending" : "in_progress";
        var inferredDestination = ExtractTaggedValue(document.Notes, "Route to")
            ?? BuildRouteDestination(document.CategoryName);
        var inferredPriority = ExtractTaggedValue(document.Notes, "Routing priority")
            ?? BuildRoutingPriority(document.CategoryName, document.Notes);
        var fromDestination = currentTask?.Destination ?? inferredDestination;
        var fromPriority = currentTask?.Priority ?? inferredPriority;
        var fromAssignedTo = currentTask?.AssignedTo;
        if (currentTask is not null
            && currentTask.Status != "completed"
            && string.Equals(currentTask.Destination, destination, StringComparison.Ordinal)
            && string.Equals(currentTask.Priority, routePriority, StringComparison.Ordinal)
            && string.Equals(currentTask.AssignedTo, assignedTo, StringComparison.OrdinalIgnoreCase)
            && currentTask.DueAt == dueAt)
        {
            throw new ArgumentException("The routing task already has these destination, priority, assignee, and due-time values.");
        }

        var nextVersion = currentVersion + 1;
        var action = currentTask is null
            ? "routed"
            : currentTask.Status == "completed"
                ? "reopened"
                : "rerouted";
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_document_routing_tasks (
                  document_id, task_version, status, destination, priority, assigned_to,
                  routing_reason, routed_by, routed_at, due_at,
                  completed_by, completed_at, completion_note)
                values (
                  @documentId, @taskVersion, @status, @destination, @priority, @assignedTo,
                  @reason, @actor, now(), @dueAt,
                  null, null, null)
                on conflict (document_id) do update set
                  task_version = excluded.task_version,
                  status = excluded.status,
                  destination = excluded.destination,
                  priority = excluded.priority,
                  assigned_to = excluded.assigned_to,
                  routing_reason = excluded.routing_reason,
                  routed_by = excluded.routed_by,
                  routed_at = excluded.routed_at,
                  due_at = excluded.due_at,
                  completed_by = null,
                  completed_at = null,
                  completion_note = null;

                insert into patient_document_routing_events (
                  event_id, document_id, document_key, patient_id, legacy_pid,
                  action, from_status, to_status,
                  from_destination, to_destination, from_priority, to_priority,
                  from_assigned_to, to_assigned_to,
                  reason, actor, occurred_at, due_at, task_version,
                  document_version, review_status, content_hash)
                values (
                  @eventId, @documentId, @documentKey, @patientId, @legacyPid,
                  @action, @fromStatus, @toStatus,
                  @fromDestination, @toDestination, @fromPriority, @toPriority,
                  @fromAssignedTo, @toAssignedTo,
                  @reason, @actor, now(), @dueAt, @taskVersion,
                  @documentVersion, @reviewStatus, @contentHash);
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("taskVersion", nextVersion);
            command.Parameters.AddWithValue("status", nextStatus);
            command.Parameters.AddWithValue("destination", destination);
            command.Parameters.AddWithValue("priority", routePriority);
            command.Parameters.AddWithValue("assignedTo", (object?)assignedTo ?? DBNull.Value);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("dueAt", dueAt);
            command.Parameters.AddWithValue("eventId", Guid.NewGuid());
            command.Parameters.AddWithValue("documentKey", document.DocumentKey);
            command.Parameters.AddWithValue("patientId", document.PatientId);
            command.Parameters.AddWithValue("legacyPid", document.LegacyPid);
            command.Parameters.AddWithValue("action", action);
            command.Parameters.AddWithValue("fromStatus", currentTask is null ? "inferred" : currentStatus);
            command.Parameters.AddWithValue("toStatus", nextStatus);
            command.Parameters.AddWithValue("fromDestination", (object?)fromDestination ?? DBNull.Value);
            command.Parameters.AddWithValue("toDestination", destination);
            command.Parameters.AddWithValue("fromPriority", (object?)fromPriority ?? DBNull.Value);
            command.Parameters.AddWithValue("toPriority", routePriority);
            command.Parameters.AddWithValue("fromAssignedTo", (object?)fromAssignedTo ?? DBNull.Value);
            command.Parameters.AddWithValue("toAssignedTo", (object?)assignedTo ?? DBNull.Value);
            command.Parameters.AddWithValue("documentVersion", document.DocumentVersion);
            command.Parameters.AddWithValue("reviewStatus", document.ReviewStatus);
            command.Parameters.AddWithValue("contentHash", (object?)document.ContentHash ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new PatientDocumentRoutingMutationResponse(
            DocumentId: documentId,
            TaskVersion: nextVersion,
            Status: nextStatus,
            AssignedTo: assignedTo,
            Destination: destination,
            Priority: routePriority,
            DueAt: dueAt.ToString("O"));
    }

    public async Task<PatientDocumentRoutingMutationResponse?> CompleteRoutingAsync(
        int documentId,
        PatientDocumentRoutingCompleteRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeRequiredRoutingText(request.Reason, "A completion note", 250);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetRoutingDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (document.Archived)
        {
            throw new ArgumentException("Archived documents must be restored before routing can be completed.");
        }

        var currentTask = await GetRoutingTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = currentTask?.Status ?? "pending";
        if (request.ExpectedTaskVersion != currentVersion)
        {
            throw new DocumentRoutingConflictException(
                currentVersion,
                currentStatus,
                $"The routing task is now at version {currentVersion} with status {currentStatus}.");
        }

        if (currentStatus == "completed")
        {
            throw new DocumentRoutingConflictException(
                currentVersion,
                currentStatus,
                "This routing task is already completed.");
        }

        var destination = currentTask?.Destination
            ?? ExtractTaggedValue(document.Notes, "Route to")
            ?? BuildRouteDestination(document.CategoryName);
        var routePriority = currentTask?.Priority
            ?? ExtractTaggedValue(document.Notes, "Routing priority")
            ?? BuildRoutingPriority(document.CategoryName, document.Notes);
        var assignedTo = currentTask?.AssignedTo;
        var routedAt = currentTask?.RoutedAt ?? document.UploadedAt;
        var dueAt = currentTask?.DueAt
            ?? routedAt.AddHours(routePriority == "High" ? 24 : 72);
        var routingReason = currentTask?.RoutingReason
            ?? $"Pending {document.CategoryName} review";
        var routedBy = currentTask?.RoutedBy ?? actor;
        var nextVersion = currentVersion + 1;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_document_routing_tasks (
                  document_id, task_version, status, destination, priority, assigned_to,
                  routing_reason, routed_by, routed_at, due_at,
                  completed_by, completed_at, completion_note)
                values (
                  @documentId, @taskVersion, 'completed', @destination, @priority, @assignedTo,
                  @routingReason, @routedBy, @routedAt, @dueAt,
                  @actor, now(), @reason)
                on conflict (document_id) do update set
                  task_version = excluded.task_version,
                  status = 'completed',
                  completed_by = excluded.completed_by,
                  completed_at = excluded.completed_at,
                  completion_note = excluded.completion_note;

                insert into patient_document_routing_events (
                  event_id, document_id, document_key, patient_id, legacy_pid,
                  action, from_status, to_status,
                  from_destination, to_destination, from_priority, to_priority,
                  from_assigned_to, to_assigned_to,
                  reason, actor, occurred_at, due_at, task_version,
                  document_version, review_status, content_hash)
                values (
                  @eventId, @documentId, @documentKey, @patientId, @legacyPid,
                  'completed', @fromStatus, 'completed',
                  @destination, @destination, @priority, @priority,
                  @assignedTo, @assignedTo,
                  @reason, @actor, now(), @dueAt, @taskVersion,
                  @documentVersion, @reviewStatus, @contentHash);
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("taskVersion", nextVersion);
            command.Parameters.AddWithValue("destination", destination);
            command.Parameters.AddWithValue("priority", routePriority);
            command.Parameters.AddWithValue("assignedTo", (object?)assignedTo ?? DBNull.Value);
            command.Parameters.AddWithValue("routingReason", routingReason);
            command.Parameters.AddWithValue("routedBy", routedBy);
            command.Parameters.AddWithValue("routedAt", routedAt);
            command.Parameters.AddWithValue("dueAt", dueAt);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("eventId", Guid.NewGuid());
            command.Parameters.AddWithValue("documentKey", document.DocumentKey);
            command.Parameters.AddWithValue("patientId", document.PatientId);
            command.Parameters.AddWithValue("legacyPid", document.LegacyPid);
            command.Parameters.AddWithValue("fromStatus", currentTask is null ? "inferred" : currentStatus);
            command.Parameters.AddWithValue("documentVersion", document.DocumentVersion);
            command.Parameters.AddWithValue("reviewStatus", document.ReviewStatus);
            command.Parameters.AddWithValue("contentHash", (object?)document.ContentHash ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new PatientDocumentRoutingMutationResponse(
            DocumentId: documentId,
            TaskVersion: nextVersion,
            Status: "completed",
            AssignedTo: assignedTo,
            Destination: destination,
            Priority: routePriority,
            DueAt: dueAt.ToString("O"));
    }

    public async Task<PatientDocumentRetentionPolicyResponse> GetRetentionPolicyAsync(
        CancellationToken cancellationToken,
        string? patientId = null)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select d.id, d.document_key, d.patient_id, d.pid, p.pubpid, p.first_name, p.last_name, p.preferred_name,
              d.category_id, d.category_name, d.name, d.doc_date, d.uploaded_at, d.mimetype, d.file_name,
              d.encounter, d.notes
            from patient_documents d
            join patients p on p.canonical_id = d.patient_id
            where d.deleted = 0
              and (@patientId is null
                   or lower(d.patient_id) = lower(@patientId)
                   or lower(p.pubpid) = lower(@patientId)
                   or d.pid::text = @patientId)
            order by d.doc_date, d.id;
            """;
        var patientParameter = command.Parameters.Add("patientId", NpgsqlTypes.NpgsqlDbType.Text);
        patientParameter.Value = string.IsNullOrWhiteSpace(patientId) ? DBNull.Value : patientId.Trim();

        var items = new List<PatientDocumentRetentionPolicyItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var preferredName = ReadNullableString(reader, "preferred_name");
            var categoryName = reader.GetString(reader.GetOrdinal("category_name"));
            var notes = ReadNullableString(reader, "notes");
            var documentDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date"));
            var retentionYears = BuildRetentionYears(categoryName, notes);
            var retainUntil = documentDate.AddYears(retentionYears);
            var retentionClass = ExtractTaggedValue(notes, "Retention class") ?? BuildRetentionClass(categoryName);
            var policyBasis = ExtractTaggedValue(notes, "Retention basis")
                ?? $"{categoryName} documents retained for {retentionYears} year{(retentionYears == 1 ? string.Empty : "s")}";
            var dispositionStatus = retainUntil <= metadata.BaseDate ? "Eligible for disposition" : "Retain";

            items.Add(new PatientDocumentRetentionPolicyItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                PatientDisplayName: string.IsNullOrWhiteSpace(preferredName)
                    ? $"{lastName}, {firstName}"
                    : $"{lastName}, {firstName} ({preferredName})",
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: categoryName,
                Name: reader.GetString(reader.GetOrdinal("name")),
                DocDate: documentDate.ToString("yyyy-MM-dd"),
                UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                Mimetype: ReadNullableString(reader, "mimetype"),
                FileName: ReadNullableString(reader, "file_name"),
                Encounter: ReadNullableInt32(reader, "encounter"),
                RetentionClass: retentionClass,
                RetentionYears: retentionYears,
                RetainUntil: retainUntil.ToString("yyyy-MM-dd"),
                DispositionStatus: dispositionStatus,
                PolicyBasis: policyBasis,
                Notes: notes));
        }

        return new PatientDocumentRetentionPolicyResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Count: items.Count,
            EligibleCount: items.Count(item => item.DispositionStatus == "Eligible for disposition"),
            Items: items);
    }

    public async Task<PatientDocumentOcrHistoryResponse?> GetOcrHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);

        string documentKey;
        string patientId;
        int legacyPid;
        string name;
        int currentTaskVersion;
        string currentStatus;
        string currentOcrStatus;
        string? currentExtractedText;
        string? currentFailureReason;
        string? currentStartedBy;
        string? currentStartedAt;
        string? currentCompletedBy;
        string? currentCompletedAt;
        string? currentFailedBy;
        string? currentFailedAt;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select d.document_key, d.patient_id, d.pid, d.name, d.file_name, d.mimetype, d.pages,
                  d.storage_method, d.notes, d.content,
                  t.task_version, t.status, t.extracted_text, t.failure_reason,
                  t.started_by, t.started_at, t.completed_by, t.completed_at,
                  t.failed_by, t.failed_at
                from patient_documents d
                left join patient_document_ocr_tasks t on t.document_id = d.id
                where d.id = @documentId;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            documentKey = reader.GetString(reader.GetOrdinal("document_key"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            name = reader.GetString(reader.GetOrdinal("name"));
            var scanReadiness = BuildScanReadiness(
                name,
                ReadNullableString(reader, "file_name"),
                ReadNullableString(reader, "mimetype"),
                ReadNullableInt32(reader, "pages"),
                ReadNullableString(reader, "storage_method"),
                ReadNullableString(reader, "notes"),
                ReadNullableString(reader, "content"));
            if (!scanReadiness.IsScannedAttachment)
            {
                throw new ArgumentException("OCR lifecycle is available only for scanned document attachments.");
            }

            var inferred = reader.IsDBNull(reader.GetOrdinal("task_version"));
            currentTaskVersion = inferred ? 0 : reader.GetInt32(reader.GetOrdinal("task_version"));
            currentStatus = inferred
                ? NormalizeInferredOcrStatus(scanReadiness.OcrStatus) ?? "queued"
                : reader.GetString(reader.GetOrdinal("status"));
            currentOcrStatus = OcrStatusLabel(currentStatus);
            currentExtractedText = inferred
                ? ExtractOcrText(ReadNullableString(reader, "content"))
                : ReadNullableString(reader, "extracted_text");
            currentFailureReason = ReadNullableString(reader, "failure_reason");
            currentStartedBy = ReadNullableString(reader, "started_by");
            currentStartedAt = ReadNullableDateTimeOffset(reader, "started_at")?.ToString("O");
            currentCompletedBy = ReadNullableString(reader, "completed_by");
            currentCompletedAt = ReadNullableDateTimeOffset(reader, "completed_at")?.ToString("O");
            currentFailedBy = ReadNullableString(reader, "failed_by");
            currentFailedAt = ReadNullableDateTimeOffset(reader, "failed_at")?.ToString("O");
        }

        var events = new List<PatientDocumentOcrEvent>();
        var eventCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select count(*) over() as event_count,
                  event_id, action, from_status, to_status, reason, actor, occurred_at,
                  task_version, document_version, review_status,
                  from_extracted_text_length, to_extracted_text_length,
                  from_extracted_text_preview, to_extracted_text_preview,
                  from_extracted_text_hash, to_extracted_text_hash, failure_reason
                from patient_document_ocr_events
                where document_id = @documentId
                order by occurred_at desc, event_id desc
                limit 100;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                eventCount = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("event_count")));
                events.Add(new PatientDocumentOcrEvent(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    Action: reader.GetString(reader.GetOrdinal("action")),
                    FromStatus: reader.GetString(reader.GetOrdinal("from_status")),
                    ToStatus: reader.GetString(reader.GetOrdinal("to_status")),
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at")).ToString("O"),
                    TaskVersion: reader.GetInt32(reader.GetOrdinal("task_version")),
                    DocumentVersion: reader.GetInt32(reader.GetOrdinal("document_version")),
                    ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                    FromExtractedTextLength: reader.GetInt32(reader.GetOrdinal("from_extracted_text_length")),
                    ToExtractedTextLength: reader.GetInt32(reader.GetOrdinal("to_extracted_text_length")),
                    FromExtractedTextPreview: ReadNullableString(reader, "from_extracted_text_preview"),
                    ToExtractedTextPreview: ReadNullableString(reader, "to_extracted_text_preview"),
                    FromExtractedTextHash: ReadNullableString(reader, "from_extracted_text_hash"),
                    ToExtractedTextHash: ReadNullableString(reader, "to_extracted_text_hash"),
                    FailureReason: ReadNullableString(reader, "failure_reason")));
            }
        }

        return new PatientDocumentOcrHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            CurrentTaskVersion: currentTaskVersion,
            CurrentStatus: currentStatus,
            CurrentOcrStatus: currentOcrStatus,
            CurrentExtractedText: currentExtractedText,
            CurrentFailureReason: currentFailureReason,
            CurrentStartedBy: currentStartedBy,
            CurrentStartedAt: currentStartedAt,
            CurrentCompletedBy: currentCompletedBy,
            CurrentCompletedAt: currentCompletedAt,
            CurrentFailedBy: currentFailedBy,
            CurrentFailedAt: currentFailedAt,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: 100,
            Events: events);
    }

    public async Task<PatientDocumentOcrMutationResponse?> StartOcrAsync(
        int documentId,
        PatientDocumentOcrStartRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeRequiredOcrReason(request.Reason, "An OCR start or retry reason");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetOcrDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        ValidateOcrDocument(document);
        var currentTask = await GetOcrTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = ResolveOcrStatus(document, currentTask);
        ValidateExpectedOcrTaskVersion(request.ExpectedTaskVersion, currentVersion, currentStatus);
        if (currentStatus is not ("queued" or "failed"))
        {
            throw new DocumentOcrConflictException(
                currentVersion,
                currentStatus,
                $"OCR can start only from queued or failed state; current state is {currentStatus}.");
        }

        var nextVersion = currentVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await UpdateOcrDocumentStatusEvidenceAsync(
            connection,
            transaction,
            documentId,
            "OCR running",
            $"OCR running by {actor}",
            cancellationToken);
        await PersistOcrTaskTransitionAsync(
            connection,
            transaction,
            documentId,
            document,
            currentTask,
            currentStatus,
            "running",
            currentStatus == "failed" ? "retried" : "started",
            reason,
            actor,
            nextVersion,
            currentTask?.ExtractedText ?? document.InferredExtractedText,
            currentTask?.ExtractedText ?? document.InferredExtractedText,
            null,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildOcrMutationResponse(
            documentId,
            nextVersion,
            "running",
            currentTask?.ExtractedText ?? document.InferredExtractedText,
            null,
            actor,
            now);
    }

    public async Task<PatientDocumentOcrMutationResponse?> FailOcrAsync(
        int documentId,
        PatientDocumentOcrFailRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeRequiredOcrReason(request.Reason, "An OCR failure reason");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetOcrDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        ValidateOcrDocument(document);
        var currentTask = await GetOcrTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = ResolveOcrStatus(document, currentTask);
        ValidateExpectedOcrTaskVersion(request.ExpectedTaskVersion, currentVersion, currentStatus);
        if (currentStatus != "running")
        {
            throw new DocumentOcrConflictException(
                currentVersion,
                currentStatus,
                $"OCR can fail only from running state; current state is {currentStatus}.");
        }

        var nextVersion = currentVersion + 1;
        var now = DateTimeOffset.UtcNow;
        var extractedText = currentTask?.ExtractedText ?? document.InferredExtractedText;
        await UpdateOcrDocumentStatusEvidenceAsync(
            connection,
            transaction,
            documentId,
            "OCR failed",
            $"OCR failed by {actor}",
            cancellationToken);
        await PersistOcrTaskTransitionAsync(
            connection,
            transaction,
            documentId,
            document,
            currentTask,
            currentStatus,
            "failed",
            "failed",
            reason,
            actor,
            nextVersion,
            extractedText,
            extractedText,
            reason,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildOcrMutationResponse(
            documentId,
            nextVersion,
            "failed",
            extractedText,
            reason,
            actor,
            now);
    }

    public async Task<PatientDocumentOcrCompleteResponse?> CompleteOcrAsync(
        int documentId,
        PatientDocumentOcrCompleteRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var extractedText = NormalizeRequiredOcrText(
            request.ExtractedText,
            "Extracted OCR text",
            MaxOcrExtractedTextCharacters);
        var reason = NormalizeText(request.Reason) is { } suppliedReason
            ? NormalizeRequiredOcrReason(suppliedReason, "An OCR completion reason")
            : "OCR completion recorded.";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetOcrDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        ValidateOcrDocument(document);
        var currentTask = await GetOcrTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = ResolveOcrStatus(document, currentTask);
        if (request.ExpectedTaskVersion is { } expectedTaskVersion)
        {
            ValidateExpectedOcrTaskVersion(expectedTaskVersion, currentVersion, currentStatus);
        }

        if (currentStatus is not ("queued" or "running"))
        {
            throw new DocumentOcrConflictException(
                currentVersion,
                currentStatus,
                $"OCR can complete only from queued or running state; current state is {currentStatus}.");
        }

        var nextVersion = currentVersion + 1;
        var now = DateTimeOffset.UtcNow;
        var previousText = currentTask?.ExtractedText ?? document.InferredExtractedText;
        await UpdateOcrDocumentStatusEvidenceAsync(
            connection,
            transaction,
            documentId,
            "OCR complete",
            $"OCR complete by {actor}",
            cancellationToken);
        await PersistOcrTaskTransitionAsync(
            connection,
            transaction,
            documentId,
            document,
            currentTask,
            currentStatus,
            "completed",
            "completed",
            reason,
            actor,
            nextVersion,
            previousText,
            extractedText,
            null,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var completedDocument = await GetContentAsync(documentId, cancellationToken);
        if (completedDocument is null)
        {
            return null;
        }

        var queue = await GetOcrQueueAsync(cancellationToken, document.PatientId);
        return new PatientDocumentOcrCompleteResponse(
            Id: documentId,
            OcrStatus: "OCR complete",
            CompletedBy: actor,
            CompletedAt: now.ToString("O"),
            TaskVersion: nextVersion,
            Status: "completed",
            Document: completedDocument,
            Queue: queue);
    }

    public async Task<PatientDocumentOcrMutationResponse?> CorrectOcrTextAsync(
        int documentId,
        PatientDocumentOcrCorrectRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var extractedText = NormalizeRequiredOcrText(
            request.ExtractedText,
            "Corrected OCR text",
            MaxOcrExtractedTextCharacters);
        var reason = NormalizeRequiredOcrReason(request.Reason, "An OCR correction reason");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var document = await GetOcrDocumentForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        ValidateOcrDocument(document);
        var currentTask = await GetOcrTaskForUpdateAsync(
            connection,
            transaction,
            documentId,
            cancellationToken);
        var currentVersion = currentTask?.TaskVersion ?? 0;
        var currentStatus = ResolveOcrStatus(document, currentTask);
        ValidateExpectedOcrTaskVersion(request.ExpectedTaskVersion, currentVersion, currentStatus);
        if (currentStatus != "completed")
        {
            throw new DocumentOcrConflictException(
                currentVersion,
                currentStatus,
                $"OCR text can be corrected only after completion; current state is {currentStatus}.");
        }

        var previousText = currentTask?.ExtractedText ?? document.InferredExtractedText;
        if (string.Equals(previousText, extractedText, StringComparison.Ordinal))
        {
            throw new ArgumentException("Corrected OCR text must differ from the retained extracted text.");
        }

        var nextVersion = currentVersion + 1;
        var now = DateTimeOffset.UtcNow;
        await UpdateOcrDocumentStatusEvidenceAsync(
            connection,
            transaction,
            documentId,
            "OCR complete",
            $"OCR text corrected by {actor}",
            cancellationToken);
        await PersistOcrTaskTransitionAsync(
            connection,
            transaction,
            documentId,
            document,
            currentTask,
            currentStatus,
            "completed",
            "corrected",
            reason,
            actor,
            nextVersion,
            previousText,
            extractedText,
            null,
            now,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildOcrMutationResponse(
            documentId,
            nextVersion,
            "completed",
            extractedText,
            null,
            actor,
            now);
    }

    public async Task<PatientDocumentRetentionDispositionResponse?> DisposeRetentionAsync(
        int documentId,
        PatientDocumentRetentionDispositionRequest request,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0 || string.IsNullOrWhiteSpace(request.DisposedBy) || string.IsNullOrWhiteSpace(request.Reason))
        {
            return null;
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        var disposedBy = request.DisposedBy.Trim();
        var reason = request.Reason.Trim();
        var disposedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        string? patientId;
        DateOnly retainUntil;

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            string categoryName;
            string? notes;
            await using (var readCommand = connection.CreateCommand())
            {
                readCommand.CommandText = """
                    select patient_id, category_name, doc_date, notes
                    from patient_documents
                    where id = @id
                      and deleted = 0;
                    """;
                readCommand.Parameters.AddWithValue("id", documentId);

                await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                categoryName = reader.GetString(reader.GetOrdinal("category_name"));
                var documentDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date"));
                notes = ReadNullableString(reader, "notes");
                retainUntil = documentDate.AddYears(BuildRetentionYears(categoryName, notes));
            }

            if (retainUntil > metadata.BaseDate)
            {
                return null;
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = """
                update patient_documents
                set deleted = 1,
                    notes = concat_ws('; ',
                        nullif(coalesce(notes, ''), ''),
                        @dispositionNote)
                where id = @id
                returning patient_id;
                """;
            updateCommand.Parameters.AddWithValue("id", documentId);
            updateCommand.Parameters.AddWithValue(
                "dispositionNote",
                $"Retention disposition by {disposedBy} at {disposedAt:yyyy-MM-dd HH:mm:ss}: {reason}; retain until {retainUntil:yyyy-MM-dd}");
            patientId = (string?)await updateCommand.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        return new PatientDocumentRetentionDispositionResponse(
            Id: documentId,
            DispositionStatus: "Disposed",
            DisposedBy: disposedBy,
            DisposedAt: disposedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            RetainUntil: retainUntil.ToString("yyyy-MM-dd"),
            Detail: detail,
            Policy: await GetRetentionPolicyAsync(cancellationToken, patientId));
    }

    public async Task<PatientDocumentMutationResponse?> CreateAsync(
        PatientDocumentCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Content)
            || !DateOnly.TryParse(request.DocDate, out var documentDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }
        if (request.Encounter.HasValue
            && !await EncounterBelongsToPatientAsync(
                connection,
                patient.PatientId,
                request.Encounter.Value,
                cancellationToken))
        {
            return null;
        }

        var id = 0;
        var categoryId = request.CategoryId <= 0 ? 3 : request.CategoryId;
        var categoryName = CategoryNameFor(categoryId);
        var name = request.Name.Trim();
        var content = request.Content.Trim();
        var notes = NullableText(request.Notes);
        var documentKey = $"DOC-MODERN-{Guid.NewGuid():N}";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var uploadedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var idCommand = connection.CreateCommand())
            {
                idCommand.Transaction = transaction;
                idCommand.CommandText = """
                    select greatest(coalesce(max(id), 8999999) + 1, 9000000)
                    from patient_documents;
                    """;
                id = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_documents
                        (id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
                         mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
                         content, content_bytes, deleted)
                    values
                        (@id, @documentKey, @patientId, @pid, @categoryId, @categoryName, @name, @docDate, @uploadedAt,
                         'text/plain', @fileName, @sizeBytes, 1, @encounter, 'database', @url, @hash, @documentationOf, @notes,
                         @content, null, 0);
                    """;
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patient.PatientId);
                command.Parameters.AddWithValue("pid", patient.LegacyPid);
                command.Parameters.AddWithValue("categoryId", categoryId);
                command.Parameters.AddWithValue("categoryName", categoryName);
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("docDate", documentDate);
                command.Parameters.AddWithValue("uploadedAt", uploadedAt);
                command.Parameters.AddWithValue("fileName", BuildDownloadFileName(name, "text/plain"));
                command.Parameters.AddWithValue("sizeBytes", contentBytes.Length);
                var encounterParameter = command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer);
                encounterParameter.Value = request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
                command.Parameters.AddWithValue("url", $"modern://documents/{documentKey}");
                command.Parameters.AddWithValue("hash", Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant());
                var documentationParameter = command.Parameters.Add("documentationOf", NpgsqlTypes.NpgsqlDbType.Text);
                documentationParameter.Value = notes;
                var notesParameter = command.Parameters.Add("notes", NpgsqlTypes.NpgsqlDbType.Text);
                notesParameter.Value = notes;
                command.Parameters.AddWithValue("content", content);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(id, detail);
    }

    public async Task<PatientDocumentMutationResponse?> CreateBinaryAsync(
        PatientDocumentBinaryCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.FileName)
            || string.IsNullOrWhiteSpace(request.Mimetype)
            || !IsValidMediaType(request.Mimetype)
            || string.IsNullOrWhiteSpace(request.ContentBase64)
            || !DateOnly.TryParse(request.DocDate, out var documentDate))
        {
            return null;
        }

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(request.ContentBase64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        if (contentBytes.Length == 0 || contentBytes.Length > MaxBinaryDocumentBytes)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }
        if (request.Encounter.HasValue
            && !await EncounterBelongsToPatientAsync(
                connection,
                patient.PatientId,
                request.Encounter.Value,
                cancellationToken))
        {
            return null;
        }

        var id = 0;
        var categoryId = request.CategoryId <= 0 ? 3 : request.CategoryId;
        var categoryName = CategoryNameFor(categoryId);
        var name = request.Name.Trim();
        var fileName = SanitizeFileName(request.FileName.Trim());
        var mimetype = request.Mimetype.Trim();
        var notes = NullableText(request.Notes);
        var preview = $"Binary document: {fileName} ({mimetype})";
        var documentKey = $"DOC-BINARY-{Guid.NewGuid():N}";
        var uploadedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var idCommand = connection.CreateCommand())
            {
                idCommand.Transaction = transaction;
                idCommand.CommandText = """
                    select greatest(coalesce(max(id), 8999999) + 1, 9000000)
                    from patient_documents;
                    """;
                id = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_documents
                        (id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
                         mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of,
                         notes, content, content_bytes, deleted)
                    values
                        (@id, @documentKey, @patientId, @pid, @categoryId, @categoryName, @name, @docDate, @uploadedAt,
                         @mimetype, @fileName, @sizeBytes, @pages, @encounter, 'database', @url, @hash, @documentationOf,
                         @notes, @content, @contentBytes, 0);
                    """;
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patient.PatientId);
                command.Parameters.AddWithValue("pid", patient.LegacyPid);
                command.Parameters.AddWithValue("categoryId", categoryId);
                command.Parameters.AddWithValue("categoryName", categoryName);
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("docDate", documentDate);
                command.Parameters.AddWithValue("uploadedAt", uploadedAt);
                command.Parameters.AddWithValue("mimetype", mimetype);
                command.Parameters.AddWithValue("fileName", fileName);
                command.Parameters.AddWithValue("sizeBytes", contentBytes.Length);
                command.Parameters.AddWithValue("pages", string.Equals(mimetype, "application/pdf", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                var encounterParameter = command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer);
                encounterParameter.Value = request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
                command.Parameters.AddWithValue("url", $"modern://documents/{documentKey}/{fileName}");
                command.Parameters.AddWithValue("hash", Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant());
                var documentationParameter = command.Parameters.Add("documentationOf", NpgsqlTypes.NpgsqlDbType.Text);
                documentationParameter.Value = notes;
                var notesParameter = command.Parameters.Add("notes", NpgsqlTypes.NpgsqlDbType.Text);
                notesParameter.Value = notes;
                command.Parameters.AddWithValue("content", preview);
                command.Parameters.Add("contentBytes", NpgsqlTypes.NpgsqlDbType.Bytea).Value = contentBytes;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(id, detail);
    }

    public async Task<PatientDocumentMutationResponse?> CreateScannerCaptureAsync(
        PatientDocumentScannerCaptureRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.CaptureSource)
            || string.IsNullOrWhiteSpace(actor)
            || request.Name.Trim().Length > 255
            || request.CaptureSource.Trim().Length > 200
            || NormalizeText(request.Notes)?.Length > 2_000
            || request.PageCount <= 0
            || request.PageCount > 100
            || !DateOnly.TryParse(request.DocDate, out var documentDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }
        if (request.Encounter.HasValue
            && !await EncounterBelongsToPatientAsync(
                connection,
                patient.PatientId,
                request.Encounter.Value,
                cancellationToken))
        {
            return null;
        }

        var id = 0;
        var categoryId = request.CategoryId <= 0 ? 3 : request.CategoryId;
        var categoryName = CategoryNameFor(categoryId);
        var name = request.Name.Trim();
        var captureSource = request.CaptureSource.Trim();
        var capturedBy = actor.Trim();
        var pageCount = request.PageCount;
        var fileName = BuildDownloadFileName(name, "application/pdf");
        var notes = string.Join(
            "; ",
            new[]
            {
                $"Scan source: {captureSource}",
                "OCR pending",
                $"Captured by: {capturedBy}",
                $"Scan pages: {pageCount}",
                NormalizeText(request.Notes)
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var contentBytes = BuildScannerCapturePdf(name, patient.DisplayName, captureSource, pageCount, documentDate);
        var preview = $"Scanner capture: {fileName} ({pageCount} page{(pageCount == 1 ? string.Empty : "s")})";
        var documentKey = $"DOC-SCAN-{Guid.NewGuid():N}";
        var uploadedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var idCommand = connection.CreateCommand())
            {
                idCommand.Transaction = transaction;
                idCommand.CommandText = """
                    select greatest(coalesce(max(id), 8999999) + 1, 9000000)
                    from patient_documents;
                    """;
                id = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_documents
                        (id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
                         mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of,
                         notes, content, content_bytes, deleted)
                    values
                        (@id, @documentKey, @patientId, @pid, @categoryId, @categoryName, @name, @docDate, @uploadedAt,
                         'application/pdf', @fileName, @sizeBytes, @pages, @encounter, 'database', @url, @hash, @documentationOf,
                         @notes, @content, @contentBytes, 0);
                    """;
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patient.PatientId);
                command.Parameters.AddWithValue("pid", patient.LegacyPid);
                command.Parameters.AddWithValue("categoryId", categoryId);
                command.Parameters.AddWithValue("categoryName", categoryName);
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("docDate", documentDate);
                command.Parameters.AddWithValue("uploadedAt", uploadedAt);
                command.Parameters.AddWithValue("fileName", fileName);
                command.Parameters.AddWithValue("sizeBytes", contentBytes.Length);
                command.Parameters.AddWithValue("pages", pageCount);
                var encounterParameter = command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer);
                encounterParameter.Value = request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
                command.Parameters.AddWithValue("url", $"modern://scanner-captures/{documentKey}/{fileName}");
                command.Parameters.AddWithValue("hash", Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant());
                command.Parameters.AddWithValue("documentationOf", notes);
                command.Parameters.AddWithValue("notes", notes);
                command.Parameters.AddWithValue("content", preview);
                command.Parameters.Add("contentBytes", NpgsqlTypes.NpgsqlDbType.Bytea).Value = contentBytes;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(id, detail);
    }

    public async Task<PatientDocumentMutationResponse?> CreateExternalLinkAsync(
        PatientDocumentExternalLinkCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Url)
            || !DateOnly.TryParse(request.DocDate, out var documentDate)
            || !Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var linkUri)
            || (linkUri.Scheme != Uri.UriSchemeHttp && linkUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }
        if (request.Encounter.HasValue
            && !await EncounterBelongsToPatientAsync(
                connection,
                patient.PatientId,
                request.Encounter.Value,
                cancellationToken))
        {
            return null;
        }

        var id = 0;
        var categoryId = request.CategoryId <= 0 ? 3 : request.CategoryId;
        var categoryName = CategoryNameFor(categoryId);
        var name = request.Name.Trim();
        var url = linkUri.AbsoluteUri;
        var notes = NullableText(request.Notes);
        var documentKey = $"DOC-WEBLINK-{Guid.NewGuid():N}";
        var content = $"External document link: {url}";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var uploadedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var idCommand = connection.CreateCommand())
            {
                idCommand.Transaction = transaction;
                idCommand.CommandText = """
                    select greatest(coalesce(max(id), 8999999) + 1, 9000000)
                    from patient_documents;
                    """;
                id = Convert.ToInt32(await idCommand.ExecuteScalarAsync(cancellationToken));
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_documents
                        (id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
                         mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of,
                         notes, content, content_bytes, deleted)
                    values
                        (@id, @documentKey, @patientId, @pid, @categoryId, @categoryName, @name, @docDate, @uploadedAt,
                         'text/uri-list', @fileName, @sizeBytes, 0, @encounter, 'web_url', @url, @hash, @documentationOf,
                         @notes, @content, null, 0);
                    """;
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patient.PatientId);
                command.Parameters.AddWithValue("pid", patient.LegacyPid);
                command.Parameters.AddWithValue("categoryId", categoryId);
                command.Parameters.AddWithValue("categoryName", categoryName);
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("docDate", documentDate);
                command.Parameters.AddWithValue("uploadedAt", uploadedAt);
                command.Parameters.AddWithValue("fileName", BuildDownloadFileName(name, "text/plain"));
                command.Parameters.AddWithValue("sizeBytes", contentBytes.Length);
                var encounterParameter = command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer);
                encounterParameter.Value = request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
                command.Parameters.AddWithValue("url", url);
                command.Parameters.AddWithValue("hash", Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant());
                var documentationParameter = command.Parameters.Add("documentationOf", NpgsqlTypes.NpgsqlDbType.Text);
                documentationParameter.Value = notes;
                var notesParameter = command.Parameters.Add("notes", NpgsqlTypes.NpgsqlDbType.Text);
                notesParameter.Value = notes;
                command.Parameters.AddWithValue("content", content);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(id, detail);
    }

    public async Task<PatientDocumentContentResponse?> GetContentAsync(int documentId, CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
              deleted,
              (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) as prior_version_count,
              coalesce(review_status, 'pending') as review_status, reviewed_by, reviewed_at,
              coalesce(content, '') as content, content_bytes
            from patient_documents
            where id = @id and deleted = 0
            limit 1;
            """;
        command.Parameters.AddWithValue("id", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var name = reader.GetString(reader.GetOrdinal("name"));
        var mimetype = ReadNullableString(reader, "mimetype");
        var content = reader.GetString(reader.GetOrdinal("content"));
        var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
        var contentBytes = reader.IsDBNull(contentBytesOrdinal) ? null : (byte[])reader.GetValue(contentBytesOrdinal);
        var isBinary = contentBytes is { Length: > 0 };
        var contentBase64 = isBinary
            ? Convert.ToBase64String(contentBytes!)
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var fileName = ReadNullableString(reader, "file_name") ?? BuildDownloadFileName(name, mimetype);
        var storageMethod = ReadNullableString(reader, "storage_method");
        var url = ReadNullableString(reader, "url");
        var pages = ReadNullableInt32(reader, "pages");
        var uploadedAt = reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss");
        var priorVersionCount = reader.GetInt32(reader.GetOrdinal("prior_version_count"));
        var currentVersion = priorVersionCount + 1;
        var revisionHash = ReadNullableString(reader, "hash");
        var id = reader.GetInt32(reader.GetOrdinal("id"));
        var documentKey = reader.GetString(reader.GetOrdinal("document_key"));
        var responsePatientId = reader.GetString(reader.GetOrdinal("patient_id"));
        var legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
        var categoryId = reader.GetInt32(reader.GetOrdinal("category_id"));
        var categoryName = reader.GetString(reader.GetOrdinal("category_name"));
        var documentDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd");
        var sizeBytes = ReadNullableInt32(reader, "size_bytes");
        var encounter = ReadNullableInt32(reader, "encounter");
        var documentationOf = ReadNullableString(reader, "documentation_of");
        var notes = ReadNullableString(reader, "notes");
        var reviewStatus = reader.GetString(reader.GetOrdinal("review_status"));
        var reviewedBy = ReadNullableString(reader, "reviewed_by");
        var reviewedAt = ReadNullableDateTimeString(reader, "reviewed_at");
        var deleted = reader.GetInt32(reader.GetOrdinal("deleted"));
        var previewInfo = BuildPreviewInfo(mimetype, storageMethod, fileName, url, pages, content);
        var scanReadiness = BuildScanReadiness(
            name,
            fileName,
            mimetype,
            pages,
            storageMethod,
            notes,
            content);

        await reader.DisposeAsync();

        var versionHistory = await GetDocumentVersionHistoryAsync(
            connection,
            documentId,
            currentVersion,
            uploadedAt,
            fileName,
            mimetype,
            sizeBytes,
            pages,
            revisionHash,
            content,
            cancellationToken);

        return new PatientDocumentContentResponse(
            Id: id,
            DocumentKey: documentKey,
            PatientId: responsePatientId,
            LegacyPid: legacyPid,
            CategoryId: categoryId,
            CategoryName: categoryName,
            Name: name,
            FileName: fileName,
            DocDate: documentDate,
            UploadedAt: uploadedAt,
            RevisionAt: uploadedAt,
            CurrentVersion: currentVersion,
            VersionLabel: $"Version {currentVersion}",
            VersionStatus: "Current version",
            VersionHistoryCount: currentVersion,
            HasPriorVersions: priorVersionCount > 0,
            RevisionHash: revisionHash,
            Mimetype: mimetype,
            SizeBytes: sizeBytes,
            Pages: pages,
            Encounter: encounter,
            StorageMethod: storageMethod,
            Url: url,
            Hash: revisionHash,
            DocumentationOf: documentationOf,
            Notes: notes,
            ReviewStatus: reviewStatus,
            ReviewedBy: reviewedBy,
            ReviewedAt: reviewedAt,
            Content: content,
            ContentBase64: contentBase64,
            IsBinary: isBinary,
            PreviewKind: previewInfo.PreviewKind,
            PreviewStatus: previewInfo.PreviewStatus,
            ThumbnailLabel: previewInfo.ThumbnailLabel,
            ThumbnailText: previewInfo.ThumbnailText,
            CanPreviewInline: previewInfo.CanPreviewInline,
            CanDownload: previewInfo.CanDownload,
            IsScannedAttachment: scanReadiness.IsScannedAttachment,
            ScanStatus: scanReadiness.ScanStatus,
            CaptureSource: scanReadiness.CaptureSource,
            ScanPageCount: scanReadiness.ScanPageCount,
            OcrStatus: scanReadiness.OcrStatus,
            LifecycleEvents: BuildDocumentLifecycleEvents(
                uploadedAt,
                uploadedAt,
                reviewStatus,
                reviewedBy,
                reviewedAt,
                deleted,
                null,
                null,
                revisionHash,
                currentVersion),
            VersionHistory: versionHistory);
    }

    public async Task<PatientDocumentVersionHistoryResponse?> GetVersionHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              id,
              document_key,
              patient_id,
              pid,
              name,
              uploaded_at,
              file_name,
              mimetype,
              size_bytes,
              pages,
              hash,
              (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) as prior_version_count,
              case
                when content_bytes is not null then left(coalesce(content, ''), 260)
                else left(regexp_replace(coalesce(content, ''), E'[\\r\\n]+', ' ', 'g'), 260)
              end as content_preview
            from patient_documents
            where id = @documentId
              and deleted = 0
              and coalesce(storage_method, 'database') <> 'web_url'
            limit 1;
            """;
        command.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var documentKey = reader.GetString(reader.GetOrdinal("document_key"));
        var patientId = reader.GetString(reader.GetOrdinal("patient_id"));
        var legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
        var name = reader.GetString(reader.GetOrdinal("name"));
        var uploadedAt = reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss");
        var fileName = ReadNullableString(reader, "file_name");
        var mimetype = ReadNullableString(reader, "mimetype");
        var sizeBytes = ReadNullableInt32(reader, "size_bytes");
        var pages = ReadNullableInt32(reader, "pages");
        var hash = ReadNullableString(reader, "hash");
        var contentPreview = ReadNullableString(reader, "content_preview") ?? string.Empty;
        var currentVersion = reader.GetInt32(reader.GetOrdinal("prior_version_count")) + 1;
        await reader.DisposeAsync();

        var versions = await GetDocumentVersionHistoryAsync(
            connection,
            documentId,
            currentVersion,
            uploadedAt,
            fileName,
            mimetype,
            sizeBytes,
            pages,
            hash,
            contentPreview,
            cancellationToken);

        return new PatientDocumentVersionHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            CurrentVersion: currentVersion,
            VersionCount: versions.Count,
            Versions: versions);
    }

    public async Task<PatientDocumentVersionContentResponse?> GetVersionContentAsync(
        int documentId,
        int version,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0 || version <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);

        string documentKey;
        string patientId;
        int legacyPid;
        string name;
        int currentVersion;
        string currentUploadedAt;
        string? currentFileName;
        string? currentMimetype;
        int? currentSizeBytes;
        int? currentPages;
        string? currentHash;
        string currentContent;
        byte[]? currentContentBytes;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  document_key,
                  patient_id,
                  pid,
                  name,
                  uploaded_at,
                  file_name,
                  mimetype,
                  size_bytes,
                  pages,
                  hash,
                  coalesce(content, '') as content,
                  content_bytes,
                  (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) as prior_version_count
                from patient_documents
                where id = @documentId
                  and deleted = 0
                  and coalesce(storage_method, 'database') <> 'web_url'
                limit 1;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            documentKey = reader.GetString(reader.GetOrdinal("document_key"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            name = reader.GetString(reader.GetOrdinal("name"));
            currentUploadedAt = reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss");
            currentFileName = ReadNullableString(reader, "file_name");
            currentMimetype = ReadNullableString(reader, "mimetype");
            currentSizeBytes = ReadNullableInt32(reader, "size_bytes");
            currentPages = ReadNullableInt32(reader, "pages");
            currentHash = ReadNullableString(reader, "hash");
            currentContent = reader.GetString(reader.GetOrdinal("content"));
            var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
            currentContentBytes = reader.IsDBNull(contentBytesOrdinal)
                ? null
                : (byte[])reader.GetValue(contentBytesOrdinal);
            currentVersion = reader.GetInt32(reader.GetOrdinal("prior_version_count")) + 1;
        }

        string capturedAt;
        string? fileName;
        string? mimetype;
        int? sizeBytes;
        int? pages;
        string? hash;
        string content;
        byte[]? contentBytes;

        if (version == currentVersion)
        {
            capturedAt = currentUploadedAt;
            fileName = currentFileName;
            mimetype = currentMimetype;
            sizeBytes = currentSizeBytes;
            pages = currentPages;
            hash = currentHash;
            content = currentContent;
            contentBytes = currentContentBytes;
        }
        else
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select captured_at, file_name, mimetype, size_bytes, pages, hash,
                  coalesce(content, '') as content, content_bytes
                from patient_document_versions
                where document_id = @documentId and version_no = @version
                limit 1;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("version", version);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            capturedAt = reader.GetDateTime(reader.GetOrdinal("captured_at")).ToString("yyyy-MM-dd HH:mm:ss");
            fileName = ReadNullableString(reader, "file_name");
            mimetype = ReadNullableString(reader, "mimetype");
            sizeBytes = ReadNullableInt32(reader, "size_bytes");
            pages = ReadNullableInt32(reader, "pages");
            hash = ReadNullableString(reader, "hash");
            content = reader.GetString(reader.GetOrdinal("content"));
            var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
            contentBytes = reader.IsDBNull(contentBytesOrdinal)
                ? null
                : (byte[])reader.GetValue(contentBytesOrdinal);
        }

        string? revisionActor = null;
        string? revisionReason = null;
        string revisionAt = capturedAt;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select actor, reason, occurred_at
                from patient_document_content_events
                where document_id = @documentId and to_version = @version
                limit 1;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("version", version);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                revisionActor = reader.GetString(reader.GetOrdinal("actor"));
                revisionReason = reader.GetString(reader.GetOrdinal("reason"));
                revisionAt = reader
                    .GetFieldValue<DateTime>(reader.GetOrdinal("occurred_at"))
                    .ToUniversalTime()
                    .ToString("O");
            }
        }

        var isBinary = contentBytes is { Length: > 0 };
        return new PatientDocumentVersionContentResponse(
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            Version: version,
            VersionLabel: $"Version {version}",
            VersionStatus: version == currentVersion ? "Current version" : "Prior version",
            RevisionAt: revisionAt,
            RevisionActor: revisionActor,
            RevisionReason: revisionReason,
            FileName: fileName ?? BuildDownloadFileName(name, mimetype),
            Mimetype: mimetype,
            SizeBytes: sizeBytes,
            Pages: pages,
            Hash: hash,
            Content: content,
            ContentBase64: isBinary ? Convert.ToBase64String(contentBytes!) : null,
            IsBinary: isBinary);
    }

    public async Task<PatientDocumentMetadataHistoryResponse?> GetMetadataHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        const int resultLimit = 100;
        var metadata = await GetMetadataAsync(cancellationToken);
        await EnsureDocumentMetadataEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        DocumentMetadataSnapshot? current;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    d.id,
                    d.document_key,
                    d.patient_id,
                    d.pid,
                    d.category_id,
                    d.category_name,
                    d.name,
                    d.doc_date,
                    d.encounter,
                    d.notes
                from patient_documents d
                where d.id = @documentId;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            current = await reader.ReadAsync(cancellationToken)
                ? ReadDocumentMetadataSnapshot(reader)
                : null;
        }

        if (current is null)
        {
            return null;
        }

        var eventCount = 0;
        var events = new List<PatientDocumentMetadataHistoryItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    count(*) over()::int as event_count,
                    event_id,
                    changed_fields,
                    from_category_id,
                    from_category_name,
                    to_category_id,
                    to_category_name,
                    from_name,
                    to_name,
                    from_doc_date,
                    to_doc_date,
                    from_encounter,
                    to_encounter,
                    from_notes,
                    to_notes,
                    reason,
                    actor,
                    occurred_at
                from patient_document_metadata_events
                where document_id = @documentId
                order by occurred_at desc, event_id desc
                limit @resultLimit;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("resultLimit", resultLimit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
                events.Add(new PatientDocumentMetadataHistoryItem(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    ChangedFields: reader.GetFieldValue<string[]>(reader.GetOrdinal("changed_fields")),
                    FromCategoryId: reader.GetInt32(reader.GetOrdinal("from_category_id")),
                    FromCategoryName: reader.GetString(reader.GetOrdinal("from_category_name")),
                    ToCategoryId: reader.GetInt32(reader.GetOrdinal("to_category_id")),
                    ToCategoryName: reader.GetString(reader.GetOrdinal("to_category_name")),
                    FromName: reader.GetString(reader.GetOrdinal("from_name")),
                    ToName: reader.GetString(reader.GetOrdinal("to_name")),
                    FromDocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("from_doc_date")).ToString("yyyy-MM-dd"),
                    ToDocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("to_doc_date")).ToString("yyyy-MM-dd"),
                    FromEncounter: ReadNullableInt32(reader, "from_encounter"),
                    ToEncounter: ReadNullableInt32(reader, "to_encounter"),
                    FromNotes: ReadNullableString(reader, "from_notes"),
                    ToNotes: ReadNullableString(reader, "to_notes"),
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetFieldValue<DateTime>(reader.GetOrdinal("occurred_at"))
                        .ToUniversalTime()
                        .ToString("O")));
            }
        }

        return new PatientDocumentMetadataHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: current.Id,
            DocumentKey: current.DocumentKey,
            PatientId: current.PatientId,
            LegacyPid: current.LegacyPid,
            CurrentCategoryId: current.CategoryId,
            CurrentCategoryName: current.CategoryName,
            CurrentName: current.Name,
            CurrentDocDate: current.DocDate.ToString("yyyy-MM-dd"),
            CurrentEncounter: current.Encounter,
            CurrentNotes: current.Notes,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: resultLimit,
            Events: events);
    }

    public async Task<PatientDocumentMutationResponse?> UpdateMetadataAsync(
        int documentId,
        PatientDocumentMetadataUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeText(request.Reason);
        if (documentId <= 0
            || !CategoryOptions.Any(category => category.Id == request.CategoryId)
            || string.IsNullOrWhiteSpace(request.Name)
            || !DateOnly.TryParse(request.DocDate, out var documentDate)
            || reason?.Length > 250)
        {
            return null;
        }

        var categoryName = CategoryNameFor(request.CategoryId);
        var name = request.Name.Trim();
        var notes = NormalizeText(request.Notes);
        var actor = string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim();
        await EnsureDocumentMetadataEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        DocumentMetadataSnapshot? current;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    d.id,
                    d.document_key,
                    d.patient_id,
                    d.pid,
                    d.category_id,
                    d.category_name,
                    d.name,
                    d.doc_date,
                    d.encounter,
                    d.notes
                from patient_documents d
                where d.id = @documentId
                  and d.deleted = 0
                for update;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            current = await reader.ReadAsync(cancellationToken)
                ? ReadDocumentMetadataSnapshot(reader)
                : null;
        }

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        if (request.Encounter.HasValue
            && !await EncounterBelongsToPatientAsync(
                connection,
                current.PatientId,
                request.Encounter.Value,
                cancellationToken,
                transaction))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var changedFields = new List<string>();
        if (current.CategoryId != request.CategoryId)
        {
            changedFields.Add("category");
        }
        if (!string.Equals(current.Name, name, StringComparison.Ordinal))
        {
            changedFields.Add("name");
        }
        if (current.DocDate != documentDate)
        {
            changedFields.Add("documentDate");
        }
        if (current.Encounter != request.Encounter)
        {
            changedFields.Add("encounter");
        }
        if (!string.Equals(current.Notes, notes, StringComparison.Ordinal))
        {
            changedFields.Add("notes");
        }

        if (changedFields.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            var unchanged = await GetForPatientAsync(current.PatientId, cancellationToken);
            return unchanged is null ? null : new PatientDocumentMutationResponse(documentId, unchanged);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patient_documents
                set category_id = @categoryId,
                    category_name = @categoryName,
                    name = @name,
                    file_name = case
                        when content_bytes is null and coalesce(storage_method, '') <> 'web_url' then @fileName
                        else file_name
                    end,
                    doc_date = @docDate,
                    encounter = @encounter,
                    documentation_of = @documentationOf,
                    notes = @notes
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            command.Parameters.AddWithValue("categoryId", request.CategoryId);
            command.Parameters.AddWithValue("categoryName", categoryName);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("fileName", BuildDownloadFileName(name, "text/plain"));
            command.Parameters.AddWithValue("docDate", documentDate);
            command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer).Value =
                request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
            command.Parameters.Add("documentationOf", NpgsqlTypes.NpgsqlDbType.Text).Value =
                notes is null ? DBNull.Value : notes;
            command.Parameters.Add("notes", NpgsqlTypes.NpgsqlDbType.Text).Value =
                notes is null ? DBNull.Value : notes;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_document_metadata_events (
                    event_id,
                    document_id,
                    document_key,
                    patient_id,
                    legacy_pid,
                    changed_fields,
                    from_category_id,
                    from_category_name,
                    to_category_id,
                    to_category_name,
                    from_name,
                    to_name,
                    from_doc_date,
                    to_doc_date,
                    from_encounter,
                    to_encounter,
                    from_notes,
                    to_notes,
                    reason,
                    actor,
                    occurred_at
                )
                values (
                    @eventId,
                    @documentId,
                    @documentKey,
                    @patientId,
                    @legacyPid,
                    @changedFields,
                    @fromCategoryId,
                    @fromCategoryName,
                    @toCategoryId,
                    @toCategoryName,
                    @fromName,
                    @toName,
                    @fromDocDate,
                    @toDocDate,
                    @fromEncounter,
                    @toEncounter,
                    @fromNotes,
                    @toNotes,
                    @reason,
                    @actor,
                    now()
                );
                """;
            command.Parameters.AddWithValue("eventId", Guid.NewGuid());
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("documentKey", current.DocumentKey);
            command.Parameters.AddWithValue("patientId", current.PatientId);
            command.Parameters.AddWithValue("legacyPid", current.LegacyPid);
            command.Parameters.AddWithValue("changedFields", changedFields.ToArray());
            command.Parameters.AddWithValue("fromCategoryId", current.CategoryId);
            command.Parameters.AddWithValue("fromCategoryName", current.CategoryName);
            command.Parameters.AddWithValue("toCategoryId", request.CategoryId);
            command.Parameters.AddWithValue("toCategoryName", categoryName);
            command.Parameters.AddWithValue("fromName", current.Name);
            command.Parameters.AddWithValue("toName", name);
            command.Parameters.AddWithValue("fromDocDate", current.DocDate);
            command.Parameters.AddWithValue("toDocDate", documentDate);
            command.Parameters.Add("fromEncounter", NpgsqlTypes.NpgsqlDbType.Integer).Value =
                current.Encounter.HasValue ? current.Encounter.Value : DBNull.Value;
            command.Parameters.Add("toEncounter", NpgsqlTypes.NpgsqlDbType.Integer).Value =
                request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
            command.Parameters.Add("fromNotes", NpgsqlTypes.NpgsqlDbType.Text).Value =
                current.Notes is null ? DBNull.Value : current.Notes;
            command.Parameters.Add("toNotes", NpgsqlTypes.NpgsqlDbType.Text).Value =
                notes is null ? DBNull.Value : notes;
            command.Parameters.AddWithValue(
                "reason",
                reason ?? "Document filing metadata updated.");
            command.Parameters.AddWithValue("actor", actor);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var detail = await GetForPatientAsync(current.PatientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentMutationResponse?> ReplaceContentAsync(
        int documentId,
        PatientDocumentContentReplaceRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0
            || string.IsNullOrWhiteSpace(request.FileName)
            || string.IsNullOrWhiteSpace(request.Content)
            || string.IsNullOrWhiteSpace(actor)
            || request.ExpectedVersion is <= 0)
        {
            return null;
        }

        var fileName = SanitizeFileName(request.FileName.Trim());
        var content = request.Content.Trim();
        var contentBytes = Encoding.UTF8.GetBytes(content);
        if (contentBytes.Length > MaxBinaryDocumentBytes)
        {
            return null;
        }

        return await ReplaceStoredContentAsync(
            documentId,
            fileName,
            "text/plain",
            content,
            contentBytes: null,
            sizeBytes: contentBytes.Length,
            pages: 1,
            hash: Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant(),
            request.Reason,
            request.ExpectedVersion,
            actor,
            cancellationToken);
    }

    public async Task<PatientDocumentMutationResponse?> ReplaceBinaryContentAsync(
        int documentId,
        PatientDocumentBinaryContentReplaceRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0
            || string.IsNullOrWhiteSpace(request.FileName)
            || string.IsNullOrWhiteSpace(request.Mimetype)
            || !IsValidMediaType(request.Mimetype)
            || string.IsNullOrWhiteSpace(request.ContentBase64)
            || string.IsNullOrWhiteSpace(actor)
            || request.ExpectedVersion is <= 0)
        {
            return null;
        }

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(request.ContentBase64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        if (contentBytes.Length == 0 || contentBytes.Length > MaxBinaryDocumentBytes)
        {
            return null;
        }

        var fileName = SanitizeFileName(request.FileName.Trim());
        var mimetype = request.Mimetype.Trim();
        var preview = $"Binary document: {fileName} ({mimetype})";
        return await ReplaceStoredContentAsync(
            documentId,
            fileName,
            mimetype,
            preview,
            contentBytes,
            contentBytes.Length,
            string.Equals(mimetype, "application/pdf", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant(),
            request.Reason,
            request.ExpectedVersion,
            actor,
            cancellationToken);
    }

    private async Task<PatientDocumentMutationResponse?> ReplaceStoredContentAsync(
        int documentId,
        string fileName,
        string mimetype,
        string content,
        byte[]? contentBytes,
        int sizeBytes,
        int pages,
        string hash,
        string? requestedReason,
        int? expectedVersion,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeText(requestedReason);
        if (reason is { Length: > 250 })
        {
            return null;
        }

        var occurredAt = DateTime.UtcNow;
        var uploadedAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Unspecified);
        string? patientId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await EnsureDocumentVersionTableAsync(connection, cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var current = await GetContentSnapshotForUpdateAsync(
                connection,
                transaction,
                documentId,
                cancellationToken);
            if (current is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            if (expectedVersion.HasValue && expectedVersion.Value != current.CurrentVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new DocumentVersionConflictException(current.CurrentVersion);
            }

            if (string.Equals(current.FileName, fileName, StringComparison.Ordinal)
                && string.Equals(current.Mimetype, mimetype, StringComparison.OrdinalIgnoreCase)
                && string.Equals(current.Hash, hash, StringComparison.OrdinalIgnoreCase)
                && current.SizeBytes == sizeBytes)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var snapshotted = await SnapshotCurrentDocumentVersionAsync(
                connection,
                transaction,
                documentId,
                cancellationToken);
            if (!snapshotted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update patient_documents
                    set mimetype = @mimetype,
                        file_name = @fileName,
                        size_bytes = @sizeBytes,
                        pages = @pages,
                        storage_method = 'database',
                        hash = @hash,
                        content = @content,
                        content_bytes = @contentBytes,
                        uploaded_at = @uploadedAt,
                        url = concat('modern://documents/', document_key, '/', @fileName)
                    where id = @documentId
                      and deleted = 0
                      and coalesce(storage_method, 'database') <> 'web_url'
                    returning patient_id;
                    """;
                command.Parameters.AddWithValue("documentId", documentId);
                command.Parameters.AddWithValue("mimetype", mimetype);
                command.Parameters.AddWithValue("fileName", fileName);
                command.Parameters.AddWithValue("sizeBytes", sizeBytes);
                command.Parameters.AddWithValue("pages", pages);
                command.Parameters.AddWithValue("hash", hash);
                command.Parameters.AddWithValue("content", content);
                command.Parameters.Add("contentBytes", NpgsqlTypes.NpgsqlDbType.Bytea).Value =
                    contentBytes is null ? DBNull.Value : contentBytes;
                command.Parameters.AddWithValue("uploadedAt", uploadedAt);
                patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
            }

            if (patientId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_document_content_events (
                      event_id,
                      document_id,
                      document_key,
                      patient_id,
                      legacy_pid,
                      from_version,
                      to_version,
                      from_file_name,
                      to_file_name,
                      from_mimetype,
                      to_mimetype,
                      from_size_bytes,
                      to_size_bytes,
                      from_hash,
                      to_hash,
                      reason,
                      actor,
                      occurred_at
                    )
                    values (
                      @eventId,
                      @documentId,
                      @documentKey,
                      @patientId,
                      @legacyPid,
                      @fromVersion,
                      @toVersion,
                      @fromFileName,
                      @toFileName,
                      @fromMimetype,
                      @toMimetype,
                      @fromSizeBytes,
                      @toSizeBytes,
                      @fromHash,
                      @toHash,
                      @reason,
                      @actor,
                      @occurredAt
                    );
                    """;
                command.Parameters.AddWithValue("eventId", Guid.NewGuid());
                command.Parameters.AddWithValue("documentId", documentId);
                command.Parameters.AddWithValue("documentKey", current.DocumentKey);
                command.Parameters.AddWithValue("patientId", current.PatientId);
                command.Parameters.AddWithValue("legacyPid", current.LegacyPid);
                command.Parameters.AddWithValue("fromVersion", current.CurrentVersion);
                command.Parameters.AddWithValue("toVersion", current.CurrentVersion + 1);
                command.Parameters.Add("fromFileName", NpgsqlTypes.NpgsqlDbType.Text).Value =
                    current.FileName is null ? DBNull.Value : current.FileName;
                command.Parameters.AddWithValue("toFileName", fileName);
                command.Parameters.Add("fromMimetype", NpgsqlTypes.NpgsqlDbType.Text).Value =
                    current.Mimetype is null ? DBNull.Value : current.Mimetype;
                command.Parameters.AddWithValue("toMimetype", mimetype);
                command.Parameters.Add("fromSizeBytes", NpgsqlTypes.NpgsqlDbType.Integer).Value =
                    current.SizeBytes.HasValue ? current.SizeBytes.Value : DBNull.Value;
                command.Parameters.AddWithValue("toSizeBytes", sizeBytes);
                command.Parameters.Add("fromHash", NpgsqlTypes.NpgsqlDbType.Text).Value =
                    current.Hash is null ? DBNull.Value : current.Hash;
                command.Parameters.AddWithValue("toHash", hash);
                command.Parameters.AddWithValue(
                    "reason",
                    reason ?? "Document content replaced.");
                command.Parameters.AddWithValue("actor", actor.Trim());
                command.Parameters.AddWithValue("occurredAt", occurredAt);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentReviewHistoryResponse?> GetReviewHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        const int resultLimit = 100;
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);

        string documentKey;
        string patientId;
        int legacyPid;
        string name;
        string currentStatus;
        string? currentReviewer;
        string? currentReviewedAt;
        int eventCount;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  document_key,
                  patient_id,
                  pid,
                  name,
                  coalesce(review_status, 'pending') as review_status,
                  reviewed_by,
                  reviewed_at,
                  (select count(*)
                     from patient_document_review_events e
                    where e.document_id = patient_documents.id) as event_count
                from patient_documents
                where id = @documentId
                  and deleted = 0
                limit 1;
                """;
            command.Parameters.AddWithValue("documentId", documentId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            documentKey = reader.GetString(reader.GetOrdinal("document_key"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            name = reader.GetString(reader.GetOrdinal("name"));
            currentStatus = reader.GetString(reader.GetOrdinal("review_status"));
            currentReviewer = ReadNullableString(reader, "reviewed_by");
            currentReviewedAt = ReadNullableDateTimeString(reader, "reviewed_at");
            eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
        }

        var events = new List<PatientDocumentReviewEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  event_id,
                  from_status,
                  to_status,
                  reason,
                  actor,
                  occurred_at,
                  document_version,
                  content_hash
                from patient_document_review_events
                where document_id = @documentId
                order by occurred_at desc, event_id desc
                limit @resultLimit;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("resultLimit", resultLimit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var toStatus = reader.GetString(reader.GetOrdinal("to_status"));
                events.Add(new PatientDocumentReviewEvent(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    FromStatus: reader.GetString(reader.GetOrdinal("from_status")),
                    ToStatus: toStatus,
                    Action: toStatus switch
                    {
                        "approved" => "Approved",
                        "denied" => "Denied",
                        _ => "Reopened"
                    },
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetDateTime(reader.GetOrdinal("occurred_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                    DocumentVersion: reader.GetInt32(reader.GetOrdinal("document_version")),
                    ContentHash: ReadNullableString(reader, "content_hash")));
            }
        }

        return new PatientDocumentReviewHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            CurrentStatus: currentStatus,
            CurrentReviewer: currentReviewer,
            CurrentReviewedAt: currentReviewedAt,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: resultLimit,
            Events: events);
    }

    public async Task<PatientDocumentMutationResponse?> SignAsync(
        int documentId,
        PatientDocumentSignRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new ArgumentException("An authenticated review actor is required.");
        }

        var reviewStatus = NormalizeReviewStatus(request.ReviewStatus)
            ?? throw new ArgumentException("Review status must be pending, approved, or denied.");
        var expectedStatus = NormalizeText(request.ExpectedReviewStatus) is { } rawExpectedStatus
            ? NormalizeReviewStatus(rawExpectedStatus)
                ?? throw new ArgumentException("Expected review status must be pending, approved, or denied.")
            : null;
        var reason = NormalizeText(request.Reason);
        if (reason?.Length > 250)
        {
            throw new ArgumentException("Review reason must be 250 characters or fewer.");
        }
        if ((reviewStatus is "denied" or "pending") &&
            reason is null &&
            expectedStatus is not null)
        {
            throw new ArgumentException(
                reviewStatus == "denied"
                    ? "A denial reason is required."
                    : "A reopen reason is required.");
        }

        reason ??= reviewStatus switch
        {
            "approved" => "Document approved.",
            "denied" => "Document denied.",
            _ => "Document review reopened."
        };

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await EnsureDocumentVersionTableAsync(connection, cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            string documentKey;
            int legacyPid;
            string currentStatus;
            int documentVersion;
            string? contentHash;

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    select
                      document_key,
                      patient_id,
                      pid,
                      coalesce(review_status, 'pending') as review_status,
                      (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) + 1 as document_version,
                      hash
                    from patient_documents
                    where id = @id
                      and deleted = 0
                    for update;
                    """;
                command.Parameters.AddWithValue("id", documentId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }

                documentKey = reader.GetString(reader.GetOrdinal("document_key"));
                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
                currentStatus = reader.GetString(reader.GetOrdinal("review_status"));
                documentVersion = reader.GetInt32(reader.GetOrdinal("document_version"));
                contentHash = ReadNullableString(reader, "hash");
            }

            if (expectedStatus is not null && currentStatus != expectedStatus)
            {
                throw new DocumentReviewConflictException(
                    currentStatus,
                    $"The document review changed from {expectedStatus} to {currentStatus}. Reload review history before acting.");
            }

            var transitionAllowed =
                currentStatus == "pending" && reviewStatus is "approved" or "denied" ||
                currentStatus is "approved" or "denied" && reviewStatus == "pending";
            if (!transitionAllowed)
            {
                throw new DocumentReviewConflictException(
                    currentStatus,
                    $"A document in {currentStatus} review state cannot transition to {reviewStatus}.");
            }

            var occurredAt = DateTimeOffset.UtcNow;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update patient_documents
                    set review_status = @reviewStatus,
                        reviewed_by = @reviewedBy,
                        reviewed_at = @reviewedAt
                    where id = @id;

                    insert into patient_document_review_events (
                      event_id,
                      document_id,
                      document_key,
                      patient_id,
                      legacy_pid,
                      from_status,
                      to_status,
                      reason,
                      actor,
                      occurred_at,
                      document_version,
                      content_hash
                    )
                    values (
                      @eventId,
                      @id,
                      @documentKey,
                      @patientId,
                      @legacyPid,
                      @fromStatus,
                      @reviewStatus,
                      @reason,
                      @reviewedBy,
                      @occurredAt,
                      @documentVersion,
                      @contentHash
                    );
                    """;
                command.Parameters.AddWithValue("eventId", Guid.NewGuid());
                command.Parameters.AddWithValue("id", documentId);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patientId);
                command.Parameters.AddWithValue("legacyPid", legacyPid);
                command.Parameters.AddWithValue("fromStatus", currentStatus);
                command.Parameters.AddWithValue("reviewStatus", reviewStatus);
                command.Parameters.AddWithValue("reason", reason);
                command.Parameters.AddWithValue("reviewedBy", actor.Trim());
                command.Parameters.AddWithValue("reviewedAt", occurredAt.UtcDateTime);
                command.Parameters.AddWithValue("occurredAt", occurredAt);
                command.Parameters.AddWithValue("documentVersion", documentVersion);
                command.Parameters.Add("contentHash", NpgsqlTypes.NpgsqlDbType.Text).Value =
                    contentHash is null ? DBNull.Value : contentHash;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentArchiveHistoryResponse?> GetArchiveHistoryAsync(
        int documentId,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        const int resultLimit = 100;
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureDocumentVersionTableAsync(connection, cancellationToken);

        string documentKey;
        string patientId;
        int legacyPid;
        string name;
        bool currentArchived;
        string? currentStateActor;
        string? currentStateAt;
        int eventCount;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  d.document_key,
                  d.patient_id,
                  d.pid,
                  d.name,
                  d.deleted <> 0 as current_archived,
                  latest.actor as current_state_actor,
                  latest.occurred_at as current_state_at,
                  (select count(*)
                     from patient_document_archive_events e
                    where e.document_id = d.id) as event_count
                from patient_documents d
                left join lateral (
                  select e.actor, e.occurred_at
                  from patient_document_archive_events e
                  where e.document_id = d.id
                  order by e.occurred_at desc, e.event_id desc
                  limit 1
                ) latest on true
                where d.id = @documentId
                limit 1;
                """;
            command.Parameters.AddWithValue("documentId", documentId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            documentKey = reader.GetString(reader.GetOrdinal("document_key"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            name = reader.GetString(reader.GetOrdinal("name"));
            currentArchived = reader.GetBoolean(reader.GetOrdinal("current_archived"));
            currentStateActor = ReadNullableString(reader, "current_state_actor");
            currentStateAt = ReadNullableDateTimeString(reader, "current_state_at");
            eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
        }

        var events = new List<PatientDocumentArchiveEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  event_id,
                  from_archived,
                  to_archived,
                  reason,
                  actor,
                  occurred_at,
                  document_version,
                  review_status,
                  content_hash
                from patient_document_archive_events
                where document_id = @documentId
                order by occurred_at desc, event_id desc
                limit @resultLimit;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("resultLimit", resultLimit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var toArchived = reader.GetBoolean(reader.GetOrdinal("to_archived"));
                events.Add(new PatientDocumentArchiveEvent(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    Action: toArchived ? "Archived" : "Restored",
                    FromArchived: reader.GetBoolean(reader.GetOrdinal("from_archived")),
                    ToArchived: toArchived,
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetDateTime(reader.GetOrdinal("occurred_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                    DocumentVersion: reader.GetInt32(reader.GetOrdinal("document_version")),
                    ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                    ContentHash: ReadNullableString(reader, "content_hash")));
            }
        }

        return new PatientDocumentArchiveHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            DocumentId: documentId,
            DocumentKey: documentKey,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Name: name,
            CurrentArchived: currentArchived,
            CurrentStateActor: currentStateActor,
            CurrentStateAt: currentStateAt,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: resultLimit,
            Events: events);
    }

    public Task<PatientDocumentMutationResponse?> SoftDeleteAsync(
        int documentId,
        PatientDocumentArchiveRequest? request,
        string actor,
        CancellationToken cancellationToken) =>
        ChangeArchiveStateAsync(
            documentId,
            targetArchived: true,
            request ?? new PatientDocumentArchiveRequest(),
            actor,
            cancellationToken);

    public Task<PatientDocumentMutationResponse?> RestoreAsync(
        int documentId,
        PatientDocumentArchiveRequest? request,
        string actor,
        CancellationToken cancellationToken) =>
        ChangeArchiveStateAsync(
            documentId,
            targetArchived: false,
            request ?? new PatientDocumentArchiveRequest(),
            actor,
            cancellationToken);

    private async Task<PatientDocumentMutationResponse?> ChangeArchiveStateAsync(
        int documentId,
        bool targetArchived,
        PatientDocumentArchiveRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new ArgumentException("An authenticated document lifecycle actor is required.");
        }

        var reason = NormalizeText(request.Reason);
        if (reason?.Length > 250)
        {
            throw new ArgumentException("Archive or restore reason must be 250 characters or fewer.");
        }
        if (request.ExpectedArchived is not null && reason is null)
        {
            throw new ArgumentException(
                targetArchived
                    ? "An archive reason is required."
                    : "A restore reason is required.");
        }
        reason ??= targetArchived
            ? "Document archived."
            : "Document restored.";

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await EnsureDocumentVersionTableAsync(connection, cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            string documentKey;
            int legacyPid;
            bool currentArchived;
            int documentVersion;
            string reviewStatus;
            string? contentHash;

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    select
                      document_key,
                      patient_id,
                      pid,
                      deleted <> 0 as current_archived,
                      (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) + 1 as document_version,
                      coalesce(review_status, 'pending') as review_status,
                      hash
                    from patient_documents
                    where id = @id
                    for update;
                    """;
                command.Parameters.AddWithValue("id", documentId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }

                documentKey = reader.GetString(reader.GetOrdinal("document_key"));
                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
                currentArchived = reader.GetBoolean(reader.GetOrdinal("current_archived"));
                documentVersion = reader.GetInt32(reader.GetOrdinal("document_version"));
                reviewStatus = reader.GetString(reader.GetOrdinal("review_status"));
                contentHash = ReadNullableString(reader, "hash");
            }

            if (request.ExpectedArchived is { } expectedArchived &&
                currentArchived != expectedArchived)
            {
                throw new DocumentArchiveConflictException(
                    currentArchived,
                    $"The document lifecycle changed from {(expectedArchived ? "archived" : "active")} to {(currentArchived ? "archived" : "active")}. Reload archive history before acting.");
            }

            if (currentArchived == targetArchived)
            {
                throw new DocumentArchiveConflictException(
                    currentArchived,
                    $"The document is already {(currentArchived ? "archived" : "active")}.");
            }

            var occurredAt = DateTimeOffset.UtcNow;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update patient_documents
                    set deleted = @deleted
                    where id = @id;

                    insert into patient_document_archive_events (
                      event_id,
                      document_id,
                      document_key,
                      patient_id,
                      legacy_pid,
                      from_archived,
                      to_archived,
                      reason,
                      actor,
                      occurred_at,
                      document_version,
                      review_status,
                      content_hash
                    )
                    values (
                      @eventId,
                      @id,
                      @documentKey,
                      @patientId,
                      @legacyPid,
                      @fromArchived,
                      @toArchived,
                      @reason,
                      @actor,
                      @occurredAt,
                      @documentVersion,
                      @reviewStatus,
                      @contentHash
                    );
                    """;
                command.Parameters.AddWithValue("eventId", Guid.NewGuid());
                command.Parameters.AddWithValue("id", documentId);
                command.Parameters.AddWithValue("deleted", targetArchived ? 1 : 0);
                command.Parameters.AddWithValue("documentKey", documentKey);
                command.Parameters.AddWithValue("patientId", patientId);
                command.Parameters.AddWithValue("legacyPid", legacyPid);
                command.Parameters.AddWithValue("fromArchived", currentArchived);
                command.Parameters.AddWithValue("toArchived", targetArchived);
                command.Parameters.AddWithValue("reason", reason);
                command.Parameters.AddWithValue("actor", actor.Trim());
                command.Parameters.AddWithValue("occurredAt", occurredAt);
                command.Parameters.AddWithValue("documentVersion", documentVersion);
                command.Parameters.AddWithValue("reviewStatus", reviewStatus);
                command.Parameters.Add("contentHash", NpgsqlTypes.NpgsqlDbType.Text).Value =
                    contentHash is null ? DBNull.Value : contentHash;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(
            patientId,
            cancellationToken,
            includeArchived: targetArchived);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<bool> DeleteAsync(int documentId, CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from patient_documents
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", documentId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<DocumentPatient?> GetPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");

        return new DocumentPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<bool> EncounterBelongsToPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        int encounter,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null)
    {
        if (encounter <= 0)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
                select 1
                from encounters
                where patient_id = @patientId
                  and encounter = @encounter
            );
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("encounter", encounter);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static DocumentMetadataSnapshot ReadDocumentMetadataSnapshot(DbDataReader reader)
    {
        return new DocumentMetadataSnapshot(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
            CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
            Name: reader.GetString(reader.GetOrdinal("name")),
            DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")),
            Encounter: ReadNullableInt32(reader, "encounter"),
            Notes: ReadNullableString(reader, "notes"));
    }

    private static async Task<IReadOnlyList<PatientDocumentItem>> GetDocumentsAsync(
        NpgsqlConnection connection,
        string patientId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
              content_bytes,
              deleted,
              latest_archive.actor as archive_state_actor,
              latest_archive.occurred_at as archive_state_at,
              (select count(*) from patient_document_archive_events ae where ae.document_id = patient_documents.id) as archive_event_count,
              (select count(*) from patient_document_versions v where v.document_id = patient_documents.id) as prior_version_count,
              coalesce(review_status, 'pending') as review_status, reviewed_by, reviewed_at,
              case
                when content_bytes is not null then left(coalesce(content, ''), 260)
                else left(regexp_replace(coalesce(content, ''), E'[\\r\\n]+', ' ', 'g'), 260)
              end as content_preview
            from patient_documents
            left join lateral (
              select ae.actor, ae.occurred_at
              from patient_document_archive_events ae
              where ae.document_id = patient_documents.id
              order by ae.occurred_at desc, ae.event_id desc
              limit 1
            ) latest_archive on true
            where patient_id = @patientId and (@includeArchived or deleted = 0)
            order by deleted, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("includeArchived", includeArchived);

        var items = new List<PatientDocumentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var mimetype = ReadNullableString(reader, "mimetype");
            var storageMethod = ReadNullableString(reader, "storage_method");
            var fileName = ReadNullableString(reader, "file_name");
            var url = ReadNullableString(reader, "url");
            var pages = ReadNullableInt32(reader, "pages");
            var contentPreview = ReadNullableString(reader, "content_preview");
            var uploadedAt = reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss");
            var priorVersionCount = reader.GetInt32(reader.GetOrdinal("prior_version_count"));
            var currentVersion = priorVersionCount + 1;
            var revisionHash = ReadNullableString(reader, "hash");
            var previewInfo = BuildPreviewInfo(mimetype, storageMethod, fileName, url, pages, contentPreview);
            var contentBytesOrdinal = reader.GetOrdinal("content_bytes");
            var contentBytes = reader.IsDBNull(contentBytesOrdinal) ? null : (byte[])reader.GetValue(contentBytesOrdinal);
            var thumbnailDataUri = BuildThumbnailDataUri(mimetype, contentBytes, fileName, pages);
            var reviewStatus = reader.GetString(reader.GetOrdinal("review_status"));
            var reviewedBy = ReadNullableString(reader, "reviewed_by");
            var reviewedAt = ReadNullableDateTimeString(reader, "reviewed_at");
            var deleted = reader.GetInt32(reader.GetOrdinal("deleted"));
            var archiveStateActor = ReadNullableString(reader, "archive_state_actor");
            var archiveStateAt = ReadNullableDateTimeString(reader, "archive_state_at");
            var archiveEventCount = reader.GetInt32(reader.GetOrdinal("archive_event_count"));
            var name = reader.GetString(reader.GetOrdinal("name"));
            var scanReadiness = BuildScanReadiness(
                name,
                fileName,
                mimetype,
                pages,
                storageMethod,
                ReadNullableString(reader, "notes"),
                contentPreview);

            items.Add(new PatientDocumentItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
                Name: name,
                DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
                UploadedAt: uploadedAt,
                RevisionAt: uploadedAt,
                CurrentVersion: currentVersion,
                VersionLabel: $"Version {currentVersion}",
                VersionStatus: "Current version",
                VersionHistoryCount: currentVersion,
                HasPriorVersions: priorVersionCount > 0,
                RevisionHash: revisionHash,
                Mimetype: mimetype,
                SizeBytes: ReadNullableInt32(reader, "size_bytes"),
                Pages: pages,
                Encounter: ReadNullableInt32(reader, "encounter"),
                StorageMethod: storageMethod,
                FileName: fileName,
                Url: url,
                Hash: revisionHash,
                DocumentationOf: ReadNullableString(reader, "documentation_of"),
                Notes: ReadNullableString(reader, "notes"),
                Deleted: deleted,
                ArchiveStateActor: archiveStateActor,
                ArchiveStateAt: archiveStateAt,
                ArchiveEventCount: archiveEventCount,
                ReviewStatus: reviewStatus,
                ReviewedBy: reviewedBy,
                ReviewedAt: reviewedAt,
                ContentPreview: contentPreview,
                PreviewKind: previewInfo.PreviewKind,
                PreviewStatus: previewInfo.PreviewStatus,
                ThumbnailLabel: previewInfo.ThumbnailLabel,
                ThumbnailText: previewInfo.ThumbnailText,
                ThumbnailDataUri: thumbnailDataUri,
                CanPreviewInline: previewInfo.CanPreviewInline,
                CanDownload: previewInfo.CanDownload,
                IsScannedAttachment: scanReadiness.IsScannedAttachment,
                ScanStatus: scanReadiness.ScanStatus,
                CaptureSource: scanReadiness.CaptureSource,
                ScanPageCount: scanReadiness.ScanPageCount,
                OcrStatus: scanReadiness.OcrStatus,
                LifecycleEvents: BuildDocumentLifecycleEvents(
                    uploadedAt,
                    uploadedAt,
                    reviewStatus,
                    reviewedBy,
                    reviewedAt,
                    deleted,
                    archiveStateActor,
                    archiveStateAt,
                    revisionHash,
                    currentVersion)));
        }

        return items;
    }

    private static async Task<(int ActiveCount, int ArchivedCount)> GetDocumentCountsAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              count(*) filter (where deleted = 0)::integer as active_count,
              count(*) filter (where deleted <> 0)::integer as archived_count
            from patient_documents
            where patient_id = @patientId;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return (
            reader.GetInt32(reader.GetOrdinal("active_count")),
            reader.GetInt32(reader.GetOrdinal("archived_count")));
    }

    private static IReadOnlyList<PatientDocumentLifecycleEvent> BuildDocumentLifecycleEvents(
        string uploadedAt,
        string revisionAt,
        string reviewStatus,
        string? reviewedBy,
        string? reviewedAt,
        int deleted,
        string? archiveStateActor,
        string? archiveStateAt,
        string? revisionHash,
        int currentVersion = 1)
    {
        var normalizedReviewStatus = (NormalizeText(reviewStatus) ?? string.Empty).ToLowerInvariant();
        PatientDocumentLifecycleEvent reviewEvent = normalizedReviewStatus switch
        {
            "approved" => new PatientDocumentLifecycleEvent(
                Code: "review-approved",
                Label: "Review approved",
                OccurredAt: reviewedAt,
                Actor: NormalizeText(reviewedBy),
                Detail: "Document approved"),
            "denied" => new PatientDocumentLifecycleEvent(
                Code: "review-denied",
                Label: "Review denied",
                OccurredAt: reviewedAt,
                Actor: NormalizeText(reviewedBy),
                Detail: "Document denied"),
            _ => new PatientDocumentLifecycleEvent(
                Code: "review-pending",
                Label: "Review pending",
                OccurredAt: null,
                Actor: null,
                Detail: "Awaiting review")
        };

        var archiveEvent = deleted == 0
            ? new PatientDocumentLifecycleEvent(
                Code: "active",
                Label: "Active",
                OccurredAt: archiveStateAt,
                Actor: archiveStateActor,
                Detail: archiveStateAt is null
                    ? "Visible in active patient documents"
                    : "Restored to active patient documents")
            : new PatientDocumentLifecycleEvent(
                Code: "archived",
                Label: "Archived",
                OccurredAt: archiveStateAt,
                Actor: archiveStateActor,
                Detail: "Hidden from active patient documents");

        return
        [
            new PatientDocumentLifecycleEvent(
                Code: "filed",
                Label: "Filed",
                OccurredAt: uploadedAt,
                Actor: "admin",
                Detail: "Filed to patient documents"),
            new PatientDocumentLifecycleEvent(
                Code: "current-version",
                Label: "Current version",
                OccurredAt: revisionAt,
                Actor: null,
                Detail: NormalizeText(revisionHash) is { } hash
                    ? $"Version {currentVersion} / {hash}"
                    : $"Version {currentVersion}"),
            reviewEvent,
            archiveEvent
        ];
    }

    private static async Task EnsureDocumentVersionTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await DocumentVersionSchemaGate.WaitAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                create table if not exists patient_document_versions (
                  id bigserial primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  version_no integer not null,
                  captured_at timestamp not null,
                  file_name text,
                  mimetype text,
                  size_bytes integer,
                  pages integer,
                  storage_method text,
                  url text,
                  hash text,
                  content text,
                  content_bytes bytea,
                  unique (document_id, version_no)
                );

                create index if not exists idx_patient_document_versions_document
                  on patient_document_versions (document_id, version_no desc);

                create table if not exists patient_document_content_events (
                  event_id uuid primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  document_key text not null,
                  patient_id text not null,
                  legacy_pid integer not null,
                  from_version integer not null,
                  to_version integer not null,
                  from_file_name text,
                  to_file_name text,
                  from_mimetype text,
                  to_mimetype text,
                  from_size_bytes integer,
                  to_size_bytes integer,
                  from_hash text,
                  to_hash text,
                  reason varchar(250) not null,
                  actor text not null,
                  occurred_at timestamptz not null default now(),
                  unique (document_id, to_version)
                );

                create index if not exists ix_patient_document_content_events_document_time
                  on patient_document_content_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_content_events_patient_time
                  on patient_document_content_events (patient_id, occurred_at desc, event_id desc);

                create table if not exists patient_document_review_events (
                  event_id uuid primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  document_key text not null,
                  patient_id text not null,
                  legacy_pid integer not null,
                  from_status varchar(20) not null,
                  to_status varchar(20) not null,
                  reason varchar(250) not null,
                  actor text not null,
                  occurred_at timestamptz not null default now(),
                  document_version integer not null,
                  content_hash text
                );

                create index if not exists ix_patient_document_review_events_document_time
                  on patient_document_review_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_review_events_patient_time
                  on patient_document_review_events (patient_id, occurred_at desc, event_id desc);

                create table if not exists patient_document_archive_events (
                  event_id uuid primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  document_key text not null,
                  patient_id text not null,
                  legacy_pid integer not null,
                  from_archived boolean not null,
                  to_archived boolean not null,
                  reason varchar(250) not null,
                  actor text not null,
                  occurred_at timestamptz not null default now(),
                  document_version integer not null,
                  review_status varchar(20) not null,
                  content_hash text
                );

                create index if not exists ix_patient_document_archive_events_document_time
                  on patient_document_archive_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_archive_events_patient_time
                  on patient_document_archive_events (patient_id, occurred_at desc, event_id desc);

                create table if not exists patient_document_ocr_tasks (
                  document_id integer primary key references patient_documents(id) on delete cascade,
                  task_version integer not null,
                  status varchar(20) not null,
                  priority varchar(20) not null,
                  extracted_text text,
                  failure_reason varchar(500),
                  started_by text,
                  started_at timestamptz,
                  completed_by text,
                  completed_at timestamptz,
                  failed_by text,
                  failed_at timestamptz,
                  updated_by text not null,
                  updated_at timestamptz not null default now()
                );

                create index if not exists ix_patient_document_ocr_tasks_status_updated
                  on patient_document_ocr_tasks (status, updated_at, document_id);

                create index if not exists ix_patient_document_ocr_tasks_priority_status
                  on patient_document_ocr_tasks (priority, status, updated_at);

                create table if not exists patient_document_ocr_events (
                  event_id uuid primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  document_key text not null,
                  patient_id text not null,
                  legacy_pid integer not null,
                  action varchar(20) not null,
                  from_status varchar(20) not null,
                  to_status varchar(20) not null,
                  reason varchar(500) not null,
                  actor text not null,
                  occurred_at timestamptz not null default now(),
                  task_version integer not null,
                  document_version integer not null,
                  review_status varchar(20) not null,
                  from_extracted_text_length integer not null,
                  to_extracted_text_length integer not null,
                  from_extracted_text_preview varchar(500),
                  to_extracted_text_preview varchar(500),
                  from_extracted_text_hash text,
                  to_extracted_text_hash text,
                  failure_reason varchar(500)
                );

                create index if not exists ix_patient_document_ocr_events_document_time
                  on patient_document_ocr_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_ocr_events_patient_time
                  on patient_document_ocr_events (patient_id, occurred_at desc, event_id desc);

                create table if not exists patient_document_routing_tasks (
                  document_id integer primary key references patient_documents(id) on delete cascade,
                  task_version integer not null,
                  status varchar(20) not null,
                  destination varchar(100) not null,
                  priority varchar(20) not null,
                  assigned_to text,
                  routing_reason varchar(250) not null,
                  routed_by text not null,
                  routed_at timestamptz not null default now(),
                  due_at timestamptz not null,
                  completed_by text,
                  completed_at timestamptz,
                  completion_note varchar(250)
                );

                create index if not exists ix_patient_document_routing_tasks_status_due
                  on patient_document_routing_tasks (status, due_at, document_id);

                create index if not exists ix_patient_document_routing_tasks_assignee_status
                  on patient_document_routing_tasks (assigned_to, status, due_at);

                create table if not exists patient_document_routing_events (
                  event_id uuid primary key,
                  document_id integer not null references patient_documents(id) on delete cascade,
                  document_key text not null,
                  patient_id text not null,
                  legacy_pid integer not null,
                  action varchar(20) not null,
                  from_status varchar(20) not null,
                  to_status varchar(20) not null,
                  from_destination varchar(100),
                  to_destination varchar(100) not null,
                  from_priority varchar(20),
                  to_priority varchar(20) not null,
                  from_assigned_to text,
                  to_assigned_to text,
                  reason varchar(250) not null,
                  actor text not null,
                  occurred_at timestamptz not null default now(),
                  due_at timestamptz not null,
                  task_version integer not null,
                  document_version integer not null,
                  review_status varchar(20) not null,
                  content_hash text
                );

                create index if not exists ix_patient_document_routing_events_document_time
                  on patient_document_routing_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_routing_events_patient_time
                  on patient_document_routing_events (patient_id, occurred_at desc, event_id desc);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            DocumentVersionSchemaGate.Release();
        }
    }

    private async Task EnsureDocumentMetadataEventsAsync(
        CancellationToken cancellationToken)
    {
        await DocumentMetadataSchemaGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                create table if not exists patient_document_metadata_events (
                    event_id uuid primary key,
                    document_id integer not null references patient_documents(id) on delete cascade,
                    document_key text not null,
                    patient_id text not null,
                    legacy_pid integer not null,
                    changed_fields text[] not null,
                    from_category_id integer not null,
                    from_category_name text not null,
                    to_category_id integer not null,
                    to_category_name text not null,
                    from_name text not null,
                    to_name text not null,
                    from_doc_date date not null,
                    to_doc_date date not null,
                    from_encounter integer,
                    to_encounter integer,
                    from_notes text,
                    to_notes text,
                    reason varchar(250) not null,
                    actor text not null,
                    occurred_at timestamptz not null default now()
                );

                create index if not exists ix_patient_document_metadata_events_document_time
                    on patient_document_metadata_events (document_id, occurred_at desc, event_id desc);

                create index if not exists ix_patient_document_metadata_events_patient_time
                    on patient_document_metadata_events (patient_id, occurred_at desc, event_id desc);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            DocumentMetadataSchemaGate.Release();
        }
    }

    private static async Task<DocumentContentSnapshot?> GetContentSnapshotForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              d.document_key,
              d.patient_id,
              d.pid,
              d.file_name,
              d.mimetype,
              d.size_bytes,
              d.hash,
              (select count(*) from patient_document_versions v where v.document_id = d.id) as prior_version_count
            from patient_documents d
            where d.id = @documentId
              and d.deleted = 0
              and coalesce(d.storage_method, 'database') <> 'web_url'
            for update;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DocumentContentSnapshot(
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            CurrentVersion: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("prior_version_count"))) + 1,
            FileName: ReadNullableString(reader, "file_name"),
            Mimetype: ReadNullableString(reader, "mimetype"),
            SizeBytes: ReadNullableInt32(reader, "size_bytes"),
            Hash: ReadNullableString(reader, "hash"));
    }

    private static async Task<bool> SnapshotCurrentDocumentVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_document_versions (
              document_id, version_no, captured_at, file_name, mimetype, size_bytes, pages,
              storage_method, url, hash, content, content_bytes
            )
            select d.id,
              coalesce((select max(v.version_no) from patient_document_versions v where v.document_id = d.id), 0) + 1,
              d.uploaded_at,
              d.file_name,
              d.mimetype,
              d.size_bytes,
              d.pages,
              d.storage_method,
              d.url,
              d.hash,
              d.content,
              d.content_bytes
            from patient_documents d
            where d.id = @documentId
              and d.deleted = 0
              and coalesce(d.storage_method, 'database') <> 'web_url'
            returning id;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        var inserted = await command.ExecuteScalarAsync(cancellationToken);
        return inserted is not null;
    }

    private static async Task<IReadOnlyList<PatientDocumentVersionItem>> GetDocumentVersionHistoryAsync(
        NpgsqlConnection connection,
        int documentId,
        int currentVersion,
        string uploadedAt,
        string? fileName,
        string? mimetype,
        int? sizeBytes,
        int? pages,
        string? hash,
        string content,
        CancellationToken cancellationToken)
    {
        string? currentRevisionActor = null;
        string? currentRevisionReason = null;
        string? currentRevisionAt = null;
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.CommandText = """
                select actor, reason, occurred_at
                from patient_document_content_events
                where document_id = @documentId and to_version = @currentVersion
                limit 1;
                """;
            eventCommand.Parameters.AddWithValue("documentId", documentId);
            eventCommand.Parameters.AddWithValue("currentVersion", currentVersion);
            await using var eventReader = await eventCommand.ExecuteReaderAsync(cancellationToken);
            if (await eventReader.ReadAsync(cancellationToken))
            {
                currentRevisionActor = eventReader.GetString(eventReader.GetOrdinal("actor"));
                currentRevisionReason = eventReader.GetString(eventReader.GetOrdinal("reason"));
                currentRevisionAt = eventReader
                    .GetFieldValue<DateTime>(eventReader.GetOrdinal("occurred_at"))
                    .ToUniversalTime()
                    .ToString("O");
            }
        }

        var items = new List<PatientDocumentVersionItem>
        {
            new(
                Version: currentVersion,
                VersionLabel: $"Version {currentVersion}",
                VersionStatus: "Current version",
                CapturedAt: uploadedAt,
                RevisionActor: currentRevisionActor,
                RevisionReason: currentRevisionReason,
                RevisionAt: currentRevisionAt ?? uploadedAt,
                FileName: fileName,
                Mimetype: mimetype,
                SizeBytes: sizeBytes,
                Pages: pages,
                Hash: hash,
                ContentPreview: BuildPreviewText(content) ?? string.Empty,
                CanDownload: true)
        };

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select v.version_no, v.captured_at, v.file_name, v.mimetype, v.size_bytes, v.pages, v.hash,
              e.actor as revision_actor,
              e.reason as revision_reason,
              e.occurred_at as revision_at,
              case
                when v.content_bytes is not null then left(coalesce(v.content, ''), 260)
                else left(regexp_replace(coalesce(v.content, ''), E'[\\r\\n]+', ' ', 'g'), 260)
              end as content_preview
            from patient_document_versions v
            left join patient_document_content_events e
              on e.document_id = v.document_id and e.to_version = v.version_no
            where v.document_id = @documentId
            order by v.version_no desc;
            """;
        command.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var version = reader.GetInt32(reader.GetOrdinal("version_no"));
            items.Add(new PatientDocumentVersionItem(
                Version: version,
                VersionLabel: $"Version {version}",
                VersionStatus: "Prior version",
                CapturedAt: reader.GetDateTime(reader.GetOrdinal("captured_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                RevisionActor: ReadNullableString(reader, "revision_actor"),
                RevisionReason: ReadNullableString(reader, "revision_reason"),
                RevisionAt: ReadNullableDateTimeString(reader, "revision_at")
                    ?? reader.GetDateTime(reader.GetOrdinal("captured_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                FileName: ReadNullableString(reader, "file_name"),
                Mimetype: ReadNullableString(reader, "mimetype"),
                SizeBytes: ReadNullableInt32(reader, "size_bytes"),
                Pages: ReadNullableInt32(reader, "pages"),
                Hash: ReadNullableString(reader, "hash"),
                ContentPreview: ReadNullableString(reader, "content_preview") ?? string.Empty,
                CanDownload: true));
        }

        return items;
    }

    private static DocumentPreviewInfo BuildPreviewInfo(
        string? mimetype,
        string? storageMethod,
        string? fileName,
        string? url,
        int? pages,
        string? contentPreview)
    {
        var normalizedMimetype = NormalizeText(mimetype)?.ToLowerInvariant() ?? string.Empty;
        var normalizedStorage = NormalizeText(storageMethod)?.ToLowerInvariant() ?? string.Empty;
        var previewText = BuildPreviewText(contentPreview);

        if (normalizedStorage == "web_url" && !string.IsNullOrWhiteSpace(url))
        {
            return new DocumentPreviewInfo(
                PreviewKind: "external-link",
                PreviewStatus: "External link",
                ThumbnailLabel: "LINK",
                ThumbnailText: TrimThumbnailText(url) ?? "External document link",
                CanPreviewInline: false,
                CanDownload: true);
        }

        if (normalizedMimetype.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentPreviewInfo(
                PreviewKind: "text",
                PreviewStatus: "Inline text preview",
                ThumbnailLabel: "TXT",
                ThumbnailText: previewText ?? "Text document",
                CanPreviewInline: true,
                CanDownload: true);
        }

        if (string.Equals(normalizedMimetype, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentPreviewInfo(
                PreviewKind: "pdf",
                PreviewStatus: "Inline PDF preview",
                ThumbnailLabel: "PDF",
                ThumbnailText: pages is > 0 ? $"{pages} page PDF document" : "PDF document",
                CanPreviewInline: true,
                CanDownload: true);
        }

        if (normalizedMimetype.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentPreviewInfo(
                PreviewKind: "image",
                PreviewStatus: "Inline image preview",
                ThumbnailLabel: "IMG",
                ThumbnailText: TrimThumbnailText(fileName) ?? "Image document",
                CanPreviewInline: true,
                CanDownload: true);
        }

        return new DocumentPreviewInfo(
            PreviewKind: "binary",
            PreviewStatus: "Download preview",
            ThumbnailLabel: BuildThumbnailLabel(fileName, normalizedMimetype),
            ThumbnailText: TrimThumbnailText(fileName) ?? "Stored document",
            CanPreviewInline: false,
            CanDownload: true);
    }

    private static string? BuildPreviewText(string? contentPreview)
    {
        var normalized = NormalizeText(contentPreview);
        if (normalized is null)
        {
            return null;
        }

        var firstLine = normalized
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return TrimThumbnailText(firstLine ?? normalized);
    }

    private static string BuildThumbnailLabel(string? fileName, string mimetype)
    {
        var extension = NormalizeText(Path.GetExtension(fileName ?? string.Empty))?.TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 4)
        {
            return extension.ToUpperInvariant();
        }

        if (mimetype.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return "JSON";
        }

        return "FILE";
    }

    private static string? BuildThumbnailDataUri(string? mimetype, byte[]? contentBytes, string? fileName, int? pages)
    {
        var normalizedMimetype = NormalizeText(mimetype)?.ToLowerInvariant() ?? string.Empty;
        if (contentBytes is not { Length: > 0 } || contentBytes.Length > MaxInlineThumbnailBytes)
        {
            return null;
        }

        if (normalizedMimetype.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return $"data:{normalizedMimetype};base64,{Convert.ToBase64String(contentBytes)}";
        }

        if (normalizedMimetype == "application/pdf")
        {
            var thumbnailSvg = BuildPdfThumbnailSvg(fileName, pages, contentBytes.Length);
            return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(thumbnailSvg))}";
        }

        return null;
    }

    private static string BuildPdfThumbnailSvg(string? fileName, int? pages, int sizeBytes)
    {
        var title = HtmlEscape(TrimThumbnailText(fileName) ?? "PDF document");
        var pageText = pages is > 0 ? $"{pages.Value} page PDF" : "PDF document";
        var sizeText = sizeBytes >= 1024 ? $"{Math.Round(sizeBytes / 1024m, 1)} KB" : $"{sizeBytes} bytes";

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="144" height="188" viewBox="0 0 144 188" role="img" aria-label="Generated PDF thumbnail">
              <rect width="144" height="188" rx="8" fill="#f8fafc"/>
              <path d="M32 14h56l24 24v136H32z" fill="#ffffff" stroke="#cbd5e1" stroke-width="2"/>
              <path d="M88 14v25h24" fill="#e2e8f0"/>
              <rect x="44" y="58" width="56" height="30" rx="4" fill="#b91c1c"/>
              <text x="72" y="79" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="20" font-weight="700" fill="#ffffff">PDF</text>
              <text x="72" y="112" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="11" fill="#334155">{HtmlEscape(pageText)}</text>
              <text x="72" y="130" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="10" fill="#64748b">{HtmlEscape(sizeText)}</text>
              <text x="72" y="154" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="9" fill="#475569">{title}</text>
            </svg>
            """;
    }

    private static string HtmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    private static string BuildRouteDestination(string categoryName)
    {
        var normalized = categoryName.ToLowerInvariant();
        if (normalized.Contains("lab", StringComparison.Ordinal))
        {
            return "Lab review";
        }

        if (normalized.Contains("advance", StringComparison.Ordinal))
        {
            return "Clinical review";
        }

        if (normalized.Contains("patient", StringComparison.Ordinal))
        {
            return "Front desk review";
        }

        return "Records review";
    }

    private static string BuildRoutingPriority(string categoryName, string? notes)
    {
        var evidence = $"{categoryName} {notes}".ToLowerInvariant();
        if (evidence.Contains("urgent", StringComparison.Ordinal)
            || evidence.Contains("stat", StringComparison.Ordinal)
            || evidence.Contains("advance directive", StringComparison.Ordinal))
        {
            return "High";
        }

        return "Standard";
    }

    private static string BuildRetentionClass(string categoryName)
    {
        var normalized = categoryName.ToLowerInvariant();
        if (normalized.Contains("advance", StringComparison.Ordinal))
        {
            return "Legal and directive";
        }

        if (normalized.Contains("lab", StringComparison.Ordinal))
        {
            return "Clinical diagnostic";
        }

        if (normalized.Contains("patient", StringComparison.Ordinal))
        {
            return "Administrative";
        }

        return "Clinical record";
    }

    private static int BuildRetentionYears(string categoryName, string? notes)
    {
        var taggedYears = ExtractTaggedValue(notes, "Retention years");
        if (int.TryParse(taggedYears, out var years) && years > 0 && years <= 99)
        {
            return years;
        }

        var normalized = categoryName.ToLowerInvariant();
        if (normalized.Contains("patient", StringComparison.Ordinal))
        {
            return 3;
        }

        if (normalized.Contains("advance", StringComparison.Ordinal))
        {
            return 10;
        }

        return 7;
    }

    private static string? ExtractTaggedValue(string? notes, string label)
    {
        var normalized = NormalizeText(notes);
        if (normalized is null)
        {
            return null;
        }

        var marker = $"{label}:";
        var start = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = normalized.IndexOf(';', start);
        var value = end < 0 ? normalized[start..] : normalized[start..end];
        return NormalizeText(value);
    }

    private static string? TrimThumbnailText(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length <= 90 ? normalized : $"{normalized[..87]}...";
    }

    private static PatientDocumentScanReadiness BuildScanReadiness(
        string? name,
        string? fileName,
        string? mimetype,
        int? pages,
        string? storageMethod,
        string? notes,
        string? previewText)
    {
        var evidence = string.Join(
            " ",
            new[]
            {
                NormalizeText(name),
                NormalizeText(fileName),
                NormalizeText(mimetype),
                NormalizeText(storageMethod),
                NormalizeText(notes),
                NormalizeText(previewText)
            }.Where(value => value is not null));
        var normalizedEvidence = evidence.ToLowerInvariant();
        var isScanned = normalizedEvidence.Contains("scan", StringComparison.Ordinal)
            || normalizedEvidence.Contains("scanner", StringComparison.Ordinal);
        var scanPageCount = Math.Max(pages ?? 0, isScanned ? 1 : 0);

        return new PatientDocumentScanReadiness(
            IsScannedAttachment: isScanned,
            ScanStatus: isScanned ? "Scanned attachment" : "Not scanned",
            CaptureSource: isScanned ? ExtractCaptureSource(notes) ?? "Document scanner" : "Not captured by scanner",
            ScanPageCount: scanPageCount,
            OcrStatus: isScanned ? ExtractOcrStatus(notes, previewText) : "Not applicable");
    }

    private static string? ExtractCaptureSource(string? notes)
    {
        var normalized = NormalizeText(notes);
        if (normalized is null)
        {
            return null;
        }

        const string marker = "scan source:";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var sourceStart = markerIndex + marker.Length;
        var sourceEnd = normalized.IndexOf(';', sourceStart);
        var source = sourceEnd < 0
            ? normalized[sourceStart..]
            : normalized[sourceStart..sourceEnd];
        return NormalizeText(source);
    }

    private static string ExtractOcrStatus(string? notes, string? previewText)
    {
        var evidence = string.Join(" ", NormalizeText(notes), NormalizeText(previewText)).ToLowerInvariant();
        if (evidence.Contains("ocr complete", StringComparison.Ordinal))
        {
            return "OCR complete";
        }

        if (evidence.Contains("ocr failed", StringComparison.Ordinal))
        {
            return "OCR failed";
        }

        if (evidence.Contains("ocr running", StringComparison.Ordinal))
        {
            return "OCR running";
        }

        return evidence.Contains("ocr pending", StringComparison.Ordinal)
            ? "OCR pending"
            : "OCR not started";
    }

    private static async Task<OcrDocumentSnapshot?> GetOcrDocumentForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select d.document_key, d.patient_id, d.pid, d.name, d.file_name, d.mimetype,
              d.pages, d.storage_method, d.notes, d.documentation_of, d.content, d.uploaded_at,
              d.deleted, coalesce(d.review_status, 'pending') as review_status, d.hash,
              coalesce((select count(*) from patient_document_versions v where v.document_id = d.id), 0) + 1
                as document_version
            from patient_documents d
            where d.id = @documentId
            for update;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var name = reader.GetString(reader.GetOrdinal("name"));
        var fileName = ReadNullableString(reader, "file_name");
        var mimetype = ReadNullableString(reader, "mimetype");
        var pages = ReadNullableInt32(reader, "pages");
        var storageMethod = ReadNullableString(reader, "storage_method");
        var notes = ReadNullableString(reader, "notes");
        var content = ReadNullableString(reader, "content");
        var readiness = BuildScanReadiness(
            name,
            fileName,
            mimetype,
            pages,
            storageMethod,
            notes,
            content);

        return new OcrDocumentSnapshot(
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            Name: name,
            FileName: fileName,
            Mimetype: mimetype,
            Pages: pages,
            StorageMethod: storageMethod,
            Notes: notes,
            DocumentationOf: ReadNullableString(reader, "documentation_of"),
            Content: content,
            UploadedAt: new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("uploaded_at")),
                DateTimeKind.Utc)),
            Archived: reader.GetInt32(reader.GetOrdinal("deleted")) != 0,
            ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
            DocumentVersion: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("document_version"))),
            ContentHash: ReadNullableString(reader, "hash"),
            ScanReadiness: readiness,
            InferredExtractedText: ExtractOcrText(content));
    }

    private static async Task<OcrTaskSnapshot?> GetOcrTaskForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select task_version, status, priority, extracted_text, failure_reason,
              started_by, started_at, completed_by, completed_at,
              failed_by, failed_at, updated_by, updated_at
            from patient_document_ocr_tasks
            where document_id = @documentId
            for update;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OcrTaskSnapshot(
            TaskVersion: reader.GetInt32(reader.GetOrdinal("task_version")),
            Status: reader.GetString(reader.GetOrdinal("status")),
            Priority: reader.GetString(reader.GetOrdinal("priority")),
            ExtractedText: ReadNullableString(reader, "extracted_text"),
            FailureReason: ReadNullableString(reader, "failure_reason"),
            StartedBy: ReadNullableString(reader, "started_by"),
            StartedAt: ReadNullableDateTimeOffset(reader, "started_at"),
            CompletedBy: ReadNullableString(reader, "completed_by"),
            CompletedAt: ReadNullableDateTimeOffset(reader, "completed_at"),
            FailedBy: ReadNullableString(reader, "failed_by"),
            FailedAt: ReadNullableDateTimeOffset(reader, "failed_at"),
            UpdatedBy: reader.GetString(reader.GetOrdinal("updated_by")),
            UpdatedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at")));
    }

    private static void ValidateOcrDocument(OcrDocumentSnapshot document)
    {
        if (document.Archived)
        {
            throw new ArgumentException("OCR lifecycle changes are not available for archived documents.");
        }

        if (!document.ScanReadiness.IsScannedAttachment)
        {
            throw new ArgumentException("OCR lifecycle is available only for scanned document attachments.");
        }
    }

    private static string ResolveOcrStatus(
        OcrDocumentSnapshot document,
        OcrTaskSnapshot? task)
    {
        if (task is not null)
        {
            return task.Status;
        }

        return NormalizeInferredOcrStatus(document.ScanReadiness.OcrStatus) ?? "queued";
    }

    private static void ValidateExpectedOcrTaskVersion(
        int expectedTaskVersion,
        int currentTaskVersion,
        string currentStatus)
    {
        if (expectedTaskVersion < 0)
        {
            throw new ArgumentException("Expected OCR task version cannot be negative.");
        }

        if (expectedTaskVersion != currentTaskVersion)
        {
            throw new DocumentOcrConflictException(
                currentTaskVersion,
                currentStatus,
                $"The OCR task changed from version {expectedTaskVersion} to {currentTaskVersion}. Reload OCR history before acting.");
        }
    }

    private static string NormalizeOcrStatusFilter(string? value)
    {
        return NormalizeText(value)?.ToLowerInvariant() switch
        {
            null or "" => "queued",
            "active" => "active",
            "queued" or "pending" => "queued",
            "running" => "running",
            "failed" => "failed",
            "completed" or "complete" => "completed",
            "all" => "all",
            _ => throw new ArgumentException("OCR status must be active, queued, running, failed, completed, or all.")
        };
    }

    private static string? NormalizeOcrPriorityFilter(string? value)
    {
        return NormalizeText(value)?.ToLowerInvariant() switch
        {
            null or "" => null,
            "high" => "High",
            "standard" => "Standard",
            _ => throw new ArgumentException("OCR priority must be High or Standard.")
        };
    }

    private static string? NormalizeInferredOcrStatus(string? value)
    {
        return NormalizeText(value)?.ToLowerInvariant() switch
        {
            "ocr pending" => "queued",
            "ocr running" => "running",
            "ocr failed" => "failed",
            "ocr complete" => "completed",
            _ => null
        };
    }

    private static string OcrQueueStatus(string status)
    {
        return status switch
        {
            "queued" => "Ready for OCR",
            "running" => "OCR running",
            "failed" => "OCR failed",
            "completed" => "OCR complete",
            _ => "OCR unavailable"
        };
    }

    private static string OcrStatusLabel(string status)
    {
        return status switch
        {
            "queued" => "OCR pending",
            "running" => "OCR running",
            "failed" => "OCR failed",
            "completed" => "OCR complete",
            _ => "OCR not started"
        };
    }

    private static string NormalizeRequiredOcrText(
        string? value,
        string fieldLabel,
        int maximumLength)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            throw new ArgumentException($"{fieldLabel} is required.");
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldLabel} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeRequiredOcrReason(
        string? value,
        string fieldLabel)
    {
        var normalized = NormalizeRequiredOcrText(value, fieldLabel, 500);
        if (normalized.Length < 3)
        {
            throw new ArgumentException($"{fieldLabel} of at least 3 characters is required.");
        }

        return normalized;
    }

    private static string? BuildOcrTextPreview(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        var collapsed = string.Join(
            " ",
            normalized.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.Length <= 500 ? collapsed : $"{collapsed[..497]}...";
    }

    private static string? HashOcrText(string? value)
    {
        return NormalizeText(value) is { } normalized
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()
            : null;
    }

    private static string? ExtractOcrText(string? content)
    {
        var normalized = NormalizeText(content);
        if (normalized is null)
        {
            return null;
        }

        const string marker = "OCR extracted text:";
        var markerIndex = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        return NormalizeText(normalized[(markerIndex + marker.Length)..]);
    }

    private static async Task UpdateOcrDocumentStatusEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        string ocrStatus,
        string auditNote,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update patient_documents
            set notes = concat_ws(
                  '; ',
                  nullif(trim(both ' ;' from regexp_replace(
                    coalesce(notes, ''),
                    'OCR (pending|running|complete|failed)',
                    '',
                    'gi')),
                    ''),
                  @ocrStatus,
                  @auditNote),
                documentation_of = concat_ws(
                  '; ',
                  nullif(trim(both ' ;' from regexp_replace(
                    coalesce(documentation_of, ''),
                    'OCR (pending|running|complete|failed)',
                    '',
                    'gi')),
                    ''),
                  @ocrStatus,
                  @auditNote)
            where id = @documentId;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        command.Parameters.AddWithValue("ocrStatus", ocrStatus);
        command.Parameters.AddWithValue("auditNote", auditNote);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task PersistOcrTaskTransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        OcrDocumentSnapshot document,
        OcrTaskSnapshot? currentTask,
        string fromStatus,
        string toStatus,
        string action,
        string reason,
        string actor,
        int nextVersion,
        string? fromExtractedText,
        string? toExtractedText,
        string? failureReason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var priority = currentTask?.Priority
            ?? (document.ScanReadiness.ScanPageCount >= 5 ? "High" : "Standard");
        var startedBy = currentTask?.StartedBy;
        var startedAt = currentTask?.StartedAt;
        var completedBy = currentTask?.CompletedBy;
        var completedAt = currentTask?.CompletedAt;
        var failedBy = currentTask?.FailedBy;
        var failedAt = currentTask?.FailedAt;

        switch (toStatus)
        {
            case "running":
                startedBy = actor;
                startedAt = occurredAt;
                completedBy = null;
                completedAt = null;
                failedBy = null;
                failedAt = null;
                failureReason = null;
                break;
            case "failed":
                startedBy ??= actor;
                startedAt ??= occurredAt;
                completedBy = null;
                completedAt = null;
                failedBy = actor;
                failedAt = occurredAt;
                break;
            case "completed" when action == "completed":
                startedBy ??= actor;
                startedAt ??= occurredAt;
                completedBy = actor;
                completedAt = occurredAt;
                failedBy = null;
                failedAt = null;
                failureReason = null;
                break;
            case "completed":
                failureReason = null;
                break;
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_document_ocr_tasks (
                  document_id, task_version, status, priority, extracted_text, failure_reason,
                  started_by, started_at, completed_by, completed_at,
                  failed_by, failed_at, updated_by, updated_at
                )
                values (
                  @documentId, @taskVersion, @status, @priority, @extractedText, @failureReason,
                  @startedBy, @startedAt, @completedBy, @completedAt,
                  @failedBy, @failedAt, @updatedBy, @updatedAt
                )
                on conflict (document_id) do update
                set task_version = excluded.task_version,
                    status = excluded.status,
                    priority = excluded.priority,
                    extracted_text = excluded.extracted_text,
                    failure_reason = excluded.failure_reason,
                    started_by = excluded.started_by,
                    started_at = excluded.started_at,
                    completed_by = excluded.completed_by,
                    completed_at = excluded.completed_at,
                    failed_by = excluded.failed_by,
                    failed_at = excluded.failed_at,
                    updated_by = excluded.updated_by,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("taskVersion", nextVersion);
            command.Parameters.AddWithValue("status", toStatus);
            command.Parameters.AddWithValue("priority", priority);
            command.Parameters.Add("extractedText", NpgsqlTypes.NpgsqlDbType.Text).Value =
                toExtractedText is null ? DBNull.Value : toExtractedText;
            command.Parameters.Add("failureReason", NpgsqlTypes.NpgsqlDbType.Text).Value =
                failureReason is null ? DBNull.Value : failureReason;
            command.Parameters.Add("startedBy", NpgsqlTypes.NpgsqlDbType.Text).Value =
                startedBy is null ? DBNull.Value : startedBy;
            command.Parameters.Add("startedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
                startedAt.HasValue ? startedAt.Value : DBNull.Value;
            command.Parameters.Add("completedBy", NpgsqlTypes.NpgsqlDbType.Text).Value =
                completedBy is null ? DBNull.Value : completedBy;
            command.Parameters.Add("completedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
                completedAt.HasValue ? completedAt.Value : DBNull.Value;
            command.Parameters.Add("failedBy", NpgsqlTypes.NpgsqlDbType.Text).Value =
                failedBy is null ? DBNull.Value : failedBy;
            command.Parameters.Add("failedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
                failedAt.HasValue ? failedAt.Value : DBNull.Value;
            command.Parameters.AddWithValue("updatedBy", actor);
            command.Parameters.AddWithValue("updatedAt", occurredAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_document_ocr_events (
                  event_id, document_id, document_key, patient_id, legacy_pid,
                  action, from_status, to_status, reason, actor, occurred_at,
                  task_version, document_version, review_status,
                  from_extracted_text_length, to_extracted_text_length,
                  from_extracted_text_preview, to_extracted_text_preview,
                  from_extracted_text_hash, to_extracted_text_hash, failure_reason
                )
                values (
                  @eventId, @documentId, @documentKey, @patientId, @legacyPid,
                  @action, @fromStatus, @toStatus, @reason, @actor, @occurredAt,
                  @taskVersion, @documentVersion, @reviewStatus,
                  @fromTextLength, @toTextLength,
                  @fromTextPreview, @toTextPreview,
                  @fromTextHash, @toTextHash, @failureReason
                );
                """;
            command.Parameters.AddWithValue("eventId", Guid.NewGuid());
            command.Parameters.AddWithValue("documentId", documentId);
            command.Parameters.AddWithValue("documentKey", document.DocumentKey);
            command.Parameters.AddWithValue("patientId", document.PatientId);
            command.Parameters.AddWithValue("legacyPid", document.LegacyPid);
            command.Parameters.AddWithValue("action", action);
            command.Parameters.AddWithValue("fromStatus", fromStatus);
            command.Parameters.AddWithValue("toStatus", toStatus);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("occurredAt", occurredAt);
            command.Parameters.AddWithValue("taskVersion", nextVersion);
            command.Parameters.AddWithValue("documentVersion", document.DocumentVersion);
            command.Parameters.AddWithValue("reviewStatus", document.ReviewStatus);
            command.Parameters.AddWithValue("fromTextLength", fromExtractedText?.Length ?? 0);
            command.Parameters.AddWithValue("toTextLength", toExtractedText?.Length ?? 0);
            command.Parameters.Add("fromTextPreview", NpgsqlTypes.NpgsqlDbType.Text).Value =
                BuildOcrTextPreview(fromExtractedText) is { } fromPreview ? fromPreview : DBNull.Value;
            command.Parameters.Add("toTextPreview", NpgsqlTypes.NpgsqlDbType.Text).Value =
                BuildOcrTextPreview(toExtractedText) is { } toPreview ? toPreview : DBNull.Value;
            command.Parameters.Add("fromTextHash", NpgsqlTypes.NpgsqlDbType.Text).Value =
                HashOcrText(fromExtractedText) is { } fromHash ? fromHash : DBNull.Value;
            command.Parameters.Add("toTextHash", NpgsqlTypes.NpgsqlDbType.Text).Value =
                HashOcrText(toExtractedText) is { } toHash ? toHash : DBNull.Value;
            command.Parameters.Add("failureReason", NpgsqlTypes.NpgsqlDbType.Text).Value =
                failureReason is null ? DBNull.Value : failureReason;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static PatientDocumentOcrMutationResponse BuildOcrMutationResponse(
        int documentId,
        int taskVersion,
        string status,
        string? extractedText,
        string? failureReason,
        string actor,
        DateTimeOffset updatedAt)
    {
        return new PatientDocumentOcrMutationResponse(
            Id: documentId,
            TaskVersion: taskVersion,
            Status: status,
            OcrStatus: OcrStatusLabel(status),
            QueueStatus: OcrQueueStatus(status),
            ExtractedTextLength: extractedText?.Length ?? 0,
            FailureReason: failureReason,
            UpdatedBy: actor,
            UpdatedAt: updatedAt.ToString("O"));
    }

    private static async Task<RoutingDocumentSnapshot?> GetRoutingDocumentForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select d.document_key, d.patient_id, d.pid, d.category_name, d.notes, d.uploaded_at,
              d.deleted, coalesce(d.review_status, 'pending') as review_status, d.hash,
              coalesce((select count(*) from patient_document_versions v where v.document_id = d.id), 0) + 1
                as document_version
            from patient_documents d
            where d.id = @documentId
            for update;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RoutingDocumentSnapshot(
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
            Notes: ReadNullableString(reader, "notes"),
            UploadedAt: new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("uploaded_at")),
                DateTimeKind.Utc)),
            Archived: reader.GetInt32(reader.GetOrdinal("deleted")) != 0,
            ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
            DocumentVersion: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("document_version"))),
            ContentHash: ReadNullableString(reader, "hash"));
    }

    private static async Task<RoutingTaskSnapshot?> GetRoutingTaskForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select task_version, status, destination, priority, assigned_to,
              routing_reason, routed_by, routed_at, due_at
            from patient_document_routing_tasks
            where document_id = @documentId
            for update;
            """;
        command.Parameters.AddWithValue("documentId", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RoutingTaskSnapshot(
            TaskVersion: reader.GetInt32(reader.GetOrdinal("task_version")),
            Status: reader.GetString(reader.GetOrdinal("status")),
            Destination: reader.GetString(reader.GetOrdinal("destination")),
            Priority: reader.GetString(reader.GetOrdinal("priority")),
            AssignedTo: ReadNullableString(reader, "assigned_to"),
            RoutingReason: reader.GetString(reader.GetOrdinal("routing_reason")),
            RoutedBy: reader.GetString(reader.GetOrdinal("routed_by")),
            RoutedAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("routed_at")),
            DueAt: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("due_at")));
    }

    private static async Task ValidateRoutingAssigneeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? assignedTo,
        CancellationToken cancellationToken)
    {
        if (assignedTo is null)
        {
            return;
        }

        if (assignedTo.Length > 80)
        {
            throw new ArgumentException("The routing assignee is too long.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1 from auth_accounts
              where active = true and lower(username) = lower(@assignedTo)
            );
            """;
        command.Parameters.AddWithValue("assignedTo", assignedTo);
        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (!exists)
        {
            throw new ArgumentException("The routing assignee must be an active staff user.");
        }
    }

    private static string NormalizeRoutingStatusFilter(string? value)
    {
        return NormalizeText(value)?.ToLowerInvariant() switch
        {
            null or "" => "active",
            "active" => "active",
            "pending" => "pending",
            "in_progress" or "in-progress" => "in_progress",
            "completed" => "completed",
            "all" => "all",
            _ => throw new ArgumentException("Routing status must be active, pending, in_progress, completed, or all.")
        };
    }

    private static string? NormalizeRoutingPriorityFilter(string? value)
    {
        return NormalizeText(value)?.ToLowerInvariant() switch
        {
            null or "" => null,
            "high" => "High",
            "standard" => "Standard",
            _ => throw new ArgumentException("Routing priority must be High or Standard.")
        };
    }

    private static string NormalizeRoutingPriority(string? value)
    {
        return NormalizeRoutingPriorityFilter(value)
            ?? throw new ArgumentException("A routing priority is required.");
    }

    private static string NormalizeRequiredRoutingText(
        string? value,
        string fieldLabel,
        int maximumLength)
    {
        var normalized = NormalizeText(value);
        if (normalized is null || normalized.Length < 3)
        {
            throw new ArgumentException($"{fieldLabel} of at least 3 characters is required.");
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{fieldLabel} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static DateTimeOffset? ParseRoutingDueAt(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(normalized, out var dueAt))
        {
            throw new ArgumentException("The routing due time is invalid.");
        }

        return dueAt.ToUniversalTime();
    }

    private static bool ContainsIgnoreCase(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static string? ReadNullableDateTimeString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static int? ReadNullableInt32(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string CategoryNameFor(int categoryId)
    {
        return CategoryOptions.FirstOrDefault(category => category.Id == categoryId)?.Name
            ?? "Medical Record";
    }

    private static bool IsValidMediaType(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length is < 3 or > 127)
        {
            return false;
        }

        var parts = normalized.Split('/');
        return parts.Length == 2
            && parts.All(part => part.Length > 0 && part.All(IsMediaTypeTokenCharacter));
    }

    private static bool IsMediaTypeTokenCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value)
            || value is '!' or '#' or '$' or '&' or '^' or '_' or '.' or '+' or '-';
    }

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildDownloadFileName(string name, string? mimetype)
    {
        var safeName = SanitizeFileName(name);

        if (Path.HasExtension(safeName))
        {
            return safeName;
        }

        return mimetype?.ToLowerInvariant() switch
        {
            "text/plain" => $"{safeName}.txt",
            "application/pdf" => $"{safeName}.pdf",
            _ => safeName
        };
    }

    private static byte[] BuildScannerCapturePdf(
        string documentName,
        string patientDisplayName,
        string captureSource,
        int pageCount,
        DateOnly documentDate)
    {
        var text = EscapePdfText(
            $"Legacy EHR scanner capture | {documentName} | {patientDisplayName} | {captureSource} | {pageCount} page{(pageCount == 1 ? string.Empty : "s")} | {documentDate:yyyy-MM-dd}");
        var stream = $"BT /F1 10 Tf 24 100 Td ({text}) Tj ET";
        var pdf = string.Join(
            "\n",
            "%PDF-1.4",
            "% Modernized Legacy EHR scanner capture",
            "1 0 obj",
            "<< /Type /Catalog /Pages 2 0 R >>",
            "endobj",
            "2 0 obj",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "endobj",
            "3 0 obj",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 420 144] /Contents 4 0 R >>",
            "endobj",
            "4 0 obj",
            $"<< /Length {stream.Length} >>",
            "stream",
            stream,
            "endstream",
            "endobj",
            "%%EOF",
            string.Empty);

        return Encoding.UTF8.GetBytes(pdf);
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string SanitizeFileName(string value)
    {
        var safeName = string.Join(
            "_",
            value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(safeName) ? "document" : safeName;
    }

    private static string? NormalizeReviewStatus(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "pending" => "pending",
            "approved" => "approved",
            "signed" => "approved",
            "denied" => "denied",
            "rejected" => "denied",
            _ => null
        };
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record DocumentPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string DisplayName);

    private sealed record DocumentMetadataSnapshot(
        int Id,
        string DocumentKey,
        string PatientId,
        int LegacyPid,
        int CategoryId,
        string CategoryName,
        string Name,
        DateOnly DocDate,
        int? Encounter,
        string? Notes);

    private sealed record DocumentContentSnapshot(
        string DocumentKey,
        string PatientId,
        int LegacyPid,
        int CurrentVersion,
        string? FileName,
        string? Mimetype,
        int? SizeBytes,
        string? Hash);

    private sealed record RoutingDocumentSnapshot(
        string DocumentKey,
        string PatientId,
        int LegacyPid,
        string CategoryName,
        string? Notes,
        DateTimeOffset UploadedAt,
        bool Archived,
        string ReviewStatus,
        int DocumentVersion,
        string? ContentHash);

    private sealed record RoutingTaskSnapshot(
        int TaskVersion,
        string Status,
        string Destination,
        string Priority,
        string? AssignedTo,
        string RoutingReason,
        string RoutedBy,
        DateTimeOffset RoutedAt,
        DateTimeOffset DueAt);

    private sealed record OcrDocumentSnapshot(
        string DocumentKey,
        string PatientId,
        int LegacyPid,
        string Name,
        string? FileName,
        string? Mimetype,
        int? Pages,
        string? StorageMethod,
        string? Notes,
        string? DocumentationOf,
        string? Content,
        DateTimeOffset UploadedAt,
        bool Archived,
        string ReviewStatus,
        int DocumentVersion,
        string? ContentHash,
        PatientDocumentScanReadiness ScanReadiness,
        string? InferredExtractedText);

    private sealed record OcrTaskSnapshot(
        int TaskVersion,
        string Status,
        string Priority,
        string? ExtractedText,
        string? FailureReason,
        string? StartedBy,
        DateTimeOffset? StartedAt,
        string? CompletedBy,
        DateTimeOffset? CompletedAt,
        string? FailedBy,
        DateTimeOffset? FailedAt,
        string UpdatedBy,
        DateTimeOffset UpdatedAt);

    private sealed record DocumentPreviewInfo(
        string PreviewKind,
        string PreviewStatus,
        string ThumbnailLabel,
        string ThumbnailText,
        bool CanPreviewInline,
        bool CanDownload);

    private sealed record PatientDocumentScanReadiness(
        bool IsScannedAttachment,
        string ScanStatus,
        string CaptureSource,
        int ScanPageCount,
        string OcrStatus);
}
