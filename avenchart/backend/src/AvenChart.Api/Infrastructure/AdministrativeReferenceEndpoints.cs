// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps administrative reference-data routes used by clinical and operational workflows.
/// </summary>
public static class AdministrativeReferenceEndpoints
{
    public static void MapAdministrativeReferenceEndpoints(this WebApplication app)
    {
        var addressBook = app.MapGroup("/api/administration/address-book").WithTags("Address Book");
        RequireAccessPermission(addressBook, "admin", "practice", "view");
        addressBook.MapGet("/", async (AddressBookRepository repository, string? organization, string? firstName, string? lastName, string? specialty, string? npi, string? type, bool? externalOnly, CancellationToken cancellationToken) => Results.Ok(await repository.SearchAsync(organization, firstName, lastName, specialty, npi, type, externalOnly ?? false, cancellationToken))).WithName("SearchAddressBook");
        addressBook.MapPost("/", async (AddressBookRepository repository, AddressBookContactRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(null, request, cancellationToken); return Results.Created($"/api/administration/address-book/{item!.Id}", item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contact"] = [exception.Message] }); } }).WithName("CreateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
        addressBook.MapPut("/{contactId:int}", async (AddressBookRepository repository, int contactId, AddressBookContactRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(contactId, request, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contact"] = [exception.Message] }); } }).WithName("UpdateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
        addressBook.MapDelete("/{contactId:int}", async (AddressBookRepository repository, int contactId, CancellationToken cancellationToken) => await repository.DeleteAsync(contactId, cancellationToken) ? Results.NoContent() : Results.NotFound()).WithName("DeleteAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));

        var tracks = app.MapGroup("/api/administration/tracks").WithTags("Track Anything");
        RequireAccessPermission(tracks, "admin", "practice", "view");
        tracks.MapGet("/", async (TrackAnythingRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetTrackAnythingTypes");
        tracks.MapPost("/", async (TrackAnythingRepository repository, TrackAnythingRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(null, request, cancellationToken); return Results.Created($"/api/administration/tracks/{item!.Id}", item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["track"] = [exception.Message] }); } }).WithName("CreateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
        tracks.MapPut("/{trackId:int}", async (TrackAnythingRepository repository, int trackId, TrackAnythingRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(trackId, request, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["track"] = [exception.Message] }); } }).WithName("UpdateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
        tracks.MapDelete("/{trackId:int}", async (TrackAnythingRepository repository, int trackId, CancellationToken cancellationToken) => await repository.DeleteAsync(trackId, cancellationToken) ? Results.NoContent() : Results.NotFound()).WithName("DeleteTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));
    }
}
