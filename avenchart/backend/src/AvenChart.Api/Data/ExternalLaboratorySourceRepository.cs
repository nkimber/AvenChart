// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using AvenChart.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Data;

/// <summary>
/// Owns the service-identity registry for the synthetic external-laboratory
/// boundary. Credentials are accepted only at create time and stored as salted
/// PBKDF2 verifiers; callers can never read a usable credential back.
/// </summary>
public sealed class ExternalLaboratorySourceRepository(NpgsqlDataSource dataSource)
{
    private const int ApiKeyIterations = 310_000;
    private const int ApiKeySaltBytes = 16;
    private const int ApiKeyHashBytes = 32;

    public async Task<IReadOnlyList<ExternalLaboratorySourceItem>> GetSourcesAsync(
        CancellationToken cancellationToken)
    {
        var sources = new List<ExternalLaboratorySourceItem>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_id,sources.display_name,sources.active,sources.created_at,sources.created_by,
                   sources.deactivated_at,sources.deactivated_by,sources.deactivation_reason,
                   coalesce(array_agg(grants.facility_id order by grants.facility_id) filter (where grants.active), array[]::integer[]) as facility_ids
            from external_laboratory_sources sources
            left join external_laboratory_source_facility_grants grants on grants.source_id=sources.source_id
            group by sources.source_id,sources.display_name,sources.active,sources.created_at,sources.created_by,
                     sources.deactivated_at,sources.deactivated_by,sources.deactivation_reason
            order by sources.active desc,sources.display_name,sources.source_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(ReadSource(reader));
        }

