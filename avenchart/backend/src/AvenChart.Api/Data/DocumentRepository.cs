using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class DocumentRepository(NpgsqlDataSource dataSource)
{
    public async Task<PatientDocumentsResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var documents = await GetDocumentsAsync(connection, patient.PatientId, cancellationToken);

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
                         mimetype, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
                         content, deleted)
                    values
                        (@id, @documentKey, @patientId, @pid, @categoryId, @categoryName, @name, @docDate, @uploadedAt,
                         'text/plain', @sizeBytes, 1, @encounter, 'database', @url, @hash, @documentationOf, @notes,
                         @content, 0);
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
              mimetype, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
              coalesce(content, '') as content
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

        return new PatientDocumentContentResponse(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
            CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
            Name: name,
            FileName: BuildDownloadFileName(name, mimetype),
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
            Content: content);
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
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, patient_id, pid, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, size_bytes, pages, encounter, storage_method, url, hash, documentation_of, notes,
              left(regexp_replace(coalesce(content, ''), E'[\\r\\n]+', ' ', 'g'), 260) as content_preview
            from patient_documents
            where patient_id = @patientId and deleted = 0
            order by doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

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
                Url: ReadNullableString(reader, "url"),
                Hash: ReadNullableString(reader, "hash"),
                DocumentationOf: ReadNullableString(reader, "documentation_of"),
                Notes: ReadNullableString(reader, "notes"),
                ContentPreview: ReadNullableString(reader, "content_preview")));
        }

        return items;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
        var safeName = string.Join(
            "_",
            name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "document";
        }

        if (Path.HasExtension(safeName))
        {
            return safeName;
        }

        return string.Equals(mimetype, "text/plain", StringComparison.OrdinalIgnoreCase)
            ? $"{safeName}.txt"
            : safeName;
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
