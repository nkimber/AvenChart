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
    int? ApproximateRequestsAhead);

public sealed class TelehealthApplicantRequestQueueStatusRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestQueueStatusRecord> GetAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
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
            reader.GetBoolean(17), reader.IsDBNull(18) ? null : checked((int?)reader.GetInt64(18)));
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
            && !source.AppointmentCreated;
        var queueStateValid = source.RequestStatus switch
        {
            TelehealthRequestStatus.Queued => source.RequestVersion >= 13 && source.QueueStatus == "Ready",
            _ => false
        };
        var downstreamValid = source.AuthorizationCount == 1
            && source.AuthorizationValid
            && source.QueueCount == 1
            && source.AppointmentCreated
            && queueStateValid;
        if (!commonValid || (!operationalReviewValid && !downstreamValid))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_queue_status_provenance_conflict",
                "The applicant-owned queue status is unavailable or changed.");
        }
    }
}
