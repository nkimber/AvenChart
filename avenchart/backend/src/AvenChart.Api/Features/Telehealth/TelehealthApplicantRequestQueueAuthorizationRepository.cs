// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestQueueAuthorizationRecord(
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string AuthorizationSnapshotFingerprint,
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
    DateTimeOffset? AuthorizedAt);

internal sealed record TelehealthApplicantRequestQueueAuthorizationSource(
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? TriageOutcome,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    DateTimeOffset ApplicantExpiresAt,
    string CanonicalPatientId,
    bool PortalEnabled,
    Guid SubmissionId,
    string SubmissionSnapshotFingerprint,
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
    DateTimeOffset SubmittedAt,
    DateTimeOffset ResultValidThrough,
    DateTimeOffset DatabaseNow,
    bool SubmissionEvidenceValid,
    bool ExactEvidenceChain,
    int CanonicalInsuranceCount,
    int AuthorizationCount,
    int QueueCount,
    bool QueueReady,
    int DownstreamCount,
    bool AppointmentCreated,
    bool AppointmentUnassigned);

internal sealed record TelehealthApplicantRequestQueueAuthorizationReplay(
    string AuthorizationSnapshotFingerprint,
    string ActorId,
    string CommandFingerprint,
    DateTimeOffset AuthorizedAt);

