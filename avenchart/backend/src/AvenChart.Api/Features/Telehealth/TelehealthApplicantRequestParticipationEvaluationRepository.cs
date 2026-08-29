// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestParticipationEvaluationRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string EvaluationSnapshotFingerprint,
    DateTimeOffset ResultValidThrough,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    Guid EligibilityVerificationId,
    Guid PracticeNetworkVerificationId,
    Guid CandidateSelectionId,
    Guid ParticipationContextConfirmationId,
    string CandidateDisplayName,
    string CandidateNpi,
    SyntheticTelehealthParticipationEvaluationRule Rule,
    Guid? EvaluationId,
    DateTimeOffset? EvaluatedAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestParticipationEvaluationApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestParticipationEvaluationSource(
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
    Guid PracticeNetworkVerificationId,
    Guid CandidateSelectionId,
    Guid ParticipationContextConfirmationId,
    string ParticipationContextSnapshotFingerprint,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeDisplayName,
    string NetworkReference,
    string OrganizationReference,
    string LocationReference,
    string ServiceReference,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    string ServiceCategory,
    string Modality,
    int CandidateStaffId,
    string CandidateDisplayName,
    string CandidateNpi,
    string CandidateRole,
    int? CandidateFacilityId,
    bool CandidateActive,
    string PractitionerReference,
    string StateAuthorityReference,
    string BillingOrganizationReference,
    string BillingProviderReference,
    string PractitionerRoleReference,
    string OrganizationAffiliationReference,
    string ContractReference,
    DateTimeOffset ContextEffectiveFrom,
    DateTimeOffset ContextEffectiveThrough,
    DateTimeOffset ContextConfirmedAt,
    DateTimeOffset ContextExpiresAt,
    int EligibilityCount,
    int PracticeNetworkCount,
    int CandidateSelectionCount,
    int ParticipationContextCount,
    int ParticipationEvaluationCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestParticipationEvaluationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestParticipationEvaluationRecord> GetAsync(
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
        var source = await LoadSourceAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw ProvenanceConflict();
        var completed = await LoadResultAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompletedEvaluation(source, completed.Value.Record, practiceId, practiceDisplayName, facilityId);
            return completed.Value.Record;
        }

        RequireReadyEvaluation(source, practiceDisplayName);
        return CreatePendingRecord(source, practiceId, practiceDisplayName, facilityId);
    }

    public async Task<TelehealthApplicantRequestParticipationEvaluationRecord> EvaluateAsync(
        string practiceId,
        string practiceDisplayName,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestParticipationEvaluationCommand command,
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

        var replay = await LoadResultAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            var replaySource = await LoadSourceAsync(
                connection, transaction, practiceId, facilityId, applicantId, false, cancellationToken)
                ?? throw ProvenanceConflict();
            RequireCompletedEvaluation(
                replaySource, replay.Value.Record, practiceId, practiceDisplayName, facilityId);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var source = await LoadSourceAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        RequireReadyEvaluation(source, practiceDisplayName);
        if (command.ExpectedRequestVersion != source.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_evaluation_version_conflict",
                "The request changed before participation evaluation. Reload and try again.");
        }

        var snapshot = CreateSnapshot(source, practiceId, practiceDisplayName, facilityId);
        if (!string.Equals(snapshot.Fingerprint, command.EvaluationSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_evaluation_snapshot_stale",
                "The participation evaluation inputs changed. Reload and try again.");
        }

        var rule = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(
            source.CurrentLocationStateCode);
        var evaluationId = Guid.NewGuid();
        DateTimeOffset evaluatedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_participation_evaluations(
                  evaluation_id,request_id,applicant_id,participation_context_confirmation_id,
                  eligibility_verification_id,practice_network_verification_id,candidate_selection_id,
                  practice_id,facility_id,canonical_patient_id,applicant_version,
                  source_request_version,resulting_request_version,source_request_status,
                  resulting_request_status,evaluation_snapshot_fingerprint,
                  participation_context_snapshot_fingerprint,plan_key,payer_display_name,
                  product_display_name,practice_display_name,network_reference,organization_reference,
                  location_reference,service_reference,current_location_state_code,purpose_category,
                  date_of_service,service_category,modality,candidate_staff_id,candidate_display_name,
                  candidate_npi_last4,practitioner_reference,state_authority_reference,
                  billing_organization_reference,billing_provider_reference,practitioner_role_reference,
                  organization_affiliation_reference,contract_reference,source_mode,
                  compatibility_target,evaluation_scope,business_outcome,catalog_key,catalog_version,
                  effective_from,effective_through,context_confirmed_at,context_expires_at,
                  result_valid_through,applicant_expires_at,synthetic_data_confirmed,
                  exact_tuple_scope_acknowledged,no_coverage_guarantee_acknowledged,
                  real_verification_still_required_acknowledged,policy_key,policy_version,
                  evidence_type,idempotency_key,command_fingerprint,evaluated_at)
                values(
                  @evaluationId,@requestId,@applicantId,@contextId,@eligibilityId,@practiceNetworkId,
                  @candidateSelectionId,@practiceId,@facilityId,@patientId,@applicantVersion,
                  10,11,'Verification','Verification',@snapshot,@contextSnapshot,@planKey,@payer,
                  @product,@practiceDisplayName,@networkReference,@organizationReference,
                  @locationReference,@serviceReference,@state,@purpose,@dateOfService,
                  @serviceCategory,@modality,@candidateStaffId,@candidateDisplay,@npiLast4,
                  @practitionerReference,@stateAuthorityReference,@billingOrganizationReference,
                  @billingProviderReference,@practitionerRoleReference,
                  @organizationAffiliationReference,@contractReference,@sourceMode,
                  @compatibilityTarget,@evaluationScope,@businessOutcome,@catalogKey,@catalogVersion,
                  @effectiveFrom,@effectiveThrough,@contextConfirmedAt,@contextExpiresAt,
                  @resultValidThrough,@applicantExpiresAt,true,true,true,true,@policyKey,@policyVersion,
                  @evidenceType,@idempotencyKey,@commandFingerprint,now())
                returning evaluated_at;
                """;
            insert.Parameters.AddWithValue("evaluationId", evaluationId);
            insert.Parameters.AddWithValue("requestId", source.RequestId);
            insert.Parameters.AddWithValue("applicantId", source.ApplicantId);
            insert.Parameters.AddWithValue("contextId", source.ParticipationContextConfirmationId);
            insert.Parameters.AddWithValue("eligibilityId", source.EligibilityVerificationId);
            insert.Parameters.AddWithValue("practiceNetworkId", source.PracticeNetworkVerificationId);
            insert.Parameters.AddWithValue("candidateSelectionId", source.CandidateSelectionId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", source.CanonicalPatientId);
            insert.Parameters.AddWithValue("applicantVersion", source.ApplicantVersion);
            insert.Parameters.AddWithValue("snapshot", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("contextSnapshot", source.ParticipationContextSnapshotFingerprint);
            insert.Parameters.AddWithValue("planKey", source.PlanKey);
            insert.Parameters.AddWithValue("payer", source.PayerDisplayName);
            insert.Parameters.AddWithValue("product", source.ProductDisplayName);
            insert.Parameters.AddWithValue("practiceDisplayName", practiceDisplayName);
            insert.Parameters.AddWithValue("networkReference", source.NetworkReference);
            insert.Parameters.AddWithValue("organizationReference", source.OrganizationReference);
            insert.Parameters.AddWithValue("locationReference", source.LocationReference);
            insert.Parameters.AddWithValue("serviceReference", source.ServiceReference);
            insert.Parameters.AddWithValue("state", source.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purpose", source.PurposeCategory);
            insert.Parameters.AddWithValue("dateOfService", source.DateOfService);
            insert.Parameters.AddWithValue("serviceCategory", rule.ServiceCategory);
            insert.Parameters.AddWithValue("modality", rule.Modality);
            insert.Parameters.AddWithValue("candidateStaffId", source.CandidateStaffId);
            insert.Parameters.AddWithValue("candidateDisplay", source.CandidateDisplayName);
            insert.Parameters.AddWithValue("npiLast4", source.CandidateNpi[^4..]);
            insert.Parameters.AddWithValue("practitionerReference", rule.PractitionerReference);
            insert.Parameters.AddWithValue("stateAuthorityReference", rule.StateAuthorityReference);
            insert.Parameters.AddWithValue("billingOrganizationReference", rule.BillingOrganizationReference);
            insert.Parameters.AddWithValue("billingProviderReference", rule.BillingProviderReference);
            insert.Parameters.AddWithValue("practitionerRoleReference", rule.PractitionerRoleReference);
            insert.Parameters.AddWithValue("organizationAffiliationReference", rule.OrganizationAffiliationReference);
            insert.Parameters.AddWithValue("contractReference", rule.ContractReference);
            insert.Parameters.AddWithValue("sourceMode", rule.SourceMode);
            insert.Parameters.AddWithValue("compatibilityTarget", rule.CompatibilityTarget);
            insert.Parameters.AddWithValue("evaluationScope", rule.EvaluationScope);
            insert.Parameters.AddWithValue("businessOutcome", rule.BusinessOutcome);
            insert.Parameters.AddWithValue("catalogKey", TelehealthApplicantRequestParticipationEvaluationPolicy.CatalogKey);
            insert.Parameters.AddWithValue("catalogVersion", TelehealthApplicantRequestParticipationEvaluationPolicy.CatalogVersion);
            insert.Parameters.AddWithValue("effectiveFrom", rule.EffectiveFrom);
            insert.Parameters.AddWithValue("effectiveThrough", rule.EffectiveThrough);
            insert.Parameters.AddWithValue("contextConfirmedAt", source.ContextConfirmedAt);
            insert.Parameters.AddWithValue("contextExpiresAt", source.ContextExpiresAt);
            insert.Parameters.AddWithValue("resultValidThrough", snapshot.ResultValidThrough);
            insert.Parameters.AddWithValue("applicantExpiresAt", source.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestParticipationEvaluationPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestParticipationEvaluationPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestParticipationEvaluationPolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var evaluatedAtValue = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Participation evaluation time is unavailable.");
            evaluatedAt = evaluatedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Participation evaluation time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests set version=11,updated_at=now()
                where request_id=@requestId and status='Verification' and version=10;
                """;
            update.Parameters.AddWithValue("requestId", source.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_participation_evaluation_version_conflict",
                    "The request changed before participation evaluation. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, source.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(source, practiceDisplayName, snapshot, rule, evaluationId, evaluatedAt);
    }

    private static TelehealthApplicantRequestParticipationEvaluationRecord CreatePendingRecord(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var rule = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(
            source.CurrentLocationStateCode);
        var snapshot = CreateSnapshot(source, practiceId, practiceDisplayName, facilityId);
        return CreateRecord(source, practiceDisplayName, snapshot, rule, null, null);
    }

    private static TelehealthApplicantRequestParticipationEvaluationRecord CreateRecord(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        string practiceDisplayName,
        TelehealthApplicantRequestParticipationEvaluationSnapshot snapshot,
        SyntheticTelehealthParticipationEvaluationRule rule,
        Guid? evaluationId,
        DateTimeOffset? evaluatedAt) => new(
            source.ApplicantId,
            source.ApplicantVersion,
            TelehealthApplicantRequestParticipationEvaluationPolicy.ApplicantStatus,
            source.RequestId,
            evaluationId is null
                ? source.RequestVersion
                : TelehealthApplicantRequestParticipationEvaluationPolicy.ResultingRequestVersion,
            TelehealthApplicantRequestParticipationEvaluationPolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ResultValidThrough,
            practiceDisplayName,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.DateOfService,
            source.EligibilityVerificationId,
            source.PracticeNetworkVerificationId,
            source.CandidateSelectionId,
            source.ParticipationContextConfirmationId,
            source.CandidateDisplayName,
            source.CandidateNpi,
            rule,
            evaluationId,
            evaluatedAt,
            source.DatabaseNow);

    private static TelehealthApplicantRequestParticipationEvaluationSnapshot CreateSnapshot(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var rule = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(
            source.CurrentLocationStateCode);
        return TelehealthApplicantRequestParticipationEvaluationPolicy.Snapshot(
            source.ApplicantId,
            source.RequestId,
            source.EligibilityVerificationId,
            source.PracticeNetworkVerificationId,
            source.CandidateSelectionId,
            source.ParticipationContextConfirmationId,
            TelehealthApplicantRequestParticipationEvaluationPolicy.EntryRequestVersion,
            source.CanonicalPatientId,
            practiceId,
            facilityId,
            practiceDisplayName,
            source.PlanKey,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.DateOfService,
            source.CandidateStaffId,
            source.CandidateDisplayName,
            source.CandidateNpi,
            source.ParticipationContextSnapshotFingerprint,
            source.ContextConfirmedAt,
            source.ContextExpiresAt,
            source.ApplicantExpiresAt,
            rule);
    }

    private static async Task<TelehealthApplicantRequestParticipationEvaluationApplicant?> LoadApplicantAsync(
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
            ? new(Convert.ToInt32(reader.GetInt64(0)), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    private static async Task<TelehealthApplicantRequestParticipationEvaluationSource?> LoadSourceAsync(
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
            select a.applicant_id,a.version,a.expires_at,now(),r.request_id,r.version,r.status,
                   r.triage_outcome,creation.canonical_patient_id,c.eligibility_verification_id,
                   c.practice_network_verification_id,c.candidate_selection_id,c.confirmation_id,
                   c.context_snapshot_fingerprint,c.plan_key,c.payer_display_name,
                   c.product_display_name,c.practice_display_name,c.network_reference,
                   c.organization_reference,c.location_reference,c.service_reference,
                   c.current_location_state_code,c.purpose_category,c.date_of_service,
                   c.service_category,c.modality,candidate.id,
                   trim(concat(candidate.first_name,' ',candidate.last_name)),candidate.npi,
                   candidate.role,candidate.facility_id,candidate.active,c.practitioner_reference,
                   c.state_authority_reference,c.billing_organization_reference,
                   c.billing_provider_reference,c.practitioner_role_reference,
                   c.organization_affiliation_reference,c.contract_reference,c.effective_from,
                   c.effective_through,c.confirmed_at,c.context_expires_at,
                   (select count(*) from telehealth_applicant_request_eligibility_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_practice_network_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_rendering_candidate_selections x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_participation_contexts x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_participation_evaluations x where x.request_id=r.request_id),
                   (select count(*) from insurance_records x where lower(x.patient_id)=lower(creation.canonical_patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (c.resulting_request_version=10 and c.resulting_request_status='Verification'
                    and c.context_purpose='PARTICIPATION_EVALUATION_PREREQUISITES_ONLY'
                    and c.policy_key='SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT'
                    and c.policy_version=1 and c.evidence_type='APPLICANT_REQUEST_PARTICIPATION_CONTEXT'
                    and c.synthetic_data_confirmed and c.npi_not_credential_acknowledged
                    and c.real_authority_not_verified_acknowledged
                    and c.exact_participation_still_required_acknowledged
                    and c.participation_evaluation_context_confirmed
                    and not c.real_state_authority_verified and not c.real_credentialing_verified
                    and not c.rendering_physician_assigned
                    and not c.rendering_physician_network_checked and not c.exact_network_confirmed
                    and not c.coverage_verified and not c.external_call_performed
                    and c.confirmed_at<=now() and c.context_expires_at>now()),
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
            join telehealth_applicant_request_participation_contexts c
              on c.request_id=r.request_id and c.applicant_id=a.applicant_id
             and c.canonical_patient_id=creation.canonical_patient_id
            join staff candidate on candidate.id=c.candidate_staff_id
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id and patient.facility_id=a.facility_id
             and patient.lifecycle_status='active' and not patient.portal_enabled
             and patient.merged_into_patient_id is null
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated' and a.version=26
              and a.expires_at>now() and r.status='Verification' and r.version in (10,11)
              and r.triage_outcome='TelehealthEligible' and r.ready_at is null
            {(forUpdate ? "for update of a,r,patient,candidate" : string.Empty)};
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
                reader.GetGuid(10), reader.GetGuid(11), reader.GetGuid(12), reader.GetString(13),
                reader.GetString(14), reader.GetString(15), reader.GetString(16), reader.GetString(17),
                reader.GetString(18), reader.GetString(19), reader.GetString(20), reader.GetString(21),
                reader.GetString(22), reader.GetString(23), reader.GetFieldValue<DateOnly>(24),
                reader.GetString(25), reader.GetString(26), reader.GetInt32(27), reader.GetString(28),
                reader.GetString(29), reader.GetString(30),
                reader.IsDBNull(31) ? null : reader.GetInt32(31), reader.GetBoolean(32),
                reader.GetString(33), reader.GetString(34), reader.GetString(35), reader.GetString(36),
                reader.GetString(37), reader.GetString(38), reader.GetString(39),
                reader.GetFieldValue<DateTimeOffset>(40), reader.GetFieldValue<DateTimeOffset>(41),
                reader.GetFieldValue<DateTimeOffset>(42), reader.GetFieldValue<DateTimeOffset>(43),
                Convert.ToInt32(reader.GetInt64(44)), Convert.ToInt32(reader.GetInt64(45)),
                Convert.ToInt32(reader.GetInt64(46)), Convert.ToInt32(reader.GetInt64(47)),
                Convert.ToInt32(reader.GetInt64(48)), Convert.ToInt32(reader.GetInt64(49)),
                Convert.ToInt32(reader.GetInt64(50)), reader.GetBoolean(51), reader.GetBoolean(52))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestParticipationEvaluationRecord Record,
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
            select e.applicant_id,e.applicant_version,a.status,e.request_id,
                   e.resulting_request_version,e.resulting_request_status,
                   e.evaluation_snapshot_fingerprint,e.result_valid_through,e.practice_display_name,
                   e.payer_display_name,e.product_display_name,e.current_location_state_code,
                   e.purpose_category,e.date_of_service,e.eligibility_verification_id,
                   e.practice_network_verification_id,e.candidate_selection_id,
                   e.participation_context_confirmation_id,e.candidate_display_name,candidate.npi,
                   e.candidate_staff_id,e.practitioner_reference,e.state_authority_reference,
                   e.billing_organization_reference,e.billing_provider_reference,
                   e.practitioner_role_reference,e.organization_affiliation_reference,
                   e.contract_reference,e.network_reference,e.organization_reference,
                   e.location_reference,e.service_reference,e.service_category,e.modality,
                   e.source_mode,e.compatibility_target,e.evaluation_scope,e.business_outcome,
                   e.effective_from,e.effective_through,e.synthetic_billing_entity_in_network,
                   e.synthetic_rendering_provider_in_network,e.synthetic_plan_network_matched,
                   e.synthetic_service_location_matched,e.synthetic_new_patients_accepted,
                   e.synthetic_exact_network_matched,e.evaluation_id,e.evaluated_at,now(),
                   e.command_fingerprint
            from telehealth_applicant_request_participation_evaluations e
            join telehealth_prospective_applicants a on a.applicant_id=e.applicant_id
            join staff candidate on candidate.id=e.candidate_staff_id
            where e.applicant_id=@applicantId and e.practice_id=@practiceId and e.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and e.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null) command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var rule = new SyntheticTelehealthParticipationEvaluationRule(
            reader.GetString(11), reader.GetInt32(20), reader.GetString(19),
            reader.GetString(21), reader.GetString(22), reader.GetString(23), reader.GetString(24),
            reader.GetString(25), reader.GetString(26), reader.GetString(27), reader.GetString(28),
            reader.GetString(29), reader.GetString(30), reader.GetString(31), reader.GetString(32),
            reader.GetString(33), reader.GetString(34), reader.GetString(35), reader.GetString(36),
            reader.GetString(37), reader.GetBoolean(40), reader.GetBoolean(41), reader.GetBoolean(42),
            reader.GetBoolean(43), reader.GetBoolean(44), reader.GetBoolean(45),
            reader.GetFieldValue<DateTimeOffset>(38), reader.GetFieldValue<DateTimeOffset>(39));
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetFieldValue<DateOnly>(13), reader.GetGuid(14), reader.GetGuid(15),
                reader.GetGuid(16), reader.GetGuid(17), reader.GetString(18), reader.GetString(19),
                rule, reader.GetGuid(46), reader.GetFieldValue<DateTimeOffset>(47),
                reader.GetFieldValue<DateTimeOffset>(48)),
            reader.GetString(49));
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
            values(@eventId,@requestId,11,'applicant-participation-evaluated',
                   'Verification','Verification','applicant',@actorId,@idempotencyKey,@fingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestParticipationEvaluationApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestParticipationEvaluationApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestParticipationEvaluationPolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestParticipationEvaluationPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_evaluation_state_conflict",
                "The applicant is not eligible for participation evaluation.");
        }
    }

    private static void RequireReadyEvaluation(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        string practiceDisplayName)
    {
        var expected = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(
            source.CurrentLocationStateCode);
        if (source.RequestStatus != TelehealthApplicantRequestParticipationEvaluationPolicy.RequestStatus
            || source.RequestVersion != TelehealthApplicantRequestParticipationEvaluationPolicy.EntryRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.PlanKey != "harbor-mutual-hd"
            || source.PracticeDisplayName != practiceDisplayName
            || source.PurposeCategory is not ("migraine" or "sleep")
            || source.ContextConfirmedAt > source.DatabaseNow
            || source.ContextExpiresAt <= source.DatabaseNow
            || source.ContextEffectiveFrom != expected.EffectiveFrom
            || source.ContextEffectiveThrough != expected.EffectiveThrough
            || expected.EffectiveFrom > source.DatabaseNow || expected.EffectiveThrough <= source.DatabaseNow
            || !MatchesRule(source, expected)
            || source.EligibilityCount != 1 || source.PracticeNetworkCount != 1
            || source.CandidateSelectionCount != 1 || source.ParticipationContextCount != 1
            || source.ParticipationEvaluationCount != 0 || source.CanonicalInsuranceCount != 0
            || source.DownstreamCount != 0 || !source.SourceEvidenceComplete
            || source.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompletedEvaluation(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        TelehealthApplicantRequestParticipationEvaluationRecord record,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var expected = TelehealthApplicantRequestParticipationEvaluationPolicy.ResolveRule(
            source.CurrentLocationStateCode);
        var snapshot = CreateSnapshot(source, practiceId, practiceDisplayName, facilityId);
        if (source.RequestStatus != TelehealthApplicantRequestParticipationEvaluationPolicy.RequestStatus
            || source.RequestVersion != TelehealthApplicantRequestParticipationEvaluationPolicy.ResultingRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.ParticipationEvaluationCount != 1 || source.ParticipationContextCount != 1
            || source.EligibilityCount != 1 || source.PracticeNetworkCount != 1
            || source.CandidateSelectionCount != 1 || source.CanonicalInsuranceCount != 0
            || source.DownstreamCount != 0 || !source.SourceEvidenceComplete
            || source.AppointmentCreated || record.EvaluationId is null || record.EvaluatedAt is null
            || record.EvaluatedAt > source.DatabaseNow || record.ResultValidThrough <= source.DatabaseNow
            || source.ApplicantId != record.ApplicantId || source.RequestId != record.RequestId
            || source.EligibilityVerificationId != record.EligibilityVerificationId
            || source.PracticeNetworkVerificationId != record.PracticeNetworkVerificationId
            || source.CandidateSelectionId != record.CandidateSelectionId
            || source.ParticipationContextConfirmationId != record.ParticipationContextConfirmationId
            || source.PayerDisplayName != record.PayerDisplayName
            || source.ProductDisplayName != record.ProductDisplayName
            || source.CurrentLocationStateCode != record.CurrentLocationStateCode
            || source.PurposeCategory != record.PurposeCategory
            || source.DateOfService != record.DateOfService
            || source.CandidateDisplayName != record.CandidateDisplayName
            || source.CandidateNpi != record.CandidateNpi
            || record.EvaluationSnapshotFingerprint != snapshot.Fingerprint
            || record.ResultValidThrough != snapshot.ResultValidThrough
            || record.Rule != expected || !MatchesRule(source, expected))
        {
            throw ProvenanceConflict();
        }
    }

    private static bool MatchesRule(
        TelehealthApplicantRequestParticipationEvaluationSource source,
        SyntheticTelehealthParticipationEvaluationRule expected) =>
        source.CurrentLocationStateCode == expected.StateCode
        && source.CandidateStaffId == expected.ExpectedStaffId
        && source.CandidateNpi == expected.ExpectedSyntheticNpi
        && source.CandidateRole == "provider"
        && source.CandidateFacilityId == 10
        && source.CandidateActive
        && !string.IsNullOrWhiteSpace(source.CandidateDisplayName)
        && source.PractitionerReference == expected.PractitionerReference
        && source.StateAuthorityReference == expected.StateAuthorityReference
        && source.BillingOrganizationReference == expected.BillingOrganizationReference
        && source.BillingProviderReference == expected.BillingProviderReference
        && source.PractitionerRoleReference == expected.PractitionerRoleReference
        && source.OrganizationAffiliationReference == expected.OrganizationAffiliationReference
        && source.ContractReference == expected.ContractReference
        && source.NetworkReference == expected.NetworkReference
        && source.OrganizationReference == expected.OrganizationReference
        && source.LocationReference == expected.LocationReference
        && source.ServiceReference == expected.ServiceReference
        && source.ServiceCategory == expected.ServiceCategory
        && source.Modality == expected.Modality;

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_evaluation_idempotency_conflict",
                "The participation-evaluation idempotency key was already used with different content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_participation_evaluation_provenance_conflict",
        "The synthetic participation context is unavailable, expired, or changed.");
}
