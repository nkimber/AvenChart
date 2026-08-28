// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantMedicationInformationContext(
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
    Guid? InventoryId,
    int? InventoryApplicantVersion,
    string? InventoryApplicantStatus,
    string? InventorySnapshotFingerprint,
    string? InventoryMedicationsStatus,
    string? InventoryReviewRoute,
    Guid? RegistrationDetailsConfirmationId,
    Guid? InsuranceHandoffConfirmationId,
    Guid? SafetyEvaluationId,
    Guid? CommunicationAccessReadinessId,
    Guid? DevicePreparationId,
    bool SourceProvenanceValid,
    long CanonicalMedicationCount,
    long CanonicalPrescriptionCount,
    Guid? ReceiptId,
    int? ReceiptApplicantVersion,
    string? ReceiptApplicantStatus,
    bool? AdditionalOrUnlistedItemsReported,
    string? MedicationReviewRoute,
    DateTimeOffset? RecordedAt);

public sealed record TelehealthApplicantMedicationInformationRecord(
    Guid ReceiptId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string MedicationInformationSnapshotFingerprint,
    IReadOnlyList<TelehealthApplicantMedicationItemResponse> MedicationItems,
    bool AdditionalOrUnlistedItemsReported,
    string ReviewRoute,
    DateTimeOffset RecordedAt);

public sealed record TelehealthApplicantMedicationInformationState(
    TelehealthApplicantMedicationInformationContext Context,
    IReadOnlyList<TelehealthApplicantMedicationItemResponse> MedicationItems);

public sealed class TelehealthApplicantMedicationInformationRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now(),a.facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id,
          patient.merged_into_patient_id,inventory.inventory_id,
          inventory.resulting_applicant_version,inventory.resulting_applicant_status,
          inventory.inventory_snapshot_fingerprint,inventory.medications_status,
          inventory.review_route,inventory.registration_details_confirmation_id,
          inventory.insurance_handoff_confirmation_id,inventory.safety_evaluation_id,
          inventory.communication_access_readiness_id,inventory.device_preparation_id,
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
            and inventory.applicant_id=a.applicant_id
            and inventory.practice_id=a.practice_id
            and inventory.facility_id=a.facility_id
            and inventory.promotion_id=promotion.promotion_id
            and inventory.canonical_patient_id=promotion.canonical_patient_id
            and inventory.resulting_applicant_status='SyntheticClinicalInformationInventoryRecorded'
            and inventory.medications_status in ('PatientReportsNone','ItemsToReview','Unsure')
            and inventory.allergies_or_intolerances_status in ('PatientReportsNone','ItemsToReview','Unsure')
            and inventory.other_health_history_status in ('PatientReportsNone','ItemsToReview','Unsure')
            and inventory.review_route = case
              when 'ItemsToReview' in (
                inventory.medications_status,inventory.allergies_or_intolerances_status,
                inventory.other_health_history_status) then 'DetailedCollectionRequired'
              when 'Unsure' in (
                inventory.medications_status,inventory.allergies_or_intolerances_status,
                inventory.other_health_history_status) then 'AssistedReviewRequired'
              else 'PendingClinicianReconciliation' end
            and inventory.patient_reported_may_be_incomplete_acknowledged
            and inventory.no_clinical_details_captured_acknowledged
            and inventory.clinician_reconciliation_required_acknowledged
            and inventory.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
            and inventory.policy_version=1
            and inventory.evidence_type='PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT'
            and not inventory.medication_list_reconciled
            and not inventory.allergy_list_reconciled
            and not inventory.health_history_reconciled
            and not inventory.clinical_intake_completed
            and not inventory.clinical_eligibility_established
            and not inventory.clinician_review_created
            and not inventory.patient_record_changed
            and not inventory.request_created
            and not inventory.queue_entered
            and not inventory.care_authorized
            and not inventory.prescribing_enabled
            and preparation.preparation_id=inventory.device_preparation_id
            and preparation.applicant_id=a.applicant_id
            and preparation.practice_id=a.practice_id
            and preparation.facility_id=a.facility_id
            and preparation.promotion_id=promotion.promotion_id
            and preparation.canonical_patient_id=promotion.canonical_patient_id
            and preparation.resulting_applicant_version=inventory.resulting_applicant_version-1
            and preparation.resulting_applicant_status='SyntheticDevicePreparationRecorded'
            and preparation.browser_supported
            and preparation.camera_available
            and preparation.microphone_available
            and preparation.speaker_available
            and preparation.network_quality in ('Unknown','Good')
            and preparation.client_reported_result_acknowledged
            and preparation.no_readiness_guarantee_acknowledged
            and preparation.recheck_before_consultation_acknowledged,
            false) as source_provenance_valid,
          (select count(*) from medications medication
             where lower(medication.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_medication_count,
          (select count(*) from prescriptions prescription
             where lower(prescription.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_prescription_count,
          receipt.receipt_id,receipt.resulting_applicant_version,
          receipt.resulting_applicant_status,receipt.additional_or_unlisted_items_reported,
          receipt.review_route,receipt.recorded_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_clinical_information_inventories inventory
          on inventory.applicant_id=a.applicant_id
        left join telehealth_applicant_device_preparations preparation
          on preparation.preparation_id=inventory.device_preparation_id
        left join telehealth_applicant_medication_information_receipts receipt
          on receipt.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantMedicationInformationState> GetAuthorizedAsync(
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
        var items = context.ReceiptId is null
            ? []
            : await LoadItemsAsync(connection, null, context.ReceiptId.Value, cancellationToken);
        return new(context, items);
    }

    public async Task<TelehealthApplicantMedicationInformationRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantMedicationInformation information,
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
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            RequireEligible(context, facilityId, allowRecorded: true);
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            var replayItems = await LoadItemsAsync(
                connection, transaction, replay.Value.Record.ReceiptId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record with { MedicationItems = replayItems };
        }

        RequireEligible(context, facilityId, allowRecorded: false);
        if (context.ApplicantVersion != information.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_medication_information_version_conflict",
                "The applicant changed. Reload the medication information before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                information.MedicationInformationSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_medication_information_snapshot_conflict",
                "The medication-information context changed. Reload it before recording the result.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticClinicalInformationInventoryRecorded';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantMedicationInformationPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", information.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_medication_information_version_conflict",
                    "The applicant changed. Reload the medication information before retrying.");
            }
        }

        var receiptId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_medication_information_receipts(
                  receipt_id,applicant_id,practice_id,facility_id,promotion_id,
                  canonical_patient_id,clinical_inventory_id,
                  registration_details_confirmation_id,insurance_handoff_confirmation_id,
                  safety_evaluation_id,communication_access_readiness_id,device_preparation_id,
                  resulting_applicant_version,resulting_applicant_status,
                  medication_information_snapshot_fingerprint,inventory_snapshot_fingerprint,
                  inventory_medications_status,selected_item_count,
                  additional_or_unlisted_items_reported,review_route,
                  patient_reported_may_be_incomplete_acknowledged,
                  synthetic_catalog_incomplete_acknowledged,
                  no_dose_or_directions_captured_acknowledged,
                  clinician_reconciliation_required_acknowledged,
                  catalog_key,catalog_version,coding_system,catalog_complete,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @receiptId,@applicantId,@practiceId,@facilityId,@promotionId,
                  @patientId,@inventoryId,@registrationId,@handoffId,@safetyId,@readinessId,
                  @preparationId,@nextVersion,@nextStatus,@snapshotFingerprint,
                  @inventoryFingerprint,@inventoryMedicationsStatus,@selectedItemCount,
                  @additionalItems,@reviewRoute,true,true,true,true,@catalogKey,
                  @catalogVersion,@codingSystem,false,@policyKey,@policyVersion,@evidenceType,
                  @applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning recorded_at;
                """;
            insert.Parameters.AddWithValue("receiptId", receiptId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("inventoryId", context.InventoryId!.Value);
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
                "nextStatus", TelehealthApplicantMedicationInformationPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue(
                "inventoryFingerprint", context.InventorySnapshotFingerprint!);
            insert.Parameters.AddWithValue(
                "inventoryMedicationsStatus", context.InventoryMedicationsStatus!);
            insert.Parameters.AddWithValue("selectedItemCount", information.MedicationItems.Count);
            insert.Parameters.AddWithValue(
                "additionalItems", information.AdditionalOrUnlistedItemsReported);
            insert.Parameters.AddWithValue("reviewRoute", information.ReviewRoute);
            insert.Parameters.AddWithValue(
                "catalogKey", SyntheticTelehealthApplicantMedicationCatalog.CatalogKey);
            insert.Parameters.AddWithValue(
                "catalogVersion", SyntheticTelehealthApplicantMedicationCatalog.CatalogVersion);
            insert.Parameters.AddWithValue(
                "codingSystem", SyntheticTelehealthApplicantMedicationCatalog.CodingSystem);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantMedicationInformationPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantMedicationInformationPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantMedicationInformationPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Synthetic medication-information receipt time is unavailable.");
            }
            recordedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        var responseItems = new List<TelehealthApplicantMedicationItemResponse>();
        for (var index = 0; index < information.MedicationItems.Count; index++)
        {
            var normalizedItem = information.MedicationItems[index];
            var catalogItem = normalizedItem.CatalogItem;
            await using var itemInsert = connection.CreateCommand();
            itemInsert.Transaction = transaction;
            itemInsert.CommandText = """
                insert into telehealth_applicant_reported_medication_items(
                  item_id,receipt_id,applicant_id,practice_id,facility_id,item_ordinal,
                  catalog_key,display_name,catalog_version,coding_system,rxnorm_mapped,
                  reported_use_status)
                values(@itemId,@receiptId,@applicantId,@practiceId,@facilityId,@ordinal,
                       @catalogKey,@displayName,@catalogVersion,@codingSystem,false,@useStatus);
                """;
            itemInsert.Parameters.AddWithValue("itemId", Guid.NewGuid());
            itemInsert.Parameters.AddWithValue("receiptId", receiptId);
            itemInsert.Parameters.AddWithValue("applicantId", applicantId);
            itemInsert.Parameters.AddWithValue("practiceId", practiceId);
            itemInsert.Parameters.AddWithValue("facilityId", facilityId);
            itemInsert.Parameters.AddWithValue("ordinal", index + 1);
            itemInsert.Parameters.AddWithValue("catalogKey", catalogItem.CatalogKey);
            itemInsert.Parameters.AddWithValue("displayName", catalogItem.DisplayName);
            itemInsert.Parameters.AddWithValue("catalogVersion", catalogItem.CatalogVersion);
            itemInsert.Parameters.AddWithValue("codingSystem", catalogItem.CodingSystem);
            itemInsert.Parameters.AddWithValue("useStatus", normalizedItem.ReportedUseStatus);
            await itemInsert.ExecuteNonQueryAsync(cancellationToken);
            responseItems.Add(new(
                catalogItem.CatalogKey,
                catalogItem.DisplayName,
                catalogItem.CatalogVersion,
                catalogItem.CodingSystem,
                catalogItem.RxNormMapped,
                normalizedItem.ReportedUseStatus));
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-medication-information-recorded',
                       'SyntheticClinicalInformationInventoryRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantMedicationInformationPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "medication-information:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            receiptId,
            applicantId,
            nextVersion,
            TelehealthApplicantMedicationInformationPolicy.ResultingStatus,
            snapshot.Fingerprint,
            responseItems,
            information.AdditionalOrUnlistedItemsReported,
            information.ReviewRoute,
            recordedAt);
    }

    public static TelehealthApplicantMedicationInformationSnapshot Snapshot(
        TelehealthApplicantMedicationInformationContext context) =>
        TelehealthApplicantMedicationInformationPolicy.Snapshot(
            context.InventoryId!.Value,
            context.InventorySnapshotFingerprint!,
            context.InventoryMedicationsStatus!,
            context.InventoryReviewRoute!);

    private static async Task<TelehealthApplicantMedicationInformationContext?> LoadAsync(
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
            NullableString(reader, 17), NullableString(reader, 18), NullableString(reader, 19),
            NullableGuid(reader, 20), NullableGuid(reader, 21), NullableGuid(reader, 22),
            NullableGuid(reader, 23), NullableGuid(reader, 24), reader.GetBoolean(25),
            reader.GetInt64(26), reader.GetInt64(27), NullableGuid(reader, 28),
            NullableInt32FromInt64(reader, 29), NullableString(reader, 30),
            NullableBoolean(reader, 31), NullableString(reader, 32),
            NullableDateTimeOffset(reader, 33));
    }

    private static async Task<(TelehealthApplicantMedicationInformationRecord Record,
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
            select receipt_id,applicant_id,resulting_applicant_version,
                   resulting_applicant_status,medication_information_snapshot_fingerprint,
                   additional_or_unlisted_items_reported,review_route,recorded_at,
                   command_fingerprint
            from telehealth_applicant_medication_information_receipts
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
            reader.GetString(3), reader.GetString(4), [], reader.GetBoolean(5),
            reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)),
            reader.GetString(8));
    }

    private static async Task<IReadOnlyList<TelehealthApplicantMedicationItemResponse>>
        LoadItemsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction? transaction,
            Guid receiptId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select catalog_key,display_name,catalog_version,coding_system,rxnorm_mapped,
                   reported_use_status
            from telehealth_applicant_reported_medication_items
            where receipt_id=@receiptId
            order by item_ordinal;
            """;
        command.Parameters.AddWithValue("receiptId", receiptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<TelehealthApplicantMedicationItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetBoolean(4), reader.GetString(5)));
        }
        return items;
    }

    private static void RequireEligible(
        TelehealthApplicantMedicationInformationContext context,
        int facilityId,
        bool allowRecorded)
    {
        var entry = context.ApplicantStatus == TelehealthApplicantMedicationInformationPolicy.EntryStatus
            && context.InventoryApplicantVersion == context.ApplicantVersion;
        var recorded = allowRecorded
            && context.ApplicantStatus == TelehealthApplicantMedicationInformationPolicy.ResultingStatus
            && context.ReceiptId is not null
            && context.ReceiptApplicantVersion == context.ApplicantVersion
            && context.ReceiptApplicantStatus == TelehealthApplicantMedicationInformationPolicy.ResultingStatus
            && context.InventoryApplicantVersion == context.ApplicantVersion - 1;
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
            || context.InventoryId is null
            || context.InventoryApplicantStatus != TelehealthApplicantMedicationInformationPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.InventorySnapshotFingerprint)
            || string.IsNullOrWhiteSpace(context.InventoryMedicationsStatus)
            || string.IsNullOrWhiteSpace(context.InventoryReviewRoute)
            || context.RegistrationDetailsConfirmationId is null
            || context.InsuranceHandoffConfirmationId is null
            || context.SafetyEvaluationId is null
            || context.CommunicationAccessReadinessId is null
            || context.DevicePreparationId is null
            || !context.SourceProvenanceValid
            || context.CanonicalMedicationCount != 0
            || context.CanonicalPrescriptionCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_medication_information_state_conflict",
                "The applicant is not eligible for this bounded synthetic medication-information receipt.");
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
                "telehealth_applicant_medication_information_idempotency_conflict",
                "The medication-information idempotency key was already used with different content.");
        }
    }
}
