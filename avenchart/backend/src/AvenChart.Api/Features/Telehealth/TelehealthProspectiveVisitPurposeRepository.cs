// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveVisitPurposeRecord(
    Guid PurposeId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string PurposeCategory,
    string PurposeDisplayLabel,
    DateTimeOffset RecordedAt);

internal sealed record TelehealthProspectiveVisitPurposeApplicant(
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
    string? SafetyProtocolKey,
    int? SafetyProtocolVersion);

public sealed class TelehealthProspectiveVisitPurposeRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProspectiveVisitPurposeRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthProspectiveVisitPurpose purpose,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var applicant = await LoadApplicantForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, cancellationToken)
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

        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired before the visit purpose was recorded. Start again.");
        }
        if (applicant.Status != "SafetyScreenPassed"
            || applicant.DuplicateDisposition != "NoCandidate"
            || applicant.ContactVerifiedAt is null
            || applicant.IdentityReviewDecisionId is null
            || applicant.IdentityReviewDecision != "ApprovedForProspectiveIntake"
            || applicant.IdentityProofed
            || applicant.CanonicalPatientCreated
            || applicant.SafetyEvaluationId is null
            || applicant.SafetyOutcome != TelehealthTriageOutcome.TelehealthEligible.ToString()
            || applicant.SafetyResultingStatus != "SafetyScreenPassed"
            || applicant.SafetyProtocolKey != "synthetic-universal-safety"
            || applicant.SafetyProtocolVersion != 1)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_visit_purpose_state_conflict",
                "The applicant is not eligible for this bounded visit-purpose classification.");
        }
        if (applicant.Version != purpose.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Reload the synthetic applicant before retrying.");
        }

        const string nextStatus = "VisitPurposeRecorded";
        var nextVersion = applicant.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@nextStatus,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SafetyScreenPassed';
                """;
            update.Parameters.AddWithValue("nextStatus", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", purpose.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Reload the synthetic applicant before retrying.");
            }
        }

        var purposeId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_visit_purposes(
                  purpose_id,applicant_id,practice_id,facility_id,
                  identity_review_decision_id,safety_triage_evaluation_id,
                  resulting_applicant_version,resulting_applicant_status,
                  purpose_category,purpose_display_label,source_safety_outcome,
                  source_safety_protocol_key,source_safety_protocol_version,
                  idempotency_key,command_fingerprint)
                values(
                  @purposeId,@applicantId,@practiceId,@facilityId,
                  @identityDecisionId,@safetyEvaluationId,@nextVersion,@nextStatus,
                  @purposeCategory,@purposeDisplayLabel,@safetyOutcome,
                  @safetyProtocolKey,@safetyProtocolVersion,
                  @idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("purposeId", purposeId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("identityDecisionId", applicant.IdentityReviewDecisionId.Value);
            insert.Parameters.AddWithValue("safetyEvaluationId", applicant.SafetyEvaluationId.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", nextStatus);
            insert.Parameters.AddWithValue("purposeCategory", purpose.PurposeCategory);
            insert.Parameters.AddWithValue("purposeDisplayLabel", purpose.PurposeDisplayLabel);
            insert.Parameters.AddWithValue("safetyOutcome", applicant.SafetyOutcome);
            insert.Parameters.AddWithValue("safetyProtocolKey", applicant.SafetyProtocolKey);
            insert.Parameters.AddWithValue("safetyProtocolVersion", applicant.SafetyProtocolVersion.Value);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Prospective visit-purpose record time is unavailable.");
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
                       'prospective-visit-purpose-recorded',
                       'SafetyScreenPassed',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "visit-purpose:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TelehealthProspectiveVisitPurposeRecord(
            purposeId,
            applicantId,
            nextVersion,
            nextStatus,
            purpose.PurposeCategory,
            purpose.PurposeDisplayLabel,
            recordedAt);
    }

    private static async Task<TelehealthProspectiveVisitPurposeApplicant?> LoadApplicantForUpdateAsync(
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
                   s.protocol_key,s.protocol_version
            from telehealth_prospective_applicants a
            left join telehealth_applicant_identity_review_decisions d
              on d.applicant_id=a.applicant_id
            left join telehealth_applicant_safety_triage_evaluations s
              on s.applicant_id=a.applicant_id
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
        return new TelehealthProspectiveVisitPurposeApplicant(
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
            reader.IsDBNull(15) ? null : reader.GetInt32(15));
    }

    private static async Task<(TelehealthProspectiveVisitPurposeRecord Record, string CommandFingerprint)?> LoadByIdempotencyAsync(
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
            select purpose_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,purpose_category,purpose_display_label,
                   recorded_at,command_fingerprint
            from telehealth_applicant_visit_purposes
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
        return (new TelehealthProspectiveVisitPurposeRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6)),
            reader.GetString(7));
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
                "telehealth_applicant_visit_purpose_idempotency_conflict",
                "The visit-purpose idempotency key was already used with different command content.");
        }
    }
}
