// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPreRequestReadinessContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int ApplicantFacilityId,
    Guid? PromotionId,
    string? PromotionOutcome,
    bool? CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    Guid? RegistrationDetailsConfirmationId,
    string? RegistrationDetailsFingerprint,
    Guid? InsuranceHandoffConfirmationId,
    string? InsuranceSnapshotFingerprint,
    Guid? CommunicationAccessReadinessId,
    string? CommunicationContextFingerprint,
    bool? InterpreterRequested,
    bool? AccessibilitySupportRequested,
    Guid? DevicePreparationId,
    string? PreparationSnapshotFingerprint,
    Guid? ClinicalInventoryId,
    string? InventorySnapshotFingerprint,
    Guid? ClinicalInformationSummaryConfirmationId,
    int? ClinicalInformationSummaryApplicantVersion,
    string? ClinicalInformationSummaryApplicantStatus,
    string? ClinicalInformationSummarySnapshotFingerprint,
    string? ClinicalInformationSummaryRoute,
    bool SourceProvenanceValid,
    long CanonicalInsuranceCount,
    long CanonicalMedicationCount,
    long CanonicalPrescriptionCount,
    long CanonicalAllergyCount,
    long CanonicalProblemCount,
    Guid? ReadinessAcknowledgmentId,
    int? ReadinessAcknowledgmentApplicantVersion,
    string? ReadinessAcknowledgmentApplicantStatus,
    string? ReadinessSnapshotFingerprint,
    string? OverallRoute,
    DateTimeOffset? AcknowledgedAt,
    Guid? PracticeReviewCaseId,
    string? PracticeReviewStatus,
    DateTimeOffset? PracticeReviewSubmittedAt);

public sealed record TelehealthApplicantPreRequestReadinessRecord(
    Guid AcknowledgmentId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string PreRequestReadinessSnapshotFingerprint,
    string OverallRoute,
    DateTimeOffset AcknowledgedAt);

public sealed record TelehealthApplicantPracticeReviewSubmissionRecord(
    Guid CaseId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string PracticeReviewSnapshotFingerprint,
    string ReviewRoute,
    string ReviewStatus,
    DateTimeOffset SubmittedAt);

