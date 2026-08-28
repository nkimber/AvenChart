// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPracticeReviewClaimRecord(
    Guid ClaimId,
    Guid PracticeReviewCaseId,
    int ApplicantVersion,
    string ActorId,
    DateTimeOffset AssignedAt,
    DateTimeOffset AssignmentExpiresAt,
    string PolicyKey,
    int PolicyVersion,
    string EvidenceType,
    bool NoDecisionAcknowledged,
    bool NoPatientContactAcknowledged,
    bool NoRequestOrCareQueueAcknowledged);

public sealed class TelehealthApplicantPracticeReviewClaimRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantPracticeReviewClaimRecord> ClaimAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        string actorRole,
        Guid caseId,
        int expectedApplicantVersion,
        bool noDecisionAcknowledged,
        bool noPatientContactAcknowledged,
        bool noRequestOrCareQueueAcknowledged,
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

        var candidate = await LoadEligibleCaseForUpdateAsync(
            connection, transaction, practiceId, facilityId, caseId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        if (candidate.ApplicantVersion != expectedApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_claim_version_conflict",
                "The practice-review item changed. Refresh the inbox before retrying.");
        }

        replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, caseId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplay(replay.Value.Record, replay.Value.CommandFingerprint, actorId, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var active = await LoadActiveAsync(
            connection, transaction, practiceId, facilityId, caseId, cancellationToken);
        if (active is not null)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_claim_active",
                active.ActorId == actorId
                    ? "You already hold the active review claim. Refresh the inbox."
                    : "Another authorized staff member currently holds this review claim.");
        }

        var claimId = Guid.NewGuid();
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            insert into telehealth_practice_review_claims(
              claim_id,case_id,practice_id,facility_id,expected_applicant_version,
              assigned_to_staff_id,assigned_to_actor_id,assigned_to_role,
              assigned_at,lease_expires_at,
              no_decision_acknowledged,no_patient_contact_acknowledged,
              no_request_or_care_queue_acknowledged,
              policy_key,policy_version,evidence_type,idempotency_key,command_fingerprint)
            values(
              @claimId,@caseId,@practiceId,@facilityId,@expectedVersion,
              @staffId,@actorId,@actorRole,now(),now()+interval '120 seconds',
              @noDecision,@noContact,@noRequestOrQueue,
              'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM',1,
              'PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT',
              @idempotencyKey,@commandFingerprint)
            returning claim_id,case_id,expected_applicant_version,assigned_to_actor_id,
                      assigned_at,lease_expires_at,policy_key,policy_version,evidence_type,
                      no_decision_acknowledged,no_patient_contact_acknowledged,
                      no_request_or_care_queue_acknowledged;
            """;
        insert.Parameters.AddWithValue("claimId", claimId);
        insert.Parameters.AddWithValue("caseId", caseId);
        insert.Parameters.AddWithValue("practiceId", practiceId);
        insert.Parameters.AddWithValue("facilityId", facilityId);
        insert.Parameters.AddWithValue("expectedVersion", expectedApplicantVersion);
        insert.Parameters.AddWithValue("staffId", (object?)staffId ?? DBNull.Value);
        insert.Parameters.AddWithValue("actorId", actorId);
        insert.Parameters.AddWithValue("actorRole", actorRole);
        insert.Parameters.AddWithValue("noDecision", noDecisionAcknowledged);
        insert.Parameters.AddWithValue("noContact", noPatientContactAcknowledged);
        insert.Parameters.AddWithValue("noRequestOrQueue", noRequestOrCareQueueAcknowledged);
        insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Practice-review claim receipt was not returned.");
        }
        var record = Read(reader);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return record;
    }

    private static async Task<(int ApplicantVersion, DateTimeOffset DatabaseNow)?> LoadEligibleCaseForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.version,now()
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
            where c.case_id=@caseId and c.practice_id=@practiceId and c.facility_id=@facilityId
              and c.case_status='PendingPracticeReview'
              and a.status='SyntheticPracticeReviewSubmitted'
              and c.applicant_expires_at>now() and a.expires_at=c.applicant_expires_at
              and submission.applicant_expires_at=c.applicant_expires_at
              and readiness.resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'
              and readiness.resulting_applicant_version=submission.resulting_applicant_version-1
              and submission.staff_review_created and not submission.clinician_review_created
              and not submission.practice_accepted and not submission.patient_record_changed
              and not submission.telehealth_request_created
              and not submission.patient_care_queue_entered and not submission.clinician_queue_entered
              and not submission.appointment_created and not submission.encounter_created
              and not submission.care_authorized and not submission.prescribing_enabled
              and not submission.billing_enabled and not submission.claim_created
              and not submission.integration_enabled and not submission.external_call_performed
              and purpose.purpose_category in ('migraine','sleep') and safety.outcome='TelehealthEligible'
              and not exists(select 1 from insurance_records x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from medications x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from prescriptions x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from allergies x where lower(x.patient_id)=lower(c.canonical_patient_id))
              and not exists(select 1 from problems x where lower(x.patient_id)=lower(c.canonical_patient_id))
            for update of c;
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (Convert.ToInt32(reader.GetInt64(0)), reader.GetFieldValue<DateTimeOffset>(1))
            : null;
    }

    private static async Task<TelehealthApplicantPracticeReviewClaimRecord?> LoadActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select claim_id,case_id,expected_applicant_version,assigned_to_actor_id,
                   assigned_at,lease_expires_at,policy_key,policy_version,evidence_type,
                   no_decision_acknowledged,no_patient_contact_acknowledged,
                   no_request_or_care_queue_acknowledged
            from telehealth_practice_review_claims
            where case_id=@caseId and practice_id=@practiceId and facility_id=@facilityId
              and lease_expires_at>now()
            order by assigned_at desc,claim_id desc limit 1;
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static async Task<(TelehealthApplicantPracticeReviewClaimRecord Record,
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
            select claim_id,case_id,expected_applicant_version,assigned_to_actor_id,
                   assigned_at,lease_expires_at,policy_key,policy_version,evidence_type,
                   no_decision_acknowledged,no_patient_contact_acknowledged,
                   no_request_or_care_queue_acknowledged,command_fingerprint
            from telehealth_practice_review_claims
            where case_id=@caseId and practice_id=@practiceId and facility_id=@facilityId
              and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("caseId", caseId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (Read(reader), reader.GetString(12)) : null;
    }

    private static TelehealthApplicantPracticeReviewClaimRecord Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
        reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
        reader.GetFieldValue<DateTimeOffset>(5), reader.GetString(6), reader.GetInt32(7),
        reader.GetString(8), reader.GetBoolean(9), reader.GetBoolean(10), reader.GetBoolean(11));

    private static void RequireReplay(
        TelehealthApplicantPracticeReviewClaimRecord record,
        string existingFingerprint,
        string actorId,
        string commandFingerprint)
    {
        if (!string.Equals(record.ActorId, actorId, StringComparison.Ordinal)
            || !string.Equals(existingFingerprint, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_practice_review_claim_idempotency_conflict",
                "The review-claim idempotency key was already used with different command content.");
        }
    }
}
