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

builder.Services.AddCors(options =>
{
    options.AddPolicy("local-app-clients", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:5173",
                "http://127.0.0.1:5173")
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

patients.MapPost("/", async (
        PatientRepository repository,
        PatientRegistrationRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.CreatePatientAsync(request, cancellationToken);
        return patient is null
            ? Results.BadRequest("Patient could not be registered from the supplied identity, demographic, and contact details.")
            : Results.Created($"/api/patients/{patient.CanonicalId}", patient);
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
        var appointment = await repository.CreateAsync(request, cancellationToken);
        return appointment is null
            ? Results.BadRequest("Appointment could not be created from the supplied patient, date, time, and duration.")
            : Results.Created($"/api/appointments/{appointment.Id}", appointment);
    })
    .WithName("CreateAppointment");

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
    .WithName("UpdateAppointment");

appointments.MapPut("/{appointmentId}/status", async (
        AppointmentRepository repository,
        string appointmentId,
        AppointmentStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.UpdateStatusAsync(appointmentId, request, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(appointment);
    })
    .WithName("UpdateAppointmentStatus");

appointments.MapPost("/{appointmentId}/recurrence-exceptions/{occurrenceDate}/restore", async (
        AppointmentRepository repository,
        string appointmentId,
        string occurrenceDate,
        CancellationToken cancellationToken) =>
    {
        var appointment = await repository.RestoreRecurrenceExceptionAsync(appointmentId, occurrenceDate, cancellationToken);
        return appointment is null ? Results.NotFound() : Results.Ok(appointment);
    })
    .WithName("RestoreAppointmentOccurrence");

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
    .WithName("RescheduleAppointmentOccurrence");

appointments.MapDelete("/{appointmentId}", async (
        AppointmentRepository repository,
        string appointmentId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(appointmentId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteAppointment");

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
    .WithName("CreateBillingCollectionsFollowUp");

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

billing.MapPost("/lines", async (
        BillingRepository repository,
        BillingLineCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateLineAsync(request, cancellationToken);
        return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/lines/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingLine");

billing.MapPut("/lines/{billingLineId}", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateLineAsync(billingLineId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingLine");

billing.MapPut("/lines/{billingLineId}/status", async (
        BillingRepository repository,
        string billingLineId,
        BillingLineStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateLineStatusAsync(billingLineId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingLineStatus");

billing.MapDelete("/lines/{billingLineId}", async (
        BillingRepository repository,
        string billingLineId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteLineAsync(billingLineId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingLine");

billing.MapPost("/claims", async (
        BillingRepository repository,
        BillingClaimCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreateClaimAsync(request, cancellationToken);
        return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/claims/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingClaimStatus");

billing.MapPut("/claims/{claimId}/status", async (
        BillingRepository repository,
        string claimId,
        BillingClaimStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateClaimStatusAsync(claimId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateBillingClaimStatus");

billing.MapDelete("/claims/{claimId}", async (
        BillingRepository repository,
        string claimId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteClaimAsync(claimId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingClaimStatus");

billing.MapPost("/payments", async (
        BillingRepository repository,
        BillingPaymentCreateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.CreatePaymentAsync(request, cancellationToken);
        return mutation is null ? Results.BadRequest() : Results.Created($"/api/billing/payments/{mutation.Id}", mutation);
    })
    .WithName("CreateBillingPaymentPosting");

billing.MapPut("/payments/{activityId}/void", async (
        BillingRepository repository,
        string activityId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.VoidPaymentAsync(activityId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("VoidBillingPaymentPosting");

billing.MapDelete("/payments/{activityId}", async (
        BillingRepository repository,
        string activityId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeletePaymentAsync(activityId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteBillingPaymentPosting");

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
