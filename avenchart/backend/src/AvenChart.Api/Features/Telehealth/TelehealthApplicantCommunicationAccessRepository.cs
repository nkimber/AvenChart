// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantCommunicationAccessContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int ApplicantFacilityId,
    string CallbackPhone,
    Guid? PromotionId,
    string? PromotionOutcome,
    bool? CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    Guid? RegistrationDetailsConfirmationId,
    Guid? InsuranceHandoffConfirmationId,
    int? InsuranceHandoffApplicantVersion,
    string? InsuranceHandoffApplicantStatus,
    Guid? SafetyEvaluationId,
    string? CurrentLocationStateCode,
    string? SafetyOutcome,
    bool? CurrentLocationPreviouslyConfirmed,
    bool SourceProvenanceValid,
    long CanonicalInsuranceRecordCount,
    Guid? ReadinessId,
    string? PreferredSpokenLanguage,
    bool? InterpreterRequested,
    bool? AccessibilitySupportRequested,
    DateTimeOffset? RecordedAt);

public sealed record TelehealthApplicantCommunicationAccessReadinessRecord(
    Guid ReadinessId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string ContextSnapshotFingerprint,
    string PreferredSpokenLanguage,
    bool InterpreterRequested,
    bool AccessibilitySupportRequested,
    DateTimeOffset RecordedAt);

