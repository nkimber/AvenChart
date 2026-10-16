// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthDemoService(
    TelehealthDemoRepository repository, TelehealthLocalWebRtcPocRelay relay,
    ITelehealthInternetCallingProvider calling, IOptions<TelehealthOptions> options,
    IHostEnvironment environment)
{
    private readonly TelehealthOptions _options = options.Value;

    public void RequireEnabled(HttpContext context)
    {
        if (!IsEnabled(_options, environment)
            || !_options.BrandedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
            throw TelehealthProblem.NotFound();
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public static bool IsEnabled(TelehealthOptions options, IHostEnvironment environment) =>
        options.DemoLauncherEnabled && options.Enabled && options.Mode == "Synthetic" && !environment.IsProduction();

    public Task<TelehealthDemoCreateResponse> CreateAsync(CreateTelehealthDemoRequest request, string commandId, CancellationToken ct)
    {
        if (!request.SyntheticDataConfirmed) throw TelehealthProblem.BadRequest("telehealth_demo_synthetic_confirmation", "Confirm that this connection demonstration uses fictional information only.");
        var transport = _options.InternetCallingPocEnabled ? "NON_PRODUCTION_INTERNET_ACS_CALLING_POC"
            : _options.LocalWebRtcPocEnabled ? "NON_PRODUCTION_LOCAL_WEBRTC_POC"
            : throw TelehealthProblem.ServiceUnavailable("telehealth_demo_media_disabled", "A synthetic media transport must be enabled before preparing this demonstration.");
        return repository.CreateAsync(_options.PracticeId, _options.FacilityId,
            TelehealthCommandFingerprint.RequireIdempotencyKey(commandId), TelehealthDemoPolicy.RequireKey(request.ObserverKey), transport, ct);
    }

    public Task<TelehealthDemoParticipant> RedeemAsync(Guid runId, string role, RedeemTelehealthDemoRequest request, CancellationToken ct) =>
        repository.RedeemAsync(_options.PracticeId, _options.FacilityId, runId, role, request.Invitation, request.SessionKey, ct);

    public Task<TelehealthDemoStatus> StatusAsync(Guid runId, string key, CancellationToken ct) =>
        repository.StatusAsync(_options.PracticeId, _options.FacilityId, runId, key, ct);

    private Task<TelehealthDemoParticipant> AuthorizeAsync(Guid runId, string role, string key, CancellationToken ct) =>
        repository.AuthorizeAsync(_options.PracticeId, _options.FacilityId, runId, role, key, ct);

    public async Task<TelehealthDemoStatus> EndAsync(Guid runId, string key, CancellationToken ct)
    {
        // Both participants and the observer can end only this synthetic run.
        await StatusAsync(runId, key, ct);
        await repository.EndAsync(runId, ct);
        return await StatusAsync(runId, key, ct);
    }

    public async Task<TelehealthDemoStatus> HeartbeatAsync(Guid runId, string role, string key, TelehealthDemoHeartbeatRequest request, CancellationToken ct)
    {
        var participant = await AuthorizeAsync(runId, role, key, ct);
        await repository.HeartbeatAsync(participant.ParticipantId, request.Stage, ct);
        return await StatusAsync(runId, key, ct);
    }

    public async Task<TelehealthInternetCallingConfigurationResponse> CallingAsync(Guid runId, string role, string key, CancellationToken ct)
    {
        var participant = await AuthorizeAsync(runId, role, key, ct);
        if (!_options.InternetCallingPocEnabled || participant.Transport != "NON_PRODUCTION_INTERNET_ACS_CALLING_POC") throw TelehealthProblem.NotFound();
        var credential = await calling.CreateConfigurationAsync(participant.ParticipantId, ct);
        await AuthorizeAsync(runId, role, key, ct);
        return new(runId.ToString(), credential.AccessToken, credential.ExpiresAt, "NON_PRODUCTION_INTERNET_ACS_CALLING_POC");
    }

    public async Task<TelehealthLocalWebRtcSignalReadResponse> ReadAsync(Guid runId, string role, string key, TelehealthDemoSignalRequest request, CancellationToken ct)
    {
        var participant = await AuthorizeAsync(runId, role, key, ct);
        if (!_options.LocalWebRtcPocEnabled || participant.Transport != "NON_PRODUCTION_LOCAL_WEBRTC_POC") throw TelehealthProblem.NotFound();
        if (request.AfterSequence < 0) throw TelehealthProblem.BadRequest("telehealth_demo_sequence", "The signal cursor cannot be negative.");
        return relay.Read(runId, role, request.AfterSequence, participant.ExpiresAt);
    }

    public async Task<TelehealthLocalWebRtcSignalWriteResponse> WriteAsync(Guid runId, string role, string key, TelehealthDemoSignalRequest request, CancellationToken ct)
    {
        var participant = await AuthorizeAsync(runId, role, key, ct);
        if (!_options.LocalWebRtcPocEnabled || participant.Transport != "NON_PRODUCTION_LOCAL_WEBRTC_POC") throw TelehealthProblem.NotFound();
        var kind = request.Kind;
        if (kind is not ("offer" or "answer" or "candidate" or "request-offer")) throw TelehealthProblem.BadRequest("telehealth_demo_signal_kind", "Choose a valid signaling operation.");
        var payload = request.Payload ?? string.Empty;
        if (payload.Length is < 2 or > 16384) throw TelehealthProblem.BadRequest("telehealth_demo_signal_size", "The signaling message is too large or empty.");
        try { using var json = System.Text.Json.JsonDocument.Parse(payload); }
        catch (System.Text.Json.JsonException) { throw TelehealthProblem.BadRequest("telehealth_demo_signal_json", "The signaling message must be valid JSON."); }
        return new(relay.Append(runId, role, kind, payload, participant.ExpiresAt), participant.ExpiresAt, "NON_PRODUCTION_LOCAL_WEBRTC_POC");
    }
}
