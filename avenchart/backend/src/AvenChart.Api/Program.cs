// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Experience;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Security;
using AvenChart.Api.Workflows;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

var builder = WebApplication.CreateBuilder(args);

var runtimeSafetyOptions = builder.Configuration
    .GetSection(RuntimeSafetyOptions.SectionName)
    .Get<RuntimeSafetyOptions>() ?? new RuntimeSafetyOptions();

builder.Services.AddOpenApi(AvenChartOpenApi.Configure);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Items["correlationId"]?.ToString()
            ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddResponseCompression();
var dataProtection = builder.Services.AddDataProtection();
if (RuntimeSafetyPolicy.HasCompleteDataProtectionConfiguration(runtimeSafetyOptions)
    && !string.IsNullOrWhiteSpace(runtimeSafetyOptions.DataProtectionKeyRingPath))
{
    var keyRingPath = runtimeSafetyOptions.DataProtectionKeyRingPath;
    var certificatePath = runtimeSafetyOptions.DataProtectionCertificatePath!;
    if (!Directory.Exists(keyRingPath))
    {
        throw new InvalidOperationException(
            "RuntimeSafety:DataProtectionKeyRingPath must name an existing durable directory when data protection is configured.");
    }

    if (!File.Exists(certificatePath))
    {
        throw new InvalidOperationException(
            "RuntimeSafety:DataProtectionCertificatePath must name an existing PFX certificate when data protection is configured.");
    }

    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        certificatePath,
        runtimeSafetyOptions.DataProtectionCertificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    dataProtection
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName(runtimeSafetyOptions.DataProtectionApplicationName!)
        .ProtectKeysWithCertificate(certificate);
}
builder.Services.AddSingleton<SchemaMigrationCatalog>();
builder.Services.AddSingleton<DatabaseBootstrapCatalog>();
builder.Services.AddSingleton<SchemaMigrationState>();
builder.Services.AddSingleton<DatabaseSchemaMigrator>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadinessHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<SchemaMigrationReadinessHealthCheck>("schemaMigrations", tags: ["ready"]);
builder.Services.AddSingleton<IIntegrationTransport, LocalDeterministicIntegrationTransport>();
builder.Services.AddSingleton<RuntimeDiagnostics>();
builder.Services.AddScoped<FhirR4ValidationService>();
builder.Services.AddHttpClient("azure-deployment-health", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AvenChart-Azure-Operations/1.0");
});
builder.Services.AddHttpClient("browser-oidc", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AvenChart-Browser-OIDC/1.0");
});

builder.Services.AddOptions<AzureOperationsOptions>()
    .BindConfiguration(AzureOperationsOptions.SectionName)
    .Validate(options => options.CommandTimeoutMinutes is >= 1 and <= 120,
        "AzureOperations:CommandTimeoutMinutes must be between 1 and 120.")
    .Validate(options => options.MigrationTimeoutMinutes is >= 1 and <= 120,
        "AzureOperations:MigrationTimeoutMinutes must be between 1 and 120.")
    .Validate(options => options.AccessGrantMinutes is >= 1 and <= 60,
        "AzureOperations:AccessGrantMinutes must be between 1 and 60.")
    .Validate(options => options.UnlockMaximumFailures is >= 3 and <= 20,
        "AzureOperations:UnlockMaximumFailures must be between 3 and 20.")
    .Validate(options => options.UnlockFailureWindowMinutes is >= 1 and <= 1440,
        "AzureOperations:UnlockFailureWindowMinutes must be between 1 and 1440.")
    .Validate(options => options.UnlockLockoutMinutes is >= 1 and <= 1440,
        "AzureOperations:UnlockLockoutMinutes must be between 1 and 1440.")
    .Validate(options => options.AccessCodeHashIterations is >= 100_000 and <= 2_000_000,
        "AzureOperations:AccessCodeHashIterations must be between 100,000 and 2,000,000.")
    .ValidateOnStart();

builder.Services.AddOptions<RuntimeSafetyOptions>()
    .BindConfiguration(RuntimeSafetyOptions.SectionName)
    .Validate(
        options => options.RateLimitPermitLimit > 0,
        "RuntimeSafety:RateLimitPermitLimit must be greater than zero.")
    .Validate(
        options => options.RateLimitWindowSeconds > 0,
        "RuntimeSafety:RateLimitWindowSeconds must be greater than zero.")
    .Validate(
        options => options.RateLimitQueueLimit >= 0,
        "RuntimeSafety:RateLimitQueueLimit must not be negative.")
    .Validate(
        options => options.ForwardedHeaderLimit is >= 1 and <= 5,
        "RuntimeSafety:ForwardedHeaderLimit must be between 1 and 5.")
    .Validate(
        options => RuntimeSafetyPolicy.HasValidTrustedProxyAddresses(options.TrustedProxyAddresses),
        "RuntimeSafety:TrustedProxyAddresses must contain only explicit IP addresses.")
    .Validate(
        RuntimeSafetyPolicy.HasCompleteDataProtectionConfiguration,
        "RuntimeSafety data-protection configuration must specify key ring path, application name, and certificate path together.")
    .Validate(
        options => !builder.Environment.IsProduction() || options.RequireHttps,
        "RuntimeSafety:RequireHttps must be true in Production.")
    .Validate(
        _ => !builder.Environment.IsProduction()
            || RuntimeSafetyPolicy.HasExplicitAllowedHosts(builder.Configuration["AllowedHosts"]),
        "AllowedHosts must name explicit allowed hosts and cannot contain '*' in Production.")
    .Validate(
        options => !builder.Environment.IsProduction()
            || RuntimeSafetyPolicy.HasProductionDataProtectionConfiguration(options),
        "Production requires absolute RuntimeSafety data-protection key-ring, application-name, and certificate-path settings.")
    .Validate(
        options => !builder.Environment.IsProduction() || !options.EnableSyntheticFinancialMutations,
        "RuntimeSafety:EnableSyntheticFinancialMutations cannot be enabled in Production.")
    .ValidateOnStart();

builder.Services.AddOptions<IdentityProviderOptions>()
    .BindConfiguration(IdentityProviderOptions.SectionName)
    .Validate(options => options.IsLocal || options.IsOidc || options.IsTestOidc,
        "IdentityProvider:Mode must be local, oidc, or test-oidc.")
    .Validate(options => options.ClockSkewSeconds is >= 0 and <= 300,
        "IdentityProvider:ClockSkewSeconds must be between 0 and 300.")
    .Validate(options => options.TestTokenLifetimeMinutes is >= 1 and <= 60,
        "IdentityProvider:TestTokenLifetimeMinutes must be between 1 and 60.")
    .Validate(options => options.BrowserStateLifetimeSeconds is >= 60 and <= 900,
        "IdentityProvider:BrowserStateLifetimeSeconds must be between 60 and 900.")
    .Validate(options => options.BrowserAllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.IsNullOrWhiteSpace(uri.UserInfo)
        && uri.AbsolutePath == "/"
        && string.IsNullOrWhiteSpace(uri.Query)
        && string.IsNullOrWhiteSpace(uri.Fragment)),
        "IdentityProvider:BrowserAllowedOrigins must contain absolute origin URLs only.")
    .Validate(options => !options.EnableBrowserBff || options.IsOidc,
        "IdentityProvider:EnableBrowserBff is supported only with IdentityProvider:Mode oidc; test-oidc enables its development BFF automatically.")
    .Validate(options => !options.EnableBrowserBff || options.BrowserBffEnabled,
        "IdentityProvider:EnableBrowserBff requires BrowserClientId and at least one BrowserAllowedOrigin.")
    .Validate(options => !options.IsOidc || (!string.IsNullOrWhiteSpace(options.Authority) && !string.IsNullOrWhiteSpace(options.Audience) && !string.IsNullOrWhiteSpace(options.ProviderId)),
        "IdentityProvider:Authority, Audience, and ProviderId are required when IdentityProvider:Mode is oidc.")
    .Validate(options => !options.IsTestOidc || (!string.IsNullOrWhiteSpace(options.TestIssuer) && !string.IsNullOrWhiteSpace(options.TestAudience)),
        "IdentityProvider:TestIssuer and TestAudience are required when IdentityProvider:Mode is test-oidc.")
    .Validate(options => !builder.Environment.IsProduction() || !options.IsTestOidc,
        "IdentityProvider:Mode test-oidc is development-only and cannot run in Production.")
    .Validate(options => !builder.Environment.IsProduction() || !options.BrowserBffEnabled
        || (Uri.TryCreate(options.BrowserCallbackUrl, UriKind.Absolute, out var callback)
            && string.Equals(callback.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && options.BrowserAllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
                && string.Equals(originUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))),
        "IdentityProvider browser BFF requires an explicit HTTPS BrowserCallbackUrl and HTTPS BrowserAllowedOrigins in Production.")
    .ValidateOnStart();

builder.Services.AddOptions<ReportExecutionOptions>()
    .BindConfiguration(ReportExecutionOptions.SectionName)
    .Validate(
        options => options.PollIntervalMilliseconds is >= 50 and <= 5000,
        "ReportExecution:PollIntervalMilliseconds must be between 50 and 5000.")
    .Validate(
        options => options.EnqueueDelayMilliseconds is >= 0 and <= 10000,
        "ReportExecution:EnqueueDelayMilliseconds must be between 0 and 10000.")
    .Validate(
        options => options.HeartbeatIntervalMilliseconds is >= 100 and <= 10000,
        "ReportExecution:HeartbeatIntervalMilliseconds must be between 100 and 10000.")
    .Validate(
        options => options.LeaseSeconds is >= 5 and <= 600,
        "ReportExecution:LeaseSeconds must be between 5 and 600.")
    .Validate(
        options => options.ExecutionTimeoutSeconds is >= 1 and <= 300,
        "ReportExecution:ExecutionTimeoutSeconds must be between 1 and 300.")
    .Validate(
        options => options.QueueExpirationMinutes is >= 1 and <= 1440,
        "ReportExecution:QueueExpirationMinutes must be between 1 and 1440.")
    .Validate(
        options => options.MaxAttempts is >= 1 and <= 10,
        "ReportExecution:MaxAttempts must be between 1 and 10.")
    .Validate(
        options => options.RetryBaseDelaySeconds is >= 1 and <= 60,
        "ReportExecution:RetryBaseDelaySeconds must be between 1 and 60.")
    .Validate(
        options =>
            options.HeartbeatIntervalMilliseconds <
            options.LeaseSeconds * 1000,
        "ReportExecution heartbeat must be shorter than its lease.")
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseConnectionOptions>()
    .BindConfiguration(DatabaseConnectionOptions.SectionName)
    .Validate(
        options => options.ConnectionTimeoutSeconds is >= 5 and <= 60,
        "DatabaseConnection:ConnectionTimeoutSeconds must be between 5 and 60.")
    .Validate(
        options => options.CommandTimeoutSeconds is >= 5 and <= 300,
        "DatabaseConnection:CommandTimeoutSeconds must be between 5 and 300.")
    .Validate(
        options => options.CancellationTimeoutMilliseconds is >= 500 and <= 10000,
        "DatabaseConnection:CancellationTimeoutMilliseconds must be between 500 and 10000.")
    .Validate(
        options => options.MinimumPoolSize is >= 0 and <= 100,
        "DatabaseConnection:MinimumPoolSize must be between 0 and 100.")
    .Validate(
        options => options.MaximumPoolSize is >= 5 and <= 500,
        "DatabaseConnection:MaximumPoolSize must be between 5 and 500.")
    .Validate(
        options => options.MinimumPoolSize <= options.MaximumPoolSize,
        "DatabaseConnection:MinimumPoolSize cannot exceed MaximumPoolSize.")
    .Validate(
        options => options.KeepAliveSeconds is 0 or >= 10 and <= 600,
        "DatabaseConnection:KeepAliveSeconds must be 0 or between 10 and 600.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("AvenChart")
    ?? "Host=localhost;Port=5433;Database=avenchart;Username=avenchart;Password=avenchart_demo";
var databaseConnectionOptions = builder.Configuration
    .GetSection(DatabaseConnectionOptions.SectionName)
    .Get<DatabaseConnectionOptions>() ?? new DatabaseConnectionOptions();
var databaseConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
{
    Timeout = databaseConnectionOptions.ConnectionTimeoutSeconds,
    CommandTimeout = databaseConnectionOptions.CommandTimeoutSeconds,
    CancellationTimeout = databaseConnectionOptions.CancellationTimeoutMilliseconds,
    MinPoolSize = databaseConnectionOptions.MinimumPoolSize,
    MaxPoolSize = databaseConnectionOptions.MaximumPoolSize,
    KeepAlive = databaseConnectionOptions.KeepAliveSeconds
}.ConnectionString;

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(databaseConnectionString));
builder.Services.AddDbContext<AvenChartDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddScoped<PatientRepository>();
builder.Services.AddScoped<PatientXmlExchangeRepository>();
builder.Services.AddScoped<PatientPrintRepository>();
builder.Services.AddScoped<AppointmentRepository>();
builder.Services.AddScoped<EncounterRepository>();
builder.Services.AddScoped<EncounterStateRepository>();
builder.Services.AddScoped<EncounterLayoutFormRepository>();
builder.Services.AddScoped<ClinicalAlertEvaluationRepository>();
builder.Services.AddScoped<ClinicalListRepository>();
builder.Services.AddScoped<ClinicalListStateRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<OfficeNoteRepository>();
builder.Services.AddScoped<AddressBookRepository>();
builder.Services.AddScoped<TrackAnythingRepository>();
builder.Services.AddScoped<PatientEducationRepository>();
builder.Services.AddScoped<RecallRepository>();
builder.Services.AddScoped<BatchCommunicationRepository>();
builder.Services.AddScoped<ChartTrackerRepository>();
builder.Services.AddScoped<DocumentTemplateRepository>();
builder.Services.AddScoped<DocumentRepository>();
builder.Services.AddScoped<ManagedRecordRepository>();
builder.Services.AddScoped<ProcedureRepository>();
builder.Services.AddScoped<ProcedureDirectoryRepository>();
builder.Services.AddScoped<BillingRepository>();
builder.Services.AddScoped<AdministrationRepository>();
builder.Services.AddScoped<AdministrationDirectoryRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddScoped<ReportDefinitionRepository>();
builder.Services.AddScoped<ReportExecutionRepository>();
builder.Services.AddScoped<ReportExecutionQueueRepository>();
builder.Services.AddHostedService<ReportExecutionWorker>();
builder.Services.AddScoped<ClinicalFormRepository>();
builder.Services.AddScoped<LegacyClinicalFormDisplayRepository>();
builder.Services.AddScoped<TherapyGroupRepository>();
builder.Services.AddScoped<ReferralRepository>();
builder.Services.AddScoped<AuthorizationRepository>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<ExternalIdentityMappingRepository>();
builder.Services.AddScoped<PatientPortalExternalIdentityMappingRepository>();
builder.Services.AddScoped<BrowserOidcSessionService>();
builder.Services.AddScoped<LocalDevelopmentStaffIdentityAdapter>();
builder.Services.AddScoped<OidcStaffIdentityAdapter>();
builder.Services.AddScoped<TestOidcStaffIdentityAdapter>();
builder.Services.AddSingleton<TestIdentityProviderService>();
builder.Services.AddScoped<IStaffIdentityAdapter>(services =>
{
    var identityProvider = services.GetRequiredService<IOptions<IdentityProviderOptions>>().Value;
    return identityProvider.IsOidc
        ? services.GetRequiredService<OidcStaffIdentityAdapter>()
        : identityProvider.IsTestOidc
            ? services.GetRequiredService<TestOidcStaffIdentityAdapter>()
            : services.GetRequiredService<LocalDevelopmentStaffIdentityAdapter>();
});
builder.Services.AddScoped<LocalPatientPortalIdentityAdapter>();
builder.Services.AddScoped<OidcPatientPortalIdentityAdapter>();
builder.Services.AddScoped<TestOidcPatientPortalIdentityAdapter>();
builder.Services.AddScoped<IPatientPortalIdentityAdapter>(services =>
{
    var identityProvider = services.GetRequiredService<IOptions<IdentityProviderOptions>>().Value;
    return identityProvider.IsOidc
        ? services.GetRequiredService<OidcPatientPortalIdentityAdapter>()
        : identityProvider.IsTestOidc
            ? services.GetRequiredService<TestOidcPatientPortalIdentityAdapter>()
            : services.GetRequiredService<LocalPatientPortalIdentityAdapter>();
});
builder.Services.AddScoped<StaffAccessContextService>();
builder.Services.AddScoped<PatientPortalRepository>();
builder.Services.AddScoped<IntegrationRepository>();
builder.Services.AddScoped<ExternalLaboratorySourceRepository>();
builder.Services.AddScoped<ExternalLaboratoryIntakeRepository>();
builder.Services.AddScoped<PhiAuditRepository>();
builder.Services.AddScoped<PatientMergeAuditRepository>();
builder.Services.AddScoped<PatientMergeExecutionRepository>();
builder.Services.AddScoped<PatientRecordRequestRepository>();
builder.Services.AddScoped<PatientDisclosureRepository>();
builder.Services.AddScoped<PatientSdohRepository>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<InventoryCostPolicyRepository>();
builder.Services.AddScoped<InventoryAccountingIntegrationRepository>();
builder.Services.AddScoped<InventoryReplenishmentPolicyRepository>();
builder.Services.AddScoped<InventoryValuationRepository>();
builder.Services.AddScoped<FlowBoardRepository>();
builder.Services.AddScoped<FhirRepository>();
builder.Services.AddScoped<AzureOperationsRepository>();
builder.Services.AddScoped<AzureOperationsAccessRepository>();
builder.Services.AddScoped<AzureOperationsAccessService>();
builder.Services.AddScoped<AzureOperationsService>();
builder.Services.AddSingleton<AzureCliRunner>();
builder.Services.AddSingleton<AzureDeploymentCoordinator>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<AzureDeploymentCoordinator>());

var configuredBrowserOrigins = builder.Configuration
    .GetSection(IdentityProviderOptions.SectionName)
    .Get<IdentityProviderOptions>()?.BrowserAllowedOrigins ?? [];
var corsOrigins = new[]
    {
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:3100",
        "http://127.0.0.1:3100"
    }
    .Concat(configuredBrowserOrigins.Where(origin => !string.IsNullOrWhiteSpace(origin)))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("local-app-clients", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("X-AvenChart-CSRF");
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = runtimeSafetyOptions.RateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(runtimeSafetyOptions.RateLimitWindowSeconds),
                QueueLimit = runtimeSafetyOptions.RateLimitQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
    options.OnRejected = static async (context, cancellationToken) =>
    {
        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Request limit reached",
                detail: "Too many requests were received. Retry after the rate-limit window.")
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

var configuredRuntimeSafety = app.Services.GetRequiredService<IOptions<RuntimeSafetyOptions>>().Value;
if (args.Any(argument => string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        var migrator = app.Services.GetRequiredService<DatabaseSchemaMigrator>();
        var result = await migrator.MigrateAsync(CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            status = "passed",
            expected = result.ExpectedCount,
            applied = result.Applied,
            alreadyApplied = result.AlreadyApplied
        }));
    }
    finally
    {
        await app.DisposeAsync();
    }

    return;
}

if (configuredRuntimeSafety.TrustedProxyAddresses.Length > 0)
{
    var forwardedHeaderOptions = new ForwardedHeadersOptions();
    RuntimeSafetyPolicy.ConfigureForwardedHeaders(
        forwardedHeaderOptions,
        RuntimeSafetyPolicy.ParseTrustedProxyAddresses(configuredRuntimeSafety.TrustedProxyAddresses),
        configuredRuntimeSafety.ForwardedHeaderLimit);
    app.UseForwardedHeaders(forwardedHeaderOptions);
}

// This is the versioned machine-readable contract for supported staff, FHIR,
// and external-laboratory clients. It contains no runtime data or credentials;
// authorization remains enforced by the documented endpoint contracts.
app.MapOpenApi();

app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    var exception = context.Features
        .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()
        ?.Error;
    if (FindSchemaShapeException(exception) is not null)
    {
        context.RequestServices.GetRequiredService<SchemaMigrationState>().Invalidate();
        await WriteSchemaNotReadyAsync(context);
        return;
    }

    if (exception is EncounterLockConflictException lockConflict)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            error = lockConflict.Message,
            code = "encounter_locked"
        });
        return;
    }

    if (exception is PostgresException
        {
            SqlState: PostgresErrorCodes.RaiseException,
            MessageText: "encounter_locked"
        } databaseLockConflict)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            error = databaseLockConflict.Detail
                ?? "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.",
            code = "encounter_locked"
        });
        return;
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected server error occurred." });
}));

if (configuredRuntimeSafety.RequireHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseCors("local-app-clients");
app.Use(async (context, next) =>
{
    var requestedCorrelationId = context.Request.Headers["X-Correlation-ID"].ToString();
    var correlationId = requestedCorrelationId.Length is > 0 and <= 80
        && requestedCorrelationId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? requestedCorrelationId
            : Guid.NewGuid().ToString("N");
    context.Items["correlationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    await next();
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.OnStarting(() =>
        {
            // API responses can contain ePHI. Keep them out of browser and intermediary caches,
            // including error responses and download endpoints that set their own response headers.
            context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            return Task.CompletedTask;
        });
    }

    await next();
});
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next(context);
        return;
    }

    var migrationState = context.RequestServices.GetRequiredService<SchemaMigrationState>();
    var validation = await migrationState.ValidateAsync(false, context.RequestAborted);
    if (!validation.IsReady)
    {
        await WriteSchemaNotReadyAsync(context);
        return;
    }

    await next(context);
});
app.Use(async (context, next) =>
{
    // A browser BFF session is cookie-authenticated. State-changing requests
    // must therefore prove same-origin intent with an independently issued
    // CSRF value; bearer and non-browser integration requests do not use this
    // path and remain governed by their own authentication boundary.
    var isUnsafeApiMethod = context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method);
    // The development-only test IdP is an identity-provider origin in this
    // topology, not an AvenChart application mutation endpoint. Its own
    // authorization-code correlation and PKCE checks apply there.
    var isDevelopmentTestIdentityProviderRequest = context.Request.Path.StartsWithSegments("/api/test-idp");
    if (isUnsafeApiMethod && !isDevelopmentTestIdentityProviderRequest)
    {
        var browserOidcSessions = context.RequestServices.GetRequiredService<BrowserOidcSessionService>();
        var hasBrowserSession = browserOidcSessions.IsBrowserSessionRequest(context, BrowserOidcSessionService.StaffAudience)
            || browserOidcSessions.IsBrowserSessionRequest(context, BrowserOidcSessionService.PortalAudience);
        if (hasBrowserSession)
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (!browserOidcSessions.IsAllowedBrowserOrigin(origin) || !browserOidcSessions.HasValidBrowserCsrf(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "A valid browser single sign-on origin and CSRF token are required." });
                return;
            }
        }
    }

    await next(context);
});
app.Use(async (context, next) =>
{
    // Keep all portal routes bound to the established server-side session
    // contract. In external-identity modes, a validated bearer is exchanged
    // for a token-bounded derived session; a legacy header is never honored.
    var isPortalRequest = context.Request.Path.StartsWithSegments("/api/patient-portal");
    var isPortalLogin = string.Equals(context.Request.Path.Value, "/api/patient-portal/login", StringComparison.OrdinalIgnoreCase);
    if (isPortalRequest && !isPortalLogin)
    {
        var adapter = context.RequestServices.GetRequiredService<IPatientPortalIdentityAdapter>();
        var sessionId = await adapter.ResolveSessionIdAsync(context, context.RequestAborted);
        if (sessionId is { } resolvedSessionId)
        {
            context.Request.Headers[PatientPortalIdentityAdapterHelpers.SessionHeader] = resolvedSessionId.ToString("D");
        }
        else
        {
            context.Request.Headers.Remove(PatientPortalIdentityAdapterHelpers.SessionHeader);
        }
    }

    await next(context);
});
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    var diagnostics = context.RequestServices.GetRequiredService<RuntimeDiagnostics>();
    try
    {
        await next(context);
    }
    finally
    {
        stopwatch.Stop();
        diagnostics.RecordCompletedResponse(context.Response.StatusCode);
        var endpointName = context.GetEndpoint()?.DisplayName ?? "unmatched";
        app.Logger.LogInformation(
            "HTTP {Method} endpoint {Endpoint} returned {StatusCode} in {ElapsedMilliseconds} ms with correlation {CorrelationId}",
            context.Request.Method,
            endpointName,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            context.Items["correlationId"]?.ToString() ?? context.TraceIdentifier);
    }
});

app.MapGet("/health", () => Results.Ok(new HealthResponse(
    Status: "healthy",
    Application: "avenchart-api",
    CheckedAtUtc: DateTimeOffset.UtcNow)));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
    ResponseWriter = WriteHealthCheckResponseAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponseAsync
});

var auth = app.MapGroup("/api/auth").WithTags("Authentication");
auth.MapStaffAuthenticationEndpoints();

if (app.Environment.IsDevelopment())
{
    var testIdentityProvider = app.MapGroup("/api/test-idp").WithTags("Development Test Identity Provider");
    testIdentityProvider.MapGet("/.well-known/openid-configuration", (
            IOptions<IdentityProviderOptions> options,
            HttpContext httpContext) =>
        {
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/api/test-idp";
            return Results.Ok(new
            {
                issuer = options.Value.TestIssuer,
                authorization_endpoint = $"{baseUrl}/authorize",
                token_endpoint = $"{baseUrl}/token",
                jwks_uri = $"{baseUrl}/jwks",
                response_types_supported = new[] { "code" },
                grant_types_supported = new[] { "authorization_code" },
                code_challenge_methods_supported = new[] { "S256" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
            });
        })
        .WithName("GetDevelopmentTestIdentityProviderConfiguration");
    testIdentityProvider.MapGet("/jwks", (TestIdentityProviderService provider) => Results.Ok(provider.GetJwks()))
        .WithName("GetDevelopmentTestIdentityProviderJwks");
    testIdentityProvider.MapGet("/authorize", (
            string? client_id,
            string? redirect_uri,
            string? state,
            string? code_challenge,
            string? code_challenge_method,
            string? scope,
            IOptions<IdentityProviderOptions> options,
            HttpContext httpContext) =>
        {
            if (!TryCreateDevelopmentTestOidcAuthorizationRequest(
                    client_id,
                    redirect_uri,
                    state,
                    code_challenge,
                    code_challenge_method,
                    scope,
                    options.Value,
                    httpContext,
                    out var authorizationRequest))
            {
                return Results.BadRequest(new { error = "The development test IdP authorization request is invalid." });
            }
            return Results.Content(BuildDevelopmentTestOidcAuthorizationPage(authorizationRequest), "text/html; charset=utf-8");
        })
        .WithName("AuthorizeDevelopmentTestIdentity");
    testIdentityProvider.MapPost("/authorize", async (
            HttpRequest request,
            AuthRepository repository,
            TestIdentityProviderService provider,
            IOptions<IdentityProviderOptions> options,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "The development test IdP authorization form is required." });
            }
            var form = await request.ReadFormAsync(cancellationToken);
            if (!TryCreateDevelopmentTestOidcAuthorizationRequest(
                    form["client_id"],
                    form["redirect_uri"],
                    form["state"],
                    form["code_challenge"],
                    form["code_challenge_method"],
                    form["scope"],
                    options.Value,
                    httpContext,
                    out var authorizationRequest))
            {
                return Results.BadRequest(new { error = "The development test IdP authorization request is invalid." });
            }
            var login = await repository.LoginAsync(
                new AuthLoginRequest(form["username"].ToString(), form["password"].ToString()),
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            if (!login.Authenticated)
            {
                return Results.Unauthorized();
            }
            var authorizationCode = provider.IssueAuthorizationCode(
                login.Username,
                login.DisplayName,
                authorizationRequest.ClientId,
                authorizationRequest.RedirectUri,
                authorizationRequest.CodeChallenge);
            return Results.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                authorizationRequest.RedirectUri,
                new Dictionary<string, string?>
                {
                    ["code"] = authorizationCode,
                    ["state"] = authorizationRequest.State,
                }));
        })
        .WithName("CompleteDevelopmentTestIdentityAuthorization");
    testIdentityProvider.MapPost("/token", async (
            HttpRequest request,
            AuthRepository repository,
            TestIdentityProviderService provider,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(cancellationToken);
                if (!string.Equals(form["grant_type"], "authorization_code", StringComparison.Ordinal))
                {
                    return Results.BadRequest(new { error = "unsupported_grant_type" });
                }
                var issued = provider.ExchangeAuthorizationCode(
                    form["code"].ToString(),
                    form["client_id"].ToString(),
                    form["redirect_uri"].ToString(),
                    form["code_verifier"].ToString());
                return issued is null
                    ? Results.BadRequest(new { error = "invalid_grant" })
                    : Results.Ok(new
                    {
                        access_token = issued.AccessToken,
                        token_type = issued.TokenType,
                        expires_in = issued.ExpiresIn,
                    });
            }

            var credentialRequest = await request.ReadFromJsonAsync<TestIdentityProviderTokenRequest>(cancellationToken);
            if (credentialRequest is null)
            {
                return Results.BadRequest(new { error = "The development test identity token request is required." });
            }
            var login = await repository.LoginAsync(
                new AuthLoginRequest(credentialRequest.Username, credentialRequest.Password),
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken);
            return login.Authenticated
                ? Results.Ok(provider.Issue(login.Username, login.DisplayName))
                : Results.Unauthorized();
        })
        .WithName("IssueDevelopmentTestIdentityToken");
}

