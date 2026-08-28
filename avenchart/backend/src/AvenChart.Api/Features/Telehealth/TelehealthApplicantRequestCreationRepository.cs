// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestCreationRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid? RequestId,
    string? RequestStatus,
    int? RequestVersion,
    string ComplaintCategory,
    DateTimeOffset? CreatedAt);

internal sealed record TelehealthApplicantRequestCreationApplicant(
    Guid ApplicantId,
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestCreationCandidate(
    Guid ApplicantId,
    int Version,
    string CanonicalPatientId,
    Guid PromotionId,
    Guid CaseId,
    Guid AuthorizationId,
    string ComplaintCategory);

public sealed class TelehealthApplicantRequestCreationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestCreationRecord> GetAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireUnexpired(applicant);

        var created = await LoadCreationAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (created is not null)
        {
            RequireCreatedState(applicant, created.Value.Record);
            return created.Value.Record;
        }

        if (applicant.Status != TelehealthApplicantRequestCreationPolicy.EntryStatus)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_state_conflict",
                "The applicant is not eligible for this synthetic request-creation step.");
        }
        var candidate = await LoadCandidateAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_provenance_conflict",
                "The authorized request-creation evidence is unavailable or changed.");
        return new(
            applicantId,
            candidate.Version,
            TelehealthApplicantRequestCreationPolicy.EntryStatus,
            null,
            null,
            null,
            candidate.ComplaintCategory,
            null);
    }

    public async Task<TelehealthApplicantRequestCreationRecord> CreateAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestCreation request,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireUnexpired(applicant);

        var replay = await LoadCreationAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_creation_idempotency_conflict",
                    "The idempotency key was already used with different command content.");
            }
            RequireCreatedState(applicant, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (await HasCreationAsync(connection, transaction, applicantId, cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_already_completed",
                "This applicant already created the synthetic telehealth request. Reload the current state.");
        }
        if (applicant.Status != TelehealthApplicantRequestCreationPolicy.EntryStatus)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_state_conflict",
                "The applicant is not eligible for this synthetic request-creation step.");
        }
        if (applicant.Version != request.ExpectedApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_version_conflict",
                "The applicant changed. Reload the authorized request-creation step before retrying.");
        }

        var candidate = await LoadCandidateAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_provenance_conflict",
                "The authorized request-creation evidence is unavailable or changed.");
        if (candidate.Version != request.ExpectedApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_version_conflict",
                "The applicant changed. Reload the authorized request-creation step before retrying.");
        }

        var nextVersion = candidate.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPracticeReviewAuthorized';
                """;
            update.Parameters.AddWithValue("nextStatus", TelehealthApplicantRequestCreationPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", candidate.Version);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_creation_version_conflict",
                    "The applicant changed. Reload the authorized request-creation step before retrying.");
            }
        }

        var requestId = Guid.NewGuid();
        DateTimeOffset createdAt;
        await using (var insertRequest = connection.CreateCommand())
        {
            insertRequest.Transaction = transaction;
            insertRequest.CommandText = """
                insert into telehealth_requests(
                  request_id,practice_id,facility_id,patient_id,status,complaint_category,
                  version,create_idempotency_key,create_fingerprint,source_applicant_id,
                  source_promotion_id,source_practice_review_case_id,
                  source_practice_review_authorization_id)
                values(@requestId,@practiceId,@facilityId,@patientId,'Draft',@complaintCategory,
                       1,@idempotencyKey,@commandFingerprint,@applicantId,@promotionId,@caseId,
                       @authorizationId)
                returning created_at;
                """;
            insertRequest.Parameters.AddWithValue("requestId", requestId);
            insertRequest.Parameters.AddWithValue("practiceId", practiceId);
            insertRequest.Parameters.AddWithValue("facilityId", facilityId);
            insertRequest.Parameters.AddWithValue("patientId", candidate.CanonicalPatientId);
            insertRequest.Parameters.AddWithValue("complaintCategory", candidate.ComplaintCategory);
            insertRequest.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insertRequest.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            insertRequest.Parameters.AddWithValue("applicantId", applicantId);
            insertRequest.Parameters.AddWithValue("promotionId", candidate.PromotionId);
            insertRequest.Parameters.AddWithValue("caseId", candidate.CaseId);
            insertRequest.Parameters.AddWithValue("authorizationId", candidate.AuthorizationId);
            await using var reader = await insertRequest.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic request creation time was not returned.");
            }
            createdAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await InsertRequestEventAsync(
            connection, transaction, requestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);

        var creationId = Guid.NewGuid();
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_applicant_request_creations(
                  creation_id,request_id,applicant_id,practice_id,facility_id,
                  canonical_patient_id,promotion_id,practice_review_case_id,
                  practice_review_authorization_id,source_applicant_version,
                  resulting_applicant_version,resulting_applicant_status,complaint_category,
                  request_status,request_version,authorization_policy_version,
                  request_creation_confirmed,no_queue_or_care_acknowledged,
                  urgent_or_worsening_action_acknowledged,
                  policy_key,policy_version,evidence_type,idempotency_key,command_fingerprint,
                  created_at)
                values(@creationId,@requestId,@applicantId,@practiceId,@facilityId,@patientId,
                       @promotionId,@caseId,@authorizationId,@sourceVersion,@nextVersion,@nextStatus,
                       @complaintCategory,'Draft',1,@authorizationPolicyVersion,true,true,true,
                       @policyKey,@policyVersion,@evidenceType,@idempotencyKey,@commandFingerprint,
                       @createdAt);
                """;
            receipt.Parameters.AddWithValue("creationId", creationId);
            receipt.Parameters.AddWithValue("requestId", requestId);
            receipt.Parameters.AddWithValue("applicantId", applicantId);
            receipt.Parameters.AddWithValue("practiceId", practiceId);
            receipt.Parameters.AddWithValue("facilityId", facilityId);
            receipt.Parameters.AddWithValue("patientId", candidate.CanonicalPatientId);
            receipt.Parameters.AddWithValue("promotionId", candidate.PromotionId);
            receipt.Parameters.AddWithValue("caseId", candidate.CaseId);
            receipt.Parameters.AddWithValue("authorizationId", candidate.AuthorizationId);
            receipt.Parameters.AddWithValue("sourceVersion", candidate.Version);
            receipt.Parameters.AddWithValue("nextVersion", nextVersion);
            receipt.Parameters.AddWithValue("nextStatus", TelehealthApplicantRequestCreationPolicy.ResultingStatus);
            receipt.Parameters.AddWithValue("complaintCategory", candidate.ComplaintCategory);
            receipt.Parameters.AddWithValue("authorizationPolicyVersion", request.AuthorizationPolicyVersion);
            receipt.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestCreationPolicy.PolicyKey);
            receipt.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestCreationPolicy.PolicyVersion);
            receipt.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestCreationPolicy.EvidenceType);
            receipt.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            receipt.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            receipt.Parameters.AddWithValue("createdAt", createdAt);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var applicantEvent = connection.CreateCommand())
        {
            applicantEvent.Transaction = transaction;
            applicantEvent.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,'prospective-telehealth-request-created',
                       'SyntheticPracticeReviewAuthorized','SyntheticRequestCreated','applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            applicantEvent.Parameters.AddWithValue("eventId", Guid.NewGuid());
            applicantEvent.Parameters.AddWithValue("applicantId", applicantId);
            applicantEvent.Parameters.AddWithValue("nextVersion", nextVersion);
            applicantEvent.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "request-creation:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            applicantEvent.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await applicantEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            applicantId,
            nextVersion,
            TelehealthApplicantRequestCreationPolicy.ResultingStatus,
            requestId,
            "Draft",
            1,
            candidate.ComplaintCategory,
            createdAt);
    }

    private static async Task<TelehealthApplicantRequestCreationApplicant?> LoadApplicantAsync(
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
            select applicant_id,version,status,access_key_hash,expires_at,now()
            from telehealth_prospective_applicants
            where applicant_id=@applicantId and practice_id=@practiceId and facility_id=@facilityId
            {(forUpdate ? "for update" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5))
            : null;
    }

    private static async Task<TelehealthApplicantRequestCreationCandidate?> LoadCandidateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool lockPatient,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select a.applicant_id,a.version,patient.canonical_id,promotion.promotion_id,
                   c.case_id,authz.authorization_id,purpose.purpose_category
            from telehealth_prospective_applicants a
            join telehealth_practice_review_authorizations authz
              on authz.applicant_id=a.applicant_id
             and authz.practice_id=a.practice_id
             and authz.facility_id=a.facility_id
             and authz.resulting_applicant_version=a.version
             and authz.resulting_applicant_status=a.status
            join telehealth_prospective_practice_review_cases c
              on c.case_id=authz.case_id and c.applicant_id=a.applicant_id
             and c.practice_id=a.practice_id and c.facility_id=a.facility_id
             and c.canonical_patient_id=authz.canonical_patient_id
             and c.readiness_acknowledgment_id=authz.readiness_acknowledgment_id
            join telehealth_applicant_practice_review_submissions submission
              on submission.submission_id=authz.submission_id
             and submission.case_id=c.case_id and submission.applicant_id=a.applicant_id
             and submission.practice_id=a.practice_id and submission.facility_id=a.facility_id
             and submission.canonical_patient_id=c.canonical_patient_id
             and submission.readiness_acknowledgment_id=c.readiness_acknowledgment_id
            join telehealth_applicant_pre_request_readiness_acknowledgments readiness
              on readiness.acknowledgment_id=authz.readiness_acknowledgment_id
             and readiness.applicant_id=a.applicant_id and readiness.practice_id=a.practice_id
             and readiness.facility_id=a.facility_id and readiness.canonical_patient_id=c.canonical_patient_id
            join telehealth_applicant_synthetic_promotions promotion
              on promotion.promotion_id=readiness.promotion_id
             and promotion.applicant_id=a.applicant_id and promotion.practice_id=a.practice_id
             and promotion.facility_id=a.facility_id
             and promotion.canonical_patient_id=c.canonical_patient_id
             and promotion.outcome='SyntheticPatientCreated' and promotion.canonical_patient_created
            join patients patient
              on patient.canonical_id=c.canonical_patient_id and patient.facility_id=a.facility_id
             and not patient.portal_enabled and patient.merged_into_patient_id is null
             and patient.lifecycle_status='active'
             and patient.first_name=a.legal_first_name and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code and patient.postal_code=a.postal_code
            join telehealth_applicant_visit_purposes purpose
              on purpose.applicant_id=a.applicant_id and purpose.practice_id=a.practice_id
             and purpose.facility_id=a.facility_id
            join telehealth_applicant_safety_triage_evaluations safety
              on safety.evaluation_id=purpose.safety_triage_evaluation_id
             and safety.applicant_id=a.applicant_id and safety.practice_id=a.practice_id
             and safety.facility_id=a.facility_id and safety.outcome=purpose.source_safety_outcome
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticPracticeReviewAuthorized'
              and a.expires_at>now() and c.applicant_expires_at=a.expires_at
              and submission.applicant_expires_at=a.expires_at
              and c.case_status='PendingPracticeReview'
              and authz.decision='AuthorizedForSyntheticRequestCreation'
              and authz.rationale_code='OperationalPrerequisitesReviewed'
              and authz.policy_key='SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION'
              and authz.policy_version=1 and authz.request_creation_authorized
              and not authz.practice_accepted and not authz.patient_contacted
              and not authz.clinician_review_created
              and not authz.telehealth_request_created
              and not authz.patient_care_queue_entered
              and not authz.clinician_queue_entered
              and not authz.appointment_created and not authz.encounter_created
              and not authz.consent_created and not authz.care_authorized
              and not authz.prescribing_enabled and not authz.billing_enabled
              and not authz.claim_created and not authz.integration_enabled
              and not authz.external_call_performed
              and submission.resulting_applicant_status='SyntheticPracticeReviewSubmitted'
              and submission.resulting_applicant_version=authz.source_applicant_version
              and submission.policy_key='SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION'
              and submission.policy_version=1 and submission.staff_review_created
              and not submission.telehealth_request_created and not submission.patient_care_queue_entered
              and not submission.clinician_queue_entered and not submission.appointment_created
              and not submission.encounter_created and not submission.care_authorized
              and not submission.prescribing_enabled and not submission.billing_enabled
              and not submission.claim_created and not submission.integration_enabled
              and not submission.external_call_performed
              and readiness.resulting_applicant_status='SyntheticPreRequestReadinessAcknowledged'
              and readiness.resulting_applicant_version=submission.resulting_applicant_version-1
              and readiness.policy_key='SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS'
              and readiness.policy_version=1
              and purpose.purpose_category in ('migraine','sleep')
              and safety.outcome='TelehealthEligible'
              and not exists(select 1 from telehealth_applicant_request_creations x
                             where x.applicant_id=a.applicant_id)
              and not exists(select 1 from telehealth_requests x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from insurance_records x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from medications x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from prescriptions x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from allergies x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from problems x
                             where lower(x.patient_id)=lower(patient.canonical_id))
            {(lockPatient ? "for update of patient" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetString(6))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestCreationRecord Record,
        string CommandFingerprint)?> LoadCreationAsync(
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
            select c.applicant_id,c.resulting_applicant_version,c.resulting_applicant_status,
                   c.request_id,c.request_status,c.request_version,c.complaint_category,c.created_at,
                   c.command_fingerprint
            from telehealth_applicant_request_creations c
            join telehealth_requests r on r.request_id=c.request_id
             and r.source_applicant_id=c.applicant_id
             and r.source_promotion_id=c.promotion_id
             and r.source_practice_review_case_id=c.practice_review_case_id
             and r.source_practice_review_authorization_id=c.practice_review_authorization_id
            where c.applicant_id=@applicantId and c.practice_id=@practiceId
              and c.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and c.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null)
        {
            command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return (new(
            reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
            reader.GetGuid(3), reader.GetString(4), Convert.ToInt32(reader.GetInt64(5)),
            reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)), reader.GetString(8));
    }

    private static async Task<bool> HasCreationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from telehealth_applicant_request_creations where applicant_id=@applicantId);";
        command.Parameters.AddWithValue("applicantId", applicantId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
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
            values(@eventId,@requestId,1,'applicant-request-created',null,'Draft','applicant',
                   @actorId,@idempotencyKey,@commandFingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestCreationApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(
                applicant.AccessKeyHash,
                accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireUnexpired(TelehealthApplicantRequestCreationApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
    }

    private static void RequireCreatedState(
        TelehealthApplicantRequestCreationApplicant applicant,
        TelehealthApplicantRequestCreationRecord created)
    {
        if (applicant.Status != TelehealthApplicantRequestCreationPolicy.ResultingStatus
            || applicant.Version != created.ApplicantVersion
            || created.RequestStatus != "Draft"
            || created.RequestVersion != 1)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_creation_provenance_conflict",
                "The synthetic request-creation evidence is unavailable or changed.");
        }
    }
}
