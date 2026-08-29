// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestComplaintTriageRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string ComplaintCategory,
    string ProtocolKey,
    string ContextSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string CurrentLocationStateCode,
    string CallbackPhoneLast4,
    Guid? AssessmentId,
    TelehealthTriageOutcome? Outcome,
    DateTimeOffset? EvaluatedAt);

internal sealed record TelehealthApplicantRequestComplaintTriageApplicant(
    Guid ApplicantId,
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestComplaintTriageContext(
    Guid ApplicantId,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestCreationId,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? RequestTriageOutcome,
    string CanonicalPatientId,
    string ComplaintCategory,
    Guid LocationConfirmationId,
    Guid LocationId,
    string CurrentLocationStateCode,
    string CallbackPhoneLast4,
    DateTimeOffset LocationConfirmedAt,
    Guid UniversalSafetyReceiptId,
    Guid UniversalSafetyAssessmentId,
    DateTimeOffset UniversalSafetyEvaluatedAt,
    DateTimeOffset ContextExpiresAt,
    int LocationCount,
    int LocationReceiptCount,
    int TriageCount,
    int UniversalSafetyReceiptCount,
    int ComplaintReceiptCount,
    int DownstreamCount,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestComplaintTriageRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestComplaintTriageRecord> GetAsync(
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
        RequireApplicant(applicant);

        var context = await LoadContextAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw ProvenanceConflict();
        var completed = await LoadAssessmentAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompletedContext(context, completed.Value.Record);
            return completed.Value.Record;
        }

        RequireReadyContext(context);
        var snapshot = CreateSnapshot(context);
        var protocol = TelehealthApplicantRequestComplaintTriagePolicy.ProtocolFor(context.ComplaintCategory);
        return new(
            applicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestComplaintTriagePolicy.ApplicantStatus,
            context.RequestId,
            context.RequestVersion,
            context.RequestStatus,
            context.ComplaintCategory,
            protocol.ProtocolKey,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            snapshot.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            null,
            null,
            null);
    }

    public async Task<TelehealthApplicantRequestComplaintTriageRecord> AssessAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestComplaintTriageAssessment assessment,
        SyntheticComplaintTriageResult result,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        RequireExactFixture(assessment, result);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireApplicant(applicant);

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        var replay = await LoadAssessmentAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_complaint_triage_idempotency_conflict",
                    "The idempotency key was already used with different complaint-triage content.");
            }
            RequireCompletedContext(context, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (context.ComplaintReceiptCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_already_completed",
                "Complaint-specific triage was already evaluated. Reload the current state.");
        }
        RequireReadyContext(context);
        if (!string.Equals(
                assessment.Answers.ComplaintCategory,
                context.ComplaintCategory,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_category_conflict",
                "The submitted answer set does not match the server-owned request complaint category.");
        }
        if (assessment.ExpectedRequestVersion != context.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_version_conflict",
                "The request changed. Reload complaint-specific triage before retrying.");
        }

        var snapshot = CreateSnapshot(context);
        if (!string.Equals(
                assessment.ContextSnapshotFingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_snapshot_conflict",
                "The request, location, universal safety, or complaint context changed. Reload before continuing.");
        }
        if (!string.Equals(
                assessment.CurrentLocationStateCode,
                context.CurrentLocationStateCode,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_location_changed",
                "The selected state differs from the confirmed request location. Stop and restart or request review.");
        }
        if (snapshot.ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_context_expired",
                "The request location and universal-safety context is too old. Restart or request review.");
        }

        var resultingStatus = TelehealthApplicantRequestComplaintTriagePolicy
            .ResultingRequestStatus(result.Outcome);
        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.SafetyScreening,
            Enum.Parse<TelehealthRequestStatus>(resultingStatus, false));
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set status=@resultingStatus,version=4,triage_outcome=@triageOutcome,updated_at=now()
                where request_id=@requestId and practice_id=@practiceId and facility_id=@facilityId
                  and source_applicant_id=@applicantId and complaint_category=@complaintCategory
                  and status='SafetyScreening' and version=3
                  and triage_outcome is null and ready_at is null and appointment_id is null;
                """;
            update.Parameters.AddWithValue("resultingStatus", resultingStatus);
            update.Parameters.AddWithValue("triageOutcome", result.Outcome.ToString());
            update.Parameters.AddWithValue("requestId", context.RequestId);
            update.Parameters.AddWithValue("practiceId", practiceId);
            update.Parameters.AddWithValue("facilityId", facilityId);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("complaintCategory", context.ComplaintCategory);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_complaint_triage_version_conflict",
                    "The request changed. Reload complaint-specific triage before retrying.");
            }
        }

        await EnsureProtocolAsync(connection, transaction, result, cancellationToken);
        var assessmentId = Guid.NewGuid();
        DateTimeOffset evaluatedAt;
        await using (var insertAssessment = connection.CreateCommand())
        {
            insertAssessment.Transaction = transaction;
            insertAssessment.CommandText = """
                insert into telehealth_triage_assessments(
                  assessment_id,request_id,protocol_id,answer_fingerprint,outcome,
                  request_version,idempotency_key,command_fingerprint)
                values(@assessmentId,@requestId,@protocolId,@answerFingerprint,@outcome,
                       4,@idempotencyKey,@commandFingerprint)
                returning evaluated_at;
                """;
            insertAssessment.Parameters.AddWithValue("assessmentId", assessmentId);
            insertAssessment.Parameters.AddWithValue("requestId", context.RequestId);
            insertAssessment.Parameters.AddWithValue("protocolId", result.ProtocolId);
            insertAssessment.Parameters.AddWithValue("answerFingerprint", result.AnswerFingerprint);
            insertAssessment.Parameters.AddWithValue("outcome", result.Outcome.ToString());
            insertAssessment.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insertAssessment.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insertAssessment.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic complaint-triage evaluation time was not returned.");
            }
            evaluatedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        if (evaluatedAt > snapshot.ContextExpiresAt || evaluatedAt >= context.ApplicantExpiresAt)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_context_expired",
                "The request safety context expired during evaluation. Restart or request review.");
        }

        await InsertRequestEventAsync(
            connection,
            transaction,
            context.RequestId,
            applicantId,
            resultingStatus,
            idempotencyKey,
            commandFingerprint,
            cancellationToken);

        var (answerKeys, answerValues) = AnswerEvidence(assessment.Answers);
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_applicant_request_complaint_triage_assessments(
                  receipt_id,assessment_id,request_id,applicant_id,request_creation_id,
                  location_confirmation_id,location_id,universal_safety_receipt_id,
                  universal_safety_assessment_id,practice_id,facility_id,canonical_patient_id,
                  applicant_version,source_request_version,resulting_request_version,
                  source_request_status,resulting_request_status,complaint_category,
                  current_location_state_code,callback_phone_last4,location_confirmed_at,
                  universal_safety_evaluated_at,context_expires_at,applicant_expires_at,
                  context_snapshot_fingerprint,current_location_confirmed,
                  callback_number_confirmed,synthetic_data_confirmed,answer_keys,answer_values,
                  protocol_id,protocol_key,protocol_version,protocol_content_hash,engine_version,
                  clinical_content_status,answers_fingerprint,fired_rule_codes,reason_codes,
                  outcome,public_disposition,policy_key,policy_version,evidence_type,
                  idempotency_key,command_fingerprint,synthetic_video_evaluation_candidate,
                  clinical_review_required,terminal_for_telehealth,evaluated_at)
                values(@receiptId,@assessmentId,@requestId,@applicantId,@requestCreationId,
                  @locationConfirmationId,@locationId,@universalSafetyReceiptId,
                  @universalSafetyAssessmentId,@practiceId,@facilityId,@patientId,
                  @applicantVersion,3,4,'SafetyScreening',@resultingStatus,@complaintCategory,
                  @stateCode,@callbackLast4,@locationConfirmedAt,@universalSafetyEvaluatedAt,
                  @contextExpiresAt,@applicantExpiresAt,@snapshotFingerprint,true,true,true,
                  @answerKeys,@answerValues,@protocolId,@protocolKey,@protocolVersion,
                  @protocolHash,@engineVersion,@clinicalContentStatus,@answersFingerprint,
                  @firedRules,@reasonCodes,@outcome,@publicDisposition,@policyKey,@policyVersion,
                  @evidenceType,@idempotencyKey,@commandFingerprint,@candidate,@clinicalReview,
                  @terminal,@evaluatedAt);
                """;
            receipt.Parameters.AddWithValue("receiptId", Guid.NewGuid());
            receipt.Parameters.AddWithValue("assessmentId", assessmentId);
            receipt.Parameters.AddWithValue("requestId", context.RequestId);
            receipt.Parameters.AddWithValue("applicantId", applicantId);
            receipt.Parameters.AddWithValue("requestCreationId", context.RequestCreationId);
            receipt.Parameters.AddWithValue("locationConfirmationId", context.LocationConfirmationId);
            receipt.Parameters.AddWithValue("locationId", context.LocationId);
            receipt.Parameters.AddWithValue("universalSafetyReceiptId", context.UniversalSafetyReceiptId);
            receipt.Parameters.AddWithValue("universalSafetyAssessmentId", context.UniversalSafetyAssessmentId);
            receipt.Parameters.AddWithValue("practiceId", practiceId);
            receipt.Parameters.AddWithValue("facilityId", facilityId);
            receipt.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            receipt.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            receipt.Parameters.AddWithValue("resultingStatus", resultingStatus);
            receipt.Parameters.AddWithValue("complaintCategory", context.ComplaintCategory);
            receipt.Parameters.AddWithValue("stateCode", context.CurrentLocationStateCode);
            receipt.Parameters.AddWithValue("callbackLast4", context.CallbackPhoneLast4);
            receipt.Parameters.AddWithValue("locationConfirmedAt", context.LocationConfirmedAt);
            receipt.Parameters.AddWithValue("universalSafetyEvaluatedAt", context.UniversalSafetyEvaluatedAt);
            receipt.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            receipt.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            receipt.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            receipt.Parameters.Add(new NpgsqlParameter("answerKeys", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = answerKeys
            });
            receipt.Parameters.Add(new NpgsqlParameter("answerValues", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = answerValues
            });
            receipt.Parameters.AddWithValue("protocolId", result.ProtocolId);
            receipt.Parameters.AddWithValue("protocolKey", result.ProtocolKey);
            receipt.Parameters.AddWithValue("protocolVersion", result.ProtocolVersion);
            receipt.Parameters.AddWithValue("protocolHash", result.ProtocolContentHash);
            receipt.Parameters.AddWithValue("engineVersion", result.EngineVersion);
            receipt.Parameters.AddWithValue("clinicalContentStatus", result.ClinicalContentStatus);
            receipt.Parameters.AddWithValue("answersFingerprint", result.AnswerFingerprint);
            receipt.Parameters.Add(new NpgsqlParameter("firedRules", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = result.FiredRuleCodes.ToArray()
            });
            receipt.Parameters.Add(new NpgsqlParameter("reasonCodes", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = result.ReasonCodes.ToArray()
            });
            receipt.Parameters.AddWithValue("outcome", result.Outcome.ToString());
            receipt.Parameters.AddWithValue(
                "publicDisposition",
                TelehealthApplicantRequestComplaintTriagePolicy.PublicDisposition(result.Outcome));
            receipt.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestComplaintTriagePolicy.PolicyKey);
            receipt.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestComplaintTriagePolicy.PolicyVersion);
            receipt.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestComplaintTriagePolicy.EvidenceType);
            receipt.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            receipt.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            receipt.Parameters.AddWithValue(
                "candidate",
                TelehealthApplicantRequestComplaintTriagePolicy.SyntheticVideoEvaluationCandidate(result.Outcome));
            receipt.Parameters.AddWithValue(
                "clinicalReview",
                TelehealthApplicantRequestComplaintTriagePolicy.ClinicalReviewRequired(result.Outcome));
            receipt.Parameters.AddWithValue(
                "terminal",
                TelehealthApplicantRequestComplaintTriagePolicy.TerminalForTelehealth(result.Outcome));
            receipt.Parameters.AddWithValue("evaluatedAt", evaluatedAt);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            applicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestComplaintTriagePolicy.ApplicantStatus,
            context.RequestId,
            TelehealthApplicantRequestComplaintTriagePolicy.ResultingRequestVersion,
            resultingStatus,
            context.ComplaintCategory,
            result.ProtocolKey,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            assessmentId,
            result.Outcome,
            evaluatedAt);
    }

    private static TelehealthApplicantRequestComplaintTriageSnapshot CreateSnapshot(
        TelehealthApplicantRequestComplaintTriageContext context) =>
        TelehealthApplicantRequestComplaintTriagePolicy.Snapshot(
            context.RequestId,
            context.RequestCreationId,
            context.LocationConfirmationId,
            context.LocationId,
            context.UniversalSafetyReceiptId,
            context.UniversalSafetyAssessmentId,
            TelehealthApplicantRequestComplaintTriagePolicy.EntryRequestVersion,
            context.ComplaintCategory,
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            context.UniversalSafetyEvaluatedAt,
            context.ContextExpiresAt,
            context.ApplicantExpiresAt);

    private static async Task<TelehealthApplicantRequestComplaintTriageApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestComplaintTriageContext?> LoadContextAsync(
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
            select a.applicant_id,a.version,a.expires_at,now(),creation.creation_id,
                   r.request_id,r.version,r.status,r.triage_outcome,creation.canonical_patient_id,
                   r.complaint_category,location_confirmation.confirmation_id,location.location_id,
                   location.state_code,location_confirmation.callback_phone_last4,
                   location.attested_at,universal.receipt_id,universal.assessment_id,
                   universal.evaluated_at,universal.context_expires_at,
                   (select count(*) from telehealth_patient_locations x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_location_confirmations x where x.request_id=r.request_id),
                   (select count(*) from telehealth_triage_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_universal_safety_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_complaint_triage_assessments x where x.request_id=r.request_id),
                   ((select count(*) from telehealth_patient_confirmations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_intake_snapshots x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_demonstration_acknowledgments x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   r.appointment_id is not null
            from telehealth_prospective_applicants a
            join telehealth_applicant_request_creations creation
              on creation.applicant_id=a.applicant_id and creation.practice_id=a.practice_id
             and creation.facility_id=a.facility_id and creation.resulting_applicant_version=a.version
             and creation.resulting_applicant_status=a.status
            join telehealth_requests r
              on r.request_id=creation.request_id and r.practice_id=a.practice_id
             and r.facility_id=a.facility_id and r.patient_id=creation.canonical_patient_id
             and r.source_applicant_id=a.applicant_id
             and r.source_promotion_id=creation.promotion_id
             and r.source_practice_review_case_id=creation.practice_review_case_id
             and r.source_practice_review_authorization_id=creation.practice_review_authorization_id
            join telehealth_applicant_request_location_confirmations location_confirmation
              on location_confirmation.request_id=r.request_id
             and location_confirmation.applicant_id=a.applicant_id
             and location_confirmation.request_creation_id=creation.creation_id
             and location_confirmation.practice_id=a.practice_id
             and location_confirmation.facility_id=a.facility_id
             and location_confirmation.canonical_patient_id=creation.canonical_patient_id
            join telehealth_patient_locations location
              on location.location_id=location_confirmation.location_id and location.request_id=r.request_id
            join telehealth_applicant_request_universal_safety_assessments universal
              on universal.request_id=r.request_id and universal.applicant_id=a.applicant_id
             and universal.request_creation_id=creation.creation_id
             and universal.location_confirmation_id=location_confirmation.confirmation_id
             and universal.location_id=location.location_id
             and universal.practice_id=a.practice_id and universal.facility_id=a.facility_id
             and universal.canonical_patient_id=creation.canonical_patient_id
            join telehealth_applicant_visit_purposes purpose
              on purpose.applicant_id=a.applicant_id and purpose.practice_id=a.practice_id
             and purpose.facility_id=a.facility_id and purpose.purpose_category=r.complaint_category
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id
             and patient.facility_id=a.facility_id and patient.lifecycle_status='active'
             and not patient.portal_enabled and patient.merged_into_patient_id is null
             and patient.first_name=a.legal_first_name and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code and patient.postal_code=a.postal_code
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated'
              and a.version=26 and a.expires_at>now()
              and creation.request_status='Draft' and creation.request_version=1
              and creation.policy_key='SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION'
              and creation.policy_version=1 and creation.telehealth_request_created
              and location_confirmation.resulting_request_status='LocationConfirmed'
              and location_confirmation.resulting_request_version=2
              and location_confirmation.policy_key='SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION'
              and location_confirmation.policy_version=1 and location_confirmation.location_confirmed
              and location.request_version=2 and location.state_code in ('GA','CA','FL')
              and location.state_code=location_confirmation.current_location_state_code
              and location.attested_at=location_confirmation.confirmed_at
              and universal.resulting_request_status='SafetyScreening'
              and universal.resulting_request_version=3
              and universal.outcome='TelehealthEligible'
              and universal.public_disposition='UniversalSafetyPassed'
              and universal.universal_safety_passed
              and universal.complaint_specific_triage_required
              and not universal.complaint_specific_triage_created
              and not universal.clinical_review_required and not universal.terminal_for_telehealth
              and universal.policy_key='SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'
              and universal.policy_version=1
              and universal.current_location_state_code=location.state_code
              and universal.callback_phone_last4=location_confirmation.callback_phone_last4
              and universal.location_confirmed_at=location.attested_at
              and purpose.purpose_category in ('migraine','sleep')
              and r.complaint_category=creation.complaint_category
              and r.status in ('SafetyScreening','EmergencyRedirected','InPersonRecommended',
                               'Unsupported','ClinicalReview','Intake')
              and r.version in (3,4) and r.ready_at is null
              and not exists(select 1 from insurance_records x where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from medications x where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from prescriptions x where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from allergies x where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from problems x where lower(x.patient_id)=lower(patient.canonical_id))
            {(forUpdate ? "for update of a,r,patient" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)),
                reader.GetFieldValue<DateTimeOffset>(2), reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetGuid(4), reader.GetGuid(5), Convert.ToInt32(reader.GetInt64(6)),
                reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetGuid(11), reader.GetGuid(12),
                reader.GetString(13), reader.GetString(14), reader.GetFieldValue<DateTimeOffset>(15),
                reader.GetGuid(16), reader.GetGuid(17), reader.GetFieldValue<DateTimeOffset>(18),
                reader.GetFieldValue<DateTimeOffset>(19), Convert.ToInt32(reader.GetInt64(20)),
                Convert.ToInt32(reader.GetInt64(21)), Convert.ToInt32(reader.GetInt64(22)),
                Convert.ToInt32(reader.GetInt64(23)), Convert.ToInt32(reader.GetInt64(24)),
                Convert.ToInt32(reader.GetInt64(25)), reader.GetBoolean(26))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestComplaintTriageRecord Record,
        string CommandFingerprint)?> LoadAssessmentAsync(
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
            select receipt.applicant_id,receipt.applicant_version,a.status,
                   receipt.request_id,receipt.resulting_request_version,
                   receipt.resulting_request_status,receipt.complaint_category,
                   receipt.protocol_key,receipt.context_snapshot_fingerprint,
                   receipt.context_expires_at,receipt.current_location_state_code,
                   receipt.callback_phone_last4,receipt.assessment_id,receipt.outcome,
                   receipt.evaluated_at,receipt.command_fingerprint
            from telehealth_applicant_request_complaint_triage_assessments receipt
            join telehealth_prospective_applicants a on a.applicant_id=receipt.applicant_id
            where receipt.applicant_id=@applicantId
              and receipt.practice_id=@practiceId and receipt.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and receipt.idempotency_key=@idempotencyKey")};
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
            reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
            reader.GetString(6), reader.GetString(7), reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9), reader.GetString(10), reader.GetString(11),
            reader.GetGuid(12), Enum.Parse<TelehealthTriageOutcome>(reader.GetString(13), false),
            reader.GetFieldValue<DateTimeOffset>(14)), reader.GetString(15));
    }

    private static async Task EnsureProtocolAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SyntheticComplaintTriageResult result,
        CancellationToken cancellationToken)
    {
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_protocol_versions(
                  protocol_id,protocol_key,protocol_version,content_hash,is_synthetic,published_at)
                values(@protocolId,@protocolKey,@protocolVersion,@protocolHash,true,
                       timestamptz '2026-08-28 00:00:00+00')
                on conflict do nothing;
                """;
            insert.Parameters.AddWithValue("protocolId", result.ProtocolId);
            insert.Parameters.AddWithValue("protocolKey", result.ProtocolKey);
            insert.Parameters.AddWithValue("protocolVersion", result.ProtocolVersion);
            insert.Parameters.AddWithValue("protocolHash", result.ProtocolContentHash);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var verify = connection.CreateCommand();
        verify.Transaction = transaction;
        verify.CommandText = """
            select count(*) from telehealth_protocol_versions
            where protocol_id=@protocolId and protocol_key=@protocolKey
              and protocol_version=@protocolVersion and content_hash=@protocolHash
              and is_synthetic;
            """;
        verify.Parameters.AddWithValue("protocolId", result.ProtocolId);
        verify.Parameters.AddWithValue("protocolKey", result.ProtocolKey);
        verify.Parameters.AddWithValue("protocolVersion", result.ProtocolVersion);
        verify.Parameters.AddWithValue("protocolHash", result.ProtocolContentHash);
        if (Convert.ToInt32(await verify.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_protocol_conflict",
                "The synthetic complaint-triage fixture changed. Stop and request review.");
        }
    }

    private static async Task InsertRequestEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        Guid applicantId,
        string resultingStatus,
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
            values(@eventId,@requestId,4,'applicant-complaint-triage-evaluated',
                   'SafetyScreening',@resultingStatus,'applicant',@actorId,
                   @idempotencyKey,@commandFingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("resultingStatus", resultingStatus);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (string[] Keys, string[] Values) AnswerEvidence(
        SyntheticComplaintTriageAnswers answers) => answers.ComplaintCategory switch
        {
            "migraine" when answers.Migraine is not null =>
            (
                [
                    "SuddenOrWorstOnset", "NewNeurologicOrVisionChange", "FeverOrStiffNeck",
                "RecentHeadInjury", "PregnantOrPostpartum", "CancerOrImmunocompromised",
                "KnownSimilarPattern", "PersistentVomiting"
                ],
                [
                    answers.Migraine.SuddenOrWorstOnset.ToString(),
                answers.Migraine.NewNeurologicOrVisionChange.ToString(),
                answers.Migraine.FeverOrStiffNeck.ToString(),
                answers.Migraine.RecentHeadInjury.ToString(),
                answers.Migraine.PregnantOrPostpartum.ToString(),
                answers.Migraine.CancerOrImmunocompromised.ToString(),
                answers.Migraine.KnownSimilarPattern.ToString(),
                answers.Migraine.PersistentVomiting.ToString()
                ]
            ),
            "sleep" when answers.Sleep is not null =>
            (
                [
                    "SelfHarmThoughts", "ManiaOrPsychosis", "DangerousSomnolence",
                "WithdrawalConcern", "BreathingPausesOrSevereSnoring",
                "PregnantOrComplexMedicationConcern", "ControlledSedativeRequest",
                "UncomplicatedSleepDifficulty"
                ],
                [
                    answers.Sleep.SelfHarmThoughts.ToString(),
                answers.Sleep.ManiaOrPsychosis.ToString(),
                answers.Sleep.DangerousSomnolence.ToString(),
                answers.Sleep.WithdrawalConcern.ToString(),
                answers.Sleep.BreathingPausesOrSevereSnoring.ToString(),
                answers.Sleep.PregnantOrComplexMedicationConcern.ToString(),
                answers.Sleep.ControlledSedativeRequest.ToString(),
                answers.Sleep.UncomplicatedSleepDifficulty.ToString()
                ]
            ),
            _ => throw new InvalidOperationException("Complaint answer evidence is inconsistent.")
        };

    private static void RequireExactFixture(
        NormalizedTelehealthApplicantRequestComplaintTriageAssessment assessment,
        SyntheticComplaintTriageResult result)
    {
        TelehealthApplicantRequestComplaintTriagePolicy.RequirePublicationBlocked(result);
        var protocol = TelehealthApplicantRequestComplaintTriagePolicy
            .ProtocolFor(assessment.Answers.ComplaintCategory);
        var expectedAnswers = AnswerEvidence(assessment.Answers).Values;
        var expectedAnswerFingerprint = TelehealthCommandFingerprint.Create(
            expectedAnswers.Cast<object?>().ToArray());
        if (result.ProtocolId != protocol.ProtocolId
            || result.ProtocolKey != protocol.ProtocolKey
            || result.ProtocolVersion != SyntheticTelehealthComplaintTriageEvaluator.ProtocolVersion
            || result.ProtocolContentHash != protocol.ProtocolContentHash
            || result.EngineVersion != SyntheticTelehealthComplaintTriageEvaluator.EngineVersion
            || result.ClinicalContentStatus != TelehealthApplicantRequestComplaintTriagePolicy.ClinicalContentStatus
            || result.AnswerFingerprint != expectedAnswerFingerprint
            || result.FiredRuleCodes.Count == 0
            || result.FiredRuleCodes.Count != result.ReasonCodes.Count)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_protocol_conflict",
                "The synthetic complaint-triage fixture changed. Stop and request review.");
        }
    }

    private static void RequireAccess(
        TelehealthApplicantRequestComplaintTriageApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestComplaintTriageApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestComplaintTriagePolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestComplaintTriagePolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_state_conflict",
                "The applicant is not eligible for complaint-specific triage.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestComplaintTriageContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestComplaintTriagePolicy.EntryRequestStatus
            || context.RequestVersion != TelehealthApplicantRequestComplaintTriagePolicy.EntryRequestVersion
            || context.RequestTriageOutcome is not null
            || context.LocationCount != 1
            || context.LocationReceiptCount != 1
            || context.TriageCount != 1
            || context.UniversalSafetyReceiptCount != 1
            || context.ComplaintReceiptCount != 0
            || context.DownstreamCount != 0
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
        if (CreateSnapshot(context).ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_complaint_triage_context_expired",
                "The request location and universal-safety context is too old. Restart or request review.");
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestComplaintTriageContext context,
        TelehealthApplicantRequestComplaintTriageRecord record)
    {
        if (record.Outcome is null
            || context.RequestStatus != TelehealthApplicantRequestComplaintTriagePolicy
                .ResultingRequestStatus(record.Outcome.Value)
            || context.RequestVersion != TelehealthApplicantRequestComplaintTriagePolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != record.Outcome.ToString()
            || context.ComplaintCategory != record.ComplaintCategory
            || context.LocationCount != 1
            || context.LocationReceiptCount != 1
            || context.TriageCount != 2
            || context.UniversalSafetyReceiptCount != 1
            || context.ComplaintReceiptCount != 1
            || context.DownstreamCount != 0
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_complaint_triage_provenance_conflict",
        "The complaint-triage context or its authorized source evidence is unavailable or changed.");
}
