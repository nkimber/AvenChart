// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveIdentityProofingCandidate(
    Guid ApplicantId,
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid MemberInsuranceDetailsId,
    Guid EligibilityResultId,
    Guid NetworkDeterminationId,
    string CurrentLocationStateCode,
    string PlanKey,
    DateTimeOffset DatabaseNow);

public sealed record TelehealthProspectiveIdentityProofingRecord(
    Guid IdentityProofingResultId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CurrentLocationStateCode,
    string PlanKey,
    string PrivacyNoticeKey,
    int PrivacyNoticeVersion,
    bool PrivacyNoticeAcknowledged,
    TelehealthProspectiveIdentityProofingAdapterResult AdapterResult,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectiveIdentityProofingContext(
    int Version,
    string Status,
    string AccessKeyHash,
    string? DuplicateDisposition,
    DateTimeOffset? ContactVerifiedAt,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid MemberInsuranceDetailsId,
    Guid EligibilityResultId,
    Guid NetworkDeterminationId,
    int NetworkResultingVersion,
    string NetworkResultingStatus,
    string CurrentLocationStateCode,
    string PlanKey,
    string EligibilityStatus,
    string BenefitInformationStatus,
    string EligibilityBusinessOutcome,
    DateTimeOffset EligibilityExpiresAt,
    string NetworkBusinessOutcome,
    bool PracticeNetworkChecked,
    bool PracticeInNetwork,
    bool NewPatientsAccepted,
    DateTimeOffset NetworkCheckedAt,
    DateTimeOffset NetworkExpiresAt);

public sealed class TelehealthProspectiveIdentityProofingRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProspectiveIdentityProofingRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        int expectedVersion,
        string privacyNoticeKey,
        int privacyNoticeVersion,
        bool privacyNoticeAcknowledged,
        string idempotencyKey,
        string commandFingerprint,
        Func<TelehealthProspectiveIdentityProofingCandidate, CancellationToken,
            ValueTask<TelehealthProspectiveIdentityProofingAdapterResult>> resolveAsync,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligibleContext(context);
        if (context.Version != expectedVersion)
        {
            throw VersionConflict();
        }

        var candidate = new TelehealthProspectiveIdentityProofingCandidate(
            applicantId,
            context.IdentityReviewDecisionId,
            context.SafetyEvaluationId,
            context.PurposeId,
            context.PrecheckId,
            context.MemberInsuranceDetailsId,
            context.EligibilityResultId,
            context.NetworkDeterminationId,
            context.CurrentLocationStateCode,
            context.PlanKey,
            context.DatabaseNow.ToUniversalTime());
        var adapterResult = await resolveAsync(candidate, cancellationToken);

        var nextVersion = context.Version + 1;
        const string nextStatus = "SyntheticIdentityProofingRecorded";
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@version,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticPracticeNetworkRecorded';
                """;
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("version", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", context.Version);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw VersionConflict();
            }
        }

        var resultId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_identity_proofing_results(
                  identity_proofing_result_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  visit_purpose_id,practice_network_precheck_id,
                  member_insurance_details_id,eligibility_result_id,
                  network_determination_id,resulting_applicant_version,
                  resulting_applicant_status,location_state_code,plan_key,
                  network_checked_at,network_expires_at,
                  privacy_notice_key,privacy_notice_version,privacy_notice_acknowledged,
                  adapter_mode,compatibility_target,practice_statement_key,
                  practice_statement_version,dataset_key,dataset_version,
                  dataset_effective_from,dataset_effective_through,source_last_updated_at,
                  request_trace_token,response_trace_token,proofing_method,
                  transport_outcome,evidence_collection_status,evidence_validation_status,
                  attribute_validation_status,applicant_verification_status,
                  fraud_check_status,business_outcome,proofing_session_reference,
                  evidence_package_reference,checked_at,expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @resultId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyId,@purposeId,@precheckId,
                  @detailsId,@eligibilityId,@networkId,@nextVersion,@nextStatus,
                  @locationState,@planKey,@networkCheckedAt,@networkExpiresAt,
                  @noticeKey,@noticeVersion,@noticeAcknowledged,
                  @adapterMode,@compatibilityTarget,@practiceStatementKey,
                  @practiceStatementVersion,@datasetKey,@datasetVersion,
                  @datasetEffectiveFrom,@datasetEffectiveThrough,@sourceLastUpdatedAt,
                  @requestTraceToken,@responseTraceToken,@proofingMethod,
                  @transportOutcome,@evidenceCollectionStatus,@evidenceValidationStatus,
                  @attributeValidationStatus,@applicantVerificationStatus,
                  @fraudCheckStatus,@businessOutcome,@proofingSessionReference,
                  @evidencePackageReference,@checkedAt,@expiresAt,
                  @idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("resultId", resultId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", context.IdentityReviewDecisionId);
            insert.Parameters.AddWithValue("safetyId", context.SafetyEvaluationId);
            insert.Parameters.AddWithValue("purposeId", context.PurposeId);
            insert.Parameters.AddWithValue("precheckId", context.PrecheckId);
            insert.Parameters.AddWithValue("detailsId", context.MemberInsuranceDetailsId);
            insert.Parameters.AddWithValue("eligibilityId", context.EligibilityResultId);
            insert.Parameters.AddWithValue("networkId", context.NetworkDeterminationId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationState", context.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("planKey", context.PlanKey);
            insert.Parameters.AddWithValue("networkCheckedAt", context.NetworkCheckedAt);
            insert.Parameters.AddWithValue("networkExpiresAt", context.NetworkExpiresAt);
            insert.Parameters.AddWithValue("noticeKey", privacyNoticeKey);
            insert.Parameters.AddWithValue("noticeVersion", privacyNoticeVersion);
            insert.Parameters.AddWithValue("noticeAcknowledged", privacyNoticeAcknowledged);
            AddAdapterParameters(insert, adapterResult);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Identity-proofing result time is unavailable.");
            }
            recordedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-synthetic-identity-proofing-recorded',
                       'SyntheticPracticeNetworkRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "prospective-identity-proofing:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            resultId,
            applicantId,
            nextVersion,
            nextStatus,
            context.CurrentLocationStateCode,
            context.PlanKey,
            privacyNoticeKey,
            privacyNoticeVersion,
            privacyNoticeAcknowledged,
            adapterResult,
            recordedAt);
    }

    private static void AddAdapterParameters(
        NpgsqlCommand command,
        TelehealthProspectiveIdentityProofingAdapterResult result)
    {
        command.Parameters.AddWithValue("adapterMode", result.AdapterMode);
        command.Parameters.AddWithValue("compatibilityTarget", result.CompatibilityTarget);
        command.Parameters.AddWithValue("practiceStatementKey", result.PracticeStatementKey);
        command.Parameters.AddWithValue("practiceStatementVersion", result.PracticeStatementVersion);
        command.Parameters.AddWithValue("datasetKey", result.DatasetKey);
        command.Parameters.AddWithValue("datasetVersion", result.DatasetVersion);
        command.Parameters.AddWithValue("datasetEffectiveFrom", result.DatasetEffectiveFrom);
        command.Parameters.AddWithValue("datasetEffectiveThrough", result.DatasetEffectiveThrough);
        command.Parameters.AddWithValue("sourceLastUpdatedAt", result.SourceLastUpdatedAt);
        command.Parameters.AddWithValue("requestTraceToken", result.RequestTraceToken);
        command.Parameters.AddWithValue("responseTraceToken", result.ResponseTraceToken);
        command.Parameters.AddWithValue("proofingMethod", result.ProofingMethod);
        command.Parameters.AddWithValue("transportOutcome", result.TransportOutcome);
        command.Parameters.AddWithValue("evidenceCollectionStatus", result.EvidenceCollectionStatus);
        command.Parameters.AddWithValue("evidenceValidationStatus", result.EvidenceValidationStatus);
        command.Parameters.AddWithValue("attributeValidationStatus", result.AttributeValidationStatus);
        command.Parameters.AddWithValue("applicantVerificationStatus", result.ApplicantVerificationStatus);
        command.Parameters.AddWithValue("fraudCheckStatus", result.FraudCheckStatus);
        command.Parameters.AddWithValue("businessOutcome", result.BusinessOutcome);
        command.Parameters.AddWithValue("proofingSessionReference", result.ProofingSessionReference);
        command.Parameters.AddWithValue("evidencePackageReference", result.EvidencePackageReference);
        command.Parameters.AddWithValue("checkedAt", result.CheckedAt);
        command.Parameters.AddWithValue("expiresAt", result.ExpiresAt);
    }

    private static async Task<TelehealthProspectiveIdentityProofingContext?> LoadContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.version,a.status,a.access_key_hash,a.duplicate_disposition,
                   a.contact_verified_at,a.expires_at,now(),
                   n.identity_review_decision_id,n.safety_triage_evaluation_id,
                   n.visit_purpose_id,n.practice_network_precheck_id,
                   n.member_insurance_details_id,n.eligibility_result_id,
                   n.network_determination_id,n.resulting_applicant_version,
                   n.resulting_applicant_status,n.location_state_code,n.plan_key,
                   n.eligibility_status,n.benefit_information_status,
                   n.eligibility_business_outcome,n.eligibility_expires_at,
                   n.business_outcome,n.practice_network_checked,n.practice_in_network,
                   n.new_patients_accepted,n.checked_at,n.expires_at
            from telehealth_prospective_applicants a
            join telehealth_applicant_practice_network_determinations n
              on n.applicant_id=a.applicant_id
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            for update of a;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new(
            Convert.ToInt32(reader.GetInt64(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetGuid(7),
            reader.GetGuid(8),
            reader.GetGuid(9),
            reader.GetGuid(10),
            reader.GetGuid(11),
            reader.GetGuid(12),
            reader.GetGuid(13),
            Convert.ToInt32(reader.GetInt64(14)),
            reader.GetString(15),
            reader.GetString(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.GetString(20),
            reader.GetFieldValue<DateTimeOffset>(21),
            reader.GetString(22),
            reader.GetBoolean(23),
            reader.GetBoolean(24),
            reader.GetBoolean(25),
            reader.GetFieldValue<DateTimeOffset>(26),
            reader.GetFieldValue<DateTimeOffset>(27));
    }

    private static async Task<(TelehealthProspectiveIdentityProofingRecord Record,
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
            select identity_proofing_result_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,location_state_code,plan_key,
                   privacy_notice_key,privacy_notice_version,privacy_notice_acknowledged,
                   adapter_mode,compatibility_target,practice_statement_key,
                   practice_statement_version,dataset_key,dataset_version,
                   dataset_effective_from,dataset_effective_through,source_last_updated_at,
                   request_trace_token,response_trace_token,proofing_method,
                   transport_outcome,evidence_collection_status,evidence_validation_status,
                   attribute_validation_status,applicant_verification_status,
                   fraud_check_status,business_outcome,proofing_session_reference,
                   evidence_package_reference,checked_at,expires_at,recorded_at,
                   command_fingerprint
            from telehealth_applicant_identity_proofing_results
            where practice_id=@practiceId and facility_id=@facilityId
              and applicant_id=@applicantId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var adapter = new TelehealthProspectiveIdentityProofingAdapterResult(
            reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetInt32(12),
            reader.GetString(13), reader.GetInt32(14), reader.GetFieldValue<DateTimeOffset>(15),
            reader.GetFieldValue<DateTimeOffset>(16), reader.GetFieldValue<DateTimeOffset>(17),
            reader.GetGuid(18), reader.GetGuid(19), reader.GetString(20), reader.GetString(21),
            reader.GetString(22), reader.GetString(23), reader.GetString(24), reader.GetString(25),
            reader.GetString(26), reader.GetString(27), reader.GetString(28), reader.GetString(29),
            reader.GetFieldValue<DateTimeOffset>(30), reader.GetFieldValue<DateTimeOffset>(31));
        return (new(
                reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.GetInt32(7), reader.GetBoolean(8), adapter,
                reader.GetFieldValue<DateTimeOffset>(32)),
            reader.GetString(33));
    }

    private static void RequireEligibleContext(TelehealthProspectiveIdentityProofingContext context)
    {
        if (context.ApplicantExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before identity proofing. Start again.");
        }
        if (context.EligibilityExpiresAt <= context.DatabaseNow
            || context.NetworkExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_proofing_evidence_expired",
                "The synthetic eligibility or practice-network evidence expired before identity proofing. Start again with a new synthetic applicant.");
        }
        if (context.Status != "SyntheticPracticeNetworkRecorded"
            || context.Version != context.NetworkResultingVersion
            || context.NetworkResultingStatus != "SyntheticPracticeNetworkRecorded"
            || context.DuplicateDisposition != "NoCandidate"
            || context.ContactVerifiedAt is null
            || context.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || context.PlanKey != "harbor-mutual-hd"
            || context.EligibilityStatus != "Active"
            || context.BenefitInformationStatus != "Reported"
            || context.EligibilityBusinessOutcome != "EligibleBenefitsReported"
            || context.NetworkBusinessOutcome != "PracticeInNetworkAcceptingNewPatients"
            || !context.PracticeNetworkChecked
            || !context.PracticeInNetwork
            || !context.NewPatientsAccepted
            || context.NetworkCheckedAt > context.DatabaseNow)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_proofing_state_conflict",
                "The applicant is not eligible for this bounded synthetic identity-proofing exercise.");
        }
    }

    private static void RequireAccess(string existingHash, string suppliedHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existingHash, suppliedHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string supplied)
    {
        if (!string.Equals(existing, supplied, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_proofing_idempotency_conflict",
                "The identity-proofing idempotency key was already used with different command content.");
        }
    }

    private static TelehealthProblem VersionConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_version_conflict",
        "The applicant changed. Reload the synthetic applicant before retrying.");
}
