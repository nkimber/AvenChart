// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestPracticeNetworkRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string NetworkSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    Guid EligibilityVerificationId,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    Guid? VerificationId,
    DateOnly? DateOfService,
    TelehealthProspectivePracticeNetworkAdapterResult? AdapterResult,
    DateTimeOffset DatabaseNow);

public sealed record TelehealthApplicantRequestPracticeNetworkCandidate(
    string PracticeId,
    string PracticeDisplayName,
    int FacilityId,
    string PlanKey,
    string CurrentLocationStateCode,
    DateOnly DateOfService,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestPracticeNetworkApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestPracticeNetworkContext(
    Guid ApplicantId,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string? RequestTriageOutcome,
    string CanonicalPatientId,
    Guid EligibilityVerificationId,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    string ServiceCategory,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityCheckedAt,
    DateTimeOffset EligibilityExpiresAt,
    int RequestEligibilityCount,
    int RequestPracticeNetworkCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool EligibilityEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestPracticeNetworkRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestPracticeNetworkRecord> GetAsync(
        string practiceId,
        string practiceDisplayName,
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
        return CreatePendingRecord(context, practiceId, practiceDisplayName, facilityId);
    }

    public async Task<TelehealthApplicantRequestPracticeNetworkRecord> RunAsync(
        string practiceId,
        string practiceDisplayName,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestPracticeNetworkCommand command,
        string idempotencyKey,
        string commandFingerprint,
        Func<TelehealthApplicantRequestPracticeNetworkCandidate, CancellationToken,
            ValueTask<TelehealthProspectivePracticeNetworkAdapterResult>> resolveNetwork,
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
                "telehealth_applicant_request_practice_network_version_conflict",
                "The request changed before practice-network verification. Reload and try again.");
        }

        var snapshot = CreateSnapshot(context, practiceId, practiceDisplayName, facilityId);
        if (!string.Equals(
                snapshot.Fingerprint,
                command.NetworkSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_practice_network_snapshot_stale",
                "The practice-network verification context changed. Reload and try again.");
        }

        var adapter = await resolveNetwork(
            new(
                practiceId,
                practiceDisplayName,
                facilityId,
                context.PlanKey,
                context.CurrentLocationStateCode,
                context.DateOfService,
                context.DatabaseNow),
            cancellationToken);
        var verificationId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_practice_network_verifications(
                  verification_id,request_id,applicant_id,eligibility_verification_id,
                  practice_id,facility_id,canonical_patient_id,applicant_version,
                  source_request_version,resulting_request_version,source_request_status,
                  resulting_request_status,network_snapshot_fingerprint,plan_key,
                  payer_display_name,product_display_name,practice_display_name,
                  current_location_state_code,purpose_category,date_of_service,service_category,
                  eligibility_business_outcome,eligibility_checked_at,eligibility_expires_at,
                  adapter_mode,compatibility_target,dataset_key,dataset_version,
                  dataset_effective_from,dataset_effective_through,source_last_updated_at,
                  request_trace_token,response_trace_token,transport_outcome,
                  plan_network_match_status,practice_affiliation_status,
                  service_availability_status,new_patient_acceptance_status,business_outcome,
                  practice_network_checked,practice_in_network,new_patients_accepted,
                  network_reference,organization_reference,location_reference,service_reference,
                  checked_at,expires_at,context_expires_at,applicant_expires_at,
                  synthetic_data_confirmed,practice_only_scope_acknowledged,
                  no_guarantee_acknowledged,policy_key,policy_version,evidence_type,
                  idempotency_key,command_fingerprint,verified_at)
                values(
                  @verificationId,@requestId,@applicantId,@eligibilityId,@practiceId,@facilityId,
                  @patientId,@applicantVersion,7,8,'Verification','Verification',@snapshotFingerprint,
                  @planKey,@payer,@product,@practiceDisplayName,@state,@purpose,@dateOfService,
                  @serviceCategory,@eligibilityBusiness,@eligibilityCheckedAt,@eligibilityExpiresAt,
                  @adapterMode,@compatibilityTarget,@datasetKey,@datasetVersion,@datasetFrom,
                  @datasetThrough,@sourceUpdatedAt,@requestTrace,@responseTrace,@transport,
                  @planMatch,@affiliation,@serviceAvailability,@newPatientStatus,@business,
                  @networkChecked,@practiceInNetwork,@newPatientsAccepted,@networkReference,
                  @organizationReference,@locationReference,@serviceReference,@checkedAt,@expiresAt,
                  @contextExpiresAt,@applicantExpiresAt,true,true,true,@policyKey,@policyVersion,
                  @evidenceType,@idempotencyKey,@commandFingerprint,@checkedAt)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("verificationId", verificationId);
            insert.Parameters.AddWithValue("requestId", context.RequestId);
            insert.Parameters.AddWithValue("applicantId", context.ApplicantId);
            insert.Parameters.AddWithValue("eligibilityId", context.EligibilityVerificationId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            insert.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("planKey", context.PlanKey);
            insert.Parameters.AddWithValue("payer", context.PayerDisplayName);
            insert.Parameters.AddWithValue("product", context.ProductDisplayName);
            insert.Parameters.AddWithValue("practiceDisplayName", practiceDisplayName);
            insert.Parameters.AddWithValue("state", context.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purpose", context.PurposeCategory);
            insert.Parameters.AddWithValue("dateOfService", context.DateOfService);
            insert.Parameters.AddWithValue("serviceCategory", context.ServiceCategory);
            insert.Parameters.AddWithValue("eligibilityBusiness", context.EligibilityBusinessOutcome);
            insert.Parameters.AddWithValue("eligibilityCheckedAt", context.EligibilityCheckedAt);
            insert.Parameters.AddWithValue("eligibilityExpiresAt", context.EligibilityExpiresAt);
            insert.Parameters.AddWithValue("adapterMode", adapter.AdapterMode);
            insert.Parameters.AddWithValue("compatibilityTarget", adapter.CompatibilityTarget);
            insert.Parameters.AddWithValue("datasetKey", adapter.DatasetKey);
            insert.Parameters.AddWithValue("datasetVersion", adapter.DatasetVersion);
            insert.Parameters.AddWithValue("datasetFrom", adapter.DatasetEffectiveFrom);
            insert.Parameters.AddWithValue("datasetThrough", adapter.DatasetEffectiveThrough);
            insert.Parameters.AddWithValue("sourceUpdatedAt", adapter.SourceLastUpdatedAt);
            insert.Parameters.AddWithValue("requestTrace", adapter.RequestTraceToken);
            insert.Parameters.AddWithValue("responseTrace", adapter.ResponseTraceToken);
            insert.Parameters.AddWithValue("transport", adapter.TransportOutcome);
            insert.Parameters.AddWithValue("planMatch", adapter.PlanNetworkMatchStatus);
            insert.Parameters.AddWithValue("affiliation", adapter.PracticeAffiliationStatus);
            insert.Parameters.AddWithValue("serviceAvailability", adapter.ServiceAvailabilityStatus);
            insert.Parameters.AddWithValue("newPatientStatus", adapter.NewPatientAcceptanceStatus);
            insert.Parameters.AddWithValue("business", adapter.BusinessOutcome);
            insert.Parameters.AddWithValue("networkChecked", adapter.PracticeNetworkChecked);
            insert.Parameters.AddWithValue("practiceInNetwork", adapter.PracticeInNetwork);
            insert.Parameters.AddWithValue("newPatientsAccepted", adapter.NewPatientsAccepted);
            insert.Parameters.AddWithValue("networkReference", (object?)adapter.NetworkReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("organizationReference", (object?)adapter.OrganizationReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("locationReference", (object?)adapter.LocationReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("serviceReference", (object?)adapter.ServiceReference ?? DBNull.Value);
            insert.Parameters.AddWithValue("checkedAt", adapter.CheckedAt);
            insert.Parameters.AddWithValue("expiresAt", adapter.ExpiresAt);
            insert.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestPracticeNetworkPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestPracticeNetworkPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestPracticeNetworkPolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var recordedAtValue = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Practice-network result time is unavailable.");
            recordedAt = recordedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Practice-network result time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set version=8,updated_at=now()
                where request_id=@requestId and status='Verification' and version=7;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_practice_network_version_conflict",
                    "The request changed before practice-network verification. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, context.RequestId, applicantId,
            idempotencyKey, commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            context.ApplicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestPracticeNetworkPolicy.ApplicantStatus,
            context.RequestId,
            TelehealthApplicantRequestPracticeNetworkPolicy.ResultingRequestVersion,
            TelehealthApplicantRequestPracticeNetworkPolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            practiceDisplayName,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.EligibilityVerificationId,
            context.EligibilityBusinessOutcome,
            context.EligibilityCheckedAt,
            context.EligibilityExpiresAt,
            verificationId,
            context.DateOfService,
            adapter,
            recordedAt);
    }

    private static TelehealthApplicantRequestPracticeNetworkRecord CreatePendingRecord(
        TelehealthApplicantRequestPracticeNetworkContext context,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var snapshot = CreateSnapshot(context, practiceId, practiceDisplayName, facilityId);
        return new(
            context.ApplicantId, context.ApplicantVersion,
            TelehealthApplicantRequestPracticeNetworkPolicy.ApplicantStatus,
            context.RequestId, context.RequestVersion, context.RequestStatus,
            snapshot.Fingerprint, snapshot.ContextExpiresAt, practiceDisplayName,
            context.PayerDisplayName, context.ProductDisplayName,
            context.CurrentLocationStateCode, context.PurposeCategory,
            context.EligibilityVerificationId, context.EligibilityBusinessOutcome,
            context.EligibilityCheckedAt, context.EligibilityExpiresAt,
            null, null, null, context.DatabaseNow);
    }

    private static TelehealthApplicantRequestPracticeNetworkSnapshot CreateSnapshot(
        TelehealthApplicantRequestPracticeNetworkContext context,
        string practiceId,
        string practiceDisplayName,
        int facilityId) =>
        TelehealthApplicantRequestPracticeNetworkPolicy.Snapshot(
            context.ApplicantId,
            context.RequestId,
            context.EligibilityVerificationId,
            context.RequestVersion,
            context.CanonicalPatientId,
            practiceId,
            facilityId,
            practiceDisplayName,
            context.PlanKey,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.EligibilityBusinessOutcome,
            context.EligibilityCheckedAt,
            context.EligibilityExpiresAt,
            context.ApplicantExpiresAt);

    private static async Task<TelehealthApplicantRequestPracticeNetworkApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestPracticeNetworkContext?> LoadContextAsync(
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
                   eligibility.verification_id,eligibility.plan_key,
                   eligibility.payer_display_name,eligibility.product_display_name,
                   eligibility.current_location_state_code,eligibility.purpose_category,
                   eligibility.date_of_service,eligibility.service_category,
                   eligibility.business_outcome,eligibility.checked_at,eligibility.expires_at,
                   (select count(*) from telehealth_applicant_request_eligibility_verifications x
                     where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_practice_network_verifications x
                     where x.request_id=r.request_id),
                   (select count(*) from insurance_records x
                     where lower(x.patient_id)=lower(creation.canonical_patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (eligibility.applicant_version=26 and eligibility.source_request_version=6
                    and eligibility.resulting_request_version=7
                    and eligibility.source_request_status='Verification'
                    and eligibility.resulting_request_status='Verification'
                    and eligibility.service_category='ProfessionalTelehealthConsultation'
                    and eligibility.adapter_mode='NON_PRODUCTION'
                    and eligibility.compatibility_target='ASC_X12N_270_271_005010X279A1'
                    and eligibility.business_outcome='EligibleBenefitsReported'
                    and eligibility.member_matched and eligibility.member_eligibility_checked
                    and eligibility.member_benefits_checked
                    and eligibility.eligibility_status='Active'
                    and eligibility.benefit_information_status='Reported'
                    and eligibility.checked_at<=eligibility.expires_at
                    and eligibility.current_eligibility_evidence_created
                    and not eligibility.raw_transaction_created
                    and not eligibility.canonical_coverage_created
                    and not eligibility.generic_coverage_selected
                    and not eligibility.network_verification_created
                    and not eligibility.rendering_physician_network_checked
                    and not eligibility.coverage_verified and not eligibility.exact_network_confirmed
                    and not eligibility.financial_acknowledgment_created
                    and not eligibility.operational_review_created),
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
            join telehealth_applicant_request_eligibility_verifications eligibility
              on eligibility.request_id=r.request_id and eligibility.applicant_id=a.applicant_id
             and eligibility.canonical_patient_id=creation.canonical_patient_id
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id and patient.facility_id=a.facility_id
             and patient.lifecycle_status='active' and not patient.portal_enabled
             and patient.merged_into_patient_id is null
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated'
              and a.version=26 and a.expires_at>now()
              and r.status='Verification' and r.version in (7,8)
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
                reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetString(13),
                reader.GetString(14), reader.GetFieldValue<DateOnly>(15), reader.GetString(16),
                reader.GetString(17), reader.GetFieldValue<DateTimeOffset>(18),
                reader.GetFieldValue<DateTimeOffset>(19), Convert.ToInt32(reader.GetInt64(20)),
                Convert.ToInt32(reader.GetInt64(21)), Convert.ToInt32(reader.GetInt64(22)),
                Convert.ToInt32(reader.GetInt64(23)), reader.GetBoolean(24), reader.GetBoolean(25))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestPracticeNetworkRecord Record,
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
                   v.network_snapshot_fingerprint,v.context_expires_at,v.practice_display_name,
                   v.payer_display_name,v.product_display_name,v.current_location_state_code,
                   v.purpose_category,v.eligibility_verification_id,
                   v.eligibility_business_outcome,v.eligibility_checked_at,
                   v.eligibility_expires_at,v.verification_id,v.date_of_service,
                   v.adapter_mode,v.compatibility_target,v.dataset_key,v.dataset_version,
                   v.dataset_effective_from,v.dataset_effective_through,v.source_last_updated_at,
                   v.request_trace_token,v.response_trace_token,v.transport_outcome,
                   v.plan_network_match_status,v.practice_affiliation_status,
                   v.service_availability_status,v.new_patient_acceptance_status,v.business_outcome,
                   v.practice_network_checked,v.practice_in_network,v.new_patients_accepted,
                   v.network_reference,v.organization_reference,v.location_reference,
                   v.service_reference,v.checked_at,v.expires_at,v.recorded_at,v.command_fingerprint
            from telehealth_applicant_request_practice_network_verifications v
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
        var adapter = new TelehealthProspectivePracticeNetworkAdapterResult(
            reader.GetString(19), reader.GetString(20), reader.GetString(21), reader.GetInt32(22),
            reader.GetFieldValue<DateTimeOffset>(23), reader.GetFieldValue<DateTimeOffset>(24),
            reader.GetFieldValue<DateTimeOffset>(25), reader.GetGuid(26), reader.GetGuid(27),
            reader.GetString(28), reader.GetString(29), reader.GetString(30), reader.GetString(31),
            reader.GetString(32), reader.GetString(33), reader.GetBoolean(34), reader.GetBoolean(35),
            reader.GetBoolean(36), reader.IsDBNull(37) ? null : reader.GetString(37),
            reader.IsDBNull(38) ? null : reader.GetString(38),
            reader.IsDBNull(39) ? null : reader.GetString(39),
            reader.IsDBNull(40) ? null : reader.GetString(40),
            reader.GetFieldValue<DateTimeOffset>(41), reader.GetFieldValue<DateTimeOffset>(42));
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetGuid(13), reader.GetString(14), reader.GetFieldValue<DateTimeOffset>(15),
                reader.GetFieldValue<DateTimeOffset>(16), reader.GetGuid(17),
                reader.GetFieldValue<DateOnly>(18), adapter, reader.GetFieldValue<DateTimeOffset>(43)),
            reader.GetString(44));
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
            values(@eventId,@requestId,8,'applicant-practice-network-verification-recorded',
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
        TelehealthApplicantRequestPracticeNetworkApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestPracticeNetworkApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestPracticeNetworkPolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestPracticeNetworkPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_practice_network_state_conflict",
                "The applicant is not eligible for request-time practice-network verification.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestPracticeNetworkContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestPracticeNetworkPolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestPracticeNetworkPolicy.EntryRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || context.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || context.PurposeCategory is not ("migraine" or "sleep")
            || context.ServiceCategory != SyntheticTelehealthProspectivePracticeNetworkGateway.ServiceCategory
            || context.DateOfService != DateOnly.FromDateTime(context.EligibilityCheckedAt.UtcDateTime)
            || context.EligibilityBusinessOutcome != "EligibleBenefitsReported"
            || context.EligibilityCheckedAt > context.DatabaseNow
            || context.EligibilityExpiresAt <= context.DatabaseNow
            || context.RequestEligibilityCount != 1
            || context.RequestPracticeNetworkCount != 0
            || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0
            || !context.EligibilityEvidenceComplete
            || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
        if (context.ApplicantExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestPracticeNetworkContext context,
        TelehealthApplicantRequestPracticeNetworkRecord record)
    {
        if (context.RequestStatus != TelehealthApplicantRequestPracticeNetworkPolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestPracticeNetworkPolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.RequestEligibilityCount != 1
            || context.RequestPracticeNetworkCount != 1
            || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0
            || !context.EligibilityEvidenceComplete
            || context.AppointmentCreated
            || record.VerificationId is null
            || record.AdapterResult is null
            || context.EligibilityVerificationId != record.EligibilityVerificationId
            || context.PayerDisplayName != record.PayerDisplayName
            || context.ProductDisplayName != record.ProductDisplayName
            || context.CurrentLocationStateCode != record.CurrentLocationStateCode
            || context.PurposeCategory != record.PurposeCategory)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_practice_network_idempotency_conflict",
                "The practice-network idempotency key was already used with different command content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_practice_network_provenance_conflict",
        "The request eligibility result or its authorized practice-network context is unavailable or changed.");
}
