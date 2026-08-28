// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthCompletionReviewRepository(NpgsqlDataSource dataSource)
{
    private static readonly string[] PermanentProductBlockers =
    [
        "FINAL_CLINICAL_REVIEW_NOT_RECORDED",
        "SIGNATURE_FINALIZATION_NOT_IMPLEMENTED",
        "ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED"
    ];

    public async Task<TelehealthCompletionPrerequisitesResponse?> GetAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.status,request.status,shift.status,appointment.status,now(),
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
                   disposition.location_callback_reconfirmed,
                   disposition.emergency_instruction_provided,
                   length(trim(coalesce(disposition.emergency_handoff_status,''))) > 0,
                   length(trim(coalesce(disposition.contact_attempt_summary,''))) > 0,
                   pharmacy.version,pharmacy.patient_choice_confirmed
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            join patients patient on patient.canonical_id=request.patient_id
            left join lateral (
              select version,subjective,objective,assessment,plan
              from clinical_notes
              where encounter=context.encounter_id
              order by version desc,id desc limit 1
            ) note on true
            left join lateral (
              select version,disposition_code,adequate_evaluation_completed,follow_up_owner,
                     follow_up_timeframe,next_step_instructions,warning_escalation_instructions,
                     communication_method,communication_completed,location_callback_reconfirmed,
                     emergency_instruction_provided,emergency_handoff_status,contact_attempt_summary
              from telehealth_consultation_disposition_draft_versions
              where consultation_id=context.consultation_id
              order by version desc limit 1
            ) disposition on true
            left join lateral (
              select version,patient_choice_confirmed
              from telehealth_consultation_pharmacy_choice_versions
              where consultation_id=context.consultation_id
              order by version desc limit 1
            ) pharmacy on true
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and not exists(
                select 1 from encounter_signatures signature
                where signature.encounter=encounter.encounter and signature.is_lock)
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years';
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var documentation = new TelehealthDocumentationPresenceResponse(
            reader.GetInt32(5),
            reader.GetBoolean(6) || reader.GetBoolean(7) || reader.GetBoolean(8) || reader.GetBoolean(9),
            reader.GetBoolean(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.GetBoolean(9));
        var disposition = reader.IsDBNull(10)
            ? null
            : new TelehealthDispositionPresenceResponse(
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetBoolean(12),
                reader.GetBoolean(13),
                reader.GetBoolean(14),
                reader.GetBoolean(15),
                reader.GetBoolean(16),
                reader.GetString(17),
                reader.GetBoolean(18),
                reader.GetBoolean(19),
                reader.GetBoolean(20),
                reader.GetBoolean(21),
                reader.GetBoolean(22));
        var pharmacy = reader.IsDBNull(23)
            ? null
            : new TelehealthPharmacyChoicePresenceResponse(reader.GetInt32(23), reader.GetBoolean(24));
        var blockers = new List<string>();
        if (!documentation.HasAnyContent)
        {
            blockers.Add("DOCUMENTATION_DRAFT_MISSING");
        }
        if (disposition is null)
        {
            blockers.Add("SAFETY_DISPOSITION_DRAFT_MISSING");
        }
        blockers.AddRange(PermanentProductBlockers);

        var response = new TelehealthCompletionPrerequisitesResponse(
            consultationId,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            documentation,
            disposition,
            pharmacy,
            documentation.HasAnyContent && disposition is not null,
            blockers,
            SigningEnabled: false,
            CompletionEnabled: false,
            PatientDeliveryEnabled: false,
            DownstreamCreationEnabled: false,
            Limitations:
            [
                "Field presence is structural evidence only; it does not establish clinical adequacy, accuracy, applicability, or readiness to sign.",
                "A pharmacy destination is optional and its presence does not imply that a medication or prescription exists.",
                "Signing, finalization, delivery, lifecycle completion, prescriptions, claims, referrals, orders, tasks, messages, outbox work, and external handoffs remain unavailable."
            ]);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
