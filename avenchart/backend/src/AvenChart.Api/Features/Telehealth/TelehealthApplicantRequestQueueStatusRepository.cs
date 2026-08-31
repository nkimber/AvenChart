// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestQueueStatusRecord(
    Guid RequestId,
    TelehealthRequestStatus RequestStatus,
    int RequestVersion,
    DateTimeOffset RequestUpdatedAt,
    DateTimeOffset SnapshotAt,
    int? ApproximateRequestsAhead);

internal sealed record TelehealthApplicantRequestQueueStatusSource(
    string AccessKeyHash,
    string ApplicantStatus,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    TelehealthRequestStatus RequestStatus,
    int RequestVersion,
    string? TriageOutcome,
    DateTimeOffset RequestUpdatedAt,
    bool PatientShellValid,
    int SubmissionCount,
    bool SubmissionValid,
    int AuthorizationCount,
    bool AuthorizationValid,
    int QueueCount,
    string? QueueStatus,
    bool AppointmentCreated,
    int ActiveReservationCount,
    bool ReservationValid,
    string? AppointmentStatus,
    int ConnectionSessionCount,
    int ActiveApplicantGrantCount,
    bool ConnectionValid,
    int ConsultationCount,
    bool ConsultationValid,
    int? ApproximateRequestsAhead);

