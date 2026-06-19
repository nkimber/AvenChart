using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class DocumentRepository(NpgsqlDataSource dataSource)
{
    public async Task<PatientDocumentsResponse?> GetForPatientAsync(
        string patientId,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var documents = await GetDocumentsAsync(connection, patient.PatientId, includeArchived, cancellationToken);

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
            Documents: documents);
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

        if (contentBytes.Length == 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
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
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, file_name, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
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

        return new PatientDocumentContentResponse(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
            CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
            Name: name,
            FileName: ReadNullableString(reader, "file_name") ?? BuildDownloadFileName(name, mimetype),
            DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
            UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
            Mimetype: mimetype,
            SizeBytes: ReadNullableInt32(reader, "size_bytes"),
            Pages: ReadNullableInt32(reader, "pages"),
            Encounter: ReadNullableInt32(reader, "encounter"),
            StorageMethod: ReadNullableString(reader, "storage_method"),
            Url: ReadNullableString(reader, "url"),
            Hash: ReadNullableString(reader, "hash"),
            DocumentationOf: ReadNullableString(reader, "documentation_of"),
            Notes: ReadNullableString(reader, "notes"),
            ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
            ReviewedBy: ReadNullableString(reader, "reviewed_by"),
            ReviewedAt: ReadNullableDateTimeString(reader, "reviewed_at"),
            Content: content,
            ContentBase64: contentBase64,
            IsBinary: isBinary);
    }

    public async Task<PatientDocumentMutationResponse?> UpdateMetadataAsync(
        int documentId,
        PatientDocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0
            || request.CategoryId <= 0
            || string.IsNullOrWhiteSpace(request.Name)
            || !DateOnly.TryParse(request.DocDate, out var documentDate))
        {
            return null;
        }

        var categoryName = CategoryNameFor(request.CategoryId);
        var name = request.Name.Trim();
        var notes = NullableText(request.Notes);
        string? patientId = null;

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
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
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            command.Parameters.AddWithValue("categoryId", request.CategoryId);
            command.Parameters.AddWithValue("categoryName", categoryName);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("fileName", BuildDownloadFileName(name, "text/plain"));
            command.Parameters.AddWithValue("docDate", documentDate);
            var encounterParameter = command.Parameters.Add("encounter", NpgsqlTypes.NpgsqlDbType.Integer);
            encounterParameter.Value = request.Encounter.HasValue ? request.Encounter.Value : DBNull.Value;
            var documentationParameter = command.Parameters.Add("documentationOf", NpgsqlTypes.NpgsqlDbType.Text);
            documentationParameter.Value = notes;
            var notesParameter = command.Parameters.Add("notes", NpgsqlTypes.NpgsqlDbType.Text);
            notesParameter.Value = notes;
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentMutationResponse?> ReplaceContentAsync(
        int documentId,
        PatientDocumentContentReplaceRequest request,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0
            || string.IsNullOrWhiteSpace(request.FileName)
            || string.IsNullOrWhiteSpace(request.Content))
        {
            return null;
        }

        var fileName = SanitizeFileName(request.FileName.Trim());
        var content = request.Content.Trim();
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var uploadedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        string? patientId = null;

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update patient_documents
                set mimetype = 'text/plain',
                    file_name = @fileName,
                    size_bytes = @sizeBytes,
                    pages = 1,
                    storage_method = 'database',
                    hash = @hash,
                    content = @content,
                    content_bytes = null,
                    uploaded_at = @uploadedAt,
                    url = case
                        when coalesce(url, '') = '' then concat('modern://documents/', document_key)
                        else url
                    end
                where id = @id and deleted = 0 and coalesce(storage_method, 'database') <> 'web_url'
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            command.Parameters.AddWithValue("fileName", fileName);
            command.Parameters.AddWithValue("sizeBytes", contentBytes.Length);
            command.Parameters.AddWithValue("hash", Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant());
            command.Parameters.AddWithValue("content", content);
            command.Parameters.AddWithValue("uploadedAt", uploadedAt);
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentMutationResponse?> SignAsync(
        int documentId,
        PatientDocumentSignRequest request,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0 || string.IsNullOrWhiteSpace(request.ReviewStatus) || string.IsNullOrWhiteSpace(request.ReviewedBy))
        {
            return null;
        }

        var reviewStatus = NormalizeReviewStatus(request.ReviewStatus);
        if (reviewStatus is null)
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update patient_documents
                set review_status = @reviewStatus,
                    reviewed_by = @reviewedBy,
                    reviewed_at = @reviewedAt
                where id = @id and deleted = 0
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            command.Parameters.AddWithValue("reviewStatus", reviewStatus);
            command.Parameters.AddWithValue("reviewedBy", request.ReviewedBy.Trim());
            command.Parameters.AddWithValue("reviewedAt", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentMutationResponse?> SoftDeleteAsync(int documentId, CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update patient_documents
                set deleted = 1
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
        return detail is null ? null : new PatientDocumentMutationResponse(documentId, detail);
    }

    public async Task<PatientDocumentMutationResponse?> RestoreAsync(int documentId, CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update patient_documents
                set deleted = 0
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", documentId);
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var detail = await GetForPatientAsync(patientId, cancellationToken);
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
              deleted,
              coalesce(review_status, 'pending') as review_status, reviewed_by, reviewed_at,
              case
                when content_bytes is not null then left(coalesce(content, ''), 260)
                else left(regexp_replace(coalesce(content, ''), E'[\\r\\n]+', ' ', 'g'), 260)
              end as content_preview
            from patient_documents
            where patient_id = @patientId and (@includeArchived or deleted = 0)
            order by deleted, doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("includeArchived", includeArchived);

        var items = new List<PatientDocumentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientDocumentItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                DocDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("doc_date")).ToString("yyyy-MM-dd"),
                UploadedAt: reader.GetDateTime(reader.GetOrdinal("uploaded_at")).ToString("yyyy-MM-dd HH:mm:ss"),
                Mimetype: ReadNullableString(reader, "mimetype"),
                SizeBytes: ReadNullableInt32(reader, "size_bytes"),
                Pages: ReadNullableInt32(reader, "pages"),
                Encounter: ReadNullableInt32(reader, "encounter"),
                StorageMethod: ReadNullableString(reader, "storage_method"),
                FileName: ReadNullableString(reader, "file_name"),
                Url: ReadNullableString(reader, "url"),
                Hash: ReadNullableString(reader, "hash"),
                DocumentationOf: ReadNullableString(reader, "documentation_of"),
                Notes: ReadNullableString(reader, "notes"),
                Deleted: reader.GetInt32(reader.GetOrdinal("deleted")),
                ReviewStatus: reader.GetString(reader.GetOrdinal("review_status")),
                ReviewedBy: ReadNullableString(reader, "reviewed_by"),
                ReviewedAt: ReadNullableDateTimeString(reader, "reviewed_at"),
                ContentPreview: ReadNullableString(reader, "content_preview")));
        }

        return items;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
        return categoryId switch
        {
            2 => "Lab Report",
            3 => "Medical Record",
            4 => "Patient Information",
            5 => "Patient ID card",
            6 => "Advance Directive",
            13 => "CCDA",
            29 => "Reviewed",
            31 => "Invoices",
            _ => "Medical Record"
        };
    }

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
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
}
