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
            select source_id,display_name,active,created_at,created_by,
                   deactivated_at,deactivated_by,deactivation_reason
            from external_laboratory_sources
            order by active desc,display_name,source_id;
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
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);
        var salt = RandomNumberGenerator.GetBytes(ApiKeySaltBytes);
        var hash = DeriveApiKeyHash(apiKey, salt, ApiKeyIterations);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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

            var source = ReadSource(reader);
            await reader.DisposeAsync();
            await InsertEventAsync(connection, transaction, sourceId, "created", normalizedActor, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return source;
        }
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
                      deactivated_at,deactivated_by,deactivation_reason;
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
            select display_name,api_key_salt,api_key_hash,api_key_iterations
            from external_laboratory_sources
            where source_id=@sourceId
              and active=true;
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
        var actualHash = DeriveApiKeyHash(apiKey, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)
            ? new ExternalLaboratorySourceAuthentication(normalizedSourceId, displayName)
            : null;
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

    private static ExternalLaboratorySourceItem ReadSource(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetBoolean(2),
        reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7));

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
