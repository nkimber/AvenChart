using System.IO.Compression;
using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class DocumentTemplateRepository(NpgsqlDataSource source)
{
    private const int MaxBinaryBytes = 25 * 1024 * 1024;

    public async Task<IReadOnlyList<DocumentTemplateItem>> GetAsync(bool includeInactive, CancellationToken ct)
    {
        await using var c = await source.OpenConnectionAsync(ct); await using var q = c.CreateCommand();
        q.CommandText = $"select id,name,content,active,created_at,updated_at from document_templates {(includeInactive ? "" : "where active")} order by name;";
        var items = new List<DocumentTemplateItem>(); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) items.Add(Read(r)); return items;
    }

    public async Task<DocumentTemplateItem?> SaveAsync(Guid? id, DocumentTemplateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content)) throw new ArgumentException("Template name and text content are required.");
        await using var c = await source.OpenConnectionAsync(ct); await using var q = c.CreateCommand(); var key = id ?? Guid.NewGuid();
        q.CommandText = id is null ? "insert into document_templates(id,name,content,active) values(@id,@name,@content,@active) returning id,name,content,active,created_at,updated_at;" : "update document_templates set name=@name,content=@content,active=@active,updated_at=now() where id=@id returning id,name,content,active,created_at,updated_at;";
        q.Parameters.AddWithValue("id", key); q.Parameters.AddWithValue("name", request.Name.Trim()); q.Parameters.AddWithValue("content", request.Content.Trim()); q.Parameters.AddWithValue("active", request.Active);
        await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<DocumentTemplateRenderResult?> RenderAsync(Guid id, DocumentTemplateRenderRequest request, CancellationToken ct)
    {
        await using var c = await source.OpenConnectionAsync(ct); await using var q = c.CreateCommand();
        q.CommandText = "select t.id,t.name,t.content,t.active,t.created_at,t.updated_at,p.first_name,p.last_name,p.date_of_birth,p.pubpid from document_templates t join patients p on p.canonical_id=@patient where t.id=@id and t.active;";
        q.Parameters.AddWithValue("id", id); q.Parameters.AddWithValue("patient", request.PatientId); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null;
        var template = Read(r); var content = ReplaceTokens(template.Content, r.GetString(6), r.GetString(7), r.GetFieldValue<DateOnly>(8), r.GetString(9)); return new(template, request.PatientId, content);
    }

    public async Task<IReadOnlyList<DocumentTemplateBinaryVersion>> GetBinaryVersionsAsync(Guid templateId, CancellationToken ct)
    {
        await using var c = await source.OpenConnectionAsync(ct); await using var q = c.CreateCommand();
        q.CommandText = "select id,template_id,version,file_name,mimetype,size_bytes,sha256,created_at from document_template_binary_versions where template_id=@templateId order by version desc;"; q.Parameters.AddWithValue("templateId", templateId);
        var results = new List<DocumentTemplateBinaryVersion>(); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) results.Add(ReadVersion(r)); return results;
    }

    public async Task<DocumentTemplateBinaryVersion?> AddBinaryVersionAsync(Guid templateId, DocumentTemplateBinaryUploadRequest request, CancellationToken ct)
    {
        var bytes = DecodeAndValidate(request);
        await using var c = await source.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using var exists = c.CreateCommand(); exists.Transaction = tx; exists.CommandText = "select exists(select 1 from document_templates where id=@id);"; exists.Parameters.AddWithValue("id", templateId);
        if (!(bool)(await exists.ExecuteScalarAsync(ct) ?? false)) return null;
        await using var q = c.CreateCommand(); q.Transaction = tx;
        q.CommandText = "insert into document_template_binary_versions(id,template_id,version,file_name,mimetype,size_bytes,sha256,content) values(@id,@templateId,(select coalesce(max(version),0)+1 from document_template_binary_versions where template_id=@templateId),@fileName,@mimetype,@size,@sha,@content) returning id,template_id,version,file_name,mimetype,size_bytes,sha256,created_at;";
        q.Parameters.AddWithValue("id", Guid.NewGuid()); q.Parameters.AddWithValue("templateId", templateId); q.Parameters.AddWithValue("fileName", request.FileName.Trim()); q.Parameters.AddWithValue("mimetype", request.Mimetype.Trim()); q.Parameters.AddWithValue("size", bytes.Length); q.Parameters.AddWithValue("sha", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()); q.Parameters.Add("content", NpgsqlDbType.Bytea).Value = bytes;
        await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; var version = ReadVersion(r); await r.DisposeAsync(); await tx.CommitAsync(ct); return version;
    }

    public async Task<DocumentTemplateBinaryDownload?> GetBinaryAsync(Guid templateId, Guid versionId, CancellationToken ct)
    {
        await using var c = await source.OpenConnectionAsync(ct); await using var q = c.CreateCommand(); q.CommandText = "select file_name,mimetype,content from document_template_binary_versions where id=@id and template_id=@templateId;"; q.Parameters.AddWithValue("id", versionId); q.Parameters.AddWithValue("templateId", templateId); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetFieldValue<byte[]>(2)) : null;
    }

    private static byte[] DecodeAndValidate(DocumentTemplateBinaryUploadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Mimetype) || string.IsNullOrWhiteSpace(request.ContentBase64)) throw new ArgumentException("File name, MIME type, and content are required.");
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant(); if (extension is not (".txt" or ".odt" or ".docx" or ".zip")) throw new ArgumentException("Only TXT, ODT, DOCX, and ZIP template files are supported.");
        byte[] bytes; try { bytes = Convert.FromBase64String(request.ContentBase64.Trim()); } catch (FormatException) { throw new ArgumentException("Template content must be base64 encoded."); }
        if (bytes.Length == 0 || bytes.Length > MaxBinaryBytes) throw new ArgumentException("Template file must be between 1 byte and 25 MB.");
        if (extension == ".zip") { try { using var stream = new MemoryStream(bytes); using var archive = new ZipArchive(stream, ZipArchiveMode.Read); if (archive.Entries.Any(entry => entry.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Nested ZIP templates are not supported."); } catch (InvalidDataException) { throw new ArgumentException("ZIP template content is invalid."); } }
        return bytes;
    }
    private static string ReplaceTokens(string content, string first, string last, DateOnly dob, string pubpid) => content.Replace("***NAME***", $"{first} {last}", StringComparison.Ordinal).Replace("***DOB***", dob.ToString("yyyy-MM-dd"), StringComparison.Ordinal).Replace("***PATIENT_ID***", pubpid, StringComparison.Ordinal);
    private static DocumentTemplateItem Read(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetBoolean(3), r.GetFieldValue<DateTimeOffset>(4).ToString("O"), r.GetFieldValue<DateTimeOffset>(5).ToString("O"));
    private static DocumentTemplateBinaryVersion ReadVersion(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.GetString(4), r.GetInt32(5), r.GetString(6), r.GetFieldValue<DateTimeOffset>(7).ToString("O"));
}
