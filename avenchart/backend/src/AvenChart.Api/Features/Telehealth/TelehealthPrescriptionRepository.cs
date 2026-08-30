// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthPrescriptionRepository(
    NpgsqlDataSource dataSource,
    ITelehealthPrescriptionSafetyGateway safetyGateway,
    IEPrescriptionGateway ePrescriptionGateway)
{
    public const string CatalogSource = "AvenChartSyntheticMedicationVocabulary";
    public const string AdapterMode = "NON_PRODUCTION";
    public const string CanonicalModelVersion = "AVENCHART_ERX_PREPARATION_V1";
    public const string IntendedStandard = "NCPDP_SCRIPT_2017071";

    public async Task<TelehealthPrescriptionPreparationWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        string? query,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var owner = await ReadOwnerAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            lockAggregate: false,
            cancellationToken);
        if (owner is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var currentPharmacyChoiceVersion = await ReadCurrentPharmacyChoiceVersionAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        var currentDraft = await ReadCurrentDraftAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        var currentSignedPrescription = await ReadCurrentSignedPrescriptionAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        var catalogResults = await SearchCatalogAsync(
            connection,
            transaction,
            query,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TelehealthPrescriptionPreparationWorkspaceResponse(
            consultationId,
            owner.ConsultationStatus,
            owner.DatabaseNow,
            CatalogSource,
            owner.DatasetId,
            owner.DatasetVersion,
            AdapterMode,
            CanonicalModelVersion,
            IntendedStandard,
            currentPharmacyChoiceVersion,
            catalogResults,
            currentDraft,
            currentSignedPrescription,
            SafetyCheckEnabled: currentDraft is not null && currentSignedPrescription is null,
            SigningEnabled: currentDraft is not null && currentSignedPrescription is null,
            PrescriptionCreationEnabled: currentDraft is not null && currentSignedPrescription is null,
            TransmissionEnabled: false,
            PatientDeliveryEnabled: false,
            CompletionEnabled: false,
            Limitations:
            [
                "Catalog results are deterministic synthetic reference facts, not drug or dosing recommendations.",
                "The conservative synthetic safety gate permits signing only when both canonical medication and allergy lists are empty and the physician reconfirms both facts.",
                "A signed synthetic prescription has no legal effect; the NCPDP SCRIPT preparation is uncertified and contacts no pharmacy or network.",
                "Transmission, patient delivery, and consultation completion remain unavailable."
            ]);
    }

    public async Task<TelehealthPrescriptionPreparationDraftResponse?> RecordAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        RecordTelehealthPrescriptionPreparationDraftRequest request,
        string actor,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var owner = await ReadOwnerAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            lockAggregate: true,
            cancellationToken);
        if (owner is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var replay = await ReadReplayAsync(
            connection,
            transaction,
            consultationId,
            idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw new TelehealthPrescriptionDraftConflictException(
                    "The idempotency key was already used for different prescription-preparation content.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Draft;
        }

        var currentVersion = await ReadCurrentVersionAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        if (request.ExpectedVersion != currentVersion)
        {
            throw new TelehealthPrescriptionDraftConflictException(
                $"The current prescription-preparation draft is version {currentVersion}. Reload before saving.");
        }

        var pharmacyChoiceVersion = await ReadCurrentPharmacyChoiceVersionAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken)
            ?? throw new ArgumentException(
                "A current patient-confirmed synthetic pharmacy choice is required before recording a prescription-preparation draft.");
        var catalog = await ReadPermittedCatalogItemAsync(
            connection,
            transaction,
            request.RxNormCode,
            cancellationToken)
            ?? throw new ArgumentException(
                "The selected medication catalog item is unknown, inactive, or controlled and cannot be used in this synthetic slice.");

        var nextVersion = currentVersion + 1;
        var draftVersionId = Guid.NewGuid();
        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into telehealth_consultation_prescription_draft_versions (
                      prescription_draft_version_id,consultation_id,encounter_id,version,rx_norm_code,
                      drug_name_snapshot,display_name_snapshot,form_snapshot,strength_snapshot,route_snapshot,
                      controlled_substance_schedule_snapshot,dose_amount,dose_unit,frequency,quantity_value,
                      quantity_unit,duration_days,refills,indication,directions,medication_list_reviewed,
                      allergy_list_reviewed,adequate_evaluation_completed,pharmacy_choice_version,catalog_source,
                      catalog_dataset_id,catalog_dataset_version,canonical_model_version,intended_standard,adapter_mode,
                      legal_effect,safety_checked,signed,transmission_queued,transmitted,patient_delivered,
                      recorded_at,recorded_by_staff_id)
                    values (
                      @draftVersionId,@consultationId,@encounterId,@version,@rxNormCode,@drugName,@displayName,
                      @form,@strength,@route,null,@doseAmount,@doseUnit,@frequency,@quantityValue,@quantityUnit,
                      @durationDays,@refills,@indication,@directions,@medicationReviewed,@allergyReviewed,@adequate,
                      @pharmacyChoiceVersion,@catalogSource,@datasetId,@datasetVersion,@canonicalModel,@standard,@adapter,
                      false,false,false,false,false,false,@now,@physician);
                    """;
                insert.Parameters.AddWithValue("draftVersionId", draftVersionId);
                insert.Parameters.AddWithValue("consultationId", consultationId);
                insert.Parameters.AddWithValue("encounterId", owner.EncounterId);
                insert.Parameters.AddWithValue("version", nextVersion);
                insert.Parameters.Add("rxNormCode", NpgsqlDbType.Text).Value = catalog.RxNormCode;
                insert.Parameters.Add("drugName", NpgsqlDbType.Text).Value = catalog.DrugName;
                insert.Parameters.Add("displayName", NpgsqlDbType.Text).Value = catalog.DisplayName;
                insert.Parameters.Add("form", NpgsqlDbType.Text).Value = catalog.Form;
                insert.Parameters.Add("strength", NpgsqlDbType.Text).Value = catalog.Strength;
                insert.Parameters.Add("route", NpgsqlDbType.Text).Value = catalog.Route;
                insert.Parameters.AddWithValue("doseAmount", request.DoseAmount);
                insert.Parameters.Add("doseUnit", NpgsqlDbType.Text).Value = request.DoseUnit;
                insert.Parameters.Add("frequency", NpgsqlDbType.Text).Value = request.Frequency;
                insert.Parameters.AddWithValue("quantityValue", request.QuantityValue);
                insert.Parameters.Add("quantityUnit", NpgsqlDbType.Text).Value = request.QuantityUnit;
                insert.Parameters.AddWithValue("durationDays", request.DurationDays);
                insert.Parameters.AddWithValue("refills", request.Refills);
                insert.Parameters.Add("indication", NpgsqlDbType.Text).Value = request.Indication;
                insert.Parameters.Add("directions", NpgsqlDbType.Text).Value = request.Directions;
                insert.Parameters.AddWithValue("medicationReviewed", request.MedicationListReviewed);
                insert.Parameters.AddWithValue("allergyReviewed", request.AllergyListReviewed);
                insert.Parameters.AddWithValue("adequate", request.AdequateEvaluationCompleted);
                insert.Parameters.AddWithValue("pharmacyChoiceVersion", pharmacyChoiceVersion);
                insert.Parameters.Add("catalogSource", NpgsqlDbType.Text).Value = CatalogSource;
                insert.Parameters.Add("datasetId", NpgsqlDbType.Text).Value = owner.DatasetId;
                insert.Parameters.Add("datasetVersion", NpgsqlDbType.Text).Value = owner.DatasetVersion;
                insert.Parameters.Add("canonicalModel", NpgsqlDbType.Text).Value = CanonicalModelVersion;
                insert.Parameters.Add("standard", NpgsqlDbType.Text).Value = IntendedStandard;
                insert.Parameters.Add("adapter", NpgsqlDbType.Text).Value = AdapterMode;
                insert.Parameters.AddWithValue("now", owner.DatabaseNow);
                insert.Parameters.AddWithValue("physician", physicianStaffId);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertEvent = connection.CreateCommand())
            {
                insertEvent.Transaction = transaction;
                insertEvent.CommandText = """
                    insert into telehealth_consultation_prescription_draft_events (
                      event_id,consultation_id,prescription_draft_version_id,aggregate_version,action,actor_type,
                      actor_id,idempotency_key,command_fingerprint,occurred_at)
                    values (@eventId,@consultationId,@draftVersionId,@version,@action,'physician',@actor,
                            @idempotencyKey,@fingerprint,@now);
                    """;
                insertEvent.Parameters.AddWithValue("eventId", Guid.NewGuid());
                insertEvent.Parameters.AddWithValue("consultationId", consultationId);
                insertEvent.Parameters.AddWithValue("draftVersionId", draftVersionId);
                insertEvent.Parameters.AddWithValue("version", nextVersion);
                insertEvent.Parameters.Add("action", NpgsqlDbType.Text).Value =
                    nextVersion == 1 ? "DraftRecorded" : "DraftRevised";
                insertEvent.Parameters.Add("actor", NpgsqlDbType.Text).Value = actor;
                insertEvent.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
                insertEvent.Parameters.Add("fingerprint", NpgsqlDbType.Char).Value = commandFingerprint;
                insertEvent.Parameters.AddWithValue("now", owner.DatabaseNow);
                await insertEvent.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new TelehealthPrescriptionDraftConflictException(
                "The prescription-preparation draft changed concurrently. Reload before saving.");
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new ArgumentException(
                "The medication catalog item is no longer permitted for this synthetic prescription-preparation draft.",
                exception);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToDraftResponse(
            nextVersion,
            catalog,
            request,
            pharmacyChoiceVersion,
            owner.DatabaseNow);
    }

    public async Task<TelehealthSignedPrescriptionResponse?> SignAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        SignTelehealthPrescriptionRequest request,
        string actor,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var owner = await ReadOwnerAsync(
            connection, transaction, practiceId, facilityId, physicianStaffId,
            consultationId, lockAggregate: true, cancellationToken);
        if (owner is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var replay = await ReadSignedPrescriptionReplayAsync(
            connection, transaction, consultationId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw new TelehealthPrescriptionDraftConflictException(
                    "The idempotency key was already used for different prescription-signing content.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Prescription;
        }

        if (await ReadCurrentSignedPrescriptionAsync(connection, transaction, consultationId, cancellationToken) is not null)
        {
            throw new TelehealthPrescriptionDraftConflictException(
                "A signed synthetic prescription already exists for this consultation.");
        }

        var source = await ReadSigningSourceAsync(connection, transaction, consultationId, cancellationToken)
            ?? throw new ArgumentException("A current prescription-preparation draft and patient-confirmed pharmacy choice are required.");
        if (source.DraftVersion != request.ExpectedDraftVersion)
        {
            throw new TelehealthPrescriptionDraftConflictException(
                $"The current prescription-preparation draft is version {source.DraftVersion}. Reload before signing.");
        }
        if (source.PharmacyChoiceVersion != source.DraftPharmacyChoiceVersion)
        {
            throw new ArgumentException("The patient-confirmed pharmacy choice changed after this draft was recorded. Record a revised draft before signing.");
        }

        var (activeMedicationCount, activeAllergyCount) = await ReadActiveClinicalListCountsAsync(
            connection, transaction, owner.PatientId, cancellationToken);
        var safety = safetyGateway.Evaluate(new TelehealthPrescriptionSafetyInput(
            activeMedicationCount,
            activeAllergyCount,
            request.NoCurrentMedicationsConfirmed,
            request.NoKnownAllergiesConfirmed));
        if (!safety.Passed)
        {
            throw new ArgumentException(
                $"The conservative synthetic medication-safety gate requires clinician resolution: {string.Join(", ", safety.Findings)}.");
        }

        var transmission = ePrescriptionGateway.PrepareNewRx();
        if (transmission.AdapterMode != SyntheticEPrescriptionGateway.AdapterMode
            || transmission.TargetStandard != SyntheticEPrescriptionGateway.TargetStandard
            || transmission.Certified
            || transmission.ExternalDestinationContacted)
        {
            throw new InvalidOperationException("The non-production e-prescription gateway returned an unsafe capability state.");
        }

        var orderId = Guid.NewGuid();
        var prescriptionId = $"RX-TELEHEALTH-{Guid.NewGuid():N}";
        var legacyTimestamp = DateTime.SpecifyKind(owner.DatabaseNow.UtcDateTime, DateTimeKind.Unspecified);
        var contentHash = TelehealthCommandFingerprint.Create(
            consultationId, owner.EncounterId, owner.PatientId, physicianStaffId,
            source.DraftVersionId, source.DraftVersion, source.RxNormCode, source.DrugName,
            source.DoseAmount, source.DoseUnit, source.Frequency, source.QuantityValue,
            source.QuantityUnit, source.DurationDays, source.Refills, source.Indication,
            source.Directions, source.PharmacyChoiceVersion, source.DirectoryEntryId,
            source.PharmacyName, transmission.CanonicalModelVersion, transmission.TargetStandard,
            safety.Outcome, safety.RulesetVersion, owner.DatabaseNow.ToString("O"));

        await using (var insertPrescription = connection.CreateCommand())
        {
            insertPrescription.Transaction = transaction;
            insertPrescription.CommandText = """
                insert into prescriptions (
                  id,patient_id,pid,provider_id,encounter,start_date,date_added,modified_date,drug,rx_norm_code,
                  dosage,quantity,dose_amount,dose_unit,frequency,duration_days,route,refills,diagnosis,note,active,
                  pharmacy_name,erx_uploaded)
                values (
                  @id,@patientId,@pid,@provider,@encounter,@date,@timestamp,@date,@drug,@rxNorm,
                  @dosage,@quantity,@doseAmount,@doseUnit,@frequency,@duration,@route,@refills,@diagnosis,@note,1,
                  @pharmacyName,0);
                """;
            insertPrescription.Parameters.Add("id", NpgsqlDbType.Text).Value = prescriptionId;
            insertPrescription.Parameters.Add("patientId", NpgsqlDbType.Text).Value = owner.PatientId;
            insertPrescription.Parameters.AddWithValue("pid", owner.LegacyPid);
            insertPrescription.Parameters.AddWithValue("provider", physicianStaffId);
            insertPrescription.Parameters.AddWithValue("encounter", owner.EncounterId);
            insertPrescription.Parameters.Add("date", NpgsqlDbType.Date).Value = DateOnly.FromDateTime(owner.DatabaseNow.UtcDateTime);
            insertPrescription.Parameters.Add("timestamp", NpgsqlDbType.Timestamp).Value = legacyTimestamp;
            insertPrescription.Parameters.Add("drug", NpgsqlDbType.Text).Value = source.DrugName;
            insertPrescription.Parameters.Add("rxNorm", NpgsqlDbType.Text).Value = source.RxNormCode;
            insertPrescription.Parameters.Add("dosage", NpgsqlDbType.Text).Value = $"{source.DoseAmount} {source.DoseUnit}";
            insertPrescription.Parameters.Add("quantity", NpgsqlDbType.Text).Value = $"{source.QuantityValue} {source.QuantityUnit}";
            insertPrescription.Parameters.AddWithValue("doseAmount", source.DoseAmount);
            insertPrescription.Parameters.Add("doseUnit", NpgsqlDbType.Text).Value = source.DoseUnit;
            insertPrescription.Parameters.Add("frequency", NpgsqlDbType.Text).Value = source.Frequency;
            insertPrescription.Parameters.AddWithValue("duration", source.DurationDays);
            insertPrescription.Parameters.Add("route", NpgsqlDbType.Text).Value = source.Route;
            insertPrescription.Parameters.AddWithValue("refills", source.Refills);
            insertPrescription.Parameters.Add("diagnosis", NpgsqlDbType.Text).Value = source.Indication;
            insertPrescription.Parameters.Add("note", NpgsqlDbType.Text).Value = source.Directions;
            insertPrescription.Parameters.Add("pharmacyName", NpgsqlDbType.Text).Value = source.PharmacyName;
            await insertPrescription.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertOrder = connection.CreateCommand())
        {
            insertOrder.Transaction = transaction;
            insertOrder.CommandText = """
                insert into telehealth_consultation_prescription_orders (
                  order_id,consultation_id,prescription_id,prescription_draft_version_id,draft_version,
                  pharmacy_choice_version,drug_name_snapshot,rx_norm_code_snapshot,directions_snapshot,
                  pharmacy_name_snapshot,pharmacy_state_code_snapshot,safety_outcome,safety_ruleset_version,
                  active_medication_count,active_allergy_count,signed_at,signed_by_staff_id,content_hash,
                  adapter_mode,canonical_model_version,target_standard,transition_standard,transaction_type,
                  transmission_state,certified,external_destination_contacted,legal_effect,patient_delivered,
                  idempotency_key,command_fingerprint)
                values (
                  @orderId,@consultationId,@prescriptionId,@draftVersionId,@draftVersion,@pharmacyVersion,
                  @drugName,@rxNorm,@directions,@pharmacyName,@pharmacyState,@safetyOutcome,@ruleset,
                  @medicationCount,@allergyCount,@signedAt,@physician,@contentHash,@adapter,@canonicalModel,
                  @targetStandard,@transitionStandard,@transactionType,@transmissionState,false,false,false,false,
                  @idempotencyKey,@fingerprint);
                """;
            insertOrder.Parameters.AddWithValue("orderId", orderId);
            insertOrder.Parameters.AddWithValue("consultationId", consultationId);
            insertOrder.Parameters.Add("prescriptionId", NpgsqlDbType.Text).Value = prescriptionId;
            insertOrder.Parameters.AddWithValue("draftVersionId", source.DraftVersionId);
            insertOrder.Parameters.AddWithValue("draftVersion", source.DraftVersion);
            insertOrder.Parameters.AddWithValue("pharmacyVersion", source.PharmacyChoiceVersion);
            insertOrder.Parameters.Add("drugName", NpgsqlDbType.Text).Value = source.DrugName;
            insertOrder.Parameters.Add("rxNorm", NpgsqlDbType.Text).Value = source.RxNormCode;
            insertOrder.Parameters.Add("directions", NpgsqlDbType.Text).Value = source.Directions;
            insertOrder.Parameters.Add("pharmacyName", NpgsqlDbType.Text).Value = source.PharmacyName;
            insertOrder.Parameters.Add("pharmacyState", NpgsqlDbType.Text).Value = source.PharmacyStateCode;
            insertOrder.Parameters.Add("safetyOutcome", NpgsqlDbType.Text).Value = safety.Outcome;
            insertOrder.Parameters.Add("ruleset", NpgsqlDbType.Text).Value = safety.RulesetVersion;
            insertOrder.Parameters.AddWithValue("medicationCount", activeMedicationCount);
            insertOrder.Parameters.AddWithValue("allergyCount", activeAllergyCount);
            insertOrder.Parameters.AddWithValue("signedAt", owner.DatabaseNow);
            insertOrder.Parameters.AddWithValue("physician", physicianStaffId);
            insertOrder.Parameters.Add("contentHash", NpgsqlDbType.Char).Value = contentHash;
            insertOrder.Parameters.Add("adapter", NpgsqlDbType.Text).Value = transmission.AdapterMode;
            insertOrder.Parameters.Add("canonicalModel", NpgsqlDbType.Text).Value = transmission.CanonicalModelVersion;
            insertOrder.Parameters.Add("targetStandard", NpgsqlDbType.Text).Value = transmission.TargetStandard;
            insertOrder.Parameters.Add("transitionStandard", NpgsqlDbType.Text).Value = transmission.TransitionStandard;
            insertOrder.Parameters.Add("transactionType", NpgsqlDbType.Text).Value = transmission.TransactionType;
            insertOrder.Parameters.Add("transmissionState", NpgsqlDbType.Text).Value = transmission.TransmissionState;
            insertOrder.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
            insertOrder.Parameters.Add("fingerprint", NpgsqlDbType.Char).Value = commandFingerprint;
            await insertOrder.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertAudit = connection.CreateCommand())
        {
            insertAudit.Transaction = transaction;
            insertAudit.CommandText = """
                insert into prescription_audit_events (
                  event_id,prescription_id,patient_id,pid,action,occurred_at,actor,detail,after_refills,pharmacy_name)
                values (@eventId,@prescriptionId,@patientId,@pid,'telehealth-synthetic-sign',@occurredAt,
                        @actor,@detail,@refills,@pharmacyName);
                """;
            insertAudit.Parameters.Add("eventId", NpgsqlDbType.Text).Value = $"RXAUD-{Guid.NewGuid():N}";
            insertAudit.Parameters.Add("prescriptionId", NpgsqlDbType.Text).Value = prescriptionId;
            insertAudit.Parameters.Add("patientId", NpgsqlDbType.Text).Value = owner.PatientId;
            insertAudit.Parameters.AddWithValue("pid", owner.LegacyPid);
            insertAudit.Parameters.Add("occurredAt", NpgsqlDbType.Timestamp).Value = legacyTimestamp;
            insertAudit.Parameters.Add("actor", NpgsqlDbType.Text).Value = actor;
            insertAudit.Parameters.Add("detail", NpgsqlDbType.Text).Value = "NON_PRODUCTION signed canonical record; no external transmission.";
            insertAudit.Parameters.AddWithValue("refills", source.Refills);
            insertAudit.Parameters.Add("pharmacyName", NpgsqlDbType.Text).Value = source.PharmacyName;
            await insertAudit.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TelehealthSignedPrescriptionResponse(
            orderId, prescriptionId, source.DraftVersion, source.PharmacyChoiceVersion,
            source.DrugName, source.RxNormCode, source.Directions, source.PharmacyName,
            source.PharmacyStateCode, safety.Outcome, safety.RulesetVersion,
            activeMedicationCount, activeAllergyCount, owner.DatabaseNow, contentHash,
            transmission.AdapterMode, transmission.CanonicalModelVersion, transmission.TargetStandard,
            transmission.TransitionStandard, transmission.TransactionType, transmission.TransmissionState,
            SafetyChecked: true, Signed: true, CanonicalPrescriptionCreated: true,
            transmission.Certified, transmission.ExternalDestinationContacted,
            LegalEffect: false, PatientDelivered: false);
    }

    private static async Task<PrescriptionSigningSource?> ReadSigningSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select draft.prescription_draft_version_id,draft.version,draft.pharmacy_choice_version,
                   draft.rx_norm_code,draft.drug_name_snapshot,draft.route_snapshot,draft.dose_amount,
                   draft.dose_unit,draft.frequency,draft.quantity_value,draft.quantity_unit,draft.duration_days,
                   draft.refills,draft.indication,draft.directions,choice.version,choice.directory_entry_id,
                   choice.pharmacy_name,choice.state_code
            from telehealth_consultation_prescription_draft_versions draft
            join telehealth_consultation_pharmacy_choice_versions choice
              on choice.consultation_id=draft.consultation_id
            where draft.consultation_id=@consultationId
              and choice.version=(select max(c.version) from telehealth_consultation_pharmacy_choice_versions c
                                  where c.consultation_id=draft.consultation_id and c.patient_choice_confirmed)
            order by draft.version desc limit 1
            for key share of draft,choice;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new PrescriptionSigningSource(
            reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7),
            reader.GetString(8), reader.GetDecimal(9), reader.GetString(10), reader.GetInt32(11),
            reader.GetInt32(12), reader.GetString(13), reader.GetString(14), reader.GetInt32(15),
            reader.GetGuid(16), reader.GetString(17), reader.GetString(18));
    }

    private static async Task<(int ActiveMedicationCount, int ActiveAllergyCount)> ReadActiveClinicalListCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              (select count(*)::integer from medications where patient_id=@patientId and activity=1),
              (select count(*)::integer from allergies where patient_id=@patientId and activity=1);
            """;
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<TelehealthSignedPrescriptionResponse?> ReadCurrentSignedPrescriptionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SignedPrescriptionSelect + " where consultation_id=@consultationId limit 1;";
        command.Parameters.AddWithValue("consultationId", consultationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSignedPrescription(reader) : null;
    }

    private static async Task<SignedPrescriptionReplay?> ReadSignedPrescriptionReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SignedPrescriptionSelect
            + " where consultation_id=@consultationId and idempotency_key=@idempotencyKey limit 1;";
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new SignedPrescriptionReplay(
            reader.GetString(reader.GetOrdinal("command_fingerprint")),
            ReadSignedPrescription(reader));
    }

    private const string SignedPrescriptionSelect = """
        select order_id,prescription_id,draft_version,pharmacy_choice_version,drug_name_snapshot,
               rx_norm_code_snapshot,directions_snapshot,pharmacy_name_snapshot,pharmacy_state_code_snapshot,
               safety_outcome,safety_ruleset_version,active_medication_count,active_allergy_count,signed_at,
               content_hash,adapter_mode,canonical_model_version,target_standard,transition_standard,
               transaction_type,transmission_state,certified,external_destination_contacted,legal_effect,
               patient_delivered,command_fingerprint
        from telehealth_consultation_prescription_orders
        """;

    private static TelehealthSignedPrescriptionResponse ReadSignedPrescription(DbDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("order_id")),
        reader.GetString(reader.GetOrdinal("prescription_id")),
        reader.GetInt32(reader.GetOrdinal("draft_version")),
        reader.GetInt32(reader.GetOrdinal("pharmacy_choice_version")),
        reader.GetString(reader.GetOrdinal("drug_name_snapshot")),
        reader.GetString(reader.GetOrdinal("rx_norm_code_snapshot")),
        reader.GetString(reader.GetOrdinal("directions_snapshot")),
        reader.GetString(reader.GetOrdinal("pharmacy_name_snapshot")),
        reader.GetString(reader.GetOrdinal("pharmacy_state_code_snapshot")),
        reader.GetString(reader.GetOrdinal("safety_outcome")),
        reader.GetString(reader.GetOrdinal("safety_ruleset_version")),
        reader.GetInt32(reader.GetOrdinal("active_medication_count")),
        reader.GetInt32(reader.GetOrdinal("active_allergy_count")),
        reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("signed_at")),
        reader.GetString(reader.GetOrdinal("content_hash")),
        reader.GetString(reader.GetOrdinal("adapter_mode")),
        reader.GetString(reader.GetOrdinal("canonical_model_version")),
        reader.GetString(reader.GetOrdinal("target_standard")),
        reader.GetString(reader.GetOrdinal("transition_standard")),
        reader.GetString(reader.GetOrdinal("transaction_type")),
        reader.GetString(reader.GetOrdinal("transmission_state")),
        SafetyChecked: true,
        Signed: true,
        CanonicalPrescriptionCreated: true,
        reader.GetBoolean(reader.GetOrdinal("certified")),
        reader.GetBoolean(reader.GetOrdinal("external_destination_contacted")),
        reader.GetBoolean(reader.GetOrdinal("legal_effect")),
        reader.GetBoolean(reader.GetOrdinal("patient_delivered")));

    private static async Task<OwnedWrapUp?> ReadOwnerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        bool lockAggregate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.encounter_id,context.status,now(),metadata.dataset_id,metadata.version,
                   patient.canonical_id,patient.legacy_pid
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            join patients patient on patient.canonical_id=request.patient_id
            join lateral (
              select dataset_id,version from dataset_metadata order by generated_at desc limit 1
            ) metadata on true
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
              and not exists (
                select 1 from encounter_signatures signature
                where signature.encounter=encounter.encounter and signature.is_lock)
            """ + (lockAggregate
                ? " for update of context,request,reservation,shift,session,appointment,encounter,patient;"
                : ";");
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new OwnedWrapUp(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6));
    }

    private static async Task<int?> ReadCurrentPharmacyChoiceVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select version
            from telehealth_consultation_pharmacy_choice_versions
            where consultation_id=@consultationId and patient_choice_confirmed
            order by version desc limit 1;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<int> ReadCurrentVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(max(version),0)
            from telehealth_consultation_prescription_draft_versions
            where consultation_id=@consultationId;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<CatalogItem?> ReadPermittedCatalogItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string rxNormCode,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select rx_norm_code,drug_name,display_name,form,strength,route
            from medication_vocabulary
            where rx_norm_code=@rxNormCode and active
              and nullif(trim(controlled_substance_schedule),'') is null
            for key share;
            """;
        command.Parameters.Add("rxNormCode", NpgsqlDbType.Text).Value = rxNormCode;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCatalogItem(reader) : null;
    }

    private static async Task<IReadOnlyList<TelehealthPrescriptionCatalogItemResponse>> SearchCatalogAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? query,
        CancellationToken cancellationToken)
    {
        if (query is null)
        {
            return [];
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select rx_norm_code,drug_name,display_name,form,strength,route
            from medication_vocabulary
            where active and nullif(trim(controlled_substance_schedule),'') is null
              and (lower(display_name) like @query or lower(drug_name) like @query or lower(rx_norm_code) like @query)
            order by lower(display_name),rx_norm_code
            limit 20;
            """;
        command.Parameters.Add("query", NpgsqlDbType.Text).Value = $"%{query.ToLowerInvariant()}%";
        var results = new List<TelehealthPrescriptionCatalogItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = ReadCatalogItem(reader);
            results.Add(new TelehealthPrescriptionCatalogItemResponse(
                item.RxNormCode,
                item.DrugName,
                item.DisplayName,
                item.Form,
                item.Strength,
                item.Route));
        }
        return results;
    }

    private static async Task<TelehealthPrescriptionPreparationDraftResponse?> ReadCurrentDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = DraftSelect + "\n" + """
            where draft.consultation_id=@consultationId
            order by draft.version desc limit 1;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDraft(reader) : null;
    }

    private static async Task<Replay?> ReadReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select event.command_fingerprint,
                   draft.version,draft.rx_norm_code,draft.drug_name_snapshot,draft.display_name_snapshot,
                   draft.form_snapshot,draft.strength_snapshot,draft.route_snapshot,draft.dose_amount,
                   draft.dose_unit,draft.frequency,draft.quantity_value,draft.quantity_unit,draft.duration_days,
                   draft.refills,draft.indication,draft.directions,draft.medication_list_reviewed,
                   draft.allergy_list_reviewed,draft.adequate_evaluation_completed,draft.pharmacy_choice_version,
                   draft.recorded_at,draft.legal_effect,draft.safety_checked,draft.signed,
                   draft.transmission_queued,draft.transmitted,draft.patient_delivered
            from telehealth_consultation_prescription_draft_versions draft
            join telehealth_consultation_prescription_draft_events event
              on event.prescription_draft_version_id=draft.prescription_draft_version_id
            where event.consultation_id=@consultationId and event.idempotency_key=@idempotencyKey
            limit 1;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.Add("idempotencyKey", NpgsqlDbType.Text).Value = idempotencyKey;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var fingerprintOrdinal = reader.GetOrdinal("command_fingerprint");
        return new Replay(reader.GetString(fingerprintOrdinal), ReadDraft(reader));
    }

    private const string DraftSelect = """
        select draft.version,draft.rx_norm_code,draft.drug_name_snapshot,draft.display_name_snapshot,
               draft.form_snapshot,draft.strength_snapshot,draft.route_snapshot,draft.dose_amount,
               draft.dose_unit,draft.frequency,draft.quantity_value,draft.quantity_unit,draft.duration_days,
               draft.refills,draft.indication,draft.directions,draft.medication_list_reviewed,
               draft.allergy_list_reviewed,draft.adequate_evaluation_completed,draft.pharmacy_choice_version,
               draft.recorded_at,draft.legal_effect,draft.safety_checked,draft.signed,
               draft.transmission_queued,draft.transmitted,draft.patient_delivered
        from telehealth_consultation_prescription_draft_versions draft
        """;

    private static CatalogItem ReadCatalogItem(DbDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5));

    private static TelehealthPrescriptionPreparationDraftResponse ReadDraft(DbDataReader reader) => new(
        reader.GetInt32(reader.GetOrdinal("version")),
        reader.GetString(reader.GetOrdinal("rx_norm_code")),
        reader.GetString(reader.GetOrdinal("drug_name_snapshot")),
        reader.GetString(reader.GetOrdinal("display_name_snapshot")),
        reader.GetString(reader.GetOrdinal("form_snapshot")),
        reader.GetString(reader.GetOrdinal("strength_snapshot")),
        reader.GetString(reader.GetOrdinal("route_snapshot")),
        reader.GetDecimal(reader.GetOrdinal("dose_amount")),
        reader.GetString(reader.GetOrdinal("dose_unit")),
        reader.GetString(reader.GetOrdinal("frequency")),
        reader.GetDecimal(reader.GetOrdinal("quantity_value")),
        reader.GetString(reader.GetOrdinal("quantity_unit")),
        reader.GetInt32(reader.GetOrdinal("duration_days")),
        reader.GetInt32(reader.GetOrdinal("refills")),
        reader.GetString(reader.GetOrdinal("indication")),
        reader.GetString(reader.GetOrdinal("directions")),
        reader.GetBoolean(reader.GetOrdinal("medication_list_reviewed")),
        reader.GetBoolean(reader.GetOrdinal("allergy_list_reviewed")),
        reader.GetBoolean(reader.GetOrdinal("adequate_evaluation_completed")),
        reader.GetInt32(reader.GetOrdinal("pharmacy_choice_version")),
        reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("recorded_at")),
        reader.GetBoolean(reader.GetOrdinal("legal_effect")),
        reader.GetBoolean(reader.GetOrdinal("safety_checked")),
        reader.GetBoolean(reader.GetOrdinal("signed")),
        reader.GetBoolean(reader.GetOrdinal("transmission_queued")),
        reader.GetBoolean(reader.GetOrdinal("transmitted")),
        reader.GetBoolean(reader.GetOrdinal("patient_delivered")));

    private static TelehealthPrescriptionPreparationDraftResponse ToDraftResponse(
        int version,
        CatalogItem catalog,
        RecordTelehealthPrescriptionPreparationDraftRequest request,
        int pharmacyChoiceVersion,
        DateTimeOffset recordedAt) => new(
        version,
        catalog.RxNormCode,
        catalog.DrugName,
        catalog.DisplayName,
        catalog.Form,
        catalog.Strength,
        catalog.Route,
        request.DoseAmount,
        request.DoseUnit,
        request.Frequency,
        request.QuantityValue,
        request.QuantityUnit,
        request.DurationDays,
        request.Refills,
        request.Indication,
        request.Directions,
        request.MedicationListReviewed,
        request.AllergyListReviewed,
        request.AdequateEvaluationCompleted,
        pharmacyChoiceVersion,
        recordedAt,
        LegalEffect: false,
        SafetyChecked: false,
        Signed: false,
        TransmissionQueued: false,
        Transmitted: false,
        PatientDelivered: false);

    private sealed record OwnedWrapUp(
        int EncounterId,
        string ConsultationStatus,
        DateTimeOffset DatabaseNow,
        string DatasetId,
        string DatasetVersion,
        string PatientId,
        int LegacyPid);

    private sealed record CatalogItem(
        string RxNormCode,
        string DrugName,
        string DisplayName,
        string Form,
        string Strength,
        string Route);

    private sealed record Replay(
        string CommandFingerprint,
        TelehealthPrescriptionPreparationDraftResponse Draft);

    private sealed record PrescriptionSigningSource(
        Guid DraftVersionId,
        int DraftVersion,
        int DraftPharmacyChoiceVersion,
        string RxNormCode,
        string DrugName,
        string Route,
        decimal DoseAmount,
        string DoseUnit,
        string Frequency,
        decimal QuantityValue,
        string QuantityUnit,
        int DurationDays,
        int Refills,
        string Indication,
        string Directions,
        int PharmacyChoiceVersion,
        Guid DirectoryEntryId,
        string PharmacyName,
        string PharmacyStateCode);

    private sealed record SignedPrescriptionReplay(
        string CommandFingerprint,
        TelehealthSignedPrescriptionResponse Prescription);
}

public sealed class TelehealthPrescriptionDraftConflictException(string message) : Exception(message);
