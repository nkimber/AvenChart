// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthFinalClinicalReviewRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthFinalClinicalReviewWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var source = await ReadSourceAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, false, cancellationToken);
        if (source is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var review = await ReadCurrentReviewAsync(connection, transaction, source, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToWorkspace(source, review);
    }

    public async Task<TelehealthFinalClinicalReviewResponse?> RecordAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        RecordTelehealthFinalClinicalReviewRequest request,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var source = await ReadSourceAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, true, cancellationToken);
        if (source is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var replay = await ReadReplayAsync(connection, transaction, consultationId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw new TelehealthFinalClinicalReviewConflictException("The idempotency key was already used for a different final clinical-review command.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Review;
        }

        ValidateSource(source, request);
        var version = await ReadNextVersionAsync(connection, transaction, consultationId, cancellationToken);
        var reviewedAt = source.DatabaseNow;
        var reviewId = Guid.NewGuid();
        var contentHash = CreateHash(string.Join("|",
            consultationId.ToString("D"), source.EncounterId, version, source.Documentation.Version,
            source.Disposition!.Version, source.PrescriptionOrderId?.ToString("D") ?? string.Empty,
            physicianStaffId, reviewedAt.ToString("O"), commandFingerprint));

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_consultation_final_clinical_review_versions(
                  final_clinical_review_version_id,consultation_id,encounter_id,version,
                  documentation_version,disposition_version,prescription_order_id,
                  documentation_reviewed,physician_responsibility_confirmed,
                  no_automatic_claim_or_delivery_confirmed,synthetic_data_confirmed,
                  reviewed_at,reviewed_by_staff_id,content_hash)
                values(
                  @reviewId,@consultationId,@encounterId,@version,
                  @documentationVersion,@dispositionVersion,@prescriptionOrderId,
                  true,true,true,true,@reviewedAt,@physician,@contentHash);
                """;
            insert.Parameters.AddWithValue("reviewId", reviewId);
            insert.Parameters.AddWithValue("consultationId", consultationId);
            insert.Parameters.AddWithValue("encounterId", source.EncounterId);
            insert.Parameters.AddWithValue("version", version);
            insert.Parameters.AddWithValue("documentationVersion", source.Documentation.Version);
            insert.Parameters.AddWithValue("dispositionVersion", source.Disposition!.Version);
            insert.Parameters.Add("prescriptionOrderId", NpgsqlDbType.Uuid).Value = source.PrescriptionOrderId is { } orderId ? orderId : DBNull.Value;
            insert.Parameters.Add("reviewedAt", NpgsqlDbType.TimestampTz).Value = reviewedAt;
            insert.Parameters.AddWithValue("physician", physicianStaffId);
            insert.Parameters.Add("contentHash", NpgsqlDbType.Text).Value = contentHash;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_consultation_final_clinical_review_events(
                  event_id,consultation_id,final_clinical_review_version_id,aggregate_version,
                  action,actor_type,actor_id,idempotency_key,command_fingerprint,occurred_at)
                values(@eventId,@consultationId,@reviewId,@version,
                       'FinalClinicalReviewRecorded','physician',@actorId,@idempotencyKey,@commandFingerprint,@occurredAt);
                """;
            insert.Parameters.AddWithValue("eventId", Guid.NewGuid());
            insert.Parameters.AddWithValue("consultationId", consultationId);
            insert.Parameters.AddWithValue("reviewId", reviewId);
            insert.Parameters.AddWithValue("version", version);
            insert.Parameters.Add("actorId", NpgsqlDbType.Text).Value = physicianStaffId.ToString();
            insert.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
            insert.Parameters.Add("commandFingerprint", NpgsqlDbType.Text).Value = commandFingerprint;
            insert.Parameters.Add("occurredAt", NpgsqlDbType.TimestampTz).Value = reviewedAt;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new TelehealthFinalClinicalReviewResponse(
            reviewId, version, source.Documentation.Version, source.Disposition!.Version,
            source.PrescriptionOrderId, reviewedAt, contentHash,
            LegalEffect: false, EncounterSignatureCreated: false, CompletionCreated: false,
            PatientDeliveryCreated: false, BillingCreated: false, ClaimCreated: false,
            ExternalDestinationContacted: false);
    }

    private static async Task<Source?> ReadSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        bool lockAggregate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.encounter_id,now(),
                   coalesce(note.version,0),
                   coalesce(length(trim(coalesce(note.subjective,''))) > 0,false),
                   coalesce(length(trim(coalesce(note.objective,''))) > 0,false),
                   coalesce(length(trim(coalesce(note.assessment,''))) > 0,false),
                   coalesce(length(trim(coalesce(note.plan,''))) > 0,false),
                   disposition.version,disposition.disposition_code,
                   disposition.adequate_evaluation_completed,
                   length(trim(disposition.follow_up_owner)) > 0,
                   length(trim(disposition.follow_up_timeframe)) > 0,
                   length(trim(disposition.next_step_instructions)) > 0,
                   length(trim(disposition.warning_escalation_instructions)) > 0,
                   disposition.communication_method,disposition.communication_completed,
                   disposition.location_callback_reconfirmed,disposition.emergency_instruction_provided,
                   length(trim(coalesce(disposition.emergency_handoff_status,''))) > 0,
                   length(trim(coalesce(disposition.contact_attempt_summary,''))) > 0,
                   prescription.order_id
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            join patients patient on patient.canonical_id=request.patient_id
            left join lateral (
              select version,subjective,objective,assessment,plan from clinical_notes
              where encounter=context.encounter_id order by version desc,id desc limit 1
            ) note on true
            left join lateral (
              select version,disposition_code,adequate_evaluation_completed,follow_up_owner,
                     follow_up_timeframe,next_step_instructions,warning_escalation_instructions,
                     communication_method,communication_completed,location_callback_reconfirmed,
                     emergency_instruction_provided,emergency_handoff_status,contact_attempt_summary
              from telehealth_consultation_disposition_draft_versions
              where consultation_id=context.consultation_id order by version desc limit 1
            ) disposition on true
            left join telehealth_consultation_prescription_orders prescription
              on prescription.consultation_id=context.consultation_id
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
              and not exists(select 1 from encounter_signatures signature
                             where signature.encounter=encounter.encounter and signature.is_lock)
            """ + (lockAggregate
                ? " for update of context,request,reservation,shift,session,appointment,encounter,patient;"
                : ";");
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var documentation = new TelehealthDocumentationPresenceResponse(
            reader.GetInt32(2), reader.GetBoolean(3) || reader.GetBoolean(4) || reader.GetBoolean(5) || reader.GetBoolean(6),
            reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6));
        var disposition = reader.IsDBNull(7) ? null : new TelehealthDispositionPresenceResponse(
            reader.GetInt32(7), reader.GetString(8), reader.GetBoolean(9), reader.GetBoolean(10), reader.GetBoolean(11),
            reader.GetBoolean(12), reader.GetBoolean(13), reader.GetString(14), reader.GetBoolean(15), reader.GetBoolean(16),
            reader.GetBoolean(17), reader.GetBoolean(18), reader.GetBoolean(19));
        return new Source(reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), documentation, disposition,
            reader.IsDBNull(20) ? null : reader.GetGuid(20))
        { ConsultationId = consultationId };
    }

    private static async Task<TelehealthFinalClinicalReviewResponse?> ReadCurrentReviewAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Source source, CancellationToken cancellationToken)
    {
        if (source.Documentation.Version < 1 || source.Disposition is null) return null;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ReviewSelect + """
            where review.consultation_id=@consultationId and review.documentation_version=@documentationVersion
              and review.disposition_version=@dispositionVersion
              and review.prescription_order_id is not distinct from @prescriptionOrderId
            order by review.version desc limit 1;
            """;
        command.Parameters.AddWithValue("consultationId", source.ConsultationId);
        command.Parameters.AddWithValue("documentationVersion", source.Documentation.Version);
        command.Parameters.AddWithValue("dispositionVersion", source.Disposition.Version);
        command.Parameters.Add("prescriptionOrderId", NpgsqlDbType.Uuid).Value = source.PrescriptionOrderId is { } orderId ? orderId : DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadReview(reader) : null;
    }

    private static async Task<Replay?> ReadReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid consultationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select event.command_fingerprint,review.final_clinical_review_version_id,review.version,
                   review.documentation_version,review.disposition_version,review.prescription_order_id,
                   review.reviewed_at,review.content_hash,review.legal_effect,review.encounter_signature_created,
                   review.completion_created,review.patient_delivery_created,review.billing_created,
                   review.claim_created,review.external_destination_contacted
            from telehealth_consultation_final_clinical_review_events event
            join telehealth_consultation_final_clinical_review_versions review
              on review.final_clinical_review_version_id=event.final_clinical_review_version_id
            where event.consultation_id=@consultationId and event.idempotency_key=@idempotencyKey limit 1;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Replay(reader.GetString(0), ReadReview(reader, 1)) : null;
    }

    private static async Task<int> ReadNextVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid consultationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(max(version),0)+1 from telehealth_consultation_final_clinical_review_versions where consultation_id=@consultationId;";
        command.Parameters.AddWithValue("consultationId", consultationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static void ValidateSource(Source source, RecordTelehealthFinalClinicalReviewRequest request)
    {
        if (source.Documentation.Version < 1 || !source.Documentation.SubjectivePresent || !source.Documentation.ObjectivePresent
            || !source.Documentation.AssessmentPresent || !source.Documentation.PlanPresent || source.Disposition is null)
        {
            throw new ArgumentException("A current SOAP draft with all sections and a current safety-disposition draft are required before recording final clinical-review evidence.");
        }
        if (request.ExpectedDocumentationVersion != source.Documentation.Version || request.ExpectedDispositionVersion != source.Disposition.Version)
        {
            throw new TelehealthFinalClinicalReviewConflictException("The documentation or safety-disposition draft changed. Reload before recording final clinical-review evidence.");
        }
    }

    private static TelehealthFinalClinicalReviewWorkspaceResponse ToWorkspace(Source source, TelehealthFinalClinicalReviewResponse? review) => new(
        source.ConsultationId, source.DatabaseNow, source.Documentation, source.Disposition, source.PrescriptionOrderId, review,
        ReviewEnabled: source.Documentation.Version > 0 && source.Documentation.SubjectivePresent && source.Documentation.ObjectivePresent
            && source.Documentation.AssessmentPresent && source.Documentation.PlanPresent && source.Disposition is not null,
        EncounterSignatureEnabled: false, CompletionEnabled: false, ClaimCreationEnabled: false, ClaimSubmissionEnabled: false,
        Limitations:
        [
            "This record is physician-authored synthetic review evidence only; it does not establish clinical adequacy, diagnosis, treatment, or legal finalization.",
            "A recorded review is not an encounter signature, after-visit summary, delivery, billing record, claim, or indication of payer payment.",
            "Signing, completion, clinician release, patient delivery, billing, claims, integrations, and external actions remain unavailable."
        ]);

    private const string ReviewSelect = """
        select review.final_clinical_review_version_id,review.version,review.documentation_version,review.disposition_version,
               review.prescription_order_id,review.reviewed_at,review.content_hash,review.legal_effect,
               review.encounter_signature_created,review.completion_created,review.patient_delivery_created,
               review.billing_created,review.claim_created,review.external_destination_contacted
        from telehealth_consultation_final_clinical_review_versions review
        """;

    private static TelehealthFinalClinicalReviewResponse ReadReview(System.Data.Common.DbDataReader reader, int offset = 0) => new(
        reader.GetGuid(offset), reader.GetInt32(offset + 1), reader.GetInt32(offset + 2), reader.GetInt32(offset + 3),
        reader.IsDBNull(offset + 4) ? null : reader.GetGuid(offset + 4), reader.GetFieldValue<DateTimeOffset>(offset + 5),
        reader.GetString(offset + 6), reader.GetBoolean(offset + 7), reader.GetBoolean(offset + 8), reader.GetBoolean(offset + 9),
        reader.GetBoolean(offset + 10), reader.GetBoolean(offset + 11), reader.GetBoolean(offset + 12), reader.GetBoolean(offset + 13));

    private static string CreateHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record Source(int EncounterId, DateTimeOffset DatabaseNow,
        TelehealthDocumentationPresenceResponse Documentation, TelehealthDispositionPresenceResponse? Disposition,
        Guid? PrescriptionOrderId)
    {
        public Guid ConsultationId { get; init; }
    }
    private sealed record Replay(string CommandFingerprint, TelehealthFinalClinicalReviewResponse Review);
}

public sealed class TelehealthFinalClinicalReviewConflictException(string message) : Exception(message);
