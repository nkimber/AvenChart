// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Adds the cross-cutting contract information that endpoint result inference
/// cannot discover from the application's endpoint filters. The contract is
/// deliberately configuration-neutral: deployments may use the local session
/// adapter or the vendor-neutral OIDC bearer adapter, but staff resource
/// access always also requires an explicit facility and purpose context.
/// </summary>
public static class AvenChartOpenApi
{
    private const string LocalStaffSessionScheme = "AvenChartLocalStaffSession";
    private const string OidcBearerScheme = "AvenChartOidcBearer";
    private const string ExternalLaboratorySourceScheme = "AvenChartExternalLaboratorySource";
    private const string ExternalLaboratoryApiKeyScheme = "AvenChartExternalLaboratoryApiKey";
    private const string JsonContentType = "application/json";

    private static readonly string[] StaffRoutePrefixes =
    [
        "api/fhir/R4",
        "api/patients",
        "api/clinical-workflows",
        "api/appointments",
        "api/encounters",
        "api/clinical-lists",
        "api/messages",
        "api/office-notes",
        "api/administration/address-book",
        "api/administration/tracks",
        "api/patient-education",
        "api/recalls",
        "api/batch-communication",
        "api/chart-tracker",
        "api/records",
        "api/documents",
        "api/procedures",
        "api/integrations",
        "api/inventory",
        "api/billing",
        "api/administration",
        "api/configuration-delegation",
        "api/form-engine",
        "api/reports",
        "api/therapy-groups"
    ];

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[LocalStaffSessionScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-AvenChart-Session",
                In = ParameterLocation.Header,
                Description = "Local-development staff session identifier. This scheme is available only when IdentityProvider:Mode is local."
            };
            document.Components.SecuritySchemes[OidcBearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Vendor-neutral OpenID Connect access token. A deployment validates issuer, audience, signature, and expiry through OIDC discovery and JWKS."
            };
            document.Components.SecuritySchemes[ExternalLaboratorySourceScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-AvenChart-Lab-Source",
                In = ParameterLocation.Header,
                Description = "Registered external laboratory source identifier."
            };
            document.Components.SecuritySchemes[ExternalLaboratoryApiKeyScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-AvenChart-Lab-Api-Key",
                In = ParameterLocation.Header,
                Description = "Credential for the registered external laboratory source."
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(TransformOperationAsync);
    }

    private static async Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var relativePath = context.Description.RelativePath?.TrimStart('/') ?? string.Empty;

        if (IsStaffRoute(relativePath))
        {
            ConfigureStaffAccess(
                operation,
                context.Document ?? throw new InvalidOperationException("OpenAPI operation transformation requires a document."));
            AddResponse(operation, "401", "The supplied staff session or OIDC bearer token is absent, expired, invalid, or revoked.");
            AddResponse(operation, "403", "The authenticated staff identity lacks the required permission, facility grant, or purpose-of-use grant.");
        }

