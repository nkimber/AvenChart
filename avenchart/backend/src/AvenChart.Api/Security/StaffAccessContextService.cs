// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Security;

/// <summary>
/// Resolves the explicitly declared facility and purpose of use for a staff
/// request. It is deliberately separate from authentication so an external
/// identity adapter can supply the principal without weakening this boundary.
/// </summary>
public sealed class StaffAccessContextService(NpgsqlDataSource dataSource)
{
    public const string FacilityHeader = "X-AvenChart-Facility-Id";
    public const string PurposeHeader = "X-AvenChart-Purpose-Of-Use";
    public const string HttpContextItemKey = "staffAccessContext";

    private static readonly string[] AllowedPurposes =
    [
        "treatment",
        "payment",
        "healthcare-operations",
    ];

    public async Task<AuthAccessContextResponse> GetAvailableAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAvailableAsync(connection, null, normalizedUsername, cancellationToken);
    }

    public async Task<StaffAccessContextResolution> ResolveAsync(
        AuthSessionResponse session,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!session.Authenticated || string.IsNullOrWhiteSpace(session.Username))
        {
            return StaffAccessContextResolution.Denied("An authenticated staff identity is required.");
        }

        var available = await GetAvailableAsync(session.Username, cancellationToken);
        if (available.Facilities.Count == 0)
        {
            return StaffAccessContextResolution.Denied("This staff identity does not have an active facility grant.");
        }

        var purpose = NormalizePurpose(httpContext.Request.Headers[PurposeHeader].ToString());
        if (purpose is null)
        {
            return StaffAccessContextResolution.Denied($"A valid {PurposeHeader} header is required.");
        }
        if (!available.Purposes.Contains(purpose, StringComparer.Ordinal))
        {
            return StaffAccessContextResolution.Denied("The declared purpose of use is not granted to this staff identity.");
        }

        var facilityHeader = httpContext.Request.Headers[FacilityHeader].ToString();
        if (!int.TryParse(facilityHeader, out var facilityId) || facilityId <= 0)
        {
            return StaffAccessContextResolution.Denied($"A valid {FacilityHeader} header is required.");
        }

        var facility = available.Facilities.SingleOrDefault(candidate => candidate.FacilityId == facilityId);
        if (facility is null)
        {
            return StaffAccessContextResolution.Denied("The requested facility is not granted to this staff identity.");
        }

        return StaffAccessContextResolution.Allowed(new StaffAccessContext(
            facility.FacilityId,
            facility.Code,
            facility.Name,
            purpose));
    }

    /// <summary>
    /// Tests whether a patient identifier resolves to a patient owned by the
    /// facility already selected and granted for the request. A false result is
    /// intentionally indistinguishable from a missing patient at the route
    /// boundary to avoid confirming cross-facility identities.
    /// </summary>
    public async Task<bool> CanAccessPatientAsync(
        string? patientIdentifier,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var normalizedIdentifier = patientIdentifier?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdentifier)
            || normalizedIdentifier.Length > 128
            || facilityId <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
              select 1
              from patients
              where facility_id=@facility
                and (lower(canonical_id)=lower(@patientIdentifier)
                     or lower(pubpid)=lower(@patientIdentifier)
                     or legacy_pid::text=@patientIdentifier));
            """;
        command.Parameters.AddWithValue("facility", facilityId);
        command.Parameters.AddWithValue("patientIdentifier", normalizedIdentifier);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> CanAccessInsuranceAsync(
        string? insuranceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var normalizedInsuranceId = insuranceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedInsuranceId)
            || normalizedInsuranceId.Length > 128
            || facilityId <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
              select 1
              from insurance_records insurance
              join patients patient on patient.canonical_id=insurance.patient_id
              where insurance.id=@insuranceId
                and patient.facility_id=@facility
                and patient.merged_into_patient_id is null);
            """;
        command.Parameters.AddWithValue("insuranceId", normalizedInsuranceId);
        command.Parameters.AddWithValue("facility", facilityId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    /// <summary>
    /// Resolves a document through its patient before permitting a direct
    /// document route. Document identifiers are otherwise globally enumerable
    /// implementation details and must never be treated as authorization.
    /// </summary>
    public Task<bool> CanAccessDocumentAsync(
        int documentId,
        int facilityId,
        CancellationToken cancellationToken) =>
        CanAccessPatientResourceAsync(
            """
            select exists(
              select 1
              from patient_documents document
              join patients patient on patient.canonical_id=document.patient_id
              where document.id=@resourceId
                and patient.facility_id=@facility
                and patient.merged_into_patient_id is null);
            """,
            documentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            facilityId,
            cancellationToken);

    /// <summary>
    /// Resolves an encounter through its patient before permitting a direct
    /// encounter route. This protects notes, forms, signatures, and attached
    /// records exposed from an encounter identifier.
    /// </summary>
    public Task<bool> CanAccessEncounterAsync(
        int encounter,
        int facilityId,
        CancellationToken cancellationToken) =>
        CanAccessPatientResourceAsync(
            """
            select exists(
              select 1
              from encounters encounter
              join patients patient on patient.legacy_pid=encounter.pid
              where encounter.encounter=@resourceId::integer
                and patient.facility_id=@facility
                and patient.merged_into_patient_id is null);
            """,
            encounter.ToString(System.Globalization.CultureInfo.InvariantCulture),
            facilityId,
            cancellationToken);

    /// <summary>
    /// Resolves a recurring appointment occurrence to its series root and
    /// then to the owning patient. A virtual-occurrence suffix cannot widen
    /// the facility scope of the underlying appointment.
    /// </summary>
    public Task<bool> CanAccessAppointmentAsync(
        string? appointmentId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        var normalized = appointmentId?.Trim();
        var separator = normalized?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        if (separator > 0)
        {
            normalized = normalized![..separator];
        }

        return CanAccessPatientResourceAsync(
            """
            select exists(
              select 1
              from appointments appointment
              join patients patient on patient.legacy_pid=appointment.pid
              where appointment.id=@resourceId
                and patient.facility_id=@facility
                and patient.merged_into_patient_id is null);
            """,
            normalized,
            facilityId,
            cancellationToken);
    }

    public async Task<AuthAccessContextGrantResponse> GetPrincipalGrantAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureAccountExistsAsync(connection, null, normalizedUsername, cancellationToken);
        var available = await ReadAvailableAsync(connection, null, normalizedUsername, cancellationToken);
        await using var metadataCommand = connection.CreateCommand();
        metadataCommand.CommandText = """
            select greatest(
                     coalesce((select max(updated_at) from auth_principal_facility_grants where username=@username), now()),
                     coalesce((select max(updated_at) from auth_principal_purpose_of_use_grants where username=@username), now())),
                   coalesce((
                     select updated_by
                     from auth_principal_facility_grants
                     where username=@username
                     order by updated_at desc,facility_id
                     limit 1), 'unknown');
            """;
        metadataCommand.Parameters.AddWithValue("username", normalizedUsername);
        await using var reader = await metadataCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The access-context grant metadata could not be loaded.");
        }

        return new AuthAccessContextGrantResponse(
            normalizedUsername,
            available.Facilities,
            available.Purposes,
            reader.GetFieldValue<DateTimeOffset>(0).ToString("O"),
            reader.GetString(1));
    }

    public async Task<AuthAccessContextGrantResponse> UpdatePrincipalGrantAsync(
        string username,
        AuthAccessContextGrantUpdateRequest request,
        string changedBy,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        var normalizedChangedBy = NormalizeUsername(changedBy);
        var facilityIds = (request.FacilityIds ?? [])
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var purposes = (request.Purposes ?? [])
            .Select(NormalizePurpose)
            .Where(purpose => purpose is not null)
            .Select(purpose => purpose!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(purpose => purpose, StringComparer.Ordinal)
            .ToArray();

        if (facilityIds.Length == 0 || facilityIds.Any(id => id <= 0))
        {
            throw new ArgumentException("At least one valid facility grant is required.");
        }
        if (!facilityIds.Contains(request.DefaultFacilityId))
        {
            throw new ArgumentException("The default facility must be one of the granted facilities.");
        }
        if (purposes.Length == 0 || (request.Purposes ?? []).Any(purpose => NormalizePurpose(purpose) is null))
        {
            throw new ArgumentException("At least one supported purpose of use is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureAccountExistsAsync(connection, transaction, normalizedUsername, cancellationToken);
        await EnsureFacilitiesActiveAsync(connection, transaction, facilityIds, cancellationToken);

        await ExecuteAsync(
            connection,
            transaction,
            "delete from auth_principal_facility_grants where username=@username;",
            command => command.Parameters.AddWithValue("username", normalizedUsername),
            cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            "delete from auth_principal_purpose_of_use_grants where username=@username;",
            command => command.Parameters.AddWithValue("username", normalizedUsername),
            cancellationToken);

        foreach (var facilityId in facilityIds)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_at,granted_by,updated_at,updated_by)
                values(@username,@facility,@default,true,now(),@changedBy,now(),@changedBy);
                """,
                command =>
                {
                    command.Parameters.AddWithValue("username", normalizedUsername);
                    command.Parameters.AddWithValue("facility", facilityId);
                    command.Parameters.AddWithValue("default", facilityId == request.DefaultFacilityId);
                    command.Parameters.AddWithValue("changedBy", normalizedChangedBy);
                },
                cancellationToken);
        }

        foreach (var purpose in purposes)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_at,granted_by,updated_at,updated_by)
                values(@username,@purpose,true,now(),@changedBy,now(),@changedBy);
                """,
                command =>
                {
                    command.Parameters.AddWithValue("username", normalizedUsername);
                    command.Parameters.AddWithValue("purpose", purpose);
                    command.Parameters.AddWithValue("changedBy", normalizedChangedBy);
                },
                cancellationToken);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            insert into auth_access_context_grant_events(event_id,occurred_at,username,action,facility_ids,default_facility_id,purposes,changed_by)
            values(@event,now(),@username,'updated',@facilities,@defaultFacility,@purposes,@changedBy);
            """,
            command =>
            {
                command.Parameters.AddWithValue("event", Guid.NewGuid());
                command.Parameters.AddWithValue("username", normalizedUsername);
                command.Parameters.Add("facilities", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = facilityIds;
                command.Parameters.AddWithValue("defaultFacility", request.DefaultFacilityId);
                command.Parameters.Add("purposes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = purposes;
                command.Parameters.AddWithValue("changedBy", normalizedChangedBy);
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetPrincipalGrantAsync(normalizedUsername, cancellationToken);
    }

    private static async Task<AuthAccessContextResponse> ReadAvailableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string username,
        CancellationToken cancellationToken)
    {
        var facilities = new List<AuthAccessFacilityItem>();
        await using (var facilityCommand = connection.CreateCommand())
        {
            facilityCommand.Transaction = transaction;
            facilityCommand.CommandText = """
                select facility.id,facility.code,facility.name,facility_grant.is_default
                from auth_principal_facility_grants facility_grant
                join facilities facility on facility.id=facility_grant.facility_id and facility.inactive=false
                where facility_grant.username=@username and facility_grant.active=true
                order by facility_grant.is_default desc,facility.name,facility.id;
                """;
            facilityCommand.Parameters.AddWithValue("username", username);
            await using var reader = await facilityCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                facilities.Add(new AuthAccessFacilityItem(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3)));
            }
        }

        var purposes = new List<string>();
        await using (var purposeCommand = connection.CreateCommand())
        {
            purposeCommand.Transaction = transaction;
            purposeCommand.CommandText = """
                select purpose_of_use
                from auth_principal_purpose_of_use_grants
                where username=@username and active=true
                order by case purpose_of_use
                  when 'treatment' then 1
                  when 'payment' then 2
                  else 3
                end;
                """;
            purposeCommand.Parameters.AddWithValue("username", username);
            await using var reader = await purposeCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                purposes.Add(reader.GetString(0));
            }
        }

        return new AuthAccessContextResponse(
            facilities.FirstOrDefault(candidate => candidate.IsDefault)?.FacilityId ?? facilities.FirstOrDefault()?.FacilityId,
            purposes.FirstOrDefault() ?? string.Empty,
            facilities,
            purposes);
    }

    private async Task<bool> CanAccessPatientResourceAsync(
        string commandText,
        string? resourceId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId)
            || resourceId.Length > 128
            || facilityId <= 0)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("resourceId", resourceId);
        command.Parameters.AddWithValue("facility", facilityId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task EnsureAccountExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from auth_accounts where username=@username and active=true);";
        command.Parameters.AddWithValue("username", username);
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException("The requested active account was not found.");
        }
    }

    private static async Task EnsureFacilitiesActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<int> facilityIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*)::integer from facilities where id=any(@facilities) and inactive=false;";
        command.Parameters.Add("facilities", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = facilityIds.ToArray();
        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count != facilityIds.Count)
        {
            throw new ArgumentException("Every granted facility must exist and be active.");
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Action<NpgsqlCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        configure(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeUsername(string? username)
    {
        var normalized = username?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
        {
            throw new ArgumentException("A valid username is required.");
        }
        return normalized;
    }

    private static string? NormalizePurpose(string? purpose)
    {
        var normalized = purpose?.Trim().ToLowerInvariant();
        return normalized is not null && AllowedPurposes.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : null;
    }
}

public sealed record StaffAccessContext(
    int FacilityId,
    string FacilityCode,
    string FacilityName,
    string PurposeOfUse);

public sealed record StaffAccessContextResolution(
    bool Authorized,
    StaffAccessContext? Context,
    string? FailureReason)
{
    public static StaffAccessContextResolution Allowed(StaffAccessContext context) => new(true, context, null);

    public static StaffAccessContextResolution Denied(string failureReason) => new(false, null, failureReason);
}
