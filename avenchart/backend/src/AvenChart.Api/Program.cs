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
builder.Services.AddScoped<ProcedureRepository>();
builder.Services.AddScoped<BillingRepository>();
builder.Services.AddScoped<AdministrationRepository>();
builder.Services.AddScoped<ReportRepository>();

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

var patients = app.MapGroup("/api/patients").WithTags("Patients");

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
    .WithName("UpdatePatientContact");

var appointments = app.MapGroup("/api/appointments").WithTags("Appointments");

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
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await repository.GetByEncounterAsync(encounter, cancellationToken);
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
    .WithName("CreateEncounter");

encounters.MapPut("/{encounter:int}", async (
        EncounterRepository repository,
        int encounter,
        EncounterUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var encounterDetail = await repository.UpdateSummaryAsync(encounter, request, cancellationToken);
        return encounterDetail is null ? Results.NotFound() : Results.Ok(encounterDetail);
    })
    .WithName("UpdateEncounter");

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
    .WithName("CreateEncounterVitals");

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
    .WithName("CreateEncounterSoapNote");

encounters.MapDelete("/{encounter:int}/vitals/{vitalsId:int}", async (
        EncounterRepository repository,
        int encounter,
        int vitalsId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteVitalsAsync(encounter, vitalsId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterVitals");

encounters.MapDelete("/{encounter:int}/soap-notes/{soapNoteId:int}", async (
        EncounterRepository repository,
        int encounter,
        int soapNoteId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteSoapNoteAsync(encounter, soapNoteId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterSoapNote");

encounters.MapDelete("/{encounter:int}", async (
        EncounterRepository repository,
        int encounter,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(encounter, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounter");

var clinicalLists = app.MapGroup("/api/clinical-lists").WithTags("Clinical Lists");

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

var messages = app.MapGroup("/api/messages").WithTags("Messages");

messages.MapGet("/{patientId}", async (
        MessageRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var patientMessages = await repository.GetForPatientAsync(patientId, cancellationToken);
        return patientMessages is null ? Results.NotFound() : Results.Ok(patientMessages);
    })
    .WithName("GetPatientMessages");

var procedures = app.MapGroup("/api/procedures").WithTags("Procedures");

procedures.MapGet("/{patientId}", async (
        ProcedureRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var procedureResults = await repository.GetForPatientAsync(patientId, cancellationToken);
        return procedureResults is null ? Results.NotFound() : Results.Ok(procedureResults);
    })
    .WithName("GetProcedureResultsForPatient");

var billing = app.MapGroup("/api/billing").WithTags("Billing");

billing.MapGet("/{patientId}", async (
        BillingRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var patientBilling = await repository.GetForPatientAsync(patientId, cancellationToken);
        return patientBilling is null ? Results.NotFound() : Results.Ok(patientBilling);
    })
    .WithName("GetBillingForPatient");

var administration = app.MapGroup("/api/administration").WithTags("Administration");

administration.MapGet("/directory", async (
        AdministrationRepository repository,
        CancellationToken cancellationToken) =>
    {
        var directory = await repository.GetDirectoryAsync(cancellationToken);
        return Results.Ok(directory);
    })
    .WithName("GetAdministrationDirectory");

var reports = app.MapGroup("/api/reports").WithTags("Reports");

reports.MapGet("/operational", async (
        ReportRepository repository,
        CancellationToken cancellationToken) =>
    {
        var report = await repository.GetOperationalReportsAsync(cancellationToken);
        return Results.Ok(report);
    })
    .WithName("GetOperationalReports");

app.Run();
