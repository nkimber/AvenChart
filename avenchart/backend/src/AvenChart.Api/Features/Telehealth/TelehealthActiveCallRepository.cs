// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Features.Telehealth;

public sealed partial class TelehealthVideoRepository
{
    // The Ended session records the completed waiting-room handoff. It must
    // stay Ended: active media authorization follows the consultation instead
    // of reviving that handoff or extending an expired reservation.
    public async Task<TelehealthVideoContextRecord> PrepareActiveCallAsync(
        string practiceId, int facilityId, Guid? requestId, string? patientId,
        Guid? consultationId, int? physicianId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select session.session_id, r.request_id, context.reservation_id, r.version,
                   now() + interval '5 minutes'
            from telehealth_consultation_contexts context
            join telehealth_requests r on r.request_id=context.request_id
            join telehealth_video_sessions session on session.session_id=context.session_id
                and session.request_id=r.request_id and session.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
                and shift.clinician_staff_id=context.physician_staff_id
            join patients patient on patient.canonical_id=r.patient_id and patient.facility_id=r.facility_id
            where context.practice_id=@practice and context.facility_id=@facility
              and r.practice_id=context.practice_id and r.facility_id=context.facility_id
              and session.practice_id=context.practice_id and session.facility_id=context.facility_id
              and shift.practice_id=context.practice_id and shift.facility_id=context.facility_id
              and context.status='Started' and r.status='InConsultation'
              and shift.status='Busy' and session.status='Ended'
              and context.started_at > now() - interval '4 hours'
              and patient.lifecycle_status='active' and patient.merged_into_patient_id is null
              and ((@patient is not null and r.request_id=@request and r.patient_id=@patient and patient.portal_enabled)
                or (@physician is not null and context.consultation_id=@consultation and context.physician_staff_id=@physician))
            for update of r,session;
            """;
        command.Parameters.AddWithValue("practice", practiceId);
        command.Parameters.AddWithValue("facility", facilityId);
        command.Parameters.AddWithValue("request", NpgsqlDbType.Uuid, (object?)requestId ?? DBNull.Value);
        command.Parameters.AddWithValue("patient", NpgsqlDbType.Text, (object?)patientId ?? DBNull.Value);
        command.Parameters.AddWithValue("consultation", NpgsqlDbType.Uuid, (object?)consultationId ?? DBNull.Value);
        command.Parameters.AddWithValue("physician", NpgsqlDbType.Integer, (object?)physicianId ?? DBNull.Value);
        TelehealthVideoContextRecord result;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw TelehealthProblem.NotFound();
            result = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), checked((int)reader.GetInt64(3)),
                "InConsultation", reader.GetFieldValue<DateTimeOffset>(4), reader.GetFieldValue<DateTimeOffset>(4), true);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<ConnectionRow?> LoadActiveCallForUpdateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select r.request_id,r.version,r.status,now() + interval '5 minutes',now() + interval '5 minutes'
            from telehealth_consultation_contexts context
            join telehealth_requests r on r.request_id=context.request_id
            join telehealth_video_sessions session on session.session_id=context.session_id
              and session.request_id=r.request_id and session.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
              and shift.clinician_staff_id=context.physician_staff_id
            join patients patient on patient.canonical_id=r.patient_id and patient.facility_id=r.facility_id
            where session.session_id=@session and session.status='Ended'
              and context.status='Started' and r.status='InConsultation' and shift.status='Busy'
              and context.started_at > now() - interval '4 hours'
              and patient.lifecycle_status='active' and patient.merged_into_patient_id is null
            for update of r,session;
            """;
        command.Parameters.AddWithValue("session", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetGuid(0), checked((int)reader.GetInt64(1)), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4)) : null;
    }
}
