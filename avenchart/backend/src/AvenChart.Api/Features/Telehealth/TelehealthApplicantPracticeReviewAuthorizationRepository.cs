// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewAuthorizationRecord(
    Guid AuthorizationId,
    Guid PracticeReviewCaseId,
    int ApplicantVersion,
    string ApplicantStatus,
    string ActorId,
    string Decision,
    string RationaleCode,
    string PolicyKey,
    int PolicyVersion,
    string EvidenceType,
    DateTimeOffset DecidedAt);

internal sealed record TelehealthApplicantPracticeReviewAuthorizationCandidate(
    Guid ApplicantId,
    int ApplicantVersion,
    Guid SubmissionId,
    string CanonicalPatientId,
    Guid ReadinessAcknowledgmentId,
    Guid ClaimId);

public sealed class TelehealthApplicantPracticeReviewAuthorizationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantPracticeReviewAuthorizationRecord> AuthorizeAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        string actorRole,
        Guid caseId,
        int expectedApplicantVersion,
        bool noClinicalEligibilityAcknowledged,
        bool noCoverageGuaranteeAcknowledged,
        bool noRequestOrQueueAcknowledged,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, caseId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplay(replay.Value.Record, replay.Value.CommandFingerprint, actorId, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var candidate = await LoadEligibleForUpdateAsync(
            connection, transaction, practiceId, facilityId, actorId, actorRole, caseId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        if (candidate.ApplicantVersion != expectedApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_authorization_version_conflict",
                "The practice-review item changed. Reload the inbox and packet before retrying.");
        }

        replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, caseId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplay(replay.Value.Record, replay.Value.CommandFingerprint, actorId, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var nextVersion = candidate.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status='SyntheticPracticeReviewAuthorized',version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPracticeReviewSubmitted';
                """;
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", candidate.ApplicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedApplicantVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_practice_review_authorization_version_conflict",
                    "The practice-review item changed. Reload the inbox and packet before retrying.");
            }
        }

        var authorizationId = Guid.NewGuid();
        DateTimeOffset decidedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_practice_review_authorizations(
                  authorization_id,case_id,applicant_id,practice_id,facility_id,
                  canonical_patient_id,submission_id,readiness_acknowledgment_id,claim_id,
                  source_applicant_version,resulting_applicant_version,resulting_applicant_status,
                  decision,rationale_code,packet_policy_key,packet_policy_version,
                  no_clinical_eligibility_acknowledged,no_coverage_guarantee_acknowledged,
                  no_request_or_queue_acknowledged,policy_key,policy_version,evidence_type,
                  decided_by_staff_id,decided_by_actor_id,decided_by_role,
                  idempotency_key,command_fingerprint)
                values(
                  @authorizationId,@caseId,@applicantId,@practiceId,@facilityId,
                  @patientId,@submissionId,@readinessId,@claimId,
                  @sourceVersion,@nextVersion,'SyntheticPracticeReviewAuthorized',
                  'AuthorizedForSyntheticRequestCreation','OperationalPrerequisitesReviewed',
                  'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET',1,
                  @noClinical,@noCoverage,@noRequestOrQueue,
                  'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION',1,
                  'CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY',
                  @staffId,@actorId,@actorRole,@idempotencyKey,@commandFingerprint)
                returning decided_at;
                """;
            insert.Parameters.AddWithValue("authorizationId", authorizationId);
            insert.Parameters.AddWithValue("caseId", caseId);
            insert.Parameters.AddWithValue("applicantId", candidate.ApplicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", candidate.CanonicalPatientId);
            insert.Parameters.AddWithValue("submissionId", candidate.SubmissionId);
            insert.Parameters.AddWithValue("readinessId", candidate.ReadinessAcknowledgmentId);
            insert.Parameters.AddWithValue("claimId", candidate.ClaimId);
            insert.Parameters.AddWithValue("sourceVersion", candidate.ApplicantVersion);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("noClinical", noClinicalEligibilityAcknowledged);
            insert.Parameters.AddWithValue("noCoverage", noCoverageGuaranteeAcknowledged);
            insert.Parameters.AddWithValue("noRequestOrQueue", noRequestOrQueueAcknowledged);
            insert.Parameters.AddWithValue("staffId", (object?)staffId ?? DBNull.Value);
            insert.Parameters.AddWithValue("actorId", actorId);
            insert.Parameters.AddWithValue("actorRole", actorRole);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Practice-review authorization time was not returned.");
            }
            decidedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-practice-review-authorized',
                       'SyntheticPracticeReviewSubmitted','SyntheticPracticeReviewAuthorized',
                       'administrator',@eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", candidate.ApplicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "practice-review-authorization:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            authorizationId, caseId, nextVersion,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.ResultingApplicantStatus,
            actorId, TelehealthApplicantPracticeReviewAuthorizationPolicy.Decision,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.RationaleCode,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.PolicyKey,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.PolicyVersion,
            TelehealthApplicantPracticeReviewAuthorizationPolicy.EvidenceType,
            decidedAt);
    }

    private static async Task<TelehealthApplicantPracticeReviewAuthorizationCandidate?> LoadEligibleForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        string actorId,
        string actorRole,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.applicant_id,a.version,submission.submission_id,c.canonical_patient_id,
                   c.readiness_acknowledgment_id,active_claim.claim_id
            from telehealth_prospective_practice_review_cases c
            join telehealth_applicant_practice_review_submissions submission
              on submission.case_id=c.case_id and submission.applicant_id=c.applicant_id
             and submission.practice_id=c.practice_id and submission.facility_id=c.facility_id
             and submission.canonical_patient_id=c.canonical_patient_id
             and submission.readiness_acknowledgment_id=c.readiness_acknowledgment_id
             and submission.readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and submission.review_route=c.review_route
            join telehealth_prospective_applicants a
              on a.applicant_id=c.applicant_id and a.practice_id=c.practice_id
             and a.facility_id=c.facility_id and a.version=submission.resulting_applicant_version
             and a.status=submission.resulting_applicant_status
            join telehealth_applicant_pre_request_readiness_acknowledgments readiness
              on readiness.acknowledgment_id=c.readiness_acknowledgment_id
             and readiness.applicant_id=c.applicant_id and readiness.practice_id=c.practice_id
             and readiness.facility_id=c.facility_id
             and readiness.canonical_patient_id=c.canonical_patient_id
             and readiness.pre_request_readiness_snapshot_fingerprint=c.readiness_snapshot_fingerprint
             and readiness.overall_route=c.review_route
            join telehealth_applicant_synthetic_promotions promotion
              on promotion.promotion_id=readiness.promotion_id
             and promotion.applicant_id=c.applicant_id and promotion.practice_id=c.practice_id
             and promotion.facility_id=c.facility_id
             and promotion.canonical_patient_id=c.canonical_patient_id
             and promotion.canonical_patient_created
            join patients patient
              on patient.canonical_id=c.canonical_patient_id and patient.facility_id=c.facility_id
             and not patient.portal_enabled and patient.merged_into_patient_id is null
             and patient.first_name=a.legal_first_name and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code and patient.postal_code=a.postal_code
            join telehealth_applicant_visit_purposes purpose
              on purpose.applicant_id=c.applicant_id and purpose.practice_id=c.practice_id
             and purpose.facility_id=c.facility_id
            join telehealth_applicant_safety_triage_evaluations safety
              on safety.evaluation_id=purpose.safety_triage_evaluation_id
             and safety.applicant_id=c.applicant_id and safety.practice_id=c.practice_id
             and safety.facility_id=c.facility_id and safety.outcome=purpose.source_safety_outcome
            join telehealth_applicant_registration_details_confirmations registration
              on registration.confirmation_id=readiness.registration_details_confirmation_id
             and registration.applicant_id=c.applicant_id and registration.practice_id=c.practice_id
             and registration.facility_id=c.facility_id and registration.promotion_id=readiness.promotion_id
             and registration.canonical_patient_id=c.canonical_patient_id
             and registration.details_fingerprint=readiness.registration_details_fingerprint
            join telehealth_applicant_insurance_handoff_confirmations insurance
              on insurance.confirmation_id=readiness.insurance_handoff_confirmation_id
             and insurance.applicant_id=c.applicant_id and insurance.practice_id=c.practice_id
             and insurance.facility_id=c.facility_id and insurance.promotion_id=readiness.promotion_id
             and insurance.canonical_patient_id=c.canonical_patient_id
             and insurance.registration_details_confirmation_id=registration.confirmation_id
             and insurance.insurance_snapshot_fingerprint=readiness.insurance_snapshot_fingerprint
            join telehealth_applicant_communication_access_readiness communication
              on communication.readiness_id=readiness.communication_access_readiness_id
             and communication.applicant_id=c.applicant_id and communication.practice_id=c.practice_id
             and communication.facility_id=c.facility_id and communication.promotion_id=readiness.promotion_id
             and communication.canonical_patient_id=c.canonical_patient_id
             and communication.registration_details_confirmation_id=registration.confirmation_id
             and communication.insurance_handoff_confirmation_id=insurance.confirmation_id
             and communication.context_snapshot_fingerprint=readiness.communication_context_fingerprint
            join telehealth_applicant_device_preparations device
              on device.preparation_id=readiness.device_preparation_id
             and device.applicant_id=c.applicant_id and device.practice_id=c.practice_id
             and device.facility_id=c.facility_id and device.promotion_id=readiness.promotion_id
             and device.canonical_patient_id=c.canonical_patient_id
             and device.registration_details_confirmation_id=registration.confirmation_id
             and device.insurance_handoff_confirmation_id=insurance.confirmation_id
             and device.communication_access_readiness_id=communication.readiness_id
             and device.preparation_snapshot_fingerprint=readiness.preparation_snapshot_fingerprint
            join telehealth_applicant_clinical_information_summary_confirmations summary
              on summary.confirmation_id=readiness.clinical_information_summary_confirmation_id
             and summary.applicant_id=c.applicant_id and summary.practice_id=c.practice_id
             and summary.facility_id=c.facility_id and summary.promotion_id=readiness.promotion_id
             and summary.canonical_patient_id=c.canonical_patient_id
             and summary.clinical_inventory_id=readiness.clinical_inventory_id
             and summary.clinical_information_summary_snapshot_fingerprint=
                 readiness.clinical_information_summary_snapshot_fingerprint
             and summary.summary_route=readiness.clinical_information_summary_route
            join lateral (
              select claim.claim_id
              from telehealth_practice_review_claims claim
              where claim.case_id=c.case_id and claim.practice_id=c.practice_id
                and claim.facility_id=c.facility_id
                and claim.assigned_to_actor_id=@actorId
                and claim.assigned_to_role=@actorRole
                and claim.expected_applicant_version=a.version
                and claim.lease_expires_at>now()
              order by claim.assigned_at desc,claim.claim_id desc limit 1
            ) active_claim on true
            where c.case_id=@caseId and c.practice_id=@practiceId and c.facility_id=@facilityId
              and c.case_status='PendingPracticeReview'
              and a.status='SyntheticPracticeReviewSubmitted'
              and c.applicant_expires_at>now() and a.expires_at=c.applicant_expires_at
              and submission.applicant_expires_at=c.applicant_expires_at
              and submission.resulting_applicant_status='SyntheticPracticeReviewSubmitted'
              and submission.policy_key='SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION'
              and submission.policy_version=1 and submission.staff_review_created
              and not submission.clinician_review_created and not submission.practice_accepted
              and not submission.patient_record_changed and not submission.telehealth_request_created
              and not submission.patient_care_queue_entered and not submission.clinician_queue_entered
              and not submission.appointment_created and not submission.encounter_created
              and not submission.care_authorized and not submission.prescribing_enabled
              and not submission.billing_enabled and not submission.claim_created
              and not submission.integration_enabled and not submission.external_call_performed
              and readiness.resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'
              and readiness.resulting_applicant_version=submission.resulting_applicant_version-1
              and readiness.policy_key='SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS'
              and readiness.policy_version=1
              and registration.policy_key='SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
              and registration.policy_version=1 and not registration.identity_assurance_established
              and not registration.patient_record_changed and not registration.correction_completed
              and insurance.policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
              and insurance.policy_version=1 and not insurance.coverage_verified
              and not insurance.exact_network_confirmed and not insurance.canonical_coverage_created
              and not insurance.patient_record_changed and not insurance.financial_record_created
              and not insurance.request_created and not insurance.queue_enabled
              and not insurance.care_enabled and not insurance.prescribing_enabled
              and not insurance.billing_enabled and not insurance.claim_created
              and not insurance.integration_enabled and not insurance.external_call_performed
              and communication.policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
              and communication.policy_version=1 and not communication.interpreter_assigned
              and not communication.accessibility_accommodation_arranged
              and not communication.communication_arrangement_completed
              and not communication.patient_record_changed and not communication.request_created
              and not communication.queue_enabled and not communication.care_enabled
              and not communication.integration_enabled and not communication.external_call_performed
              and device.policy_key='SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
              and device.policy_version=1 and not device.technology_ready
              and not device.waiting_room_created and not device.media_session_created
              and not device.patient_record_changed and not device.request_created
              and not device.queue_entered and not device.care_authorized
              and not device.integration_enabled and not device.external_call_performed
              and summary.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
              and summary.policy_version=1 and not summary.questionnaire_response_created
              and not summary.medication_list_reconciled and not summary.allergy_list_reconciled
              and not summary.health_history_reconciled and not summary.clinician_review_created
              and not summary.clinical_eligibility_established and not summary.patient_record_changed
              and not summary.request_created and not summary.queue_entered
              and not summary.care_authorized and not summary.prescribing_enabled
              and purpose.purpose_category in ('migraine','sleep') and safety.outcome='TelehealthEligible'
              and not exists(select 1 from telehealth_practice_review_authorizations existing_authorization
                             where existing_authorization.case_id=c.case_id)
              and not exists(select 1 from insurance_records x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from medications x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from prescriptions x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from allergies x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from problems x where lower(x.patient_id)=lower(c.canonical_patient_id))
            for update of a,c;
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("actorId", actorId);
        command.Parameters.AddWithValue("actorRole", actorRole);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetGuid(2),
                reader.GetString(3), reader.GetGuid(4), reader.GetGuid(5))
            : null;
    }

    private static async Task<(TelehealthApplicantPracticeReviewAuthorizationRecord Record,
        string CommandFingerprint)?> LoadByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid caseId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select authorization_id,case_id,resulting_applicant_version,
                   resulting_applicant_status,decided_by_actor_id,decision,rationale_code,
                   policy_key,policy_version,evidence_type,decided_at,command_fingerprint
            from telehealth_practice_review_authorizations
            where case_id=@caseId and practice_id=@practiceId and facility_id=@facilityId
              and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return (new(
            reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetInt32(8), reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10)), reader.GetString(11));
    }

    private static void RequireReplay(
        TelehealthApplicantPracticeReviewAuthorizationRecord record,
        string existingFingerprint,
        string actorId,
        string commandFingerprint)
    {
        if (!string.Equals(record.ActorId, actorId, StringComparison.Ordinal)
            || !string.Equals(existingFingerprint, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_authorization_idempotency_conflict",
                "The authorization idempotency key was already used with different command content.");
        }
    }
}
