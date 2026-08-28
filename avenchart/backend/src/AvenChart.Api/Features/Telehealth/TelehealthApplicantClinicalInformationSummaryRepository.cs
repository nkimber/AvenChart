// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantClinicalInformationSummaryContext(
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
    Guid? ClinicalInventoryId,
    string? MedicationsStatus,
    string? AllergiesOrIntolerancesStatus,
    string? OtherHealthHistoryStatus,
    Guid? MedicationInformationId,
    string? MedicationInformationSnapshotFingerprint,
    int? MedicationItemCount,
    bool? AdditionalMedicationItemsReported,
    string? MedicationReviewRoute,
    Guid? AllergyInformationId,
    string? AllergyInformationSnapshotFingerprint,
    int? AllergyItemCount,
    bool? AdditionalAllergyItemsReported,
    string? AllergyReviewRoute,
    Guid? HealthHistoryInformationId,
    int? HealthHistoryApplicantVersion,
    string? HealthHistoryApplicantStatus,
    string? HealthHistoryInformationSnapshotFingerprint,
    int? HealthHistoryTopicCount,
    bool? AdditionalHealthHistoryTopicsReported,
    string? HealthHistoryReviewRoute,
    bool SourceProvenanceValid,
    long CanonicalInsuranceCount,
    long CanonicalMedicationCount,
    long CanonicalPrescriptionCount,
    long CanonicalAllergyCount,
    long CanonicalProblemCount,
    Guid? ConfirmationId,
    int? ConfirmationApplicantVersion,
    string? ConfirmationApplicantStatus,
    string? SummaryRoute,
    DateTimeOffset? ConfirmedAt);

public sealed record TelehealthApplicantClinicalInformationSummaryRecord(
    Guid ConfirmationId,
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    string ClinicalInformationSummarySnapshotFingerprint,
    string SummaryRoute,
    DateTimeOffset ConfirmedAt);

