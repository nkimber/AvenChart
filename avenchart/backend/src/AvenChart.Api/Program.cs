using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Experience;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using AvenChart.Api.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.Items["correlationId"]?.ToString()
            ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddResponseCompression();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadinessHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddSingleton<IIntegrationTransport, LocalDeterministicIntegrationTransport>();
builder.Services.AddSingleton<RuntimeDiagnostics>();

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
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("AvenChart")
    ?? "Host=localhost;Port=5433;Database=legacy-ehr_modernized;Username=legacy-ehr;Password=legacy-ehr_demo";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<PatientRepository>();
builder.Services.AddScoped<PatientXmlExchangeRepository>();
builder.Services.AddScoped<PatientPrintRepository>();
builder.Services.AddScoped<AppointmentRepository>();
builder.Services.AddScoped<EncounterRepository>();
builder.Services.AddScoped<EncounterLayoutFormRepository>();
builder.Services.AddScoped<ClinicalAlertEvaluationRepository>();
builder.Services.AddScoped<ClinicalListRepository>();
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
builder.Services.AddScoped<ProcedureRepository>();
builder.Services.AddScoped<BillingRepository>();
builder.Services.AddScoped<AdministrationRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddScoped<TherapyGroupRepository>();
builder.Services.AddScoped<ReferralRepository>();
builder.Services.AddScoped<AuthorizationRepository>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<PatientPortalRepository>();
builder.Services.AddScoped<IntegrationRepository>();
builder.Services.AddScoped<PhiAuditRepository>();
builder.Services.AddScoped<PatientMergeAuditRepository>();
builder.Services.AddScoped<PatientMergeExecutionRepository>();
builder.Services.AddScoped<PatientRecordRequestRepository>();
builder.Services.AddScoped<PatientSdohRepository>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<InventoryCostPolicyRepository>();
builder.Services.AddScoped<FlowBoardRepository>();
builder.Services.AddScoped<FhirRepository>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("local-app-clients", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:3100",
                "http://127.0.0.1:3100")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var runtimeSafetyOptions = builder.Configuration
    .GetSection(RuntimeSafetyOptions.SectionName)
    .Get<RuntimeSafetyOptions>() ?? new RuntimeSafetyOptions();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

var configuredRuntimeSafety = app.Services.GetRequiredService<IOptions<RuntimeSafetyOptions>>().Value;
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

