using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class EncounterRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;

    public async Task<EncounterSearchResponse> SearchAsync(
        string? patientId,
        string? from,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, MaximumSearchLimit);
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedPatientId = Normalize(patientId);
        var fromDate = ParseDateOrDefault(from, new DateOnly(metadata.BaseDate.Year, 1, 1));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var totalMatches = await CountMatchesAsync(connection, normalizedPatientId, fromDate, cancellationToken);

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
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name,
                exists (select 1 from vitals v where v.pid = e.pid and v.encounter = e.encounter) as has_vitals,
                exists (select 1 from clinical_notes cn where cn.pid = e.pid and cn.encounter = e.encounter) as has_soap_note,
                (select count(*) from billing b where b.pid = e.pid and b.encounter = e.encounter)::int as billing_line_count
            from encounters e
            join patients p on p.legacy_pid = e.pid
            left join staff s on s.id = e.provider_id
            left join facilities f on f.id = e.facility_id
            where {EncounterSearchPredicate}
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

    public async Task<EncounterDetail?> GetByEncounterAsync(int encounter, CancellationToken cancellationToken)
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
                cn.subjective,
                cn.objective,
                cn.assessment,
                cn.plan,
                (select count(*) from billing b where b.pid = e.pid and b.encounter = e.encounter)::int as billing_line_count
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
                order by note_datetime desc, id desc
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

        return new EncounterDetail(
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
            Vitals: ReadVitals(reader),
            SoapNote: ReadSoapNote(reader),
            BillingLineCount: reader.GetInt32(reader.GetOrdinal("billing_line_count")));
    }

    public async Task<EncounterDetail?> CreateAsync(EncounterCreateRequest request, CancellationToken cancellationToken)
    {
        var patientId = Normalize(request.PatientId);
        var reason = NormalizeText(request.Reason);
        if (patientId is null || reason is null || !TryParseDateTime(request.DateTime, out var encounterDateTime))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with selected_patient as (
                select canonical_id, legacy_pid, provider_id as patient_provider_id, facility_id as patient_facility_id
                from patients
                where lower(canonical_id) = @patientId
                   or lower(pubpid) = @patientId
                   or legacy_pid::text = @patientId
                limit 1
            ),
            next_id as (
                select coalesce(max(greatest(id, encounter)), 0) + 1 as id
                from encounters
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
                billing_note
            )
            select
                next_id.id,
                next_id.id,
                selected_patient.canonical_id,
                selected_patient.legacy_pid,
                coalesce(
                    (select id from staff where id = @providerId),
                    selected_patient.patient_provider_id,
                    (select id from staff where role = 'provider' order by id limit 1)
                ),
                coalesce(
                    (select id from facilities where id = @facilityId),
                    selected_patient.patient_facility_id,
                    (select id from facilities order by id limit 1)
                ),
                coalesce(
                    (select id from facilities where id = @billingFacilityId),
                    (select id from facilities where id = @facilityId),
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
                @billingNote
            from selected_patient
            cross join next_id
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

        var encounter = await command.ExecuteScalarAsync(cancellationToken);
        return encounter is null || encounter is DBNull
            ? null
            : await GetByEncounterAsync(Convert.ToInt32(encounter), cancellationToken);
    }

    public async Task<EncounterDetail?> UpdateSummaryAsync(
        int encounter,
        EncounterUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeText(request.Reason);
        if (reason is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update encounters
            set reason = @reason,
                sensitivity = @sensitivity,
                referral_source = @referralSource,
                external_id = @externalId,
                pos_code = @posCode,
                billing_note = @billingNote
            where encounter = @encounter
            returning encounter;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = reason;
        AddNullableText(command, "sensitivity", NormalizeText(request.Sensitivity));
        AddNullableText(command, "referralSource", NormalizeText(request.ReferralSource));
        AddNullableText(command, "externalId", NormalizeText(request.ExternalId));
        AddNullableInt(command, "posCode", request.PosCode);
        AddNullableText(command, "billingNote", NormalizeText(request.BillingNote));

        var updated = await command.ExecuteScalarAsync(cancellationToken);
        return updated is null || updated is DBNull
            ? null
            : await GetByEncounterAsync(Convert.ToInt32(updated), cancellationToken);
    }

    public async Task<EncounterFormMutationResponse?> CreateVitalsAsync(
        int encounter,
        EncounterVitalsCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDateTime(request.DateTime, out var vitalDateTime))
        {
            return null;
        }

        var bmi = ComputeBmi(request.Weight, request.Height);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with selected_encounter as (
                select patient_id, pid, encounter
                from encounters
                where encounter = @encounter
                limit 1
            ),
            next_id as (
                select coalesce(max(id), 0) + 1 as id
                from vitals
            )
            insert into vitals (
                id,
                patient_id,
                pid,
                encounter,
                vital_datetime,
                bps,
                bpd,
                weight,
                height,
                temperature,
                pulse,
                respiration,
                bmi,
                oxygen_saturation,
                note
            )
            select
                next_id.id,
                selected_encounter.patient_id,
                selected_encounter.pid,
                selected_encounter.encounter,
                @vitalDateTime,
                @systolic,
                @diastolic,
                @weight,
                @height,
                @temperature,
                @pulse,
                @respiration,
                @bmi,
                @oxygenSaturation,
                @note
            from selected_encounter
            cross join next_id
            returning id;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.Add("vitalDateTime", NpgsqlDbType.Timestamp).Value = vitalDateTime;
        AddNullableInt(command, "systolic", request.Systolic);
        AddNullableInt(command, "diastolic", request.Diastolic);
        AddNullableDecimal(command, "weight", request.Weight);
        AddNullableDecimal(command, "height", request.Height);
        AddNullableDecimal(command, "temperature", request.Temperature);
        AddNullableInt(command, "pulse", request.Pulse);
        AddNullableInt(command, "respiration", request.Respiration);
        AddNullableDecimal(command, "bmi", bmi);
        AddNullableInt(command, "oxygenSaturation", request.OxygenSaturation);
        AddNullableText(command, "note", NormalizeText(request.Note));

        var id = await command.ExecuteScalarAsync(cancellationToken);
        if (id is null || id is DBNull)
        {
            return null;
        }

        var detail = await GetByEncounterAsync(encounter, cancellationToken);
        return detail is null ? null : new EncounterFormMutationResponse(Convert.ToInt32(id), detail);
    }

    public async Task<EncounterFormMutationResponse?> CreateSoapNoteAsync(
        int encounter,
        EncounterSoapNoteCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDateTime(request.DateTime, out var noteDateTime))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with selected_encounter as (
                select patient_id, pid, encounter
                from encounters
                where encounter = @encounter
                limit 1
            ),
            next_id as (
                select coalesce(max(id), 0) + 1 as id
                from clinical_notes
            )
            insert into clinical_notes (
                id,
                patient_id,
                pid,
                encounter,
                note_datetime,
                subjective,
                objective,
                assessment,
                plan
            )
            select
                next_id.id,
                selected_encounter.patient_id,
                selected_encounter.pid,
                selected_encounter.encounter,
                @noteDateTime,
                @subjective,
                @objective,
                @assessment,
                @plan
            from selected_encounter
            cross join next_id
            returning id;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.Add("noteDateTime", NpgsqlDbType.Timestamp).Value = noteDateTime;
        AddNullableText(command, "subjective", NormalizeText(request.Subjective));
        AddNullableText(command, "objective", NormalizeText(request.Objective));
        AddNullableText(command, "assessment", NormalizeText(request.Assessment));
        AddNullableText(command, "plan", NormalizeText(request.Plan));

        var id = await command.ExecuteScalarAsync(cancellationToken);
        if (id is null || id is DBNull)
        {
            return null;
        }

        var detail = await GetByEncounterAsync(encounter, cancellationToken);
        return detail is null ? null : new EncounterFormMutationResponse(Convert.ToInt32(id), detail);
    }

    public async Task<bool> DeleteVitalsAsync(int encounter, int vitalsId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from vitals
            where encounter = @encounter and id = @vitalsId;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("vitalsId", vitalsId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteSoapNoteAsync(int encounter, int soapNoteId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from clinical_notes
            where encounter = @encounter and id = @soapNoteId;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        command.Parameters.AddWithValue("soapNoteId", soapNoteId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int encounter, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with deleted_notes as (
                delete from clinical_notes
                where encounter = @encounter
            ),
            deleted_vitals as (
                delete from vitals
                where encounter = @encounter
            ),
            deleted_encounter as (
                delete from encounters
                where encounter = @encounter
                returning 1
            )
            select count(*) from deleted_encounter;
            """;
        command.Parameters.AddWithValue("encounter", encounter);
        var deleted = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(deleted) > 0;
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
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select count(*)
            from encounters e
            join patients p on p.legacy_pid = e.pid
            where {EncounterSearchPredicate};
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
        var subjective = ReadNullableString(reader, "subjective");
        var objective = ReadNullableString(reader, "objective");
        var assessment = ReadNullableString(reader, "assessment");
        var plan = ReadNullableString(reader, "plan");

        if (subjective is null && objective is null && assessment is null && plan is null)
        {
            return null;
        }

        return new EncounterSoapNote(subjective, objective, assessment, plan);
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

    private static bool TryParseDateTime(string? value, out DateTime parsed)
    {
        return DateTime.TryParse(value, out parsed);
    }

    private static decimal? ComputeBmi(decimal? weight, decimal? height)
    {
        if (weight is null || height is null || height <= 0)
        {
            return null;
        }

        return Math.Round(weight.Value / (height.Value * height.Value) * 703m, 2);
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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);
}
