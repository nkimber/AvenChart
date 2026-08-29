// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestEligibilityRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string EligibilitySnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdLast4,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string CurrentLocationStateCode,
    string PurposeCategory,
    Guid? VerificationId,
    DateOnly? DateOfService,
    TelehealthProspectiveEligibilityAdapterResult? AdapterResult,
    DateTimeOffset DatabaseNow);

public sealed record TelehealthApplicantRequestEligibilityCandidate(
    string PlanKey,
    string MemberIdLast4,
    bool GroupNumberPresent,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string ProtectedPayload,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestEligibilityApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestEligibilityContext(
    Guid ApplicantId,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? RequestTriageOutcome,
    string CanonicalPatientId,
    Guid InsuranceSourceConfirmationId,
    Guid MemberInsuranceDetailsId,
    string InsuranceSourceSnapshotFingerprint,
    DateTimeOffset SourceConfirmedAt,
    DateTimeOffset ContextExpiresAt,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string MemberIdLast4,
    bool GroupNumberPresent,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string ProtectedPayload,
    string ProtectionScheme,
    string ProtectionPurpose,
    int ProtectionVersion,
    string CurrentLocationStateCode,
    string PurposeCategory,
    int InsuranceSourceCount,
    int RequestEligibilityCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestEligibilityRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestEligibilityRecord> GetAsync(
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
        var completed = await LoadResultAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompletedContext(context, completed.Value.Record);
            return completed.Value.Record;
        }

        RequireReadyContext(context);
        return CreatePendingRecord(context);
    }

    public async Task<TelehealthApplicantRequestEligibilityRecord> RunAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestEligibilityCommand command,
        string idempotencyKey,
        string commandFingerprint,
        Func<TelehealthApplicantRequestEligibilityCandidate, CancellationToken,
            ValueTask<TelehealthProspectiveEligibilityAdapterResult>> resolveEligibility,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireApplicant(applicant);

        var replay = await LoadResultAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            var replayContext = await LoadContextAsync(
                connection, transaction, practiceId, facilityId, applicantId, false, cancellationToken)
                ?? throw ProvenanceConflict();
            RequireCompletedContext(replayContext, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        RequireReadyContext(context);
        if (command.ExpectedRequestVersion != context.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_eligibility_version_conflict",
                "The request changed before eligibility verification. Reload and try again.");
        }

        var snapshot = CreateSnapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                command.EligibilitySnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_eligibility_snapshot_stale",
                "The eligibility verification context changed. Reload and try again.");
        }

        var adapter = await resolveEligibility(
            new(
                context.PlanKey,
                context.MemberIdLast4,
                context.GroupNumberPresent,
                context.GroupNumberLast4,
                context.SubscriberRelationship,
                context.CoveragePriority,
                context.ProtectedPayload,
                context.DatabaseNow),
            cancellationToken);
        var verificationId = Guid.NewGuid();
        var dateOfService = DateOnly.FromDateTime(adapter.CheckedAt.UtcDateTime);
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_eligibility_verifications(
                  verification_id,request_id,applicant_id,insurance_source_confirmation_id,
                  member_insurance_details_id,practice_id,facility_id,canonical_patient_id,
                  applicant_version,source_request_version,resulting_request_version,
                  source_request_status,resulting_request_status,eligibility_snapshot_fingerprint,
                  insurance_source_snapshot_fingerprint,plan_key,payer_display_name,
                  product_display_name,member_id_last4,group_number_present,group_number_last4,
                  subscriber_relationship,coverage_priority,current_location_state_code,
                  purpose_category,date_of_service,service_category,adapter_mode,
                  compatibility_target,dataset_key,dataset_version,dataset_effective_from,
                  dataset_effective_through,inquiry_trace_token,response_trace_token,
                  transport_outcome,member_match_status,eligibility_status,
                  benefit_information_status,business_outcome,member_matched,
                  member_eligibility_checked,member_benefits_checked,checked_at,expires_at,
                  context_expires_at,applicant_expires_at,synthetic_data_confirmed,
                  no_guarantee_acknowledged,policy_key,policy_version,evidence_type,
                  idempotency_key,command_fingerprint,verified_at)
                values(
                  @verificationId,@requestId,@applicantId,@sourceId,@memberId,
                  @practiceId,@facilityId,@patientId,@applicantVersion,6,7,
                  'Verification','Verification',@snapshotFingerprint,@sourceFingerprint,
                  @planKey,@payer,@product,@memberLast4,@groupPresent,@groupLast4,
                  @relationship,@priority,@state,@purpose,@dateOfService,@serviceCategory,
                  @adapterMode,@compatibilityTarget,@datasetKey,@datasetVersion,
                  @datasetFrom,@datasetThrough,@inquiryTrace,@responseTrace,@transport,
                  @match,@eligibility,@benefits,@business,@memberMatched,@eligibilityChecked,
                  @benefitsChecked,@checkedAt,@expiresAt,@contextExpiresAt,@applicantExpiresAt,
                  true,true,@policyKey,@policyVersion,@evidenceType,@idempotencyKey,
                  @commandFingerprint,@checkedAt)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("verificationId", verificationId);
            insert.Parameters.AddWithValue("requestId", context.RequestId);
            insert.Parameters.AddWithValue("applicantId", context.ApplicantId);
            insert.Parameters.AddWithValue("sourceId", context.InsuranceSourceConfirmationId);
            insert.Parameters.AddWithValue("memberId", context.MemberInsuranceDetailsId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            insert.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("sourceFingerprint", context.InsuranceSourceSnapshotFingerprint);
            insert.Parameters.AddWithValue("planKey", context.PlanKey);
            insert.Parameters.AddWithValue("payer", context.PayerDisplayName);
            insert.Parameters.AddWithValue("product", context.ProductDisplayName);
            insert.Parameters.AddWithValue("memberLast4", context.MemberIdLast4);
            insert.Parameters.AddWithValue("groupPresent", context.GroupNumberPresent);
            insert.Parameters.AddWithValue("groupLast4", (object?)context.GroupNumberLast4 ?? DBNull.Value);
            insert.Parameters.AddWithValue("relationship", context.SubscriberRelationship);
            insert.Parameters.AddWithValue("priority", context.CoveragePriority);
            insert.Parameters.AddWithValue("state", context.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purpose", context.PurposeCategory);
            insert.Parameters.AddWithValue("dateOfService", dateOfService);
            insert.Parameters.AddWithValue("serviceCategory", SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory);
            insert.Parameters.AddWithValue("adapterMode", adapter.AdapterMode);
            insert.Parameters.AddWithValue("compatibilityTarget", adapter.CompatibilityTarget);
            insert.Parameters.AddWithValue("datasetKey", adapter.DatasetKey);
            insert.Parameters.AddWithValue("datasetVersion", adapter.DatasetVersion);
            insert.Parameters.AddWithValue("datasetFrom", adapter.DatasetEffectiveFrom);
            insert.Parameters.AddWithValue("datasetThrough", adapter.DatasetEffectiveThrough);
            insert.Parameters.AddWithValue("inquiryTrace", adapter.InquiryTraceToken);
            insert.Parameters.AddWithValue("responseTrace", adapter.ResponseTraceToken);
            insert.Parameters.AddWithValue("transport", adapter.TransportOutcome);
            insert.Parameters.AddWithValue("match", adapter.MemberMatchStatus);
            insert.Parameters.AddWithValue("eligibility", adapter.EligibilityStatus);
            insert.Parameters.AddWithValue("benefits", adapter.BenefitInformationStatus);
            insert.Parameters.AddWithValue("business", adapter.BusinessOutcome);
            insert.Parameters.AddWithValue("memberMatched", adapter.MemberMatched);
            insert.Parameters.AddWithValue("eligibilityChecked", adapter.MemberEligibilityChecked);
            insert.Parameters.AddWithValue("benefitsChecked", adapter.MemberBenefitsChecked);
            insert.Parameters.AddWithValue("checkedAt", adapter.CheckedAt);
            insert.Parameters.AddWithValue("expiresAt", adapter.ExpiresAt);
            insert.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestEligibilityPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestEligibilityPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestEligibilityPolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var recordedAtValue = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Request eligibility result time is unavailable.");
            recordedAt = recordedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(
                    DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Request eligibility result time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set version=7,updated_at=now()
                where request_id=@requestId and status='Verification' and version=6;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_eligibility_version_conflict",
                    "The request changed before eligibility verification. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, context.RequestId, applicantId,
            idempotencyKey, commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            context.ApplicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestEligibilityPolicy.ApplicantStatus,
            context.RequestId,
            TelehealthApplicantRequestEligibilityPolicy.ResultingRequestVersion,
            TelehealthApplicantRequestEligibilityPolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.MemberIdLast4,
            context.GroupNumberLast4,
            context.SubscriberRelationship,
            context.CoveragePriority,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            verificationId,
            dateOfService,
            adapter,
            recordedAt);
    }

    private static TelehealthApplicantRequestEligibilityRecord CreatePendingRecord(
        TelehealthApplicantRequestEligibilityContext context)
    {
        var snapshot = CreateSnapshot(context);
        return new(
            context.ApplicantId, context.ApplicantVersion,
            TelehealthApplicantRequestEligibilityPolicy.ApplicantStatus,
            context.RequestId, context.RequestVersion, context.RequestStatus,
            snapshot.Fingerprint, snapshot.ContextExpiresAt,
            context.PayerDisplayName, context.ProductDisplayName,
            context.MemberIdLast4, context.GroupNumberLast4,
            context.SubscriberRelationship, context.CoveragePriority,
            context.CurrentLocationStateCode, context.PurposeCategory,
            null, null, null, context.DatabaseNow);
    }

    private static TelehealthApplicantRequestEligibilitySnapshot CreateSnapshot(
        TelehealthApplicantRequestEligibilityContext context) =>
        TelehealthApplicantRequestEligibilityPolicy.Snapshot(
            context.ApplicantId,
            context.RequestId,
            context.InsuranceSourceConfirmationId,
            context.MemberInsuranceDetailsId,
            context.RequestVersion,
            context.CanonicalPatientId,
            context.InsuranceSourceSnapshotFingerprint,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.MemberIdLast4,
            context.GroupNumberLast4,
            context.SubscriberRelationship,
            context.CoveragePriority,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.SourceConfirmedAt,
            context.ContextExpiresAt,
            context.ApplicantExpiresAt);

    private static async Task<TelehealthApplicantRequestEligibilityApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestEligibilityContext?> LoadContextAsync(
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
            select a.applicant_id,a.version,a.expires_at,now(),r.request_id,r.version,
                   r.status,r.triage_outcome,creation.canonical_patient_id,
                   source.confirmation_id,source.member_insurance_details_id,
                   source.insurance_source_snapshot_fingerprint,source.confirmed_at,
                   source.context_expires_at,member.plan_key,source.payer_display_name,
                   source.product_display_name,source.member_id_last4,
                   source.group_number_present,source.group_number_last4,
                   source.subscriber_relationship,source.coverage_priority,
                   member.protected_payload,member.protection_scheme,
                   member.protection_purpose,member.protection_version,
                   intake.current_location_state_code,intake.complaint_category,
                   (select count(*) from telehealth_applicant_request_insurance_source_confirmations x
                     where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_eligibility_verifications x
                     where x.request_id=r.request_id),
                   (select count(*) from insurance_records x
                     where lower(x.patient_id)=lower(creation.canonical_patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (source.applicant_version=26 and source.source_request_version=5
                    and source.resulting_request_version=6
                    and source.source_request_status='Verification'
                    and source.resulting_request_status='Verification'
                    and source.fresh_verification_requested
                    and source.evidence_limitations_acknowledged and source.synthetic_data_confirmed
                    and source.protected_payload_referenced and not source.protected_payload_copied
                    and not source.protected_payload_decrypted and not source.prior_result_reused
                    and not source.canonical_coverage_created and not source.generic_coverage_selected
                    and not source.eligibility_verification_created
                    and not source.network_verification_created and not source.coverage_verified
                    and not source.exact_network_confirmed and not source.operational_review_created
                    and member.details_id=source.member_insurance_details_id
                    and member.payer_display_name=source.payer_display_name
                    and member.product_display_name=source.product_display_name
                    and member.member_id_last4=source.member_id_last4
                    and member.group_number_present=source.group_number_present
                    and member.group_number_last4 is not distinct from source.group_number_last4
                    and member.subscriber_relationship=source.subscriber_relationship
                    and member.coverage_priority=source.coverage_priority
                    and member.details_confirmed and member.synthetic_data_confirmed),
                   r.appointment_id is not null
            from telehealth_prospective_applicants a
            join telehealth_applicant_request_creations creation
              on creation.applicant_id=a.applicant_id and creation.practice_id=a.practice_id
             and creation.facility_id=a.facility_id and creation.resulting_applicant_version=a.version
             and creation.resulting_applicant_status=a.status
            join telehealth_requests r
              on r.request_id=creation.request_id and r.source_applicant_id=a.applicant_id
             and r.patient_id=creation.canonical_patient_id and r.practice_id=a.practice_id
             and r.facility_id=a.facility_id
            join telehealth_applicant_request_insurance_source_confirmations source
              on source.request_id=r.request_id and source.applicant_id=a.applicant_id
             and source.request_creation_id=creation.creation_id
            join telehealth_applicant_request_intake_snapshots intake
              on intake.receipt_id=source.request_intake_receipt_id and intake.request_id=r.request_id
            join telehealth_applicant_member_insurance_details member
              on member.details_id=source.member_insurance_details_id and member.applicant_id=a.applicant_id
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id and patient.facility_id=a.facility_id
             and patient.lifecycle_status='active' and not patient.portal_enabled
             and patient.merged_into_patient_id is null
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated'
              and a.version=26 and a.expires_at>now()
              and r.status='Verification' and r.version in (6,7)
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
                reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetGuid(9),
                reader.GetGuid(10), reader.GetString(11), reader.GetFieldValue<DateTimeOffset>(12),
                reader.GetFieldValue<DateTimeOffset>(13), reader.GetString(14), reader.GetString(15),
                reader.GetString(16), reader.GetString(17), reader.GetBoolean(18),
                reader.IsDBNull(19) ? null : reader.GetString(19), reader.GetString(20),
                reader.GetString(21), reader.GetString(22), reader.GetString(23), reader.GetString(24),
                reader.GetInt32(25), reader.GetString(26), reader.GetString(27),
                Convert.ToInt32(reader.GetInt64(28)), Convert.ToInt32(reader.GetInt64(29)),
                Convert.ToInt32(reader.GetInt64(30)), Convert.ToInt32(reader.GetInt64(31)),
                reader.GetBoolean(32), reader.GetBoolean(33))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestEligibilityRecord Record,
        string CommandFingerprint)?> LoadResultAsync(
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
            select v.applicant_id,v.applicant_version,a.status,v.request_id,
                   v.resulting_request_version,v.resulting_request_status,
                   v.eligibility_snapshot_fingerprint,v.context_expires_at,
                   v.payer_display_name,v.product_display_name,v.member_id_last4,
                   v.group_number_last4,v.subscriber_relationship,v.coverage_priority,
                   v.current_location_state_code,v.purpose_category,v.verification_id,
                   v.date_of_service,v.adapter_mode,v.compatibility_target,v.dataset_key,
                   v.dataset_version,v.dataset_effective_from,v.dataset_effective_through,
                   v.inquiry_trace_token,v.response_trace_token,v.transport_outcome,
                   v.member_match_status,v.eligibility_status,v.benefit_information_status,
                   v.business_outcome,v.member_matched,v.member_eligibility_checked,
                   v.member_benefits_checked,v.checked_at,v.expires_at,v.recorded_at,
                   v.command_fingerprint
            from telehealth_applicant_request_eligibility_verifications v
            join telehealth_prospective_applicants a on a.applicant_id=v.applicant_id
            where v.applicant_id=@applicantId and v.practice_id=@practiceId
              and v.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and v.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null) command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var adapter = new TelehealthProspectiveEligibilityAdapterResult(
            reader.GetString(18), reader.GetString(19), reader.GetString(20), reader.GetInt32(21),
            reader.GetFieldValue<DateTimeOffset>(22), reader.GetFieldValue<DateTimeOffset>(23),
            reader.GetGuid(24), reader.GetGuid(25), reader.GetString(26), reader.GetString(27),
            reader.GetString(28), reader.GetString(29), reader.GetString(30), reader.GetBoolean(31),
            reader.GetBoolean(32), reader.GetBoolean(33), reader.GetFieldValue<DateTimeOffset>(34),
            reader.GetFieldValue<DateTimeOffset>(35));
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12), reader.GetString(13), reader.GetString(14), reader.GetString(15),
                reader.GetGuid(16), reader.GetFieldValue<DateOnly>(17), adapter,
                reader.GetFieldValue<DateTimeOffset>(36)),
            reader.GetString(37));
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
            values(@eventId,@requestId,7,'applicant-eligibility-verification-recorded',
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
        TelehealthApplicantRequestEligibilityApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestEligibilityApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestEligibilityPolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestEligibilityPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_eligibility_state_conflict",
                "The applicant is not eligible for request-time eligibility verification.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestEligibilityContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestEligibilityPolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestEligibilityPolicy.EntryRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || context.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || context.PurposeCategory is not ("migraine" or "sleep")
            || context.GroupNumberPresent != (context.GroupNumberLast4 is not null)
            || context.SubscriberRelationship is not ("Self" or "Spouse" or "Parent" or "Other")
            || context.CoveragePriority != "Primary"
            || context.ProtectionScheme != TelehealthProspectiveMemberInsuranceDetailsProtector.Scheme
            || context.ProtectionPurpose != TelehealthProspectiveMemberInsuranceDetailsProtector.Purpose
            || context.ProtectionVersion != TelehealthProspectiveMemberInsuranceDetailsProtector.Version
            || string.IsNullOrWhiteSpace(context.ProtectedPayload)
            || context.InsuranceSourceCount != 1
            || context.RequestEligibilityCount != 0
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
                "telehealth_applicant_request_eligibility_context_expired",
                "The request eligibility context expired. Restart or request review.");
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestEligibilityContext context,
        TelehealthApplicantRequestEligibilityRecord record)
    {
        if (context.RequestStatus != TelehealthApplicantRequestEligibilityPolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestEligibilityPolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.InsuranceSourceCount != 1
            || context.RequestEligibilityCount != 1
            || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0
            || !context.SourceEvidenceComplete
            || context.AppointmentCreated
            || record.VerificationId is null
            || record.AdapterResult is null
            || context.PayerDisplayName != record.PayerDisplayName
            || context.ProductDisplayName != record.ProductDisplayName
            || context.MemberIdLast4 != record.MemberIdLast4
            || context.GroupNumberLast4 != record.GroupNumberLast4)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_eligibility_idempotency_conflict",
                "The eligibility idempotency key was already used with different command content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_eligibility_provenance_conflict",
        "The request insurance source or its authorized eligibility context is unavailable or changed.");
}
