// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthProspectiveApplicantRecord(
    Guid ApplicantId,
    string PracticeId,
    int FacilityId,
    string Status,
    int Version,
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    string ResidenceStateCode,
    string PostalCode,
    string AccessKeyHash,
    string? DuplicateDisposition,
    DateTimeOffset? ContactVerifiedAt,
    DateTimeOffset ExpiresAt,
    int MaximumAttempts,
    int AttemptCount,
    DateTimeOffset DatabaseNow);

public sealed class TelehealthProspectiveApplicantRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthProspectiveApplicantRecord> CreateAsync(
        string practiceId,
        int facilityId,
        NormalizedTelehealthProspectiveApplicant applicant,
        string accessKeyHash,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var applicantId = Guid.NewGuid();
        var created = false;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into telehealth_prospective_applicants(
                  applicant_id,practice_id,facility_id,status,version,
                  legal_first_name,legal_last_name,date_of_birth,email,phone,
                  residence_state_code,postal_code,access_key_hash,
                  create_idempotency_key,create_fingerprint,expires_at)
                values(
                  @applicantId,@practiceId,@facilityId,'ContactVerificationPending',1,
                  @firstName,@lastName,@dateOfBirth,@email,@phone,
                  @stateCode,@postalCode,@accessKeyHash,
                  @idempotencyKey,@fingerprint,now() + @lifetime)
                on conflict (practice_id,facility_id,create_idempotency_key) do nothing
                returning applicant_id;
                """;
            command.Parameters.AddWithValue("applicantId", applicantId);
            command.Parameters.AddWithValue("practiceId", practiceId);
            command.Parameters.AddWithValue("facilityId", facilityId);
            command.Parameters.AddWithValue("firstName", applicant.LegalFirstName);
            command.Parameters.AddWithValue("lastName", applicant.LegalLastName);
            command.Parameters.AddWithValue("dateOfBirth", applicant.DateOfBirth);
            command.Parameters.AddWithValue("email", applicant.Email);
            command.Parameters.AddWithValue("phone", applicant.Phone);
            command.Parameters.AddWithValue("stateCode", applicant.ResidenceStateCode);
            command.Parameters.AddWithValue("postalCode", applicant.PostalCode);
            command.Parameters.AddWithValue("accessKeyHash", accessKeyHash);
            command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            command.Parameters.AddWithValue("fingerprint", commandFingerprint);
            command.Parameters.AddWithValue("lifetime", TelehealthProspectiveApplicantPolicy.ApplicantLifetime);
            var inserted = await command.ExecuteScalarAsync(cancellationToken);
            created = inserted is Guid;
        }

        if (!created)
        {
            var existing = await LoadByCreateIdempotencyAsync(
                connection, transaction, practiceId, facilityId, idempotencyKey, cancellationToken);
            if (existing is null
                || !TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(existing.Value.AccessKeyHash, accessKeyHash)
                || !string.Equals(existing.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_idempotency_conflict",
                    "The create idempotency key is not reusable for this applicant command.");
            }
            applicantId = existing.Value.ApplicantId;
            await transaction.CommitAsync(cancellationToken);
            return await GetAuthorizedAsync(
                practiceId, facilityId, applicantId, accessKeyHash, cancellationToken);
        }

        var verifierHash = TelehealthProspectiveApplicantPolicy.VerificationHash(
            applicantId, TelehealthProspectiveApplicantPolicy.DemonstrationVerificationCode);
        await using (var challenge = connection.CreateCommand())
        {
            challenge.Transaction = transaction;
            challenge.CommandText = """
                insert into telehealth_applicant_contact_challenges(
                  challenge_id,applicant_id,channel,destination_fingerprint,
                  verifier_hash,maximum_attempts,expires_at)
                values(@challengeId,@applicantId,'email',@destinationFingerprint,
                       @verifierHash,@maximumAttempts,now() + @lifetime);
                """;
            challenge.Parameters.AddWithValue("challengeId", Guid.NewGuid());
            challenge.Parameters.AddWithValue("applicantId", applicantId);
            challenge.Parameters.AddWithValue(
                "destinationFingerprint", TelehealthProspectiveApplicantPolicy.Hash(applicant.Email));
            challenge.Parameters.AddWithValue("verifierHash", verifierHash);
            challenge.Parameters.AddWithValue(
                "maximumAttempts", TelehealthProspectiveApplicantPolicy.MaximumVerificationAttempts);
            challenge.Parameters.AddWithValue("lifetime", TelehealthProspectiveApplicantPolicy.ApplicantLifetime);
            await challenge.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(
            connection,
            transaction,
            applicantId,
            1,
            "applicant-created",
            null,
            "ContactVerificationPending",
            "applicant",
            idempotencyKey,
            commandFingerprint,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAuthorizedAsync(
            practiceId, facilityId, applicantId, accessKeyHash, cancellationToken);
    }

    public async Task<TelehealthProspectiveApplicantRecord> GetAuthorizedAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(current, accessKeyHash);
        current = await ExpireIfNeededAsync(connection, transaction, current, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return current;
    }

    public async Task<TelehealthProspectiveApplicantRecord> VerifyContactAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        int expectedVersion,
        string verificationCode,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(current, accessKeyHash);

        var priorAttempt = await LoadAttemptByIdempotencyAsync(
            connection, transaction, applicantId, idempotencyKey, cancellationToken);
        if (priorAttempt is not null)
        {
            if (!string.Equals(priorAttempt.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_idempotency_conflict",
                    "The verification idempotency key was already used with different command content.");
            }
            await transaction.CommitAsync(cancellationToken);
            if (priorAttempt.Value.Result == "Rejected")
            {
                throw TelehealthProblem.BadRequest(
                    "telehealth_applicant_verification_code_invalid",
                    "The synthetic verification code is not correct.");
            }
            if (priorAttempt.Value.Result == "Locked")
            {
                throw TelehealthProblem.Gone(
                    "telehealth_applicant_verification_locked",
                    "The maximum number of synthetic verification attempts was reached. Start again.");
            }
            return await GetAuthorizedAsync(
                practiceId, facilityId, applicantId, accessKeyHash, cancellationToken);
        }

        current = await ExpireIfNeededAsync(connection, transaction, current, cancellationToken);
        if (current.Status == "Expired")
        {
            await transaction.CommitAsync(cancellationToken);
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (current.Status == "VerificationLocked")
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_verification_locked",
                "The maximum number of synthetic verification attempts was reached. Start again.");
        }
        if (current.Status != "ContactVerificationPending")
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_state_conflict",
                "Contact verification is not available in the applicant's current state.");
        }
        if (current.Version != expectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Refresh the current applicant state before retrying.");
        }

        var challenge = await LoadChallengeAsync(
            connection, transaction, applicantId, cancellationToken)
            ?? throw TelehealthProblem.Conflict(
                "telehealth_applicant_challenge_missing",
                "The synthetic verification challenge is unavailable. Start again.");
        if (challenge.ExpiresAt <= current.DatabaseNow)
        {
            current = await ExpireAsync(connection, transaction, current, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic verification challenge expired. Start again.");
        }

        var suppliedHash = TelehealthProspectiveApplicantPolicy.VerificationHash(applicantId, verificationCode);
        var accepted = TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(
            challenge.VerifierHash, suppliedHash);
        // Re-read after the applicant row lock is held. The correlated count in
        // the initial SELECT can have been evaluated before a concurrent
        // verifier waited for and acquired that lock under READ COMMITTED.
        var attemptOrdinal = await CountAttemptsAsync(
            connection, transaction, applicantId, cancellationToken) + 1;
        if (!accepted)
        {
            var locked = attemptOrdinal >= challenge.MaximumAttempts;
            await InsertAttemptAsync(
                connection,
                transaction,
                applicantId,
                attemptOrdinal,
                locked ? "Locked" : "Rejected",
                idempotencyKey,
                commandFingerprint,
                cancellationToken);
            if (locked)
            {
                var nextVersion = current.Version + 1;
                await UpdateStatusAsync(
                    connection,
                    transaction,
                    applicantId,
                    current.Version,
                    "VerificationLocked",
                    nextVersion,
                    null,
                    null,
                    null,
                    cancellationToken);
                await InsertEventAsync(
                    connection,
                    transaction,
                    applicantId,
                    nextVersion,
                    "verification-locked",
                    current.Status,
                    "VerificationLocked",
                    "system",
                    idempotencyKey,
                    commandFingerprint,
                    cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            throw locked
                ? TelehealthProblem.Gone(
                    "telehealth_applicant_verification_locked",
                    "The maximum number of synthetic verification attempts was reached. Start again.")
                : TelehealthProblem.BadRequest(
                    "telehealth_applicant_verification_code_invalid",
                    "The synthetic verification code is not correct.");
        }

        var duplicate = await ClassifyDuplicateAsync(connection, transaction, current, cancellationToken);
        var duplicateDisposition = duplicate.PossibleMatch
            ? "PossibleMatchManualReview"
            : "NoCandidate";
        var duplicateFingerprint = TelehealthCommandFingerprint.Create(
            "prospective-duplicate-classification-v1",
            practiceId,
            facilityId,
            current.LegalFirstName.ToLowerInvariant(),
            current.LegalLastName.ToLowerInvariant(),
            current.DateOfBirth,
            current.Email,
            current.Phone,
            duplicate.DatasetId,
            duplicate.DatasetVersion,
            duplicate.PossibleMatch);
        await InsertAttemptAsync(
            connection,
            transaction,
            applicantId,
            attemptOrdinal,
            "Accepted",
            idempotencyKey,
            commandFingerprint,
            cancellationToken);
        var verifiedVersion = current.Version + 1;
        await UpdateStatusAsync(
            connection,
            transaction,
            applicantId,
            current.Version,
            "IdentityReviewPending",
            verifiedVersion,
            duplicateDisposition,
            duplicateFingerprint,
            current.DatabaseNow,
            cancellationToken);
        await InsertEventAsync(
            connection,
            transaction,
            applicantId,
            verifiedVersion,
            "contact-verified",
            current.Status,
            "IdentityReviewPending",
            "applicant",
            idempotencyKey,
            commandFingerprint,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAuthorizedAsync(
            practiceId, facilityId, applicantId, accessKeyHash, cancellationToken);
    }

    private static async Task<TelehealthProspectiveApplicantRecord?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select applicant.applicant_id,applicant.practice_id,applicant.facility_id,
                   applicant.status,applicant.version,applicant.legal_first_name,
                   applicant.legal_last_name,applicant.date_of_birth,applicant.email,
                   applicant.phone,applicant.residence_state_code,applicant.postal_code,
                   applicant.access_key_hash,applicant.duplicate_disposition,
                   applicant.contact_verified_at,applicant.expires_at,
                   challenge.maximum_attempts,
                   (select count(*) from telehealth_applicant_verification_attempts attempt
                    where attempt.applicant_id=applicant.applicant_id) as attempt_count,
                   now() as database_now
            from telehealth_prospective_applicants applicant
            join telehealth_applicant_contact_challenges challenge
              on challenge.applicant_id=applicant.applicant_id
            where applicant.practice_id=@practiceId
              and applicant.facility_id=@facilityId
              and applicant.applicant_id=@applicantId
            {(forUpdate ? "for update of applicant" : string.Empty)};
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new TelehealthProspectiveApplicantRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            Convert.ToInt32(reader.GetInt64(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetFieldValue<DateOnly>(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
            reader.GetFieldValue<DateTimeOffset>(15),
            reader.GetInt32(16),
            Convert.ToInt32(reader.GetInt64(17)),
            reader.GetFieldValue<DateTimeOffset>(18));
    }

    private static void RequireAccess(TelehealthProspectiveApplicantRecord applicant, string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(applicant.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static async Task<TelehealthProspectiveApplicantRecord> ExpireIfNeededAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TelehealthProspectiveApplicantRecord current,
        CancellationToken cancellationToken) =>
        current.Status == "ContactVerificationPending" && current.ExpiresAt <= current.DatabaseNow
            ? await ExpireAsync(connection, transaction, current, cancellationToken)
            : current;

    private static async Task<TelehealthProspectiveApplicantRecord> ExpireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TelehealthProspectiveApplicantRecord current,
        CancellationToken cancellationToken)
    {
        if (current.Status != "ContactVerificationPending")
        {
            return current;
        }
        var nextVersion = current.Version + 1;
        var fingerprint = TelehealthCommandFingerprint.Create(
            "prospective-applicant-expiry-v1", current.ApplicantId, current.ExpiresAt);
        await UpdateStatusAsync(
            connection,
            transaction,
            current.ApplicantId,
            current.Version,
            "Expired",
            nextVersion,
            null,
            null,
            null,
            cancellationToken);
        await InsertEventAsync(
            connection,
            transaction,
            current.ApplicantId,
            nextVersion,
            "applicant-expired",
            current.Status,
            "Expired",
            "system",
            "system-expiry-v1",
            fingerprint,
            cancellationToken);
        return current with { Status = "Expired", Version = nextVersion };
    }

    private static async Task UpdateStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        int expectedVersion,
        string status,
        int version,
        string? duplicateDisposition,
        string? duplicateFingerprint,
        DateTimeOffset? contactVerifiedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update telehealth_prospective_applicants
            set status=@status,
                version=@version,
                duplicate_disposition=@duplicateDisposition,
                duplicate_evidence_fingerprint=@duplicateFingerprint,
                contact_verified_at=@contactVerifiedAt,
                updated_at=now()
            where applicant_id=@applicantId and version=@expectedVersion;
            """;
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("duplicateDisposition", (object?)duplicateDisposition ?? DBNull.Value);
        command.Parameters.AddWithValue("duplicateFingerprint", (object?)duplicateFingerprint ?? DBNull.Value);
        command.Parameters.AddWithValue("contactVerifiedAt", (object?)contactVerifiedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("expectedVersion", expectedVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_version_conflict",
                "The applicant changed. Refresh the current applicant state before retrying.");
        }
    }

    private static async Task InsertAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        int ordinal,
        string result,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_applicant_verification_attempts(
              attempt_id,applicant_id,attempt_ordinal,result,idempotency_key,command_fingerprint)
            values(@attemptId,@applicantId,@ordinal,@result,@idempotencyKey,@fingerprint);
            """;
        command.Parameters.AddWithValue("attemptId", Guid.NewGuid());
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("ordinal", ordinal);
        command.Parameters.AddWithValue("result", result);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        int version,
        string action,
        string? fromStatus,
        string toStatus,
        string actorType,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_applicant_events(
              event_id,applicant_id,aggregate_version,action,from_status,to_status,
              actor_type,idempotency_key,command_fingerprint)
            values(@eventId,@applicantId,@version,@action,@fromStatus,@toStatus,
                   @actorType,@idempotencyKey,@fingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("fromStatus", (object?)fromStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("toStatus", toStatus);
        command.Parameters.AddWithValue("actorType", actorType);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(Guid ApplicantId, string AccessKeyHash, string CommandFingerprint)?> LoadByCreateIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select applicant_id,access_key_hash,create_fingerprint
            from telehealth_prospective_applicants
            where practice_id=@practiceId and facility_id=@facilityId
              and create_idempotency_key=@idempotencyKey
            for update;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    private static async Task<(string CommandFingerprint, string Result)?> LoadAttemptByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select command_fingerprint,result
            from telehealth_applicant_verification_attempts
            where applicant_id=@applicantId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task<int> CountAttemptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)
            from telehealth_applicant_verification_attempts
            where applicant_id=@applicantId;
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<(string VerifierHash, int MaximumAttempts, DateTimeOffset ExpiresAt)?> LoadChallengeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid applicantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select verifier_hash,maximum_attempts,expires_at
            from telehealth_applicant_contact_challenges
            where applicant_id=@applicantId;
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetInt32(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<(bool PossibleMatch, string DatasetId, string DatasetVersion)> ClassifyDuplicateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TelehealthProspectiveApplicantRecord applicant,
        CancellationToken cancellationToken)
    {
        var phoneDigits = applicant.Phone[^10..];
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
                )
            ) as possible_match,
            metadata.dataset_id,
            metadata.version
            from dataset_metadata metadata
            order by metadata.dataset_id
            limit 1;
            """;
        command.Parameters.AddWithValue("facilityId", applicant.FacilityId);
        command.Parameters.AddWithValue("firstName", applicant.LegalFirstName.ToLowerInvariant());
        command.Parameters.AddWithValue("lastName", applicant.LegalLastName.ToLowerInvariant());
        command.Parameters.AddWithValue("dateOfBirth", applicant.DateOfBirth);
        command.Parameters.AddWithValue("email", applicant.Email);
        command.Parameters.AddWithValue("phoneDigits", phoneDigits);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_duplicate_evidence_unavailable",
                "Identity review evidence is unavailable. No patient record was created.");
        }
        return (reader.GetBoolean(0), reader.GetString(1), reader.GetString(2));
    }
}
