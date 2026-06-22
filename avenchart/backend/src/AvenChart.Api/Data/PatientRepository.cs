using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
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
                p.provider_id,
                p.facility_id,
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
                ProviderId: ReadNullableInt(reader, "provider_id"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
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
                p.race,
                p.ethnicity,
                p.interpreter,
                p.family_size,
                p.monthly_income,
                p.homeless,
                p.financial_review_date,
                p.mother_name,
                p.guardian_name,
                p.guardian_relationship,
                p.guardian_phone,
                p.guardian_email,
                p.guardian_sex,
                p.guardian_address,
                p.guardian_city,
                p.guardian_state,
                p.guardian_postal_code,
                p.guardian_country,
                p.guardian_work_phone,
                pe.name as employer_name,
                pe.street as employer_street,
                pe.city as employer_city,
                pe.state as employer_state,
                pe.postal_code as employer_postal_code,
                pe.country as employer_country,
                p.portal_enabled,
                p.registration_date,
                p.deceased_date,
                p.deceased_reason,
                p.provider_id,
                p.facility_id,
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
            left join patient_employers pe on pe.patient_id = p.canonical_id
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
                Race: ReadNullableString(reader, "race"),
                Ethnicity: ReadNullableString(reader, "ethnicity"),
                Interpreter: ReadNullableString(reader, "interpreter"),
                FamilySize: ReadNullableIntAsString(reader, "family_size"),
                MonthlyIncome: ReadNullableIntAsString(reader, "monthly_income"),
                Homeless: ReadNullableString(reader, "homeless"),
                FinancialReviewDate: ReadNullableDate(reader, "financial_review_date"),
                MotherName: ReadNullableString(reader, "mother_name"),
                GuardianName: ReadNullableString(reader, "guardian_name"),
                GuardianRelationship: ReadNullableString(reader, "guardian_relationship"),
                GuardianPhone: ReadNullableString(reader, "guardian_phone"),
                GuardianEmail: ReadNullableString(reader, "guardian_email"),
                GuardianSex: ReadNullableString(reader, "guardian_sex"),
                GuardianAddress: ReadNullableString(reader, "guardian_address"),
                GuardianCity: ReadNullableString(reader, "guardian_city"),
                GuardianState: ReadNullableString(reader, "guardian_state"),
                GuardianPostalCode: ReadNullableString(reader, "guardian_postal_code"),
                GuardianCountry: ReadNullableString(reader, "guardian_country"),
                GuardianWorkPhone: ReadNullableString(reader, "guardian_work_phone"),
                EmployerName: ReadNullableString(reader, "employer_name"),
                EmployerStreet: ReadNullableString(reader, "employer_street"),
                EmployerCity: ReadNullableString(reader, "employer_city"),
                EmployerState: ReadNullableString(reader, "employer_state"),
                EmployerPostalCode: ReadNullableString(reader, "employer_postal_code"),
                EmployerCountry: ReadNullableString(reader, "employer_country"),
                PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")),
                RegistrationDate: ReadDate(reader, "registration_date"),
                DeceasedDate: ReadNullableDate(reader, "deceased_date"),
                DeceasedReason: ReadNullableString(reader, "deceased_reason"),
                ProviderId: ReadNullableInt(reader, "provider_id"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                PrimaryProviderName: ReadNullableString(reader, "provider_name"),
                CareTeam: null,
                Insurance: Array.Empty<PatientInsuranceItem>(),
                DuplicateCandidates: Array.Empty<PatientDuplicateCandidate>(),
                Counts: ReadCounts(reader),
                NextAppointment: ReadAppointment(reader),
                LatestEncounter: ReadEncounter(reader));
        }

        var insurance = await GetInsuranceForPatientAsync(connection, summary.CanonicalId, cancellationToken);
        var careTeam = await GetCareTeamForPatientAsync(connection, summary.CanonicalId, cancellationToken);
        var duplicateCandidates = await GetDuplicateCandidatesAsync(
            connection,
            new NormalizedDuplicateSearch(
                FirstName: summary.FirstName,
                LastName: summary.LastName,
                DateOfBirth: DateOnly.ParseExact(summary.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Phone: summary.PhoneHome ?? summary.PhoneCell ?? summary.Phone,
                PhoneDigits: NormalizePhoneDigits(summary.PhoneHome ?? summary.PhoneCell ?? summary.Phone),
                Email: NormalizeString(summary.Email)?.ToLowerInvariant(),
                ExcludePatientId: summary.CanonicalId),
            5,
            cancellationToken);
        return summary with { CareTeam = careTeam, Insurance = insurance, DuplicateCandidates = duplicateCandidates };
    }

    public async Task<PatientProviderAssignmentOptionsResponse> GetProviderAssignmentOptionsAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                s.id,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                s.facility_id,
                f.name as facility_name
            from staff s
            left join facilities f on f.id = s.facility_id
            where s.active = true
              and lower(s.role) = 'provider'
            order by s.last_name, s.first_name, s.id;
            """;

        var providers = new List<PatientProviderAssignmentOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            providers.Add(new PatientProviderAssignmentOption(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                DisplayName: reader.GetString(reader.GetOrdinal("provider_name")),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name")));
        }

        return new PatientProviderAssignmentOptionsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Providers: providers);
    }

    private static async Task<PatientCareTeamSummary?> GetCareTeamForPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                ct.team_name,
                ct.team_status,
                ctm.id,
                ctm.user_id,
                trim(concat(s.first_name, ' ', s.last_name)) as member_name,
                ctm.role,
                ctm.facility_id,
                f.name as facility_name,
                ctm.provider_since,
                ctm.status,
                ctm.note
            from patient_care_teams ct
            left join patient_care_team_members ctm on ctm.patient_id = ct.patient_id
            left join staff s on s.id = ctm.user_id
            left join facilities f on f.id = ctm.facility_id
            where ct.patient_id = @patientId
            order by ctm.id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        string? teamName = null;
        string? teamStatus = null;
        var members = new List<PatientCareTeamMember>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            teamName ??= reader.GetString(reader.GetOrdinal("team_name"));
            teamStatus ??= reader.GetString(reader.GetOrdinal("team_status"));
            if (reader.IsDBNull(reader.GetOrdinal("id")))
            {
                continue;
            }

            var role = reader.GetString(reader.GetOrdinal("role"));
            var status = reader.GetString(reader.GetOrdinal("status"));
            members.Add(new PatientCareTeamMember(
                Id: reader.GetInt64(reader.GetOrdinal("id")),
                UserId: ReadNullableInt(reader, "user_id"),
                MemberName: ReadNullableString(reader, "member_name"),
                Role: role,
                RoleDisplay: CareTeamRoleDisplay(role),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                ProviderSince: ReadNullableDate(reader, "provider_since"),
                Status: status,
                StatusDisplay: CareTeamStatusDisplay(status),
                Note: ReadNullableString(reader, "note")));
        }

        if (teamName is null || teamStatus is null)
        {
            return null;
        }

        return new PatientCareTeamSummary(
            TeamName: teamName,
            TeamStatus: teamStatus,
            TeamStatusDisplay: CareTeamStatusDisplay(teamStatus),
            Members: members);
    }

    public async Task<PatientDuplicateSearchResponse> FindDuplicateCandidatesAsync(
        string? firstName,
        string? lastName,
        string? dateOfBirth,
        string? phone,
        string? email,
        string? excludePatientId,
        int? limit,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var safeLimit = Math.Clamp(limit ?? 10, 1, 25);
        var normalized = NormalizeDuplicateSearch(firstName, lastName, dateOfBirth, phone, email, excludePatientId);
        if (normalized is null)
        {
            return new PatientDuplicateSearchResponse(
                DatasetId: metadata.DatasetId,
                DatasetVersion: metadata.DatasetVersion,
                FirstName: NormalizeString(firstName),
                LastName: NormalizeString(lastName),
                DateOfBirth: NormalizeString(dateOfBirth),
                Phone: NormalizeString(phone),
                Email: NormalizeString(email),
                Limit: safeLimit,
                TotalCandidates: 0,
                Candidates: Array.Empty<PatientDuplicateCandidate>());
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var candidates = await GetDuplicateCandidatesAsync(connection, normalized, safeLimit, cancellationToken);
        return new PatientDuplicateSearchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            FirstName: normalized.FirstName,
            LastName: normalized.LastName,
            DateOfBirth: normalized.DateOfBirth?.ToString("yyyy-MM-dd"),
            Phone: normalized.Phone,
            Email: normalized.Email,
            Limit: safeLimit,
            TotalCandidates: candidates.Count,
            Candidates: candidates);
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

    private static async Task<IReadOnlyList<PatientDuplicateCandidate>> GetDuplicateCandidatesAsync(
        NpgsqlConnection connection,
        NormalizedDuplicateSearch search,
        int limit,
        CancellationToken cancellationToken)
    {
        if (search.DateOfBirth is null
            && string.IsNullOrWhiteSpace(search.PhoneDigits)
            && string.IsNullOrWhiteSpace(search.Email))
        {
            return Array.Empty<PatientDuplicateCandidate>();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                p.canonical_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.date_of_birth,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.email
            from patients p
            where (@excludePatientId is null
                   or (lower(p.canonical_id) <> lower(@excludePatientId)
                       and lower(p.pubpid) <> lower(@excludePatientId)
                       and p.legacy_pid::text <> @excludePatientId))
              and (
                    (@firstName is not null
                     and @lastName is not null
                     and @dateOfBirth is not null
                     and lower(p.first_name) = @firstName
                     and lower(p.last_name) = @lastName
                     and p.date_of_birth = @dateOfBirth)
                    or (@phoneDigits is not null
                        and @phoneDigits in (
                            regexp_replace(coalesce(p.phone, ''), '[^0-9]', '', 'g'),
                            regexp_replace(coalesce(p.phone_home, ''), '[^0-9]', '', 'g'),
                            regexp_replace(coalesce(p.phone_cell, ''), '[^0-9]', '', 'g')))
                    or (@email is not null and lower(coalesce(p.email, '')) = @email)
                  )
            order by p.last_name, p.first_name, p.pubpid
            limit 50;
            """;
        AddDuplicateSearchParameters(command, search);

        var candidates = new List<PatientDuplicateCandidate>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var candidate = BuildDuplicateCandidate(reader, search);
            if (candidate.MatchScore > 0)
            {
                candidates.Add(candidate);
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Pubpid, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public async Task<PatientRegistrationMutationResult> CreatePatientAsync(
        PatientRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var validationIssues = ValidateRegistration(request, out var normalized);
        if (validationIssues.Count > 0)
        {
            return new PatientRegistrationMutationResult(null, validationIssues);
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into patients
                (canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name, sex, date_of_birth,
                 cohort, purpose, street, city, state, postal_code, email, phone, phone_home, phone_cell,
                 hipaa_allow_sms, hipaa_allow_email, marital_status, occupation, provider_id, facility_id,
                 portal_enabled, registration_date)
            values
                (@canonicalId, (select coalesce(max(legacy_pid), 100000) + 1 from patients), @pubpid,
                 @firstName, @lastName, @preferredName, @sex, @dateOfBirth,
                 null, 'registered via modernized patient workspace', @street, @city, @state, @postalCode,
                 @email, @phoneHome, @phoneHome, @phoneCell, @hipaaAllowSms, @hipaaAllowEmail,
                 @maritalStatus, @occupation, null, null, false, @registrationDate)
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("canonicalId", normalized.Pubpid);
        command.Parameters.AddWithValue("pubpid", normalized.Pubpid);
        command.Parameters.AddWithValue("firstName", normalized.FirstName);
        command.Parameters.AddWithValue("lastName", normalized.LastName);
        command.Parameters.Add("preferredName", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PreferredName);
        command.Parameters.Add("sex", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Sex);
        command.Parameters.Add("dateOfBirth", NpgsqlDbType.Date).Value = normalized.DateOfBirth;
        command.Parameters.Add("street", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Street);
        command.Parameters.Add("city", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.City);
        command.Parameters.Add("state", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.State);
        command.Parameters.Add("postalCode", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PostalCode);
        command.Parameters.Add("email", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Email);
        command.Parameters.Add("phoneHome", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PhoneHome);
        command.Parameters.Add("phoneCell", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.PhoneCell);
        command.Parameters.Add("hipaaAllowSms", NpgsqlDbType.Text).Value = normalized.HipaaAllowSms;
        command.Parameters.Add("hipaaAllowEmail", NpgsqlDbType.Text).Value = normalized.HipaaAllowEmail;
        command.Parameters.Add("maritalStatus", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.MaritalStatus);
        command.Parameters.Add("occupation", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Occupation);
        command.Parameters.Add("registrationDate", NpgsqlDbType.Date).Value = metadata.BaseDate;

        try
        {
            var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
            var patient = canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
            return new PatientRegistrationMutationResult(patient, Array.Empty<PatientRegistrationValidationIssue>());
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new PatientRegistrationMutationResult(
                null,
                new[]
                {
                    new PatientRegistrationValidationIssue(
                        "pubpid",
                        "duplicate",
                        "Public ID is already in use.")
                });
        }
    }

    public async Task<bool> DeleteTemporaryPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from patients
            where (lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId)
              and (canonical_id like 'TMP-PAT-REG-%' or pubpid like 'TMP-PAT-REG-%')
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
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
                occupation = @occupation,
                race = @race,
                ethnicity = @ethnicity,
                interpreter = @interpreter,
                family_size = @familySize,
                monthly_income = @monthlyIncome,
                homeless = @homeless,
                financial_review_date = @financialReviewDate
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
        command.Parameters.Add("race", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Race);
        command.Parameters.Add("ethnicity", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Ethnicity);
        command.Parameters.Add("interpreter", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Interpreter);
        command.Parameters.Add("familySize", NpgsqlDbType.Integer).Value = normalized.FamilySize is null
            ? DBNull.Value
            : normalized.FamilySize.Value;
        command.Parameters.Add("monthlyIncome", NpgsqlDbType.Integer).Value = normalized.MonthlyIncome is null
            ? DBNull.Value
            : normalized.MonthlyIncome.Value;
        command.Parameters.Add("homeless", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Homeless);
        command.Parameters.Add("financialReviewDate", NpgsqlDbType.Date).Value = normalized.FinancialReviewDate is null
            ? DBNull.Value
            : normalized.FinancialReviewDate.Value;

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateDeceasedStatusAsync(
        string patientId,
        PatientDeceasedStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeDeceasedStatus(request, out var deceasedDate, out var deceasedReason))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set
                deceased_date = @deceasedDate,
                deceased_reason = @deceasedReason
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("deceasedDate", NpgsqlDbType.Date).Value = deceasedDate is null
            ? DBNull.Value
            : deceasedDate.Value;
        command.Parameters.Add("deceasedReason", NpgsqlDbType.Text).Value = deceasedReason is null
            ? DBNull.Value
            : deceasedReason;

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateGuardianContactAsync(
        string patientId,
        PatientGuardianContactUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set
                mother_name = @motherName,
                guardian_name = @guardianName,
                guardian_relationship = @guardianRelationship,
                guardian_phone = @guardianPhone,
                guardian_email = @guardianEmail,
                guardian_sex = @guardianSex,
                guardian_address = @guardianAddress,
                guardian_city = @guardianCity,
                guardian_state = @guardianState,
                guardian_postal_code = @guardianPostalCode,
                guardian_country = @guardianCountry,
                guardian_work_phone = @guardianWorkPhone
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("motherName", NpgsqlDbType.Text).Value = NormalizeNullable(request.MotherName);
        command.Parameters.Add("guardianName", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianName);
        command.Parameters.Add("guardianRelationship", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianRelationship);
        command.Parameters.Add("guardianPhone", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianPhone);
        command.Parameters.Add("guardianEmail", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianEmail);
        command.Parameters.Add("guardianSex", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianSex);
        command.Parameters.Add("guardianAddress", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianAddress);
        command.Parameters.Add("guardianCity", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianCity);
        command.Parameters.Add("guardianState", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianState);
        command.Parameters.Add("guardianPostalCode", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianPostalCode);
        command.Parameters.Add("guardianCountry", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianCountry);
        command.Parameters.Add("guardianWorkPhone", NpgsqlDbType.Text).Value = NormalizeNullable(request.GuardianWorkPhone);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateEmployerAsync(
        string patientId,
        PatientEmployerUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with matched_patient as (
                select canonical_id, legacy_pid
                from patients
                where lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId
                limit 1
            ),
            upserted as (
                insert into patient_employers (
                    patient_id,
                    pid,
                    name,
                    street,
                    city,
                    state,
                    postal_code,
                    country,
                    recorded_date
                )
                select
                    canonical_id,
                    legacy_pid,
                    @employerName,
                    @employerStreet,
                    @employerCity,
                    @employerState,
                    @employerPostalCode,
                    @employerCountry,
                    current_date
                from matched_patient
                on conflict (patient_id) do update set
                    name = excluded.name,
                    street = excluded.street,
                    city = excluded.city,
                    state = excluded.state,
                    postal_code = excluded.postal_code,
                    country = excluded.country,
                    recorded_date = excluded.recorded_date
                returning patient_id
            )
            select patient_id from upserted;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("employerName", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerName);
        command.Parameters.Add("employerStreet", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerStreet);
        command.Parameters.Add("employerCity", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerCity);
        command.Parameters.Add("employerState", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerState);
        command.Parameters.Add("employerPostalCode", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerPostalCode);
        command.Parameters.Add("employerCountry", NpgsqlDbType.Text).Value = NormalizeNullable(request.EmployerCountry);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateProviderAssignmentAsync(
        string patientId,
        PatientProviderAssignmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set provider_id = @providerId
            where (
                    lower(canonical_id) = lower(@patientId)
                 or lower(pubpid) = lower(@patientId)
                 or legacy_pid::text = @patientId
            )
              and (
                    @providerId is null
                 or exists (
                        select 1
                        from staff
                        where id = @providerId
                          and active = true
                          and lower(role) = 'provider'
                    )
              )
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null
            ? DBNull.Value
            : request.ProviderId.Value;

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateCareTeamAsync(
        string patientId,
        PatientCareTeamUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCareTeam(request);
        if (normalized.Invalid)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientIdentityAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (normalized.Members.Count == 0)
        {
            await DeleteCareTeamAsync(connection, transaction, patient.CanonicalId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
        }

        foreach (var member in normalized.Members)
        {
            if (!await CareTeamUserExistsAsync(connection, transaction, member.UserId, cancellationToken)
                || (member.FacilityId is not null
                    && !await CareTeamFacilityExistsAsync(connection, transaction, member.FacilityId.Value, cancellationToken)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        await DeleteCareTeamAsync(connection, transaction, patient.CanonicalId, cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_care_teams (
                    patient_id,
                    pid,
                    team_name,
                    team_status,
                    note,
                    updated_at
                )
                values (
                    @patientId,
                    @pid,
                    @teamName,
                    @teamStatus,
                    @note,
                    now()
                );
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("pid", patient.LegacyPid);
            command.Parameters.AddWithValue("teamName", normalized.TeamName);
            command.Parameters.AddWithValue("teamStatus", normalized.TeamStatus);
            command.Parameters.Add("note", NpgsqlDbType.Text).Value = normalized.Note is null ? DBNull.Value : normalized.Note;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var member in normalized.Members)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_care_team_members (
                    patient_id,
                    user_id,
                    role,
                    facility_id,
                    provider_since,
                    status,
                    note
                )
                values (
                    @patientId,
                    @userId,
                    @role,
                    @facilityId,
                    @providerSince,
                    @status,
                    @note
                );
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("userId", member.UserId);
            command.Parameters.AddWithValue("role", member.Role);
            command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = member.FacilityId is null
                ? DBNull.Value
                : member.FacilityId.Value;
            command.Parameters.Add("providerSince", NpgsqlDbType.Date).Value = member.ProviderSince is null
                ? DBNull.Value
                : member.ProviderSince.Value;
            command.Parameters.AddWithValue("status", member.Status);
            command.Parameters.Add("note", NpgsqlDbType.Text).Value = member.Note is null ? DBNull.Value : member.Note;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
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

    private static void AddDuplicateSearchParameters(NpgsqlCommand command, NormalizedDuplicateSearch search)
    {
        command.Parameters.Add("firstName", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(search.FirstName)
            ? DBNull.Value
            : search.FirstName.ToLowerInvariant();
        command.Parameters.Add("lastName", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(search.LastName)
            ? DBNull.Value
            : search.LastName.ToLowerInvariant();
        command.Parameters.Add("dateOfBirth", NpgsqlDbType.Date).Value = search.DateOfBirth is null
            ? DBNull.Value
            : search.DateOfBirth.Value;
        command.Parameters.Add("phoneDigits", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(search.PhoneDigits)
            ? DBNull.Value
            : search.PhoneDigits;
        command.Parameters.Add("email", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(search.Email)
            ? DBNull.Value
            : search.Email.ToLowerInvariant();
        command.Parameters.Add("excludePatientId", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(search.ExcludePatientId)
            ? DBNull.Value
            : search.ExcludePatientId;
    }

    private static PatientDuplicateCandidate BuildDuplicateCandidate(DbDataReader reader, NormalizedDuplicateSearch search)
    {
        var candidateFirstName = reader.GetString(reader.GetOrdinal("first_name"));
        var candidateLastName = reader.GetString(reader.GetOrdinal("last_name"));
        var candidateDateOfBirth = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("date_of_birth"));
        var phone = ReadNullableString(reader, "phone");
        var phoneHome = ReadNullableString(reader, "phone_home");
        var phoneCell = ReadNullableString(reader, "phone_cell");
        var email = ReadNullableString(reader, "email");

        var score = 0;
        var reasons = new List<string>();
        if (!string.IsNullOrWhiteSpace(search.FirstName)
            && !string.IsNullOrWhiteSpace(search.LastName)
            && search.DateOfBirth is not null
            && string.Equals(candidateFirstName, search.FirstName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidateLastName, search.LastName, StringComparison.OrdinalIgnoreCase)
            && candidateDateOfBirth == search.DateOfBirth)
        {
            score += 80;
            reasons.Add("Same first name, last name, and date of birth");
        }

        if (!string.IsNullOrWhiteSpace(search.PhoneDigits)
            && new[] { phone, phoneHome, phoneCell }
                .Select(NormalizePhoneDigits)
                .Any(candidatePhone => candidatePhone == search.PhoneDigits))
        {
            score += 10;
            reasons.Add("Matching phone");
        }

        if (!string.IsNullOrWhiteSpace(search.Email)
            && string.Equals(email, search.Email, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("Matching email");
        }

        return new PatientDuplicateCandidate(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            DisplayName: BuildDisplayName(reader),
            FirstName: candidateFirstName,
            LastName: candidateLastName,
            DateOfBirth: candidateDateOfBirth.ToString("yyyy-MM-dd"),
            Phone: phone,
            PhoneHome: phoneHome,
            PhoneCell: phoneCell,
            Email: email,
            MatchScore: score,
            MatchReasons: reasons);
    }

    private static string? NormalizeSearch(string? search)
    {
        var trimmed = search?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static NormalizedDuplicateSearch? NormalizeDuplicateSearch(
        string? firstName,
        string? lastName,
        string? dateOfBirth,
        string? phone,
        string? email,
        string? excludePatientId)
    {
        var normalizedFirstName = NormalizeString(firstName);
        var normalizedLastName = NormalizeString(lastName);
        DateOnly? normalizedDateOfBirth = null;
        if (!string.IsNullOrWhiteSpace(dateOfBirth)
            && DateOnly.TryParseExact(
                dateOfBirth.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDateOfBirth))
        {
            normalizedDateOfBirth = parsedDateOfBirth;
        }

        var normalizedPhone = NormalizeString(phone);
        var normalizedPhoneDigits = NormalizePhoneDigits(normalizedPhone);
        var normalizedEmail = NormalizeString(email)?.ToLowerInvariant();

        if ((string.IsNullOrWhiteSpace(normalizedFirstName)
             || string.IsNullOrWhiteSpace(normalizedLastName)
             || normalizedDateOfBirth is null)
            && string.IsNullOrWhiteSpace(normalizedPhoneDigits)
            && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        return new NormalizedDuplicateSearch(
            FirstName: normalizedFirstName,
            LastName: normalizedLastName,
            DateOfBirth: normalizedDateOfBirth,
            Phone: normalizedPhone,
            PhoneDigits: normalizedPhoneDigits,
            Email: normalizedEmail,
            ExcludePatientId: NormalizeString(excludePatientId));
    }

    private static string? NormalizePhoneDigits(string? value)
    {
        var digits = value is null ? "" : new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
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
        var familySize = NormalizeOptionalInt(request.FamilySize);
        var monthlyIncome = NormalizeOptionalInt(request.MonthlyIncome);
        var financialReviewDate = NormalizeOptionalDate(request.FinancialReviewDate);

        if (string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(lastName)
            || !DateOnly.TryParseExact(
                dateOfBirthText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOfBirth)
            || familySize.Invalid
            || monthlyIncome.Invalid
            || financialReviewDate.Invalid)
        {
            normalized = new NormalizedPatientDemographics("", "", null, null, default, null, null, null, null, null, null, null, null, null, null, null, null, null);
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
            Occupation: NormalizeString(request.Occupation),
            Race: NormalizeString(request.Race),
            Ethnicity: NormalizeString(request.Ethnicity),
            Interpreter: NormalizeString(request.Interpreter),
            FamilySize: familySize.Value,
            MonthlyIncome: monthlyIncome.Value,
            Homeless: NormalizeString(request.Homeless),
            FinancialReviewDate: financialReviewDate.Value);
        return true;
    }

    private static NormalizedPatientCareTeam NormalizeCareTeam(PatientCareTeamUpdateRequest request)
    {
        var teamStatus = NormalizeCareTeamStatus(request.TeamStatus);
        if (teamStatus is null && !string.IsNullOrWhiteSpace(request.TeamStatus))
        {
            return NormalizedPatientCareTeam.InvalidValue;
        }

        var memberRequests = request.Members;
        if (memberRequests is null)
        {
            memberRequests =
            [
                new PatientCareTeamMemberUpdateRequest(
                    UserId: request.UserId,
                    Role: request.Role,
                    FacilityId: request.FacilityId,
                    ProviderSince: request.ProviderSince,
                    Status: request.Status,
                    Note: request.Note)
            ];
        }

        var members = new List<NormalizedPatientCareTeamMember>();
        foreach (var memberRequest in memberRequests)
        {
            var normalizedMember = NormalizeCareTeamMember(memberRequest);
            if (normalizedMember.Invalid)
            {
                return NormalizedPatientCareTeam.InvalidValue;
            }

            if (normalizedMember.UserId is not null)
            {
                members.Add(new NormalizedPatientCareTeamMember(
                    UserId: normalizedMember.UserId.Value,
                    Role: normalizedMember.Role,
                    FacilityId: normalizedMember.FacilityId,
                    ProviderSince: normalizedMember.ProviderSince,
                    Status: normalizedMember.Status,
                    Note: normalizedMember.Note));
            }
        }

        return new NormalizedPatientCareTeam(
            TeamName: NormalizeString(request.TeamName) ?? "Care Team",
            TeamStatus: teamStatus ?? "active",
            Members: members,
            Note: NormalizeString(request.Note),
            Invalid: false);
    }

    private static NormalizedPatientCareTeamMemberCandidate NormalizeCareTeamMember(
        PatientCareTeamMemberUpdateRequest request)
    {
        if (request.UserId is null)
        {
            return new NormalizedPatientCareTeamMemberCandidate(
                UserId: null,
                Role: "primary_care_provider",
                FacilityId: null,
                ProviderSince: null,
                Status: "active",
                Note: NormalizeString(request.Note),
                Invalid: false);
        }

        if (request.UserId <= 0 || request.FacilityId <= 0)
        {
            return NormalizedPatientCareTeamMemberCandidate.InvalidValue;
        }

        var providerSince = NormalizeOptionalDate(request.ProviderSince);
        var role = NormalizeCareTeamRole(request.Role);
        var status = NormalizeCareTeamStatus(request.Status);
        if (providerSince.Invalid
            || (role is null && !string.IsNullOrWhiteSpace(request.Role))
            || (status is null && !string.IsNullOrWhiteSpace(request.Status)))
        {
            return NormalizedPatientCareTeamMemberCandidate.InvalidValue;
        }

        return new NormalizedPatientCareTeamMemberCandidate(
            UserId: request.UserId.Value,
            Role: role ?? "primary_care_provider",
            FacilityId: request.FacilityId,
            ProviderSince: providerSince.Value,
            Status: status ?? "active",
            Note: NormalizeString(request.Note),
            Invalid: false);
    }

    private static string? NormalizeCareTeamRole(string? value)
    {
        var normalized = NormalizeString(value);
        return normalized switch
        {
            null => null,
            "family_medicine_specialist" => normalized,
            "case_manager" => normalized,
            "caregiver" => normalized,
            "nurse" => normalized,
            "social_worker" => normalized,
            "pharmacist" => normalized,
            "specialist" => normalized,
            "other" => normalized,
            "physician" => normalized,
            "nurse_practitioner" => normalized,
            "physician_assistant" => normalized,
            "therapist" => normalized,
            "primary_care_provider" => normalized,
            "dietitian" => normalized,
            "mental_health" => normalized,
            "healthcare_professional" => normalized,
            _ => null
        };
    }

    private static string? NormalizeCareTeamStatus(string? value)
    {
        var normalized = NormalizeString(value);
        return normalized switch
        {
            null => null,
            "proposed" => normalized,
            "active" => normalized,
            "suspended" => normalized,
            "inactive" => normalized,
            "entered-in-error" => normalized,
            _ => null
        };
    }

    private static string CareTeamRoleDisplay(string value) =>
        value switch
        {
            "family_medicine_specialist" => "Family Medicine Specialist",
            "case_manager" => "Case Manager",
            "caregiver" => "Caregiver",
            "nurse" => "Nurse",
            "social_worker" => "Social Worker",
            "pharmacist" => "Pharmacist",
            "specialist" => "Specialist",
            "physician" => "Physician",
            "nurse_practitioner" => "Nurse Practitioner",
            "physician_assistant" => "Physician Assistant",
            "therapist" => "Clinical Therapist",
            "primary_care_provider" => "Primary Care Provider",
            "dietitian" => "Dietitian",
            "mental_health" => "Mental Health Professional",
            "healthcare_professional" => "Healthcare Professional",
            "other" => "Other",
            _ => value
        };

    private static string CareTeamStatusDisplay(string value) =>
        value switch
        {
            "proposed" => "Proposed",
            "active" => "Active",
            "suspended" => "Suspended",
            "inactive" => "Inactive",
            "entered-in-error" => "Entered In Error",
            _ => value
        };

    private static async Task<bool> CareTeamUserExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from staff
                where id = @userId
                  and active = true
            );
            """;
        command.Parameters.AddWithValue("userId", userId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> CareTeamFacilityExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from facilities
                where id = @facilityId
                  and inactive = false
            );
            """;
        command.Parameters.AddWithValue("facilityId", facilityId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task DeleteCareTeamAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            delete from patient_care_team_members
            where patient_id = @patientId;

            delete from patient_care_teams
            where patient_id = @patientId;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool TryNormalizeDeceasedStatus(
        PatientDeceasedStatusUpdateRequest request,
        out DateOnly? deceasedDate,
        out string? deceasedReason)
    {
        var dateText = request.DeceasedDate?.Trim();
        if (string.IsNullOrWhiteSpace(dateText))
        {
            deceasedDate = null;
            deceasedReason = NormalizeString(request.DeceasedReason);
            return true;
        }

        if (!DateOnly.TryParseExact(
                dateText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            deceasedDate = null;
            deceasedReason = null;
            return false;
        }

        deceasedDate = parsedDate;
        deceasedReason = NormalizeString(request.DeceasedReason);
        return true;
    }

    private static NormalizedOptionalInt NormalizeOptionalInt(string? value)
    {
        var normalized = NormalizeString(value);
        if (normalized is null)
        {
            return new NormalizedOptionalInt(null, false);
        }

        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? new NormalizedOptionalInt(parsed, false)
            : new NormalizedOptionalInt(null, true);
    }

    private static NormalizedOptionalDate NormalizeOptionalDate(string? value)
    {
        var normalized = NormalizeString(value);
        if (normalized is null)
        {
            return new NormalizedOptionalDate(null, false);
        }

        return DateOnly.TryParseExact(
                normalized,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
            ? new NormalizedOptionalDate(parsed, false)
            : new NormalizedOptionalDate(null, true);
    }

    private static IReadOnlyList<PatientRegistrationValidationIssue> ValidateRegistration(
        PatientRegistrationRequest request,
        out NormalizedPatientRegistration normalized)
    {
        var issues = new List<PatientRegistrationValidationIssue>();
        var pubpid = request.Pubpid?.Trim();
        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        var sex = request.Sex?.Trim();
        var dateOfBirthText = request.DateOfBirth?.Trim();
        var email = NormalizeString(request.Email);

        if (string.IsNullOrWhiteSpace(pubpid))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "pubpid",
                "required",
                "Public ID is required by the modernized canonical patient mapping."));
        }
        else if (pubpid.Length > 255)
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "pubpid",
                "maxLength",
                "Public ID must be 255 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "firstName",
                "required",
                "First name is required."));
        }
        else if (firstName.Length > 255)
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "firstName",
                "maxLength",
                "First name must be 255 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "lastName",
                "required",
                "Last name is required."));
        }
        else if (lastName.Length < 2)
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "lastName",
                "minLength",
                "Last name must be at least 2 characters."));
        }
        else if (lastName.Length > 255)
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "lastName",
                "maxLength",
                "Last name must be 255 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(sex))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "sex",
                "required",
                "Sex is required."));
        }
        else if (sex.Length is < 4 or > 30)
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "sex",
                "length",
                "Sex must be between 4 and 30 characters."));
        }

        if (string.IsNullOrWhiteSpace(dateOfBirthText)
            || !DateOnly.TryParseExact(
                dateOfBirthText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOfBirth))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "dateOfBirth",
                "date",
                "Date of birth must be a valid date in yyyy-MM-dd format."));
            dateOfBirth = default;
        }

        if (email is not null && !IsValidEmail(email))
        {
            issues.Add(new PatientRegistrationValidationIssue(
                "email",
                "email",
                "Email must be a valid email address."));
        }

        if (issues.Count > 0)
        {
            normalized = new NormalizedPatientRegistration(
                "",
                "",
                "",
                null,
                null,
                default,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "YES",
                "YES");
            return issues;
        }

        normalized = new NormalizedPatientRegistration(
            Pubpid: pubpid!,
            FirstName: firstName!,
            LastName: lastName!,
            PreferredName: NormalizeString(request.PreferredName),
            Sex: sex,
            DateOfBirth: dateOfBirth,
            Street: NormalizeString(request.Street),
            City: NormalizeString(request.City),
            State: NormalizeString(request.State),
            PostalCode: NormalizeString(request.PostalCode),
            MaritalStatus: NormalizeString(request.MaritalStatus),
            Occupation: NormalizeString(request.Occupation),
            PhoneHome: NormalizeString(request.PhoneHome),
            PhoneCell: NormalizeString(request.PhoneCell),
            Email: email,
            HipaaAllowSms: NormalizePermissionOrDefault(request.HipaaAllowSms),
            HipaaAllowEmail: NormalizePermissionOrDefault(request.HipaaAllowEmail));
        return Array.Empty<PatientRegistrationValidationIssue>();
    }

    private static string? NormalizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizePermissionOrDefault(string? value)
    {
        var normalized = NormalizeString(value)?.ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "YES" : normalized;
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
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

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

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

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableIntAsString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal).ToString(CultureInfo.InvariantCulture);
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

    private sealed record NormalizedDuplicateSearch(
        string? FirstName,
        string? LastName,
        DateOnly? DateOfBirth,
        string? Phone,
        string? PhoneDigits,
        string? Email,
        string? ExcludePatientId);

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
        string? Occupation,
        string? Race,
        string? Ethnicity,
        string? Interpreter,
        int? FamilySize,
        int? MonthlyIncome,
        string? Homeless,
        DateOnly? FinancialReviewDate);

    private sealed record NormalizedOptionalInt(int? Value, bool Invalid);

    private sealed record NormalizedOptionalDate(DateOnly? Value, bool Invalid);

    private sealed record NormalizedPatientCareTeam(
        string TeamName,
        string TeamStatus,
        IReadOnlyList<NormalizedPatientCareTeamMember> Members,
        string? Note,
        bool Invalid)
    {
        public static NormalizedPatientCareTeam InvalidValue { get; } =
            new("", "", [], null, true);
    }

    private sealed record NormalizedPatientCareTeamMember(
        int UserId,
        string Role,
        int? FacilityId,
        DateOnly? ProviderSince,
        string Status,
        string? Note);

    private sealed record NormalizedPatientCareTeamMemberCandidate(
        int? UserId,
        string Role,
        int? FacilityId,
        DateOnly? ProviderSince,
        string Status,
        string? Note,
        bool Invalid)
    {
        public static NormalizedPatientCareTeamMemberCandidate InvalidValue { get; } =
            new(null, "", null, null, "", null, true);
    }

    private sealed record NormalizedPatientRegistration(
        string Pubpid,
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
        string? Occupation,
        string? PhoneHome,
        string? PhoneCell,
        string? Email,
        string HipaaAllowSms,
        string HipaaAllowEmail);
}
