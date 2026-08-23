// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps the protected scheduling and Flow Board API. Keeping this aggregate in
/// one module makes its route, facility-scope, and concurrency contract visible
/// without coupling it to host composition.
/// </summary>
public static class AppointmentEndpoints
{
    public static RouteGroupBuilder MapAppointmentEndpoints(this WebApplication app)
    {
        var appointments = app.MapGroup("/api/appointments").WithTags("Appointments");
        RequireAccessPermission(appointments, "patients", "appt", "view");
        appointments.AddEndpointFilter(ClinicalResourceFacilityScopeFilter());

        appointments.MapGet("/flow-board", async (FlowBoardRepository repository, HttpContext httpContext, string? date, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetAsync(date, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)))
            .WithName("GetAppointmentFlowBoard");

        appointments.MapGet("/scheduling-options", async (AppointmentRepository repository, HttpContext httpContext, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetSchedulingOptionsAsync(RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)))
            .WithName("GetAppointmentSchedulingOptions");

        appointments.MapGet("/", async (AppointmentRepository repository, string? patientId, string? from, string? fromDate, string? toDate, int? limit, HttpContext httpContext, CancellationToken cancellationToken) =>
            Results.Ok(await repository.SearchAsync(patientId, fromDate ?? from, toDate, limit ?? 25, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)))
            .WithName("SearchAppointments");

        appointments.MapGet("/waitlist", async (AppointmentRepository repository, HttpContext httpContext, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetWaitlistAsync(RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)))
            .WithName("GetAppointmentWaitlist");

        appointments.MapGet("/reminders/templates", async (AppointmentRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetReminderTemplateCatalogAsync(cancellationToken)))
            .WithName("GetAppointmentReminderTemplates")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPost("/{appointmentId}/reminders/dispatch", async (AppointmentRepository repository, HttpRequest request, string appointmentId, CancellationToken cancellationToken) =>
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
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(exception.Message);
                }
            })
            .WithName("DispatchAppointmentReminder")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPost("/{appointmentId}/reminders/dispatch/retry", async (AppointmentRepository repository, string appointmentId, CancellationToken cancellationToken) =>
            {
                var dispatch = await repository.RetryReminderDispatchAsync(appointmentId, cancellationToken);
                return dispatch is null
                    ? Results.BadRequest("Appointment reminder could not be retried because no prior dispatch exists, no reminder is due, or the reminder rule is inactive.")
                    : Results.Ok(dispatch);
            })
            .WithName("RetryAppointmentReminderDispatch")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapGet("/reminders/dispatch-history", async (AppointmentRepository repository, HttpContext httpContext, string? appointmentId, int? limit, CancellationToken cancellationToken) =>
            Results.Ok(await repository.GetReminderDispatchHistoryAsync(appointmentId, limit ?? 10, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken)))
            .WithName("GetAppointmentReminderDispatchHistory");

        appointments.MapGet("/{appointmentId}", async (AppointmentRepository repository, string appointmentId, CancellationToken cancellationToken) =>
            {
                var appointment = await repository.GetByIdAsync(appointmentId, cancellationToken);
                return appointment is null ? Results.NotFound() : Results.Ok(appointment);
            })
            .WithName("GetAppointmentDetail");

        appointments.MapPost("/", async (AppointmentRepository repository, AppointmentCreateRequest request, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try
                {
                    var appointment = await repository.CreateAsync(request, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken);
                    return appointment is null
                        ? Results.BadRequest("Appointment could not be created from the supplied patient, date, time, and duration.")
                        : Results.Created($"/api/appointments/{appointment.Id}", appointment);
                }
                catch (AppointmentAvailabilityConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, validation = exception.Validation });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("CreateAppointment")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPost("/availability/validate", async (AppointmentRepository repository, AppointmentAvailabilityValidationRequest request, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try
                {
                    var validation = await repository.ValidateAvailabilityAsync(request, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken);
                    return validation is null
                        ? Results.BadRequest("Appointment availability could not be validated from the supplied patient, date, time, and duration.")
                        : Results.Ok(validation);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("ValidateAppointmentAvailability")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPut("/{appointmentId}", async (AppointmentRepository repository, string appointmentId, AppointmentUpdateRequest request, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try
                {
                    var appointment = await repository.UpdateAsync(appointmentId, request, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken);
                    return appointment is null
                        ? Results.BadRequest("Appointment could not be updated from the supplied date, time, and duration.")
                        : Results.Ok(appointment);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (AppointmentConcurrencyException)
                {
                    return Results.Conflict(new { error = "This appointment changed since it was loaded. Refresh it before saving again." });
                }
                catch (AppointmentMutationNotAllowedException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .WithName("UpdateAppointment")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPut("/{appointmentId}/status", async (AppointmentRepository repository, string appointmentId, AppointmentStatusUpdateRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var appointment = await repository.UpdateStatusAsync(appointmentId, request, cancellationToken);
                    return appointment is null ? Results.NotFound() : Results.Ok(appointment);
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
                catch (AppointmentConcurrencyException)
                {
                    return Results.Conflict(new { error = "This appointment changed since it was loaded. Refresh it before applying another status." });
                }
            })
            .WithName("UpdateAppointmentStatus")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPost("/{appointmentId}/recurrence-exceptions/{occurrenceDate}/restore", async (AppointmentRepository repository, string appointmentId, string occurrenceDate, AppointmentRecurrenceExceptionRequest request, CancellationToken cancellationToken) =>
            {
                try
                {
                    var appointment = await repository.RestoreRecurrenceExceptionAsync(appointmentId, occurrenceDate, request, cancellationToken);
                    return appointment is null ? Results.NotFound() : Results.Ok(appointment);
                }
                catch (AppointmentConcurrencyException)
                {
                    return Results.Conflict(new { error = "This appointment series changed since it was loaded. Refresh it before restoring an occurrence." });
                }
                catch (AppointmentMutationNotAllowedException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .WithName("RestoreAppointmentOccurrence")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapPost("/{appointmentId}/occurrences/{occurrenceDate}/reschedule", async (AppointmentRepository repository, string appointmentId, string occurrenceDate, AppointmentOccurrenceRescheduleRequest request, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                try
                {
                    var appointment = await repository.RescheduleOccurrenceAsync(appointmentId, occurrenceDate, request, RequireStaffAccessContext(httpContext).FacilityId, cancellationToken);
                    return appointment is null
                        ? Results.BadRequest("Appointment occurrence could not be rescheduled from the supplied date, time, and duration.")
                        : Results.Created($"/api/appointments/{appointment.Id}", appointment);
                }
                catch (AppointmentConcurrencyException)
                {
                    return Results.Conflict(new { error = "This appointment series changed since it was loaded. Refresh it before rescheduling an occurrence." });
                }
                catch (AppointmentAvailabilityConflictException exception)
                {
                    return Results.Conflict(new { error = exception.Message, validation = exception.Validation });
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("RescheduleAppointmentOccurrence")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        appointments.MapDelete("/{appointmentId}", async (AppointmentRepository repository, string appointmentId, int? expectedVersion, CancellationToken cancellationToken) =>
            {
                if (expectedVersion is not > 0)
                {
                    return Results.BadRequest(new { error = "An expectedVersion greater than zero is required when changing an appointment occurrence." });
                }

                try
                {
                    var deleted = await repository.DeleteAsync(appointmentId, expectedVersion.Value, cancellationToken);
                    return deleted ? Results.NoContent() : Results.NotFound();
                }
                catch (AppointmentConcurrencyException)
                {
                    return Results.Conflict(new { error = "This appointment series changed since it was loaded. Refresh it before cancelling an occurrence." });
                }
                catch (AppointmentMutationNotAllowedException exception)
                {
                    return Results.Conflict(new { error = exception.Message });
                }
            })
            .WithName("DeleteAppointment")
            .AddEndpointFilter(AccessPermissionFilter("patients", "appt", "write"));

        return appointments;
    }
}
