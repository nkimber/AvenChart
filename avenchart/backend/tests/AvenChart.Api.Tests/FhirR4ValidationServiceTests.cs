// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvenChart.Api.Tests;

public sealed class FhirR4ValidationServiceTests
{
    [Fact]
    public void ValidCoreLaboratoryBundleIsAccepted()
    {
        using var payload = JsonDocument.Parse(ValidLaboratoryBundleJson);

        var service = new FhirR4ValidationService();

        service.ValidateExternalLaboratoryBundle(payload.RootElement);
    }

    [Fact]
    public void ObservationThatViolatesCoreInvariantIsRejected()
    {
        using var payload = JsonDocument.Parse(ValidLaboratoryBundleJson.Replace(
            "\"valueQuantity\": { \"value\": 16.4, \"unit\": \"g/dL\" }",
            "\"valueQuantity\": { \"value\": 16.4, \"unit\": \"g/dL\" }, \"dataAbsentReason\": { \"text\": \"Not applicable\" }",
            StringComparison.Ordinal));

        var service = new FhirR4ValidationService();

        Assert.Throws<FhirR4ValidationException>(() => service.ValidateExternalLaboratoryBundle(payload.RootElement));
    }

    [Fact]
    public void NonFhirJsonMemberIsRejectedBeforeClinicalParsing()
    {
        using var payload = JsonDocument.Parse(ValidLaboratoryBundleJson.Replace(
            "\"type\": \"collection\",",
            "\"type\": \"collection\", \"notAValidFhirMember\": true,",
            StringComparison.Ordinal));

        var service = new FhirR4ValidationService();

        Assert.Throws<FhirR4ValidationException>(() => service.ValidateExternalLaboratoryBundle(payload.RootElement));
    }

