// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected patient-document retrieval, version, archive, and signature
/// workflows with their selected-facility and retained-evidence boundary.
/// </summary>
public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this WebApplication app)
    {
        var documents = app.MapGroup("/api/documents").WithTags("Documents");
        RequireAccessPermission(documents, "patients", "docs", "view");
        documents.AddEndpointFilter(ClinicalResourceFacilityScopeFilter());

        documents.MapGet("/ocr-queue", async (
                DocumentRepository repository,
                string? patientId,
                string? status,
                string? priority,
                string? query,
                int? offset,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var queue = await repository.GetOcrQueueAsync(
                        cancellationToken,
                        patientId,
                        status,
                        priority,
                        query,
                        offset ?? 0,
                        limit ?? 1_000);
                    return Results.Ok(queue);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientDocumentOcrQueue");

        documents.MapGet("/{documentId:int}/ocr-history", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var history = await repository.GetOcrHistoryAsync(documentId, cancellationToken);
                    return history is null ? Results.NotFound() : Results.Ok(history);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientDocumentOcrHistory");

        documents.MapGet("/routing-queue", async (
                DocumentRepository repository,
                CancellationToken cancellationToken,
                string? patientId = null,
                string? status = null,
                string? priority = null,
                string? assignedTo = null,
                int? minimumAgeHours = null,
                string? query = null,
                int offset = 0,
                int limit = 50) =>
            {
                try
                {
                    var queue = await repository.GetRoutingQueueAsync(
                        cancellationToken,
                        patientId,
                        status,
                        priority,
                        assignedTo,
                        minimumAgeHours,
                        query,
                        offset,
                        limit);
                    return Results.Ok(queue);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientDocumentRoutingQueue");

        documents.MapGet("/routing-assignees", async (
                DocumentRepository repository,
                CancellationToken cancellationToken) =>
            {
                return Results.Ok(await repository.GetRoutingAssigneesAsync(cancellationToken));
            })
            .WithName("GetPatientDocumentRoutingAssignees");

        documents.MapGet("/{documentId:int}/routing-history", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetRoutingHistoryAsync(documentId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDocumentRoutingHistory");

        documents.MapPut("/{documentId:int}/routing", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentRoutingMutationRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.RouteDocumentAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentRoutingConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("RoutePatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPost("/{documentId:int}/routing/complete", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentRoutingCompleteRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.CompleteRoutingAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentRoutingConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("CompletePatientDocumentRouting")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapGet("/retention-policy", async (
                DocumentRepository repository,
                CancellationToken cancellationToken,
                string? patientId = null) =>
            {
                var policy = await repository.GetRetentionPolicyAsync(cancellationToken, patientId);
                return Results.Ok(policy);
            })
            .WithName("GetPatientDocumentRetentionPolicy");

        documents.MapPost("/{documentId:int}/ocr/complete", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentOcrCompleteRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var completion = await repository.CompleteOcrAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return completion is null ? Results.NotFound() : Results.Ok(completion);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentOcrConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("CompletePatientDocumentOcr")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPost("/{documentId:int}/ocr/start", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentOcrStartRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.StartOcrAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentOcrConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("StartPatientDocumentOcr")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPost("/{documentId:int}/ocr/fail", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentOcrFailRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.FailOcrAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentOcrConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("FailPatientDocumentOcr")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPost("/{documentId:int}/ocr/correct", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentOcrCorrectRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var result = await repository.CorrectOcrTextAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentOcrConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentTaskVersion = conflict.CurrentTaskVersion,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("CorrectPatientDocumentOcrText")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPost("/{documentId:int}/retention/dispose", async (
                DocumentRepository repository,
                int documentId,
                PatientDocumentRetentionDispositionRequest request,
                CancellationToken cancellationToken) =>
            {
                var disposition = await repository.DisposeRetentionAsync(documentId, request, cancellationToken);
                return disposition is null
                    ? Results.BadRequest("Patient document retention disposition could not be completed from the supplied document and policy evidence.")
                    : Results.Ok(disposition);
            })
            .WithName("DisposePatientDocumentRetention")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapGet("/{documentId:int}/content", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var document = await repository.GetContentAsync(documentId, cancellationToken);
                return document is null ? Results.NotFound() : Results.Ok(document);
            })
            .WithName("GetPatientDocumentContent");

        documents.MapGet("/{documentId:int}/download", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var document = await repository.GetContentAsync(documentId, cancellationToken);
                if (document is null)
                {
                    return Results.NotFound();
                }

                var fileBytes = document.IsBinary && !string.IsNullOrWhiteSpace(document.ContentBase64)
                    ? Convert.FromBase64String(document.ContentBase64)
                    : Encoding.UTF8.GetBytes(document.Content);

                return Results.File(
                    fileBytes,
                    document.Mimetype ?? "application/octet-stream",
                    document.FileName);
            })
            .WithName("DownloadPatientDocument");

        documents.MapGet("/{documentId:int}/versions", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetVersionHistoryAsync(documentId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDocumentVersionHistory");

        documents.MapGet("/{documentId:int}/review-history", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetReviewHistoryAsync(documentId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDocumentReviewHistory");

        documents.MapGet("/{documentId:int}/archive-history", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetArchiveHistoryAsync(documentId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDocumentArchiveHistory");

        documents.MapGet("/{documentId:int}/versions/{version:int}/content", async (
                DocumentRepository repository,
                int documentId,
                int version,
                CancellationToken cancellationToken) =>
            {
                var content = await repository.GetVersionContentAsync(documentId, version, cancellationToken);
                return content is null ? Results.NotFound() : Results.Ok(content);
            })
            .WithName("GetPatientDocumentVersionContent");

        documents.MapGet("/{documentId:int}/versions/{version:int}/download", async (
                DocumentRepository repository,
                int documentId,
                int version,
                CancellationToken cancellationToken) =>
            {
                var content = await repository.GetVersionContentAsync(documentId, version, cancellationToken);
                if (content is null)
                {
                    return Results.NotFound();
                }

                var fileBytes = content.IsBinary && !string.IsNullOrWhiteSpace(content.ContentBase64)
                    ? Convert.FromBase64String(content.ContentBase64)
                    : Encoding.UTF8.GetBytes(content.Content);

                return Results.File(
                    fileBytes,
                    content.Mimetype ?? "application/octet-stream",
                    content.FileName);
            })
            .WithName("DownloadPatientDocumentVersion");

        documents.MapGet("/category-options", async (
                DocumentRepository repository,
                CancellationToken cancellationToken) =>
            {
                var options = await repository.GetCategoryOptionsAsync(cancellationToken);
                return Results.Ok(options);
            })
            .WithName("GetPatientDocumentCategoryOptions");

        documents.MapGet("/{patientId}", async (
                DocumentRepository repository,
                string patientId,
                CancellationToken cancellationToken,
                bool includeArchived = false) =>
            {
                var patientDocuments = await repository.GetForPatientAsync(patientId, cancellationToken, includeArchived);
                return patientDocuments is null ? Results.NotFound() : Results.Ok(patientDocuments);
            })
            .WithName("GetPatientDocuments");

        documents.MapPost("/", async (
                DocumentRepository repository,
                PatientDocumentCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Patient document could not be created from the supplied patient and document details.")
                    : Results.Created($"/api/documents/{mutation.Id}", mutation);
            })
            .WithName("CreatePatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        documents.MapPost("/binary", async (
                DocumentRepository repository,
                PatientDocumentBinaryCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateBinaryAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Binary patient document could not be created from the supplied patient, file, and document details.")
                    : Results.Created($"/api/documents/{mutation.Id}", mutation);
            })
            .WithName("CreateBinaryPatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        documents.MapPost("/scanner-captures", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                PatientDocumentScannerCaptureRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(
                    authRepository,
                    httpContext,
                    cancellationToken);
                var mutation = await repository.CreateScannerCaptureAsync(
                    request,
                    session.Username,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Scanner-captured patient document could not be created from the supplied patient, scanner, and document details.")
                    : Results.Created($"/api/documents/{mutation.Id}", mutation);
            })
            .WithName("CreateScannerCapturePatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        documents.MapPost("/external-link", async (
                DocumentRepository repository,
                PatientDocumentExternalLinkCreateRequest request,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateExternalLinkAsync(request, cancellationToken);
                return mutation is null
                    ? Results.BadRequest("External-link patient document could not be created from the supplied patient, URL, and document details.")
                    : Results.Created($"/api/documents/{mutation.Id}", mutation);
            })
            .WithName("CreateExternalLinkPatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

        documents.MapGet("/{documentId:int}/metadata-history", async (
                DocumentRepository repository,
                int documentId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetMetadataHistoryAsync(documentId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDocumentMetadataHistory");

        documents.MapPut("/{documentId:int}/metadata", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentMetadataUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var mutation = await repository.UpdateMetadataAsync(
                    documentId,
                    request,
                    session.Username,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Patient document metadata could not be updated from the supplied filing details.")
                    : Results.Ok(mutation);
            })
            .WithName("UpdatePatientDocumentMetadata")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPut("/{documentId:int}/content", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentContentReplaceRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.ReplaceContentAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Patient document content could not be replaced from the supplied text payload or did not materially change.")
                        : Results.Ok(mutation);
                }
                catch (DocumentVersionConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                        currentVersion = conflict.CurrentVersion
                    });
                }
            })
            .WithName("ReplacePatientDocumentContent")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPut("/{documentId:int}/content/binary", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentBinaryContentReplaceRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.ReplaceBinaryContentAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return mutation is null
                        ? Results.BadRequest("Binary patient document content could not be replaced from the supplied file payload or did not materially change.")
                        : Results.Ok(mutation);
                }
                catch (DocumentVersionConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                        currentVersion = conflict.CurrentVersion
                    });
                }
            })
            .WithName("ReplaceBinaryPatientDocumentContent")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPut("/{documentId:int}/soft-delete", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentArchiveRequest? request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.SoftDeleteAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentArchiveConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentArchived = conflict.CurrentArchived
                    });
                }
            })
            .WithName("SoftDeletePatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPut("/{documentId:int}/restore", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentArchiveRequest? request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.RestoreAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentArchiveConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentArchived = conflict.CurrentArchived
                    });
                }
            })
            .WithName("RestorePatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapPut("/{documentId:int}/sign", async (
                DocumentRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                int documentId,
                PatientDocumentSignRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.SignAsync(
                        documentId,
                        request,
                        session.Username,
                        cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (DocumentReviewConflictException conflict)
                {
                    return Results.Conflict(new
                    {
                        error = conflict.Message,
                        currentStatus = conflict.CurrentStatus
                    });
                }
            })
            .WithName("SignPatientDocument")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

        documents.MapDelete("/{documentId:int}", (int documentId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Document deletion is not available",
                    detail: "Patient documents are retained. Use the reasoned archive workflow instead."))
            .WithName("RetirePatientDocumentDeletion")
            .AddEndpointFilter(AccessPermissionFilter("patients", "docs_rm", "write"));

        app.MapProcedureEndpoints();
        app.MapIntegrationEndpoints();
        app.MapInventoryEndpoints();

        return documents;
    }
}
