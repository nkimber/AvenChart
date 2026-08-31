// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

/// <summary>Physician-only structural receipt for the non-transmitting professional-claim adapter seam.</summary>
public sealed class TelehealthProfessionalClaimPreparationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProfessionalClaimPreparationWorkspaceResponse?> GetWorkspaceAsync(string practiceId, int facilityId, int physicianStaffId, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select now(),note.version,disposition.version,review.version,
                   exists(select 1 from encounter_signatures signature where signature.encounter=context.encounter_id and signature.is_lock),
                   preparation.claim_preparation_id,preparation.prepared_at,preparation.documentation_version,preparation.disposition_version,preparation.final_clinical_review_version,
                   preparation.adapter_mode,preparation.adapter_name,preparation.target_standard,preparation.claim_state,preparation.correlation_reference,
                   preparation.transaction_created,preparation.external_destination_contacted,preparation.submission_accepted
            from telehealth_consultation_contexts context join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join appointments appointment on appointment.id=context.appointment_id join encounters encounter on encounter.encounter=context.encounter_id
            left join lateral (select version from clinical_notes where encounter=context.encounter_id order by version desc,id desc limit 1) note on true
            left join lateral (select version from telehealth_consultation_disposition_draft_versions where consultation_id=context.consultation_id order by version desc limit 1) disposition on true
            left join telehealth_consultation_prescription_orders prescription on prescription.consultation_id=context.consultation_id
            left join lateral (select version from telehealth_consultation_final_clinical_review_versions candidate where candidate.consultation_id=context.consultation_id and candidate.documentation_version=note.version and candidate.disposition_version=disposition.version and candidate.prescription_order_id is not distinct from prescription.order_id order by version desc limit 1) review on true
            left join telehealth_professional_claim_preparations preparation on preparation.consultation_id=context.consultation_id
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp' and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp' and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null;
        int? documentationVersion = reader.IsDBNull(1) ? null : reader.GetInt32(1); int? dispositionVersion = reader.IsDBNull(2) ? null : reader.GetInt32(2); int? reviewVersion = reader.IsDBNull(3) ? null : reader.GetInt32(3);
        var reviewed = reviewVersion is not null; var locked = reader.GetBoolean(4); var current = reader.IsDBNull(5) ? null : ReadPreparation(reader, 5, consultationId);
        var blockers = new List<string>();
        if (!reviewed) blockers.Add("A current source-bound synthetic final clinical-review record is required before preparation.");
        if (!locked) blockers.Add("A governed synthetic encounter lock is required before preparation.");
        blockers.Add("No physician-confirmed coding evidence (diagnosis, service, modifiers, and rule versions) is recorded.");
        blockers.Add("No billing-provider, payer/product, fee-schedule, or confirmed service-location evidence is recorded.");
        blockers.Add("No human billing approval is recorded. Submission is prohibited.");
        return new(consultationId, reader.GetFieldValue<DateTimeOffset>(0), documentationVersion, dispositionVersion, reviewVersion, reviewed, locked, false, false, false, false,
            SyntheticProfessionalClaimGateway.AdapterMode, SyntheticProfessionalClaimGateway.TargetStandard, reviewed && locked && current is null, false, current, blockers,
            ["A PreparedOnly receipt is structural NON_PRODUCTION evidence, not an ASC X12 transaction or claim payload.", "No clearinghouse, payer, pharmacy, or other external destination is contacted.", "Prepared, submitted, acknowledged, accepted, adjudicated, paid, and patient-billed are separate states."]);
    }

    public async Task<TelehealthProfessionalClaimPreparationResponse?> PrepareAsync(string practiceId, int facilityId, int physicianStaffId, Guid consultationId, PrepareTelehealthProfessionalClaimRequest request, string actorHash, string idempotencyKey, string commandFingerprint, IProfessionalClaimGateway gateway, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, actorHash, idempotencyKey, cancellationToken);
        if (replay is not null) { if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal)) throw new TelehealthProfessionalClaimPreparationConflictException("The idempotency key was already used for a different claim-preparation command."); await transaction.CommitAsync(cancellationToken); return replay.Response; }
        var source = await ReadAndLockSourceAsync(connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, cancellationToken);
        if (source is null) { await transaction.RollbackAsync(cancellationToken); return null; }
        Validate(source, request);
        var id = Guid.NewGuid(); var sourceHash = TelehealthCommandFingerprint.Create("synthetic-professional-claim-packet-v1", consultationId, source.EncounterId, source.DocumentationVersion, source.DispositionVersion, source.FinalReviewVersion);
        var receipt = await gateway.PrepareAsync(new(id, consultationId, source.EncounterId, "telehealth-claim-v1", sourceHash, true), cancellationToken);
        if (!string.Equals(receipt.AdapterMode, SyntheticProfessionalClaimGateway.AdapterMode, StringComparison.Ordinal) || !string.Equals(receipt.TargetStandard, SyntheticProfessionalClaimGateway.TargetStandard, StringComparison.Ordinal) || !string.Equals(receipt.ClaimState, "PreparedOnly", StringComparison.Ordinal) || receipt.TransactionCreated || receipt.ExternalDestinationContacted || receipt.SubmissionAccepted) throw new InvalidOperationException("The synthetic professional-claim gateway returned an unsafe receipt.");
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """insert into telehealth_professional_claim_preparations(claim_preparation_id,consultation_id,encounter_id,practice_id,facility_id,physician_staff_id,documentation_version,disposition_version,final_clinical_review_version,canonical_claim_version,source_evidence_hash,adapter_mode,adapter_name,target_standard,claim_state,correlation_reference,synthetic_data_confirmed,transaction_created,external_destination_contacted,submission_accepted,actor_subject_hash,idempotency_key,command_fingerprint,prepared_at) values(@id,@consultationId,@encounterId,@practiceId,@facilityId,@physician,@documentationVersion,@dispositionVersion,@reviewVersion,'telehealth-claim-v1',@sourceHash,@adapterMode,@adapterName,@targetStandard,@claimState,@correlation,true,false,false,false,@actorHash,@key,@fingerprint,@now);""";
            insert.Parameters.AddWithValue("id", id); insert.Parameters.AddWithValue("consultationId", consultationId); insert.Parameters.AddWithValue("encounterId", source.EncounterId); insert.Parameters.AddWithValue("practiceId", practiceId); insert.Parameters.AddWithValue("facilityId", facilityId); insert.Parameters.AddWithValue("physician", physicianStaffId); insert.Parameters.AddWithValue("documentationVersion", source.DocumentationVersion); insert.Parameters.AddWithValue("dispositionVersion", source.DispositionVersion); insert.Parameters.AddWithValue("reviewVersion", source.FinalReviewVersion); insert.Parameters.AddWithValue("sourceHash", sourceHash); insert.Parameters.AddWithValue("adapterMode", receipt.AdapterMode); insert.Parameters.AddWithValue("adapterName", receipt.AdapterName); insert.Parameters.AddWithValue("targetStandard", receipt.TargetStandard); insert.Parameters.AddWithValue("claimState", receipt.ClaimState); insert.Parameters.AddWithValue("correlation", receipt.CorrelationReference); insert.Parameters.AddWithValue("actorHash", actorHash); insert.Parameters.AddWithValue("key", idempotencyKey); insert.Parameters.AddWithValue("fingerprint", commandFingerprint); insert.Parameters.AddWithValue("now", source.DatabaseNow); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken); return ToResponse(id, consultationId, source.DatabaseNow, source.DocumentationVersion, source.DispositionVersion, source.FinalReviewVersion, receipt);
    }

    private static async Task<Source?> ReadAndLockSourceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string practiceId, int facilityId, int physician, Guid consultationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select context.encounter_id,note.version,disposition.version,review.version,now() from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id join telehealth_video_sessions session on session.session_id=context.session_id join appointments appointment on appointment.id=context.appointment_id join encounters encounter on encounter.encounter=context.encounter_id
            join lateral (select version from clinical_notes where encounter=context.encounter_id order by version desc,id desc limit 1) note on true join lateral (select version from telehealth_consultation_disposition_draft_versions where consultation_id=context.consultation_id order by version desc limit 1) disposition on true left join telehealth_consultation_prescription_orders prescription on prescription.consultation_id=context.consultation_id join lateral (select review.version from telehealth_consultation_final_clinical_review_versions review where review.consultation_id=context.consultation_id and review.documentation_version=note.version and review.disposition_version=disposition.version and review.prescription_order_id is not distinct from prescription.order_id order by review.version desc limit 1) review on true
            where context.consultation_id=@consultationId and context.practice_id=@practiceId and context.facility_id=@facilityId and context.physician_staff_id=@physician and context.status='MediaEnded' and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp' and reservation.clinician_staff_id=@physician and reservation.status='Released' and shift.clinician_staff_id=@physician and shift.status='WrapUp' and session.status='Ended' and appointment.status='>' and encounter.provider_id=@physician and encounter.facility_id=@facilityId and exists(select 1 from encounter_signatures signature where signature.encounter=encounter.encounter and signature.is_lock) and not exists(select 1 from telehealth_professional_claim_preparations preparation where preparation.consultation_id=context.consultation_id) for update of context,request,reservation,shift,session,appointment,encounter;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physician); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetFieldValue<DateTimeOffset>(4)) : null;
    }

    private static async Task<Replay?> ReadReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string practiceId, int facilityId, int physician, Guid consultationId, string actorHash, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = """select command_fingerprint,claim_preparation_id,prepared_at,documentation_version,disposition_version,final_clinical_review_version,adapter_mode,adapter_name,target_standard,claim_state,correlation_reference,transaction_created,external_destination_contacted,submission_accepted from telehealth_professional_claim_preparations where consultation_id=@consultationId and practice_id=@practiceId and facility_id=@facilityId and physician_staff_id=@physician and actor_subject_hash=@actorHash and idempotency_key=@key;""";
        command.Parameters.AddWithValue("consultationId", consultationId); command.Parameters.AddWithValue("practiceId", practiceId); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("physician", physician); command.Parameters.AddWithValue("actorHash", actorHash); command.Parameters.AddWithValue("key", key); await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null;
        var receipt = new TelehealthProfessionalClaimGatewayReceipt(reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetBoolean(11), reader.GetBoolean(12), reader.GetBoolean(13), Limitations()); return new(reader.GetString(0), ToResponse(reader.GetGuid(1), consultationId, reader.GetFieldValue<DateTimeOffset>(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), receipt));
    }

    private static TelehealthProfessionalClaimPreparationResponse ReadPreparation(NpgsqlDataReader reader, int offset, Guid consultationId) { var receipt = new TelehealthProfessionalClaimGatewayReceipt(reader.GetString(offset + 5), reader.GetString(offset + 6), reader.GetString(offset + 7), reader.GetString(offset + 8), reader.GetString(offset + 9), reader.GetBoolean(offset + 10), reader.GetBoolean(offset + 11), reader.GetBoolean(offset + 12), Limitations()); return ToResponse(reader.GetGuid(offset), consultationId, reader.GetFieldValue<DateTimeOffset>(offset + 1), reader.GetInt32(offset + 2), reader.GetInt32(offset + 3), reader.GetInt32(offset + 4), receipt); }
    private static TelehealthProfessionalClaimPreparationResponse ToResponse(Guid id, Guid consultationId, DateTimeOffset preparedAt, int documentationVersion, int dispositionVersion, int reviewVersion, TelehealthProfessionalClaimGatewayReceipt receipt) => new(id, consultationId, preparedAt, documentationVersion, dispositionVersion, reviewVersion, receipt.AdapterMode, receipt.AdapterName, receipt.TargetStandard, receipt.ClaimState, receipt.CorrelationReference, receipt.TransactionCreated, receipt.ExternalDestinationContacted, receipt.SubmissionAccepted, Limitations());
    private static IReadOnlyList<string> Limitations() => ["PreparedOnly is a structural NON_PRODUCTION adapter receipt, not an ASC X12 transaction or claim payload.", "No clearinghouse, payer, pharmacy, or other external destination was contacted.", "No billing record, claim submission, acknowledgment, adjudication, payment, or patient billing was created."];
    private static void Validate(Source source, PrepareTelehealthProfessionalClaimRequest request) { if (!request.SourceEvidenceReviewed || !request.SyntheticOnlyConfirmed || !request.NoSubmissionConfirmed) throw new ArgumentException("Confirm the locked source evidence, synthetic-only effect, and no-submission boundary before preparing the receipt."); if (request.ExpectedDocumentationVersion != source.DocumentationVersion || request.ExpectedDispositionVersion != source.DispositionVersion || request.ExpectedFinalClinicalReviewVersion != source.FinalReviewVersion) throw new TelehealthProfessionalClaimPreparationConflictException("Current source evidence changed. Reload before preparing the synthetic claim receipt."); }
    private sealed record Source(int EncounterId, int DocumentationVersion, int DispositionVersion, int FinalReviewVersion, DateTimeOffset DatabaseNow);
    private sealed record Replay(string CommandFingerprint, TelehealthProfessionalClaimPreparationResponse Response);
}

public sealed class TelehealthProfessionalClaimPreparationConflictException(string message) : Exception(message);