public sealed class TelehealthApplicantClinicalInformationSummaryRepository(NpgsqlDataSource dataSource)
{
    private const string ContextProjection = """
        select
          a.applicant_id,a.version,a.status,a.access_key_hash,a.expires_at,now() as database_now,
          a.facility_id as applicant_facility_id,
          promotion.promotion_id,promotion.outcome,promotion.canonical_patient_created,
          promotion.canonical_patient_id,patient.portal_enabled,
          patient.facility_id as patient_facility_id,patient.merged_into_patient_id,
          inventory.inventory_id as clinical_inventory_id,
          inventory.medications_status,inventory.allergies_or_intolerances_status,
          inventory.other_health_history_status,
          medication.receipt_id as medication_information_id,
          medication.medication_information_snapshot_fingerprint,
          medication.selected_item_count as medication_item_count,
          medication.additional_or_unlisted_items_reported as additional_medication_items_reported,
          medication.review_route as medication_review_route,
          allergy.receipt_id as allergy_information_id,
          allergy.allergy_information_snapshot_fingerprint,
          allergy.selected_item_count as allergy_item_count,
          allergy.additional_or_unlisted_items_reported as additional_allergy_items_reported,
          allergy.review_route as allergy_review_route,
          history.receipt_id as health_history_information_id,
          history.resulting_applicant_version as health_history_applicant_version,
          history.resulting_applicant_status as health_history_applicant_status,
          history.health_history_information_snapshot_fingerprint,
          history.selected_topic_count as health_history_topic_count,
          history.additional_or_unlisted_topics_reported as additional_health_history_topics_reported,
          history.review_route as health_history_review_route,
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
            and medication.applicant_id=a.applicant_id
            and medication.practice_id=a.practice_id
            and medication.facility_id=a.facility_id
            and medication.promotion_id=promotion.promotion_id
            and medication.canonical_patient_id=promotion.canonical_patient_id
            and medication.clinical_inventory_id=inventory.inventory_id
            and medication.resulting_applicant_status='SyntheticMedicationInformationRecorded'
            and medication.inventory_medications_status=inventory.medications_status
            and (select count(*) from telehealth_applicant_reported_medication_items item
                 where item.receipt_id=medication.receipt_id)=medication.selected_item_count
            and allergy.applicant_id=a.applicant_id
            and allergy.practice_id=a.practice_id
            and allergy.facility_id=a.facility_id
            and allergy.promotion_id=promotion.promotion_id
            and allergy.canonical_patient_id=promotion.canonical_patient_id
            and allergy.clinical_inventory_id=inventory.inventory_id
            and allergy.medication_information_id=medication.receipt_id
            and allergy.resulting_applicant_status='SyntheticAllergyInformationRecorded'
            and allergy.inventory_allergies_or_intolerances_status=inventory.allergies_or_intolerances_status
            and (select count(*) from telehealth_applicant_reported_allergy_items item
                 where item.receipt_id=allergy.receipt_id)=allergy.selected_item_count
            and history.applicant_id=a.applicant_id
            and history.practice_id=a.practice_id
            and history.facility_id=a.facility_id
            and history.promotion_id=promotion.promotion_id
            and history.canonical_patient_id=promotion.canonical_patient_id
            and history.clinical_inventory_id=inventory.inventory_id
            and history.medication_information_id=medication.receipt_id
            and history.allergy_information_id=allergy.receipt_id
            and history.resulting_applicant_status='SyntheticHealthHistoryInformationRecorded'
            and history.inventory_other_health_history_status=inventory.other_health_history_status
            and (select count(*) from telehealth_applicant_reported_health_history_topics topic
                 where topic.receipt_id=history.receipt_id)=history.selected_topic_count
            and history.policy_key='SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION'
            and history.policy_version=1
            and history.evidence_type='PROMOTED_PATIENT_HEALTH_HISTORY_INFORMATION_RECEIPT'
            and not history.condition_created
            and not history.procedure_created
            and not history.observation_created
            and not history.family_member_history_created
            and not history.questionnaire_response_created
            and not history.health_history_reconciled
            and not history.risk_modifier_evaluated
            and not history.clinical_triage_changed
            and not history.clinician_review_created
            and not history.clinical_intake_completed
            and not history.clinical_eligibility_established
            and not history.patient_record_changed
            and not history.request_created
            and not history.queue_entered
            and not history.care_authorized
            and not history.prescribing_enabled,
            false) as source_provenance_valid,
          (select count(*) from insurance_records r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_insurance_count,
          (select count(*) from medications r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_medication_count,
          (select count(*) from prescriptions r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_prescription_count,
          (select count(*) from allergies r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_allergy_count,
          (select count(*) from problems r
             where lower(r.patient_id)=lower(promotion.canonical_patient_id)) as canonical_problem_count,
          confirmation.confirmation_id,
          confirmation.resulting_applicant_version as confirmation_applicant_version,
          confirmation.resulting_applicant_status as confirmation_applicant_status,
          confirmation.summary_route,confirmation.confirmed_at
        from telehealth_prospective_applicants a
        left join telehealth_applicant_synthetic_promotions promotion
          on promotion.applicant_id=a.applicant_id
        left join patients patient on patient.canonical_id=promotion.canonical_patient_id
        left join telehealth_applicant_health_history_information_receipts history
          on history.applicant_id=a.applicant_id
        left join telehealth_applicant_clinical_information_inventories inventory
          on inventory.inventory_id=history.clinical_inventory_id
        left join telehealth_applicant_medication_information_receipts medication
          on medication.receipt_id=history.medication_information_id
        left join telehealth_applicant_allergy_information_receipts allergy
          on allergy.receipt_id=history.allergy_information_id
        left join telehealth_applicant_clinical_information_summary_confirmations confirmation
          on confirmation.applicant_id=a.applicant_id
        """;

    public async Task<TelehealthApplicantClinicalInformationSummaryContext> GetAuthorizedAsync(
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

    public async Task<TelehealthApplicantClinicalInformationSummaryRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantClinicalInformationSummaryConfirmation confirmation,
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
            RequireEligible(context, facilityId, allowConfirmed: true);
            RequireReplayFingerprint(replay.Value.CommandFingerprint, commandFingerprint);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        RequireEligible(context, facilityId, allowConfirmed: false);
        if (context.ApplicantVersion != confirmation.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_summary_version_conflict",
                "The applicant changed. Reload the clinical-information summary before retrying.");
        }

        var snapshot = Snapshot(context);
        if (!string.Equals(
                snapshot.Fingerprint,
                confirmation.ClinicalInformationSummarySnapshotFingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_summary_snapshot_conflict",
                "The clinical-information summary changed. Reload it before confirming.");
        }

