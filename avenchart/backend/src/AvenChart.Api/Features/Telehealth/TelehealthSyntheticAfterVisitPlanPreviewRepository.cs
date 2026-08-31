// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>Reads the immutable, physician-authored synthetic after-visit plan preview.</summary>
public sealed class TelehealthSyntheticAfterVisitPlanPreviewRepository(NpgsqlDataSource dataSource)
{
    public Task<TelehealthSyntheticAfterVisitPlanPreviewResponse?> GetForPatientAsync(
        string practiceId, string patientId, Guid requestId, CancellationToken cancellationToken) =>
        ReadAsync("and request.patient_id=@patientId", command => command.Parameters.AddWithValue("patientId", patientId), practiceId, requestId, cancellationToken);

    public Task<TelehealthSyntheticAfterVisitPlanPreviewResponse?> GetForApplicantAsync(
        string practiceId, int facilityId, Guid applicantId, string accessKeyHash, Guid requestId, CancellationToken cancellationToken) =>
        ReadAsync("""
            and request.source_applicant_id=@applicantId
            and exists(select 1 from telehealth_prospective_applicants applicant
                       where applicant.applicant_id=@applicantId
                         and applicant.practice_id=request.practice_id
                         and applicant.facility_id=request.facility_id
                         and applicant.access_key_hash=@accessKeyHash
                         and applicant.expires_at>now())
            """, command =>
        {
            command.Parameters.AddWithValue("applicantId", applicantId);
            command.Parameters.AddWithValue("accessKeyHash", accessKeyHash);
        }, practiceId, requestId, cancellationToken, facilityId);

    private async Task<TelehealthSyntheticAfterVisitPlanPreviewResponse?> ReadAsync(
        string ownerPredicate, Action<NpgsqlCommand> addOwnerParameters, string practiceId, Guid requestId,
        CancellationToken cancellationToken, int? expectedFacilityId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select preview.preview_id,preview.request_id,preview.created_at,preview.preview_version,
                   preview.consultation_version,preview.request_version,preview.disposition_version,
                   preview.final_clinical_review_version,preview.preview_state,preview.source_mode,
                   preview.synthetic_data_confirmed,preview.disposition_code,preview.follow_up_owner,
                   preview.follow_up_timeframe,preview.next_step_instructions,preview.warning_escalation_instructions,
                   preview.communication_method,preview.communication_completed,preview.appointment_completed,
                   preview.encounter_completed,preview.avs_delivered,preview.notification_sent,
                   preview.external_destination_contacted,preview.consultation_id,preview.encounter_id,
                   preview.source_evidence_hash
            from telehealth_synthetic_after_visit_plan_previews preview
            join telehealth_requests request on request.request_id=preview.request_id
            join telehealth_consultation_contexts consultation on consultation.consultation_id=preview.consultation_id
            join appointments appointment on appointment.id=consultation.appointment_id
            join encounters encounter on encounter.encounter=preview.encounter_id
            join telehealth_consultation_disposition_draft_versions disposition
              on disposition.consultation_id=preview.consultation_id and disposition.version=preview.disposition_version
            join telehealth_consultation_final_clinical_review_versions review
              on review.consultation_id=preview.consultation_id and review.version=preview.final_clinical_review_version
             and review.disposition_version=preview.disposition_version
            where preview.request_id=@requestId and preview.practice_id=@practiceId
              and preview.practice_id=request.practice_id and preview.facility_id=request.facility_id
              and preview.patient_id=request.patient_id and consultation.request_id=request.request_id
              and consultation.encounter_id=preview.encounter_id and consultation.practice_id=preview.practice_id
              and consultation.facility_id=preview.facility_id and request.status='Closed' and consultation.status='Closed'
              and consultation.version=preview.consultation_version and request.version=preview.request_version
              and appointment.patient_id=request.patient_id and appointment.facility_id=preview.facility_id and appointment.status='>'
              and encounter.patient_id=request.patient_id and encounter.source_appointment_id=appointment.id and encounter.facility_id=preview.facility_id
              and preview.source_mode='NON_PRODUCTION' and preview.synthetic_data_confirmed
              and not preview.appointment_completed and not preview.encounter_completed and not preview.avs_delivered
              and not preview.notification_sent and not preview.external_destination_contacted
              and review.synthetic_data_confirmed and review.no_automatic_claim_or_delivery_confirmed
              and preview.disposition_code=disposition.disposition_code and preview.follow_up_owner=disposition.follow_up_owner
              and preview.follow_up_timeframe=disposition.follow_up_timeframe and preview.next_step_instructions=disposition.next_step_instructions
              and preview.warning_escalation_instructions=disposition.warning_escalation_instructions
              and preview.communication_method=disposition.communication_method and preview.communication_completed=disposition.communication_completed
              and exists(select 1 from encounter_signatures signature where signature.encounter=preview.encounter_id and signature.is_lock)
              and exists(select 1 from telehealth_consultation_events event where event.consultation_id=preview.consultation_id and event.request_id=preview.request_id and event.aggregate_version=preview.consultation_version and event.action='synthetic-visit-closed')
              and exists(select 1 from telehealth_request_events event where event.request_id=preview.request_id and event.aggregate_version=preview.request_version and event.action='synthetic-visit-closed' and event.to_status='Closed')
              {(expectedFacilityId is null ? string.Empty : "and preview.facility_id=@facilityId")}
              {ownerPredicate};
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        if (expectedFacilityId is not null) command.Parameters.AddWithValue("facilityId", expectedFacilityId.Value);
        addOwnerParameters(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static TelehealthSyntheticAfterVisitPlanPreviewResponse? Read(NpgsqlDataReader reader)
    {
        var sourceHash = TelehealthCommandFingerprint.Create("synthetic-after-visit-plan-preview-v1",
            reader.GetGuid(23), reader.GetGuid(1), reader.GetInt32(24), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7));
        if (!string.Equals(reader.GetString(25), sourceHash, StringComparison.Ordinal)) return null;

        return new(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetString(8), reader.GetString(9),
            reader.GetBoolean(10), reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetString(14), reader.GetString(15),
            reader.GetString(16), reader.GetBoolean(17), reader.GetBoolean(18), reader.GetBoolean(19), reader.GetBoolean(20), reader.GetBoolean(21), reader.GetBoolean(22),
            ["This is an immutable NON_PRODUCTION synthetic plan preview, not medical advice or a delivered after-visit summary.",
             "The appointment and encounter remain incomplete; this preview creates no completed clinical record, notification, delivery, billing, claim, or external action.",
             "Use a practice-approved, clinically governed workflow for real patient instructions, an after-visit summary, or urgent care guidance."]);
    }
}
