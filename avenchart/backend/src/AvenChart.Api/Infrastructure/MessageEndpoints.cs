// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected staff messaging, including versioned content, assignment,
/// attachment, escalation, retention, and correction operations.
/// </summary>
public static class MessageEndpoints
{
    public static RouteGroupBuilder MapMessageEndpoints(this WebApplication app)
    {
        var messages = app.MapGroup("/api/messages").WithTags("Messages");
        RequireAccessPermission(messages, "patients", "notes", "view");
        messages.AddEndpointFilter(MessageFacilityScopeFilter());

        messages.MapGet("/inbox", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string? status,
                string? assignment,
                string? patient,
                string? subject,
                string? priority,
                string? owner,
                int? minimumAgeDays,
                int? maximumAgeDays,
                int? offset,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var query = new StaffMessageInboxQuery(
                    Status: status,
                    Assignment: assignment,
                    Patient: patient,
                    Subject: subject,
                    Priority: priority,
                    Owner: owner,
                    MinimumAgeDays: minimumAgeDays,
                    MaximumAgeDays: maximumAgeDays,
                    Offset: offset ?? 0,
                    Limit: limit ?? 25);
                return Results.Ok(await repository.GetInboxAsync(
                    session.Username,
                    query,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken));
            })
            .WithName("GetStaffMessageInbox");

        messages.MapGet("/assignees", async (
                AuthorizationRepository repository,
                CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAssigneesAsync(cancellationToken)))
            .WithName("GetPatientMessageAssignees");

        messages.MapGet("/{patientId}", async (
                MessageRepository repository,
                string patientId,
                bool? includeArchived,
                CancellationToken cancellationToken) =>
            {
                var patientMessages = await repository.GetForPatientAsync(
                    patientId,
                    cancellationToken,
                    includeArchived == true);
                return patientMessages is null ? Results.NotFound() : Results.Ok(patientMessages);
            })
            .WithName("GetPatientMessages");

