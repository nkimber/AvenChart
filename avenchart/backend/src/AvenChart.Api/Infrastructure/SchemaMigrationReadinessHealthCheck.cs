// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace AvenChart.Api.Infrastructure;

public sealed partial class SchemaMigrationReadinessHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<SchemaMigrationReadinessHealthCheck> _logger;
    private readonly Lazy<ExpectedMigrationManifest> _expectedManifest;

    public SchemaMigrationReadinessHealthCheck(
        NpgsqlDataSource dataSource,
        IConfiguration configuration,
        ILogger<SchemaMigrationReadinessHealthCheck> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        _expectedManifest = new Lazy<ExpectedMigrationManifest>(
            () => LoadExpectedManifest(configuration["DatabaseSchema:MigrationsPath"]),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var expected = _expectedManifest.Value;
            if (expected.Error is not null)
            {
                return HealthCheckResult.Unhealthy(
                    expected.Error,
                    data: BuildDetails(expected, 0, [], [], []));
            }

            var applied = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select migration_id, checksum_sha256 from schema_migrations order by migration_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetString(0), reader.GetString(1));
            }

            var missing = expected.Migrations.Keys
                .Where(migrationId => !applied.ContainsKey(migrationId))
                .ToArray();
            var unexpected = applied.Keys
                .Where(migrationId => !expected.Migrations.ContainsKey(migrationId))
                .ToArray();
            var checksumMismatches = expected.Migrations
                .Where(pair => applied.TryGetValue(pair.Key, out var checksum)
                    && !string.Equals(pair.Value, checksum, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToArray();

            var details = BuildDetails(
                expected,
                applied.Count,
                missing,
                unexpected,
                checksumMismatches);
            if (missing.Length > 0 || unexpected.Length > 0 || checksumMismatches.Length > 0)
            {
                return HealthCheckResult.Unhealthy(
                    "The database migration ledger does not match the migrations packaged with this API.",
                    data: details);
            }

            return HealthCheckResult.Healthy(
                $"The database migration ledger matches all {expected.Migrations.Count} packaged migrations.",
                details);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Schema migration readiness check failed.");
            return HealthCheckResult.Unhealthy(
                "The database migration ledger could not be validated.",
                exception);
        }
    }

    private static ExpectedMigrationManifest LoadExpectedManifest(string? configuredPath)
    {
        var migrationsPath = ResolveMigrationsPath(configuredPath);
        if (!Directory.Exists(migrationsPath))
        {
            return new ExpectedMigrationManifest(
                migrationsPath,
                new Dictionary<string, string>(StringComparer.Ordinal),
                $"The packaged migrations directory was not found at '{migrationsPath}'.");
        }

        var files = Directory.GetFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            return new ExpectedMigrationManifest(
                migrationsPath,
                new Dictionary<string, string>(StringComparer.Ordinal),
                $"No packaged migration files were found at '{migrationsPath}'.");
        }

        var migrations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!MigrationFileNamePattern().IsMatch(fileName))
            {
                return new ExpectedMigrationManifest(
                    migrationsPath,
                    migrations,
                    $"Packaged migration '{fileName}' does not use the required naming convention.");
            }

            var migrationId = Path.GetFileNameWithoutExtension(file);
            var checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))
                .ToLowerInvariant();
            if (!migrations.TryAdd(migrationId, checksum))
            {
                return new ExpectedMigrationManifest(
                    migrationsPath,
                    migrations,
                    $"Packaged migration id '{migrationId}' is duplicated.");
            }
        }

        if (!migrations.ContainsKey("V0001__migration_ledger"))
        {
            return new ExpectedMigrationManifest(
                migrationsPath,
                migrations,
                "The packaged migrations do not contain V0001__migration_ledger.");
        }

        return new ExpectedMigrationManifest(migrationsPath, migrations, null);
    }

    private static string ResolveMigrationsPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var packagedPath = Path.Combine(AppContext.BaseDirectory, "database", "migrations");
        if (Directory.Exists(packagedPath))
        {
            return packagedPath;
        }

        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, "database", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return packagedPath;
    }

    private static Dictionary<string, object> BuildDetails(
        ExpectedMigrationManifest expected,
        int appliedCount,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> unexpected,
        IReadOnlyList<string> checksumMismatches)
    {
        return new Dictionary<string, object>
        {
            ["migrationsPath"] = expected.MigrationsPath,
            ["expected"] = expected.Migrations.Count,
            ["applied"] = appliedCount,
            ["latestExpected"] = expected.Migrations.Keys.LastOrDefault() ?? string.Empty,
            ["missing"] = missing,
            ["unexpected"] = unexpected,
            ["checksumMismatches"] = checksumMismatches
        };
    }

    [GeneratedRegex("^V\\d{4}__[A-Za-z0-9_-]+\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationFileNamePattern();

    private sealed record ExpectedMigrationManifest(
        string MigrationsPath,
        IReadOnlyDictionary<string, string> Migrations,
        string? Error);
}
