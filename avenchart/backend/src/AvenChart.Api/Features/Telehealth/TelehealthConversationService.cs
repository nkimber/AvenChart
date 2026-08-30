// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Data;
using AvenChart.Api.Models;
using AvenChart.Api.Security;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthConversationService(
    TelehealthConversationRepository repository,
    PatientPortalRepository patientPortalRepository,
    IPatientPortalIdentityAdapter patientIdentityAdapter,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthConversationResponse> GetForPatientAsync(HttpContext context, Guid requestId, CancellationToken cancellationToken)
    {
        RequireConfiguredHost(context.Request.Host);
        var patient = await RequirePatientAsync(context, cancellationToken);
        return await repository.GetForPatientAsync(_options.PracticeId, patient.CanonicalId, requestId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthConversationResponse> AddForPatientAsync(HttpContext context, Guid requestId, TelehealthConversationMessageRequest request, CancellationToken cancellationToken)
    {
        RequireConfiguredHost(context.Request.Host);
        var patient = await RequirePatientAsync(context, cancellationToken);
        var body = Validate(request);
        return await repository.AddForPatientAsync(_options.PracticeId, patient.CanonicalId, requestId, body, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthConversationResponse> GetForPhysicianAsync(AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId, CancellationToken cancellationToken)
    {
        var staffId = RequirePhysician(session, accessContext);
        return await repository.GetForPhysicianAsync(_options.PracticeId, staffId, consultationId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    public async Task<TelehealthConversationResponse> AddForPhysicianAsync(AuthSessionResponse session, StaffAccessContext accessContext, Guid consultationId, TelehealthConversationMessageRequest request, CancellationToken cancellationToken)
    {
        var staffId = RequirePhysician(session, accessContext);
        var body = Validate(request);
        return await repository.AddForPhysicianAsync(_options.PracticeId, staffId, consultationId, body, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    private int RequirePhysician(AuthSessionResponse session, StaffAccessContext accessContext)
    {
        if (!TelehealthAuthorizationPolicy.IsPhysicianRole(session.Role))
            throw TelehealthProblem.Forbidden("telehealth_physician_role_required", "An eligible physician role is required for the synthetic transcript.");
        if (accessContext.FacilityId != _options.FacilityId)
            throw TelehealthProblem.NotFound();
        return session.StaffId ?? throw TelehealthProblem.Forbidden("telehealth_staff_record_required", "The authenticated identity is not bound to an active staff record.");
    }

    private static string Validate(TelehealthConversationMessageRequest request)
    {
        if (!request.SyntheticDataConfirmed)
            throw TelehealthProblem.BadRequest("telehealth_conversation_synthetic_confirmation_required", "Confirm that this transcript message contains synthetic demonstration data only.");
        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length is < 1 or > 1000 || body.Any(char.IsControl))
            throw TelehealthProblem.BadRequest("telehealth_conversation_message_invalid", "A synthetic transcript message must contain 1 to 1000 printable characters.");
        return body;
    }

    private async Task<PatientPortalSessionResponse> RequirePatientAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = await patientIdentityAdapter.ResolveSessionIdAsync(context, cancellationToken);
        if (sessionId is null) throw TelehealthProblem.Unauthorized();
        var session = await patientPortalRepository.GetCurrentSessionAsync(sessionId.Value, cancellationToken);
        if (!session.Authenticated || string.IsNullOrWhiteSpace(session.CanonicalId))
            throw TelehealthProblem.Unauthorized(session.FailureReason ?? "The patient session is not active.");
        return session;
    }

    private void RequireConfiguredHost(HostString host)
    {
        if (!_options.BrandedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase)) throw TelehealthProblem.NotFound();
    }
}
