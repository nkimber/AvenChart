// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using AvenChart.Api.Models;
using Microsoft.AspNetCore.Http;

namespace AvenChart.Api.Infrastructure;

public static class FhirResults
{
    public const string ContentType = "application/fhir+json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IResult Ok<T>(T resource) =>
        Results.Json(resource, SerializerOptions, contentType: ContentType);

    public static IResult NotFound(string resourceType, string id) =>
        Results.Json(
            new FhirOperationOutcome(
                "OperationOutcome",
                [new FhirOperationOutcomeIssue("error", "not-found", $"{resourceType}/{id} was not found.")]),
            SerializerOptions,
            statusCode: StatusCodes.Status404NotFound,
            contentType: ContentType);

    public static IResult NotAcceptable() =>
        Results.Json(
            new FhirOperationOutcome(
                "OperationOutcome",
                [new FhirOperationOutcomeIssue(
                    "error",
                    "not-supported",
                    "AvenChart FHIR R4 supports application/fhir+json and application/json response representations.")]),
            SerializerOptions,
            statusCode: StatusCodes.Status406NotAcceptable,
            contentType: ContentType);

    public static IResult Error(int statusCode, string code, string diagnostics) =>
        Results.Json(
            new FhirOperationOutcome(
                "OperationOutcome",
                [new FhirOperationOutcomeIssue("error", code, diagnostics)]),
            SerializerOptions,
            statusCode: statusCode,
            contentType: ContentType);
}
