// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Data.Common;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthPrescriptionRepository(NpgsqlDataSource dataSource)
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
            SafetyCheckEnabled: false,
            SigningEnabled: false,
            PrescriptionCreationEnabled: false,
            TransmissionEnabled: false,
            PatientDeliveryEnabled: false,
            CompletionEnabled: false,
            Limitations:
            [
                "Catalog results are deterministic synthetic reference facts, not drug or dosing recommendations.",
                "This preparation draft has no interaction or contraindication check and is not a medication order or prescription.",
                "Signing, canonical prescription creation, NCPDP mapping, transmission, patient delivery, and consultation completion are unavailable."
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
            select context.encounter_id,context.status,now(),metadata.dataset_id,metadata.version
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
            reader.GetString(4));
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
        string DatasetVersion);

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
}

public sealed class TelehealthPrescriptionDraftConflictException(string message) : Exception(message);
