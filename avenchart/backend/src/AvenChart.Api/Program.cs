using System.Text;
using Npgsql;
using AvenChart.Api.Data;
using AvenChart.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("AvenChart")
    ?? "Host=localhost;Port=5433;Database=legacy-ehr_modernized;Username=legacy-ehr;Password=legacy-ehr_demo";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<PatientRepository>();
builder.Services.AddScoped<AppointmentRepository>();
builder.Services.AddScoped<EncounterRepository>();
builder.Services.AddScoped<ClinicalListRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<DocumentRepository>();
builder.Services.AddScoped<ProcedureRepository>();
builder.Services.AddScoped<BillingRepository>();
builder.Services.AddScoped<AdministrationRepository>();
builder.Services.AddScoped<ReportRepository>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<PatientPortalRepository>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("local-app-clients");

app.MapGet("/health", () => Results.Ok(new HealthResponse(
    Status: "healthy",
    Application: "avenchart-api",
    CheckedAtUtc: DateTimeOffset.UtcNow)));

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
    .WithName("GetLoginAudit");

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

var patients = app.MapGroup("/api/patients").WithTags("Patients");
RequireAccessPermission(patients, "patients", "demo", "view");

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

patients.MapGet("/provider-options", async (
        PatientRepository repository,
        CancellationToken cancellationToken) =>
    {
        var options = await repository.GetProviderAssignmentOptionsAsync(cancellationToken);
        return Results.Ok(options);
    })
    .WithName("GetPatientProviderAssignmentOptions")
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

