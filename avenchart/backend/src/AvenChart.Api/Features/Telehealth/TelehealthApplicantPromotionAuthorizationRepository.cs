// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantPromotionAuthorizationCandidate(
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
    DateTimeOffset CreatedAt,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid MemberInsuranceDetailsId,
    Guid EligibilityResultId,
    Guid NetworkDeterminationId,
    Guid IdentityProofingResultId,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    string NetworkBusinessOutcome,
    string ProofingMethod,
    string TransportOutcome,
    string EvidenceCollectionStatus,
    string EvidenceValidationStatus,
    string AttributeValidationStatus,
    string ApplicantVerificationStatus,
    string FraudCheckStatus,
    string ProofingBusinessOutcome,
    string AssuranceLevelAchieved,
    bool IdentityProofed,
    DateTimeOffset ProofingCheckedAt,
    DateTimeOffset ProofingExpiresAt);

public sealed record TelehealthApplicantPromotionAuthorizationDecisionRecord(
    Guid DecisionId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string Decision,
    string Reason,
    string PolicyKey,
    int PolicyVersion,
    string EvidenceType,
    DateTimeOffset DecidedAt,
    bool NoneAssuranceAcknowledged,
    bool RealIdentityProofed,
    bool CanonicalPatientCreated,
    bool ChartLinked,
    bool PortalAccountCreated,
    bool ProspectiveIntakeCompleted,
    bool ConsentCreated,
    bool PracticeAccepted,
    bool RequestCreated,
    bool QueueEnabled);

public sealed class TelehealthApplicantPromotionAuthorizationRepository(NpgsqlDataSource dataSource)
{
    private const string CandidateProjection = """
        select a.applicant_id,a.version,a.status,a.legal_first_name,a.legal_last_name,
               a.date_of_birth,a.email,a.phone,a.residence_state_code,a.postal_code,
               a.created_at,a.expires_at,now(),
               p.identity_review_decision_id,p.safety_triage_evaluation_id,
               p.visit_purpose_id,p.practice_network_precheck_id,
               p.member_insurance_details_id,p.eligibility_result_id,
               p.network_determination_id,p.identity_proofing_result_id,
               p.plan_key,m.payer_display_name,m.product_display_name,
               e.eligibility_status,e.benefit_information_status,e.business_outcome,
               n.business_outcome,p.proofing_method,p.transport_outcome,
               p.evidence_collection_status,p.evidence_validation_status,
               p.attribute_validation_status,p.applicant_verification_status,
               p.fraud_check_status,p.business_outcome,p.assurance_level_achieved,
               p.identity_proofed,p.checked_at,p.expires_at
        from telehealth_prospective_applicants a
        join telehealth_applicant_identity_proofing_results p
          on p.applicant_id=a.applicant_id
        join telehealth_applicant_member_insurance_details m
          on m.details_id=p.member_insurance_details_id and m.applicant_id=a.applicant_id
        join telehealth_applicant_eligibility_results e
          on e.eligibility_result_id=p.eligibility_result_id and e.applicant_id=a.applicant_id
        join telehealth_applicant_practice_network_determinations n
          on n.network_determination_id=p.network_determination_id and n.applicant_id=a.applicant_id
        """;

    public async Task<(IReadOnlyList<TelehealthApplicantPromotionAuthorizationCandidate> Applicants,
        DateTimeOffset DatabaseNow)> ListAsync(
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CandidateProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.status='SyntheticIdentityProofingRecorded'
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
            order by p.recorded_at,a.applicant_id
            limit 100;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        var applicants = new List<TelehealthApplicantPromotionAuthorizationCandidate>();
        var databaseNow = DateTimeOffset.UtcNow;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            databaseNow = reader.GetFieldValue<DateTimeOffset>(12);
            applicants.Add(ReadCandidate(reader));
        }
        if (applicants.Count == 0)
        {
            await reader.DisposeAsync();
            await using var clock = connection.CreateCommand();
            clock.CommandText = "select now();";
            databaseNow = (DateTimeOffset)(await clock.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Database clock is unavailable."));
        }
        return (applicants, databaseNow);
    }

    public async Task<TelehealthApplicantPromotionAuthorizationDecisionRecord> RecordAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        string actorRole,
        Guid applicantId,
        int expectedVersion,
        string decision,
        string reason,
        bool noneAssuranceAcknowledged,
        bool syntheticDataConfirmed,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var replay = await LoadDecisionByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        var applicant = await LoadForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();

