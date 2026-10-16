// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthLocalWebRtcGrantRecord(Guid SessionId, Guid GrantId, string ParticipantRole, DateTimeOffset ExpiresAt);

public sealed class TelehealthLocalWebRtcPocRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthLocalWebRtcGrantRecord?> AuthorizeAsync(
        Guid sessionId,
        Guid grantId,
        string credentialHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select video_grant.session_id,video_grant.participant_role,video_grant.expires_at
            from telehealth_video_participant_grants video_grant
            join telehealth_video_sessions session on session.session_id=video_grant.session_id
            join telehealth_requests request on request.request_id=session.request_id
            join patients patient on patient.canonical_id=request.patient_id and patient.facility_id=request.facility_id
            left join telehealth_consultation_contexts context on context.session_id=session.session_id
            left join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            where video_grant.session_id=@sessionId and video_grant.grant_id=@grantId
              and video_grant.credential_hash=@credentialHash
              and video_grant.status='Issued' and video_grant.expires_at>now()
              and patient.lifecycle_status='active' and patient.merged_into_patient_id is null
              and ((session.status='WaitingRoom' and session.expires_at>now() and request.status='Connecting')
                or (session.status='Ended' and request.status='InConsultation' and context.status='Started'
                    and shift.status='Busy' and context.started_at > now() - interval '4 hours'));
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("grantId", grantId);
        command.Parameters.AddWithValue("credentialHash", credentialHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new TelehealthLocalWebRtcGrantRecord(
                reader.GetGuid(0),
                grantId,
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }
}
