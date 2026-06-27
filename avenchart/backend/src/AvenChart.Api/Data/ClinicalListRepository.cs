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
        await EnsureMedicationVocabularyTableAsync(connection, cancellationToken);

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
        var medicationDuplicates = BuildMedicationDuplicates(medications);
        var immunizations = await GetImmunizationsAsync(connection, patient.LegacyPid, cancellationToken);
        var prescriptions = await GetPrescriptionsAsync(connection, patient.LegacyPid, cancellationToken);
        var medicationReconciliations = BuildMedicationReconciliations(medications, prescriptions);
        var prescriptionDiagnosisInteractions = BuildPrescriptionDiagnosisInteractions(problems, prescriptions);
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

    public async Task<ClinicalListMutationResponse?> CreateAllergyAsync(
        ClinicalAllergyCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var allergyDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var id = $"ALG-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into allergies
                (id, patient_id, pid, type, title, reaction, severity, allergy_date, comments, activity, end_date, list_option_id)
            values
                (@id, @patientId, @pid, 'allergy', @title, @reaction, @severity, @allergyDate, @comments, 1, null, @listOptionId);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("title", request.Title.Trim());
        command.Parameters.AddWithValue("reaction", NullableText(request.Reaction));
        command.Parameters.AddWithValue("severity", NullableText(request.Severity));
        command.Parameters.Add("allergyDate", NpgsqlDbType.Date).Value = allergyDate;
        command.Parameters.AddWithValue("comments", NullableText(request.Comments));
        command.Parameters.AddWithValue("listOptionId", NullableText(request.ListOptionId));
        await command.ExecuteNonQueryAsync(cancellationToken);

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    public async Task<ClinicalListMutationResponse?> CreateProblemAsync(
        ClinicalProblemCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var problemDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var id = $"PROB-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into problems
                (id, patient_id, pid, type, title, diagnosis, problem_date, comments, activity, end_date)
            values
                (@id, @patientId, @pid, 'medical_problem', @title, @diagnosis, @problemDate, @comments, 1, null);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("title", request.Title.Trim());
        command.Parameters.AddWithValue("diagnosis", NullableText(request.Diagnosis));
        command.Parameters.Add("problemDate", NpgsqlDbType.Date).Value = problemDate;
        command.Parameters.AddWithValue("comments", NullableText(request.Comments));
        await command.ExecuteNonQueryAsync(cancellationToken);

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    public async Task<ClinicalListMutationResponse?> DeactivateProblemAsync(
        string problemId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(problemId))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update problems
                set activity = 0,
                    end_date = @endDate,
                    comments = @comments
                where id = @id and type = 'medical_problem'
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", problemId);
            command.Parameters.Add("endDate", NpgsqlDbType.Date).Value = new DateOnly(2026, 6, 18);
            command.Parameters.AddWithValue("comments", NullableText(request.Comments));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(problemId, lists);
    }

    public async Task<bool> DeleteProblemAsync(string problemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(problemId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from problems
            where id = @id and type = 'medical_problem';
            """;
        command.Parameters.AddWithValue("id", problemId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ClinicalListMutationResponse?> CreateMedicationAsync(
        ClinicalMedicationCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Title)
            || !TryReadDate(request.DateTime, out var medicationDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var id = $"MED-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into medications
                (id, patient_id, pid, type, title, diagnosis, medication_date, modified_date, comments, activity, end_date)
            values
                (@id, @patientId, @pid, 'medication', @title, @diagnosis, @medicationDate, @medicationDate, @comments, 1, null);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("title", request.Title.Trim());
        command.Parameters.AddWithValue("diagnosis", NullableText(request.Diagnosis));
        command.Parameters.Add("medicationDate", NpgsqlDbType.Date).Value = medicationDate;
        command.Parameters.AddWithValue("comments", NullableText(request.Comments));
        await command.ExecuteNonQueryAsync(cancellationToken);

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    public async Task<ClinicalListMutationResponse?> DeactivateMedicationAsync(
        string medicationId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update medications
                set activity = 0,
                    end_date = @endDate,
                    comments = @comments
                where id = @id and type = 'medication'
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", medicationId);
            command.Parameters.Add("endDate", NpgsqlDbType.Date).Value = new DateOnly(2026, 6, 18);
            command.Parameters.AddWithValue("comments", NullableText(request.Comments));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(medicationId, lists);
    }

    public async Task<bool> DeleteMedicationAsync(string medicationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(medicationId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from medications
            where id = @id and type = 'medication';
            """;
        command.Parameters.AddWithValue("id", medicationId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ClinicalListMutationResponse?> DeactivateAllergyAsync(
        string allergyId,
        ClinicalListDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(allergyId))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update allergies
                set activity = 0,
                    end_date = @endDate,
                    comments = @comments
                where id = @id and type = 'allergy'
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", allergyId);
            command.Parameters.Add("endDate", NpgsqlDbType.Date).Value = new DateOnly(2026, 6, 18);
            command.Parameters.AddWithValue("comments", NullableText(request.Comments));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(allergyId, lists);
    }

    public async Task<bool> DeleteAllergyAsync(string allergyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(allergyId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from allergies
            where id = @id and type = 'allergy';
            """;
        command.Parameters.AddWithValue("id", allergyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ClinicalListMutationResponse?> CreatePrescriptionAsync(
        ClinicalPrescriptionCreateRequest request,
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
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        await EnsurePrescriptionStructuredDoseColumnsAsync(connection, cancellationToken);
        await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);

        var id = $"RX-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
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
            transaction: null,
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
            cancellationToken);

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id, lists);
    }

    public async Task<ClinicalListMutationResponse?> DeactivatePrescriptionAsync(
        string prescriptionId,
        ClinicalPrescriptionDeactivateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId) || !TryReadDate(request.EndDate, out var endDate))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);
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
                var pid = ReadInt(reader, "pid");
                await reader.DisposeAsync();
                await InsertPrescriptionAuditEventAsync(
                    connection,
                    transaction: null,
                    prescriptionId,
                    patientId,
                    pid,
                    "deactivate",
                    endDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
                    request.Note,
                    beforeRefills: null,
                    afterRefills: null,
                    pharmacyId: null,
                    pharmacyName: null,
                    failureReason: null,
                    cancellationToken);
            }
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(prescriptionId, lists);
    }

    public async Task<ClinicalListMutationResponse?> RefillPrescriptionAsync(
        string prescriptionId,
        ClinicalPrescriptionRefillRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId)
            || request.AdditionalRefills <= 0
            || !TryReadDate(request.RefillDate, out var refillDate))
        {
            return null;
        }

        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);
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
                var pid = ReadInt(reader, "pid");
                var afterRefills = ReadInt(reader, "refills");
                await reader.DisposeAsync();
                await InsertPrescriptionAuditEventAsync(
                    connection,
                    transaction: null,
                    prescriptionId: prescriptionId,
                    patientId: patientId,
                    pid: pid,
                    action: "refill",
                    occurredAt: refillDate.ToDateTime(TimeOnly.Parse("10:00", CultureInfo.InvariantCulture)),
                    detail: request.Note,
                    beforeRefills: afterRefills - request.AdditionalRefills,
                    afterRefills: afterRefills,
                    pharmacyId: null,
                    pharmacyName: null,
                    failureReason: null,
                    cancellationToken);
            }
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(prescriptionId, lists);
    }

    public async Task<ClinicalListMutationResponse?> ApprovePrescriptionRefillRequestAsync(
        int messageId,
        ClinicalPrescriptionRefillApprovalRequest request,
        CancellationToken cancellationToken)
    {
        if (messageId <= 0
            || request.AdditionalRefills <= 0
            || !TryReadDate(request.RefillDate, out var refillDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);
        var refillRequest = await GetPrescriptionRefillRequestAsync(connection, messageId, cancellationToken);
        if (refillRequest is null)
        {
            return null;
        }

        string? patientId;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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
                    cancellationToken);
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

        await transaction.CommitAsync(cancellationToken);

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(refillRequest.PrescriptionId, lists);
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
        await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);
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
        await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);
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
        await EnsurePrescriptionAuditTableAsync(connection, cancellationToken);

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

    public async Task<ClinicalListMutationResponse?> CreateImmunizationAsync(
        ClinicalImmunizationCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.Vaccine)
            || !TryReadDateTime(request.AdministeredAt, out var administeredAt)
            || !TryReadOptionalDate(request.EducationDate, out var educationDate)
            || !TryReadOptionalDate(request.VisDate, out var visDate)
            || !TryReadOptionalDate(request.ExpirationDate, out var expirationDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var key = $"IMM-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with next_immunization as (
                select coalesce(max(id), 8500000) + 1 as id
                from immunizations
            )
            insert into immunizations
                (id, key, patient_id, pid, encounter, immunization_id, cvx_code, vaccine, administered_at,
                 manufacturer, lot_number, administered_by_id, administered_by, education_date, vis_date,
                 amount_administered, amount_administered_unit, expiration_date, route, administration_site,
                 completion_status, information_source, note, added_erroneously)
            select
                next_immunization.id, @key, @patientId, @pid, @encounter, @immunizationId, @cvxCode, @vaccine,
                @administeredAt, @manufacturer, @lotNumber, @administeredById, @administeredBy, @educationDate,
                @visDate, @amountAdministered, @amountAdministeredUnit, @expirationDate, @route, @administrationSite,
                @completionStatus, @informationSource, @note, 0
            from next_immunization
            returning id;
            """;
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("patientId", patient.PatientId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        AddNullableInt(command, "encounter", request.Encounter);
        AddNullableInt(command, "immunizationId", request.ImmunizationId);
        command.Parameters.AddWithValue("cvxCode", NullableText(request.CvxCode));
        command.Parameters.AddWithValue("vaccine", request.Vaccine.Trim());
        command.Parameters.Add("administeredAt", NpgsqlDbType.Timestamp).Value = administeredAt;
        command.Parameters.AddWithValue("manufacturer", NullableText(request.Manufacturer));
        command.Parameters.AddWithValue("lotNumber", NullableText(request.LotNumber));
        AddNullableInt(command, "administeredById", request.AdministeredById ?? patient.ProviderId);
        command.Parameters.AddWithValue("administeredBy", NullableText(request.AdministeredBy));
        AddNullableDate(command, "educationDate", educationDate);
        AddNullableDate(command, "visDate", visDate);
        AddNullableDecimal(command, "amountAdministered", request.AmountAdministered);
        command.Parameters.AddWithValue("amountAdministeredUnit", NullableText(request.AmountAdministeredUnit));
        AddNullableDate(command, "expirationDate", expirationDate);
        command.Parameters.AddWithValue("route", NullableText(request.Route));
        command.Parameters.AddWithValue("administrationSite", NullableText(request.AdministrationSite));
        command.Parameters.AddWithValue("completionStatus", NullableText(request.CompletionStatus));
        command.Parameters.AddWithValue("informationSource", NullableText(request.InformationSource));
        command.Parameters.AddWithValue("note", NullableText(request.Note));

        var id = (int?)await command.ExecuteScalarAsync(cancellationToken);
        if (id is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(id.Value.ToString(CultureInfo.InvariantCulture), lists);
    }

    public async Task<ClinicalListMutationResponse?> MarkImmunizationEnteredInErrorAsync(
        int immunizationId,
        ClinicalImmunizationErrorRequest request,
        CancellationToken cancellationToken)
    {
        string? patientId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update immunizations
                set added_erroneously = 1,
                    note = @note
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", immunizationId);
            command.Parameters.AddWithValue("note", NullableText(request.Note));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null
            ? null
            : new ClinicalListMutationResponse(immunizationId.ToString(CultureInfo.InvariantCulture), lists);
    }

    public async Task<bool> DeleteImmunizationAsync(int immunizationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from immunizations
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", immunizationId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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

    private static async Task<IReadOnlyList<ProblemListItem>> GetProblemsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, title, diagnosis, problem_date, comments, activity
            from problems
            where pid = @pid and activity = 1
            order by problem_date desc, id;
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
            select id, title, reaction, severity, allergy_date, comments, activity, list_option_id
            from allergies
            where pid = @pid and activity = 1
            order by allergy_date desc, id;
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
            select id, title, diagnosis, medication_date, comments, activity
            from medications
            where pid = @pid and activity = 1
            order by medication_date desc, id;
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
                Comments: ReadNullableString(reader, "comments"),
                Activity: reader.GetInt32(reader.GetOrdinal("activity"))));
        }

        return items;
    }

    private static async Task<IReadOnlyList<PrescriptionListItem>> GetPrescriptionsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await EnsurePrescriptionStructuredDoseColumnsAsync(connection, cancellationToken);

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
                pr.erx_payload
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
                ErxPayload: ReadNullableString(reader, "erx_payload")));
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
                m.message_status,
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
            where m.pid = @pid
              and m.deleted = 0
              and m.owner = m.assigned_to
              and m.portal_relation = 'portal:prescription-refill-request'
              and m.message_status = 'New'
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
                Status: reader.GetString(reader.GetOrdinal("message_status")),
                PatientNote: ReadBodyLineValue(body, "Patient note:"),
                Body: body));
        }

        return items;
    }

    private static async Task<PrescriptionRefillRequestApprovalAnchor?> GetPrescriptionRefillRequestAsync(
        NpgsqlConnection connection,
        int messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                m.id,
                m.pid,
                m.reply_mail_chain,
                p.id::text as prescription_id
            from portal_mailbox_messages m
            join prescriptions p
              on p.pid = m.pid
             and p.id::text = nullif(substring(m.body from 'Prescription ID: ([^\r\n]+)'), '')
            where m.id = @messageId
              and m.deleted = 0
              and m.owner = m.assigned_to
              and m.portal_relation = 'portal:prescription-refill-request'
              and m.message_status = 'New'
              and p.active = 1
              and p.end_date is null
            limit 1;
            """;
        command.Parameters.Add("messageId", NpgsqlDbType.Integer).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PrescriptionRefillRequestApprovalAnchor(
            MessageId: reader.GetInt32(reader.GetOrdinal("id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
            ReplyMailChain: ReadInt(reader, "reply_mail_chain"),
            PrescriptionId: reader.GetString(reader.GetOrdinal("prescription_id")));
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
                encounter
            from immunizations
            where pid = @pid and added_erroneously = 0
            order by administered_at desc, id;
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
                Encounter: ReadNullableInt(reader, "encounter")));
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

    private static bool TryReadOptionalDate(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (TryReadDate(value, out var parsedDate))
        {
            date = parsedDate;
            return true;
        }

        date = null;
        return false;
    }

    private static void AddNullableDate(NpgsqlCommand command, string name, DateOnly? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Date).Value = value.HasValue ? value.Value : DBNull.Value;
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

    private static async Task EnsureMedicationVocabularyTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists medication_vocabulary (
                rx_norm_code text primary key,
                drug_name text not null,
                display_name text not null,
                form text not null,
                strength text not null,
                route text not null,
                dose_amount numeric(10,2),
                dose_unit text,
                frequency text,
                duration_days integer,
                controlled_substance_schedule text
            );

            insert into medication_vocabulary
                (rx_norm_code, drug_name, display_name, form, strength, route, dose_amount, dose_unit, frequency, duration_days, controlled_substance_schedule)
            values
                ('860975', 'Metformin', 'Metformin 500 mg tablet', 'tablet', '500 mg', 'oral', 500, 'mg', 'twice daily', 30, null),
                ('1049502', 'Omeprazole', 'Omeprazole 20 mg delayed release capsule', 'capsule', '20 mg', 'oral', 20, 'mg', 'once daily', 30, null),
                ('312615', 'Lisinopril', 'Lisinopril 10 mg tablet', 'tablet', '10 mg', 'oral', 10, 'mg', 'once daily', 30, null),
                ('617314', 'Atorvastatin', 'Atorvastatin 20 mg tablet', 'tablet', '20 mg', 'oral', 20, 'mg', 'nightly', 30, null),
                ('1049621', 'Oxycodone', 'Oxycodone 5 mg tablet', 'tablet', '5 mg', 'oral', 5, 'mg', 'every 6 hours as needed', 7, 'CII')
            on conflict (rx_norm_code) do update
            set drug_name = excluded.drug_name,
                display_name = excluded.display_name,
                form = excluded.form,
                strength = excluded.strength,
                route = excluded.route,
                dose_amount = excluded.dose_amount,
                dose_unit = excluded.dose_unit,
                frequency = excluded.frequency,
                duration_days = excluded.duration_days,
                controlled_substance_schedule = excluded.controlled_substance_schedule;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsurePrescriptionStructuredDoseColumnsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            alter table prescriptions
                add column if not exists dose_amount numeric(10,2),
                add column if not exists dose_unit text,
                add column if not exists frequency text,
                add column if not exists duration_days integer;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsurePrescriptionAuditTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists prescription_audit_events (
                event_id text primary key,
                prescription_id text not null,
                patient_id text not null,
                pid integer not null,
                action text not null,
                occurred_at timestamp not null,
                actor text not null,
                detail text,
                before_refills integer,
                after_refills integer,
                pharmacy_id integer,
                pharmacy_name text,
                failure_reason text
            );
            create index if not exists idx_prescription_audit_events_prescription
                on prescription_audit_events (prescription_id, occurred_at, event_id);
            create index if not exists idx_prescription_audit_events_pid
                on prescription_audit_events (pid, occurred_at desc, event_id desc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
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
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into prescription_audit_events
                (event_id, prescription_id, patient_id, pid, action, occurred_at, actor, detail, before_refills, after_refills,
                 pharmacy_id, pharmacy_name, failure_reason)
            values
                (@eventId, @prescriptionId, @patientId, @pid, @action, @occurredAt, 'admin', @detail, @beforeRefills, @afterRefills,
                 @pharmacyId, @pharmacyName, @failureReason);
            """;
        command.Parameters.AddWithValue("eventId", $"RXAUD-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("prescriptionId", prescriptionId);
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = pid;
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("occurredAt", NpgsqlDbType.Timestamp).Value = occurredAt;
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
        string PrescriptionId);

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

    private sealed record ControlledSubstanceInfo(
        string? Schedule,
        bool ReviewRequired,
        string? Reason);
}
