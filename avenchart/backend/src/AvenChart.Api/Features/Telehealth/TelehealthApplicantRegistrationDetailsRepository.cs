// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRegistrationDetailsContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int ApplicantFacilityId,
    string LegalFirstName,
    string LegalLastName,
    DateOnly DateOfBirth,
    string Email,
    string Phone,
    string ResidenceStateCode,
    string PostalCode,
    Guid? PromotionId,
    string? PromotionOutcome,
    bool? CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    string? PatientFirstName,
    string? PatientLastName,
    DateOnly? PatientDateOfBirth,
    string? PatientEmail,
    string? PatientPhone,
    string? PatientStateCode,
    string? PatientPostalCode,
    Guid? NoticeAcknowledgmentId,
    Guid? ConfirmationId,
    DateTimeOffset? ConfirmedAt);

public sealed record TelehealthApplicantRegistrationDetailsConfirmationRecord(
    Guid ConfirmationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string DetailsFingerprint,
    DateTimeOffset ConfirmedAt);

public sealed class TelehealthApplicantRegistrationDetailsRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),
               a.facility_id,a.legal_first_name,a.legal_last_name,a.date_of_birth,
               a.email,a.phone,a.residence_state_code,a.postal_code,
               promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
               promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
               patient.merged_into_patient_id,patient.first_name,patient.last_name,
               patient.date_of_birth,patient.email,
               coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone),
               patient.state,patient.postal_code,notice.acknowledgment_id,
               confirmation.confirmation_id,confirmation.confirmed_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_notice_acknowledgments notice
          on notice.applicant_id=a.applicant_id
        left join telehealth_applicant_registration_details_confirmations confirmation
          on confirmation.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantRegistrationDetailsContext> GetAuthorizedAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);
        RequireEligible(context, facilityId, allowConfirmed: true);
        return context;
    }

    public async Task<TelehealthApplicantRegistrationDetailsConfirmationRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRegistrationDetailsConfirmation confirmation,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(context.AccessKeyHash, accessKeyHash);

        var replay = await LoadByIdempotencyAsync(
            connection, transaction, practiceId, facilityId, applicantId,
            idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowConfirmed: false);
        if (context.ApplicantVersion != confirmation.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_registration_details_version_conflict",
                "The applicant changed. Reload the minimum registration details before retrying.");
        }
        var snapshot = Snapshot(context);
        if (!string.Equals(snapshot.Fingerprint, confirmation.DetailsFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_registration_details_snapshot_conflict",
                "The minimum registration details changed. Reload them before confirming.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticTelehealthNoticeAcknowledged';
                """;
            update.Parameters.AddWithValue("status", TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", confirmation.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_registration_details_version_conflict",
                    "The applicant changed. Reload the minimum registration details before retrying.");
            }
        }

        var confirmationId = Guid.NewGuid();
        DateTimeOffset confirmedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_registration_details_confirmations(
                  confirmation_id,applicant_id,practice_id,facility_id,
                  notice_acknowledgment_id,promotion_id,canonical_patient_id,
                  resulting_applicant_version,resulting_applicant_status,
                  details_fingerprint,legal_name_birth_date_confirmed,
                  contact_channels_confirmed,residence_region_confirmed,
                  no_corrections_needed_confirmed,synthetic_data_confirmed,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @confirmationId,@applicantId,@practiceId,@facilityId,
                  @noticeId,@promotionId,@patientId,@nextVersion,@nextStatus,
                  @detailsFingerprint,true,true,true,true,true,
                  @policyKey,@policyVersion,@evidenceType,@applicantExpiresAt,
                  @idempotencyKey,@commandFingerprint)
                returning confirmed_at;
                """;
            insert.Parameters.AddWithValue("confirmationId", confirmationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("noticeId", context.NoticeAcknowledgmentId!.Value);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("detailsFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantRegistrationDetailsPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantRegistrationDetailsPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantRegistrationDetailsPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic minimum registration-details confirmation time is unavailable.");
            }
            confirmedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-minimum-registration-details-confirmed',
                       'SyntheticTelehealthNoticeAcknowledged',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "registration-details-confirmation:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            confirmationId,
            applicantId,
            nextVersion,
            TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus,
            snapshot.Fingerprint,
            confirmedAt);
    }

    public static TelehealthApplicantRegistrationDetailsSnapshot Snapshot(
        TelehealthApplicantRegistrationDetailsContext context) =>
        TelehealthApplicantRegistrationDetailsPolicy.Snapshot(
            context.LegalFirstName,
            context.LegalLastName,
            context.DateOfBirth,
            context.Email,
            context.Phone,
            context.ResidenceStateCode,
            context.PostalCode);

    private static async Task<TelehealthApplicantRegistrationDetailsContext?> LoadAsync(
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
        command.CommandText = ContextProjection + "\n" + """
            where a.practice_id=@practiceId and a.facility_id=@facilityId
              and a.applicant_id=@applicantId
            """ + (forUpdate ? "\nfor update of a;" : ";");
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(
            reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
            reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt32(6),
            reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateOnly>(9),
            reader.GetString(10), reader.GetString(11), reader.GetString(12),
            reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetBoolean(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetBoolean(18),
            reader.IsDBNull(19) ? null : reader.GetInt32(19),
            reader.IsDBNull(20) ? null : reader.GetString(20),
            reader.IsDBNull(21) ? null : reader.GetString(21),
            reader.IsDBNull(22) ? null : reader.GetString(22),
            reader.IsDBNull(23) ? null : reader.GetFieldValue<DateOnly>(23),
            reader.IsDBNull(24) ? null : reader.GetString(24),
            reader.IsDBNull(25) ? null : reader.GetString(25),
            reader.IsDBNull(26) ? null : reader.GetString(26),
            reader.IsDBNull(27) ? null : reader.GetString(27),
            reader.IsDBNull(28) ? null : reader.GetGuid(28),
            reader.IsDBNull(29) ? null : reader.GetGuid(29),
            reader.IsDBNull(30) ? null : reader.GetFieldValue<DateTimeOffset>(30));
    }

    private static async Task<(TelehealthApplicantRegistrationDetailsConfirmationRecord Record,
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
            select confirmation_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,details_fingerprint,confirmed_at,
                   command_fingerprint
            from telehealth_applicant_registration_details_confirmations
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
            reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)),
            reader.GetString(6));
    }

    private static void RequireEligible(
        TelehealthApplicantRegistrationDetailsContext context,
        int facilityId,
        bool allowConfirmed)
    {
        var statusAllowed = context.ApplicantStatus == "SyntheticTelehealthNoticeAcknowledged"
            || (allowConfirmed
                && context.ApplicantStatus == TelehealthApplicantRegistrationDetailsPolicy.ResultingStatus
                && context.ConfirmationId is not null);
        var patientMatchesApplicant = context.PatientFirstName == context.LegalFirstName
            && context.PatientLastName == context.LegalLastName
            && context.PatientDateOfBirth == context.DateOfBirth
            && context.PatientEmail == context.Email
            && context.PatientPhone == context.Phone
            && context.PatientStateCode == context.ResidenceStateCode
            && context.PatientPostalCode == context.PostalCode;
        if (!statusAllowed
            || context.ApplicantExpiresAt <= context.DatabaseNow
            || context.ApplicantFacilityId != facilityId
            || context.PromotionOutcome != "SyntheticPatientCreated"
            || context.CanonicalPatientCreated is not true
            || context.PromotionId is null
            || string.IsNullOrWhiteSpace(context.CanonicalPatientId)
            || context.PatientPortalEnabled is not false
            || context.PatientFacilityId != facilityId
            || context.MergedIntoPatientId is not null
            || context.NoticeAcknowledgmentId is null
            || !patientMatchesApplicant)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_registration_details_state_conflict",
                "The applicant is not eligible for this bounded minimum registration-details confirmation.");
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
                "telehealth_applicant_registration_details_idempotency_conflict",
                "The registration-details idempotency key was already used with different content.");
        }
    }
}
