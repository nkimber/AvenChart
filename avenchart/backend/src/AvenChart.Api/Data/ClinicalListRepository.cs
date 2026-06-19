using System.Data.Common;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ClinicalListRepository(NpgsqlDataSource dataSource)
{
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
        var immunizations = await GetImmunizationsAsync(connection, patient.LegacyPid, cancellationToken);
        var prescriptions = await GetPrescriptionsAsync(connection, patient.LegacyPid, cancellationToken);

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
            Immunizations: immunizations,
            Prescriptions: prescriptions);
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

        var id = $"RX-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into prescriptions
                (id, patient_id, pid, provider_id, encounter, start_date, end_date, drug, rx_norm_code,
                 dosage, quantity, route, refills, diagnosis, note, active)
            values
                (@id, @patientId, @pid, @providerId, 0, @startDate, null, @drug, @rxNormCode,
                 @dosage, @quantity, @route, @refills, @diagnosis, @note, 1);
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
        command.Parameters.AddWithValue("route", string.IsNullOrWhiteSpace(request.Route) ? "oral" : request.Route.Trim());
        command.Parameters.AddWithValue("refills", request.Refills);
        command.Parameters.AddWithValue("diagnosis", NullableText(request.Diagnosis));
        command.Parameters.AddWithValue("note", NullableText(request.Note));
        await command.ExecuteNonQueryAsync(cancellationToken);

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
            command.CommandText = """
                update prescriptions
                set active = 0,
                    end_date = @endDate,
                    note = @note
                where id = @id
                returning patient_id;
                """;
            command.Parameters.AddWithValue("id", prescriptionId);
            command.Parameters.Add("endDate", NpgsqlDbType.Date).Value = endDate;
            command.Parameters.AddWithValue("note", NullableText(request.Note));
            patientId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

        if (patientId is null)
        {
            return null;
        }

        var lists = await GetForPatientAsync(patientId, cancellationToken);
        return lists is null ? null : new ClinicalListMutationResponse(prescriptionId, lists);
    }

    public async Task<bool> DeletePrescriptionAsync(string prescriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from prescriptions
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", prescriptionId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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
            select id, title, diagnosis, problem_date, comments
            from problems
            where pid = @pid
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
                Comments: ReadNullableString(reader, "comments")));
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
            select id, title, diagnosis, medication_date, comments
            from medications
            where pid = @pid
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
                Comments: ReadNullableString(reader, "comments")));
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
                pr.route,
                pr.rx_norm_code,
                pr.diagnosis,
                pr.start_date,
                pr.end_date,
                pr.refills,
                pr.active,
                pr.note,
                pr.encounter,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name
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
            items.Add(new PrescriptionListItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Drug: reader.GetString(reader.GetOrdinal("drug")),
                Dosage: ReadNullableString(reader, "dosage"),
                Quantity: ReadNullableString(reader, "quantity"),
                Route: ReadNullableString(reader, "route"),
                RxNormCode: ReadNullableString(reader, "rx_norm_code"),
                Diagnosis: ReadNullableString(reader, "diagnosis"),
                StartDate: ReadNullableDate(reader, "start_date"),
                EndDate: ReadNullableDate(reader, "end_date"),
                Refills: ReadInt(reader, "refills"),
                Active: ReadInt(reader, "active"),
                Note: ReadNullableString(reader, "note"),
                Encounter: ReadNullableInt(reader, "encounter"),
                ProviderName: ReadNullableString(reader, "provider_name")));
        }

        return items;
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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record ClinicalListPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        int ProviderId,
        string FirstName,
        string LastName,
        string DisplayName);
}
