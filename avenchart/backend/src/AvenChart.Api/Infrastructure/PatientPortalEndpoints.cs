// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Configuration;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;
using static AvenChart.Api.Infrastructure.EndpointAccessPolicies;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Maps patient portal identity, profile, messaging, release, and clinical-view routes as one aggregate.
/// </summary>
public static class PatientPortalEndpoints
{
    public static RouteGroupBuilder MapPatientPortalEndpoints(this WebApplication app)
    {
        var patientPortal = app.MapGroup("/api/patient-portal").WithTags("Patient Portal");

        patientPortal.MapPost("/login", async (
                PatientPortalRepository repository,
                IOptions<IdentityProviderOptions> identityProviderOptions,
                PatientPortalLoginRequest request,
                CancellationToken cancellationToken) =>
            {
                if (!identityProviderOptions.Value.IsLocal)
                {
                    return Results.NotFound();
                }
                var response = await repository.LoginAsync(request, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("PatientPortalLogin");

        patientPortal.MapGet("/session", async (
                PatientPortalRepository repository,
                BrowserOidcSessionService browserOidcSessions,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                var response = Guid.TryParse(header, out var sessionId)
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
                if (browserOidcSessions.TryGetCsrfToken(
                        httpContext,
                        BrowserOidcSessionService.PortalAudience,
                        out var csrfToken))
                {
                    httpContext.Response.Headers["X-AvenChart-CSRF"] = csrfToken;
                }
                return response;
            })
            .WithName("GetPatientPortalSession");

        patientPortal.MapGet("/home", async (
                PatientPortalRepository repository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                return Guid.TryParse(header, out var sessionId)
                    ? Results.Ok(await repository.GetMessageThreadAsync(sessionId, messageId, cancellationToken))
                    : Results.Ok(PatientPortalRepository.MissingSessionHeaderMessageThread(messageId.ToString()));
            })
            .WithName("GetPatientPortalMessageThread");

        patientPortal.MapGet("/messages/attachments/{attachmentId:guid}", async (
                PatientPortalRepository repository,
                HttpContext httpContext,
                Guid attachmentId,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                var attachment = Guid.TryParse(header, out var sessionId)
                    ? await repository.DownloadMessageAttachmentAsync(sessionId, attachmentId, cancellationToken)
                    : new PatientPortalMessageAttachmentDownload(false, string.Empty, "application/octet-stream", Array.Empty<byte>(), "Patient portal session header was not supplied.");
                return attachment.Downloadable
                    ? Results.File(attachment.Content, attachment.ContentType, attachment.FileName)
                    : Results.BadRequest(new { attachment.FailureReason });
            })
            .WithName("DownloadPatientPortalMessageAttachment");

        patientPortal.MapPost("/messages", async (
                PatientPortalRepository repository,
                HttpContext httpContext,
                PatientPortalComposeMessageRequest request,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                return Guid.TryParse(header, out var sessionId)
                    ? Results.Ok(await repository.ComposeMessageAsync(sessionId, request, cancellationToken))
                    : Results.Ok(PatientPortalRepository.MissingSessionHeaderComposeMessage());
            })
            .WithName("ComposePatientPortalMessage");

        patientPortal.MapGet("/prescription-refill-requests", async (
                PatientPortalRepository repository,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                return Guid.TryParse(header, out var sessionId)
                    ? Results.Ok(await repository.GetPrescriptionRefillHistoryAsync(sessionId, cancellationToken))
                    : Results.Ok(new PatientPortalPrescriptionRefillHistoryResponse(
                        Authenticated: false,
                        SessionId: null,
                        Username: string.Empty,
                        PortalUsername: string.Empty,
                        CanonicalId: string.Empty,
                        LegacyPid: null,
                        Pubpid: string.Empty,
                        DisplayName: string.Empty,
                        DatasetId: "unseeded",
                        DatasetVersion: "unknown",
                        RequestCount: 0,
                        Requests: Array.Empty<PatientPortalPrescriptionRefillHistoryItem>(),
                        FailureReason: "Patient portal session header was not supplied.",
                        SessionSource: "avenchart-portal"));
            })
            .WithName("GetPatientPortalPrescriptionRefillHistory");

        patientPortal.MapPost("/prescriptions/{prescriptionId}/refill-request", async (
                PatientPortalRepository repository,
                HttpContext httpContext,
                string prescriptionId,
                PatientPortalPrescriptionRefillRequest request,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
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
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                return Guid.TryParse(header, out var sessionId)
                    ? Results.Ok(await repository.DeleteMessageAsync(sessionId, messageId, cancellationToken))
                    : Results.Ok(PatientPortalRepository.MissingSessionHeaderDeleteMessage(messageId.ToString()));
            })
            .WithName("DeletePatientPortalMessage");

        patientPortal.MapDelete("/session", async (
                PatientPortalRepository repository,
                BrowserOidcSessionService browserOidcSessions,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var header = httpContext.Request.Headers["X-AvenChart-Patient-Portal-Session"].ToString();
                var response = Guid.TryParse(header, out var sessionId)
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
                if (browserOidcSessions.IsBrowserSessionRequest(httpContext, BrowserOidcSessionService.PortalAudience))
                {
                    browserOidcSessions.ClearBrowserSessionCookies(httpContext, BrowserOidcSessionService.PortalAudience);
                }
                return response;
            })
            .WithName("EndPatientPortalSession");

        app.MapFhirR4Endpoints();

        return patientPortal;
    }
}