        messages.MapGet("/{messageId}/version", async (
                MessageRepository repository,
                string messageId,
                CancellationToken cancellationToken) =>
            {
                var version = await repository.GetCurrentMessageVersionAsync(messageId, cancellationToken);
                return version is null ? Results.NotFound() : Results.Ok(new { messageId, version });
            })
            .WithName("GetPatientMessageVersion")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapPost("/", async (
                MessageRepository repository,
                PatientMessageCreateRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var mutation = await repository.CreateAsync(
                    request,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return mutation is null
                    ? Results.BadRequest("Patient message could not be created from the supplied patient, title, and body.")
                    : Results.Created($"/api/messages/{mutation.Id}", mutation);
            })
            .WithName("CreatePatientMessage")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "addonly"));

        messages.MapPut("/{messageId}/status", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string messageId,
                PatientMessageStatusUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateStatusAsync(messageId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (PatientMessageVersionConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = [exception.Message] });
                }
            })
            .WithName("UpdatePatientMessageStatus")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPut("/{messageId}/content", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string messageId,
                PatientMessageContentUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateContentAsync(messageId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (PatientMessageVersionConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = [exception.Message] });
                }
            })
            .WithName("UpdatePatientMessageContent")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPut("/{messageId}/assignment", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string messageId,
                PatientMessageAssignmentUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.UpdateAssignmentAsync(messageId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (PatientMessageAssignmentVersionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        expectedVersion = exception.ExpectedVersion,
                        currentVersion = exception.CurrentVersion,
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdatePatientMessageAssignment")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapGet("/{messageId}/assignment-history", async (
                MessageRepository repository,
                string messageId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetAssignmentHistoryAsync(messageId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientMessageAssignmentHistory")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapPost("/{messageId}/forward", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string messageId,
                PatientMessageForwardRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.ForwardAsync(messageId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (PatientMessageAssignmentVersionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        expectedVersion = exception.ExpectedVersion,
                        currentVersion = exception.CurrentVersion,
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("ForwardPatientMessage")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapGet("/{messageId}/attachments", async (MessageRepository repository, string messageId, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAttachmentsAsync(messageId, cancellationToken)))
            .WithName("GetStaffMessageAttachments")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapPost("/{messageId}/attachments", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, StaffMessageAttachmentSubmission request, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var attachment = await repository.AddAttachmentAsync(messageId, request, session.Username, cancellationToken);
                return attachment is null ? Results.NotFound() : Results.Created($"/api/messages/{messageId}/attachments/{attachment.Id}", attachment);
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("AddStaffMessageAttachment").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapGet("/{messageId}/attachments/{attachmentId:guid}", async (MessageRepository repository, string messageId, Guid attachmentId, CancellationToken cancellationToken) =>
        {
            var attachment = await repository.DownloadAttachmentAsync(messageId, attachmentId, cancellationToken);
            return attachment.Downloadable ? Results.File(attachment.Content, attachment.ContentType, attachment.FileName) : Results.NotFound(new { error = attachment.FailureReason });
        }).WithName("DownloadStaffMessageAttachment").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapGet("/{messageId}/correction-history", async (MessageRepository repository, string messageId, CancellationToken cancellationToken) =>
        {
            var history = await repository.GetCorrectionHistoryAsync(messageId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).WithName("GetStaffMessageCorrectionHistory").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapGet("/{messageId}/content-history", async (MessageRepository repository, string messageId, CancellationToken cancellationToken) =>
        {
            var history = await repository.GetContentHistoryAsync(messageId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).WithName("GetStaffMessageContentHistory").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapPost("/{messageId}/correct", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, PatientMessageCorrectionRequest request, CancellationToken cancellationToken) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Correction) || string.IsNullOrWhiteSpace(request.Reason))
                {
                    return Results.BadRequest(new { error = "A correction and its reason are required." });
                }
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var correction = await repository.CorrectAsync(messageId, request, session.Username, cancellationToken);
                return correction is null ? Results.NotFound() : Results.Ok(correction);
            }
            catch (PatientMessageVersionConflictException exception) { return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion }); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CorrectStaffMessage").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapGet("/{messageId}/retention-history", async (MessageRepository repository, string messageId, CancellationToken cancellationToken) =>
        {
            var history = await repository.GetRetentionHistoryAsync(messageId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).WithName("GetStaffMessageRetentionHistory").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapGet("/{messageId}/escalation-history", async (MessageRepository repository, string messageId, CancellationToken cancellationToken) =>
        {
            var history = await repository.GetEscalationHistoryAsync(messageId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }).WithName("GetStaffMessageEscalationHistory").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "view"));

        messages.MapPost("/{messageId}/escalate", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, PatientMessageEscalationRequest request, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = await repository.SetEscalationAsync(messageId, true, request, session.Username, cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("EscalateStaffMessage").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPost("/{messageId}/resolve-escalation", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, PatientMessageEscalationRequest request, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = await repository.SetEscalationAsync(messageId, false, request, session.Username, cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("ResolveStaffMessageEscalation").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPost("/{messageId}/archive", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, PatientMessageArchiveRequest request, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = await repository.SetArchiveAsync(messageId, true, request, session.Username, cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("ArchiveStaffMessage").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPost("/{messageId}/restore", async (MessageRepository repository, AuthRepository authRepository, HttpContext httpContext, string messageId, PatientMessageArchiveRequest request, CancellationToken cancellationToken) =>
        {
            try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = await repository.SetArchiveAsync(messageId, false, request, session.Username, cancellationToken); return result is null ? Results.NotFound() : Results.Ok(result); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("RestoreStaffMessage").AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

        messages.MapPut("/{messageId}/reply", async (
                MessageRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string messageId,
                PatientMessageReplyRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var mutation = await repository.ReplyAsync(messageId, request, session.Username, cancellationToken);
                    return mutation is null ? Results.NotFound() : Results.Ok(mutation);
                }
                catch (PatientMessageVersionConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = [exception.Message] });
                }
            })
            .WithName("ReplyToPatientMessage")
            .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));


        return messages;
    }
}
