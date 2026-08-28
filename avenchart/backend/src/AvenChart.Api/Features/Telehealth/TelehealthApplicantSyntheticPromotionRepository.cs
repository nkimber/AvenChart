// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantSyntheticPromotionCandidate(
    Guid ApplicantId,
    int Version,
    string Status,
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    string ResidenceStateCode,
    string PostalCode,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int FacilityId,
    Guid AuthorizationDecisionId,
    string AuthorizationDecision,
    DateTimeOffset AuthorizedAt,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    string NetworkBusinessOutcome,
    bool PracticeNetworkChecked,
    bool PracticeInNetwork,
    bool NewPatientsAccepted,
    string ProofingBusinessOutcome,
    string AssuranceLevelAchieved,
    bool IdentityProofed,
    bool IdentityEvidenceCollected,
    bool GovernmentIdentifierCollected,
    bool BiometricDataCollected,
    bool AuthoritativeSourceQueried,
    bool AuthenticatorBound,
    DateTimeOffset ProofingExpiresAt);

public sealed record TelehealthApplicantSyntheticPromotionRecord(
    Guid PromotionId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string Outcome,
    bool PossibleMatchDetected,
    bool CanonicalPatientCreated,
    string PolicyKey,
    int PolicyVersion,
    string EvidenceType,
    DateTimeOffset ExecutedAt,
    bool PortalAccountCreated,
    bool ProspectiveIntakeCompleted,
    bool ConsentCreated,
    bool PracticeAccepted,
    bool InsuranceCreated,
    bool RequestCreated,
    bool QueueEnabled,
    bool CareEnabled);

public sealed class TelehealthApplicantSyntheticPromotionRepository(NpgsqlDataSource dataSource)
{
    private const string CandidateProjection = """
        select a.applicant_id,a.version,a.status,a.legal_first_name,a.legal_last_name,
               a.date_of_birth,a.email,a.phone,a.residence_state_code,a.postal_code,
               a.expires_at,now(),a.facility_id,d.decision_id,d.decision,d.decided_at,
               e.eligibility_status,e.benefit_information_status,e.business_outcome,
               n.business_outcome,n.practice_network_checked,n.practice_in_network,
               n.new_patients_accepted,p.business_outcome,p.assurance_level_achieved,
               p.identity_proofed,p.identity_evidence_collected,
               p.government_identifier_collected,p.biometric_data_collected,
               p.authoritative_source_queried,p.authenticator_bound,p.expires_at
        from telehealth_prospective_applicants a
        join telehealth_applicant_promotion_authorization_decisions d
          on d.applicant_id=a.applicant_id
        join telehealth_applicant_identity_proofing_results p
          on p.identity_proofing_result_id=d.identity_proofing_result_id
         and p.applicant_id=a.applicant_id
        join telehealth_applicant_eligibility_results e
          on e.eligibility_result_id=d.eligibility_result_id
         and e.applicant_id=a.applicant_id
        join telehealth_applicant_practice_network_determinations n
          on n.network_determination_id=d.network_determination_id
         and n.applicant_id=a.applicant_id
        """;

