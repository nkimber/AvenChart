// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Infrastructure;

public sealed class DatabaseSchemaMigrator(
    NpgsqlDataSource dataSource,
    SchemaMigrationCatalog catalog,
    DatabaseBootstrapCatalog bootstrap,
    SchemaMigrationState state,
    IConfiguration configuration,
    ILogger<DatabaseSchemaMigrator> logger)
{
    private const long AdvisoryLockId = 67531924012026001;

    public async Task<SchemaMigrationRunResult> MigrateAsync(CancellationToken cancellationToken)
    {
        if (catalog.Error is not null)
        {
            throw new InvalidOperationException(catalog.Error);
        }
        if (bootstrap.Error is not null)
        {
            throw new InvalidOperationException(bootstrap.Error);
        }

        var faultAfterAppliedCount = configuration.GetValue<int?>("DatabaseSchema:FaultAfterAppliedMigrationCount") ?? 0;
        var allowFaultInjection = configuration.GetValue<bool>("DatabaseSchema:AllowTestFaultInjection");
        if (faultAfterAppliedCount < 0)
        {
            throw new InvalidOperationException("DatabaseSchema:FaultAfterAppliedMigrationCount cannot be negative.");
        }

        if (faultAfterAppliedCount > 0 && !allowFaultInjection)
        {
            throw new InvalidOperationException(
                "Migration fault injection is disabled. Set DatabaseSchema:AllowTestFaultInjection only in an isolated test database.");
        }

        var applied = new List<string>();
        var alreadyApplied = new List<string>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (faultAfterAppliedCount > 0 && !IsIsolatedTestDatabase(connection.Database))
        {
            throw new InvalidOperationException(
                "Migration fault injection is restricted to an avenchart_test_* database.");
        }

        await AcquireLockAsync(connection, cancellationToken);
        try
        {
            await EnsureBaseSchemaAsync(connection, bootstrap, cancellationToken);
            var ledger = await BootstrapLedgerAsync(connection, cancellationToken);
            ValidateExistingLedger(ledger);

            foreach (var migration in catalog.Migrations)
            {
                if (ledger.TryGetValue(migration.Id, out var checksum))
                {
                    if (!string.Equals(checksum, migration.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Migration drift detected for '{migration.Id}'. The applied checksum does not match the packaged SQL file.");
                    }

                    alreadyApplied.Add(migration.Id);
                    continue;
                }

                await ApplyMigrationAsync(connection, migration, cancellationToken);
                ledger.Add(migration.Id, migration.ChecksumSha256);
                applied.Add(migration.Id);
                logger.LogDebug("Applied database migration {MigrationId}.", migration.Id);

                if (faultAfterAppliedCount > 0 && applied.Count == faultAfterAppliedCount)
                {
                    throw new SchemaMigrationFaultInjectionException(migration.Id, applied.Count);
                }
            }

            state.Invalidate();
            var finalState = await state.ValidateAsync(true, cancellationToken);
            if (!finalState.IsReady)
            {
                throw new InvalidOperationException($"Migration run completed but exact schema readiness failed: {finalState.Description}");
            }

            return new SchemaMigrationRunResult(applied, alreadyApplied, finalState.ExpectedCount);
        }
        finally
        {
            await ReleaseLockAsync(connection);
        }
    }

    private async Task<Dictionary<string, string>> BootstrapLedgerAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var bootstrap = catalog.Migrations[0];
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var bootstrapCommand = connection.CreateCommand())
        {
            bootstrapCommand.Transaction = transaction;
            bootstrapCommand.CommandText = bootstrap.Sql;
            await bootstrapCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var ledger = await ReadLedgerAsync(connection, transaction, cancellationToken);
        var unexpected = ledger.Keys
            .Where(migrationId => !catalog.ById.ContainsKey(migrationId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                $"Migration drift detected. The ledger contains migrations that are not packaged with this API: {string.Join(", ", unexpected)}.");
        }

        foreach (var migration in catalog.Migrations)
        {
            if (ledger.TryGetValue(migration.Id, out var checksum)
                && !string.Equals(checksum, migration.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Migration drift detected for '{migration.Id}'. The applied checksum does not match the packaged SQL file.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return ledger;
    }

    private async Task EnsureBaseSchemaAsync(
        NpgsqlConnection connection,
        DatabaseBootstrapCatalog bootstrap,
        CancellationToken cancellationToken)
    {
        var presence = await bootstrap.GetPresenceAsync(connection, cancellationToken);
        if (presence.IsComplete)
        {
            return;
        }

        if (!presence.IsEmpty)
        {
            throw new InvalidOperationException(
                $"The database has a partial base schema. Present anchor tables: {string.Join(", ", presence.Present)}. " +
                $"Missing anchor tables: {string.Join(", ", presence.Missing)}. Recover or provision a clean database before migration.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = bootstrap.Sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        var afterBootstrap = await bootstrap.GetPresenceAsync(connection, cancellationToken);
        if (!afterBootstrap.IsComplete)
        {
            throw new InvalidOperationException(
                $"The packaged base schema did not create all required tables. Missing: {string.Join(", ", afterBootstrap.Missing)}.");
        }

        logger.LogInformation(
            "Provisioned empty database base schema from {BootstrapPath} with SHA-256 {BootstrapChecksum}.",
            bootstrap.Path,
            bootstrap.ChecksumSha256);
    }

    private void ValidateExistingLedger(IReadOnlyDictionary<string, string> ledger)
    {
        var unexpected = ledger.Keys
            .Where(migrationId => !catalog.ById.ContainsKey(migrationId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                $"Migration drift detected. The ledger contains migrations that are not packaged with this API: {string.Join(", ", unexpected)}.");
        }
    }

    private static async Task<Dictionary<string, string>> ReadLedgerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var ledger = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select migration_id, checksum_sha256 from schema_migrations order by migration_id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ledger.Add(reader.GetString(0), reader.GetString(1));
        }

        return ledger;
    }

    private static async Task ApplyMigrationAsync(
        NpgsqlConnection connection,
        SchemaMigrationDefinition migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var migrationCommand = connection.CreateCommand())
        {
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = migration.Sql;
            await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText = """
                insert into schema_migrations
                    (migration_id, checksum_sha256, description, applied_at, applied_by)
                values
                    (@migrationId, @checksum, @description, now(), 'api-schema-migrator');
                """;
            ledgerCommand.Parameters.AddWithValue("migrationId", migration.Id);
            ledgerCommand.Parameters.AddWithValue("checksum", migration.ChecksumSha256);
            ledgerCommand.Parameters.AddWithValue("description", migration.Description);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select pg_advisory_lock(@lockId);";
        command.Parameters.AddWithValue("lockId", AdvisoryLockId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsIsolatedTestDatabase(string databaseName)
    {
        const string prefix = "avenchart_test_";
        return databaseName.StartsWith(prefix, StringComparison.Ordinal)
            && databaseName.Length > prefix.Length
            && databaseName[prefix.Length..].All(character =>
                char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select pg_advisory_unlock(@lockId);";
            command.Parameters.AddWithValue("lockId", AdvisoryLockId);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch
        {
            // Closing the connection also releases the session lock. Preserve the original failure.
        }
    }
}

public sealed record SchemaMigrationRunResult(
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> AlreadyApplied,
    int ExpectedCount);

public sealed class SchemaMigrationFaultInjectionException(string migrationId, int appliedCount)
    : Exception($"Test fault injected after committing migration '{migrationId}' ({appliedCount} newly applied migrations).")
{
    public string MigrationId { get; } = migrationId;

    public int AppliedCount { get; } = appliedCount;
}
