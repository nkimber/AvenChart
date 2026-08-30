// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthSyntheticVisitClosureRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthSyntheticVisitClosureResponse?> CloseAsync(string practiceId, int facilityId, int physicianStaffId,
        Guid consultationId, CloseSyntheticTelehealthVisitRequest request, string actorHash, string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, actorHash, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.Fingerprint, fingerprint, StringComparison.Ordinal)) throw new TelehealthSyntheticVisitClosureConflictException("The idempotency key was already used for a different closure command.");
            await transaction.CommitAsync(cancellationToken); return new TelehealthSyntheticVisitClosureResponse(consultationId,replay.Value.ConsultationVersion,replay.Value.RequestVersion,replay.Value.ClosedAt,true,true,false,false,false,false,false,["This is a replay of a NON_PRODUCTION synthetic lifecycle closure only.","The appointment remains in progress; no encounter completion, patient delivery, billing, claim, integration, or external action was created."]);
        }
        var source = await ReadAndLockAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, cancellationToken);
        if (source is null) { await transaction.RollbackAsync(cancellationToken); return null; }
        if (!request.EncounterLockReviewed || !request.SyntheticClosureConfirmed) throw new ArgumentException("Confirm the governed encounter lock and synthetic-only closure effect.");
        if (request.ExpectedConsultationVersion != source.ConsultationVersion) throw new TelehealthSyntheticVisitClosureConflictException("The consultation changed. Reload before closing the synthetic visit.");
        TelehealthRequestStateMachine.RequireTransition(TelehealthRequestStatus.WrapUp, TelehealthRequestStatus.Closed);
        var nextConsultationVersion = source.ConsultationVersion + 1; var nextRequestVersion = source.RequestVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                with closed_context as (update telehealth_consultation_contexts set status='Closed',version=@consultationVersion,closed_at=@now where consultation_id=@consultationId and status='MediaEnded' and version=@expectedVersion returning 1),
                closed_request as (update telehealth_requests set status='Closed',version=@requestVersion,updated_at=@now where request_id=@requestId and status='WrapUp' and version=@expectedRequestVersion returning 1),
                released_shift as (update telehealth_clinician_shifts set status='Active',version=version+1 where shift_id=@shiftId and status='WrapUp' and clinician_staff_id=@physician returning 1)
                select (select count(*) from closed_context),(select count(*) from closed_request),(select count(*) from released_shift);
                """;
            update.Parameters.AddWithValue("consultationId", consultationId); update.Parameters.AddWithValue("requestId", source.RequestId); update.Parameters.AddWithValue("shiftId", source.ShiftId); update.Parameters.AddWithValue("physician", physicianStaffId);
            update.Parameters.AddWithValue("expectedVersion", request.ExpectedConsultationVersion); update.Parameters.AddWithValue("expectedRequestVersion", source.RequestVersion); update.Parameters.AddWithValue("consultationVersion", nextConsultationVersion); update.Parameters.AddWithValue("requestVersion", nextRequestVersion); update.Parameters.AddWithValue("now", source.DatabaseNow);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetInt64(0)!=1 || reader.GetInt64(1)!=1 || reader.GetInt64(2)!=1) throw new TelehealthSyntheticVisitClosureConflictException("The consultation changed while closing the synthetic visit.");
        }
        await using (var events = connection.CreateCommand())
        {
            events.Transaction = transaction;
            events.CommandText = """
                insert into telehealth_consultation_events(event_id,consultation_id,request_id,aggregate_version,action,actor_type,actor_subject_hash,idempotency_key,command_fingerprint,occurred_at)
                values(@eventId,@consultationId,@requestId,@consultationVersion,'synthetic-visit-closed','physician',@actorHash,@key,@fingerprint,@now);
                insert into telehealth_request_events(event_id,request_id,aggregate_version,action,from_status,to_status,actor_type,actor_id,idempotency_key,command_fingerprint,occurred_at)
                values(@requestEventId,@requestId,@requestVersion,'synthetic-visit-closed','WrapUp','Closed','physician',@actorId,@key,@fingerprint,@now);
                """;
            events.Parameters.AddWithValue("eventId", Guid.NewGuid()); events.Parameters.AddWithValue("requestEventId", Guid.NewGuid()); events.Parameters.AddWithValue("consultationId", consultationId); events.Parameters.AddWithValue("requestId", source.RequestId); events.Parameters.AddWithValue("consultationVersion", nextConsultationVersion); events.Parameters.AddWithValue("requestVersion", nextRequestVersion); events.Parameters.AddWithValue("actorHash", actorHash); events.Parameters.AddWithValue("actorId", physicianStaffId.ToString()); events.Parameters.AddWithValue("key", idempotencyKey); events.Parameters.AddWithValue("fingerprint", fingerprint); events.Parameters.AddWithValue("now", source.DatabaseNow);
            await events.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(consultationId, source with { ConsultationVersion = nextConsultationVersion, RequestVersion = nextRequestVersion, ClosedAt = source.DatabaseNow });
    }

    private static async Task<Source?> ReadAndLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string practiceId, int facilityId, int physician, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select context.request_id,context.shift_id,context.version::integer,request.version::integer,now()
            from telehealth_consultation_contexts context join telehealth_requests request on request.request_id=context.request_id join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id join telehealth_video_sessions session on session.session_id=context.session_id join appointments appointment on appointment.id=context.appointment_id join encounters encounter on encounter.encounter=context.encounter_id
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='MediaEnded' and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp' and reservation.clinician_staff_id=@physician and reservation.status='Released' and shift.clinician_staff_id=@physician and shift.status='WrapUp' and session.status='Ended' and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId and exists(select 1 from encounter_signatures signature where signature.encounter=encounter.encounter and signature.is_lock)
            for update of context,request,reservation,shift,session,appointment,encounter;
            """;
        command.Parameters.AddWithValue("consultationId",consultationId); command.Parameters.AddWithValue("practiceId",practiceId); command.Parameters.AddWithValue("facilityId",facilityId); command.Parameters.AddWithValue("physician",physician);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? new Source(reader.GetGuid(0),reader.GetGuid(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetFieldValue<DateTimeOffset>(4),null) : null;
    }
    private static async Task<(string Fingerprint,int ConsultationVersion,int RequestVersion,DateTimeOffset ClosedAt)?> ReadReplayAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string practiceId,int facilityId,int physician,Guid consultationId,string actorHash,string key,CancellationToken cancellationToken) { await using var command=connection.CreateCommand(); command.Transaction=transaction; command.CommandText="select consultation.command_fingerprint,consultation.aggregate_version::integer,request.aggregate_version::integer,consultation.occurred_at from telehealth_consultation_events consultation join telehealth_request_events request on request.request_id=consultation.request_id and request.idempotency_key=consultation.idempotency_key and request.action='synthetic-visit-closed' join telehealth_consultation_contexts context on context.consultation_id=consultation.consultation_id where consultation.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='Closed' and consultation.actor_subject_hash=@actorHash and consultation.idempotency_key=@key and consultation.action='synthetic-visit-closed';"; command.Parameters.AddWithValue("consultationId",consultationId); command.Parameters.AddWithValue("practiceId",practiceId); command.Parameters.AddWithValue("facilityId",facilityId); command.Parameters.AddWithValue("physician",physician); command.Parameters.AddWithValue("actorHash",actorHash); command.Parameters.AddWithValue("key",key); await using var reader=await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? (reader.GetString(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetFieldValue<DateTimeOffset>(3)) : null; }
    private static TelehealthSyntheticVisitClosureResponse ToResponse(Guid consultationId, Source source) => new(consultationId,source.ConsultationVersion,source.RequestVersion,source.ClosedAt!.Value,true,true,false,false,false,false,false,["This is a NON_PRODUCTION synthetic lifecycle closure only.","The appointment remains in progress; no encounter completion, patient delivery, billing, claim, integration, or external action was created."]);
    private sealed record Source(Guid RequestId,Guid ShiftId,int ConsultationVersion,int RequestVersion,DateTimeOffset DatabaseNow,DateTimeOffset? ClosedAt);
}
public sealed class TelehealthSyntheticVisitClosureConflictException(string message) : Exception(message);