    [Fact]
    public async Task CoreCapabilityStatementOutputIsAccepted()
    {
        var capability = new FhirCapabilityStatement(
            "CapabilityStatement",
            "https://avenchart.example/api/fhir/R4/metadata",
            "1.0.0",
            "AvenChartFhirR4",
            "active",
            false,
            "2026-08-22T12:00:00Z",
            "AvenChart contributors",
            "instance",
            "4.0.1",
            new FhirSoftware("AvenChart", "Phase 3"),
            new FhirImplementation("AvenChart FHIR R4 read-only API", "https://avenchart.example/api/fhir/R4"),
            ["json"],
            [new FhirCapabilityResource(
                "server",
                null,
                [new FhirResourceCapability(
                    "Patient",
                    [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
                    [new FhirSearchParameter("name", "string"), new FhirSearchParameter("identifier", "token")])])]);

        using var payload = await ExecuteFhirResultAsync(capability);

        new FhirR4ValidationService().ValidateCoreResource(payload.RootElement);
    }

    [Fact]
    public async Task CorePatientSearchBundleOutputIsAccepted()
    {
        var patient = new FhirPatientResource(
            "Patient",
            "MOD-PAT-0004",
            [new FhirIdentifier("urn:avenchart:canonical-id", "MOD-PAT-0004")],
            [new FhirHumanName("official", "Example", ["Avery"])],
            "unknown",
            "1980-01-01",
            null,
            null);
        var bundle = new FhirSearchBundle(
            "Bundle",
            "searchset",
            1,
            [new FhirBundleLink("self", "https://avenchart.example/api/fhir/R4/Patient?_count=1&page=1")],
            [new FhirSearchEntry("https://avenchart.example/api/fhir/R4/Patient/MOD-PAT-0004", patient)]);
        using var payload = await ExecuteFhirResultAsync(bundle);

        new FhirR4ValidationService().ValidateCoreResource(payload.RootElement);
    }

    [Fact]
    public async Task CoreEmptyPatientSearchBundleOmitsEntryAndIsAccepted()
    {
        var bundle = new FhirSearchBundle(
            "Bundle",
            "searchset",
            0,
            [new FhirBundleLink("self", "https://avenchart.example/api/fhir/R4/Patient?_count=1&page=1")],
            null);
        using var payload = await ExecuteFhirResultAsync(bundle);

        new FhirR4ValidationService().ValidateCoreResource(payload.RootElement);

        Assert.False(payload.RootElement.TryGetProperty("entry", out _));
    }

    [Fact]
    public async Task CoreObservationSearchBundleOmitsEmptyOptionalRepeatsAndIsAccepted()
    {
        var observation = new FhirObservationResource(
            "Observation",
            "1",
            "final",
            [new FhirCodeableConcept(
                [new FhirCoding("http://terminology.hl7.org/CodeSystem/observation-category", "laboratory", "Laboratory")],
                "Laboratory")],
            new FhirCodeableConcept([new FhirCoding("http://loinc.org", "718-7", "Hemoglobin")], "Hemoglobin"),
            new FhirReference("Patient/MOD-PAT-0004"),
            "2026-08-22T12:00:00Z",
            new FhirQuantity(16.4m, "g/dL"),
            null,
            null,
            null);
        var bundle = new FhirObservationBundle(
            "Bundle",
            "searchset",
            1,
            [new FhirBundleLink("self", "https://avenchart.example/api/fhir/R4/Observation?_count=1&page=1")],
            [new FhirObservationSearchEntry("https://avenchart.example/api/fhir/R4/Observation/1", observation)]);
        using var payload = await ExecuteFhirResultAsync(bundle);

        new FhirR4ValidationService().ValidateCoreResource(payload.RootElement);

        var resource = payload.RootElement.GetProperty("entry")[0].GetProperty("resource");
        Assert.False(resource.TryGetProperty("referenceRange", out _));
        Assert.False(resource.TryGetProperty("interpretation", out _));
    }

    [Fact]
    public async Task CoreOperationOutcomeOutputIsAccepted()
    {
        using var payload = await ExecuteFhirResultAsync(
            FhirResults.Error(StatusCodes.Status400BadRequest, "invalid", "The supplied FHIR resource is invalid."));

        new FhirR4ValidationService().ValidateCoreResource(payload.RootElement);
    }

    private static Task<JsonDocument> ExecuteFhirResultAsync<T>(T resource) =>
        ExecuteFhirResultAsync(FhirResults.Ok(resource));

    private static async Task<JsonDocument> ExecuteFhirResultAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        await using var responseBody = new MemoryStream();
        context.RequestServices = services;
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        Assert.Equal(FhirResults.ContentType, context.Response.ContentType);
        responseBody.Position = 0;
        return await JsonDocument.ParseAsync(responseBody);
    }

    private const string ValidLaboratoryBundleJson = """
        {
          "resourceType": "Bundle",
          "type": "collection",
          "entry": [
            {
              "fullUrl": "https://synthetic-laboratory.example/fhir/DiagnosticReport/report-1",
              "resource": {
                "resourceType": "DiagnosticReport",
                "id": "report-1",
                "status": "final",
                "code": { "coding": [{ "system": "http://loinc.org", "code": "58410-2", "display": "Complete blood count" }] },
                "subject": { "reference": "Patient/MOD-PAT-0004" },
                "basedOn": [{ "reference": "ServiceRequest/42" }],
                "specimen": [{ "reference": "Specimen/17" }],
                "effectiveDateTime": "2026-08-22T12:00:00Z",
                "issued": "2026-08-22T12:05:00Z",
                "result": [{ "reference": "Observation/observation-1" }]
              }
            },
            {
              "fullUrl": "https://synthetic-laboratory.example/fhir/Observation/observation-1",
              "resource": {
                "resourceType": "Observation",
                "id": "observation-1",
                "status": "final",
                "code": { "coding": [{ "system": "http://loinc.org", "code": "718-7", "display": "Hemoglobin" }] },
                "subject": { "reference": "Patient/MOD-PAT-0004" },
                "effectiveDateTime": "2026-08-22T12:00:00Z",
                "valueQuantity": { "value": 16.4, "unit": "g/dL" }
              }
            }
          ]
        }
        """;
}