public sealed class TelehealthApplicantRequestQueueStatusRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestQueueStatusRecord> GetAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        string participantSubjectHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select a.access_key_hash,a.status,a.version,a.expires_at,now(),
                   r.request_id,r.status,r.version,r.triage_outcome,r.updated_at,
                   (not p.portal_enabled and p.merged_into_patient_id is null
                    and coalesce(lower(p.lifecycle_status),'active')='active'
                    and p.deceased_date is null),
                   (select count(*)::int
                      from telehealth_applicant_request_operational_review_submissions submission
                     where submission.request_id=r.request_id and submission.applicant_id=a.applicant_id),
                   exists(
                     select 1 from telehealth_applicant_request_operational_review_submissions submission
                      where submission.request_id=r.request_id and submission.applicant_id=a.applicant_id
                        and submission.practice_id=a.practice_id and submission.facility_id=a.facility_id
                        and submission.canonical_patient_id=r.patient_id
                        and submission.resulting_request_status='OperationalReview'
                        and submission.resulting_request_version=12
                        and submission.policy_key='SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
                        and submission.policy_version=1
                        and submission.evidence_type='APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
                        and submission.source_mode='NON_PRODUCTION'
                        and submission.business_outcome='SyntheticRequestSubmittedForOperationalReview'
                        and submission.synthetic_automated_checks_complete
                        and submission.operational_review_created
                        and not submission.practice_accepted
                        and not submission.patient_care_queue_entered
                        and not submission.clinician_queue_entered
                        and not submission.appointment_created),
                   (select count(*)::int
                      from telehealth_applicant_request_queue_authorizations queue_authorization
                     where queue_authorization.request_id=r.request_id and queue_authorization.applicant_id=a.applicant_id),
                   exists(
                     select 1 from telehealth_applicant_request_queue_authorizations queue_authorization
                      where queue_authorization.request_id=r.request_id and queue_authorization.applicant_id=a.applicant_id
                        and queue_authorization.practice_id=a.practice_id and queue_authorization.facility_id=a.facility_id
                        and queue_authorization.canonical_patient_id=r.patient_id
                        and queue_authorization.resulting_request_status='Queued'
                        and queue_authorization.resulting_request_version=13
                        and queue_authorization.policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.policy_version=1
                        and queue_authorization.evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.source_mode='NON_PRODUCTION'
                        and queue_authorization.business_outcome='SyntheticRequestAuthorizedToQueue'
                        and queue_authorization.practice_accepted
                        and queue_authorization.patient_care_queue_entered
                        and queue_authorization.clinician_queue_entered
                        and queue_authorization.doctor_search_started
                        and queue_authorization.appointment_created
                        and not queue_authorization.rendering_physician_assigned
                        and not queue_authorization.coverage_verified
                        and not queue_authorization.financial_route_created
                        and not queue_authorization.queue_position_assigned
                        and not queue_authorization.encounter_created
                        and not queue_authorization.consent_created
                        and not queue_authorization.care_authorized
                        and not queue_authorization.integration_enabled
                        and not queue_authorization.external_call_performed),
                   (select count(*)::int from telehealth_queue_entries entry where entry.request_id=r.request_id),
                   current_queue.status,
                   exists(select 1 from appointments appointment
                           where appointment.id=r.appointment_id and appointment.patient_id=r.patient_id
                             and appointment.facility_id=r.facility_id),
                   (select count(*)::int from telehealth_reservations reservation
                     where reservation.request_id=r.request_id and reservation.status='Active'),
                   exists(
                     select 1
                       from telehealth_reservations reservation
                       join telehealth_clinician_shifts shift on shift.shift_id=reservation.shift_id
                       join telehealth_applicant_request_queue_authorizations queue_authorization
                         on queue_authorization.request_id=reservation.request_id
                        and queue_authorization.applicant_id=a.applicant_id
                       join appointments appointment on appointment.id=r.appointment_id
                      where reservation.request_id=r.request_id
                        and reservation.queue_entry_id=current_queue.queue_entry_id
                        and reservation.status='Active'
                        and reservation.lease_expires_at>now()
                        and reservation.clinician_staff_id=queue_authorization.candidate_staff_id
                        and reservation.reserved_at>=queue_authorization.authorized_at
                        and reservation.reserved_at<queue_authorization.result_valid_through
                        and shift.practice_id=r.practice_id and shift.facility_id=r.facility_id
                        and shift.clinician_staff_id=reservation.clinician_staff_id
                        and shift.status='Active'
                        and queue_authorization.practice_id=r.practice_id
                        and queue_authorization.facility_id=r.facility_id
                        and queue_authorization.canonical_patient_id=r.patient_id
                        and queue_authorization.resulting_request_status='Queued'
                        and queue_authorization.resulting_request_version=13
                        and queue_authorization.policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.policy_version=1
                        and queue_authorization.evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.source_mode='NON_PRODUCTION'
                        and queue_authorization.business_outcome='SyntheticRequestAuthorizedToQueue'
                        and now()<queue_authorization.result_valid_through
                        and appointment.patient_id=r.patient_id
                        and appointment.facility_id=r.facility_id
                        and appointment.provider_id=reservation.clinician_staff_id),
                   (select appointment.status from appointments appointment where appointment.id=r.appointment_id),
                   (select count(*)::int from telehealth_video_sessions session where session.request_id=r.request_id),
                   (select count(*)::int
                      from telehealth_video_sessions session
                      join telehealth_video_participant_grants grant_record
                        on grant_record.session_id=session.session_id
                     where session.request_id=r.request_id
                       and grant_record.participant_role='patient'
                       and grant_record.participant_subject_hash=@participantSubjectHash
                       and grant_record.status='Issued'
                       and grant_record.expires_at>now()),
                   exists(
                     select 1
                       from telehealth_video_sessions session
                       join telehealth_reservations reservation
                         on reservation.reservation_id=session.reservation_id
                        and reservation.request_id=session.request_id
                       join telehealth_video_participant_grants grant_record
                         on grant_record.session_id=session.session_id
                       join telehealth_video_preflights preflight
                         on preflight.preflight_id=grant_record.preflight_id
                        and preflight.session_id=session.session_id
                        and preflight.participant_role=grant_record.participant_role
                        and preflight.participant_subject_hash=grant_record.participant_subject_hash
                       join appointments appointment on appointment.id=r.appointment_id
                      where session.request_id=r.request_id
                        and session.practice_id=r.practice_id
                        and session.facility_id=r.facility_id
                        and session.adapter_mode='NON_PRODUCTION'
                        and session.status='WaitingRoom'
                        and session.expires_at>now()
                        and not session.recording_enabled
                        and not session.transcription_enabled
                        and not session.media_transport_enabled
                        and reservation.status='Active'
                        and reservation.lease_expires_at>now()
                        and grant_record.participant_role='patient'
                        and grant_record.participant_subject_hash=@participantSubjectHash
                        and grant_record.status='Issued'
                        and grant_record.expires_at>now()
                        and grant_record.expires_at<=session.expires_at
                        and grant_record.credential_hash~'^[0-9a-f]{64}$'
                        and grant_record.command_fingerprint=preflight.command_fingerprint
                        and grant_record.idempotency_key=preflight.idempotency_key
                        and preflight.browser_supported
                        and preflight.camera_available
                        and preflight.microphone_available
                        and preflight.speaker_available
                        and preflight.synthetic_data_confirmed
                        and preflight.network_quality in ('unknown','limited','good')
                        and appointment.patient_id=r.patient_id
                        and appointment.facility_id=r.facility_id
                        and appointment.provider_id=reservation.clinician_staff_id
                        and appointment.status='@'
                        and exists(
                          select 1 from telehealth_request_events request_event
                           where request_event.request_id=r.request_id
                             and request_event.aggregate_version=r.version
                             and request_event.action='connection-room-entered'
                             and request_event.from_status='Reserved'
                             and request_event.to_status='Connecting'
                             and request_event.actor_type='patient'
                             and request_event.actor_id=@participantSubjectHash
                             and request_event.idempotency_key=grant_record.idempotency_key
                             and request_event.command_fingerprint=grant_record.command_fingerprint)
                        and exists(
                          select 1 from telehealth_video_events video_event
                           where video_event.session_id=session.session_id
                             and video_event.action='participant-grant-issued'
                             and video_event.actor_type='patient'
                             and video_event.actor_subject_hash=@participantSubjectHash
                             and video_event.idempotency_key=grant_record.idempotency_key
                             and video_event.command_fingerprint=grant_record.command_fingerprint)),
                   (select count(*)::int
                      from telehealth_consultation_contexts consultation
                     where consultation.request_id=r.request_id),
                   exists(
                     select 1
                       from telehealth_consultation_contexts consultation
                       join telehealth_reservations reservation
                         on reservation.reservation_id=consultation.reservation_id
                        and reservation.request_id=consultation.request_id
                       join telehealth_clinician_shifts shift
                         on shift.shift_id=consultation.shift_id
                        and shift.clinician_staff_id=consultation.physician_staff_id
                       join telehealth_video_sessions session
                         on session.session_id=consultation.session_id
                        and session.request_id=consultation.request_id
                       join appointments appointment
                         on appointment.id=consultation.appointment_id
                       join encounters encounter
                         on encounter.encounter=consultation.encounter_id
                       join telehealth_applicant_request_queue_authorizations queue_authorization
                         on queue_authorization.request_id=consultation.request_id
                        and queue_authorization.applicant_id=a.applicant_id
                        and queue_authorization.candidate_staff_id=consultation.physician_staff_id
                      where consultation.request_id=r.request_id
                        and consultation.practice_id=r.practice_id
                        and consultation.facility_id=r.facility_id
                        and consultation.modality='SYNTHETIC_VIDEO'
                        and consultation.patient_location_state in ('GA','CA','FL')
                        and consultation.patient_identity_discussed
                        and consultation.callback_confirmed
                        and consultation.privacy_confirmed
                        and consultation.consent_discussed
                        and consultation.no_concerning_symptom_change
                        and consultation.emergency_plan_confirmed
                        and consultation.communication_sufficient
                        and consultation.synthetic_data_confirmed
                        and not consultation.legal_effect
                        and reservation.status='Released'
                        and reservation.clinician_staff_id=consultation.physician_staff_id
                        and shift.practice_id=r.practice_id
                        and shift.facility_id=r.facility_id
                        and session.status='Ended'
                        and session.adapter_mode='NON_PRODUCTION'
                        and not session.recording_enabled
                        and not session.transcription_enabled
                        and not session.media_transport_enabled
                        and appointment.patient_id=r.patient_id
                        and appointment.facility_id=r.facility_id
                        and appointment.provider_id=consultation.physician_staff_id
                        and appointment.status='>'
                        and encounter.patient_id=r.patient_id
                        and encounter.provider_id=consultation.physician_staff_id
                        and encounter.facility_id=r.facility_id
                        and encounter.source_appointment_id=consultation.appointment_id
                        and current_queue.status='Removed'
                        and (select count(*) from telehealth_video_participant_grants grant_record
                              where grant_record.session_id=session.session_id)>=2
                        and not exists(
                          select 1 from telehealth_video_participant_grants grant_record
                           where grant_record.session_id=session.session_id
                             and grant_record.participant_role not in ('patient','physician'))
                        and not exists(
                          select 1 from telehealth_video_participant_grants grant_record
                           where grant_record.session_id=session.session_id
                             and grant_record.status='Issued')
                        and exists(
                          select 1 from telehealth_video_participant_grants grant_record
                           where grant_record.session_id=session.session_id
                             and grant_record.participant_role='patient'
                             and grant_record.participant_subject_hash=@participantSubjectHash
                             and grant_record.status='Revoked')
                        and exists(
                          select 1 from telehealth_video_participant_grants grant_record
                           where grant_record.session_id=session.session_id
                             and grant_record.participant_role='physician'
                             and grant_record.status='Revoked')
                        and exists(
                          select 1 from telehealth_consultation_events consultation_event
                           where consultation_event.consultation_id=consultation.consultation_id
                             and consultation_event.request_id=r.request_id
                             and consultation_event.aggregate_version=1
                             and consultation_event.action='consultation-started'
                             and consultation_event.actor_type='physician')
                        and exists(
                          select 1 from telehealth_request_events request_event
                           where request_event.request_id=r.request_id
                             and request_event.aggregate_version=case
                               when r.status='WrapUp' then r.version-1
                               when r.status='Closed' then r.version-2
                               else r.version
                             end
                             and request_event.action='consultation-started'
                             and request_event.from_status='Connecting'
                             and request_event.to_status='InConsultation'
                             and request_event.actor_type='physician')
                        and (
                          (r.status='InConsultation'
                           and consultation.status='Started'
                           and consultation.version=1
                           and consultation.media_ended_at is null
                           and shift.status='Busy')
                          or
                          (r.status='WrapUp'
                           and consultation.status='MediaEnded'
                           and consultation.version=2
                           and consultation.media_ended_at is not null
                           and shift.status='WrapUp'
                           and exists(
                             select 1 from telehealth_consultation_events consultation_event
                              where consultation_event.consultation_id=consultation.consultation_id
                                and consultation_event.request_id=r.request_id
                                and consultation_event.aggregate_version=2
                                and consultation_event.action='consultation-wrap-up-entered'
                                and consultation_event.actor_type='physician')
                           and exists(
                             select 1 from telehealth_request_events request_event
                              where request_event.request_id=r.request_id
                                and request_event.aggregate_version=r.version
                                and request_event.action='consultation-wrap-up-entered'
                                and request_event.from_status='InConsultation'
                                and request_event.to_status='WrapUp'
                                and request_event.actor_type='physician'))
                          or
                          (r.status='Closed'
                           and consultation.status='Closed'
                           and consultation.version=3
                           and consultation.media_ended_at is not null
                           and consultation.closed_at is not null
                           and shift.status='Active'
                           and exists(
                             select 1 from encounter_signatures signature
                              where signature.encounter=encounter.encounter and signature.is_lock)
                           and exists(
                             select 1 from telehealth_consultation_events consultation_event
                              where consultation_event.consultation_id=consultation.consultation_id
                                and consultation_event.request_id=r.request_id
                                and consultation_event.aggregate_version=3
                                and consultation_event.action='synthetic-visit-closed'
                                and consultation_event.actor_type='physician')
                           and exists(
                             select 1 from telehealth_request_events request_event
                              where request_event.request_id=r.request_id
                                and request_event.aggregate_version=r.version
                                and request_event.action='synthetic-visit-closed'
                                and request_event.from_status='WrapUp'
                                and request_event.to_status='Closed'
                                and request_event.actor_type='physician')))),
                   case
                     when r.status='Queued' and current_queue.status='Ready' then (
                       select count(*)
                         from telehealth_queue_entries candidate
                         join telehealth_requests candidate_request
                           on candidate_request.request_id=candidate.request_id
                        where candidate.practice_id=r.practice_id
                          and candidate.facility_id=r.facility_id
                          and candidate.status='Ready'
                          and candidate_request.status='Queued'
                          and (
                            candidate.ready_at < current_queue.ready_at
                            or (candidate.ready_at=current_queue.ready_at
                                and candidate.request_id < current_queue.request_id)))
                     else null
                   end
              from telehealth_prospective_applicants a
              join telehealth_applicant_request_creations creation
                on creation.applicant_id=a.applicant_id
               and creation.practice_id=a.practice_id and creation.facility_id=a.facility_id
              join telehealth_requests r
                on r.request_id=creation.request_id and r.source_applicant_id=a.applicant_id
               and r.practice_id=a.practice_id and r.facility_id=a.facility_id
               and r.patient_id=creation.canonical_patient_id
              join patients p on p.canonical_id=r.patient_id and p.facility_id=r.facility_id
              left join telehealth_queue_entries current_queue on current_queue.request_id=r.request_id
             where a.applicant_id=@applicantId and a.practice_id=@practiceId
               and a.facility_id=@facilityId;
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("participantSubjectHash", participantSubjectHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }

        var source = new TelehealthApplicantRequestQueueStatusSource(
            reader.GetString(0), reader.GetString(1), checked((int)reader.GetInt64(2)),
            reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetGuid(5), Enum.Parse<TelehealthRequestStatus>(reader.GetString(6)),
            checked((int)reader.GetInt64(7)), reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9), reader.GetBoolean(10), reader.GetInt32(11),
            reader.GetBoolean(12), reader.GetInt32(13), reader.GetBoolean(14),
            reader.GetInt32(15), reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.GetBoolean(17), reader.GetInt32(18), reader.GetBoolean(19),
            reader.IsDBNull(20) ? null : reader.GetString(20), reader.GetInt32(21),
            reader.GetInt32(22), reader.GetBoolean(23), reader.GetInt32(24),
            reader.GetBoolean(25), reader.IsDBNull(26) ? null : checked((int?)reader.GetInt64(26)));
        RequireAccess(source, accessKeyHash);
        RequireApplicant(source);
        RequireVisibleState(source);
        return new(
            source.RequestId,
            source.RequestStatus,
            source.RequestVersion,
            source.RequestUpdatedAt,
            source.DatabaseNow,
            source.RequestStatus == TelehealthRequestStatus.Queued
                ? source.ApproximateRequestsAhead
                : null);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestQueueStatusSource source,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(source.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestQueueStatusSource source)
    {
        if (source.ApplicantExpiresAt <= source.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (source.ApplicantStatus != TelehealthApplicantRequestQueueStatusPolicy.ApplicantStatus
            || source.ApplicantVersion != TelehealthApplicantRequestQueueStatusPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_status_state_conflict",
                "Queue status is not available for this applicant state.");
        }
    }

    private static void RequireVisibleState(TelehealthApplicantRequestQueueStatusSource source)
    {
        var commonValid = source.PatientShellValid
            && source.TriageOutcome == "TelehealthEligible"
            && source.SubmissionCount == 1
            && source.SubmissionValid
            && TelehealthApplicantRequestQueueStatusPolicy.IsVisibleStatus(source.RequestStatus);
        var operationalReviewValid = source.RequestStatus == TelehealthRequestStatus.OperationalReview
            && source.RequestVersion == 12
            && source.AuthorizationCount == 0
            && source.QueueCount == 0
            && source.ActiveReservationCount == 0
            && source.ConnectionSessionCount == 0
            && source.ActiveApplicantGrantCount == 0
            && !source.AppointmentCreated;
        var preAuthorizationCancellationValid = source.RequestStatus == TelehealthRequestStatus.Cancelled
            && source.RequestVersion == 13
            && source.AuthorizationCount == 0
            && source.QueueCount == 0
            && !source.AppointmentCreated
            && source.ActiveReservationCount == 0
            && source.ConnectionSessionCount == 0
            && source.ActiveApplicantGrantCount == 0
            && source.ConsultationCount == 0;
        var queueStateValid = source.RequestStatus switch
        {
            TelehealthRequestStatus.Queued => source.RequestVersion >= 13
                && source.QueueStatus == "Ready"
                && source.ActiveReservationCount == 0
                && source.ConnectionSessionCount == 0
                && source.ActiveApplicantGrantCount == 0,
            TelehealthRequestStatus.Reserved => source.RequestVersion >= 14
                && source.QueueStatus == "Reserved"
                && source.ActiveReservationCount == 1
                && source.ReservationValid
                && source.AppointmentStatus is null or "-"
                && source.ConnectionSessionCount == 0
                && source.ActiveApplicantGrantCount == 0,
            TelehealthRequestStatus.Connecting => source.RequestVersion >= 15
                && source.QueueStatus == "Reserved"
                && source.ActiveReservationCount == 1
                && source.ReservationValid
                && source.AppointmentStatus == "@"
                && source.ConnectionSessionCount == 1
                && source.ActiveApplicantGrantCount == 1
                && source.ConnectionValid,
            TelehealthRequestStatus.InConsultation => source.RequestVersion >= 16
                && source.QueueStatus == "Removed"
                && source.ActiveReservationCount == 0
                && source.AppointmentStatus == ">"
                && source.ConnectionSessionCount == 1
                && source.ActiveApplicantGrantCount == 0
                && source.ConsultationCount == 1
                && source.ConsultationValid,
            TelehealthRequestStatus.WrapUp => source.RequestVersion >= 17
                && source.QueueStatus == "Removed"
                && source.ActiveReservationCount == 0
                && source.AppointmentStatus == ">"
                && source.ConnectionSessionCount == 1
                && source.ActiveApplicantGrantCount == 0
                && source.ConsultationCount == 1
                && source.ConsultationValid,
            TelehealthRequestStatus.Closed => source.RequestVersion >= 18
                && source.QueueStatus == "Removed"
                && source.ActiveReservationCount == 0
                && source.AppointmentStatus == ">"
                && source.ConnectionSessionCount == 1
                && source.ActiveApplicantGrantCount == 0
                && source.ConsultationCount == 1
                && source.ConsultationValid,
            TelehealthRequestStatus.Cancelled =>
                (source.RequestVersion == 13
                 && source.AuthorizationCount == 0
                 && source.QueueCount == 0
                 && !source.AppointmentCreated
                 && source.ActiveReservationCount == 0
                 && source.ConnectionSessionCount == 0
                 && source.ActiveApplicantGrantCount == 0
                 && source.ConsultationCount == 0)
                || (source.RequestVersion >= 14
                    && source.QueueStatus == "Removed"
                    && source.ActiveReservationCount == 0
                    && source.AppointmentStatus == "x"
                    && source.ConnectionSessionCount == 0
                    && source.ActiveApplicantGrantCount == 0
                    && source.ConsultationCount == 0),
            _ => false
        };
        var downstreamValid = source.AuthorizationCount == 1
            && source.AuthorizationValid
            && source.QueueCount == 1
            && source.AppointmentCreated
            && queueStateValid;
        if (!commonValid || (!operationalReviewValid && !preAuthorizationCancellationValid && !downstreamValid))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_status_provenance_conflict",
                "The applicant-owned queue status is unavailable or changed.");
        }
    }
}
