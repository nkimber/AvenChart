// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveMemberInsuranceDetailsRecord(
    Guid DetailsId,
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
    string ProtectionScheme,
    int ProtectionVersion,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectiveMemberInsuranceDetailsContext(
    int Version,
    string Status,
    string AccessKeyHash,
    string? DuplicateDisposition,
    DateTimeOffset? ContactVerifiedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow,
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
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
    string? PlanKey,
    string? PayerDisplayName,
    string? ProductDisplayName,
    string? PracticeNetworkStatus);

internal sealed record TelehealthProspectiveMemberInsuranceDetailsReplay(
    TelehealthProspectiveMemberInsuranceDetailsRecord Record,
    string ProtectedPayload,
    string CommandFingerprint);

public sealed class TelehealthProspectiveMemberInsuranceDetailsRepository(
    NpgsqlDataSource dataSource,
    TelehealthProspectiveMemberInsuranceDetailsProtector protector)
{
    public async Task<TelehealthProspectiveMemberInsuranceDetailsRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthProspectiveMemberInsuranceDetails details,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var payload = TelehealthProspectiveMemberInsuranceDetailsPolicy.ResolveSubscriber(
            details,
            context.LegalFirstName,
            context.LegalLastName,
            context.DateOfBirth);
        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.CommandFingerprint, commandFingerprint);
            if (!protector.Matches(replay.ProtectedPayload, payload))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_member_details_idempotency_conflict",
                    "The member-details idempotency key was already used with different command content.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Record;
        }

        RequireEligibleContext(context);
        if (context.Version != details.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the synthetic applicant before retrying.");
        }

        var protectedPayload = protector.Protect(payload);
        const string nextStatus = "MemberInsuranceDetailsRecorded";
        var nextVersion = context.Version + 1;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='PracticeNetworkPrecheckRecorded';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", details.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the synthetic applicant before retrying.");
            }
        }

        var detailsId = Guid.NewGuid();
        var memberLast4 = details.MemberId[^4..];
        var groupLast4 = details.GroupNumber is null ? null : details.GroupNumber[^4..];
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_member_insurance_details(
                  details_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  visit_purpose_id,practice_network_precheck_id,
                  resulting_applicant_version,resulting_applicant_status,
                  location_state_code,purpose_category,plan_key,
                  payer_display_name,product_display_name,practice_network_status,
                  subscriber_relationship,coverage_priority,member_id_last4,
                  group_number_present,group_number_last4,
                  details_confirmed,synthetic_data_confirmed,protected_payload,
                  protection_scheme,protection_purpose,protection_version,
                  idempotency_key,command_fingerprint)
                values(
                  @detailsId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyEvaluationId,@purposeId,@precheckId,
                  @nextVersion,@nextStatus,@locationStateCode,@purposeCategory,
                  @planKey,@payerDisplayName,@productDisplayName,@practiceNetworkStatus,
                  @subscriberRelationship,@coveragePriority,@memberLast4,
                  @groupPresent,@groupLast4,@detailsConfirmed,@syntheticConfirmed,
                  @protectedPayload,@protectionScheme,@protectionPurpose,
                  @protectionVersion,@idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("detailsId", detailsId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", context.IdentityReviewDecisionId!.Value);
            insert.Parameters.AddWithValue("safetyEvaluationId", context.SafetyEvaluationId!.Value);
            insert.Parameters.AddWithValue("purposeId", context.PurposeId!.Value);
            insert.Parameters.AddWithValue("precheckId", context.PrecheckId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationStateCode", context.CurrentLocationStateCode!);
            insert.Parameters.AddWithValue("purposeCategory", context.PurposeCategory!);
            insert.Parameters.AddWithValue("planKey", context.PlanKey!);
            insert.Parameters.AddWithValue("payerDisplayName", context.PayerDisplayName!);
            insert.Parameters.AddWithValue("productDisplayName", context.ProductDisplayName!);
            insert.Parameters.AddWithValue("practiceNetworkStatus", context.PracticeNetworkStatus!);
            insert.Parameters.AddWithValue("subscriberRelationship", details.SubscriberRelationship);
            insert.Parameters.AddWithValue("coveragePriority", TelehealthProspectiveMemberInsuranceDetailsPolicy.CoveragePriority);
            insert.Parameters.AddWithValue("memberLast4", memberLast4);
            insert.Parameters.AddWithValue("groupPresent", details.GroupNumber is not null);
            insert.Parameters.AddWithValue("groupLast4", (object?)groupLast4 ?? DBNull.Value);
            insert.Parameters.AddWithValue("detailsConfirmed", details.DetailsConfirmed);
            insert.Parameters.AddWithValue("syntheticConfirmed", details.SyntheticDataConfirmed);
            insert.Parameters.AddWithValue("protectedPayload", protectedPayload);
            insert.Parameters.AddWithValue("protectionScheme", TelehealthProspectiveMemberInsuranceDetailsProtector.Scheme);
            insert.Parameters.AddWithValue("protectionPurpose", TelehealthProspectiveMemberInsuranceDetailsProtector.Purpose);
            insert.Parameters.AddWithValue("protectionVersion", TelehealthProspectiveMemberInsuranceDetailsProtector.Version);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Prospective member-details receipt time is unavailable.");
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
                       'prospective-member-insurance-details-recorded',
                       'PracticeNetworkPrecheckRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "member-insurance-details:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            detailsId,
            applicantId,
            nextVersion,
            nextStatus,
            context.CurrentLocationStateCode!,
            context.PurposeCategory!,
            context.PlanKey!,
            context.PayerDisplayName!,
            context.ProductDisplayName!,
            context.PracticeNetworkStatus!,
            memberLast4,
            groupLast4,
            details.SubscriberRelationship,
            TelehealthProspectiveMemberInsuranceDetailsPolicy.CoveragePriority,
            TelehealthProspectiveMemberInsuranceDetailsProtector.Scheme,
            TelehealthProspectiveMemberInsuranceDetailsProtector.Version,
            recordedAt);
    }

    private static async Task<TelehealthProspectiveMemberInsuranceDetailsContext?> LoadContextAsync(
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
                   a.legal_first_name,a.legal_last_name,a.date_of_birth,
                   d.decision_id,d.decision,d.identity_proofed,d.canonical_patient_created,
                   s.evaluation_id,s.outcome,s.resulting_applicant_status,
                   s.current_location_state_code,
                   p.purpose_id,p.purpose_category,p.resulting_applicant_status,
                   p.safety_triage_evaluation_id,p.identity_review_decision_id,
                   n.precheck_id,n.resulting_applicant_status,
                   n.identity_review_decision_id,n.safety_triage_evaluation_id,
                   n.visit_purpose_id,n.location_state_code,n.purpose_category,
                   n.plan_key,n.payer_display_name,n.product_display_name,
                   n.practice_network_status
            from telehealth_prospective_applicants a
            left join telehealth_applicant_identity_review_decisions d
              on d.applicant_id=a.applicant_id
            left join telehealth_applicant_safety_triage_evaluations s
              on s.applicant_id=a.applicant_id
            left join telehealth_applicant_visit_purposes p
              on p.applicant_id=a.applicant_id
            left join telehealth_applicant_practice_network_prechecks n
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
            reader.GetString(7),
            reader.GetString(8),
            reader.GetFieldValue<DateOnly>(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            !reader.IsDBNull(12) && reader.GetBoolean(12),
            !reader.IsDBNull(13) && reader.GetBoolean(13),
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20),
            reader.IsDBNull(21) ? null : reader.GetGuid(21),
            reader.IsDBNull(22) ? null : reader.GetGuid(22),
            reader.IsDBNull(23) ? null : reader.GetGuid(23),
            reader.IsDBNull(24) ? null : reader.GetString(24),
            reader.IsDBNull(25) ? null : reader.GetGuid(25),
            reader.IsDBNull(26) ? null : reader.GetGuid(26),
            reader.IsDBNull(27) ? null : reader.GetGuid(27),
            reader.IsDBNull(28) ? null : reader.GetString(28),
            reader.IsDBNull(29) ? null : reader.GetString(29),
            reader.IsDBNull(30) ? null : reader.GetString(30),
            reader.IsDBNull(31) ? null : reader.GetString(31),
            reader.IsDBNull(32) ? null : reader.GetString(32),
            reader.IsDBNull(33) ? null : reader.GetString(33));
    }

    private static async Task<TelehealthProspectiveMemberInsuranceDetailsReplay?> LoadByIdempotencyAsync(
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
            select details_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,location_state_code,purpose_category,
                   plan_key,payer_display_name,product_display_name,
                   practice_network_status,member_id_last4,group_number_last4,
                   subscriber_relationship,coverage_priority,protection_scheme,
                   protection_version,recorded_at,protected_payload,command_fingerprint
            from telehealth_applicant_member_insurance_details
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

        return new(
            new(
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
                reader.GetString(14),
                reader.GetInt32(15),
                reader.GetFieldValue<DateTimeOffset>(16)),
            reader.GetString(17),
            reader.GetString(18));
    }

    private static void RequireEligibleContext(TelehealthProspectiveMemberInsuranceDetailsContext context)
    {
        if (context.ExpiresAt <= context.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before member-detail confirmation. Start again.");
        }
        if (context.Status != "PracticeNetworkPrecheckRecorded"
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
            || context.PlanKey is not ("harbor-mutual-hd" or "blue-valley-standard" or "pine-state-choice")
            || context.PayerDisplayName is null
            || context.ProductDisplayName is null
            || context.PracticeNetworkStatus is not ("PracticeNetworkConfirmedFixture" or "NetworkUnknown" or "PracticeOutOfNetworkFixture"))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_member_details_state_conflict",
                "The applicant is not eligible for this bounded member-details receipt.");
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
                "telehealth_applicant_member_details_idempotency_conflict",
                "The member-details idempotency key was already used with different command content.");
        }
    }
}
