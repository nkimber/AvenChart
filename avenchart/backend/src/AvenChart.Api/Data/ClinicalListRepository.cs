// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data.Common;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ClinicalListRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<MedicationVocabularyItem>> SearchMedicationVocabularyAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var normalizedQuery = query?.Trim() ?? string.Empty;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                rx_norm_code,
                drug_name,
                display_name,
                form,
                strength,
                route,
                dose_amount,
                dose_unit,
                frequency,
                duration_days,
                controlled_substance_schedule
            from medication_vocabulary
            where @query = ''
               or lower(drug_name) like @pattern
               or lower(display_name) like @pattern
               or rx_norm_code = @query
            order by drug_name, dose_amount nulls last, rx_norm_code
            limit 10;
            """;
        command.Parameters.AddWithValue("query", normalizedQuery.ToLowerInvariant());
        command.Parameters.AddWithValue("pattern", $"%{normalizedQuery.ToLowerInvariant()}%");

        var items = new List<MedicationVocabularyItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MedicationVocabularyItem(
                RxNormCode: reader.GetString(reader.GetOrdinal("rx_norm_code")),
                DrugName: reader.GetString(reader.GetOrdinal("drug_name")),
                DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
                Form: reader.GetString(reader.GetOrdinal("form")),
                Strength: reader.GetString(reader.GetOrdinal("strength")),
                Route: reader.GetString(reader.GetOrdinal("route")),
                DoseAmount: ReadNullableDecimal(reader, "dose_amount"),
                DoseUnit: ReadNullableString(reader, "dose_unit"),
                Frequency: ReadNullableString(reader, "frequency"),
                DurationDays: ReadNullableInt(reader, "duration_days"),
                ControlledSubstanceSchedule: ReadNullableString(reader, "controlled_substance_schedule")));
        }

        return items;
    }

    public async Task<ClinicalPharmacyDirectoryResponse> GetPharmacyDirectoryAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, name, transmit_method, email, ncpdp, npi
            from pharmacies
            order by name, id;
            """;

        var pharmacies = new List<ClinicalPharmacyDirectoryItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pharmacies.Add(new ClinicalPharmacyDirectoryItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                TransmitMethod: reader.GetInt32(reader.GetOrdinal("transmit_method")),
                Email: ReadNullableString(reader, "email"),
                Ncpdp: ReadNullableInt(reader, "ncpdp"),
                Npi: ReadNullableInt(reader, "npi")));
        }

        return new ClinicalPharmacyDirectoryResponse(
            metadata.DatasetId,
            metadata.DatasetVersion,
            pharmacies.Count,
            pharmacies);
    }

    public async Task<PrescriptionRefillQueueResponse> GetPrescriptionRefillQueueAsync(
        string? status,
        string? patient,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedStatus = NormalizeOptionalText(status)?.ToLowerInvariant() ?? "open";
        if (normalizedStatus is not (
            "open"
            or "pending"
            or "clarification-requested"
            or "approved"
            or "denied"
            or "completed"
            or "all"))
        {
            normalizedStatus = "open";
        }
        var normalizedPatient = NormalizeOptionalText(patient);
        var boundedLimit = Math.Clamp(limit, 1, 200);
        var boundedOffset = Math.Max(0, offset);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        PrescriptionRefillQueueCounts counts;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                with refill_requests as (
                    select
                        coalesce(
                            lifecycle.status,
                            case when m.message_status = 'Done' then 'approved' else 'pending' end
                        ) as lifecycle_status
                    from portal_mailbox_messages m
                    join patients patient_record on patient_record.legacy_pid = m.pid
                    join prescriptions prescription
                      on prescription.pid = m.pid
                     and prescription.id::text = nullif(
                        substring(m.body from 'Prescription ID: ([^\r\n]+)'),
                        ''
                     )
                    left join prescription_refill_request_lifecycle lifecycle
                      on lifecycle.thread_id = m.reply_mail_chain
                    where m.deleted = 0
                      and m.owner = m.assigned_to
                      and m.portal_relation = 'portal:prescription-refill-request'
                      and (
                        @patient is null
                        or patient_record.canonical_id ilike '%' || @patient || '%'
                        or patient_record.pubpid ilike '%' || @patient || '%'
                        or trim(concat(patient_record.first_name, ' ', patient_record.last_name))
                            ilike '%' || @patient || '%'
                        or patient_record.legacy_pid::text = @patient
                      )
                )
                select
                    count(*) filter (where lifecycle_status = 'pending')::integer as pending,
                    count(*) filter (where lifecycle_status = 'clarification-requested')::integer as clarification_requested,
                    count(*) filter (where lifecycle_status = 'approved')::integer as approved,
                    count(*) filter (where lifecycle_status = 'denied')::integer as denied,
                    count(*) filter (where lifecycle_status = 'completed')::integer as completed,
                    count(*)::integer as total
                from refill_requests;
                """;
            countCommand.Parameters.Add("patient", NpgsqlDbType.Text).Value =
                NullableText(normalizedPatient);
            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            counts = new PrescriptionRefillQueueCounts(
                Pending: ReadInt(reader, "pending"),
                ClarificationRequested: ReadInt(reader, "clarification_requested"),
                Approved: ReadInt(reader, "approved"),
                Denied: ReadInt(reader, "denied"),
                Completed: ReadInt(reader, "completed"),
                Total: ReadInt(reader, "total"));
        }

        var requests = new List<PrescriptionRefillQueueItem>();
        var totalMatches = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                with refill_requests as (
                    select
                        m.id,
                        m.reply_mail_chain as thread_id,
                        patient_record.canonical_id as patient_id,
                        patient_record.legacy_pid as pid,
                        patient_record.pubpid,
                        trim(concat(patient_record.first_name, ' ', patient_record.last_name)) as patient_display_name,
                        m.sender_id as portal_username,
                        prescription.id::text as prescription_id,
                        prescription.drug,
                        prescription.dosage,
                        prescription.quantity,
                        prescription.route,
                        prescription.refills,
                        coalesce(lifecycle.request_date, m.message_date) as request_date,
                        coalesce(
                            lifecycle.patient_note,
                            nullif(substring(m.body from 'Patient note: ([^\r\n]+)'), '')
                        ) as patient_note,
                        coalesce(
                            lifecycle.status,
                            case when m.message_status = 'Done' then 'approved' else 'pending' end
                        ) as lifecycle_status,
                        lifecycle.staff_response,
                        coalesce(lifecycle.updated_at, m.message_date::timestamp) as updated_at,
                        coalesce(lifecycle.updated_by, m.assigned_to) as updated_by
                    from portal_mailbox_messages m
                    join patients patient_record on patient_record.legacy_pid = m.pid
                    join prescriptions prescription
                      on prescription.pid = m.pid
                     and prescription.id::text = nullif(
                        substring(m.body from 'Prescription ID: ([^\r\n]+)'),
                        ''
                     )
                    left join prescription_refill_request_lifecycle lifecycle
                      on lifecycle.thread_id = m.reply_mail_chain
                    where m.deleted = 0
                      and m.owner = m.assigned_to
                      and m.portal_relation = 'portal:prescription-refill-request'
                      and (
                        @patient is null
                        or patient_record.canonical_id ilike '%' || @patient || '%'
                        or patient_record.pubpid ilike '%' || @patient || '%'
                        or trim(concat(patient_record.first_name, ' ', patient_record.last_name))
                            ilike '%' || @patient || '%'
                        or patient_record.legacy_pid::text = @patient
                      )
                ),
                selected as (
                    select *, count(*) over()::integer as total_matches
                    from refill_requests
                    where @status = 'all'
                       or (@status = 'open' and lifecycle_status in ('pending', 'clarification-requested'))
                       or lifecycle_status = @status
                    order by request_date asc, id asc
                    limit @limit offset @offset
                )
                select *
                from selected;
                """;
            command.Parameters.Add("patient", NpgsqlDbType.Text).Value =
                NullableText(normalizedPatient);
            command.Parameters.AddWithValue("status", normalizedStatus);
            command.Parameters.AddWithValue("limit", boundedLimit);
            command.Parameters.AddWithValue("offset", boundedOffset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                totalMatches = ReadInt(reader, "total_matches");
                requests.Add(new PrescriptionRefillQueueItem(
                    MessageId: ReadInt(reader, "id"),
                    ThreadId: ReadInt(reader, "thread_id"),
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    LegacyPid: ReadInt(reader, "pid"),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                    PortalUsername: reader.GetString(reader.GetOrdinal("portal_username")),
                    PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")),
                    Drug: reader.GetString(reader.GetOrdinal("drug")),
                    Dosage: ReadNullableString(reader, "dosage"),
                    Quantity: ReadNullableString(reader, "quantity"),
                    Route: ReadNullableString(reader, "route"),
                    CurrentRefills: ReadInt(reader, "refills"),
                    RequestDate: ReadNullableDate(reader, "request_date") ?? string.Empty,
                    Status: reader.GetString(reader.GetOrdinal("lifecycle_status")),
                    PatientNote: ReadNullableString(reader, "patient_note"),
                    StaffResponse: ReadNullableString(reader, "staff_response"),
                    UpdatedAt: reader.GetFieldValue<DateTime>(reader.GetOrdinal("updated_at")).ToString("O"),
                    UpdatedBy: reader.GetString(reader.GetOrdinal("updated_by"))));
            }
        }

        return new PrescriptionRefillQueueResponse(
            metadata.DatasetId,
            metadata.DatasetVersion,
            normalizedStatus,
            normalizedPatient,
            totalMatches,
            requests.Count,
            counts,
            requests);
    }

    public async Task<ClinicalListsResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var problems = await GetProblemsAsync(connection, patient.LegacyPid, cancellationToken);
        var allergies = await GetAllergiesAsync(connection, patient.LegacyPid, cancellationToken);
        var medications = await GetMedicationsAsync(connection, patient.LegacyPid, cancellationToken);
        var activeProblems = problems.Where(item => item.Activity == 1).ToList();
        var activeMedications = medications.Where(item => item.Activity == 1).ToList();
        var medicationDuplicates = BuildMedicationDuplicates(activeMedications);
        var immunizations = await GetImmunizationsAsync(connection, patient.LegacyPid, cancellationToken);
        var prescriptions = await GetPrescriptionsAsync(connection, patient.LegacyPid, cancellationToken);
        var medicationReconciliations = BuildMedicationReconciliations(activeMedications, prescriptions);
        var prescriptionDiagnosisInteractions = BuildPrescriptionDiagnosisInteractions(activeProblems, prescriptions);
        var prescriptionRefillRequests = await GetPrescriptionRefillRequestsAsync(connection, patient.LegacyPid, cancellationToken);

        return new ClinicalListsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            Problems: problems,
            Allergies: allergies,
            Medications: medications,
            MedicationDuplicates: medicationDuplicates,
            MedicationReconciliations: medicationReconciliations,
            Immunizations: immunizations,
            Prescriptions: prescriptions,
            PrescriptionDiagnosisInteractions: prescriptionDiagnosisInteractions,
            PrescriptionRefillRequests: prescriptionRefillRequests);
    }

    public async Task<ClinicalListMutationResponse?> CreatePrescriptionAsync(
        ClinicalPrescriptionCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Drug)
            || string.IsNullOrWhiteSpace(request.Dosage)
            || string.IsNullOrWhiteSpace(request.Quantity)
            || request.Refills < 0
            || !TryReadDate(request.StartDate, out var startDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var patient = await GetActivePatientForNewClinicalContentAsync(
            connection,
            transaction,
            request.PatientId,
            cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var id = $"RX-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into prescriptions
                (id, patient_id, pid, provider_id, encounter, start_date, date_added, modified_date, end_date, drug, rx_norm_code,
                 dosage, quantity, dose_amount, dose_unit, frequency, duration_days, route, refills, diagnosis, note, active)
            values
                (@id, @patientId, @pid, @providerId, 0, @startDate, @startDate + time '10:00:00', @startDate, null, @drug, @rxNormCode,
                 @dosage, @quantity, @doseAmount, @doseUnit, @frequency, @durationDays, @route, @refills, @diagnosis, @note, 1);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("providerId", request.ProviderId ?? patient.ProviderId);
        command.Parameters.Add("startDate", NpgsqlDbType.Date).Value = startDate;
        command.Parameters.AddWithValue("drug", request.Drug.Trim());
        command.Parameters.AddWithValue("rxNormCode", NullableText(request.RxNormCode));
        command.Parameters.AddWithValue("dosage", request.Dosage.Trim());
        command.Parameters.AddWithValue("quantity", request.Quantity.Trim());
        AddNullableDecimal(command, "doseAmount", request.DoseAmount);
        command.Parameters.AddWithValue("doseUnit", NullableText(request.DoseUnit));
        command.Parameters.AddWithValue("frequency", NullableText(request.Frequency));
        AddNullableInt(command, "durationDays", request.DurationDays);
        command.Parameters.AddWithValue("route", string.IsNullOrWhiteSpace(request.Route) ? "oral" : request.Route.Trim());
        command.Parameters.AddWithValue("refills", request.Refills);
        command.Parameters.AddWithValue("diagnosis", NullableText(request.Diagnosis));
        command.Parameters.AddWithValue("note", NullableText(request.Note));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            prescriptionId: id,
            patientId: patient.PatientId,
            pid: patient.LegacyPid,
            action: "create",
            occurredAt: startDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
            detail: request.Note,
            beforeRefills: null,
            afterRefills: request.Refills,
            pharmacyId: null,
            pharmacyName: null,
            failureReason: null,
            cancellationToken,
            actor: username);
        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    public async Task<ClinicalPrescriptionUpdateResult> UpdatePrescriptionAsync(
        string prescriptionId,
        ClinicalPrescriptionUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId)
            || string.IsNullOrWhiteSpace(request.ExpectedVersion)
            || string.IsNullOrWhiteSpace(request.Dosage)
            || request.Dosage.Trim().Length > 250
            || string.IsNullOrWhiteSpace(request.Quantity)
            || request.Quantity.Trim().Length > 100
            || request.DoseAmount < 0
            || request.DoseUnit?.Trim().Length > 50
            || request.Frequency?.Trim().Length > 100
            || request.DurationDays is <= 0
            || request.Route?.Trim().Length > 100
            || request.Refills is < 0 or > 12
            || request.Diagnosis?.Trim().Length > 100
            || request.Note?.Trim().Length > 1000
            || string.IsNullOrWhiteSpace(request.EditReason)
            || request.EditReason.Trim().Length > 500
            || !TryReadDate(request.StartDate, out var startDate))
        {
            return new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.Invalid,
                CurrentVersion: null,
                Mutation: null);
        }

        var dosage = request.Dosage.Trim();
        var quantity = request.Quantity.Trim();
        var doseUnit = NormalizeOptionalText(request.DoseUnit);
        var frequency = NormalizeOptionalText(request.Frequency);
        var route = NormalizeOptionalText(request.Route) ?? "oral";
        var diagnosis = NormalizeOptionalText(request.Diagnosis);
        var note = NormalizeOptionalText(request.Note);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        PrescriptionEditSnapshot? current = null;
        await using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = """
                select
                    patient_id,
                    pid,
                    start_date,
                    dosage,
                    quantity,
                    dose_amount,
                    dose_unit,
                    frequency,
                    duration_days,
                    route,
                    refills,
                    diagnosis,
                    note,
                    pharmacy_id,
                    erx_uploaded,
                    xmin::text as version
                from prescriptions
                where id = @id
                  and active = 1
                for update;
                """;
            query.Parameters.AddWithValue("id", prescriptionId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                current = new PrescriptionEditSnapshot(
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    Pid: ReadInt(reader, "pid"),
                    StartDate: ReadNullableDate(reader, "start_date"),
                    Dosage: ReadNullableString(reader, "dosage"),
                    Quantity: ReadNullableString(reader, "quantity"),
                    DoseAmount: ReadNullableDecimal(reader, "dose_amount"),
                    DoseUnit: ReadNullableString(reader, "dose_unit"),
                    Frequency: ReadNullableString(reader, "frequency"),
                    DurationDays: ReadNullableInt(reader, "duration_days"),
                    Route: ReadNullableString(reader, "route"),
                    Refills: ReadInt(reader, "refills"),
                    Diagnosis: ReadNullableString(reader, "diagnosis"),
                    Note: ReadNullableString(reader, "note"),
                    HadRouteEvidence:
                        !reader.IsDBNull(reader.GetOrdinal("pharmacy_id"))
                        || ReadInt(reader, "erx_uploaded") == 1,
                    Version: reader.GetString(reader.GetOrdinal("version")));
            }
        }

        if (current is null)
        {
            return new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.NotFound,
                CurrentVersion: null,
                Mutation: null);
        }

        if (!string.Equals(current.Version, request.ExpectedVersion.Trim(), StringComparison.Ordinal))
        {
            return new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.Conflict,
                current.Version,
                Mutation: null);
        }

        var changes = new List<string>();
        if (!string.Equals(current.StartDate, startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            changes.Add("start date");
        }
        if (!string.Equals(current.Dosage, dosage, StringComparison.Ordinal)) changes.Add("directions");
        if (!string.Equals(current.Quantity, quantity, StringComparison.Ordinal)) changes.Add("quantity");
        if (current.DoseAmount != request.DoseAmount) changes.Add("dose amount");
        if (!string.Equals(current.DoseUnit, doseUnit, StringComparison.Ordinal)) changes.Add("dose unit");
        if (!string.Equals(current.Frequency, frequency, StringComparison.Ordinal)) changes.Add("frequency");
        if (current.DurationDays != request.DurationDays) changes.Add("duration");
        if (!string.Equals(current.Route, route, StringComparison.Ordinal)) changes.Add("route");
        if (current.Refills != request.Refills) changes.Add("refills");
        if (!string.Equals(current.Diagnosis, diagnosis, StringComparison.Ordinal)) changes.Add("diagnosis");
        if (!string.Equals(current.Note, note, StringComparison.Ordinal)) changes.Add("clinical note");

        if (changes.Count == 0)
        {
            return new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.Invalid,
                current.Version,
                Mutation: null);
        }

        var modifiedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update prescriptions
                set start_date = @startDate,
                    dosage = @dosage,
                    quantity = @quantity,
                    dose_amount = @doseAmount,
                    dose_unit = @doseUnit,
                    frequency = @frequency,
                    duration_days = @durationDays,
                    route = @route,
                    refills = @refills,
                    diagnosis = @diagnosis,
                    note = @note,
                    modified_date = @modifiedDate,
                    pharmacy_id = null,
                    pharmacy_name = null,
                    pharmacy_ncpdp = null,
                    erx_uploaded = 0,
                    erx_sent_at = null,
                    erx_payload = null
                where id = @id
                  and active = 1
                  and xmin::text = @expectedVersion;
                """;
            update.Parameters.AddWithValue("id", prescriptionId);
            update.Parameters.AddWithValue("expectedVersion", current.Version);
            update.Parameters.Add("startDate", NpgsqlDbType.Date).Value = startDate;
            update.Parameters.AddWithValue("dosage", dosage);
            update.Parameters.AddWithValue("quantity", quantity);
            AddNullableDecimal(update, "doseAmount", request.DoseAmount);
            update.Parameters.AddWithValue("doseUnit", NullableText(doseUnit));
            update.Parameters.AddWithValue("frequency", NullableText(frequency));
            AddNullableInt(update, "durationDays", request.DurationDays);
            update.Parameters.AddWithValue("route", route);
            update.Parameters.AddWithValue("refills", request.Refills);
            update.Parameters.AddWithValue("diagnosis", NullableText(diagnosis));
            update.Parameters.AddWithValue("note", NullableText(note));
            update.Parameters.Add("modifiedDate", NpgsqlDbType.Date).Value = modifiedDate;
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return new ClinicalPrescriptionUpdateResult(
                    ClinicalPrescriptionUpdateStatus.Conflict,
                    CurrentVersion: null,
                    Mutation: null);
            }
        }

        var auditDetail =
            $"{request.EditReason.Trim()} Updated fields: {string.Join(", ", changes)}."
            + (current.HadRouteEvidence
                ? " Prior local pharmacy route evidence was cleared."
                : string.Empty);
        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            prescriptionId,
            current.PatientId,
            current.Pid,
            "update",
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            auditDetail,
            beforeRefills: current.Refills,
            afterRefills: request.Refills,
            pharmacyId: null,
            pharmacyName: null,
            failureReason: null,
            cancellationToken,
            actor: username);
        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(current.PatientId, cancellationToken);
        return lists is null
            ? new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.NotFound,
                CurrentVersion: null,
                Mutation: null)
            : new ClinicalPrescriptionUpdateResult(
                ClinicalPrescriptionUpdateStatus.Updated,
                CurrentVersion: null,
                Mutation: new ClinicalListMutationResponse(prescriptionId, lists));
    }

    public async Task<ClinicalListMutationResponse?> DeactivatePrescriptionAsync(
        string prescriptionId,
        ClinicalPrescriptionDeactivateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId) || !TryReadDate(request.EndDate, out var endDate))
        {
            return null;
        }

        string? patientId = null;
        int? pid = null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update prescriptions
                set active = 0,
                    end_date = @endDate,
                    modified_date = @endDate,
                    note = @note
                where id = @id
                returning patient_id, pid;
                """;
            command.Parameters.AddWithValue("id", prescriptionId);
            command.Parameters.Add("endDate", NpgsqlDbType.Date).Value = endDate;
            command.Parameters.AddWithValue("note", NullableText(request.Note));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                pid = ReadInt(reader, "pid");
            }
        }

        if (patientId is null || pid is null)
        {
            return null;
        }

        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            prescriptionId,
            patientId,
            pid.Value,
            "deactivate",
            endDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
            request.Note,
            beforeRefills: null,
            afterRefills: null,
            pharmacyId: null,
            pharmacyName: null,
            failureReason: null,
            cancellationToken,
            actor: username);
        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(prescriptionId, lists);
    }

    public async Task<ClinicalListMutationResponse?> RefillPrescriptionAsync(
        string prescriptionId,
        ClinicalPrescriptionRefillRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId)
            || request.AdditionalRefills <= 0
            || !TryReadDate(request.RefillDate, out var refillDate))
        {
            return null;
        }

        string? patientId = null;
        int? pid = null;
        int? afterRefills = null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update prescriptions
                set refills = refills + @additionalRefills,
                    modified_date = @refillDate,
                    note = @note
                where id = @id and active = 1
                returning patient_id, pid, refills;
                """;
            command.Parameters.AddWithValue("id", prescriptionId);
            command.Parameters.AddWithValue("additionalRefills", request.AdditionalRefills);
            command.Parameters.Add("refillDate", NpgsqlDbType.Date).Value = refillDate;
            command.Parameters.AddWithValue("note", NullableText(request.Note));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                pid = ReadInt(reader, "pid");
                afterRefills = ReadInt(reader, "refills");
            }
        }

        if (patientId is null || pid is null || afterRefills is null)
        {
            return null;
        }

        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            prescriptionId: prescriptionId,
            patientId: patientId,
            pid: pid.Value,
            action: "refill",
            occurredAt: refillDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
            detail: request.Note,
            beforeRefills: afterRefills.Value - request.AdditionalRefills,
            afterRefills: afterRefills.Value,
            pharmacyId: null,
            pharmacyName: null,
            failureReason: null,
            cancellationToken,
            actor: username);
        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(prescriptionId, lists);
    }

    public async Task<ClinicalListMutationResponse?> ApprovePrescriptionRefillRequestAsync(
        int messageId,
        ClinicalPrescriptionRefillApprovalRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (messageId <= 0
            || request.AdditionalRefills <= 0
            || string.IsNullOrWhiteSpace(request.Note)
            || request.Note.Length > 500
            || !TryReadDate(request.RefillDate, out var refillDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var refillRequest = await GetPrescriptionRefillRequestAsync(
            connection,
            transaction,
            messageId,
            openOnly: true,
            cancellationToken);
        if (refillRequest is null)
        {
            return null;
        }

        string? patientId;
        await using (var updatePrescription = connection.CreateCommand())
        {
            updatePrescription.Transaction = transaction;
            updatePrescription.CommandText = """
                update prescriptions
                set refills = refills + @additionalRefills,
                    modified_date = @refillDate,
                    note = @note
                where id::text = @id
                  and pid = @pid
                  and active = 1
                  and end_date is null
                returning patient_id, refills;
                """;
            updatePrescription.Parameters.Add("id", NpgsqlDbType.Text).Value = refillRequest.PrescriptionId;
            updatePrescription.Parameters.Add("pid", NpgsqlDbType.Integer).Value = refillRequest.LegacyPid;
            updatePrescription.Parameters.Add("additionalRefills", NpgsqlDbType.Integer).Value = request.AdditionalRefills;
            updatePrescription.Parameters.Add("refillDate", NpgsqlDbType.Date).Value = refillDate;
            updatePrescription.Parameters.Add("note", NpgsqlDbType.Text).Value = NullableText(request.Note);
            await using var reader = await updatePrescription.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                patientId = reader.GetString(reader.GetOrdinal("patient_id"));
                var afterRefills = ReadInt(reader, "refills");
                await reader.DisposeAsync();
                await InsertPrescriptionAuditEventAsync(
                    connection,
                    transaction,
                    refillRequest.PrescriptionId,
                    patientId,
                    refillRequest.LegacyPid,
                    "refill-request-approved",
                    refillDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
                    request.Note,
                    afterRefills - request.AdditionalRefills,
                    afterRefills,
                    pharmacyId: null,
                    pharmacyName: null,
                    failureReason: null,
                    cancellationToken,
                    actor: username);
            }
            else
            {
                patientId = null;
            }
        }

        if (patientId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using (var updateMessages = connection.CreateCommand())
        {
            updateMessages.Transaction = transaction;
            updateMessages.CommandText = """
                update portal_mailbox_messages
                set message_status = 'Done',
                    activity = 1
                where deleted = 0
                  and portal_relation = 'portal:prescription-refill-request'
                  and (
                    id = @messageId
                    or reply_mail_chain = @replyMailChain
                    or mail_chain = @replyMailChain
                  );
                """;
            updateMessages.Parameters.Add("messageId", NpgsqlDbType.Integer).Value = messageId;
            updateMessages.Parameters.Add("replyMailChain", NpgsqlDbType.Integer).Value = refillRequest.ReplyMailChain;
            await updateMessages.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertPrescriptionRefillLifecycleAsync(
            connection,
            transaction,
            refillRequest,
            status: "approved",
            staffResponse: request.Note,
            username,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(refillRequest.PrescriptionId, lists);
    }

    public async Task<ClinicalPrescriptionRefillDecisionResponse?> DecidePrescriptionRefillRequestAsync(
        int messageId,
        ClinicalPrescriptionRefillDecisionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var action = NormalizeOptionalText(request.Action)?.ToLowerInvariant();
        var response = NormalizeOptionalText(request.Response);
        if (messageId <= 0
            || action is not ("deny" or "request-clarification" or "complete")
            || response is null
            || response.Length > 500)
        {
            throw new ArgumentException(
                "Refill decisions require deny, request-clarification, or complete plus a response of 500 characters or fewer.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var anchor = await GetPrescriptionRefillRequestAsync(
            connection,
            transaction,
            messageId,
            openOnly: false,
            cancellationToken);
        if (anchor is null)
        {
            return null;
        }

        var isOpen = anchor.Status is "pending" or "clarification-requested";
        if ((action is "deny" or "request-clarification") && !isOpen)
        {
            throw new ArgumentException(
                $"A refill request in {anchor.Status} state cannot be denied or sent for clarification.");
        }
        if (action == "complete" && anchor.Status != "approved")
        {
            throw new ArgumentException(
                $"Only an approved refill request can be marked locally completed; the current state is {anchor.Status}.");
        }

        var nextStatus = action switch
        {
            "deny" => "denied",
            "request-clarification" => "clarification-requested",
            _ => "completed"
        };
        await using (var updateMessages = connection.CreateCommand())
        {
            updateMessages.Transaction = transaction;
            updateMessages.CommandText = """
                update portal_mailbox_messages
                set message_status = @messageStatus,
                    activity = 1
                where deleted = 0
                  and portal_relation = 'portal:prescription-refill-request'
                  and (
                    id = @messageId
                    or reply_mail_chain = @replyMailChain
                    or mail_chain = @replyMailChain
                  );
                """;
            updateMessages.Parameters.AddWithValue(
                "messageStatus",
                nextStatus == "clarification-requested" ? "New" : "Done");
            updateMessages.Parameters.Add("messageId", NpgsqlDbType.Integer).Value = messageId;
            updateMessages.Parameters.Add("replyMailChain", NpgsqlDbType.Integer).Value = anchor.ReplyMailChain;
            await updateMessages.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertPrescriptionRefillLifecycleAsync(
            connection,
            transaction,
            anchor,
            nextStatus,
            response,
            username,
            cancellationToken);
        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            anchor.PrescriptionId,
            anchor.PatientId,
            anchor.LegacyPid,
            action switch
            {
                "deny" => "refill-request-denied",
                "request-clarification" => "refill-clarification-requested",
                _ => "refill-request-completed"
            },
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            response,
            beforeRefills: null,
            afterRefills: null,
            pharmacyId: null,
            pharmacyName: null,
            failureReason: null,
            cancellationToken,
            actor: username);
        await transaction.CommitAsync(cancellationToken);

        return new ClinicalPrescriptionRefillDecisionResponse(
            messageId,
            anchor.PrescriptionId,
            nextStatus,
            response);
    }

    public async Task<ClinicalPrescriptionPharmacyRouteResponse?> RoutePrescriptionToPharmacyAsync(
        string prescriptionId,
        ClinicalPrescriptionPharmacyRouteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId)
            || request.PharmacyId <= 0
            || !TryReadDateTime(request.SentAt, out var sentAt))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        PharmacyRouteAnchor anchor;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    pr.patient_id,
                    pr.pid,
                    pr.drug,
                    pr.dosage,
                    pr.quantity,
                    pr.rx_norm_code,
                    ph.id as pharmacy_id,
                    ph.name as pharmacy_name,
                    ph.ncpdp
                from prescriptions pr
                join pharmacies ph on ph.id = @pharmacyId
                where pr.id = @id
                  and pr.active = 1
                limit 1;
                """;
            command.Parameters.AddWithValue("id", prescriptionId);
            command.Parameters.Add("pharmacyId", NpgsqlDbType.Integer).Value = request.PharmacyId;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            anchor = new PharmacyRouteAnchor(
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                Pid: ReadInt(reader, "pid"),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                Dosage: ReadNullableString(reader, "dosage"),
                Quantity: ReadNullableString(reader, "quantity"),
                RxNormCode: ReadNullableString(reader, "rx_norm_code"),
                PharmacyId: reader.GetInt32(reader.GetOrdinal("pharmacy_id")),
                PharmacyName: reader.GetString(reader.GetOrdinal("pharmacy_name")),
                PharmacyNcpdp: ReadNullableInt(reader, "ncpdp"));
        }

        var controlledSubstance = GetControlledSubstanceInfo(anchor.Drug, anchor.RxNormCode);
        if (controlledSubstance.ReviewRequired)
        {
            await InsertPrescriptionAuditEventAsync(
                connection,
                transaction,
                prescriptionId,
                anchor.PatientId,
                anchor.Pid,
                "route-blocked",
                sentAt,
                request.Note,
                beforeRefills: null,
                afterRefills: null,
                anchor.PharmacyId,
                anchor.PharmacyName,
                controlledSubstance.Reason,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var detail = await GetForPatientAsync(anchor.PatientId, cancellationToken);
            return detail is null
                ? null
                : new ClinicalPrescriptionPharmacyRouteResponse(
                    prescriptionId,
                    Routed: false,
                    FailureReason: controlledSubstance.Reason,
                    Detail: detail);
        }

        var sentAtText = sentAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var payload = string.Join(
            "\n",
            $"Prescription ID: {prescriptionId}",
            $"Drug: {anchor.Drug}",
            $"Dosage: {anchor.Dosage ?? "Not recorded"}",
            $"Quantity: {anchor.Quantity ?? "Not recorded"}",
            $"Pharmacy: {anchor.PharmacyName}",
            $"NCPDP: {anchor.PharmacyNcpdp?.ToString(CultureInfo.InvariantCulture) ?? "Not recorded"}",
            $"Sent: {sentAtText}");

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update prescriptions
                set pharmacy_id = @pharmacyId,
                    pharmacy_name = @pharmacyName,
                    pharmacy_ncpdp = @pharmacyNcpdp,
                    erx_uploaded = 1,
                    erx_sent_at = @sentAt,
                    erx_payload = @payload,
                    modified_date = @sentDate,
                    note = @note
                where id = @id
                  and active = 1;
                """;
            command.Parameters.AddWithValue("id", prescriptionId);
            command.Parameters.Add("pharmacyId", NpgsqlDbType.Integer).Value = anchor.PharmacyId;
            command.Parameters.AddWithValue("pharmacyName", anchor.PharmacyName);
            AddNullableInt(command, "pharmacyNcpdp", anchor.PharmacyNcpdp);
            command.Parameters.Add("sentAt", NpgsqlDbType.Timestamp).Value = sentAt;
            command.Parameters.AddWithValue("payload", payload);
            command.Parameters.Add("sentDate", NpgsqlDbType.Date).Value = DateOnly.FromDateTime(sentAt);
            command.Parameters.AddWithValue("note", NullableText(request.Note));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertPrescriptionAuditEventAsync(
            connection,
            transaction,
            prescriptionId,
            anchor.PatientId,
            anchor.Pid,
            "route-pharmacy",
            sentAt,
            request.Note,
            beforeRefills: null,
            afterRefills: null,
            anchor.PharmacyId,
            anchor.PharmacyName,
            failureReason: null,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(anchor.PatientId, cancellationToken);
        return lists is null
            ? null
            : new ClinicalPrescriptionPharmacyRouteResponse(
                prescriptionId,
                Routed: true,
                FailureReason: null,
                Detail: lists);
    }

    public async Task<bool> DeletePrescriptionAsync(string prescriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.CommandText = """
                delete from prescription_audit_events
                where prescription_id = @id;
                """;
            auditCommand.Parameters.AddWithValue("id", prescriptionId);
            await auditCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from prescriptions
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", prescriptionId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ClinicalPrescriptionAuditHistoryResponse?> GetPrescriptionAuditHistoryAsync(
        string prescriptionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var exists = connection.CreateCommand())
        {
            exists.CommandText = "select 1 from prescriptions where id = @id limit 1;";
            exists.Parameters.AddWithValue("id", prescriptionId);
            if (await exists.ExecuteScalarAsync(cancellationToken) is null)
            {
                return null;
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                event_id,
                prescription_id,
                action,
                occurred_at,
                actor,
                detail,
                before_refills,
                after_refills,
                pharmacy_id,
                pharmacy_name,
                failure_reason
            from prescription_audit_events
            where prescription_id = @id
            order by occurred_at, event_id;
            """;
        command.Parameters.AddWithValue("id", prescriptionId);

        var events = new List<ClinicalPrescriptionAuditEventItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new ClinicalPrescriptionAuditEventItem(
                EventId: reader.GetString(reader.GetOrdinal("event_id")),
                PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")),
                Action: reader.GetString(reader.GetOrdinal("action")),
                OccurredAt: ReadNullableDateTime(reader, "occurred_at") ?? string.Empty,
                Actor: reader.GetString(reader.GetOrdinal("actor")),
                Detail: ReadNullableString(reader, "detail"),
                BeforeRefills: ReadNullableInt(reader, "before_refills"),
                AfterRefills: ReadNullableInt(reader, "after_refills"),
                PharmacyId: ReadNullableInt(reader, "pharmacy_id"),
                PharmacyName: ReadNullableString(reader, "pharmacy_name"),
                FailureReason: ReadNullableString(reader, "failure_reason")));
        }

        return new ClinicalPrescriptionAuditHistoryResponse(prescriptionId, events.Count, events);
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<ClinicalListPatient?> GetPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name, provider_id
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");

        return new ClinicalListPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            ProviderId: reader.GetInt32(reader.GetOrdinal("provider_id")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    /// <summary>
    /// New clinical content is only valid on the current, active patient record.
    /// The row lock serializes this decision with retirement, death correction,
    /// and merge execution; historical reads deliberately use <see cref="GetPatientAsync"/>.
    /// </summary>
    private static async Task<ClinicalListPatient?> GetActivePatientForNewClinicalContentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name, provider_id
            from patients
            where (lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId)
              and merged_into_patient_id is null
              and coalesce(lower(lifecycle_status), 'active') = 'active'
              and deceased_date is null
            limit 1
            for update;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");
        return new ClinicalListPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            ProviderId: reader.GetInt32(reader.GetOrdinal("provider_id")),
            FirstName: firstName,
            LastName: lastName,
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<IReadOnlyList<ProblemListItem>> GetProblemsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, diagnosis, problem_date, end_date, comments, activity
            from problems
            where pid = @pid
            order by activity desc, problem_date desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<ProblemListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ProblemListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                Date: ReadNullableDate(reader, "problem_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Comments: ReadNullableString(reader, "comments"),
                Activity: reader.GetInt32(reader.GetOrdinal("activity"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<AllergyListItem>> GetAllergiesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, reaction, severity, allergy_date, end_date, comments, activity, list_option_id
            from allergies
            where pid = @pid
            order by activity desc, allergy_date desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<AllergyListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AllergyListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Reaction: ReadNullableString(reader, "reaction"),
                Severity: ReadNullableString(reader, "severity"),
                Date: ReadNullableDate(reader, "allergy_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Comments: ReadNullableString(reader, "comments"),
                Activity: reader.GetInt32(reader.GetOrdinal("activity")),
                ListOptionId: ReadNullableString(reader, "list_option_id")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<MedicationListItem>> GetMedicationsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select medication.id, medication.title, medication.diagnosis, medication.medication_date, medication.end_date,
                   medication.comments, medication.activity, medication.lifecycle_version,
                   (select count(*) from medication_list_lifecycle_events event where event.medication_id = medication.id) as lifecycle_event_count
            from medications medication
            where medication.pid = @pid
            order by medication.activity desc, medication.medication_date desc, medication.id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<MedicationListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MedicationListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                Date: ReadNullableDate(reader, "medication_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Comments: ReadNullableString(reader, "comments"),
                Activity: reader.GetInt32(reader.GetOrdinal("activity")),
                LifecycleVersion: reader.GetInt32(reader.GetOrdinal("lifecycle_version")),
                LifecycleEventCount: reader.GetInt32(reader.GetOrdinal("lifecycle_event_count"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PrescriptionListItem>> GetPrescriptionsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                pr.id,
                pr.drug,
                pr.dosage,
                pr.quantity,
                pr.dose_amount,
                pr.dose_unit,
                pr.frequency,
                pr.duration_days,
                pr.route,
                pr.rx_norm_code,
                pr.diagnosis,
                pr.start_date,
                pr.end_date,
                pr.refills,
                pr.active,
                pr.note,
                pr.encounter,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                pr.pharmacy_id,
                pr.pharmacy_name,
                pr.pharmacy_ncpdp,
                pr.erx_uploaded,
                pr.erx_sent_at,
                pr.erx_payload,
                pr.xmin::text as version
            from prescriptions pr
            left join staff s on s.id = pr.provider_id
            where pr.pid = @pid and pr.active = 1
            order by pr.start_date desc, pr.id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<PrescriptionListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var controlledSubstance = GetControlledSubstanceInfo(
                reader.GetString(reader.GetOrdinal("drug")),
                ReadNullableString(reader, "rx_norm_code"));

            items.Add(new PrescriptionListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                Dosage: ReadNullableString(reader, "dosage"),
                Quantity: ReadNullableString(reader, "quantity"),
                DoseAmount: ReadNullableDecimal(reader, "dose_amount"),
                DoseUnit: ReadNullableString(reader, "dose_unit"),
                Frequency: ReadNullableString(reader, "frequency"),
                DurationDays: ReadNullableInt(reader, "duration_days"),
                Route: ReadNullableString(reader, "route"),
                RxNormCode: ReadNullableString(reader, "rx_norm_code"),
                ControlledSubstanceSchedule: controlledSubstance.Schedule,
                ControlledSubstanceReviewRequired: controlledSubstance.ReviewRequired,
                ControlledSubstanceReason: controlledSubstance.Reason,
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                StartDate: ReadNullableDate(reader, "start_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Refills: ReadInt(reader, "refills"),
                Active: ReadInt(reader, "active"),
                Note: ReadNullableString(reader, "note"),
                Encounter: ReadNullableInt(reader, "encounter"),
                ProviderName: ReadNullableString(reader, "provider_name"),
                PharmacyId: ReadNullableInt(reader, "pharmacy_id"),
                PharmacyName: ReadNullableString(reader, "pharmacy_name"),
                PharmacyNcpdp: ReadNullableInt(reader, "pharmacy_ncpdp"),
                ErxUploaded: ReadInt(reader, "erx_uploaded"),
                ErxSentAt: ReadNullableDateTime(reader, "erx_sent_at"),
                ErxPayload: ReadNullableString(reader, "erx_payload"),
                Version: reader.GetString(reader.GetOrdinal("version"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PrescriptionRefillRequestItem>> GetPrescriptionRefillRequestsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                m.id,
                m.message_date,
                m.title,
                m.body,
                coalesce(
                    lifecycle.status,
                    case when m.message_status = 'Done' then 'approved' else 'pending' end
                ) as lifecycle_status,
                lifecycle.staff_response,
                m.sender_id,
                m.sender_name,
                p.id::text as prescription_id,
                p.drug,
                p.dosage,
                p.quantity,
                p.route,
                p.refills
            from portal_mailbox_messages m
            join prescriptions p
              on p.pid = m.pid
             and p.id::text = nullif(substring(m.body from 'Prescription ID: ([^\r\n]+)'), '')
            left join prescription_refill_request_lifecycle lifecycle
              on lifecycle.thread_id = m.reply_mail_chain
            where m.pid = @pid
              and m.deleted = 0
              and m.owner = m.assigned_to
              and m.portal_relation = 'portal:prescription-refill-request'
              and coalesce(
                    lifecycle.status,
                    case when m.message_status = 'Done' then 'approved' else 'pending' end
                  ) in ('pending', 'clarification-requested')
              and p.active = 1
              and p.end_date is null
            order by m.message_date asc, m.id asc;
            """;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = legacyPid;

        var items = new List<PrescriptionRefillRequestItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var body = reader.GetString(reader.GetOrdinal("body"));
            items.Add(new PrescriptionRefillRequestItem(
                MessageId: reader.GetInt32(reader.GetOrdinal("id")),
                Title: reader.GetString(reader.GetOrdinal("title")),
                RequestDate: ReadNullableDate(reader, "message_date") ?? string.Empty,
                PatientDisplayName: ReadNullableString(reader, "sender_name") ?? string.Empty,
                PortalUsername: ReadNullableString(reader, "sender_id") ?? string.Empty,
                PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                Dosage: ReadNullableString(reader, "dosage"),
                Quantity: ReadNullableString(reader, "quantity"),
                Route: ReadNullableString(reader, "route"),
                CurrentRefills: ReadInt(reader, "refills"),
                Status: reader.GetString(reader.GetOrdinal("lifecycle_status")),
                StaffResponse: ReadNullableString(reader, "staff_response"),
                PatientNote: ReadBodyLineValue(body, "Patient note:"),
                Body: body));
        }

        return items;
    }

    private static async Task<PrescriptionRefillRequestApprovalAnchor?> GetPrescriptionRefillRequestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int messageId,
        bool openOnly,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                m.id,
                m.pid,
                m.reply_mail_chain,
                p.patient_id,
                p.id::text as prescription_id,
                coalesce(
                    lifecycle.status,
                    case when m.message_status = 'Done' then 'approved' else 'pending' end
                ) as lifecycle_status
            from portal_mailbox_messages m
            join prescriptions p
              on p.pid = m.pid
             and p.id::text = nullif(substring(m.body from 'Prescription ID: ([^\r\n]+)'), '')
            left join prescription_refill_request_lifecycle lifecycle
              on lifecycle.thread_id = m.reply_mail_chain
            where m.id = @messageId
              and m.deleted = 0
              and m.owner = m.assigned_to
              and m.portal_relation = 'portal:prescription-refill-request'
              and (
                not @openOnly
                or coalesce(
                    lifecycle.status,
                    case when m.message_status = 'Done' then 'approved' else 'pending' end
                ) in ('pending', 'clarification-requested')
              )
            limit 1
            for update of m;
            """;
        command.Parameters.Add("messageId", NpgsqlDbType.Integer).Value = messageId;
        command.Parameters.AddWithValue("openOnly", openOnly);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PrescriptionRefillRequestApprovalAnchor(
            MessageId: reader.GetInt32(reader.GetOrdinal("id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            ReplyMailChain: ReadInt(reader, "reply_mail_chain"),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")),
            Status: reader.GetString(reader.GetOrdinal("lifecycle_status")));
    }

    private static async Task UpsertPrescriptionRefillLifecycleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PrescriptionRefillRequestApprovalAnchor anchor,
        string status,
        string? staffResponse,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into prescription_refill_request_lifecycle
                (thread_id, staff_message_id, pid, patient_id, prescription_id, status,
                 staff_response, updated_at, updated_by)
            values
                (@threadId, @messageId, @pid, @patientId, @prescriptionId, @status,
                 @staffResponse, @updatedAt, @username)
            on conflict (thread_id) do update
            set staff_message_id = excluded.staff_message_id,
                pid = excluded.pid,
                patient_id = excluded.patient_id,
                prescription_id = excluded.prescription_id,
                status = excluded.status,
                staff_response = excluded.staff_response,
                updated_at = excluded.updated_at,
                updated_by = excluded.updated_by;
            """;
        command.Parameters.Add("threadId", NpgsqlDbType.Integer).Value = anchor.ReplyMailChain;
        command.Parameters.Add("messageId", NpgsqlDbType.Integer).Value = anchor.MessageId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = anchor.LegacyPid;
        command.Parameters.AddWithValue("patientId", anchor.PatientId);
        command.Parameters.AddWithValue("prescriptionId", anchor.PrescriptionId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("staffResponse", NullableText(staffResponse));
        command.Parameters.Add("updatedAt", NpgsqlDbType.Timestamp).Value =
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<MedicationDuplicateSummary> BuildMedicationDuplicates(
        IReadOnlyList<MedicationListItem> medications)
    {
        return medications
            .GroupBy(item => NormalizeMedicationTitle(item.Title))
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(item => item.Date ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToList();
                var dates = ordered
                    .Select(item => item.Date)
                    .Where(date => !string.IsNullOrWhiteSpace(date))
                    .ToList();
                var diagnoses = ordered
                    .Select(item => item.Diagnosis)
                    .Where(diagnosis => !string.IsNullOrWhiteSpace(diagnosis))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(diagnosis => diagnosis, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var displayTitle = ordered
                    .Select(item => item.Title.Trim())
                    .OrderBy(title => title, StringComparer.Ordinal)
                    .First();

                return new MedicationDuplicateSummary(
                    NormalizedTitle: group.Key,
                    DisplayTitle: displayTitle,
                    ActiveCount: ordered.Count,
                    MedicationIds: ordered.Select(item => item.Id).ToList(),
                    FirstDate: dates.FirstOrDefault(),
                    LatestDate: dates.LastOrDefault(),
                    Diagnoses: diagnoses!);
            })
            .OrderBy(item => item.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<PrescriptionDiagnosisInteractionSummary> BuildPrescriptionDiagnosisInteractions(
        IReadOnlyList<ProblemListItem> problems,
        IReadOnlyList<PrescriptionListItem> prescriptions)
    {
        var activeProblemByDiagnosis = problems
            .Where(problem => !string.IsNullOrWhiteSpace(problem.Diagnosis))
            .GroupBy(problem => NormalizeDiagnosis(problem.Diagnosis))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(problem => problem.Date ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(problem => problem.Id, StringComparer.Ordinal)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        return prescriptions
            .Where(prescription => !string.IsNullOrWhiteSpace(prescription.Diagnosis))
            .GroupBy(prescription => NormalizeDiagnosis(prescription.Diagnosis))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                activeProblemByDiagnosis.TryGetValue(group.Key, out var problem);
                var orderedPrescriptions = group
                    .OrderBy(prescription => prescription.Drug, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(prescription => prescription.Id, StringComparer.Ordinal)
                    .ToList();

                return new PrescriptionDiagnosisInteractionSummary(
                    Diagnosis: group.Key,
                    Status: problem is null ? "unmatched" : "matched-active-problem",
                    ProblemId: problem?.Id,
                    ProblemTitle: problem?.Title,
                    PrescriptionCount: orderedPrescriptions.Count,
                    PrescriptionIds: orderedPrescriptions.Select(prescription => prescription.Id).ToList(),
                    Drugs: orderedPrescriptions.Select(prescription => prescription.Drug).ToList());
            })
            .ToList();
    }

    private static IReadOnlyList<MedicationReconciliationSummary> BuildMedicationReconciliations(
        IReadOnlyList<MedicationListItem> medications,
        IReadOnlyList<PrescriptionListItem> prescriptions)
    {
        var medicationGroups = medications
            .Where(medication => !string.IsNullOrWhiteSpace(medication.Title))
            .GroupBy(medication => NormalizeMedicationTitle(medication.Title))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var prescriptionGroups = prescriptions
            .Where(prescription => !string.IsNullOrWhiteSpace(prescription.Drug))
            .GroupBy(prescription => NormalizeMedicationTitle(prescription.Drug))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        return medicationGroups.Keys
            .Concat(prescriptionGroups.Keys)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                medicationGroups.TryGetValue(key, out var medicationGroup);
                prescriptionGroups.TryGetValue(key, out var prescriptionGroup);
                medicationGroup ??= [];
                prescriptionGroup ??= [];

                var orderedMedications = medicationGroup
                    .OrderBy(medication => medication.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(medication => medication.Id, StringComparer.Ordinal)
                    .ToList();
                var orderedPrescriptions = prescriptionGroup
                    .OrderBy(prescription => prescription.Drug, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(prescription => prescription.Id, StringComparer.Ordinal)
                    .ToList();
                var diagnoses = orderedMedications
                    .Select(medication => medication.Diagnosis)
                    .Concat(orderedPrescriptions.Select(prescription => prescription.Diagnosis))
                    .Where(diagnosis => !string.IsNullOrWhiteSpace(diagnosis))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(diagnosis => diagnosis, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var displayTitle = orderedMedications
                    .Select(medication => medication.Title.Trim())
                    .Concat(orderedPrescriptions.Select(prescription => prescription.Drug.Trim()))
                    .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
                    .First();
                var status = (orderedMedications.Count, orderedPrescriptions.Count) switch
                {
                    (> 0, > 0) => "matched",
                    (> 0, 0) => "medication-list-only",
                    _ => "prescription-only"
                };

                return new MedicationReconciliationSummary(
                    NormalizedTitle: key,
                    DisplayTitle: displayTitle,
                    Status: status,
                    MedicationCount: orderedMedications.Count,
                    PrescriptionCount: orderedPrescriptions.Count,
                    MedicationIds: orderedMedications.Select(medication => medication.Id).ToList(),
                    PrescriptionIds: orderedPrescriptions.Select(prescription => prescription.Id).ToList(),
                    MedicationTitles: orderedMedications.Select(medication => medication.Title).ToList(),
                    PrescriptionDrugs: orderedPrescriptions.Select(prescription => prescription.Drug).ToList(),
                    Diagnoses: diagnoses!);
            })
            .ToList();
    }

    private static string NormalizeDiagnosis(string? diagnosis)
    {
        return string.Join(
            ' ',
            (diagnosis ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeMedicationTitle(string title)
    {
        return string.Join(
                " ",
                title.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string? ReadBodyLineValue(string body, string label)
    {
        return body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.StartsWith(label, StringComparison.OrdinalIgnoreCase))?
            .Substring(label.Length)
            .Trim();
    }

    private static async Task<IReadOnlyList<ImmunizationListItem>> GetImmunizationsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                id,
                key,
                immunization_id,
                cvx_code,
                vaccine,
                administered_at,
                manufacturer,
                lot_number,
                administered_by,
                education_date,
                vis_date,
                amount_administered,
                amount_administered_unit,
                expiration_date,
                route,
                administration_site,
                completion_status,
                information_source,
                note,
                encounter,
                added_erroneously
            from immunizations
            where pid = @pid
            order by added_erroneously, administered_at desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<ImmunizationListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ImmunizationListItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Key: reader.GetString(reader.GetOrdinal("key")),
                ImmunizationId: ReadNullableInt(reader, "immunization_id"),
                CvxCode: ReadNullableString(reader, "cvx_code"),
                Vaccine: reader.GetString(reader.GetOrdinal("vaccine")),
                AdministeredAt: ReadNullableDateTime(reader, "administered_at"),
                Manufacturer: ReadNullableString(reader, "manufacturer"),
                LotNumber: ReadNullableString(reader, "lot_number"),
                AdministeredBy: ReadNullableString(reader, "administered_by"),
                EducationDate: ReadNullableDate(reader, "education_date"),
                VisDate: ReadNullableDate(reader, "vis_date"),
                AmountAdministered: ReadNullableDecimal(reader, "amount_administered"),
                AmountAdministeredUnit: ReadNullableString(reader, "amount_administered_unit"),
                ExpirationDate: ReadNullableDate(reader, "expiration_date"),
                Route: ReadNullableString(reader, "route"),
                AdministrationSite: ReadNullableString(reader, "administration_site"),
                CompletionStatus: ReadNullableString(reader, "completion_status"),
                InformationSource: ReadNullableString(reader, "information_source"),
                Note: ReadNullableString(reader, "note"),
                Encounter: ReadNullableInt(reader, "encounter"),
                EnteredInError: reader.GetInt32(reader.GetOrdinal("added_erroneously")) == 1));
        }

        return items;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        return reader.GetInt32(reader.GetOrdinal(columnName));
    }

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private static string? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static bool TryReadDate(string value, out DateOnly date)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryReadDateTime(string value, out DateTime dateTime)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out dateTime);
    }

    private static void AddNullableDecimal(NpgsqlCommand command, string name, decimal? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Numeric).Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private static object NullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task InsertPrescriptionAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string prescriptionId,
        string patientId,
        int pid,
        string action,
        DateTime occurredAt,
        string? detail,
        int? beforeRefills,
        int? afterRefills,
        int? pharmacyId,
        string? pharmacyName,
        string? failureReason,
        CancellationToken cancellationToken,
        string actor = "admin")
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into prescription_audit_events
                (event_id, prescription_id, patient_id, pid, action, occurred_at, actor, detail, before_refills, after_refills,
                 pharmacy_id, pharmacy_name, failure_reason)
            values
                (@eventId, @prescriptionId, @patientId, @pid, @action, @occurredAt, @actor, @detail, @beforeRefills, @afterRefills,
                 @pharmacyId, @pharmacyName, @failureReason);
            """;
        command.Parameters.AddWithValue("eventId", $"RXAUD-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("prescriptionId", prescriptionId);
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = pid;
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("occurredAt", NpgsqlDbType.Timestamp).Value = occurredAt;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("detail", NullableText(detail));
        AddNullableInt(command, "beforeRefills", beforeRefills);
        AddNullableInt(command, "afterRefills", afterRefills);
        AddNullableInt(command, "pharmacyId", pharmacyId);
        command.Parameters.AddWithValue("pharmacyName", NullableText(pharmacyName));
        command.Parameters.AddWithValue("failureReason", NullableText(failureReason));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ControlledSubstanceInfo GetControlledSubstanceInfo(string drug, string? rxNormCode)
    {
        var normalizedDrug = drug.ToUpperInvariant();
        var normalizedRxNorm = (rxNormCode ?? string.Empty).Trim();

        if (normalizedDrug.Contains("OXYCODONE", StringComparison.Ordinal)
            || normalizedDrug.Contains("HYDROCODONE", StringComparison.Ordinal)
            || normalizedDrug.Contains("MORPHINE", StringComparison.Ordinal))
        {
            return new ControlledSubstanceInfo(
                "CII",
                true,
                "Controlled substance requires EPCS review before pharmacy routing.");
        }

        if (normalizedDrug.Contains("ALPRAZOLAM", StringComparison.Ordinal)
            || normalizedDrug.Contains("CLONAZEPAM", StringComparison.Ordinal)
            || normalizedDrug.Contains("LORAZEPAM", StringComparison.Ordinal)
            || normalizedDrug.Contains("DIAZEPAM", StringComparison.Ordinal)
            || normalizedRxNorm is "197901" or "197902")
        {
            return new ControlledSubstanceInfo(
                "CIV",
                true,
                "Controlled substance requires EPCS review before pharmacy routing.");
        }

        return new ControlledSubstanceInfo(null, false, null);
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record ClinicalListPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        int ProviderId,
        string FirstName,
        string LastName,
        string DisplayName);

    private sealed record PrescriptionRefillRequestApprovalAnchor(
        int MessageId,
        int LegacyPid,
        int ReplyMailChain,
        string PatientId,
        string PrescriptionId,
        string Status);

    private sealed record PharmacyRouteAnchor(
        string PatientId,
        int Pid,
        string Drug,
        string? Dosage,
        string? Quantity,
        string? RxNormCode,
        int PharmacyId,
        string PharmacyName,
        int? PharmacyNcpdp);

    private sealed record PrescriptionEditSnapshot(
        string PatientId,
        int Pid,
        string? StartDate,
        string? Dosage,
        string? Quantity,
        decimal? DoseAmount,
        string? DoseUnit,
        string? Frequency,
        int? DurationDays,
        string? Route,
        int Refills,
        string? Diagnosis,
        string? Note,
        bool HadRouteEvidence,
        string Version);

    private sealed record ControlledSubstanceInfo(
        string? Schedule,
        bool ReviewRequired,
        string? Reason);
}
