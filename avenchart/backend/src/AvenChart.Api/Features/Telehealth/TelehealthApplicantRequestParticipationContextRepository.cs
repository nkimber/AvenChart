// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestParticipationContextRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string ContextSnapshotFingerprint,
    DateTimeOffset ContextExpiresAt,
    string PracticeDisplayName,
    string PayerDisplayName,
    string ProductDisplayName,
    string CurrentLocationStateCode,
    string PurposeCategory,
    Guid EligibilityVerificationId,
    Guid PracticeNetworkVerificationId,
    Guid CandidateSelectionId,
    string CandidateDisplayName,
    string CandidateNpi,
    SyntheticTelehealthParticipationContext ParticipationContext,
    Guid? ConfirmationId,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestParticipationContextApplicant(
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestParticipationContextSource(
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
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
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
    DateTimeOffset CandidateSelectedAt,
    DateTimeOffset CandidateContextExpiresAt,
    int EligibilityCount,
    int PracticeNetworkCount,
    int CandidateSelectionCount,
    int ParticipationContextCount,
    int CanonicalInsuranceCount,
    int DownstreamCount,
    bool SourceEvidenceComplete,
    bool AppointmentCreated);

public sealed class TelehealthApplicantRequestParticipationContextRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestParticipationContextRecord> GetAsync(
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
            RequireCompletedContext(source, completed.Value.Record);
            return completed.Value.Record;
        }

        RequireReadyContext(source);
        return CreatePendingRecord(source, practiceId, practiceDisplayName, facilityId);
    }

    public async Task<TelehealthApplicantRequestParticipationContextRecord> ConfirmAsync(
        string practiceId,
        string practiceDisplayName,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestParticipationContextCommand command,
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
            RequireCompletedContext(replaySource, replay.Value.Record);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var source = await LoadSourceAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        RequireReadyContext(source);
        if (command.ExpectedRequestVersion != source.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_context_version_conflict",
                "The request changed before participation-context confirmation. Reload and try again.");
        }

        var snapshot = CreateSnapshot(source, practiceId, practiceDisplayName, facilityId);
        if (!string.Equals(snapshot.Fingerprint, command.ContextSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_context_snapshot_stale",
                "The participation context changed. Reload and try again.");
        }

        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(
            source.CurrentLocationStateCode);
        var confirmationId = Guid.NewGuid();
        DateTimeOffset confirmedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_request_participation_contexts(
                  confirmation_id,request_id,applicant_id,eligibility_verification_id,
                  practice_network_verification_id,candidate_selection_id,practice_id,facility_id,
                  canonical_patient_id,applicant_version,source_request_version,
                  resulting_request_version,source_request_status,resulting_request_status,
                  context_snapshot_fingerprint,plan_key,payer_display_name,product_display_name,
                  practice_display_name,network_reference,organization_reference,location_reference,
                  service_reference,current_location_state_code,purpose_category,date_of_service,
                  service_category,modality,candidate_staff_id,candidate_display_name,
                  candidate_npi_last4,practitioner_reference,state_authority_reference,
                  billing_organization_reference,billing_provider_reference,
                  practitioner_role_reference,organization_affiliation_reference,contract_reference,
                  authority_kind,authority_fixture_status,role_fixture_status,
                  affiliation_fixture_status,contract_fixture_status,context_purpose,
                  catalog_key,catalog_version,effective_from,effective_through,
                  candidate_selected_at,candidate_context_expires_at,context_expires_at,
                  applicant_expires_at,synthetic_data_confirmed,npi_not_credential_acknowledged,
                  real_authority_not_verified_acknowledged,
                  exact_participation_still_required_acknowledged,policy_key,policy_version,
                  evidence_type,idempotency_key,command_fingerprint,confirmed_at)
                values(
                  @confirmationId,@requestId,@applicantId,@eligibilityId,@practiceNetworkId,
                  @candidateSelectionId,@practiceId,@facilityId,@patientId,@applicantVersion,
                  9,10,'Verification','Verification',@snapshot,@planKey,@payer,@product,
                  @practiceDisplayName,@networkReference,@organizationReference,@locationReference,
                  @serviceReference,@state,@purpose,@dateOfService,@serviceCategory,@modality,
                  @candidateStaffId,@candidateDisplay,@npiLast4,@practitionerReference,
                  @stateAuthorityReference,@billingOrganizationReference,@billingProviderReference,
                  @practitionerRoleReference,@organizationAffiliationReference,@contractReference,
                  @authorityKind,@authorityFixtureStatus,@roleFixtureStatus,@affiliationFixtureStatus,
                  @contractFixtureStatus,@contextPurpose,@catalogKey,@catalogVersion,@effectiveFrom,
                  @effectiveThrough,@candidateSelectedAt,@candidateContextExpiresAt,@contextExpiresAt,
                  @applicantExpiresAt,true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @idempotencyKey,@commandFingerprint,now())
                returning confirmed_at;
                """;
            insert.Parameters.AddWithValue("confirmationId", confirmationId);
            insert.Parameters.AddWithValue("requestId", source.RequestId);
            insert.Parameters.AddWithValue("applicantId", source.ApplicantId);
            insert.Parameters.AddWithValue("eligibilityId", source.EligibilityVerificationId);
            insert.Parameters.AddWithValue("practiceNetworkId", source.PracticeNetworkVerificationId);
            insert.Parameters.AddWithValue("candidateSelectionId", source.CandidateSelectionId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("patientId", source.CanonicalPatientId);
            insert.Parameters.AddWithValue("applicantVersion", source.ApplicantVersion);
            insert.Parameters.AddWithValue("snapshot", snapshot.Fingerprint);
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
            insert.Parameters.AddWithValue("serviceCategory", context.ServiceCategory);
            insert.Parameters.AddWithValue("modality", context.Modality);
            insert.Parameters.AddWithValue("candidateStaffId", source.CandidateStaffId);
            insert.Parameters.AddWithValue("candidateDisplay", source.CandidateDisplayName);
            insert.Parameters.AddWithValue("npiLast4", source.CandidateNpi[^4..]);
            insert.Parameters.AddWithValue("practitionerReference", context.PractitionerReference);
            insert.Parameters.AddWithValue("stateAuthorityReference", context.StateAuthorityReference);
            insert.Parameters.AddWithValue("billingOrganizationReference", context.BillingOrganizationReference);
            insert.Parameters.AddWithValue("billingProviderReference", context.BillingProviderReference);
            insert.Parameters.AddWithValue("practitionerRoleReference", context.PractitionerRoleReference);
            insert.Parameters.AddWithValue("organizationAffiliationReference", context.OrganizationAffiliationReference);
            insert.Parameters.AddWithValue("contractReference", context.ContractReference);
            insert.Parameters.AddWithValue("authorityKind", context.AuthorityKind);
            insert.Parameters.AddWithValue("authorityFixtureStatus", context.AuthorityFixtureStatus);
            insert.Parameters.AddWithValue("roleFixtureStatus", context.RoleFixtureStatus);
            insert.Parameters.AddWithValue("affiliationFixtureStatus", context.AffiliationFixtureStatus);
            insert.Parameters.AddWithValue("contractFixtureStatus", context.ContractFixtureStatus);
            insert.Parameters.AddWithValue("contextPurpose", TelehealthApplicantRequestParticipationContextPolicy.ContextPurpose);
            insert.Parameters.AddWithValue("catalogKey", TelehealthApplicantRequestParticipationContextPolicy.CatalogKey);
            insert.Parameters.AddWithValue("catalogVersion", TelehealthApplicantRequestParticipationContextPolicy.CatalogVersion);
            insert.Parameters.AddWithValue("effectiveFrom", context.EffectiveFrom);
            insert.Parameters.AddWithValue("effectiveThrough", context.EffectiveThrough);
            insert.Parameters.AddWithValue("candidateSelectedAt", source.CandidateSelectedAt);
            insert.Parameters.AddWithValue("candidateContextExpiresAt", source.CandidateContextExpiresAt);
            insert.Parameters.AddWithValue("contextExpiresAt", snapshot.ContextExpiresAt);
            insert.Parameters.AddWithValue("applicantExpiresAt", source.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestParticipationContextPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestParticipationContextPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestParticipationContextPolicy.EvidenceType);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            var confirmedAtValue = await insert.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Participation-context confirmation time is unavailable.");
            confirmedAt = confirmedAtValue switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException(
                    "Participation-context confirmation time had an unexpected database type.")
            };
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests set version=10,updated_at=now()
                where request_id=@requestId and status='Verification' and version=9;
                """;
            update.Parameters.AddWithValue("requestId", source.RequestId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_participation_context_version_conflict",
                    "The request changed before participation-context confirmation. Reload and try again.");
            }
        }

        await InsertRequestEventAsync(
            connection, transaction, source.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateRecord(source, practiceDisplayName, snapshot, context, confirmationId, confirmedAt);
    }

    private static TelehealthApplicantRequestParticipationContextRecord CreatePendingRecord(
        TelehealthApplicantRequestParticipationContextSource source,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(
            source.CurrentLocationStateCode);
        var snapshot = CreateSnapshot(source, practiceId, practiceDisplayName, facilityId);
        return CreateRecord(source, practiceDisplayName, snapshot, context, null, null);
    }

    private static TelehealthApplicantRequestParticipationContextRecord CreateRecord(
        TelehealthApplicantRequestParticipationContextSource source,
        string practiceDisplayName,
        TelehealthApplicantRequestParticipationContextSnapshot snapshot,
        SyntheticTelehealthParticipationContext context,
        Guid? confirmationId,
        DateTimeOffset? confirmedAt) => new(
            source.ApplicantId,
            source.ApplicantVersion,
            TelehealthApplicantRequestParticipationContextPolicy.ApplicantStatus,
            source.RequestId,
            confirmationId is null
                ? source.RequestVersion
                : TelehealthApplicantRequestParticipationContextPolicy.ResultingRequestVersion,
            TelehealthApplicantRequestParticipationContextPolicy.RequestStatus,
            snapshot.Fingerprint,
            snapshot.ContextExpiresAt,
            practiceDisplayName,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.EligibilityVerificationId,
            source.PracticeNetworkVerificationId,
            source.CandidateSelectionId,
            source.CandidateDisplayName,
            source.CandidateNpi,
            context,
            confirmationId,
            confirmedAt,
            source.DatabaseNow);

    private static TelehealthApplicantRequestParticipationContextSnapshot CreateSnapshot(
        TelehealthApplicantRequestParticipationContextSource source,
        string practiceId,
        string practiceDisplayName,
        int facilityId)
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(
            source.CurrentLocationStateCode);
        return TelehealthApplicantRequestParticipationContextPolicy.Snapshot(
            source.ApplicantId,
            source.RequestId,
            source.EligibilityVerificationId,
            source.PracticeNetworkVerificationId,
            source.CandidateSelectionId,
            source.RequestVersion,
            source.CanonicalPatientId,
            practiceId,
            facilityId,
            practiceDisplayName,
            source.PlanKey,
            source.PayerDisplayName,
            source.ProductDisplayName,
            source.NetworkReference,
            source.OrganizationReference,
            source.LocationReference,
            source.ServiceReference,
            source.CurrentLocationStateCode,
            source.PurposeCategory,
            source.DateOfService,
            source.CandidateStaffId,
            source.CandidateDisplayName,
            source.CandidateNpi,
            context,
            source.CandidateSelectedAt,
            source.CandidateContextExpiresAt,
            source.ApplicantExpiresAt);
    }

    private static async Task<TelehealthApplicantRequestParticipationContextApplicant?> LoadApplicantAsync(
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

    private static async Task<TelehealthApplicantRequestParticipationContextSource?> LoadSourceAsync(
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
                   network.verification_id,selection.selection_id,network.plan_key,
                   network.payer_display_name,network.product_display_name,network.network_reference,
                   network.organization_reference,network.location_reference,network.service_reference,
                   network.current_location_state_code,network.purpose_category,network.date_of_service,
                   selection.service_category,selection.modality,candidate.id,
                   trim(concat(candidate.first_name,' ',candidate.last_name)),candidate.npi,
                   candidate.role,candidate.facility_id,candidate.active,
                   selection.practitioner_reference,selection.state_authority_reference,
                   selection.selected_at,selection.context_expires_at,
                   (select count(*) from telehealth_applicant_request_eligibility_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_practice_network_verifications x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_rendering_candidate_selections x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_participation_contexts x where x.request_id=r.request_id),
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
                    and network.new_patients_accepted
                    and selection.eligibility_verification_id=eligibility.verification_id
                    and selection.practice_network_verification_id=network.verification_id
                    and selection.resulting_request_version=9
                    and selection.resulting_request_status='Verification'
                    and selection.candidate_selected_for_network_evaluation
                    and not selection.rendering_physician_assigned
                    and not selection.rendering_physician_network_checked
                    and not selection.exact_network_confirmed
                    and not selection.coverage_verified
                    and selection.context_expires_at>now()),
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
            join telehealth_applicant_request_rendering_candidate_selections selection
              on selection.request_id=r.request_id and selection.applicant_id=a.applicant_id
             and selection.practice_network_verification_id=network.verification_id
            join staff candidate on candidate.id=selection.candidate_staff_id
            join patients patient
              on patient.canonical_id=creation.canonical_patient_id and patient.facility_id=a.facility_id
             and patient.lifecycle_status='active' and not patient.portal_enabled
             and patient.merged_into_patient_id is null
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated' and a.version=26
              and a.expires_at>now() and r.status='Verification' and r.version in (9,10)
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
                reader.GetGuid(10), reader.GetGuid(11), reader.GetString(12), reader.GetString(13),
                reader.GetString(14), reader.GetString(15), reader.GetString(16), reader.GetString(17),
                reader.GetString(18), reader.GetString(19), reader.GetString(20),
                reader.GetFieldValue<DateOnly>(21), reader.GetString(22), reader.GetString(23),
                reader.GetInt32(24), reader.GetString(25), reader.GetString(26), reader.GetString(27),
                reader.IsDBNull(28) ? null : reader.GetInt32(28), reader.GetBoolean(29),
                reader.GetString(30), reader.GetString(31), reader.GetFieldValue<DateTimeOffset>(32),
                reader.GetFieldValue<DateTimeOffset>(33), Convert.ToInt32(reader.GetInt64(34)),
                Convert.ToInt32(reader.GetInt64(35)), Convert.ToInt32(reader.GetInt64(36)),
                Convert.ToInt32(reader.GetInt64(37)), Convert.ToInt32(reader.GetInt64(38)),
                Convert.ToInt32(reader.GetInt64(39)), reader.GetBoolean(40), reader.GetBoolean(41))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestParticipationContextRecord Record,
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
            select c.applicant_id,c.applicant_version,a.status,c.request_id,
                   c.resulting_request_version,c.resulting_request_status,
                   c.context_snapshot_fingerprint,c.context_expires_at,c.practice_display_name,
                   c.payer_display_name,c.product_display_name,c.current_location_state_code,
                   c.purpose_category,c.eligibility_verification_id,c.practice_network_verification_id,
                   c.candidate_selection_id,c.candidate_display_name,candidate.npi,
                   c.candidate_staff_id,c.practitioner_reference,c.state_authority_reference,
                   c.billing_organization_reference,c.billing_provider_reference,
                   c.practitioner_role_reference,c.organization_affiliation_reference,
                   c.contract_reference,c.authority_kind,c.authority_fixture_status,
                   c.role_fixture_status,c.affiliation_fixture_status,c.contract_fixture_status,
                   c.service_category,c.modality,c.effective_from,c.effective_through,
                   c.confirmation_id,c.confirmed_at,now(),c.command_fingerprint
            from telehealth_applicant_request_participation_contexts c
            join telehealth_prospective_applicants a on a.applicant_id=c.applicant_id
            join staff candidate on candidate.id=c.candidate_staff_id
            where c.applicant_id=@applicantId and c.practice_id=@practiceId and c.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and c.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null) command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var context = new SyntheticTelehealthParticipationContext(
            reader.GetString(11), reader.GetInt32(18), reader.GetString(17),
            reader.GetString(19), reader.GetString(20), reader.GetString(21),
            reader.GetString(22), reader.GetString(23), reader.GetString(24), reader.GetString(25),
            reader.GetString(26), reader.GetString(27), reader.GetString(28), reader.GetString(29),
            reader.GetString(30), reader.GetString(31), reader.GetString(32),
            reader.GetFieldValue<DateTimeOffset>(33), reader.GetFieldValue<DateTimeOffset>(34));
        return (new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetGuid(13), reader.GetGuid(14), reader.GetGuid(15), reader.GetString(16),
                reader.GetString(17), context, reader.GetGuid(35),
                reader.GetFieldValue<DateTimeOffset>(36), reader.GetFieldValue<DateTimeOffset>(37)),
            reader.GetString(38));
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
            values(@eventId,@requestId,10,'applicant-participation-context-confirmed',
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
        TelehealthApplicantRequestParticipationContextApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestParticipationContextApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestParticipationContextPolicy.ApplicantStatus
            || applicant.Version != TelehealthApplicantRequestParticipationContextPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_context_state_conflict",
                "The applicant is not eligible for participation-context confirmation.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestParticipationContextSource source)
    {
        var context = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(
            source.CurrentLocationStateCode);
        if (source.RequestStatus != TelehealthApplicantRequestParticipationContextPolicy.RequestStatus
            || source.RequestVersion != TelehealthApplicantRequestParticipationContextPolicy.EntryRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.PlanKey != "harbor-mutual-hd"
            || source.PurposeCategory is not ("migraine" or "sleep")
            || source.ServiceCategory != context.ServiceCategory || source.Modality != context.Modality
            || source.CandidateSelectedAt > source.DatabaseNow
            || source.CandidateContextExpiresAt <= source.DatabaseNow
            || context.EffectiveFrom > source.DatabaseNow || context.EffectiveThrough <= source.DatabaseNow
            || source.CandidateStaffId != context.ExpectedStaffId
            || source.CandidateNpi != context.ExpectedSyntheticNpi
            || source.PractitionerReference != context.PractitionerReference
            || source.StateAuthorityReference != context.StateAuthorityReference
            || source.CandidateRole != "provider" || source.CandidateFacilityId != 10
            || !source.CandidateActive || string.IsNullOrWhiteSpace(source.CandidateDisplayName)
            || source.EligibilityCount != 1 || source.PracticeNetworkCount != 1
            || source.CandidateSelectionCount != 1 || source.ParticipationContextCount != 0
            || source.CanonicalInsuranceCount != 0 || source.DownstreamCount != 0
            || !source.SourceEvidenceComplete || source.AppointmentCreated)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompletedContext(
        TelehealthApplicantRequestParticipationContextSource source,
        TelehealthApplicantRequestParticipationContextRecord record)
    {
        var expected = TelehealthApplicantRequestParticipationContextPolicy.ResolveContext(
            source.CurrentLocationStateCode);
        if (source.RequestStatus != TelehealthApplicantRequestParticipationContextPolicy.RequestStatus
            || source.RequestVersion != TelehealthApplicantRequestParticipationContextPolicy.ResultingRequestVersion
            || source.RequestTriageOutcome != "TelehealthEligible"
            || source.EligibilityCount != 1 || source.PracticeNetworkCount != 1
            || source.CandidateSelectionCount != 1 || source.ParticipationContextCount != 1
            || source.CanonicalInsuranceCount != 0 || source.DownstreamCount != 0
            || !source.SourceEvidenceComplete || source.AppointmentCreated
            || record.ConfirmationId is null
            || source.EligibilityVerificationId != record.EligibilityVerificationId
            || source.PracticeNetworkVerificationId != record.PracticeNetworkVerificationId
            || source.CandidateSelectionId != record.CandidateSelectionId
            || source.CurrentLocationStateCode != expected.StateCode
            || source.CandidateStaffId != expected.ExpectedStaffId
            || source.CandidateNpi != expected.ExpectedSyntheticNpi
            || source.CandidateRole != "provider" || source.CandidateFacilityId != 10
            || !source.CandidateActive || string.IsNullOrWhiteSpace(source.CandidateDisplayName)
            || source.CandidateDisplayName != record.CandidateDisplayName
            || source.CandidateNpi != record.CandidateNpi
            || record.ParticipationContext != expected)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_participation_context_idempotency_conflict",
                "The participation-context idempotency key was already used with different content.");
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_participation_context_provenance_conflict",
        "The current candidate selection or synthetic participation context is unavailable or changed.");
}