public sealed class TelehealthApplicantCommunicationAccessRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),
          a.facility_id,a.phone,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
          patient.merged_into_patient_id,registration.confirmation_id,
          handoff.confirmation_id,handoff.resulting_applicant_version,
          handoff.resulting_applicant_status,safety.evaluation_id,
          safety.current_location_state_code,safety.outcome,safety.current_location_confirmed,
          coalesce(
            promotion.outcome='SyntheticPatientCreated'
            and promotion.canonical_patient_created
            and promotion.practice_id=a.practice_id
            and promotion.facility_id=a.facility_id
            and patient.canonical_id=promotion.canonical_patient_id
            and patient.facility_id=a.facility_id
            and not patient.portal_enabled
            and patient.merged_into_patient_id is null
            and patient.first_name=a.legal_first_name
            and patient.last_name=a.legal_last_name
            and patient.date_of_birth=a.date_of_birth
            and patient.email=a.email
            and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
            and patient.state=a.residence_state_code
            and patient.postal_code=a.postal_code
            and registration.practice_id=a.practice_id
            and registration.facility_id=a.facility_id
            and registration.promotion_id=promotion.promotion_id
            and registration.canonical_patient_id=promotion.canonical_patient_id
            and registration.resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'
            and handoff.practice_id=a.practice_id
            and handoff.facility_id=a.facility_id
            and handoff.registration_details_confirmation_id=registration.confirmation_id
            and handoff.promotion_id=promotion.promotion_id
            and handoff.canonical_patient_id=promotion.canonical_patient_id
            and handoff.resulting_applicant_status='SyntheticInsuranceDetailsConfirmed'
            and handoff.policy_key='SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
            and handoff.policy_version=1
            and not handoff.coverage_verified
            and not handoff.exact_network_confirmed
            and not handoff.canonical_coverage_created
            and not handoff.patient_record_changed
            and not handoff.portal_access_enabled
            and not handoff.intake_completed
            and not handoff.legal_consent_established
            and not handoff.practice_accepted
            and not handoff.request_created
            and not handoff.queue_enabled
            and not handoff.care_enabled
            and safety.practice_id=a.practice_id
            and safety.facility_id=a.facility_id
            and safety.outcome='TelehealthEligible'
            and safety.resulting_applicant_status='SafetyScreenPassed'
            and safety.current_location_confirmed
            and safety.current_location_state_code in ('GA','CA','FL'),
            false) as source_provenance_valid,
          (select count(*) from insurance_records insurance
             where lower(insurance.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_insurance_record_count,
          readiness.readiness_id,readiness.preferred_spoken_language,
          readiness.interpreter_requested,readiness.accessibility_support_requested,
          readiness.recorded_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_registration_details_confirmations registration
          on registration.applicant_id=a.applicant_id
        left join telehealth_applicant_insurance_handoff_confirmations handoff
          on handoff.applicant_id=a.applicant_id
        left join telehealth_applicant_safety_triage_evaluations safety
          on safety.applicant_id=a.applicant_id
        left join telehealth_applicant_communication_access_readiness readiness
          on readiness.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantCommunicationAccessContext> GetAuthorizedAsync(
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
        RequireEligible(context, facilityId, allowRecorded: true);
        return context;
    }

    public async Task<TelehealthApplicantCommunicationAccessReadinessRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantCommunicationAccessReadiness readiness,
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
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowRecorded: false);
        if (context.ApplicantVersion != readiness.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_communication_access_version_conflict",
                "The applicant changed. Reload the communication and access context before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(snapshot.Fingerprint, readiness.ContextSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_communication_access_snapshot_conflict",
                "The communication and access context changed. Reload it before confirming.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticInsuranceDetailsConfirmed';
                """;
            update.Parameters.AddWithValue("status", TelehealthApplicantCommunicationAccessPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", readiness.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_communication_access_version_conflict",
                    "The applicant changed. Reload the communication and access context before retrying.");
            }
        }

        var readinessId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_communication_access_readiness(
                  readiness_id,applicant_id,practice_id,facility_id,
                  promotion_id,canonical_patient_id,registration_details_confirmation_id,
                  insurance_handoff_confirmation_id,safety_evaluation_id,
                  resulting_applicant_version,resulting_applicant_status,
                  context_snapshot_fingerprint,current_location_state_code,
                  callback_phone_last4,preferred_spoken_language,
                  interpreter_requested,accessibility_support_requested,
                  current_location_confirmed,callback_number_confirmed,
                  safe_private_communication_confirmed,
                  disconnection_emergency_plan_acknowledged,synthetic_data_confirmed,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @readinessId,@applicantId,@practiceId,@facilityId,
                  @promotionId,@patientId,@registrationId,@handoffId,@safetyId,
                  @nextVersion,@nextStatus,@snapshotFingerprint,@stateCode,
                  @callbackLast4,@preferredLanguage,@interpreterRequested,@accessibilityRequested,
                  true,true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("readinessId", readinessId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("registrationId", context.RegistrationDetailsConfirmationId!.Value);
            insert.Parameters.AddWithValue("handoffId", context.InsuranceHandoffConfirmationId!.Value);
            insert.Parameters.AddWithValue("safetyId", context.SafetyEvaluationId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", TelehealthApplicantCommunicationAccessPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("stateCode", snapshot.CurrentLocationStateCode);
            insert.Parameters.AddWithValue("callbackLast4", Digits(context.CallbackPhone)[^4..]);
            insert.Parameters.AddWithValue("preferredLanguage", readiness.PreferredSpokenLanguage);
            insert.Parameters.AddWithValue("interpreterRequested", readiness.InterpreterRequested);
            insert.Parameters.AddWithValue("accessibilityRequested", readiness.AccessibilitySupportRequested);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantCommunicationAccessPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantCommunicationAccessPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantCommunicationAccessPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic communication/access readiness time is unavailable.");
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
                       'prospective-communication-access-readiness-recorded',
                       'SyntheticInsuranceDetailsConfirmed',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", TelehealthApplicantCommunicationAccessPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "communication-access-readiness:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            readinessId,
            applicantId,
            nextVersion,
            TelehealthApplicantCommunicationAccessPolicy.ResultingStatus,
            snapshot.Fingerprint,
            readiness.PreferredSpokenLanguage,
            readiness.InterpreterRequested,
            readiness.AccessibilitySupportRequested,
            recordedAt);
    }

    public static TelehealthApplicantCommunicationAccessSnapshot Snapshot(
        TelehealthApplicantCommunicationAccessContext context) =>
        TelehealthApplicantCommunicationAccessPolicy.Snapshot(
            context.SafetyEvaluationId!.Value,
            context.InsuranceHandoffConfirmationId!.Value,
            context.CurrentLocationStateCode!,
            context.CallbackPhone);

    private static async Task<TelehealthApplicantCommunicationAccessContext?> LoadAsync(
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
            reader.GetFieldValue<DateTimeOffset>(5), reader.GetInt32(6), reader.GetString(7),
            NullableGuid(reader, 8), NullableString(reader, 9), NullableBoolean(reader, 10),
            NullableString(reader, 11), NullableBoolean(reader, 12), NullableInt32(reader, 13),
            NullableString(reader, 14), NullableGuid(reader, 15), NullableGuid(reader, 16),
            NullableInt32FromInt64(reader, 17), NullableString(reader, 18), NullableGuid(reader, 19),
            NullableString(reader, 20), NullableString(reader, 21), NullableBoolean(reader, 22),
            reader.GetBoolean(23), reader.GetInt64(24), NullableGuid(reader, 25),
            NullableString(reader, 26), NullableBoolean(reader, 27), NullableBoolean(reader, 28),
            NullableDateTimeOffset(reader, 29));
    }

    private static async Task<(TelehealthApplicantCommunicationAccessReadinessRecord Record,
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
            select readiness_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,context_snapshot_fingerprint,
                   preferred_spoken_language,interpreter_requested,
                   accessibility_support_requested,recorded_at,command_fingerprint
            from telehealth_applicant_communication_access_readiness
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
            reader.GetString(3), reader.GetString(4), reader.GetString(5),
            reader.GetBoolean(6), reader.GetBoolean(7), reader.GetFieldValue<DateTimeOffset>(8)),
            reader.GetString(9));
    }

    private static void RequireEligible(
        TelehealthApplicantCommunicationAccessContext context,
        int facilityId,
        bool allowRecorded)
    {
        var statusAllowed = context.ApplicantStatus == TelehealthApplicantCommunicationAccessPolicy.EntryStatus
            || (allowRecorded
                && context.ApplicantStatus == TelehealthApplicantCommunicationAccessPolicy.ResultingStatus
                && context.ReadinessId is not null);
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
            || context.RegistrationDetailsConfirmationId is null
            || context.InsuranceHandoffConfirmationId is null
            || context.SafetyEvaluationId is null
            || string.IsNullOrWhiteSpace(context.CurrentLocationStateCode)
            || string.IsNullOrWhiteSpace(context.CallbackPhone)
            || Digits(context.CallbackPhone).Length < 4
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceRecordCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_communication_access_state_conflict",
                "The applicant is not eligible for this bounded synthetic communication/access-readiness receipt.");
        }
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private static Guid? NullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static string? NullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static bool? NullableBoolean(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);

    private static int? NullableInt32(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static int? NullableInt32FromInt64(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetInt64(ordinal));

    private static DateTimeOffset? NullableDateTimeOffset(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);

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
                "telehealth_applicant_communication_access_idempotency_conflict",
                "The communication/access readiness idempotency key was already used with different content.");
        }
    }
}
