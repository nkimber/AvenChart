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
using AvenChart.Api.Features.Telehealth;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Security;
using AvenChart.Api.Workflows;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTelehealth(builder.Configuration, builder.Environment);

var runtimeSafetyOptions = builder.Configuration
    .GetSection(RuntimeSafetyOptions.SectionName)
    .Get<RuntimeSafetyOptions>() ?? new RuntimeSafetyOptions();

builder.Services.AddOpenApi(options =>
{
    AvenChartOpenApi.Configure(options);
    TelehealthOpenApi.Configure(options);
});
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
            context.Response.Headers.CacheControl = "no-store, no-cache, private, max-age=0";
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

app.MapDevelopmentTestIdentityProviderEndpoints();
app.MapPatientPortalEndpoints();
app.MapTelehealthEndpoints();
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
app.MapConfigurationEndpoints(administration);
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