    public async Task<(IReadOnlyList<TelehealthApplicantSyntheticPromotionCandidate> Applicants,
        DateTimeOffset DatabaseNow)> ListAsync(
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CandidateProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.status='SyntheticPromotionAuthorized'
              and d.resulting_applicant_version=a.version
              and d.resulting_applicant_status=a.status
              and d.decision='AuthorizedForSyntheticPromotion'
              and a.expires_at > now() and p.expires_at > now()
              and e.eligibility_status='Active'
              and e.benefit_information_status='Reported'
              and e.business_outcome='EligibleBenefitsReported'
              and n.business_outcome='PracticeInNetworkAcceptingNewPatients'
              and n.practice_network_checked and n.practice_in_network
              and n.new_patients_accepted
              and p.business_outcome='SyntheticProofingPassed'
              and p.assurance_level_achieved='None' and not p.identity_proofed
              and not p.identity_evidence_collected
              and not p.government_identifier_collected
              and not p.biometric_data_collected
              and not p.authoritative_source_queried
              and not p.authenticator_bound
            order by d.decided_at,a.applicant_id
            limit 100;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        var applicants = new List<TelehealthApplicantSyntheticPromotionCandidate>();
        var databaseNow = DateTimeOffset.UtcNow;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            databaseNow = reader.GetFieldValue<DateTimeOffset>(11);
            applicants.Add(ReadCandidate(reader));
        }
        if (applicants.Count == 0)
        {
            await reader.DisposeAsync();
            await using var clock = connection.CreateCommand();
            clock.CommandText = "select now();";
            await using var clockReader = await clock.ExecuteReaderAsync(cancellationToken);
            if (!await clockReader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Database clock is unavailable.");
            }
            databaseNow = clockReader.GetFieldValue<DateTimeOffset>(0);
        }
        return (applicants, databaseNow);
    }

    public async Task<TelehealthApplicantSyntheticPromotionRecord> ExecuteAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        Guid applicantId,
        int expectedVersion,
        string reason,
        bool canonicalPatientCreationAcknowledged,
        bool noPortalNoCareAcknowledged,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var registrationLock = connection.CreateCommand())
        {
            registrationLock.Transaction = transaction;
            registrationLock.CommandText = "select pg_advisory_xact_lock(873421986);";
            await registrationLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId,
            idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var applicant = await LoadForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireEligible(applicant);
        if (applicant.Version != expectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_synthetic_promotion_version_conflict",
                "The applicant changed. Refresh the synthetic-promotion queue before retrying.");
        }

        var possibleMatchDetected = await PossiblePatientMatchExistsAsync(
            connection, transaction, applicant, cancellationToken);
        var outcome = TelehealthApplicantSyntheticPromotionPolicy.Outcome(possibleMatchDetected);
        var nextStatus = TelehealthApplicantSyntheticPromotionPolicy.ResultingStatus(possibleMatchDetected);
        var nextVersion = applicant.Version + 1;
        string? canonicalPatientId = null;
        int? canonicalLegacyPid = null;

        if (!possibleMatchDetected)
        {
            canonicalPatientId = TelehealthApplicantSyntheticPromotionPolicy.CanonicalPatientId(applicantId);
            if (await PatientIdentifierExistsAsync(
                connection, transaction, canonicalPatientId, cancellationToken))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_synthetic_promotion_identifier_conflict",
                    "The deterministic synthetic patient identifier is already in use. No patient record was created.");
            }
            canonicalLegacyPid = await InsertPatientAsync(
                connection, transaction, applicant, canonicalPatientId, cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPromotionAuthorized';
                """;
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_synthetic_promotion_version_conflict",
                    "The applicant changed. Refresh the synthetic-promotion queue before retrying.");
            }
        }

        var promotionId = Guid.NewGuid();
        DateTimeOffset executedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_synthetic_promotions(
                  promotion_id,applicant_id,practice_id,facility_id,
                  authorization_decision_id,resulting_applicant_version,
                  resulting_applicant_status,command,outcome,possible_match_detected,
                  canonical_patient_id,canonical_legacy_pid,canonical_patient_created,
                  canonical_patient_creation_acknowledged,no_portal_no_care_acknowledged,
                  reason,policy_key,policy_version,evidence_type,
                  assurance_level_achieved,identity_proofed,
                  executed_by_staff_id,executed_by_actor_id,executed_by_role,
                  idempotency_key,command_fingerprint)
                values(
                  @promotionId,@applicantId,@practiceId,@facilityId,
                  @authorizationDecisionId,@nextVersion,@nextStatus,
                  'PromoteAuthorizedSyntheticApplicant',@outcome,@possibleMatch,
                  @patientId,@legacyPid,@patientCreated,@creationAcknowledged,
                  @noPortalNoCareAcknowledged,@reason,
                  'SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION',1,
                  'AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK',
                  'None',false,@staffId,@actorId,'administrator',
                  @idempotencyKey,@commandFingerprint)
                returning executed_at;
                """;
            insert.Parameters.AddWithValue("promotionId", promotionId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("authorizationDecisionId", applicant.AuthorizationDecisionId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("outcome", outcome);
            insert.Parameters.AddWithValue("possibleMatch", possibleMatchDetected);
            insert.Parameters.AddWithValue("patientId", (object?)canonicalPatientId ?? DBNull.Value);
            insert.Parameters.AddWithValue("legacyPid", (object?)canonicalLegacyPid ?? DBNull.Value);
            insert.Parameters.AddWithValue("patientCreated", !possibleMatchDetected);
            insert.Parameters.AddWithValue("creationAcknowledged", canonicalPatientCreationAcknowledged);
            insert.Parameters.AddWithValue("noPortalNoCareAcknowledged", noPortalNoCareAcknowledged);
            insert.Parameters.AddWithValue("reason", reason);
            insert.Parameters.AddWithValue("staffId", (object?)staffId ?? DBNull.Value);
            insert.Parameters.AddWithValue("actorId", actorId);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic promotion time is unavailable.");
            }
            executedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-synthetic-patient-promotion-recorded',
                       'SyntheticPromotionAuthorized',@nextStatus,'administrator',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "synthetic-promotion:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return NewRecord(
            promotionId, applicantId, nextVersion, nextStatus, outcome,
            possibleMatchDetected, !possibleMatchDetected, executedAt);
    }

    private static async Task<TelehealthApplicantSyntheticPromotionCandidate?> LoadForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = CandidateProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            for update of a;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCandidate(reader) : null;
    }

    private static TelehealthApplicantSyntheticPromotionCandidate ReadCandidate(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5),
        reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10), reader.GetFieldValue<DateTimeOffset>(11),
        reader.GetInt32(12), reader.GetGuid(13), reader.GetString(14),
        reader.GetFieldValue<DateTimeOffset>(15), reader.GetString(16), reader.GetString(17),
        reader.GetString(18), reader.GetString(19), reader.GetBoolean(20),
        reader.GetBoolean(21), reader.GetBoolean(22), reader.GetString(23),
        reader.GetString(24), reader.GetBoolean(25), reader.GetBoolean(26),
        reader.GetBoolean(27), reader.GetBoolean(28), reader.GetBoolean(29),
        reader.GetBoolean(30), reader.GetFieldValue<DateTimeOffset>(31));

    private static void RequireEligible(TelehealthApplicantSyntheticPromotionCandidate applicant)
    {
        if (applicant.Status != "SyntheticPromotionAuthorized"
            || applicant.AuthorizationDecision != "AuthorizedForSyntheticPromotion")
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_synthetic_promotion_state_conflict",
                "The applicant is not authorized for the synthetic patient-promotion exercise.");
        }
        if (applicant.ApplicantExpiresAt <= applicant.DatabaseNow
            || applicant.ProofingExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_synthetic_promotion_evidence_expired",
                "The applicant or synthetic process evidence expired. No patient record was created.");
        }
        if (applicant.EligibilityStatus != "Active"
            || applicant.BenefitInformationStatus != "Reported"
            || applicant.EligibilityBusinessOutcome != "EligibleBenefitsReported"
            || applicant.NetworkBusinessOutcome != "PracticeInNetworkAcceptingNewPatients"
            || !applicant.PracticeNetworkChecked
            || !applicant.PracticeInNetwork
            || !applicant.NewPatientsAccepted
            || applicant.ProofingBusinessOutcome != "SyntheticProofingPassed"
            || applicant.AssuranceLevelAchieved != "None"
            || applicant.IdentityProofed
            || applicant.IdentityEvidenceCollected
            || applicant.GovernmentIdentifierCollected
            || applicant.BiometricDataCollected
            || applicant.AuthoritativeSourceQueried
            || applicant.AuthenticatorBound)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_synthetic_promotion_evidence_invalid",
                "The complete server-held synthetic evidence chain does not permit patient-shell creation.");
        }
    }

    private static async Task<bool> PossiblePatientMatchExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TelehealthApplicantSyntheticPromotionCandidate applicant,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(
              select 1
              from patients patient
              where patient.facility_id=@facilityId
                and patient.merged_into_patient_id is null
                and (
                  (lower(btrim(patient.first_name))=@firstName
                   and lower(btrim(patient.last_name))=@lastName
                   and patient.date_of_birth=@dateOfBirth)
                  or
                  (patient.date_of_birth=@dateOfBirth
                   and lower(btrim(coalesce(patient.email,'')))=@email)
                  or
                  (patient.date_of_birth=@dateOfBirth
                   and right(regexp_replace(coalesce(nullif(patient.phone_cell,''),
                                                      nullif(patient.phone_home,''),
                                                      patient.phone,''),
                                            '[^0-9]','','g'),10)=@phoneDigits)
                ));
            """;
        command.Parameters.AddWithValue("facilityId", applicant.FacilityId);
        command.Parameters.AddWithValue("firstName", applicant.LegalFirstName.ToLowerInvariant());
        command.Parameters.AddWithValue("lastName", applicant.LegalLastName.ToLowerInvariant());
        command.Parameters.AddWithValue("dateOfBirth", applicant.DateOfBirth);
        command.Parameters.AddWithValue("email", applicant.Email);
        command.Parameters.AddWithValue("phoneDigits", applicant.Phone[^10..]);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Patient duplicate check is unavailable."));
    }

    private static async Task<bool> PatientIdentifierExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from patients where canonical_id=@patientId or pubpid=@patientId);";
        command.Parameters.AddWithValue("patientId", patientId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Patient identifier check is unavailable."));
    }

    private static async Task<int> InsertPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TelehealthApplicantSyntheticPromotionCandidate applicant,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patients(
              canonical_id,legacy_pid,pubpid,first_name,last_name,date_of_birth,
              purpose,state,postal_code,email,phone,phone_home,phone_cell,
              provider_id,facility_id,portal_enabled,registration_date)
            values(
              @patientId,nextval('patients_legacy_pid_seq'),@patientId,
              @firstName,@lastName,@dateOfBirth,
              'synthetic telehealth prospective promotion',@stateCode,@postalCode,
              @email,@phone,@phone,@phone,null,@facilityId,false,
              (select base_date from dataset_metadata order by dataset_id limit 1))
            returning legacy_pid;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("firstName", applicant.LegalFirstName);
        command.Parameters.AddWithValue("lastName", applicant.LegalLastName);
        command.Parameters.AddWithValue("dateOfBirth", applicant.DateOfBirth);
        command.Parameters.AddWithValue("stateCode", applicant.ResidenceStateCode);
        command.Parameters.AddWithValue("postalCode", applicant.PostalCode);
        command.Parameters.AddWithValue("email", applicant.Email);
        command.Parameters.AddWithValue("phone", applicant.Phone);
        command.Parameters.AddWithValue("facilityId", applicant.FacilityId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Synthetic patient creation did not return an identifier."));
    }

    private static async Task<(TelehealthApplicantSyntheticPromotionRecord Record,
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
            select promotion_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,outcome,possible_match_detected,
                   canonical_patient_created,policy_key,policy_version,evidence_type,
                   executed_at,portal_account_created,prospective_intake_completed,
                   consent_created,practice_accepted,insurance_created,request_created,
                   queue_enabled,care_enabled,command_fingerprint
            from telehealth_applicant_synthetic_promotions
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
            reader.GetString(3), reader.GetString(4), reader.GetBoolean(5), reader.GetBoolean(6),
            reader.GetString(7), reader.GetInt32(8), reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10), reader.GetBoolean(11),
            reader.GetBoolean(12), reader.GetBoolean(13), reader.GetBoolean(14),
            reader.GetBoolean(15), reader.GetBoolean(16), reader.GetBoolean(17),
            reader.GetBoolean(18)), reader.GetString(19));
    }

    private static TelehealthApplicantSyntheticPromotionRecord NewRecord(
        Guid promotionId,
        Guid applicantId,
        int version,
        string status,
        string outcome,
        bool possibleMatch,
        bool patientCreated,
        DateTimeOffset executedAt) => new(
        promotionId, applicantId, version, status, outcome, possibleMatch, patientCreated,
        TelehealthApplicantSyntheticPromotionPolicy.PolicyKey,
        TelehealthApplicantSyntheticPromotionPolicy.PolicyVersion,
        TelehealthApplicantSyntheticPromotionPolicy.EvidenceType,
        executedAt, false, false, false, false, false, false, false, false);

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_synthetic_promotion_idempotency_conflict",
                "The synthetic-promotion idempotency key was already used with different command content.");
        }
    }
}