public sealed class TelehealthApplicantPreRequestReadinessRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now() as database_now,
          a.facility_id as applicant_facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,
          patient.facility_id as patient_facility_id,patient.merged_into_patient_id,
          registration.confirmation_id as registration_details_confirmation_id,
          registration.details_fingerprint as registration_details_fingerprint,
          insurance.confirmation_id as insurance_handoff_confirmation_id,
          insurance.insurance_snapshot_fingerprint,
          communication.readiness_id as communication_access_readiness_id,
          communication.context_snapshot_fingerprint as communication_context_fingerprint,
          communication.interpreter_requested,communication.accessibility_support_requested,
          device.preparation_id as device_preparation_id,device.preparation_snapshot_fingerprint,
          inventory.inventory_id as clinical_inventory_id,inventory.inventory_snapshot_fingerprint,
          summary.confirmation_id as clinical_information_summary_confirmation_id,
          summary.resulting_applicant_version as clinical_information_summary_applicant_version,
          summary.resulting_applicant_status as clinical_information_summary_applicant_status,
          summary.clinical_information_summary_snapshot_fingerprint,
          summary.summary_route as clinical_information_summary_route,
          coalesce(
            promotion.outcome='SyntheticPatientCreated'
            and promotion.canonical_patient_created
            and promotion.practice_id=a.practice_id
            and promotion.facility_id=a.facility_id
            and patient.canonical_id=promotion.canonical_patient_id
            and patient.facility_id=a.facility_id
            and not patient.portal_enabled
            and patient.merged_into_patient_id is null
            and patient.first_name=a.legal_first_name
            and patient.last_name=a.legal_last_name
            and patient.date_of_birth=a.date_of_birth
            and patient.email=a.email
            and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
            and patient.state=a.residence_state_code
            and patient.postal_code=a.postal_code
            and registration.applicant_id=a.applicant_id
            and registration.practice_id=a.practice_id
            and registration.facility_id=a.facility_id
            and registration.promotion_id=promotion.promotion_id
            and registration.canonical_patient_id=promotion.canonical_patient_id
            and registration.resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'
            and registration.policy_key='SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
            and registration.policy_version=1
            and not registration.patient_record_changed
            and not registration.intake_completed
            and not registration.practice_accepted
            and not registration.request_created
            and not registration.queue_enabled
            and insurance.applicant_id=a.applicant_id
            and insurance.practice_id=a.practice_id
            and insurance.facility_id=a.facility_id
            and insurance.promotion_id=promotion.promotion_id
            and insurance.canonical_patient_id=promotion.canonical_patient_id
            and insurance.registration_details_confirmation_id=registration.confirmation_id
            and insurance.resulting_applicant_status='SyntheticInsuranceDetailsConfirmed'
            and insurance.policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
            and insurance.policy_version=1
            and not insurance.canonical_coverage_created
            and not insurance.practice_accepted
            and not insurance.request_created
            and not insurance.queue_enabled
            and communication.applicant_id=a.applicant_id
            and communication.practice_id=a.practice_id
            and communication.facility_id=a.facility_id
            and communication.promotion_id=promotion.promotion_id
            and communication.canonical_patient_id=promotion.canonical_patient_id
            and communication.registration_details_confirmation_id=registration.confirmation_id
            and communication.insurance_handoff_confirmation_id=insurance.confirmation_id
            and communication.resulting_applicant_status='SyntheticCommunicationAccessReadinessRecorded'
            and communication.policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
            and communication.policy_version=1
            and not communication.interpreter_assigned
            and not communication.accessibility_accommodation_arranged
            and not communication.support_request_created
            and not communication.request_created
            and not communication.queue_enabled
            and device.applicant_id=a.applicant_id
            and device.practice_id=a.practice_id
            and device.facility_id=a.facility_id
            and device.promotion_id=promotion.promotion_id
            and device.canonical_patient_id=promotion.canonical_patient_id
            and device.registration_details_confirmation_id=registration.confirmation_id
            and device.insurance_handoff_confirmation_id=insurance.confirmation_id
            and device.communication_access_readiness_id=communication.readiness_id
            and device.resulting_applicant_status='SyntheticDevicePreparationRecorded'
            and device.policy_key='SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
            and device.policy_version=1
            and device.browser_supported and device.camera_available
            and device.microphone_available and device.speaker_available
            and not device.technology_ready
            and not device.waiting_room_created
            and not device.request_created
            and not device.queue_entered
            and inventory.applicant_id=a.applicant_id
            and inventory.practice_id=a.practice_id
            and inventory.facility_id=a.facility_id
            and inventory.promotion_id=promotion.promotion_id
            and inventory.canonical_patient_id=promotion.canonical_patient_id
            and inventory.registration_details_confirmation_id=registration.confirmation_id
            and inventory.insurance_handoff_confirmation_id=insurance.confirmation_id
            and inventory.communication_access_readiness_id=communication.readiness_id
            and inventory.device_preparation_id=device.preparation_id
            and inventory.resulting_applicant_status='SyntheticClinicalInformationInventoryRecorded'
            and inventory.preparation_snapshot_fingerprint=device.preparation_snapshot_fingerprint
            and inventory.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
            and inventory.policy_version=1
            and not inventory.clinical_intake_completed
            and not inventory.clinical_eligibility_established
            and not inventory.request_created
            and not inventory.queue_entered
            and summary.applicant_id=a.applicant_id
            and summary.practice_id=a.practice_id
            and summary.facility_id=a.facility_id
            and summary.promotion_id=promotion.promotion_id
            and summary.canonical_patient_id=promotion.canonical_patient_id
            and summary.clinical_inventory_id=inventory.inventory_id
            and summary.resulting_applicant_status='SyntheticClinicalInformationSummaryConfirmed'
            and summary.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
            and summary.policy_version=1
            and not summary.questionnaire_response_created
            and not summary.medication_list_reconciled
            and not summary.allergy_list_reconciled
            and not summary.health_history_reconciled
            and not summary.clinical_intake_completed
            and not summary.clinical_eligibility_established
            and not summary.clinician_review_created
            and not summary.patient_record_changed
            and not summary.practice_accepted
            and not summary.request_created
            and not summary.queue_entered
            and not summary.care_authorized
            and not summary.prescribing_enabled,
            false) as source_provenance_valid,
          (select count(*) from insurance_records r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_insurance_count,
          (select count(*) from medications r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_medication_count,
          (select count(*) from prescriptions r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_prescription_count,
          (select count(*) from allergies r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_allergy_count,
          (select count(*) from problems r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_problem_count,
          readiness.acknowledgment_id as readiness_acknowledgment_id,
          readiness.resulting_applicant_version as readiness_acknowledgment_applicant_version,
          readiness.resulting_applicant_status as readiness_acknowledgment_applicant_status,
          readiness.pre_request_readiness_snapshot_fingerprint,
          readiness.overall_route,readiness.acknowledged_at,
          submission.case_id as practice_review_case_id,
          review_case.case_status as practice_review_status,
          submission.submitted_at as practice_review_submitted_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_practice_review_submissions submission
          on submission.applicant_id=a.applicant_id
        left join telehealth_prospective_practice_review_cases review_case
          on review_case.case_id=submission.case_id
        left join telehealth_applicant_pre_request_readiness_acknowledgments readiness
          on readiness.applicant_id=a.applicant_id
        left join telehealth_applicant_clinical_information_summary_confirmations summary
          on summary.applicant_id=a.applicant_id
        left join telehealth_applicant_clinical_information_inventories inventory
          on inventory.inventory_id=summary.clinical_inventory_id
        left join telehealth_applicant_registration_details_confirmations registration
          on registration.confirmation_id=inventory.registration_details_confirmation_id
        left join telehealth_applicant_insurance_handoff_confirmations insurance
          on insurance.confirmation_id=inventory.insurance_handoff_confirmation_id
        left join telehealth_applicant_communication_access_readiness communication
          on communication.readiness_id=inventory.communication_access_readiness_id
        left join telehealth_applicant_device_preparations device
          on device.preparation_id=inventory.device_preparation_id
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.promotion_id=summary.promotion_id
        left join patients patient on patient.canonical_id=promotion.canonical_patient_id
        """;

    public async Task<TelehealthApplicantPreRequestReadinessContext> GetAuthorizedAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);
        RequireEligible(context, facilityId, allowAcknowledged: true, allowSubmitted: true);
        return context;
    }

    public async Task<TelehealthApplicantPreRequestReadinessRecord> AcknowledgeAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantPreRequestReadinessAcknowledgment acknowledgment,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            RequireEligible(context, facilityId, allowAcknowledged: true);
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowAcknowledged: false);
        if (context.ApplicantVersion != acknowledgment.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_request_readiness_version_conflict",
                "The applicant changed. Reload the pre-request readiness review before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                acknowledgment.PreRequestReadinessSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_request_readiness_snapshot_conflict",
                "The pre-request readiness review changed. Reload it before acknowledging.");
        }

        var overallRoute = OverallRoute(context);
        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticClinicalInformationSummaryConfirmed';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", acknowledgment.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_pre_request_readiness_version_conflict",
                    "The applicant changed. Reload the pre-request readiness review before retrying.");
            }
        }

        var acknowledgmentId = Guid.NewGuid();
        DateTimeOffset acknowledgedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_pre_request_readiness_acknowledgments(
                  acknowledgment_id,applicant_id,practice_id,facility_id,promotion_id,
                  canonical_patient_id,registration_details_confirmation_id,
                  registration_details_fingerprint,insurance_handoff_confirmation_id,
                  insurance_snapshot_fingerprint,communication_access_readiness_id,
                  communication_context_fingerprint,interpreter_requested,
                  accessibility_support_requested,device_preparation_id,
                  preparation_snapshot_fingerprint,clinical_inventory_id,
                  inventory_snapshot_fingerprint,clinical_information_summary_confirmation_id,
                  clinical_information_summary_snapshot_fingerprint,
                  clinical_information_summary_route,resulting_applicant_version,
                  resulting_applicant_status,pre_request_readiness_snapshot_fingerprint,
                  overall_route,prior_sections_reviewed_acknowledged,
                  outstanding_steps_remain_acknowledged,no_request_or_queue_created_acknowledged,
                  correction_requires_separate_workflow_acknowledged,policy_key,policy_version,
                  evidence_type,applicant_expires_at,idempotency_key,command_fingerprint)
                values(
                  @acknowledgmentId,@applicantId,@practiceId,@facilityId,@promotionId,
                  @patientId,@registrationId,@registrationFingerprint,@insuranceId,
                  @insuranceFingerprint,@communicationId,@communicationFingerprint,
                  @interpreterRequested,@accessibilityRequested,@deviceId,@deviceFingerprint,
                  @inventoryId,@inventoryFingerprint,@summaryId,@summaryFingerprint,@summaryRoute,
                  @nextVersion,@nextStatus,@readinessFingerprint,@overallRoute,true,true,true,true,
                  @policyKey,@policyVersion,@evidenceType,@applicantExpiresAt,@idempotencyKey,
                  @commandFingerprint)
                returning acknowledged_at;
                """;
            insert.Parameters.AddWithValue("acknowledgmentId", acknowledgmentId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("registrationId", context.RegistrationDetailsConfirmationId!.Value);
            insert.Parameters.AddWithValue("registrationFingerprint", context.RegistrationDetailsFingerprint!);
            insert.Parameters.AddWithValue("insuranceId", context.InsuranceHandoffConfirmationId!.Value);
            insert.Parameters.AddWithValue("insuranceFingerprint", context.InsuranceSnapshotFingerprint!);
            insert.Parameters.AddWithValue("communicationId", context.CommunicationAccessReadinessId!.Value);
            insert.Parameters.AddWithValue("communicationFingerprint", context.CommunicationContextFingerprint!);
            insert.Parameters.AddWithValue("interpreterRequested", context.InterpreterRequested!.Value);
            insert.Parameters.AddWithValue("accessibilityRequested", context.AccessibilitySupportRequested!.Value);
            insert.Parameters.AddWithValue("deviceId", context.DevicePreparationId!.Value);
            insert.Parameters.AddWithValue("deviceFingerprint", context.PreparationSnapshotFingerprint!);
            insert.Parameters.AddWithValue("inventoryId", context.ClinicalInventoryId!.Value);
            insert.Parameters.AddWithValue("inventoryFingerprint", context.InventorySnapshotFingerprint!);
            insert.Parameters.AddWithValue("summaryId", context.ClinicalInformationSummaryConfirmationId!.Value);
            insert.Parameters.AddWithValue("summaryFingerprint", context.ClinicalInformationSummarySnapshotFingerprint!);
            insert.Parameters.AddWithValue("summaryRoute", context.ClinicalInformationSummaryRoute!);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("readinessFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("overallRoute", overallRoute);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantPreRequestReadinessPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantPreRequestReadinessPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantPreRequestReadinessPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Synthetic pre-request readiness acknowledgment time is unavailable.");
            }
            acknowledgedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-pre-request-readiness-acknowledged',
                       'SyntheticClinicalInformationSummaryConfirmed',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "pre-request-readiness:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            acknowledgmentId,
            applicantId,
            nextVersion,
            TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus,
            snapshot.Fingerprint,
            overallRoute,
            acknowledgedAt);
    }

    public async Task<TelehealthApplicantPracticeReviewSubmissionRecord> SubmitPracticeReviewAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantPracticeReviewSubmission submission,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var replay = await LoadPracticeReviewByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            RequireEligible(context, facilityId, allowAcknowledged: true, allowSubmitted: true);
            RequirePracticeReviewReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            var currentSnapshot = PracticeReviewSnapshot(context);
            if (!string.Equals(
                    currentSnapshot.Fingerprint,
                    replay.Value.Record.PracticeReviewSnapshotFingerprint,
                    StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_practice_review_provenance_conflict",
                    "The practice review source changed. Reload before retrying.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowAcknowledged: true);
        if (context.ApplicantStatus != TelehealthApplicantPracticeReviewSubmissionPolicy.EntryStatus
            || context.ReadinessAcknowledgmentApplicantVersion != context.ApplicantVersion
            || context.PracticeReviewCaseId is not null)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_review_state_conflict",
                "The applicant is not eligible for this bounded synthetic practice review submission.");
        }
        if (context.ApplicantVersion != submission.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_review_version_conflict",
                "The applicant changed. Reload the practice review submission before retrying.");
        }

        var snapshot = PracticeReviewSnapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                submission.PracticeReviewSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_review_snapshot_conflict",
                "The practice review submission changed. Reload before continuing.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPreRequestReadinessAcknowledged';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", submission.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_practice_review_version_conflict",
                    "The applicant changed. Reload the practice review submission before retrying.");
            }
        }

        var caseId = Guid.NewGuid();
        await using (var caseCommand = connection.CreateCommand())
        {
            caseCommand.Transaction = transaction;
            caseCommand.CommandText = """
                insert into telehealth_prospective_practice_review_cases(
                  case_id,applicant_id,practice_id,facility_id,canonical_patient_id,
                  readiness_acknowledgment_id,readiness_snapshot_fingerprint,review_route,
                  case_status,applicant_expires_at)
                values(@caseId,@applicantId,@practiceId,@facilityId,@patientId,@readinessId,
                       @readinessFingerprint,@reviewRoute,@reviewStatus,@applicantExpiresAt);
                """;
            caseCommand.Parameters.AddWithValue("caseId", caseId);
            caseCommand.Parameters.AddWithValue("applicantId", applicantId);
            caseCommand.Parameters.AddWithValue("practiceId", practiceId);
            caseCommand.Parameters.AddWithValue("facilityId", facilityId);
            caseCommand.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            caseCommand.Parameters.AddWithValue("readinessId", context.ReadinessAcknowledgmentId!.Value);
            caseCommand.Parameters.AddWithValue("readinessFingerprint", context.ReadinessSnapshotFingerprint!);
            caseCommand.Parameters.AddWithValue("reviewRoute", context.OverallRoute!);
            caseCommand.Parameters.AddWithValue(
                "reviewStatus", TelehealthApplicantPracticeReviewSubmissionPolicy.ReviewStatus);
            caseCommand.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            await caseCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var submissionId = Guid.NewGuid();
        DateTimeOffset submittedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_practice_review_submissions(
                  submission_id,case_id,applicant_id,practice_id,facility_id,
                  canonical_patient_id,readiness_acknowledgment_id,
                  readiness_snapshot_fingerprint,review_route,resulting_applicant_version,
                  resulting_applicant_status,practice_review_snapshot_fingerprint,
                  patient_reported_information_acknowledged,
                  practice_may_request_information_or_decline_acknowledged,
                  no_telehealth_request_or_care_queue_acknowledged,
                  worsening_symptoms_require_immediate_action_acknowledged,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint,staff_review_created)
                values(@submissionId,@caseId,@applicantId,@practiceId,@facilityId,@patientId,
                       @readinessId,@readinessFingerprint,@reviewRoute,@nextVersion,@nextStatus,
                       @snapshotFingerprint,true,true,true,true,@policyKey,@policyVersion,
                       @evidenceType,@applicantExpiresAt,@idempotencyKey,@commandFingerprint,true)
                returning submitted_at;
                """;
            insert.Parameters.AddWithValue("submissionId", submissionId);
            insert.Parameters.AddWithValue("caseId", caseId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("readinessId", context.ReadinessAcknowledgmentId!.Value);
            insert.Parameters.AddWithValue("readinessFingerprint", context.ReadinessSnapshotFingerprint!);
            insert.Parameters.AddWithValue("reviewRoute", context.OverallRoute!);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantPracticeReviewSubmissionPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantPracticeReviewSubmissionPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantPracticeReviewSubmissionPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic practice review submission time is unavailable.");
            }
            submittedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-practice-review-submitted',
                       'SyntheticPreRequestReadinessAcknowledged',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "practice-review-submission:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            caseId,
            applicantId,
            nextVersion,
            TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus,
            snapshot.Fingerprint,
            context.OverallRoute!,
            TelehealthApplicantPracticeReviewSubmissionPolicy.ReviewStatus,
            submittedAt);
    }

    public static TelehealthApplicantPracticeReviewSubmissionSnapshot PracticeReviewSnapshot(
        TelehealthApplicantPreRequestReadinessContext context) =>
        TelehealthApplicantPracticeReviewSubmissionPolicy.Snapshot(
            context.ApplicantId,
            context.ReadinessAcknowledgmentId!.Value,
            context.ReadinessAcknowledgmentApplicantVersion!.Value,
            context.ReadinessSnapshotFingerprint!,
            context.OverallRoute!,
            context.CanonicalPatientId!,
            context.ApplicantExpiresAt);

    public static TelehealthApplicantPreRequestReadinessSnapshot Snapshot(
        TelehealthApplicantPreRequestReadinessContext context) =>
        TelehealthApplicantPreRequestReadinessPolicy.Snapshot(
            context.RegistrationDetailsConfirmationId!.Value,
            context.RegistrationDetailsFingerprint!,
            context.InsuranceHandoffConfirmationId!.Value,
            context.InsuranceSnapshotFingerprint!,
            context.CommunicationAccessReadinessId!.Value,
            context.CommunicationContextFingerprint!,
            context.InterpreterRequested!.Value,
            context.AccessibilitySupportRequested!.Value,
            context.DevicePreparationId!.Value,
            context.PreparationSnapshotFingerprint!,
            context.ClinicalInventoryId!.Value,
            context.InventorySnapshotFingerprint!,
            context.ClinicalInformationSummaryConfirmationId!.Value,
            context.ClinicalInformationSummarySnapshotFingerprint!,
            context.ClinicalInformationSummaryRoute!);

    public static string OverallRoute(TelehealthApplicantPreRequestReadinessContext context) =>
        TelehealthApplicantPreRequestReadinessPolicy.DetermineOverallRoute(
            context.ClinicalInformationSummaryRoute!,
            context.InterpreterRequested!.Value,
            context.AccessibilitySupportRequested!.Value);

    private static async Task<TelehealthApplicantPreRequestReadinessContext?> LoadAsync(
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
        command.CommandText = ContextProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            """ + (forUpdate ? "\nfor update of a;" : ";");
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new(
            reader.GetGuid(reader.GetOrdinal("applicant_id")),
            Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("version"))),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetString(reader.GetOrdinal("access_key_hash")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("database_now")),
            reader.GetInt32(reader.GetOrdinal("applicant_facility_id")),
            NullableGuid(reader, "promotion_id"),
            NullableString(reader, "outcome"),
            NullableBoolean(reader, "canonical_patient_created"),
            NullableString(reader, "canonical_patient_id"),
            NullableBoolean(reader, "portal_enabled"),
            NullableInt32(reader, "patient_facility_id"),
            NullableString(reader, "merged_into_patient_id"),
            NullableGuid(reader, "registration_details_confirmation_id"),
            NullableString(reader, "registration_details_fingerprint"),
            NullableGuid(reader, "insurance_handoff_confirmation_id"),
            NullableString(reader, "insurance_snapshot_fingerprint"),
            NullableGuid(reader, "communication_access_readiness_id"),
            NullableString(reader, "communication_context_fingerprint"),
            NullableBoolean(reader, "interpreter_requested"),
            NullableBoolean(reader, "accessibility_support_requested"),
            NullableGuid(reader, "device_preparation_id"),
            NullableString(reader, "preparation_snapshot_fingerprint"),
            NullableGuid(reader, "clinical_inventory_id"),
            NullableString(reader, "inventory_snapshot_fingerprint"),
            NullableGuid(reader, "clinical_information_summary_confirmation_id"),
            NullableInt32FromInt64(reader, "clinical_information_summary_applicant_version"),
            NullableString(reader, "clinical_information_summary_applicant_status"),
            NullableString(reader, "clinical_information_summary_snapshot_fingerprint"),
            NullableString(reader, "clinical_information_summary_route"),
            reader.GetBoolean(reader.GetOrdinal("source_provenance_valid")),
            reader.GetInt64(reader.GetOrdinal("canonical_insurance_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_medication_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_prescription_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_allergy_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_problem_count")),
            NullableGuid(reader, "readiness_acknowledgment_id"),
            NullableInt32FromInt64(reader, "readiness_acknowledgment_applicant_version"),
            NullableString(reader, "readiness_acknowledgment_applicant_status"),
            NullableString(reader, "pre_request_readiness_snapshot_fingerprint"),
            NullableString(reader, "overall_route"),
            NullableDateTimeOffset(reader, "acknowledged_at"),
            NullableGuid(reader, "practice_review_case_id"),
            NullableString(reader, "practice_review_status"),
            NullableDateTimeOffset(reader, "practice_review_submitted_at"));
    }

    private static async Task<(TelehealthApplicantPreRequestReadinessRecord Record,
        string CommandFingerprint)?> LoadByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select acknowledgment_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,pre_request_readiness_snapshot_fingerprint,
                   overall_route,acknowledged_at,command_fingerprint
            from telehealth_applicant_pre_request_readiness_acknowledgments
            where practice_id=@practiceId and facility_id=@facilityId
              and applicant_id=@applicantId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (new(
            reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3), reader.GetString(4), reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6)), reader.GetString(7));
    }

    private static async Task<(TelehealthApplicantPracticeReviewSubmissionRecord Record,
        string CommandFingerprint)?> LoadPracticeReviewByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select s.case_id,s.applicant_id,s.resulting_applicant_version,
                   s.resulting_applicant_status,s.practice_review_snapshot_fingerprint,
                   s.review_route,c.case_status,s.submitted_at,s.command_fingerprint
            from telehealth_applicant_practice_review_submissions s
            join telehealth_prospective_practice_review_cases c on c.case_id=s.case_id
            where s.practice_id=@practiceId and s.facility_id=@facilityId
              and s.applicant_id=@applicantId and s.idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return (new(
            reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetFieldValue<DateTimeOffset>(7)), reader.GetString(8));
    }

    private static void RequireEligible(
        TelehealthApplicantPreRequestReadinessContext context,
        int facilityId,
        bool allowAcknowledged,
        bool allowSubmitted = false)
    {
        var entry = context.ApplicantStatus == TelehealthApplicantPreRequestReadinessPolicy.EntryStatus
            && context.ClinicalInformationSummaryApplicantVersion == context.ApplicantVersion;
        var acknowledged = allowAcknowledged
            && context.ApplicantStatus == TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus
            && context.ReadinessAcknowledgmentId is not null
            && context.ReadinessAcknowledgmentApplicantVersion == context.ApplicantVersion
            && context.ReadinessAcknowledgmentApplicantStatus ==
                TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus
            && context.ClinicalInformationSummaryApplicantVersion == context.ApplicantVersion - 1;
        var submitted = allowSubmitted
            && context.ApplicantStatus == TelehealthApplicantPracticeReviewSubmissionPolicy.ResultingStatus
            && context.ReadinessAcknowledgmentId is not null
            && context.ReadinessAcknowledgmentApplicantVersion == context.ApplicantVersion - 1
            && context.ReadinessAcknowledgmentApplicantStatus ==
                TelehealthApplicantPreRequestReadinessPolicy.ResultingStatus
            && context.PracticeReviewCaseId is not null
            && context.PracticeReviewStatus == TelehealthApplicantPracticeReviewSubmissionPolicy.ReviewStatus
            && context.PracticeReviewSubmittedAt is not null
            && context.ClinicalInformationSummaryApplicantVersion == context.ApplicantVersion - 2;
        if ((!entry && !acknowledged && !submitted)
            || context.ApplicantExpiresAt <= context.DatabaseNow
            || context.ApplicantFacilityId != facilityId
            || context.PromotionOutcome != "SyntheticPatientCreated"
            || context.CanonicalPatientCreated is not true
            || context.PromotionId is null
            || string.IsNullOrWhiteSpace(context.CanonicalPatientId)
            || context.PatientPortalEnabled is not false
            || context.PatientFacilityId != facilityId
            || context.MergedIntoPatientId is not null
            || context.RegistrationDetailsConfirmationId is null
            || string.IsNullOrWhiteSpace(context.RegistrationDetailsFingerprint)
            || context.InsuranceHandoffConfirmationId is null
            || string.IsNullOrWhiteSpace(context.InsuranceSnapshotFingerprint)
            || context.CommunicationAccessReadinessId is null
            || string.IsNullOrWhiteSpace(context.CommunicationContextFingerprint)
            || context.InterpreterRequested is null
            || context.AccessibilitySupportRequested is null
            || context.DevicePreparationId is null
            || string.IsNullOrWhiteSpace(context.PreparationSnapshotFingerprint)
            || context.ClinicalInventoryId is null
            || string.IsNullOrWhiteSpace(context.InventorySnapshotFingerprint)
            || context.ClinicalInformationSummaryConfirmationId is null
            || context.ClinicalInformationSummaryApplicantStatus !=
                TelehealthApplicantPreRequestReadinessPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.ClinicalInformationSummarySnapshotFingerprint)
            || string.IsNullOrWhiteSpace(context.ClinicalInformationSummaryRoute)
            || ((acknowledged || submitted)
                && (context.ReadinessAcknowledgmentId is null
                    || string.IsNullOrWhiteSpace(context.ReadinessSnapshotFingerprint)
                    || string.IsNullOrWhiteSpace(context.OverallRoute)))
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceCount != 0
            || context.CanonicalMedicationCount != 0
            || context.CanonicalPrescriptionCount != 0
            || context.CanonicalAllergyCount != 0
            || context.CanonicalProblemCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_request_readiness_state_conflict",
                "The applicant is not eligible for this bounded synthetic pre-request readiness acknowledgment.");
        }
    }

    private static Guid? NullableGuid(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? NullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool? NullableBoolean(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private static int? NullableInt32(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static int? NullableInt32FromInt64(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetInt64(ordinal));
    }

    private static DateTimeOffset? NullableDateTimeOffset(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }

    private static void RequireAccess(string existingHash, string suppliedHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existingHash, suppliedHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_request_readiness_idempotency_conflict",
                "The pre-request readiness idempotency key was already used with different content.");
        }
    }

    private static void RequirePracticeReviewReplayFingerprint(
        string existing,
        string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_review_idempotency_conflict",
                "The practice review submission idempotency key was already used with different content.");
        }
    }
}
