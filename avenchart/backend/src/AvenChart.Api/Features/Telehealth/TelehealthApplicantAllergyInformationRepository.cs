// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantAllergyInformationContext(
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
    Guid? MedicationInformationId,
    int? MedicationApplicantVersion,
    string? MedicationApplicantStatus,
    string? MedicationInformationSnapshotFingerprint,
    string? MedicationReviewRoute,
    Guid? InventoryId,
    string? InventoryAllergiesOrIntolerancesStatus,
    Guid? RegistrationDetailsConfirmationId,
    Guid? InsuranceHandoffConfirmationId,
    Guid? SafetyEvaluationId,
    Guid? CommunicationAccessReadinessId,
    Guid? DevicePreparationId,
    bool SourceProvenanceValid,
    long CanonicalMedicationCount,
    long CanonicalPrescriptionCount,
    long CanonicalAllergyCount,
    Guid? ReceiptId,
    int? ReceiptApplicantVersion,
    string? ReceiptApplicantStatus,
    bool? AdditionalOrUnlistedItemsReported,
    string? AllergyReviewRoute,
    DateTimeOffset? RecordedAt);

public sealed record TelehealthApplicantAllergyInformationRecord(
    Guid ReceiptId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string AllergyInformationSnapshotFingerprint,
    IReadOnlyList<TelehealthApplicantAllergyItemResponse> AllergyItems,
    bool AdditionalOrUnlistedItemsReported,
    string ReviewRoute,
    DateTimeOffset RecordedAt);

public sealed record TelehealthApplicantAllergyInformationState(
    TelehealthApplicantAllergyInformationContext Context,
    IReadOnlyList<TelehealthApplicantAllergyItemResponse> AllergyItems);

public sealed class TelehealthApplicantAllergyInformationRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now() as database_now,
          a.facility_id as applicant_facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,patient.facility_id as patient_facility_id,
          patient.merged_into_patient_id,
          medication.receipt_id as medication_information_id,
          medication.resulting_applicant_version as medication_applicant_version,
          medication.resulting_applicant_status as medication_applicant_status,
          medication.medication_information_snapshot_fingerprint,
          medication.review_route as medication_review_route,
          inventory.inventory_id,inventory.allergies_or_intolerances_status,
          medication.registration_details_confirmation_id,
          medication.insurance_handoff_confirmation_id,medication.safety_evaluation_id,
          medication.communication_access_readiness_id,medication.device_preparation_id,
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
            and medication.applicant_id=a.applicant_id
            and medication.practice_id=a.practice_id
            and medication.facility_id=a.facility_id
            and medication.promotion_id=promotion.promotion_id
            and medication.canonical_patient_id=promotion.canonical_patient_id
            and medication.clinical_inventory_id=inventory.inventory_id
            and medication.resulting_applicant_status='SyntheticMedicationInformationRecorded'
            and medication.medication_information_snapshot_fingerprint ~ '^[0-9a-f]{64}$'
            and medication.catalog_key='avenchart-synthetic-applicant-medication-ingredients-2026-08'
            and medication.catalog_version=1
            and medication.coding_system='LOCAL_SYNTHETIC_ONLY'
            and not medication.catalog_complete
            and medication.policy_key='SYNTHETIC_APPLICANT_MEDICATION_INFORMATION'
            and medication.policy_version=1
            and medication.evidence_type='PROMOTED_PATIENT_MEDICATION_INFORMATION_RECEIPT'
            and medication.patient_reported_may_be_incomplete_acknowledged
            and medication.synthetic_catalog_incomplete_acknowledged
            and medication.no_dose_or_directions_captured_acknowledged
            and medication.clinician_reconciliation_required_acknowledged
            and not medication.medication_statement_created
            and not medication.medication_request_created
            and not medication.medication_list_reconciled
            and not medication.interaction_check_performed
            and not medication.clinician_review_created
            and not medication.clinical_intake_completed
            and not medication.clinical_eligibility_established
            and not medication.patient_record_changed
            and not medication.request_created
            and not medication.queue_entered
            and not medication.care_authorized
            and not medication.prescribing_enabled
            and (select count(*) from telehealth_applicant_reported_medication_items item
                 where item.receipt_id=medication.receipt_id)=medication.selected_item_count
            and inventory.applicant_id=a.applicant_id
            and inventory.practice_id=a.practice_id
            and inventory.facility_id=a.facility_id
            and inventory.promotion_id=promotion.promotion_id
            and inventory.canonical_patient_id=promotion.canonical_patient_id
            and inventory.resulting_applicant_status='SyntheticClinicalInformationInventoryRecorded'
            and inventory.allergies_or_intolerances_status in ('PatientReportsNone','ItemsToReview','Unsure')
            and inventory.policy_key='SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
            and inventory.policy_version=1
            and inventory.evidence_type='PROMOTED_PATIENT_CLINICAL_INFORMATION_INVENTORY_RECEIPT'
            and not inventory.allergy_list_reconciled
            and not inventory.clinical_intake_completed
            and not inventory.clinical_eligibility_established
            and not inventory.clinician_review_created
            and not inventory.patient_record_changed
            and not inventory.request_created
            and not inventory.queue_entered
            and not inventory.care_authorized
            and not inventory.prescribing_enabled,
            false) as source_provenance_valid,
          (select count(*) from medications canonical_medication
             where lower(canonical_medication.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_medication_count,
          (select count(*) from prescriptions canonical_prescription
             where lower(canonical_prescription.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_prescription_count,
          (select count(*) from allergies canonical_allergy
             where lower(canonical_allergy.patient_id)=lower(promotion.canonical_patient_id))
             as canonical_allergy_count,
          receipt.receipt_id,receipt.resulting_applicant_version as receipt_applicant_version,
          receipt.resulting_applicant_status as receipt_applicant_status,
          receipt.additional_or_unlisted_items_reported,
          receipt.review_route as allergy_review_route,receipt.recorded_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient
          on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_medication_information_receipts medication
          on medication.applicant_id=a.applicant_id
        left join telehealth_applicant_clinical_information_inventories inventory
          on inventory.inventory_id=medication.clinical_inventory_id
        left join telehealth_applicant_allergy_information_receipts receipt
          on receipt.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantAllergyInformationState> GetAuthorizedAsync(
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

    public async Task<TelehealthApplicantAllergyInformationRecord> RecordAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantAllergyInformation information,
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
            return replay.Value.Record with { AllergyItems = replayItems };
        }

        RequireEligible(context, facilityId, allowRecorded: false);
        if (context.ApplicantVersion != information.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_allergy_information_version_conflict",
                "The applicant changed. Reload the allergy information before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                information.AllergyInformationSnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_allergy_information_snapshot_conflict",
                "The allergy-information context changed. Reload it before recording the result.");
        }

        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticMedicationInformationRecorded';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantAllergyInformationPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", information.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_allergy_information_version_conflict",
                    "The applicant changed. Reload the allergy information before retrying.");
            }
        }

        var receiptId = Guid.NewGuid();
        DateTimeOffset recordedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_allergy_information_receipts(
                  receipt_id,applicant_id,practice_id,facility_id,promotion_id,
                  canonical_patient_id,clinical_inventory_id,medication_information_id,
                  registration_details_confirmation_id,insurance_handoff_confirmation_id,
                  safety_evaluation_id,communication_access_readiness_id,device_preparation_id,
                  resulting_applicant_version,resulting_applicant_status,
                  allergy_information_snapshot_fingerprint,
                  medication_information_snapshot_fingerprint,
                  inventory_allergies_or_intolerances_status,selected_item_count,
                  additional_or_unlisted_items_reported,review_route,
                  patient_reported_may_be_incomplete_acknowledged,
                  synthetic_catalog_incomplete_acknowledged,
                  no_reaction_or_criticality_captured_acknowledged,
                  clinician_verification_required_acknowledged,
                  catalog_key,catalog_version,coding_system,catalog_complete,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @receiptId,@applicantId,@practiceId,@facilityId,@promotionId,
                  @patientId,@inventoryId,@medicationId,@registrationId,@handoffId,
                  @safetyId,@readinessId,@preparationId,@nextVersion,@nextStatus,
                  @snapshotFingerprint,@medicationFingerprint,@inventoryAllergyStatus,
                  @selectedItemCount,@additionalItems,@reviewRoute,true,true,true,true,
                  @catalogKey,@catalogVersion,@codingSystem,false,@policyKey,@policyVersion,
                  @evidenceType,@applicantExpiresAt,@idempotencyKey,@commandFingerprint)
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
                "medicationId", context.MedicationInformationId!.Value);
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
                "nextStatus", TelehealthApplicantAllergyInformationPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue(
                "medicationFingerprint", context.MedicationInformationSnapshotFingerprint!);
            insert.Parameters.AddWithValue(
                "inventoryAllergyStatus", context.InventoryAllergiesOrIntolerancesStatus!);
            insert.Parameters.AddWithValue("selectedItemCount", information.AllergyItems.Count);
            insert.Parameters.AddWithValue(
                "additionalItems", information.AdditionalOrUnlistedItemsReported);
            insert.Parameters.AddWithValue("reviewRoute", information.ReviewRoute);
            insert.Parameters.AddWithValue(
                "catalogKey", SyntheticTelehealthApplicantAllergyCatalog.CatalogKey);
            insert.Parameters.AddWithValue(
                "catalogVersion", SyntheticTelehealthApplicantAllergyCatalog.CatalogVersion);
            insert.Parameters.AddWithValue(
                "codingSystem", SyntheticTelehealthApplicantAllergyCatalog.CodingSystem);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantAllergyInformationPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantAllergyInformationPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantAllergyInformationPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Synthetic allergy-information receipt time is unavailable.");
            }
            recordedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        var responseItems = new List<TelehealthApplicantAllergyItemResponse>();
        for (var index = 0; index < information.AllergyItems.Count; index++)
        {
            var catalogItem = information.AllergyItems[index];
            await using var itemInsert = connection.CreateCommand();
            itemInsert.Transaction = transaction;
            itemInsert.CommandText = """
                insert into telehealth_applicant_reported_allergy_items(
                  item_id,receipt_id,applicant_id,practice_id,facility_id,item_ordinal,
                  catalog_key,display_name,category,catalog_version,coding_system,
                  snomed_ct_mapped,rxnorm_mapped)
                values(@itemId,@receiptId,@applicantId,@practiceId,@facilityId,@ordinal,
                       @catalogKey,@displayName,@category,@catalogVersion,@codingSystem,
                       false,false);
                """;
            itemInsert.Parameters.AddWithValue("itemId", Guid.NewGuid());
            itemInsert.Parameters.AddWithValue("receiptId", receiptId);
            itemInsert.Parameters.AddWithValue("applicantId", applicantId);
            itemInsert.Parameters.AddWithValue("practiceId", practiceId);
            itemInsert.Parameters.AddWithValue("facilityId", facilityId);
            itemInsert.Parameters.AddWithValue("ordinal", index + 1);
            itemInsert.Parameters.AddWithValue("catalogKey", catalogItem.CatalogKey);
            itemInsert.Parameters.AddWithValue("displayName", catalogItem.DisplayName);
            itemInsert.Parameters.AddWithValue("category", catalogItem.Category);
            itemInsert.Parameters.AddWithValue("catalogVersion", catalogItem.CatalogVersion);
            itemInsert.Parameters.AddWithValue("codingSystem", catalogItem.CodingSystem);
            await itemInsert.ExecuteNonQueryAsync(cancellationToken);
            responseItems.Add(new(
                catalogItem.CatalogKey,
                catalogItem.DisplayName,
                catalogItem.Category,
                catalogItem.CatalogVersion,
                catalogItem.CodingSystem,
                catalogItem.SnomedCtMapped,
                catalogItem.RxNormMapped));
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_applicant_events(
                  event_id,applicant_id,aggregate_version,action,from_status,to_status,
                  actor_type,idempotency_key,command_fingerprint)
                values(@eventId,@applicantId,@nextVersion,
                       'prospective-allergy-information-recorded',
                       'SyntheticMedicationInformationRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantAllergyInformationPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "allergy-information:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            receiptId,
            applicantId,
            nextVersion,
            TelehealthApplicantAllergyInformationPolicy.ResultingStatus,
            snapshot.Fingerprint,
            responseItems,
            information.AdditionalOrUnlistedItemsReported,
            information.ReviewRoute,
            recordedAt);
    }

    public static TelehealthApplicantAllergyInformationSnapshot Snapshot(
        TelehealthApplicantAllergyInformationContext context) =>
        TelehealthApplicantAllergyInformationPolicy.Snapshot(
            context.MedicationInformationId!.Value,
            context.MedicationInformationSnapshotFingerprint!,
            context.InventoryAllergiesOrIntolerancesStatus!,
            context.MedicationReviewRoute!);

    private static async Task<TelehealthApplicantAllergyInformationContext?> LoadAsync(
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
            reader.GetGuid(reader.GetOrdinal("applicant_id")),
            Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("version"))),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetString(reader.GetOrdinal("access_key_hash")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("expires_at")),
            reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("database_now")),
            reader.GetInt32(reader.GetOrdinal("applicant_facility_id")),
            NullableGuid(reader, "promotion_id"),
            NullableString(reader, "outcome"),
            NullableBoolean(reader, "canonical_patient_created"),
            NullableString(reader, "canonical_patient_id"),
            NullableBoolean(reader, "portal_enabled"),
            NullableInt32(reader, "patient_facility_id"),
            NullableString(reader, "merged_into_patient_id"),
            NullableGuid(reader, "medication_information_id"),
            NullableInt32FromInt64(reader, "medication_applicant_version"),
            NullableString(reader, "medication_applicant_status"),
            NullableString(reader, "medication_information_snapshot_fingerprint"),
            NullableString(reader, "medication_review_route"),
            NullableGuid(reader, "inventory_id"),
            NullableString(reader, "allergies_or_intolerances_status"),
            NullableGuid(reader, "registration_details_confirmation_id"),
            NullableGuid(reader, "insurance_handoff_confirmation_id"),
            NullableGuid(reader, "safety_evaluation_id"),
            NullableGuid(reader, "communication_access_readiness_id"),
            NullableGuid(reader, "device_preparation_id"),
            reader.GetBoolean(reader.GetOrdinal("source_provenance_valid")),
            reader.GetInt64(reader.GetOrdinal("canonical_medication_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_prescription_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_allergy_count")),
            NullableGuid(reader, "receipt_id"),
            NullableInt32FromInt64(reader, "receipt_applicant_version"),
            NullableString(reader, "receipt_applicant_status"),
            NullableBoolean(reader, "additional_or_unlisted_items_reported"),
            NullableString(reader, "allergy_review_route"),
            NullableDateTimeOffset(reader, "recorded_at"));
    }

    private static async Task<(TelehealthApplicantAllergyInformationRecord Record,
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
                   resulting_applicant_status,allergy_information_snapshot_fingerprint,
                   additional_or_unlisted_items_reported,review_route,recorded_at,
                   command_fingerprint
            from telehealth_applicant_allergy_information_receipts
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

    private static async Task<IReadOnlyList<TelehealthApplicantAllergyItemResponse>>
        LoadItemsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction? transaction,
            Guid receiptId,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select catalog_key,display_name,category,catalog_version,coding_system,
                   snomed_ct_mapped,rxnorm_mapped
            from telehealth_applicant_reported_allergy_items
            where receipt_id=@receiptId
            order by item_ordinal;
            """;
        command.Parameters.AddWithValue("receiptId", receiptId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<TelehealthApplicantAllergyItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetString(4), reader.GetBoolean(5),
                reader.GetBoolean(6)));
        }
        return items;
    }

    private static void RequireEligible(
        TelehealthApplicantAllergyInformationContext context,
        int facilityId,
        bool allowRecorded)
    {
        var entry = context.ApplicantStatus == TelehealthApplicantAllergyInformationPolicy.EntryStatus
            && context.MedicationApplicantVersion == context.ApplicantVersion;
        var recorded = allowRecorded
            && context.ApplicantStatus == TelehealthApplicantAllergyInformationPolicy.ResultingStatus
            && context.ReceiptId is not null
            && context.ReceiptApplicantVersion == context.ApplicantVersion
            && context.ReceiptApplicantStatus == TelehealthApplicantAllergyInformationPolicy.ResultingStatus
            && context.MedicationApplicantVersion == context.ApplicantVersion - 1;
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
            || context.MedicationInformationId is null
            || context.MedicationApplicantStatus != TelehealthApplicantAllergyInformationPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.MedicationInformationSnapshotFingerprint)
            || string.IsNullOrWhiteSpace(context.MedicationReviewRoute)
            || context.InventoryId is null
            || string.IsNullOrWhiteSpace(context.InventoryAllergiesOrIntolerancesStatus)
            || context.RegistrationDetailsConfirmationId is null
            || context.InsuranceHandoffConfirmationId is null
            || context.SafetyEvaluationId is null
            || context.CommunicationAccessReadinessId is null
            || context.DevicePreparationId is null
            || !context.SourceProvenanceValid
            || context.CanonicalMedicationCount != 0
            || context.CanonicalPrescriptionCount != 0
            || context.CanonicalAllergyCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_allergy_information_state_conflict",
                "The applicant is not eligible for this bounded synthetic allergy-information receipt.");
        }
    }

    private static Guid? NullableGuid(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? NullableString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool? NullableBoolean(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private static int? NullableInt32(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static int? NullableInt32FromInt64(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetInt64(ordinal));
    }

    private static DateTimeOffset? NullableDateTimeOffset(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal);
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
                "telehealth_applicant_allergy_information_idempotency_conflict",
                "The allergy-information idempotency key was already used with different content.");
        }
    }
}
