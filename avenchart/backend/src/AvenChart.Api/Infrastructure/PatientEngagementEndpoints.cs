// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps patient education, recall, outreach, and chart-tracker workflow routes.
/// </summary>
public static class PatientEngagementEndpoints
{
    public static void MapPatientEngagementEndpoints(this WebApplication app)
    {
        var patientEducation = app.MapGroup("/api/patient-education").WithTags("Patient Education");
        RequireAccessPermission(patientEducation, "patients", "demo", "view");
        patientEducation.MapGet("/resources", async (PatientEducationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetPatientEducationResources");
        patientEducation.MapPost("/search", async (PatientEducationRepository repository, PatientEducationSearchRequest request, CancellationToken cancellationToken) => { var result = await repository.SearchAsync(request, cancellationToken); return result is null ? Results.BadRequest("An active HTTPS resource and search text are required.") : Results.Ok(result); }).WithName("SearchPatientEducation");

        var recalls = app.MapGroup("/api/recalls").WithTags("Recalls");
        RequireAccessPermission(recalls, "patients", "appt", "view");
        recalls.MapGet("/", async (RecallRepository repository, bool? includeClosed, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(includeClosed ?? false, cancellationToken))).WithName("GetRecalls");
        recalls.MapPost("/", async (RecallRepository repository, RecallRequest request, AuthRepository authRepository, HttpContext context, CancellationToken cancellationToken) => { var session = await GetSessionFromHeaderAsync(authRepository, context, cancellationToken); var item = await repository.CreateAsync(request, session.Username, cancellationToken); return item is null ? Results.BadRequest() : Results.Created($"/api/recalls/{item.Id}", item); }).WithName("CreateRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        recalls.MapPost("/{id:guid}/close", async (RecallRepository repository, Guid id, RecallClosureRequest request, AuthRepository authRepository, HttpContext context, CancellationToken cancellationToken) => { try { var session = await GetSessionFromHeaderAsync(authRepository, context, cancellationToken); var item = await repository.CloseAsync(id, request, session.Username, cancellationToken); return item is null ? Results.NotFound() : Results.Ok(item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["closure"] = [exception.Message] }); } }).WithName("CloseRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        recalls.MapDelete("/{id:guid}", (Guid id) => Results.Problem(statusCode: StatusCodes.Status405MethodNotAllowed, title: "Recall deletion is retired", detail: "Close or cancel a recall so its outreach evidence is retained.")).WithName("DeleteRecall").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
        recalls.MapGet("/{id:guid}/activity", async (RecallRepository repository, Guid id, CancellationToken cancellationToken) => { var activity = await repository.GetActivityAsync(id, cancellationToken); return activity is null ? Results.NotFound() : Results.Ok(activity); }).WithName("GetRecallActivity");
        recalls.MapPost("/{id:guid}/activity", async (RecallRepository repository, Guid id, RecallActivityRequest request, CancellationToken cancellationToken) => { try { var result = await repository.AddActivityAsync(id, request, cancellationToken); return result is null ? Results.NotFound() : Results.Created($"/api/recalls/{id}/activity/{result.Id}", result); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["activity"] = [exception.Message] }); } }).WithName("AddRecallActivity").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        var batchCommunication = app.MapGroup("/api/batch-communication").WithTags("Batch Communication");
        RequireAccessPermission(batchCommunication, "admin", "batchcom", "view");
        batchCommunication.MapPost("/preview", async (BatchCommunicationRepository repository, BatchCommunicationPreviewRequest request, CancellationToken cancellationToken) => { try { return Results.Ok(await repository.PreviewAsync(request, cancellationToken)); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["filter"] = [exception.Message] }); } }).WithName("PreviewBatchCommunication");
        batchCommunication.MapPost("/campaigns", async (BatchCommunicationRepository repository, BatchCommunicationCampaignCreateRequest request, CancellationToken cancellationToken) => { try { var campaign = await repository.CreateAsync(request, cancellationToken); return Results.Created($"/api/batch-communication/campaigns/{campaign.Campaign.Id}", campaign); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["campaign"] = [exception.Message] }); } }).WithName("CreateBatchCommunicationCampaign").AddEndpointFilter(AccessPermissionFilter("admin", "batchcom", "write"));
        batchCommunication.MapGet("/campaigns", async (BatchCommunicationRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetAsync(cancellationToken))).WithName("GetBatchCommunicationCampaigns");
        batchCommunication.MapGet("/campaigns/{id:guid}", async (BatchCommunicationRepository repository, Guid id, CancellationToken cancellationToken) => { var campaign = await repository.GetAsync(id, cancellationToken); return campaign is null ? Results.NotFound() : Results.Ok(campaign); }).WithName("GetBatchCommunicationCampaign");
        batchCommunication.MapGet("/campaigns/{id:guid}/output", async (BatchCommunicationRepository repository, Guid id, CancellationToken cancellationToken) => { var campaign = await repository.GetAsync(id, cancellationToken); if (campaign is null) return Results.NotFound(); var csv = new StringBuilder("Patient ID,Name,Email,Home Phone,Cell Phone,Postal Code,Next Appointment,Last Appointment,Last Visit,Subject,Body\n"); foreach (var item in campaign.Recipients) csv.AppendLine(string.Join(',', new[] { item.PatientId, item.DisplayName, item.Email, item.PhoneHome, item.PhoneCell, item.PostalCode, item.NextAppointmentDate, item.LastAppointmentDate, item.LastVisitDate, item.RenderedSubject, item.RenderedBody }.Select(value => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\""))); return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"batch-communication-{id}.csv"); }).WithName("ExportBatchCommunicationCampaign");

        var chartTracker = app.MapGroup("/api/chart-tracker").WithTags("Chart Tracker");
        RequireAccessPermission(chartTracker, "patients", "appt", "view");
        chartTracker.MapGet("/options", async (ChartTrackerRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.GetOptionsAsync(cancellationToken))).WithName("GetChartTrackerOptions");
        chartTracker.MapGet("/lookup/{identifier}", async (ChartTrackerRepository repository, string identifier, CancellationToken cancellationToken) => { var patient = await repository.FindAsync(identifier, cancellationToken); return patient is null ? Results.NotFound() : Results.Ok(patient); }).WithName("LookupChartTrackerPatient");
        chartTracker.MapGet("/patients/{patientId}/history", async (ChartTrackerRepository repository, string patientId, CancellationToken cancellationToken) => { var history = await repository.GetHistoryAsync(patientId, cancellationToken); return history is null ? Results.NotFound() : Results.Ok(history); }).WithName("GetChartTrackerHistory");
        chartTracker.MapPost("/patients/{patientId}/events", async (ChartTrackerRepository repository, string patientId, ChartTrackerUpdateRequest request, CancellationToken cancellationToken) => { try { var item = await repository.RecordAsync(patientId, request, cancellationToken); return item is null ? Results.NotFound() : Results.Created($"/api/chart-tracker/patients/{patientId}/events/{item.Id}", item); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["tracker"] = [exception.Message] }); } }).WithName("RecordChartTrackerEvent").AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));
    }
}