        return sources;
    }

    public async Task<ExternalLaboratorySourceItem> CreateSourceAsync(
        ExternalLaboratorySourceCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var sourceId = NormalizeSourceId(request.SourceId);
        var displayName = NormalizeRequired(request.DisplayName, "Display name", 160);
        var apiKey = RequireApiKey(request.ApiKey);
        var facilityIds = NormalizeFacilityIds(request.FacilityIds);
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);
        var salt = RandomNumberGenerator.GetBytes(ApiKeySaltBytes);
        var hash = DeriveApiKeyHash(apiKey, salt, ApiKeyIterations);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureFacilitiesActiveAsync(connection, transaction, facilityIds, cancellationToken);
        ExternalLaboratorySourceItem source;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into external_laboratory_sources
                  (source_id,display_name,api_key_salt,api_key_hash,api_key_iterations,active,created_at,created_by)
                values
                  (@sourceId,@displayName,@salt,@hash,@iterations,true,now(),@actor)
                returning source_id,display_name,active,created_at,created_by,
                          deactivated_at,deactivated_by,deactivation_reason;
                """;
            insert.Parameters.AddWithValue("sourceId", sourceId);
            insert.Parameters.AddWithValue("displayName", displayName);
            insert.Parameters.Add("salt", NpgsqlDbType.Bytea).Value = salt;
            insert.Parameters.Add("hash", NpgsqlDbType.Bytea).Value = hash;
            insert.Parameters.AddWithValue("iterations", ApiKeyIterations);
            insert.Parameters.AddWithValue("actor", normalizedActor);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The external laboratory source could not be created.");
            }

            source = ReadSource(reader, facilityIds);
            await reader.DisposeAsync();
        }
        foreach (var facilityId in facilityIds)
        {
            await InsertFacilityGrantAsync(connection, transaction, sourceId, facilityId, normalizedActor, cancellationToken);
        }
        await InsertEventAsync(connection, transaction, sourceId, "created", normalizedActor, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return source;
    }

    public async Task<ExternalLaboratorySourceItem?> DeactivateSourceAsync(
        string sourceId,
        ExternalLaboratorySourceDeactivateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedSourceId = NormalizeSourceId(sourceId);
        var reason = NormalizeRequired(request.Reason, "Deactivation reason", 500);
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            update external_laboratory_sources
            set active=false,
                deactivated_at=now(),
                deactivated_by=@actor,
                deactivation_reason=@reason
            where source_id=@sourceId
              and active=true
            returning source_id,display_name,active,created_at,created_by,
                      deactivated_at,deactivated_by,deactivation_reason,
                      (select coalesce(array_agg(facility_id order by facility_id) filter (where active), array[]::integer[])
                       from external_laboratory_source_facility_grants
                       where source_id=external_laboratory_sources.source_id) as facility_ids;
            """;
        update.Parameters.AddWithValue("sourceId", normalizedSourceId);
        update.Parameters.AddWithValue("actor", normalizedActor);
        update.Parameters.AddWithValue("reason", reason);
        await using var reader = await update.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var source = ReadSource(reader);
        await reader.DisposeAsync();
        await InsertEventAsync(connection, transaction, normalizedSourceId, "deactivated", normalizedActor, reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return source;
    }

    public async Task<ExternalLaboratorySourceItem?> ReplaceFacilityGrantsAsync(
        string sourceId,
        ExternalLaboratorySourceFacilityGrantUpdateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var normalizedSourceId = NormalizeSourceId(sourceId);
        var facilityIds = NormalizeFacilityIds(request.FacilityIds);
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureFacilitiesActiveAsync(connection, transaction, facilityIds, cancellationToken);
        await using (var sourceLock = connection.CreateCommand())
        {
            sourceLock.Transaction = transaction;
            sourceLock.CommandText = "select source_id from external_laboratory_sources where source_id=@sourceId and active=true for update;";
            sourceLock.Parameters.AddWithValue("sourceId", normalizedSourceId);
            if (await sourceLock.ExecuteScalarAsync(cancellationToken) is null) return null;
        }

        var previousFacilityIds = new List<int>();
        await using (var current = connection.CreateCommand())
        {
            current.Transaction = transaction;
            current.CommandText = "select facility_id from external_laboratory_source_facility_grants where source_id=@sourceId and active=true for update;";
            current.Parameters.AddWithValue("sourceId", normalizedSourceId);
            await using var reader = await current.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) previousFacilityIds.Add(reader.GetInt32(0));
        }

        foreach (var facilityId in previousFacilityIds.Except(facilityIds))
        {
            await using var revoke = connection.CreateCommand();
            revoke.Transaction = transaction;
            revoke.CommandText = """
                update external_laboratory_source_facility_grants
                set active=false,revoked_at=now(),revoked_by=@actor
                where source_id=@sourceId and facility_id=@facilityId and active=true;
                """;
            revoke.Parameters.AddWithValue("sourceId", normalizedSourceId);
            revoke.Parameters.AddWithValue("facilityId", facilityId);
            revoke.Parameters.AddWithValue("actor", normalizedActor);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
            await InsertFacilityEventAsync(connection, transaction, normalizedSourceId, facilityId, "revoked", normalizedActor, cancellationToken);
        }
        foreach (var facilityId in facilityIds.Except(previousFacilityIds))
        {
            await InsertFacilityGrantAsync(connection, transaction, normalizedSourceId, facilityId, normalizedActor, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetSourceAsync(normalizedSourceId, cancellationToken);
    }

    public async Task<ExternalLaboratorySourceAuthentication?> AuthenticateAsync(
        string? sourceId,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeSourceId(sourceId, out var normalizedSourceId)
            || string.IsNullOrWhiteSpace(apiKey)
            || apiKey.Length is < 32 or > 512)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.display_name,sources.api_key_salt,sources.api_key_hash,sources.api_key_iterations,
                   coalesce(array_agg(grants.facility_id order by grants.facility_id) filter (where grants.active), array[]::integer[])
            from external_laboratory_sources sources
            left join external_laboratory_source_facility_grants grants on grants.source_id=sources.source_id
            where sources.source_id=@sourceId and sources.active=true
            group by sources.display_name,sources.api_key_salt,sources.api_key_hash,sources.api_key_iterations;
            """;
        command.Parameters.AddWithValue("sourceId", normalizedSourceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var displayName = reader.GetString(0);
        var salt = reader.GetFieldValue<byte[]>(1);
        var expectedHash = reader.GetFieldValue<byte[]>(2);
        var iterations = reader.GetInt32(3);
        var facilityIds = reader.GetFieldValue<int[]>(4);
        var actualHash = DeriveApiKeyHash(apiKey, salt, iterations);
        return facilityIds.Length > 0 && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)
            ? new ExternalLaboratorySourceAuthentication(normalizedSourceId, displayName, facilityIds)
            : null;
    }

    private async Task<ExternalLaboratorySourceItem?> GetSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select sources.source_id,sources.display_name,sources.active,sources.created_at,sources.created_by,
                   sources.deactivated_at,sources.deactivated_by,sources.deactivation_reason,
                   coalesce(array_agg(grants.facility_id order by grants.facility_id) filter (where grants.active), array[]::integer[]) as facility_ids
            from external_laboratory_sources sources
            left join external_laboratory_source_facility_grants grants on grants.source_id=sources.source_id
            where sources.source_id=@sourceId
            group by sources.source_id,sources.display_name,sources.active,sources.created_at,sources.created_by,
                     sources.deactivated_at,sources.deactivated_by,sources.deactivation_reason;
            """;
        command.Parameters.AddWithValue("sourceId", sourceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSource(reader) : null;
    }

    private static async Task EnsureFacilitiesActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<int> facilityIds, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*)::integer from facilities where id=any(@facilityIds) and inactive=false;";
        command.Parameters.Add("facilityIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = facilityIds.ToArray();
        if ((int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0) != facilityIds.Count)
        {
            throw new ArgumentException("Every external laboratory source facility must exist and be active.");
        }
    }

    private static async Task InsertFacilityGrantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sourceId, int facilityId, string actor, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into external_laboratory_source_facility_grants(source_id,facility_id,active,granted_at,granted_by,revoked_at,revoked_by)
            values(@sourceId,@facilityId,true,now(),@actor,null,null)
            on conflict(source_id,facility_id) do update
            set active=true,granted_at=now(),granted_by=excluded.granted_by,revoked_at=null,revoked_by=null;
            """;
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertFacilityEventAsync(connection, transaction, sourceId, facilityId, "granted", actor, cancellationToken);
    }

    private static async Task InsertFacilityEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sourceId, int facilityId, string action, string actor, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into external_laboratory_source_facility_events(source_id,facility_id,action,actor,occurred_at) values(@sourceId,@facilityId,@action,@actor,now());";
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId,
        string action,
        string actor,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into external_laboratory_source_events(event_id,source_id,action,actor,reason,occurred_at)
            values(@eventId,@sourceId,@action,@actor,@reason,now());
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = (object?)reason ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ExternalLaboratorySourceItem ReadSource(NpgsqlDataReader reader, IReadOnlyList<int>? suppliedFacilityIds = null) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetBoolean(2),
        reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        suppliedFacilityIds ?? reader.GetFieldValue<int[]>(8));

    private static string NormalizeSourceId(string? sourceId)
    {
        if (!TryNormalizeSourceId(sourceId, out var normalized))
        {
            throw new ArgumentException("Source ID must be 3-80 lowercase letters, digits, or hyphens, and cannot begin or end with a hyphen.");
        }

        return normalized;
    }

    private static bool TryNormalizeSourceId(string? sourceId, out string normalized)
    {
        normalized = sourceId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is < 3 or > 80
            || normalized[0] == '-'
            || normalized[^1] == '-')
        {
            return false;
        }

        return normalized.All(character =>
            char.IsAsciiLetterLower(character)
            || char.IsAsciiDigit(character)
            || character == '-');
    }

    private static string RequireApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length is < 32 or > 512)
        {
            throw new ArgumentException("API key must contain 32-512 characters.");
        }

        return apiKey;
    }

    private static int[] NormalizeFacilityIds(IReadOnlyList<int>? facilityIds)
    {
        var normalized = (facilityIds ?? []).Distinct().OrderBy(id => id).ToArray();
        if (normalized.Length == 0 || normalized.Any(id => id <= 0))
        {
            throw new ArgumentException("At least one valid facility grant is required for an external laboratory source.");
        }
        return normalized;
    }

    private static string NormalizeRequired(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{label} is required and must not exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static byte[] DeriveApiKeyHash(string apiKey, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(apiKey),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            ApiKeyHashBytes);
}
