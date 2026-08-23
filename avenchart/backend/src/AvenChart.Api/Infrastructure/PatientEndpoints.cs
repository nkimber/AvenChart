// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using AvenChart.Api.Workflows;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps patient identity, demographics, clinical workflow, disclosure, and care-team routes as one facility-scoped aggregate.
/// </summary>
public static class PatientEndpoints
{
    public static RouteGroupBuilder MapPatientEndpoints(this WebApplication app)
    {
        var patients = app.MapGroup("/api/patients").WithTags("Patients");
        RequireAccessPermission(patients, "patients", "demo", "view");
        patients.AddEndpointFilter(PatientFacilityScopeFilter());

        app.MapClinicalWorkflowEndpoints();
        patients.MapGet("/", async (
                PatientRepository repository,
                HttpContext httpContext,
                string? search,
                int? limit,
                CancellationToken cancellationToken) =>
            {
                var response = await repository.SearchAsync(
                    search,
                    limit ?? 25,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
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
        patients.MapPost("/{patientId}/referrals", async (string patientId, ReferralCreateRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                return Results.Created($"/api/patients/{patientId}/referrals", await repository.CreateAsync(patientId, request, session.Username, cancellationToken));
            }
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithName("CreatePatientReferral").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));
        patients.MapPut("/{patientId}/referrals/{referralId:guid}/status", async (string patientId, Guid referralId, ReferralStatusRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                return Results.Ok(await repository.UpdateStatusAsync(patientId, referralId, request, session.Username, cancellationToken));
            }
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
            catch (ClinicalWorkflowVersionConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message, expectedVersion = ex.ExpectedVersion, currentVersion = ex.CurrentVersion, current = await repository.GetByIdAsync(patientId, referralId, cancellationToken) });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithName("UpdatePatientReferralStatus").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        patients.MapPut("/{patientId}/referrals/{referralId:guid}/assignment", async (string patientId, Guid referralId, ReferralAssignmentRequest request, ReferralRepository repository, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                return Results.Ok(await repository.UpdateAssignmentAsync(patientId, referralId, request, session.Username, cancellationToken));
            }
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
            catch (ClinicalWorkflowVersionConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message, expectedVersion = ex.ExpectedVersion, currentVersion = ex.CurrentVersion, current = await repository.GetByIdAsync(patientId, referralId, cancellationToken) });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithName("UpdatePatientReferralAssignment").AddEndpointFilter(AccessPermissionFilter("encounters", "auth_a", "write"));

        patients.MapGet("/{patientId}/referrals/{referralId:guid}/history", async (string patientId, Guid referralId, ReferralRepository repository, CancellationToken cancellationToken) =>
        {
            try { return await repository.GetHistoryAsync(patientId, referralId, cancellationToken) is { } history ? Results.Ok(history) : Results.NotFound(); }
            catch (ArgumentException ex) { return Results.NotFound(new { error = ex.Message }); }
        }).WithName("GetPatientReferralHistory");

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
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
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
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
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
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
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
            catch (EncounterLockConflictException ex) { return Results.Conflict(new { error = ex.Message, code = "encounter_locked" }); }
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

        patients.MapGet("/{patientId}/disclosure-policy", () =>
            Results.Ok(PatientDisclosurePolicyCatalog.Build()))
            .WithName("GetPatientDisclosurePolicy")
            .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        patients.MapGet("/{patientId}/disclosure-authorities", async (
            string patientId,
            PatientDisclosureRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await repository.GetAuthoritiesAsync(
                    patientId,
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).WithName("GetPatientDisclosureAuthorities")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        patients.MapPost("/{patientId}/disclosure-authorities", async (
            string patientId,
            PatientDisclosureAuthorityCreateRequest request,
            PatientDisclosureRepository repository,
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
                var authority = await repository.CreateAuthorityAsync(
                    patientId,
                    request,
                    session.Username,
                    cancellationToken);
                return Results.Created(
                    $"/api/patients/{patientId}/disclosure-authorities/{authority.AuthorityId}",
                    authority);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("CreatePatientDisclosureAuthority")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        patients.MapPost("/{patientId}/disclosure-authorities/{authorityId:guid}/{action}", async (
            string patientId,
            Guid authorityId,
            string action,
            PatientDisclosureAuthorityTransitionRequest request,
            PatientDisclosureRepository repository,
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
                return Results.Ok(await repository.TransitionAuthorityAsync(
                    patientId,
                    authorityId,
                    action,
                    request,
                    session.Username,
                    cancellationToken));
            }
            catch (PatientDisclosureConcurrencyException ex)
            {
                return Results.Conflict(new
                {
                    error = ex.Message,
                    expectedVersion = ex.ExpectedVersion,
                    currentVersion = ex.CurrentVersion,
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("TransitionPatientDisclosureAuthority")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        patients.MapGet("/{patientId}/disclosure-authorities/{authorityId:guid}/history", async (
            string patientId,
            Guid authorityId,
            PatientDisclosureRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await repository.GetAuthorityHistoryAsync(
                    patientId,
                    authorityId,
                    cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).WithName("GetPatientDisclosureAuthorityHistory")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        patients.MapGet("/{patientId}/disclosure-requests", async (
            string patientId,
            PatientDisclosureRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await repository.GetRequestsAsync(
                    patientId,
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).WithName("GetPatientDisclosureRequests")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        patients.MapPost("/{patientId}/disclosure-requests", async (
            string patientId,
            PatientDisclosureRequestCreateRequest request,
            PatientDisclosureRepository repository,
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
                var disclosure = await repository.CreateRequestAsync(
                    patientId,
                    request,
                    session.Username,
                    cancellationToken);
                return Results.Created(
                    $"/api/patients/{patientId}/disclosure-requests/{disclosure.RequestId}",
                    disclosure);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("CreatePatientDisclosureRequest")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        patients.MapPost("/{patientId}/disclosure-requests/{requestId:guid}/decision", async (
            string patientId,
            Guid requestId,
            PatientDisclosureDecisionRequest request,
            PatientDisclosureRepository repository,
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
                return Results.Ok(await repository.DecideRequestAsync(
                    patientId,
                    requestId,
                    request,
                    session.Username,
                    cancellationToken));
            }
            catch (PatientDisclosureConcurrencyException ex)
            {
                return Results.Conflict(new
                {
                    error = ex.Message,
                    expectedVersion = ex.ExpectedVersion,
                    currentVersion = ex.CurrentVersion,
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("DecidePatientDisclosureRequest")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

        patients.MapGet("/{patientId}/disclosure-requests/{requestId:guid}/history", async (
            string patientId,
            Guid requestId,
            PatientDisclosureRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await repository.GetRequestHistoryAsync(
                    patientId,
                    requestId,
                    cancellationToken));
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).WithName("GetPatientDisclosureRequestHistory")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "view"));

        patients.MapDelete("/{patientId}/disclosure-authorities/{authorityId:guid}/test-fixture", async (
            string patientId,
            Guid authorityId,
            PatientDisclosureRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return await repository.DeleteFixtureAsync(
                    patientId,
                    authorityId,
                    cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        }).WithName("DeletePatientDisclosureTestFixture")
          .AddEndpointFilter(AccessPermissionFilter("patients", "pat_rep", "write"));

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
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["duplicateReview"] = [ex.Message] }); }
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
                AuthRepository authRepository,
                HttpContext httpContext,
                PatientRegistrationRequest request,
                CancellationToken cancellationToken) =>
            {
                var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                var result = await repository.CreatePatientAsync(
                    request,
                    session.Username,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return result.Patient is null
                    ? RegistrationValidationProblem(result.ValidationIssues)
                    : Results.Created($"/api/patients/{result.Patient.CanonicalId}", result.Patient);
            })
            .WithName("RegisterPatient")
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "addonly"));

        patients.MapGet("/{canonicalId}", async (
                PatientRepository repository,
                HttpContext httpContext,
                string canonicalId,
                CancellationToken cancellationToken) =>
            {
                var patient = await repository.GetChartSummaryAsync(canonicalId, cancellationToken);
                if (patient is not null)
                {
                    return Results.Ok(patient);
                }

                var mergedIntoPatientId = await repository.GetMergedIntoPatientIdAsync(
                    canonicalId,
                    RequireStaffAccessContext(httpContext).FacilityId,
                    cancellationToken);
                return string.IsNullOrWhiteSpace(mergedIntoPatientId)
                    ? Results.NotFound()
                    : Results.Problem(
                        statusCode: StatusCodes.Status410Gone,
                        title: "Patient chart has been merged",
                        detail: "This chart is no longer independently available. Continue with the surviving patient chart.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["targetPatientId"] = mergedIntoPatientId
                        });
            })
            .WithName("GetPatientChartSummary");
        patients.MapGet("/{patientId}/xml-export", async (string patientId, PatientXmlExchangeRepository repository, CancellationToken ct) => { var xml = await repository.ExportAsync(patientId, ct); return xml is null ? Results.NotFound() : Results.File(Encoding.UTF8.GetBytes(xml), "application/xml", $"avenchart-patient-{patientId}.xml"); }).WithName("ExportPatientXml");
        patients.MapPost("/xml-import/preview", async (PatientXmlExchangeRepository repository, PatientXmlImportRequest request, CancellationToken ct) => { try { var preview = await repository.PreviewAsync(request, ct); return preview is null ? Results.NotFound() : Results.Ok(preview); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { { "xml", [e.Message] } }); } }).WithName("PreviewPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        patients.MapPost("/xml-import", async (PatientXmlExchangeRepository repository, AuthRepository auth, HttpContext context, PatientXmlImportRequest request, CancellationToken ct) => { try { var session = await GetSessionFromHeaderAsync(auth, context, ct); var result = await repository.ImportAsync(request, session.Username, ct); return result is null ? Results.NotFound() : Results.Ok(result); } catch (ArgumentException e) { return Results.ValidationProblem(new Dictionary<string, string[]> { { "xml", [e.Message] } }); } }).WithName("ImportPatientXml").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));
        patients.MapPost("/xml-import/{auditId:guid}/rollback", async (PatientXmlExchangeRepository repository, AuthRepository auth, HttpContext context, Guid auditId, CancellationToken ct) => { var session = await GetSessionFromHeaderAsync(auth, context, ct); return await repository.RollbackAsync(auditId, session.Username, ct) ? Results.NoContent() : Results.NotFound(); }).WithName("RollbackPatientXmlImport").AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

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

        patients.MapPut("/{patientId}/administration", async (
                PatientRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string patientId,
                PatientAdministrationUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var patient = await repository.UpdateAdministrationAsync(
                        patientId,
                        request,
                        session.Username,
                        cancellationToken);
                    return patient is null ? Results.NotFound() : Results.Ok(patient);
                }
                catch (PatientAdministrationVersionConflictException exception)
                {
                    return Results.Conflict(new
                    {
                        error = exception.Message,
                        expectedVersion = exception.ExpectedVersion,
                        currentVersion = exception.CurrentVersion,
                        current = await repository.GetChartSummaryAsync(patientId, cancellationToken)
                    });
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["administration"] = [exception.Message]
                    });
                }
            })
            .WithName("UpdatePatientAdministration")
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

        patients.MapPut("/{patientId}/contact", (string patientId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Patient contact update is retired",
                    detail: "Use the atomic patient administration update endpoint with the current administration version."))
            .WithName("RetirePatientContactUpdate")
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

        patients.MapPut("/{patientId}/demographics", (string patientId) =>
                Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "Patient demographics update is retired",
                    detail: "Use the atomic patient administration update endpoint with the current administration version."))
            .WithName("RetirePatientDemographicsUpdate")
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

        patients.MapPut("/{patientId}/deceased-status", async (
                PatientRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string patientId,
                PatientDeceasedStatusUpdateRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var patient = await repository.UpdateDeceasedStatusAsync(
                        patientId, request, session.Username, cancellationToken);
                    return patient is null ? Results.NotFound() : Results.Ok(patient);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("UpdatePatientDeceasedStatus")
            .AddEndpointFilter(AccessPermissionFilter("patients", "demo", "write"));

        patients.MapGet("/{patientId}/deceased-status-history", async (
                PatientRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetDeceasedStatusHistoryAsync(patientId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientDeceasedStatusHistory");

        patients.MapGet("/{patientId}/lifecycle-history", async (
                PatientRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                var history = await repository.GetLifecycleHistoryAsync(patientId, cancellationToken);
                return history is null ? Results.NotFound() : Results.Ok(history);
            })
            .WithName("GetPatientLifecycleHistory");

        patients.MapPost("/{patientId}/lifecycle/{action}", async (
                PatientRepository repository,
                AuthRepository authRepository,
                HttpContext httpContext,
                string patientId,
                string action,
                PatientLifecycleTransitionRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var session = await GetSessionFromHeaderAsync(authRepository, httpContext, cancellationToken);
                    var patient = await repository.TransitionLifecycleAsync(
                        patientId,
                        action,
                        request,
                        session.Username,
                        cancellationToken);
                    return patient is null ? Results.NotFound() : Results.Ok(patient);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("TransitionPatientLifecycle")
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

        return patients;
    }

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

    static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> PatientFacilityScopeFilter()
    {
        return async (context, next) =>
        {
            var routeValues = context.HttpContext.Request.RouteValues;
            var patientIdentifier = routeValues.TryGetValue("patientId", out var patientId)
                ? patientId?.ToString()
                : routeValues.TryGetValue("canonicalId", out var canonicalId)
                    ? canonicalId?.ToString()
                    : null;
            var insuranceId = routeValues.TryGetValue("insuranceId", out var insurance)
                ? insurance?.ToString()
                : null;
            if (string.IsNullOrWhiteSpace(patientIdentifier) && string.IsNullOrWhiteSpace(insuranceId))
            {
                return await next(context);
            }

            PhiAuditResourceContext.Set(
                context.HttpContext,
                string.IsNullOrWhiteSpace(patientIdentifier) ? "Insurance" : "Patient",
                patientIdentifier ?? insuranceId);

            var accessContext = RequireStaffAccessContext(context.HttpContext);
            var accessContextService = context.HttpContext.RequestServices
                .GetRequiredService<StaffAccessContextService>();
            var authorized = string.IsNullOrWhiteSpace(patientIdentifier)
                ? await accessContextService.CanAccessInsuranceAsync(
                    insuranceId,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted)
                : await accessContextService.CanAccessPatientAsync(
                    patientIdentifier,
                    accessContext.FacilityId,
                    context.HttpContext.RequestAborted);
            return authorized ? await next(context) : Results.NotFound();
        };
    }
}
