// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Infrastructure;

public sealed class SchemaMigrationState(
    NpgsqlDataSource dataSource,
    SchemaMigrationCatalog catalog,
    DatabaseBootstrapCatalog bootstrap,
    ILogger<SchemaMigrationState> logger)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private SchemaMigrationValidationResult? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<SchemaMigrationValidationResult> ValidateAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var cached = Volatile.Read(ref _cached);
        if (!forceRefresh && cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            cached = _cached;
            if (!forceRefresh && cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
            {
                return cached;
            }

            var result = await ReadStateAsync(cancellationToken);
            _cachedAt = DateTimeOffset.UtcNow;
            Volatile.Write(ref _cached, result);
            return result;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        _cachedAt = DateTimeOffset.MinValue;
        Volatile.Write(ref _cached, null);
    }

    private async Task<SchemaMigrationValidationResult> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (catalog.Error is not null)
        {
            return SchemaMigrationValidationResult.Invalid(
                catalog,
                0,
                catalog.Error,
                [],
                [],
                []);
        }
        if (bootstrap.Error is not null)
        {
            return SchemaMigrationValidationResult.Invalid(
                catalog,
                0,
                bootstrap.Error,
                [],
                [],
                []);
        }

        try
        {
            var applied = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var baseSchema = await bootstrap.GetPresenceAsync(connection, cancellationToken);
            if (!baseSchema.IsComplete)
            {
                return SchemaMigrationValidationResult.Invalid(
                    catalog,
                    0,
                    $"The database base schema is incomplete. Missing anchor tables: {string.Join(", ", baseSchema.Missing)}.",
                    [],
                    [],
                    []);
            }
            await using var command = connection.CreateCommand();
            command.CommandText = "select migration_id, checksum_sha256 from schema_migrations order by migration_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetString(0), reader.GetString(1));
            }

            var missing = catalog.Migrations
                .Where(migration => !applied.ContainsKey(migration.Id))
                .Select(migration => migration.Id)
                .ToArray();
            var unexpected = applied.Keys
                .Where(migrationId => !catalog.ById.ContainsKey(migrationId))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var checksumMismatches = catalog.Migrations
                .Where(migration => applied.TryGetValue(migration.Id, out var checksum)
                    && !catalog.IsAcceptedLedgerChecksum(migration, checksum))
                .Select(migration => migration.Id)
                .ToArray();

            return missing.Length > 0 || unexpected.Length > 0 || checksumMismatches.Length > 0
                ? SchemaMigrationValidationResult.Invalid(
                    catalog,
                    applied.Count,
                    "The database migration ledger does not match the migrations packaged with this API.",
                    missing,
                    unexpected,
                    checksumMismatches)
                : SchemaMigrationValidationResult.Valid(catalog, applied.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Schema migration state validation failed.");
            return SchemaMigrationValidationResult.Invalid(
                catalog,
                0,
                "The database migration ledger could not be validated.",
                [],
                [],
                []);
        }
    }
}

public sealed record SchemaMigrationValidationResult(
    bool IsReady,
    string Description,
    string MigrationsPath,
    int ExpectedCount,
    int AppliedCount,
    string LatestExpected,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Unexpected,
    IReadOnlyList<string> ChecksumMismatches)
{
    public IReadOnlyDictionary<string, object> Details => new Dictionary<string, object>
    {
        ["migrationsPath"] = MigrationsPath,
        ["expected"] = ExpectedCount,
        ["applied"] = AppliedCount,
        ["latestExpected"] = LatestExpected,
        ["missing"] = Missing,
        ["unexpected"] = Unexpected,
        ["checksumMismatches"] = ChecksumMismatches
    };

    public static SchemaMigrationValidationResult Valid(SchemaMigrationCatalog catalog, int appliedCount) =>
        new(
            true,
            $"The database migration ledger matches all {catalog.Migrations.Count} packaged migrations.",
            catalog.MigrationsPath,
            catalog.Migrations.Count,
            appliedCount,
            catalog.Migrations.LastOrDefault()?.Id ?? string.Empty,
            [],
            [],
            []);

    public static SchemaMigrationValidationResult Invalid(
        SchemaMigrationCatalog catalog,
        int appliedCount,
        string description,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> unexpected,
        IReadOnlyList<string> checksumMismatches) =>
        new(
            false,
            description,
            catalog.MigrationsPath,
            catalog.Migrations.Count,
            appliedCount,
            catalog.Migrations.LastOrDefault()?.Id ?? string.Empty,
            missing,
            unexpected,
            checksumMismatches);
}
