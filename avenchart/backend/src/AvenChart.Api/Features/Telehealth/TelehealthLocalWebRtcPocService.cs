// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthLocalWebRtcPocService(
    TelehealthLocalWebRtcPocRepository repository,
    TelehealthLocalWebRtcPocRelay relay,
    IOptions<TelehealthOptions> options)
{
    private readonly TelehealthOptions _options = options.Value;

    public async Task<TelehealthLocalWebRtcSignalWriteResponse> WriteAsync(
        TelehealthLocalWebRtcSignalWriteRequest request,
        CancellationToken cancellationToken)
    {
        var grant = await AuthorizeAsync(request.SessionId, request.GrantId, request.JoinCredential, cancellationToken);
        var kind = NormalizeKind(request.Kind);
        var payload = NormalizePayload(request.Payload);
        var sequence = relay.Append(grant.SessionId, grant.ParticipantRole, kind, payload, grant.ExpiresAt);
        return new TelehealthLocalWebRtcSignalWriteResponse(sequence, grant.ExpiresAt, "NON_PRODUCTION_LOCAL_WEBRTC_POC");
    }

    public async Task<TelehealthLocalWebRtcSignalReadResponse> ReadAsync(
        TelehealthLocalWebRtcSignalReadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AfterSequence < 0)
        {
            throw TelehealthProblem.BadRequest("telehealth_local_webrtc_sequence_invalid", "AfterSequence cannot be negative.");
        }

        var grant = await AuthorizeAsync(request.SessionId, request.GrantId, request.JoinCredential, cancellationToken);
        return relay.Read(grant.SessionId, grant.ParticipantRole, request.AfterSequence, grant.ExpiresAt);
    }

    private async Task<TelehealthLocalWebRtcGrantRecord> AuthorizeAsync(
        Guid sessionId,
        Guid grantId,
        string joinCredential,
        CancellationToken cancellationToken)
    {
        if (!_options.LocalWebRtcPocEnabled)
        {
            throw TelehealthProblem.NotFound();
        }

        var credential = TelehealthProspectiveApplicantPolicy.RequireAccessKey(joinCredential);
        var credentialHash = TelehealthProspectiveApplicantPolicy.Hash(credential);
        return await repository.AuthorizeAsync(sessionId, grantId, credentialHash, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
    }

    private static string NormalizeKind(string? value)
    {
        var kind = value?.Trim().ToLowerInvariant();
        if (kind is not ("offer" or "answer" or "candidate"))
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_local_webrtc_signal_kind_invalid",
                "Signal kind must be offer, answer, or candidate.");
        }
        return kind;
    }

    private static string NormalizePayload(string? value)
    {
        var payload = value?.Trim() ?? string.Empty;
        if (payload.Length is < 2 or > 16_384)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_local_webrtc_signal_payload_invalid",
                "Signal payload must contain a compact JSON value no longer than 16384 characters.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
            {
                throw new JsonException();
            }
        }
        catch (JsonException)
        {
            throw TelehealthProblem.BadRequest(
                "telehealth_local_webrtc_signal_payload_invalid",
                "Signal payload must contain a compact JSON object or null value.");
        }

        return payload;
    }
}
