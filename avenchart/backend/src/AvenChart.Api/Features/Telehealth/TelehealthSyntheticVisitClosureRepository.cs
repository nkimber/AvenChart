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
            await transaction.CommitAsync(cancellationToken); return new TelehealthSyntheticVisitClosureResponse(consultationId, replay.Value.ConsultationVersion, replay.Value.RequestVersion, replay.Value.ClosedAt, true, true, false, false, false, false, false, ["This is a replay of a NON_PRODUCTION synthetic lifecycle closure only.", "The appointment remains in progress; no encounter completion, patient delivery, billing, claim, integration, or external action was created."]);
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
            if (!await reader.ReadAsync(cancellationToken) || reader.GetInt64(0) != 1 || reader.GetInt64(1) != 1 || reader.GetInt64(2) != 1) throw new TelehealthSyntheticVisitClosureConflictException("The consultation changed while closing the synthetic visit.");
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
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_synthetic_post_visit_receipts(
                    receipt_id,request_id,consultation_id,encounter_id,practice_id,facility_id,patient_id,
                    consultation_version,request_version,source_evidence_hash,receipt_state,source_mode,
                    synthetic_data_confirmed,appointment_completed,encounter_completed,clinical_record_delivered,
                    prescription_delivered,billing_created,claim_created,notification_sent,external_destination_contacted,created_at)
                values(
                    @receiptId,@requestId,@consultationId,@encounterId,@practiceId,@facilityId,@patientId,
                    @consultationVersion,@requestVersion,@sourceHash,'AvailableInPortal','NON_PRODUCTION',
                    true,false,false,false,false,false,false,false,false,@now);
                """;
            receipt.Parameters.AddWithValue("receiptId", Guid.NewGuid()); receipt.Parameters.AddWithValue("requestId", source.RequestId); receipt.Parameters.AddWithValue("consultationId", consultationId); receipt.Parameters.AddWithValue("encounterId", source.EncounterId);
            receipt.Parameters.AddWithValue("practiceId", practiceId); receipt.Parameters.AddWithValue("facilityId", facilityId); receipt.Parameters.AddWithValue("patientId", source.PatientId); receipt.Parameters.AddWithValue("consultationVersion", nextConsultationVersion); receipt.Parameters.AddWithValue("requestVersion", nextRequestVersion);
            receipt.Parameters.AddWithValue("sourceHash", TelehealthCommandFingerprint.Create("synthetic-post-visit-receipt-v1", consultationId, source.RequestId, source.EncounterId, nextConsultationVersion, nextRequestVersion)); receipt.Parameters.AddWithValue("now", source.DatabaseNow);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var preview = connection.CreateCommand())
        {
            preview.Transaction = transaction;
            preview.CommandText = """
                insert into telehealth_synthetic_after_visit_plan_previews(
                    preview_id,request_id,consultation_id,encounter_id,practice_id,facility_id,patient_id,
                    consultation_version,request_version,disposition_version,final_clinical_review_version,
                    source_evidence_hash,preview_state,source_mode,synthetic_data_confirmed,disposition_code,
                    follow_up_owner,follow_up_timeframe,next_step_instructions,warning_escalation_instructions,
                    communication_method,communication_completed,appointment_completed,encounter_completed,
                    avs_delivered,notification_sent,external_destination_contacted,created_at)
                values(
                    @previewId,@requestId,@consultationId,@encounterId,@practiceId,@facilityId,@patientId,
                    @consultationVersion,@requestVersion,@dispositionVersion,@finalReviewVersion,
                    @sourceHash,'AvailableInPortal','NON_PRODUCTION',true,@dispositionCode,
                    @followUpOwner,@followUpTimeframe,@nextSteps,@warnings,@communicationMethod,
                    @communicationCompleted,false,false,false,false,false,@now);
                """;
            preview.Parameters.AddWithValue("previewId", Guid.NewGuid()); preview.Parameters.AddWithValue("requestId", source.RequestId); preview.Parameters.AddWithValue("consultationId", consultationId); preview.Parameters.AddWithValue("encounterId", source.EncounterId);
            preview.Parameters.AddWithValue("practiceId", practiceId); preview.Parameters.AddWithValue("facilityId", facilityId); preview.Parameters.AddWithValue("patientId", source.PatientId); preview.Parameters.AddWithValue("consultationVersion", nextConsultationVersion); preview.Parameters.AddWithValue("requestVersion", nextRequestVersion);
            preview.Parameters.AddWithValue("dispositionVersion", source.DispositionVersion); preview.Parameters.AddWithValue("finalReviewVersion", source.FinalClinicalReviewVersion);
            preview.Parameters.AddWithValue("sourceHash", TelehealthCommandFingerprint.Create("synthetic-after-visit-plan-preview-v1", consultationId, source.RequestId, source.EncounterId, nextConsultationVersion, nextRequestVersion, source.DispositionVersion, source.FinalClinicalReviewVersion));
            preview.Parameters.AddWithValue("dispositionCode", source.DispositionCode); preview.Parameters.AddWithValue("followUpOwner", source.FollowUpOwner); preview.Parameters.AddWithValue("followUpTimeframe", source.FollowUpTimeframe); preview.Parameters.AddWithValue("nextSteps", source.NextStepInstructions); preview.Parameters.AddWithValue("warnings", source.WarningEscalationInstructions); preview.Parameters.AddWithValue("communicationMethod", source.CommunicationMethod); preview.Parameters.AddWithValue("communicationCompleted", source.CommunicationCompleted); preview.Parameters.AddWithValue("now", source.DatabaseNow);
            await preview.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(consultationId, source with { ConsultationVersion = nextConsultationVersion, RequestVersion = nextRequestVersion, ClosedAt = source.DatabaseNow });
    }

    private static async Task<Source?> ReadAndLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string practiceId, int facilityId, int physician, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select context.request_id,context.shift_id,context.encounter_id,request.patient_id,context.version::integer,request.version::integer,now(),
                   disposition.version,final_review.version,disposition.disposition_code,disposition.follow_up_owner,
                   disposition.follow_up_timeframe,disposition.next_step_instructions,disposition.warning_escalation_instructions,
                   disposition.communication_method,disposition.communication_completed
            from telehealth_consultation_contexts context join telehealth_requests request on request.request_id=context.request_id join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id join telehealth_video_sessions session on session.session_id=context.session_id join appointments appointment on appointment.id=context.appointment_id join encounters encounter on encounter.encounter=context.encounter_id
            left join telehealth_consultation_prescription_orders prescription on prescription.consultation_id=context.consultation_id
            left join lateral (select version from clinical_notes where encounter=context.encounter_id order by version desc,id desc limit 1) note on true
            join lateral (select version,disposition_code,follow_up_owner,follow_up_timeframe,next_step_instructions,warning_escalation_instructions,communication_method,communication_completed from telehealth_consultation_disposition_draft_versions where consultation_id=context.consultation_id order by version desc limit 1) disposition on true
            join lateral (select version from telehealth_consultation_final_clinical_review_versions review where review.consultation_id=context.consultation_id and review.documentation_version=coalesce(note.version,0) and review.disposition_version=disposition.version and review.prescription_order_id is not distinct from prescription.order_id order by version desc limit 1) final_review on true
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='MediaEnded' and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp' and reservation.clinician_staff_id=@physician and reservation.status='Released' and shift.clinician_staff_id=@physician and shift.status='WrapUp' and session.status='Ended' and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId and exists(select 1 from encounter_signatures signature where signature.encounter=encounter.encounter and signature.is_lock)
            for update of context,request,reservation,shift,session,appointment,encounter;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physician);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? new Source(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetString(14), reader.GetBoolean(15), null) : null;
    }
    private static async Task<(string Fingerprint, int ConsultationVersion, int RequestVersion, DateTimeOffset ClosedAt)?> ReadReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string practiceId, int facilityId, int physician, Guid consultationId, string actorHash, string key, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select consultation.command_fingerprint,consultation.aggregate_version::integer,request.aggregate_version::integer,consultation.occurred_at from telehealth_consultation_events consultation join telehealth_request_events request on request.request_id=consultation.request_id and request.idempotency_key=consultation.idempotency_key and request.action='synthetic-visit-closed' join telehealth_consultation_contexts context on context.consultation_id=consultation.consultation_id where consultation.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='Closed' and consultation.actor_subject_hash=@actorHash and consultation.idempotency_key=@key and consultation.action='synthetic-visit-closed';"; command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physician); command.Parameters.AddWithValue("actorHash", actorHash); command.Parameters.AddWithValue("key", key); await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetFieldValue<DateTimeOffset>(3)) : null; }
    private static TelehealthSyntheticVisitClosureResponse ToResponse(Guid consultationId, Source source) => new(consultationId, source.ConsultationVersion, source.RequestVersion, source.ClosedAt!.Value, true, true, false, false, false, false, false, ["This is a NON_PRODUCTION synthetic lifecycle closure only.", "The appointment remains in progress; no encounter completion, patient delivery, billing, claim, integration, or external action was created."]);
    private sealed record Source(Guid RequestId, Guid ShiftId, int EncounterId, string PatientId, int ConsultationVersion, int RequestVersion, DateTimeOffset DatabaseNow, int DispositionVersion, int FinalClinicalReviewVersion, string DispositionCode, string FollowUpOwner, string FollowUpTimeframe, string NextStepInstructions, string WarningEscalationInstructions, string CommunicationMethod, bool CommunicationCompleted, DateTimeOffset? ClosedAt);
}
public sealed class TelehealthSyntheticVisitClosureConflictException(string message) : Exception(message);
