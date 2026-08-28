// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveSafetyTriageRecord(
    Guid EvaluationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string CurrentLocationStateCode,
    string ProtocolKey,
    int ProtocolVersion,
    TelehealthTriageOutcome Outcome,
    DateTimeOffset EvaluatedAt);

internal sealed record TelehealthProspectiveSafetyTriageApplicant(
    Guid ApplicantId,
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
    bool CanonicalPatientCreated);

public sealed class TelehealthProspectiveSafetyTriageRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProspectiveSafetyTriageRecord> EvaluateAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthProspectiveSafetyTriage answers,
        TelehealthTriageResult result,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var applicant = await LoadApplicantForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);

        var replay = await LoadEvaluationByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before the safety screen was recorded. Start again.");
        }
        if (applicant.Status != "IdentityReviewApproved"
            || applicant.DuplicateDisposition != "NoCandidate"
            || applicant.ContactVerifiedAt is null
            || applicant.IdentityReviewDecisionId is null
            || applicant.IdentityReviewDecision != "ApprovedForProspectiveIntake"
            || applicant.IdentityProofed
            || applicant.CanonicalPatientCreated)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_safety_triage_state_conflict",
                "The applicant is not eligible for this bounded universal safety screen.");
        }
        if (applicant.Version != answers.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the synthetic applicant before retrying.");
        }

        var nextStatus = TelehealthProspectiveSafetyTriagePolicy.ResultingStatus(result.Outcome);
        var nextVersion = applicant.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='IdentityReviewApproved';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", answers.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the synthetic applicant before retrying.");
            }
        }

        var evaluationId = Guid.NewGuid();
        DateTimeOffset evaluatedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_safety_triage_evaluations(
                  evaluation_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,resulting_applicant_version,
                  resulting_applicant_status,current_location_state_code,
                  current_location_confirmed,has_emergency_warning,severe_or_worsening,
                  requires_hands_on_exam,unsure,protocol_id,protocol_key,protocol_version,
                  protocol_content_hash,answers_fingerprint,outcome,
                  idempotency_key,command_fingerprint)
                values(
                  @evaluationId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@nextVersion,@nextStatus,@locationState,
                  true,@emergency,@severe,@handsOn,@unsure,@protocolId,@protocolKey,
                  @protocolVersion,@protocolHash,@answersFingerprint,@outcome,
                  @idempotencyKey,@commandFingerprint)
                returning evaluated_at;
                """;
            insert.Parameters.AddWithValue("evaluationId", evaluationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", applicant.IdentityReviewDecisionId.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("locationState", answers.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("emergency", answers.HasEmergencyWarning);
            insert.Parameters.AddWithValue("severe", answers.SevereOrWorsening);
            insert.Parameters.AddWithValue("handsOn", answers.RequiresHandsOnExam);
            insert.Parameters.AddWithValue("unsure", answers.Unsure);
            insert.Parameters.AddWithValue("protocolId", result.ProtocolId);
            insert.Parameters.AddWithValue("protocolKey", result.ProtocolKey);
            insert.Parameters.AddWithValue("protocolVersion", result.ProtocolVersion);
            insert.Parameters.AddWithValue("protocolHash", result.ProtocolContentHash);
            insert.Parameters.AddWithValue("answersFingerprint", result.AnswerFingerprint);
            insert.Parameters.AddWithValue("outcome", result.Outcome.ToString());
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Prospective safety-triage evaluation time is unavailable.");
            }
            evaluatedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-safety-triage-evaluated',
                       'IdentityReviewApproved',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "safety-triage:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TelehealthProspectiveSafetyTriageRecord(
            evaluationId,
            applicantId,
            nextVersion,
            nextStatus,
            answers.CurrentLocationStateCode,
            result.ProtocolKey,
            result.ProtocolVersion,
            result.Outcome,
            evaluatedAt);
    }

    private static async Task<TelehealthProspectiveSafetyTriageApplicant?> LoadApplicantForUpdateAsync(
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
            select a.applicant_id,a.version,a.status,a.access_key_hash,
                   a.duplicate_disposition,a.contact_verified_at,a.expires_at,now(),
                   d.decision_id,d.decision,d.identity_proofed,d.canonical_patient_created
            from telehealth_prospective_applicants a
            left join telehealth_applicant_identity_review_decisions d
              on d.applicant_id=a.applicant_id
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
        return new TelehealthProspectiveSafetyTriageApplicant(
            reader.GetGuid(0),
            Convert.ToInt32(reader.GetInt64(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            !reader.IsDBNull(10) && reader.GetBoolean(10),
            !reader.IsDBNull(11) && reader.GetBoolean(11));
    }

    private static async Task<(TelehealthProspectiveSafetyTriageRecord Record, string CommandFingerprint)?> LoadEvaluationByIdempotencyAsync(
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
            select evaluation_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,current_location_state_code,
                   protocol_key,protocol_version,outcome,evaluated_at,command_fingerprint
            from telehealth_applicant_safety_triage_evaluations
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
        return (new TelehealthProspectiveSafetyTriageRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            Enum.Parse<TelehealthTriageOutcome>(reader.GetString(7), false),
            reader.GetFieldValue<DateTimeOffset>(8)),
            reader.GetString(9));
    }

    private static void RequireAccess(
        TelehealthProspectiveSafetyTriageApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(
                applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_safety_triage_idempotency_conflict",
                "The safety-triage idempotency key was already used with different command content.");
        }
    }
}
