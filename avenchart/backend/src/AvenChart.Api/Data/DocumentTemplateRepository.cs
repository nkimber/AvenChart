// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Compression;
using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class DocumentTemplateRepository(NpgsqlDataSource source)
{
    private const int MaxBinaryBytes = 25 * 1024 * 1024;
    private const int HistoryLimit = 100;

    public async Task<DocumentTemplateListResponse> GetAsync(
        string? search,
        bool includeInactive,
        int offset,
        int limit,
        CancellationToken ct)
    {
        var normalizedSearch = NormalizeSearch(search);
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = Math.Clamp(limit, 1, 100);
        await using var connection = await source.OpenConnectionAsync(ct);

        await using var count = connection.CreateCommand();
        count.CommandText = """
            select
              count(*) filter (where @includeInactive or active)::int,
              count(*) filter (where active)::int,
              count(*) filter (where not active)::int
            from document_templates
            where @search is null
               or name ilike '%' || @search || '%'
               or content ilike '%' || @search || '%';
            """;
        AddListParameters(count, normalizedSearch, includeInactive);
        int total;
        int activeCount;
        int retiredCount;
        await using (var reader = await count.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            total = reader.GetInt32(0);
            activeCount = reader.GetInt32(1);
            retiredCount = reader.GetInt32(2);
        }

        await using var query = connection.CreateCommand();
        query.CommandText = """
            select id,name,content,active,created_at,updated_at
            from document_templates
            where (@includeInactive or active)
              and (@search is null or name ilike '%' || @search || '%' or content ilike '%' || @search || '%')
            order by active desc, name, id
            offset @offset limit @limit;
            """;
        AddListParameters(query, normalizedSearch, includeInactive);
        query.Parameters.AddWithValue("offset", boundedOffset);
        query.Parameters.AddWithValue("limit", boundedLimit);
        var items = new List<DocumentTemplateItem>();
        await using (var reader = await query.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                items.Add(Read(reader));
            }
        }

        return new(
            normalizedSearch ?? string.Empty,
            includeInactive,
            boundedOffset,
            boundedLimit,
            total,
            activeCount,
            retiredCount,
            items);
    }

    public async Task<DocumentTemplateItem?> SaveAsync(
        Guid? id,
        DocumentTemplateRequest request,
        string username,
        CancellationToken ct)
    {
        var name = NormalizeName(request.Name);
        var content = NormalizeContent(request.Content);
        var actor = NormalizeActor(username);
        var key = id ?? Guid.NewGuid();

        try
        {
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            string action;
            string summary;

            if (id is not null)
            {
                await using var prior = connection.CreateCommand();
                prior.Transaction = transaction;
                prior.CommandText = "select name,active from document_templates where id=@id for update;";
                prior.Parameters.AddWithValue("id", key);
                await using var reader = await prior.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                var priorName = reader.GetString(0);
                var priorActive = reader.GetBoolean(1);
                action = priorActive == request.Active
                    ? "updated"
                    : request.Active
                        ? "activated"
                        : "retired";
                summary = action switch
                {
                    "activated" => $"Activated template \"{name}\".",
                    "retired" => $"Retired template \"{name}\".",
                    _ when !string.Equals(priorName, name, StringComparison.Ordinal) =>
                        $"Updated and renamed template \"{priorName}\" to \"{name}\".",
                    _ => $"Updated template \"{name}\"."
                };
            }
            else
            {
                action = "created";
                summary = $"Created template \"{name}\".";
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = id is null
                ? """
                  insert into document_templates(id,name,content,active)
                  values(@id,@name,@content,@active)
                  returning id,name,content,active,created_at,updated_at;
                  """
                : """
                  update document_templates
                  set name=@name,content=@content,active=@active,updated_at=now()
                  where id=@id
                  returning id,name,content,active,created_at,updated_at;
                  """;
            command.Parameters.AddWithValue("id", key);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("content", content);
            command.Parameters.AddWithValue("active", request.Active);

            DocumentTemplateItem? result;
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                result = await reader.ReadAsync(ct) ? Read(reader) : null;
            }

            if (result is null)
            {
                return null;
            }

            await WriteEventAsync(
                connection,
                transaction,
                key,
                action,
                summary,
                null,
                null,
                null,
                actor,
                ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DocumentTemplateNameConflictException(
                $"A document template named \"{name}\" already exists.");
        }
    }

    public async Task<DocumentTemplateRenderResult?> RenderAsync(
        Guid id,
        DocumentTemplateRenderRequest request,
        CancellationToken ct)
    {
        var patientId = NormalizePatientId(request.PatientId);
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              t.id,t.name,t.content,t.active,t.created_at,t.updated_at,
              p.first_name,p.last_name,p.date_of_birth,p.pubpid
            from document_templates t
            join patients p on p.canonical_id=@patient
            where t.id=@id and t.active;
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patient", patientId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var template = Read(reader);
        var content = ReplaceTokens(
            template.Content,
            reader.GetString(6),
            reader.GetString(7),
            reader.GetFieldValue<DateOnly>(8),
            reader.GetString(9));
        return new(template, patientId, content);
    }

    public async Task<IReadOnlyList<DocumentTemplateBinaryVersion>> GetBinaryVersionsAsync(
        Guid templateId,
        CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id,template_id,version,file_name,mimetype,size_bytes,sha256,created_at
            from document_template_binary_versions
            where template_id=@templateId
            order by version desc;
            """;
        command.Parameters.AddWithValue("templateId", templateId);
        var results = new List<DocumentTemplateBinaryVersion>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadVersion(reader));
        }

        return results;
    }

    public async Task<DocumentTemplateBinaryVersion?> AddBinaryVersionAsync(
        Guid templateId,
        DocumentTemplateBinaryUploadRequest request,
        string username,
        CancellationToken ct)
    {
        var upload = DecodeAndValidate(request);
        var actor = NormalizeActor(username);
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var template = connection.CreateCommand();
        template.Transaction = transaction;
        template.CommandText = "select name from document_templates where id=@id for update;";
        template.Parameters.AddWithValue("id", templateId);
        var templateName = await template.ExecuteScalarAsync(ct) as string;
        if (templateName is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into document_template_binary_versions(
              id,template_id,version,file_name,mimetype,size_bytes,sha256,content)
            values(
              @id,@templateId,
              (select avenchart_next_integer(
                   concat('document_template_binary_versions.version:', @templateId),
                   coalesce(max(version), 0))
               from document_template_binary_versions
               where template_id=@templateId),
              @fileName,@mimetype,@size,@sha,@content)
            returning id,template_id,version,file_name,mimetype,size_bytes,sha256,created_at;
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("templateId", templateId);
        command.Parameters.AddWithValue("fileName", upload.FileName);
        command.Parameters.AddWithValue("mimetype", upload.Mimetype);
        command.Parameters.AddWithValue("size", upload.Content.Length);
        command.Parameters.AddWithValue(
            "sha",
            Convert.ToHexString(SHA256.HashData(upload.Content)).ToLowerInvariant());
        command.Parameters.Add("content", NpgsqlDbType.Bytea).Value = upload.Content;

        DocumentTemplateBinaryVersion? version;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            version = await reader.ReadAsync(ct) ? ReadVersion(reader) : null;
        }

        if (version is null)
        {
            return null;
        }

        await WriteEventAsync(
            connection,
            transaction,
            templateId,
            "binary-version-uploaded",
            $"Uploaded binary version {version.Version} ({version.FileName}) for template \"{templateName}\".",
            version.Id,
            null,
            null,
            actor,
            ct);
        await transaction.CommitAsync(ct);
        return version;
    }

    public async Task<DocumentTemplateBinaryDownload?> GetBinaryAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select file_name,mimetype,content
            from document_template_binary_versions
            where id=@id and template_id=@templateId;
            """;
        command.Parameters.AddWithValue("id", versionId);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<byte[]>(2))
            : null;
    }

    public async Task<DocumentTemplateHistoryResponse?> GetHistoryAsync(
        Guid templateId,
        CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        DocumentTemplateItem? template;
        await using (var templateCommand = connection.CreateCommand())
        {
            templateCommand.CommandText = """
                select id,name,content,active,created_at,updated_at
                from document_templates
                where id=@id;
                """;
            templateCommand.Parameters.AddWithValue("id", templateId);
            await using var reader = await templateCommand.ExecuteReaderAsync(ct);
            template = await reader.ReadAsync(ct) ? Read(reader) : null;
        }

        if (template is null)
        {
            return null;
        }

        int eventCount;
        await using (var count = connection.CreateCommand())
        {
            count.CommandText = """
                select count(*)::int
                from document_template_events
                where template_id=@id;
                """;
            count.Parameters.AddWithValue("id", templateId);
            eventCount = (int)(await count.ExecuteScalarAsync(ct) ?? 0);
        }

        var events = new List<DocumentTemplateEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                  event_id,template_id,action,summary,binary_version_id,
                  patient_document_id,patient_id,occurred_at,username
                from document_template_events
                where template_id=@id
                order by occurred_at desc,event_id desc
                limit @limit;
                """;
            command.Parameters.AddWithValue("id", templateId);
            command.Parameters.AddWithValue("limit", HistoryLimit);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                events.Add(ReadEvent(reader));
            }
        }

        return new(template, eventCount, events.Count, HistoryLimit, events);
    }

    public async Task RecordAttachmentGeneratedAsync(
        Guid templateId,
        Guid? binaryVersionId,
        long patientDocumentId,
        string patientId,
        string username,
        CancellationToken ct)
    {
        var normalizedPatientId = NormalizePatientId(patientId);
        var actor = NormalizeActor(username);
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var template = connection.CreateCommand();
        template.Transaction = transaction;
        template.CommandText = "select name from document_templates where id=@id for update;";
        template.Parameters.AddWithValue("id", templateId);
        var templateName = await template.ExecuteScalarAsync(ct) as string;
        if (templateName is null)
        {
            throw new ArgumentException("The document template no longer exists.");
        }

        await WriteEventAsync(
            connection,
            transaction,
            templateId,
            "patient-attachment-generated",
            $"Generated patient document {patientDocumentId} for {normalizedPatientId} from template \"{templateName}\".",
            binaryVersionId,
            patientDocumentId,
            normalizedPatientId,
            actor,
            ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<bool> DeleteTestFixtureAsync(
        Guid templateId,
        CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var template = connection.CreateCommand();
        template.Transaction = transaction;
        template.CommandText = "select name from document_templates where id=@id for update;";
        template.Parameters.AddWithValue("id", templateId);
        var name = await template.ExecuteScalarAsync(ct) as string;
        if (name is null)
        {
            return false;
        }

        if (!name.StartsWith("TMP-DOC-TEMPLATE-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only TMP-DOC-TEMPLATE-* browser-test fixtures can be removed through this cleanup route.");
        }

        await using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "delete from document_templates where id=@id;";
        delete.Parameters.AddWithValue("id", templateId);
        var deleted = await delete.ExecuteNonQueryAsync(ct) == 1;
        await transaction.CommitAsync(ct);
        return deleted;
    }

    private static void AddListParameters(
        NpgsqlCommand command,
        string? search,
        bool includeInactive)
    {
        command.Parameters.AddWithValue("includeInactive", includeInactive);
        command.Parameters.Add(
            new NpgsqlParameter("search", NpgsqlDbType.Text)
            {
                Value = (object?)search ?? DBNull.Value
            });
    }

    private static async Task WriteEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid templateId,
        string action,
        string summary,
        Guid? binaryVersionId,
        long? patientDocumentId,
        string? patientId,
        string username,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into document_template_events(
              template_id,action,summary,binary_version_id,patient_document_id,
              patient_id,occurred_at,username)
            values(
              @templateId,@action,@summary,@binaryVersionId,@patientDocumentId,
              @patientId,now(),@username);
            """;
        command.Parameters.AddWithValue("templateId", templateId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("summary", summary);
        command.Parameters.AddWithValue(
            "binaryVersionId",
            (object?)binaryVersionId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "patientDocumentId",
            (object?)patientDocumentId ?? DBNull.Value);
        command.Parameters.AddWithValue("patientId", (object?)patientId ?? DBNull.Value);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static BinaryUpload DecodeAndValidate(DocumentTemplateBinaryUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) ||
            string.IsNullOrWhiteSpace(request.Mimetype) ||
            string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            throw new ArgumentException("File name, MIME type, and content are required.");
        }

        var fileName = Path.GetFileName(request.FileName.Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
        {
            throw new ArgumentException("Template file name must be between 1 and 255 characters.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var mimetype = extension switch
        {
            ".txt" => "text/plain",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".zip" => "application/zip",
            _ => throw new ArgumentException(
                "Only TXT, ODT, DOCX, and ZIP template files are supported.")
        };

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.ContentBase64.Trim());
        }
        catch (FormatException)
        {
            throw new ArgumentException("Template content must be base64 encoded.");
        }

        if (bytes.Length == 0 || bytes.Length > MaxBinaryBytes)
        {
            throw new ArgumentException("Template file must be between 1 byte and 25 MB.");
        }

        if (extension == ".zip")
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                if (archive.Entries.Any(entry =>
                        entry.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Nested ZIP templates are not supported.");
                }
            }
            catch (InvalidDataException)
            {
                throw new ArgumentException("ZIP template content is invalid.");
            }
        }

        return new(fileName, mimetype, bytes);
    }

    private static string NormalizeName(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A template name is required.");
        }

        if (normalized.Length > 120)
        {
            throw new ArgumentException("Template name may not exceed 120 characters.");
        }

        return normalized;
    }

    private static string NormalizeContent(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Template text content is required.");
        }

        if (normalized.Length > 250_000)
        {
            throw new ArgumentException("Template text content may not exceed 250,000 characters.");
        }

        return normalized;
    }

    private static string? NormalizeSearch(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > 120)
        {
            throw new ArgumentException("Template search may not exceed 120 characters.");
        }

        return normalized;
    }

    private static string NormalizePatientId(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A patient is required.");
        }

        if (normalized.Length > 100)
        {
            throw new ArgumentException("Patient identifier may not exceed 100 characters.");
        }

        return normalized;
    }

    private static string NormalizeActor(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("An authenticated template actor is required.");
        }

        return normalized;
    }

    private static string ReplaceTokens(
        string content,
        string first,
        string last,
        DateOnly dob,
        string pubpid) =>
        content
            .Replace("***NAME***", $"{first} {last}", StringComparison.Ordinal)
            .Replace("***DOB***", dob.ToString("yyyy-MM-dd"), StringComparison.Ordinal)
            .Replace("***PATIENT_ID***", pubpid, StringComparison.Ordinal);

    private static DocumentTemplateItem Read(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetFieldValue<DateTimeOffset>(4).ToString("O"),
            reader.GetFieldValue<DateTimeOffset>(5).ToString("O"));

    private static DocumentTemplateBinaryVersion ReadVersion(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7).ToString("O"));

    private static DocumentTemplateEvent ReadEvent(NpgsqlDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),
            reader.GetString(8));

    private sealed record BinaryUpload(
        string FileName,
        string Mimetype,
        byte[] Content);
}