var patientPortal = app.MapGroup("/api/patient-portal").WithTags("Patient Portal");

patientPortal.MapPost("/login", async (
        PatientPortalRepository repository,
        IOptions<IdentityProviderOptions> identityProviderOptions,
        PatientPortalLoginRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!identityProviderOptions.Value.IsLocal)
        {
            return Results.NotFound();
        }
        var response = await repository.LoginAsync(request, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("PatientPortalLogin");

patientPortal.MapGet("/session", async (
        PatientPortalRepository repository,
        BrowserOidcSessionService browserOidcSessions,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var response = Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetCurrentSessionAsync(sessionId, cancellationToken))
            : Results.Ok(new PatientPortalSessionResponse(
                Authenticated: false,
                SessionId: null,
                Username: string.Empty,
                PortalUsername: string.Empty,
                CanonicalId: string.Empty,
                LegacyPid: null,
                Pubpid: string.Empty,
                DisplayName: string.Empty,
                CreatedAt: null,
                LastSeenAt: null,
                ExpiresAt: null,
                EndedAt: null,
                FailureReason: "Patient portal session header was not supplied.",
                SessionSource: "avenchart-portal"));
        if (browserOidcSessions.TryGetCsrfToken(
                httpContext,
                BrowserOidcSessionService.PortalAudience,
                out var csrfToken))
        {
            httpContext.Response.Headers["X-AvenChart-CSRF"] = csrfToken;
        }
        return response;
    })
    .WithName("GetPatientPortalSession");

patientPortal.MapGet("/home", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetHomeSummaryAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderHomeSummary());
    })
    .WithName("GetPatientPortalHome");

patientPortal.MapGet("/profile", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetProfileAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderProfile());
    })
    .WithName("GetPatientPortalProfile");

patientPortal.MapPost("/profile/changes", async (
        PatientPortalProfileChangeSubmitRequest request,
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.SubmitProfileChangeAsync(sessionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderProfile());
    })
    .WithName("SubmitPatientPortalProfileChange");

patientPortal.MapGet("/appointments", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetAppointmentsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderAppointments());
    })
    .WithName("GetPatientPortalAppointments");

patientPortal.MapGet("/clinical-summary", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetClinicalSummaryAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderClinicalSummary());
    })
    .WithName("GetPatientPortalClinicalSummary");

patientPortal.MapGet("/lab-results", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetLabResultsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderLabResults());
    })
    .WithName("GetPatientPortalLabResults");

patientPortal.MapGet("/medical-report", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMedicalReportAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMedicalReport());
    })
    .WithName("GetPatientPortalMedicalReport");

patientPortal.MapPost("/medical-report/generate", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GenerateMedicalReportAsync(sessionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderGeneratedMedicalReport());
    })
    .WithName("GeneratePatientPortalMedicalReport");

patientPortal.MapPost("/medical-report/pdf", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var package = Guid.TryParse(header, out var sessionId)
            ? await repository.DownloadGeneratedMedicalReportPdfAsync(sessionId, request, cancellationToken)
            : PatientPortalRepository.MissingSessionHeaderGeneratedMedicalReportPdf();

        return package.Downloadable
            ? Results.File(package.Content, package.ContentType, package.FileName)
            : Results.BadRequest(new { package.FailureReason });
    })
    .WithName("DownloadPatientPortalMedicalReportPdf");

patientPortal.MapPost("/medical-report/package", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalMedicalReportGenerationRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var package = Guid.TryParse(header, out var sessionId)
            ? await repository.DownloadGeneratedMedicalReportPackageAsync(sessionId, request, cancellationToken)
            : PatientPortalRepository.MissingSessionHeaderGeneratedMedicalReportPackage();

        return package.Downloadable
            ? Results.File(package.Content, package.ContentType, package.FileName)
            : Results.BadRequest(new { package.FailureReason });
    })
    .WithName("DownloadPatientPortalMedicalReportPackage");

patientPortal.MapGet("/medical-report/audit", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMedicalReportAuditAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderGeneratedMedicalReportAudit());
    })
    .WithName("GetPatientPortalMedicalReportAudit");

patientPortal.MapGet("/appointments/request-options", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetAppointmentRequestOptionsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderAppointmentRequestOptions());
    })
    .WithName("GetPatientPortalAppointmentRequestOptions");

patientPortal.MapPost("/appointments/requests", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalAppointmentRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.RequestAppointmentAsync(sessionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderAppointmentRequest());
    })
    .WithName("RequestPatientPortalAppointment");

patientPortal.MapGet("/messages", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMessagesAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessages());
    })
    .WithName("GetPatientPortalMessages");

patientPortal.MapGet("/messages/recipients", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMessageRecipientsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessageRecipients());
    })
    .WithName("GetPatientPortalMessageRecipients");

patientPortal.MapGet("/messages/compose-options", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMessageComposeOptionsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessageComposeOptions());
    })
    .WithName("GetPatientPortalMessageComposeOptions");

patientPortal.MapGet("/messages/audit", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMessageAuditAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessageAudit());
    })
    .WithName("GetPatientPortalMessageAudit");

patientPortal.MapGet("/documents", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetDocumentsAsync(sessionId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderDocuments());
    })
    .WithName("GetPatientPortalDocuments");

patientPortal.MapPost("/documents/download", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalDocumentsDownloadRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var package = Guid.TryParse(header, out var sessionId)
            ? await repository.DownloadDocumentsAsync(sessionId, request, cancellationToken)
            : PatientPortalRepository.MissingSessionHeaderDocumentsDownload();

        return package.Downloadable
            ? Results.File(package.Content, package.ContentType, package.FileName)
            : Results.BadRequest(new { package.FailureReason });
    })
    .WithName("DownloadPatientPortalDocuments");

patientPortal.MapGet("/messages/{messageId:int}/thread", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        int messageId,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetMessageThreadAsync(sessionId, messageId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessageThread(messageId.ToString()));
    })
    .WithName("GetPatientPortalMessageThread");

patientPortal.MapGet("/messages/attachments/{attachmentId:guid}", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        Guid attachmentId,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var attachment = Guid.TryParse(header, out var sessionId)
            ? await repository.DownloadMessageAttachmentAsync(sessionId, attachmentId, cancellationToken)
            : new PatientPortalMessageAttachmentDownload(false, string.Empty, "application/octet-stream", Array.Empty<byte>(), "Patient portal session header was not supplied.");
        return attachment.Downloadable
            ? Results.File(attachment.Content, attachment.ContentType, attachment.FileName)
            : Results.BadRequest(new { attachment.FailureReason });
    })
    .WithName("DownloadPatientPortalMessageAttachment");

patientPortal.MapPost("/messages", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalComposeMessageRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.ComposeMessageAsync(sessionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderComposeMessage());
    })
    .WithName("ComposePatientPortalMessage");

patientPortal.MapGet("/prescription-refill-requests", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetPrescriptionRefillHistoryAsync(sessionId, cancellationToken))
            : Results.Ok(new PatientPortalPrescriptionRefillHistoryResponse(
                Authenticated: false,
                SessionId: null,
                Username: string.Empty,
                PortalUsername: string.Empty,
                CanonicalId: string.Empty,
                LegacyPid: null,
                Pubpid: string.Empty,
                DisplayName: string.Empty,
                DatasetId: "unseeded",
                DatasetVersion: "unknown",
                RequestCount: 0,
                Requests: Array.Empty<PatientPortalPrescriptionRefillHistoryItem>(),
                FailureReason: "Patient portal session header was not supplied.",
                SessionSource: "avenchart-portal"));
    })
    .WithName("GetPatientPortalPrescriptionRefillHistory");

patientPortal.MapPost("/prescriptions/{prescriptionId}/refill-request", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        string prescriptionId,
        PatientPortalPrescriptionRefillRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.RequestPrescriptionRefillAsync(sessionId, prescriptionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderComposeMessage());
    })
    .WithName("RequestPatientPortalPrescriptionRefill");

patientPortal.MapPost("/messages/{messageId:int}/reply", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        int messageId,
        PatientPortalReplyMessageRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.ReplyToMessageAsync(sessionId, messageId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderReplyMessage(messageId.ToString()));
    })
    .WithName("ReplyToPatientPortalMessage");

patientPortal.MapPost("/messages/{messageId:int}/forward", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        int messageId,
        PatientPortalForwardMessageRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.ForwardMessageAsync(sessionId, messageId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderForwardMessage(messageId.ToString()));
    })
    .WithName("ForwardPatientPortalMessage");

patientPortal.MapPut("/messages/{messageId:int}/read", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        int messageId,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.MarkMessageReadAsync(sessionId, messageId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderReadMessage(messageId.ToString()));
    })
    .WithName("ReadPatientPortalMessage");

patientPortal.MapPost("/messages/archive", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        PatientPortalArchiveMessagesRequest request,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.ArchiveMessagesAsync(sessionId, request, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderArchiveMessages());
    })
    .WithName("ArchivePatientPortalMessages");

patientPortal.MapDelete("/messages/{messageId:int}", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        int messageId,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.DeleteMessageAsync(sessionId, messageId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderDeleteMessage(messageId.ToString()));
    })
    .WithName("DeletePatientPortalMessage");

patientPortal.MapDelete("/session", async (
        PatientPortalRepository repository,
        BrowserOidcSessionService browserOidcSessions,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
        var response = Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.EndSessionAsync(sessionId, cancellationToken))
            : Results.Ok(new PatientPortalSessionResponse(
                Authenticated: false,
                SessionId: null,
                Username: string.Empty,
                PortalUsername: string.Empty,
                CanonicalId: string.Empty,
                LegacyPid: null,
                Pubpid: string.Empty,
                DisplayName: string.Empty,
                CreatedAt: null,
                LastSeenAt: null,
                ExpiresAt: null,
                EndedAt: null,
                FailureReason: "Patient portal session header was not supplied.",
                SessionSource: "avenchart-portal"));
        if (browserOidcSessions.IsBrowserSessionRequest(httpContext, BrowserOidcSessionService.PortalAudience))
        {
            browserOidcSessions.ClearBrowserSessionCookies(httpContext, BrowserOidcSessionService.PortalAudience);
        }
        return response;
    })
    .WithName("EndPatientPortalSession");

app.MapFhirR4Endpoints();

app.MapExternalLaboratoryFhirIntakeEndpoints();

var patients = app.MapGroup("/api/patients").WithTags("Patients");
RequireAccessPermission(patients, "patients", "demo", "view");
patients.AddEndpointFilter(PatientFacilityScopeFilter());

var clinicalWorkflows = app.MapGroup("/api/clinical-workflows").WithTags("Clinical Workflows");
RequireAccessPermission(clinicalWorkflows, "patients", "med", "view");
clinicalWorkflows.MapGet("/assignees", async (
        AuthorizationRepository repository,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAssigneesAsync(cancellationToken)))
    .WithName("GetClinicalWorkflowAssignees");
