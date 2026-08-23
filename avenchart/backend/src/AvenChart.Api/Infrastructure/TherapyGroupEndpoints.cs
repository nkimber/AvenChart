// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps therapy-group membership, session, attendance, and encounter routes as one aggregate.
/// </summary>
public static class TherapyGroupEndpoints
{
    public static RouteGroupBuilder MapTherapyGroupEndpoints(this WebApplication app)
    {
        var therapyGroups = app.MapGroup("/api/therapy-groups").WithTags("Therapy Groups");
        RequireAccessPermission(therapyGroups, "groups", "gadd", "view");

        therapyGroups.MapGet("/", async (TherapyGroupRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetTherapyGroups");
        therapyGroups.MapPost("/", async (TherapyGroupRepository repository, TherapyGroupCreateRequest request, CancellationToken cancellationToken) =>
        {
            try { return Results.Created("/api/therapy-groups", await repository.CreateAsync(request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateTherapyGroup").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
        therapyGroups.MapGet("/{groupId:guid}/members", async (Guid groupId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetMembersAsync(groupId, cancellationToken))).WithName("GetTherapyGroupMembers");
        therapyGroups.MapPost("/{groupId:guid}/members", async (Guid groupId, TherapyGroupMemberRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Created($"/api/therapy-groups/{groupId}/members", await repository.AddMemberAsync(groupId, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("AddTherapyGroupMember").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
        therapyGroups.MapGet("/{groupId:guid}/sessions", async (Guid groupId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetSessionsAsync(groupId, cancellationToken))).WithName("GetTherapyGroupSessions");
        therapyGroups.MapPost("/{groupId:guid}/sessions", async (Guid groupId, TherapyGroupSessionCreateRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Created($"/api/therapy-groups/{groupId}/sessions", await repository.CreateSessionAsync(groupId, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateTherapyGroupSession").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
        therapyGroups.MapGet("/{groupId:guid}/sessions/{sessionId:guid}/attendance", async (Guid groupId, Guid sessionId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.GetSessionAttendanceAsync(groupId, sessionId, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("GetTherapyGroupSessionAttendance");
        therapyGroups.MapPut("/{groupId:guid}/sessions/{sessionId:guid}/attendance/{patientId}", async (Guid groupId, Guid sessionId, string patientId, TherapyGroupSessionAttendanceRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.RecordSessionAttendanceAsync(groupId, sessionId, patientId, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("RecordTherapyGroupSessionAttendance").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
        therapyGroups.MapPut("/{groupId:guid}/sessions/{sessionId:guid}/status", async (Guid groupId, Guid sessionId, TherapyGroupSessionStatusRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.UpdateSessionStatusAsync(groupId, sessionId, request, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("UpdateTherapyGroupSessionStatus").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
        therapyGroups.MapGet("/{groupId:guid}/sessions/{sessionId:guid}/encounters", async (Guid groupId, Guid sessionId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetSessionEncountersAsync(groupId, sessionId, cancellationToken))).WithName("GetTherapyGroupSessionEncounters");
        therapyGroups.MapPost("/{groupId:guid}/sessions/{sessionId:guid}/encounters", async (Guid groupId, Guid sessionId, TherapyGroupSessionEncounterRequest request, TherapyGroupRepository repository, EncounterRepository encounterRepository, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await repository.CreateSessionEncountersAsync(groupId, sessionId, request, encounterRepository, cancellationToken)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).WithName("CreateTherapyGroupSessionEncounters").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write")).AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        return therapyGroups;
    }
}