patients.MapPut("/{patientId}/contact", async (
        PatientRepository repository,
        string patientId,
        PatientContactUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateContactAsync(patientId, request, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("UpdatePatientContact")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/{patientId}/demographics", async (
        PatientRepository repository,
        string patientId,
        PatientDemographicsUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateDemographicsAsync(patientId, request, cancellationToken);
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
        string patientId,
        PatientProviderAssignmentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateProviderAssignmentAsync(patientId, request, cancellationToken);
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
        string patientId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.CreateInsuranceAsync(patientId, request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Insurance coverage could not be created from the supplied patient and coverage details.")
            : Results.Created($"/api/patients/{patient.CanonicalId}", patient);
    })
    .WithName("CreatePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapPut("/insurance/{insuranceId}", async (
        PatientRepository repository,
        string insuranceId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateInsuranceAsync(insuranceId, request, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("UpdatePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

patients.MapDelete("/insurance/{insuranceId}", async (
        PatientRepository repository,
        string insuranceId,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.DeleteInsuranceAsync(insuranceId, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("DeletePatientInsurance")
    .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

var appointments = app.MapGroup("/api/appointments").WithTags("Appointments");
RequireAccessPermission(appointments, "patients", "appt", "view");

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
                ? Results.BadRequest("Appointment reminder could not be dispatched because the appointment was not found or no reminder is due.")
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
            ? Results.BadRequest("Appointment reminder could not be retried because no prior dispatch exists or no reminder is due.")
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

        var appointment = await repository.CreateAsync(request, cancellationToken);
        return appointment is null
            ? Results.BadRequest("Appointment could not be created from the supplied patient, date, time, and duration.")
            : Results.Created($"/api/appointments/{appointment.Id}", appointment);
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
        var appointment = await repository.UpdateAsync(appointmentId, request, cancellationToken);
        return appointment is null
            ? Results.BadRequest("Appointment could not be updated from the supplied date, time, and duration.")
            : Results.Ok(appointment);
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
        CancellationToken cancellationToken) =>
    {
        var response = await repository.SearchAsync(patientId, from, limit ?? 25, cancellationToken);
        return Results.Ok(response);
    })
    .WithName("SearchEncounters");

encounters.MapGet("/soap-note-templates", async (
        EncounterRepository repository,
        CancellationToken cancellationToken) =>
    {
        var response = await repository.GetSoapNoteTemplateCatalogAsync(cancellationToken);
        return Results.Ok(response);
    })
    .WithName("GetEncounterSoapNoteTemplates")
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
        int encounter,
        EncounterUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await repository.UpdateSummaryAsync(encounter, request, cancellationToken);
        return encounterDetail is null ? Results.NotFound() : Results.Ok(encounterDetail);
    })
    .WithName("UpdateEncounter")
    .AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

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

        var mutation = await documentRepository.UpdateMetadataAsync(documentId, request with
        {
            Encounter = encounter
        }, cancellationToken);
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

        var mutation = await documentRepository.UpdateMetadataAsync(documentId, new PatientDocumentMetadataUpdateRequest(
            CategoryId: document.CategoryId,
            Name: document.Name,
            DocDate: document.DocDate,
            Encounter: targetDetail.Encounter,
            Notes: document.Notes), cancellationToken);
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

        var mutation = await documentRepository.ReplaceContentAsync(documentId, request, cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter document content could not be replaced from the supplied text payload.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
    })
    .WithName("ReplaceEncounterDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/content/binary", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
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

        var mutation = await documentRepository.ReplaceBinaryContentAsync(documentId, request, cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter binary document content could not be replaced from the supplied file payload.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
    })
    .WithName("ReplaceEncounterDocumentBinaryContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/soft-delete", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        int encounter,
        int documentId,
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

        var mutation = await documentRepository.SoftDeleteAsync(documentId, cancellationToken);
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
    })
    .WithName("SoftDeleteEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/restore", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
        int encounter,
        int documentId,
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

        var mutation = await documentRepository.RestoreAsync(documentId, cancellationToken);
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
    })
    .WithName("RestoreEncounterDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

encounters.MapPut("/{encounter:int}/documents/{documentId:int}/sign", async (
        EncounterRepository encounterRepository,
        DocumentRepository documentRepository,
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

        var mutation = await documentRepository.SignAsync(documentId, request, cancellationToken);
        if (mutation is null)
        {
            return Results.BadRequest("Encounter document could not be signed from the supplied review details.");
        }

        var refreshed = await encounterRepository.GetByEncounterAsync(encounter, cancellationToken);
        return refreshed is null
            ? Results.NotFound()
            : Results.Ok(new EncounterDocumentMutationResponse(documentId, refreshed));
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
        int messageId,
        ClinicalPrescriptionRefillApprovalRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ApprovePrescriptionRefillRequestAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("ApproveClinicalPrescriptionRefillRequest");

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

var documents = app.MapGroup("/api/documents").WithTags("Documents");
RequireAccessPermission(documents, "patients", "docs", "view");

documents.MapGet("/ocr-queue", async (
        DocumentRepository repository,
        CancellationToken cancellationToken,
        string? patientId = null) =>
    {
        var queue = await repository.GetOcrQueueAsync(cancellationToken, patientId);
        return Results.Ok(queue);
    })
    .WithName("GetPatientDocumentOcrQueue");

documents.MapGet("/routing-queue", async (
        DocumentRepository repository,
        CancellationToken cancellationToken,
        string? patientId = null) =>
    {
        var queue = await repository.GetRoutingQueueAsync(cancellationToken, patientId);
        return Results.Ok(queue);
    })
    .WithName("GetPatientDocumentRoutingQueue");

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
        int documentId,
        PatientDocumentOcrCompleteRequest request,
        CancellationToken cancellationToken) =>
    {
        var completion = await repository.CompleteOcrAsync(documentId, request, cancellationToken);
        return completion is null
            ? Results.BadRequest("Patient document OCR could not be completed from the supplied document and extracted text.")
            : Results.Ok(completion);
    })
    .WithName("CompletePatientDocumentOcr")
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
        PatientDocumentScannerCaptureRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateScannerCaptureAsync(request, cancellationToken);
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

documents.MapPut("/{documentId:int}/metadata", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentMetadataUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateMetadataAsync(documentId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient document metadata could not be updated from the supplied filing details.")
            : Results.Ok(mutation);
    })
    .WithName("UpdatePatientDocumentMetadata")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/content", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ReplaceContentAsync(documentId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Patient document content could not be replaced from the supplied text payload.")
            : Results.Ok(mutation);
    })
    .WithName("ReplacePatientDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/content/binary", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentBinaryContentReplaceRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.ReplaceBinaryContentAsync(documentId, request, cancellationToken);
        return mutation is null
            ? Results.BadRequest("Binary patient document content could not be replaced from the supplied file payload.")
            : Results.Ok(mutation);
    })
    .WithName("ReplaceBinaryPatientDocumentContent")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/soft-delete", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SoftDeleteAsync(documentId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("SoftDeletePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/restore", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.RestoreAsync(documentId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("RestorePatientDocument")
    .AddEndpointFilter(AccessPermissionFilter("patients", "docs", "write"));

documents.MapPut("/{documentId:int}/sign", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SignAsync(documentId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
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

administration.MapGet("/directory", async (
        AdministrationRepository repository,
        CancellationToken cancellationToken) =>
    {
        var directory = await repository.GetDirectoryAsync(cancellationToken);
        return Results.Ok(directory);
    })
    .WithName("GetAdministrationDirectory");

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
    return async (context, next) =>
    {
        var repository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
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

        return await next(context);
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
