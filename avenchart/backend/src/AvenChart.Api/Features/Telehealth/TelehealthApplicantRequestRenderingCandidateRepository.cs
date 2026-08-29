// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestRenderingCandidateRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string CandidateSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    Guid EligibilityVerificationId,
    Guid PracticeNetworkVerificationId,
    string PracticeNetworkBusinessOutcome,
    DateTimeOffset PracticeNetworkCheckedAt,
    DateTimeOffset PracticeNetworkExpiresAt,
    int CandidateStaffId,
    string CandidateDisplayName,
    string CandidateNpi,
    SyntheticTelehealthRenderingCandidate Candidate,
    Guid? SelectionId,
    DateTimeOffset? SelectedAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestRenderingCandidateApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestRenderingCandidateContext(
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
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    DateOnly DateOfService,
    string ServiceCategory,
    string PracticeNetworkBusinessOutcome,
    DateTimeOffset PracticeNetworkCheckedAt,
    DateTimeOffset PracticeNetworkExpiresAt,
    string NetworkReference,
    string OrganizationReference,
    string LocationReference,
    string ServiceReference,
    int CandidateStaffId,
    string CandidateDisplayName,
    string CandidateNpi,
    string CandidateRole,
    int? CandidateFacilityId,
    bool CandidateActive,
    int EligibilityCount,
    int PracticeNetworkCount,
    int CandidateSelectionCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestRenderingCandidateRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestRenderingCandidateRecord> GetAsync(
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

    public async Task<TelehealthApplicantRequestRenderingCandidateRecord> SelectAsync(
        string practiceId,
        string practiceDisplayName,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestRenderingCandidateCommand command,
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
                "telehealth_applicant_request_rendering_candidate_version_conflict",
                "The request changed before candidate selection. Reload and try again.");
        }

        var snapshot = CreateSnapshot(context, practiceId, practiceDisplayName, facilityId);
        if (!string.Equals(snapshot.Fingerprint, command.CandidateSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_rendering_candidate_snapshot_stale",
                "The rendering-candidate context changed. Reload and try again.");
        }

        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(
            context.CurrentLocationStateCode);
        var selectionId = Guid.NewGuid();
        DateTimeOffset selectedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_rendering_candidate_selections(
                  selection_id,request_id,applicant_id,eligibility_verification_id,
                  practice_network_verification_id,practice_id,facility_id,canonical_patient_id,
                  applicant_version,source_request_version,resulting_request_version,
                  source_request_status,resulting_request_status,candidate_snapshot_fingerprint,
                  plan_key,payer_display_name,product_display_name,practice_display_name,
                  network_reference,organization_reference,location_reference,service_reference,
                  current_location_state_code,purpose_category,date_of_service,service_category,
                  modality,practice_network_business_outcome,practice_network_checked_at,
                  practice_network_expires_at,candidate_staff_id,candidate_display_name,
                  candidate_npi_last4,practitioner_reference,state_authority_reference,
                  candidate_purpose,catalog_key,catalog_version,catalog_effective_from,
                  catalog_effective_through,context_expires_at,applicant_expires_at,
                  synthetic_data_confirmed,candidate_only_scope_acknowledged,
                  no_assignment_acknowledged,network_check_still_required_acknowledged,
                  policy_key,policy_version,evidence_type,idempotency_key,command_fingerprint,
                  selected_at)
                values(
                  @selectionId,@requestId,@applicantId,@eligibilityId,@practiceNetworkId,
                  @practiceId,@facilityId,@patientId,@applicantVersion,8,9,'Verification',
                  'Verification',@snapshot,@planKey,@payer,@product,@practiceDisplayName,
                  @networkReference,@organizationReference,@locationReference,@serviceReference,
                  @state,@purpose,@dateOfService,@serviceCategory,@modality,@practiceBusiness,
                  @practiceCheckedAt,@practiceExpiresAt,@candidateStaffId,@candidateDisplay,
                  @npiLast4,@practitionerReference,@stateAuthorityReference,@candidatePurpose,
                  @catalogKey,@catalogVersion,@catalogFrom,@catalogThrough,@contextExpiresAt,
                  @applicantExpiresAt,true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @idempotencyKey,@commandFingerprint,now())
                returning selected_at;
                """;
            insert.Parameters.AddWithValue("selectionId", selectionId);
            insert.Parameters.AddWithValue("requestId", context.RequestId);
            insert.Parameters.AddWithValue("applicantId", context.ApplicantId);
            insert.Parameters.AddWithValue("eligibilityId", context.EligibilityVerificationId);
            insert.Parameters.AddWithValue("practiceNetworkId", context.PracticeNetworkVerificationId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            insert.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            insert.Parameters.AddWithValue("snapshot", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("planKey", context.PlanKey);
            insert.Parameters.AddWithValue("payer", context.PayerDisplayName);
            insert.Parameters.AddWithValue("product", context.ProductDisplayName);
            insert.Parameters.AddWithValue("practiceDisplayName", practiceDisplayName);
            insert.Parameters.AddWithValue("networkReference", context.NetworkReference);
            insert.Parameters.AddWithValue("organizationReference", context.OrganizationReference);
            insert.Parameters.AddWithValue("locationReference", context.LocationReference);
            insert.Parameters.AddWithValue("serviceReference", context.ServiceReference);
            insert.Parameters.AddWithValue("state", context.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purpose", context.PurposeCategory);
            insert.Parameters.AddWithValue("dateOfService", context.DateOfService);
            insert.Parameters.AddWithValue("serviceCategory", context.ServiceCategory);
            insert.Parameters.AddWithValue("modality", candidate.Modality);
            insert.Parameters.AddWithValue("practiceBusiness", context.PracticeNetworkBusinessOutcome);
            insert.Parameters.AddWithValue("practiceCheckedAt", context.PracticeNetworkCheckedAt);
            insert.Parameters.AddWithValue("practiceExpiresAt", context.PracticeNetworkExpiresAt);
            insert.Parameters.AddWithValue("candidateStaffId", context.CandidateStaffId);
            insert.Parameters.AddWithValue("candidateDisplay", context.CandidateDisplayName);
            insert.Parameters.AddWithValue("npiLast4", context.CandidateNpi[^4..]);
            insert.Parameters.AddWithValue("practitionerReference", candidate.PractitionerReference);
            insert.Parameters.AddWithValue("stateAuthorityReference", candidate.StateAuthorityReference);
            insert.Parameters.AddWithValue("candidatePurpose", TelehealthApplicantRequestRenderingCandidatePolicy.CandidatePurpose);
            insert.Parameters.AddWithValue("catalogKey", TelehealthApplicantRequestRenderingCandidatePolicy.CatalogKey);
            insert.Parameters.AddWithValue("catalogVersion", TelehealthApplicantRequestRenderingCandidatePolicy.CatalogVersion);
            insert.Parameters.AddWithValue("catalogFrom", candidate.EffectiveFrom);
            insert.Parameters.AddWithValue("catalogThrough", candidate.EffectiveThrough);
            insert.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestRenderingCandidatePolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestRenderingCandidatePolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestRenderingCandidatePolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var selectedAtValue = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Rendering-candidate selection time is unavailable.");
            selectedAt = selectedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(
                    DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Rendering-candidate selection time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests set version=9,updated_at=now()
                where request_id=@requestId and status='Verification' and version=8;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_rendering_candidate_version_conflict",
                    "The request changed before candidate selection. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, context.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(context, practiceDisplayName, snapshot, candidate, selectionId, selectedAt);
    }

    private static TelehealthApplicantRequestRenderingCandidateRecord CreatePendingRecord(
        TelehealthApplicantRequestRenderingCandidateContext context,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(
            context.CurrentLocationStateCode);
        var snapshot = CreateSnapshot(context, practiceId, practiceDisplayName, facilityId);
        return CreateRecord(context, practiceDisplayName, snapshot, candidate, null, null);
    }

    private static TelehealthApplicantRequestRenderingCandidateRecord CreateRecord(
        TelehealthApplicantRequestRenderingCandidateContext context,
        string practiceDisplayName,
        TelehealthApplicantRequestRenderingCandidateSnapshot snapshot,
        SyntheticTelehealthRenderingCandidate candidate,
        Guid? selectionId,
        DateTimeOffset? selectedAt) => new(
            context.ApplicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestRenderingCandidatePolicy.ApplicantStatus,
            context.RequestId,
            selectionId is null ? context.RequestVersion : TelehealthApplicantRequestRenderingCandidatePolicy.ResultingRequestVersion,
            TelehealthApplicantRequestRenderingCandidatePolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            practiceDisplayName,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.EligibilityVerificationId,
            context.PracticeNetworkVerificationId,
            context.PracticeNetworkBusinessOutcome,
            context.PracticeNetworkCheckedAt,
            context.PracticeNetworkExpiresAt,
            context.CandidateStaffId,
            context.CandidateDisplayName,
            context.CandidateNpi,
            candidate,
            selectionId,
            selectedAt,
            context.DatabaseNow);

    private static TelehealthApplicantRequestRenderingCandidateSnapshot CreateSnapshot(
        TelehealthApplicantRequestRenderingCandidateContext context,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(
            context.CurrentLocationStateCode);
        return TelehealthApplicantRequestRenderingCandidatePolicy.Snapshot(
            context.ApplicantId,
            context.RequestId,
            context.EligibilityVerificationId,
            context.PracticeNetworkVerificationId,
            context.RequestVersion,
            context.CanonicalPatientId,
            practiceId,
            facilityId,
            practiceDisplayName,
            context.PlanKey,
            context.PayerDisplayName,
            context.ProductDisplayName,
            context.NetworkReference,
            context.CurrentLocationStateCode,
            context.PurposeCategory,
            context.CandidateStaffId,
            context.CandidateDisplayName,
            context.CandidateNpi,
            candidate,
            context.PracticeNetworkCheckedAt,
            context.PracticeNetworkExpiresAt,
            context.ApplicantExpiresAt);
    }

    private static async Task<TelehealthApplicantRequestRenderingCandidateApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestRenderingCandidateContext?> LoadContextAsync(
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
                   r.triage_outcome,creation.canonical_patient_id,eligibility.verification_id,
                   network.verification_id,network.plan_key,network.payer_display_name,
                   network.product_display_name,network.current_location_state_code,
                   network.purpose_category,network.date_of_service,network.service_category,
                   network.business_outcome,network.checked_at,network.expires_at,
                   network.network_reference,network.organization_reference,
                   network.location_reference,network.service_reference,candidate.id,
                   trim(concat(candidate.first_name,' ',candidate.last_name)),candidate.npi,
                   candidate.role,candidate.facility_id,candidate.active,
                   (select count(*) from telehealth_applicant_request_eligibility_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_practice_network_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_rendering_candidate_selections x where x.request_id=r.request_id),
                   (select count(*) from insurance_records x where lower(x.patient_id)=lower(creation.canonical_patient_id)),
                   ((select count(*) from telehealth_coverage_selections x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_coverage_verifications x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_reservations x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_video_sessions x where x.request_id=r.request_id)
                    +(select count(*) from telehealth_consultation_contexts x where x.request_id=r.request_id)),
                   (eligibility.business_outcome='EligibleBenefitsReported'
                    and eligibility.member_matched and eligibility.member_eligibility_checked
                    and eligibility.member_benefits_checked and eligibility.eligibility_status='Active'
                    and eligibility.benefit_information_status='Reported'
                    and network.eligibility_verification_id=eligibility.verification_id
                    and network.resulting_request_version=8
                    and network.resulting_request_status='Verification'
                    and network.adapter_mode='NON_PRODUCTION'
                    and network.business_outcome='PracticeInNetworkAcceptingNewPatients'
                    and network.practice_network_checked and network.practice_in_network
                    and network.new_patients_accepted and network.network_reference is not null
                    and network.organization_reference is not null
                    and network.location_reference is not null and network.service_reference is not null
                    and network.current_eligibility_evidence_referenced
                    and not network.eligibility_payload_copied
                    and network.practice_network_verification_created
                    and not network.rendering_physician_selected
                    and not network.rendering_physician_network_checked
                    and not network.exact_network_confirmed and not network.coverage_verified
                    and network.checked_at<=network.expires_at),
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
            join telehealth_applicant_request_practice_network_verifications network
              on network.request_id=r.request_id and network.applicant_id=a.applicant_id
            join staff candidate on candidate.id=case network.current_location_state_code
              when 'GA' then 101 when 'CA' then 104 when 'FL' then 107 else -1 end
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id and patient.facility_id=a.facility_id
             and patient.lifecycle_status='active' and not patient.portal_enabled
             and patient.merged_into_patient_id is null
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated' and a.version=26
              and a.expires_at>now() and r.status='Verification' and r.version in (8,9)
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
                reader.GetGuid(10), reader.GetString(11), reader.GetString(12), reader.GetString(13),
                reader.GetString(14), reader.GetString(15), reader.GetFieldValue<DateOnly>(16),
                reader.GetString(17), reader.GetString(18), reader.GetFieldValue<DateTimeOffset>(19),
                reader.GetFieldValue<DateTimeOffset>(20), reader.GetString(21), reader.GetString(22),
                reader.GetString(23), reader.GetString(24), reader.GetInt32(25), reader.GetString(26),
                reader.GetString(27), reader.GetString(28), reader.IsDBNull(29) ? null : reader.GetInt32(29),
                reader.GetBoolean(30), Convert.ToInt32(reader.GetInt64(31)),
                Convert.ToInt32(reader.GetInt64(32)), Convert.ToInt32(reader.GetInt64(33)),
                Convert.ToInt32(reader.GetInt64(34)), Convert.ToInt32(reader.GetInt64(35)),
                reader.GetBoolean(36), reader.GetBoolean(37))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestRenderingCandidateRecord Record,
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
            select s.applicant_id,s.applicant_version,a.status,s.request_id,
                   s.resulting_request_version,s.resulting_request_status,
                   s.candidate_snapshot_fingerprint,s.context_expires_at,s.practice_display_name,
                   s.payer_display_name,s.product_display_name,s.current_location_state_code,
                   s.purpose_category,s.eligibility_verification_id,s.practice_network_verification_id,
                   s.practice_network_business_outcome,s.practice_network_checked_at,
                   s.practice_network_expires_at,s.candidate_staff_id,s.candidate_display_name,
                   candidate.npi,s.practitioner_reference,s.state_authority_reference,
                   s.service_category,s.modality,s.catalog_effective_from,s.catalog_effective_through,
                   s.selection_id,s.selected_at,now(),s.command_fingerprint
            from telehealth_applicant_request_rendering_candidate_selections s
            join telehealth_prospective_applicants a on a.applicant_id=s.applicant_id
            join staff candidate on candidate.id=s.candidate_staff_id
            where s.applicant_id=@applicantId and s.practice_id=@practiceId and s.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and s.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null) command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var candidate = new SyntheticTelehealthRenderingCandidate(
            reader.GetString(11), reader.GetInt32(18), reader.GetString(20), reader.GetString(21),
            reader.GetString(22), reader.GetString(23), reader.GetString(24),
            reader.GetFieldValue<DateTimeOffset>(25), reader.GetFieldValue<DateTimeOffset>(26));
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetGuid(13), reader.GetGuid(14), reader.GetString(15),
                reader.GetFieldValue<DateTimeOffset>(16), reader.GetFieldValue<DateTimeOffset>(17),
                reader.GetInt32(18), reader.GetString(19), reader.GetString(20), candidate,
                reader.GetGuid(27), reader.GetFieldValue<DateTimeOffset>(28),
                reader.GetFieldValue<DateTimeOffset>(29)),
            reader.GetString(30));
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
            values(@eventId,@requestId,9,'applicant-rendering-candidate-selected',
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
        TelehealthApplicantRequestRenderingCandidateApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestRenderingCandidateApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestRenderingCandidatePolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestRenderingCandidatePolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_rendering_candidate_state_conflict",
                "The applicant is not eligible for rendering-candidate selection.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestRenderingCandidateContext context)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(
            context.CurrentLocationStateCode);
        if (context.RequestStatus != TelehealthApplicantRequestRenderingCandidatePolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestRenderingCandidatePolicy.EntryRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.PlanKey != "harbor-mutual-hd"
            || context.PurposeCategory is not ("migraine" or "sleep")
            || context.ServiceCategory != candidate.ServiceCategory
            || context.PracticeNetworkBusinessOutcome != "PracticeInNetworkAcceptingNewPatients"
            || context.PracticeNetworkCheckedAt > context.DatabaseNow
            || context.PracticeNetworkExpiresAt <= context.DatabaseNow
            || candidate.EffectiveFrom > context.DatabaseNow || candidate.EffectiveThrough <= context.DatabaseNow
            || context.CandidateStaffId != candidate.StaffId
            || context.CandidateNpi != candidate.ExpectedSyntheticNpi
            || context.CandidateRole != "provider" || context.CandidateFacilityId != 10
            || !context.CandidateActive || string.IsNullOrWhiteSpace(context.CandidateDisplayName)
            || context.EligibilityCount != 1 || context.PracticeNetworkCount != 1
            || context.CandidateSelectionCount != 0 || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0 || !context.SourceEvidenceComplete || context.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestRenderingCandidateContext context,
        TelehealthApplicantRequestRenderingCandidateRecord record)
    {
        var candidate = TelehealthApplicantRequestRenderingCandidatePolicy.ResolveCandidate(
            context.CurrentLocationStateCode);
        if (context.RequestStatus != TelehealthApplicantRequestRenderingCandidatePolicy.RequestStatus
            || context.RequestVersion != TelehealthApplicantRequestRenderingCandidatePolicy.ResultingRequestVersion
            || context.RequestTriageOutcome != "TelehealthEligible"
            || context.EligibilityCount != 1 || context.PracticeNetworkCount != 1
            || context.CandidateSelectionCount != 1 || context.CanonicalInsuranceCount != 0
            || context.DownstreamCount != 0 || !context.SourceEvidenceComplete || context.AppointmentCreated
            || record.SelectionId is null
            || context.EligibilityVerificationId != record.EligibilityVerificationId
            || context.PracticeNetworkVerificationId != record.PracticeNetworkVerificationId
            || context.CurrentLocationStateCode != candidate.StateCode
            || context.ServiceCategory != candidate.ServiceCategory
            || context.CandidateStaffId != candidate.StaffId
            || context.CandidateNpi != candidate.ExpectedSyntheticNpi
            || context.CandidateRole != "provider" || context.CandidateFacilityId != 10
            || !context.CandidateActive || string.IsNullOrWhiteSpace(context.CandidateDisplayName)
            || context.CandidateStaffId != record.CandidateStaffId
            || context.CandidateDisplayName != record.CandidateDisplayName
            || context.CandidateNpi != record.CandidateNpi
            || record.Candidate.PractitionerReference != candidate.PractitionerReference
            || record.Candidate.StateAuthorityReference != candidate.StateAuthorityReference
            || record.Candidate.ServiceCategory != candidate.ServiceCategory
            || record.Candidate.Modality != candidate.Modality)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_rendering_candidate_idempotency_conflict",
                "The rendering-candidate idempotency key was already used with different content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_rendering_candidate_provenance_conflict",
        "The current practice-network evidence or synthetic rendering-candidate context is unavailable or changed.");
}
