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
    .WithName("RegisterPatient");

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
    .WithName("UpdatePatientDemographics");

patients.MapDelete("/{patientId}", async (
        PatientRepository repository,
        string patientId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteTemporaryPatientAsync(patientId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteTemporaryPatient");

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
    .WithName("CreatePatientInsurance");

patients.MapPut("/insurance/{insuranceId}", async (
        PatientRepository repository,
        string insuranceId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.UpdateInsuranceAsync(insuranceId, request, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("UpdatePatientInsurance");

patients.MapDelete("/insurance/{insuranceId}", async (
        PatientRepository repository,
        string insuranceId,
        CancellationToken cancellationToken) =>
    {
        var patient = await repository.DeleteInsuranceAsync(insuranceId, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    })
    .WithName("DeletePatientInsurance");

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
    .WithName("SignEncounter");

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
    .WithName("CreateEncounterDocument");

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
    .WithName("CreateBinaryEncounterDocument");

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
    .WithName("CreateExternalLinkEncounterDocument");

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
    .WithName("UpdateEncounterDocumentMetadata");

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
    .WithName("MoveEncounterDocument");

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
    .WithName("ReplaceEncounterDocumentContent");

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
    .WithName("ReplaceEncounterDocumentBinaryContent");

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
    .WithName("SoftDeleteEncounterDocument");

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
    .WithName("RestoreEncounterDocument");

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
    .WithName("SignEncounterDocument");

encounters.MapDelete("/{encounter:int}/signatures/{signatureId:int}", async (
        EncounterRepository repository,
        int encounter,
        int signatureId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteSignatureAsync(encounter, signatureId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteEncounterSignature");

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
    .WithName("CreatePatientMessage");

messages.MapPut("/{messageId}/status", async (
        MessageRepository repository,
        string messageId,
        PatientMessageStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateStatusAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageStatus");

messages.MapPut("/{messageId}/content", async (
        MessageRepository repository,
        string messageId,
        PatientMessageContentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateContentAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageContent");

messages.MapPut("/{messageId}/assignment", async (
        MessageRepository repository,
        string messageId,
        PatientMessageAssignmentUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateAssignmentAsync(messageId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdatePatientMessageAssignment");

messages.MapPut("/{messageId}/soft-delete", async (
        MessageRepository repository,
        string messageId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SoftDeleteAsync(messageId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("SoftDeletePatientMessage");

messages.MapDelete("/{messageId}", async (
        MessageRepository repository,
        string messageId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(messageId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeletePatientMessage");

var documents = app.MapGroup("/api/documents").WithTags("Documents");

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
    .WithName("CreatePatientDocument");

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
    .WithName("CreateBinaryPatientDocument");

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
    .WithName("CreateExternalLinkPatientDocument");

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
    .WithName("UpdatePatientDocumentMetadata");

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
    .WithName("ReplacePatientDocumentContent");

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
    .WithName("ReplaceBinaryPatientDocumentContent");

documents.MapPut("/{documentId:int}/soft-delete", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SoftDeleteAsync(documentId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("SoftDeletePatientDocument");

documents.MapPut("/{documentId:int}/restore", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.RestoreAsync(documentId, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("RestorePatientDocument");

documents.MapPut("/{documentId:int}/sign", async (
        DocumentRepository repository,
        int documentId,
        PatientDocumentSignRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.SignAsync(documentId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("SignPatientDocument");

documents.MapDelete("/{documentId:int}", async (
        DocumentRepository repository,
        int documentId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteAsync(documentId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeletePatientDocument");

var procedures = app.MapGroup("/api/procedures").WithTags("Procedures");

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
    .WithName("CreateProcedureOrder");

procedures.MapPut("/orders/{orderId:int}/status", async (
        ProcedureRepository repository,
        int orderId,
        ProcedureOrderStatusUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateOrderStatusAsync(orderId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureOrderStatus");

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
    .WithName("UpdateProcedureOrder");

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
    .WithName("CreateProcedureReport");

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
    .WithName("UpdateProcedureReport");

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
    .WithName("SignProcedureReport");

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
    .WithName("CreateProcedureSpecimen");

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
    .WithName("CreateProcedureResult");

procedures.MapPut("/results/{resultId:int}", async (
        ProcedureRepository repository,
        int resultId,
        ProcedureResultUpdateRequest request,
        CancellationToken cancellationToken) =>
    {
        var mutation = await repository.UpdateResultAsync(resultId, request, cancellationToken);
        return mutation is null ? Results.NotFound() : Results.Ok(mutation);
    })
    .WithName("UpdateProcedureResult");

procedures.MapDelete("/orders/{orderId:int}", async (
        ProcedureRepository repository,
        int orderId,
        CancellationToken cancellationToken) =>
    {
        var deleted = await repository.DeleteOrderCascadeAsync(orderId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteProcedureOrderCascade");

var billing = app.MapGroup("/api/billing").WithTags("Billing");

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
