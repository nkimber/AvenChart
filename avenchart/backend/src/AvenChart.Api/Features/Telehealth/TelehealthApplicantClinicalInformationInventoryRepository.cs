// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantClinicalInformationInventoryContext(
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
    Guid? DevicePreparationId,
    int? DevicePreparationApplicantVersion,
    string? DevicePreparationApplicantStatus,
    string? PreparationSnapshotFingerprint,
    string? NetworkQuality,
    Guid? RegistrationDetailsConfirmationId,
    Guid? InsuranceHandoffConfirmationId,
    Guid? SafetyEvaluationId,
    Guid? CommunicationAccessReadinessId,
    bool SourceProvenanceValid,
    long CanonicalInsuranceRecordCount,
    Guid? InventoryId,
    int? InventoryApplicantVersion,
    string? InventoryApplicantStatus,
    string? MedicationsStatus,
    string? AllergiesOrIntolerancesStatus,
    string? OtherHealthHistoryStatus,
    string? ReviewRoute,
    DateTimeOffset? RecordedAt);

public sealed record TelehealthApplicantClinicalInformationInventoryRecord(
    Guid InventoryId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string InventorySnapshotFingerprint,
    string MedicationsStatus,
    string AllergiesOrIntolerancesStatus,
    string OtherHealthHistoryStatus,
    string ReviewRoute,
    DateTimeOffset RecordedAt);

