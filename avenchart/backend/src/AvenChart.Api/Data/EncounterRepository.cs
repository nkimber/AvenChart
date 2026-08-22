// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class EncounterRepository(
    NpgsqlDataSource dataSource,
    DocumentRepository documentRepository)
{
    private const int MaximumSearchLimit = 100;
    private const string SignatureContentRevision = "encounter-signature-content-v1";

    public async Task<EncounterSearchResponse> SearchAsync(
        string? patientId,
        string? from,
        int limit,
        CancellationToken cancellationToken,
        bool archived = false)
    {
        var safeLimit = Math.Clamp(limit, 1, MaximumSearchLimit);
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedPatientId = Normalize(patientId);
        var fromDate = ParseDateOrDefault(from, new DateOnly(metadata.BaseDate.Year, 1, 1));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var totalMatches = await CountMatchesAsync(connection, normalizedPatientId, fromDate, archived, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
                e.id,
                e.encounter,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                e.encounter_date,
                e.reason,
                e.diagnosis_code,
                e.diagnosis_text,
                e.category_id,
                e.sensitivity,
                e.referral_source,
                e.external_id,
                e.pos_code,
                e.source_appointment_id,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name,
                exists (select 1 from vitals v where v.pid = e.pid and v.encounter = e.encounter) as has_vitals,
                exists (select 1 from clinical_notes cn where cn.pid = e.pid and cn.encounter = e.encounter) as has_soap_note,
                (select count(*) from billing b where b.pid = e.pid and b.encounter = e.encounter and b.activity = 1)::int as billing_line_count
            from encounters e
            join patients p on p.legacy_pid = e.pid
            left join staff s on s.id = e.provider_id
            left join facilities f on f.id = e.facility_id
            where {EncounterSearchPredicate}
              and e.archived_at is {(archived ? "not" : string.Empty)} null
            order by e.encounter_date desc, e.encounter desc
            limit @limit;
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate);
        command.Parameters.AddWithValue("limit", safeLimit);

        var encounters = new List<EncounterListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            encounters.Add(ReadListItem(reader));
        }

        return new EncounterSearchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patientId,
            FromDate: fromDate.ToString("yyyy-MM-dd"),
            Limit: safeLimit,
            TotalMatches: totalMatches,
            Encounters: encounters);
    }

    public async Task<EncounterDetail?> GetByEncounterAsync(
        int encounter,
        CancellationToken cancellationToken,
        bool includeArchivedDocuments = false)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                e.id,
                e.encounter,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.sex,
                p.date_of_birth,
                e.encounter_date,
                e.encounter_datetime,
                e.reason,
                e.diagnosis_code,
                e.diagnosis_text,
                e.category_id,
                e.sensitivity,
                e.referral_source,
                e.external_id,
                e.pos_code,
                e.billing_note,
                e.source_appointment_id,
                e.archived_at,
                e.archive_version,
                e.row_version,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name,
                v.bps,
                v.bpd,
                v.weight,
                v.height,
                v.temperature,
                v.pulse,
                v.respiration,
                v.bmi,
                v.oxygen_saturation,
                cn.id as soap_note_id,
                cn.version as soap_note_version,
                cn.note_datetime as soap_note_datetime,
                cn.saved_at as soap_note_saved_at,
                cn.saved_by as soap_note_saved_by,
                cn.evidence_source as soap_note_evidence_source,
                cn.subjective,
                cn.objective,
                cn.assessment,
                cn.plan,
                (select count(*) from billing b where b.pid = e.pid and b.encounter = e.encounter and b.activity = 1)::int as billing_line_count
            from encounters e
            join patients p on p.legacy_pid = e.pid
            left join staff s on s.id = e.provider_id
            left join facilities f on f.id = e.facility_id
            left join lateral (
                select *
                from vitals
                where pid = e.pid and encounter = e.encounter
                order by vital_datetime desc, id desc
                limit 1
            ) v on true
            left join lateral (
                select *
                from clinical_notes
                where pid = e.pid and encounter = e.encounter
                order by version desc, id desc
                limit 1
            ) cn on true
            where e.encounter = @encounter;
            """;
        command.Parameters.AddWithValue("encounter", encounter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var detail = new EncounterDetail(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            PatientDisplayName: BuildDisplayName(reader),
            FirstName: reader.GetString(reader.GetOrdinal("first_name")),
            LastName: reader.GetString(reader.GetOrdinal("last_name")),
            Sex: ReadNullableString(reader, "sex"),
            DateOfBirth: ReadDate(reader, "date_of_birth"),
            Date: ReadDate(reader, "encounter_date"),
            DateTime: ReadDateTime(reader, "encounter_datetime"),
            Reason: ReadNullableString(reader, "reason"),
            DiagnosisCode: ReadNullableString(reader, "diagnosis_code"),
            DiagnosisText: ReadNullableString(reader, "diagnosis_text"),
            CategoryId: ReadNullableInt(reader, "category_id"),
            ProviderName: ReadNullableString(reader, "provider_name"),
            FacilityName: ReadNullableString(reader, "facility_name"),
            Sensitivity: ReadNullableString(reader, "sensitivity"),
            ReferralSource: ReadNullableString(reader, "referral_source"),
            ExternalId: ReadNullableString(reader, "external_id"),
            PosCode: ReadNullableInt(reader, "pos_code"),
            BillingNote: ReadNullableString(reader, "billing_note"),
            SourceAppointmentId: ReadNullableString(reader, "source_appointment_id"),
            ArchivedAt: ReadNullableDateTime(reader, "archived_at"),
            ArchiveVersion: reader.GetInt32(reader.GetOrdinal("archive_version")),
            RowVersion: reader.GetInt64(reader.GetOrdinal("row_version")),
            Vitals: ReadVitals(reader),
            SoapNote: ReadSoapNote(reader),
            BillingLineCount: reader.GetInt32(reader.GetOrdinal("billing_line_count")),
            DiagnosisCodes: Array.Empty<EncounterDiagnosisCode>(),
            BillingLines: Array.Empty<BillingLineItem>(),
            Claims: Array.Empty<BillingClaimItem>(),
            ProcedureOrders: Array.Empty<ProcedureOrderItem>(),
            Signatures: Array.Empty<EncounterSignatureItem>(),
            AmendmentHistory: Array.Empty<EncounterAmendmentHistoryItem>(),
            Documents: Array.Empty<EncounterDocumentAttachment>());

        await reader.DisposeAsync();
        var billingLines = await GetBillingLinesForEncounterAsync(connection, detail.LegacyPid, detail.Encounter, cancellationToken);
        var claims = await GetClaimsForEncounterAsync(connection, detail.LegacyPid, detail.Encounter, cancellationToken);
        var procedureOrders = await GetProcedureOrdersForEncounterAsync(connection, detail.LegacyPid, detail.Encounter, cancellationToken);
        var signatures = await GetSignaturesForEncounterAsync(connection, detail.Encounter, cancellationToken);
        var soapNoteVersions = await GetSoapNoteVersionsAsync(connection, detail.Encounter, cancellationToken);
        var diagnosisCodes = BuildDiagnosisCodes(detail, billingLines, procedureOrders);
        var patientDocuments = await documentRepository.GetForPatientAsync(
            detail.PatientId,
            cancellationToken,
            includeArchivedDocuments);
        var documents = patientDocuments?.Documents
            .Where(document => document.Encounter == detail.Encounter)
            .Select(MapEncounterDocument)
            .ToArray()
            ?? Array.Empty<EncounterDocumentAttachment>();
        return detail with
        {
            DiagnosisCodes = diagnosisCodes,
            BillingLineCount = billingLines.Count,
            BillingLines = billingLines,
            Claims = claims,
            ProcedureOrders = procedureOrders,
            Signatures = signatures,
            AmendmentHistory = BuildAmendmentHistory(signatures),
            SoapNote = detail.SoapNote is null
                ? null
                : detail.SoapNote with
                {
                    IsLocked = signatures.Any(signature => signature.IsLock),
                    Versions = soapNoteVersions
                },
            Documents = documents
        };
    }

    public async Task<EncounterSoapNoteTemplateCatalogResponse> GetSoapNoteTemplateCatalogAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        return new EncounterSoapNoteTemplateCatalogResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Templates: SoapNoteTemplateOptions);
    }

    public async Task<EncounterDetail?> CreateAsync(EncounterCreateRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var encounter = await CreateCoreAsync(connection, null, request, cancellationToken);
        return encounter is null
            ? null
            : await GetByEncounterAsync(encounter.Value, cancellationToken);
    }

    internal Task<int?> CreateInTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        EncounterCreateRequest request,
        CancellationToken cancellationToken) =>
        CreateCoreAsync(connection, transaction, request, cancellationToken);

    private static async Task<int?> CreateCoreAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        EncounterCreateRequest request,
        CancellationToken cancellationToken)
    {
        var patientId = Normalize(request.PatientId);
        var reason = NormalizeText(request.Reason);
        if (patientId is null || reason is null || !TryParseDateTime(request.DateTime, out var encounterDateTime))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with selected_patient as (
                select canonical_id, legacy_pid, provider_id as patient_provider_id, facility_id as patient_facility_id
                from patients
                where (lower(canonical_id) = @patientId
                       or lower(pubpid) = @patientId
                       or legacy_pid::text = @patientId)
                  and merged_into_patient_id is null
                  and coalesce(lower(lifecycle_status), 'active') = 'active'
                  and deceased_date is null
                limit 1
                for update
            ),
            next_id as (
                select nextval('encounters_id_seq')::integer as id
            ),
            source_appointment as (
                select a.id,
                       a.patient_id,
                       a.provider_id,
                       a.facility_id,
                       a.billing_location_id,
                       a.appointment_date,
                       a.start_time,
                       a.title
                from appointments a
                join selected_patient p on p.canonical_id = a.patient_id
                where a.id = @sourceAppointmentId
                limit 1
            )
            insert into encounters (
                id,
                encounter,
                patient_id,
                pid,
                provider_id,
                facility_id,
                billing_facility_id,
                encounter_date,
                encounter_datetime,
                reason,
                diagnosis_code,
                diagnosis_text,
                category_id,
                sensitivity,
                referral_source,
                external_id,
                pos_code,
                billing_note,
                source_appointment_id
            )
            select
                next_id.id,
                next_id.id,
                selected_patient.canonical_id,
                selected_patient.legacy_pid,
                coalesce(
                    (select id from staff where id = @providerId),
                    source_appointment.provider_id,
                    selected_patient.patient_provider_id,
                    (select id from staff where role = 'provider' order by id limit 1)
                ),
                coalesce(
                    (select id from facilities where id = @facilityId),
                    source_appointment.facility_id,
                    selected_patient.patient_facility_id,
                    (select id from facilities order by id limit 1)
                ),
                coalesce(
                    (select id from facilities where id = @billingFacilityId),
                    (select id from facilities where id = @facilityId),
                    source_appointment.billing_location_id,
                    source_appointment.facility_id,
                    selected_patient.patient_facility_id,
                    (select id from facilities order by id limit 1)
                ),
                @encounterDate,
                @encounterDateTime,
                @reason,
                null,
                null,
                9,
                @sensitivity,
                @referralSource,
                @externalId,
                @posCode,
                @billingNote,
                source_appointment.id
            from selected_patient
            cross join next_id
            left join source_appointment on true
            where @sourceAppointmentId is null or source_appointment.id is not null
            returning encounter;
            """;
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId;
        AddNullableInt(command, "providerId", request.ProviderId);
        AddNullableInt(command, "facilityId", request.FacilityId);
        AddNullableInt(command, "billingFacilityId", request.BillingFacilityId);
        command.Parameters.Add("encounterDate", NpgsqlDbType.Date).Value = DateOnly.FromDateTime(encounterDateTime);
        command.Parameters.Add("encounterDateTime", NpgsqlDbType.Timestamp).Value = encounterDateTime;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason;
        AddNullableText(command, "sensitivity", NormalizeText(request.Sensitivity));
        AddNullableText(command, "referralSource", NormalizeText(request.ReferralSource));
        AddNullableText(command, "externalId", NormalizeText(request.ExternalId));
        AddNullableInt(command, "posCode", request.PosCode);
        AddNullableText(command, "billingNote", NormalizeText(request.BillingNote));
        AddNullableText(command, "sourceAppointmentId", NormalizeText(request.SourceAppointmentId));

        var encounter = await command.ExecuteScalarAsync(cancellationToken);
        return encounter is null || encounter is DBNull ? null : Convert.ToInt32(encounter);
    }

    public async Task<EncounterAuditHistoryResponse?> GetAuditHistoryAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var exists = connection.CreateCommand();
        exists.CommandText = "select exists(select 1 from encounters where encounter = @encounter);";
        exists.Parameters.AddWithValue("encounter", encounter);
        if (await exists.ExecuteScalarAsync(cancellationToken) is not true)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, occurred_at, username, action, changed_fields
            from encounter_audit_events
            where encounter = @encounter
            order by occurred_at desc, event_id desc
            limit 100;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        var events = new List<EncounterAuditEventItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new EncounterAuditEventItem(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1).ToString("O"),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        }
        return new EncounterAuditHistoryResponse(encounter, events.Count, events);
    }

    public async Task<bool> HasLockingSignatureAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await IsEncounterLockedAsync(connection, encounter, cancellationToken);
    }

    public async Task<EncounterFormMutationResponse?> CreateSoapNoteAsync(
        int encounter,
        EncounterSoapNoteCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (!TryParseDateTime(request.DateTime, out var noteDateTime))
        {
            throw new ArgumentException("SOAP note date/time must be a valid ISO-style timestamp.");
        }
        noteDateTime = DateTime.SpecifyKind(noteDateTime, DateTimeKind.Unspecified);

        var savedBy = NormalizeText(actor)
            ?? throw new ArgumentException("An authenticated staff identity is required.");
        var subjective = NormalizeSoapField(request.Subjective, "Subjective");
        var objective = NormalizeSoapField(request.Objective, "Objective");
        var assessment = NormalizeSoapField(request.Assessment, "Assessment");
        var plan = NormalizeSoapField(request.Plan, "Plan");
        if (subjective is null && objective is null && assessment is null && plan is null)
        {
            throw new ArgumentException("At least one SOAP section is required.");
        }

        if (request.ExpectedVersion is < 0)
        {
            throw new ArgumentException("Expected version cannot be negative.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        string patientId;
        int pid;
        bool isLocked;
        await using (var encounterCommand = connection.CreateCommand())
        {
            encounterCommand.Transaction = transaction;
            encounterCommand.CommandText = """
                select
                    patient_id,
                    pid,
                    exists (
                        select 1
                        from encounter_signatures signature
                        where signature.encounter = encounters.encounter
                          and signature.is_lock
                    ) as is_locked
                from encounters
                where encounter = @encounter
                for update;
                """;
            encounterCommand.Parameters.AddWithValue("encounter", encounter);
            await using var encounterReader = await encounterCommand.ExecuteReaderAsync(cancellationToken);
            if (!await encounterReader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("The encounter was not found.");
            }

            patientId = encounterReader.GetString(encounterReader.GetOrdinal("patient_id"));
            pid = encounterReader.GetInt32(encounterReader.GetOrdinal("pid"));
            isLocked = encounterReader.GetBoolean(encounterReader.GetOrdinal("is_locked"));
        }

        var currentVersion = 0;
        int? currentNoteId = null;
        string? currentSubjective = null;
        string? currentObjective = null;
        string? currentAssessment = null;
        string? currentPlan = null;
        await using (var currentCommand = connection.CreateCommand())
        {
            currentCommand.Transaction = transaction;
            currentCommand.CommandText = """
                select id, version, subjective, objective, assessment, plan
                from clinical_notes
                where encounter = @encounter
                order by version desc, id desc
                limit 1
                for update;
                """;
            currentCommand.Parameters.AddWithValue("encounter", encounter);
            await using var currentReader = await currentCommand.ExecuteReaderAsync(cancellationToken);
            if (await currentReader.ReadAsync(cancellationToken))
            {
                currentNoteId = currentReader.GetInt32(currentReader.GetOrdinal("id"));
                currentVersion = currentReader.GetInt32(currentReader.GetOrdinal("version"));
                currentSubjective = ReadNullableString(currentReader, "subjective");
                currentObjective = ReadNullableString(currentReader, "objective");
                currentAssessment = ReadNullableString(currentReader, "assessment");
                currentPlan = ReadNullableString(currentReader, "plan");
            }
        }

        if (isLocked)
        {
            throw new EncounterSoapNoteConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.",
                currentVersion,
                true);
        }

        if (request.ExpectedVersion is { } expectedVersion && expectedVersion != currentVersion)
        {
            throw new EncounterSoapNoteConflictException(
                $"SOAP note version {currentVersion} is current; the submitted draft was based on version {expectedVersion}.",
                currentVersion,
                false);
        }

        if (currentNoteId is not null
            && string.Equals(currentSubjective, subjective, StringComparison.Ordinal)
            && string.Equals(currentObjective, objective, StringComparison.Ordinal)
            && string.Equals(currentAssessment, assessment, StringComparison.Ordinal)
            && string.Equals(currentPlan, plan, StringComparison.Ordinal))
        {
            throw new ArgumentException("The SOAP draft does not change the current saved version.");
        }

        var savedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into clinical_notes (
                id,
                patient_id,
                pid,
                encounter,
                note_datetime,
                version,
                supersedes_note_id,
                saved_at,
                saved_by,
                evidence_source,
                subjective,
                objective,
                assessment,
                plan
            )
            values (
                nextval('clinical_note_id_seq'),
                @patientId,
                @pid,
                @encounter,
                @noteDateTime,
                @version,
                @supersedesNoteId,
                @savedAt,
                @savedBy,
                'runtime',
                @subjective,
                @objective,
                @assessment,
                @plan
            )
            returning id;
            """;
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.Add("noteDateTime", NpgsqlDbType.Timestamp).Value = noteDateTime;
        command.Parameters.AddWithValue("version", currentVersion + 1);
        command.Parameters.Add("supersedesNoteId", NpgsqlDbType.Integer).Value =
            currentNoteId is null ? DBNull.Value : currentNoteId.Value;
        command.Parameters.Add("savedAt", NpgsqlDbType.Timestamp).Value = savedAt;
        command.Parameters.Add("savedBy", NpgsqlDbType.Text).Value = savedBy;
        AddNullableText(command, "subjective", subjective);
        AddNullableText(command, "objective", objective);
        AddNullableText(command, "assessment", assessment);
        AddNullableText(command, "plan", plan);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        if (id is null || id is DBNull)
        {
            throw new InvalidOperationException("SOAP note persistence did not return an identifier.");
        }

        await using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.Transaction = transaction;
            versionCommand.CommandText = "update encounters set row_version = row_version + 1 where encounter = @encounter;";
            versionCommand.Parameters.AddWithValue("encounter", encounter);
            await versionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var detail = await GetByEncounterAsync(encounter, cancellationToken);
        return detail is null ? null : new EncounterFormMutationResponse(Convert.ToInt32(id), detail);
    }

    public async Task<EncounterSignatureMutationResponse?> SignAsync(
        int encounter,
        EncounterSignRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var signerUsername = NormalizeText(actor);
        if (signerUsername is null)
        {
            return null;
        }
        var signedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        long encounterVersion;
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "select row_version from encounters where encounter = @encounter for update;";
            lockCommand.Parameters.AddWithValue("encounter", encounter);
            var result = await lockCommand.ExecuteScalarAsync(cancellationToken);
            if (result is null || result is DBNull)
            {
                return null;
            }

            encounterVersion = Convert.ToInt64(result);
        }
        var contentSnapshot = await CaptureSignatureContentSnapshotAsync(
            connection,
            transaction,
            encounter,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with selected_encounter as (
                select id, encounter, patient_id, pid, row_version
                from encounters
                where encounter = @encounter
                limit 1
            ),
            selected_user as (
                select id, username
                from staff
                where lower(username) = lower(@signerUsername)
                union all
                select null::integer as id, user_value as username
                from access_user_memberships
                where lower(user_value) = lower(@signerUsername)
                limit 1
            ),
            next_id as (
                select nextval('encounter_signatures_id_seq')::integer as id
            )
            insert into encounter_signatures (
                id,
                encounter_id,
                encounter,
                patient_id,
                pid,
                table_name,
                signer_user_id,
                signer_username,
                signed_at,
                is_lock,
                amendment,
                hash,
                signature_hash,
                encounter_version,
                content_revision,
                content_checksum,
                content_manifest
            )
            select
                next_id.id,
                selected_encounter.id,
                selected_encounter.encounter,
                selected_encounter.patient_id,
                selected_encounter.pid,
                'form_encounter',
                selected_user.id,
                selected_user.username,
                @signedAt,
                @isLock,
                @amendment,
                @hash,
                @signatureHash,
                selected_encounter.row_version,
                @contentRevision,
                @contentChecksum,
                @contentManifest
            from selected_encounter
            join selected_user on true
            cross join next_id
            returning id;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.Add("signerUsername", NpgsqlDbType.Text).Value = signerUsername;
        command.Parameters.Add("signedAt", NpgsqlDbType.Timestamp).Value = signedAt;
        command.Parameters.Add("isLock", NpgsqlDbType.Boolean).Value = request.IsLock;
        AddNullableText(command, "amendment", NormalizeText(request.Amendment));
        var hash = CreateSignatureHash($"{encounter}|form_encounter|{encounterVersion}|{signerUsername}|{signedAt:O}|{request.IsLock}|{request.Amendment}|{contentSnapshot.Revision}|{contentSnapshot.Checksum}");
        command.Parameters.Add("hash", NpgsqlDbType.Text).Value = hash;
        command.Parameters.Add("signatureHash", NpgsqlDbType.Text).Value = CreateSignatureHash($"{hash}|{signerUsername}");
        command.Parameters.Add("contentRevision", NpgsqlDbType.Text).Value = contentSnapshot.Revision;
        command.Parameters.Add("contentChecksum", NpgsqlDbType.Text).Value = contentSnapshot.Checksum;
        command.Parameters.Add("contentManifest", NpgsqlDbType.Jsonb).Value = contentSnapshot.Manifest;

        var id = await command.ExecuteScalarAsync(cancellationToken);
        if (id is null || id is DBNull)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        var detail = await GetByEncounterAsync(encounter, cancellationToken);
        return detail is null ? null : new EncounterSignatureMutationResponse(Convert.ToInt32(id), detail);
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

    private static async Task<int> CountMatchesAsync(
        NpgsqlConnection connection,
        string? normalizedPatientId,
        DateOnly fromDate,
        bool archived,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select count(*)
            from encounters e
            join patients p on p.legacy_pid = e.pid
            where {EncounterSearchPredicate}
              and e.archived_at is {(archived ? "not" : string.Empty)} null;
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private const string EncounterSearchPredicate = """
        (@patientId is null
         or lower(p.canonical_id) = @patientId
         or lower(p.pubpid) = @patientId
         or p.legacy_pid::text = @patientId)
        and e.encounter_date >= @fromDate
        """;

    private static void AddSearchParameters(NpgsqlCommand command, string? patientId, DateOnly fromDate)
    {
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId is null ? DBNull.Value : patientId;
        command.Parameters.Add("fromDate", NpgsqlDbType.Date).Value = fromDate;
    }

    private static EncounterListItem ReadListItem(DbDataReader reader) => new(
        Id: reader.GetInt32(reader.GetOrdinal("id")),
        Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
        PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
        LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
        Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
        PatientDisplayName: BuildDisplayName(reader),
        Date: ReadDate(reader, "encounter_date"),
        Reason: ReadNullableString(reader, "reason"),
        DiagnosisCode: ReadNullableString(reader, "diagnosis_code"),
        DiagnosisText: ReadNullableString(reader, "diagnosis_text"),
        CategoryId: ReadNullableInt(reader, "category_id"),
        ProviderName: ReadNullableString(reader, "provider_name"),
        FacilityName: ReadNullableString(reader, "facility_name"),
        Sensitivity: ReadNullableString(reader, "sensitivity"),
        ReferralSource: ReadNullableString(reader, "referral_source"),
        ExternalId: ReadNullableString(reader, "external_id"),
        PosCode: ReadNullableInt(reader, "pos_code"),
        HasVitals: reader.GetBoolean(reader.GetOrdinal("has_vitals")),
        HasSoapNote: reader.GetBoolean(reader.GetOrdinal("has_soap_note")),
        BillingLineCount: reader.GetInt32(reader.GetOrdinal("billing_line_count")));

    private static EncounterVitals? ReadVitals(DbDataReader reader)
    {
        var hasVitals = !reader.IsDBNull(reader.GetOrdinal("bps"))
            || !reader.IsDBNull(reader.GetOrdinal("bpd"))
            || !reader.IsDBNull(reader.GetOrdinal("weight"))
            || !reader.IsDBNull(reader.GetOrdinal("height"));

        if (!hasVitals)
        {
            return null;
        }

        var systolic = ReadNullableInt(reader, "bps");
        var diastolic = ReadNullableInt(reader, "bpd");
        return new EncounterVitals(
            Systolic: systolic,
            Diastolic: diastolic,
            BloodPressure: systolic is null || diastolic is null ? null : $"{systolic}/{diastolic}",
            Weight: ReadNullableDecimal(reader, "weight"),
            Height: ReadNullableDecimal(reader, "height"),
            Temperature: ReadNullableDecimal(reader, "temperature"),
            Pulse: ReadNullableInt(reader, "pulse"),
            Respiration: ReadNullableInt(reader, "respiration"),
            Bmi: ReadNullableDecimal(reader, "bmi"),
            OxygenSaturation: ReadNullableInt(reader, "oxygen_saturation"));
    }

    private static EncounterSoapNote? ReadSoapNote(DbDataReader reader)
    {
        var noteIdOrdinal = reader.GetOrdinal("soap_note_id");
        if (reader.IsDBNull(noteIdOrdinal))
        {
            return null;
        }

        var subjective = ReadNullableString(reader, "subjective");
        var objective = ReadNullableString(reader, "objective");
        var assessment = ReadNullableString(reader, "assessment");
        var plan = ReadNullableString(reader, "plan");

        return new EncounterSoapNote(
            Id: reader.GetInt32(noteIdOrdinal),
            Version: reader.GetInt32(reader.GetOrdinal("soap_note_version")),
            NoteDateTime: ReadDateTime(reader, "soap_note_datetime"),
            SavedAt: ReadDateTime(reader, "soap_note_saved_at"),
            SavedBy: ReadNullableString(reader, "soap_note_saved_by"),
            EvidenceSource: reader.GetString(reader.GetOrdinal("soap_note_evidence_source")),
            IsLocked: false,
            Subjective: subjective,
            Objective: objective,
            Assessment: assessment,
            Plan: plan,
            Versions: Array.Empty<EncounterSoapNoteVersion>());
    }

    private static async Task<IReadOnlyList<EncounterSoapNoteVersion>> GetSoapNoteVersionsAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                id,
                version,
                supersedes_note_id,
                note_datetime,
                saved_at,
                saved_by,
                evidence_source,
                subjective,
                objective,
                assessment,
                plan
            from clinical_notes
            where encounter = @encounter
            order by version desc, id desc;
            """;
        command.Parameters.AddWithValue("encounter", encounter);

        var versions = new List<EncounterSoapNoteVersion>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(new EncounterSoapNoteVersion(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Version: reader.GetInt32(reader.GetOrdinal("version")),
                SupersedesNoteId: ReadNullableInt(reader, "supersedes_note_id"),
                NoteDateTime: ReadDateTime(reader, "note_datetime"),
                SavedAt: ReadDateTime(reader, "saved_at"),
                SavedBy: ReadNullableString(reader, "saved_by"),
                EvidenceSource: reader.GetString(reader.GetOrdinal("evidence_source")),
                Subjective: ReadNullableString(reader, "subjective"),
                Objective: ReadNullableString(reader, "objective"),
                Assessment: ReadNullableString(reader, "assessment"),
                Plan: ReadNullableString(reader, "plan")));
        }

        return versions;
    }

    private static readonly IReadOnlyList<EncounterSoapNoteTemplateOption> SoapNoteTemplateOptions =
    [
        new(
            TemplateId: "soap-follow-up-stable-v1",
            Name: "Stable follow-up SOAP",
            Category: "Follow-up",
            Description: "General established-patient follow-up template for stable symptoms and continued monitoring.",
            Subjective: "Patient reports symptoms are stable and denies new acute concerns.",
            Objective: "Vitals and interval history reviewed. No acute distress noted.",
            Assessment: "Stable chronic condition with no red-flag changes today.",
            Plan: "Continue current care plan, reinforce return precautions, and schedule routine follow-up.",
            IsDefault: true),
        new(
            TemplateId: "soap-diabetes-follow-up-v1",
            Name: "Diabetes follow-up SOAP",
            Category: "Chronic disease",
            Description: "Focused diabetes follow-up template with medication adherence, foot-care, and lab-review prompts.",
            Subjective: "Patient reports home glucose readings and medication adherence were reviewed.",
            Objective: "Vitals reviewed. Foot-care status, recent labs, and medication list reconciled.",
            Assessment: "Diabetes mellitus follow-up with control and complication risk reviewed.",
            Plan: "Continue diabetes care plan, reinforce diet and foot-care education, and update labs as indicated.",
            IsDefault: false),
        new(
            TemplateId: "soap-acute-respiratory-v1",
            Name: "Acute respiratory SOAP",
            Category: "Acute visit",
            Description: "Acute cough/upper-respiratory template with symptom duration, exam, assessment, and precautions.",
            Subjective: "Patient reports acute respiratory symptoms; duration, fever, exposure, and medication history reviewed.",
            Objective: "Respiratory status, oxygen saturation, lung exam, and hydration status reviewed.",
            Assessment: "Acute respiratory symptoms assessed with severity and complication risk reviewed.",
            Plan: "Provide supportive-care instructions, medication plan as appropriate, and clear return precautions.",
            IsDefault: false),
        new(
            TemplateId: "soap-preventive-annual-v1",
            Name: "Preventive annual SOAP",
            Category: "Preventive care",
            Description: "Preventive visit template for screening, immunization, risk review, and health-maintenance planning.",
            Subjective: "Patient presents for preventive care; interval history, screening needs, and health goals reviewed.",
            Objective: "Vitals, preventive screening status, immunization history, and risk factors reviewed.",
            Assessment: "Preventive health maintenance visit with screening and risk-reduction needs identified.",
            Plan: "Update preventive screenings, immunizations, counseling, and follow-up plan as indicated.",
            IsDefault: false)
    ];

    private static async Task<IReadOnlyList<BillingLineItem>> GetBillingLinesForEncounterAsync(
        NpgsqlConnection connection,
        int pid,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, billing_date, code_type, code, modifier, code_text, fee, justify, units, billed, activity
            from billing
            where pid = @pid and encounter = @encounter and activity = 1
            order by id;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.AddWithValue("encounter", encounter);

        var lines = new List<BillingLineItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new BillingLineItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                BillingDate: ReadDate(reader, "billing_date"),
                CodeType: ReadNullableString(reader, "code_type"),
                Code: ReadNullableString(reader, "code"),
                Modifier: ReadNullableString(reader, "modifier"),
                CodeText: ReadNullableString(reader, "code_text"),
                Fee: ReadNullableDecimal(reader, "fee"),
                Justify: ReadNullableString(reader, "justify"),
                Units: ReadInt(reader, "units"),
                Billed: ReadInt(reader, "billed"),
                Activity: ReadInt(reader, "activity")));
        }

        return lines;
    }

    private static async Task<IReadOnlyList<BillingClaimItem>> GetClaimsForEncounterAsync(
        NpgsqlConnection connection,
        int pid,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, version, payer_id, payer_name, payer_type, status, bill_process,
                   bill_time, process_time, process_file, target, submitted_claim
            from claims
            where pid = @pid and encounter = @encounter
            order by version;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.AddWithValue("encounter", encounter);

        var claims = new List<BillingClaimItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = ReadInt(reader, "status");
            var billProcess = ReadInt(reader, "bill_process");
            claims.Add(new BillingClaimItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                Version: reader.GetInt32(reader.GetOrdinal("version")),
                PayerId: reader.GetInt32(reader.GetOrdinal("payer_id")),
                PayerName: ReadNullableString(reader, "payer_name"),
                PayerType: reader.GetInt32(reader.GetOrdinal("payer_type")),
                Status: status,
                StatusLabel: ClaimStatusLabel(status, billProcess),
                BillProcess: billProcess,
                BillTime: ReadNullableDateTime(reader, "bill_time"),
                ProcessTime: ReadNullableDateTime(reader, "process_time"),
                ProcessFile: ReadNullableString(reader, "process_file"),
                Target: ReadNullableString(reader, "target"),
                SubmittedClaim: ReadNullableString(reader, "submitted_claim")));
        }

        return claims;
    }

    private static async Task<IReadOnlyList<ProcedureOrderItem>> GetProcedureOrdersForEncounterAsync(
        NpgsqlConnection connection,
        int pid,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                lo.id,
                lo.encounter,
                nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                lo.order_date,
                lo.order_priority,
                lo.code,
                lo.name,
                lo.procedure_type,
                lo.diagnosis,
                lo.instructions,
                lo.order_status
            from lab_orders lo
            left join staff s on s.id = lo.provider_id
            where lo.pid = @pid and lo.encounter = @encounter
            order by lo.order_date desc, lo.id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.AddWithValue("encounter", encounter);

        var orderRows = new List<ProcedureOrderRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                orderRows.Add(new ProcedureOrderRow(
                    Id: id,
                    Order: new ProcedureOrderItem(
                        Id: id,
                        Encounter: ReadNullableInt(reader, "encounter"),
                        ProviderName: ReadNullableString(reader, "provider_name"),
                        OrderDate: ReadDate(reader, "order_date"),
                        OrderPriority: ReadNullableString(reader, "order_priority"),
                        Code: ReadNullableString(reader, "code"),
                        Name: ReadNullableString(reader, "name"),
                        ProcedureType: ReadNullableString(reader, "procedure_type"),
                        Diagnosis: ReadNullableString(reader, "diagnosis"),
                        Instructions: ReadNullableString(reader, "instructions"),
                        OrderStatus: ReadNullableString(reader, "order_status"),
                        Specimens: [],
                        Reports: [])));
            }
        }

        if (orderRows.Count == 0)
        {
            return [];
        }

        var orderIds = orderRows.Select(row => row.Id).ToArray();
        var specimenRows = await GetProcedureSpecimensForOrdersAsync(connection, orderIds, cancellationToken);
        var reportRows = await GetProcedureReportsForOrdersAsync(connection, orderIds, cancellationToken);
        var reportIds = reportRows.Select(row => row.Id).ToArray();
        var resultRows = await GetProcedureResultsForReportsAsync(connection, reportIds, cancellationToken);

        var specimensByOrder = specimenRows
            .GroupBy(row => row.OrderId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Specimen).ToList());
        var resultsByReport = resultRows
            .GroupBy(row => row.ReportId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Result).ToList());
        var reportsByOrder = reportRows
            .GroupBy(row => row.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.Report with
                {
                    Results = resultsByReport.GetValueOrDefault(row.Id, [])
                }).ToList());

        return orderRows.Select(row => row.Order with
        {
            Specimens = specimensByOrder.GetValueOrDefault(row.Id, []),
            Reports = reportsByOrder.GetValueOrDefault(row.Id, [])
        }).ToList();
    }

    private static async Task<IReadOnlyList<EncounterSignatureItem>> GetSignaturesForEncounterAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, table_name, signer_user_id, signer_username, signed_at, is_lock, amendment, hash, signature_hash, encounter_version,
                   content_revision, content_checksum
            from encounter_signatures
            where encounter = @encounter
            order by signed_at desc, id desc;
            """;
        command.Parameters.AddWithValue("encounter", encounter);

        var signatures = new List<EncounterSignatureItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            signatures.Add(new EncounterSignatureItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                TableName: reader.GetString(reader.GetOrdinal("table_name")),
                SignerUserId: ReadNullableInt(reader, "signer_user_id"),
                SignerUsername: reader.GetString(reader.GetOrdinal("signer_username")),
                SignedAt: ReadDateTime(reader, "signed_at"),
                IsLock: reader.GetBoolean(reader.GetOrdinal("is_lock")),
                Amendment: ReadNullableString(reader, "amendment"),
                Hash: reader.GetString(reader.GetOrdinal("hash")),
                SignatureHash: reader.GetString(reader.GetOrdinal("signature_hash")),
                EncounterVersion: reader.IsDBNull(reader.GetOrdinal("encounter_version"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("encounter_version")),
                ContentRevision: ReadNullableString(reader, "content_revision"),
                ContentChecksum: ReadNullableString(reader, "content_checksum")));
        }

        return signatures;
    }

    private static IReadOnlyList<EncounterAmendmentHistoryItem> BuildAmendmentHistory(
        IReadOnlyList<EncounterSignatureItem> signatures)
    {
        return signatures
            .Where(signature => !string.IsNullOrWhiteSpace(signature.Amendment))
            .Select(signature => new EncounterAmendmentHistoryItem(
                SignatureId: signature.Id,
                SignerUsername: signature.SignerUsername,
                SignedAt: signature.SignedAt,
                IsLock: signature.IsLock,
                Amendment: signature.Amendment!,
                Hash: signature.Hash,
                SignatureHash: signature.SignatureHash))
            .ToList();
    }

    private static async Task<SignatureContentSnapshot> CaptureSignatureContentSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select jsonb_build_object(
              'revision', @revision,
              'encounter', jsonb_build_object(
                'id', encounter_row.id,
                'encounter', encounter_row.encounter,
                'patientId', encounter_row.patient_id,
                'legacyPid', encounter_row.pid,
                'rowVersion', encounter_row.row_version,
                'date', encounter_row.encounter_date,
                'dateTime', encounter_row.encounter_datetime,
                'providerId', encounter_row.provider_id,
                'facilityId', encounter_row.facility_id,
                'billingFacilityId', encounter_row.billing_facility_id,
                'reason', encounter_row.reason,
                'diagnosisCode', encounter_row.diagnosis_code,
                'diagnosisText', encounter_row.diagnosis_text,
                'categoryId', encounter_row.category_id,
                'sensitivity', encounter_row.sensitivity,
                'referralSource', encounter_row.referral_source,
                'externalId', encounter_row.external_id,
                'posCode', encounter_row.pos_code,
                'billingNote', encounter_row.billing_note,
                'sourceAppointmentId', encounter_row.source_appointment_id),
              'vitals', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', vital.id,
                  'dateTime', vital.vital_datetime,
                  'systolic', vital.bps,
                  'diastolic', vital.bpd,
                  'weight', vital.weight,
                  'height', vital.height,
                  'temperature', vital.temperature,
                  'pulse', vital.pulse,
                  'respiration', vital.respiration,
                  'bmi', vital.bmi,
                  'oxygenSaturation', vital.oxygen_saturation,
                  'note', vital.note)
                  order by vital.id)
                from vitals vital
                where vital.pid=encounter_row.pid and vital.encounter=encounter_row.encounter), '[]'::jsonb),
              'soapNotes', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', note.id,
                  'version', note.version,
                  'supersedesNoteId', note.supersedes_note_id,
                  'dateTime', note.note_datetime,
                  'savedAt', note.saved_at,
                  'savedBy', note.saved_by,
                  'evidenceSource', note.evidence_source,
                  'subjective', note.subjective,
                  'objective', note.objective,
                  'assessment', note.assessment,
                  'plan', note.plan)
                  order by note.version, note.id)
                from clinical_notes note
                where note.pid=encounter_row.pid and note.encounter=encounter_row.encounter), '[]'::jsonb),
              'layoutForms', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'recordId', form.record_id,
                  'layoutKey', form.layout_key,
                  'revision', form.revision,
                  'savedAt', form.saved_at,
                  'savedBy', form.saved_by,
                  'values', coalesce((
                    select jsonb_object_agg(value.field_key, value.field_value order by value.field_key)
                    from encounter_layout_form_values value
                    where value.record_id=form.record_id), '{}'::jsonb))
                  order by form.layout_key, form.revision, form.record_id)
                from encounter_layout_form_records form
                where form.encounter=encounter_row.encounter), '[]'::jsonb),
              'clinicalAlertAcknowledgements', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'ruleKey', acknowledgement.rule_key,
                  'acknowledgedAt', acknowledgement.acknowledged_at,
                  'acknowledgedBy', acknowledgement.acknowledged_by,
                  'reopenedAt', acknowledgement.reopened_at,
                  'reopenedBy', acknowledgement.reopened_by)
                  order by acknowledgement.rule_key)
                from encounter_clinical_alert_acknowledgments acknowledgement
                where acknowledgement.encounter=encounter_row.encounter), '[]'::jsonb),
              'laboratoryOrders', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', laboratory_order.id,
                  'orderDate', laboratory_order.order_date,
                  'providerId', laboratory_order.provider_id,
                  'labId', laboratory_order.lab_id,
                  'priority', laboratory_order.order_priority,
                  'code', laboratory_order.code,
                  'name', laboratory_order.name,
                  'procedureType', laboratory_order.procedure_type,
                  'diagnosis', laboratory_order.diagnosis,
                  'instructions', laboratory_order.instructions,
                  'status', laboratory_order.order_status,
                  'dateTransmitted', laboratory_order.date_transmitted,
                  'reports', coalesce((
                    select jsonb_agg(jsonb_build_object(
                      'id', report.id,
                      'specimenId', report.specimen_id,
                      'dateCollected', report.date_collected,
                      'reportDate', report.report_date,
                      'specimenNumber', report.specimen_number,
                      'status', report.status,
                      'reviewStatus', report.review_status,
                      'reviewedBy', report.reviewed_by,
                      'reviewedAt', report.reviewed_at,
                      'reviewVersion', report.review_version,
                      'notes', report.notes,
                      'results', coalesce((
                        select jsonb_agg(jsonb_build_object(
                          'id', result.id,
                          'contentVersion', coalesce((
                            select max(version.version_no)
                            from procedure_result_versions version
                            where version.result_id=result.id), 0) + 1,
                          'code', result.code,
                          'text', result.text,
                          'units', result.units,
                          'result', result.result,
                          'range', result.range,
                          'abnormal', result.abnormal,
                          'resultDate', result.result_date,
                          'status', result.result_status)
                          order by result.id)
                        from lab_results result
                        where result.report_id=report.id), '[]'::jsonb))
                      order by report.report_date, report.id)
                    from lab_reports report
                    where report.order_id=laboratory_order.id), '[]'::jsonb))
                  order by laboratory_order.order_date, laboratory_order.id)
                from lab_orders laboratory_order
                where laboratory_order.pid=encounter_row.pid
                  and laboratory_order.encounter=encounter_row.encounter), '[]'::jsonb),
              'encounterDocuments', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', document.id,
                  'documentKey', document.document_key,
                  'currentVersion', coalesce((
                    select max(version.version_no)
                    from patient_document_versions version
                    where version.document_id=document.id), 0) + 1,
                  'categoryId', document.category_id,
                  'categoryName', document.category_name,
                  'name', document.name,
                  'date', document.doc_date,
                  'uploadedAt', document.uploaded_at,
                  'mimetype', document.mimetype,
                  'fileName', document.file_name,
                  'sizeBytes', document.size_bytes,
                  'pages', document.pages,
                  'storageMethod', document.storage_method,
                  'url', document.url,
                  'contentHash', document.hash,
                  'documentationOf', document.documentation_of,
                  'notes', document.notes,
                  'reviewStatus', document.review_status,
                  'reviewedBy', document.reviewed_by,
                  'reviewedAt', document.reviewed_at,
                  'deleted', document.deleted)
                  order by document.id)
                from patient_documents document
                where document.pid=encounter_row.pid
                  and document.encounter=encounter_row.encounter), '[]'::jsonb),
              'billingLines', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', line.id,
                  'date', line.billing_date,
                  'providerId', line.provider_id,
                  'codeType', line.code_type,
                  'code', line.code,
                  'modifier', line.modifier,
                  'codeText', line.code_text,
                  'fee', line.fee,
                  'justify', line.justify,
                  'units', line.units,
                  'billed', line.billed,
                  'activity', line.activity)
                  order by line.id)
                from billing line
                where line.pid=encounter_row.pid and line.encounter=encounter_row.encounter), '[]'::jsonb),
              'claims', coalesce((
                select jsonb_agg(jsonb_build_object(
                  'id', claim.id,
                  'version', claim.version,
                  'payerId', claim.payer_id,
                  'payerName', claim.payer_name,
                  'payerType', claim.payer_type,
                  'status', claim.status,
                  'billProcess', claim.bill_process,
                  'billTime', claim.bill_time,
                  'processTime', claim.process_time,
                  'processFile', claim.process_file,
                  'target', claim.target,
                  'submittedClaim', claim.submitted_claim)
                  order by claim.version, claim.id)
                from claims claim
                where claim.pid=encounter_row.pid and claim.encounter=encounter_row.encounter), '[]'::jsonb)
            )::text
            from encounters encounter_row
            where encounter_row.encounter=@encounter;
            """;
        command.Parameters.AddWithValue("revision", SignatureContentRevision);
        command.Parameters.AddWithValue("encounter", encounter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException("The encounter content could not be captured for signature.");
        }

        var manifest = result switch
        {
            string text => text,
            JsonDocument document => document.RootElement.GetRawText(),
            _ => result.ToString() ?? throw new InvalidOperationException(
                "The encounter signature content snapshot was not JSON.")
        };
        return new SignatureContentSnapshot(
            SignatureContentRevision,
            manifest,
            CreateSignatureHash(manifest));
    }

    private static async Task<IReadOnlyList<ProcedureReportRow>> GetProcedureReportsForOrdersAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_id, specimen_id, date_collected, report_date, specimen_number, status, review_status, reviewed_by, reviewed_at,
                   review_version,
                   (select count(*) from lab_report_review_events event where event.report_id = lab_reports.id) as review_history_count,
                   notes
            from lab_reports
            where order_id = any(@orderIds)
            order by report_date desc, id desc;
            """;
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        var rows = new List<ProcedureReportRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            rows.Add(new ProcedureReportRow(
                Id: id,
                OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                Report: new ProcedureReportItem(
                    Id: id,
                    DateCollected: ReadDateTime(reader, "date_collected"),
                    ReportDate: ReadDateTime(reader, "report_date"),
                    SpecimenId: ReadNullableInt(reader, "specimen_id"),
                    SpecimenNumber: ReadNullableString(reader, "specimen_number"),
                    Status: ReadNullableString(reader, "status"),
                    ReviewStatus: ReadNullableString(reader, "review_status"),
                    ReviewedBy: ReadNullableString(reader, "reviewed_by"),
                    ReviewedAt: ReadNullableDateTime(reader, "reviewed_at"),
                    ReviewVersion: reader.GetInt32(reader.GetOrdinal("review_version")),
                    ReviewHistoryCount: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("review_history_count"))),
                    Notes: ReadNullableString(reader, "notes"),
                    Results: [])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProcedureSpecimenRow>> GetProcedureSpecimensForOrdersAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, order_id, specimen_identifier, accession_identifier, specimen_type_code, specimen_type,
                   collection_method_code, collection_method, specimen_location_code, specimen_location,
                   collected_date, volume_value, volume_unit, condition_code, specimen_condition, comments,
                   specimen_status, specimen_version,
                   (select count(*) from procedure_specimen_events event where event.specimen_id = lab_specimens.id) as lifecycle_history_count
            from lab_specimens
            where order_id = any(@orderIds)
            order by collected_date desc, id desc;
            """;
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        var rows = new List<ProcedureSpecimenRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProcedureSpecimenRow(
                OrderId: reader.GetInt32(reader.GetOrdinal("order_id")),
                Specimen: new ProcedureSpecimenItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    SpecimenIdentifier: ReadNullableString(reader, "specimen_identifier"),
                    AccessionIdentifier: ReadNullableString(reader, "accession_identifier"),
                    SpecimenTypeCode: ReadNullableString(reader, "specimen_type_code"),
                    SpecimenType: ReadNullableString(reader, "specimen_type"),
                    CollectionMethodCode: ReadNullableString(reader, "collection_method_code"),
                    CollectionMethod: ReadNullableString(reader, "collection_method"),
                    SpecimenLocationCode: ReadNullableString(reader, "specimen_location_code"),
                    SpecimenLocation: ReadNullableString(reader, "specimen_location"),
                    CollectedDate: ReadDateTime(reader, "collected_date"),
                    VolumeValue: ReadNullableDecimal(reader, "volume_value"),
                    VolumeUnit: ReadNullableString(reader, "volume_unit"),
                    ConditionCode: ReadNullableString(reader, "condition_code"),
                    SpecimenCondition: ReadNullableString(reader, "specimen_condition"),
                    Comments: ReadNullableString(reader, "comments"),
                    LifecycleStatus: reader.GetString(reader.GetOrdinal("specimen_status")),
                    LifecycleVersion: reader.GetInt32(reader.GetOrdinal("specimen_version")),
                    LifecycleHistoryCount: Convert.ToInt32(reader.GetValue(reader.GetOrdinal("lifecycle_history_count"))))));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProcedureResultRow>> GetProcedureResultsForReportsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<int> reportIds,
        CancellationToken cancellationToken)
    {
        if (reportIds.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, report_id, code, text, units, result, range, abnormal, result_date, result_status
            from lab_results
            where report_id = any(@reportIds)
            order by id;
            """;
        command.Parameters.AddWithValue("reportIds", reportIds.ToArray());

        var rows = new List<ProcedureResultRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProcedureResultRow(
                ReportId: reader.GetInt32(reader.GetOrdinal("report_id")),
                Result: new ProcedureResultItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Code: ReadNullableString(reader, "code"),
                    Text: ReadNullableString(reader, "text"),
                    Units: ReadNullableString(reader, "units"),
                    Result: ReadNullableString(reader, "result"),
                    Range: ReadNullableString(reader, "range"),
                    Abnormal: ReadNullableString(reader, "abnormal"),
                    ResultDate: ReadDateTime(reader, "result_date"),
                    ResultStatus: ReadNullableString(reader, "result_status"),
                    CurrentVersion: 1,
                    VersionLabel: "Version 1",
                    VersionHistoryCount: 1,
                    HasPriorVersions: false,
                    VersionHistory: [])));
        }

        return rows;
    }

    private static IReadOnlyList<EncounterDiagnosisCode> BuildDiagnosisCodes(
        EncounterDetail detail,
        IReadOnlyList<BillingLineItem> billingLines,
        IReadOnlyList<ProcedureOrderItem> procedureOrders)
    {
        var codes = new Dictionary<string, DiagnosisAccumulator>(StringComparer.OrdinalIgnoreCase);
        var orderedCodes = new List<string>();

        void AddDiagnosis(
            string? rawCode,
            string? description,
            string source,
            int billingLineCount = 0,
            int procedureOrderCount = 0,
            IEnumerable<string>? supportingBillingCodes = null)
        {
            var code = NormalizeDiagnosisCode(rawCode);
            if (code is null)
            {
                return;
            }

            if (!codes.TryGetValue(code, out var accumulator))
            {
                accumulator = new DiagnosisAccumulator(code);
                codes.Add(code, accumulator);
                orderedCodes.Add(code);
            }

            accumulator.Description ??= NormalizeText(description);
            accumulator.AddSource(source);
            accumulator.BillingLineCount += billingLineCount;
            accumulator.ProcedureOrderCount += procedureOrderCount;

            if (supportingBillingCodes is null)
            {
                return;
            }

            foreach (var supportingBillingCode in supportingBillingCodes)
            {
                accumulator.AddSupportingBillingCode(supportingBillingCode);
            }
        }

        AddDiagnosis(detail.DiagnosisCode, detail.DiagnosisText, "Encounter diagnosis");

        foreach (var line in billingLines.Where(line => line.Activity == 1))
        {
            var supportingBillingCode = FormatBillingSupport(line);
            var supportingCodes = supportingBillingCode is null
                ? Array.Empty<string>()
                : new[] { supportingBillingCode };

            if (string.Equals(line.CodeType, "ICD10", StringComparison.OrdinalIgnoreCase)
                || string.Equals(line.CodeType, "ICD9", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnosis(
                    line.Code,
                    line.CodeText,
                    "Fee sheet diagnosis line",
                    billingLineCount: 1,
                    supportingBillingCodes: supportingCodes);
            }

            foreach (var diagnosisCode in SplitDiagnosisCodes(line.Justify))
            {
                AddDiagnosis(
                    diagnosisCode,
                    CodesMatch(diagnosisCode, detail.DiagnosisCode) ? detail.DiagnosisText : null,
                    "Fee sheet justification",
                    billingLineCount: 1,
                    supportingBillingCodes: supportingCodes);
            }
        }

        foreach (var procedureOrder in procedureOrders)
        {
            AddDiagnosis(
                procedureOrder.Diagnosis,
                CodesMatch(procedureOrder.Diagnosis, detail.DiagnosisCode) ? detail.DiagnosisText : null,
                "Procedure order diagnosis",
                procedureOrderCount: 1);
        }

        return orderedCodes.Select(code =>
        {
            var accumulator = codes[code];
            return new EncounterDiagnosisCode(
                Code: accumulator.Code,
                Description: accumulator.Description,
                Sources: accumulator.Sources,
                BillingLineCount: accumulator.BillingLineCount,
                ProcedureOrderCount: accumulator.ProcedureOrderCount,
                SupportingBillingCodes: accumulator.SupportingBillingCodes);
        }).ToList();
    }

    private static IEnumerable<string> SplitDiagnosisCodes(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            yield break;
        }

        foreach (var candidate in normalized.Split(
                     [',', ';', '|', ' ', '\t', '\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var code = NormalizeDiagnosisCode(candidate);
            if (code is not null)
            {
                yield return code;
            }
        }
    }

    private static bool CodesMatch(string? left, string? right) =>
        NormalizeDiagnosisCode(left) is { } normalizedLeft
        && NormalizeDiagnosisCode(right) is { } normalizedRight
        && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeDiagnosisCode(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        foreach (var prefix in new[] { "ICD10:", "ICD9:" })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        return normalized.Length == 0 ? null : normalized;
    }

    private static string? FormatBillingSupport(BillingLineItem line)
    {
        var codeType = NormalizeText(line.CodeType);
        var code = NormalizeText(line.Code);
        if (codeType is null || code is null)
        {
            return null;
        }

        var modifier = NormalizeText(line.Modifier);
        return modifier is null ? $"{codeType} {code}" : $"{codeType} {code}-{modifier}";
    }

    private static EncounterDocumentAttachment MapEncounterDocument(PatientDocumentItem document)
    {
        return new EncounterDocumentAttachment(
            Id: document.Id,
            DocumentKey: document.DocumentKey,
            CategoryId: document.CategoryId,
            CategoryName: document.CategoryName,
            Name: document.Name,
            DocDate: document.DocDate,
            UploadedAt: document.UploadedAt,
            RevisionAt: document.RevisionAt,
            CurrentVersion: document.CurrentVersion,
            VersionLabel: document.VersionLabel,
            VersionStatus: document.VersionStatus,
            VersionHistoryCount: document.VersionHistoryCount,
            HasPriorVersions: document.HasPriorVersions,
            RevisionHash: document.RevisionHash,
            Mimetype: document.Mimetype,
            SizeBytes: document.SizeBytes,
            Pages: document.Pages,
            StorageMethod: document.StorageMethod,
            FileName: document.FileName,
            Url: document.Url,
            Hash: document.Hash,
            Notes: document.Notes,
            Deleted: document.Deleted,
            ReviewStatus: document.ReviewStatus,
            ReviewedBy: document.ReviewedBy,
            ReviewedAt: document.ReviewedAt,
            ContentPreview: document.ContentPreview,
            PreviewKind: document.PreviewKind,
            PreviewStatus: document.PreviewStatus,
            ThumbnailLabel: document.ThumbnailLabel,
            ThumbnailText: document.ThumbnailText,
            CanPreviewInline: document.CanPreviewInline,
            CanDownload: document.CanDownload,
            IsScannedAttachment: document.IsScannedAttachment,
            ScanStatus: document.ScanStatus,
            CaptureSource: document.CaptureSource,
            ScanPageCount: document.ScanPageCount,
            OcrStatus: document.OcrStatus,
            LifecycleEvents: document.LifecycleEvents
                .Select(lifecycle => new EncounterDocumentLifecycleEvent(
                    lifecycle.Code,
                    lifecycle.Label,
                    lifecycle.OccurredAt,
                    lifecycle.Actor,
                    lifecycle.Detail))
                .ToArray());
    }

    private static async Task<IReadOnlyList<EncounterDocumentAttachment>> GetDocumentsForEncounterAsync(
        NpgsqlConnection connection,
        int pid,
        int encounter,
        bool includeArchivedDocuments,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, document_key, category_id, category_name, name, doc_date, uploaded_at,
              mimetype, size_bytes, pages, storage_method, file_name, url, hash, notes, deleted,
              coalesce(review_status, 'pending') as review_status, reviewed_by, reviewed_at,
              case
                when content_bytes is not null then left(coalesce(content, ''), 220)
                else left(regexp_replace(coalesce(content, ''), E'[\\r\\n]+', ' ', 'g'), 220)
              end as content_preview
            from patient_documents
            where pid = @pid and encounter = @encounter and (@includeArchivedDocuments or deleted = 0)
            order by doc_date desc, id desc;
            """;
        command.Parameters.AddWithValue("pid", pid);
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("includeArchivedDocuments", includeArchivedDocuments);

        var documents = new List<EncounterDocumentAttachment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var mimetype = ReadNullableString(reader, "mimetype");
            var storageMethod = ReadNullableString(reader, "storage_method");
            var fileName = ReadNullableString(reader, "file_name");
            var url = ReadNullableString(reader, "url");
            var hash = ReadNullableString(reader, "hash");
            var pages = ReadNullableInt(reader, "pages");
            var name = reader.GetString(reader.GetOrdinal("name"));
            var notes = ReadNullableString(reader, "notes");
            var contentPreview = ReadNullableString(reader, "content_preview");
            var preview = BuildDocumentPreviewInfo(mimetype, storageMethod, fileName, url, pages, contentPreview);
            var scanReadiness = BuildScanReadiness(name, fileName, mimetype, pages, storageMethod, notes, contentPreview);
            var uploadedAt = ReadDateTime(reader, "uploaded_at");
            var revisionAt = uploadedAt;
            var deleted = reader.GetInt32(reader.GetOrdinal("deleted"));
            var reviewStatus = reader.GetString(reader.GetOrdinal("review_status"));
            var reviewedBy = ReadNullableString(reader, "reviewed_by");
            var reviewedAt = ReadNullableDateTime(reader, "reviewed_at");

            documents.Add(new EncounterDocumentAttachment(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DocumentKey: reader.GetString(reader.GetOrdinal("document_key")),
                CategoryId: reader.GetInt32(reader.GetOrdinal("category_id")),
                CategoryName: reader.GetString(reader.GetOrdinal("category_name")),
                Name: name,
                DocDate: ReadDate(reader, "doc_date"),
                UploadedAt: uploadedAt,
                RevisionAt: revisionAt,
                CurrentVersion: 1,
                VersionLabel: "Version 1",
                VersionStatus: "Current version",
                VersionHistoryCount: 1,
                HasPriorVersions: false,
                RevisionHash: hash,
                Mimetype: mimetype,
                SizeBytes: ReadNullableInt(reader, "size_bytes"),
                Pages: pages,
                StorageMethod: storageMethod,
                FileName: fileName,
                Url: url,
                Hash: hash,
                Notes: notes,
                Deleted: deleted,
                ReviewStatus: reviewStatus,
                ReviewedBy: reviewedBy,
                ReviewedAt: reviewedAt,
                ContentPreview: contentPreview,
                PreviewKind: preview.PreviewKind,
                PreviewStatus: preview.PreviewStatus,
                ThumbnailLabel: preview.ThumbnailLabel,
                ThumbnailText: preview.ThumbnailText,
                CanPreviewInline: preview.CanPreviewInline,
                CanDownload: preview.CanDownload,
                IsScannedAttachment: scanReadiness.IsScannedAttachment,
                ScanStatus: scanReadiness.ScanStatus,
                CaptureSource: scanReadiness.CaptureSource,
                ScanPageCount: scanReadiness.ScanPageCount,
                OcrStatus: scanReadiness.OcrStatus,
                LifecycleEvents: BuildDocumentLifecycleEvents(
                    uploadedAt,
                    revisionAt,
                    reviewStatus,
                    reviewedBy,
                    reviewedAt,
                    deleted,
                    hash)));
        }

        return documents;
    }

    private static IReadOnlyList<EncounterDocumentLifecycleEvent> BuildDocumentLifecycleEvents(
        string uploadedAt,
        string revisionAt,
        string reviewStatus,
        string? reviewedBy,
        string? reviewedAt,
        int deleted,
        string? revisionHash)
    {
        var normalizedReviewStatus = NormalizePreviewText(reviewStatus).ToLowerInvariant();
        var reviewEvent = normalizedReviewStatus switch
        {
            "approved" => new EncounterDocumentLifecycleEvent(
                Code: "review-approved",
                Label: "Review approved",
                OccurredAt: reviewedAt,
                Actor: NormalizeText(reviewedBy),
                Detail: "Document approved"),
            "denied" => new EncounterDocumentLifecycleEvent(
                Code: "review-denied",
                Label: "Review denied",
                OccurredAt: reviewedAt,
                Actor: NormalizeText(reviewedBy),
                Detail: "Document denied"),
            _ => new EncounterDocumentLifecycleEvent(
                Code: "review-pending",
                Label: "Review pending",
                OccurredAt: null,
                Actor: null,
                Detail: "Awaiting review")
        };

        var archiveEvent = deleted == 0
            ? new EncounterDocumentLifecycleEvent(
                Code: "active",
                Label: "Active",
                OccurredAt: null,
                Actor: null,
                Detail: "Visible in active encounter documents")
            : new EncounterDocumentLifecycleEvent(
                Code: "archived",
                Label: "Archived",
                OccurredAt: null,
                Actor: null,
                Detail: "Hidden from active encounter documents");

        return
        [
            new EncounterDocumentLifecycleEvent(
                Code: "filed",
                Label: "Filed",
                OccurredAt: uploadedAt,
                Actor: "admin",
                Detail: "Filed to encounter documents"),
            new EncounterDocumentLifecycleEvent(
                Code: "current-version",
                Label: "Current version",
                OccurredAt: revisionAt,
                Actor: null,
                Detail: NormalizeText(revisionHash) is { } hash
                    ? $"Version 1 / {hash}"
                    : "Version 1"),
            reviewEvent,
            archiveEvent
        ];
    }

    private static EncounterDocumentPreviewInfo BuildDocumentPreviewInfo(
        string? mimetype,
        string? storageMethod,
        string? fileName,
        string? url,
        int? pages,
        string? contentPreview)
    {
        var normalizedMimetype = NormalizePreviewText(mimetype).ToLowerInvariant();
        var normalizedStorageMethod = NormalizePreviewText(storageMethod).ToLowerInvariant();
        var normalizedFileName = NormalizePreviewText(fileName);
        var normalizedUrl = NormalizePreviewText(url);
        var previewText = TrimPreviewText(contentPreview);

        if (normalizedStorageMethod == "web_url" && normalizedUrl.Length > 0)
        {
            return new EncounterDocumentPreviewInfo(
                PreviewKind: "external-link",
                PreviewStatus: "External link",
                ThumbnailLabel: "LINK",
                ThumbnailText: TrimPreviewText(normalizedUrl),
                CanPreviewInline: false,
                CanDownload: true);
        }

        if (normalizedMimetype.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return new EncounterDocumentPreviewInfo(
                PreviewKind: "text",
                PreviewStatus: "Inline text preview",
                ThumbnailLabel: "TXT",
                ThumbnailText: previewText.Length == 0 ? "Text document" : previewText,
                CanPreviewInline: true,
                CanDownload: true);
        }

        if (normalizedMimetype == "application/pdf")
        {
            return new EncounterDocumentPreviewInfo(
                PreviewKind: "pdf",
                PreviewStatus: "Inline PDF preview",
                ThumbnailLabel: "PDF",
                ThumbnailText: pages is > 0 ? $"{pages} page PDF document" : "PDF document",
                CanPreviewInline: true,
                CanDownload: true);
        }

        if (normalizedMimetype.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new EncounterDocumentPreviewInfo(
                PreviewKind: "image",
                PreviewStatus: "Inline image preview",
                ThumbnailLabel: "IMG",
                ThumbnailText: normalizedFileName.Length == 0 ? "Image document" : TrimPreviewText(normalizedFileName),
                CanPreviewInline: true,
                CanDownload: true);
        }

        return new EncounterDocumentPreviewInfo(
            PreviewKind: "binary",
            PreviewStatus: "Download preview",
            ThumbnailLabel: BuildDocumentThumbnailLabel(normalizedFileName, normalizedMimetype),
            ThumbnailText: normalizedFileName.Length == 0 ? "Stored document" : TrimPreviewText(normalizedFileName),
            CanPreviewInline: false,
            CanDownload: true);
    }

    private static string BuildDocumentThumbnailLabel(string fileName, string mimetype)
    {
        var extension = fileName.Contains('.', StringComparison.Ordinal)
            ? fileName.Split('.').LastOrDefault() ?? string.Empty
            : string.Empty;
        if (extension.Length is > 0 and <= 4)
        {
            return extension.ToUpperInvariant();
        }

        return mimetype.Contains("json", StringComparison.OrdinalIgnoreCase) ? "JSON" : "FILE";
    }

    private static EncounterDocumentScanReadiness BuildScanReadiness(
        string? name,
        string? fileName,
        string? mimetype,
        int? pages,
        string? storageMethod,
        string? notes,
        string? previewText)
    {
        var evidence = string.Join(
            " ",
            new[]
            {
                NormalizeText(name),
                NormalizeText(fileName),
                NormalizeText(mimetype),
                NormalizeText(storageMethod),
                NormalizeText(notes),
                NormalizeText(previewText)
            }.Where(value => value is not null));
        var normalizedEvidence = evidence.ToLowerInvariant();
        var isScanned = normalizedEvidence.Contains("scan", StringComparison.Ordinal)
            || normalizedEvidence.Contains("scanner", StringComparison.Ordinal);
        var scanPageCount = Math.Max(pages ?? 0, isScanned ? 1 : 0);

        return new EncounterDocumentScanReadiness(
            IsScannedAttachment: isScanned,
            ScanStatus: isScanned ? "Scanned attachment" : "Not scanned",
            CaptureSource: isScanned ? ExtractCaptureSource(notes) ?? "Document scanner" : "Not captured by scanner",
            ScanPageCount: scanPageCount,
            OcrStatus: isScanned ? ExtractOcrStatus(notes, previewText) : "Not applicable");
    }

    private static string? ExtractCaptureSource(string? notes)
    {
        var normalized = NormalizeText(notes);
        if (normalized is null)
        {
            return null;
        }

        const string marker = "scan source:";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var sourceStart = markerIndex + marker.Length;
        var sourceEnd = normalized.IndexOf(';', sourceStart);
        var source = sourceEnd < 0
            ? normalized[sourceStart..]
            : normalized[sourceStart..sourceEnd];
        return NormalizeText(source);
    }

    private static string ExtractOcrStatus(string? notes, string? previewText)
    {
        var evidence = string.Join(" ", NormalizeText(notes), NormalizeText(previewText)).ToLowerInvariant();
        if (evidence.Contains("ocr complete", StringComparison.Ordinal))
        {
            return "OCR complete";
        }

        if (evidence.Contains("ocr failed", StringComparison.Ordinal))
        {
            return "OCR failed";
        }

        return evidence.Contains("ocr pending", StringComparison.Ordinal)
            ? "OCR pending"
            : "OCR not started";
    }

    private static string TrimPreviewText(string? value)
    {
        var normalized = NormalizePreviewText(value).Replace("\r", "\n");
        var firstLine = normalized.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        var text = firstLine ?? normalized;
        return text.Length <= 90 ? text : $"{text[..87]}...";
    }

    private static string NormalizePreviewText(string? value) => value?.Trim() ?? string.Empty;

    private static async Task<bool> IsEncounterLockedAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from encounter_signatures where encounter = @encounter and is_lock;";
        command.Parameters.AddWithValue("encounter", encounter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeSoapField(string? value, string fieldName)
    {
        var normalized = NormalizeText(value);
        if (normalized is { Length: > 10_000 })
        {
            throw new ArgumentException($"{fieldName} cannot exceed 10,000 characters.");
        }

        return normalized;
    }

    private static bool TryParseDateTime(string? value, out DateTime parsed)
    {
        return DateTime.TryParse(value, out parsed);
    }

    private static string CreateSignatureHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Integer);
        parameter.Value = value is null ? DBNull.Value : value.Value;
    }

    private static void AddNullableDecimal(NpgsqlCommand command, string name, decimal? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Numeric);
        parameter.Value = value is null ? DBNull.Value : value.Value;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Text);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    private static DateOnly ParseDateOrDefault(string? value, DateOnly defaultDate) =>
        DateOnly.TryParse(value, out var parsed) ? parsed : defaultDate;

    private static string BuildDisplayName(DbDataReader reader)
    {
        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");
        return string.IsNullOrWhiteSpace(preferredName)
            ? $"{lastName}, {firstName}"
            : $"{lastName}, {firstName} ({preferredName})";
    }

    private static string ReadDate(DbDataReader reader, string columnName) =>
        reader.GetFieldValue<DateOnly>(reader.GetOrdinal(columnName)).ToString("yyyy-MM-dd");

    private static string ReadDateTime(DbDataReader reader, string columnName) =>
        reader.GetFieldValue<DateTime>(reader.GetOrdinal(columnName)).ToString("yyyy-MM-dd HH:mm");

    private static string? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm");
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

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string ClaimStatusLabel(int status, int billProcess)
    {
        if (billProcess != 0)
        {
            return "Queued for billing";
        }

        return status switch
        {
            1 => "Re-opened",
            2 or 3 => "Marked as cleared",
            4 => "Closed",
            5 => "Canceled",
            6 => "Forwarded",
            7 => "Denied",
            _ => "Unsubmitted"
        };
    }

    private sealed record EncounterDocumentPreviewInfo(
        string PreviewKind,
        string PreviewStatus,
        string ThumbnailLabel,
        string ThumbnailText,
        bool CanPreviewInline,
        bool CanDownload);

    private sealed record EncounterDocumentScanReadiness(
        bool IsScannedAttachment,
        string ScanStatus,
        string CaptureSource,
        int ScanPageCount,
        string OcrStatus);

    private sealed record ProcedureOrderRow(int Id, ProcedureOrderItem Order);

    private sealed record ProcedureSpecimenRow(int OrderId, ProcedureSpecimenItem Specimen);

    private sealed record ProcedureReportRow(int Id, int OrderId, ProcedureReportItem Report);

    private sealed record SignatureContentSnapshot(
        string Revision,
        string Manifest,
        string Checksum);

    private sealed record ProcedureResultRow(int ReportId, ProcedureResultItem Result);

    private sealed class DiagnosisAccumulator(string code)
    {
        private readonly List<string> sources = [];
        private readonly HashSet<string> sourceSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> supportingBillingCodes = [];
        private readonly HashSet<string> supportingBillingCodeSet = new(StringComparer.OrdinalIgnoreCase);

        public string Code { get; } = code;

        public string? Description { get; set; }

        public int BillingLineCount { get; set; }

        public int ProcedureOrderCount { get; set; }

        public IReadOnlyList<string> Sources => sources;

        public IReadOnlyList<string> SupportingBillingCodes => supportingBillingCodes;

        public void AddSource(string source)
        {
            if (sourceSet.Add(source))
            {
                sources.Add(source);
            }
        }

        public void AddSupportingBillingCode(string supportingBillingCode)
        {
            var normalized = NormalizeText(supportingBillingCode);
            if (normalized is not null && supportingBillingCodeSet.Add(normalized))
            {
                supportingBillingCodes.Add(normalized);
            }
        }
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

}