public sealed class TelehealthApplicantRequestQueueAuthorizationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestQueueAuthorizationRecord> GetAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorRole,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireCurrentActorAsync(connection, null, facilityId, staffId, actorRole, cancellationToken);
        var source = await LoadSourceAsync(
            connection, null, practiceId, facilityId, requestId, false, cancellationToken)
            ?? throw TelehealthProblem.NotFound();
        var completed = await LoadAuthorizationAsync(
            connection, null, practiceId, facilityId, requestId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompleted(source, completed, practiceId, facilityId);
            return CreateRecord(source, practiceId, facilityId, completed.AuthorizedAt);
        }

        RequireReady(source);
        return CreateRecord(source, practiceId, facilityId, null);
    }

    public async Task<TelehealthApplicantRequestQueueAuthorizationRecord> AuthorizeAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        string actorRole,
        Guid requestId,
        NormalizedTelehealthApplicantRequestQueueAuthorizationCommand command,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RequireCurrentActorAsync(connection, transaction, facilityId, staffId, actorRole, cancellationToken);
        var source = await LoadSourceAsync(
            connection, transaction, practiceId, facilityId, requestId, true, cancellationToken)
            ?? throw TelehealthProblem.NotFound();

        var replay = await LoadAuthorizationAsync(
            connection, transaction, practiceId, facilityId, requestId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplay(replay, actorId, commandFingerprint);
            RequireCompleted(source, replay, practiceId, facilityId);
            await transaction.CommitAsync(cancellationToken);
            return CreateRecord(source, practiceId, facilityId, replay.AuthorizedAt);
        }

        RequireReady(source);
        if (command.ExpectedRequestVersion != source.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_authorization_version_conflict",
                "The operational-review request changed. Reload before continuing.");
        }
        var snapshot = CreateSnapshot(source, practiceId, facilityId);
        if (!string.Equals(snapshot, command.AuthorizationSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_authorization_snapshot_stale",
                "The queue-authorization review changed. Reload before continuing.");
        }

        DateTimeOffset authorizedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_queue_authorizations(
                  authorization_id,request_id,submission_id,applicant_id,practice_id,facility_id,
                  canonical_patient_id,source_request_version,resulting_request_version,
                  source_request_status,resulting_request_status,authorization_snapshot_fingerprint,
                  submission_snapshot_fingerprint,practice_display_name,payer_display_name,
                  product_display_name,current_location_state_code,purpose_category,date_of_service,
                  candidate_staff_id,candidate_display_name,candidate_npi_last4,service_category,
                  modality,operational_review_submitted_at,result_valid_through,source_mode,
                  compatibility_target,business_outcome,synthetic_evidence_reviewed,
                  no_coverage_guarantee_acknowledged,practice_accepts_for_queue_acknowledged,
                  queue_not_care_acknowledged,policy_key,policy_version,evidence_type,
                  decided_by_staff_id,decided_by_actor_id,decided_by_role,idempotency_key,
                  command_fingerprint,authorized_at)
                values(
                  @authorizationId,@requestId,@submissionId,@applicantId,@practiceId,@facilityId,
                  @patientId,12,13,'OperationalReview','Queued',@snapshot,@submissionSnapshot,
                  @practiceDisplay,@payer,@product,@state,@purpose,@dateOfService,@candidateStaffId,
                  @candidateDisplay,@npiLast4,@serviceCategory,@modality,@submittedAt,
                  @resultValidThrough,'NON_PRODUCTION','AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1',
                  'SyntheticRequestAuthorizedToQueue',true,true,true,true,
                  'SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION',1,
                  'APPLICANT_REQUEST_QUEUE_AUTHORIZATION',@staffId,@actorId,@actorRole,
                  @idempotencyKey,@commandFingerprint,now())
                returning authorized_at;
                """;
            insert.Parameters.AddWithValue("authorizationId", Guid.NewGuid());
            insert.Parameters.AddWithValue("requestId", source.RequestId);
            insert.Parameters.AddWithValue("submissionId", source.SubmissionId);
            insert.Parameters.AddWithValue("applicantId", source.ApplicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", source.CanonicalPatientId);
            insert.Parameters.AddWithValue("snapshot", snapshot);
            insert.Parameters.AddWithValue("submissionSnapshot", source.SubmissionSnapshotFingerprint);
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
            insert.Parameters.AddWithValue("submittedAt", source.SubmittedAt);
            insert.Parameters.AddWithValue("resultValidThrough", source.ResultValidThrough);
            insert.Parameters.AddWithValue("staffId", (object?)staffId ?? DBNull.Value);
            insert.Parameters.AddWithValue("actorId", actorId);
            insert.Parameters.AddWithValue("actorRole", actorRole);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var value = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Queue-authorization time is unavailable.");
            authorizedAt = value switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException("Queue-authorization time had an unexpected database type.")
            };
        }

        var appointmentId = $"TH-APPT-{requestId:N}";
        await using (var appointment = connection.CreateCommand())
        {
            appointment.Transaction = transaction;
            appointment.CommandText = """
                insert into appointments(
                  id,patient_id,pid,provider_id,facility_id,billing_location_id,
                  appointment_date,start_time,duration_minutes,category_id,title,status,
                  room,comments,recurrence_type)
                select @appointmentId,p.canonical_id,p.legacy_pid,null,@facilityId,@facilityId,
                       current_date,localtime(0),30,9,'Immediate telehealth','-',null,null,0
                from patients p
                where p.canonical_id=@patientId and p.facility_id=@facilityId
                  and p.portal_enabled=false and p.merged_into_patient_id is null
                  and coalesce(lower(p.lifecycle_status),'active')='active'
                  and p.deceased_date is null;
                """;
            appointment.Parameters.AddWithValue("appointmentId", appointmentId);
            appointment.Parameters.AddWithValue("patientId", source.CanonicalPatientId);
            appointment.Parameters.AddWithValue("facilityId", facilityId);
            if (await appointment.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw ProvenanceConflict();
            }
        }

        var queueEntryId = Guid.NewGuid();
        await using (var transition = connection.CreateCommand())
        {
            transition.Transaction = transaction;
            transition.CommandText = """
                update telehealth_requests
                   set status='Queued',appointment_id=@appointmentId,version=13,
                       ready_at=now(),updated_at=now()
                 where request_id=@requestId and source_applicant_id=@applicantId
                   and status='OperationalReview' and version=12 and appointment_id is null;
                insert into telehealth_queue_entries(
                  queue_entry_id,request_id,practice_id,facility_id,status,ready_at,
                  authorized_by_actor_id)
                values(@queueEntryId,@requestId,@practiceId,@facilityId,'Ready',now(),@actorId);
                """;
            transition.Parameters.AddWithValue("appointmentId", appointmentId);
            transition.Parameters.AddWithValue("requestId", source.RequestId);
            transition.Parameters.AddWithValue("applicantId", source.ApplicantId);
            transition.Parameters.AddWithValue("queueEntryId", queueEntryId);
            transition.Parameters.AddWithValue("practiceId", practiceId);
            transition.Parameters.AddWithValue("facilityId", facilityId);
            transition.Parameters.AddWithValue("actorId", actorId);
            if (await transition.ExecuteNonQueryAsync(cancellationToken) != 2)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_queue_authorization_version_conflict",
                    "The operational-review request changed. Reload before continuing.");
            }
        }

        await using (var requestEvent = connection.CreateCommand())
        {
            requestEvent.Transaction = transaction;
            requestEvent.CommandText = """
                insert into telehealth_request_events(
                  event_id,request_id,aggregate_version,action,from_status,to_status,
                  actor_type,actor_id,idempotency_key,command_fingerprint)
                values(@eventId,@requestId,13,'applicant-request-operationally-authorized',
                       'OperationalReview','Queued',@actorRole,@actorId,@idempotencyKey,@fingerprint);
                """;
            requestEvent.Parameters.AddWithValue("eventId", Guid.NewGuid());
            requestEvent.Parameters.AddWithValue("requestId", requestId);
            requestEvent.Parameters.AddWithValue("actorId", actorId);
            requestEvent.Parameters.AddWithValue("actorRole", actorRole);
            requestEvent.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            requestEvent.Parameters.AddWithValue("fingerprint", commandFingerprint);
            await requestEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(source with
        {
            RequestVersion = TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestVersion,
            RequestStatus = TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestStatus
        }, practiceId, facilityId, authorizedAt);
    }

    private static TelehealthApplicantRequestQueueAuthorizationRecord CreateRecord(
        TelehealthApplicantRequestQueueAuthorizationSource source,
        string practiceId,
        int facilityId,
        DateTimeOffset? authorizedAt) => new(
            source.RequestId,
            authorizedAt is null
                ? TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestVersion
                : TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestVersion,
            authorizedAt is null
                ? TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestStatus
                : TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestStatus,
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
            authorizedAt);

    private static string CreateSnapshot(
        TelehealthApplicantRequestQueueAuthorizationSource source,
        string practiceId,
        int facilityId) =>
        TelehealthApplicantRequestQueueAuthorizationPolicy.SnapshotFingerprint(
            source.RequestId,
            source.SubmissionId,
            source.ApplicantId,
            TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestVersion,
            practiceId,
            facilityId,
            source.CanonicalPatientId,
            source.SubmissionSnapshotFingerprint,
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
            source.SubmittedAt,
            source.ResultValidThrough);

    private static async Task<TelehealthApplicantRequestQueueAuthorizationSource?> LoadSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid requestId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select r.request_id,r.version,r.status,r.triage_outcome,
                   a.applicant_id,a.version,a.status,a.expires_at,r.patient_id,p.portal_enabled,
                   s.submission_id,s.submission_snapshot_fingerprint,s.practice_display_name,
                   s.payer_display_name,s.product_display_name,s.current_location_state_code,
                   s.purpose_category,s.date_of_service,s.candidate_staff_id,
                   s.candidate_display_name,s.candidate_npi_last4,
                   trim(concat(candidate.first_name,' ',candidate.last_name)),candidate.npi,
                   candidate.role,candidate.facility_id,candidate.active,
                   s.service_category,s.modality,s.submitted_at,s.result_valid_through,now(),
                   (s.policy_key='SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
                    and s.policy_version=1 and s.evidence_type='APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
                    and s.source_mode='NON_PRODUCTION'
                    and s.compatibility_target='AVENCHART_SYNTHETIC_OPERATIONAL_REVIEW_V1'
                    and s.business_outcome='SyntheticRequestSubmittedForOperationalReview'
                    and s.synthetic_automated_checks_complete and s.operational_review_created
                    and not s.practice_accepted and not s.patient_care_queue_entered
                    and not s.clinician_queue_entered and not s.appointment_created),
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
                    and (select count(*) from telehealth_applicant_request_operational_review_submissions x where x.request_id=r.request_id)=1),
                   (select count(*)::int from insurance_records x where lower(x.patient_id)=lower(r.patient_id)),
                   (select count(*)::int from telehealth_applicant_request_queue_authorizations x where x.request_id=r.request_id),
                   (select count(*)::int from telehealth_queue_entries x where x.request_id=r.request_id),
                   exists(select 1 from telehealth_queue_entries x where x.request_id=r.request_id and x.status='Ready'),
                   ((select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id))::int,
                   exists(select 1 from appointments appt where appt.id=r.appointment_id),
                   exists(select 1 from appointments appt where appt.id=r.appointment_id and appt.provider_id is null)
            from telehealth_requests r
            join telehealth_prospective_applicants a
              on a.applicant_id=r.source_applicant_id and a.practice_id=r.practice_id
             and a.facility_id=r.facility_id
            join patients p on p.canonical_id=r.patient_id and p.facility_id=r.facility_id
            join telehealth_applicant_request_operational_review_submissions s
              on s.request_id=r.request_id and s.applicant_id=a.applicant_id
             and s.practice_id=r.practice_id and s.facility_id=r.facility_id
             and s.canonical_patient_id=r.patient_id
            join staff candidate on candidate.id=s.candidate_staff_id
            where r.request_id=@requestId and r.practice_id=@practiceId and r.facility_id=@facilityId
            {(forUpdate ? "for update of r,a,p,s,candidate" : string.Empty)};
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(
            reader.GetGuid(0), checked((int)reader.GetInt64(1)), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetGuid(4),
            checked((int)reader.GetInt64(5)), reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8), reader.GetBoolean(9),
            reader.GetGuid(10), reader.GetString(11), reader.GetString(12), reader.GetString(13),
            reader.GetString(14), reader.GetString(15), reader.GetString(16),
            reader.GetFieldValue<DateOnly>(17), reader.GetInt32(18), reader.GetString(19),
            reader.GetString(20), reader.GetString(21), reader.GetString(22), reader.GetString(23),
            reader.IsDBNull(24) ? null : reader.GetInt32(24), reader.GetBoolean(25),
            reader.GetString(26), reader.GetString(27), reader.GetFieldValue<DateTimeOffset>(28),
            reader.GetFieldValue<DateTimeOffset>(29), reader.GetFieldValue<DateTimeOffset>(30),
            reader.GetBoolean(31), reader.GetBoolean(32), reader.GetInt32(33), reader.GetInt32(34),
            reader.GetInt32(35), reader.GetBoolean(36), reader.GetInt32(37), reader.GetBoolean(38),
            reader.GetBoolean(39));
    }

    private static async Task<TelehealthApplicantRequestQueueAuthorizationReplay?> LoadAuthorizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid requestId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select authorization_snapshot_fingerprint,decided_by_actor_id,
                   command_fingerprint,authorized_at
            from telehealth_applicant_request_queue_authorizations
            where practice_id=@practiceId and facility_id=@facilityId and request_id=@requestId
              and (@idempotencyKey::text is null or idempotency_key=@idempotencyKey);
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("idempotencyKey", (object?)idempotencyKey ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3))
            : null;
    }

    private static async Task RequireCurrentActorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int facilityId,
        int? staffId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (staffId is null)
        {
            if (actorRole == "administrator") return;
            throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "This queue authorization requires an active configured-facility staff record.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(select 1 from staff where id=@staffId and facility_id=@facilityId and active=true);
            """;
        command.Parameters.AddWithValue("staffId", staffId.Value);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw TelehealthProblem.Forbidden(
                "telehealth_staff_record_required",
                "This queue authorization requires an active configured-facility staff record.");
        }
    }

    private static void RequireReady(TelehealthApplicantRequestQueueAuthorizationSource source)
    {
        if (source.RequestStatus != TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestStatus
            || source.RequestVersion != TelehealthApplicantRequestQueueAuthorizationPolicy.EntryRequestVersion
            || source.TriageOutcome != "TelehealthEligible"
            || source.ApplicantStatus != "SyntheticRequestCreated" || source.ApplicantVersion != 26
            || source.ApplicantExpiresAt <= source.DatabaseNow || source.ResultValidThrough <= source.DatabaseNow
            || source.PortalEnabled || source.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || source.PurposeCategory is not ("migraine" or "sleep")
            || !source.SubmissionEvidenceValid || !source.ExactEvidenceChain
            || source.CurrentCandidateDisplayName != source.CandidateDisplayName
            || !source.CurrentCandidateNpi.EndsWith(source.CandidateNpiLast4, StringComparison.Ordinal)
            || source.CandidateRole != "provider" || source.CandidateFacilityId != 10
            || !source.CandidateActive || source.CanonicalInsuranceCount != 0
            || source.AuthorizationCount != 0 || source.QueueCount != 0 || source.DownstreamCount != 0
            || source.AppointmentCreated || source.AppointmentUnassigned)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompleted(
        TelehealthApplicantRequestQueueAuthorizationSource source,
        TelehealthApplicantRequestQueueAuthorizationReplay record,
        string practiceId,
        int facilityId)
    {
        if (source.RequestStatus != TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestStatus
            || source.RequestVersion != TelehealthApplicantRequestQueueAuthorizationPolicy.ResultingRequestVersion
            || source.TriageOutcome != "TelehealthEligible" || source.PortalEnabled
            || !source.SubmissionEvidenceValid || !source.ExactEvidenceChain
            || source.AuthorizationCount != 1 || source.QueueCount != 1 || !source.QueueReady
            || source.DownstreamCount != 0 || !source.AppointmentCreated || !source.AppointmentUnassigned
            || source.CanonicalInsuranceCount != 0
            || record.AuthorizedAt > source.DatabaseNow
            || record.AuthorizationSnapshotFingerprint != CreateSnapshot(source, practiceId, facilityId))
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplay(
        TelehealthApplicantRequestQueueAuthorizationReplay replay,
        string actorId,
        string commandFingerprint)
    {
        if (!string.Equals(replay.ActorId, actorId, StringComparison.Ordinal)
            || !string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_authorization_idempotency_conflict",
                "The queue-authorization idempotency key was already used with different content or by another actor.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_queue_authorization_provenance_conflict",
        "The operational-review evidence is unavailable, expired, or changed.");
}
