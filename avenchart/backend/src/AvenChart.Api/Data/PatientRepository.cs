using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;
    private static int mergeColumnsInitialized;

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
                p.cms_portal_login,
                ppa.portal_username as portal_account_username,
                ppa.portal_login_username as portal_account_login_username,
                ppa.password_status as portal_account_password_status,
                ppa.one_time_token as portal_account_one_time_token,
                p.registration_date,
                p.deceased_date,
                p.deceased_reason,
                p.lifecycle_status,
                p.retired_at,
                p.retired_by,
                p.retirement_reason,
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
            left join patient_portal_accounts ppa on ppa.patient_id = p.canonical_id
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
                PortalAccount: ReadPortalAccount(reader),
                RegistrationDate: ReadDate(reader, "registration_date"),
                DeceasedDate: ReadNullableDate(reader, "deceased_date"),
                DeceasedReason: ReadNullableString(reader, "deceased_reason"),
                LifecycleStatus: ReadNullableString(reader, "lifecycle_status") ?? "active",
                RetiredAt: ReadNullableTimestamp(reader, "retired_at"),
                RetiredBy: ReadNullableString(reader, "retired_by"),
                RetirementReason: ReadNullableString(reader, "retirement_reason"),
                ProviderId: ReadNullableInt(reader, "provider_id"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                PrimaryProviderName: ReadNullableString(reader, "provider_name"),
                CareTeam: null,
                Insurance: Array.Empty<PatientInsuranceItem>(),
                History: null,
                DuplicateCandidates: Array.Empty<PatientDuplicateCandidate>(),
                Counts: ReadCounts(reader),
                NextAppointment: ReadAppointment(reader),
                LatestEncounter: ReadEncounter(reader));
        }

        var insurance = await GetInsuranceForPatientAsync(connection, summary.CanonicalId, cancellationToken);
        var careTeam = await GetCareTeamForPatientAsync(connection, summary.CanonicalId, cancellationToken);
        var history = await GetHistoryForPatientAsync(connection, summary.CanonicalId, cancellationToken);
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
        return summary with { CareTeam = careTeam, Insurance = insurance, History = history, DuplicateCandidates = duplicateCandidates };
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

    public async Task<PatientCareTeamOptionsResponse?> GetCareTeamOptionsAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientIdentityAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var providers = new List<PatientProviderAssignmentOption>();
        await using (var command = connection.CreateCommand())
        {
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

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                providers.Add(new PatientProviderAssignmentOption(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    DisplayName: reader.GetString(reader.GetOrdinal("provider_name")),
                    FacilityId: ReadNullableInt(reader, "facility_id"),
                    FacilityName: ReadNullableString(reader, "facility_name")));
            }
        }

        var contacts = new List<PatientCareTeamContactOption>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    contact_id,
                    display_name,
                    relationship,
                    phone,
                    email
                from patient_related_contacts
                where patient_id = @patientId
                  and active = true
                order by display_name, contact_id;
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                contacts.Add(new PatientCareTeamContactOption(
                    Id: reader.GetInt64(reader.GetOrdinal("contact_id")),
                    DisplayName: reader.GetString(reader.GetOrdinal("display_name")),
                    Relationship: ReadNullableString(reader, "relationship"),
                    Phone: ReadNullableString(reader, "phone"),
                    Email: ReadNullableString(reader, "email")));
            }
        }

        return new PatientCareTeamOptionsResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Providers: providers,
            Contacts: contacts);
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
                ctm.contact_id,
                coalesce(nullif(trim(concat(s.first_name, ' ', s.last_name)), ''), prc.display_name) as member_name,
                ctm.role,
                ctm.facility_id,
                f.name as facility_name,
                ctm.provider_since,
                ctm.status,
                ctm.note
            from patient_care_teams ct
            left join patient_care_team_members ctm on ctm.patient_id = ct.patient_id
            left join staff s on s.id = ctm.user_id
            left join patient_related_contacts prc on prc.contact_id = ctm.contact_id
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
            var contactId = ReadNullableLong(reader, "contact_id");
            members.Add(new PatientCareTeamMember(
                Id: reader.GetInt64(reader.GetOrdinal("id")),
                UserId: ReadNullableInt(reader, "user_id"),
                ContactId: contactId,
                MemberType: contactId is null ? "provider" : "contact",
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

    public async Task<PatientDuplicateReviewQueueResponse> GetDuplicateReviewQueueAsync(int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select p1.canonical_id,p2.canonical_id,p1.last_name||', '||p1.first_name,p2.last_name||', '||p2.first_name,p1.date_of_birth,
              (case when lower(p1.first_name)=lower(p2.first_name) then 35 else 0 end + case when lower(p1.last_name)=lower(p2.last_name) then 35 else 0 end + case when coalesce(nullif(lower(p1.email),''),'')<>'' and lower(p1.email)=lower(p2.email) then 20 else 0 end + case when coalesce(nullif(p1.phone_cell,''),'')<>'' and p1.phone_cell=p2.phone_cell then 10 else 0 end) as score,
              coalesce(d.status,'pending')
            from patients p1 join patients p2 on p1.canonical_id<p2.canonical_id and p1.date_of_birth=p2.date_of_birth
            left join patient_duplicate_review_dispositions d on d.target_patient_id=p1.canonical_id and d.source_patient_id=p2.canonical_id
            where p1.merged_into_patient_id is null and p2.merged_into_patient_id is null and
              (lower(p1.first_name)=lower(p2.first_name) or lower(p1.last_name)=lower(p2.last_name) or (coalesce(nullif(lower(p1.email),''),'')<>'' and lower(p1.email)=lower(p2.email)) or (coalesce(nullif(p1.phone_cell,''),'')<>'' and p1.phone_cell=p2.phone_cell))
            order by score desc,p1.last_name,p1.first_name limit @limit;
            """;
        command.Parameters.AddWithValue("limit", safeLimit);
        var items = new List<PatientDuplicateReviewItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var reasons = new List<string>(); var score = reader.GetInt32(5);
            if (score >= 70) reasons.Add("same name and date of birth"); else reasons.Add("same date of birth with matching demographics");
            items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetFieldValue<DateOnly>(4).ToString("yyyy-MM-dd"),score,reasons,reader.GetString(6)));
        }
        return new(items);
    }

    public async Task<PatientDuplicateReviewItem?> SetDuplicateReviewDispositionAsync(PatientDuplicateReviewDispositionRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant(); if (status is not ("pending" or "unique" or "reviewed")) throw new ArgumentException("Status must be pending, unique, or reviewed.");
        if (request.TargetPatientId == request.SourcePatientId) throw new ArgumentException("Duplicate review records require two different patients.");
        var target = string.CompareOrdinal(request.TargetPatientId,request.SourcePatientId)<0?request.TargetPatientId:request.SourcePatientId; var source = target==request.TargetPatientId?request.SourcePatientId:request.TargetPatientId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText="insert into patient_duplicate_review_dispositions(target_patient_id,source_patient_id,status,note,updated_at) values(@target,@source,@status,@note,now()) on conflict(target_patient_id,source_patient_id) do update set status=excluded.status,note=excluded.note,updated_at=now();";
        command.Parameters.AddWithValue("target",target);command.Parameters.AddWithValue("source",source);command.Parameters.AddWithValue("status",status);command.Parameters.AddWithValue("note",(object?)request.Note?.Trim()??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken);
        return (await GetDuplicateReviewQueueAsync(200,cancellationToken)).Items.FirstOrDefault(x=>x.TargetPatientId==target&&x.SourcePatientId==source);
    }

    public async Task<PatientMergePreviewResponse?> GetMergePreviewAsync(
        string targetPatientId,
        string sourcePatientId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var target = await GetMergePreviewPatientAsync(connection, targetPatientId, cancellationToken);
        var source = await GetMergePreviewPatientAsync(connection, sourcePatientId, cancellationToken);
        if (target is null || source is null)
        {
            return null;
        }

        if (string.Equals(target.Patient.CanonicalId, source.Patient.CanonicalId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Source patient and target patient must be different records.");
        }

        var match = BuildMergeMatch(source.Patient, target.Patient);
        return new PatientMergePreviewResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PreviewOnly: true,
            TargetPatient: target.Patient,
            SourcePatient: source.Patient,
            TargetCounts: target.Counts,
            SourceCounts: source.Counts,
            CombinedCounts: CombineCounts(target.Counts, source.Counts),
            MatchScore: match.Score,
            MatchReasons: match.Reasons,
            Safeguards: new[]
            {
                "Preview only; no patient rows or clinical records are changed.",
                "Source and target must be separate patient records.",
                "Constrained merge execution requires this audited preview and blocks care-team, one-to-one, and unsupported-record conflicts."
            });
    }

    private static async Task<PatientHistorySummary?> GetHistoryForPatientAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                coffee,
                tobacco,
                alcohol,
                sleep_patterns,
                exercise_patterns,
                seatbelt_use,
                counseling,
                hazardous_activities,
                recreational_drugs,
                last_physical_exam,
                last_mammogram,
                last_prostate_exam,
                last_colonoscopy,
                last_ecg,
                last_retinal,
                last_fluvax,
                last_pneuvax,
                last_ldl,
                last_hemoglobin,
                last_psa,
                last_exam_results,
                history_mother,
                history_father,
                history_siblings,
                history_offspring,
                history_spouse,
                relatives_cancer,
                relatives_tuberculosis,
                relatives_diabetes,
                relatives_high_blood_pressure,
                relatives_heart_problems,
                relatives_stroke,
                relatives_epilepsy,
                relatives_mental_illness,
                relatives_suicide,
                appendectomy_date,
                tonsillectomy_date,
                cholecystectomy_date,
                heart_surgery_date,
                hysterectomy_date,
                hernia_repair_date,
                hip_replacement_date,
                knee_replacement_date,
                additional_history,
                exams,
                to_char(recorded_at, 'YYYY-MM-DD HH24:MI:SS') as recorded_at
            from patient_histories
            where lower(patient_id) = lower(@canonicalId);
            """;
        command.Parameters.AddWithValue("canonicalId", canonicalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PatientHistorySummary(
            Coffee: ReadNullableString(reader, "coffee"),
            Tobacco: ReadNullableString(reader, "tobacco"),
            Alcohol: ReadNullableString(reader, "alcohol"),
            SleepPatterns: ReadNullableString(reader, "sleep_patterns"),
            ExercisePatterns: ReadNullableString(reader, "exercise_patterns"),
            SeatbeltUse: ReadNullableString(reader, "seatbelt_use"),
            Counseling: ReadNullableString(reader, "counseling"),
            HazardousActivities: ReadNullableString(reader, "hazardous_activities"),
            RecreationalDrugs: ReadNullableString(reader, "recreational_drugs"),
            LastPhysicalExam: ReadNullableString(reader, "last_physical_exam"),
            LastMammogram: ReadNullableString(reader, "last_mammogram"),
            LastProstateExam: ReadNullableString(reader, "last_prostate_exam"),
            LastColonoscopy: ReadNullableString(reader, "last_colonoscopy"),
            LastEcg: ReadNullableString(reader, "last_ecg"),
            LastRetinal: ReadNullableString(reader, "last_retinal"),
            LastFluvax: ReadNullableString(reader, "last_fluvax"),
            LastPneuvax: ReadNullableString(reader, "last_pneuvax"),
            LastLdl: ReadNullableString(reader, "last_ldl"),
            LastHemoglobin: ReadNullableString(reader, "last_hemoglobin"),
            LastPsa: ReadNullableString(reader, "last_psa"),
            LastExamResults: ReadNullableString(reader, "last_exam_results"),
            HistoryMother: ReadNullableString(reader, "history_mother"),
            HistoryFather: ReadNullableString(reader, "history_father"),
            HistorySiblings: ReadNullableString(reader, "history_siblings"),
            HistoryOffspring: ReadNullableString(reader, "history_offspring"),
            HistorySpouse: ReadNullableString(reader, "history_spouse"),
            RelativesCancer: ReadNullableString(reader, "relatives_cancer"),
            RelativesTuberculosis: ReadNullableString(reader, "relatives_tuberculosis"),
            RelativesDiabetes: ReadNullableString(reader, "relatives_diabetes"),
            RelativesHighBloodPressure: ReadNullableString(reader, "relatives_high_blood_pressure"),
            RelativesHeartProblems: ReadNullableString(reader, "relatives_heart_problems"),
            RelativesStroke: ReadNullableString(reader, "relatives_stroke"),
            RelativesEpilepsy: ReadNullableString(reader, "relatives_epilepsy"),
            RelativesMentalIllness: ReadNullableString(reader, "relatives_mental_illness"),
            RelativesSuicide: ReadNullableString(reader, "relatives_suicide"),
            AppendectomyDate: ReadNullableDate(reader, "appendectomy_date"),
            TonsillectomyDate: ReadNullableDate(reader, "tonsillectomy_date"),
            CholecystectomyDate: ReadNullableDate(reader, "cholecystectomy_date"),
            HeartSurgeryDate: ReadNullableDate(reader, "heart_surgery_date"),
            HysterectomyDate: ReadNullableDate(reader, "hysterectomy_date"),
            HerniaRepairDate: ReadNullableDate(reader, "hernia_repair_date"),
            HipReplacementDate: ReadNullableDate(reader, "hip_replacement_date"),
            KneeReplacementDate: ReadNullableDate(reader, "knee_replacement_date"),
            AdditionalHistory: ReadNullableString(reader, "additional_history"),
            Exams: ReadNullableString(reader, "exams"),
            RecordedAt: ReadNullableString(reader, "recorded_at"));
    }

    private static async Task<IReadOnlyList<PatientInsuranceItem>> GetInsuranceForPatientAsync(
        NpgsqlConnection connection,
        string canonicalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                id,
                type,
                provider,
                plan_name,
                policy_number,
                group_number,
                relationship,
                subscriber_first_name,
                subscriber_middle_name,
                subscriber_last_name,
                subscriber_date_of_birth,
                subscriber_sex,
                subscriber_street,
                subscriber_street_line_2,
                subscriber_city,
                subscriber_state,
                subscriber_postal_code,
                subscriber_country,
                subscriber_phone,
                subscriber_employer,
                subscriber_employer_street,
                subscriber_employer_street_line_2,
                subscriber_employer_city,
                subscriber_employer_state,
                subscriber_employer_postal_code,
                subscriber_employer_country
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
                Relationship: ReadNullableString(reader, "relationship"),
                SubscriberFirstName: ReadNullableString(reader, "subscriber_first_name"),
                SubscriberMiddleName: ReadNullableString(reader, "subscriber_middle_name"),
                SubscriberLastName: ReadNullableString(reader, "subscriber_last_name"),
                SubscriberDateOfBirth: ReadNullableDate(reader, "subscriber_date_of_birth"),
                SubscriberSex: ReadNullableString(reader, "subscriber_sex"),
                SubscriberStreet: ReadNullableString(reader, "subscriber_street"),
                SubscriberStreetLine2: ReadNullableString(reader, "subscriber_street_line_2"),
                SubscriberCity: ReadNullableString(reader, "subscriber_city"),
                SubscriberState: ReadNullableString(reader, "subscriber_state"),
                SubscriberPostalCode: ReadNullableString(reader, "subscriber_postal_code"),
                SubscriberCountry: ReadNullableString(reader, "subscriber_country"),
                SubscriberPhone: ReadNullableString(reader, "subscriber_phone"),
                SubscriberEmployer: ReadNullableString(reader, "subscriber_employer"),
                SubscriberEmployerStreet: ReadNullableString(reader, "subscriber_employer_street"),
                SubscriberEmployerStreetLine2: ReadNullableString(reader, "subscriber_employer_street_line_2"),
                SubscriberEmployerCity: ReadNullableString(reader, "subscriber_employer_city"),
                SubscriberEmployerState: ReadNullableString(reader, "subscriber_employer_state"),
                SubscriberEmployerPostalCode: ReadNullableString(reader, "subscriber_employer_postal_code"),
                SubscriberEmployerCountry: ReadNullableString(reader, "subscriber_employer_country")));
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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? canonicalId;
        await using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = """
                select canonical_id
                from patients
                where (lower(canonical_id) = lower(@patientId)
                       or lower(pubpid) = lower(@patientId)
                       or legacy_pid::text = @patientId)
                  and (canonical_id like 'TMP-PAT-REG-%' or pubpid like 'TMP-PAT-REG-%')
                for update;
                """;
            lookup.Parameters.AddWithValue("patientId", patientId);
            canonicalId = (string?)await lookup.ExecuteScalarAsync(cancellationToken);
        }

        if (canonicalId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        // Temporary patient fixtures can participate in the merge smoke workflow.
        // Remove only their merge metadata before removing the synthetic patient.
        await using (var cleanup = connection.CreateCommand())
        {
            cleanup.Transaction = transaction;
            cleanup.CommandText = """
                delete from patient_merge_execution_manifest_rows
                where execution_id in (
                    select execution_id
                    from patient_merge_executions
                    where source_patient_id = @canonicalId or target_patient_id = @canonicalId
                );

                delete from patient_merge_executions
                where source_patient_id = @canonicalId or target_patient_id = @canonicalId;

                delete from patient_merge_audit_plans
                where source_patient_id = @canonicalId or target_patient_id = @canonicalId;

                delete from insurance_records
                where patient_id = @canonicalId
                   or pid = (select legacy_pid from patients where canonical_id = @canonicalId);

                delete from patient_lifecycle_events
                where patient_id = @canonicalId;

                delete from patient_deceased_status_events
                where patient_id = @canonicalId;

                update clinical_form_instances
                set predecessor_instance_id = null,
                    successor_instance_id = null
                where patient_id = @canonicalId;

                delete from clinical_form_signatures
                where instance_id in (
                    select instance_id from clinical_form_instances
                    where patient_id = @canonicalId
                );

                delete from clinical_form_instance_events
                where instance_id in (
                    select instance_id from clinical_form_instances
                    where patient_id = @canonicalId
                );

                delete from clinical_form_instances
                where patient_id = @canonicalId;

                delete from patients where canonical_id = @canonicalId;
                """;
            cleanup.Parameters.AddWithValue("canonicalId", canonicalId);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PatientChartSummary?> UpdateContactAsync(
        string patientId,
        PatientContactUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var prior = await ReadPatientAdministrationSnapshotAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        if (prior is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var after = BuildContactAuditValues(request);
        if (ChangedFields(prior.ContactValues, after).Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update patients
                set
                    phone = @phoneHome,
                    phone_home = @phoneHome,
                    phone_cell = @phoneCell,
                    email = @email,
                    hipaa_allow_sms = @hipaaAllowSms,
                    hipaa_allow_email = @hipaaAllowEmail
                where canonical_id = @patientId;
                """;
            command.Parameters.AddWithValue("patientId", prior.Patient.CanonicalId);
            command.Parameters.Add("phoneHome", NpgsqlDbType.Text).Value = ToDatabaseValue(after["phoneHome"]);
            command.Parameters.Add("phoneCell", NpgsqlDbType.Text).Value = ToDatabaseValue(after["phoneCell"]);
            command.Parameters.Add("email", NpgsqlDbType.Text).Value = ToDatabaseValue(after["email"]);
            command.Parameters.Add("hipaaAllowSms", NpgsqlDbType.Text).Value = ToDatabaseValue(after["hipaaAllowSms"]);
            command.Parameters.Add("hipaaAllowEmail", NpgsqlDbType.Text).Value = ToDatabaseValue(after["hipaaAllowEmail"]);
            await command.ExecuteNonQueryAsync(cancellationToken);

            await InsertPatientAdministrationAuditAsync(
                connection,
                transaction,
                prior.Patient,
                area: "contact",
                action: "updated",
                entityId: null,
                prior.ContactValues,
                after,
                username,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(prior.Patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateDemographicsAsync(
        string patientId,
        PatientDemographicsUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeDemographics(request, out var normalized))
        {
            return null;
        }

        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var prior = await ReadPatientAdministrationSnapshotAsync(
            connection,
            transaction,
            patientId,
            cancellationToken);
        if (prior is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var after = BuildDemographicsAuditValues(normalized);
        if (ChangedFields(prior.DemographicValues, after).Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
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
                where canonical_id = @patientId;
                """;
            command.Parameters.AddWithValue("patientId", prior.Patient.CanonicalId);
            AddDemographicsParameters(command, normalized);
            await command.ExecuteNonQueryAsync(cancellationToken);

            await InsertPatientAdministrationAuditAsync(
                connection,
                transaction,
                prior.Patient,
                area: "demographics",
                action: "updated",
                entityId: null,
                prior.DemographicValues,
                after,
                username,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(prior.Patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateDeceasedStatusAsync(
        string patientId,
        PatientDeceasedStatusUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeDeceasedStatus(request, out var deceasedDate, out var deceasedReason))
        {
            throw new ArgumentException("The deceased date must use YYYY-MM-DD and cannot be in the future.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var patientCommand = connection.CreateCommand();
        patientCommand.Transaction = transaction;
        patientCommand.CommandText = """
            select canonical_id, legacy_pid, deceased_date, deceased_reason
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1
            for update;
            """;
        patientCommand.Parameters.AddWithValue("patientId", patientId);

        string canonicalId;
        int legacyPid;
        DateOnly? priorDate;
        string? priorReason;
        await using (var reader = await patientCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            canonicalId = reader.GetString(reader.GetOrdinal("canonical_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("legacy_pid"));
            priorDate = reader.IsDBNull(reader.GetOrdinal("deceased_date"))
                ? null
                : reader.GetFieldValue<DateOnly>(reader.GetOrdinal("deceased_date"));
            priorReason = ReadNullableString(reader, "deceased_reason");
        }

        if (priorDate == deceasedDate && string.Equals(priorReason, deceasedReason, StringComparison.Ordinal))
        {
            throw new ArgumentException("The deceased status does not change the current patient record.");
        }

        var correctionReason = NormalizeString(request.CorrectionReason);
        if (correctionReason is null || correctionReason.Length > 500)
        {
            throw new ArgumentException("A deceased-status correction reason of 1 to 500 characters is required.");
        }

        var action = priorDate is null
            ? "recorded"
            : deceasedDate is null
                ? "cleared"
                : "corrected";
        var actor = NormalizeString(username) ?? "unknown";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update patients
            set
                deceased_date = @deceasedDate,
                deceased_reason = @deceasedReason
            where canonical_id = @patientId;
            """;
        command.Parameters.AddWithValue("patientId", canonicalId);
        command.Parameters.Add("deceasedDate", NpgsqlDbType.Date).Value = deceasedDate is null
            ? DBNull.Value
            : deceasedDate.Value;
        command.Parameters.Add("deceasedReason", NpgsqlDbType.Text).Value = deceasedReason is null
            ? DBNull.Value
            : deceasedReason;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = """
            insert into patient_deceased_status_events (
                event_id, patient_id, legacy_pid, action,
                prior_deceased_date, prior_deceased_reason,
                resulting_deceased_date, resulting_deceased_reason,
                correction_reason, actor, occurred_at)
            values (
                @eventId, @patientId, @legacyPid, @action,
                @priorDate, @priorReason,
                @resultingDate, @resultingReason,
                @correctionReason, @actor, now());
            """;
        eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
        eventCommand.Parameters.AddWithValue("patientId", canonicalId);
        eventCommand.Parameters.AddWithValue("legacyPid", legacyPid);
        eventCommand.Parameters.AddWithValue("action", action);
        eventCommand.Parameters.Add("priorDate", NpgsqlDbType.Date).Value = priorDate is null ? DBNull.Value : priorDate.Value;
        eventCommand.Parameters.Add("priorReason", NpgsqlDbType.Text).Value = priorReason is null ? DBNull.Value : priorReason;
        eventCommand.Parameters.Add("resultingDate", NpgsqlDbType.Date).Value = deceasedDate is null ? DBNull.Value : deceasedDate.Value;
        eventCommand.Parameters.Add("resultingReason", NpgsqlDbType.Text).Value = deceasedReason is null ? DBNull.Value : deceasedReason;
        eventCommand.Parameters.AddWithValue("correctionReason", correctionReason);
        eventCommand.Parameters.AddWithValue("actor", actor);
        await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientDeceasedStatusHistoryResponse?> GetDeceasedStatusHistoryAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = """
            select canonical_id, legacy_pid, deceased_date, deceased_reason
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patientId", patientId);

        string canonicalId;
        int legacyPid;
        string? currentDate;
        string? currentReason;
        await using (var reader = await patientCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            canonicalId = reader.GetString(reader.GetOrdinal("canonical_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("legacy_pid"));
            currentDate = ReadNullableDate(reader, "deceased_date");
            currentReason = ReadNullableString(reader, "deceased_reason");
        }

        await using var eventCommand = connection.CreateCommand();
        eventCommand.CommandText = """
            select event_id, action,
                prior_deceased_date, prior_deceased_reason,
                resulting_deceased_date, resulting_deceased_reason,
                correction_reason, actor, occurred_at
            from patient_deceased_status_events
            where patient_id = @patientId
            order by occurred_at desc, event_id desc;
            """;
        eventCommand.Parameters.AddWithValue("patientId", canonicalId);
        var events = new List<PatientDeceasedStatusHistoryItem>();
        await using var eventReader = await eventCommand.ExecuteReaderAsync(cancellationToken);
        while (await eventReader.ReadAsync(cancellationToken))
        {
            events.Add(new PatientDeceasedStatusHistoryItem(
                eventReader.GetGuid(eventReader.GetOrdinal("event_id")),
                eventReader.GetString(eventReader.GetOrdinal("action")),
                ReadNullableDate(eventReader, "prior_deceased_date"),
                ReadNullableString(eventReader, "prior_deceased_reason"),
                ReadNullableDate(eventReader, "resulting_deceased_date"),
                ReadNullableString(eventReader, "resulting_deceased_reason"),
                eventReader.GetString(eventReader.GetOrdinal("correction_reason")),
                eventReader.GetString(eventReader.GetOrdinal("actor")),
                eventReader.GetFieldValue<DateTimeOffset>(eventReader.GetOrdinal("occurred_at")).ToString("O")));
        }

        return new PatientDeceasedStatusHistoryResponse(
            metadata.DatasetId, metadata.DatasetVersion, canonicalId, legacyPid,
            currentDate, currentReason, events.Count, events);
    }

    public async Task<PatientLifecycleHistoryResponse?> GetLifecycleHistoryAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var patientCommand = connection.CreateCommand();
        patientCommand.CommandText = """
            select canonical_id, legacy_pid, lifecycle_status, retired_at, retired_by, retirement_reason
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        patientCommand.Parameters.AddWithValue("patientId", patientId);

        string canonicalId;
        int legacyPid;
        string currentStatus;
        string? retiredAt;
        string? retiredBy;
        string? retirementReason;
        await using (var reader = await patientCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            canonicalId = reader.GetString(reader.GetOrdinal("canonical_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("legacy_pid"));
            currentStatus = ReadNullableString(reader, "lifecycle_status") ?? "active";
            retiredAt = ReadNullableTimestamp(reader, "retired_at");
            retiredBy = ReadNullableString(reader, "retired_by");
            retirementReason = ReadNullableString(reader, "retirement_reason");
        }

        await using var eventCommand = connection.CreateCommand();
        eventCommand.CommandText = """
            select event_id, action, prior_status, resulting_status, reason, actor, occurred_at
            from patient_lifecycle_events
            where patient_id = @patientId
            order by occurred_at desc, event_id desc;
            """;
        eventCommand.Parameters.AddWithValue("patientId", canonicalId);
        var events = new List<PatientLifecycleHistoryItem>();
        await using var eventReader = await eventCommand.ExecuteReaderAsync(cancellationToken);
        while (await eventReader.ReadAsync(cancellationToken))
        {
            events.Add(new PatientLifecycleHistoryItem(
                EventId: eventReader.GetGuid(eventReader.GetOrdinal("event_id")),
                Action: eventReader.GetString(eventReader.GetOrdinal("action")),
                PriorStatus: eventReader.GetString(eventReader.GetOrdinal("prior_status")),
                ResultingStatus: eventReader.GetString(eventReader.GetOrdinal("resulting_status")),
                Reason: eventReader.GetString(eventReader.GetOrdinal("reason")),
                Actor: eventReader.GetString(eventReader.GetOrdinal("actor")),
                OccurredAt: eventReader.GetFieldValue<DateTimeOffset>(eventReader.GetOrdinal("occurred_at")).ToString("O")));
        }

        return new PatientLifecycleHistoryResponse(
            metadata.DatasetId,
            metadata.DatasetVersion,
            canonicalId,
            legacyPid,
            currentStatus,
            retiredAt,
            retiredBy,
            retirementReason,
            events.Count,
            events);
    }

    public async Task<PatientChartSummary?> TransitionLifecycleAsync(
        string patientId,
        string action,
        PatientLifecycleTransitionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var normalizedAction = NormalizeString(action)?.ToLowerInvariant();
        if (normalizedAction is not ("retire" or "reactivate"))
        {
            throw new ArgumentException("The patient lifecycle action must be retire or reactivate.");
        }

        var reason = NormalizeString(request.Reason);
        if (reason is null || reason.Length > 500)
        {
            throw new ArgumentException("A patient lifecycle reason of 1 to 500 characters is required.");
        }

        var actor = NormalizeString(username) ?? "unknown";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var patientCommand = connection.CreateCommand();
        patientCommand.Transaction = transaction;
        patientCommand.CommandText = """
            select canonical_id, legacy_pid, lifecycle_status, deceased_date, merged_into_patient_id
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1
            for update;
            """;
        patientCommand.Parameters.AddWithValue("patientId", patientId);

        string canonicalId;
        int legacyPid;
        string priorStatus;
        DateOnly? deceasedDate;
        string? mergedIntoPatientId;
        await using (var reader = await patientCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            canonicalId = reader.GetString(reader.GetOrdinal("canonical_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("legacy_pid"));
            priorStatus = ReadNullableString(reader, "lifecycle_status") ?? "active";
            deceasedDate = reader.IsDBNull(reader.GetOrdinal("deceased_date"))
                ? null
                : reader.GetFieldValue<DateOnly>(reader.GetOrdinal("deceased_date"));
            mergedIntoPatientId = ReadNullableString(reader, "merged_into_patient_id");
        }

        if (mergedIntoPatientId is not null)
        {
            throw new ArgumentException("A merged patient cannot receive an independent lifecycle transition.");
        }

        var resultingStatus = normalizedAction == "retire" ? "retired" : "active";
        if (!string.Equals(priorStatus, normalizedAction == "retire" ? "active" : "retired", StringComparison.Ordinal))
        {
            throw new ArgumentException($"The patient is already {priorStatus} and cannot be {normalizedAction}d.");
        }

        if (normalizedAction == "reactivate" && deceasedDate is not null)
        {
            throw new ArgumentException("A deceased patient cannot be reactivated until the deceased status is corrected.");
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update patients
            set lifecycle_status = @resultingStatus,
                retired_at = case when @resultingStatus = 'retired' then now() else null end,
                retired_by = case when @resultingStatus = 'retired' then @actor else null end,
                retirement_reason = case when @resultingStatus = 'retired' then @reason else null end
            where canonical_id = @patientId;
            """;
        updateCommand.Parameters.AddWithValue("resultingStatus", resultingStatus);
        updateCommand.Parameters.AddWithValue("actor", actor);
        updateCommand.Parameters.AddWithValue("reason", reason);
        updateCommand.Parameters.AddWithValue("patientId", canonicalId);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText = """
            insert into patient_lifecycle_events (
                event_id, patient_id, legacy_pid, action, prior_status, resulting_status, reason, actor, occurred_at)
            values (@eventId, @patientId, @legacyPid, @action, @priorStatus, @resultingStatus, @reason, @actor, now());
            """;
        eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
        eventCommand.Parameters.AddWithValue("patientId", canonicalId);
        eventCommand.Parameters.AddWithValue("legacyPid", legacyPid);
        eventCommand.Parameters.AddWithValue("action", normalizedAction == "retire" ? "retired" : "reactivated");
        eventCommand.Parameters.AddWithValue("priorStatus", priorStatus);
        eventCommand.Parameters.AddWithValue("resultingStatus", resultingStatus);
        eventCommand.Parameters.AddWithValue("reason", reason);
        eventCommand.Parameters.AddWithValue("actor", actor);
        await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdatePortalAccountResetAsync(
        string patientId,
        PatientPortalAccountResetRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with matched_patient as (
                select canonical_id
                from patients
                where lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId
                limit 1
            )
            update patient_portal_accounts
            set
                one_time_token = case
                    when @oneTimeLinkPending then concat('reset-', lower(patient_id))
                    else null
                end,
                password_status = case
                    when @oneTimeLinkPending then 0
                    else 1
                end
            where patient_id in (select canonical_id from matched_patient)
            returning patient_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("oneTimeLinkPending", request.OneTimeLinkPending);

        var canonicalId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return canonicalId is null ? null : await GetChartSummaryAsync(canonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdatePortalAccountAccessAsync(
        string patientId,
        PatientPortalAccountAccessRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update patients
            set portal_enabled = @portalEnabled
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            returning canonical_id;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("portalEnabled", request.PortalEnabled);

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
        string username,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (reason?.Length > 250)
        {
            return null;
        }

        await EnsureProviderAssignmentEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        ProviderAssignmentPatient? patient;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select
                    p.canonical_id,
                    p.legacy_pid,
                    p.provider_id,
                    nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                    s.facility_id,
                    f.name as facility_name
                from patients p
                left join staff s on s.id = p.provider_id
                left join facilities f on f.id = s.facility_id
                where lower(p.canonical_id) = lower(@patientId)
                   or lower(p.pubpid) = lower(@patientId)
                   or p.legacy_pid::text = @patientId
                limit 1
                for update of p;
                """;
            command.Parameters.AddWithValue("patientId", patientId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            patient = await reader.ReadAsync(cancellationToken)
                ? new ProviderAssignmentPatient(
                    CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    Provider: new ProviderAssignmentSnapshot(
                        ProviderId: ReadNullableInt(reader, "provider_id"),
                        ProviderName: ReadNullableString(reader, "provider_name"),
                        FacilityId: ReadNullableInt(reader, "facility_id"),
                        FacilityName: ReadNullableString(reader, "facility_name")))
                : null;
        }

        if (patient is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        ProviderAssignmentSnapshot targetProvider;
        if (request.ProviderId is null)
        {
            targetProvider = new ProviderAssignmentSnapshot(null, null, null, null);
        }
        else
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select
                    s.id as provider_id,
                    nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                    s.facility_id,
                    f.name as facility_name
                from staff s
                left join facilities f on f.id = s.facility_id
                where s.id = @providerId
                  and s.active = true
                  and lower(s.role) = 'provider';
                """;
            command.Parameters.AddWithValue("providerId", request.ProviderId.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            targetProvider = new ProviderAssignmentSnapshot(
                ProviderId: reader.GetInt32(reader.GetOrdinal("provider_id")),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"));
        }

        if (patient.Provider.ProviderId == targetProvider.ProviderId)
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patients
                set provider_id = @providerId
                where canonical_id = @patientId;
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value =
                targetProvider.ProviderId is null ? DBNull.Value : targetProvider.ProviderId.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_provider_assignment_events (
                    event_id,
                    patient_id,
                    legacy_pid,
                    from_provider_id,
                    from_provider_name,
                    from_facility_id,
                    from_facility_name,
                    to_provider_id,
                    to_provider_name,
                    to_facility_id,
                    to_facility_name,
                    reason,
                    actor,
                    occurred_at
                )
                values (
                    @eventId,
                    @patientId,
                    @legacyPid,
                    @fromProviderId,
                    @fromProviderName,
                    @fromFacilityId,
                    @fromFacilityName,
                    @toProviderId,
                    @toProviderName,
                    @toFacilityId,
                    @toFacilityName,
                    @reason,
                    @actor,
                    now()
                );
                """;
            command.Parameters.AddWithValue("eventId", Guid.NewGuid());
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("legacyPid", patient.LegacyPid);
            AddNullableProviderAssignmentParameters(command, "from", patient.Provider);
            AddNullableProviderAssignmentParameters(command, "to", targetProvider);
            command.Parameters.AddWithValue(
                "reason",
                string.IsNullOrWhiteSpace(reason) ? "Primary provider assignment updated." : reason);
            command.Parameters.AddWithValue(
                "actor",
                string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientProviderAssignmentHistoryResponse?> GetProviderAssignmentHistoryAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        await EnsureProviderAssignmentEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        ProviderAssignmentPatient? patient;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    p.canonical_id,
                    p.legacy_pid,
                    p.provider_id,
                    nullif(trim(concat(s.first_name, ' ', s.last_name)), '') as provider_name,
                    s.facility_id,
                    f.name as facility_name
                from patients p
                left join staff s on s.id = p.provider_id
                left join facilities f on f.id = s.facility_id
                where lower(p.canonical_id) = lower(@patientId)
                   or lower(p.pubpid) = lower(@patientId)
                   or p.legacy_pid::text = @patientId
                limit 1;
                """;
            command.Parameters.AddWithValue("patientId", patientId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            patient = await reader.ReadAsync(cancellationToken)
                ? new ProviderAssignmentPatient(
                    CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    Provider: new ProviderAssignmentSnapshot(
                        ProviderId: ReadNullableInt(reader, "provider_id"),
                        ProviderName: ReadNullableString(reader, "provider_name"),
                        FacilityId: ReadNullableInt(reader, "facility_id"),
                        FacilityName: ReadNullableString(reader, "facility_name")))
                : null;
        }

        if (patient is null)
        {
            return null;
        }

        const int resultLimit = 100;
        var events = new List<PatientProviderAssignmentHistoryItem>();
        var eventCount = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    count(*) over()::int as event_count,
                    event_id,
                    from_provider_id,
                    from_provider_name,
                    from_facility_id,
                    from_facility_name,
                    to_provider_id,
                    to_provider_name,
                    to_facility_id,
                    to_facility_name,
                    reason,
                    actor,
                    occurred_at
                from patient_provider_assignment_events
                where patient_id = @patientId
                order by occurred_at desc, event_id desc
                limit @resultLimit;
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("resultLimit", resultLimit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
                events.Add(new PatientProviderAssignmentHistoryItem(
                    EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                    FromProviderId: ReadNullableInt(reader, "from_provider_id"),
                    FromProviderName: ReadNullableString(reader, "from_provider_name"),
                    FromFacilityId: ReadNullableInt(reader, "from_facility_id"),
                    FromFacilityName: ReadNullableString(reader, "from_facility_name"),
                    ToProviderId: ReadNullableInt(reader, "to_provider_id"),
                    ToProviderName: ReadNullableString(reader, "to_provider_name"),
                    ToFacilityId: ReadNullableInt(reader, "to_facility_id"),
                    ToFacilityName: ReadNullableString(reader, "to_facility_name"),
                    Reason: reader.GetString(reader.GetOrdinal("reason")),
                    Actor: reader.GetString(reader.GetOrdinal("actor")),
                    OccurredAt: reader.GetFieldValue<DateTime>(reader.GetOrdinal("occurred_at"))
                        .ToUniversalTime()
                        .ToString("O", CultureInfo.InvariantCulture)));
            }
        }

        return new PatientProviderAssignmentHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.CanonicalId,
            LegacyPid: patient.LegacyPid,
            CurrentProviderId: patient.Provider.ProviderId,
            CurrentProviderName: patient.Provider.ProviderName,
            CurrentFacilityId: patient.Provider.FacilityId,
            CurrentFacilityName: patient.Provider.FacilityName,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: resultLimit,
            Events: events);
    }

    public async Task<PatientAdministrationHistoryResponse?> GetAdministrationHistoryAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        const int resultLimit = 100;
        var metadata = await GetMetadataAsync(cancellationToken);
        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientIdentityAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var eventCount = 0;
        var events = new List<PatientAdministrationHistoryItem>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                count(*) over()::int as event_count,
                event_id,
                area,
                action,
                entity_id,
                changed_fields,
                before_values,
                after_values,
                actor,
                occurred_at
            from patient_administration_audit_events
            where patient_id = @patientId
            order by occurred_at desc, event_id desc
            limit @resultLimit;
            """;
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        command.Parameters.AddWithValue("resultLimit", resultLimit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            eventCount = reader.GetInt32(reader.GetOrdinal("event_count"));
            events.Add(new PatientAdministrationHistoryItem(
                EventId: reader.GetGuid(reader.GetOrdinal("event_id")),
                Area: reader.GetString(reader.GetOrdinal("area")),
                Action: reader.GetString(reader.GetOrdinal("action")),
                EntityId: ReadNullableString(reader, "entity_id"),
                ChangedFields: reader.GetFieldValue<string[]>(reader.GetOrdinal("changed_fields")),
                BeforeValues: ReadAuditValues(reader, "before_values"),
                AfterValues: ReadAuditValues(reader, "after_values"),
                Actor: reader.GetString(reader.GetOrdinal("actor")),
                OccurredAt: reader.GetFieldValue<DateTime>(reader.GetOrdinal("occurred_at"))
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture)));
        }

        return new PatientAdministrationHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.CanonicalId,
            LegacyPid: patient.LegacyPid,
            EventCount: eventCount,
            ReturnedCount: events.Count,
            ResultLimit: resultLimit,
            Events: events);
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
            var providerMemberInvalid = member.UserId is not null
                && (!await CareTeamUserExistsAsync(connection, transaction, member.UserId.Value, cancellationToken)
                    || (member.FacilityId is not null
                        && !await CareTeamFacilityExistsAsync(connection, transaction, member.FacilityId.Value, cancellationToken)));
            var contactMemberInvalid = member.ContactId is not null
                && !await CareTeamContactExistsAsync(
                    connection,
                    transaction,
                    patient.CanonicalId,
                    member.ContactId.Value,
                    cancellationToken);

            if (providerMemberInvalid || contactMemberInvalid)
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
                    contact_id,
                    role,
                    facility_id,
                    provider_since,
                    status,
                    note
                )
                values (
                    @patientId,
                    @userId,
                    @contactId,
                    @role,
                    @facilityId,
                    @providerSince,
                    @status,
                    @note
                );
                """;
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = member.UserId is null
                ? DBNull.Value
                : member.UserId.Value;
            command.Parameters.Add("contactId", NpgsqlDbType.Bigint).Value = member.ContactId is null
                ? DBNull.Value
                : member.ContactId.Value;
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
        string username,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeInsurance(request, out var normalized))
        {
            return null;
        }

        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientIdentityAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var insuranceId = $"INS-PARITY-{Guid.NewGuid():N}";
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into insurance_records
                    (
                        id,
                        patient_id,
                        pid,
                        type,
                        provider,
                        plan_name,
                        policy_number,
                        group_number,
                        relationship,
                        subscriber_first_name,
                        subscriber_middle_name,
                        subscriber_last_name,
                        subscriber_date_of_birth,
                        subscriber_sex,
                        subscriber_street,
                        subscriber_street_line_2,
                        subscriber_city,
                        subscriber_state,
                        subscriber_postal_code,
                        subscriber_country,
                        subscriber_phone,
                        subscriber_employer,
                        subscriber_employer_street,
                        subscriber_employer_street_line_2,
                        subscriber_employer_city,
                        subscriber_employer_state,
                        subscriber_employer_postal_code,
                        subscriber_employer_country
                    )
                values
                    (
                        @id,
                        @patientId,
                        @pid,
                        @type,
                        @provider,
                        @planName,
                        @policyNumber,
                        @groupNumber,
                        @relationship,
                        @subscriberFirstName,
                        @subscriberMiddleName,
                        @subscriberLastName,
                        @subscriberDateOfBirth,
                        @subscriberSex,
                        @subscriberStreet,
                        @subscriberStreetLine2,
                        @subscriberCity,
                        @subscriberState,
                        @subscriberPostalCode,
                        @subscriberCountry,
                        @subscriberPhone,
                        @subscriberEmployer,
                        @subscriberEmployerStreet,
                        @subscriberEmployerStreetLine2,
                        @subscriberEmployerCity,
                        @subscriberEmployerState,
                        @subscriberEmployerPostalCode,
                        @subscriberEmployerCountry
                    );
                """;
            command.Parameters.AddWithValue("id", insuranceId);
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("pid", patient.LegacyPid);
            AddInsuranceParameters(command, normalized);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertPatientAdministrationAuditAsync(
            connection,
            transaction,
            patient,
            area: "insurance",
            action: "created",
            entityId: insuranceId,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            BuildInsuranceAuditValues(normalized),
            username,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> UpdateInsuranceAsync(
        string insuranceId,
        PatientInsuranceMutationRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(insuranceId) || !TryNormalizeInsurance(request, out var normalized))
        {
            return null;
        }

        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var prior = await ReadInsuranceAuditSnapshotAsync(
            connection,
            transaction,
            insuranceId,
            cancellationToken);
        if (prior is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var after = BuildInsuranceAuditValues(normalized);
        if (ChangedFields(prior.Values, after).Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update insurance_records
                set
                    type = @type,
                    provider = @provider,
                    plan_name = @planName,
                    policy_number = @policyNumber,
                    group_number = @groupNumber,
                    relationship = @relationship,
                    subscriber_first_name = @subscriberFirstName,
                    subscriber_middle_name = @subscriberMiddleName,
                    subscriber_last_name = @subscriberLastName,
                    subscriber_date_of_birth = @subscriberDateOfBirth,
                    subscriber_sex = @subscriberSex,
                    subscriber_street = @subscriberStreet,
                    subscriber_street_line_2 = @subscriberStreetLine2,
                    subscriber_city = @subscriberCity,
                    subscriber_state = @subscriberState,
                    subscriber_postal_code = @subscriberPostalCode,
                    subscriber_country = @subscriberCountry,
                    subscriber_phone = @subscriberPhone,
                    subscriber_employer = @subscriberEmployer,
                    subscriber_employer_street = @subscriberEmployerStreet,
                    subscriber_employer_street_line_2 = @subscriberEmployerStreetLine2,
                    subscriber_employer_city = @subscriberEmployerCity,
                    subscriber_employer_state = @subscriberEmployerState,
                    subscriber_employer_postal_code = @subscriberEmployerPostalCode,
                    subscriber_employer_country = @subscriberEmployerCountry
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", insuranceId);
            AddInsuranceParameters(command, normalized);
            await command.ExecuteNonQueryAsync(cancellationToken);

            await InsertPatientAdministrationAuditAsync(
                connection,
                transaction,
                prior.Patient,
                area: "insurance",
                action: "updated",
                entityId: insuranceId,
                prior.Values,
                after,
                username,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(prior.Patient.CanonicalId, cancellationToken);
    }

    public async Task<PatientChartSummary?> DeleteInsuranceAsync(
        string insuranceId,
        string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(insuranceId))
        {
            return null;
        }

        await EnsurePatientAdministrationAuditEventsAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var prior = await ReadInsuranceAuditSnapshotAsync(
            connection,
            transaction,
            insuranceId,
            cancellationToken);
        if (prior is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "delete from insurance_records where id = @id;";
            command.Parameters.AddWithValue("id", insuranceId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertPatientAdministrationAuditAsync(
            connection,
            transaction,
            prior.Patient,
            area: "insurance",
            action: "deleted",
            entityId: insuranceId,
            prior.Values,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            username,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetChartSummaryAsync(prior.Patient.CanonicalId, cancellationToken);
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await EnsureMergeColumnsAsync(cancellationToken);
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

    private async Task EnsureMergeColumnsAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref mergeColumnsInitialized) == 1)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            alter table patients add column if not exists merged_into_patient_id text references patients(canonical_id);
            alter table patients add column if not exists merged_at timestamptz;
            alter table patients add column if not exists merged_by text;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        Volatile.Write(ref mergeColumnsInitialized, 1);
    }

    private async Task EnsureProviderAssignmentEventsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists patient_provider_assignment_events (
                event_id uuid primary key,
                patient_id text not null,
                legacy_pid integer not null,
                from_provider_id integer,
                from_provider_name text,
                from_facility_id integer,
                from_facility_name text,
                to_provider_id integer,
                to_provider_name text,
                to_facility_id integer,
                to_facility_name text,
                reason varchar(250) not null,
                actor text not null,
                occurred_at timestamptz not null default now()
            );

            create index if not exists ix_patient_provider_assignment_events_patient_time
                on patient_provider_assignment_events (patient_id, occurred_at desc, event_id desc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsurePatientAdministrationAuditEventsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists patient_administration_audit_events (
                event_id uuid primary key,
                patient_id text not null,
                legacy_pid integer not null,
                area varchar(24) not null,
                action varchar(24) not null,
                entity_id text,
                changed_fields text[] not null,
                before_values jsonb not null,
                after_values jsonb not null,
                actor text not null,
                occurred_at timestamptz not null default now()
            );

            create index if not exists ix_patient_administration_audit_events_patient_time
                on patient_administration_audit_events (patient_id, occurred_at desc, event_id desc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<PatientAdministrationSnapshot?> ReadPatientAdministrationSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                canonical_id,
                legacy_pid,
                first_name,
                last_name,
                preferred_name,
                sex,
                date_of_birth,
                street,
                city,
                state,
                postal_code,
                marital_status,
                occupation,
                race,
                ethnicity,
                interpreter,
                family_size,
                monthly_income,
                homeless,
                financial_review_date,
                coalesce(phone_home, phone) as phone_home,
                phone_cell,
                email,
                hipaa_allow_sms,
                hipaa_allow_email
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1
            for update;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var patient = new PatientIdentity(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")));
        var demographics = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["firstName"] = ReadNullableString(reader, "first_name"),
            ["lastName"] = ReadNullableString(reader, "last_name"),
            ["preferredName"] = ReadNullableString(reader, "preferred_name"),
            ["sex"] = ReadNullableString(reader, "sex"),
            ["dateOfBirth"] = ReadNullableDate(reader, "date_of_birth"),
            ["street"] = ReadNullableString(reader, "street"),
            ["city"] = ReadNullableString(reader, "city"),
            ["state"] = ReadNullableString(reader, "state"),
            ["postalCode"] = ReadNullableString(reader, "postal_code"),
            ["maritalStatus"] = ReadNullableString(reader, "marital_status"),
            ["occupation"] = ReadNullableString(reader, "occupation"),
            ["race"] = ReadNullableString(reader, "race"),
            ["ethnicity"] = ReadNullableString(reader, "ethnicity"),
            ["interpreter"] = ReadNullableString(reader, "interpreter"),
            ["familySize"] = ReadNullableIntAsString(reader, "family_size"),
            ["monthlyIncome"] = ReadNullableIntAsString(reader, "monthly_income"),
            ["homeless"] = ReadNullableString(reader, "homeless"),
            ["financialReviewDate"] = ReadNullableDate(reader, "financial_review_date")
        };
        var contact = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["phoneHome"] = ReadNullableString(reader, "phone_home"),
            ["phoneCell"] = ReadNullableString(reader, "phone_cell"),
            ["email"] = ReadNullableString(reader, "email"),
            ["hipaaAllowSms"] = ReadNullableString(reader, "hipaa_allow_sms")?.ToUpperInvariant(),
            ["hipaaAllowEmail"] = ReadNullableString(reader, "hipaa_allow_email")?.ToUpperInvariant()
        };
        return new PatientAdministrationSnapshot(patient, demographics, contact);
    }

    private static async Task<InsuranceAuditSnapshot?> ReadInsuranceAuditSnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string insuranceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
                patient_id,
                pid,
                type,
                provider,
                plan_name,
                policy_number,
                group_number,
                relationship,
                subscriber_first_name,
                subscriber_middle_name,
                subscriber_last_name,
                subscriber_date_of_birth,
                subscriber_sex,
                subscriber_street,
                subscriber_street_line_2,
                subscriber_city,
                subscriber_state,
                subscriber_postal_code,
                subscriber_country,
                subscriber_phone,
                subscriber_employer,
                subscriber_employer_street,
                subscriber_employer_street_line_2,
                subscriber_employer_city,
                subscriber_employer_state,
                subscriber_employer_postal_code,
                subscriber_employer_country
            from insurance_records
            where id = @insuranceId
            for update;
            """;
        command.Parameters.AddWithValue("insuranceId", insuranceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var patient = new PatientIdentity(
            CanonicalId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")));
        return new InsuranceAuditSnapshot(
            patient,
            ReadInsuranceAuditValues(reader));
    }

    private static Dictionary<string, string?> ReadInsuranceAuditValues(DbDataReader reader) => new(StringComparer.Ordinal)
    {
        ["type"] = ReadNullableString(reader, "type"),
        ["provider"] = ReadNullableString(reader, "provider"),
        ["planName"] = ReadNullableString(reader, "plan_name"),
        ["policyNumber"] = ReadNullableString(reader, "policy_number"),
        ["groupNumber"] = ReadNullableString(reader, "group_number"),
        ["relationship"] = ReadNullableString(reader, "relationship"),
        ["subscriberFirstName"] = ReadNullableString(reader, "subscriber_first_name"),
        ["subscriberMiddleName"] = ReadNullableString(reader, "subscriber_middle_name"),
        ["subscriberLastName"] = ReadNullableString(reader, "subscriber_last_name"),
        ["subscriberDateOfBirth"] = ReadNullableDate(reader, "subscriber_date_of_birth"),
        ["subscriberSex"] = ReadNullableString(reader, "subscriber_sex"),
        ["subscriberStreet"] = ReadNullableString(reader, "subscriber_street"),
        ["subscriberStreetLine2"] = ReadNullableString(reader, "subscriber_street_line_2"),
        ["subscriberCity"] = ReadNullableString(reader, "subscriber_city"),
        ["subscriberState"] = ReadNullableString(reader, "subscriber_state"),
        ["subscriberPostalCode"] = ReadNullableString(reader, "subscriber_postal_code"),
        ["subscriberCountry"] = ReadNullableString(reader, "subscriber_country"),
        ["subscriberPhone"] = ReadNullableString(reader, "subscriber_phone"),
        ["subscriberEmployer"] = ReadNullableString(reader, "subscriber_employer"),
        ["subscriberEmployerStreet"] = ReadNullableString(reader, "subscriber_employer_street"),
        ["subscriberEmployerStreetLine2"] = ReadNullableString(reader, "subscriber_employer_street_line_2"),
        ["subscriberEmployerCity"] = ReadNullableString(reader, "subscriber_employer_city"),
        ["subscriberEmployerState"] = ReadNullableString(reader, "subscriber_employer_state"),
        ["subscriberEmployerPostalCode"] = ReadNullableString(reader, "subscriber_employer_postal_code"),
        ["subscriberEmployerCountry"] = ReadNullableString(reader, "subscriber_employer_country")
    };

    private static async Task<bool> InsertPatientAdministrationAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PatientIdentity patient,
        string area,
        string action,
        string? entityId,
        IReadOnlyDictionary<string, string?> beforeValues,
        IReadOnlyDictionary<string, string?> afterValues,
        string username,
        CancellationToken cancellationToken)
    {
        var changedFields = ChangedFields(beforeValues, afterValues);
        if (changedFields.Count == 0)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_administration_audit_events (
                event_id,
                patient_id,
                legacy_pid,
                area,
                action,
                entity_id,
                changed_fields,
                before_values,
                after_values,
                actor,
                occurred_at
            )
            values (
                @eventId,
                @patientId,
                @legacyPid,
                @area,
                @action,
                @entityId,
                @changedFields,
                @beforeValues,
                @afterValues,
                @actor,
                now()
            );
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        command.Parameters.AddWithValue("legacyPid", patient.LegacyPid);
        command.Parameters.AddWithValue("area", area);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("entityId", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(entityId) ? DBNull.Value : entityId;
        command.Parameters.Add("changedFields", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            changedFields.ToArray();
        command.Parameters.Add("beforeValues", NpgsqlDbType.Jsonb).Value =
            JsonSerializer.Serialize(beforeValues);
        command.Parameters.Add("afterValues", NpgsqlDbType.Jsonb).Value =
            JsonSerializer.Serialize(afterValues);
        command.Parameters.AddWithValue(
            "actor",
            string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static IReadOnlyList<string> ChangedFields(
        IReadOnlyDictionary<string, string?> beforeValues,
        IReadOnlyDictionary<string, string?> afterValues) =>
        beforeValues.Keys
            .Concat(afterValues.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(key =>
            {
                beforeValues.TryGetValue(key, out var before);
                afterValues.TryGetValue(key, out var after);
                return !string.Equals(before, after, StringComparison.Ordinal);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static Dictionary<string, string?> BuildContactAuditValues(
        PatientContactUpdateRequest request) => new(StringComparer.Ordinal)
        {
            ["phoneHome"] = NormalizeString(request.PhoneHome),
            ["phoneCell"] = NormalizeString(request.PhoneCell),
            ["email"] = NormalizeString(request.Email),
            ["hipaaAllowSms"] = NormalizeString(request.HipaaAllowSms)?.ToUpperInvariant(),
            ["hipaaAllowEmail"] = NormalizeString(request.HipaaAllowEmail)?.ToUpperInvariant()
        };

    private static Dictionary<string, string?> BuildDemographicsAuditValues(
        NormalizedPatientDemographics normalized) => new(StringComparer.Ordinal)
        {
            ["firstName"] = normalized.FirstName,
            ["lastName"] = normalized.LastName,
            ["preferredName"] = normalized.PreferredName,
            ["sex"] = normalized.Sex,
            ["dateOfBirth"] = normalized.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["street"] = normalized.Street,
            ["city"] = normalized.City,
            ["state"] = normalized.State,
            ["postalCode"] = normalized.PostalCode,
            ["maritalStatus"] = normalized.MaritalStatus,
            ["occupation"] = normalized.Occupation,
            ["race"] = normalized.Race,
            ["ethnicity"] = normalized.Ethnicity,
            ["interpreter"] = normalized.Interpreter,
            ["familySize"] = normalized.FamilySize?.ToString(CultureInfo.InvariantCulture),
            ["monthlyIncome"] = normalized.MonthlyIncome?.ToString(CultureInfo.InvariantCulture),
            ["homeless"] = normalized.Homeless,
            ["financialReviewDate"] = normalized.FinancialReviewDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

    private static Dictionary<string, string?> BuildInsuranceAuditValues(
        NormalizedInsurance normalized) => new(StringComparer.Ordinal)
        {
            ["type"] = normalized.Type,
            ["provider"] = normalized.Provider,
            ["planName"] = normalized.PlanName,
            ["policyNumber"] = normalized.PolicyNumber,
            ["groupNumber"] = normalized.GroupNumber,
            ["relationship"] = normalized.Relationship,
            ["subscriberFirstName"] = normalized.SubscriberFirstName,
            ["subscriberMiddleName"] = normalized.SubscriberMiddleName,
            ["subscriberLastName"] = normalized.SubscriberLastName,
            ["subscriberDateOfBirth"] = normalized.SubscriberDateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["subscriberSex"] = normalized.SubscriberSex,
            ["subscriberStreet"] = normalized.SubscriberStreet,
            ["subscriberStreetLine2"] = normalized.SubscriberStreetLine2,
            ["subscriberCity"] = normalized.SubscriberCity,
            ["subscriberState"] = normalized.SubscriberState,
            ["subscriberPostalCode"] = normalized.SubscriberPostalCode,
            ["subscriberCountry"] = normalized.SubscriberCountry,
            ["subscriberPhone"] = normalized.SubscriberPhone,
            ["subscriberEmployer"] = normalized.SubscriberEmployer,
            ["subscriberEmployerStreet"] = normalized.SubscriberEmployerStreet,
            ["subscriberEmployerStreetLine2"] = normalized.SubscriberEmployerStreetLine2,
            ["subscriberEmployerCity"] = normalized.SubscriberEmployerCity,
            ["subscriberEmployerState"] = normalized.SubscriberEmployerState,
            ["subscriberEmployerPostalCode"] = normalized.SubscriberEmployerPostalCode,
            ["subscriberEmployerCountry"] = normalized.SubscriberEmployerCountry
        };

    private static IReadOnlyDictionary<string, string?> ReadAuditValues(
        DbDataReader reader,
        string columnName)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            reader.GetString(reader.GetOrdinal(columnName)));
        return values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    private static object ToDatabaseValue(string? value) =>
        value is null ? DBNull.Value : value;

    private static void AddNullableProviderAssignmentParameters(
        NpgsqlCommand command,
        string prefix,
        ProviderAssignmentSnapshot provider)
    {
        command.Parameters.Add($"{prefix}ProviderId", NpgsqlDbType.Integer).Value =
            provider.ProviderId is null ? DBNull.Value : provider.ProviderId.Value;
        command.Parameters.Add($"{prefix}ProviderName", NpgsqlDbType.Text).Value =
            provider.ProviderName is null ? DBNull.Value : provider.ProviderName;
        command.Parameters.Add($"{prefix}FacilityId", NpgsqlDbType.Integer).Value =
            provider.FacilityId is null ? DBNull.Value : provider.FacilityId.Value;
        command.Parameters.Add($"{prefix}FacilityName", NpgsqlDbType.Text).Value =
            provider.FacilityName is null ? DBNull.Value : provider.FacilityName;
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
        p.merged_into_patient_id is null
        and (@search is null
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

    private static async Task<PatientMergePreviewRow?> GetMergePreviewPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select
                p.canonical_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.date_of_birth,
                p.phone_home,
                p.phone_cell,
                p.email,
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
            left join lateral ({CountsSql("p.legacy_pid")}) counts on true
            where lower(p.canonical_id) = lower(@patientId)
               or lower(p.pubpid) = lower(@patientId)
               or p.legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var patient = new PatientMergePreviewPatient(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            DisplayName: BuildDisplayName(reader),
            FirstName: reader.GetString(reader.GetOrdinal("first_name")),
            LastName: reader.GetString(reader.GetOrdinal("last_name")),
            DateOfBirth: ReadDate(reader, "date_of_birth"),
            PhoneHome: ReadNullableString(reader, "phone_home"),
            PhoneCell: ReadNullableString(reader, "phone_cell"),
            Email: ReadNullableString(reader, "email"));

        return new PatientMergePreviewRow(patient, ReadCounts(reader));
    }

    private static (int Score, IReadOnlyList<string> Reasons) BuildMergeMatch(
        PatientMergePreviewPatient source,
        PatientMergePreviewPatient target)
    {
        var score = 0;
        var reasons = new List<string>();
        if (string.Equals(source.FirstName, target.FirstName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.LastName, target.LastName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.DateOfBirth, target.DateOfBirth, StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
            reasons.Add("Same first name, last name, and date of birth");
        }

        var sourcePhones = new[] { source.PhoneHome, source.PhoneCell }
            .Select(NormalizePhoneDigits)
            .Where(phone => !string.IsNullOrWhiteSpace(phone))
            .ToHashSet(StringComparer.Ordinal);
        var targetPhones = new[] { target.PhoneHome, target.PhoneCell }
            .Select(NormalizePhoneDigits)
            .Where(phone => !string.IsNullOrWhiteSpace(phone))
            .ToArray();
        if (targetPhones.Any(sourcePhones.Contains))
        {
            score += 10;
            reasons.Add("Matching phone");
        }

        if (!string.IsNullOrWhiteSpace(source.Email)
            && string.Equals(source.Email, target.Email, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add("Matching email");
        }

        return (score, reasons);
    }

    private static PatientActivityCounts CombineCounts(PatientActivityCounts target, PatientActivityCounts source) => new(
        Appointments: target.Appointments + source.Appointments,
        Encounters: target.Encounters + source.Encounters,
        Prescriptions: target.Prescriptions + source.Prescriptions,
        BillingItems: target.BillingItems + source.BillingItems,
        LabOrders: target.LabOrders + source.LabOrders,
        Messages: target.Messages + source.Messages,
        Problems: target.Problems + source.Problems,
        Allergies: target.Allergies + source.Allergies,
        Medications: target.Medications + source.Medications);

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
                    ContactId: null,
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

            if (normalizedMember.UserId is not null || normalizedMember.ContactId is not null)
            {
                members.Add(new NormalizedPatientCareTeamMember(
                    UserId: normalizedMember.UserId,
                    ContactId: normalizedMember.ContactId,
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
        if (request.UserId is null && request.ContactId is null)
        {
            return new NormalizedPatientCareTeamMemberCandidate(
                UserId: null,
                ContactId: null,
                Role: "primary_care_provider",
                FacilityId: null,
                ProviderSince: null,
                Status: "active",
                Note: NormalizeString(request.Note),
                Invalid: false);
        }

        if ((request.UserId is not null && request.UserId <= 0)
            || (request.ContactId is not null && request.ContactId <= 0)
            || (request.UserId is not null && request.ContactId is not null)
            || (request.UserId is not null && request.FacilityId <= 0))
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
            UserId: request.UserId,
            ContactId: request.ContactId,
            Role: role ?? "primary_care_provider",
            FacilityId: request.UserId is null ? null : request.FacilityId,
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

    private static async Task<bool> CareTeamContactExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        long contactId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from patient_related_contacts
                where contact_id = @contactId
                  and patient_id = @patientId
                  and active = true
            );
            """;
        command.Parameters.AddWithValue("contactId", contactId);
        command.Parameters.AddWithValue("patientId", patientId);
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

        if (parsedDate > DateOnly.FromDateTime(DateTime.UtcNow))
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
        var subscriberDateOfBirth = NormalizeOptionalDate(request.SubscriberDateOfBirth);

        if (string.IsNullOrWhiteSpace(type)
            || string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(planName)
            || string.IsNullOrWhiteSpace(policyNumber)
            || string.IsNullOrWhiteSpace(groupNumber)
            || string.IsNullOrWhiteSpace(relationship)
            || subscriberDateOfBirth.Invalid)
        {
            normalized = new NormalizedInsurance("", "", "", "", "", "", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
            return false;
        }

        normalized = new NormalizedInsurance(
            type,
            provider,
            planName,
            policyNumber,
            groupNumber,
            relationship,
            NormalizeString(request.SubscriberFirstName),
            NormalizeString(request.SubscriberMiddleName),
            NormalizeString(request.SubscriberLastName),
            subscriberDateOfBirth.Value,
            NormalizeString(request.SubscriberSex),
            NormalizeString(request.SubscriberStreet),
            NormalizeString(request.SubscriberStreetLine2),
            NormalizeString(request.SubscriberCity),
            NormalizeString(request.SubscriberState),
            NormalizeString(request.SubscriberPostalCode),
            NormalizeString(request.SubscriberCountry),
            NormalizeString(request.SubscriberPhone),
            NormalizeString(request.SubscriberEmployer),
            NormalizeString(request.SubscriberEmployerStreet),
            NormalizeString(request.SubscriberEmployerStreetLine2),
            NormalizeString(request.SubscriberEmployerCity),
            NormalizeString(request.SubscriberEmployerState),
            NormalizeString(request.SubscriberEmployerPostalCode),
            NormalizeString(request.SubscriberEmployerCountry));
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
        command.Parameters.Add("subscriberFirstName", NpgsqlDbType.Text).Value = normalized.SubscriberFirstName is null ? DBNull.Value : normalized.SubscriberFirstName;
        command.Parameters.Add("subscriberMiddleName", NpgsqlDbType.Text).Value = normalized.SubscriberMiddleName is null ? DBNull.Value : normalized.SubscriberMiddleName;
        command.Parameters.Add("subscriberLastName", NpgsqlDbType.Text).Value = normalized.SubscriberLastName is null ? DBNull.Value : normalized.SubscriberLastName;
        command.Parameters.Add("subscriberDateOfBirth", NpgsqlDbType.Date).Value = normalized.SubscriberDateOfBirth is null ? DBNull.Value : normalized.SubscriberDateOfBirth.Value;
        command.Parameters.Add("subscriberSex", NpgsqlDbType.Text).Value = normalized.SubscriberSex is null ? DBNull.Value : normalized.SubscriberSex;
        command.Parameters.Add("subscriberStreet", NpgsqlDbType.Text).Value = normalized.SubscriberStreet is null ? DBNull.Value : normalized.SubscriberStreet;
        command.Parameters.Add("subscriberStreetLine2", NpgsqlDbType.Text).Value = normalized.SubscriberStreetLine2 is null ? DBNull.Value : normalized.SubscriberStreetLine2;
        command.Parameters.Add("subscriberCity", NpgsqlDbType.Text).Value = normalized.SubscriberCity is null ? DBNull.Value : normalized.SubscriberCity;
        command.Parameters.Add("subscriberState", NpgsqlDbType.Text).Value = normalized.SubscriberState is null ? DBNull.Value : normalized.SubscriberState;
        command.Parameters.Add("subscriberPostalCode", NpgsqlDbType.Text).Value = normalized.SubscriberPostalCode is null ? DBNull.Value : normalized.SubscriberPostalCode;
        command.Parameters.Add("subscriberCountry", NpgsqlDbType.Text).Value = normalized.SubscriberCountry is null ? DBNull.Value : normalized.SubscriberCountry;
        command.Parameters.Add("subscriberPhone", NpgsqlDbType.Text).Value = normalized.SubscriberPhone is null ? DBNull.Value : normalized.SubscriberPhone;
        command.Parameters.Add("subscriberEmployer", NpgsqlDbType.Text).Value = normalized.SubscriberEmployer is null ? DBNull.Value : normalized.SubscriberEmployer;
        command.Parameters.Add("subscriberEmployerStreet", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerStreet is null ? DBNull.Value : normalized.SubscriberEmployerStreet;
        command.Parameters.Add("subscriberEmployerStreetLine2", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerStreetLine2 is null ? DBNull.Value : normalized.SubscriberEmployerStreetLine2;
        command.Parameters.Add("subscriberEmployerCity", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerCity is null ? DBNull.Value : normalized.SubscriberEmployerCity;
        command.Parameters.Add("subscriberEmployerState", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerState is null ? DBNull.Value : normalized.SubscriberEmployerState;
        command.Parameters.Add("subscriberEmployerPostalCode", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerPostalCode is null ? DBNull.Value : normalized.SubscriberEmployerPostalCode;
        command.Parameters.Add("subscriberEmployerCountry", NpgsqlDbType.Text).Value = normalized.SubscriberEmployerCountry is null ? DBNull.Value : normalized.SubscriberEmployerCountry;
    }

    private static void AddDemographicsParameters(
        NpgsqlCommand command,
        NormalizedPatientDemographics normalized)
    {
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
        command.Parameters.Add("familySize", NpgsqlDbType.Integer).Value =
            normalized.FamilySize is null ? DBNull.Value : normalized.FamilySize.Value;
        command.Parameters.Add("monthlyIncome", NpgsqlDbType.Integer).Value =
            normalized.MonthlyIncome is null ? DBNull.Value : normalized.MonthlyIncome.Value;
        command.Parameters.Add("homeless", NpgsqlDbType.Text).Value = NormalizeNullable(normalized.Homeless);
        command.Parameters.Add("financialReviewDate", NpgsqlDbType.Date).Value =
            normalized.FinancialReviewDate is null ? DBNull.Value : normalized.FinancialReviewDate.Value;
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

    private static PatientPortalAccountSummary ReadPortalAccount(DbDataReader reader)
    {
        var passwordStatus = ReadNullableInt(reader, "portal_account_password_status");
        var oneTimeToken = ReadNullableString(reader, "portal_account_one_time_token");
        var portalUsername = ReadNullableString(reader, "portal_account_username");

        return new PatientPortalAccountSummary(
            PortalEnabled: reader.GetBoolean(reader.GetOrdinal("portal_enabled")),
            AccessStatusLabel: PortalAccessStatusLabel(reader.GetBoolean(reader.GetOrdinal("portal_enabled")), portalUsername),
            CmsPortalLogin: ReadNullableString(reader, "cms_portal_login"),
            HasAccount: !string.IsNullOrWhiteSpace(portalUsername),
            PortalUsername: portalUsername,
            PortalLoginUsername: ReadNullableString(reader, "portal_account_login_username"),
            PasswordStatus: passwordStatus,
            PasswordStatusLabel: PortalPasswordStatusLabel(passwordStatus),
            OneTimeLinkPending: !string.IsNullOrWhiteSpace(oneTimeToken),
            ResetStatusLabel: PortalResetStatusLabel(!string.IsNullOrWhiteSpace(oneTimeToken), portalUsername));
    }

    private static string PortalPasswordStatusLabel(int? status) => status switch
    {
        0 => "Temporary password issued",
        1 => "Patient-managed password",
        null => "No account provisioned",
        _ => $"Status {status.Value.ToString(CultureInfo.InvariantCulture)}"
    };

    private static string PortalResetStatusLabel(bool oneTimeLinkPending, string? portalUsername)
    {
        if (string.IsNullOrWhiteSpace(portalUsername))
        {
            return "No account provisioned";
        }

        return oneTimeLinkPending ? "One-time reset pending" : "No reset pending";
    }

    private static string PortalAccessStatusLabel(bool portalEnabled, string? portalUsername)
    {
        if (portalEnabled)
        {
            return "Enabled";
        }

        return string.IsNullOrWhiteSpace(portalUsername) ? "Pending" : "Access disabled";
    }

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

    private static string? ReadNullableTimestamp(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetFieldValue<DateTimeOffset>(ordinal).ToString("O");
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

    private static long? ReadNullableLong(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
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

    private sealed record ProviderAssignmentPatient(
        string CanonicalId,
        int LegacyPid,
        ProviderAssignmentSnapshot Provider);

    private sealed record ProviderAssignmentSnapshot(
        int? ProviderId,
        string? ProviderName,
        int? FacilityId,
        string? FacilityName);

    private sealed record PatientAdministrationSnapshot(
        PatientIdentity Patient,
        IReadOnlyDictionary<string, string?> DemographicValues,
        IReadOnlyDictionary<string, string?> ContactValues);

    private sealed record InsuranceAuditSnapshot(
        PatientIdentity Patient,
        IReadOnlyDictionary<string, string?> Values);

    private sealed record PatientMergePreviewRow(PatientMergePreviewPatient Patient, PatientActivityCounts Counts);

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
        string Relationship,
        string? SubscriberFirstName,
        string? SubscriberMiddleName,
        string? SubscriberLastName,
        DateOnly? SubscriberDateOfBirth,
        string? SubscriberSex,
        string? SubscriberStreet,
        string? SubscriberStreetLine2,
        string? SubscriberCity,
        string? SubscriberState,
        string? SubscriberPostalCode,
        string? SubscriberCountry,
        string? SubscriberPhone,
        string? SubscriberEmployer,
        string? SubscriberEmployerStreet,
        string? SubscriberEmployerStreetLine2,
        string? SubscriberEmployerCity,
        string? SubscriberEmployerState,
        string? SubscriberEmployerPostalCode,
        string? SubscriberEmployerCountry);

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
        int? UserId,
        long? ContactId,
        string Role,
        int? FacilityId,
        DateOnly? ProviderSince,
        string Status,
        string? Note);

    private sealed record NormalizedPatientCareTeamMemberCandidate(
        int? UserId,
        long? ContactId,
        string Role,
        int? FacilityId,
        DateOnly? ProviderSince,
        string Status,
        string? Note,
        bool Invalid)
    {
        public static NormalizedPatientCareTeamMemberCandidate InvalidValue { get; } =
            new(null, null, "", null, null, "", null, true);
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
