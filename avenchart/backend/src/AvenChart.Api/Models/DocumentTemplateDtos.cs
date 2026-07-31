// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Models;

public sealed record DocumentTemplateItem(
    Guid Id,
    string Name,
    string Content,
    bool Active,
    string CreatedAt,
    string UpdatedAt);

public sealed record DocumentTemplateListResponse(
    string Search,
    bool IncludeInactive,
    int Offset,
    int Limit,
    int Total,
    int ActiveCount,
    int RetiredCount,
    IReadOnlyList<DocumentTemplateItem> Items);

public sealed record DocumentTemplateRequest(
    string Name,
    string Content,
    bool Active);

public sealed record DocumentTemplateRenderRequest(string PatientId);

public sealed record DocumentTemplateRenderResult(
    DocumentTemplateItem Template,
    string PatientId,
    string Content);

public sealed record DocumentTemplateBinaryUploadRequest(
    string FileName,
    string Mimetype,
    string ContentBase64);

public sealed record DocumentTemplateBinaryVersion(
    Guid Id,
    Guid TemplateId,
    int Version,
    string FileName,
    string Mimetype,
    int SizeBytes,
    string Sha256,
    string CreatedAt);

public sealed record DocumentTemplateBinaryDownload(
    string FileName,
    string Mimetype,
    byte[] Content);

public sealed record DocumentTemplateAttachmentRequest(
    string PatientId,
    int CategoryId,
    int? Encounter,
    string? DocDate,
    Guid? BinaryVersionId);

public sealed record DocumentTemplateEvent(
    long EventId,
    Guid TemplateId,
    string Action,
    string Summary,
    Guid? BinaryVersionId,
    long? PatientDocumentId,
    string? PatientId,
    string OccurredAt,
    string Username);

public sealed record DocumentTemplateHistoryResponse(
    DocumentTemplateItem Template,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<DocumentTemplateEvent> Events);

public sealed class DocumentTemplateNameConflictException(string message)
    : Exception(message);
