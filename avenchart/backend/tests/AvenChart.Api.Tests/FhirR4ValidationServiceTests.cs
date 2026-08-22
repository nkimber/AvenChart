// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using AvenChart.Api.Infrastructure;

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
