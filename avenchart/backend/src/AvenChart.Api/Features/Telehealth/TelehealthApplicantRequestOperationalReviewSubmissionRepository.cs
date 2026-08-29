// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestOperationalReviewSubmissionRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string SubmissionSnapshotFingerprint,
    DateTimeOffset ResultValidThrough,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    string CandidateDisplayName,
    string CandidateNpiLast4,
    string ServiceCategory,
    string Modality,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestOperationalReviewSubmissionSource(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? RequestTriageOutcome,
    string CanonicalPatientId,
    Guid ParticipationEvaluationId,
    string EvaluationSnapshotFingerprint,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset ResultValidThrough,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    int CandidateStaffId,
    string CandidateDisplayName,
    string CandidateNpiLast4,
    string CurrentCandidateDisplayName,
    string CurrentCandidateNpi,
    string CandidateRole,
    int? CandidateFacilityId,
    bool CandidateActive,
    string ServiceCategory,
    string Modality,
    bool ExactEvidenceChain,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    int SubmissionCount,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestOperationalReviewSubmissionRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestOperationalReviewSubmissionRecord> GetAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var source = await LoadSourceAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(source, accessKeyHash);
        RequireApplicant(source);

        var completed = await LoadResultAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompleted(source, completed.Value.Record, practiceId, facilityId);
            return completed.Value.Record;
        }

        RequireReady(source);
        return CreateRecord(source, practiceId, facilityId, null);
    }

    public async Task<TelehealthApplicantRequestOperationalReviewSubmissionRecord> SubmitAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestOperationalReviewSubmissionCommand command,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var source = await LoadSourceAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(source, accessKeyHash);
        RequireApplicant(source);

        var replay = await LoadResultAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            RequireCompleted(source, replay.Value.Record, practiceId, facilityId);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireReady(source);
        if (command.ExpectedRequestVersion != source.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_operational_review_submission_version_conflict",
                "The request changed before submission. Reload and try again.");
        }

        var snapshot = CreateSnapshot(source, practiceId, facilityId);
        if (!string.Equals(snapshot, command.SubmissionSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_operational_review_submission_snapshot_stale",
                "The operational-review submission changed. Reload and try again.");
        }

        DateTimeOffset submittedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_operational_review_submissions(
                  submission_id,request_id,applicant_id,participation_evaluation_id,
                  practice_id,facility_id,canonical_patient_id,applicant_version,
                  source_request_version,resulting_request_version,source_request_status,
                  resulting_request_status,submission_snapshot_fingerprint,
                  evaluation_snapshot_fingerprint,practice_display_name,payer_display_name,
                  product_display_name,current_location_state_code,purpose_category,date_of_service,
                  candidate_staff_id,candidate_display_name,candidate_npi_last4,service_category,
                  modality,evaluated_at,result_valid_through,applicant_expires_at,source_mode,
                  compatibility_target,business_outcome,synthetic_evidence_acknowledged,
                  no_coverage_guarantee_acknowledged,practice_review_pending_acknowledged,
                  no_care_relationship_acknowledged,policy_key,policy_version,evidence_type,
                  idempotency_key,command_fingerprint,submitted_at)
                values(
                  @submissionId,@requestId,@applicantId,@evaluationId,@practiceId,@facilityId,
                  @patientId,26,11,12,'Verification','OperationalReview',@snapshot,
                  @evaluationSnapshot,@practiceDisplay,@payer,@product,@state,@purpose,@dateOfService,
                  @candidateStaffId,@candidateDisplay,@npiLast4,@serviceCategory,@modality,
                  @evaluatedAt,@resultValidThrough,@applicantExpiresAt,@sourceMode,
                  @compatibilityTarget,@businessOutcome,true,true,true,true,@policyKey,1,
                  @evidenceType,@idempotencyKey,@commandFingerprint,now())
                returning submitted_at;
                """;
            insert.Parameters.AddWithValue("submissionId", Guid.NewGuid());
            insert.Parameters.AddWithValue("requestId", source.RequestId);
            insert.Parameters.AddWithValue("applicantId", source.ApplicantId);
            insert.Parameters.AddWithValue("evaluationId", source.ParticipationEvaluationId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", source.CanonicalPatientId);
            insert.Parameters.AddWithValue("snapshot", snapshot);
            insert.Parameters.AddWithValue("evaluationSnapshot", source.EvaluationSnapshotFingerprint);
            insert.Parameters.AddWithValue("practiceDisplay", source.PracticeDisplayName);
            insert.Parameters.AddWithValue("payer", source.PayerDisplayName);
            insert.Parameters.AddWithValue("product", source.ProductDisplayName);
            insert.Parameters.AddWithValue("state", source.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purpose", source.PurposeCategory);
            insert.Parameters.AddWithValue("dateOfService", source.DateOfService);
            insert.Parameters.AddWithValue("candidateStaffId", source.CandidateStaffId);
            insert.Parameters.AddWithValue("candidateDisplay", source.CandidateDisplayName);
            insert.Parameters.AddWithValue("npiLast4", source.CandidateNpiLast4);
            insert.Parameters.AddWithValue("serviceCategory", source.ServiceCategory);
            insert.Parameters.AddWithValue("modality", source.Modality);
            insert.Parameters.AddWithValue("evaluatedAt", source.EvaluatedAt);
            insert.Parameters.AddWithValue("resultValidThrough", source.ResultValidThrough);
            insert.Parameters.AddWithValue("applicantExpiresAt", source.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("sourceMode", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.SourceMode);
            insert.Parameters.AddWithValue("compatibilityTarget", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.CompatibilityTarget);
            insert.Parameters.AddWithValue("businessOutcome", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.BusinessOutcome);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.PolicyKey);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var value = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Operational-review submission time is unavailable.");
            submittedAt = value switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException("Operational-review submission time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                   set status='OperationalReview',version=12,updated_at=now()
                 where request_id=@requestId and status='Verification' and version=11;
                """;
            update.Parameters.AddWithValue("requestId", source.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_operational_review_submission_version_conflict",
                    "The request changed before submission. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, source.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(source, practiceId, facilityId, submittedAt);
    }

    private static TelehealthApplicantRequestOperationalReviewSubmissionRecord CreateRecord(
        TelehealthApplicantRequestOperationalReviewSubmissionSource source,
        string practiceId,
        int facilityId,
        DateTimeOffset? submittedAt) => new(
            source.ApplicantId,
            source.ApplicantVersion,
            source.ApplicantStatus,
            source.RequestId,
            submittedAt is null
                ? TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestVersion
                : TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestVersion,
            submittedAt is null
                ? TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestStatus
                : TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestStatus,
            CreateSnapshot(source, practiceId, facilityId),
            source.ResultValidThrough,
            source.PracticeDisplayName,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.DateOfService,
            source.CandidateDisplayName,
            source.CandidateNpiLast4,
            source.ServiceCategory,
            source.Modality,
            submittedAt,
            source.DatabaseNow);

    private static string CreateSnapshot(
        TelehealthApplicantRequestOperationalReviewSubmissionSource source,
        string practiceId,
        int facilityId) =>
        TelehealthApplicantRequestOperationalReviewSubmissionPolicy.SnapshotFingerprint(
            source.ApplicantId,
            source.RequestId,
            source.ParticipationEvaluationId,
            TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestVersion,
            practiceId,
            facilityId,
            source.CanonicalPatientId,
            source.PracticeDisplayName,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.DateOfService,
            source.CandidateStaffId,
            source.CandidateDisplayName,
            source.CandidateNpiLast4,
            source.ServiceCategory,
            source.Modality,
            source.EvaluationSnapshotFingerprint,
            source.EvaluatedAt,
            source.ResultValidThrough);

    private static async Task<TelehealthApplicantRequestOperationalReviewSubmissionSource?> LoadSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),
                   r.request_id,r.version,r.status,r.triage_outcome,r.patient_id,
                   e.evaluation_id,e.evaluation_snapshot_fingerprint,e.evaluated_at,
                   e.result_valid_through,e.practice_display_name,e.payer_display_name,
                   e.product_display_name,e.current_location_state_code,e.purpose_category,
                   e.date_of_service,e.candidate_staff_id,e.candidate_display_name,
                   e.candidate_npi_last4,trim(concat(candidate.first_name,' ',candidate.last_name)),
                   candidate.npi,candidate.role,candidate.facility_id,candidate.active,
                   e.service_category,e.modality,
                   ((select count(*) from telehealth_applicant_request_creations x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_location_confirmations x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_universal_safety_assessments x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_complaint_triage_assessments x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_intake_snapshots x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_insurance_source_confirmations x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_eligibility_verifications x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_practice_network_verifications x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_rendering_candidate_selections x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_participation_contexts x where x.request_id=r.request_id)=1
                    and (select count(*) from telehealth_applicant_request_participation_evaluations x where x.request_id=r.request_id)=1
                    and e.policy_key='SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
                    and e.policy_version=1 and e.evidence_type='APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
                    and e.source_request_version=10 and e.resulting_request_version=11
                    and e.source_request_status='Verification' and e.resulting_request_status='Verification'
                    and e.source_mode='NON_PRODUCTION'
                    and e.compatibility_target='HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
                    and e.business_outcome='SyntheticExactParticipationMatched'
                    and e.synthetic_data_confirmed and e.exact_tuple_scope_acknowledged
                    and e.no_coverage_guarantee_acknowledged
                    and e.real_verification_still_required_acknowledged
                    and e.synthetic_participation_evaluated and e.synthetic_billing_entity_in_network
                    and e.synthetic_rendering_provider_in_network and e.synthetic_plan_network_matched
                    and e.synthetic_service_location_matched and e.synthetic_new_patients_accepted
                    and e.synthetic_exact_network_matched and not e.real_state_authority_verified
                    and not e.real_credentialing_verified and not e.rendering_physician_assigned
                    and not e.rendering_physician_network_checked and not e.exact_network_confirmed
                    and not e.canonical_coverage_created and not e.generic_coverage_selected
                    and not e.coverage_verified and not e.operational_review_created
                    and not e.practice_accepted and not e.patient_contacted
                    and not e.patient_care_queue_entered and not e.clinician_queue_entered
                    and not e.appointment_created and not e.encounter_created
                    and not e.consent_created and not e.care_authorized
                    and not e.integration_enabled and not e.external_call_performed),
                   (select count(*) from insurance_records x where lower(x.patient_id)=lower(r.patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (select count(*) from telehealth_applicant_request_operational_review_submissions x where x.request_id=r.request_id),
                   r.appointment_id is not null
              from telehealth_prospective_applicants a
              join telehealth_requests r on r.source_applicant_id=a.applicant_id
               and r.practice_id=a.practice_id and r.facility_id=a.facility_id
              join telehealth_applicant_request_participation_evaluations e
                on e.request_id=r.request_id and e.applicant_id=a.applicant_id
               and e.canonical_patient_id=r.patient_id
              join patients patient on patient.canonical_id=r.patient_id
               and patient.facility_id=a.facility_id and patient.lifecycle_status='active'
               and not patient.portal_enabled and patient.merged_into_patient_id is null
              join staff candidate on candidate.id=e.candidate_staff_id
             where a.applicant_id=@applicantId and a.practice_id=@practiceId
               and a.facility_id=@facilityId and a.status='SyntheticRequestCreated' and a.version=26
               and r.status in ('Verification','OperationalReview') and r.version in (11,12)
               and r.ready_at is null
            {(forUpdate ? "for update of a,r,patient,candidate" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5), reader.GetGuid(6),
                Convert.ToInt32(reader.GetInt64(7)), reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10),
                reader.GetGuid(11), reader.GetString(12), reader.GetFieldValue<DateTimeOffset>(13),
                reader.GetFieldValue<DateTimeOffset>(14), reader.GetString(15), reader.GetString(16),
                reader.GetString(17), reader.GetString(18), reader.GetString(19),
                reader.GetFieldValue<DateOnly>(20), reader.GetInt32(21), reader.GetString(22),
                reader.GetString(23), reader.GetString(24), reader.GetString(25), reader.GetString(26),
                reader.IsDBNull(27) ? null : reader.GetInt32(27), reader.GetBoolean(28),
                reader.GetString(29), reader.GetString(30), reader.GetBoolean(31),
                Convert.ToInt32(reader.GetInt64(32)), Convert.ToInt32(reader.GetInt64(33)),
                Convert.ToInt32(reader.GetInt64(34)), reader.GetBoolean(35))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestOperationalReviewSubmissionRecord Record,
        string CommandFingerprint)?> LoadResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select s.applicant_id,s.applicant_version,a.status,s.request_id,
                   s.resulting_request_version,s.resulting_request_status,
                   s.submission_snapshot_fingerprint,s.result_valid_through,
                   s.practice_display_name,s.payer_display_name,s.product_display_name,
                   s.current_location_state_code,s.purpose_category,s.date_of_service,
                   s.candidate_display_name,s.candidate_npi_last4,s.service_category,s.modality,
                   s.submitted_at,now(),s.command_fingerprint
              from telehealth_applicant_request_operational_review_submissions s
              join telehealth_prospective_applicants a on a.applicant_id=s.applicant_id
             where s.applicant_id=@applicantId and s.practice_id=@practiceId
               and s.facility_id=@facilityId
               {(idempotencyKey is null ? string.Empty : "and s.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null) command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetFieldValue<DateOnly>(13), reader.GetString(14), reader.GetString(15),
                reader.GetString(16), reader.GetString(17), reader.GetFieldValue<DateTimeOffset>(18),
                reader.GetFieldValue<DateTimeOffset>(19)),
            reader.GetString(20));
    }

    private static async Task InsertRequestEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        Guid applicantId,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_request_events(
              event_id,request_id,aggregate_version,action,from_status,to_status,
              actor_type,actor_id,idempotency_key,command_fingerprint)
            values(@eventId,@requestId,12,'applicant-operational-review-submitted',
                   'Verification','OperationalReview','applicant',@actorId,@idempotencyKey,@fingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestOperationalReviewSubmissionSource source,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(source.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestOperationalReviewSubmissionSource source)
    {
        if (source.ApplicantExpiresAt <= source.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (source.ApplicantStatus != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ApplicantStatus
            || source.ApplicantVersion != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_operational_review_submission_state_conflict",
                "The applicant is not eligible for operational-review submission.");
        }
    }

    private static void RequireReady(TelehealthApplicantRequestOperationalReviewSubmissionSource source)
    {
        if (source.RequestStatus != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestStatus
            || source.RequestVersion != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.EntryRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.EvaluatedAt > source.DatabaseNow || source.ResultValidThrough <= source.DatabaseNow
            || source.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || source.PurposeCategory is not ("migraine" or "sleep")
            || source.CurrentCandidateDisplayName != source.CandidateDisplayName
            || !source.CurrentCandidateNpi.EndsWith(source.CandidateNpiLast4, StringComparison.Ordinal)
            || source.CandidateRole != "provider" || source.CandidateFacilityId != 10
            || !source.CandidateActive || !source.ExactEvidenceChain
            || source.CanonicalInsuranceCount != 0 || source.DownstreamCount != 0
            || source.SubmissionCount != 0 || source.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompleted(
        TelehealthApplicantRequestOperationalReviewSubmissionSource source,
        TelehealthApplicantRequestOperationalReviewSubmissionRecord record,
        string practiceId,
        int facilityId)
    {
        if (source.RequestStatus != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestStatus
            || source.RequestVersion != TelehealthApplicantRequestOperationalReviewSubmissionPolicy.ResultingRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.ResultValidThrough <= source.DatabaseNow || source.SubmissionCount != 1
            || !source.ExactEvidenceChain || source.CanonicalInsuranceCount != 0
            || source.DownstreamCount != 0 || source.AppointmentCreated
            || source.CurrentCandidateDisplayName != source.CandidateDisplayName
            || !source.CurrentCandidateNpi.EndsWith(source.CandidateNpiLast4, StringComparison.Ordinal)
            || source.CandidateRole != "provider" || source.CandidateFacilityId != 10
            || !source.CandidateActive || record.SubmittedAt is null
            || record.SubmittedAt > source.DatabaseNow
            || record.SubmissionSnapshotFingerprint != CreateSnapshot(source, practiceId, facilityId)
            || record.ApplicantId != source.ApplicantId || record.RequestId != source.RequestId
            || record.PracticeDisplayName != source.PracticeDisplayName
            || record.PayerDisplayName != source.PayerDisplayName
            || record.ProductDisplayName != source.ProductDisplayName
            || record.CurrentLocationStateCode != source.CurrentLocationStateCode
            || record.PurposeCategory != source.PurposeCategory
            || record.DateOfService != source.DateOfService
            || record.CandidateDisplayName != source.CandidateDisplayName
            || record.CandidateNpiLast4 != source.CandidateNpiLast4
            || record.ServiceCategory != source.ServiceCategory || record.Modality != source.Modality)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_operational_review_submission_idempotency_conflict",
                "The operational-review submission idempotency key was already used with different content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_operational_review_submission_provenance_conflict",
        "The synthetic evidence is unavailable, expired, or changed.");
}