        replay = await LoadDecisionByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(applicant);
        if (applicant.Version != expectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Refresh the promotion-authorization queue before retrying.");
        }

        var nextStatus = TelehealthApplicantPromotionAuthorizationPolicy.ResultingStatus(decision);
        var nextVersion = applicant.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticIdentityProofingRecorded';
                """;
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Refresh the promotion-authorization queue before retrying.");
            }
        }

        var decisionId = Guid.NewGuid();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_promotion_authorization_decisions(
                  decision_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,visit_purpose_id,
                  practice_network_precheck_id,member_insurance_details_id,
                  eligibility_result_id,network_determination_id,identity_proofing_result_id,
                  resulting_applicant_version,resulting_applicant_status,
                  location_state_code,plan_key,eligibility_business_outcome,
                  network_business_outcome,proofing_business_outcome,
                  assurance_level_achieved,proofing_identity_proofed,
                  proofing_checked_at,proofing_expires_at,applicant_expires_at,
                  decision,reason,none_assurance_acknowledged,synthetic_data_confirmed,
                  policy_key,policy_version,evidence_type,
                  decided_by_staff_id,decided_by_actor_id,decided_by_role,
                  idempotency_key,command_fingerprint)
                values(
                  @decisionId,@applicantId,@practiceId,@facilityId,
                  @identityReviewDecisionId,@safetyEvaluationId,@purposeId,@precheckId,
                  @memberInsuranceDetailsId,@eligibilityResultId,@networkDeterminationId,
                  @identityProofingResultId,@nextVersion,@nextStatus,@stateCode,@planKey,
                  @eligibilityOutcome,@networkOutcome,@proofingOutcome,@assuranceLevel,
                  @proofingIdentityProofed,@proofingCheckedAt,@proofingExpiresAt,
                  @applicantExpiresAt,@decision,@reason,@noneAcknowledged,@syntheticConfirmed,
                  'SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION',1,
                  'COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY',
                  @staffId,@actorId,@actorRole,@idempotencyKey,@commandFingerprint);
                """;
            insert.Parameters.AddWithValue("decisionId", decisionId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityReviewDecisionId", applicant.IdentityReviewDecisionId);
            insert.Parameters.AddWithValue("safetyEvaluationId", applicant.SafetyEvaluationId);
            insert.Parameters.AddWithValue("purposeId", applicant.PurposeId);
            insert.Parameters.AddWithValue("precheckId", applicant.PrecheckId);
            insert.Parameters.AddWithValue("memberInsuranceDetailsId", applicant.MemberInsuranceDetailsId);
            insert.Parameters.AddWithValue("eligibilityResultId", applicant.EligibilityResultId);
            insert.Parameters.AddWithValue("networkDeterminationId", applicant.NetworkDeterminationId);
            insert.Parameters.AddWithValue("identityProofingResultId", applicant.IdentityProofingResultId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("stateCode", applicant.ResidenceStateCode);
            insert.Parameters.AddWithValue("planKey", applicant.PlanKey);
            insert.Parameters.AddWithValue("eligibilityOutcome", applicant.EligibilityBusinessOutcome);
            insert.Parameters.AddWithValue("networkOutcome", applicant.NetworkBusinessOutcome);
            insert.Parameters.AddWithValue("proofingOutcome", applicant.ProofingBusinessOutcome);
            insert.Parameters.AddWithValue("assuranceLevel", applicant.AssuranceLevelAchieved);
            insert.Parameters.AddWithValue("proofingIdentityProofed", applicant.IdentityProofed);
            insert.Parameters.AddWithValue("proofingCheckedAt", applicant.ProofingCheckedAt);
            insert.Parameters.AddWithValue("proofingExpiresAt", applicant.ProofingExpiresAt);
            insert.Parameters.AddWithValue("applicantExpiresAt", applicant.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("decision", decision);
            insert.Parameters.AddWithValue("reason", reason);
            insert.Parameters.AddWithValue("noneAcknowledged", noneAssuranceAcknowledged);
            insert.Parameters.AddWithValue("syntheticConfirmed", syntheticDataConfirmed);
            insert.Parameters.AddWithValue("staffId", (object?)staffId ?? DBNull.Value);
            insert.Parameters.AddWithValue("actorId", actorId);
            insert.Parameters.AddWithValue("actorRole", actorRole);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-synthetic-promotion-authorization-recorded',
                       'SyntheticIdentityProofingRecorded',@nextStatus,'administrator',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "promotion-authorization:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            decisionId, applicantId, nextVersion, nextStatus, decision, reason,
            TelehealthApplicantPromotionAuthorizationPolicy.PolicyKey,
            TelehealthApplicantPromotionAuthorizationPolicy.PolicyVersion,
            TelehealthApplicantPromotionAuthorizationPolicy.EvidenceType,
            await LoadDecisionTimeAsync(connection, decisionId, cancellationToken),
            noneAssuranceAcknowledged, false, false, false, false, false, false, false, false, false);
    }

    private static async Task<TelehealthApplicantPromotionAuthorizationCandidate?> LoadForUpdateAsync(
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

    private static TelehealthApplicantPromotionAuthorizationCandidate ReadCandidate(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5),
        reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10), reader.GetFieldValue<DateTimeOffset>(11),
        reader.GetFieldValue<DateTimeOffset>(12), reader.GetGuid(13), reader.GetGuid(14),
        reader.GetGuid(15), reader.GetGuid(16), reader.GetGuid(17), reader.GetGuid(18),
        reader.GetGuid(19), reader.GetGuid(20), reader.GetString(21), reader.GetString(22),
        reader.GetString(23), reader.GetString(24), reader.GetString(25), reader.GetString(26),
        reader.GetString(27), reader.GetString(28), reader.GetString(29), reader.GetString(30),
        reader.GetString(31), reader.GetString(32), reader.GetString(33), reader.GetString(34),
        reader.GetString(35), reader.GetString(36), reader.GetBoolean(37),
        reader.GetFieldValue<DateTimeOffset>(38), reader.GetFieldValue<DateTimeOffset>(39));

