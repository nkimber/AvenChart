using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AdministrationRepository(NpgsqlDataSource dataSource)
{
    private const string DefaultFacilityColor = "#246b73";
    private const string DefaultUserEmailDomain = "example.test";
    private static readonly JsonSerializerOptions PortalProfileChangeJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ValidAccessReturnValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "addonly",
        "view",
        "write",
        "wsome"
    };

    public async Task<PracticeSettingsResponse> GetPracticeSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select setting_key, setting_value, value_type, updated_at, updated_by from practice_settings order by setting_key;";
        var settings = new List<PracticeSettingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) settings.Add(new(reader.GetString(0), reader.GetString(0) switch { "practice.name" => "Practice name", "practice.default-facility-id" => "Default facility", _ => "Time zone" }, reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4)));
        return new PracticeSettingsResponse(settings);
    }

    public async Task<CodingCatalogResponse> GetCodingCatalogsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select catalog_key, display_name, sequence, active, claim_enabled, fee_enabled, modifier_length, updated_at, updated_by from coding_catalogs order by sequence, catalog_key;";
        var catalogs = new List<CodingCatalogItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) catalogs.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O"), reader.GetString(8)));
        return new CodingCatalogResponse(catalogs);
    }

    public async Task<CodingCatalogResponse> UpdateCodingCatalogAsync(string key, CodingCatalogUpdateRequest request, string username, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(key);
        ValidateCodingCatalog(request.DisplayName, request.Sequence, request.ModifierLength);
        var displayName = request.DisplayName.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        bool changed;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "select display_name,sequence,active,claim_enabled,fee_enabled,modifier_length from coding_catalogs where catalog_key=@key for update;";
            existing.Parameters.AddWithValue("key", catalogKey);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Catalog was not found.");
            changed = reader.GetString(0) != displayName || reader.GetInt32(1) != request.Sequence || reader.GetBoolean(2) != request.Active || reader.GetBoolean(3) != request.ClaimEnabled || reader.GetBoolean(4) != request.FeeEnabled || reader.GetInt32(5) != request.ModifierLength;
        }
        if (changed)
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "update coding_catalogs set display_name=@name,sequence=@sequence,active=@active,claim_enabled=@claim,fee_enabled=@fee,modifier_length=@modifier,updated_at=now(),updated_by=@user where catalog_key=@key;";
            command.Parameters.AddWithValue("key", catalogKey); command.Parameters.AddWithValue("name", displayName); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("claim", request.ClaimEnabled); command.Parameters.AddWithValue("fee", request.FeeEnabled); command.Parameters.AddWithValue("modifier", request.ModifierLength); command.Parameters.AddWithValue("user", username);
            try { await command.ExecuteNonQueryAsync(cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Catalog key and sequence must be unique."); }
            await using var audit = connection.CreateCommand(); audit.Transaction = transaction; audit.CommandText = "insert into coding_catalog_audit_events(event_id,catalog_key,action,occurred_at,username) values(@eventId,@key,'updated',now(),@user);"; audit.Parameters.AddWithValue("eventId", Guid.NewGuid()); audit.Parameters.AddWithValue("key", catalogKey); audit.Parameters.AddWithValue("user", username); await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetCodingCatalogsAsync(cancellationToken);
    }

    public async Task<CodingCatalogResponse> CreateCodingCatalogAsync(CodingCatalogCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(request.Key);
        ValidateCodingCatalog(request.DisplayName, request.Sequence, request.ModifierLength);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "insert into coding_catalogs(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,updated_at,updated_by) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,now(),@user); insert into coding_catalog_audit_events(event_id,catalog_key,action,occurred_at,username) values(@eventId,@key,'created',now(),@user);";
        command.Parameters.AddWithValue("key", catalogKey); command.Parameters.AddWithValue("name", request.DisplayName.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("claim", request.ClaimEnabled); command.Parameters.AddWithValue("fee", request.FeeEnabled); command.Parameters.AddWithValue("modifier", request.ModifierLength); command.Parameters.AddWithValue("user", username); command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        try { await command.ExecuteNonQueryAsync(cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Catalog key and sequence must be unique."); }
        await transaction.CommitAsync(cancellationToken);
        return await GetCodingCatalogsAsync(cancellationToken);
    }

    private static void ValidateCodingCatalog(string displayName, int sequence, int modifierLength)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120 || sequence < 0 || modifierLength is < 0 or > 12)
            throw new ArgumentException("Catalog label, sequence, and modifier length are invalid.");
    }

    private static string NormalizeCatalogKey(string key)
    {
        var normalized = key.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 32 || !normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new ArgumentException("Catalog key must be 2-32 uppercase letters, numbers, or underscores.");
        return normalized;
    }

    public async Task<PracticeSettingsResponse> UpdatePracticeSettingAsync(string key, string value, string username, CancellationToken cancellationToken)
    {
        if (key is not ("practice.name" or "practice.default-facility-id" or "practice.time-zone")) throw new ArgumentException("The requested practice setting is not mutable.");
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A setting value is required.");
        if (key == "practice.default-facility-id" && (!int.TryParse(normalized, out var facilityId) || facilityId <= 0)) throw new ArgumentException("Default facility must be a valid facility identifier.");
        if (key == "practice.time-zone" && !TimeZoneInfo.GetSystemTimeZones().Any(zone => zone.Id == normalized)) throw new ArgumentException("Time zone must be a supported IANA or Windows time-zone identifier.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var existing = connection.CreateCommand(); existing.Transaction = transaction; existing.CommandText = "select setting_value from practice_settings where setting_key = @key for update;"; existing.Parameters.AddWithValue("key", key);
        var prior = await existing.ExecuteScalarAsync(cancellationToken) as string ?? throw new ArgumentException("The requested practice setting was not found.");
        if (prior != normalized) { await using var update = connection.CreateCommand(); update.Transaction = transaction; update.CommandText = "update practice_settings set setting_value=@value, updated_at=now(), updated_by=@username where setting_key=@key; insert into practice_setting_audit_events(event_id,setting_key,prior_value,new_value,occurred_at,username) values(@eventId,@key,@prior,@value,now(),@username);"; update.Parameters.AddWithValue("key", key); update.Parameters.AddWithValue("value", normalized); update.Parameters.AddWithValue("prior", prior); update.Parameters.AddWithValue("username", username); update.Parameters.AddWithValue("eventId", Guid.NewGuid()); await update.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken); return await GetPracticeSettingsAsync(cancellationToken);
    }

    public async Task<AdministrationDirectoryResponse> GetDirectoryAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await GetUsersAsync(connection, cancellationToken);
        var facilities = await GetFacilitiesAsync(connection, cancellationToken);
        var accessControl = await GetAccessControlAsync(connection, cancellationToken);
        var portalActivity = await GetPortalActivityAsync(connection, cancellationToken);

        return new AdministrationDirectoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Counts: new AdministrationDirectoryCounts(
                Users: users.Count,
                Providers: users.Count(user => string.Equals(user.Role, "provider", StringComparison.OrdinalIgnoreCase)),
                CalendarUsers: users.Count(user => user.Calendar),
                Facilities: facilities.Count,
                AccessGroups: accessControl.Groups.Count,
                AccessPermissions: accessControl.Permissions.Count,
                AccessGroupPermissions: accessControl.GroupPermissions.Count,
                AccessUserMemberships: accessControl.UserMemberships.Count,
                WaitingPortalAudits: portalActivity.WaitingAuditCount,
                WaitingProfileReviews: portalActivity.WaitingProfileReviewCount),
            Users: users,
            Facilities: facilities,
            AccessControl: accessControl,
            PortalActivity: portalActivity);
    }

    public async Task<AdministrationPortalProfileReviewMutationResponse?> AcceptPortalProfileReviewAsync(
        long requestId,
        string actionUser,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText = """
            select
                id::text,
                patient_id,
                pid,
                requested_changes::text as requested_changes
            from patient_portal_profile_change_requests
            where id = @id
              and status = 'waiting'
              and activity = 'profile'
              and require_audit = 1
              and pending_action = 'review'
            for update;
            """;
        findCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;

        string id;
        string patientId;
        int legacyPid;
        PatientPortalProfileDemographics requestedDemographics;
        await using (var reader = await findCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetString(reader.GetOrdinal("id"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            requestedDemographics = JsonSerializer.Deserialize<PatientPortalProfileDemographics>(
                reader.GetString(reader.GetOrdinal("requested_changes")),
                PortalProfileChangeJsonOptions) ?? EmptyRequestedDemographics();
        }

        await using (var updatePatientCommand = connection.CreateCommand())
        {
            updatePatientCommand.Transaction = transaction;
            updatePatientCommand.CommandText = """
                update patients
                set first_name = @first_name,
                    last_name = @last_name,
                    preferred_name = @preferred_name,
                    date_of_birth = coalesce(@date_of_birth, date_of_birth),
                    sex = @sex,
                    email = @email,
                    street = @street,
                    city = @city,
                    state = @state,
                    postal_code = @postal_code,
                    phone_home = @phone_home,
                    phone_cell = @phone_cell,
                    phone = @phone_contact,
                    hipaa_allow_sms = @hipaa_allow_sms,
                    hipaa_allow_email = @hipaa_allow_email,
                    guardian_relationship = @guardian_relationship,
                    mother_name = @mother_name,
                    guardian_name = @guardian_name,
                    guardian_phone = @guardian_phone,
                    guardian_email = @guardian_email
                where canonical_id = @patient_id;
                """;
            updatePatientCommand.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = patientId;
            updatePatientCommand.Parameters.Add("first_name", NpgsqlDbType.Text).Value =
                NormalizeRequired(requestedDemographics.FirstName, "First name");
            updatePatientCommand.Parameters.Add("last_name", NpgsqlDbType.Text).Value =
                NormalizeRequired(requestedDemographics.LastName, "Last name");
            AddNullableText(updatePatientCommand, "preferred_name", requestedDemographics.PreferredName);
            AddNullableDate(updatePatientCommand, "date_of_birth", requestedDemographics.DateOfBirth);
            AddNullableText(updatePatientCommand, "sex", requestedDemographics.Sex);
            AddNullableText(updatePatientCommand, "email", requestedDemographics.Email);
            AddNullableText(updatePatientCommand, "street", requestedDemographics.Street);
            AddNullableText(updatePatientCommand, "city", requestedDemographics.City);
            AddNullableText(updatePatientCommand, "state", requestedDemographics.State);
            AddNullableText(updatePatientCommand, "postal_code", requestedDemographics.PostalCode);
            AddNullableText(updatePatientCommand, "phone_home", requestedDemographics.PhoneHome);
            AddNullableText(updatePatientCommand, "phone_cell", requestedDemographics.PhoneCell);
            AddNullableText(updatePatientCommand, "phone_contact", requestedDemographics.PhoneContact);
            AddNullableText(updatePatientCommand, "hipaa_allow_sms", NormalizePermission(requestedDemographics.HipaaAllowSms));
            AddNullableText(updatePatientCommand, "hipaa_allow_email", NormalizePermission(requestedDemographics.HipaaAllowEmail));
            AddNullableText(
                updatePatientCommand,
                "guardian_relationship",
                requestedDemographics.GuardianRelationship ?? requestedDemographics.ContactRelationship);
            AddNullableText(updatePatientCommand, "mother_name", requestedDemographics.MotherName);
            AddNullableText(updatePatientCommand, "guardian_name", requestedDemographics.GuardianName);
            AddNullableText(updatePatientCommand, "guardian_phone", requestedDemographics.GuardianPhone);
            AddNullableText(updatePatientCommand, "guardian_email", requestedDemographics.GuardianEmail);

            if (await updatePatientCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        const string acceptedStatus = "closed";
        const string acceptedPendingAction = "completed";
        const string acceptedActionTaken = "accept";
        const string acceptedNarrative = "Changes reviewed and committed to demographics.";
        const string acceptedTableAction = "update";
        string acceptedAt;
        await using (var updateRequestCommand = connection.CreateCommand())
        {
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.CommandText = """
                update patient_portal_profile_change_requests
                set pending_action = @pending_action,
                    action_taken = @action_taken,
                    status = @status,
                    narrative = @narrative,
                    table_action = @table_action,
                    action_user = @action_user,
                    action_taken_at = now(),
                    updated_at = now()
                where id = @id
                returning to_char(action_taken_at, 'YYYY-MM-DD HH24:MI:SS') as action_taken_at;
                """;
            updateRequestCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;
            updateRequestCommand.Parameters.Add("pending_action", NpgsqlDbType.Text).Value = acceptedPendingAction;
            updateRequestCommand.Parameters.Add("action_taken", NpgsqlDbType.Text).Value = acceptedActionTaken;
            updateRequestCommand.Parameters.Add("status", NpgsqlDbType.Text).Value = acceptedStatus;
            updateRequestCommand.Parameters.Add("narrative", NpgsqlDbType.Text).Value = acceptedNarrative;
            updateRequestCommand.Parameters.Add("table_action", NpgsqlDbType.Text).Value = acceptedTableAction;
            updateRequestCommand.Parameters.Add("action_user", NpgsqlDbType.Text).Value = NormalizeRequired(actionUser, "Action user");
            acceptedAt = (string)(await updateRequestCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Profile review accept did not return an action timestamp."));
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationPortalProfileReviewMutationResponse(
            Id: id,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Status: acceptedStatus,
            PendingAction: acceptedPendingAction,
            ActionTaken: acceptedActionTaken,
            Narrative: acceptedNarrative,
            TableAction: acceptedTableAction,
            ActionUser: NormalizeRequired(actionUser, "Action user"),
            ActionTakenAt: acceptedAt,
            RequestedDemographics: requestedDemographics,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationPortalProfileReviewMutationResponse?> RevertPortalProfileReviewAsync(
        long requestId,
        string actionUser,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText = """
            select
                r.id::text,
                r.patient_id,
                r.pid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.date_of_birth,
                p.sex,
                p.email,
                p.street,
                p.city,
                p.state,
                p.postal_code,
                p.phone_home,
                p.phone_cell,
                p.phone as phone_contact,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                p.guardian_relationship as contact_relationship,
                p.mother_name,
                p.guardian_name,
                p.guardian_relationship,
                p.guardian_phone,
                p.guardian_email
            from patient_portal_profile_change_requests r
            join patients p on p.canonical_id = r.patient_id
            where r.id = @id
              and r.status = 'waiting'
              and r.activity = 'profile'
              and r.require_audit = 1
              and r.pending_action = 'review'
            for update;
            """;
        findCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;

        string id;
        string patientId;
        int legacyPid;
        PatientPortalProfileDemographics chartDemographics;
        await using (var reader = await findCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetString(reader.GetOrdinal("id"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            chartDemographics = new PatientPortalProfileDemographics(
                FirstName: reader.GetString(reader.GetOrdinal("first_name")),
                LastName: reader.GetString(reader.GetOrdinal("last_name")),
                PreferredName: ReadNullableString(reader, "preferred_name"),
                DateOfBirth: ReadNullableDate(reader, "date_of_birth"),
                Sex: ReadNullableString(reader, "sex"),
                Email: ReadNullableString(reader, "email"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                PhoneHome: ReadNullableString(reader, "phone_home"),
                PhoneCell: ReadNullableString(reader, "phone_cell"),
                PhoneContact: ReadNullableString(reader, "phone_contact"),
                HipaaAllowSms: ReadNullableString(reader, "hipaa_allow_sms"),
                HipaaAllowEmail: ReadNullableString(reader, "hipaa_allow_email"),
                ContactRelationship: ReadNullableString(reader, "contact_relationship"),
                MotherName: ReadNullableString(reader, "mother_name"),
                GuardianName: ReadNullableString(reader, "guardian_name"),
                GuardianRelationship: ReadNullableString(reader, "guardian_relationship"),
                GuardianPhone: ReadNullableString(reader, "guardian_phone"),
                GuardianEmail: ReadNullableString(reader, "guardian_email"));
        }

        const string resolvedStatus = "closed";
        const string resolvedPendingAction = "completed";
        const string resolvedActionTaken = "accept";
        const string resolvedNarrative = "Changes reviewed and committed to demographics.";
        const string resolvedTableAction = "update";
        string resolvedAt;
        await using (var updateRequestCommand = connection.CreateCommand())
        {
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.CommandText = """
                update patient_portal_profile_change_requests
                set requested_changes = @requested_changes::jsonb,
                    pending_action = @pending_action,
                    action_taken = @action_taken,
                    status = @status,
                    narrative = @narrative,
                    table_action = @table_action,
                    action_user = @action_user,
                    action_taken_at = now(),
                    updated_at = now()
                where id = @id
                returning to_char(action_taken_at, 'YYYY-MM-DD HH24:MI:SS') as action_taken_at;
                """;
            updateRequestCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;
            updateRequestCommand.Parameters.Add("requested_changes", NpgsqlDbType.Text).Value =
                JsonSerializer.Serialize(chartDemographics, PortalProfileChangeJsonOptions);
            updateRequestCommand.Parameters.Add("pending_action", NpgsqlDbType.Text).Value = resolvedPendingAction;
            updateRequestCommand.Parameters.Add("action_taken", NpgsqlDbType.Text).Value = resolvedActionTaken;
            updateRequestCommand.Parameters.Add("status", NpgsqlDbType.Text).Value = resolvedStatus;
            updateRequestCommand.Parameters.Add("narrative", NpgsqlDbType.Text).Value = resolvedNarrative;
            updateRequestCommand.Parameters.Add("table_action", NpgsqlDbType.Text).Value = resolvedTableAction;
            updateRequestCommand.Parameters.Add("action_user", NpgsqlDbType.Text).Value = NormalizeRequired(actionUser, "Action user");
            resolvedAt = (string)(await updateRequestCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Profile review revert did not return an action timestamp."));
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationPortalProfileReviewMutationResponse(
            Id: id,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Status: resolvedStatus,
            PendingAction: resolvedPendingAction,
            ActionTaken: resolvedActionTaken,
            Narrative: resolvedNarrative,
            TableAction: resolvedTableAction,
            ActionUser: NormalizeRequired(actionUser, "Action user"),
            ActionTakenAt: resolvedAt,
            RequestedDemographics: chartDemographics,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse> CreateUserAsync(
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextStaffIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into staff
              (id, username, first_name, last_name, role, calendar, facility_id, email, npi, active)
            values
              (@id, @username, @firstName, @lastName, @role, @calendar, @facilityId, @email, @npi, @active)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddUserParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User create did not return an ID."));

        return new AdministrationUserMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse?> UpdateUserAsync(
        int userId,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update staff
            set username = @username,
                first_name = @firstName,
                last_name = @lastName,
                role = @role,
                calendar = @calendar,
                facility_id = @facilityId,
                email = @email,
                npi = @npi,
                active = @active
            where id = @userId
            returning id;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        AddUserParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationUserMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var membershipCommand = connection.CreateCommand())
        {
            membershipCommand.Transaction = transaction;
            membershipCommand.CommandText = """
                delete from access_user_memberships
                where staff_id = @userId;
                """;
            membershipCommand.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
            await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            delete from staff
            where id = @userId;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        var deleted = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async Task<AdministrationFacilityMutationResponse> CreateFacilityAsync(
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextFacilityIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into facilities
              (id, code, name, phone, street, city, state, postal_code, color, inactive)
            values
              (@id, @code, @name, @phone, @street, @city, @state, @postalCode, @color, @inactive)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddFacilityParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility create did not return an ID."));

        return new AdministrationFacilityMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationFacilityMutationResponse?> UpdateFacilityAsync(
        int facilityId,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update facilities
            set code = @code,
                name = @name,
                phone = @phone,
                street = @street,
                city = @city,
                state = @state,
                postal_code = @postalCode,
                color = @color,
                inactive = @inactive
            where id = @facilityId
            returning id;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        AddFacilityParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationFacilityMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteFacilityAsync(int facilityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from facilities
            where id = @facilityId;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<AdministrationAccessPermissionMutationResponse> GrantAccessGroupPermissionAsync(
        AdministrationAccessPermissionMutationRequest request,
        CancellationToken cancellationToken)
    {
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");
        var sectionValue = NormalizeAccessToken(request.SectionValue, "Permission section");
        var permissionValue = NormalizeAccessToken(request.PermissionValue, "Permission");
        var returnValue = NormalizeAccessReturnValue(request.ReturnValue);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await AccessGroupExistsAsync(connection, groupValue, cancellationToken))
        {
            throw new ArgumentException($"Access group '{groupValue}' was not found.");
        }

        var permissionName = await GetAccessPermissionNameAsync(connection, sectionValue, permissionValue, cancellationToken)
            ?? throw new ArgumentException($"Access permission '{sectionValue}:{permissionValue}' was not found.");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = """
                delete from access_group_permissions
                where group_value = @groupValue
                  and section_value = @sectionValue
                  and permission_value = @permissionValue;
                """;
            AddAccessAssignmentKeys(deleteCommand, groupValue, sectionValue, permissionValue);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                insert into access_group_permissions
                  (group_value, section_value, permission_value, permission_name, return_value)
                values
                  (@groupValue, @sectionValue, @permissionValue, @permissionName, @returnValue);
                """;
            AddAccessAssignmentKeys(insertCommand, groupValue, sectionValue, permissionValue);
            insertCommand.Parameters.Add("permissionName", NpgsqlDbType.Text).Value = permissionName;
            insertCommand.Parameters.Add("returnValue", NpgsqlDbType.Text).Value = returnValue;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationAccessPermissionMutationResponse(
            GroupValue: groupValue,
            SectionValue: sectionValue,
            PermissionValue: permissionValue,
            ReturnValue: returnValue,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessPermissionMutationResponse?> RevokeAccessGroupPermissionAsync(
        string groupValue,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken)
    {
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");
        var normalizedSection = NormalizeAccessToken(sectionValue, "Permission section");
        var normalizedPermission = NormalizeAccessToken(permissionValue, "Permission");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from access_group_permissions
            where group_value = @groupValue
              and section_value = @sectionValue
              and permission_value = @permissionValue;
            """;
        AddAccessAssignmentKeys(command, normalizedGroup, normalizedSection, normalizedPermission);

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (deleted == 0)
        {
            return null;
        }

        return new AdministrationAccessPermissionMutationResponse(
            GroupValue: normalizedGroup,
            SectionValue: normalizedSection,
            PermissionValue: normalizedPermission,
            ReturnValue: null,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse> GrantAccessUserMembershipAsync(
        AdministrationAccessUserMembershipMutationRequest request,
        CancellationToken cancellationToken)
    {
        var userValue = NormalizeAccessToken(request.UserValue, "User");
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var groupName = await GetAccessGroupNameAsync(connection, groupValue, cancellationToken)
            ?? throw new ArgumentException($"Access group '{groupValue}' was not found.");
        var staff = await GetStaffAccessUserAsync(connection, userValue, cancellationToken)
            ?? throw new ArgumentException($"User '{userValue}' was not found.");

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into access_user_memberships
              (user_value, user_name, group_value, group_name, staff_id)
            values
              (@userValue, @userName, @groupValue, @groupName, @staffId)
            on conflict (user_value, group_value) do update
            set user_name = excluded.user_name,
                group_name = excluded.group_name,
                staff_id = excluded.staff_id;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = staff.UserValue;
        command.Parameters.Add("userName", NpgsqlDbType.Text).Value = staff.UserName;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        command.Parameters.Add("groupName", NpgsqlDbType.Text).Value = groupName;
        command.Parameters.Add("staffId", NpgsqlDbType.Integer).Value = staff.StaffId;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new AdministrationAccessUserMembershipMutationResponse(
            UserValue: staff.UserValue,
            GroupValue: groupValue,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse?> RevokeAccessUserMembershipAsync(
        string userValue,
        string groupValue,
        CancellationToken cancellationToken)
    {
        var normalizedUser = NormalizeAccessToken(userValue, "User");
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from access_user_memberships
            where user_value = @userValue
              and group_value = @groupValue;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = normalizedUser;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = normalizedGroup;

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (deleted == 0)
        {
            return null;
        }

        return new AdministrationAccessUserMembershipMutationResponse(
            UserValue: normalizedUser,
            GroupValue: normalizedGroup,
            Detail: await GetDirectoryAsync(cancellationToken));
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

    private static async Task<IReadOnlyList<AdministrationUserItem>> GetUsersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                s.id,
                s.username,
                s.first_name,
                s.last_name,
                s.role,
                s.active,
                s.calendar,
                s.facility_id,
                f.name as facility_name,
                s.email,
                s.npi
            from staff s
            left join facilities f on f.id = s.facility_id
            order by s.id;
            """;

        var users = new List<AdministrationUserItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            var username = reader.GetString(reader.GetOrdinal("username"));
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var role = reader.GetString(reader.GetOrdinal("role"));
            var isProvider = string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);

            users.Add(new AdministrationUserItem(
                Id: id,
                Username: username,
                FirstName: firstName,
                LastName: lastName,
                DisplayName: $"{lastName}, {firstName}",
                Role: role,
                Authorized: isProvider,
                Active: reader.GetBoolean(reader.GetOrdinal("active")),
                Calendar: reader.GetBoolean(reader.GetOrdinal("calendar")),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Email: ReadNullableString(reader, "email") ?? $"{username}@{DefaultUserEmailDomain}",
                Npi: ReadNullableString(reader, "npi") ?? (isProvider ? $"18888{id}" : null)));
        }

        return users;
    }

    private static async Task<IReadOnlyList<AdministrationFacilityItem>> GetFacilitiesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, code, name, phone, street, city, state, postal_code, color, inactive
            from facilities
            order by id;
            """;

        var facilities = new List<AdministrationFacilityItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facilities.Add(new AdministrationFacilityItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Code: reader.GetString(reader.GetOrdinal("code")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                Active: !reader.GetBoolean(reader.GetOrdinal("inactive")),
                Phone: ReadNullableString(reader, "phone"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                Color: ReadNullableString(reader, "color")));
        }

        return facilities;
    }

    private static async Task<AdministrationAccessControlSummary> GetAccessControlAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var groups = new List<AdministrationAccessGroupItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    g.id,
                    g.value,
                    g.name,
                    g.parent_id,
                    count(gp.*) as permission_count
                from access_groups g
                left join access_group_permissions gp on gp.group_value = g.value
                group by g.id, g.value, g.name, g.parent_id
                order by g.id;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groups.Add(new AdministrationAccessGroupItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    ParentId: ReadNullableInt(reader, "parent_id"),
                    PermissionCount: (int)reader.GetInt64(reader.GetOrdinal("permission_count"))));
            }
        }

        var permissions = new List<AdministrationAccessPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select section_value, value, name
                from access_permissions
                order by section_value, value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new AdministrationAccessPermissionItem(
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name"))));
            }
        }

        var groupPermissions = new List<AdministrationAccessGroupPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select group_value, section_value, permission_value, permission_name, return_value
                from access_group_permissions
                order by group_value, section_value, permission_value, return_value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groupPermissions.Add(new AdministrationAccessGroupPermissionItem(
                    GroupValue: reader.GetString(reader.GetOrdinal("group_value")),
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    PermissionValue: reader.GetString(reader.GetOrdinal("permission_value")),
                    PermissionName: reader.GetString(reader.GetOrdinal("permission_name")),
                    ReturnValue: reader.GetString(reader.GetOrdinal("return_value"))));
            }
        }

        var userMemberships = new List<AdministrationAccessUserMembershipItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select user_value, user_name, group_value, group_name, staff_id
                from access_user_memberships
                order by user_value, group_value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                userMemberships.Add(new AdministrationAccessUserMembershipItem(
                    UserValue: reader.GetString(reader.GetOrdinal("user_value")),
                    UserName: reader.GetString(reader.GetOrdinal("user_name")),
                    GroupValue: reader.GetString(reader.GetOrdinal("group_value")),
                    GroupName: reader.GetString(reader.GetOrdinal("group_name")),
                    StaffId: ReadNullableInt(reader, "staff_id")));
            }
        }

        return new AdministrationAccessControlSummary(groups, permissions, groupPermissions, userMemberships);
    }

    private static async Task<AdministrationPortalActivitySummary> GetPortalActivityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var waitingAuditCount = 0;
        var waitingProfileReviewCount = 0;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                select
                    count(*) filter (where status = 'waiting') as waiting_audit_count,
                    count(*) filter (
                        where status = 'waiting'
                          and activity = 'profile'
                          and require_audit = 1
                          and pending_action = 'review'
                    ) as waiting_profile_review_count
                from patient_portal_profile_change_requests;
                """;

            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                waitingAuditCount = (int)reader.GetInt64(reader.GetOrdinal("waiting_audit_count"));
                waitingProfileReviewCount = (int)reader.GetInt64(reader.GetOrdinal("waiting_profile_review_count"));
            }
        }

        var requests = new List<AdministrationPortalProfileReviewRequest>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    r.id::text,
                    to_char(r.created_at, 'YYYY-MM-DD HH24:MI:SS') as requested_at,
                    r.patient_id,
                    r.pid,
                    p.pubpid,
                    p.first_name,
                    '' as middle_name,
                    p.last_name,
                    r.activity,
                    r.require_audit,
                    r.pending_action,
                    r.action_taken,
                    r.status,
                    r.narrative,
                    r.table_action,
                    nullif(r.action_user, '') as action_user,
                    case
                        when r.action_taken_at is null then null
                        else to_char(r.action_taken_at, 'YYYY-MM-DD HH24:MI:SS')
                    end as action_taken_at,
                    r.checksum,
                    r.requested_changes::text as requested_changes
                from patient_portal_profile_change_requests r
                join patients p on p.canonical_id = r.patient_id
                where r.status = 'waiting'
                  and r.activity = 'profile'
                  and r.require_audit = 1
                  and r.pending_action = 'review'
                order by r.created_at desc, r.id desc;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var requestedChanges = reader.GetString(reader.GetOrdinal("requested_changes"));
                var demographics = JsonSerializer.Deserialize<PatientPortalProfileDemographics>(
                    requestedChanges,
                    PortalProfileChangeJsonOptions) ?? EmptyRequestedDemographics();
                var firstName = reader.GetString(reader.GetOrdinal("first_name"));
                var lastName = reader.GetString(reader.GetOrdinal("last_name"));
                var middleName = reader.GetString(reader.GetOrdinal("middle_name"));
                var patientName = string.Join(
                    " ",
                    new[] { firstName, middleName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

                requests.Add(new AdministrationPortalProfileReviewRequest(
                    Id: reader.GetString(reader.GetOrdinal("id")),
                    RequestedAt: reader.GetString(reader.GetOrdinal("requested_at")),
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    FirstName: firstName,
                    MiddleName: middleName,
                    LastName: lastName,
                    PatientName: patientName,
                    Activity: reader.GetString(reader.GetOrdinal("activity")),
                    RequireAudit: reader.GetInt32(reader.GetOrdinal("require_audit")),
                    PendingAction: reader.GetString(reader.GetOrdinal("pending_action")),
                    ActionTaken: reader.GetString(reader.GetOrdinal("action_taken")),
                    Status: reader.GetString(reader.GetOrdinal("status")),
                    Narrative: reader.GetString(reader.GetOrdinal("narrative")),
                    TableAction: reader.GetString(reader.GetOrdinal("table_action")),
                    ActionUser: ReadNullableString(reader, "action_user"),
                    ActionTakenAt: ReadNullableString(reader, "action_taken_at"),
                    Checksum: reader.GetString(reader.GetOrdinal("checksum")),
                    RequestedDemographics: demographics));
            }
        }

        return new AdministrationPortalActivitySummary(
            WaitingAuditCount: waitingAuditCount,
            WaitingProfileReviewCount: waitingProfileReviewCount,
            ProfileReviewRequests: requests);
    }

    private async Task<int> GetNextStaffIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from staff;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User ID allocation failed."));
    }

    private async Task<int> GetNextFacilityIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from facilities;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility ID allocation failed."));
    }

    private static void AddUserParameters(
        NpgsqlCommand command,
        AdministrationUserMutationRequest request,
        bool defaultActive)
    {
        var username = NormalizeRequired(request.Username, "Username");
        var role = NormalizeRequired(request.Role, "Role").ToLowerInvariant();
        command.Parameters.Add("username", NpgsqlDbType.Text).Value = username;
        command.Parameters.Add("firstName", NpgsqlDbType.Text).Value = NormalizeRequired(request.FirstName, "First name");
        command.Parameters.Add("lastName", NpgsqlDbType.Text).Value = NormalizeRequired(request.LastName, "Last name");
        command.Parameters.Add("role", NpgsqlDbType.Text).Value = role;
        command.Parameters.Add("calendar", NpgsqlDbType.Boolean).Value = request.Calendar ?? string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);
        AddNullableInt(command, "facilityId", request.FacilityId);
        AddNullableText(command, "email", request.Email ?? $"{username}@{DefaultUserEmailDomain}");
        AddNullableText(command, "npi", request.Npi);
        command.Parameters.Add("active", NpgsqlDbType.Boolean).Value = request.Active ?? defaultActive;
    }

    private static void AddFacilityParameters(
        NpgsqlCommand command,
        AdministrationFacilityMutationRequest request,
        bool defaultActive)
    {
        command.Parameters.Add("code", NpgsqlDbType.Text).Value = NormalizeRequired(request.Code, "Facility code");
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = NormalizeRequired(request.Name, "Facility name");
        AddNullableText(command, "phone", request.Phone);
        AddNullableText(command, "street", request.Street);
        AddNullableText(command, "city", request.City);
        AddNullableText(command, "state", request.State);
        AddNullableText(command, "postalCode", request.PostalCode);
        command.Parameters.Add("color", NpgsqlDbType.Text).Value = NormalizeOptional(request.Color) ?? DefaultFacilityColor;
        command.Parameters.Add("inactive", NpgsqlDbType.Boolean).Value = !(request.Active ?? defaultActive);
    }

    private static async Task<bool> AccessGroupExistsAsync(
        NpgsqlConnection connection,
        string groupValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
                select 1
                from access_groups
                where value = @groupValue
            );
            """;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<string?> GetAccessGroupNameAsync(
        NpgsqlConnection connection,
        string groupValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select name
            from access_groups
            where value = @groupValue
            limit 1;
            """;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<AccessStaffUser?> GetStaffAccessUserAsync(
        NpgsqlConnection connection,
        string userValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, username, first_name, last_name
            from staff
            where lower(username) = @userValue
            limit 1;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = userValue;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        return new AccessStaffUser(
            StaffId: reader.GetInt32(reader.GetOrdinal("id")),
            UserValue: reader.GetString(reader.GetOrdinal("username")),
            UserName: $"{lastName}, {firstName}");
    }

    private static async Task<string?> GetAccessPermissionNameAsync(
        NpgsqlConnection connection,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select name
            from access_permissions
            where section_value = @sectionValue
              and value = @permissionValue
            limit 1;
            """;
        command.Parameters.Add("sectionValue", NpgsqlDbType.Text).Value = sectionValue;
        command.Parameters.Add("permissionValue", NpgsqlDbType.Text).Value = permissionValue;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static void AddAccessAssignmentKeys(
        NpgsqlCommand command,
        string groupValue,
        string sectionValue,
        string permissionValue)
    {
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        command.Parameters.Add("sectionValue", NpgsqlDbType.Text).Value = sectionValue;
        command.Parameters.Add("permissionValue", NpgsqlDbType.Text).Value = permissionValue;
    }

    private static string NormalizeAccessToken(string? value, string label)
    {
        return NormalizeRequired(value, label).ToLowerInvariant();
    }

    private static string NormalizeAccessReturnValue(string? value)
    {
        var returnValue = NormalizeAccessToken(value, "Return value");
        return ValidAccessReturnValues.Contains(returnValue)
            ? returnValue
            : throw new ArgumentException($"Return value '{returnValue}' is not supported.");
    }

    private static PatientPortalProfileDemographics EmptyRequestedDemographics() =>
        new(
            FirstName: string.Empty,
            LastName: string.Empty,
            PreferredName: null,
            DateOfBirth: null,
            Sex: null,
            Email: null,
            Street: null,
            City: null,
            State: null,
            PostalCode: null,
            PhoneHome: null,
            PhoneCell: null,
            PhoneContact: null,
            HipaaAllowSms: null,
            HipaaAllowEmail: null,
            ContactRelationship: null,
            MotherName: null,
            GuardianName: null,
            GuardianRelationship: null,
            GuardianPhone: null,
            GuardianEmail: null);

    private static string? NormalizePermission(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) ? "YES" : "NO";
    }

    private static string NormalizeRequired(string? value, string label)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{label} is required.")
            : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = NormalizeOptional(value) is { } normalized
            ? normalized
            : DBNull.Value;
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value = value is { } integer
            ? integer
            : DBNull.Value;
    }

    private static void AddNullableDate(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Date).Value =
            DateOnly.TryParse(value, out var date) ? date : (object)DBNull.Value;
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

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record AccessStaffUser(int StaffId, string UserValue, string UserName);
}