clinicalWorkflows.MapGet("/referral-work-queue", async (
        ReferralRepository repository,
        HttpContext httpContext,
        string? status,
        string? assignedTo,
        bool? overdueOnly,
        string? query,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetWorkQueueAsync(
                status,
                assignedTo,
                overdueOnly ?? false,
                query,
                limit ?? 25,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetReferralWorkQueue");

clinicalWorkflows.MapGet("/authorization-work-queue", async (
        AuthorizationRepository repository,
        HttpContext httpContext,
        string? status,
        string? assignedTo,
        bool? overdueOnly,
        bool? expiringOnly,
        string? query,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetWorkQueueAsync(
                status,
                assignedTo,
                overdueOnly ?? false,
                expiringOnly ?? false,
                query,
                limit ?? 25,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetAuthorizationWorkQueue");

patients.MapGet("/", async (
        PatientRepository repository,
        HttpContext httpContext,
        string? search,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(
            search,
            limit ?? 25,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return Results.Ok(response);
    })
    .WithName("SearchPatients");

patients.MapGet("/{patientId}/track-history", async (string patientId, TrackAnythingRepository repository, CancellationToken cancellationToken) =>
    (await repository.GetPatientHistoryAsync(patientId, cancellationToken)) is { } history ? Results.Ok(history) : Results.NotFound())
    .WithName("GetPatientTrackAnythingHistory");

patients.MapGet("/{patientId}/referrals", async (string patientId, ReferralRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetAsync(patientId, cancellationToken)); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientReferrals");
patients.MapPost("/{patientId}/referrals", async (string patientId, ReferralCreateRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        return Results.Created($"/api/patients/{patientId}/referrals", await repository.CreateAsync(patientId, request, session.Username, cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreatePatientReferral").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
patients.MapPut("/{patientId}/referrals/{referralId:guid}/status", async (string patientId, Guid referralId, ReferralStatusRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        return Results.Ok(await repository.UpdateStatusAsync(patientId, referralId, request, session.Username, cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ClinicalWorkflowVersionConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message, expectedVersion = ex.ExpectedVersion, currentVersion = ex.CurrentVersion, current = await repository.GetByIdAsync(patientId, referralId, cancellationToken) });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientReferralStatus").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

patients.MapPut("/{patientId}/referrals/{referralId:guid}/assignment", async (string patientId, Guid referralId, ReferralAssignmentRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        return Results.Ok(await repository.UpdateAssignmentAsync(patientId, referralId, request, session.Username, cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ClinicalWorkflowVersionConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message, expectedVersion = ex.ExpectedVersion, currentVersion = ex.CurrentVersion, current = await repository.GetByIdAsync(patientId, referralId, cancellationToken) });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientReferralAssignment").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

patients.MapGet("/{patientId}/referrals/{referralId:guid}/history", async (string patientId, Guid referralId, ReferralRepository repository, CancellationToken cancellationToken) =>
{
    try { return await repository.GetHistoryAsync(patientId, referralId, cancellationToken) is { } history ? Results.Ok(history) : Results.NotFound(); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientReferralHistory");

patients.MapGet("/{patientId}/authorizations", async (string patientId, AuthorizationRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetAsync(patientId, cancellationToken)); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientAuthorizations");
patients.MapPost("/{patientId}/authorizations", async (
    string patientId,
    AuthorizationCreateRequest request,
    AuthorizationRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Created(
            $"/api/patients/{patientId}/authorizations",
            await repository.CreateAsync(
                patientId,
                request,
                session.Username,
                cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreatePatientAuthorization").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
patients.MapPut("/{patientId}/authorizations/{authorizationId:guid}/status", async (
    string patientId,
    Guid authorizationId,
    AuthorizationStatusRequest request,
    AuthorizationRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Ok(await repository.UpdateStatusAsync(
            patientId,
            authorizationId,
            request,
            session.Username,
            cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ClinicalWorkflowVersionConflictException ex)
    {
        return Results.Conflict(new
        {
            error = ex.Message,
            expectedVersion = ex.ExpectedVersion,
            currentVersion = ex.CurrentVersion,
            current = await repository.GetByIdAsync(
                patientId,
                authorizationId,
                cancellationToken),
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientAuthorizationStatus").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
patients.MapPut("/{patientId}/authorizations/{authorizationId:guid}/assignment", async (
    string patientId,
    Guid authorizationId,
    AuthorizationAssignmentRequest request,
    AuthorizationRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Ok(await repository.UpdateAssignmentAsync(
            patientId,
            authorizationId,
            request,
            session.Username,
            cancellationToken));
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ClinicalWorkflowVersionConflictException ex)
    {
        return Results.Conflict(new
        {
            error = ex.Message,
            expectedVersion = ex.ExpectedVersion,
            currentVersion = ex.CurrentVersion,
            current = await repository.GetByIdAsync(
                patientId,
                authorizationId,
                cancellationToken),
        });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientAuthorizationAssignment").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
patients.MapGet("/{patientId}/authorizations/{authorizationId:guid}/history", async (
    string patientId,
    Guid authorizationId,
    AuthorizationRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.GetHistoryAsync(
            patientId,
            authorizationId,
            cancellationToken) is { } history
            ? Results.Ok(history)
            : Results.NotFound();
    }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientAuthorizationHistory");
patients.MapDelete("/{patientId}/authorizations/{authorizationId:guid}/test-fixture", async (
    string patientId,
    Guid authorizationId,
    AuthorizationRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.DeleteFixtureAsync(
            patientId,
            authorizationId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }
    catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("DeletePatientAuthorizationTestFixture").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

patients.MapGet("/{patientId}/record-requests", async (string patientId, PatientRecordRequestRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetAsync(patientId, cancellationToken)); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientRecordRequests").AddEndpointFilter(AccessPermissionFilter("patients", "med", "view"));

patients.MapPost("/{patientId}/record-requests", async (string patientId, PatientRecordRequestRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var request = await repository.CreateAsync(patientId, session.Username, cancellationToken);
        return Results.Created($"/api/patients/{patientId}/record-requests/{request.RequestId}", request);
    }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreatePatientRecordRequest").AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

patients.MapPost("/{patientId}/record-requests/{requestId:guid}/complete", async (string patientId, Guid requestId, PatientRecordRequestRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        return Results.Ok(await repository.CompleteAsync(patientId, requestId, session.Username, cancellationToken));
    }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CompletePatientRecordRequest").AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

patients.MapGet("/{patientId}/disclosure-policy", () =>
    Results.Ok(PatientDisclosurePolicyCatalog.Build()))
    .WithName("GetPatientDisclosurePolicy")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

patients.MapGet("/{patientId}/disclosure-authorities", async (
    string patientId,
    PatientDisclosureRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetAuthoritiesAsync(
            patientId,
            cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).WithName("GetPatientDisclosureAuthorities")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

patients.MapPost("/{patientId}/disclosure-authorities", async (
    string patientId,
    PatientDisclosureAuthorityCreateRequest request,
    PatientDisclosureRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var authority = await repository.CreateAuthorityAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return Results.Created(
            $"/api/patients/{patientId}/disclosure-authorities/{authority.AuthorityId}",
            authority);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("CreatePatientDisclosureAuthority")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

patients.MapPost("/{patientId}/disclosure-authorities/{authorityId:guid}/{action}", async (
    string patientId,
    Guid authorityId,
    string action,
    PatientDisclosureAuthorityTransitionRequest request,
    PatientDisclosureRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Ok(await repository.TransitionAuthorityAsync(
            patientId,
            authorityId,
            action,
            request,
            session.Username,
            cancellationToken));
    }
    catch (PatientDisclosureConcurrencyException ex)
    {
        return Results.Conflict(new
        {
            error = ex.Message,
            expectedVersion = ex.ExpectedVersion,
            currentVersion = ex.CurrentVersion,
        });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("TransitionPatientDisclosureAuthority")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

patients.MapGet("/{patientId}/disclosure-authorities/{authorityId:guid}/history", async (
    string patientId,
    Guid authorityId,
    PatientDisclosureRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetAuthorityHistoryAsync(
            patientId,
            authorityId,
            cancellationToken));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).WithName("GetPatientDisclosureAuthorityHistory")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

patients.MapGet("/{patientId}/disclosure-requests", async (
    string patientId,
    PatientDisclosureRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetRequestsAsync(
            patientId,
            cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).WithName("GetPatientDisclosureRequests")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

patients.MapPost("/{patientId}/disclosure-requests", async (
    string patientId,
    PatientDisclosureRequestCreateRequest request,
    PatientDisclosureRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var disclosure = await repository.CreateRequestAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return Results.Created(
            $"/api/patients/{patientId}/disclosure-requests/{disclosure.RequestId}",
            disclosure);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("CreatePatientDisclosureRequest")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

patients.MapPost("/{patientId}/disclosure-requests/{requestId:guid}/decision", async (
    string patientId,
    Guid requestId,
    PatientDisclosureDecisionRequest request,
    PatientDisclosureRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Ok(await repository.DecideRequestAsync(
            patientId,
            requestId,
            request,
            session.Username,
            cancellationToken));
    }
    catch (PatientDisclosureConcurrencyException ex)
    {
        return Results.Conflict(new
        {
            error = ex.Message,
            expectedVersion = ex.ExpectedVersion,
            currentVersion = ex.CurrentVersion,
        });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).WithName("DecidePatientDisclosureRequest")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

patients.MapGet("/{patientId}/disclosure-requests/{requestId:guid}/history", async (
    string patientId,
    Guid requestId,
    PatientDisclosureRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetRequestHistoryAsync(
            patientId,
            requestId,
            cancellationToken));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).WithName("GetPatientDisclosureRequestHistory")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

patients.MapDelete("/{patientId}/disclosure-authorities/{authorityId:guid}/test-fixture", async (
    string patientId,
    Guid authorityId,
    PatientDisclosureRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.DeleteFixtureAsync(
            patientId,
            authorityId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }
    catch (ArgumentException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}).WithName("DeletePatientDisclosureTestFixture")
  .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

patients.MapGet("/{patientId}/sdoh-assessments", async (string patientId, PatientSdohRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetAsync(patientId, cancellationToken)); }
    catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
}).WithName("GetPatientSdohAssessments").AddEndpointFilter(AccessPermissionFilter("patients", "med", "view"));

patients.MapPost("/{patientId}/sdoh-assessments", async (string patientId, PatientSdohAssessmentRequest request, PatientSdohRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var assessment = await repository.CreateAsync(patientId, request, session.Username, cancellationToken);
        return Results.Created($"/api/patients/{patientId}/sdoh-assessments/{assessment.AssessmentId}", assessment);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreatePatientSdohAssessment").AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

patients.MapPut("/{patientId}/sdoh-assessments/{assessmentId:guid}", async (string patientId, Guid assessmentId, PatientSdohAssessmentRequest request, PatientSdohRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        return Results.Ok(await repository.UpdateAsync(patientId, assessmentId, request, session.Username, cancellationToken));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientSdohAssessment").AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

patients.MapGet("/duplicates", async (
        PatientRepository repository,
        string? firstName,
        string? lastName,
        string? dateOfBirth,
        string? phone,
        string? email,
        string? excludePatientId,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.FindDuplicateCandidatesAsync(
            firstName,
            lastName,
            dateOfBirth,
            phone,
            email,
            excludePatientId,
            limit,
            cancellationToken);
        return Results.Ok(response);
    })
    .WithName("FindPatientDuplicateCandidates");

patients.MapGet("/duplicates/review-queue", async (PatientRepository repository, int? limit, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetDuplicateReviewQueueAsync(limit ?? 50, cancellationToken))).WithName("GetPatientDuplicateReviewQueue").AddEndpointFilter(AccessPermissionFilter("admin", "super", "view"));
patients.MapPut("/duplicates/review-disposition", async (PatientRepository repository, PatientDuplicateReviewDispositionRequest request, CancellationToken cancellationToken) =>
{
    try { var item = await repository.SetDuplicateReviewDispositionAsync(request, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); }
    catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["duplicateReview"] = [ex.Message] }); }
}).WithName("SetPatientDuplicateReviewDisposition").AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

patients.MapGet("/merge-preview", async (
        PatientRepository repository,
        string targetPatientId,
        string sourcePatientId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var response = await repository.GetMergePreviewAsync(targetPatientId, sourcePatientId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("GetPatientMergePreview")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPost("/merge-audits", async (
        PatientRepository patientRepository,
        PatientMergeAuditRepository auditRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        PatientMergeAuditPlanRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var preview = await patientRepository.GetMergePreviewAsync(
                request.TargetPatientId,
                request.SourcePatientId,
                cancellationToken);
            if (preview is null)
            {
                return Results.NotFound();
            }

            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var audit = await auditRepository.RecordPreviewAsync(request, preview, session.Username, cancellationToken);
            return Results.Created($"/api/patients/merge-audits/{audit.AuditId}", audit);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("CreatePatientMergeAuditPlan")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPost("/merge-executions", async (
        PatientMergeExecutionRepository mergeRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        PatientMergeExecutionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var execution = await mergeRepository.ExecuteAsync(request.AuditId, session.Username, cancellationToken);
            return Results.Created($"/api/patients/merge-executions/{execution.ExecutionId}", execution);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("ExecutePatientMerge")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPost("/merge-executions/rollback", async (
        PatientMergeExecutionRepository mergeRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        PatientMergeRollbackRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var rollback = await mergeRepository.RollbackAsync(request.ExecutionId, session.Username, cancellationToken);
            return Results.Ok(rollback);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    })
    .WithName("RollbackPatientMerge")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapGet("/provider-options", async (
        PatientRepository repository,
        CancellationToken cancellationToken) =>
    {
        var options = await repository.GetProviderAssignmentOptionsAsync(cancellationToken);
        return Results.Ok(options);
    })
    .WithName("GetPatientProviderAssignmentOptions")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));

patients.MapGet("/{patientId}/provider-assignment-history", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetProviderAssignmentHistoryAsync(patientId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientProviderAssignmentHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));

patients.MapGet("/{patientId}/administration-history", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetAdministrationHistoryAsync(patientId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientAdministrationHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));

patients.MapPost("/", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        PatientRegistrationRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var result = await repository.CreatePatientAsync(
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return result.Patient is null
            ? RegistrationValidationProblem(result.ValidationIssues)
            : Results.Created($"/api/patients/{result.Patient.CanonicalId}", result.Patient);
    })
    .WithName("RegisterPatient")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "addonly"));

patients.MapGet("/{canonicalId}", async (
        PatientRepository repository,
        HttpContext httpContext,
        string canonicalId,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.GetChartSummaryAsync(canonicalId, cancellationToken);
        if (patient is not null)
        {
            return Results.Ok(patient);
        }

        var mergedIntoPatientId = await repository.GetMergedIntoPatientIdAsync(
            canonicalId,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return string.IsNullOrWhiteSpace(mergedIntoPatientId)
            ? Results.NotFound()
            : Results.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Patient chart has been merged",
                detail: "This chart is no longer independently available. Continue with the surviving patient chart.",
                extensions: new Dictionary<string, object?>
                {
                    ["targetPatientId"] = mergedIntoPatientId
                });
    })
    .WithName("GetPatientChartSummary");
patients.MapGet("/{patientId}/xml-export", async (string patientId, PatientXmlExchangeRepository repository, CancellationToken ct) => { var xml = await repository.ExportAsync(patientId, ct); return xml is null ? Results.NotFound() : Results.File(Encoding.UTF8.GetBytes(xml), "application/xml", $"avenchart-patient-{patientId}.xml"); }).WithName("ExportPatientXml");
patients.MapPost("/xml-import/preview", async (PatientXmlExchangeRepository repository, PatientXmlImportRequest request, CancellationToken ct) => { try { var preview = await repository.PreviewAsync(request, ct); return preview is null ? Results.NotFound() : Results.Ok(preview); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { { "xml", [e.Message] } }); } }).WithName("PreviewPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
patients.MapPost("/xml-import", async (PatientXmlExchangeRepository repository, AuthRepository auth, HttpContext context, PatientXmlImportRequest request, CancellationToken ct) => { try { var session = await GetSessionFromHeaderAsync(auth, context, ct); var result = await repository.ImportAsync(request, session.Username, ct); return result is null ? Results.NotFound() : Results.Ok(result); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { { "xml", [e.Message] } }); } }).WithName("ImportPatientXml").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
patients.MapPost("/xml-import/{auditId:guid}/rollback", async (PatientXmlExchangeRepository repository, AuthRepository auth, HttpContext context, Guid auditId, CancellationToken ct) => { var session = await GetSessionFromHeaderAsync(auth, context, ct); return await repository.RollbackAsync(auditId, session.Username, ct) ? Results.NoContent() : Results.NotFound(); }).WithName("RollbackPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapGet("/{patientId}/print/{output}", async (string patientId, string output, Guid? referralId, int? encounterId, int? labelCount, PatientPrintRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var html = await repository.RenderAsync(patientId, output, referralId, encounterId, labelCount, cancellationToken);
        return html is null ? Results.NotFound() : Results.Content(html, "text/html; charset=utf-8");
    }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["print"] = [exception.Message] }); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
}).WithName("GetPatientPrintableOutput").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "view"));

patients.MapPut("/{patientId}/administration", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientAdministrationUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var patient = await repository.UpdateAdministrationAsync(
                patientId,
                request,
                session.Username,
                cancellationToken);
            return patient is null ? Results.NotFound() : Results.Ok(patient);
        }
        catch (PatientAdministrationVersionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                expectedVersion = exception.ExpectedVersion,
                currentVersion = exception.CurrentVersion,
                current = await repository.GetChartSummaryAsync(patientId, cancellationToken)
            });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["administration"] = [exception.Message]
            });
        }
    })
    .WithName("UpdatePatientAdministration")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/contact", (string patientId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Patient contact update is retired",
            detail: "Use the atomic patient administration update endpoint with the current administration version."))
    .WithName("RetirePatientContactUpdate")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/demographics", (string patientId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Patient demographics update is retired",
            detail: "Use the atomic patient administration update endpoint with the current administration version."))
    .WithName("RetirePatientDemographicsUpdate")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/deceased-status", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientDeceasedStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var patient = await repository.UpdateDeceasedStatusAsync(
                patientId, request, session.Username, cancellationToken);
            return patient is null ? Results.NotFound() : Results.Ok(patient);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdatePatientDeceasedStatus")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapGet("/{patientId}/deceased-status-history", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetDeceasedStatusHistoryAsync(patientId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDeceasedStatusHistory");

patients.MapGet("/{patientId}/lifecycle-history", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetLifecycleHistoryAsync(patientId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientLifecycleHistory");

patients.MapPost("/{patientId}/lifecycle/{action}", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        string action,
        PatientLifecycleTransitionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var patient = await repository.TransitionLifecycleAsync(
                patientId,
                action,
                request,
                session.Username,
                cancellationToken);
            return patient is null ? Results.NotFound() : Results.Ok(patient);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("TransitionPatientLifecycle")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/portal-account/reset", async (
        PatientRepository repository,
        string patientId,
        PatientPortalAccountResetRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdatePortalAccountResetAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient portal account reset state could not be updated from the supplied patient and reset details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientPortalAccountReset")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/portal-account/access", async (
        PatientRepository repository,
        string patientId,
        PatientPortalAccountAccessRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdatePortalAccountAccessAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient portal account access could not be updated from the supplied patient and access details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientPortalAccountAccess")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/guardian-contact", async (
        PatientRepository repository,
        string patientId,
        PatientGuardianContactUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateGuardianContactAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient guardian contact could not be updated from the supplied patient and guardian details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientGuardianContact")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/employer", async (
        PatientRepository repository,
        string patientId,
        PatientEmployerUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateEmployerAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient employer could not be updated from the supplied patient and employer details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientEmployer")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/provider-assignment", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientProviderAssignmentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.UpdateProviderAssignmentAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient provider assignment could not be updated from the supplied patient and provider details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientProviderAssignment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/care-team", async (
        PatientRepository repository,
        string patientId,
        PatientCareTeamUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateCareTeamAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient care team could not be updated from the supplied patient and care-team details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientCareTeam")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapGet("/{patientId}/care-team-options", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var options = await repository.GetCareTeamOptionsAsync(patientId, cancellationToken);
        return options is null ? Results.NotFound() : Results.Ok(options);
    })
    .WithName("GetPatientCareTeamOptions");

patients.MapDelete("/{patientId}", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteTemporaryPatientAsync(patientId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteTemporaryPatient")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPost("/{patientId}/insurance", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.CreateInsuranceAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return patient is null
            ? Results.BadRequest("Insurance coverage could not be created from the supplied patient and coverage details.")
            : Results.Created($"/api/patients/{patient.CanonicalId}", patient);
    })
    .WithName("CreatePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/insurance/{insuranceId}", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string insuranceId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.UpdateInsuranceAsync(
            insuranceId,
            request,
            session.Username,
            cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("UpdatePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapDelete("/insurance/{insuranceId}", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string insuranceId,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.DeleteInsuranceAsync(
            insuranceId,
            session.Username,
            cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("DeletePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

app.MapAppointmentEndpoints();
var encounters = app.MapGroup("/api/encounters").WithTags("Encounters");
RequireAccessPermission(encounters, "encounters", "auth_a", "view");
encounters.AddEndpointFilter(ClinicalResourceFacilityScopeFilter());

encounters.MapGet("/", async (
        EncounterRepository repository,
        HttpContext httpContext,
        string? patientId,
        string? from,
        int? limit,
        bool? archived,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(
            patientId,
            from,
            limit ?? 25,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken,
            archived == true);
        return Results.Ok(response);
    })
    .WithName("SearchEncounters");

encounters.MapPut("/{encounter:int}/archive", async (EncounterStateRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, EncounterArchiveRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return await repository.ArchiveAsync(encounter, request, session.Username, cancellationToken) ? Results.NoContent() : Results.Conflict(new { error = "The encounter is missing, already archived, or has changed. Reload and try again." }); }
    catch (EncounterLockConflictException exception) { return Results.Conflict(new { error = exception.Message, code = "encounter_locked" }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("ArchiveEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/restore", async (EncounterStateRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, EncounterArchiveRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return await repository.RestoreAsync(encounter, request, session.Username, cancellationToken) ? Results.NoContent() : Results.Conflict(new { error = "The encounter is missing, already restored, or has changed. Reload and try again." }); }
    catch (EncounterLockConflictException exception) { return Results.Conflict(new { error = exception.Message, code = "encounter_locked" }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("RestoreEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/soap-note-templates", async (
        EncounterRepository repository,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.GetSoapNoteTemplateCatalogAsync(cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetEncounterSoapNoteTemplates")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/forms/{layoutKey}", async (EncounterLayoutFormRepository repository, int encounter, string layoutKey, CancellationToken cancellationToken) =>
    (await repository.GetAsync(encounter, layoutKey, cancellationToken)) is { } form ? Results.Ok(form) : Results.NotFound())
    .WithName("GetEncounterLayoutForm");

encounters.MapGet("/{encounter:int}/forms", async (EncounterLayoutFormRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetAvailableAsync(encounter, cancellationToken)) is { } forms ? Results.Ok(forms) : Results.NotFound())
    .WithName("GetEncounterLayoutFormCatalog");

encounters.MapGet("/{encounter:int}/alerts", async (ClinicalAlertEvaluationRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetEncounterAlertsAsync(encounter, cancellationToken)) is { } alerts ? Results.Ok(alerts) : Results.NotFound())
    .WithName("GetEncounterClinicalAlerts");

encounters.MapGet("/{encounter:int}/alerts/history", async (ClinicalAlertEvaluationRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetHistoryAsync(encounter, cancellationToken)) is { } history ? Results.Ok(history) : Results.NotFound())
    .WithName("GetEncounterClinicalAlertHistory");

encounters.MapPost("/{encounter:int}/alerts/{ruleKey}/acknowledge", async (ClinicalAlertEvaluationRepository repository, EncounterRepository encounterRepository, AuthRepository authRepository, HttpContext httpContext, int encounter, string ruleKey, CancellationToken cancellationToken) =>
{
    try { if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)) return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" }); var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.AcknowledgeAsync(encounter, ruleKey, session.Username, cancellationToken)) is { } alerts ? Results.Ok(alerts) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("AcknowledgeEncounterClinicalAlert")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPost("/{encounter:int}/alerts/{ruleKey}/reopen", async (ClinicalAlertEvaluationRepository repository, EncounterRepository encounterRepository, AuthRepository authRepository, HttpContext httpContext, int encounter, string ruleKey, CancellationToken cancellationToken) =>
{
    try { if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)) return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" }); var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.ReopenAsync(encounter, ruleKey, session.Username, cancellationToken)) is { } alerts ? Results.Ok(alerts) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("ReopenEncounterClinicalAlert")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/forms/{layoutKey}", async (EncounterLayoutFormRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, string layoutKey, EncounterLayoutFormSaveRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.SaveAsync(encounter, layoutKey, request, session.Username, cancellationToken)) is { } form ? Results.Ok(form) : Results.NotFound(); }
    catch (EncounterLockConflictException exception) { return Results.Conflict(new { error = exception.Message, code = "encounter_locked" }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("SaveEncounterLayoutForm")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/tracks", async (TrackAnythingRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetEncounterCatalogAsync(encounter, cancellationToken)) is { } tracks ? Results.Ok(tracks) : Results.NotFound())
    .WithName("GetEncounterTracks");

encounters.MapPost("/{encounter:int}/tracks", async (TrackAnythingRepository repository, EncounterRepository encounterRepository, AuthRepository authRepository, HttpContext httpContext, int encounter, TrackAnythingEncounterRecordCreateRequest request, CancellationToken cancellationToken) =>
{
    try { if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)) return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" }); var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.CreateEncounterRecordAsync(encounter, request, session.Username, cancellationToken)) is { } record ? Results.Created($"/api/encounters/{encounter}/tracks/{record.RecordId}", record) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("CreateEncounterTrack")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/tracks/{recordId:guid}", async (TrackAnythingRepository repository, int encounter, Guid recordId, CancellationToken cancellationToken) =>
    (await repository.GetEncounterRecordAsync(encounter, recordId, cancellationToken)) is { } record ? Results.Ok(record) : Results.NotFound())
    .WithName("GetEncounterTrack");

encounters.MapPost("/{encounter:int}/tracks/{recordId:guid}/readings", async (TrackAnythingRepository repository, EncounterRepository encounterRepository, AuthRepository authRepository, HttpContext httpContext, int encounter, Guid recordId, TrackAnythingReadingCreateRequest request, CancellationToken cancellationToken) =>
{
    try { if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)) return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" }); var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.AddReadingAsync(encounter, recordId, request, session.Username, cancellationToken)) is { } reading ? Results.Created($"/api/encounters/{encounter}/tracks/{recordId}/readings/{reading.ReadingId}", reading) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("AddEncounterTrackReading")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/tracks/{recordId:guid}/readings/{readingId:guid}", async (TrackAnythingRepository repository, EncounterRepository encounterRepository, AuthRepository authRepository, HttpContext httpContext, int encounter, Guid recordId, Guid readingId, TrackAnythingReadingUpdateRequest request, CancellationToken cancellationToken) =>
{
    try { if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)) return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" }); var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.UpdateReadingAsync(encounter, recordId, readingId, request, session.Username, cancellationToken)) is { } reading ? Results.Ok(reading) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("UpdateEncounterTrackReading")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}", async (
        EncounterRepository repository,
        int encounter,
        bool? includeArchivedDocuments,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await repository.GetByEncounterAsync(
            encounter,
            cancellationToken,
            includeArchivedDocuments == true);
        return encounterDetail is null ? Results.NotFound() : Results.Ok(encounterDetail);
    })
    .WithName("GetEncounterDetail");

encounters.MapPost("/", async (
        EncounterRepository repository,
        EncounterCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await repository.CreateAsync(request, cancellationToken);
        return encounterDetail is null
            ? Results.BadRequest("Encounter could not be created from the supplied patient and visit details.")
            : Results.Created($"/api/encounters/{encounterDetail.Encounter}", encounterDetail);
    })
    .WithName("CreateEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}", async (
        EncounterStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        EncounterUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var encounterDetail = await repository.UpdateSummaryAsync(encounter, request, session.Username, cancellationToken);
            return encounterDetail is null ? Results.NotFound() : Results.Ok(encounterDetail);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (EncounterStateConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                code = "encounter_changed",
                exception.ExpectedVersion,
                exception.CurrentVersion
            });
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.BadRequest(new { error = exception.Message, code = "invalid_encounter_version" });
        }
    })
    .WithName("UpdateEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/audit", async (EncounterRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetAuditHistoryAsync(encounter, cancellationToken)) is { } history ? Results.Ok(history) : Results.NotFound())
    .WithName("GetEncounterAuditHistory");

encounters.MapPost("/{encounter:int}/vitals", async (
        EncounterStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        EncounterVitalsCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var response = await repository.CreateVitalsAsync(encounter, request, session.Username, cancellationToken);
            return response is null
                ? Results.BadRequest("Vitals could not be recorded for the supplied encounter.")
                : Results.Created($"/api/encounters/{encounter}/vitals/{response.Id}", response);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vitals"] = [exception.Message]
            });
        }
    })
    .WithName("CreateEncounterVitals")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPost("/{encounter:int}/soap-notes", async (
        EncounterRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        EncounterSoapNoteCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var response = await repository.CreateSoapNoteAsync(
                encounter,
                request,
                session.Username,
                cancellationToken);
            return response is null
                ? Results.BadRequest("SOAP note could not be recorded for the supplied encounter.")
                : Results.Created($"/api/encounters/{encounter}/soap-notes/{response.Id}", response);
        }
        catch (EncounterSoapNoteConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                code = exception.IsLocked ? "encounter_locked" : "soap_note_version_conflict",
                currentVersion = exception.CurrentVersion,
                isLocked = exception.IsLocked
            });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["soapNote"] = [exception.Message]
            });
        }
    })
    .WithName("CreateEncounterSoapNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

app.MapPut("/api/encounters/{encounter:int}/sign", async (
        EncounterRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        EncounterSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var response = await repository.SignAsync(encounter, request, session.Username, cancellationToken);
        return response is null
            ? Results.BadRequest("Encounter could not be signed for the authenticated session.")
            : Results.Ok(response);
    })
    .WithName("SignEncounter")
    .AddEndpointFilter(EncounterSigningPermissionFilter())
    .AddEndpointFilter(ClinicalResourceFacilityScopeFilter());

encounters.MapPost("/{encounter:int}/documents", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        int encounter,
        EncounterDocumentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        var mutation = await documentRepository.CreateAsync(
            new PatientDocumentCreateRequest(
                PatientId: encounterDetail.PatientId,
                CategoryId: request.CategoryId,
                Name: request.Name,
                DocDate: request.DocDate,
                Encounter: encounterDetail.Encounter,
                Content: request.Content,
                Notes: request.Notes),
            cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter document could not be attached from the supplied document details.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Created($"/api/documents/{mutation.Id}", new EncounterDocumentMutationResponse(mutation.Id, refreshed));
    })
    .WithName("CreateEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

encounters.MapPost("/{encounter:int}/documents/binary", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        int encounter,
        EncounterBinaryDocumentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        var mutation = await documentRepository.CreateBinaryAsync(
            new PatientDocumentBinaryCreateRequest(
                PatientId: encounterDetail.PatientId,
                CategoryId: request.CategoryId,
                Name: request.Name,
                DocDate: request.DocDate,
                Encounter: encounterDetail.Encounter,
                FileName: request.FileName,
                Mimetype: request.Mimetype,
                ContentBase64: request.ContentBase64,
                Notes: request.Notes),
            cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Binary encounter document could not be attached from the supplied file details.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Created($"/api/documents/{mutation.Id}", new EncounterDocumentMutationResponse(mutation.Id, refreshed));
    })
    .WithName("CreateBinaryEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

encounters.MapPost("/{encounter:int}/documents/external-link", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        int encounter,
        EncounterExternalLinkDocumentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        var mutation = await documentRepository.CreateExternalLinkAsync(
            new PatientDocumentExternalLinkCreateRequest(
                PatientId: encounterDetail.PatientId,
                CategoryId: request.CategoryId,
                Name: request.Name,
                DocDate: request.DocDate,
                Encounter: encounterDetail.Encounter,
                Url: request.Url,
                Notes: request.Notes),
            cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("External-link encounter document could not be attached from the supplied URL and document details.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Created($"/api/documents/{mutation.Id}", new EncounterDocumentMutationResponse(mutation.Id, refreshed));
    })
    .WithName("CreateExternalLinkEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/metadata", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        if (request.Encounter.HasValue && request.Encounter.Value != encounter)
        {
            return Results.BadRequest("Encounter document metadata must remain attached to the selected encounter.");
        }

        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await documentRepository.UpdateMetadataAsync(documentId, request with
        {
            Encounter = encounter
        }, session.Username, cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter document metadata could not be updated from the supplied filing details.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
    })
    .WithName("UpdateEncounterDocumentMetadata")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/move", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        EncounterDocumentMoveRequest request,
        CancellationToken cancellationToken) =>
    {
        var sourceDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (sourceDetail is null)
        {
            return Results.NotFound();
        }

        var document = sourceDetail.Documents.FirstOrDefault(document => document.Id == documentId);
        if (document is null)
        {
            return Results.NotFound();
        }

        var targetDetail = await encounterRepository.GetByEncounterAsync(request.TargetEncounter, cancellationToken);
        if (targetDetail is null)
        {
            return Results.BadRequest("Target encounter was not found.");
        }

        if (targetDetail.LegacyPid != sourceDetail.LegacyPid)
        {
            return Results.BadRequest("Encounter document can only be moved to another encounter for the same patient.");
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken)
            || await encounterRepository.HasLockingSignatureAsync(targetDetail.Encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "A source or target encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await documentRepository.UpdateMetadataAsync(documentId, new PatientDocumentMetadataUpdateRequest(
            CategoryId: document.CategoryId,
            Name: document.Name,
            DocDate: document.DocDate,
            Encounter: targetDetail.Encounter,
            Notes: document.Notes,
            Reason: request.Reason), session.Username, cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter document could not be moved to the supplied target encounter.");
        }

        var refreshedSource = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        var refreshedTarget = await encounterRepository.GetByEncounterAsync(targetDetail.Encounter, cancellationToken);
        return refreshedSource is null || refreshedTarget is null
            ? Results.NotFound()
            : Results.Ok(new EncounterDocumentMoveResponse(documentId, refreshedSource, refreshedTarget));
    })
    .WithName("MoveEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/content", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await documentRepository.ReplaceContentAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            if (mutation is null)
            {
                return Results.BadRequest("Encounter document content could not be replaced from the supplied text payload or did not materially change.");
            }

            var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
            return refreshed is null
                ? Results.NotFound()
                : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
        }
        catch (DocumentVersionConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                currentVersion = conflict.CurrentVersion
            });
        }
    })
    .WithName("ReplaceEncounterDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/content/binary", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentBinaryContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await documentRepository.ReplaceBinaryContentAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            if (mutation is null)
            {
                return Results.BadRequest("Encounter binary document content could not be replaced from the supplied file payload or did not materially change.");
            }

            var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
            return refreshed is null
                ? Results.NotFound()
                : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
        }
        catch (DocumentVersionConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                currentVersion = conflict.CurrentVersion
            });
        }
    })
    .WithName("ReplaceEncounterDocumentBinaryContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/soft-delete", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentArchiveRequest? request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await documentRepository.SoftDeleteAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            if (mutation is null)
            {
                return Results.BadRequest("Encounter document could not be archived.");
            }

            var refreshed = await encounterRepository.GetByEncounterAsync(
                encounter,
                cancellationToken,
                includeArchivedDocuments: true);
            return refreshed is null
                ? Results.NotFound()
                : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentArchiveConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentArchived = conflict.CurrentArchived
            });
        }
    })
    .WithName("SoftDeleteEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/restore", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentArchiveRequest? request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(
            encounter,
            cancellationToken,
            includeArchivedDocuments: true);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await documentRepository.RestoreAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            if (mutation is null)
            {
                return Results.BadRequest("Encounter document could not be restored.");
            }

            var refreshed = await encounterRepository.GetByEncounterAsync(
                encounter,
                cancellationToken,
                includeArchivedDocuments: true);
            return refreshed is null
                ? Results.NotFound()
                : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentArchiveConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentArchived = conflict.CurrentArchived
            });
        }
    })
    .WithName("RestoreEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/sign", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        int documentId,
        PatientDocumentSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        if (encounterDetail is null)
        {
            return Results.NotFound();
        }

        if (await encounterRepository.HasLockingSignatureAsync(encounter, cancellationToken))
        {
            return Results.Conflict(new { error = "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.", code = "encounter_locked" });
        }

        if (!encounterDetail.Documents.Any(document => document.Id == documentId))
        {
            return Results.NotFound();
        }

        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await documentRepository.SignAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            if (mutation is null)
            {
                return Results.BadRequest("Encounter document review state could not be changed.");
            }

            var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
            return refreshed is null
                ? Results.NotFound()
                : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentReviewConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("SignEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

var clinicalLists = app.MapGroup("/api/clinical-lists").WithTags("Clinical Lists");
RequireAccessPermission(clinicalLists, "patients", "med", "view");
clinicalLists.AddEndpointFilter(ClinicalListFacilityScopeFilter());

clinicalLists.MapGet("/medication-vocabulary", async (
        ClinicalListRepository repository,
        string? query,
        CancellationToken cancellationToken) =>
    {
        var items = await repository.SearchMedicationVocabularyAsync(query, cancellationToken);
        return Results.Ok(items);
    })
    .WithName("SearchClinicalMedicationVocabulary");

clinicalLists.MapGet("/pharmacies", async (
        ClinicalListRepository repository,
        CancellationToken cancellationToken) =>
    {
        return Results.Ok(await repository.GetPharmacyDirectoryAsync(cancellationToken));
    })
    .WithName("GetClinicalPharmacyDirectory");

clinicalLists.MapGet("/prescription-refill-requests", async (
        ClinicalListRepository repository,
        HttpContext httpContext,
        string? status,
        string? patient,
        int? limit,
        int? offset,
        CancellationToken cancellationToken) =>
    {
        return Results.Ok(await repository.GetPrescriptionRefillQueueAsync(
            status,
            patient,
            limit ?? 100,
            offset ?? 0,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken));
    })
    .WithName("GetClinicalPrescriptionRefillQueue");

clinicalLists.MapGet("/{patientId}", async (
        ClinicalListRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var lists = await repository.GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? Results.NotFound() : Results.Ok(lists);
    })
    .WithName("GetClinicalListsForPatient");

clinicalLists.MapPost("/allergies", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalAllergyCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!await CanAccessSelectedFacilityPatientAsync(httpContext, request.PatientId, cancellationToken)) return Results.NotFound();
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateAllergyAsync(request, session.Username, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Allergy could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/allergies/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalAllergy")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPost("/problems", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalProblemCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!await CanAccessSelectedFacilityPatientAsync(httpContext, request.PatientId, cancellationToken)) return Results.NotFound();
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateProblemAsync(request, session.Username, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Problem could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/problems/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalProblem")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/problems/{problemId}/deactivate", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string problemId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Comments) || request.Comments.Trim().Length > 500)
        {
            return Results.BadRequest(new { error = "A non-empty clinical reason of at most 500 characters is required." });
        }
        var mutation = await repository.DeactivateProblemAsync(problemId, request, session.Username, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalProblem")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapDelete("/problems/{problemId}", () =>
        Results.Conflict(new
        {
            error = "Clinical problems are retained as part of the longitudinal record. Use the deactivation workflow with a clinical reason instead."
        }))
    .WithName("RejectClinicalProblemDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPost("/medications", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalMedicationCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!await CanAccessSelectedFacilityPatientAsync(httpContext, request.PatientId, cancellationToken)) return Results.NotFound();
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateMedicationAsync(request, session.Username, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Medication could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/medications/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalMedication")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/medications/{medicationId}/deactivate", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string medicationId,
        ClinicalMedicationDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.DeactivateMedicationAsync(medicationId, request, session.Username, cancellationToken);
        return mutation.Status switch
        {
            ClinicalMedicationLifecycleMutationStatus.Updated => Results.Ok(mutation.Mutation),
            ClinicalMedicationLifecycleMutationStatus.Invalid => Results.BadRequest(new { error = "A non-empty 1-500 character reason and loaded version are required." }),
            ClinicalMedicationLifecycleMutationStatus.NotFound => Results.NotFound(),
            _ => Results.Conflict(new { error = "The medication changed after it was loaded. Refresh and try again." })
        };
    })
    .WithName("DeactivateClinicalMedication")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/medications/{medicationId}/restore", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string medicationId,
        ClinicalMedicationRestoreRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.RestoreMedicationAsync(medicationId, request, session.Username, cancellationToken);
        return mutation.Status switch
        {
            ClinicalMedicationLifecycleMutationStatus.Updated => Results.Ok(mutation.Mutation),
            ClinicalMedicationLifecycleMutationStatus.Invalid => Results.BadRequest(new { error = "A non-empty 1-500 character reason and loaded version are required." }),
            ClinicalMedicationLifecycleMutationStatus.NotFound => Results.NotFound(),
            _ => Results.Conflict(new { error = "The medication changed after it was loaded. Refresh and try again." })
        };
    })
    .WithName("RestoreClinicalMedication")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/medications/{medicationId}", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string medicationId,
        ClinicalMedicationUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.UpdateMedicationAsync(medicationId, request, session.Username, cancellationToken);
        return mutation.Status switch
        {
            ClinicalMedicationLifecycleMutationStatus.Updated => Results.Ok(mutation.Mutation),
            ClinicalMedicationLifecycleMutationStatus.Invalid => Results.BadRequest(new { error = "A title, valid date, non-empty 1-500 character reason, and loaded version are required." }),
            ClinicalMedicationLifecycleMutationStatus.NotFound => Results.NotFound(),
            _ => Results.Conflict(new { error = "The medication changed after it was loaded. Refresh and try again." })
        };
    })
    .WithName("UpdateClinicalMedication")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapGet("/medications/{medicationId}/lifecycle-history", async (
        ClinicalListStateRepository repository,
        string medicationId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetMedicationLifecycleHistoryAsync(medicationId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetClinicalMedicationLifecycleHistory");

clinicalLists.MapGet("/allergies/{allergyId}/audit-history", async (
        ClinicalListStateRepository repository,
        string allergyId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetAuditHistoryAsync("allergy", allergyId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetClinicalAllergyAuditHistory");

clinicalLists.MapPut("/allergies/{allergyId}/deactivate", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string allergyId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Comments) || request.Comments.Trim().Length > 500)
        {
            return Results.BadRequest(new { error = "A non-empty clinical reason of at most 500 characters is required." });
        }
        var mutation = await repository.DeactivateAllergyAsync(allergyId, request, session.Username, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalAllergy")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapDelete("/allergies/{allergyId}", () =>
        Results.Conflict(new
        {
            error = "Clinical allergies are retained as part of the longitudinal record. Use the deactivation workflow with a clinical reason instead."
        }))
    .WithName("RejectClinicalAllergyDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapGet("/problems/{problemId}/audit-history", async (
        ClinicalListStateRepository repository,
        string problemId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetAuditHistoryAsync("problem", problemId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetClinicalProblemAuditHistory");

clinicalLists.MapPost("/prescriptions", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalPrescriptionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!await CanAccessSelectedFacilityPatientAsync(httpContext, request.PatientId, cancellationToken)) return Results.NotFound();
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreatePrescriptionAsync(request, session.Username, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Prescription could not be created from the supplied patient, drug, dose, and start date.")
            : Results.Created($"/api/clinical-lists/prescriptions/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalPrescription")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/prescriptions/{prescriptionId}", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string prescriptionId,
        ClinicalPrescriptionUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var result = await repository.UpdatePrescriptionAsync(
            prescriptionId,
            request,
            session.Username,
            cancellationToken);
        return result.Status switch
        {
            ClinicalPrescriptionUpdateStatus.Updated when result.Mutation is not null =>
                Results.Ok(result.Mutation),
            ClinicalPrescriptionUpdateStatus.Invalid =>
                Results.BadRequest(new
                {
                    error = "Prescription changes require a current version, valid structured fields, at least one change, and an edit reason."
                }),
            ClinicalPrescriptionUpdateStatus.NotFound => Results.NotFound(),
            ClinicalPrescriptionUpdateStatus.PatientInactive =>
                Results.Conflict(new
                {
                    error = "Prescription continuation is not permitted for a merged, retired, or deceased patient."
                }),
            ClinicalPrescriptionUpdateStatus.Conflict =>
                Results.Conflict(new
                {
                    error = "The prescription changed after it was loaded. Reload the current prescription before editing again.",
                    currentVersion = result.CurrentVersion
                }),
            _ => Results.Problem("The prescription update did not produce an authoritative result.")
        };
    })
    .WithName("UpdateClinicalPrescription")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/prescriptions/{prescriptionId}/deactivate", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string prescriptionId,
        ClinicalPrescriptionDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.DeactivatePrescriptionAsync(prescriptionId, request, session.Username, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalPrescription")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/prescriptions/{prescriptionId}/refill", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string prescriptionId,
        ClinicalPrescriptionRefillRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            var mutation = await repository.RefillPrescriptionAsync(prescriptionId, request, session.Username, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (PrescriptionContinuationBlockedException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .WithName("RefillClinicalPrescription")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/prescriptions/{prescriptionId}/route-pharmacy", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string prescriptionId,
        ClinicalPrescriptionPharmacyRouteRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            var mutation = await repository.RoutePrescriptionToPharmacyAsync(
                prescriptionId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (PrescriptionContinuationBlockedException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .WithName("RouteClinicalPrescriptionToPharmacy")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapGet("/prescriptions/{prescriptionId}/audit-history", async (
        ClinicalListRepository repository,
        string prescriptionId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetPrescriptionAuditHistoryAsync(prescriptionId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetClinicalPrescriptionAuditHistory");

clinicalLists.MapPut("/prescription-refill-requests/{messageId:int}/approve", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int messageId,
        ClinicalPrescriptionRefillApprovalRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        try
        {
            var mutation = await repository.ApprovePrescriptionRefillRequestAsync(
                messageId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (PrescriptionContinuationBlockedException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .WithName("ApproveClinicalPrescriptionRefillRequest")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/prescription-refill-requests/{messageId:int}/decision", async (
        ClinicalListRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int messageId,
        ClinicalPrescriptionRefillDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var decision = await repository.DecidePrescriptionRefillRequestAsync(
                messageId,
                request,
                session.Username,
                cancellationToken);
            return decision is null ? Results.NotFound() : Results.Ok(decision);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("DecideClinicalPrescriptionRefillRequest")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapDelete("/prescriptions/{prescriptionId}", () =>
        Results.Conflict(new
        {
            error = "Prescriptions and their audit trail are retained as part of the longitudinal record. Use the deactivation workflow with a clinical reason instead."
        }))
    .WithName("RejectClinicalPrescriptionDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPost("/immunizations", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalImmunizationCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!await CanAccessSelectedFacilityPatientAsync(httpContext, request.PatientId, cancellationToken)) return Results.NotFound();
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateImmunizationAsync(request, session.Username, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Immunization could not be created from the supplied patient, vaccine, and administered date.")
            : Results.Created($"/api/clinical-lists/immunizations/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalImmunization")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapPut("/immunizations/{immunizationId:int}/entered-in-error", async (
        ClinicalListStateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int immunizationId,
        ClinicalImmunizationErrorRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 500)
        {
            return Results.BadRequest(new { error = "A non-empty clinical reason of at most 500 characters is required." });
        }
        var mutation = await repository.MarkImmunizationEnteredInErrorAsync(immunizationId, request, session.Username, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("MarkClinicalImmunizationEnteredInError")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapDelete("/immunizations/{immunizationId:int}", () =>
        Results.Conflict(new
        {
            error = "Immunization records are retained as part of the longitudinal record. Mark an incorrect record entered in error with a clinical reason instead."
        }))
    .WithName("RejectClinicalImmunizationDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "med", "write"));

clinicalLists.MapGet("/immunizations/{immunizationKey}/audit-history", async (
        ClinicalListStateRepository repository,
        string immunizationKey,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetAuditHistoryAsync("immunization", immunizationKey, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetClinicalImmunizationAuditHistory");

app.MapMessageEndpoints();
var officeNotes = app.MapGroup("/api/office-notes").WithTags("Office Notes");
RequireAccessPermission(officeNotes, "encounters", "notes", "view");

officeNotes.MapGet("/", async (OfficeNoteRepository repository, string? activity, int? offset, int? limit, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAsync(activity ?? "active", offset ?? 0, limit ?? 8, cancellationToken)))
    .WithName("GetOfficeNotes");

officeNotes.MapPost("/", async (OfficeNoteRepository repository, OfficeNoteCreateRequest request, HttpContext context, CancellationToken cancellationToken) =>
    {
        var author = context.User.Identity?.Name ?? "system";
        var note = await repository.CreateAsync(request.Body, author, cancellationToken);
        return note is null ? Results.BadRequest("Office note body is required and must be 4000 characters or fewer.") : Results.Created($"/api/office-notes/{note.Id}", note);
    })
    .WithName("CreateOfficeNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "addonly"));

officeNotes.MapPut("/{noteId:guid}", async (OfficeNoteRepository repository, Guid noteId, OfficeNoteUpdateRequest request, CancellationToken cancellationToken) =>
    {
        var note = await repository.UpdateAsync(noteId, request.Body, cancellationToken);
        return note is null ? Results.NotFound() : Results.Ok(note);
    })
    .WithName("UpdateOfficeNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

officeNotes.MapPut("/{noteId:guid}/activity", async (OfficeNoteRepository repository, Guid noteId, OfficeNoteActivityRequest request, CancellationToken cancellationToken) =>
    {
        var note = await repository.SetActivityAsync(noteId, request.Active, cancellationToken);
        return note is null ? Results.NotFound() : Results.Ok(note);
    })
    .WithName("SetOfficeNoteActivity")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

officeNotes.MapDelete("/{noteId:guid}", async (OfficeNoteRepository repository, Guid noteId, CancellationToken cancellationToken) =>
    await repository.DeleteAsync(noteId, cancellationToken) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteOfficeNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "notes", "write"));

var addressBook = app.MapGroup("/api/administration/address-book").WithTags("Address Book");
RequireAccessPermission(addressBook, "admin", "practice", "view");
addressBook.MapGet("/", async (AddressBookRepository repository, string? organization, string? firstName, string? lastName, string? specialty, string? npi, string? type, bool? externalOnly, CancellationToken cancellationToken) => Results.Ok(await repository.SearchAsync(organization, firstName, lastName, specialty, npi, type, externalOnly ?? false, cancellationToken))).WithName("SearchAddressBook");
addressBook.MapPost("/", async (AddressBookRepository repository, AddressBookContactRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(null, request, cancellationToken); return Results.Created($"/api/administration/address-book/{item!.Id}", item); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contact"] = [e.Message] }); } }).WithName("CreateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
addressBook.MapPut("/{contactId:int}", async (AddressBookRepository repository, int contactId, AddressBookContactRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(contactId, request, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contact"] = [e.Message] }); } }).WithName("UpdateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
addressBook.MapDelete("/{contactId:int}", async (AddressBookRepository repository, int contactId, CancellationToken cancellationToken) => await repository.DeleteAsync(contactId, cancellationToken) ? Results.NoContent() : Results.NotFound()).WithName("DeleteAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));

var tracks = app.MapGroup("/api/administration/tracks").WithTags("Track Anything");
RequireAccessPermission(tracks, "admin", "practice", "view");
tracks.MapGet("/", async (TrackAnythingRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetTrackAnythingTypes");
tracks.MapPost("/", async (TrackAnythingRepository repository, TrackAnythingRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(null, request, cancellationToken); return Results.Created($"/api/administration/tracks/{item!.Id}", item); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["track"] = [e.Message] }); } }).WithName("CreateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
tracks.MapPut("/{trackId:int}", async (TrackAnythingRepository repository, int trackId, TrackAnythingRequest request, CancellationToken cancellationToken) => { try { var item = await repository.SaveAsync(trackId, request, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["track"] = [e.Message] }); } }).WithName("UpdateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "practice", "write"));
tracks.MapDelete("/{trackId:int}", async (TrackAnythingRepository repository, int trackId, CancellationToken cancellationToken) => await repository.DeleteAsync(trackId, cancellationToken) ? Results.NoContent() : Results.NotFound()).WithName("DeleteTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

var patientEducation = app.MapGroup("/api/patient-education").WithTags("Patient Education");
RequireAccessPermission(patientEducation, "patients", "demo", "view");
patientEducation.MapGet("/resources", async (PatientEducationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetPatientEducationResources");
patientEducation.MapPost("/search", async (PatientEducationRepository repository, PatientEducationSearchRequest request, CancellationToken cancellationToken) => { var result = await repository.SearchAsync(request, cancellationToken); return result is null ? Results.BadRequest("An active HTTPS resource and search text are required.") : Results.Ok(result); }).WithName("SearchPatientEducation");
var recalls = app.MapGroup("/api/recalls").WithTags("Recalls"); RequireAccessPermission(recalls, "patients", "appt", "view");
recalls.MapGet("/", async (RecallRepository repository, bool? includeClosed, CancellationToken ct) => Results.Ok(await repository.GetAsync(includeClosed ?? false, ct))).WithName("GetRecalls");
recalls.MapPost("/", async (RecallRepository repository, RecallRequest request, AuthRepository authRepository, HttpContext context, CancellationToken ct) => { var session = await GetSessionFromHeaderAsync(authRepository, context, ct); var item = await repository.CreateAsync(request, session.Username, ct); return item is null ? Results.BadRequest() : Results.Created($"/api/recalls/{item.Id}", item); }).WithName("CreateRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
recalls.MapPost("/{id:guid}/close", async (RecallRepository repository, Guid id, RecallClosureRequest request, AuthRepository authRepository, HttpContext context, CancellationToken ct) => { try { var session = await GetSessionFromHeaderAsync(authRepository, context, ct); var item = await repository.CloseAsync(id, request, session.Username, ct); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["closure"] = [exception.Message] }); } }).WithName("CloseRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
recalls.MapDelete("/{id:guid}", (Guid id) => Results.Problem(statusCode: StatusCodes.Status405MethodNotAllowed, title: "Recall deletion is retired", detail: "Close or cancel a recall so its outreach evidence is retained.")).WithName("DeleteRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
recalls.MapGet("/{id:guid}/activity", async (RecallRepository repository, Guid id, CancellationToken ct) => { var activity = await repository.GetActivityAsync(id, ct); return activity is null ? Results.NotFound() : Results.Ok(activity); }).WithName("GetRecallActivity");
recalls.MapPost("/{id:guid}/activity", async (RecallRepository repository, Guid id, RecallActivityRequest request, CancellationToken ct) => { try { var result = await repository.AddActivityAsync(id, request, ct); return result is null ? Results.NotFound() : Results.Created($"/api/recalls/{id}/activity/{result.Id}", result); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["activity"] = [e.Message] }); } }).WithName("AddRecallActivity").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

var batchCommunication = app.MapGroup("/api/batch-communication").WithTags("Batch Communication"); RequireAccessPermission(batchCommunication, "admin", "batchcom", "view");
batchCommunication.MapPost("/preview", async (BatchCommunicationRepository repository, BatchCommunicationPreviewRequest request, CancellationToken ct) => { try { return Results.Ok(await repository.PreviewAsync(request, ct)); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["filter"] = [e.Message] }); } }).WithName("PreviewBatchCommunication");
batchCommunication.MapPost("/campaigns", async (BatchCommunicationRepository repository, BatchCommunicationCampaignCreateRequest request, CancellationToken ct) => { try { var campaign = await repository.CreateAsync(request, ct); return Results.Created($"/api/batch-communication/campaigns/{campaign.Campaign.Id}", campaign); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["campaign"] = [e.Message] }); } }).WithName("CreateBatchCommunicationCampaign").AddEndpointFilter(AccessPermissionFilter("admin", "batchcom", "write"));
batchCommunication.MapGet("/campaigns", async (BatchCommunicationRepository repository, CancellationToken ct) => Results.Ok(await repository.GetAsync(ct))).WithName("GetBatchCommunicationCampaigns");
batchCommunication.MapGet("/campaigns/{id:guid}", async (BatchCommunicationRepository repository, Guid id, CancellationToken ct) => { var campaign = await repository.GetAsync(id, ct); return campaign is null ? Results.NotFound() : Results.Ok(campaign); }).WithName("GetBatchCommunicationCampaign");
batchCommunication.MapGet("/campaigns/{id:guid}/output", async (BatchCommunicationRepository repository, Guid id, CancellationToken ct) => { var campaign = await repository.GetAsync(id, ct); if (campaign is null) return Results.NotFound(); var csv = new System.Text.StringBuilder("Patient ID,Name,Email,Home Phone,Cell Phone,Postal Code,Next Appointment,Last Appointment,Last Visit,Subject,Body\n"); foreach (var item in campaign.Recipients) csv.AppendLine(string.Join(',', new[] { item.PatientId, item.DisplayName, item.Email, item.PhoneHome, item.PhoneCell, item.PostalCode, item.NextAppointmentDate, item.LastAppointmentDate, item.LastVisitDate, item.RenderedSubject, item.RenderedBody }.Select(value => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\""))); return Results.File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"batch-communication-{id}.csv"); }).WithName("ExportBatchCommunicationCampaign");

var chartTracker = app.MapGroup("/api/chart-tracker").WithTags("Chart Tracker"); RequireAccessPermission(chartTracker, "patients", "appt", "view");
chartTracker.MapGet("/options", async (ChartTrackerRepository repository, CancellationToken ct) => Results.Ok(await repository.GetOptionsAsync(ct))).WithName("GetChartTrackerOptions");
chartTracker.MapGet("/lookup/{identifier}", async (ChartTrackerRepository repository, string identifier, CancellationToken ct) => { var patient = await repository.FindAsync(identifier, ct); return patient is null ? Results.NotFound() : Results.Ok(patient); }).WithName("LookupChartTrackerPatient");
chartTracker.MapGet("/patients/{patientId}/history", async (ChartTrackerRepository repository, string patientId, CancellationToken ct) => { var history = await repository.GetHistoryAsync(patientId, ct); return history is null ? Results.NotFound() : Results.Ok(history); }).WithName("GetChartTrackerHistory");
chartTracker.MapPost("/patients/{patientId}/events", async (ChartTrackerRepository repository, string patientId, ChartTrackerUpdateRequest request, CancellationToken ct) => { try { var item = await repository.RecordAsync(patientId, request, ct); return item is null ? Results.NotFound() : Results.Created($"/api/chart-tracker/patients/{patientId}/events/{item.Id}", item); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["tracker"] = [e.Message] }); } }).WithName("RecordChartTrackerEvent").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

var documentTemplates = app
    .MapGroup("/api/administration/document-templates")
    .WithTags("Document Templates");
RequireAccessPermission(documentTemplates, "admin", "super", "view");

documentTemplates
    .MapGet("/", async (
        DocumentTemplateRepository repository,
        string? search,
        bool? includeInactive,
        int? offset,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetAsync(
                search,
                includeInactive ?? true,
                offset ?? 0,
                limit ?? 10,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("GetDocumentTemplates");

documentTemplates
    .MapPost("/", async (
        DocumentTemplateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        DocumentTemplateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var item = await repository.SaveAsync(
                null,
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/administration/document-templates/{item!.Id}",
                item);
        }
        catch (DocumentTemplateNameConflictException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DocumentTemplateConcurrencyException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("CreateDocumentTemplate")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

documentTemplates
    .MapPut("/{id:guid}", async (
        DocumentTemplateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid id,
        DocumentTemplateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var item = await repository.SaveAsync(
                id,
                request,
                session.Username,
                cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }
        catch (DocumentTemplateNameConflictException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DocumentTemplateConcurrencyException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("UpdateDocumentTemplate")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

documentTemplates
    .MapPost("/{id:guid}/render", async (
        DocumentTemplateRepository repository,
        Guid id,
        DocumentTemplateRenderRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var item = await repository.RenderAsync(id, request, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("RenderDocumentTemplate");

documentTemplates
    .MapGet("/{id:guid}/history", async (
        DocumentTemplateRepository repository,
        Guid id,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetHistoryAsync(id, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetDocumentTemplateHistory");

documentTemplates
    .MapGet("/{id:guid}/binary-versions", async (
        DocumentTemplateRepository repository,
        Guid id,
        CancellationToken cancellationToken) =>
        Results.Ok(await repository.GetBinaryVersionsAsync(id, cancellationToken)))
    .WithName("GetDocumentTemplateBinaryVersions");

documentTemplates
    .MapPost("/{id:guid}/binary-versions", async (
        DocumentTemplateRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid id,
        DocumentTemplateBinaryUploadRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var item = await repository.AddBinaryVersionAsync(
                id,
                request,
                session.Username,
                cancellationToken);
            return item is null
                ? Results.NotFound()
                : Results.Created(
                    $"/api/administration/document-templates/{id}/binary-versions/{item.Id}",
                    item);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("AddDocumentTemplateBinaryVersion")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

documentTemplates
    .MapGet("/{id:guid}/binary-versions/{versionId:guid}/download", async (
        DocumentTemplateRepository repository,
        Guid id,
        Guid versionId,
        CancellationToken cancellationToken) =>
    {
        var item = await repository.GetBinaryAsync(id, versionId, cancellationToken);
        return item is null
            ? Results.NotFound()
            : Results.File(item.Content, item.Mimetype, item.FileName);
    })
    .WithName("DownloadDocumentTemplateBinaryVersion");

documentTemplates
    .MapPost("/{id:guid}/generate-attachment", async (
        DocumentTemplateRepository repository,
        DocumentRepository documents,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid id,
        DocumentTemplateAttachmentRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var documentDate = string.IsNullOrWhiteSpace(request.DocDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
                : request.DocDate;

            if (request.BinaryVersionId is { } versionId)
            {
                var binary = await repository.GetBinaryAsync(
                    id,
                    versionId,
                    cancellationToken);
                if (binary is null)
                {
                    return Results.NotFound();
                }

                var mutation = await documents.CreateBinaryAsync(
                    new PatientDocumentBinaryCreateRequest(
                        request.PatientId,
                        request.CategoryId,
                        $"Template: {Path.GetFileNameWithoutExtension(binary.FileName)}",
                        documentDate,
                        request.Encounter,
                        binary.FileName,
                        binary.Mimetype,
                        Convert.ToBase64String(binary.Content),
                        $"Generated from document template {id}, binary version {versionId}."),
                    cancellationToken);
                if (mutation is null)
                {
                    return Results.Problem(
                        detail: "Patient attachment could not be created from this template.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                await repository.RecordAttachmentGeneratedAsync(
                    id,
                    versionId,
                    mutation.Id,
                    request.PatientId,
                    session.Username,
                    cancellationToken);
                return Results.Created($"/api/documents/{mutation.Id}", mutation);
            }

            var rendered = await repository.RenderAsync(
                id,
                new DocumentTemplateRenderRequest(request.PatientId),
                cancellationToken);
            if (rendered is null)
            {
                return Results.NotFound();
            }

            var textMutation = await documents.CreateAsync(
                new PatientDocumentCreateRequest(
                    request.PatientId,
                    request.CategoryId,
                    $"Template: {rendered.Template.Name}",
                    documentDate,
                    request.Encounter,
                    rendered.Content,
                    $"Generated from document template {id}."),
                cancellationToken);
            if (textMutation is null)
            {
                return Results.Problem(
                    detail: "Patient attachment could not be created from this template.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await repository.RecordAttachmentGeneratedAsync(
                id,
                null,
                textMutation.Id,
                request.PatientId,
                session.Username,
                cancellationToken);
            return Results.Created($"/api/documents/{textMutation.Id}", textMutation);
        }
        catch (ArgumentException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("GenerateDocumentTemplateAttachment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

documentTemplates
    .MapDelete("/{id:guid}/test-fixture", async (
        DocumentTemplateRepository repository,
        Guid id,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await repository.DeleteTestFixtureAsync(id, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    })
    .WithName("DeleteDocumentTemplateTestFixture")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "write"));

var records = app.MapGroup("/api/records").WithTags("Records");
RequireAccessPermission(records, "patients", "docs", "view");

records.MapGet("/policy", (ManagedRecordRepository repository) =>
        Results.Ok(repository.GetPolicy()))
    .WithName("GetManagedRecordPolicy");

records.MapGet("/", async (
        ManagedRecordRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListAsync(patientId, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetManagedRecordIntakes");

records.MapPost("/", async (
        ManagedRecordRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ManagedRecordCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var result = await repository.CreateAsync(
                request,
                session.Username,
                cancellationToken);
            return result.IdempotentReplay
                ? Results.Ok(result)
                : Results.Created($"/api/records/{result.Intake.IntakeId}", result);
        }
        catch (ManagedRecordIdempotencyConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateManagedRecordIntake")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

records.MapPut("/{intakeId:guid}/classification", async (
        ManagedRecordRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid intakeId,
        ManagedRecordClassificationUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var result = await repository.UpdateClassificationAsync(
                intakeId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ManagedRecordConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdateManagedRecordClassification")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

records.MapPost("/{intakeId:guid}/{action}", async (
        ManagedRecordRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid intakeId,
        string action,
        ManagedRecordActionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var result = await repository.ActAsync(
                intakeId,
                action,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ManagedRecordConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("ActOnManagedRecordIntake")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

records.MapGet("/{intakeId:guid}/history", async (
        ManagedRecordRepository repository,
        Guid intakeId,
        CancellationToken cancellationToken) =>
    {
        var result = await repository.GetHistoryAsync(intakeId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetManagedRecordHistory");

records.MapDelete("/{intakeId:guid}/test-fixture", async (
        ManagedRecordRepository repository,
        Guid intakeId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await repository.DeleteTestFixtureAsync(intakeId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("DeleteManagedRecordTestFixture")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

var documents = app.MapGroup("/api/documents").WithTags("Documents");
RequireAccessPermission(documents, "patients", "docs", "view");
documents.AddEndpointFilter(ClinicalResourceFacilityScopeFilter());

documents.MapGet("/ocr-queue", async (
        DocumentRepository repository,
        string? patientId,
        string? status,
        string? priority,
        string? query,
        int? offset,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var queue = await repository.GetOcrQueueAsync(
                cancellationToken,
                patientId,
                status,
                priority,
                query,
                offset ?? 0,
                limit ?? 1_000);
            return Results.Ok(queue);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetPatientDocumentOcrQueue");

documents.MapGet("/{documentId:int}/ocr-history", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var history = await repository.GetOcrHistoryAsync(documentId, cancellationToken);
            return history is null ? Results.NotFound() : Results.Ok(history);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetPatientDocumentOcrHistory");

documents.MapGet("/routing-queue", async (
        DocumentRepository repository,
        CancellationToken cancellationToken,
        string? patientId = null,
        string? status = null,
        string? priority = null,
        string? assignedTo = null,
        int? minimumAgeHours = null,
        string? query = null,
        int offset = 0,
        int limit = 50) =>
    {
        try
        {
            var queue = await repository.GetRoutingQueueAsync(
                cancellationToken,
                patientId,
                status,
                priority,
                assignedTo,
                minimumAgeHours,
                query,
                offset,
                limit);
            return Results.Ok(queue);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetPatientDocumentRoutingQueue");

documents.MapGet("/routing-assignees", async (
        DocumentRepository repository,
        CancellationToken cancellationToken) =>
    {
        return Results.Ok(await repository.GetRoutingAssigneesAsync(cancellationToken));
    })
    .WithName("GetPatientDocumentRoutingAssignees");

documents.MapGet("/{documentId:int}/routing-history", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetRoutingHistoryAsync(documentId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDocumentRoutingHistory");

documents.MapPut("/{documentId:int}/routing", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentRoutingMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var result = await repository.RouteDocumentAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentRoutingConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("RoutePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPost("/{documentId:int}/routing/complete", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentRoutingCompleteRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var result = await repository.CompleteRoutingAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentRoutingConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("CompletePatientDocumentRouting")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapGet("/retention-policy", async (
        DocumentRepository repository,
        CancellationToken cancellationToken,
        string? patientId = null) =>
    {
        var policy = await repository.GetRetentionPolicyAsync(cancellationToken, patientId);
        return Results.Ok(policy);
    })
    .WithName("GetPatientDocumentRetentionPolicy");

documents.MapPost("/{documentId:int}/ocr/complete", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentOcrCompleteRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var completion = await repository.CompleteOcrAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return completion is null ? Results.NotFound() : Results.Ok(completion);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentOcrConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("CompletePatientDocumentOcr")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPost("/{documentId:int}/ocr/start", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentOcrStartRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var result = await repository.StartOcrAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentOcrConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("StartPatientDocumentOcr")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPost("/{documentId:int}/ocr/fail", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentOcrFailRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var result = await repository.FailOcrAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentOcrConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("FailPatientDocumentOcr")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPost("/{documentId:int}/ocr/correct", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentOcrCorrectRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var result = await repository.CorrectOcrTextAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentOcrConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentTaskVersion = conflict.CurrentTaskVersion,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("CorrectPatientDocumentOcrText")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPost("/{documentId:int}/retention/dispose", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentRetentionDispositionRequest request,
        CancellationToken cancellationToken) =>
    {
        var disposition = await repository.DisposeRetentionAsync(documentId, request, cancellationToken);
        return disposition is null
            ? Results.BadRequest("Patient document retention disposition could not be completed from the supplied document and policy evidence.")
            : Results.Ok(disposition);
    })
    .WithName("DisposePatientDocumentRetention")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapGet("/{documentId:int}/content", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var document = await repository.GetContentAsync(documentId, cancellationToken);
        return document is null ? Results.NotFound() : Results.Ok(document);
    })
    .WithName("GetPatientDocumentContent");

documents.MapGet("/{documentId:int}/download", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var document = await repository.GetContentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        var fileBytes = document.IsBinary && !string.IsNullOrWhiteSpace(document.ContentBase64)
            ? Convert.FromBase64String(document.ContentBase64)
            : Encoding.UTF8.GetBytes(document.Content);

        return Results.File(
            fileBytes,
            document.Mimetype ?? "application/octet-stream",
            document.FileName);
    })
    .WithName("DownloadPatientDocument");

documents.MapGet("/{documentId:int}/versions", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetVersionHistoryAsync(documentId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDocumentVersionHistory");

documents.MapGet("/{documentId:int}/review-history", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetReviewHistoryAsync(documentId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDocumentReviewHistory");

documents.MapGet("/{documentId:int}/archive-history", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetArchiveHistoryAsync(documentId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDocumentArchiveHistory");

documents.MapGet("/{documentId:int}/versions/{version:int}/content", async (
        DocumentRepository repository,
        int documentId,
        int version,
        CancellationToken cancellationToken) =>
    {
        var content = await repository.GetVersionContentAsync(documentId, version, cancellationToken);
        return content is null ? Results.NotFound() : Results.Ok(content);
    })
    .WithName("GetPatientDocumentVersionContent");

documents.MapGet("/{documentId:int}/versions/{version:int}/download", async (
        DocumentRepository repository,
        int documentId,
        int version,
        CancellationToken cancellationToken) =>
    {
        var content = await repository.GetVersionContentAsync(documentId, version, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var fileBytes = content.IsBinary && !string.IsNullOrWhiteSpace(content.ContentBase64)
            ? Convert.FromBase64String(content.ContentBase64)
            : Encoding.UTF8.GetBytes(content.Content);

        return Results.File(
            fileBytes,
            content.Mimetype ?? "application/octet-stream",
            content.FileName);
    })
    .WithName("DownloadPatientDocumentVersion");

documents.MapGet("/category-options", async (
        DocumentRepository repository,
        CancellationToken cancellationToken) =>
    {
        var options = await repository.GetCategoryOptionsAsync(cancellationToken);
        return Results.Ok(options);
    })
    .WithName("GetPatientDocumentCategoryOptions");

documents.MapGet("/{patientId}", async (
        DocumentRepository repository,
        string patientId,
        CancellationToken cancellationToken,
        bool includeArchived = false) =>
    {
        var patientDocuments = await repository.GetForPatientAsync(patientId, cancellationToken, includeArchived);
        return patientDocuments is null ? Results.NotFound() : Results.Ok(patientDocuments);
    })
    .WithName("GetPatientDocuments");

documents.MapPost("/", async (
        DocumentRepository repository,
        PatientDocumentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient document could not be created from the supplied patient and document details.")
            : Results.Created($"/api/documents/{mutation.Id}", mutation);
    })
    .WithName("CreatePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

documents.MapPost("/binary", async (
        DocumentRepository repository,
        PatientDocumentBinaryCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateBinaryAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Binary patient document could not be created from the supplied patient, file, and document details.")
            : Results.Created($"/api/documents/{mutation.Id}", mutation);
    })
    .WithName("CreateBinaryPatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

documents.MapPost("/scanner-captures", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        PatientDocumentScannerCaptureRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var mutation = await repository.CreateScannerCaptureAsync(
            request,
            session.Username,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Scanner-captured patient document could not be created from the supplied patient, scanner, and document details.")
            : Results.Created($"/api/documents/{mutation.Id}", mutation);
    })
    .WithName("CreateScannerCapturePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

documents.MapPost("/external-link", async (
        DocumentRepository repository,
        PatientDocumentExternalLinkCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateExternalLinkAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("External-link patient document could not be created from the supplied patient, URL, and document details.")
            : Results.Created($"/api/documents/{mutation.Id}", mutation);
    })
    .WithName("CreateExternalLinkPatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "addonly"));

documents.MapGet("/{documentId:int}/metadata-history", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetMetadataHistoryAsync(documentId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetPatientDocumentMetadataHistory");

documents.MapPut("/{documentId:int}/metadata", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.UpdateMetadataAsync(
            documentId,
            request,
            session.Username,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient document metadata could not be updated from the supplied filing details.")
            : Results.Ok(mutation);
    })
    .WithName("UpdatePatientDocumentMetadata")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/content", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.ReplaceContentAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Patient document content could not be replaced from the supplied text payload or did not materially change.")
                : Results.Ok(mutation);
        }
        catch (DocumentVersionConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                currentVersion = conflict.CurrentVersion
            });
        }
    })
    .WithName("ReplacePatientDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/content/binary", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentBinaryContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.ReplaceBinaryContentAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Binary patient document content could not be replaced from the supplied file payload or did not materially change.")
                : Results.Ok(mutation);
        }
        catch (DocumentVersionConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = "The document changed after this version was loaded. Reload its version history before replacing content.",
                currentVersion = conflict.CurrentVersion
            });
        }
    })
    .WithName("ReplaceBinaryPatientDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/soft-delete", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentArchiveRequest? request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.SoftDeleteAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentArchiveConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentArchived = conflict.CurrentArchived
            });
        }
    })
    .WithName("SoftDeletePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/restore", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentArchiveRequest? request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.RestoreAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentArchiveConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentArchived = conflict.CurrentArchived
            });
        }
    })
    .WithName("RestorePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/sign", async (
        DocumentRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int documentId,
        PatientDocumentSignRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.SignAsync(
                documentId,
                request,
                session.Username,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentReviewConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = conflict.Message,
                currentStatus = conflict.CurrentStatus
            });
        }
    })
    .WithName("SignPatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapDelete("/{documentId:int}", (int documentId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Document deletion is not available",
            detail: "Patient documents are retained. Use the reasoned archive workflow instead."))
    .WithName("RetirePatientDocumentDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs_rm", "write"));

var procedures = app.MapGroup("/api/procedures").WithTags("Procedures");
RequireAccessPermission(procedures, "patients", "lab", "view");
procedures.AddEndpointFilter(ProcedureFacilityScopeFilter());

procedures.MapGet("/lab-provider-address-book", async (
        ProcedureRepository repository,
        CancellationToken cancellationToken) =>
    {
        var addressBook = await repository.GetLabProviderAddressBookAsync(cancellationToken);
        return Results.Ok(addressBook);
    })
    .WithName("GetProcedureLabProviderAddressBook");

procedures.MapPost("/lab-provider-address-book", async (
        ProcedureDirectoryRepository repository,
        ProcedureLabProviderAddressBookMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateLabProviderAddressBookOrganizationAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest(new { error = "Procedure lab provider address-book organization is required." })
            : Results.Created($"/api/procedures/lab-provider-address-book/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureLabProviderAddressBookOrganization")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapDelete("/lab-provider-address-book/{organizationId:int}", async (
        ProcedureDirectoryRepository repository,
        int organizationId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteLabProviderAddressBookOrganizationAsync(organizationId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteProcedureLabProviderAddressBookOrganization")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapGet("/lab-providers", async (
        ProcedureRepository repository,
        bool? includeInactive,
        CancellationToken cancellationToken) =>
    {
        var directory = await repository.GetLabProvidersAsync(includeInactive ?? false, cancellationToken);
        return Results.Ok(directory);
    })
    .WithName("GetProcedureLabProviders");

procedures.MapPost("/lab-providers", async (
        ProcedureDirectoryRepository repository,
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateLabProviderAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest(new { error = "Procedure lab provider name or valid address-book organization is required." })
            : Results.Created($"/api/procedures/lab-providers/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureLabProvider")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/lab-providers/{providerId:int}", async (
        ProcedureDirectoryRepository repository,
        int providerId,
        ProcedureLabProviderMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateLabProviderAsync(providerId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureLabProvider")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapDelete("/lab-providers/{providerId:int}", async (
        ProcedureDirectoryRepository repository,
        int providerId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteLabProviderAsync(providerId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteProcedureLabProvider")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapGet("/order-catalog", async (
        ProcedureRepository repository,
        CancellationToken cancellationToken) =>
    {
        var catalog = await repository.GetOrderCatalogAsync(cancellationToken);
        return Results.Ok(catalog);
    })
    .WithName("GetProcedureOrderCatalog");

procedures.MapPost("/order-catalog", async (
        ProcedureDirectoryRepository repository,
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateOrderCatalogItemAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest(new { error = "Procedure order catalog item requires a valid name, type, parent, lab, and code." })
            : Results.Created($"/api/procedures/order-catalog/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureOrderCatalogItem")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/order-catalog/import-compendium", async (
        ProcedureRepository repository,
        ProcedureOrderCatalogImportRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var import = await repository.ImportOrderCatalogCompendiumAsync(request, cancellationToken);
            return import is null
                ? Results.BadRequest(new { error = "Procedure order catalog compendium import requires a valid vendor format, group, lab, and CSV payload." })
                : Results.Ok(import);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["csvText"] = [exception.Message] });
        }
    })
    .WithName("ImportProcedureOrderCatalogCompendium")
    .WithMetadata(new RequestSizeLimitAttribute(ProcedureRepository.MaximumOrderCatalogImportRequestBytes))
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/order-catalog/{itemId:int}", async (
        ProcedureDirectoryRepository repository,
        int itemId,
        ProcedureOrderCatalogMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateOrderCatalogItemAsync(itemId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureOrderCatalogItem")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapDelete("/order-catalog/{itemId:int}", async (
        ProcedureDirectoryRepository repository,
        int itemId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteOrderCatalogItemAsync(itemId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteProcedureOrderCatalogItem")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapGet("/report-review-queue", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        string? status,
        string? patientId,
        int? providerId,
        int? labId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var queue = await repository.GetReportReviewQueueAsync(
            status,
            patientId,
            providerId,
            labId,
            fromDate,
            toDate,
            limit ?? 25,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return Results.Ok(queue);
    })
    .WithName("GetProcedureReportReviewQueue");

procedures.MapGet("/critical-result-queue", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetCriticalLabResultQueueAsync(
        RequireStaffAccessContext(httpContext).FacilityId,
        cancellationToken)))
    .WithName("GetCriticalLabResultQueue")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

procedures.MapPut("/results/{resultId:int}/critical-acknowledgement", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultAcknowledgementRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.AcknowledgeCriticalLabResultAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { accepted = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("AcceptCriticalLabResultFollowUp")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/results/{resultId:int}/critical-follow-up/ownership", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultFollowUpOwnershipRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.TransferCriticalLabResultFollowUpAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { updated = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("TransferCriticalLabResultFollowUpOwnership")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/results/{resultId:int}/critical-follow-up/communications", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultFollowUpCommunicationRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.RecordCriticalLabResultCommunicationAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { recorded = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RecordCriticalLabResultCommunication")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/results/{resultId:int}/critical-follow-up/clinical-actions", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultFollowUpActionRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.RecordCriticalLabResultClinicalActionAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { recorded = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RecordCriticalLabResultClinicalAction")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/results/{resultId:int}/critical-follow-up/escalations", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultFollowUpEscalationRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.EscalateCriticalLabResultFollowUpAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { escalated = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("EscalateCriticalLabResultFollowUp")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/results/{resultId:int}/critical-follow-up/closure", async (
        ProcedureRepository repository, AuthRepository authRepository, HttpContext httpContext,
        int resultId, CriticalLabResultFollowUpClosureRequest request, CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        try
        {
            return await repository.CloseCriticalLabResultFollowUpAsync(
                    resultId, request, session.Username, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)
                ? Results.Ok(new { closed = true })
                : Results.NotFound();
        }
        catch (CriticalLabResultFollowUpConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, exception.ExpectedVersion, exception.CurrentVersion, exception.CurrentStatus });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CloseCriticalLabResultFollowUp")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapGet("/results/{resultId:int}/critical-follow-up/history", async (
        ProcedureRepository repository, HttpContext httpContext, int resultId, CancellationToken cancellationToken) =>
    (await repository.GetCriticalLabResultFollowUpHistoryAsync(
        resultId, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)) is { } history
        ? Results.Ok(history)
        : Results.NotFound())
    .WithName("GetCriticalLabResultFollowUpHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

procedures.MapGet("/reports/{reportId:int}/review-history", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        int reportId,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetReportReviewHistoryAsync(
            reportId,
            cancellationToken,
            RequireStaffAccessContext(httpContext).FacilityId);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetProcedureReportReviewHistory");

procedures.MapGet("/order-queue", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        string? status,
        string? patientId,
        int? providerId,
        int? labId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var queue = await repository.GetOrderQueueAsync(
            status,
            patientId,
            providerId,
            labId,
            fromDate,
            toDate,
            limit.GetValueOrDefault(50),
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return Results.Ok(queue);
    })
    .WithName("GetProcedureOrderQueue");

procedures.MapGet("/{patientId}", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var procedureResults = await repository.GetForPatientAsync(
            patientId,
            cancellationToken,
            RequireStaffAccessContext(httpContext).FacilityId);
        return procedureResults is null ? Results.NotFound() : Results.Ok(procedureResults);
    })
    .WithName("GetProcedureResultsForPatient");

procedures.MapPost("/orders", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        ProcedureOrderCreateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.CreateOrderAsync(
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure order could not be created from the supplied patient, encounter, and order details.")
                : Results.Created($"/api/procedures/orders/{mutation.Id}", mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("CreateProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapGet("/orders/{orderId:int}/history", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        int orderId,
        CancellationToken cancellationToken) =>
    (await repository.GetOrderMutationHistoryAsync(
        orderId,
        RequireStaffAccessContext(httpContext).FacilityId,
        cancellationToken)) is { } history
        ? Results.Ok(history)
        : Results.NotFound())
    .WithName("GetProcedureOrderMutationHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

procedures.MapPut("/orders/{orderId:int}/status", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int orderId,
        ProcedureOrderStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.UpdateOrderStatusAsync(
                orderId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("UpdateProcedureOrderStatus")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/orders/{orderId:int}/transmit", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int orderId,
        ProcedureOrderTransmitRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.TransmitOrderAsync(
                orderId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure order could not be marked transmitted from the supplied order state.")
                : Results.Ok(mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("TransmitProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/orders/{orderId:int}", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int orderId,
        ProcedureOrderUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.UpdateOrderAsync(
                orderId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure order could not be updated from the supplied order details.")
                : Results.Ok(mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("UpdateProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/reports", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        ProcedureReportCreateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateReportAsync(
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report could not be created from the supplied order and report details.")
            : Results.Created($"/api/procedures/reports/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPut("/reports/{reportId:int}", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int reportId,
        ProcedureReportUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.UpdateReportAsync(
            reportId,
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report could not be updated from the supplied report details.")
            : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/{reportId:int}/sign", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int reportId,
        ProcedureReportSignRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.SignReportAsync(
                reportId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure report could not be signed from the supplied review details.")
                : Results.Ok(mutation);
        }
        catch (ProcedureReportReviewConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
        }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    })
    .WithName("SignProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

procedures.MapPut("/reports/{reportId:int}/deny-review", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int reportId,
        ProcedureReportReviewDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.DenyReportReviewAsync(
                reportId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure report review could not be denied from the supplied details.")
                : Results.Ok(mutation);
        }
        catch (ProcedureReportReviewConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
        }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    })
    .WithName("DenyProcedureReportReview")
    .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

procedures.MapPut("/reports/{reportId:int}/review-assignment", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int reportId,
        ProcedureReportReviewAssignmentRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.AssignReportReviewerAsync(
                reportId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure report reviewer assignment could not be saved from the supplied details.")
                : Results.Ok(mutation);
        }
        catch (ProcedureReportReviewConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
        }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    })
    .WithName("AssignProcedureReportReviewer")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/{reportId:int}/reopen-review", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int reportId,
        ProcedureReportReviewDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.ReopenReportReviewAsync(
                reportId,
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure report review could not be reopened.")
                : Results.Ok(mutation);
        }
        catch (ProcedureReportReviewConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
        }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    })
    .WithName("ReopenProcedureReportReview")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/bulk-sign", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ProcedureReportBulkSignRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.BulkSignReportsAsync(
                request,
                session.Username,
                RequireStaffAccessContext(httpContext).FacilityId,
                cancellationToken);
            return mutation is null
                ? Results.BadRequest("Procedure reports could not be bulk signed from the supplied review details.")
                : Results.Ok(mutation);
        }
        catch (ProcedureReportReviewConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, expectedVersion = exception.ExpectedVersion, currentVersion = exception.CurrentVersion, currentStatus = exception.CurrentStatus });
        }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    })
    .WithName("BulkSignProcedureReports")
    .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

procedures.MapPost("/specimens", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        ProcedureSpecimenCreateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateSpecimenAsync(
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure specimen could not be created from the supplied order and specimen details.")
            : Results.Created($"/api/procedures/specimens/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureSpecimen")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPut("/specimens/{specimenId:int}/lifecycle", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int specimenId,
        ProcedureSpecimenLifecycleTransitionRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.TransitionSpecimenLifecycleAsync(
            specimenId,
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null
            ? Results.Conflict(new { error = "The specimen lifecycle transition is no longer valid at the supplied version." })
            : Results.Ok(mutation);
    })
    .WithName("TransitionProcedureSpecimenLifecycle")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapGet("/specimens/{specimenId:int}/lifecycle-history", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        int specimenId,
        CancellationToken cancellationToken) =>
    (await repository.GetSpecimenLifecycleHistoryAsync(
        specimenId,
        RequireStaffAccessContext(httpContext).FacilityId,
        cancellationToken)) is { } history
        ? Results.Ok(history)
        : Results.NotFound())
    .WithName("GetProcedureSpecimenLifecycleHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

procedures.MapPost("/results", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        ProcedureResultCreateRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.CreateResultAsync(
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure result could not be created from the supplied report and result details.")
            : Results.Created($"/api/procedures/results/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureResult")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapGet("/results/{resultId:int}/history", async (
        ProcedureRepository repository,
        HttpContext httpContext,
        int resultId,
        CancellationToken cancellationToken) =>
    (await repository.GetResultMutationHistoryAsync(
        resultId,
        RequireStaffAccessContext(httpContext).FacilityId,
        cancellationToken)) is { } history
        ? Results.Ok(history)
        : Results.NotFound())
    .WithName("GetProcedureResultMutationHistory")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "view"));

procedures.MapPut("/results/{resultId:int}", async (
        ProcedureRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int resultId,
        ProcedureResultUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var mutation = await repository.UpdateResultAsync(
            resultId,
            request,
            session.Username,
            RequireStaffAccessContext(httpContext).FacilityId,
            cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureResult")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapDelete("/orders/{orderId:int}", async (
        ProcedureRepository repository,
        int orderId,
        CancellationToken cancellationToken) =>
    {
        var deletion = await repository.DeleteOrderCascadeAsync(
            orderId,
            cancellationToken);
        return deletion switch
        {
            ProcedureRepository.ProcedureOrderDeletionDisposition.NotFound =>
                Results.NotFound(),
            _ => Results.Conflict(new
            {
                error = "Laboratory orders are retained to preserve specimens, results, acknowledgements, and audit evidence. Use an approved cancellation workflow when one is available."
            })
        };
    })
    .WithName("RejectProcedureOrderCascadeDeletion")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

var integrations = app.MapGroup("/api/integrations").WithTags("Integrations");
RequireAccessPermission(integrations, "admin", "super", "write");

integrations.MapGet("/laboratory-sources", async (
        ExternalLaboratorySourceRepository repository,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetSourcesAsync(cancellationToken)))
    .WithName("ListExternalLaboratorySources");

integrations.MapPost("/laboratory-sources", async (
        ExternalLaboratorySourceCreateRequest request,
        ExternalLaboratorySourceRepository repository,
        AuthRepository authRepository,
        StaffAccessContextService accessContextService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            await EnsureExternalLaboratorySourceFacilityScopeAsync(session, request.FacilityIds, accessContextService, cancellationToken);
            var source = await repository.CreateSourceAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/integrations/laboratory-sources/{source.SourceId}", source);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "An external laboratory source with that source ID already exists." });
        }
    })
    .WithName("CreateExternalLaboratorySource");

integrations.MapPut("/laboratory-sources/{sourceId}/facilities", async (
        string sourceId,
        ExternalLaboratorySourceFacilityGrantUpdateRequest request,
        ExternalLaboratorySourceRepository repository,
        AuthRepository authRepository,
        StaffAccessContextService accessContextService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            await EnsureExternalLaboratorySourceFacilityScopeAsync(session, request.FacilityIds, accessContextService, cancellationToken);
            var source = await repository.ReplaceFacilityGrantsAsync(sourceId, request, session.Username, cancellationToken);
            return source is null ? Results.NotFound() : Results.Ok(source);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("ReplaceExternalLaboratorySourceFacilityGrants");

integrations.MapPost("/laboratory-sources/{sourceId}/deactivate", async (
        string sourceId,
        ExternalLaboratorySourceDeactivateRequest request,
        ExternalLaboratorySourceRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var source = await repository.DeactivateSourceAsync(sourceId, request, session.Username, cancellationToken);
            return source is null
                ? Results.Conflict(new { error = "The external laboratory source does not exist or is already deactivated." })
                : Results.Ok(source);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("DeactivateExternalLaboratorySource");

integrations.MapGet("/outbox", async (
        IntegrationRepository repository,
        string? status,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetOutboxAsync(status, limit ?? 25, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = [exception.Message]
            });
        }
    })
    .WithName("ListIntegrationOutbox");

integrations.MapPost("/outbox", async (
        IntegrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        IntegrationOutboxQueueRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var message = await repository.QueueAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/integrations/outbox/{message.EventId}", message);
        }
        catch (IntegrationIdempotencyConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("QueueIntegrationOutbox");

integrations.MapPost("/outbox/{eventId:guid}/dispatch", async (
        IntegrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid eventId,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var dispatch = await repository.DispatchAsync(eventId, session.Username, cancellationToken);
        return dispatch is null ? Results.NotFound() : Results.Ok(dispatch);
    })
    .WithName("DispatchIntegrationOutbox");

integrations.MapGet("/outbox/{eventId:guid}/history", async (
        IntegrationRepository repository,
        Guid eventId,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetOutboxHistoryAsync(eventId, cancellationToken)))
    .WithName("GetIntegrationOutboxHistory");

integrations.MapPost("/outbox/{eventId:guid}/requeue", async (
        IntegrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid eventId,
        IntegrationOutboxRecoveryRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            return Results.Ok(await repository.RequeueQuarantinedAsync(eventId, request, session.Username, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    })
    .WithName("RequeueQuarantinedIntegrationOutbox");

integrations.MapPost("/inbox", async (
        IntegrationRepository repository,
        IntegrationInboxReceiveRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var receipt = await repository.ReceiveAsync(request, cancellationToken);
            return receipt.Duplicate
                ? Results.Ok(receipt)
                : Results.Created($"/api/integrations/inbox/{receipt.InboxId}", receipt);
        }
        catch (IntegrationIdempotencyConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("ReceiveIntegrationInbox");

integrations.MapGet("/inbox", async (IntegrationRepository repository, string? status, int? limit, CancellationToken token) =>
{
    try { return Results.Ok(await repository.GetInboxAsync(status, limit ?? 25, token)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [exception.Message] }); }
}).WithName("ListIntegrationInbox");

integrations.MapGet("/inbox/{inboxId:guid}/history", async (Guid inboxId, IntegrationRepository repository, CancellationToken token) =>
    Results.Ok(await repository.GetInboxHistoryAsync(inboxId, token))).WithName("GetIntegrationInboxHistory");

foreach (var action in new[] { "reconcile", "reject" })
    integrations.MapPost($"/inbox/{{inboxId:guid}}/{action}", async (Guid inboxId, IntegrationInboxDecisionRequest request, IntegrationRepository repository, AuthRepository authRepository, HttpContext context, CancellationToken token) =>
    {
        try { var session = await GetSessionFromHeaderAsync(authRepository, context, token); return Results.Ok(await repository.DecideInboxAsync(inboxId, action, request, session.Username, token)); }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [exception.Message] }); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
    }).WithName($"{action}IntegrationInbox");

var inventory = app.MapGroup("/api/inventory").WithTags("Inventory");
RequireAccessPermission(inventory, "inventory", "reporting", "view");

inventory.MapGet("/", async (
        InventoryRepository repository,
        CancellationToken cancellationToken) =>
    {
        return Results.Ok(await repository.GetInventoryAsync(cancellationToken));
    })
    .WithName("GetInventory");

inventory.MapGet("/cost-policies", async (InventoryCostPolicyRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
    .WithName("GetInventoryCostPolicies")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/receipt-cost-layers", async (int? lotId, int? limit, InventoryRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetReceiptCostLayersAsync(lotId, limit.GetValueOrDefault(50), cancellationToken)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReceiptCostLayers"] = [exception.Message] }); }
})
    .WithName("GetInventoryReceiptCostLayers")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/receipt-cost-layers/{layerId:guid}/applications", async (Guid layerId, InventoryRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetReceiptCostLayerApplicationsAsync(layerId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReceiptCostLayer"] = [exception.Message] }); }
})
    .WithName("GetInventoryReceiptCostLayerApplications")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/valuation-runs", async (int? limit, InventoryValuationRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetRunsAsync(limit ?? 30, cancellationToken)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryValuationRun"] = [exception.Message] }); }
})
    .WithName("GetInventoryValuationRuns")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/valuation-runs/{runId:guid}", async (Guid runId, InventoryValuationRepository repository, CancellationToken cancellationToken) =>
{
    var result = await repository.GetDetailAsync(runId, cancellationToken);
    return result is null ? Results.NotFound(new { error = "The inventory valuation run was not found." }) : Results.Ok(result);
})
    .WithName("GetInventoryValuationRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapPost("/valuation-runs", async (InventoryValuationRunCreateRequest request, InventoryValuationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var result = await repository.CreateAsync(request, session.Username, cancellationToken);
        return Results.Created($"/api/inventory/valuation-runs/{result.Run.RunId}", result);
    }
    catch (InventoryValuationPolicyMissingException exception) { return Results.Conflict(new { error = exception.Message, code = "inventory_cost_policy_missing" }); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryValuationRun"] = [exception.Message] }); }
})
    .WithName("CreateInventoryValuationRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/cost-policy-change-requests", async (InventoryCostPolicyChangeRequestCreateRequest request, InventoryCostPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/cost-policy-change-requests/{created.Request.RequestId}", created); } catch (InventoryCostPolicyChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryCostPolicy"] = [exception.Message] }); } })
    .WithName("CreateInventoryCostPolicyChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/cost-policy-change-requests/{requestId:guid}", async (Guid requestId, InventoryCostPolicyRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
    .WithName("GetInventoryCostPolicyChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
    inventory.MapPost($"/cost-policy-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryCostPolicyChangeRequestDecisionRequest request, InventoryCostPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
    { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); } catch (InventoryCostPolicyChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
        .WithName($"TransitionInventoryCostPolicyChangeRequest{action}")
        .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/accounting-integration-decision", async (InventoryAccountingIntegrationRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
    .WithName("GetInventoryAccountingIntegrationDecision")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

inventory.MapPost("/accounting-integration-change-requests", async (InventoryAccountingIntegrationChangeRequestCreateRequest request, InventoryAccountingIntegrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/accounting-integration-change-requests/{created.Request.RequestId}", created); }
    catch (InventoryAccountingIntegrationConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryAccountingIntegration"] = [exception.Message] }); }
})
    .WithName("CreateInventoryAccountingIntegrationChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapGet("/accounting-integration-change-requests/{requestId:guid}", async (Guid requestId, InventoryAccountingIntegrationRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
})
    .WithName("GetInventoryAccountingIntegrationChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
    inventory.MapPost($"/accounting-integration-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryAccountingIntegrationChangeRequestDecisionRequest request, InventoryAccountingIntegrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
    {
        try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); }
        catch (InventoryAccountingIntegrationConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
    })
        .WithName($"TransitionInventoryAccountingIntegrationChangeRequest{action}")
        .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapGet("/replenishment-policies", async (InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetCatalogAsync(cancellationToken)))
    .WithName("GetInventoryReplenishmentPolicies")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

inventory.MapGet("/replenishment-recommendations", async (InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetRecommendationsAsync(cancellationToken)))
    .WithName("GetInventoryReplenishmentRecommendations")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

inventory.MapPost("/replenishment-policy-change-requests", async (InventoryReplenishmentPolicyChangeRequestCreateRequest request, InventoryReplenishmentPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/replenishment-policy-change-requests/{created.Request.RequestId}", created); }
    catch (InventoryReplenishmentPolicyConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReplenishmentPolicy"] = [exception.Message] }); }
})
    .WithName("CreateInventoryReplenishmentPolicyChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapGet("/replenishment-policy-change-requests/{requestId:guid}", async (Guid requestId, InventoryReplenishmentPolicyRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetDetailAsync(requestId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
})
    .WithName("GetInventoryReplenishmentPolicyChangeRequest")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

foreach (var action in new[] { "submit", "approve", "reject", "activate", "cancel" })
    inventory.MapPost($"/replenishment-policy-change-requests/{{requestId:guid}}/{action}", async (Guid requestId, InventoryReplenishmentPolicyChangeRequestDecisionRequest request, InventoryReplenishmentPolicyRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
    {
        try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var result = action switch { "submit" => await repository.SubmitAsync(requestId, request, session.Username, cancellationToken), "approve" => await repository.ApproveAsync(requestId, request, session.Username, cancellationToken), "reject" => await repository.RejectAsync(requestId, request, session.Username, cancellationToken), "activate" => await repository.ActivateAsync(requestId, request, session.Username, cancellationToken), _ => await repository.CancelAsync(requestId, request, session.Username, cancellationToken) }; return Results.Ok(result); }
        catch (InventoryReplenishmentPolicyConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
    })
        .WithName($"TransitionInventoryReplenishmentPolicyChangeRequest{action}")
        .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapGet("/medication-catalog", async (InventoryRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetMedicationCatalogAsync(cancellationToken)))
    .WithName("GetInventoryMedicationCatalog")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapGet("/controlled-substances", async (InventoryRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetControlledSubstanceCatalogAsync(cancellationToken)))
    .WithName("GetInventoryControlledSubstanceCatalog")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPost("/controlled-custody-movements", async (InventoryControlledCustodyMovementRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var movement = await repository.CreateControlledCustodyMovementAsync(request, session.Username, cancellationToken);
        return Results.Created($"/api/inventory/controlled-custody-movements/{movement.Event.EventId}", movement);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyMovement"] = [exception.Message] });
    }
})
    .WithName("CreateInventoryControlledCustodyMovement")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapPost("/controlled-custody-movement-attestations", async (InventoryControlledCustodyMovementRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var attestation = await repository.CreateControlledCustodyMovementAttestationAsync(request, session.Username, cancellationToken);
        return Results.Created($"/api/inventory/controlled-custody-movement-attestations/{attestation.AttestationId}", attestation);
    }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyAttestation"] = [exception.Message] }); }
})
    .WithName("RequestInventoryControlledCustodyMovementAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/controlled-custody-movement-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    return Results.Ok(await repository.GetPendingControlledAttestationsAsync("custody_movement", session.Username, cancellationToken));
})
    .WithName("GetPendingInventoryControlledCustodyMovementAttestations")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPost("/controlled-custody-movement-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "custody_movement", session.Username, cancellationToken)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyAttestation"] = [exception.Message] }); }
})
    .WithName("ApproveInventoryControlledCustodyMovementAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/controlled-custody-lots/{lotId:int}/history", async (int lotId, InventoryRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetControlledCustodyLotHistoryAsync(lotId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
})
    .WithName("GetInventoryControlledCustodyLotHistory")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPost("/controlled-count-sessions", async (InventoryControlledCountSessionCreateRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var count = await repository.CreateControlledCountSessionAsync(request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-sessions/{count.SessionId}", count); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
    .WithName("CreateInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/controlled-count-sessions/{sessionId:guid}", async (Guid sessionId, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetControlledCountSessionAsync(sessionId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
    .WithName("GetInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/controlled-count-sessions", async (int? limit, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetControlledCountSessionsAsync(limit ?? 30, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
    .WithName("GetInventoryControlledCountSessions")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapPost("/controlled-count-sessions/{sessionId:guid}/submission-attestations", async (Guid sessionId, InventoryControlledCountSubmitRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var attestation = await repository.CreateControlledCountSubmissionAttestationAsync(sessionId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-attestations/{attestation.AttestationId}", attestation); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCountAttestation"] = [exception.Message] }); } })
    .WithName("RequestInventoryControlledCountSubmissionAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/controlled-count-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.GetPendingControlledAttestationsAsync("count_submit", session.Username, cancellationToken)); })
    .WithName("GetPendingInventoryControlledCountAttestations")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapPost("/controlled-count-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "count_submit", session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCountAttestation"] = [exception.Message] }); } })
    .WithName("ApproveInventoryControlledCountAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-sessions/{sessionId:guid}/submit", async (Guid sessionId, InventoryControlledCountSubmitRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitControlledCountSessionAsync(sessionId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCount"] = [exception.Message] }); } })
    .WithName("SubmitInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPut("/controlled-count-discrepancies/{discrepancyId:guid}/investigation", async (Guid discrepancyId, InventoryControlledDiscrepancyInvestigationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.InvestigateControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
    .WithName("InvestigateInventoryControlledCountDiscrepancy")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/correction-attestations", async (Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var attestation = await repository.CreateControlledDiscrepancyCorrectionAttestationAsync(discrepancyId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-count-attestations/{attestation.AttestationId}", attestation); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancyAttestation"] = [exception.Message] }); } })
    .WithName("RequestInventoryControlledDiscrepancyCorrectionAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/controlled-count-discrepancy-correction-attestations/pending", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.GetPendingControlledAttestationsAsync("discrepancy_correction", session.Username, cancellationToken)); })
    .WithName("GetPendingInventoryControlledDiscrepancyCorrectionAttestations")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapPost("/controlled-count-discrepancy-correction-attestations/{attestationId:guid}/approve", async (Guid attestationId, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveControlledAttestationAsync(attestationId, "discrepancy_correction", session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancyAttestation"] = [exception.Message] }); } })
    .WithName("ApproveInventoryControlledDiscrepancyCorrectionAttestation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/corrections", async (Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var correction = await repository.CorrectControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken); return Results.Created($"/api/inventory/controlled-custody-movements/{correction.Event.EventId}", correction); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
    .WithName("CorrectInventoryControlledCountDiscrepancy")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/close", async (Guid discrepancyId, InventoryControlledDiscrepancyCloseRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CloseControlledCountDiscrepancyAsync(discrepancyId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledDiscrepancy"] = [exception.Message] }); } })
    .WithName("CloseInventoryControlledCountDiscrepancy")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-locations", async (InventoryControlledLocationMutationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/inventory/controlled-locations", await repository.CreateControlledLocationAsync(request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledLocation"] = [exception.Message] }); } })
    .WithName("CreateInventoryControlledLocation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapPut("/items/{itemId:int}/controlled-classification", async (int itemId, InventoryControlledSubstanceClassificationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateControlledSubstanceClassificationAsync(itemId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledClassification"] = [exception.Message] }); } })
    .WithName("UpdateInventoryControlledSubstanceClassification")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/items/{itemId:int}/controlled-classification/history", async (int itemId, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetControlledSubstanceClassificationHistoryAsync(itemId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
    .WithName("GetInventoryControlledSubstanceClassificationHistory")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPut("/items/{itemId:int}/medication-link", async (
        int itemId,
        InventoryMedicationLinkUpdateRequest request,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var link = await repository.UpdateMedicationLinkAsync(itemId, request, session.Username, cancellationToken);
            return link is null ? Results.NotFound() : Results.Ok(link);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryMedicationLink"] = [exception.Message] });
        }
    })
    .WithName("UpdateInventoryMedicationLink")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/items/{itemId:int}/medication-link/history", async (int itemId, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetMedicationLinkHistoryAsync(itemId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } })
    .WithName("GetInventoryMedicationLinkHistory")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapDelete("/items/{itemId:int}/medication-link", async (int itemId, [FromBody] InventoryMedicationLinkUnlinkRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UnlinkMedicationAsync(itemId, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryMedicationLink"] = [exception.Message] }); } })
    .WithName("UnlinkInventoryMedicationLink")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapPost("/prescription-dispensations", async (
        InventoryPrescriptionDispenseRequest request,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var dispense = await repository.DispensePrescriptionAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/inventory/prescription-dispensations/{dispense.Sale.SaleId}", dispense);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPrescriptionDispense"] = [exception.Message] });
        }
    })
    .WithName("DispenseInventoryPrescription")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

inventory.MapPost("/transactions", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryTransactionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.CreateTransactionAsync(request, session.Username, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/transactions/{mutation.Transaction.TransactionId}", mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("CreateInventoryTransaction")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/transfers", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryTransferCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.CreateTransferAsync(request, session.Username, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/transfers/{mutation.TransferId}", mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["inventoryTransfer"] = [exception.Message]
            });
        }
    })
    .WithName("CreateInventoryTransfer")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "transfers", "write"));

inventory.MapGet("/vendors", async (
        InventoryRepository repository,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetVendorsAsync(cancellationToken)))
    .WithName("GetInventoryVendors")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

inventory.MapPost("/vendors", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryVendorCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var vendor = await repository.CreateVendorAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/inventory/vendors/{vendor.VendorId}", vendor);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryVendor"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryVendor")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapGet("/purchase-requisitions", async (InventoryRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetPurchaseRequisitionsAsync(cancellationToken)))
    .WithName("GetInventoryPurchaseRequisitions")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "view"));

inventory.MapPost("/purchase-requisitions", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryPurchaseRequisitionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var requisition = await repository.CreatePurchaseRequisitionAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/inventory/purchase-requisitions/{requisition!.RequisitionId}", requisition);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryPurchaseRequisition")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapPost("/purchase-requisitions/{requisitionId:guid}/submit", async (
        Guid requisitionId,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var requisition = await repository.SubmitPurchaseRequisitionAsync(requisitionId, session.Username, cancellationToken);
            return requisition is null ? Results.NotFound() : Results.Ok(requisition);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
        }
    })
    .WithName("SubmitInventoryPurchaseRequisition")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapPost("/purchase-requisitions/{requisitionId:guid}/decisions/{decision}", async (
        Guid requisitionId,
        string decision,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryPurchaseRequisitionDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        if (!string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase) && !string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var requisition = await repository.DecidePurchaseRequisitionAsync(requisitionId, string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase), request, session.Username, cancellationToken);
            return requisition is null ? Results.NotFound() : Results.Ok(requisition);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseRequisition"] = [exception.Message] });
        }
    })
    .WithName("DecideInventoryPurchaseRequisition")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapPost("/purchase-receipts", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryPurchaseReceiptCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var receipt = await repository.CreatePurchaseReceiptAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/inventory/purchase-receipts/{receipt.ReceiptId}", receipt);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPurchaseReceipt"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryPurchaseReceipt")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapPost("/returns", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryTransactionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.CreateTransactionAsync(request with { TransactionType = "return" }, session.Username, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Created($"/api/inventory/returns/{mutation.Transaction.TransactionId}", mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryReturn"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryReturn")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "purchases", "write"));

inventory.MapPost("/patient-sales", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryPatientSaleCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var sale = await repository.CreatePatientSaleAsync(request, session.Username, cancellationToken);
            return sale is null ? Results.NotFound() : Results.Created($"/api/inventory/patient-sales/{sale.SaleId}", sale);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPatientSale"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryPatientSale")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

inventory.MapPost("/patient-sales/allocate", async (InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, InventoryPatientSaleAllocationCreateRequest request, CancellationToken cancellationToken) =>
    {
        try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/inventory/patient-sales/allocate", await repository.CreatePatientSaleAllocationAsync(request, session.Username, cancellationToken)); }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryPatientSaleAllocation"] = [exception.Message] }); }
    })
    .WithName("AllocateInventoryPatientSale")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "sales", "write"));

inventory.MapPut("/lots/{lotId:int}", async (
        int lotId,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryLotMetadataUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mutation = await repository.UpdateLotMetadataAsync(lotId, request, session.Username, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryLot"] = [exception.Message] });
        }
    })
    .WithName("UpdateInventoryLotMetadata")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/lots/{lotId:int}/metadata-history", async (
        int lotId,
        InventoryRepository repository,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetLotMetadataHistoryAsync(lotId, cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    })
    .WithName("GetInventoryLotMetadataHistory")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPost("/lots/{lotId:int}/destructions", async (
        int lotId,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryLotDestructionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var destruction = await repository.DestroyLotAsync(lotId, request, session.Username, cancellationToken);
            return destruction is null
                ? Results.NotFound()
                : Results.Created($"/api/inventory/lots/{lotId}/destructions/{destruction.DestructionId}", destruction);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryLotDestruction"] = [exception.Message] });
        }
    })
    .WithName("DestroyInventoryLot")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "destruction", "write"));

inventory.MapPost("/lots/{lotId:int}/expiry-dispositions", async (
        int lotId,
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryExpiryDispositionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var disposition = await repository.CreateExpiryDispositionAsync(lotId, request, session.Username, cancellationToken);
            return disposition is null ? Results.NotFound() : Results.Created($"/api/inventory/lots/{lotId}/expiry-dispositions/{disposition.DispositionId}", disposition);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryExpiryDisposition"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryExpiryDisposition")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "destruction", "write"));

inventory.MapPost("/count-reconciliations", async (
        InventoryRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        InventoryCountReconciliationCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var reconciliation = await repository.CreateCountReconciliationAsync(request, session.Username, cancellationToken);
            return reconciliation is null
                ? Results.NotFound()
                : Results.Created($"/api/inventory/count-reconciliations/{reconciliation.ReconciliationId}", reconciliation);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["inventoryCount"] = [exception.Message] });
        }
    })
    .WithName("CreateInventoryCountReconciliation")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/activity", async (
        InventoryRepository repository,
        DateOnly? from,
        DateOnly? to,
        int? facilityId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetActivityReportAsync(from, to, facilityId, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["inventoryActivity"] = [exception.Message]
            });
        }
    })
    .WithName("GetInventoryActivityReport");

inventory.MapGet("/activity/export", async (
        InventoryRepository repository,
        DateOnly? from,
        DateOnly? to,
        int? facilityId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var csv = await repository.GetActivityReportCsvAsync(from, to, facilityId, cancellationToken);
            return Results.File(Encoding.UTF8.GetBytes(csv), contentType: "text/csv", fileDownloadName: "avenchart-inventory-activity.csv");
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["inventoryActivity"] = [exception.Message]
            });
        }
    })
    .WithName("ExportInventoryActivityReport");

var billing = app.MapGroup("/api/billing").WithTags("Billing");
RequireAccessPermission(billing, "acct", "bill", "view");

billing.MapGet("/statements/batch", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var statementBatch = await repository.GetStatementBatchAsync(limit ?? 10, cancellationToken);
        return Results.Ok(statementBatch);
    })
    .WithName("GetBillingStatementBatch");

billing.MapGet("/statements/batch/package.zip", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var package = await repository.GetStatementBatchPackageAsync(limit ?? 10, cancellationToken);
        return Results.File(
            package.Content,
            contentType: "application/zip",
            fileDownloadName: package.FileName);
    })
    .WithName("DownloadBillingStatementBatchPackage");

billing.MapPost("/statements/batch/delivery-manifest", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var delivery = await repository.PrepareStatementBatchDeliveryAsync(limit ?? 10, cancellationToken);
        return Results.Ok(delivery);
    })
    .WithName("PrepareBillingStatementBatchDeliveryManifest");

billing.MapPost("/statements/batch/dispatch", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var dispatch = await repository.DispatchStatementBatchDeliveryAsync(limit ?? 10, cancellationToken);
        return Results.Ok(dispatch);
    })
    .WithName("DispatchBillingStatementBatchDelivery")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapGet("/statements/batch/dispatch-history", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetStatementDeliveryAuditHistoryAsync(limit ?? 10, cancellationToken);
        return Results.Ok(history);
    })
    .WithName("GetBillingStatementDeliveryAuditHistory");

billing.MapPost("/statements/batch/portal-delivery", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var delivery = await repository.DeliverStatementBatchToPortalAsync(limit ?? 10, cancellationToken);
        return Results.Ok(delivery);
    })
    .WithName("DeliverBillingStatementBatchToPortal")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/statements/batch/email-outbox", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var outbox = await repository.QueueStatementBatchEmailOutboxAsync(limit ?? 10, cancellationToken);
        return Results.Ok(outbox);
    })
    .WithName("QueueBillingStatementBatchEmailOutbox")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapGet("/collections/work-queue", async (
        BillingRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var workQueue = await repository.GetCollectionsWorkQueueAsync(limit ?? 10, cancellationToken);
        return Results.Ok(workQueue);
    })
    .WithName("GetBillingCollectionsWorkQueue");

billing.MapPost("/collections/follow-ups", async (
        BillingRepository repository,
        CollectionsFollowUpCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateCollectionsFollowUpAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Collections follow-up could not be created from the supplied patient and account state.")
            : Results.Created($"/api/messages/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingCollectionsFollowUp")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapGet("/{patientId}", async (
        BillingRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var patientBilling = await repository.GetForPatientAsync(patientId, cancellationToken);
        return patientBilling is null ? Results.NotFound() : Results.Ok(patientBilling);
    })
    .WithName("GetBillingForPatient");

billing.MapGet("/{patientId}/statement.pdf", async (
        BillingRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var export = await repository.GetStatementPdfAsync(patientId, cancellationToken);
        return export is null
            ? Results.NotFound()
            : Results.File(
                export.Value.Content,
                contentType: "application/pdf",
                fileDownloadName: export.Value.FileName);
    })
    .WithName("DownloadBillingStatementPdf");

billing.MapGet("/charge-templates/{templateId}", (
        BillingRepository repository,
        string templateId) =>
    {
        var template = repository.GetChargeTemplate(templateId);
        return template is null ? Results.NotFound() : Results.Ok(template);
    })
    .WithName("GetBillingChargeTemplate")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "view"));

billing.MapPost("/lines", async (
        BillingRepository repository,
        BillingLineCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.CreateLineAsync(request, cancellationToken);
            return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/lines/{mutation.Id}", mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("CreateBillingLine")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPut("/lines/{billingLineId}", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.UpdateLineAsync(billingLineId, request, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("UpdateBillingLine")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPut("/lines/{billingLineId}/status", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.UpdateLineStatusAsync(billingLineId, request, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (EncounterLockConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = "encounter_locked" });
        }
    })
    .WithName("UpdateBillingLineStatus")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapDelete("/lines/{billingLineId}", (string billingLineId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Billing-line deletion is not available",
            detail: "Financial evidence is retained. Use the line-status workflow to deactivate a line instead."))
    .WithName("RetireBillingLineDeletion")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims", async (
        BillingRepository repository,
        BillingClaimCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateClaimAsync(request, cancellationToken);
        return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/claims/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingClaimStatus")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPut("/claims/{claimId}/status", async (
        BillingRepository repository,
        string claimId,
        BillingClaimStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateClaimStatusAsync(claimId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingClaimStatus")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/scrub", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ScrubClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ScrubBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/generate", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.GenerateClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("GenerateBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/resubmit", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ResubmitClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ResubmitBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/deny", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.DenyClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DenyBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/clear", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ClearClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ClearBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/claims/{claimId}/adjudicate", async (
        BillingRepository repository,
        string claimId,
        IOptions<RuntimeSafetyOptions> runtimeSafety,
        CancellationToken cancellationToken) =>
    {
        if (RuntimeSafetyPolicy.GetSyntheticFinancialMutationBlocker(runtimeSafety.Value) is { } blocker)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Generated claim adjudication is disabled",
                detail: blocker);
        }

        var mutation = await repository.AdjudicateClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("AdjudicateBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapDelete("/claims/{claimId}", (string claimId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Claim deletion is not available",
            detail: "Financial evidence is retained. Use a governed claim-status transition instead."))
    .WithName("RetireBillingClaimDeletion")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/payments/patient-payments", async (
        BillingRepository repository,
        BillingPatientPaymentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreatePatientPaymentAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient payment could not be posted for the supplied patient and encounter.")
            : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingPatientPayment")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/payments/patient-refunds", async (
        BillingRepository repository,
        BillingPatientRefundCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreatePatientRefundAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient refund could not be posted for the supplied patient and encounter.")
            : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingPatientRefund")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/payments/insurance-payments", async (
        BillingRepository repository,
        BillingInsurancePaymentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateInsurancePaymentAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Insurance payment could not be posted for the supplied patient, encounter, and payer.")
            : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingInsurancePayment")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/payments/insurance-reversals", async (
        BillingRepository repository,
        BillingInsuranceReversalCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateInsuranceReversalAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Insurance reversal could not be posted for the supplied patient, encounter, and payer.")
            : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingInsuranceReversal")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/payments/adjustment-reversals", async (
        BillingRepository repository,
        BillingAdjustmentReversalCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateAdjustmentReversalAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Adjustment reversal could not be posted for the supplied patient, encounter, and payer.")
            : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingAdjustmentReversal")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPost("/eob-batches/import", async (
        BillingRepository repository,
        BillingEobBatchImportRequest request,
        IOptions<RuntimeSafetyOptions> runtimeSafety,
        CancellationToken cancellationToken) =>
    {
        if (RuntimeSafetyPolicy.GetSyntheticFinancialMutationBlocker(runtimeSafety.Value) is { } blocker)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Generated EOB import is disabled",
                detail: blocker);
        }

        var mutation = await repository.ImportEobBatchAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("EOB batch could not be imported for the supplied patient.")
            : Results.Created("/api/billing/eob-batches/import", mutation);
    })
    .WithName("ImportBillingEobBatch")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapGet("/payments/{activityId}/receipt.pdf", async (
        BillingRepository repository,
        string activityId,
        CancellationToken cancellationToken) =>
    {
        var export = await repository.GetPaymentReceiptPdfAsync(activityId, cancellationToken);
        return export is null
            ? Results.NotFound()
            : Results.File(
                export.Value.Content,
                contentType: "application/pdf",
                fileDownloadName: export.Value.FileName);
    })
    .WithName("DownloadBillingPaymentReceiptPdf");

billing.MapPut("/payments/{activityId}/void", async (
        BillingRepository repository,
        string activityId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.VoidPaymentAsync(activityId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("VoidBillingPaymentPosting")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapDelete("/payments/{activityId}", (string activityId) =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Payment deletion is not available",
            detail: "Financial evidence is retained. Use the payment void workflow instead."))
    .WithName("RetireBillingPaymentDeletion")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

var administration = app.MapGroup("/api/administration").WithTags("Administration");
RequireAccessPermission(administration, "admin", "acl", "write");
administration.MapAzureOperationsEndpoints();

administration.MapGet("/access-context-grants/{username}", async (
        string username,
        StaffAccessContextService accessContextService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await accessContextService.GetPrincipalGrantAsync(username, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
    })
    .WithName("GetStaffAccessContextGrant");

administration.MapPut("/access-context-grants/{username}", async (
        string username,
        AuthAccessContextGrantUpdateRequest request,
        StaffAccessContextService accessContextService,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            return Results.Ok(await accessContextService.UpdatePrincipalGrantAsync(
                username,
                request,
                session.Username,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["accessContextGrant"] = [exception.Message]
            });
        }
    })
    .WithName("UpdateStaffAccessContextGrant");

administration.MapGet("/external-identity-mappings", async (
        string? providerId,
        ExternalIdentityMappingRepository repository,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetMappingsAsync(providerId, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["providerId"] = [exception.Message]
            });
        }
    })
    .WithName("ListExternalIdentityMappings");

administration.MapPost("/external-identity-mappings", async (
        ExternalIdentityMappingCreateRequest request,
        ExternalIdentityMappingRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mapping = await repository.CreateAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/administration/external-identity-mappings/{mapping.MappingId}", mapping);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "An active mapping already exists for this provider subject or local account." });
        }
    })
    .WithName("CreateExternalIdentityMapping");

administration.MapPost("/external-identity-mappings/{mappingId:guid}/deactivate", async (
        Guid mappingId,
        ExternalIdentityMappingDeactivateRequest request,
        ExternalIdentityMappingRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mapping = await repository.DeactivateAsync(mappingId, request, session.Username, cancellationToken);
            return mapping is null
                ? Results.Conflict(new { error = "The mapping does not exist or is already deactivated." })
                : Results.Ok(mapping);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("DeactivateExternalIdentityMapping");

administration.MapGet("/patient-portal-external-identity-mappings", async (
        string? providerId,
        PatientPortalExternalIdentityMappingRepository repository,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetMappingsAsync(providerId, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["providerId"] = [exception.Message]
            });
        }
    })
    .WithName("ListPatientPortalExternalIdentityMappings");

administration.MapPost("/patient-portal-external-identity-mappings", async (
        PatientPortalExternalIdentityMappingCreateRequest request,
        PatientPortalExternalIdentityMappingRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mapping = await repository.CreateAsync(request, session.Username, cancellationToken);
            return Results.Created($"/api/administration/patient-portal-external-identity-mappings/{mapping.MappingId}", mapping);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "An active portal mapping already exists for this provider subject or patient." });
        }
    })
    .WithName("CreatePatientPortalExternalIdentityMapping");

administration.MapPost("/patient-portal-external-identity-mappings/{mappingId:guid}/deactivate", async (
        Guid mappingId,
        PatientPortalExternalIdentityMappingDeactivateRequest request,
        PatientPortalExternalIdentityMappingRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var mapping = await repository.DeactivateAsync(mappingId, request, session.Username, cancellationToken);
            return mapping is null
                ? Results.Conflict(new { error = "The portal mapping does not exist or is already deactivated." })
                : Results.Ok(mapping);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("DeactivatePatientPortalExternalIdentityMapping");

var delegatedConfiguration = app.MapGroup("/api/configuration-delegation").WithTags("Configuration delegation");
delegatedConfiguration.AddEndpointFilter(StaffAccessContextFilter("delegated-configuration"));
delegatedConfiguration.MapPost("/practice-settings/{key}/change-requests", async (string key, PracticeSettingChangeRequestCreateRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    if (!session.Authenticated) return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
    try { var response = await repository.CreateDelegatedPracticeSettingChangeRequestAsync(key, request, session.Username, cancellationToken); return Results.Created($"/api/administration/practice-setting-change-requests/{response.Request.RequestId}", response); }
    catch (UnauthorizedAccessException exception) { return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden); }
    catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("CreateDelegatedPracticeSettingChangeRequest");
delegatedConfiguration.MapPost("/practice-setting-change-requests/{requestId:guid}/submit", async (Guid requestId, PracticeSettingChangeRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    if (!session.Authenticated) return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
    try { return Results.Ok(await repository.SubmitDelegatedPracticeSettingChangeRequestAsync(requestId, request, session.Username, cancellationToken)); }
    catch (UnauthorizedAccessException exception) { return Results.Json(new { error = exception.Message }, statusCode: StatusCodes.Status403Forbidden); }
    catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("SubmitDelegatedPracticeSettingChangeRequest");

administration.MapGet("/experience-baseline", () =>
    Results.Ok(ExperienceBaselineCatalog.Build()))
    .WithName("GetExperienceBaseline");

administration.MapGet("/identity-provider/readiness", () =>
    Results.Ok(IdentityProviderCatalog.Build(
        app.Services.GetRequiredService<IOptions<IdentityProviderOptions>>().Value,
        app.Environment.IsDevelopment())))
    .WithName("GetIdentityProviderReadiness");

administration.MapGet("/configuration-catalog", () => Results.Ok(new ConfigurationCatalogResponse([
    new("practice.identity", "Practice identity and contact", "Local implemented", "Practice administrator", "Required non-blank practice name", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
    new("practice.default-facility", "Default facility", "Local implemented", "Practice administrator", "Must reference a positive facility identifier", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
    new("practice.locale-timezone", "Locale and time zone", "Local implemented", "Practice and operations owners", "Supported IANA or Windows time-zone identifier", "Stale-safe governed change-request activation enabled; direct endpoint retained for compatibility"),
    new("coding.catalogs", "Coding catalogs", "Local implemented", "Practice administrator", "Unique key/order, bounded modifiers, immutable historical key", "Create, edit, and activation state enabled"),
    new("forms.option-lists", "Form option lists", "Local implemented", "Practice administrator", "Ordered option key, label, value, default, and activation metadata", "Create, edit, and activation state enabled"),
    new("scheduling.defaults", "Appointment defaults", "Owner-gated", "Operations owner", "Facility/provider compatibility and bounded values", "No mutable source selected"),
    new("clinical.templates", "Clinical forms and templates", "Clinical-governed", "Clinical owner", "Versioned content and activation date", "No mutable source selected"),
    new("integrations.secrets", "Security and integration settings", "Deployment-only", "Security and operations owners", "Environment validation; never return secrets", "Excluded from application API")
]))).WithName("GetConfigurationCatalog");

administration.MapGet("/runtime-diagnostics", (RuntimeDiagnostics diagnostics) =>
    Results.Ok(diagnostics.GetSnapshot()))
    .WithName("GetRuntimeDiagnostics");
administration.MapGet("/authorization-policy-catalog", (
        string? query,
        string? gap,
        int? offset,
        int? limit) =>
    {
        try
        {
            return Results.Ok(AuthorizationPolicyCatalog.Search(
                query,
                gap,
                offset ?? 0,
                limit ?? 8));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["authorizationPolicies"] = [exception.Message],
            });
        }
    })
    .WithName("GetAuthorizationPolicyCatalog");

administration.MapGet("/practice-settings", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetPracticeSettingsAsync(cancellationToken))).WithName("GetPracticeSettings");
administration.MapGet("/practice-settings/registry", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetPracticeSettingRegistryAsync(cancellationToken))).WithName("GetPracticeSettingRegistry");
administration.MapPost("/configuration-packages/export", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    return Results.Ok(await repository.ExportConfigurationPackageAsync(session.Username, cancellationToken));
}).WithName("ExportConfigurationPackage");
administration.MapPost("/configuration-packages/dry-run", async (ConfigurationPackageDryRunRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    return Results.Ok(await repository.DryRunConfigurationPackageAsync(request, session.Username, cancellationToken));
}).WithName("DryRunConfigurationPackage");
administration.MapGet("/configuration-package-import-requests", async (string? status, string? kind, int? offset, int? limit, AdministrationRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetConfigurationPackageImportRequestsAsync(status, kind, offset ?? 0, limit ?? 12, cancellationToken)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("GetConfigurationPackageImportRequests");
administration.MapGet("/configuration-package-import-requests/{requestId:guid}", async (Guid requestId, AdministrationRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetConfigurationPackageImportRequestAsync(requestId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
}).WithName("GetConfigurationPackageImportRequest");
administration.MapPost("/configuration-package-import-requests", async (ConfigurationPackageImportRequestCreateRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var response = await repository.CreateConfigurationPackageImportRequestAsync(request, session.Username, cancellationToken);
        return Results.Created($"/api/administration/configuration-package-import-requests/{response.Request.RequestId}", response);
    }
    catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("CreateConfigurationPackageImportRequest");
administration.MapPost("/configuration-package-import-requests/{requestId:guid}/compensating-rollback", async (Guid requestId, ConfigurationPackageImportRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var response = await repository.CreateConfigurationPackageCompensatingRollbackAsync(requestId, request.Note ?? string.Empty, session.Username, cancellationToken);
        return Results.Created($"/api/administration/configuration-package-import-requests/{response.Request.RequestId}", response);
    }
    catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("CreateConfigurationPackageCompensatingRollback");
administration.MapPost("/configuration-package-import-requests/{requestId:guid}/{action}", async (Guid requestId, string action, ConfigurationPackageImportRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var response = action switch
        {
            "submit" => await repository.SubmitConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
            "approve" => await repository.ApproveConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
            "reject" => await repository.RejectConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
            "activate" => await repository.ActivateConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
            "cancel" => await repository.CancelConfigurationPackageImportRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken),
            _ => throw new ArgumentException("The requested import-request action is not supported."),
        };
        return Results.Ok(response);
    }
    catch (ConfigurationPackageImportRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("TransitionConfigurationPackageImportRequest");
administration.MapGet("/practice-setting-delegations", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetPracticeSettingDelegationsAsync(cancellationToken))).WithName("GetPracticeSettingDelegations");
administration.MapPost("/practice-setting-delegations", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, PracticeSettingDelegationCreateRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/administration/practice-setting-delegations", await repository.GrantPracticeSettingDelegationAsync(request, session.Username, cancellationToken)); } catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GrantPracticeSettingDelegation");
administration.MapPost("/practice-setting-delegations/{delegationId:guid}/revoke", async (Guid delegationId, PracticeSettingChangeRequestDecisionRequest request, AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RevokePracticeSettingDelegationAsync(delegationId, request.Note, session.Username, cancellationToken)); } catch (PracticeSettingChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RevokePracticeSettingDelegation");
administration.MapGet("/practice-settings/effective", async (AdministrationRepository repository, int? facilityId, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetEffectivePracticeSettingsAsync(facilityId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("GetEffectivePracticeSettings");
administration.MapGet("/practice-settings/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetPracticeSettingHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingHistory");
administration.MapGet("/practice-setting-change-requests", async (
        AdministrationRepository repository,
        string? settingKey,
        string? status,
        int? offset,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetPracticeSettingChangeRequestsAsync(
                settingKey,
                status,
                offset ?? 0,
                limit ?? 8,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequests"] = [exception.Message],
            });
        }
    })
    .WithName("GetPracticeSettingChangeRequests");
administration.MapGet("/practice-setting-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetPracticeSettingChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingChangeRequest");
administration.MapGet("/practice-setting-change-requests/{requestId:guid}/impact-preview", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetPracticeSettingChangeRequestImpactPreviewAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetPracticeSettingChangeRequestImpactPreview");
administration.MapPost("/practice-settings/{key}/change-requests", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string key,
        PracticeSettingChangeRequestCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var response = await repository.CreatePracticeSettingChangeRequestAsync(
                key,
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/administration/practice-setting-change-requests/{response.Request.RequestId}",
                response);
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting change request conflicts with current state",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("CreatePracticeSettingChangeRequest");

administration.MapPost("/practice-setting-change-requests/{requestId:guid}/submit", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid requestId,
        PracticeSettingChangeRequestDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.SubmitPracticeSettingChangeRequestAsync(
                requestId,
                request.Note,
                request.ExpectedVersion,
                session.Username,
                cancellationToken));
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting change request is stale",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("SubmitPracticeSettingChangeRequest");

administration.MapPost("/practice-setting-change-requests/{requestId:guid}/approve", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid requestId,
        PracticeSettingChangeRequestDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.ApprovePracticeSettingChangeRequestAsync(
                requestId,
                request.Note,
                request.ExpectedVersion,
                session.Username,
                cancellationToken));
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting change request is stale",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("ApprovePracticeSettingChangeRequest");

administration.MapPost("/practice-setting-change-requests/{requestId:guid}/reject", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid requestId,
        PracticeSettingChangeRequestDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.RejectPracticeSettingChangeRequestAsync(
                requestId,
                request.Note,
                request.ExpectedVersion,
                session.Username,
                cancellationToken));
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting change request is stale",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("RejectPracticeSettingChangeRequest");

administration.MapPost("/practice-setting-change-requests/{requestId:guid}/activate", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid requestId,
        PracticeSettingChangeRequestDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.ActivatePracticeSettingChangeRequestAsync(
                requestId,
                request.Note,
                request.ExpectedVersion,
                session.Username,
                cancellationToken));
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting activation is stale",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("ActivatePracticeSettingChangeRequest");

administration.MapPost("/practice-setting-change-requests/{requestId:guid}/cancel", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid requestId,
        PracticeSettingChangeRequestDecisionRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.CancelPracticeSettingChangeRequestAsync(
                requestId,
                request.Note,
                request.ExpectedVersion,
                session.Username,
                cancellationToken));
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting change request is stale",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("CancelPracticeSettingChangeRequest");

administration.MapDelete("/practice-setting-change-requests/{requestId:guid}/test-fixture", async (
        AdministrationRepository repository,
        Guid requestId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await repository.DeletePracticeSettingChangeRequestTestFixtureAsync(
                requestId,
                cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (PracticeSettingChangeRequestConflictException exception)
        {
            return Results.Problem(
                title: "Practice-setting fixture is still active",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["changeRequest"] = [exception.Message],
            });
        }
    })
    .WithName("DeletePracticeSettingChangeRequestTestFixture");

administration.MapGet("/coding-catalogs", async (AdministrationRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetCodingCatalogsAsync(cancellationToken))).WithName("GetCodingCatalogs");
administration.MapGet("/coding-catalogs/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetCodingCatalogHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetCodingCatalogHistory");
administration.MapPost("/coding-catalogs", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CodingCatalogCreateRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/administration/coding-catalogs/" + request.Key, await repository.CreateCodingCatalogAsync(request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateCodingCatalog");
administration.MapPut("/coding-catalogs/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, CodingCatalogUpdateRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateCodingCatalogAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpdateCodingCatalog");
administration.MapPost("/coding-catalogs/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackCodingCatalogAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackCodingCatalog");
administration.MapGet("/coding-catalog-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetCodingCatalogChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetCodingCatalogChangeRequests");
administration.MapPost("/coding-catalog-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, CodingCatalogChangeRequestCreateRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateCodingCatalogChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/coding-catalog-change-requests/{created.Request.RequestId}", created); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateCodingCatalogChangeRequest");
administration.MapGet("/coding-catalog-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetCodingCatalogChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetCodingCatalogChangeRequest");
administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitCodingCatalogChangeRequest");
administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveCodingCatalogChangeRequest");
administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectCodingCatalogChangeRequest");
administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateCodingCatalogChangeRequest");
administration.MapPost("/coding-catalog-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, CodingCatalogChangeRequestDecisionRequest request, CancellationToken cancellationToken) =>
{ try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelCodingCatalogChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (CodingCatalogChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelCodingCatalogChangeRequest");

administration.MapGet("/form-layouts", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetFormLayoutsAsync(cancellationToken))).WithName("GetFormLayouts");
administration.MapGet("/form-layouts/{key}", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayout");
administration.MapGet("/form-layouts/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayoutHistory");
administration.MapPut("/form-layouts/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, FormLayoutMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayout");
administration.MapPut("/form-layouts/{layoutKey}/groups/{groupKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string layoutKey, string groupKey, FormLayoutGroupMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutGroupAsync(layoutKey, groupKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayoutGroup");
administration.MapPut("/form-layouts/{layoutKey}/fields/{fieldKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string layoutKey, string fieldKey, FormLayoutFieldMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormLayoutFieldAsync(layoutKey, fieldKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormLayoutField");
administration.MapPost("/form-layouts/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackFormLayoutAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackFormLayout");

administration.MapGet("/form-layout-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetFormLayoutChangeRequests");
administration.MapPost("/form-layout-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, FormLayoutChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateFormLayoutChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/form-layout-change-requests/{created.Request.RequestId}", created); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateFormLayoutChangeRequest");
administration.MapGet("/form-layout-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormLayoutChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormLayoutChangeRequest");
administration.MapPost("/form-layout-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitFormLayoutChangeRequest");
administration.MapPost("/form-layout-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveFormLayoutChangeRequest");
administration.MapPost("/form-layout-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectFormLayoutChangeRequest");
administration.MapPost("/form-layout-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateFormLayoutChangeRequest");
administration.MapPost("/form-layout-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormLayoutChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelFormLayoutChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormLayoutChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelFormLayoutChangeRequest");

administration.MapGet("/form-option-lists", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetFormOptionListsAsync(cancellationToken))).WithName("GetFormOptionLists");
administration.MapGet("/form-option-lists/{key}", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionList");
administration.MapGet("/form-option-lists/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionListHistory");
administration.MapPut("/form-option-lists/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, FormOptionListMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormOptionListAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormOptionList");
administration.MapPut("/form-option-lists/{listKey}/options/{optionKey}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string listKey, string optionKey, FormOptionValueMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertFormOptionValueAsync(listKey, optionKey, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertFormOptionValue");
administration.MapPost("/form-option-lists/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackFormOptionListAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackFormOptionList");

administration.MapGet("/form-option-list-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListChangeRequestsAsync(status, offset ?? 0, limit ?? 25, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetFormOptionListChangeRequests");
administration.MapPost("/form-option-list-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, FormOptionListChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateFormOptionListChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/form-option-list-change-requests/{created.Request.RequestId}", created); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateFormOptionListChangeRequest");
administration.MapGet("/form-option-list-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetFormOptionListChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetFormOptionListChangeRequest");
administration.MapPost("/form-option-list-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitFormOptionListChangeRequest");
administration.MapPost("/form-option-list-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveFormOptionListChangeRequest");
administration.MapPost("/form-option-list-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectFormOptionListChangeRequest");
administration.MapPost("/form-option-list-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateFormOptionListChangeRequest");
administration.MapPost("/form-option-list-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, FormOptionListChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelFormOptionListChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (FormOptionListChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelFormOptionListChangeRequest");

administration.MapGet("/clinical-alert-rules", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetClinicalAlertRulesAsync(cancellationToken))).WithName("GetClinicalAlertRules");
administration.MapGet("/clinical-alert-rules/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleHistory");
administration.MapPost("/clinical-alert-rule-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ClinicalAlertRuleChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateClinicalAlertRuleChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/clinical-alert-rule-change-requests/{created.Request.RequestId}", created); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateClinicalAlertRuleChangeRequest");
administration.MapGet("/clinical-alert-rule-change-requests", async (AdministrationRepository repository, string? status, int? offset, int? limit, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleChangeRequestsAsync(status, offset ?? 0, limit ?? 50, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleChangeRequests");
administration.MapGet("/clinical-alert-rule-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetClinicalAlertRuleChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetClinicalAlertRuleChangeRequest");
administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitClinicalAlertRuleChangeRequest");
administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveClinicalAlertRuleChangeRequest");
administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectClinicalAlertRuleChangeRequest");
administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateClinicalAlertRuleChangeRequest");
administration.MapPost("/clinical-alert-rule-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ClinicalAlertRuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelClinicalAlertRuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ClinicalAlertRuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelClinicalAlertRuleChangeRequest");
administration.MapGet("/modules", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetModuleCatalogAsync(cancellationToken))).WithName("GetModuleCatalog");
administration.MapGet("/modules/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleCatalogHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetModuleCatalogHistory");
administration.MapPost("/module-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ModuleChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateModuleChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/module-change-requests/{created.Request.RequestId}", created); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateModuleChangeRequest");
administration.MapGet("/module-change-requests", async (AdministrationRepository repository, string? status, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleChangeRequestsAsync(status, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetModuleChangeRequests");
administration.MapGet("/module-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetModuleChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetModuleChangeRequest");
administration.MapPost("/module-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitModuleChangeRequest");
administration.MapPost("/module-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveModuleChangeRequest");
administration.MapPost("/module-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectModuleChangeRequest");
administration.MapPost("/module-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateModuleChangeRequest");
administration.MapPost("/module-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ModuleChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelModuleChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ModuleChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelModuleChangeRequest");
administration.MapPut("/modules/{key}/status", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ModuleCatalogStatusUpdateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdateModuleCatalogStatusAsync(key, request.Status, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpdateModuleCatalogStatus");
administration.MapPost("/modules/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackModuleCatalogAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackModuleCatalog");
administration.MapGet("/api-clients", async (AdministrationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetApiClientsAsync(cancellationToken))).WithName("GetApiClients");
administration.MapGet("/api-clients/{key}/history", async (AdministrationRepository repository, string key, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientRegistryHistoryAsync(key, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetApiClientRegistryHistory");
administration.MapPut("/api-clients/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ApiClientRegistryMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertApiClientAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertApiClient");
administration.MapPost("/api-clients/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackApiClientRegistryAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackApiClientRegistry");
administration.MapPost("/api-client-change-requests", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, ApiClientChangeRequestCreateRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); var created = await repository.CreateApiClientChangeRequestAsync(request, session.Username, cancellationToken); return Results.Created($"/api/administration/api-client-change-requests/{created.Request.RequestId}", created); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("CreateApiClientChangeRequest");
administration.MapGet("/api-client-change-requests", async (AdministrationRepository repository, string? status, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientChangeRequestsAsync(status, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("GetApiClientChangeRequests");
administration.MapGet("/api-client-change-requests/{requestId:guid}", async (AdministrationRepository repository, Guid requestId, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.GetApiClientChangeRequestAsync(requestId, cancellationToken)); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("GetApiClientChangeRequest");
administration.MapPost("/api-client-change-requests/{requestId:guid}/submit", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.SubmitApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("SubmitApiClientChangeRequest");
administration.MapPost("/api-client-change-requests/{requestId:guid}/approve", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ApproveApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ApproveApiClientChangeRequest");
administration.MapPost("/api-client-change-requests/{requestId:guid}/reject", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RejectApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("RejectApiClientChangeRequest");
administration.MapPost("/api-client-change-requests/{requestId:guid}/activate", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.ActivateApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("ActivateApiClientChangeRequest");
administration.MapPost("/api-client-change-requests/{requestId:guid}/cancel", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, Guid requestId, ApiClientChangeRequestDecisionRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.CancelApiClientChangeRequestAsync(requestId, request.Note, request.ExpectedVersion, session.Username, cancellationToken)); } catch (ApiClientChangeRequestConflictException exception) { return Results.Conflict(new { error = exception.Message }); } catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); } }).WithName("CancelApiClientChangeRequest");
administration.MapPut("/clinical-alert-rules/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, ClinicalAlertRuleMutationRequest request, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpsertClinicalAlertRuleAsync(key, request, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("UpsertClinicalAlertRule");
administration.MapPost("/clinical-alert-rules/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackClinicalAlertRuleAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackClinicalAlertRule");

administration.MapPut("/practice-settings/{key}", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, PracticeSettingUpdateRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.UpdatePracticeSettingAsync(key, request.Value, session.Username, cancellationToken)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).WithName("UpdatePracticeSetting");
administration.MapPost("/practice-settings/{key}/revisions/{revisionId:long}/rollback", async (AdministrationRepository repository, AuthRepository authRepository, HttpContext httpContext, string key, long revisionId, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Ok(await repository.RollbackPracticeSettingAsync(key, revisionId, session.Username, cancellationToken)); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } }).WithName("RollbackPracticeSetting");

administration.MapGet("/directory", async (
        AdministrationRepository repository,
        CancellationToken cancellationToken) =>
    {
        var directory = await repository.GetDirectoryAsync(cancellationToken);
        return Results.Ok(directory);
    })
    .WithName("GetAdministrationDirectory");

administration.MapGet("/audit/phi", async (
        PhiAuditRepository repository,
        int? limit,
        string? username,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await repository.GetRecentAsync(limit ?? 50, username, from, to, cancellationToken)); }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["audit"] = [exception.Message] }); }
    })
    .WithName("GetPhiAccessAudit");

administration.MapGet("/audit/phi/export", async (
        PhiAuditRepository repository,
        int? limit,
        string? username,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken) =>
    {
        try { return Results.File(Encoding.UTF8.GetBytes(await repository.GetCsvAsync(limit ?? 200, username, from, to, cancellationToken)), "text/csv", "avenchart-phi-access-audit.csv"); }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["audit"] = [exception.Message] }); }
    })
    .WithName("ExportPhiAccessAudit");

administration.MapPut("/portal-activity/profile-reviews/{requestId:long}/accept", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        long requestId,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.AcceptPortalProfileReviewAsync(
            requestId,
            session.Username,
            cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("AcceptAdministrationPortalProfileReview");

administration.MapPut("/portal-activity/profile-reviews/{requestId:long}/revert", async (
        AdministrationRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        long requestId,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var mutation = await repository.RevertPortalProfileReviewAsync(
            requestId,
            session.Username,
            cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("RevertAdministrationPortalProfileReview");

administration.MapPost("/users", async (
        AdministrationDirectoryRepository repository,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.CreateUserAsync(request, cancellationToken);
            return Results.Created($"/api/administration/users/{mutation.Id}", mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateAdministrationUser");

administration.MapPut("/users/{userId:int}", async (
        AdministrationDirectoryRepository repository,
        int userId,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.UpdateUserAsync(userId, request, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdateAdministrationUser");

administration.MapDelete("/users/{userId:int}", async (
        AdministrationDirectoryRepository repository,
        int userId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteUserAsync(userId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAdministrationUser");

administration.MapPost("/facilities", async (
        AdministrationDirectoryRepository repository,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.CreateFacilityAsync(request, cancellationToken);
            return Results.Created($"/api/administration/facilities/{mutation.Id}", mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateAdministrationFacility");

administration.MapPut("/facilities/{facilityId:int}", async (
        AdministrationDirectoryRepository repository,
        int facilityId,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.UpdateFacilityAsync(facilityId, request, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdateAdministrationFacility");

administration.MapDelete("/facilities/{facilityId:int}", async (
        AdministrationDirectoryRepository repository,
        int facilityId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteFacilityAsync(facilityId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAdministrationFacility");

administration.MapPut("/access-control/group-permissions", async (
        AdministrationDirectoryRepository repository,
        AdministrationAccessPermissionMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.GrantAccessGroupPermissionAsync(request, cancellationToken);
            return Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GrantAdministrationAccessGroupPermission");

administration.MapDelete("/access-control/group-permissions/{groupValue}/{sectionValue}/{permissionValue}", async (
        AdministrationDirectoryRepository repository,
        string groupValue,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.RevokeAccessGroupPermissionAsync(
                groupValue,
                sectionValue,
                permissionValue,
                cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RevokeAdministrationAccessGroupPermission");

administration.MapPut("/access-control/user-memberships", async (
        AdministrationDirectoryRepository repository,
        AdministrationAccessUserMembershipMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.GrantAccessUserMembershipAsync(request, cancellationToken);
            return Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GrantAdministrationAccessUserMembership");

administration.MapDelete("/access-control/user-memberships/{userValue}/{groupValue}", async (
        AdministrationDirectoryRepository repository,
        string userValue,
        string groupValue,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var mutation = await repository.RevokeAccessUserMembershipAsync(userValue, groupValue, cancellationToken);
            return mutation is null ? Results.NotFound() : Results.Ok(mutation);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RevokeAdministrationAccessUserMembership");

var formEngine = app.MapGroup("/api/form-engine").WithTags("Clinical Form Engine");
RequireAccessPermission(formEngine, "patients", "demo", "view");
formEngine.MapLegacyClinicalFormDisplayEndpoints();
formEngine.MapClinicalFormOptionListEndpoints();

formEngine.MapGet("/policy", (ClinicalFormRepository repository) =>
        Results.Ok(repository.GetPolicy()))
    .WithName("GetClinicalFormPolicy");

formEngine.MapPost("/preview", (
        ClinicalFormRepository repository,
        ClinicalFormPreviewRequest request) =>
    {
        try
        {
            return Results.Ok(repository.Preview(request));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("PreviewClinicalFormDefinition")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

formEngine.MapGet("/catalog", async (
        ClinicalFormRepository repository,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListCatalogAsync(
                search,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetClinicalFormCatalog");

formEngine.MapGet("/definitions", async (
        ClinicalFormRepository repository,
        string? search,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListDefinitionsAsync(
                search,
                status,
                page ?? 1,
                pageSize ?? 20,
                catalogOnly: false,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetClinicalFormDefinitions")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

formEngine.MapPost("/definitions", async (
        ClinicalFormRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        ClinicalFormDefinitionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var created = await repository.CreateDefinitionAsync(
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/form-engine/definitions/{created.Definition.DefinitionId}",
                created);
        }
        catch (ClinicalFormConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateClinicalFormDefinition")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

formEngine.MapGet("/definitions/{definitionId:guid}", async (
        ClinicalFormRepository repository,
        Guid definitionId,
        CancellationToken cancellationToken) =>
    {
        var result = await repository.GetDefinitionAsync(
            definitionId,
            cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetClinicalFormDefinition")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

formEngine.MapPost("/definitions/{definitionId:guid}/revisions", async (
        ClinicalFormRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        ClinicalFormRevisionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var created = await repository.CreateRevisionAsync(
                definitionId,
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/form-engine/definitions/{definitionId}",
                created);
        }
        catch (ClinicalFormConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateClinicalFormRevision")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

foreach (var action in new[]
         {
             "review",
             "approve",
             "reject",
             "activate",
             "suspend",
             "retire"
         })
{
    formEngine.MapPost(
            $"/definitions/{{definitionId:guid}}/{action}",
            async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid definitionId,
                ClinicalFormDefinitionTransitionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    return Results.Ok(await repository.TransitionDefinitionAsync(
                        definitionId,
                        action,
                        request,
                        session.Username,
                        cancellationToken));
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
        .WithName($"TransitionClinicalFormDefinition{action}")
        .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));
}

formEngine.MapDelete("/definitions/{definitionId:guid}/test-fixture", async (
        ClinicalFormRepository repository,
        Guid definitionId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await repository.DeleteTestFixtureAsync(
                definitionId,
                cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("DeleteClinicalFormTestFixture")
    .AddEndpointFilter(AccessPermissionFilter("admin", "acl", "write"));

formEngine.MapGet("/patients/{patientId}/instances", async (
        ClinicalFormRepository repository,
        string patientId,
        int? encounterId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListInstancesAsync(
                patientId,
                encounterId,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetPatientClinicalFormInstances");

formEngine.MapPost("/patients/{patientId}/instances", async (
        ClinicalFormRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        ClinicalFormInstanceCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var created = await repository.CreateInstanceAsync(
                patientId,
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/form-engine/instances/{created.Instance.InstanceId}",
                created);
        }
        catch (ClinicalFormConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreatePatientClinicalFormInstance")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

formEngine.MapGet("/instances/{instanceId:guid}", async (
        ClinicalFormRepository repository,
        Guid instanceId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetInstanceAsync(
                instanceId,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    })
    .WithName("GetClinicalFormInstance");

formEngine.MapPut("/instances/{instanceId:guid}", async (
        ClinicalFormRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid instanceId,
        ClinicalFormInstanceUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.UpdateInstanceAsync(
                instanceId,
                request,
                session.Username,
                cancellationToken));
        }
        catch (ClinicalFormConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdateClinicalFormInstance")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

foreach (var action in new[] { "finalize", "sign", "cosign" })
{
    formEngine.MapPost(
            $"/instances/{{instanceId:guid}}/{action}",
            async (
                ClinicalFormRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                Guid instanceId,
                ClinicalFormInstanceTransitionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(
                        authRepository,
                        httpContext,
                        cancellationToken);
                    var result = action switch
                    {
                        "finalize" => await repository.FinalizeInstanceAsync(
                            instanceId,
                            request,
                            session.Username,
                            cancellationToken),
                        "sign" => await repository.SignInstanceAsync(
                            instanceId,
                            request,
                            session.Username,
                            cancellationToken),
                        _ => await repository.CosignInstanceAsync(
                            instanceId,
                            request,
                            session.Username,
                            cancellationToken)
                    };
                    return Results.Ok(result);
                }
                catch (ClinicalFormConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        currentVersion = exception.CurrentVersion,
                        currentState = exception.CurrentState
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
        .WithName($"TransitionClinicalFormInstance{action}")
        .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
}

formEngine.MapPost("/instances/{instanceId:guid}/amend", async (
        ClinicalFormRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid instanceId,
        ClinicalFormInstanceAmendRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Created(
                "/api/form-engine/instances",
                await repository.AmendInstanceAsync(
                    instanceId,
                    request,
                    session.Username,
                    cancellationToken));
        }
        catch (ClinicalFormConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentState = exception.CurrentState
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("AmendClinicalFormInstance")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

formEngine.MapGet("/instances/{instanceId:guid}/render", async (
        ClinicalFormRepository repository,
        Guid instanceId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.RenderInstanceAsync(
                instanceId,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    })
    .WithName("RenderClinicalFormInstance");

formEngine.MapGet("/instances/{instanceId:guid}/field-dictionary", async (
        ClinicalFormRepository repository,
        Guid instanceId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.GetInstanceFieldDictionaryAsync(
                instanceId,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    })
    .WithName("GetClinicalFormInstanceFieldDictionary");

formEngine.MapGet("/instances/{instanceId:guid}/structured-export", async (
        ClinicalFormRepository repository,
        Guid instanceId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ExportInstanceStructuredAsync(
                instanceId,
                cancellationToken));
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    })
    .WithName("ExportClinicalFormInstanceStructured");

formEngine.MapGet("/instances/{instanceId:guid}/export", async (
        ClinicalFormRepository repository,
        Guid instanceId,
        string? locale,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Content(
                await repository.ExportInstanceHtmlAsync(
                    instanceId,
                    locale,
                    cancellationToken),
                "text/html",
                Encoding.UTF8);
        }
        catch (ArgumentException)
        {
            return Results.NotFound();
        }
    })
    .WithName("ExportClinicalFormInstance");

var reports = app.MapGroup("/api/reports").WithTags("Reports");
RequireAccessPermission(reports, "patients", "pat_rep", "view");

reports.MapGet("/operational", async (
        ReportRepository repository,
        CancellationToken cancellationToken) =>
    {
        var report = await repository.GetOperationalReportsAsync(cancellationToken);
        return Results.Ok(report);
    })
    .WithName("GetOperationalReports");

reports.MapPost("/controlled-inventory/as-of", async (
        ControlledInventoryReportRequest request,
        ReportRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var report = await repository.RunControlledInventoryReportAsync(request, session.Username, cancellationToken);
            return Results.Created("/api/reports/controlled-inventory/as-of", report);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] });
        }
    })
    .WithName("RunControlledInventoryAsOfReport")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

reports.MapPost("/controlled-inventory/activity", async (
        ControlledInventoryActivityReportRequest request,
        ReportRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
            var report = await repository.RunControlledInventoryActivityReportAsync(request, session.Username, cancellationToken);
            return Results.Created("/api/reports/controlled-inventory/activity", report);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] });
        }
    })
    .WithName("RunControlledInventoryActivityReport")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

reports.MapGet("/controlled-inventory/as-of/{runId:guid}/export", async (
    Guid runId,
    ReportRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    var csv = await repository.ExportControlledInventoryRunCsvAsync(
        runId,
        session.Username,
        cancellationToken);
    return csv is null
        ? Results.NotFound()
        : Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"avenchart-controlled-inventory-{runId}.csv");
}).WithName("ExportControlledInventoryAsOfRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

reports.MapGet("/controlled-inventory/activity/{runId:guid}/export", async (
    Guid runId,
    ReportRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    var csv = await repository.ExportControlledInventoryActivityRunCsvAsync(
        runId,
        session.Username,
        cancellationToken);
    return csv is null
        ? Results.NotFound()
        : Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"avenchart-controlled-activity-{runId}.csv");
}).WithName("ExportControlledInventoryActivityRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

reports.MapPost("/controlled-inventory/count-variance", async (ControlledCountVarianceReportRequest request, ReportRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/reports/controlled-inventory/count-variance", await repository.RunControlledCountVarianceReportAsync(request, session.Username, cancellationToken)); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledReport"] = [exception.Message] }); }
}).WithName("RunControlledCountVarianceReport").AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

reports.MapGet("/controlled-inventory/count-variance/{runId:guid}/export", async (
    Guid runId,
    ReportRepository repository,
    AuthRepository authRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
    var csv = await repository.ExportControlledCountVarianceRunCsvAsync(
        runId,
        session.Username,
        cancellationToken);
    return csv is null
        ? Results.NotFound()
        : Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"avenchart-controlled-count-variance-{runId}.csv");
}).WithName("ExportControlledCountVarianceRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

reports.MapGet("/operational/export", () =>
        Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Compatibility report export retired",
            detail: "Use governed report execution to create a scoped, purpose-bound, auditable download."))
    .WithName("ExportOperationalReports");

reports.MapGet("/families", (ReportRepository repository) => Results.Ok(repository.GetFamilies())).WithName("GetReportFamilies");
reports.MapGet("/families/{family}/export", () =>
    Results.Problem(
        statusCode: StatusCodes.Status410Gone,
        title: "Compatibility report export retired",
        detail: "Use governed report execution to create a scoped, purpose-bound, auditable download."))
    .WithName("ExportReportFamily");

reports.MapGet("/definition-policy", (ReportDefinitionRepository repository) =>
        Results.Ok(repository.GetPolicy()))
    .WithName("GetReportDefinitionGovernancePolicy");

reports.MapGet("/execution-policy", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        var operatorAccess = await authRepository.HasAccessPermissionAsync(
            session.Username,
            "patients",
            "pat_rep",
            "write",
            cancellationToken);
        return Results.Ok(await repository.GetPolicyAsync(
            session.Username,
            operatorAccess,
            cancellationToken));
    })
    .WithName("GetGovernedReportExecutionPolicy");

reports.MapGet("/operations/runs", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string? search,
        string? status,
        string? family,
        string? requestedBy,
        bool? attentionOnly,
        DateOnly? from,
        DateOnly? to,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Ok(await repository.GetOperationsAsync(
                session.Username,
                search,
                status,
                family,
                requestedBy,
                attentionOnly ?? false,
                from,
                to,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetGovernedReportOperations")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

reports.MapGet("/operations/runs/{runId}", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string runId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var run = await repository.GetOperatorRunAsync(
                runId,
                session.Username,
                cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetGovernedReportOperationsRun")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

reports.MapGet("/catalog", async (
        ReportDefinitionRepository repository,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListAsync(
                search,
                status: "active",
                page ?? 1,
                pageSize ?? 20,
                catalogOnly: true,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetGovernedReportCatalog");

reports.MapGet("/definitions", async (
        ReportDefinitionRepository repository,
        string? search,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await repository.ListAsync(
                search,
                status,
                page ?? 1,
                pageSize ?? 20,
                catalogOnly: false,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetGovernedReportDefinitions");

reports.MapPost("/definitions", async (
        ReportDefinitionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        GovernedReportDefinitionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var result = await repository.CreateAsync(
                request,
                session.Username,
                cancellationToken);
            return Results.Created(
                $"/api/reports/definitions/{result.DefinitionId}",
                result);
        }
        catch (ReportDefinitionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentStatus = exception.CurrentStatus
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateGovernedReportDefinition")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

reports.MapGet("/definitions/{definitionId:guid}", async (
        ReportDefinitionRepository repository,
        Guid definitionId,
        CancellationToken cancellationToken) =>
    {
        var result = await repository.GetDetailAsync(definitionId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .WithName("GetGovernedReportDefinition");

reports.MapPost("/definitions/{definitionId:guid}/revisions", async (
        ReportDefinitionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        GovernedReportRevisionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            return Results.Created(
                $"/api/reports/definitions/{definitionId}",
                await repository.CreateRevisionAsync(
                    definitionId,
                    request,
                    session.Username,
                    cancellationToken));
        }
        catch (ReportDefinitionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                currentVersion = exception.CurrentVersion,
                currentStatus = exception.CurrentStatus
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateGovernedReportDefinitionRevision")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

foreach (var action in new[] { "review", "approve", "activate", "suspend", "retire" })
{
    reports.MapPost($"/definitions/{{definitionId:guid}}/{action}", async (
            ReportDefinitionRepository repository,
            AuthRepository authRepository,
            HttpContext httpContext,
            Guid definitionId,
            GovernedReportTransitionRequest request,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(
                    authRepository,
                    httpContext,
                    cancellationToken);
                return Results.Ok(await repository.TransitionAsync(
                    definitionId,
                    action,
                    request,
                    session.Username,
                    cancellationToken));
            }
            catch (ReportDefinitionConflictException exception)
            {
                return Results.Conflict(new
                {
                    error = exception.Message,
                    currentVersion = exception.CurrentVersion,
                    currentStatus = exception.CurrentStatus
                });
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName($"TransitionGovernedReportDefinition{action}")
        .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));
}

reports.MapDelete("/definitions/{definitionId:guid}/test-fixture", async (
        ReportDefinitionRepository repository,
        Guid definitionId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return await repository.DeleteTestFixtureAsync(
                definitionId,
                cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("DeleteGovernedReportDefinitionTestFixture")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

var therapyGroups = app.MapGroup("/api/therapy-groups").WithTags("Therapy Groups");
RequireAccessPermission(therapyGroups, "groups", "gadd", "view");
therapyGroups.MapGet("/", async (TherapyGroupRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetTherapyGroups");
therapyGroups.MapPost("/", async (TherapyGroupRepository repository, TherapyGroupCreateRequest request, CancellationToken cancellationToken) =>
{
    try { return Results.Created("/api/therapy-groups", await repository.CreateAsync(request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreateTherapyGroup").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
therapyGroups.MapGet("/{groupId:guid}/members", async (Guid groupId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetMembersAsync(groupId, cancellationToken))).WithName("GetTherapyGroupMembers");
therapyGroups.MapPost("/{groupId:guid}/members", async (Guid groupId, TherapyGroupMemberRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Created($"/api/therapy-groups/{groupId}/members", await repository.AddMemberAsync(groupId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("AddTherapyGroupMember").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
therapyGroups.MapGet("/{groupId:guid}/sessions", async (Guid groupId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetSessionsAsync(groupId, cancellationToken))).WithName("GetTherapyGroupSessions");
therapyGroups.MapPost("/{groupId:guid}/sessions", async (Guid groupId, TherapyGroupSessionCreateRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Created($"/api/therapy-groups/{groupId}/sessions", await repository.CreateSessionAsync(groupId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreateTherapyGroupSession").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
therapyGroups.MapGet("/{groupId:guid}/sessions/{sessionId:guid}/attendance", async (Guid groupId, Guid sessionId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetSessionAttendanceAsync(groupId, sessionId, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("GetTherapyGroupSessionAttendance");
therapyGroups.MapPut("/{groupId:guid}/sessions/{sessionId:guid}/attendance/{patientId}", async (Guid groupId, Guid sessionId, string patientId, TherapyGroupSessionAttendanceRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.RecordSessionAttendanceAsync(groupId, sessionId, patientId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("RecordTherapyGroupSessionAttendance").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
therapyGroups.MapPut("/{groupId:guid}/sessions/{sessionId:guid}/status", async (Guid groupId, Guid sessionId, TherapyGroupSessionStatusRequest request, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.UpdateSessionStatusAsync(groupId, sessionId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdateTherapyGroupSessionStatus").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write"));
therapyGroups.MapGet("/{groupId:guid}/sessions/{sessionId:guid}/encounters", async (Guid groupId, Guid sessionId, TherapyGroupRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetSessionEncountersAsync(groupId, sessionId, cancellationToken))).WithName("GetTherapyGroupSessionEncounters");
therapyGroups.MapPost("/{groupId:guid}/sessions/{sessionId:guid}/encounters", async (Guid groupId, Guid sessionId, TherapyGroupSessionEncounterRequest request, TherapyGroupRepository repository, EncounterRepository encounterRepository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.CreateSessionEncountersAsync(groupId, sessionId, request, encounterRepository, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreateTherapyGroupSessionEncounters").AddEndpointFilter(AccessPermissionFilter("groups", "gadd", "write")).AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

reports.MapPost("/definitions/{definitionId:guid}/preview", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        GovernedReportPreviewRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var preview = await repository.PreviewAsync(
                definitionId,
                request,
                session.Username,
                cancellationToken);
            return preview is null ? Results.NotFound() : Results.Ok(preview);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("PreviewGovernedReportDefinition")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapPost("/definitions/{definitionId:guid}/run", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        GovernedReportRunRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var run = await repository.RunAsync(
                definitionId,
                request,
                session.Username,
                cancellationToken);
            return run is null
                ? Results.NotFound()
                : Results.Created($"/api/reports/runs/{run.Run.RunId}", run);
        }
        catch (ReportExecutionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                existingRun = exception.ExistingRun
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RunGovernedReportDefinition")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapGet("/definitions/{definitionId:guid}/runs", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(
            authRepository,
            httpContext,
            cancellationToken);
        return Results.Ok(await repository.ListRunsAsync(
            definitionId,
            session.Username,
            page ?? 1,
            pageSize ?? 20,
            cancellationToken));
    })
    .WithName("GetGovernedReportRuns")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapGet("/runs/{runId}", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string runId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var run = await repository.GetRunAsync(
                runId,
                session.Username,
                cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("GetGovernedReportRun")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapPost("/runs/{runId}/cancel", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string runId,
        GovernedReportLifecycleRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var run = await repository.CancelAsync(
                runId,
                request,
                session.Username,
                cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }
        catch (ReportExecutionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                existingRun = exception.ExistingRun
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CancelGovernedReportRun")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapPost("/runs/{runId}/retry", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string runId,
        GovernedReportLifecycleRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var run = await repository.RetryAsync(
                runId,
                request,
                session.Username,
                cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }
        catch (ReportExecutionConflictException exception)
        {
            return Results.Conflict(new
            {
                error = exception.Message,
                existingRun = exception.ExistingRun
            });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("RetryGovernedReportRun")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

reports.MapGet("/runs/{runId}/download", async (
        ReportExecutionRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string runId,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var session = await GetSessionFromHeaderAsync(
                authRepository,
                httpContext,
                cancellationToken);
            var artifact = await repository.DownloadAsync(
                runId,
                session.Username,
                cancellationToken);
            return artifact is null
                ? Results.NotFound()
                : Results.File(
                    artifact.Content,
                    artifact.ContentType,
                    artifact.FileName);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("DownloadGovernedReportRun")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

app.Run();

static IResult RegistrationValidationProblem(IReadOnlyList<PatientRegistrationValidationIssue> issues)
{
    var errors = issues
        .GroupBy(issue => issue.Field)
        .ToDictionary(
            group => group.Key,
            group => group.Select(issue => issue.Message).ToArray());

    return Results.ValidationProblem(
        errors,
        statusCode: StatusCodes.Status400BadRequest,
        title: "Patient registration validation failed");
}

static Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
{
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        application = "avenchart-api",
        checkedAtUtc = DateTimeOffset.UtcNow,
        dependencies = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Status.ToString().ToLowerInvariant()),
        details = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                description = entry.Value.Description,
                data = entry.Value.Data
            })
    });
}

static PostgresException? FindSchemaShapeException(Exception? exception)
{
    while (exception is not null)
    {
        if (exception is PostgresException postgresException
            && postgresException.SqlState is "42P01" or "42703")
        {
            return postgresException;
        }

        exception = exception.InnerException;
    }

    return null;
}

static Task WriteSchemaNotReadyAsync(HttpContext context)
{
    var correlationId = context.Items["correlationId"]?.ToString() ?? context.TraceIdentifier;
    return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database schema is not ready",
            detail: "The application database schema is unavailable or does not match this API version.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "schema_not_ready",
                ["correlationId"] = correlationId
            })
        .ExecuteAsync(context);
}

static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> PatientFacilityScopeFilter()
{
    return async (context, next) =>
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        var patientIdentifier = routeValues.TryGetValue("patientId", out var patientId)
            ? patientId?.ToString()
            : routeValues.TryGetValue("canonicalId", out var canonicalId)
                ? canonicalId?.ToString()
                : null;
        var insuranceId = routeValues.TryGetValue("insuranceId", out var insurance)
            ? insurance?.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(patientIdentifier) && string.IsNullOrWhiteSpace(insuranceId))
        {
            return await next(context);
        }

        PhiAuditResourceContext.Set(
            context.HttpContext,
            string.IsNullOrWhiteSpace(patientIdentifier) ? "Insurance" : "Patient",
            patientIdentifier ?? insuranceId);

        var accessContext = RequireStaffAccessContext(context.HttpContext);
        var accessContextService = context.HttpContext.RequestServices
            .GetRequiredService<StaffAccessContextService>();
        var authorized = string.IsNullOrWhiteSpace(patientIdentifier)
            ? await accessContextService.CanAccessInsuranceAsync(
                insuranceId,
                accessContext.FacilityId,
                context.HttpContext.RequestAborted)
            : await accessContextService.CanAccessPatientAsync(
                patientIdentifier,
                accessContext.FacilityId,
                context.HttpContext.RequestAborted);
        return authorized ? await next(context) : Results.NotFound();
    };
}

static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> ProcedureFacilityScopeFilter()
{
    return async (context, next) =>
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        var accessContext = RequireStaffAccessContext(context.HttpContext);
        var accessContextService = context.HttpContext.RequestServices
            .GetRequiredService<StaffAccessContextService>();
        var cancellationToken = context.HttpContext.RequestAborted;

        if (routeValues.TryGetValue("patientId", out var patientRouteValue))
        {
            var patientId = patientRouteValue?.ToString();
            PhiAuditResourceContext.Set(context.HttpContext, "Patient", patientId);
            var allowed = await accessContextService.CanAccessPatientAsync(
                patientId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        if (routeValues.TryGetValue("orderId", out var orderRouteValue)
            && int.TryParse(orderRouteValue?.ToString(), out var orderId))
        {
            PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryOrder", orderId.ToString(CultureInfo.InvariantCulture));
            var allowed = await accessContextService.CanAccessLaboratoryOrderAsync(
                orderId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        if (routeValues.TryGetValue("reportId", out var reportRouteValue)
            && int.TryParse(reportRouteValue?.ToString(), out var reportId))
        {
            PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryReport", reportId.ToString(CultureInfo.InvariantCulture));
            var allowed = await accessContextService.CanAccessLaboratoryReportAsync(
                reportId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        if (routeValues.TryGetValue("resultId", out var resultRouteValue)
            && int.TryParse(resultRouteValue?.ToString(), out var resultId))
        {
            PhiAuditResourceContext.Set(context.HttpContext, "LaboratoryResult", resultId.ToString(CultureInfo.InvariantCulture));
            var allowed = await accessContextService.CanAccessLaboratoryResultAsync(
                resultId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        if (routeValues.TryGetValue("specimenId", out var specimenRouteValue)
            && int.TryParse(specimenRouteValue?.ToString(), out var specimenId))
        {
            PhiAuditResourceContext.Set(context.HttpContext, "LaboratorySpecimen", specimenId.ToString(CultureInfo.InvariantCulture));
            var allowed = await accessContextService.CanAccessLaboratorySpecimenAsync(
                specimenId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        return await next(context);
    };
}

static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> ClinicalListFacilityScopeFilter()
{
    return async (context, next) =>
    {
        var routeValues = context.HttpContext.Request.RouteValues;
        var accessContext = RequireStaffAccessContext(context.HttpContext);
        var accessContextService = context.HttpContext.RequestServices
            .GetRequiredService<StaffAccessContextService>();
        var cancellationToken = context.HttpContext.RequestAborted;

        if (routeValues.TryGetValue("patientId", out var patientRouteValue))
        {
            var patientId = patientRouteValue?.ToString();
            PhiAuditResourceContext.Set(context.HttpContext, "Patient", patientId);
            var allowed = await accessContextService.CanAccessPatientAsync(
                patientId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        (string? ResourceType, string? ResourceId) clinicalResource = routeValues.TryGetValue("allergyId", out var allergyId) ? ("Allergy", allergyId?.ToString())
            : routeValues.TryGetValue("problemId", out var problemId) ? ("Problem", problemId?.ToString())
            : routeValues.TryGetValue("medicationId", out var medicationId) ? ("Medication", medicationId?.ToString())
            : routeValues.TryGetValue("prescriptionId", out var prescriptionId) ? ("Prescription", prescriptionId?.ToString())
            : routeValues.TryGetValue("immunizationId", out var immunizationId) ? ("Immunization", immunizationId?.ToString())
            : routeValues.TryGetValue("immunizationKey", out var immunizationKey) ? ("ImmunizationKey", immunizationKey?.ToString())
            : (null, null);
        if (clinicalResource.ResourceType is not null)
        {
            PhiAuditResourceContext.Set(context.HttpContext, clinicalResource.ResourceType, clinicalResource.ResourceId);
            var allowed = await accessContextService.CanAccessClinicalListResourceAsync(
                clinicalResource.ResourceType,
                clinicalResource.ResourceId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        if (routeValues.TryGetValue("messageId", out var messageRouteValue))
        {
            var messageId = messageRouteValue?.ToString();
            PhiAuditResourceContext.Set(context.HttpContext, "Message", messageId);
            var allowed = await accessContextService.CanAccessMessageAsync(
                messageId,
                accessContext.FacilityId,
                cancellationToken);
            return allowed ? await next(context) : Results.NotFound();
        }

        return await next(context);
    };
}

static async Task EnsureExternalLaboratorySourceFacilityScopeAsync(
    AuthSessionResponse session,
    IReadOnlyList<int>? requestedFacilityIds,
    StaffAccessContextService accessContextService,
    CancellationToken cancellationToken)
{
    var requested = (requestedFacilityIds ?? []).Distinct().OrderBy(id => id).ToArray();
    if (requested.Length == 0 || requested.Any(id => id <= 0))
    {
        throw new ArgumentException("At least one valid facility grant is required for an external laboratory source.");
    }
    var available = await accessContextService.GetAvailableAsync(session.Username, cancellationToken);
    var permitted = available.Facilities.Select(facility => facility.FacilityId).ToHashSet();
    if (requested.Any(id => !permitted.Contains(id)))
    {
        throw new ArgumentException("A laboratory source may be granted only to facilities available to the authenticated administrator.");
    }
}

static bool TryCreateDevelopmentTestOidcAuthorizationRequest(
    string? clientId,
    string? redirectUri,
    string? state,
    string? codeChallenge,
    string? codeChallengeMethod,
    string? scope,
    IdentityProviderOptions options,
    HttpContext httpContext,
    out TestIdentityProviderAuthorizationRequest request)
{
    request = default!;
    if (string.IsNullOrWhiteSpace(clientId)
        || string.IsNullOrWhiteSpace(redirectUri)
        || string.IsNullOrWhiteSpace(state)
        || string.IsNullOrWhiteSpace(codeChallenge)
        || !string.Equals(codeChallengeMethod, "S256", StringComparison.Ordinal)
        || !string.Equals(clientId, options.BrowserClientId, StringComparison.Ordinal)
        || codeChallenge.Length is < 43 or > 128
        || !codeChallenge.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
    {
        return false;
    }

    var expectedCallback = string.IsNullOrWhiteSpace(options.BrowserCallbackUrl)
        ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}{BrowserOidcSessionService.CallbackPath}"
        : options.BrowserCallbackUrl;
    if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var requestedCallback)
        || !Uri.TryCreate(expectedCallback, UriKind.Absolute, out var configuredCallback)
        || !string.Equals(requestedCallback.ToString(), configuredCallback.ToString(), StringComparison.Ordinal))
    {
        return false;
    }

    request = new TestIdentityProviderAuthorizationRequest(
        clientId!,
        redirectUri!,
        state!,
        codeChallenge!,
        codeChallengeMethod!,
        scope);
    return true;
}

static string BuildDevelopmentTestOidcAuthorizationPage(TestIdentityProviderAuthorizationRequest request)
{
    static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    return $"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>AvenChart development test identity provider</title>
        </head>
        <body>
          <main>
            <h1>Development test identity provider</h1>
            <p>This non-production page issues a short-lived token only for the configured AvenChart test client.</p>
            <form method="post" action="/api/test-idp/authorize">
              <input type="hidden" name="client_id" value="{Encode(request.ClientId)}">
              <input type="hidden" name="redirect_uri" value="{Encode(request.RedirectUri)}">
              <input type="hidden" name="state" value="{Encode(request.State)}">
              <input type="hidden" name="code_challenge" value="{Encode(request.CodeChallenge)}">
              <input type="hidden" name="code_challenge_method" value="{Encode(request.CodeChallengeMethod)}">
              <input type="hidden" name="scope" value="{Encode(request.Scope)}">
              <p><label>Username <input name="username" autocomplete="username" required></label></p>
              <p><label>Password <input name="password" type="password" autocomplete="current-password" required></label></p>
              <button type="submit">Continue</button>
            </form>
          </main>
        </body>
        </html>
        """;
}
