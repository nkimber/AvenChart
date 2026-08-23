// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps AvenChart's read-only FHIR R4 staff API. The group owns its FHIR media
/// contract while <see cref="EndpointAccessPolicies"/> supplies the common staff
/// authorization and selected facility/purpose-of-use boundary.
/// </summary>
public static class FhirR4Endpoints
{
    public static RouteGroupBuilder MapFhirR4Endpoints(this WebApplication app)
    {
        var fhir = app.MapGroup("/api/fhir/R4").WithTags("FHIR R4");
        EndpointAccessPolicies.RequireAccessPermission(fhir, "patients", "demo", "view");
        fhir.AddEndpointFilter(FhirContentNegotiationFilter());

        fhir.MapGet("/metadata", (HttpContext httpContext) =>
            {
                var baseUrl = BuildFhirBaseUrl(httpContext);
                var patientCapability = new FhirResourceCapability(
                    "Patient",
                    [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
                    [new FhirSearchParameter("name", "string"), new FhirSearchParameter("identifier", "token")]);
                var encounterCapability = new FhirResourceCapability(
                    "Encounter",
                    [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
                    [new FhirSearchParameter("subject", "reference")]);
                var observationCapability = new FhirResourceCapability(
                    "Observation",
                    [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
                    [new FhirSearchParameter("subject", "reference")]);
                var server = new FhirCapabilityResource("server", null, [patientCapability, encounterCapability, observationCapability]);
                return FhirResults.Ok(new FhirCapabilityStatement(
                    "CapabilityStatement",
                    $"{baseUrl}/metadata",
                    "1.0.0",
                    "AvenChartFhirR4",
                    "active",
                    false,
                    DateTimeOffset.UtcNow.ToString("O"),
                    "AvenChart contributors",
                    "instance",
                    "4.0.1",
                    new FhirSoftware("AvenChart", "Phase 3"),
                    new FhirImplementation("AvenChart FHIR R4 read-only API", baseUrl),
                    ["json"],
                    [server]));
            })
            .WithName("GetFhirCapabilityStatement");

        fhir.MapGet("/Patient/{id}", async (FhirRepository repository, HttpContext httpContext, string id, CancellationToken cancellationToken) =>
            {
                PhiAuditResourceContext.Set(httpContext, "Patient", id);
                var patient = await repository.GetPatientAsync(
                    id,
                    EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return patient is null ? FhirResults.NotFound("Patient", id) : FhirResults.Ok(patient);
            })
            .WithName("GetFhirPatient");

        fhir.MapGet("/Patient", async (FhirRepository repository, HttpContext httpContext, string? name, string? identifier, int? _count, int? page, CancellationToken cancellationToken) =>
            FhirResults.Ok(await repository.SearchPatientsAsync(
                name,
                identifier,
                _count,
                page,
                BuildFhirBaseUrl(httpContext),
                EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)))
            .WithName("SearchFhirPatients");

        fhir.MapGet("/Encounter/{id:int}", async (FhirRepository repository, HttpContext httpContext, int id, CancellationToken cancellationToken) =>
            {
                PhiAuditResourceContext.Set(httpContext, "Encounter", id.ToString(CultureInfo.InvariantCulture));
                var encounter = await repository.GetEncounterAsync(
                    id,
                    EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return encounter is null ? FhirResults.NotFound("Encounter", id.ToString(CultureInfo.InvariantCulture)) : FhirResults.Ok(encounter);
            })
            .WithName("GetFhirEncounter");

        fhir.MapGet("/Encounter", async (FhirRepository repository, HttpContext httpContext, string? subject, int? _count, int? page, CancellationToken cancellationToken) =>
            FhirResults.Ok(await repository.SearchEncountersAsync(
                subject,
                _count,
                page,
                BuildFhirBaseUrl(httpContext),
                EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)))
            .WithName("SearchFhirEncounters");

        fhir.MapGet("/Observation/{id:int}", async (FhirRepository repository, HttpContext httpContext, int id, CancellationToken cancellationToken) =>
            {
                PhiAuditResourceContext.Set(httpContext, "Observation", id.ToString(CultureInfo.InvariantCulture));
                var observation = await repository.GetObservationAsync(
                    id,
                    EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return observation is null ? FhirResults.NotFound("Observation", id.ToString(CultureInfo.InvariantCulture)) : FhirResults.Ok(observation);
            })
            .WithName("GetFhirObservation");

        fhir.MapGet("/Observation", async (FhirRepository repository, HttpContext httpContext, string? subject, int? _count, int? page, CancellationToken cancellationToken) =>
            FhirResults.Ok(await repository.SearchObservationsAsync(
                subject,
                _count,
                page,
                BuildFhirBaseUrl(httpContext),
                EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)))
            .WithName("SearchFhirObservations");

        fhir.MapGet("/Observation/sdoh", async (FhirRepository repository, HttpContext httpContext, string? subject, int? _count, int? page, CancellationToken cancellationToken) =>
            FhirResults.Ok(await repository.SearchSdohObservationsAsync(
                subject,
                _count,
                page,
                BuildFhirBaseUrl(httpContext),
                EndpointAccessPolicies.RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken)))
            .WithName("SearchFhirSdohObservations");

        return fhir;
    }

    private static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> FhirContentNegotiationFilter()
    {
        return async (context, next) =>
        {
            var accept = context.HttpContext.Request.GetTypedHeaders().Accept;
            if (accept is { Count: > 0 }
                && !accept.Any(header =>
                    (header.Quality is null || header.Quality > 0)
                    && (string.Equals(header.MediaType.Value, "application/fhir+json", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(header.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(header.MediaType.Value, "*/*", StringComparison.OrdinalIgnoreCase))))
            {
                return FhirResults.NotAcceptable();
            }

            return await next(context);
        };
    }

    private static string BuildFhirBaseUrl(HttpContext httpContext) =>
        $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/api/fhir/R4";
}
