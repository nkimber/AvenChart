// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Compression;
using System.Security.Cryptography;
using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Data;

public sealed class DocumentTemplateRepository(AvenChartDbContext dbContext)
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
        var searched = dbContext.DocumentTemplates.AsNoTracking().AsQueryable();
        if (normalizedSearch is not null)
        {
            var pattern = $"%{normalizedSearch}%";
            searched = searched.Where(template =>
                EF.Functions.ILike(template.Name, pattern) ||
                EF.Functions.ILike(template.Content, pattern));
        }

        var total = await searched.CountAsync(
            template => includeInactive || template.Active,
            ct);
        var activeCount = await searched.CountAsync(template => template.Active, ct);
        var retiredCount = await searched.CountAsync(template => !template.Active, ct);
        var items = await searched
            .Where(template => includeInactive || template.Active)
            .OrderByDescending(template => template.Active)
            .ThenBy(template => template.Name)
            .ThenBy(template => template.Id)
            .Skip(boundedOffset)
            .Take(boundedLimit)
            .ToListAsync(ct);

        return new DocumentTemplateListResponse(
            normalizedSearch ?? string.Empty,
            includeInactive,
            boundedOffset,
            boundedLimit,
            total,
            activeCount,
            retiredCount,
            items.Select(ToItem).ToList());
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
        var now = DateTimeOffset.UtcNow;
        var template = id is null
            ? new DocumentTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                Content = content,
                Active = request.Active,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = 1
            }
            : await dbContext.DocumentTemplates.SingleOrDefaultAsync(
                candidate => candidate.Id == id.Value,
                ct);
        if (template is null)
        {
            return null;
        }

        string action;
        string summary;
        if (id is null)
        {
            action = "created";
            summary = $"Created template \"{name}\".";
            dbContext.DocumentTemplates.Add(template);
        }
        else
        {
            var priorName = template.Name;
            var priorActive = template.Active;
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
            template.Name = name;
            template.Content = content;
            template.Active = request.Active;
            template.UpdatedAt = now;
            template.RowVersion++;
        }

        dbContext.DocumentTemplateEvents.Add(CreateEvent(
            template.Id,
            action,
            summary,
            null,
            null,
            null,
            actor,
            now));
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return ToItem(template);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DocumentTemplateNameConflictException(
                $"A document template named \"{name}\" already exists.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DocumentTemplateConcurrencyException(
                "The document template changed before this update could be saved.");
        }
    }

    public async Task<DocumentTemplateRenderResult?> RenderAsync(
        Guid id,
        DocumentTemplateRenderRequest request,
        CancellationToken ct)
    {
        var patientId = NormalizePatientId(request.PatientId);
        var result = await (
                from template in dbContext.DocumentTemplates.AsNoTracking()
                from patient in dbContext.Patients.AsNoTracking()
                where template.Id == id && template.Active && patient.CanonicalId == patientId
                select new
                {
                    Template = template,
                    patient.FirstName,
                    patient.LastName,
                    patient.DateOfBirth,
                    patient.PublicId
                })
            .SingleOrDefaultAsync(ct);
        if (result is null)
        {
            return null;
        }

        var templateItem = ToItem(result.Template);
        var content = ReplaceTokens(
            templateItem.Content,
            result.FirstName,
            result.LastName,
            result.DateOfBirth,
            result.PublicId);
        return new DocumentTemplateRenderResult(templateItem, patientId, content);
    }

    public async Task<IReadOnlyList<DocumentTemplateBinaryVersion>> GetBinaryVersionsAsync(
        Guid templateId,
        CancellationToken ct)
    {
        var versions = await dbContext.DocumentTemplateBinaryVersions
            .AsNoTracking()
            .Where(version => version.TemplateId == templateId)
            .OrderByDescending(version => version.Version)
            .ToListAsync(ct);
        return versions.Select(ToVersion).ToList();
    }

    // The scoped version number is allocated by the database so concurrent uploads cannot
    // choose the same per-template value. The version row and its EF event share one transaction.
    public async Task<DocumentTemplateBinaryVersion?> AddBinaryVersionAsync(
        Guid templateId,
        DocumentTemplateBinaryUploadRequest request,
        string username,
        CancellationToken ct)
    {
        var upload = DecodeAndValidate(request);
        var actor = NormalizeActor(username);
        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(ct);
        var templateName = await dbContext.DocumentTemplates
            .AsNoTracking()
            .Where(template => template.Id == templateId)
            .Select(template => template.Name)
            .SingleOrDefaultAsync(ct);
        if (templateName is null)
        {
            return null;
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)dbTransaction.GetDbTransaction();
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

        dbContext.DocumentTemplateEvents.Add(CreateEvent(
            templateId,
            "binary-version-uploaded",
            $"Uploaded binary version {version.Version} ({version.FileName}) for template \"{templateName}\".",
            version.Id,
            null,
            null,
            actor,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(ct);
        await dbTransaction.CommitAsync(ct);
        return version;
    }

    public async Task<DocumentTemplateBinaryDownload?> GetBinaryAsync(
        Guid templateId,
        Guid versionId,
        CancellationToken ct)
    {
        var version = await dbContext.DocumentTemplateBinaryVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == versionId && candidate.TemplateId == templateId,
                ct);
        return version is null
            ? null
            : new DocumentTemplateBinaryDownload(
                version.FileName,
                version.Mimetype,
                version.Content);
    }

    public async Task<DocumentTemplateHistoryResponse?> GetHistoryAsync(
        Guid templateId,
        CancellationToken ct)
    {
        var template = await dbContext.DocumentTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == templateId, ct);
        if (template is null)
        {
            return null;
        }

        var eventCount = await dbContext.DocumentTemplateEvents
            .AsNoTracking()
            .CountAsync(templateEvent => templateEvent.TemplateId == templateId, ct);
        var eventEntities = await dbContext.DocumentTemplateEvents
            .AsNoTracking()
            .Where(templateEvent => templateEvent.TemplateId == templateId)
            .OrderByDescending(templateEvent => templateEvent.OccurredAt)
            .ThenByDescending(templateEvent => templateEvent.EventId)
            .Take(HistoryLimit)
            .ToListAsync(ct);
        var events = eventEntities.Select(ToEvent).ToList();
        return new DocumentTemplateHistoryResponse(
            ToItem(template),
            eventCount,
            events.Count,
            HistoryLimit,
            events);
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
        var templateName = await dbContext.DocumentTemplates
            .AsNoTracking()
            .Where(template => template.Id == templateId)
            .Select(template => template.Name)
            .SingleOrDefaultAsync(ct)
            ?? throw new ArgumentException("The document template no longer exists.");
        dbContext.DocumentTemplateEvents.Add(CreateEvent(
            templateId,
            "patient-attachment-generated",
            $"Generated patient document {patientDocumentId} for {normalizedPatientId} from template \"{templateName}\".",
            binaryVersionId,
            patientDocumentId,
            normalizedPatientId,
            actor,
            DateTimeOffset.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ArgumentException("The document template no longer exists.");
        }
    }

    public async Task<bool> DeleteTestFixtureAsync(
        Guid templateId,
        CancellationToken ct)
    {
        var template = await dbContext.DocumentTemplates.SingleOrDefaultAsync(
            candidate => candidate.Id == templateId,
            ct);
        if (template is null)
        {
            return false;
        }

        if (!template.Name.StartsWith("TMP-DOC-TEMPLATE-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only TMP-DOC-TEMPLATE-* browser-test fixtures can be removed through this cleanup route.");
        }

        dbContext.DocumentTemplates.Remove(template);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static DocumentTemplateEventEntity CreateEvent(
        Guid templateId,
        string action,
        string summary,
        Guid? binaryVersionId,
        long? patientDocumentId,
        string? patientId,
        string username,
        DateTimeOffset occurredAt) =>
        new()
        {
            TemplateId = templateId,
            Action = action,
            Summary = summary,
            BinaryVersionId = binaryVersionId,
            PatientDocumentId = patientDocumentId,
            PatientId = patientId,
            OccurredAt = occurredAt,
            Username = username
        };

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
            throw new ArgumentException(
                "Template file name must be between 1 and 255 characters.");
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

        return new BinaryUpload(fileName, mimetype, bytes);
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
            throw new ArgumentException(
                "Template text content may not exceed 250,000 characters.");
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

    private static DocumentTemplateItem ToItem(DocumentTemplateEntity template) =>
        new(
            template.Id,
            template.Name,
            template.Content,
            template.Active,
            template.CreatedAt.ToString("O"),
            template.UpdatedAt.ToString("O"));

    private static DocumentTemplateBinaryVersion ToVersion(
        DocumentTemplateBinaryVersionEntity version) =>
        new(
            version.Id,
            version.TemplateId,
            version.Version,
            version.FileName,
            version.Mimetype,
            version.SizeBytes,
            version.Sha256,
            version.CreatedAt.ToString("O"));

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

    private static DocumentTemplateEvent ToEvent(DocumentTemplateEventEntity templateEvent) =>
        new(
            templateEvent.EventId,
            templateEvent.TemplateId,
            templateEvent.Action,
            templateEvent.Summary,
            templateEvent.BinaryVersionId,
            templateEvent.PatientDocumentId,
            templateEvent.PatientId,
            templateEvent.OccurredAt.ToString("O"),
            templateEvent.Username);

    private sealed record BinaryUpload(
        string FileName,
        string Mimetype,
        byte[] Content);
}
