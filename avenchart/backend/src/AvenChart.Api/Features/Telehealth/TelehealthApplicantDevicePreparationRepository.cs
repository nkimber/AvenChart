// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantDevicePreparationContext(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AccessKeyHash,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    int ApplicantFacilityId,
    Guid? PromotionId,
    string? PromotionOutcome,
    bool? CanonicalPatientCreated,
    string? CanonicalPatientId,
    bool? PatientPortalEnabled,
    int? PatientFacilityId,
    string? MergedIntoPatientId,
    Guid? RegistrationDetailsConfirmationId,
    Guid? InsuranceHandoffConfirmationId,
    Guid? SafetyEvaluationId,
    Guid? CommunicationAccessReadinessId,
    int? CommunicationAccessApplicantVersion,
    string? CommunicationAccessApplicantStatus,
    string? CommunicationContextFingerprint,
    string? CurrentLocationStateCode,
    string? CallbackPhoneLast4,
    bool SourceProvenanceValid,
    long CanonicalInsuranceRecordCount,
    Guid? PreparationId,
    int? PreparationApplicantVersion,
    string? PreparationApplicantStatus,
    bool? BrowserSupported,
    bool? CameraAvailable,
    bool? MicrophoneAvailable,
    bool? SpeakerAvailable,
    string? NetworkQuality,
    DateTimeOffset? RecordedAt);

public sealed record TelehealthApplicantDevicePreparationRecord(
    Guid PreparationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string PreparationSnapshotFingerprint,
    bool BrowserSupported,
    bool CameraAvailable,
    bool MicrophoneAvailable,
    bool SpeakerAvailable,
    string NetworkQuality,
    DateTimeOffset RecordedAt);

public sealed class TelehealthApplicantDevicePreparationRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),a.facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
          patient.merged_into_patient_id,registration.confirmation_id,handoff.confirmation_id,
          safety.evaluation_id,readiness.readiness_id,readiness.resulting_applicant_version,
          readiness.resulting_applicant_status,readiness.context_snapshot_fingerprint,
          readiness.current_location_state_code,readiness.callback_phone_last4,
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
            and registration.applicant_id=a.applicant_id
            and registration.practice_id=a.practice_id
            and registration.facility_id=a.facility_id
            and registration.promotion_id=promotion.promotion_id
            and registration.canonical_patient_id=promotion.canonical_patient_id
            and registration.resulting_applicant_status='SyntheticMinimumRegistrationDetailsConfirmed'
            and handoff.applicant_id=a.applicant_id
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
            and safety.applicant_id=a.applicant_id
            and safety.practice_id=a.practice_id
            and safety.facility_id=a.facility_id
            and safety.outcome='TelehealthEligible'
            and safety.resulting_applicant_status='SafetyScreenPassed'
            and safety.current_location_confirmed
            and safety.current_location_state_code in ('GA','CA','FL')
            and readiness.applicant_id=a.applicant_id
            and readiness.practice_id=a.practice_id
            and readiness.facility_id=a.facility_id
            and readiness.promotion_id=promotion.promotion_id
            and readiness.canonical_patient_id=promotion.canonical_patient_id
            and readiness.registration_details_confirmation_id=registration.confirmation_id
            and readiness.insurance_handoff_confirmation_id=handoff.confirmation_id
            and readiness.safety_evaluation_id=safety.evaluation_id
            and readiness.resulting_applicant_status='SyntheticCommunicationAccessReadinessRecorded'
            and readiness.current_location_state_code=safety.current_location_state_code
            and readiness.callback_phone_last4=right(regexp_replace(a.phone,'[^0-9]','','g'),4)
            and readiness.preferred_spoken_language in ('English','Spanish')
            and readiness.current_location_confirmed
            and readiness.callback_number_confirmed
            and readiness.safe_private_communication_confirmed
            and readiness.disconnection_emergency_plan_acknowledged
            and readiness.synthetic_data_confirmed
            and readiness.policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
            and readiness.policy_version=1
            and not readiness.interpreter_assigned
            and not readiness.accessibility_accommodation_arranged
            and not readiness.communication_arrangement_completed
            and not readiness.support_request_created
            and not readiness.technology_readiness_completed
            and not readiness.patient_record_changed
            and not readiness.portal_access_enabled
            and not readiness.intake_completed
            and not readiness.legal_consent_established
            and not readiness.practice_accepted
            and not readiness.request_created
            and not readiness.queue_enabled
            and not readiness.care_enabled
            and not readiness.communication_enabled
            and not readiness.integration_enabled
            and not readiness.external_call_performed,
            false) as source_provenance_valid,
          (select count(*) from insurance_records insurance
             where lower(insurance.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_insurance_record_count,
          preparation.preparation_id,preparation.resulting_applicant_version,
          preparation.resulting_applicant_status,preparation.browser_supported,
          preparation.camera_available,preparation.microphone_available,
          preparation.speaker_available,preparation.network_quality,preparation.recorded_at
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
        left join telehealth_applicant_device_preparations preparation
          on preparation.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantDevicePreparationContext> GetAuthorizedAsync(
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

    public async Task<TelehealthApplicantDevicePreparationRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantDevicePreparation preparation,
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
        if (context.ApplicantVersion != preparation.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_device_preparation_version_conflict",
                "The applicant changed. Reload the device-preparation context before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(snapshot.Fingerprint, preparation.PreparationSnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_device_preparation_snapshot_conflict",
                "The device-preparation context changed. Reload it before recording the result.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticCommunicationAccessReadinessRecorded';
                """;
            update.Parameters.AddWithValue("status", TelehealthApplicantDevicePreparationPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", preparation.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_device_preparation_version_conflict",
                    "The applicant changed. Reload the device-preparation context before retrying.");
            }
        }

        var preparationId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_device_preparations(
                  preparation_id,applicant_id,practice_id,facility_id,
                  promotion_id,canonical_patient_id,registration_details_confirmation_id,
                  insurance_handoff_confirmation_id,safety_evaluation_id,
                  communication_access_readiness_id,resulting_applicant_version,
                  resulting_applicant_status,preparation_snapshot_fingerprint,
                  communication_context_fingerprint,browser_supported,camera_available,
                  microphone_available,speaker_available,network_quality,
                  client_reported_result_acknowledged,no_readiness_guarantee_acknowledged,
                  recheck_before_consultation_acknowledged,policy_key,policy_version,
                  evidence_type,applicant_expires_at,idempotency_key,command_fingerprint)
                values(
                  @preparationId,@applicantId,@practiceId,@facilityId,
                  @promotionId,@patientId,@registrationId,@handoffId,@safetyId,@readinessId,
                  @nextVersion,@nextStatus,@snapshotFingerprint,@contextFingerprint,
                  true,true,true,true,@networkQuality,true,true,true,@policyKey,@policyVersion,
                  @evidenceType,@applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("preparationId", preparationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("registrationId", context.RegistrationDetailsConfirmationId!.Value);
            insert.Parameters.AddWithValue("handoffId", context.InsuranceHandoffConfirmationId!.Value);
            insert.Parameters.AddWithValue("safetyId", context.SafetyEvaluationId!.Value);
            insert.Parameters.AddWithValue("readinessId", context.CommunicationAccessReadinessId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue("nextStatus", TelehealthApplicantDevicePreparationPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue("contextFingerprint", context.CommunicationContextFingerprint!);
            insert.Parameters.AddWithValue("networkQuality", preparation.NetworkQuality);
            insert.Parameters.AddWithValue("policyKey", TelehealthApplicantDevicePreparationPolicy.PolicyKey);
            insert.Parameters.AddWithValue("policyVersion", TelehealthApplicantDevicePreparationPolicy.PolicyVersion);
            insert.Parameters.AddWithValue("evidenceType", TelehealthApplicantDevicePreparationPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic device-preparation time is unavailable.");
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
                       'prospective-device-preparation-recorded',
                       'SyntheticCommunicationAccessReadinessRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("nextStatus", TelehealthApplicantDevicePreparationPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "device-preparation:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            preparationId,
            applicantId,
            nextVersion,
            TelehealthApplicantDevicePreparationPolicy.ResultingStatus,
            snapshot.Fingerprint,
            true,
            true,
            true,
            true,
            preparation.NetworkQuality,
            recordedAt);
    }

    public static TelehealthApplicantDevicePreparationSnapshot Snapshot(
        TelehealthApplicantDevicePreparationContext context) =>
        TelehealthApplicantDevicePreparationPolicy.Snapshot(
            context.CommunicationAccessReadinessId!.Value,
            context.CommunicationContextFingerprint!,
            context.CurrentLocationStateCode!,
            context.CallbackPhoneLast4!);

    private static async Task<TelehealthApplicantDevicePreparationContext?> LoadAsync(
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
            NullableGuid(reader, 7), NullableString(reader, 8), NullableBoolean(reader, 9),
            NullableString(reader, 10), NullableBoolean(reader, 11), NullableInt32(reader, 12),
            NullableString(reader, 13), NullableGuid(reader, 14), NullableGuid(reader, 15),
            NullableGuid(reader, 16), NullableGuid(reader, 17), NullableInt32FromInt64(reader, 18),
            NullableString(reader, 19), NullableString(reader, 20), NullableString(reader, 21),
            NullableString(reader, 22), reader.GetBoolean(23), reader.GetInt64(24),
            NullableGuid(reader, 25), NullableInt32FromInt64(reader, 26), NullableString(reader, 27),
            NullableBoolean(reader, 28), NullableBoolean(reader, 29), NullableBoolean(reader, 30),
            NullableBoolean(reader, 31), NullableString(reader, 32), NullableDateTimeOffset(reader, 33));
    }

    private static async Task<(TelehealthApplicantDevicePreparationRecord Record,
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
            select preparation_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,preparation_snapshot_fingerprint,
                   browser_supported,camera_available,microphone_available,
                   speaker_available,network_quality,recorded_at,command_fingerprint
            from telehealth_applicant_device_preparations
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
            reader.GetString(3), reader.GetString(4), reader.GetBoolean(5),
            reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8),
            reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10)),
            reader.GetString(11));
    }

    private static void RequireEligible(
        TelehealthApplicantDevicePreparationContext context,
        int facilityId,
        bool allowRecorded)
    {
        var entry = context.ApplicantStatus == TelehealthApplicantDevicePreparationPolicy.EntryStatus
            && context.CommunicationAccessApplicantVersion == context.ApplicantVersion;
        var recorded = allowRecorded
            && context.ApplicantStatus == TelehealthApplicantDevicePreparationPolicy.ResultingStatus
            && context.PreparationId is not null
            && context.PreparationApplicantVersion == context.ApplicantVersion
            && context.PreparationApplicantStatus == TelehealthApplicantDevicePreparationPolicy.ResultingStatus
            && context.CommunicationAccessApplicantVersion == context.ApplicantVersion - 1;
        if ((!entry && !recorded)
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
            || context.CommunicationAccessReadinessId is null
            || context.CommunicationAccessApplicantStatus
                != TelehealthApplicantDevicePreparationPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.CommunicationContextFingerprint)
            || string.IsNullOrWhiteSpace(context.CurrentLocationStateCode)
            || string.IsNullOrWhiteSpace(context.CallbackPhoneLast4)
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceRecordCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_device_preparation_state_conflict",
                "The applicant is not eligible for this bounded synthetic device-preparation receipt.");
        }
    }

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
                "telehealth_applicant_device_preparation_idempotency_conflict",
                "The device-preparation idempotency key was already used with different content.");
        }
    }
}