    private static void RequireEligible(TelehealthApplicantPromotionAuthorizationCandidate applicant)
    {
        if (applicant.Status != "SyntheticIdentityProofingRecorded")
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_promotion_authorization_state_conflict",
                "The applicant is not awaiting synthetic promotion authorization.");
        }
        if (applicant.ApplicantExpiresAt <= applicant.DatabaseNow
            || applicant.ProofingExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_promotion_authorization_evidence_expired",
                "The applicant or synthetic process evidence expired. Start the prospective flow again.");
        }
        if (applicant.EligibilityStatus != "Active"
            || applicant.BenefitInformationStatus != "Reported"
            || applicant.EligibilityBusinessOutcome != "EligibleBenefitsReported"
            || applicant.NetworkBusinessOutcome != "PracticeInNetworkAcceptingNewPatients"
            || applicant.ProofingBusinessOutcome != "SyntheticProofingPassed"
            || applicant.AssuranceLevelAchieved != "None"
            || applicant.IdentityProofed)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_promotion_authorization_evidence_invalid",
                "The complete server-held synthetic evidence chain does not permit this governance decision.");
        }
    }

    private static async Task<(TelehealthApplicantPromotionAuthorizationDecisionRecord Record,
        string CommandFingerprint)?> LoadDecisionByIdempotencyAsync(
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
            select decision_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,decision,reason,policy_key,policy_version,
                   evidence_type,decided_at,none_assurance_acknowledged,
                   real_identity_proofed,canonical_patient_created,chart_linked,
                   portal_account_created,prospective_intake_completed,consent_created,
                   practice_accepted,request_created,queue_enabled,command_fingerprint
            from telehealth_applicant_promotion_authorization_decisions
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
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetInt32(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetBoolean(10), reader.GetBoolean(11), reader.GetBoolean(12),
            reader.GetBoolean(13), reader.GetBoolean(14), reader.GetBoolean(15),
            reader.GetBoolean(16), reader.GetBoolean(17), reader.GetBoolean(18),
            reader.GetBoolean(19)), reader.GetString(20));
    }

    private static async Task<DateTimeOffset> LoadDecisionTimeAsync(
        NpgsqlConnection connection,
        Guid decisionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select decided_at from telehealth_applicant_promotion_authorization_decisions where decision_id=@decisionId;";
        command.Parameters.AddWithValue("decisionId", decisionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Promotion-authorization decision time is unavailable.");
        }
        return reader.GetFieldValue<DateTimeOffset>(0);
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_promotion_authorization_idempotency_conflict",
                "The promotion-authorization idempotency key was already used with different command content.");
        }
    }
}
