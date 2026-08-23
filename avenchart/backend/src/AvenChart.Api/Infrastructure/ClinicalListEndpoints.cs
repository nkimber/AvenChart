// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps protected longitudinal clinical-list operations. This module retains
/// version, correction, and facility-scope contracts in one visible boundary.
/// </summary>
public static class ClinicalListEndpoints
{
    public static RouteGroupBuilder MapClinicalListEndpoints(this WebApplication app)
    {
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

        return clinicalLists;
    }
}
