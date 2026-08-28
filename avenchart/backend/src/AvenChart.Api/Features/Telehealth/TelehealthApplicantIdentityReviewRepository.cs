// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantIdentityReviewRecord(
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
    DateTimeOffset ContactVerifiedAt,
    string DuplicateDisposition,
    string DuplicateEvidenceFingerprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

public sealed record TelehealthApplicantIdentityDecisionRecord(
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
    bool IdentityProofed,
    bool CanonicalPatientCreated,
    bool ChartLinked,
    bool ProspectiveIntakeCompleted,
    bool RequestCreated,
    bool QueueEnabled);

public sealed class TelehealthApplicantIdentityReviewRepository(NpgsqlDataSource dataSource)
{
    public async Task<(IReadOnlyList<TelehealthApplicantIdentityReviewRecord> Applicants, DateTimeOffset DatabaseNow)> ListAsync(
        string practiceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select applicant_id,version,status,legal_first_name,legal_last_name,
                   date_of_birth,email,phone,residence_state_code,postal_code,
                   contact_verified_at,duplicate_disposition,
                   duplicate_evidence_fingerprint,created_at,expires_at,now()
            from telehealth_prospective_applicants
            where practice_id=@practiceId and facility_id=@facilityId
              and status='IdentityReviewPending'
            order by contact_verified_at,applicant_id
            limit 100;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        var applicants = new List<TelehealthApplicantIdentityReviewRecord>();
        var databaseNow = DateTimeOffset.UtcNow;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            databaseNow = reader.GetFieldValue<DateTimeOffset>(15);
            applicants.Add(ReadApplicant(reader));
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

    public async Task<TelehealthApplicantIdentityDecisionRecord> RecordAsync(
        string practiceId,
        int facilityId,
        int? staffId,
        string actorId,
        string actorRole,
        Guid applicantId,
        int expectedVersion,
        string decision,
        string reason,
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

        var applicant = await LoadApplicantForUpdateAsync(
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

        if (applicant.Status != "IdentityReviewPending")
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_review_state_conflict",
                "The applicant is not awaiting this bounded identity review.");
        }
        if (applicant.Version != expectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Refresh the identity-review queue before retrying.");
        }
        var allowedDecision = TelehealthApplicantIdentityReviewPolicy.AllowedDecision(
            applicant.DuplicateDisposition);
        if (!string.Equals(decision, allowedDecision, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_review_outcome_conflict",
                "The requested decision is not permitted by the server-held duplicate disposition.");
        }

        var nextStatus = TelehealthApplicantIdentityReviewPolicy.ResultingStatus(decision);
        var nextVersion = applicant.Version + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='IdentityReviewPending';
                """;
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_version_conflict",
                    "The applicant changed. Refresh the identity-review queue before retrying.");
            }
        }

        var decisionId = Guid.NewGuid();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_identity_review_decisions(
                  decision_id,applicant_id,practice_id,facility_id,
                  resulting_applicant_version,decision,reason,
                  contact_verified_at_snapshot,duplicate_disposition_snapshot,
                  duplicate_evidence_fingerprint_snapshot,policy_key,policy_version,
                  evidence_type,decided_by_staff_id,decided_by_actor_id,decided_by_role,
                  idempotency_key,command_fingerprint)
                values(
                  @decisionId,@applicantId,@practiceId,@facilityId,
                  @nextVersion,@decision,@reason,@contactVerifiedAt,
                  @duplicateDisposition,@duplicateFingerprint,
                  'SYNTHETIC_STAFF_IDENTITY_REVIEW',1,
                  'CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY',
                  @staffId,@actorId,@actorRole,@idempotencyKey,@commandFingerprint);
                """;
            insert.Parameters.AddWithValue("decisionId", decisionId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("decision", decision);
            insert.Parameters.AddWithValue("reason", reason);
            insert.Parameters.AddWithValue("contactVerifiedAt", applicant.ContactVerifiedAt);
            insert.Parameters.AddWithValue("duplicateDisposition", applicant.DuplicateDisposition);
            insert.Parameters.AddWithValue("duplicateFingerprint", applicant.DuplicateEvidenceFingerprint);
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
                values(@eventId,@applicantId,@nextVersion,'identity-review-recorded',
                       'IdentityReviewPending',@nextStatus,'administrator',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", nextStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "identity-review:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TelehealthApplicantIdentityDecisionRecord(
            decisionId, applicantId, nextVersion, nextStatus, decision, reason,
            TelehealthApplicantIdentityReviewPolicy.PolicyKey,
            TelehealthApplicantIdentityReviewPolicy.PolicyVersion,
            TelehealthApplicantIdentityReviewPolicy.EvidenceType,
            await LoadDecisionTimeAsync(connection, decisionId, cancellationToken),
            false, false, false, false, false, false);
    }

    private static TelehealthApplicantIdentityReviewRecord ReadApplicant(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5),
        reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10), reader.GetString(11), reader.GetString(12),
        reader.GetFieldValue<DateTimeOffset>(13), reader.GetFieldValue<DateTimeOffset>(14),
        reader.GetFieldValue<DateTimeOffset>(15));

    private static async Task<TelehealthApplicantIdentityReviewRecord?> LoadApplicantForUpdateAsync(
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
            select applicant_id,version,status,legal_first_name,legal_last_name,
                   date_of_birth,email,phone,residence_state_code,postal_code,
                   contact_verified_at,duplicate_disposition,
                   duplicate_evidence_fingerprint,created_at,expires_at,now()
            from telehealth_prospective_applicants
            where practice_id=@practiceId and facility_id=@facilityId
              and applicant_id=@applicantId
            for update;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadApplicant(reader) : null;
    }

    private static async Task<(TelehealthApplicantIdentityDecisionRecord Record, string CommandFingerprint)?> LoadDecisionByIdempotencyAsync(
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
                   case decision
                     when 'ApprovedForProspectiveIntake' then 'IdentityReviewApproved'
                     else 'ManualReviewRequired'
                   end as applicant_status,
                   decision,reason,policy_key,policy_version,evidence_type,decided_at,
                   identity_proofed,canonical_patient_created,chart_linked,
                   prospective_intake_completed,request_created,queue_enabled,
                   command_fingerprint
            from telehealth_applicant_identity_review_decisions
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
        return (new TelehealthApplicantIdentityDecisionRecord(
            reader.GetGuid(0), reader.GetGuid(1), Convert.ToInt32(reader.GetInt64(2)),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetInt32(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetBoolean(10), reader.GetBoolean(11), reader.GetBoolean(12),
            reader.GetBoolean(13), reader.GetBoolean(14), reader.GetBoolean(15)),
            reader.GetString(16));
    }

    private static async Task<DateTimeOffset> LoadDecisionTimeAsync(
        NpgsqlConnection connection,
        Guid decisionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select decided_at from telehealth_applicant_identity_review_decisions where decision_id=@decisionId;";
        command.Parameters.AddWithValue("decisionId", decisionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Identity-review decision time is unavailable.");
        }
        return reader.GetFieldValue<DateTimeOffset>(0);
    }

    private static void RequireReplayFingerprint(string existing, string commandFingerprint)
    {
        if (!string.Equals(existing, commandFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_identity_review_idempotency_conflict",
                "The identity-review idempotency key was already used with different command content.");
        }
    }
}
