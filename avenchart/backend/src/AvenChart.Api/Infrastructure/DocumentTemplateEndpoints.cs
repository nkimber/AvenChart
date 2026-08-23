// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps document-template authoring, versioning, rendering, and attachment routes as one aggregate.
/// </summary>
public static class DocumentTemplateEndpoints
{
    public static RouteGroupBuilder MapDocumentTemplateEndpoints(this WebApplication app)
    {
        var documentTemplates = app
            .MapGroup("/api/administration/document-templates")
            .WithTags("Document Templates");
        RequireAccessPermission(documentTemplates, "admin", "super", "view");

        documentTemplates
            .MapGet("/", async (
                DocumentTemplateRepository repository,
                string? search,
                bool? includeInactive,
                int? offset,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.GetAsync(
                        search,
                        includeInactive ?? true,
                        offset ?? 0,
                        limit ?? 10,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("GetDocumentTemplates");

        documentTemplates
            .MapPost("/", async (
                DocumentTemplateRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                DocumentTemplateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var item = await repository.SaveAsync(
                        null,
                        request,
                        session.Username,
                        cancellationToken);
                    return Results.Created(
                        $"/api/administration/document-templates/{item!.Id}",
                        item);
                }
                catch (DocumentTemplateNameConflictException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (DocumentTemplateConcurrencyException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("CreateDocumentTemplate")
            .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

        documentTemplates
            .MapPut("/{id:guid}", async (
                DocumentTemplateRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid id,
                DocumentTemplateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var item = await repository.SaveAsync(
                        id,
                        request,
                        session.Username,
                        cancellationToken);
                    return item is null ? Results.NotFound() : Results.Ok(item);
                }
                catch (DocumentTemplateNameConflictException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (DocumentTemplateConcurrencyException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status409Conflict);
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("UpdateDocumentTemplate")
            .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

        documentTemplates
            .MapPost("/{id:guid}/render", async (
                DocumentTemplateRepository repository,
                Guid id,
                DocumentTemplateRenderRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var item = await repository.RenderAsync(id, request, cancellationToken);
                    return item is null ? Results.NotFound() : Results.Ok(item);
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("RenderDocumentTemplate");

        documentTemplates
            .MapGet("/{id:guid}/history", async (
                DocumentTemplateRepository repository,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetHistoryAsync(id, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetDocumentTemplateHistory");

        documentTemplates
            .MapGet("/{id:guid}/binary-versions", async (
                DocumentTemplateRepository repository,
                Guid id,
                CancellationToken cancellationToken) =>
                Results.Ok(await repository.GetBinaryVersionsAsync(id, cancellationToken)))
            .WithName("GetDocumentTemplateBinaryVersions");

        documentTemplates
            .MapPost("/{id:guid}/binary-versions", async (
                DocumentTemplateRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid id,
                DocumentTemplateBinaryUploadRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var item = await repository.AddBinaryVersionAsync(
                        id,
                        request,
                        session.Username,
                        cancellationToken);
                    return item is null
                        ? Results.NotFound()
                        : Results.Created(
                            $"/api/administration/document-templates/{id}/binary-versions/{item.Id}",
                            item);
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("AddDocumentTemplateBinaryVersion")
            .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

        documentTemplates
            .MapGet("/{id:guid}/binary-versions/{versionId:guid}/download", async (
                DocumentTemplateRepository repository,
                Guid id,
                Guid versionId,
                CancellationToken cancellationToken) =>
            {
                var item = await repository.GetBinaryAsync(id, versionId, cancellationToken);
                return item is null
                    ? Results.NotFound()
                    : Results.File(item.Content, item.Mimetype, item.FileName);
            })
            .WithName("DownloadDocumentTemplateBinaryVersion");

        documentTemplates
            .MapPost("/{id:guid}/generate-attachment", async (
                DocumentTemplateRepository repository,
                DocumentRepository documents,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid id,
                DocumentTemplateAttachmentRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var documentDate = string.IsNullOrWhiteSpace(request.DocDate)
                        ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
                        : request.DocDate;

                    if (request.BinaryVersionId is { } versionId)
                    {
                        var binary = await repository.GetBinaryAsync(
                            id,
                            versionId,
                            cancellationToken);
                        if (binary is null)
                        {
                            return Results.NotFound();
                        }

                        var mutation = await documents.CreateBinaryAsync(
                            new PatientDocumentBinaryCreateRequest(
                                request.PatientId,
                                request.CategoryId,
                                $"Template: {Path.GetFileNameWithoutExtension(binary.FileName)}",
                                documentDate,
                                request.Encounter,
                                binary.FileName,
                                binary.Mimetype,
                                Convert.ToBase64String(binary.Content),
                                $"Generated from document template {id}, binary version {versionId}."),
                            cancellationToken);
                        if (mutation is null)
                        {
                            return Results.Problem(
                                detail: "Patient attachment could not be created from this template.",
                                statusCode: StatusCodes.Status400BadRequest);
                        }

                        await repository.RecordAttachmentGeneratedAsync(
                            id,
                            versionId,
                            mutation.Id,
                            request.PatientId,
                            session.Username,
                            cancellationToken);
                        return Results.Created($"/api/documents/{mutation.Id}", mutation);
                    }

                    var rendered = await repository.RenderAsync(
                        id,
                        new DocumentTemplateRenderRequest(request.PatientId),
                        cancellationToken);
                    if (rendered is null)
                    {
                        return Results.NotFound();
                    }

                    var textMutation = await documents.CreateAsync(
                        new PatientDocumentCreateRequest(
                            request.PatientId,
                            request.CategoryId,
                            $"Template: {rendered.Template.Name}",
                            documentDate,
                            request.Encounter,
                            rendered.Content,
                            $"Generated from document template {id}."),
                        cancellationToken);
                    if (textMutation is null)
                    {
                        return Results.Problem(
                            detail: "Patient attachment could not be created from this template.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    await repository.RecordAttachmentGeneratedAsync(
                        id,
                        null,
                        textMutation.Id,
                        request.PatientId,
                        session.Username,
                        cancellationToken);
                    return Results.Created($"/api/documents/{textMutation.Id}", textMutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("GenerateDocumentTemplateAttachment")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        documentTemplates
            .MapDelete("/{id:guid}/test-fixture", async (
                DocumentTemplateRepository repository,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return await repository.DeleteTestFixtureAsync(id, cancellationToken)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Problem(
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("DeleteDocumentTemplateTestFixture")
            .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

        return documentTemplates;
    }
}
