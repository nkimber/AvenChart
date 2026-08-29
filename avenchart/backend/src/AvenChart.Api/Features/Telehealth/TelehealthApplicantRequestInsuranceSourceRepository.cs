// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestInsuranceSourceRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string InsuranceSourceSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdLast4,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string PreviousEligibilityBusinessOutcome,
    DateTimeOffset PreviousEligibilityCheckedAt,
    DateTimeOffset PreviousEligibilityExpiresAt,
    string PreviousPracticeNetworkBusinessOutcome,
    DateTimeOffset PreviousPracticeNetworkCheckedAt,
    DateTimeOffset PreviousPracticeNetworkExpiresAt,
    Guid? ConfirmationId,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestInsuranceSourceApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestInsuranceSourceContext(
    Guid ApplicantId,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? RequestTriageOutcome,
    string CanonicalPatientId,
    Guid RequestCreationId,
    Guid PromotionId,
    Guid PracticeReviewCaseId,
    Guid PracticeReviewAuthorizationId,
    Guid RequestIntakeReceiptId,
    DateTimeOffset RequestIntakeCapturedAt,
    DateTimeOffset ContextExpiresAt,
    Guid InsuranceHandoffConfirmationId,
    Guid MemberInsuranceDetailsId,
    Guid EligibilityResultId,
    Guid NetworkDeterminationId,
    string SourceInsuranceSnapshotFingerprint,
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdLast4,
    bool GroupNumberPresent,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string PreviousEligibilityBusinessOutcome,
    DateTimeOffset PreviousEligibilityCheckedAt,
    DateTimeOffset PreviousEligibilityExpiresAt,
    string PreviousPracticeNetworkBusinessOutcome,
    DateTimeOffset PreviousPracticeNetworkCheckedAt,
    DateTimeOffset PreviousPracticeNetworkExpiresAt,
    bool PreviousRenderingPhysicianNetworkChecked,
    int GenericIntakeCount,
    int IntakeReceiptCount,
    int InsuranceSourceCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestInsuranceSourceRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestInsuranceSourceRecord> GetAsync(
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
        return CreateRecord(context, null, null);
    }

    public async Task<TelehealthApplicantRequestInsuranceSourceRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestInsuranceSourceConfirmation confirmation,
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
                    "telehealth_applicant_request_insurance_source_idempotency_conflict",
                    "The idempotency key was already used with different insurance-source content.");
            }
            RequireCompletedContext(context, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (context.InsuranceSourceCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_already_completed",
                "The request insurance source was already confirmed. Reload the current state.");
        }
        RequireReadyContext(context);
        if (confirmation.ExpectedRequestVersion != context.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_version_conflict",
                "The request changed. Reload the insurance source before retrying.");
        }

        var snapshot = CreateSnapshot(context);
        if (!string.Equals(
                confirmation.InsuranceSourceSnapshotFingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_snapshot_conflict",
                "The request or insurance source changed. Reload before continuing.");
        }
        if (snapshot.ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_context_expired",
                "The request context expired. Restart or request review.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.Verification,
            TelehealthRequestStatus.Verification);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set version=6,updated_at=now()
                where request_id=@requestId and practice_id=@practiceId and facility_id=@facilityId
                  and source_applicant_id=@applicantId and status='Verification' and version=5
                  and triage_outcome='TelehealthEligible' and ready_at is null
                  and appointment_id is null;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            update.Parameters.AddWithValue("practiceId", practiceId);
            update.Parameters.AddWithValue("facilityId", facilityId);
            update.Parameters.AddWithValue("applicantId", applicantId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_insurance_source_version_conflict",
                    "The request changed. Reload the insurance source before retrying.");
            }
        }

        DateTimeOffset confirmedAt;
        var receiptId = Guid.NewGuid();
        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_applicant_request_insurance_source_confirmations(
                  confirmation_id,request_id,applicant_id,request_intake_receipt_id,
                  request_creation_id,insurance_handoff_confirmation_id,
                  member_insurance_details_id,eligibility_result_id,network_determination_id,
                  promotion_id,practice_review_case_id,practice_review_authorization_id,
                  practice_id,facility_id,canonical_patient_id,applicant_version,
                  source_request_version,resulting_request_version,source_request_status,
                  resulting_request_status,insurance_source_snapshot_fingerprint,
                  source_insurance_snapshot_fingerprint,payer_display_name,product_display_name,
                  member_id_last4,group_number_present,group_number_last4,
                  subscriber_relationship,coverage_priority,
                  previous_eligibility_business_outcome,previous_eligibility_checked_at,
                  previous_eligibility_expires_at,previous_practice_network_business_outcome,
                  previous_practice_network_checked_at,previous_practice_network_expires_at,
                  previous_rendering_physician_network_checked,request_intake_captured_at,
                  context_expires_at,applicant_expires_at,payer_product_confirmed,
                  masked_member_details_confirmed,subscriber_relationship_confirmed,
                  primary_coverage_source_confirmed,fresh_verification_requested,
                  evidence_limitations_acknowledged,synthetic_data_confirmed,
                  policy_key,policy_version,evidence_type,idempotency_key,
                  command_fingerprint,confirmed_at)
                values(
                  @confirmationId,@requestId,@applicantId,@intakeReceiptId,
                  @requestCreationId,@handoffId,@memberDetailsId,@eligibilityId,@networkId,
                  @promotionId,@reviewCaseId,@reviewAuthorizationId,@practiceId,@facilityId,
                  @patientId,@applicantVersion,5,6,'Verification','Verification',
                  @snapshotFingerprint,@sourceInsuranceFingerprint,@payer,@product,
                  @memberLast4,@groupPresent,@groupLast4,@relationship,@priority,
                  @eligibilityOutcome,@eligibilityCheckedAt,@eligibilityExpiresAt,
                  @networkOutcome,@networkCheckedAt,@networkExpiresAt,false,
                  @intakeCapturedAt,@contextExpiresAt,@applicantExpiresAt,
                  true,true,true,true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @idempotencyKey,@commandFingerprint,now())
                returning confirmed_at;
                """;
            receipt.Parameters.AddWithValue("confirmationId", receiptId);
            receipt.Parameters.AddWithValue("requestId", context.RequestId);
            receipt.Parameters.AddWithValue("applicantId", applicantId);
            receipt.Parameters.AddWithValue("intakeReceiptId", context.RequestIntakeReceiptId);
            receipt.Parameters.AddWithValue("requestCreationId", context.RequestCreationId);
            receipt.Parameters.AddWithValue("handoffId", context.InsuranceHandoffConfirmationId);
            receipt.Parameters.AddWithValue("memberDetailsId", context.MemberInsuranceDetailsId);
            receipt.Parameters.AddWithValue("eligibilityId", context.EligibilityResultId);
            receipt.Parameters.AddWithValue("networkId", context.NetworkDeterminationId);
            receipt.Parameters.AddWithValue("promotionId", context.PromotionId);
            receipt.Parameters.AddWithValue("reviewCaseId", context.PracticeReviewCaseId);
            receipt.Parameters.AddWithValue("reviewAuthorizationId", context.PracticeReviewAuthorizationId);
            receipt.Parameters.AddWithValue("practiceId", practiceId);
            receipt.Parameters.AddWithValue("facilityId", facilityId);
            receipt.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            receipt.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            receipt.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            receipt.Parameters.AddWithValue("sourceInsuranceFingerprint", context.SourceInsuranceSnapshotFingerprint);
            receipt.Parameters.AddWithValue("payer", context.PayerDisplayName);
            receipt.Parameters.AddWithValue("product", context.ProductDisplayName);
            receipt.Parameters.AddWithValue("memberLast4", context.MemberIdLast4);
            receipt.Parameters.AddWithValue("groupPresent", context.GroupNumberPresent);
            receipt.Parameters.AddWithValue("groupLast4", (object?)context.GroupNumberLast4 ?? DBNull.Value);
            receipt.Parameters.AddWithValue("relationship", context.SubscriberRelationship);
            receipt.Parameters.AddWithValue("priority", context.CoveragePriority);
            receipt.Parameters.AddWithValue("eligibilityOutcome", context.PreviousEligibilityBusinessOutcome);
            receipt.Parameters.AddWithValue("eligibilityCheckedAt", context.PreviousEligibilityCheckedAt);
            receipt.Parameters.AddWithValue("eligibilityExpiresAt", context.PreviousEligibilityExpiresAt);
            receipt.Parameters.AddWithValue("networkOutcome", context.PreviousPracticeNetworkBusinessOutcome);
            receipt.Parameters.AddWithValue("networkCheckedAt", context.PreviousPracticeNetworkCheckedAt);
            receipt.Parameters.AddWithValue("networkExpiresAt", context.PreviousPracticeNetworkExpiresAt);
            receipt.Parameters.AddWithValue("intakeCapturedAt", context.RequestIntakeCapturedAt);
            receipt.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            receipt.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            receipt.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestInsuranceSourcePolicy.PolicyKey);
            receipt.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestInsuranceSourcePolicy.PolicyVersion);
            receipt.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestInsuranceSourcePolicy.EvidenceType);
            receipt.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            receipt.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var confirmedAtValue = await receipt.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Synthetic insurance-source confirmation time was not returned.");
            confirmedAt = confirmedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(
                    DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Synthetic insurance-source confirmation time had an unexpected database type.")
            };
        }

        await InsertRequestEventAsync(
            connection, transaction, context.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(context, receiptId, confirmedAt);
    }

    private static TelehealthApplicantRequestInsuranceSourceRecord CreateRecord(
        TelehealthApplicantRequestInsuranceSourceContext context,
        Guid? confirmationId,
        DateTimeOffset? confirmedAt)
    {
        var snapshot = CreateSnapshot(context);
        return new(
            context.ApplicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestInsuranceSourcePolicy.ApplicantStatus,
            context.RequestId,
            confirmationId is null ? context.RequestVersion : TelehealthApplicantRequestInsuranceSourcePolicy.ResultingRequestVersion,
            TelehealthApplicantRequestInsuranceSourcePolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            snapshot.PayerDisplayName,
            snapshot.ProductDisplayName,
            context.MemberIdLast4,
            context.GroupNumberLast4,
            snapshot.SubscriberRelationship,
            snapshot.CoveragePriority,
            snapshot.PreviousEligibilityBusinessOutcome,
            snapshot.PreviousEligibilityCheckedAt,
            snapshot.PreviousEligibilityExpiresAt,
            snapshot.PreviousPracticeNetworkBusinessOutcome,
            snapshot.PreviousPracticeNetworkCheckedAt,
            snapshot.PreviousPracticeNetworkExpiresAt,
            confirmationId,
            confirmedAt,
            context.DatabaseNow);
    }

    private static TelehealthApplicantRequestInsuranceSourceSnapshot CreateSnapshot(
        TelehealthApplicantRequestInsuranceSourceContext context) =>
        TelehealthApplicantRequestInsuranceSourcePolicy.Snapshot(
            context.ApplicantId,
            context.RequestId,
            context.RequestIntakeReceiptId,
            context.RequestCreationId,
            context.InsuranceHandoffConfirmationId,
            context.MemberInsuranceDetailsId,
            context.EligibilityResultId,
            context.NetworkDeterminationId,
            context.PromotionId,
            context.PracticeReviewCaseId,
            context.PracticeReviewAuthorizationId,
            TelehealthApplicantRequestInsuranceSourcePolicy.EntryRequestVersion,
            context.CanonicalPatientId,
            context.SourceInsuranceSnapshotFingerprint,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.MemberIdLast4,
            context.GroupNumberLast4,
            context.SubscriberRelationship,
            context.CoveragePriority,
            context.PreviousEligibilityBusinessOutcome,
            context.PreviousEligibilityCheckedAt,
            context.PreviousEligibilityExpiresAt,
            context.PreviousPracticeNetworkBusinessOutcome,
            context.PreviousPracticeNetworkCheckedAt,
            context.PreviousPracticeNetworkExpiresAt,
            context.RequestIntakeCapturedAt,
            context.ContextExpiresAt,
            context.ApplicantExpiresAt);

    private static async Task<TelehealthApplicantRequestInsuranceSourceApplicant?> LoadApplicantAsync(
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
            select version,status,access_key_hash,expires_at,now()
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
                Convert.ToInt32(reader.GetInt64(0)), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    private static async Task<TelehealthApplicantRequestInsuranceSourceContext?> LoadContextAsync(
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
            select a.applicant_id,a.version,a.expires_at,now(),
                   r.request_id,r.version,r.status,r.triage_outcome,creation.canonical_patient_id,
                   creation.creation_id,creation.promotion_id,creation.practice_review_case_id,
                   creation.practice_review_authorization_id,intake.receipt_id,intake.captured_at,
                   intake.context_expires_at,handoff.confirmation_id,handoff.member_insurance_details_id,
                   handoff.eligibility_result_id,handoff.network_determination_id,
                   handoff.insurance_snapshot_fingerprint,handoff.payer_display_name,
                   handoff.product_display_name,handoff.member_id_last4,handoff.group_number_present,
                   handoff.group_number_last4,handoff.subscriber_relationship,handoff.coverage_priority,
                   eligibility.business_outcome,eligibility.checked_at,eligibility.expires_at,
                   network.business_outcome,network.checked_at,network.expires_at,
                   network.rendering_physician_network_checked,
                   (select count(*) from telehealth_intake_snapshots x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_intake_snapshots x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_insurance_source_confirmations x where x.request_id=r.request_id),
                   (select count(*) from insurance_records x where lower(x.patient_id)=lower(creation.canonical_patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (handoff.payer_product_confirmed and handoff.masked_member_details_confirmed
                    and handoff.subscriber_relationship_confirmed
                    and handoff.evidence_limitations_acknowledged and handoff.synthetic_data_confirmed
                    and not handoff.coverage_verified and not handoff.exact_network_confirmed
                    and not handoff.canonical_coverage_created and not handoff.patient_record_changed
                    and member.details_confirmed and member.synthetic_data_confirmed
                    and member.protection_scheme='ASP.NET_CORE_DATA_PROTECTION'
                    and member.protection_version=1 and length(member.protected_payload)>=64
                    and eligibility.adapter_mode='NON_PRODUCTION'
                    and network.adapter_mode='NON_PRODUCTION'
                    and not network.rendering_physician_network_checked
                    and not network.exact_network_confirmed and not network.coverage_verified
                    and intake.intake_snapshot_created and intake.request_advanced_to_verification
                    and not intake.coverage_record_created and not intake.coverage_verified
                    and not intake.exact_network_confirmed and not intake.operational_review_created),
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
            join telehealth_applicant_request_intake_snapshots intake
              on intake.request_id=r.request_id and intake.applicant_id=a.applicant_id
             and intake.request_creation_id=creation.creation_id
            join telehealth_applicant_insurance_handoff_confirmations handoff
              on handoff.applicant_id=a.applicant_id and handoff.promotion_id=creation.promotion_id
             and handoff.canonical_patient_id=creation.canonical_patient_id
            join telehealth_applicant_member_insurance_details member
              on member.details_id=handoff.member_insurance_details_id and member.applicant_id=a.applicant_id
            join telehealth_applicant_eligibility_results eligibility
              on eligibility.eligibility_result_id=handoff.eligibility_result_id
             and eligibility.member_insurance_details_id=member.details_id
            join telehealth_applicant_practice_network_determinations network
              on network.network_determination_id=handoff.network_determination_id
             and network.eligibility_result_id=eligibility.eligibility_result_id
             and network.member_insurance_details_id=member.details_id
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
              and intake.applicant_version=26 and intake.resulting_request_status='Verification'
              and intake.resulting_request_version=5
              and handoff.resulting_applicant_status='SyntheticInsuranceDetailsConfirmed'
              and handoff.coverage_priority='Primary'
              and r.status='Verification' and r.version in (5,6)
              and r.triage_outcome='TelehealthEligible' and r.ready_at is null
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
                reader.GetGuid(4), Convert.ToInt32(reader.GetInt64(5)), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8),
                reader.GetGuid(9), reader.GetGuid(10), reader.GetGuid(11), reader.GetGuid(12),
                reader.GetGuid(13), reader.GetFieldValue<DateTimeOffset>(14),
                reader.GetFieldValue<DateTimeOffset>(15), reader.GetGuid(16), reader.GetGuid(17),
                reader.GetGuid(18), reader.GetGuid(19), reader.GetString(20), reader.GetString(21),
                reader.GetString(22), reader.GetString(23), reader.GetBoolean(24),
                reader.IsDBNull(25) ? null : reader.GetString(25), reader.GetString(26),
                reader.GetString(27), reader.GetString(28), reader.GetFieldValue<DateTimeOffset>(29),
                reader.GetFieldValue<DateTimeOffset>(30), reader.GetString(31),
                reader.GetFieldValue<DateTimeOffset>(32), reader.GetFieldValue<DateTimeOffset>(33),
                reader.GetBoolean(34), Convert.ToInt32(reader.GetInt64(35)),
                Convert.ToInt32(reader.GetInt64(36)), Convert.ToInt32(reader.GetInt64(37)),
                Convert.ToInt32(reader.GetInt64(38)), Convert.ToInt32(reader.GetInt64(39)),
                reader.GetBoolean(40), reader.GetBoolean(41))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestInsuranceSourceRecord Record,
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
                   receipt.insurance_source_snapshot_fingerprint,receipt.context_expires_at,
                   receipt.payer_display_name,receipt.product_display_name,receipt.member_id_last4,
                   receipt.group_number_last4,receipt.subscriber_relationship,receipt.coverage_priority,
                   receipt.previous_eligibility_business_outcome,
                   receipt.previous_eligibility_checked_at,receipt.previous_eligibility_expires_at,
                   receipt.previous_practice_network_business_outcome,
                   receipt.previous_practice_network_checked_at,
                   receipt.previous_practice_network_expires_at,
                   receipt.confirmation_id,receipt.confirmed_at,receipt.command_fingerprint,now()
            from telehealth_applicant_request_insurance_source_confirmations receipt
            join telehealth_prospective_applicants a on a.applicant_id=receipt.applicant_id
            where receipt.applicant_id=@applicantId and receipt.practice_id=@practiceId
              and receipt.facility_id=@facilityId
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
            reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
            reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetString(12), reader.GetString(13), reader.GetString(14),
            reader.GetFieldValue<DateTimeOffset>(15), reader.GetFieldValue<DateTimeOffset>(16),
            reader.GetString(17), reader.GetFieldValue<DateTimeOffset>(18),
            reader.GetFieldValue<DateTimeOffset>(19), reader.GetGuid(20),
            reader.GetFieldValue<DateTimeOffset>(21), reader.GetFieldValue<DateTimeOffset>(23)),
            reader.GetString(22));
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
            values(@eventId,@requestId,6,'applicant-insurance-source-confirmed',
                   'Verification','Verification','applicant',@actorId,
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
        TelehealthApplicantRequestInsuranceSourceApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestInsuranceSourceApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestInsuranceSourcePolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestInsuranceSourcePolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_state_conflict",
                "The applicant is not eligible for request insurance-source confirmation.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestInsuranceSourceContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestInsuranceSourcePolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestInsuranceSourcePolicy.EntryRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.PreviousRenderingPhysicianNetworkChecked
            || context.GenericIntakeCount != 1
            || context.IntakeReceiptCount != 1
            || context.InsuranceSourceCount != 0
            || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0
            || !context.SourceEvidenceComplete
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
        if (CreateSnapshot(context).ContextExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_insurance_source_context_expired",
                "The request context expired. Restart or request review.");
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestInsuranceSourceContext context,
        TelehealthApplicantRequestInsuranceSourceRecord record)
    {
        if (context.RequestStatus != TelehealthApplicantRequestInsuranceSourcePolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestInsuranceSourcePolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.PayerDisplayName != record.PayerDisplayName
            || context.ProductDisplayName != record.ProductDisplayName
            || context.MemberIdLast4 != record.MemberIdLast4
            || context.GroupNumberLast4 != record.GroupNumberLast4
            || context.GenericIntakeCount != 1
            || context.IntakeReceiptCount != 1
            || context.InsuranceSourceCount != 1
            || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0
            || !context.SourceEvidenceComplete
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_insurance_source_provenance_conflict",
        "The request insurance source or its authorized evidence is unavailable or changed.");
}
