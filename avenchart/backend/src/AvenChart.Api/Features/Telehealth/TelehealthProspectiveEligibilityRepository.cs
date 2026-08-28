// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveEligibilityRecord(
    Guid EligibilityResultId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeNetworkStatus,
    string MemberIdLast4,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    DateOnly DateOfService,
    string ServiceCategory,
    TelehealthProspectiveEligibilityAdapterResult AdapterResult,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectiveEligibilityCandidate(
    Guid IdentityReviewDecisionId,
    Guid SafetyEvaluationId,
    Guid PurposeId,
    Guid PrecheckId,
    Guid DetailsId,
    string CurrentLocationStateCode,
    string PurposeCategory,
    string PlanKey,
    string PayerDisplayName,
    string ProductDisplayName,
    string PracticeNetworkStatus,
    string MemberIdLast4,
    bool GroupNumberPresent,
    string? GroupNumberLast4,
    string SubscriberRelationship,
    string CoveragePriority,
    string ProtectedPayload,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthProspectiveEligibilityContext(
    int Version,
    string Status,
    string AccessKeyHash,
    string? DuplicateDisposition,
    DateTimeOffset? ContactVerifiedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid? IdentityReviewDecisionId,
    string? IdentityReviewDecision,
    bool IdentityProofed,
    bool CanonicalPatientCreated,
    Guid? SafetyEvaluationId,
    string? SafetyOutcome,
    string? SafetyResultingStatus,
    string? CurrentLocationStateCode,
    Guid? PurposeId,
    string? PurposeCategory,
    string? PurposeResultingStatus,
    Guid? PurposeSafetyEvaluationId,
    Guid? PurposeIdentityReviewDecisionId,
    Guid? PrecheckId,
    string? PrecheckResultingStatus,
    Guid? PrecheckIdentityReviewDecisionId,
    Guid? PrecheckSafetyEvaluationId,
    Guid? PrecheckPurposeId,
    string? PrecheckLocationStateCode,
    string? PrecheckPurposeCategory,
    string? PrecheckPlanKey,
    string? PrecheckPayerDisplayName,
    string? PrecheckProductDisplayName,
    string? PrecheckPracticeNetworkStatus,
    Guid? DetailsId,
    string? DetailsResultingStatus,
    Guid? DetailsIdentityReviewDecisionId,
    Guid? DetailsSafetyEvaluationId,
    Guid? DetailsPurposeId,
    Guid? DetailsPrecheckId,
    string? DetailsLocationStateCode,
    string? DetailsPurposeCategory,
    string? DetailsPlanKey,
    string? DetailsPayerDisplayName,
    string? DetailsProductDisplayName,
    string? DetailsPracticeNetworkStatus,
    string? MemberIdLast4,
    bool GroupNumberPresent,
    string? GroupNumberLast4,
    string? SubscriberRelationship,
    string? CoveragePriority,
    string? ProtectedPayload,
    string? ProtectionScheme,
    string? ProtectionPurpose,
    int? ProtectionVersion,
    bool DetailsConfirmed,
    bool SyntheticDataConfirmed);

public sealed class TelehealthProspectiveEligibilityRepository(NpgsqlDataSource dataSource)
{
    internal async Task<TelehealthProspectiveEligibilityRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        int expectedVersion,
        string idempotencyKey,
        string commandFingerprint,
        Func<TelehealthProspectiveEligibilityCandidate, CancellationToken,
            ValueTask<TelehealthProspectiveEligibilityAdapterResult>> resolveEligibility,
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
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the synthetic applicant before retrying.");
        }

        var candidate = new TelehealthProspectiveEligibilityCandidate(
            context.IdentityReviewDecisionId!.Value,
            context.SafetyEvaluationId!.Value,
            context.PurposeId!.Value,
            context.PrecheckId!.Value,
            context.DetailsId!.Value,
            context.CurrentLocationStateCode!,
            context.PurposeCategory!,
            context.PrecheckPlanKey!,
            context.PrecheckPayerDisplayName!,
            context.PrecheckProductDisplayName!,
            context.PrecheckPracticeNetworkStatus!,
            context.MemberIdLast4!,
            context.GroupNumberPresent,
            context.GroupNumberLast4,
            context.SubscriberRelationship!,
            context.CoveragePriority!,
            context.ProtectedPayload!,
            context.DatabaseNow);
        var adapterResult = await resolveEligibility(candidate, cancellationToken);
        const string nextStatus = "SyntheticEligibilityRecorded";
        var nextVersion = context.Version + 1;
        var dateOfService = DateOnly.FromDateTime(context.DatabaseNow.UtcDateTime);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='MemberInsuranceDetailsRecorded';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the synthetic applicant before retrying.");
            }
        }

        var eligibilityResultId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_eligibility_results(
                  eligibility_result_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  visit_purpose_id,practice_network_precheck_id,
                  member_insurance_details_id,resulting_applicant_version,
                  resulting_applicant_status,location_state_code,purpose_category,
                  plan_key,payer_display_name,product_display_name,
                  practice_network_status,member_id_last4,group_number_present,
                  group_number_last4,subscriber_relationship,coverage_priority,
                  date_of_service,service_category,adapter_mode,
                  compatibility_target,dataset_key,dataset_version,
                  dataset_effective_from,dataset_effective_through,
                  inquiry_trace_token,response_trace_token,transport_outcome,
                  member_match_status,eligibility_status,benefit_information_status,
                  business_outcome,member_matched,member_eligibility_checked,
                  member_benefits_checked,checked_at,expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @resultId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyEvaluationId,@purposeId,@precheckId,
                  @detailsId,@nextVersion,@nextStatus,@locationStateCode,
                  @purposeCategory,@planKey,@payerDisplayName,@productDisplayName,
                  @practiceNetworkStatus,@memberLast4,@groupPresent,@groupLast4,
                  @subscriberRelationship,@coveragePriority,@dateOfService,
                  @serviceCategory,@adapterMode,@compatibilityTarget,@datasetKey,
                  @datasetVersion,@datasetEffectiveFrom,@datasetEffectiveThrough,
                  @inquiryTraceToken,@responseTraceToken,@transportOutcome,
                  @memberMatchStatus,@eligibilityStatus,@benefitInformationStatus,
                  @businessOutcome,@memberMatched,@memberEligibilityChecked,
                  @memberBenefitsChecked,@checkedAt,@expiresAt,
                  @idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("resultId", eligibilityResultId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", candidate.IdentityReviewDecisionId);
            insert.Parameters.AddWithValue("safetyEvaluationId", candidate.SafetyEvaluationId);
            insert.Parameters.AddWithValue("purposeId", candidate.PurposeId);
            insert.Parameters.AddWithValue("precheckId", candidate.PrecheckId);
            insert.Parameters.AddWithValue("detailsId", candidate.DetailsId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationStateCode", candidate.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("purposeCategory", candidate.PurposeCategory);
            insert.Parameters.AddWithValue("planKey", candidate.PlanKey);
            insert.Parameters.AddWithValue("payerDisplayName", candidate.PayerDisplayName);
            insert.Parameters.AddWithValue("productDisplayName", candidate.ProductDisplayName);
            insert.Parameters.AddWithValue("practiceNetworkStatus", candidate.PracticeNetworkStatus);
            insert.Parameters.AddWithValue("memberLast4", candidate.MemberIdLast4);
            insert.Parameters.AddWithValue("groupPresent", candidate.GroupNumberPresent);
            insert.Parameters.AddWithValue("groupLast4", (object?)candidate.GroupNumberLast4 ?? DBNull.Value);
            insert.Parameters.AddWithValue("subscriberRelationship", candidate.SubscriberRelationship);
            insert.Parameters.AddWithValue("coveragePriority", candidate.CoveragePriority);
            insert.Parameters.AddWithValue("dateOfService", dateOfService);
            insert.Parameters.AddWithValue("serviceCategory", SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory);
            insert.Parameters.AddWithValue("adapterMode", adapterResult.AdapterMode);
            insert.Parameters.AddWithValue("compatibilityTarget", adapterResult.CompatibilityTarget);
            insert.Parameters.AddWithValue("datasetKey", adapterResult.DatasetKey);
            insert.Parameters.AddWithValue("datasetVersion", adapterResult.DatasetVersion);
            insert.Parameters.AddWithValue("datasetEffectiveFrom", adapterResult.DatasetEffectiveFrom);
            insert.Parameters.AddWithValue("datasetEffectiveThrough", adapterResult.DatasetEffectiveThrough);
            insert.Parameters.AddWithValue("inquiryTraceToken", adapterResult.InquiryTraceToken);
            insert.Parameters.AddWithValue("responseTraceToken", adapterResult.ResponseTraceToken);
            insert.Parameters.AddWithValue("transportOutcome", adapterResult.TransportOutcome);
            insert.Parameters.AddWithValue("memberMatchStatus", adapterResult.MemberMatchStatus);
            insert.Parameters.AddWithValue("eligibilityStatus", adapterResult.EligibilityStatus);
            insert.Parameters.AddWithValue("benefitInformationStatus", adapterResult.BenefitInformationStatus);
            insert.Parameters.AddWithValue("businessOutcome", adapterResult.BusinessOutcome);
            insert.Parameters.AddWithValue("memberMatched", adapterResult.MemberMatched);
            insert.Parameters.AddWithValue("memberEligibilityChecked", adapterResult.MemberEligibilityChecked);
            insert.Parameters.AddWithValue("memberBenefitsChecked", adapterResult.MemberBenefitsChecked);
            insert.Parameters.AddWithValue("checkedAt", adapterResult.CheckedAt);
            insert.Parameters.AddWithValue("expiresAt", adapterResult.ExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Prospective eligibility result time is unavailable.");
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
                       'prospective-synthetic-eligibility-recorded',
                       'MemberInsuranceDetailsRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "prospective-eligibility:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            eligibilityResultId,
            applicantId,
            nextVersion,
            nextStatus,
            candidate.CurrentLocationStateCode,
            candidate.PurposeCategory,
            candidate.PlanKey,
            candidate.PayerDisplayName,
            candidate.ProductDisplayName,
            candidate.PracticeNetworkStatus,
            candidate.MemberIdLast4,
            candidate.GroupNumberLast4,
            candidate.SubscriberRelationship,
            candidate.CoveragePriority,
            dateOfService,
            SyntheticTelehealthProspectiveEligibilityGateway.ServiceCategory,
            adapterResult,
            recordedAt);
    }

    private static async Task<TelehealthProspectiveEligibilityContext?> LoadContextAsync(
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
                   d.decision_id,d.decision,d.identity_proofed,d.canonical_patient_created,
                   s.evaluation_id,s.outcome,s.resulting_applicant_status,
                   s.current_location_state_code,
                   p.purpose_id,p.purpose_category,p.resulting_applicant_status,
                   p.safety_triage_evaluation_id,p.identity_review_decision_id,
                   n.precheck_id,n.resulting_applicant_status,
                   n.identity_review_decision_id,n.safety_triage_evaluation_id,
                   n.visit_purpose_id,n.location_state_code,n.purpose_category,
                   n.plan_key,n.payer_display_name,n.product_display_name,
                   n.practice_network_status,
                   m.details_id,m.resulting_applicant_status,
                   m.identity_review_decision_id,m.safety_triage_evaluation_id,
                   m.visit_purpose_id,m.practice_network_precheck_id,
                   m.location_state_code,m.purpose_category,m.plan_key,
                   m.payer_display_name,m.product_display_name,
                   m.practice_network_status,m.member_id_last4,
                   m.group_number_present,m.group_number_last4,
                   m.subscriber_relationship,m.coverage_priority,m.protected_payload,
                   m.protection_scheme,m.protection_purpose,m.protection_version,
                   m.details_confirmed,m.synthetic_data_confirmed
            from telehealth_prospective_applicants a
            left join telehealth_applicant_identity_review_decisions d
              on d.applicant_id=a.applicant_id
            left join telehealth_applicant_safety_triage_evaluations s
              on s.applicant_id=a.applicant_id
            left join telehealth_applicant_visit_purposes p
              on p.applicant_id=a.applicant_id
            left join telehealth_applicant_practice_network_prechecks n
              on n.applicant_id=a.applicant_id
            left join telehealth_applicant_member_insurance_details m
              on m.applicant_id=a.applicant_id
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
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            !reader.IsDBNull(9) && reader.GetBoolean(9),
            !reader.IsDBNull(10) && reader.GetBoolean(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.IsDBNull(19) ? null : reader.GetGuid(19),
            reader.IsDBNull(20) ? null : reader.GetGuid(20),
            reader.IsDBNull(21) ? null : reader.GetString(21),
            reader.IsDBNull(22) ? null : reader.GetGuid(22),
            reader.IsDBNull(23) ? null : reader.GetGuid(23),
            reader.IsDBNull(24) ? null : reader.GetGuid(24),
            reader.IsDBNull(25) ? null : reader.GetString(25),
            reader.IsDBNull(26) ? null : reader.GetString(26),
            reader.IsDBNull(27) ? null : reader.GetString(27),
            reader.IsDBNull(28) ? null : reader.GetString(28),
            reader.IsDBNull(29) ? null : reader.GetString(29),
            reader.IsDBNull(30) ? null : reader.GetString(30),
            reader.IsDBNull(31) ? null : reader.GetGuid(31),
            reader.IsDBNull(32) ? null : reader.GetString(32),
            reader.IsDBNull(33) ? null : reader.GetGuid(33),
            reader.IsDBNull(34) ? null : reader.GetGuid(34),
            reader.IsDBNull(35) ? null : reader.GetGuid(35),
            reader.IsDBNull(36) ? null : reader.GetGuid(36),
            reader.IsDBNull(37) ? null : reader.GetString(37),
            reader.IsDBNull(38) ? null : reader.GetString(38),
            reader.IsDBNull(39) ? null : reader.GetString(39),
            reader.IsDBNull(40) ? null : reader.GetString(40),
            reader.IsDBNull(41) ? null : reader.GetString(41),
            reader.IsDBNull(42) ? null : reader.GetString(42),
            reader.IsDBNull(43) ? null : reader.GetString(43),
            !reader.IsDBNull(44) && reader.GetBoolean(44),
            reader.IsDBNull(45) ? null : reader.GetString(45),
            reader.IsDBNull(46) ? null : reader.GetString(46),
            reader.IsDBNull(47) ? null : reader.GetString(47),
            reader.IsDBNull(48) ? null : reader.GetString(48),
            reader.IsDBNull(49) ? null : reader.GetString(49),
            reader.IsDBNull(50) ? null : reader.GetString(50),
            reader.IsDBNull(51) ? null : reader.GetInt32(51),
            !reader.IsDBNull(52) && reader.GetBoolean(52),
            !reader.IsDBNull(53) && reader.GetBoolean(53));
    }

    private static async Task<(TelehealthProspectiveEligibilityRecord Record, string CommandFingerprint)?>
        LoadByIdempotencyAsync(
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
            select eligibility_result_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,location_state_code,purpose_category,
                   plan_key,payer_display_name,product_display_name,
                   practice_network_status,member_id_last4,group_number_last4,
                   subscriber_relationship,coverage_priority,date_of_service,
                   service_category,adapter_mode,compatibility_target,dataset_key,
                   dataset_version,dataset_effective_from,dataset_effective_through,
                   inquiry_trace_token,response_trace_token,transport_outcome,
                   member_match_status,eligibility_status,benefit_information_status,
                   business_outcome,member_matched,member_eligibility_checked,
                   member_benefits_checked,checked_at,expires_at,recorded_at,
                   command_fingerprint
            from telehealth_applicant_eligibility_results
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

        var adapterResult = new TelehealthProspectiveEligibilityAdapterResult(
            reader.GetString(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetInt32(19),
            reader.GetFieldValue<DateTimeOffset>(20),
            reader.GetFieldValue<DateTimeOffset>(21),
            reader.GetGuid(22),
            reader.GetGuid(23),
            reader.GetString(24),
            reader.GetString(25),
            reader.GetString(26),
            reader.GetString(27),
            reader.GetString(28),
            reader.GetBoolean(29),
            reader.GetBoolean(30),
            reader.GetBoolean(31),
            reader.GetFieldValue<DateTimeOffset>(32),
            reader.GetFieldValue<DateTimeOffset>(33));
        return (new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                Convert.ToInt32(reader.GetInt64(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetFieldValue<DateOnly>(14),
                reader.GetString(15),
                adapterResult,
                reader.GetFieldValue<DateTimeOffset>(34)),
            reader.GetString(35));
    }

    private static void RequireEligibleContext(TelehealthProspectiveEligibilityContext context)
    {
        if (context.ExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before the eligibility check. Start again.");
        }
        if (context.Status != "MemberInsuranceDetailsRecorded"
            || context.DuplicateDisposition != "NoCandidate"
            || context.ContactVerifiedAt is null
            || context.IdentityReviewDecisionId is null
            || context.IdentityReviewDecision != "ApprovedForProspectiveIntake"
            || context.IdentityProofed
            || context.CanonicalPatientCreated
            || context.SafetyEvaluationId is null
            || context.SafetyOutcome != TelehealthTriageOutcome.TelehealthEligible.ToString()
            || context.SafetyResultingStatus != "SafetyScreenPassed"
            || context.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || context.PurposeId is null
            || context.PurposeCategory is not ("migraine" or "sleep")
            || context.PurposeResultingStatus != "VisitPurposeRecorded"
            || context.PurposeSafetyEvaluationId != context.SafetyEvaluationId
            || context.PurposeIdentityReviewDecisionId != context.IdentityReviewDecisionId
            || context.PrecheckId is null
            || context.PrecheckResultingStatus != "PracticeNetworkPrecheckRecorded"
            || context.PrecheckIdentityReviewDecisionId != context.IdentityReviewDecisionId
            || context.PrecheckSafetyEvaluationId != context.SafetyEvaluationId
            || context.PrecheckPurposeId != context.PurposeId
            || context.PrecheckLocationStateCode != context.CurrentLocationStateCode
            || context.PrecheckPurposeCategory != context.PurposeCategory
            || context.PrecheckPlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || context.PrecheckPayerDisplayName is null
            || context.PrecheckProductDisplayName is null
            || context.PrecheckPracticeNetworkStatus is not ("PracticeNetworkConfirmedFixture" or "NetworkUnknown" or "PracticeOutOfNetworkFixture")
            || context.DetailsId is null
            || context.DetailsResultingStatus != "MemberInsuranceDetailsRecorded"
            || context.DetailsIdentityReviewDecisionId != context.IdentityReviewDecisionId
            || context.DetailsSafetyEvaluationId != context.SafetyEvaluationId
            || context.DetailsPurposeId != context.PurposeId
            || context.DetailsPrecheckId != context.PrecheckId
            || context.DetailsLocationStateCode != context.CurrentLocationStateCode
            || context.DetailsPurposeCategory != context.PurposeCategory
            || context.DetailsPlanKey != context.PrecheckPlanKey
            || context.DetailsPayerDisplayName != context.PrecheckPayerDisplayName
            || context.DetailsProductDisplayName != context.PrecheckProductDisplayName
            || context.DetailsPracticeNetworkStatus != context.PrecheckPracticeNetworkStatus
            || context.MemberIdLast4 is null
            || (context.GroupNumberPresent != (context.GroupNumberLast4 is not null))
            || context.SubscriberRelationship is not ("Self" or "Spouse" or "Parent" or "Other")
            || context.CoveragePriority != TelehealthProspectiveMemberInsuranceDetailsPolicy.CoveragePriority
            || string.IsNullOrEmpty(context.ProtectedPayload)
            || context.ProtectionScheme != TelehealthProspectiveMemberInsuranceDetailsProtector.Scheme
            || context.ProtectionPurpose != TelehealthProspectiveMemberInsuranceDetailsProtector.Purpose
            || context.ProtectionVersion != TelehealthProspectiveMemberInsuranceDetailsProtector.Version
            || !context.DetailsConfirmed
            || !context.SyntheticDataConfirmed)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_eligibility_state_conflict",
                "The applicant is not eligible for this bounded synthetic eligibility check.");
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
                "telehealth_applicant_eligibility_idempotency_conflict",
                "The eligibility idempotency key was already used with different command content.");
        }
    }
}
