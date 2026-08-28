// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthVideoService(
    TelehealthVideoRepository repository,
    PatientPortalRepository patientPortalRepository,
    IPatientPortalIdentityAdapter patientIdentityAdapter,
    ITelehealthVideoProvider videoProvider,
    IOptions<TelehealthOptions> options)
{
    private static readonly IReadOnlyList<string> Limitations =
    [
        "NON_PRODUCTION simulator: no video, audio, signaling, TURN, or vendor connection is created.",
        "Recording, transcription, summarization, and persistent media are disabled.",
        "Entering this waiting room does not start a consultation or create an encounter.",
        "If symptoms worsen or you are unsure it is safe to wait, contact the practice or seek in-person care. Call 911 for an emergency."
    ];

    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthConnectionGrantResponse> PreparePatientAsync(
        HttpContext httpContext,
        Guid requestId,
        PrepareTelehealthConnectionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        var normalized = Normalize(request);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var subjectHash = SubjectHash("patient", patient.CanonicalId);
        var context = await repository.PreparePatientContextAsync(
            _options.PracticeId,
            _options.FacilityId,
            patient.CanonicalId,
            requestId,
            cancellationToken);
        return await IssueAsync(context, normalized, "patient", subjectHash, key, cancellationToken);
    }

    public async Task<TelehealthConnectionGrantResponse> PreparePhysicianAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid reservationId,
        PrepareTelehealthConnectionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        var staffId = session.StaffId
            ?? throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "The authenticated identity is not bound to an active staff record.");
        var normalized = Normalize(request);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var subjectHash = SubjectHash("physician", staffId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var context = await repository.PreparePhysicianContextAsync(
            _options.PracticeId,
            _options.FacilityId,
            staffId,
            reservationId,
            cancellationToken);
        return await IssueAsync(context, normalized, "physician", subjectHash, key, cancellationToken);
    }

    private async Task<TelehealthConnectionGrantResponse> IssueAsync(
        TelehealthVideoContextRecord context,
        PrepareTelehealthConnectionRequest request,
        string participantRole,
        string participantSubjectHash,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prepare-video-connection",
            context.SessionId,
            participantRole,
            request.ExpectedVersion,
            request.BrowserSupported,
            request.CameraAvailable,
            request.MicrophoneAvailable,
            request.SpeakerAvailable,
            request.NetworkQuality,
            request.SyntheticDataConfirmed);
        var grantId = DeterministicGrantId(
            context.SessionId, participantRole, participantSubjectHash, idempotencyKey);
        var provision = videoProvider.Prepare(
            context.SessionId, grantId, participantRole, context.GrantExpiresAt);
        if (!string.Equals(
                provision.AdapterMode,
                SyntheticTelehealthVideoProvider.AdapterMode,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_adapter_unsafe",
                "Only the NON_PRODUCTION connection-room adapter is allowed in this slice.");
        }

        var grant = await repository.IssueGrantAsync(
            context,
            request.ExpectedVersion,
            participantRole,
            participantSubjectHash,
            request,
            grantId,
            provision,
            idempotencyKey,
            fingerprint,
            cancellationToken);
        return new TelehealthConnectionGrantResponse(
            grant.SessionId,
            grant.GrantId,
            grant.RequestId,
            grant.RequestVersion,
            grant.RequestStatus,
            grant.ParticipantRole,
            provision.AdapterMode,
            provision.JoinCredential,
            grant.ExpiresAt,
            false,
            false,
            false,
            "Your private synthetic waiting room is ready. No media is connected in this demonstration.",
            Limitations);
    }

    private static PrepareTelehealthConnectionRequest Normalize(PrepareTelehealthConnectionRequest request)
    {
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_version_invalid", "ExpectedVersion must be positive.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_video_synthetic_confirmation_required",
                "Confirm that this connection-room demonstration contains synthetic data only.");
        }
        if (!request.BrowserSupported
            || !request.CameraAvailable
            || !request.MicrophoneAvailable
            || !request.SpeakerAvailable)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_video_preflight_failed",
                "Browser, camera, microphone, and speaker checks must pass before entering the synthetic waiting room.");
        }
        var networkQuality = request.NetworkQuality?.Trim().ToLowerInvariant() ?? string.Empty;
        if (networkQuality is not ("unknown" or "limited" or "good"))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_video_network_quality_invalid",
                "NetworkQuality must be unknown, limited, or good.");
        }
        return request with { NetworkQuality = networkQuality };
    }

    private async Task<PatientPortalSessionResponse> RequirePatientAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var sessionId = await patientIdentityAdapter.ResolveSessionIdAsync(httpContext, cancellationToken);
        if (sessionId is null)
        {
            throw TelehealthProblem.Unauthorized();
        }
        var session = await patientPortalRepository.GetCurrentSessionAsync(sessionId.Value, cancellationToken);
        if (!session.Authenticated || string.IsNullOrWhiteSpace(session.CanonicalId))
        {
            throw TelehealthProblem.Unauthorized(session.FailureReason ?? "The patient session is not active.");
        }
        return session;
    }

    private static Guid DeterministicGrantId(
        Guid sessionId,
        string role,
        string subjectHash,
        string idempotencyKey)
    {
        var bytes = Convert.FromHexString(TelehealthCommandFingerprint.Create(
            "video-grant-id", sessionId, role, subjectHash, idempotencyKey));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string SubjectHash(string role, string subject) =>
        TelehealthProspectiveApplicantPolicy.Hash($"telehealth-video-participant-v1\u001f{role}\u001f{subject}");

    private static void RequirePhysician(AuthSessionResponse session)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_physician_role_required",
                "An eligible physician role is required for this action.");
        }
    }

    private void RequireConfiguredFacility(StaffAccessContext accessContext)
    {
        if (!TelehealthAuthorizationPolicy.IsConfiguredFacility(accessContext.FacilityId, _options.FacilityId))
        {
            throw TelehealthProblem.NotFound();
        }
    }

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw TelehealthProblem.NotFound();
        }
    }
}
