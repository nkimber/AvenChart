// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectivePracticeNetworkOptionsRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    SyntheticTelehealthProspectivePracticeNetworkCatalogSnapshot Catalog);

public sealed record TelehealthProspectivePracticeNetworkPrecheckRecord(
    Guid PrecheckId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CurrentLocationStateCode,
    string PurposeCategory,
    SyntheticTelehealthProspectivePracticeNetworkPlan Plan,
    string AdapterMode,
    string CatalogKey,
    int CatalogVersion,
    DateTimeOffset CatalogEffectiveFrom,
    DateTimeOffset CatalogEffectiveThrough,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectivePracticeNetworkApplicant(
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
    string? SafetyProtocolKey,
    int? SafetyProtocolVersion,
    Guid? PurposeId,
    string? PurposeCategory,
    string? PurposeResultingStatus,
    Guid? PurposeSafetyEvaluationId,
    Guid? PurposeIdentityReviewDecisionId);

public sealed class TelehealthProspectivePracticeNetworkPrecheckRepository(
    NpgsqlDataSource dataSource,
    SyntheticTelehealthProspectivePracticeNetworkCatalog catalog)
{
    public async Task<TelehealthProspectivePracticeNetworkOptionsRecord> GetOptionsAsync(
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
        RequireAccess(applicant.AccessKeyHash, accessKeyHash);
        RequireEligibleContext(applicant);
        return new(
            applicantId,
            applicant.Version,
            applicant.Status,
            catalog.GetCurrent(applicant.DatabaseNow));
    }

    public async Task<TelehealthProspectivePracticeNetworkPrecheckRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthProspectivePracticeNetworkPrecheck precheck,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var applicant = await LoadApplicantAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant.AccessKeyHash, accessKeyHash);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligibleContext(applicant);
        if (applicant.Version != precheck.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the synthetic applicant before retrying.");
        }

        var plan = catalog.Resolve(precheck.PlanKey, applicant.DatabaseNow);
        var snapshot = catalog.GetCurrent(applicant.DatabaseNow);
        const string nextStatus = "PracticeNetworkPrecheckRecorded";
        var nextVersion = applicant.Version + 1;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='VisitPurposeRecorded';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", precheck.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the synthetic applicant before retrying.");
            }
        }

        var precheckId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_practice_network_prechecks(
                  precheck_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  visit_purpose_id,resulting_applicant_version,
                  resulting_applicant_status,location_state_code,purpose_category,
                  plan_key,payer_display_name,product_display_name,
                  practice_network_status,adapter_mode,catalog_key,catalog_version,
                  catalog_effective_from,catalog_effective_through,
                  idempotency_key,command_fingerprint)
                values(
                  @precheckId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyEvaluationId,@purposeId,@nextVersion,
                  @nextStatus,@locationStateCode,@purposeCategory,
                  @planKey,@payerDisplayName,@productDisplayName,
                  @practiceNetworkStatus,@adapterMode,@catalogKey,@catalogVersion,
                  @catalogEffectiveFrom,@catalogEffectiveThrough,
                  @idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("precheckId", precheckId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", applicant.IdentityReviewDecisionId!.Value);
            insert.Parameters.AddWithValue("safetyEvaluationId", applicant.SafetyEvaluationId!.Value);
            insert.Parameters.AddWithValue("purposeId", applicant.PurposeId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationStateCode", applicant.CurrentLocationStateCode!);
            insert.Parameters.AddWithValue("purposeCategory", applicant.PurposeCategory!);
            insert.Parameters.AddWithValue("planKey", plan.PlanKey);
            insert.Parameters.AddWithValue("payerDisplayName", plan.PayerDisplayName);
            insert.Parameters.AddWithValue("productDisplayName", plan.ProductDisplayName);
            insert.Parameters.AddWithValue("practiceNetworkStatus", plan.PracticeNetworkStatus);
            insert.Parameters.AddWithValue("adapterMode", snapshot.AdapterMode);
            insert.Parameters.AddWithValue("catalogKey", snapshot.CatalogKey);
            insert.Parameters.AddWithValue("catalogVersion", snapshot.CatalogVersion);
            insert.Parameters.AddWithValue("catalogEffectiveFrom", snapshot.EffectiveFrom);
            insert.Parameters.AddWithValue("catalogEffectiveThrough", snapshot.EffectiveThrough);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Prospective practice-network precheck time is unavailable.");
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
                       'prospective-practice-network-precheck-recorded',
                       'VisitPurposeRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "practice-network-precheck:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            precheckId,
            applicantId,
            nextVersion,
            nextStatus,
            applicant.CurrentLocationStateCode!,
            applicant.PurposeCategory!,
            plan,
            snapshot.AdapterMode,
            snapshot.CatalogKey,
            snapshot.CatalogVersion,
            snapshot.EffectiveFrom,
            snapshot.EffectiveThrough,
            recordedAt);
    }

    private static async Task<TelehealthProspectivePracticeNetworkApplicant?> LoadApplicantAsync(
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
        command.CommandText = """
            select a.version,a.status,a.access_key_hash,a.duplicate_disposition,
                   a.contact_verified_at,a.expires_at,now(),
                   d.decision_id,d.decision,d.identity_proofed,d.canonical_patient_created,
                   s.evaluation_id,s.outcome,s.resulting_applicant_status,
                   s.current_location_state_code,s.protocol_key,s.protocol_version,
                   p.purpose_id,p.purpose_category,p.resulting_applicant_status,
                   p.safety_triage_evaluation_id,p.identity_review_decision_id
            from telehealth_prospective_applicants a
            left join telehealth_applicant_identity_review_decisions d
              on d.applicant_id=a.applicant_id
            left join telehealth_applicant_safety_triage_evaluations s
              on s.applicant_id=a.applicant_id
            left join telehealth_applicant_visit_purposes p
              on p.applicant_id=a.applicant_id
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            """ + (forUpdate ? " for update of a;" : ";");
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
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetInt32(16),
            reader.IsDBNull(17) ? null : reader.GetGuid(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetGuid(20),
            reader.IsDBNull(21) ? null : reader.GetGuid(21));
    }

    private static async Task<(TelehealthProspectivePracticeNetworkPrecheckRecord Record, string CommandFingerprint)?> LoadByIdempotencyAsync(
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
            select precheck_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,location_state_code,purpose_category,
                   plan_key,payer_display_name,product_display_name,
                   practice_network_status,adapter_mode,catalog_key,catalog_version,
                   catalog_effective_from,catalog_effective_through,recorded_at,
                   command_fingerprint
            from telehealth_applicant_practice_network_prechecks
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

        var plan = new SyntheticTelehealthProspectivePracticeNetworkPlan(
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            MeaningFor(reader.GetString(9)));
        return (new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            plan,
            reader.GetString(10),
            reader.GetString(11),
            reader.GetInt32(12),
            reader.GetFieldValue<DateTimeOffset>(13),
            reader.GetFieldValue<DateTimeOffset>(14),
            reader.GetFieldValue<DateTimeOffset>(15)),
            reader.GetString(16));
    }

    private static void RequireEligibleContext(TelehealthProspectivePracticeNetworkApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before the practice-network precheck. Start again.");
        }
        if (applicant.Status != "VisitPurposeRecorded"
            || applicant.DuplicateDisposition != "NoCandidate"
            || applicant.ContactVerifiedAt is null
            || applicant.IdentityReviewDecisionId is null
            || applicant.IdentityReviewDecision != "ApprovedForProspectiveIntake"
            || applicant.IdentityProofed
            || applicant.CanonicalPatientCreated
            || applicant.SafetyEvaluationId is null
            || applicant.SafetyOutcome != TelehealthTriageOutcome.TelehealthEligible.ToString()
            || applicant.SafetyResultingStatus != "SafetyScreenPassed"
            || applicant.CurrentLocationStateCode is not ("GA" or "CA" or "FL")
            || applicant.SafetyProtocolKey != "synthetic-universal-safety"
            || applicant.SafetyProtocolVersion != 1
            || applicant.PurposeId is null
            || applicant.PurposeCategory is not ("migraine" or "sleep")
            || applicant.PurposeResultingStatus != "VisitPurposeRecorded"
            || applicant.PurposeSafetyEvaluationId != applicant.SafetyEvaluationId
            || applicant.PurposeIdentityReviewDecisionId != applicant.IdentityReviewDecisionId)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_network_precheck_state_conflict",
                "The applicant is not eligible for this bounded practice-network precheck.");
        }
    }

    private static void RequireAccess(string existingHash, string suppliedHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existingHash, suppliedHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_practice_network_precheck_idempotency_conflict",
                "The practice-network precheck idempotency key was already used with different command content.");
        }
    }

    private static string MeaningFor(string status) => status switch
    {
        "PracticeNetworkConfirmedFixture" =>
            "The synthetic fixture says the practice participates for this plan, state, and visit category. It does not check the member or rendering physician.",
        "NetworkUnknown" =>
            "The synthetic fixture has no authoritative practice-plan participation result. Treat network status as unknown.",
        _ =>
            "The synthetic fixture says the practice does not participate for this plan. No self-pay choice or estimate is created."
    };
}