        if (relativePath.StartsWith("api/fhir/R4", StringComparison.OrdinalIgnoreCase))
        {
            await ConfigureFhirOperationAsync(operation, context, cancellationToken);
        }
        else if (string.Equals(relativePath, "api/external-laboratory-results/fhir-r4", StringComparison.OrdinalIgnoreCase))
        {
            await ConfigureExternalLaboratoryOperationAsync(operation, context, cancellationToken);
        }
        else if (string.Equals(relativePath, "api/integrations/outbox", StringComparison.OrdinalIgnoreCase)
                 && string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await ConfigureIntegrationOutboxQueueOperationAsync(operation, context, cancellationToken);
        }
        else if (string.Equals(relativePath, "api/integrations/inbox", StringComparison.OrdinalIgnoreCase)
                 && string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            await ConfigureIntegrationInboxReceiveOperationAsync(operation, context, cancellationToken);
        }
    }

    private static bool IsStaffRoute(string relativePath) =>
        StaffRoutePrefixes.Any(prefix =>
            relativePath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase));

    private static void ConfigureStaffAccess(OpenApiOperation operation, OpenApiDocument document)
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Clear();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(LocalStaffSessionScheme, document, null)] = []
        });
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(OidcBearerScheme, document, null)] = []
        });

        AddRequiredHeader(
            operation,
            StaffAccessContextService.FacilityHeader,
            "The granted facility identifier selected for this request.",
            JsonSchemaType.Integer);
        AddRequiredHeader(
            operation,
            StaffAccessContextService.PurposeHeader,
            "The granted purpose of use selected for this request (treatment, payment, or healthcare-operations).",
            JsonSchemaType.String);
    }

    private static async Task ConfigureFhirOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var fhirResourceSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "FHIR R4 resource or Bundle. Every response representation is application/fhir+json."
        };
        var operationOutcomeSchema = await context.GetOrCreateSchemaAsync(
            typeof(FhirOperationOutcome), null, cancellationToken);

        operation.Responses ??= new OpenApiResponses();
        operation.Responses["200"] = JsonResponse(
            "FHIR R4 resource, Bundle, or CapabilityStatement returned successfully.",
            FhirResults.ContentType,
            fhirResourceSchema);
        AddResponse(operation, "404", "The requested FHIR resource was not found.", FhirResults.ContentType, operationOutcomeSchema);
        AddResponse(operation, "406", "The requested response representation is unsupported. A FHIR OperationOutcome explains the failure.", FhirResults.ContentType, operationOutcomeSchema);
    }

    private static async Task ConfigureExternalLaboratoryOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Clear();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ExternalLaboratorySourceScheme, context.Document, null)] = [],
            [new OpenApiSecuritySchemeReference(ExternalLaboratoryApiKeyScheme, context.Document, null)] = []
        });
        AddRequiredHeader(
            operation,
            "X-AvenChart-Lab-Message-Id",
            "Stable source-scoped message identifier used for idempotency. It must contain 3-160 letters, digits, '.', '_', ':', or '-'.",
            JsonSchemaType.String);

        var receiptSchema = await context.GetOrCreateSchemaAsync(
            typeof(ExternalLaboratoryIntakeReceipt), null, cancellationToken);
        var operationOutcomeSchema = await context.GetOrCreateSchemaAsync(
            typeof(FhirOperationOutcome), null, cancellationToken);
        var fhirBundleSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "FHIR R4 Bundle containing the profiled patient, ServiceRequest, Specimen, DiagnosticReport, Observation, and laboratory identity required by the external-laboratory intake contract."
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "A FHIR R4 external laboratory result Bundle.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [FhirResults.ContentType] = new() { Schema = fhirBundleSchema },
                [JsonContentType] = new() { Schema = fhirBundleSchema }
            }
        };
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["200"] = JsonResponse("An exact prior source-message replay was accepted without a clinical mutation.", JsonContentType, receiptSchema);
        operation.Responses["201"] = JsonResponse("The authenticated laboratory bundle was applied to the governed clinical intake contract.", JsonContentType, receiptSchema);
        AddResponse(operation, "400", "The message ID or FHIR payload is invalid.", FhirResults.ContentType, operationOutcomeSchema);
        AddResponse(operation, "401", "The laboratory source identifier or API key is invalid, inactive, or absent.", FhirResults.ContentType, operationOutcomeSchema);
        AddResponse(operation, "409", "The source message identifier was replayed with different payload content.", FhirResults.ContentType, operationOutcomeSchema);
        AddResponse(operation, "415", "Only application/fhir+json and application/json request representations are accepted.", FhirResults.ContentType, operationOutcomeSchema);
        AddResponse(operation, "422", "The authenticated bundle could not be reconciled to the required clinical context.", FhirResults.ContentType, operationOutcomeSchema);
    }

    private static async Task ConfigureIntegrationOutboxQueueOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requestSchema = await context.GetOrCreateSchemaAsync(
            typeof(IntegrationOutboxQueueRequest), null, cancellationToken);
        var responseSchema = await context.GetOrCreateSchemaAsync(
            typeof(IntegrationOutboxMessage), null, cancellationToken);
        operation.RequestBody = JsonRequestBody("A semantic idempotency key is required and is bound to the complete queue request content.", requestSchema);
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.Remove("200");
        operation.Responses["201"] = JsonResponse("A new outbox message was queued.", JsonContentType, responseSchema);
        AddResponse(operation, "400", "The queue request is invalid, including a missing idempotency key.");
        AddResponse(operation, "409", "The idempotency key was previously bound to different request content.");
    }

    private static async Task ConfigureIntegrationInboxReceiveOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requestSchema = await context.GetOrCreateSchemaAsync(
            typeof(IntegrationInboxReceiveRequest), null, cancellationToken);
        var responseSchema = await context.GetOrCreateSchemaAsync(
            typeof(IntegrationInboxReceipt), null, cancellationToken);
        operation.RequestBody = JsonRequestBody("The source and source message ID are bound to the complete inbound message content.", requestSchema);
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["200"] = JsonResponse("An exact prior inbound message replay was accepted as a duplicate.", JsonContentType, responseSchema);
        operation.Responses["201"] = JsonResponse("A new inbound message was accepted.", JsonContentType, responseSchema);
        AddResponse(operation, "400", "The inbound message is invalid.");
        AddResponse(operation, "409", "The source message identifier was previously bound to different content.");
    }

    private static void AddRequiredHeader(
        OpenApiOperation operation,
        string name,
        string description,
        JsonSchemaType type)
    {
        var parameters = operation.Parameters ??= new List<IOpenApiParameter>();
        if (parameters.Any(parameter =>
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)
                && parameter.In == ParameterLocation.Header))
        {
            return;
        }

        parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = true,
            Description = description,
            Schema = new OpenApiSchema { Type = type }
        });
    }

    private static OpenApiRequestBody JsonRequestBody(string description, IOpenApiSchema schema) => new()
    {
        Required = true,
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            [JsonContentType] = new() { Schema = schema }
        }
    };

    private static OpenApiResponse JsonResponse(string description, string contentType, IOpenApiSchema schema) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            [contentType] = new() { Schema = schema }
        }
    };

    private static void AddResponse(
        OpenApiOperation operation,
        string statusCode,
        string description,
        string? contentType = null,
        IOpenApiSchema? schema = null)
    {
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(statusCode))
        {
            return;
        }

        operation.Responses[statusCode] = contentType is not null && schema is not null
            ? JsonResponse(description, contentType, schema)
            : new OpenApiResponse { Description = description };
    }
}