        var summaryRoute = SummaryRoute(context);
        var nextVersion = context.ApplicantVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_prospective_applicants
                set status=@status,version=@nextVersion,updated_at=now()
                where applicant_id=@applicantId and version=@expectedVersion
                  and status='SyntheticHealthHistoryInformationRecorded';
                """;
            update.Parameters.AddWithValue(
                "status", TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus);
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("applicantId", applicantId);
            update.Parameters.AddWithValue("expectedVersion", confirmation.ExpectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_clinical_information_summary_version_conflict",
                    "The applicant changed. Reload the clinical-information summary before retrying.");
            }
        }

        var confirmationId = Guid.NewGuid();
        DateTimeOffset confirmedAt;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_applicant_clinical_information_summary_confirmations(
                  confirmation_id,applicant_id,practice_id,facility_id,promotion_id,
                  canonical_patient_id,clinical_inventory_id,medication_information_id,
                  allergy_information_id,health_history_information_id,
                  resulting_applicant_version,resulting_applicant_status,
                  clinical_information_summary_snapshot_fingerprint,
                  medication_information_snapshot_fingerprint,
                  allergy_information_snapshot_fingerprint,
                  health_history_information_snapshot_fingerprint,
                  medications_status,allergies_or_intolerances_status,other_health_history_status,
                  medication_item_count,allergy_item_count,health_history_topic_count,
                  additional_medication_items_reported,additional_allergy_items_reported,
                  additional_health_history_topics_reported,medication_review_route,
                  allergy_review_route,health_history_review_route,summary_route,
                  patient_reported_may_be_incomplete_acknowledged,
                  not_clinically_verified_or_reconciled_acknowledged,
                  no_intake_completion_or_eligibility_acknowledged,
                  correction_requires_separate_workflow_acknowledged,
                  policy_key,policy_version,evidence_type,applicant_expires_at,
                  idempotency_key,command_fingerprint)
                values(
                  @confirmationId,@applicantId,@practiceId,@facilityId,@promotionId,
                  @patientId,@inventoryId,@medicationId,@allergyId,@historyId,
                  @nextVersion,@nextStatus,@summaryFingerprint,@medicationFingerprint,
                  @allergyFingerprint,@historyFingerprint,@medicationsStatus,@allergiesStatus,
                  @historyStatus,@medicationCount,@allergyCount,@historyCount,
                  @additionalMedications,@additionalAllergies,@additionalHistory,
                  @medicationRoute,@allergyRoute,@historyRoute,@summaryRoute,
                  true,true,true,true,@policyKey,@policyVersion,@evidenceType,
                  @applicantExpiresAt,@idempotencyKey,@commandFingerprint)
                returning confirmed_at;
                """;
            insert.Parameters.AddWithValue("confirmationId", confirmationId);
            insert.Parameters.AddWithValue("applicantId", applicantId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue("promotionId", context.PromotionId!.Value);
            insert.Parameters.AddWithValue("patientId", context.CanonicalPatientId!);
            insert.Parameters.AddWithValue("inventoryId", context.ClinicalInventoryId!.Value);
            insert.Parameters.AddWithValue("medicationId", context.MedicationInformationId!.Value);
            insert.Parameters.AddWithValue("allergyId", context.AllergyInformationId!.Value);
            insert.Parameters.AddWithValue("historyId", context.HealthHistoryInformationId!.Value);
            insert.Parameters.AddWithValue("nextVersion", nextVersion);
            insert.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus);
            insert.Parameters.AddWithValue("summaryFingerprint", snapshot.Fingerprint);
            insert.Parameters.AddWithValue(
                "medicationFingerprint", context.MedicationInformationSnapshotFingerprint!);
            insert.Parameters.AddWithValue(
                "allergyFingerprint", context.AllergyInformationSnapshotFingerprint!);
            insert.Parameters.AddWithValue(
                "historyFingerprint", context.HealthHistoryInformationSnapshotFingerprint!);
            insert.Parameters.AddWithValue("medicationsStatus", context.MedicationsStatus!);
            insert.Parameters.AddWithValue("allergiesStatus", context.AllergiesOrIntolerancesStatus!);
            insert.Parameters.AddWithValue("historyStatus", context.OtherHealthHistoryStatus!);
            insert.Parameters.AddWithValue("medicationCount", context.MedicationItemCount!.Value);
            insert.Parameters.AddWithValue("allergyCount", context.AllergyItemCount!.Value);
            insert.Parameters.AddWithValue("historyCount", context.HealthHistoryTopicCount!.Value);
            insert.Parameters.AddWithValue(
                "additionalMedications", context.AdditionalMedicationItemsReported!.Value);
            insert.Parameters.AddWithValue(
                "additionalAllergies", context.AdditionalAllergyItemsReported!.Value);
            insert.Parameters.AddWithValue(
                "additionalHistory", context.AdditionalHealthHistoryTopicsReported!.Value);
            insert.Parameters.AddWithValue("medicationRoute", context.MedicationReviewRoute!);
            insert.Parameters.AddWithValue("allergyRoute", context.AllergyReviewRoute!);
            insert.Parameters.AddWithValue("historyRoute", context.HealthHistoryReviewRoute!);
            insert.Parameters.AddWithValue("summaryRoute", summaryRoute);
            insert.Parameters.AddWithValue(
                "policyKey", TelehealthApplicantClinicalInformationSummaryPolicy.PolicyKey);
            insert.Parameters.AddWithValue(
                "policyVersion", TelehealthApplicantClinicalInformationSummaryPolicy.PolicyVersion);
            insert.Parameters.AddWithValue(
                "evidenceType", TelehealthApplicantClinicalInformationSummaryPolicy.EvidenceType);
            insert.Parameters.AddWithValue("applicantExpiresAt", context.ApplicantExpiresAt);
            insert.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insert.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Synthetic clinical-information summary confirmation time is unavailable.");
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
                       'prospective-clinical-information-summary-confirmed',
                       'SyntheticHealthHistoryInformationRecorded',@nextStatus,'applicant',
                       @eventIdempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("applicantId", applicantId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue(
                "nextStatus", TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus);
            eventCommand.Parameters.AddWithValue(
                "eventIdempotencyKey",
                "clinical-information-summary:" + TelehealthCommandFingerprint.Create(idempotencyKey));
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            confirmationId,
            applicantId,
            nextVersion,
            TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus,
            snapshot.Fingerprint,
            summaryRoute,
            confirmedAt);
    }

    public static TelehealthApplicantClinicalInformationSummarySnapshot Snapshot(
        TelehealthApplicantClinicalInformationSummaryContext context) =>
        TelehealthApplicantClinicalInformationSummaryPolicy.Snapshot(
            context.ClinicalInventoryId!.Value,
            context.MedicationsStatus!,
            context.AllergiesOrIntolerancesStatus!,
            context.OtherHealthHistoryStatus!,
            context.MedicationInformationId!.Value,
            context.MedicationInformationSnapshotFingerprint!,
            context.MedicationItemCount!.Value,
            context.AdditionalMedicationItemsReported!.Value,
            context.MedicationReviewRoute!,
            context.AllergyInformationId!.Value,
            context.AllergyInformationSnapshotFingerprint!,
            context.AllergyItemCount!.Value,
            context.AdditionalAllergyItemsReported!.Value,
            context.AllergyReviewRoute!,
            context.HealthHistoryInformationId!.Value,
            context.HealthHistoryInformationSnapshotFingerprint!,
            context.HealthHistoryTopicCount!.Value,
            context.AdditionalHealthHistoryTopicsReported!.Value,
            context.HealthHistoryReviewRoute!);

    public static string SummaryRoute(TelehealthApplicantClinicalInformationSummaryContext context) =>
        TelehealthApplicantClinicalInformationSummaryPolicy.DetermineSummaryRoute(
            context.MedicationsStatus!,
            context.AllergiesOrIntolerancesStatus!,
            context.OtherHealthHistoryStatus!,
            context.AdditionalMedicationItemsReported!.Value,
            context.AdditionalAllergyItemsReported!.Value,
            context.AdditionalHealthHistoryTopicsReported!.Value);

    private static async Task<TelehealthApplicantClinicalInformationSummaryContext?> LoadAsync(
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
            NullableGuid(reader, "clinical_inventory_id"),
            NullableString(reader, "medications_status"),
            NullableString(reader, "allergies_or_intolerances_status"),
            NullableString(reader, "other_health_history_status"),
            NullableGuid(reader, "medication_information_id"),
            NullableString(reader, "medication_information_snapshot_fingerprint"),
            NullableInt32(reader, "medication_item_count"),
            NullableBoolean(reader, "additional_medication_items_reported"),
            NullableString(reader, "medication_review_route"),
            NullableGuid(reader, "allergy_information_id"),
            NullableString(reader, "allergy_information_snapshot_fingerprint"),
            NullableInt32(reader, "allergy_item_count"),
            NullableBoolean(reader, "additional_allergy_items_reported"),
            NullableString(reader, "allergy_review_route"),
            NullableGuid(reader, "health_history_information_id"),
            NullableInt32FromInt64(reader, "health_history_applicant_version"),
            NullableString(reader, "health_history_applicant_status"),
            NullableString(reader, "health_history_information_snapshot_fingerprint"),
            NullableInt32(reader, "health_history_topic_count"),
            NullableBoolean(reader, "additional_health_history_topics_reported"),
            NullableString(reader, "health_history_review_route"),
            reader.GetBoolean(reader.GetOrdinal("source_provenance_valid")),
            reader.GetInt64(reader.GetOrdinal("canonical_insurance_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_medication_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_prescription_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_allergy_count")),
            reader.GetInt64(reader.GetOrdinal("canonical_problem_count")),
            NullableGuid(reader, "confirmation_id"),
            NullableInt32FromInt64(reader, "confirmation_applicant_version"),
            NullableString(reader, "confirmation_applicant_status"),
            NullableString(reader, "summary_route"),
            NullableDateTimeOffset(reader, "confirmed_at"));
    }

    private static async Task<(TelehealthApplicantClinicalInformationSummaryRecord Record,
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
                   resulting_applicant_status,clinical_information_summary_snapshot_fingerprint,
                   summary_route,confirmed_at,command_fingerprint
            from telehealth_applicant_clinical_information_summary_confirmations
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
            reader.GetFieldValue<DateTimeOffset>(6)), reader.GetString(7));
    }

    private static void RequireEligible(
        TelehealthApplicantClinicalInformationSummaryContext context,
        int facilityId,
        bool allowConfirmed)
    {
        var entry = context.ApplicantStatus == TelehealthApplicantClinicalInformationSummaryPolicy.EntryStatus
            && context.HealthHistoryApplicantVersion == context.ApplicantVersion;
        var confirmed = allowConfirmed
            && context.ApplicantStatus == TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus
            && context.ConfirmationId is not null
            && context.ConfirmationApplicantVersion == context.ApplicantVersion
            && context.ConfirmationApplicantStatus == TelehealthApplicantClinicalInformationSummaryPolicy.ResultingStatus
            && context.HealthHistoryApplicantVersion == context.ApplicantVersion - 1;
        if ((!entry && !confirmed)
            || context.ApplicantExpiresAt <= context.DatabaseNow
            || context.ApplicantFacilityId != facilityId
            || context.PromotionOutcome != "SyntheticPatientCreated"
            || context.CanonicalPatientCreated is not true
            || context.PromotionId is null
            || string.IsNullOrWhiteSpace(context.CanonicalPatientId)
            || context.PatientPortalEnabled is not false
            || context.PatientFacilityId != facilityId
            || context.MergedIntoPatientId is not null
            || context.ClinicalInventoryId is null
            || string.IsNullOrWhiteSpace(context.MedicationsStatus)
            || string.IsNullOrWhiteSpace(context.AllergiesOrIntolerancesStatus)
            || string.IsNullOrWhiteSpace(context.OtherHealthHistoryStatus)
            || context.MedicationInformationId is null
            || string.IsNullOrWhiteSpace(context.MedicationInformationSnapshotFingerprint)
            || context.MedicationItemCount is null
            || context.AdditionalMedicationItemsReported is null
            || string.IsNullOrWhiteSpace(context.MedicationReviewRoute)
            || context.AllergyInformationId is null
            || string.IsNullOrWhiteSpace(context.AllergyInformationSnapshotFingerprint)
            || context.AllergyItemCount is null
            || context.AdditionalAllergyItemsReported is null
            || string.IsNullOrWhiteSpace(context.AllergyReviewRoute)
            || context.HealthHistoryInformationId is null
            || context.HealthHistoryApplicantStatus != TelehealthApplicantClinicalInformationSummaryPolicy.EntryStatus
            || string.IsNullOrWhiteSpace(context.HealthHistoryInformationSnapshotFingerprint)
            || context.HealthHistoryTopicCount is null
            || context.AdditionalHealthHistoryTopicsReported is null
            || string.IsNullOrWhiteSpace(context.HealthHistoryReviewRoute)
            || !context.SourceProvenanceValid
            || context.CanonicalInsuranceCount != 0
            || context.CanonicalMedicationCount != 0
            || context.CanonicalPrescriptionCount != 0
            || context.CanonicalAllergyCount != 0
            || context.CanonicalProblemCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_clinical_information_summary_state_conflict",
                "The applicant is not eligible for this bounded synthetic clinical-information summary confirmation.");
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
                "telehealth_applicant_clinical_information_summary_idempotency_conflict",
                "The clinical-information summary idempotency key was already used with different content.");
        }
    }
}
