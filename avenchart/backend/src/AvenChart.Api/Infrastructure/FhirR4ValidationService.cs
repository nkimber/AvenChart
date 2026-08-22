// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Firely.Fhir.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Validates constrained external-laboratory input against the FHIR R4 core
/// profiles before AvenChart applies its narrower clinical intake contract.
/// The service is request scoped because the upstream validator and resolver
/// caches are not thread-safe; its validator is still reused for every
/// validation performed during the owning request.
/// </summary>
public sealed class FhirR4ValidationService
{
    private readonly Validator validator;

    public FhirR4ValidationService()
    {
        var resolver = ZipSource.CreateValidationSource();
        validator = new Validator(
            resolver,
            TerminologyServiceFactory.CreateDefaultForCore(resolver));
    }

    /// <summary>
    /// Parses and validates the FHIR Bundle, DiagnosticReport, and Observation
    /// core profiles used by the external laboratory endpoint.  This does not
    /// replace the local contract: facility authorization, source provenance,
    /// local identifiers, LOINC, and workflow state remain enforced by the
    /// clinical intake repository after this standard validation succeeds.
    /// </summary>
    /// <exception cref="FhirR4ValidationException">The payload is not valid FHIR R4 JSON or violates a core profile.</exception>
    public void ValidateExternalLaboratoryBundle(JsonElement payload)
    {
        Bundle bundle;
        try
        {
            bundle = FhirJsonDeserializer.STRICT.Deserialize<Bundle>(payload.GetRawText());
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or JsonException)
        {
            throw new FhirR4ValidationException("The payload is not valid FHIR R4 JSON.", exception);
        }

        ValidateCoreProfile(bundle, "Bundle");
        foreach (var entry in bundle.Entry ?? [])
        {
            switch (entry.Resource)
            {
                case DiagnosticReport diagnosticReport:
                    ValidateCoreProfile(diagnosticReport, "DiagnosticReport");
                    break;
                case Observation observation:
                    ValidateCoreProfile(observation, "Observation");
                    break;
            }
        }
    }

    private void ValidateCoreProfile(Resource resource, string resourceType)
    {
        var outcome = validator.Validate(resource);
        if (!outcome.Success)
        {
            var diagnostics = outcome.Issue
                .Select(issue => $"{issue.Severity}: {issue.Code} {issue.Details?.Text} {issue.Diagnostics}".Trim())
                .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
                .Take(3)
                .ToArray();
            var detail = diagnostics.Length == 0
                ? string.Empty
                : $" {string.Join(" ", diagnostics)}";
            throw new FhirR4ValidationException($"The payload does not conform to the FHIR R4 {resourceType} profile.{detail}");
        }
    }
}

public sealed class FhirR4ValidationException(string message, Exception? innerException = null)
    : ArgumentException(message, innerException);
