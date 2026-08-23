// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected encounter operations, including locking, signing, amendments,
/// and their selected-facility and content-bound evidence contracts.
/// </summary>
public static class EncounterEndpoints
{
    public static RouteGroupBuilder MapEncounterEndpoints(this WebApplication app)
    {
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

        app.MapClinicalListEndpoints();

        return encounters;
    }
}
