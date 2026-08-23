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

app.MapPatientPortalEndpoints();
app.MapExternalLaboratoryFhirIntakeEndpoints();

app.MapPatientEndpoints();
app.MapAppointmentEndpoints();
app.MapEncounterEndpoints();
app.MapOfficeNoteEndpoints();

app.MapAdministrativeReferenceEndpoints();

app.MapPatientEngagementEndpoints();

app.MapDocumentTemplateEndpoints();
app.MapManagedRecordEndpoints();

app.MapDocumentEndpoints();
var administration = app.MapAdministrationEndpoints();
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

app.MapClinicalFormEndpoints();
app.MapReportEndpoints();
app.MapTherapyGroupEndpoints();

app.Run();


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
