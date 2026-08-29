// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestIntakeRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string ComplaintCategory,
    string ContextSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string CurrentLocationStateCode,
    string CallbackPhoneLast4,
    Guid? ReceiptId,
    string? SymptomDuration,
    DateTimeOffset? CapturedAt);

internal sealed record TelehealthApplicantRequestIntakeApplicant(
    Guid ApplicantId,
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestIntakeContext(
    Guid ApplicantId,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestCreationId,
    Guid PromotionId,
    Guid PracticeReviewCaseId,
    Guid PracticeReviewAuthorizationId,
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
    Guid ComplaintTriageReceiptId,
    Guid ComplaintTriageAssessmentId,
    DateTimeOffset ComplaintEvaluatedAt,
    DateTimeOffset ContextExpiresAt,
    string ProtocolKey,
    int ProtocolVersion,
    string ProtocolContentHash,
    string ClinicalContentStatus,
    bool MedicalDirectorApprovalRecorded,
    bool ClinicalGoldenCasePackApproved,
    bool ProductionPublicationAllowed,
    string ComplaintContextFingerprint,
    string ComplaintCommandFingerprint,
    int LocationCount,
    int LocationReceiptCount,
    int TriageCount,
    int UniversalSafetyReceiptCount,
    int ComplaintReceiptCount,
    int IntakeCount,
    int IntakeReceiptCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestIntakeRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestIntakeRecord> GetAsync(
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
        var completed = await LoadReceiptAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompletedContext(context, completed.Value.Record);
            return completed.Value.Record;
        }

        RequireReadyContext(context);
        return CreateRecord(context, null, null, null);
    }

    public async Task<TelehealthApplicantRequestIntakeRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestIntakeConfirmation confirmation,
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
        RequireApplicant(applicant);

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        var replay = await LoadReceiptAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_intake_idempotency_conflict",
                    "The idempotency key was already used with different intake content.");
            }
            RequireCompletedContext(context, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (context.IntakeCount != 0 || context.IntakeReceiptCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_already_completed",
                "The request intake snapshot was already confirmed. Reload the current state.");
        }
        RequireReadyContext(context);
        if (confirmation.ExpectedRequestVersion != context.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_version_conflict",
                "The request changed. Reload intake confirmation before retrying.");
        }

        var snapshot = CreateSnapshot(context);
        if (!string.Equals(
                confirmation.ContextSnapshotFingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_snapshot_conflict",
                "The request or its authorized source evidence changed. Reload before continuing.");
        }
        if (!string.Equals(
                confirmation.CurrentLocationStateCode,
                context.CurrentLocationStateCode,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_location_changed",
                "The selected state differs from the confirmed request location. Stop and restart or request review.");
        }
        if (snapshot.ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_context_expired",
                "The request safety context is too old. Restart or request review.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.Intake,
            TelehealthRequestStatus.Verification);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set status='Verification',version=5,updated_at=now()
                where request_id=@requestId and practice_id=@practiceId and facility_id=@facilityId
                  and source_applicant_id=@applicantId and complaint_category=@complaintCategory
                  and status='Intake' and version=4 and triage_outcome='TelehealthEligible'
                  and ready_at is null and appointment_id is null;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            update.Parameters.AddWithValue("practiceId", practiceId);
            update.Parameters.AddWithValue("facilityId", facilityId);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("complaintCategory", context.ComplaintCategory);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_intake_version_conflict",
                    "The request changed. Reload intake confirmation before retrying.");
            }
        }

        var intakeId = Guid.NewGuid();
        DateTimeOffset capturedAt;
        await using (var intake = connection.CreateCommand())
        {
            intake.Transaction = transaction;
            intake.CommandText = """
                insert into telehealth_intake_snapshots(
                  intake_id,request_id,complaint_summary,symptom_duration,
                  synthetic_data_confirmed,request_version,idempotency_key,command_fingerprint)
                values(@intakeId,@requestId,@summary,@duration,true,5,@idempotencyKey,
                       @commandFingerprint)
                returning captured_at;
                """;
            intake.Parameters.AddWithValue("intakeId", intakeId);
            intake.Parameters.AddWithValue("requestId", context.RequestId);
            intake.Parameters.AddWithValue("summary", snapshot.ComplaintSummary);
            intake.Parameters.AddWithValue("duration", confirmation.SymptomDuration);
            intake.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            intake.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await intake.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic applicant intake capture time was not returned.");
            }
            capturedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        if (capturedAt > snapshot.ContextExpiresAt || capturedAt >= context.ApplicantExpiresAt)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_context_expired",
                "The request safety context expired during intake confirmation. Restart or request review.");
        }

        await InsertRequestEventAsync(
            connection,
            transaction,
            context.RequestId,
            applicantId,
            idempotencyKey,
            commandFingerprint,
            cancellationToken);

        var receiptId = Guid.NewGuid();
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_applicant_request_intake_snapshots(
                  receipt_id,intake_id,request_id,applicant_id,request_creation_id,
                  location_confirmation_id,location_id,universal_safety_receipt_id,
                  complaint_triage_receipt_id,complaint_triage_assessment_id,
                  promotion_id,practice_review_case_id,practice_review_authorization_id,
                  practice_id,facility_id,canonical_patient_id,applicant_version,
                  source_request_version,resulting_request_version,source_request_status,
                  resulting_request_status,complaint_category,complaint_outcome,
                  complaint_summary,symptom_duration,current_location_state_code,
                  callback_phone_last4,location_confirmed_at,complaint_evaluated_at,
                  context_expires_at,applicant_expires_at,context_snapshot_fingerprint,
                  source_complaint_context_fingerprint,protocol_key,protocol_version,
                  protocol_content_hash,clinical_content_status,
                  current_location_confirmed,callback_number_confirmed,
                  prior_information_reviewed,insurance_limitations_acknowledged,
                  pending_consent_acknowledged,pending_verification_acknowledged,
                  complaint_result_acknowledged,synthetic_data_confirmed,
                  policy_key,policy_version,evidence_type,idempotency_key,
                  command_fingerprint,captured_at)
                values(@receiptId,@intakeId,@requestId,@applicantId,@requestCreationId,
                  @locationConfirmationId,@locationId,@universalSafetyReceiptId,
                  @complaintReceiptId,@complaintAssessmentId,@promotionId,@reviewCaseId,
                  @reviewAuthorizationId,@practiceId,@facilityId,@patientId,@applicantVersion,
                  4,5,'Intake','Verification',@complaintCategory,'TelehealthEligible',
                  @summary,@duration,@stateCode,@callbackLast4,@locationConfirmedAt,
                  @complaintEvaluatedAt,@contextExpiresAt,@applicantExpiresAt,
                  @snapshotFingerprint,@sourceComplaintFingerprint,@protocolKey,
                  @protocolVersion,@protocolHash,@clinicalContentStatus,true,true,true,true,
                  true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @idempotencyKey,@commandFingerprint,@capturedAt);
                """;
            receipt.Parameters.AddWithValue("receiptId", receiptId);
            receipt.Parameters.AddWithValue("intakeId", intakeId);
            receipt.Parameters.AddWithValue("requestId", context.RequestId);
            receipt.Parameters.AddWithValue("applicantId", applicantId);
            receipt.Parameters.AddWithValue("requestCreationId", context.RequestCreationId);
            receipt.Parameters.AddWithValue("locationConfirmationId", context.LocationConfirmationId);
            receipt.Parameters.AddWithValue("locationId", context.LocationId);
            receipt.Parameters.AddWithValue("universalSafetyReceiptId", context.UniversalSafetyReceiptId);
            receipt.Parameters.AddWithValue("complaintReceiptId", context.ComplaintTriageReceiptId);
            receipt.Parameters.AddWithValue("complaintAssessmentId", context.ComplaintTriageAssessmentId);
            receipt.Parameters.AddWithValue("promotionId", context.PromotionId);
            receipt.Parameters.AddWithValue("reviewCaseId", context.PracticeReviewCaseId);
            receipt.Parameters.AddWithValue("reviewAuthorizationId", context.PracticeReviewAuthorizationId);
            receipt.Parameters.AddWithValue("practiceId", practiceId);
            receipt.Parameters.AddWithValue("facilityId", facilityId);
            receipt.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            receipt.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            receipt.Parameters.AddWithValue("complaintCategory", context.ComplaintCategory);
            receipt.Parameters.AddWithValue("summary", snapshot.ComplaintSummary);
            receipt.Parameters.AddWithValue("duration", confirmation.SymptomDuration);
            receipt.Parameters.AddWithValue("stateCode", context.CurrentLocationStateCode);
            receipt.Parameters.AddWithValue("callbackLast4", context.CallbackPhoneLast4);
            receipt.Parameters.AddWithValue("locationConfirmedAt", context.LocationConfirmedAt);
            receipt.Parameters.AddWithValue("complaintEvaluatedAt", context.ComplaintEvaluatedAt);
            receipt.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            receipt.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            receipt.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            receipt.Parameters.AddWithValue("sourceComplaintFingerprint", context.ComplaintContextFingerprint);
            receipt.Parameters.AddWithValue("protocolKey", context.ProtocolKey);
            receipt.Parameters.AddWithValue("protocolVersion", context.ProtocolVersion);
            receipt.Parameters.AddWithValue("protocolHash", context.ProtocolContentHash);
            receipt.Parameters.AddWithValue("clinicalContentStatus", context.ClinicalContentStatus);
            receipt.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestIntakePolicy.PolicyKey);
            receipt.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestIntakePolicy.PolicyVersion);
            receipt.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestIntakePolicy.EvidenceType);
            receipt.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            receipt.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            receipt.Parameters.AddWithValue("capturedAt", capturedAt);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(context, receiptId, confirmation.SymptomDuration, capturedAt);
    }

    private static TelehealthApplicantRequestIntakeRecord CreateRecord(
        TelehealthApplicantRequestIntakeContext context,
        Guid? receiptId,
        string? symptomDuration,
        DateTimeOffset? capturedAt)
    {
        var snapshot = CreateSnapshot(context);
        return new(
            context.ApplicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestIntakePolicy.ApplicantStatus,
            context.RequestId,
            receiptId is null ? context.RequestVersion : TelehealthApplicantRequestIntakePolicy.ResultingRequestVersion,
            receiptId is null ? context.RequestStatus : TelehealthApplicantRequestIntakePolicy.ResultingRequestStatus,
            context.ComplaintCategory,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            receiptId,
            symptomDuration,
            capturedAt);
    }

    private static TelehealthApplicantRequestIntakeSnapshot CreateSnapshot(
        TelehealthApplicantRequestIntakeContext context) =>
        TelehealthApplicantRequestIntakePolicy.Snapshot(
            context.RequestId,
            context.RequestCreationId,
            context.LocationConfirmationId,
            context.LocationId,
            context.UniversalSafetyReceiptId,
            context.ComplaintTriageReceiptId,
            context.ComplaintTriageAssessmentId,
            context.PromotionId,
            context.PracticeReviewCaseId,
            context.PracticeReviewAuthorizationId,
            TelehealthApplicantRequestIntakePolicy.EntryRequestVersion,
            context.ComplaintCategory,
            "TelehealthEligible",
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            context.LocationConfirmedAt,
            context.ComplaintEvaluatedAt,
            context.ContextExpiresAt,
            context.ApplicantExpiresAt,
            context.ProtocolKey,
            context.ProtocolVersion,
            context.ProtocolContentHash,
            context.ClinicalContentStatus,
            context.MedicalDirectorApprovalRecorded,
            context.ClinicalGoldenCasePackApproved,
            context.ProductionPublicationAllowed,
            context.ComplaintContextFingerprint,
            context.ComplaintCommandFingerprint);

    private static async Task<TelehealthApplicantRequestIntakeApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestIntakeContext?> LoadContextAsync(
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
                   creation.promotion_id,creation.practice_review_case_id,
                   creation.practice_review_authorization_id,r.request_id,r.version,r.status,
                   r.triage_outcome,creation.canonical_patient_id,r.complaint_category,
                   location_confirmation.confirmation_id,location.location_id,location.state_code,
                   location_confirmation.callback_phone_last4,location.attested_at,
                   universal.receipt_id,complaint.receipt_id,complaint.assessment_id,
                   complaint.evaluated_at,complaint.context_expires_at,complaint.protocol_key,
                   complaint.protocol_version,complaint.protocol_content_hash,
                   complaint.clinical_content_status,complaint.medical_director_approval_recorded,
                   complaint.clinical_golden_case_pack_approved,
                   complaint.production_publication_allowed,
                   complaint.context_snapshot_fingerprint,complaint.command_fingerprint,
                   (select count(*) from telehealth_patient_locations x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_location_confirmations x where x.request_id=r.request_id),
                   (select count(*) from telehealth_triage_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_universal_safety_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_complaint_triage_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_intake_snapshots x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_intake_snapshots x where x.request_id=r.request_id),
                   ((select count(*) from telehealth_patient_confirmations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_demonstration_acknowledgments x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (exists(select 1 from telehealth_applicant_synthetic_promotions x
                           where x.applicant_id=a.applicant_id and x.promotion_id=creation.promotion_id)
                    and exists(select 1 from telehealth_applicant_notice_acknowledgments x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_registration_details_confirmations x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_insurance_handoff_confirmations x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_communication_access_readiness x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_device_preparations x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_clinical_information_summary_confirmations x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_pre_request_readiness_acknowledgments x where x.applicant_id=a.applicant_id)
                    and exists(select 1 from telehealth_applicant_practice_review_submissions x
                               where x.applicant_id=a.applicant_id and x.case_id=creation.practice_review_case_id)
                    and exists(select 1 from telehealth_prospective_practice_review_cases x
                               where x.applicant_id=a.applicant_id and x.case_id=creation.practice_review_case_id)
                    and exists(select 1 from telehealth_practice_review_claims x
                               where x.case_id=creation.practice_review_case_id)
                    and exists(select 1 from telehealth_practice_review_authorizations x
                               where x.applicant_id=a.applicant_id
                                 and x.authorization_id=creation.practice_review_authorization_id
                                 and x.request_creation_authorized)),
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
            join telehealth_patient_locations location
              on location.location_id=location_confirmation.location_id and location.request_id=r.request_id
            join telehealth_applicant_request_universal_safety_assessments universal
              on universal.request_id=r.request_id and universal.applicant_id=a.applicant_id
             and universal.request_creation_id=creation.creation_id
             and universal.location_confirmation_id=location_confirmation.confirmation_id
             and universal.location_id=location.location_id
            join telehealth_applicant_request_complaint_triage_assessments complaint
              on complaint.request_id=r.request_id and complaint.applicant_id=a.applicant_id
             and complaint.request_creation_id=creation.creation_id
             and complaint.location_confirmation_id=location_confirmation.confirmation_id
             and complaint.location_id=location.location_id
             and complaint.universal_safety_receipt_id=universal.receipt_id
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
              and universal.resulting_request_version=3 and universal.outcome='TelehealthEligible'
              and universal.universal_safety_passed and universal.complaint_specific_triage_required
              and not universal.clinical_review_required and not universal.terminal_for_telehealth
              and universal.policy_key='SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'
              and universal.policy_version=1
              and complaint.resulting_request_status='Intake'
              and complaint.resulting_request_version=4
              and complaint.outcome='TelehealthEligible'
              and complaint.public_disposition='SyntheticVideoEvaluationCandidate'
              and complaint.synthetic_video_evaluation_candidate
              and not complaint.clinical_review_required and not complaint.terminal_for_telehealth
              and complaint.clinical_content_status='UNAPPROVED_SYNTHETIC'
              and complaint.medical_director_approval_required
              and not complaint.medical_director_approval_recorded
              and not complaint.clinical_golden_case_pack_approved
              and not complaint.production_publication_allowed
              and complaint.policy_key='SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE'
              and complaint.policy_version=1 and complaint.intake_snapshot_created=false
              and complaint.current_location_state_code=location.state_code
              and complaint.callback_phone_last4=location_confirmation.callback_phone_last4
              and complaint.location_confirmed_at=location.attested_at
              and r.complaint_category=creation.complaint_category
              and r.status in ('Intake','Verification') and r.version in (4,5)
              and r.triage_outcome='TelehealthEligible' and r.ready_at is null
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
                reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7),
                reader.GetGuid(8), Convert.ToInt32(reader.GetInt64(9)), reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12),
                reader.GetString(13), reader.GetGuid(14), reader.GetGuid(15), reader.GetString(16),
                reader.GetString(17), reader.GetFieldValue<DateTimeOffset>(18), reader.GetGuid(19),
                reader.GetGuid(20), reader.GetGuid(21), reader.GetFieldValue<DateTimeOffset>(22),
                reader.GetFieldValue<DateTimeOffset>(23), reader.GetString(24),
                reader.GetInt32(25), reader.GetString(26), reader.GetString(27),
                reader.GetBoolean(28), reader.GetBoolean(29), reader.GetBoolean(30),
                reader.GetString(31), reader.GetString(32),
                Convert.ToInt32(reader.GetInt64(33)), Convert.ToInt32(reader.GetInt64(34)),
                Convert.ToInt32(reader.GetInt64(35)), Convert.ToInt32(reader.GetInt64(36)),
                Convert.ToInt32(reader.GetInt64(37)), Convert.ToInt32(reader.GetInt64(38)),
                Convert.ToInt32(reader.GetInt64(39)), Convert.ToInt32(reader.GetInt64(40)),
                reader.GetBoolean(41), reader.GetBoolean(42))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestIntakeRecord Record,
        string CommandFingerprint)?> LoadReceiptAsync(
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
            select receipt.applicant_id,receipt.applicant_version,a.status,receipt.request_id,
                   receipt.resulting_request_version,receipt.resulting_request_status,
                   receipt.complaint_category,receipt.context_snapshot_fingerprint,
                   receipt.context_expires_at,receipt.current_location_state_code,
                   receipt.callback_phone_last4,receipt.receipt_id,receipt.symptom_duration,
                   receipt.captured_at,receipt.command_fingerprint
            from telehealth_applicant_request_intake_snapshots receipt
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
            reader.GetString(6), reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetString(9), reader.GetString(10), reader.GetGuid(11), reader.GetString(12),
            reader.GetFieldValue<DateTimeOffset>(13)), reader.GetString(14));
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
            values(@eventId,@requestId,5,'applicant-intake-snapshot-confirmed',
                   'Intake','Verification','applicant',@actorId,
                   @idempotencyKey,@commandFingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestIntakeApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestIntakeApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestIntakePolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestIntakePolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_state_conflict",
                "The applicant is not eligible for request intake confirmation.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestIntakeContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestIntakePolicy.EntryRequestStatus
            || context.RequestVersion != TelehealthApplicantRequestIntakePolicy.EntryRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.LocationCount != 1
            || context.LocationReceiptCount != 1
            || context.TriageCount != 2
            || context.UniversalSafetyReceiptCount != 1
            || context.ComplaintReceiptCount != 1
            || context.IntakeCount != 0
            || context.IntakeReceiptCount != 0
            || context.DownstreamCount != 0
            || !context.SourceEvidenceComplete
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
        RequireClinicalPublicationBlocked(context);
        if (CreateSnapshot(context).ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_context_expired",
                "The request safety context is too old. Restart or request review.");
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestIntakeContext context,
        TelehealthApplicantRequestIntakeRecord record)
    {
        if (context.RequestStatus != TelehealthApplicantRequestIntakePolicy.ResultingRequestStatus
            || context.RequestVersion != TelehealthApplicantRequestIntakePolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.ComplaintCategory != record.ComplaintCategory
            || context.LocationCount != 1
            || context.LocationReceiptCount != 1
            || context.TriageCount != 2
            || context.UniversalSafetyReceiptCount != 1
            || context.ComplaintReceiptCount != 1
            || context.IntakeCount != 1
            || context.IntakeReceiptCount != 1
            || context.DownstreamCount != 0
            || !context.SourceEvidenceComplete
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
        RequireClinicalPublicationBlocked(context);
    }

    private static void RequireClinicalPublicationBlocked(
        TelehealthApplicantRequestIntakeContext context)
    {
        if (context.ClinicalContentStatus != TelehealthApplicantRequestIntakePolicy.ClinicalContentStatus
            || context.MedicalDirectorApprovalRecorded
            || context.ClinicalGoldenCasePackApproved
            || context.ProductionPublicationAllowed)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_intake_publication_conflict",
                "The complaint fixture is not in the required unapproved synthetic state.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_intake_provenance_conflict",
        "The intake context or its authorized source evidence is unavailable or changed.");
}