auth.MapPost("/login", async (
        AuthRepository repository,
        AuthLoginRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var sourceIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var response = await repository.LoginAsync(request, sourceIp, userAgent, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("Login");

auth.MapGet("/session", async (
        AuthRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-Legacy EHR-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.GetCurrentSessionAsync(sessionId, cancellationToken))
            : Results.Ok(new AuthSessionResponse(
                Authenticated: false,
                SessionId: null,
                Username: string.Empty,
                DisplayName: string.Empty,
                Role: string.Empty,
                StaffId: null,
                CreatedAt: null,
                LastSeenAt: null,
                ExpiresAt: null,
                EndedAt: null,
                FailureReason: "Session header was not supplied.",
                SessionSource: "avenchart"));
    })
    .WithName("GetCurrentSession");

auth.MapPost("/logout", async (
        AuthRepository repository,
        AuthSessionRequest request,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.LogoutAsync(request.SessionId, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("Logout");

auth.MapGet("/login-audit", async (
        AuthRepository repository,
        HttpContext httpContext,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(repository, httpContext, cancellationToken);
        if (!session.Authenticated)
        {
            return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
        }

        var response = await repository.GetLoginAuditAsync(limit ?? 10, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetLoginAudit")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "view"));

auth.MapGet("/activity-audit", async (
        AuthRepository repository,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.GetAuthenticationActivityAuditAsync(
            limit ?? 25,
            cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetAuthenticationActivityAudit")
    .AddEndpointFilter(AccessPermissionFilter("admin", "super", "view"));

var patientPortal = app.MapGroup("/api/patient-portal").WithTags("Patient Portal");

patientPortal.MapPost("/login", async (
        PatientPortalRepository repository,
        PatientPortalLoginRequest request,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.LoginAsync(request, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("PatientPortalLogin");

patientPortal.MapGet("/session", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
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
    })
    .WithName("GetPatientPortalSession");

patientPortal.MapGet("/home", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
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
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
            ? Results.Ok(await repository.DeleteMessageAsync(sessionId, messageId, cancellationToken))
            : Results.Ok(PatientPortalRepository.MissingSessionHeaderDeleteMessage(messageId.ToString()));
    })
    .WithName("DeletePatientPortalMessage");

patientPortal.MapDelete("/session", async (
        PatientPortalRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var header = httpContext.Request.Headers["X-Legacy EHR-Patient-Portal-Session"].ToString();
        return Guid.TryParse(header, out var sessionId)
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
    })
    .WithName("EndPatientPortalSession");

var fhir = app.MapGroup("/api/fhir/R4").WithTags("FHIR R4");
RequireAccessPermission(fhir, "patients", "demo", "view");

fhir.MapGet("/metadata", () =>
    {
        var patientCapability = new FhirPatientCapability(
            "Patient",
            [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
            ["name", "identifier", "_count"]);
        var encounterCapability = new FhirPatientCapability(
            "Encounter",
            [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
            ["subject", "_count"]);
        var observationCapability = new FhirPatientCapability(
            "Observation",
            [new FhirCapabilityInteraction("read"), new FhirCapabilityInteraction("search-type")],
            ["subject", "_count"]);
        var server = new FhirCapabilityResource("server", [], [patientCapability, encounterCapability, observationCapability]);
        return Results.Ok(new FhirCapabilityStatement(
            "CapabilityStatement",
            "active",
            DateTimeOffset.UtcNow.ToString("O"),
            "instance",
            "4.0.1",
            "json",
            [server]));
    })
    .WithName("GetFhirCapabilityStatement");

fhir.MapGet("/Patient/{id}", async (FhirRepository repository, string id, CancellationToken cancellationToken) =>
    {
        var patient = await repository.GetPatientAsync(id, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("GetFhirPatient");

fhir.MapGet("/Patient", async (FhirRepository repository, string? name, string? identifier, int? _count, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchPatientsAsync(name, identifier, _count, cancellationToken)))
    .WithName("SearchFhirPatients");

fhir.MapGet("/Encounter/{id:int}", async (FhirRepository repository, int id, CancellationToken cancellationToken) =>
    {
        var encounter = await repository.GetEncounterAsync(id, cancellationToken);
        return encounter is null ? Results.NotFound() : Results.Ok(encounter);
    })
    .WithName("GetFhirEncounter");

fhir.MapGet("/Encounter", async (FhirRepository repository, string? subject, int? _count, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchEncountersAsync(subject, _count, cancellationToken)))
    .WithName("SearchFhirEncounters");

fhir.MapGet("/Observation/{id:int}", async (FhirRepository repository, int id, CancellationToken cancellationToken) =>
    {
        var observation = await repository.GetObservationAsync(id, cancellationToken);
        return observation is null ? Results.NotFound() : Results.Ok(observation);
    })
    .WithName("GetFhirObservation");

fhir.MapGet("/Observation", async (FhirRepository repository, string? subject, int? _count, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchObservationsAsync(subject, _count, cancellationToken)))
    .WithName("SearchFhirObservations");

fhir.MapGet("/Observation/sdoh", async (FhirRepository repository, string? subject, int? _count, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchSdohObservationsAsync(subject, _count, cancellationToken)))
    .WithName("SearchFhirSdohObservations");

var patients = app.MapGroup("/api/patients").WithTags("Patients");
RequireAccessPermission(patients, "patients", "demo", "view");

var clinicalWorkflows = app.MapGroup("/api/clinical-workflows").WithTags("Clinical Workflows");
RequireAccessPermission(clinicalWorkflows, "patients", "med", "view");
clinicalWorkflows.MapGet("/assignees", async (
        AuthorizationRepository repository,
        CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAssigneesAsync(cancellationToken)))
    .WithName("GetClinicalWorkflowAssignees");

patients.MapGet("/", async (
        PatientRepository repository,
        string? search,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(search, limit ?? 25, cancellationToken);
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
patients.MapPost("/{patientId}/referrals", async (string patientId, ReferralCreateRequest request, ReferralRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Created($"/api/patients/{patientId}/referrals", await repository.CreateAsync(patientId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("CreatePatientReferral").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
patients.MapPut("/{patientId}/referrals/{referralId:guid}/status", async (string patientId, Guid referralId, ReferralStatusRequest request, ReferralRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.UpdateStatusAsync(patientId, referralId, request, cancellationToken)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
}).WithName("UpdatePatientReferralStatus").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

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
    catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string,string[]> { ["duplicateReview"] = [ex.Message] }); }
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
        PatientRegistrationRequest request,
        CancellationToken cancellationToken) =>
    {
        var result = await repository.CreatePatientAsync(request, cancellationToken);
        return result.Patient is null
            ? RegistrationValidationProblem(result.ValidationIssues)
            : Results.Created($"/api/patients/{result.Patient.CanonicalId}", result.Patient);
    })
    .WithName("RegisterPatient")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "addonly"));

patients.MapGet("/{canonicalId}", async (
        PatientRepository repository,
        string canonicalId,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.GetChartSummaryAsync(canonicalId, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("GetPatientChartSummary");
patients.MapGet("/{patientId}/xml-export",async(string patientId,PatientXmlExchangeRepository repository,CancellationToken ct)=>{var xml=await repository.ExportAsync(patientId,ct);return xml is null?Results.NotFound():Results.File(Encoding.UTF8.GetBytes(xml),"application/xml",$"legacy-ehr-patient-{patientId}.xml");}).WithName("ExportPatientXml");
patients.MapPost("/xml-import/preview",async(PatientXmlExchangeRepository repository,PatientXmlImportRequest request,CancellationToken ct)=>{try{var preview=await repository.PreviewAsync(request,ct);return preview is null?Results.NotFound():Results.Ok(preview);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]>{{"xml",[e.Message]}});}}).WithName("PreviewPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients","demo","write"));
patients.MapPost("/xml-import",async(PatientXmlExchangeRepository repository,AuthRepository auth,HttpContext context,PatientXmlImportRequest request,CancellationToken ct)=>{try{var session=await GetSessionFromHeaderAsync(auth,context,ct);var result=await repository.ImportAsync(request,session.Username,ct);return result is null?Results.NotFound():Results.Ok(result);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]>{{"xml",[e.Message]}});}}).WithName("ImportPatientXml").AddEndpointFilter(AccessPermissionFilter("patients","demo","write"));
patients.MapPost("/xml-import/{auditId:guid}/rollback",async(PatientXmlExchangeRepository repository,AuthRepository auth,HttpContext context,Guid auditId,CancellationToken ct)=>{var session=await GetSessionFromHeaderAsync(auth,context,ct);return await repository.RollbackAsync(auditId,session.Username,ct)?Results.NoContent():Results.NotFound();}).WithName("RollbackPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients","demo","write"));

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

patients.MapPut("/{patientId}/contact", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientContactUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.UpdateContactAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("UpdatePatientContact")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/demographics", async (
        PatientRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string patientId,
        PatientDemographicsUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var patient = await repository.UpdateDemographicsAsync(
            patientId,
            request,
            session.Username,
            cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient demographics could not be updated from the supplied patient and demographic details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientDemographics")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/deceased-status", async (
        PatientRepository repository,
        string patientId,
        PatientDeceasedStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateDeceasedStatusAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient deceased status could not be updated from the supplied patient and status details.")
            : Results.Ok(patient);
    })
    .WithName("UpdatePatientDeceasedStatus")
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

var appointments = app.MapGroup("/api/appointments").WithTags("Appointments");
RequireAccessPermission(appointments, "patients", "appt", "view");

appointments.MapGet("/flow-board", async (FlowBoardRepository repository, string? date, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAsync(date, cancellationToken)))
    .WithName("GetAppointmentFlowBoard");

appointments.MapGet("/scheduling-options", async (AppointmentRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetSchedulingOptionsAsync(cancellationToken)))
    .WithName("GetAppointmentSchedulingOptions");

appointments.MapGet("/", async (
        AppointmentRepository repository,
        string? patientId,
        string? from,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(patientId, from, limit ?? 25, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("SearchAppointments");

appointments.MapGet("/waitlist", async (
        AppointmentRepository repository,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.GetWaitlistAsync(cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetAppointmentWaitlist");

appointments.MapGet("/reminders/templates", async (
        AppointmentRepository repository,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.GetReminderTemplateCatalogAsync(cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetAppointmentReminderTemplates")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPost("/{appointmentId}/reminders/dispatch", async (
        AppointmentRepository repository,
        HttpRequest request,
        string appointmentId,
        CancellationToken cancellationToken) =>
    {
        AppointmentReminderDispatchRequest? dispatchRequest = null;
        if (request.ContentLength.GetValueOrDefault() > 0)
        {
            dispatchRequest = await request.ReadFromJsonAsync<AppointmentReminderDispatchRequest>(cancellationToken);
        }

        try
        {
            var dispatch = await repository.DispatchReminderAsync(appointmentId, dispatchRequest?.TemplateId, cancellationToken);
            return dispatch is null
                ? Results.BadRequest("Appointment reminder could not be dispatched because the appointment was not found, no reminder is due, or the reminder rule is inactive.")
                : Results.Ok(dispatch);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    })
    .WithName("DispatchAppointmentReminder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPost("/{appointmentId}/reminders/dispatch/retry", async (
        AppointmentRepository repository,
        string appointmentId,
        CancellationToken cancellationToken) =>
    {
        var dispatch = await repository.RetryReminderDispatchAsync(appointmentId, cancellationToken);
        return dispatch is null
            ? Results.BadRequest("Appointment reminder could not be retried because no prior dispatch exists, no reminder is due, or the reminder rule is inactive.")
            : Results.Ok(dispatch);
    })
    .WithName("RetryAppointmentReminderDispatch")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapGet("/reminders/dispatch-history", async (
        AppointmentRepository repository,
        string? appointmentId,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var history = await repository.GetReminderDispatchHistoryAsync(appointmentId, limit ?? 10, cancellationToken);
        return Results.Ok(history);
    })
    .WithName("GetAppointmentReminderDispatchHistory");

appointments.MapGet("/{appointmentId}", async (
        AppointmentRepository repository,
        string appointmentId,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.GetByIdAsync(appointmentId, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(appointment);
    })
    .WithName("GetAppointmentDetail");

appointments.MapPost("/", async (
        AppointmentRepository repository,
        AppointmentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        if (request.EnforceConflictPolicy)
        {
            var validation = await repository.ValidateAvailabilityAsync(
                new AppointmentAvailabilityValidationRequest(
                    PatientId: request.PatientId,
                    ProviderId: request.ProviderId,
                    Date: request.Date,
                    StartTime: request.StartTime,
                    DurationMinutes: request.DurationMinutes,
                    FacilityId: request.FacilityId,
                    Room: request.Room,
                    ExcludeAppointmentId: null),
                cancellationToken);

            if (validation is null)
            {
                return Results.BadRequest("Appointment availability could not be validated from the supplied patient, date, time, and duration.");
            }

            if (!validation.Available)
            {
                return Results.Conflict(new
                {
                    error = "Appointment conflicts with existing schedule availability.",
                    validation
                });
            }
        }

        try
        {
            var appointment = await repository.CreateAsync(request, cancellationToken);
            return appointment is null
                ? Results.BadRequest("Appointment could not be created from the supplied patient, date, time, and duration.")
                : Results.Created($"/api/appointments/{appointment.Id}", appointment);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("CreateAppointment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPost("/availability/validate", async (
        AppointmentRepository repository,
        AppointmentAvailabilityValidationRequest request,
        CancellationToken cancellationToken) =>
    {
        var validation = await repository.ValidateAvailabilityAsync(request, cancellationToken);
        return validation is null
            ? Results.BadRequest("Appointment availability could not be validated from the supplied patient, date, time, and duration.")
            : Results.Ok(validation);
    })
    .WithName("ValidateAppointmentAvailability")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPut("/{appointmentId}", async (
        AppointmentRepository repository,
        string appointmentId,
        AppointmentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var appointment = await repository.UpdateAsync(appointmentId, request, cancellationToken);
            return appointment is null
                ? Results.BadRequest("Appointment could not be updated from the supplied date, time, and duration.")
                : Results.Ok(appointment);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    })
    .WithName("UpdateAppointment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPut("/{appointmentId}/status", async (
        AppointmentRepository repository,
        string appointmentId,
        AppointmentStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.UpdateStatusAsync(appointmentId, request, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(appointment);
    })
    .WithName("UpdateAppointmentStatus")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPost("/{appointmentId}/recurrence-exceptions/{occurrenceDate}/restore", async (
        AppointmentRepository repository,
        string appointmentId,
        string occurrenceDate,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.RestoreRecurrenceExceptionAsync(appointmentId, occurrenceDate, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(appointment);
    })
    .WithName("RestoreAppointmentOccurrence")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapPost("/{appointmentId}/occurrences/{occurrenceDate}/reschedule", async (
        AppointmentRepository repository,
        string appointmentId,
        string occurrenceDate,
        AppointmentOccurrenceRescheduleRequest request,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.RescheduleOccurrenceAsync(appointmentId, occurrenceDate, request, cancellationToken);
        return appointment is null
            ? Results.BadRequest("Appointment occurrence could not be rescheduled from the supplied date, time, and duration.")
            : Results.Created($"/api/appointments/{appointment.Id}", appointment);
    })
    .WithName("RescheduleAppointmentOccurrence")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

appointments.MapDelete("/{appointmentId}", async (
        AppointmentRepository repository,
        string appointmentId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(appointmentId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAppointment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

var encounters = app.MapGroup("/api/encounters").WithTags("Encounters");
RequireAccessPermission(encounters, "encounters", "auth_a", "view");

encounters.MapGet("/", async (
        EncounterRepository repository,
        string? patientId,
        string? from,
        int? limit,
        bool? archived,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(patientId, from, limit ?? 25, cancellationToken, archived == true);
        return Results.Ok(response);
    })
    .WithName("SearchEncounters");

encounters.MapPut("/{encounter:int}/archive", async (EncounterRepository repository, int encounter, CancellationToken cancellationToken) =>
    await repository.ArchiveAsync(encounter, cancellationToken) ? Results.NoContent() : Results.NotFound())
    .WithName("ArchiveEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/restore", async (EncounterRepository repository, int encounter, CancellationToken cancellationToken) =>
    await repository.RestoreAsync(encounter, cancellationToken) ? Results.NoContent() : Results.NotFound())
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

encounters.MapPost("/{encounter:int}/alerts/{ruleKey}/acknowledge", async (ClinicalAlertEvaluationRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, string ruleKey, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.AcknowledgeAsync(encounter, ruleKey, session.Username, cancellationToken)) is { } alerts ? Results.Ok(alerts) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("AcknowledgeEncounterClinicalAlert")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPost("/{encounter:int}/alerts/{ruleKey}/reopen", async (ClinicalAlertEvaluationRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, string ruleKey, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.ReopenAsync(encounter, ruleKey, session.Username, cancellationToken)) is { } alerts ? Results.Ok(alerts) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("ReopenEncounterClinicalAlert")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/forms/{layoutKey}", async (EncounterLayoutFormRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, string layoutKey, EncounterLayoutFormSaveRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.SaveAsync(encounter, layoutKey, request, session.Username, cancellationToken)) is { } form ? Results.Ok(form) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("SaveEncounterLayoutForm")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/tracks", async (TrackAnythingRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetEncounterCatalogAsync(encounter, cancellationToken)) is { } tracks ? Results.Ok(tracks) : Results.NotFound())
    .WithName("GetEncounterTracks");

encounters.MapPost("/{encounter:int}/tracks", async (TrackAnythingRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, TrackAnythingEncounterRecordCreateRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.CreateEncounterRecordAsync(encounter, request, session.Username, cancellationToken)) is { } record ? Results.Created($"/api/encounters/{encounter}/tracks/{record.RecordId}", record) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("CreateEncounterTrack")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/tracks/{recordId:guid}", async (TrackAnythingRepository repository, int encounter, Guid recordId, CancellationToken cancellationToken) =>
    (await repository.GetEncounterRecordAsync(encounter, recordId, cancellationToken)) is { } record ? Results.Ok(record) : Results.NotFound())
    .WithName("GetEncounterTrack");

encounters.MapPost("/{encounter:int}/tracks/{recordId:guid}/readings", async (TrackAnythingRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, Guid recordId, TrackAnythingReadingCreateRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.AddReadingAsync(encounter, recordId, request, session.Username, cancellationToken)) is { } reading ? Results.Created($"/api/encounters/{encounter}/tracks/{recordId}/readings/{reading.ReadingId}", reading) : Results.NotFound(); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
})
    .WithName("AddEncounterTrackReading")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/tracks/{recordId:guid}/readings/{readingId:guid}", async (TrackAnythingRepository repository, AuthRepository authRepository, HttpContext httpContext, int encounter, Guid recordId, Guid readingId, TrackAnythingReadingUpdateRequest request, CancellationToken cancellationToken) =>
{
    try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return (await repository.UpdateReadingAsync(encounter, recordId, readingId, request, session.Username, cancellationToken)) is { } reading ? Results.Ok(reading) : Results.NotFound(); }
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
        EncounterRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        int encounter,
        EncounterUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var encounterDetail = await repository.UpdateSummaryAsync(encounter, request, session.Username, cancellationToken);
        return encounterDetail is null ? Results.NotFound() : Results.Ok(encounterDetail);
    })
    .WithName("UpdateEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapGet("/{encounter:int}/audit", async (EncounterRepository repository, int encounter, CancellationToken cancellationToken) =>
    (await repository.GetAuditHistoryAsync(encounter, cancellationToken)) is { } history ? Results.Ok(history) : Results.NotFound())
    .WithName("GetEncounterAuditHistory");

encounters.MapPost("/{encounter:int}/vitals", async (
        EncounterRepository repository,
        int encounter,
        EncounterVitalsCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.CreateVitalsAsync(encounter, request, cancellationToken);
        return response is null
            ? Results.BadRequest("Vitals could not be recorded for the supplied encounter.")
            : Results.Created($"/api/encounters/{encounter}/vitals/{response.Id}", response);
    })
    .WithName("CreateEncounterVitals")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPost("/{encounter:int}/soap-notes", async (
        EncounterRepository repository,
        int encounter,
        EncounterSoapNoteCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.CreateSoapNoteAsync(encounter, request, cancellationToken);
        return response is null
            ? Results.BadRequest("SOAP note could not be recorded for the supplied encounter.")
            : Results.Created($"/api/encounters/{encounter}/soap-notes/{response.Id}", response);
    })
    .WithName("CreateEncounterSoapNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapPut("/{encounter:int}/sign", async (
        EncounterRepository repository,
        int encounter,
        EncounterSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SignAsync(encounter, request, cancellationToken);
        return response is null
            ? Results.BadRequest("Encounter could not be signed from the supplied encounter and signer details.")
            : Results.Ok(response);
    })
    .WithName("SignEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

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

encounters.MapDelete("/{encounter:int}/signatures/{signatureId:int}", async (
        EncounterRepository repository,
        int encounter,
        int signatureId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteSignatureAsync(encounter, signatureId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterSignature")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapDelete("/{encounter:int}/vitals/{vitalsId:int}", async (
        EncounterRepository repository,
        int encounter,
        int vitalsId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteVitalsAsync(encounter, vitalsId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterVitals")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapDelete("/{encounter:int}/soap-notes/{soapNoteId:int}", async (
        EncounterRepository repository,
        int encounter,
        int soapNoteId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteSoapNoteAsync(encounter, soapNoteId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterSoapNote")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

encounters.MapDelete("/{encounter:int}", async (
        EncounterRepository repository,
        int encounter,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(encounter, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

var clinicalLists = app.MapGroup("/api/clinical-lists").WithTags("Clinical Lists");
RequireAccessPermission(clinicalLists, "patients", "med", "view");

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
        ClinicalListRepository repository,
        ClinicalAllergyCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateAllergyAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Allergy could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/allergies/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalAllergy");

clinicalLists.MapPost("/problems", async (
        ClinicalListRepository repository,
        ClinicalProblemCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateProblemAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Problem could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/problems/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalProblem");

clinicalLists.MapPut("/problems/{problemId}/deactivate", async (
        ClinicalListRepository repository,
        string problemId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.DeactivateProblemAsync(problemId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalProblem");

clinicalLists.MapDelete("/problems/{problemId}", async (
        ClinicalListRepository repository,
        string problemId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteProblemAsync(problemId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteClinicalProblem");

clinicalLists.MapPost("/medications", async (
        ClinicalListRepository repository,
        ClinicalMedicationCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateMedicationAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Medication could not be created from the supplied patient, title, and date.")
            : Results.Created($"/api/clinical-lists/medications/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalMedication");

clinicalLists.MapPut("/medications/{medicationId}/deactivate", async (
        ClinicalListRepository repository,
        string medicationId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.DeactivateMedicationAsync(medicationId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalMedication");

clinicalLists.MapDelete("/medications/{medicationId}", async (
        ClinicalListRepository repository,
        string medicationId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteMedicationAsync(medicationId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteClinicalMedication");

clinicalLists.MapPut("/allergies/{allergyId}/deactivate", async (
        ClinicalListRepository repository,
        string allergyId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.DeactivateAllergyAsync(allergyId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalAllergy");

clinicalLists.MapDelete("/allergies/{allergyId}", async (
        ClinicalListRepository repository,
        string allergyId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAllergyAsync(allergyId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteClinicalAllergy");

clinicalLists.MapPost("/prescriptions", async (
        ClinicalListRepository repository,
        ClinicalPrescriptionCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreatePrescriptionAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Prescription could not be created from the supplied patient, drug, dose, and start date.")
            : Results.Created($"/api/clinical-lists/prescriptions/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalPrescription");

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
            ClinicalPrescriptionUpdateStatus.Conflict =>
                Results.Conflict(new
                {
                    error = "The prescription changed after it was loaded. Reload the current prescription before editing again.",
                    currentVersion = result.CurrentVersion
                }),
            _ => Results.Problem("The prescription update did not produce an authoritative result.")
        };
    })
    .WithName("UpdateClinicalPrescription");

clinicalLists.MapPut("/prescriptions/{prescriptionId}/deactivate", async (
        ClinicalListRepository repository,
        string prescriptionId,
        ClinicalPrescriptionDeactivateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.DeactivatePrescriptionAsync(prescriptionId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("DeactivateClinicalPrescription");

clinicalLists.MapPut("/prescriptions/{prescriptionId}/refill", async (
        ClinicalListRepository repository,
        string prescriptionId,
        ClinicalPrescriptionRefillRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.RefillPrescriptionAsync(prescriptionId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("RefillClinicalPrescription");

clinicalLists.MapPut("/prescriptions/{prescriptionId}/route-pharmacy", async (
        ClinicalListRepository repository,
        string prescriptionId,
        ClinicalPrescriptionPharmacyRouteRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.RoutePrescriptionToPharmacyAsync(prescriptionId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("RouteClinicalPrescriptionToPharmacy");

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
        var mutation = await repository.ApprovePrescriptionRefillRequestAsync(
            messageId,
            request,
            session.Username,
            cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ApproveClinicalPrescriptionRefillRequest");

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
    .WithName("DecideClinicalPrescriptionRefillRequest");

clinicalLists.MapDelete("/prescriptions/{prescriptionId}", async (
        ClinicalListRepository repository,
        string prescriptionId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeletePrescriptionAsync(prescriptionId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteClinicalPrescription");

clinicalLists.MapPost("/immunizations", async (
        ClinicalListRepository repository,
        ClinicalImmunizationCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateImmunizationAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Immunization could not be created from the supplied patient, vaccine, and administered date.")
            : Results.Created($"/api/clinical-lists/immunizations/{mutation.Id}", mutation);
    })
    .WithName("CreateClinicalImmunization");

clinicalLists.MapPut("/immunizations/{immunizationId:int}/entered-in-error", async (
        ClinicalListRepository repository,
        int immunizationId,
        ClinicalImmunizationErrorRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.MarkImmunizationEnteredInErrorAsync(immunizationId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("MarkClinicalImmunizationEnteredInError");

clinicalLists.MapDelete("/immunizations/{immunizationId:int}", async (
        ClinicalListRepository repository,
        int immunizationId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteImmunizationAsync(immunizationId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteClinicalImmunization");

var messages = app.MapGroup("/api/messages").WithTags("Messages");
RequireAccessPermission(messages, "patients", "notes", "view");

messages.MapGet("/inbox", async (
        MessageRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        string? status,
        string? assignment,
        string? patient,
        string? subject,
        string? priority,
        string? owner,
        int? minimumAgeDays,
        int? maximumAgeDays,
        int? offset,
        int? limit,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var query = new StaffMessageInboxQuery(
            Status: status,
            Assignment: assignment,
            Patient: patient,
            Subject: subject,
            Priority: priority,
            Owner: owner,
            MinimumAgeDays: minimumAgeDays,
            MaximumAgeDays: maximumAgeDays,
            Offset: offset ?? 0,
            Limit: limit ?? 25);
        return Results.Ok(await repository.GetInboxAsync(session.Username, query, cancellationToken));
    })
    .WithName("GetStaffMessageInbox");

messages.MapGet("/{patientId}", async (
        MessageRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var patientMessages = await repository.GetForPatientAsync(patientId, cancellationToken);
        return patientMessages is null ? Results.NotFound() : Results.Ok(patientMessages);
    })
    .WithName("GetPatientMessages");

messages.MapPost("/", async (
        MessageRepository repository,
        PatientMessageCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient message could not be created from the supplied patient, title, and body.")
            : Results.Created($"/api/messages/{mutation.Id}", mutation);
    })
    .WithName("CreatePatientMessage")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "addonly"));

messages.MapPut("/{messageId}/status", async (
        MessageRepository repository,
        string messageId,
        PatientMessageStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateStatusAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageStatus")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

messages.MapPut("/{messageId}/content", async (
        MessageRepository repository,
        string messageId,
        PatientMessageContentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateContentAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

messages.MapPut("/{messageId}/assignment", async (
        MessageRepository repository,
        string messageId,
        PatientMessageAssignmentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateAssignmentAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageAssignment")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

messages.MapPut("/{messageId}/reply", async (
        MessageRepository repository,
        string messageId,
        PatientMessageReplyRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ReplyAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ReplyToPatientMessage")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

messages.MapPut("/{messageId}/soft-delete", async (
        MessageRepository repository,
        string messageId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SoftDeleteAsync(messageId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("SoftDeletePatientMessage")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

messages.MapDelete("/{messageId}", async (
        MessageRepository repository,
        string messageId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(messageId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeletePatientMessage")
    .AddEndpointFilter(AccessPermissionFilter("patients", "notes", "write"));

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
addressBook.MapPost("/", async (AddressBookRepository repository, AddressBookContactRequest request, CancellationToken cancellationToken) => { try { var item=await repository.SaveAsync(null,request,cancellationToken); return Results.Created($"/api/administration/address-book/{item!.Id}",item); } catch(ArgumentException e) { return Results.ValidationProblem(new Dictionary<string,string[]> { ["contact"]=[e.Message] }); } }).WithName("CreateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin","practice","write"));
addressBook.MapPut("/{contactId:int}", async (AddressBookRepository repository,int contactId,AddressBookContactRequest request,CancellationToken cancellationToken)=> { try { var item=await repository.SaveAsync(contactId,request,cancellationToken);return item is null?Results.NotFound():Results.Ok(item); }catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["contact"]=[e.Message] });}}).WithName("UpdateAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin","practice","write"));
addressBook.MapDelete("/{contactId:int}", async (AddressBookRepository repository,int contactId,CancellationToken cancellationToken)=>await repository.DeleteAsync(contactId,cancellationToken)?Results.NoContent():Results.NotFound()).WithName("DeleteAddressBookContact").AddEndpointFilter(AccessPermissionFilter("admin","practice","write"));

var tracks = app.MapGroup("/api/administration/tracks").WithTags("Track Anything");
RequireAccessPermission(tracks, "admin", "practice", "view");
tracks.MapGet("/", async (TrackAnythingRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetTrackAnythingTypes");
tracks.MapPost("/", async (TrackAnythingRepository repository, TrackAnythingRequest request, CancellationToken cancellationToken) => { try { var item=await repository.SaveAsync(null,request,cancellationToken);return Results.Created($"/api/administration/tracks/{item!.Id}",item); }catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["track"]=[e.Message] });}}).WithName("CreateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin","practice","write"));
tracks.MapPut("/{trackId:int}", async (TrackAnythingRepository repository,int trackId,TrackAnythingRequest request,CancellationToken cancellationToken)=>{try{var item=await repository.SaveAsync(trackId,request,cancellationToken);return item is null?Results.NotFound():Results.Ok(item);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["track"]=[e.Message] });}}).WithName("UpdateTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin","practice","write"));
tracks.MapDelete("/{trackId:int}", async (TrackAnythingRepository repository,int trackId,CancellationToken cancellationToken)=>await repository.DeleteAsync(trackId,cancellationToken)?Results.NoContent():Results.NotFound()).WithName("DeleteTrackAnythingType").AddEndpointFilter(AccessPermissionFilter("admin","super","write"));

var patientEducation = app.MapGroup("/api/patient-education").WithTags("Patient Education");
RequireAccessPermission(patientEducation, "patients", "demo", "view");
patientEducation.MapGet("/resources", async (PatientEducationRepository repository,CancellationToken cancellationToken)=>Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetPatientEducationResources");
patientEducation.MapPost("/search", async (PatientEducationRepository repository,PatientEducationSearchRequest request,CancellationToken cancellationToken)=>{var result=await repository.SearchAsync(request,cancellationToken);return result is null?Results.BadRequest("An active HTTPS resource and search text are required."):Results.Ok(result);}).WithName("SearchPatientEducation");
var recalls=app.MapGroup("/api/recalls").WithTags("Recalls");RequireAccessPermission(recalls,"patients","appt","view");
recalls.MapGet("/",async(RecallRepository repository,CancellationToken ct)=>Results.Ok(await repository.GetAsync(ct))).WithName("GetRecalls");
recalls.MapPost("/",async(RecallRepository repository,RecallRequest request,CancellationToken ct)=>{var item=await repository.CreateAsync(request,ct);return item is null?Results.BadRequest():Results.Created($"/api/recalls/{item.Id}",item);}).WithName("CreateRecall").AddEndpointFilter(AccessPermissionFilter("patients","appt","write"));
recalls.MapDelete("/{id:guid}",async(RecallRepository repository,Guid id,CancellationToken ct)=>await repository.DeleteAsync(id,ct)?Results.NoContent():Results.NotFound()).WithName("DeleteRecall").AddEndpointFilter(AccessPermissionFilter("patients","appt","write"));
recalls.MapGet("/{id:guid}/activity",async(RecallRepository repository,Guid id,CancellationToken ct)=>{var activity=await repository.GetActivityAsync(id,ct);return activity is null?Results.NotFound():Results.Ok(activity);}).WithName("GetRecallActivity");
recalls.MapPost("/{id:guid}/activity",async(RecallRepository repository,Guid id,RecallActivityRequest request,CancellationToken ct)=>{try{var result=await repository.AddActivityAsync(id,request,ct);return result is null?Results.NotFound():Results.Created($"/api/recalls/{id}/activity/{result.Id}",result);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["activity"]=[e.Message] });}}).WithName("AddRecallActivity").AddEndpointFilter(AccessPermissionFilter("patients","appt","write"));

var batchCommunication=app.MapGroup("/api/batch-communication").WithTags("Batch Communication");RequireAccessPermission(batchCommunication,"admin","batchcom","view");
batchCommunication.MapPost("/preview",async(BatchCommunicationRepository repository,BatchCommunicationPreviewRequest request,CancellationToken ct)=>{try{return Results.Ok(await repository.PreviewAsync(request,ct));}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["filter"]=[e.Message] });}}).WithName("PreviewBatchCommunication");
batchCommunication.MapPost("/campaigns",async(BatchCommunicationRepository repository,BatchCommunicationCampaignCreateRequest request,CancellationToken ct)=>{try{var campaign=await repository.CreateAsync(request,ct);return Results.Created($"/api/batch-communication/campaigns/{campaign.Campaign.Id}",campaign);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["campaign"]=[e.Message] });}}).WithName("CreateBatchCommunicationCampaign").AddEndpointFilter(AccessPermissionFilter("admin","batchcom","write"));
batchCommunication.MapGet("/campaigns",async(BatchCommunicationRepository repository,CancellationToken ct)=>Results.Ok(await repository.GetAsync(ct))).WithName("GetBatchCommunicationCampaigns");
batchCommunication.MapGet("/campaigns/{id:guid}",async(BatchCommunicationRepository repository,Guid id,CancellationToken ct)=>{var campaign=await repository.GetAsync(id,ct);return campaign is null?Results.NotFound():Results.Ok(campaign);}).WithName("GetBatchCommunicationCampaign");
batchCommunication.MapGet("/campaigns/{id:guid}/output",async(BatchCommunicationRepository repository,Guid id,CancellationToken ct)=>{var campaign=await repository.GetAsync(id,ct);if(campaign is null)return Results.NotFound();var csv=new System.Text.StringBuilder("Patient ID,Name,Email,Home Phone,Cell Phone,Postal Code,Next Appointment,Last Appointment,Last Visit,Subject,Body\n");foreach(var item in campaign.Recipients)csv.AppendLine(string.Join(',',new[]{item.PatientId,item.DisplayName,item.Email,item.PhoneHome,item.PhoneCell,item.PostalCode,item.NextAppointmentDate,item.LastAppointmentDate,item.LastVisitDate,item.RenderedSubject,item.RenderedBody}.Select(value=>$"\"{(value??string.Empty).Replace("\"","\"\"")}\"")));return Results.File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()),"text/csv",$"batch-communication-{id}.csv");}).WithName("ExportBatchCommunicationCampaign");

var chartTracker=app.MapGroup("/api/chart-tracker").WithTags("Chart Tracker");RequireAccessPermission(chartTracker,"patients","appt","view");
chartTracker.MapGet("/options",async(ChartTrackerRepository repository,CancellationToken ct)=>Results.Ok(await repository.GetOptionsAsync(ct))).WithName("GetChartTrackerOptions");
chartTracker.MapGet("/lookup/{identifier}",async(ChartTrackerRepository repository,string identifier,CancellationToken ct)=>{var patient=await repository.FindAsync(identifier,ct);return patient is null?Results.NotFound():Results.Ok(patient);}).WithName("LookupChartTrackerPatient");
chartTracker.MapGet("/patients/{patientId}/history",async(ChartTrackerRepository repository,string patientId,CancellationToken ct)=>{var history=await repository.GetHistoryAsync(patientId,ct);return history is null?Results.NotFound():Results.Ok(history);}).WithName("GetChartTrackerHistory");
chartTracker.MapPost("/patients/{patientId}/events",async(ChartTrackerRepository repository,string patientId,ChartTrackerUpdateRequest request,CancellationToken ct)=>{try{var item=await repository.RecordAsync(patientId,request,ct);return item is null?Results.NotFound():Results.Created($"/api/chart-tracker/patients/{patientId}/events/{item.Id}",item);}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]> { ["tracker"]=[e.Message] });}}).WithName("RecordChartTrackerEvent").AddEndpointFilter(AccessPermissionFilter("patients","appt","write"));

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

var documents = app.MapGroup("/api/documents").WithTags("Documents");
RequireAccessPermission(documents, "patients", "docs", "view");

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

documents.MapDelete("/{documentId:int}", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(documentId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeletePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs_rm", "write"));

var procedures = app.MapGroup("/api/procedures").WithTags("Procedures");
RequireAccessPermission(procedures, "patients", "lab", "view");

procedures.MapGet("/lab-provider-address-book", async (
        ProcedureRepository repository,
        CancellationToken cancellationToken) =>
    {
        var addressBook = await repository.GetLabProviderAddressBookAsync(cancellationToken);
        return Results.Ok(addressBook);
    })
    .WithName("GetProcedureLabProviderAddressBook");

procedures.MapPost("/lab-provider-address-book", async (
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
        var import = await repository.ImportOrderCatalogCompendiumAsync(request, cancellationToken);
        return import is null
            ? Results.BadRequest(new { error = "Procedure order catalog compendium import requires a valid vendor format, group, lab, and CSV payload." })
            : Results.Ok(import);
    })
    .WithName("ImportProcedureOrderCatalogCompendium")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/order-catalog/{itemId:int}", async (
        ProcedureRepository repository,
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
        ProcedureRepository repository,
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
            cancellationToken);
        return Results.Ok(queue);
    })
    .WithName("GetProcedureReportReviewQueue");

procedures.MapGet("/order-queue", async (
        ProcedureRepository repository,
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
            cancellationToken);
        return Results.Ok(queue);
    })
    .WithName("GetProcedureOrderQueue");

procedures.MapGet("/{patientId}", async (
        ProcedureRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var procedureResults = await repository.GetForPatientAsync(patientId, cancellationToken);
        return procedureResults is null ? Results.NotFound() : Results.Ok(procedureResults);
    })
    .WithName("GetProcedureResultsForPatient");

procedures.MapPost("/orders", async (
        ProcedureRepository repository,
        ProcedureOrderCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateOrderAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure order could not be created from the supplied patient, encounter, and order details.")
            : Results.Created($"/api/procedures/orders/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPut("/orders/{orderId:int}/status", async (
        ProcedureRepository repository,
        int orderId,
        ProcedureOrderStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateOrderStatusAsync(orderId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureOrderStatus")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/orders/{orderId:int}/transmit", async (
        ProcedureRepository repository,
        int orderId,
        ProcedureOrderTransmitRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.TransmitOrderAsync(orderId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure order could not be marked transmitted from the supplied order state.")
            : Results.Ok(mutation);
    })
    .WithName("TransmitProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/orders/{orderId:int}", async (
        ProcedureRepository repository,
        int orderId,
        ProcedureOrderUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateOrderAsync(orderId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure order could not be updated from the supplied order details.")
            : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureOrder")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPost("/reports", async (
        ProcedureRepository repository,
        ProcedureReportCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateReportAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report could not be created from the supplied order and report details.")
            : Results.Created($"/api/procedures/reports/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPut("/reports/{reportId:int}", async (
        ProcedureRepository repository,
        int reportId,
        ProcedureReportUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateReportAsync(reportId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report could not be updated from the supplied report details.")
            : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/{reportId:int}/sign", async (
        ProcedureRepository repository,
        int reportId,
        ProcedureReportSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SignReportAsync(reportId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report could not be signed from the supplied review details.")
            : Results.Ok(mutation);
    })
    .WithName("SignProcedureReport")
    .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

procedures.MapPut("/reports/{reportId:int}/review-assignment", async (
        ProcedureRepository repository,
        int reportId,
        ProcedureReportReviewAssignmentRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.AssignReportReviewerAsync(reportId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report reviewer assignment could not be saved from the supplied details.")
            : Results.Ok(mutation);
    })
    .WithName("AssignProcedureReportReviewer")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/{reportId:int}/reopen-review", async (
        ProcedureRepository repository,
        int reportId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ReopenReportReviewAsync(reportId, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure report review could not be reopened.")
            : Results.Ok(mutation);
    })
    .WithName("ReopenProcedureReportReview")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapPut("/reports/bulk-sign", async (
        ProcedureRepository repository,
        ProcedureReportBulkSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.BulkSignReportsAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure reports could not be bulk signed from the supplied review details.")
            : Results.Ok(mutation);
    })
    .WithName("BulkSignProcedureReports")
    .AddEndpointFilter(AccessPermissionFilter("patients", "sign", "write"));

procedures.MapPost("/specimens", async (
        ProcedureRepository repository,
        ProcedureSpecimenCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateSpecimenAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure specimen could not be created from the supplied order and specimen details.")
            : Results.Created($"/api/procedures/specimens/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureSpecimen")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPost("/results", async (
        ProcedureRepository repository,
        ProcedureResultCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateResultAsync(request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Procedure result could not be created from the supplied report and result details.")
            : Results.Created($"/api/procedures/results/{mutation.Id}", mutation);
    })
    .WithName("CreateProcedureResult")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "addonly"));

procedures.MapPut("/results/{resultId:int}", async (
        ProcedureRepository repository,
        int resultId,
        ProcedureResultUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateResultAsync(resultId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureResult")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

procedures.MapDelete("/orders/{orderId:int}", async (
        ProcedureRepository repository,
        int orderId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteOrderCascadeAsync(orderId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteProcedureOrderCascade")
    .AddEndpointFilter(AccessPermissionFilter("patients", "lab", "write"));

var integrations = app.MapGroup("/api/integrations").WithTags("Integrations");
RequireAccessPermission(integrations, "admin", "super", "write");

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
        IntegrationOutboxQueueRequest request,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var message = await repository.QueueAsync(request, cancellationToken);
            return Results.Created($"/api/integrations/outbox/{message.EventId}", message);
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
        Guid eventId,
        CancellationToken cancellationToken) =>
    {
        var dispatch = await repository.DispatchAsync(eventId, cancellationToken);
        return dispatch is null ? Results.NotFound() : Results.Ok(dispatch);
    })
    .WithName("DispatchIntegrationOutbox");

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
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
        }
    })
    .WithName("ReceiveIntegrationInbox");

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
        string? witnessUsername = null;
        if (request.WitnessSessionId is { } witnessSessionId)
        {
            var witness = await authRepository.GetCurrentSessionAsync(witnessSessionId, cancellationToken);
            if (!witness.Authenticated)
                throw new ArgumentException("The controlled-custody witness session is not active.");
            if (string.Equals(witness.Username, session.Username, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The controlled-custody witness must be a different authenticated user.");
            witnessUsername = witness.Username;
        }
        var movement = await repository.CreateControlledCustodyMovementAsync(request, session.Username, witnessUsername, cancellationToken);
        return Results.Created($"/api/inventory/controlled-custody-movements/{movement.Event.EventId}", movement);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["controlledCustodyMovement"] = [exception.Message] });
    }
})
    .WithName("CreateInventoryControlledCustodyMovement")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "write"));

inventory.MapGet("/controlled-custody-lots/{lotId:int}/history", async (int lotId, InventoryRepository repository, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await repository.GetControlledCustodyLotHistoryAsync(lotId, cancellationToken)); }
    catch (ArgumentException exception) { return Results.NotFound(new { error = exception.Message }); }
})
    .WithName("GetInventoryControlledCustodyLotHistory")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

inventory.MapPost("/controlled-count-sessions", async (InventoryControlledCountSessionCreateRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken);var count=await repository.CreateControlledCountSessionAsync(request,session.Username,cancellationToken);return Results.Created($"/api/inventory/controlled-count-sessions/{count.SessionId}",count); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledCount"]=[exception.Message] });} })
    .WithName("CreateInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapGet("/controlled-count-sessions/{sessionId:guid}", async (Guid sessionId, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetControlledCountSessionAsync(sessionId,cancellationToken)); } catch(ArgumentException exception){return Results.NotFound(new { error=exception.Message });} })
    .WithName("GetInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapGet("/controlled-count-sessions", async (int? limit, InventoryRepository repository, CancellationToken cancellationToken) =>
{ try { return Results.Ok(await repository.GetControlledCountSessionsAsync(limit ?? 30,cancellationToken)); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledCount"]=[exception.Message] });} })
    .WithName("GetInventoryControlledCountSessions")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

inventory.MapPost("/controlled-count-sessions/{sessionId:guid}/submit", async (Guid sessionId, InventoryControlledCountSubmitRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken);var counter=await authRepository.GetCurrentSessionAsync(request.CounterSessionId,cancellationToken);if(!counter.Authenticated)throw new ArgumentException("The controlled count counter session is not active.");if(string.Equals(session.Username,counter.Username,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The controlled count counter must be a different authenticated user.");return Results.Ok(await repository.SubmitControlledCountSessionAsync(sessionId,request,session.Username,counter.Username,cancellationToken)); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledCount"]=[exception.Message] });} })
    .WithName("SubmitInventoryControlledCountSession")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPut("/controlled-count-discrepancies/{discrepancyId:guid}/investigation", async (Guid discrepancyId, InventoryControlledDiscrepancyInvestigationRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken);return Results.Ok(await repository.InvestigateControlledCountDiscrepancyAsync(discrepancyId,request,session.Username,cancellationToken)); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledDiscrepancy"]=[exception.Message] });} })
    .WithName("InvestigateInventoryControlledCountDiscrepancy")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/corrections", async (Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken);string? witnessUsername=null;if(request.WitnessSessionId is { } witnessId){var witness=await authRepository.GetCurrentSessionAsync(witnessId,cancellationToken);if(!witness.Authenticated)throw new ArgumentException("The controlled-custody witness session is not active.");if(string.Equals(witness.Username,session.Username,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The controlled-custody witness must be a different authenticated user.");witnessUsername=witness.Username;}var correction=await repository.CorrectControlledCountDiscrepancyAsync(discrepancyId,request,session.Username,witnessUsername,cancellationToken);return Results.Created($"/api/inventory/controlled-custody-movements/{correction.Event.EventId}",correction); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledDiscrepancy"]=[exception.Message] });} })
    .WithName("CorrectInventoryControlledCountDiscrepancy")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "write"));

inventory.MapPost("/controlled-count-discrepancies/{discrepancyId:guid}/close", async (Guid discrepancyId, InventoryControlledDiscrepancyCloseRequest request, InventoryRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{ try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken);return Results.Ok(await repository.CloseControlledCountDiscrepancyAsync(discrepancyId,request,session.Username,cancellationToken)); } catch(ArgumentException exception){return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledDiscrepancy"]=[exception.Message] });} })
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
            return Results.File(Encoding.UTF8.GetBytes(csv), contentType: "text/csv", fileDownloadName: "legacy-ehr-inventory-activity.csv");
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
        var mutation = await repository.CreateLineAsync(request, cancellationToken);
        return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/lines/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingLine")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPut("/lines/{billingLineId}", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateLineAsync(billingLineId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingLine")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapPut("/lines/{billingLineId}/status", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateLineStatusAsync(billingLineId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingLineStatus")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapDelete("/lines/{billingLineId}", async (
        BillingRepository repository,
        string billingLineId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteLineAsync(billingLineId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingLine")
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
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.AdjudicateClaimAsync(claimId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("AdjudicateBillingClaim")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

billing.MapDelete("/claims/{claimId}", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteClaimAsync(claimId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingClaimStatus")
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
        CancellationToken cancellationToken) =>
    {
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

billing.MapDelete("/payments/{activityId}", async (
        BillingRepository repository,
        string activityId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeletePaymentAsync(activityId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingPaymentPosting")
    .AddEndpointFilter(AccessPermissionFilter("acct", "bill", "write"));

var administration = app.MapGroup("/api/administration").WithTags("Administration");
RequireAccessPermission(administration, "admin", "acl", "write");

administration.MapGet("/experience-baseline", () =>
    Results.Ok(ExperienceBaselineCatalog.Build()))
    .WithName("GetExperienceBaseline");

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
        try { return Results.File(Encoding.UTF8.GetBytes(await repository.GetCsvAsync(limit ?? 200, username, from, to, cancellationToken)), "text/csv", "legacy-ehr-phi-access-audit.csv"); }
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
        AdministrationRepository repository,
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
        AdministrationRepository repository,
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
        AdministrationRepository repository,
        int userId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteUserAsync(userId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAdministrationUser");

administration.MapPost("/facilities", async (
        AdministrationRepository repository,
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
        AdministrationRepository repository,
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
        AdministrationRepository repository,
        int facilityId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteFacilityAsync(facilityId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAdministrationFacility");

administration.MapPut("/access-control/group-permissions", async (
        AdministrationRepository repository,
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
        AdministrationRepository repository,
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
        AdministrationRepository repository,
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
        AdministrationRepository repository,
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
            $"legacy-ehr-controlled-inventory-{runId}.csv");
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
            $"legacy-ehr-controlled-activity-{runId}.csv");
}).WithName("ExportControlledInventoryActivityRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "lots", "view"));

reports.MapPost("/controlled-inventory/count-variance", async (ControlledCountVarianceReportRequest request, ReportRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try { var session=await GetSessionFromHeaderAsync(authRepository,httpContext,cancellationToken); return Results.Created("/api/reports/controlled-inventory/count-variance",await repository.RunControlledCountVarianceReportAsync(request,session.Username,cancellationToken)); }
    catch(ArgumentException exception){ return Results.ValidationProblem(new Dictionary<string,string[]> { ["controlledReport"]=[exception.Message] }); }
}).WithName("RunControlledCountVarianceReport").AddEndpointFilter(AccessPermissionFilter("inventory","adjustments","view"));

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
            $"legacy-ehr-controlled-count-variance-{runId}.csv");
}).WithName("ExportControlledCountVarianceRun")
    .AddEndpointFilter(AccessPermissionFilter("inventory", "adjustments", "view"));

reports.MapGet("/operational/export", async (
        ReportRepository repository,
        CancellationToken cancellationToken) =>
    {
        var csv = await repository.GetOperationalReportsCsvAsync(cancellationToken);
        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            contentType: "text/csv",
            fileDownloadName: "legacy-ehr-operational-report.csv");
    })
    .WithName("ExportOperationalReports");

reports.MapGet("/families", (ReportRepository repository) => Results.Ok(repository.GetFamilies())).WithName("GetReportFamilies");
reports.MapGet("/families/{family}/export", async (ReportRepository repository, string family, DateOnly? from, DateOnly? to, CancellationToken cancellationToken) =>
{
    try { var csv = await repository.GetFamilyCsvAsync(family, from, to, cancellationToken); return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"legacy-ehr-{family}-report.csv"); }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["report"] = [exception.Message] }); }
}).WithName("ExportReportFamily");

reports.MapGet("/definitions", async (ReportRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetSavedDefinitionsAsync(cancellationToken)))
    .WithName("GetSavedReportDefinitions");

reports.MapPost("/definitions", async (
        ReportRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        SavedReportDefinitionRequest request,
        CancellationToken cancellationToken) =>
    {
        try { var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken); return Results.Created("/api/reports/definitions", await repository.CreateSavedDefinitionAsync(request, session.Username, cancellationToken)); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    })
    .WithName("CreateSavedReportDefinition")
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

reports.MapPost("/definitions/{definitionId:guid}/run", async (
        ReportRepository repository,
        AuthRepository authRepository,
        HttpContext httpContext,
        Guid definitionId,
        CancellationToken cancellationToken) =>
    {
        var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
        var run = await repository.RunSavedDefinitionAsync(definitionId, session.Username, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(run);
    })
    .WithName("RunSavedReportDefinition")
    .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

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
            entry => entry.Value.Status.ToString().ToLowerInvariant())
    });
}

static void RequireAccessPermission(
    RouteGroupBuilder group,
    string sectionValue,
    string permissionValue,
    string returnValue)
{
    group.AddEndpointFilter(AccessPermissionFilter(sectionValue, permissionValue, returnValue));
}

static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> AccessPermissionFilter(
    string sectionValue,
    string permissionValue,
    string returnValue)
{
    var policy = AuthorizationPolicyCatalog.Require(
        sectionValue,
        permissionValue,
        returnValue);
    return async (context, next) =>
    {
        var repository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
        var phiAuditRepository = context.HttpContext.RequestServices.GetRequiredService<PhiAuditRepository>();
        var session = await GetSessionFromHeaderAsync(repository, context.HttpContext, context.HttpContext.RequestAborted);
        if (!session.Authenticated)
        {
            return Results.Json(session, statusCode: StatusCodes.Status401Unauthorized);
        }

        var authorized = await repository.HasAccessPermissionAsync(
            session.Username,
            sectionValue,
            permissionValue,
            returnValue,
            context.HttpContext.RequestAborted);
        if (!authorized)
        {
            await phiAuditRepository.RecordAccessDecisionAsync(
                session,
                context.HttpContext.Request.Method,
                context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                authorized: false,
                responseStatus: StatusCodes.Status403Forbidden,
                context.HttpContext.RequestAborted);
            return Results.Json(new AuthAuthorizationFailureResponse(
                Authenticated: true,
                Authorized: false,
                SessionId: session.SessionId,
                Username: session.Username,
                Role: session.Role,
                RequiredSection: sectionValue,
                RequiredPermission: permissionValue,
                RequiredReturnValue: returnValue,
                FailureReason: $"User '{session.Username}' is not authorized for {sectionValue}:{permissionValue} {returnValue}.",
                SessionSource: session.SessionSource), statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            var result = await next(context);
            await phiAuditRepository.RecordAccessDecisionAsync(
                session,
                context.HttpContext.Request.Method,
                context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                authorized: true,
                responseStatus: context.HttpContext.Response.StatusCode,
                context.HttpContext.RequestAborted);
            return result;
        }
        catch
        {
            await phiAuditRepository.RecordAccessDecisionAsync(
                session,
                context.HttpContext.Request.Method,
                context.HttpContext.GetEndpoint()?.DisplayName ?? "unmatched",
                $"{policy.PolicyId}@{AuthorizationPolicyCatalog.Revision}",
                authorized: true,
                responseStatus: StatusCodes.Status500InternalServerError,
                context.HttpContext.RequestAborted);
            throw;
        }
    };
}

static async Task<AuthSessionResponse> GetSessionFromHeaderAsync(
    AuthRepository repository,
    HttpContext httpContext,
    CancellationToken cancellationToken)
{
    var header = httpContext.Request.Headers["X-Legacy EHR-Session"].ToString();
    if (!Guid.TryParse(header, out var sessionId))
    {
        return new AuthSessionResponse(
            Authenticated: false,
            SessionId: null,
            Username: string.Empty,
            DisplayName: string.Empty,
            Role: string.Empty,
            StaffId: null,
            CreatedAt: null,
            LastSeenAt: null,
            ExpiresAt: null,
            EndedAt: null,
            FailureReason: "A valid Legacy EHR session is required.",
            SessionSource: "avenchart");
    }

    return await repository.GetCurrentSessionAsync(sessionId, cancellationToken);
}
