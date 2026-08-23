// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using AvenChart.Api.Data;
using AvenChart.Api.Models;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps the external-laboratory FHIR R4 intake boundary. This deliberately
/// remains separate from staff integration routes: a laboratory authenticates
/// as a governed service source, never with a reusable staff session.
/// </summary>
public static class ExternalLaboratoryFhirIntakeEndpoints
{
    public static RouteGroupBuilder MapExternalLaboratoryFhirIntakeEndpoints(this WebApplication app)
    {
        var externalLaboratory = app.MapGroup("/api/external-laboratory-results")
            .WithTags("External Laboratory FHIR R4");

        externalLaboratory.MapPost("/fhir-r4", async (
                HttpContext httpContext,
                JsonElement payload,
                ExternalLaboratorySourceRepository sourceRepository,
                ExternalLaboratoryIntakeRepository intakeRepository,
                CancellationToken cancellationToken) =>
            {
                if (!IsFhirContentType(httpContext.Request.ContentType))
                {
                    return FhirResults.Error(StatusCodes.Status415UnsupportedMediaType, "not-supported",
                        "The external laboratory endpoint accepts application/fhir+json or application/json only.");
                }

                var source = await sourceRepository.AuthenticateAsync(
                    httpContext.Request.Headers["X-AvenChart-Lab-Source"].ToString(),
                    httpContext.Request.Headers["X-AvenChart-Lab-Api-Key"].ToString(),
                    cancellationToken);
                if (source is null)
                {
                    return FhirResults.Error(StatusCodes.Status401Unauthorized, "security",
                        "A valid active external laboratory source credential is required.");
                }

                try
                {
                    var receipt = await intakeRepository.ReceiveAsync(
                        source,
                        httpContext.Request.Headers["X-AvenChart-Lab-Message-Id"].ToString(),
                        payload,
                        cancellationToken);
                    if (receipt.Conflict)
                    {
                        return FhirResults.Error(StatusCodes.Status409Conflict, "conflict", receipt.Reason ?? "The source message conflicts with existing provenance.");
                    }
                    if (receipt.Rejected)
                    {
                        return FhirResults.Error(StatusCodes.Status422UnprocessableEntity, "processing", receipt.Reason ?? "The laboratory message could not be reconciled.");
                    }
                    return Results.Json(
                        receipt,
                        statusCode: receipt.Duplicate ? StatusCodes.Status200OK : StatusCodes.Status201Created,
                        contentType: "application/json");
                }
                catch (ExternalLaboratoryFhirValidationException exception)
                {
                    return FhirResults.Error(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
                }
                catch (ArgumentException exception)
                {
                    return FhirResults.Error(StatusCodes.Status400BadRequest, "invalid", exception.Message);
                }
            })
            .WithName("ReceiveExternalLaboratoryFhirR4Result")
            .WithSummary("Receives a validated FHIR R4 external laboratory result bundle")
            .WithDescription("Requires an authenticated laboratory source and validates FHIR R4 Bundle, DiagnosticReport, and Observation core profiles before applying AvenChart's facility-scoped clinical intake contract.");

        return externalLaboratory;
    }

    private static bool IsFhirContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var mediaType = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
        return string.Equals(mediaType, "application/fhir+json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}
