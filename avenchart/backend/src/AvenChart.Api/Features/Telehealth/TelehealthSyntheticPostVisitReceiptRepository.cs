// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>Reads the minimized, immutable receipt created with synthetic lifecycle closure.</summary>
public sealed class TelehealthSyntheticPostVisitReceiptRepository(NpgsqlDataSource dataSource)
{
    public Task<TelehealthSyntheticPostVisitReceiptResponse?> GetForPatientAsync(
        string practiceId, string patientId, Guid requestId, CancellationToken cancellationToken) =>
        ReadAsync("""
            and request.patient_id=@patientId
            """, command => command.Parameters.AddWithValue("patientId", patientId), practiceId, requestId, cancellationToken);

    public Task<TelehealthSyntheticPostVisitReceiptResponse?> GetForApplicantAsync(
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

    private async Task<TelehealthSyntheticPostVisitReceiptResponse?> ReadAsync(
        string ownerPredicate,
        Action<NpgsqlCommand> addOwnerParameters,
        string practiceId,
        Guid requestId,
        CancellationToken cancellationToken,
        int? expectedFacilityId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select receipt.receipt_id,receipt.request_id,receipt.created_at,receipt.receipt_version,
                   receipt.consultation_version,receipt.request_version,receipt.receipt_state,receipt.source_mode,
                   receipt.synthetic_data_confirmed,receipt.appointment_completed,receipt.encounter_completed,
                   receipt.clinical_record_delivered,receipt.prescription_delivered,receipt.billing_created,
                   receipt.claim_created,receipt.notification_sent,receipt.external_destination_contacted,
                   receipt.consultation_id,receipt.encounter_id,receipt.source_evidence_hash
            from telehealth_synthetic_post_visit_receipts receipt
            join telehealth_requests request on request.request_id=receipt.request_id
            join telehealth_consultation_contexts consultation on consultation.consultation_id=receipt.consultation_id
            join appointments appointment on appointment.id=consultation.appointment_id
            join encounters encounter on encounter.encounter=receipt.encounter_id
            where receipt.request_id=@requestId
              and receipt.practice_id=@practiceId
              and receipt.practice_id=request.practice_id and receipt.facility_id=request.facility_id
              and receipt.patient_id=request.patient_id
              and consultation.request_id=request.request_id
              and consultation.encounter_id=receipt.encounter_id
              and consultation.practice_id=receipt.practice_id and consultation.facility_id=receipt.facility_id
              and request.status='Closed' and consultation.status='Closed'
              and consultation.version=receipt.consultation_version and request.version=receipt.request_version
              and appointment.id=consultation.appointment_id and appointment.patient_id=request.patient_id
              and appointment.facility_id=receipt.facility_id and appointment.status='>'
              and encounter.patient_id=request.patient_id and encounter.source_appointment_id=appointment.id
              and encounter.facility_id=receipt.facility_id
              and receipt.source_mode='NON_PRODUCTION' and receipt.synthetic_data_confirmed
              and not receipt.appointment_completed and not receipt.encounter_completed
              and not receipt.clinical_record_delivered and not receipt.prescription_delivered
              and not receipt.billing_created and not receipt.claim_created and not receipt.notification_sent
              and not receipt.external_destination_contacted
              and exists(select 1 from encounter_signatures signature
                         where signature.encounter=receipt.encounter_id and signature.is_lock)
              and exists(select 1 from telehealth_consultation_events event
                         where event.consultation_id=receipt.consultation_id
                           and event.request_id=receipt.request_id
                           and event.aggregate_version=receipt.consultation_version
                           and event.action='synthetic-visit-closed')
              and exists(select 1 from telehealth_request_events event
                         where event.request_id=receipt.request_id
                           and event.aggregate_version=receipt.request_version
                           and event.action='synthetic-visit-closed'
                           and event.to_status='Closed')
              {(expectedFacilityId is null ? string.Empty : "and receipt.facility_id=@facilityId")}
              {ownerPredicate};
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        if (expectedFacilityId is not null) command.Parameters.AddWithValue("facilityId", expectedFacilityId.Value);
        addOwnerParameters(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static TelehealthSyntheticPostVisitReceiptResponse? Read(NpgsqlDataReader reader)
    {
        var sourceHash = TelehealthCommandFingerprint.Create(
            "synthetic-post-visit-receipt-v1", reader.GetGuid(17), reader.GetGuid(1), reader.GetInt32(18), reader.GetInt32(4), reader.GetInt32(5));
        if (!string.Equals(reader.GetString(19), sourceHash, StringComparison.Ordinal)) return null;

        return new(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6), reader.GetString(7), reader.GetBoolean(8),
            reader.GetBoolean(9), reader.GetBoolean(10), reader.GetBoolean(11), reader.GetBoolean(12), reader.GetBoolean(13),
            reader.GetBoolean(14), reader.GetBoolean(15), reader.GetBoolean(16),
            ["This is an immutable NON_PRODUCTION synthetic lifecycle receipt, not an after-visit summary.",
             "The appointment and encounter remain incomplete; no clinical record, prescription, billing item, claim, notification, or external delivery was created.",
             "No clinician, clinical, medication, pharmacy, insurance, billing, or claim information is included."]);
    }
}
