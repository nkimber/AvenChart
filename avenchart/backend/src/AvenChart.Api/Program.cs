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
