// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps internal clinical office-note routes as one permissioned aggregate.
/// </summary>
public static class OfficeNoteEndpoints
{
    public static RouteGroupBuilder MapOfficeNoteEndpoints(this WebApplication app)
    {
        var officeNotes = app.MapGroup("/api/office-notes").WithTags("Office Notes");
        RequireAccessPermission(officeNotes, "encounters", "notes", "view");

        officeNotes.MapGet("/", async (OfficeNoteRepository repository, string? activity, int? offset, int? limit, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAsync(activity ?? "active", offset ?? 0, limit ?? 8, cancellationToken)))
            .WithName("GetOfficeNotes");

        officeNotes.MapPost("/", async (OfficeNoteRepository repository, OfficeNoteCreateRequest request, HttpContext context, CancellationToken cancellationToken) =>
            {
                var author = context.User.Identity?.Name ?? "system";
                var note = await repository.CreateAsync(request.Body, author, cancellationToken);
                return note is null ? Results.BadRequest("Office note body is required and must be 4000 characters or fewer.") : Results.Created($"/api/office-notes/{note.Id}", note);
            })
            .WithName("CreateOfficeNote")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "addonly"));

        officeNotes.MapPut("/{noteId:guid}", async (OfficeNoteRepository repository, Guid noteId, OfficeNoteUpdateRequest request, CancellationToken cancellationToken) =>
            {
                var note = await repository.UpdateAsync(noteId, request.Body, cancellationToken);
                return note is null ? Results.NotFound() : Results.Ok(note);
            })
            .WithName("UpdateOfficeNote")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

        officeNotes.MapPut("/{noteId:guid}/activity", async (OfficeNoteRepository repository, Guid noteId, OfficeNoteActivityRequest request, CancellationToken cancellationToken) =>
            {
                var note = await repository.SetActivityAsync(noteId, request.Active, cancellationToken);
                return note is null ? Results.NotFound() : Results.Ok(note);
            })
            .WithName("SetOfficeNoteActivity")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

        officeNotes.MapDelete("/{noteId:guid}", async (OfficeNoteRepository repository, Guid noteId, CancellationToken cancellationToken) =>
                await repository.DeleteAsync(noteId, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DeleteOfficeNote")
            .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

        return officeNotes;
    }
}
