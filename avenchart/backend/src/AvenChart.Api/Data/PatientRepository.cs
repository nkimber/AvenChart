using System.Data.Common;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;

    public async Task<PatientSearchResponse> SearchAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, MaximumSearchLimit);
        var normalizedSearch = NormalizeSearch(search);
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var totalMatches = await CountMatchesAsync(connection, normalizedSearch, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
                p.canonical_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.sex,
                p.date_of_birth,
                p.cohort,
                p.purpose,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.email,
                f.name as facility_name,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                counts.appointment_count,
                counts.encounter_count,
                counts.prescription_count,
                counts.billing_count,
                counts.lab_order_count,
                counts.message_count,
                counts.problem_count,
                counts.allergy_count,
                counts.medication_count
            from patients p
            left join facilities f on f.id = p.facility_id
            left join staff s on s.id = p.provider_id
            left join lateral ({CountsSql("p.legacy_pid")}) counts on true
            where {PatientSearchPredicate}
            order by p.last_name, p.first_name, p.canonical_id
            limit @limit;
            """;
        command.Parameters.AddWithValue("limit", safeLimit);
        AddSearchParameter(command, normalizedSearch);

        var patients = new List<PatientListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            patients.Add(new PatientListItem(
                CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                DisplayName: BuildDisplayName(reader),
                FirstName: reader.GetString(reader.GetOrdinal("first_name")),
                LastName: reader.GetString(reader.GetOrdinal("last_name")),
                PreferredName: ReadNullableString(reader, "preferred_name"),
                Sex: ReadNullableString(reader, "sex"),
                DateOfBirth: ReadDate(reader, "date_of_birth"),
                Age: CalculateAge(reader.GetFieldValue<DateOnly>(reader.GetOrdinal("date_of_birth")), metadata.BaseDate),
                Cohort: ReadNullableString(reader, "cohort"),
                Purpose: ReadNullableString(reader, "purpose"),
                Phone: ReadNullableString(reader, "phone"),
                PhoneHome: ReadNullableString(reader, "phone_home"),
                PhoneCell: ReadNullableString(reader, "phone_cell"),
                Email: ReadNullableString(reader, "email"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                PrimaryProviderName: ReadNullableString(reader, "provider_name"),
                Counts: ReadCounts(reader)));
        }

        return new PatientSearchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Search: search,
            Limit: safeLimit,
            TotalMatches: totalMatches,
            Patients: patients);
    }

    public async Task<PatientChartSummary?> GetChartSummaryAsync(string canonicalId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
                p.canonical_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.sex,
                p.date_of_birth,
                p.cohort,
                p.purpose,
                p.street,
                p.city,
                p.state,
                p.postal_code,
                p.email,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                p.marital_status,
                p.occupation,
                p.portal_enabled,
                p.registration_date,
                f.name as facility_name,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                counts.appointment_count,
                counts.encounter_count,
                counts.prescription_count,
                counts.billing_count,
                counts.lab_order_count,
                counts.message_count,
                counts.problem_count,
                counts.allergy_count,
                counts.medication_count,
                next_appt.appointment_id,
                next_appt.appointment_date,
                next_appt.start_time,
                next_appt.title as appointment_title,
                next_appt.status as appointment_status,
                next_appt.provider_name as appointment_provider,
                next_appt.facility_name as appointment_facility,
                latest_enc.encounter_id,
                latest_enc.encounter_date,
                latest_enc.reason as encounter_reason,
                latest_enc.diagnosis_text,
                latest_enc.provider_name as encounter_provider,
                latest_enc.facility_name as encounter_facility
            from patients p
            left join facilities f on f.id = p.facility_id
            left join staff s on s.id = p.provider_id
            left join lateral ({CountsSql("p.legacy_pid")}) counts on true
            left join lateral (
                select
                    a.id as appointment_id,
                    a.appointment_date,
                    a.start_time,
                    a.title,
                    a.status,
                    trim(concat(ap.first_name, ' ', ap.last_name)) as provider_name,
                    af.name as facility_name
                from appointments a
                left join staff ap on ap.id = a.provider_id
                left join facilities af on af.id = a.facility_id
                where a.pid = p.legacy_pid
                  and a.appointment_date >= @baseDate
                order by a.appointment_date, a.start_time
                limit 1
            ) next_appt on true
            left join lateral (
                select
                    e.encounter as encounter_id,
                    e.encounter_date,
                    e.reason,
                    e.diagnosis_text,
                    trim(concat(ep.first_name, ' ', ep.last_name)) as provider_name,
                    ef.name as facility_name
                from encounters e
                left join staff ep on ep.id = e.provider_id
                left join facilities ef on ef.id = e.facility_id
                where e.pid = p.legacy_pid
                order by e.encounter_date desc, e.encounter desc
                limit 1
            ) latest_enc on true
            where lower(p.canonical_id) = lower(@canonicalId)
               or lower(p.pubpid) = lower(@canonicalId);
            """;
        command.Parameters.AddWithValue("canonicalId", canonicalId);
        command.Parameters.AddWithValue("baseDate", metadata.BaseDate);

        PatientChartSummary summary;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var dateOfBirth = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("date_of_birth"));
            summary = new PatientChartSummary(
                CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                DisplayName: BuildDisplayName(reader),
                FirstName: reader.GetString(reader.GetOrdinal("first_name")),
                LastName: reader.GetString(reader.GetOrdinal("last_name")),
                PreferredName: ReadNullableString(reader, "preferred_name"),
                Sex: ReadNullableString(reader, "sex"),
                DateOfBirth: dateOfBirth.ToString("yyyy-MM-dd"),
                Age: CalculateAge(dateOfBirth, metadata.BaseDate),
                Cohort: ReadNullableString(reader, "cohort"),
                Purpose: ReadNullableString(reader, "purpose"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                Email: ReadNullableString(reader, "email"),
                Phone: ReadNullableString(reader, "phone"),
                PhoneHome: ReadNullableString(reader, "phone_home"),
                PhoneCell: ReadNullableString(reader, "phone_cell"),
                HipaaAllowSms: ReadNullableString(reader, "hipaa_allow_sms"),
                HipaaAllowEmail: ReadNullableString(reader, "hipaa_allow_email"),
                MaritalStatus: ReadNullableString(reader, "marital_status"),
                Occupation: ReadNullableString(reader, "occupation"),
                PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")),
                RegistrationDate: ReadDate(reader, "registration_date"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                PrimaryProviderName: ReadNullableString(reader, "provider_name"),
                Insurance: Array.Empty<PatientInsuranceItem>(),
                Counts: ReadCounts(reader),
                NextAppointment: ReadAppointment(reader),
                LatestEncounter: ReadEncounter(reader));
        }

        var insurance = await GetInsuranceForPatientAsync(connection, summary.CanonicalId, cancellationToken);
        return summary with { Insurance = insurance };
    }

    private static async Task<IReadOnlyList<PatientInsuranceItem>> GetInsuranceForPatientAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, type, provider, plan_name, policy_number, group_number, relationship
            from insurance_records
            where lower(patient_id) = lower(@canonicalId)
            order by
                case lower(coalesce(type, ''))
                    when 'primary' then 1
                    when 'secondary' then 2
                    else 3
                end,
                id;
            """;
        command.Parameters.AddWithValue("canonicalId", canonicalId);

        var coverage = new List<PatientInsuranceItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            coverage.Add(new PatientInsuranceItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Type: ReadNullableString(reader, "type"),
                Provider: ReadNullableString(reader, "provider"),
                PlanName: ReadNullableString(reader, "plan_name"),
                PolicyNumber: ReadNullableString(reader, "policy_number"),
                GroupNumber: ReadNullableString(reader, "group_number"),
                Relationship: ReadNullableString(reader, "relationship")));
        }

        return coverage;
    }

    public async Task<PatientChartSummary?> UpdateContactAsync(
        string patientId,
        PatientContactUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set
                phone = @phoneHome,
                phone_home = @phoneHome,
                phone_cell = @phoneCell,
                email = @email,
                hipaa_allow_sms = @hipaaAllowSms,
                hipaa_allow_email = @hipaaAllowEmail
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("phoneHome", NpgsqlDbType.Text).Value = NormalizeNullable(request.PhoneHome);
        command.Parameters.Add("phoneCell", NpgsqlDbType.Text).Value = NormalizeNullable(request.PhoneCell);
        command.Parameters.Add("email", NpgsqlDbType.Text).Value = NormalizeNullable(request.Email);
        command.Parameters.Add("hipaaAllowSms", NpgsqlDbType.Text).Value = NormalizePermission(request.HipaaAllowSms);
        command.Parameters.Add("hipaaAllowEmail", NpgsqlDbType.Text).Value = NormalizePermission(request.HipaaAllowEmail);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateDemographicsAsync(
        string patientId,
        PatientDemographicsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeDemographics(request, out var normalized))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set
                first_name = @firstName,
                last_name = @lastName,
                preferred_name = @preferredName,
                sex = @sex,
                date_of_birth = @dateOfBirth,
                street = @street,
                city = @city,
                state = @state,
                postal_code = @postalCode,
                marital_status = @maritalStatus,
                occupation = @occupation
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("firstName", normalized.FirstName);
        command.Parameters.AddWithValue("lastName", normalized.LastName);
        command.Parameters.Add("preferredName", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PreferredName);
        command.Parameters.Add("sex", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Sex);
        command.Parameters.Add("dateOfBirth", NpgsqlDbType.Date).Value = normalized.DateOfBirth;
        command.Parameters.Add("street", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Street);
        command.Parameters.Add("city", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.City);
        command.Parameters.Add("state", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.State);
        command.Parameters.Add("postalCode", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PostalCode);
        command.Parameters.Add("maritalStatus", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.MaritalStatus);
        command.Parameters.Add("occupation", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Occupation);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> CreateInsuranceAsync(
        string patientId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeInsurance(request, out var normalized))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientIdentityAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into insurance_records
                (id, patient_id, pid, type, provider, plan_name, policy_number, group_number, relationship)
            values
                (@id, @patientId, @pid, @type, @provider, @planName, @policyNumber, @groupNumber, @relationship);
            """;
        command.Parameters.AddWithValue("id", $"INS-PARITY-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        AddInsuranceParameters(command, normalized);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateInsuranceAsync(
        string insuranceId,
        PatientInsuranceMutationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(insuranceId) || !TryNormalizeInsurance(request, out var normalized))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update insurance_records
            set
                type = @type,
                provider = @provider,
                plan_name = @planName,
                policy_number = @policyNumber,
                group_number = @groupNumber,
                relationship = @relationship
            where id = @id
            returning patient_id;
            """;
        command.Parameters.AddWithValue("id", insuranceId);
        AddInsuranceParameters(command, normalized);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> DeleteInsuranceAsync(string insuranceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(insuranceId))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from insurance_records
            where id = @id
            returning patient_id;
            """;
        command.Parameters.AddWithValue("id", insuranceId);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
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

    private static async Task<int> CountMatchesAsync(NpgsqlConnection connection, string? normalizedSearch, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from patients p where {PatientSearchPredicate};";
        AddSearchParameter(command, normalizedSearch);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task<PatientIdentity?> GetPatientIdentityAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PatientIdentity(
                CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")))
            : null;
    }

    private const string PatientSearchPredicate = """
        (@search is null
         or lower(p.canonical_id) like @search
         or lower(p.pubpid) like @search
         or lower(p.first_name) like @search
         or lower(p.last_name) like @search
         or lower(concat(p.first_name, ' ', p.last_name)) like @search
         or lower(coalesce(p.phone, '')) like @search
         or lower(coalesce(p.phone_home, '')) like @search
         or lower(coalesce(p.phone_cell, '')) like @search
         or lower(coalesce(p.email, '')) like @search)
        """;

    private static string CountsSql(string pidExpression) => $"""
        select
            (select count(*) from appointments a where a.pid = {pidExpression})::int as appointment_count,
            (select count(*) from encounters e where e.pid = {pidExpression})::int as encounter_count,
            (select count(*) from prescriptions pr where pr.pid = {pidExpression})::int as prescription_count,
            (select count(*) from billing b where b.pid = {pidExpression})::int as billing_count,
            (select count(*) from lab_orders lo where lo.pid = {pidExpression})::int as lab_order_count,
            (select count(*) from messages m where m.pid = {pidExpression})::int as message_count,
            (select count(*) from problems prob where prob.pid = {pidExpression} and prob.activity = 1)::int as problem_count,
            (select count(*) from allergies al where al.pid = {pidExpression})::int as allergy_count,
            (select count(*) from medications med where med.pid = {pidExpression} and med.activity = 1)::int as medication_count
        """;

    private static void AddSearchParameter(NpgsqlCommand command, string? normalizedSearch)
    {
        command.Parameters.Add("search", NpgsqlDbType.Text).Value = normalizedSearch is null ? DBNull.Value : $"%{normalizedSearch}%";
    }

    private static string? NormalizeSearch(string? search)
    {
        var trimmed = search?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static object NormalizeNullable(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? DBNull.Value : trimmed;
    }

    private static object NormalizePermission(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? DBNull.Value : trimmed.ToUpperInvariant();
    }

    private static bool TryNormalizeDemographics(
        PatientDemographicsUpdateRequest request,
        out NormalizedPatientDemographics normalized)
    {
        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        var dateOfBirthText = request.DateOfBirth?.Trim();

        if (string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(lastName)
            || !DateOnly.TryParseExact(
                dateOfBirthText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOfBirth))
        {
            normalized = new NormalizedPatientDemographics("", "", null, null, default, null, null, null, null, null, null);
            return false;
        }

        normalized = new NormalizedPatientDemographics(
            FirstName: firstName,
            LastName: lastName,
            PreferredName: NormalizeString(request.PreferredName),
            Sex: NormalizeString(request.Sex),
            DateOfBirth: dateOfBirth,
            Street: NormalizeString(request.Street),
            City: NormalizeString(request.City),
            State: NormalizeString(request.State),
            PostalCode: NormalizeString(request.PostalCode),
            MaritalStatus: NormalizeString(request.MaritalStatus),
            Occupation: NormalizeString(request.Occupation));
        return true;
    }

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool TryNormalizeInsurance(
        PatientInsuranceMutationRequest request,
        out NormalizedInsurance normalized)
    {
        var type = request.Type?.Trim().ToLowerInvariant();
        var provider = request.Provider?.Trim();
        var planName = request.PlanName?.Trim();
        var policyNumber = request.PolicyNumber?.Trim();
        var groupNumber = request.GroupNumber?.Trim();
        var relationship = request.Relationship?.Trim();

        if (string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(planName)
            || string.IsNullOrWhiteSpace(policyNumber)
            || string.IsNullOrWhiteSpace(groupNumber)
            || string.IsNullOrWhiteSpace(relationship))
        {
            normalized = new NormalizedInsurance("", "", "", "", "", "");
            return false;
        }

        normalized = new NormalizedInsurance(type, provider, planName, policyNumber, groupNumber, relationship);
        return true;
    }

    private static void AddInsuranceParameters(NpgsqlCommand command, NormalizedInsurance normalized)
    {
        command.Parameters.AddWithValue("type", normalized.Type);
        command.Parameters.AddWithValue("provider", normalized.Provider);
        command.Parameters.AddWithValue("planName", normalized.PlanName);
        command.Parameters.AddWithValue("policyNumber", normalized.PolicyNumber);
        command.Parameters.AddWithValue("groupNumber", normalized.GroupNumber);
        command.Parameters.AddWithValue("relationship", normalized.Relationship);
    }

    private static PatientActivityCounts ReadCounts(DbDataReader reader) => new(
        Appointments: ReadInt(reader, "appointment_count"),
        Encounters: ReadInt(reader, "encounter_count"),
        Prescriptions: ReadInt(reader, "prescription_count"),
        BillingItems: ReadInt(reader, "billing_count"),
        LabOrders: ReadInt(reader, "lab_order_count"),
        Messages: ReadInt(reader, "message_count"),
        Problems: ReadInt(reader, "problem_count"),
        Allergies: ReadInt(reader, "allergy_count"),
        Medications: ReadInt(reader, "medication_count"));

    private static PatientTimelineItem? ReadAppointment(DbDataReader reader)
    {
        if (reader.IsDBNull(reader.GetOrdinal("appointment_id")))
        {
            return null;
        }

        var time = reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time"));
        return new PatientTimelineItem(
            Id: reader.GetString(reader.GetOrdinal("appointment_id")),
            Date: ReadDate(reader, "appointment_date"),
            Time: time.ToString("HH:mm"),
            Title: ReadNullableString(reader, "appointment_title") ?? "Appointment",
            Status: ReadNullableString(reader, "appointment_status"),
            ProviderName: ReadNullableString(reader, "appointment_provider"),
            FacilityName: ReadNullableString(reader, "appointment_facility"));
    }

    private static PatientTimelineItem? ReadEncounter(DbDataReader reader)
    {
        if (reader.IsDBNull(reader.GetOrdinal("encounter_id")))
        {
            return null;
        }

        var title = ReadNullableString(reader, "encounter_reason")
            ?? ReadNullableString(reader, "diagnosis_text")
            ?? "Encounter";

        return new PatientTimelineItem(
            Id: reader.GetInt32(reader.GetOrdinal("encounter_id")).ToString(),
            Date: ReadDate(reader, "encounter_date"),
            Time: null,
            Title: title,
            Status: ReadNullableString(reader, "diagnosis_text"),
            ProviderName: ReadNullableString(reader, "encounter_provider"),
            FacilityName: ReadNullableString(reader, "encounter_facility"));
    }

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

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly asOfDate)
    {
        var age = asOfDate.Year - dateOfBirth.Year;
        if (dateOfBirth > asOfDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record PatientIdentity(string CanonicalId, int LegacyPid);

    private sealed record NormalizedInsurance(
        string Type,
        string Provider,
        string PlanName,
        string PolicyNumber,
        string GroupNumber,
        string Relationship);

    private sealed record NormalizedPatientDemographics(
        string FirstName,
        string LastName,
        string? PreferredName,
        string? Sex,
        DateOnly DateOfBirth,
        string? Street,
        string? City,
        string? State,
        string? PostalCode,
        string? MaritalStatus,
        string? Occupation);
}