public sealed class TelehealthApplicantClinicalInformationInventoryRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),a.facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
          patient.merged_into_patient_id,preparation.preparation_id,
          preparation.resulting_applicant_version,preparation.resulting_applicant_status,
          preparation.preparation_snapshot_fingerprint,preparation.network_quality,
          preparation.registration_details_confirmation_id,
          preparation.insurance_handoff_confirmation_id,preparation.safety_evaluation_id,
          preparation.communication_access_readiness_id,
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
            and preparation.applicant_id=a.applicant_id
            and preparation.practice_id=a.practice_id
            and preparation.facility_id=a.facility_id
            and preparation.promotion_id=promotion.promotion_id
            and preparation.canonical_patient_id=promotion.canonical_patient_id
            and preparation.resulting_applicant_status='SyntheticDevicePreparationRecorded'
            and preparation.browser_supported
            and preparation.camera_available
            and preparation.microphone_available
            and preparation.speaker_available
            and preparation.network_quality in ('Unknown','Good')
            and preparation.client_reported_result_acknowledged
            and preparation.no_readiness_guarantee_acknowledged
            and preparation.recheck_before_consultation_acknowledged
            and preparation.policy_key='SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
            and preparation.policy_version=1
            and preparation.evidence_type='PROMOTED_PATIENT_DEVICE_PREPARATION_RECEIPT'
            and not preparation.technology_ready
            and not preparation.waiting_room_created
            and not preparation.media_session_created
            and not preparation.communication_started
            and not preparation.support_arrangement_completed
            and not preparation.patient_record_changed
            and not preparation.portal_access_enabled
            and not preparation.intake_completed
            and not preparation.legal_consent_established
            and not preparation.practice_accepted
            and not preparation.financial_record_created
            and not preparation.request_created
            and not preparation.queue_entered
            and not preparation.appointment_created
            and not preparation.encounter_created
            and not preparation.care_authorized
            and not preparation.prescribing_enabled
            and not preparation.billing_enabled
            and not preparation.claim_created
            and not preparation.integration_enabled
            and not preparation.external_call_performed,
            false) as source_provenance_valid,
          (select count(*) from insurance_records insurance
             where lower(insurance.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_insurance_record_count,
          inventory.inventory_id,inventory.resulting_applicant_version,
          inventory.resulting_applicant_status,inventory.medications_status,
          inventory.allergies_or_intolerances_status,inventory.other_health_history_status,
          inventory.review_route,inventory.recorded_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_device_preparations preparation
          on preparation.applicant_id=a.applicant_id
        left join telehealth_applicant_clinical_information_inventories inventory
          on inventory.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantClinicalInformationInventoryContext> GetAuthorizedAsync(
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

    public async Task<TelehealthApplicantClinicalInformationInventoryRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantClinicalInformationInventory inventory,
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
            RequireEligible(context, facilityId, allowRecorded: true);
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowRecorded: false);
        if (context.ApplicantVersion != inventory.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_inventory_version_conflict",
                "The applicant changed. Reload the clinical-information inventory before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(snapshot.Fingerprint, inventory.InventorySnapshotFingerprint, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_inventory_snapshot_conflict",
                "The clinical-information inventory context changed. Reload it before recording the result.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticDevicePreparationRecorded';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", inventory.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_clinical_information_inventory_version_conflict",
                    "The applicant changed. Reload the clinical-information inventory before retrying.");
            }
        }

        var inventoryId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_clinical_information_inventories(
                  inventory_id,applicant_id,practice_id,facility_id,promotion_id,
                  canonical_patient_id,registration_details_confirmation_id,
                  insurance_handoff_confirmation_id,safety_evaluation_id,
                  communication_access_readiness_id,device_preparation_id,
                  resulting_applicant_version,resulting_applicant_status,
                  inventory_snapshot_fingerprint,preparation_snapshot_fingerprint,
                  medications_status,allergies_or_intolerances_status,
                  other_health_history_status,review_route,
                  patient_reported_may_be_incomplete_acknowledged,
                  no_clinical_details_captured_acknowledged,
                  clinician_reconciliation_required_acknowledged,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @inventoryId,@applicantId,@practiceId,@facilityId,@promotionId,
                  @patientId,@registrationId,@handoffId,@safetyId,@readinessId,@preparationId,
                  @nextVersion,@nextStatus,@snapshotFingerprint,@preparationFingerprint,
                  @medicationsStatus,@allergiesStatus,@historyStatus,@reviewRoute,
                  true,true,true,@policyKey,@policyVersion,@evidenceType,@applicantExpiresAt,
                  @idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("inventoryId", inventoryId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue(
                "registrationId", context.RegistrationDetailsConfirmationId!.Value);
            insert.Parameters.AddWithValue(
                "handoffId", context.InsuranceHandoffConfirmationId!.Value);
            insert.Parameters.AddWithValue("safetyId", context.SafetyEvaluationId!.Value);
            insert.Parameters.AddWithValue(
                "readinessId", context.CommunicationAccessReadinessId!.Value);
            insert.Parameters.AddWithValue("preparationId", context.DevicePreparationId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue(
                "preparationFingerprint", context.PreparationSnapshotFingerprint!);
            insert.Parameters.AddWithValue("medicationsStatus", inventory.MedicationsStatus);
            insert.Parameters.AddWithValue(
                "allergiesStatus", inventory.AllergiesOrIntolerancesStatus);
            insert.Parameters.AddWithValue("historyStatus", inventory.OtherHealthHistoryStatus);
            insert.Parameters.AddWithValue("reviewRoute", inventory.ReviewRoute);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantClinicalInformationInventoryPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantClinicalInformationInventoryPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantClinicalInformationInventoryPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Synthetic clinical-information inventory time is unavailable.");
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
                       'prospective-clinical-information-inventory-recorded',
                       'SyntheticDevicePreparationRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "clinical-information-inventory:" +
                TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            inventoryId,
            applicantId,
            nextVersion,
            TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus,
            snapshot.Fingerprint,
            inventory.MedicationsStatus,
            inventory.AllergiesOrIntolerancesStatus,
            inventory.OtherHealthHistoryStatus,
            inventory.ReviewRoute,
            recordedAt);
    }

    public static TelehealthApplicantClinicalInformationInventorySnapshot Snapshot(
        TelehealthApplicantClinicalInformationInventoryContext context) =>
        TelehealthApplicantClinicalInformationInventoryPolicy.Snapshot(
            context.DevicePreparationId!.Value,
            context.PreparationSnapshotFingerprint!,
            context.NetworkQuality!);

    private static async Task<TelehealthApplicantClinicalInformationInventoryContext?> LoadAsync(
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
            NullableString(reader, 13), NullableGuid(reader, 14),
            NullableInt32FromInt64(reader, 15), NullableString(reader, 16),
            NullableString(reader, 17), NullableString(reader, 18), NullableGuid(reader, 19),
            NullableGuid(reader, 20), NullableGuid(reader, 21), NullableGuid(reader, 22),
            reader.GetBoolean(23), reader.GetInt64(24), NullableGuid(reader, 25),
            NullableInt32FromInt64(reader, 26), NullableString(reader, 27),
            NullableString(reader, 28), NullableString(reader, 29), NullableString(reader, 30),
            NullableString(reader, 31), NullableDateTimeOffset(reader, 32));
    }

    private static async Task<(TelehealthApplicantClinicalInformationInventoryRecord Record,
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
            select inventory_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,inventory_snapshot_fingerprint,
                   medications_status,allergies_or_intolerances_status,
                   other_health_history_status,review_route,recorded_at,command_fingerprint
            from telehealth_applicant_clinical_information_inventories
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
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)),
            reader.GetString(10));
    }

    private static void RequireEligible(
        TelehealthApplicantClinicalInformationInventoryContext context,
        int facilityId,
        bool allowRecorded)
    {
        var entry = context.ApplicantStatus ==
                TelehealthApplicantClinicalInformationInventoryPolicy.EntryStatus
            && context.DevicePreparationApplicantVersion == context.ApplicantVersion;
        var recorded = allowRecorded
            && context.ApplicantStatus ==
                TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus
            && context.InventoryId is not null
            && context.InventoryApplicantVersion == context.ApplicantVersion
            && context.InventoryApplicantStatus ==
                TelehealthApplicantClinicalInformationInventoryPolicy.ResultingStatus
            && context.DevicePreparationApplicantVersion == context.ApplicantVersion - 1;
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
            || context.DevicePreparationId is null
            || context.DevicePreparationApplicantStatus !=
                TelehealthApplicantClinicalInformationInventoryPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.PreparationSnapshotFingerprint)
            || string.IsNullOrWhiteSpace(context.NetworkQuality)
            || context.RegistrationDetailsConfirmationId is null
            || context.InsuranceHandoffConfirmationId is null
            || context.SafetyEvaluationId is null
            || context.CommunicationAccessReadinessId is null
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceRecordCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_inventory_state_conflict",
                "The applicant is not eligible for this bounded synthetic clinical-information inventory receipt.");
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
                "telehealth_applicant_clinical_information_inventory_idempotency_conflict",
                "The clinical-information inventory idempotency key was already used with different content.");
        }
    }
}
