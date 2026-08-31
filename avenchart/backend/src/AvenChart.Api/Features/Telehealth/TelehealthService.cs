// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthService(
    TelehealthRepository repository,
    PatientPortalRepository patientPortalRepository,
    IPatientPortalIdentityAdapter patientIdentityAdapter,
    ITelehealthTriageEvaluator triageEvaluator,
    ITelehealthCoverageGateway coverageGateway,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public TelehealthPracticeContextResponse GetPracticeContext(HostString host)
    {
        RequireConfiguredHost(host);

        return new TelehealthPracticeContextResponse(
            true,
            _options.PracticeDisplayName,
            _options.SupportedStates,
            true,
            "Synthetic demonstration only. This service is not available for patient care.");
    }

    public async Task<TelehealthRequestResponse> CreateRequestAsync(
        HttpContext httpContext,
        CreateTelehealthRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        var complaint = NormalizeComplaint(request.ComplaintCategory);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create("create", complaint);
        return await repository.CreateAsync(
            _options.PracticeId,
            _options.FacilityId,
            patient.CanonicalId,
            complaint,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthRequestListResponse> ListPatientRequestsAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        return await repository.ListPatientRequestsAsync(_options.PracticeId, patient.CanonicalId, cancellationToken);
    }

    public async Task<TelehealthPatientQueueStatusResponse> GetPatientQueueStatusAsync(
        HttpContext httpContext,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        return await repository.GetPatientQueueStatusAsync(
            _options.PracticeId, patient.CanonicalId, requestId, cancellationToken);
    }

    public async Task<TelehealthRequestHistoryResponse> GetPatientRequestHistoryAsync(
        HttpContext httpContext,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        return await repository.GetPatientRequestHistoryAsync(
            _options.PracticeId, patient.CanonicalId, requestId, cancellationToken);
    }

    public async Task<TelehealthRequestResponse> ConfirmLocationAsync(
        HttpContext httpContext,
        Guid requestId,
        ConfirmTelehealthLocationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        var state = request.StateCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!_options.SupportedStates.Contains(state, StringComparer.Ordinal))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_location_unsupported",
                "Current physical location must be Georgia, California, or Florida for this synthetic demonstration.");
        }
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_version_invalid", "ExpectedVersion must be positive.");
        }

        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create("confirm-location", state, request.ExpectedVersion);
        return await repository.ConfirmLocationAsync(
            _options.PracticeId,
            patient.CanonicalId,
            requestId,
            state,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthRequestResponse> EvaluateTriageAsync(
        HttpContext httpContext,
        Guid requestId,
        EvaluateTelehealthTriageRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        if (request.ExpectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_version_invalid", "ExpectedVersion must be positive.");
        }
        var answers = new SyntheticTelehealthTriageAnswers(
            request.HasEmergencyWarning,
            request.SevereOrWorsening,
            request.RequiresHandsOnExam,
            request.Unsure);
        var result = triageEvaluator.Evaluate(answers);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "evaluate-triage", result.AnswerFingerprint, request.ExpectedVersion);
        return await repository.EvaluateTriageAsync(
            _options.PracticeId,
            patient.CanonicalId,
            requestId,
            result,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthPatientReadinessResponse> GetPatientReadinessAsync(
        HttpContext httpContext,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        return await repository.GetPatientReadinessAsync(
            _options.PracticeId, patient.CanonicalId, requestId, cancellationToken);
    }

    public async Task<TelehealthRequestResponse> CompleteReadinessAsync(
        HttpContext httpContext,
        Guid requestId,
        CompleteTelehealthReadinessRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        RequirePositiveVersion(request.ExpectedVersion);
        var summary = request.ComplaintSummary?.Trim() ?? string.Empty;
        if (summary.Length is < 10 or > 500)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_complaint_summary_invalid",
                "The synthetic complaint summary must contain 10 to 500 characters.");
        }
        var duration = request.SymptomDuration?.Trim().ToLowerInvariant() ?? string.Empty;
        if (duration is not ("less-than-day" or "1-3-days" or "4-14-days" or "more-than-14-days"))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_symptom_duration_invalid",
                "Choose one of the supported synthetic symptom-duration ranges.");
        }
        var normalized = request with
        {
            DemographicsFingerprint = request.DemographicsFingerprint?.Trim().ToLowerInvariant() ?? string.Empty,
            ClinicalSummaryFingerprint = request.ClinicalSummaryFingerprint?.Trim().ToLowerInvariant() ?? string.Empty,
            ComplaintSummary = summary,
            SymptomDuration = duration,
            CoverageToken = request.CoverageToken?.Trim().ToLowerInvariant() ?? string.Empty,
            CoverageFingerprint = request.CoverageFingerprint?.Trim().ToLowerInvariant() ?? string.Empty,
            AcknowledgmentPackageKey = request.AcknowledgmentPackageKey?.Trim() ?? string.Empty,
            AcknowledgmentContentHash = request.AcknowledgmentContentHash?.Trim().ToLowerInvariant() ?? string.Empty
        };
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "complete-readiness",
            normalized.ExpectedVersion,
            normalized.DemographicsFingerprint,
            normalized.ClinicalSummaryFingerprint,
            normalized.DemographicsConfirmed,
            normalized.ContactConfirmed,
            normalized.ClinicalSummaryConfirmed,
            normalized.ComplaintSummary,
            normalized.SymptomDuration,
            normalized.SyntheticDataConfirmed,
            normalized.CoverageToken,
            normalized.CoverageFingerprint,
            normalized.CoverageConfirmed,
            normalized.AcknowledgmentPackageKey,
            normalized.AcknowledgmentPackageVersion,
            normalized.AcknowledgmentContentHash,
            normalized.AcknowledgmentAccepted);
        return await repository.CompleteReadinessAsync(
            _options.PracticeId,
            patient.CanonicalId,
            requestId,
            normalized,
            summary,
            duration,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthRequestResponse> VerifyCoverageAsync(
        HttpContext httpContext,
        Guid requestId,
        VerifyTelehealthCoverageRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        RequirePositiveVersion(request.ExpectedVersion);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var input = await repository.GetCoverageGatewayInputAsync(
            _options.PracticeId, patient.CanonicalId, requestId, cancellationToken);
        var result = coverageGateway.Verify(input);
        if (!string.Equals(result.AdapterMode, SyntheticTelehealthCoverageGateway.AdapterMode, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_coverage_adapter_unsafe",
                "Only the approved NON_PRODUCTION coverage adapter can run in this synthetic slice.");
        }
        var fingerprint = TelehealthCommandFingerprint.Create(
            "verify-coverage",
            request.ExpectedVersion,
            result.AdapterMode,
            result.EligibilityStatus,
            result.NetworkStatus,
            result.FinancialRoute,
            result.EligibilitySource,
            result.NetworkSource,
            result.EvidenceKey,
            result.EvidenceVersion,
            result.InputFingerprint,
            string.Join('\u001f', result.Limitations));
        return await repository.RecordCoverageVerificationAsync(
            _options.PracticeId,
            patient.CanonicalId,
            requestId,
            request.ExpectedVersion,
            input,
            result,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthRequestResponse> CancelRequestAsync(
        HttpContext httpContext,
        Guid requestId,
        CancelTelehealthRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireConfiguredHost(httpContext.Request.Host);
        var patient = await RequirePatientAsync(httpContext, cancellationToken);
        RequirePositiveVersion(request.ExpectedVersion);
        if (!request.SyntheticCancellationConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_synthetic_cancellation_confirmation_required",
                "Confirm that this is a synthetic request cancellation.");
        }

        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "cancel-synthetic-request", requestId, request.ExpectedVersion, request.SyntheticCancellationConfirmed);
        return await repository.CancelRequestAsync(
            _options.PracticeId,
            patient.CanonicalId,
            requestId,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthOperationalReviewResponse> ListOperationalReviewAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        return await repository.ListOperationalReviewAsync(_options.PracticeId, accessContext.FacilityId, cancellationToken);
    }

    public async Task<TelehealthRequestResponse> AuthorizeToQueueAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid requestId,
        AuthorizeTelehealthRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(session);
        RequireConfiguredFacility(accessContext);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create("authorize-to-queue", requestId, request.ExpectedVersion);
        return await repository.AuthorizeToQueueAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            session.Username,
            requestId,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthShiftResponse> StartShiftAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        var staffId = RequireStaffId(session);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create("start-shift", _options.PracticeId, accessContext.FacilityId, staffId);
        return await repository.StartShiftAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthShiftResponse> EndIdleShiftAsync(AuthSessionResponse session, StaffAccessContext accessContext, Guid shiftId, EndTelehealthShiftRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        RequirePhysician(session); RequireConfiguredFacility(accessContext);
        if (request.ExpectedVersion < 1 || !request.NoActiveWorkConfirmed || !request.SyntheticEndConfirmed) throw TelehealthProblem.BadRequest("telehealth_shift_end_invalid", "Confirm no active work and the synthetic-only end effect with a current shift version.");
        var staffId = RequireStaffId(session); var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        return await repository.EndIdleShiftAsync(_options.PracticeId, accessContext.FacilityId, staffId, shiftId, request.ExpectedVersion, key,
            TelehealthCommandFingerprint.Create("end-idle-shift", _options.PracticeId, accessContext.FacilityId, staffId, shiftId, request.ExpectedVersion), cancellationToken);
    }

    public async Task<TelehealthQueueResponse> ListClinicianQueueAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        return await repository.ListClinicianQueueAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            RequireStaffId(session),
            cancellationToken);
    }

    public async Task<TelehealthReservationResponse?> ReserveNextAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        var staffId = RequireStaffId(session);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create("reserve-next", _options.PracticeId, accessContext.FacilityId, staffId);
        return await repository.ReserveNextAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            _options.ReservationLeaseSeconds,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthReservationReleaseResponse> ReleaseReservationAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid reservationId,
        ReleaseTelehealthReservationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        if (request.ExpectedVersion < 1
            || !request.NoConnectionOrConsultationConfirmed
            || !request.SyntheticReleaseConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_reservation_release_invalid",
                "Confirm the current version, no connection or consultation, and the synthetic-only release effect.");
        }

        var staffId = RequireStaffId(session);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "release-reservation",
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            reservationId,
            request.ExpectedVersion);
        return await repository.ReleaseReservationAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            reservationId,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
    }

    public async Task<TelehealthReservationReleaseResponse> AbandonConnectionAsync(
        AuthSessionResponse session,
        StaffAccessContext accessContext,
        Guid reservationId,
        AbandonTelehealthConnectionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        RequirePhysician(session);
        RequireConfiguredFacility(accessContext);
        if (request.ExpectedVersion < 1
            || !request.NoConsultationConfirmed
            || !request.SyntheticConnectionAbandonConfirmed)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_connection_abandon_invalid",
                "Confirm the current version, that no consultation has started, and the synthetic-only connection-abandon effect.");
        }

        var staffId = RequireStaffId(session);
        var key = TelehealthCommandFingerprint.RequireIdempotencyKey(idempotencyKey);
        var fingerprint = TelehealthCommandFingerprint.Create(
            "abandon-connection",
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            reservationId,
            request.ExpectedVersion);
        return await repository.AbandonConnectionAsync(
            _options.PracticeId,
            accessContext.FacilityId,
            staffId,
            reservationId,
            request.ExpectedVersion,
            key,
            fingerprint,
            cancellationToken);
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

    private static string NormalizeComplaint(string? complaintCategory)
    {
        var value = complaintCategory?.Trim().ToLowerInvariant() ?? string.Empty;
        return value is "migraine" or "sleep"
            ? value
            : throw TelehealthProblem.BadRequest(
                "telehealth_complaint_not_supported",
                "The synthetic foundation supports only migraine or sleep demonstration categories.");
    }

    private static void RequirePositiveVersion(int expectedVersion)
    {
        if (expectedVersion < 1)
        {
            throw TelehealthProblem.BadRequest("telehealth_version_invalid", "ExpectedVersion must be positive.");
        }
    }

    private static void RequireAdministrator(AuthSessionResponse session)
    {
        if (!TelehealthAuthorizationPolicy.IsAdministratorRole(session.Role))
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_administrator_role_required",
                "An authorized practice administrator is required for this action.");
        }
    }

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

    private static int RequireStaffId(AuthSessionResponse session) => session.StaffId
        ?? throw TelehealthProblem.Forbidden("telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
}
