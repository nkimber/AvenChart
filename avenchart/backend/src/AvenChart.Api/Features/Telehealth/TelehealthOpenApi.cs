// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthOpenApi
{
    private const string PortalSessionScheme = "AvenChartPatientPortalSession";
    private const string ApplicantAccessScheme = "AvenChartTelehealthApplicantAccess";
    private const string StaffSessionScheme = "AvenChartLocalStaffSession";
    private const string OidcBearerScheme = "AvenChartOidcBearer";

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[PortalSessionScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-AvenChart-Patient-Portal-Session",
                In = ParameterLocation.Header,
                Description = "Local synthetic patient-portal session. External identity modes use the documented OIDC bearer alternative."
            };
            document.Components.SecuritySchemes[ApplicantAccessScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = TelehealthEndpoints.ApplicantAccessHeader,
                In = ParameterLocation.Header,
                Description = "High-entropy, browser-generated synthetic applicant credential. The server stores only its SHA-256 hash."
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer(TransformAsync);
    }

    private static Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var path = context.Description.RelativePath?.TrimStart('/') ?? string.Empty;
        if (!path.StartsWith("api/telehealth/v1/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        operation.Description = string.Join(" ", new[]
        {
            operation.Description,
            "This endpoint belongs to the disabled-by-default synthetic telehealth contract. Mutating commands use semantic idempotency; state changes after aggregate creation also use ExpectedVersion for optimistic concurrency."
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var document = context.Document ?? throw new InvalidOperationException("Telehealth OpenAPI transformation requires a document.");
        if (path.StartsWith("api/telehealth/v1/applicants", StringComparison.OrdinalIgnoreCase))
        {
            SetSingleSecurity(operation, document, ApplicantAccessScheme);
            AddResponse(operation, "401", "A valid high-entropy synthetic applicant access key is required.");
            AddResponse(operation, "404", "The applicant is not present in the authorized practice/facility/key scope.");
        }
        else if (path.StartsWith("api/telehealth/v1/patient/", StringComparison.OrdinalIgnoreCase))
        {
            SetAlternativeSecurity(operation, document, PortalSessionScheme, OidcBearerScheme);
            AddResponse(operation, "401", "An active patient-portal session or valid mapped OIDC bearer is required.");
        }
        else if (path.StartsWith("api/telehealth/v1/admin/", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("api/telehealth/v1/clinician/", StringComparison.OrdinalIgnoreCase))
        {
            SetAlternativeSecurity(operation, document, StaffSessionScheme, OidcBearerScheme);
            AddHeader(operation, StaffAccessContextService.FacilityHeader, JsonSchemaType.Integer, "Granted selected facility.");
            AddHeader(operation, StaffAccessContextService.PurposeHeader, JsonSchemaType.String, "Granted purpose of use.");
            AddResponse(operation, "401", "An active staff session or valid OIDC bearer is required.");
            AddResponse(operation, "403", "The staff role, permission, facility, purpose, or resource scope is not authorized.");
        }

        var isSemanticCommand = string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(context.Description.HttpMethod, "PUT", StringComparison.OrdinalIgnoreCase)
                && (path.EndsWith("/pharmacy-choice", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/prescription-preparation-draft", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/safety-disposition-draft", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/identity-review-decision", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/promotion-authorization-decision", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith("/synthetic-promotion", StringComparison.OrdinalIgnoreCase)));
        if (isSemanticCommand)
        {
            AddHeader(operation, TelehealthEndpoints.IdempotencyHeader, JsonSchemaType.String,
                "Required semantic idempotency key, bound to the complete command content.");
            AddResponse(operation, "400", "The command, idempotency key, or expected version is invalid.");
            AddResponse(operation, "409", "The idempotency key, aggregate version, state, or queue reservation conflicts.");
        }

        return Task.CompletedTask;
    }

    private static void SetAlternativeSecurity(
        OpenApiOperation operation,
        OpenApiDocument document,
        string first,
        string second)
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Clear();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(first, document, null)] = []
        });
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(second, document, null)] = []
        });
    }

    private static void SetSingleSecurity(
        OpenApiOperation operation,
        OpenApiDocument document,
        string scheme)
    {
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Clear();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(scheme, document, null)] = []
        });
    }

    private static void AddHeader(OpenApiOperation operation, string name, JsonSchemaType type, string description)
    {
        var parameters = operation.Parameters ??= new List<IOpenApiParameter>();
        if (parameters.Any(item => item.In == ParameterLocation.Header && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
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

    private static void AddResponse(OpenApiOperation operation, string code, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        if (!operation.Responses.ContainsKey(code))
        {
            operation.Responses[code] = new OpenApiResponse { Description = description };
        }
    }
}
