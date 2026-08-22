// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AvenChart.Api.Infrastructure;

public sealed partial class SchemaMigrationCatalog
{
    // These legacy-display migrations originally inserted synthetic fixtures
    // or a dependent permission unconditionally. Their corrected source is
    // safe for an empty or production database, while these exact hashes
    // remain accepted for ledgers written before the correction. No other
    // migration drift is tolerated.
    private static readonly IReadOnlyDictionary<string, string> AcceptedHistoricalChecksums =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["V0200__legacy_clinic_note_display_adapter"] = "7307b31b53c0217cfc70b812ad69f5cfc3ae7ab9f8ca15d14b6d8ce7d1cd4f4d",
            ["V0202__legacy_clinic_note_manifest_governance"] = "f238d1e23acc6e1366bc7b0aa85990776412ba47cef70e7530f0db5f8367fe9e",
            ["V0203__legacy_clinical_instructions_display_adapter"] = "17c5adf055ab1b59642351e0be22e10f85cfd0e131b27a765693522d0c0f7dd7",
            ["V0204__legacy_soap_display_adapter"] = "91794a8fe0d27bedf56b349e883a3a204d3a79494f88b6016bc5453be11cdc39",
        };

    public SchemaMigrationCatalog(IConfiguration configuration)
    {
        MigrationsPath = ResolveMigrationsPath(configuration["DatabaseSchema:MigrationsPath"]);
        (Migrations, Error) = LoadMigrations(MigrationsPath);
        ById = Migrations.ToDictionary(migration => migration.Id, StringComparer.Ordinal);
    }

    public string MigrationsPath { get; }

    public IReadOnlyList<SchemaMigrationDefinition> Migrations { get; }

    public string? Error { get; }

    public IReadOnlyDictionary<string, SchemaMigrationDefinition> ById { get; }

    public bool IsAcceptedLedgerChecksum(SchemaMigrationDefinition migration, string checksum) =>
        string.Equals(migration.ChecksumSha256, checksum, StringComparison.OrdinalIgnoreCase)
        || (AcceptedHistoricalChecksums.TryGetValue(migration.Id, out var historicalChecksum)
            && string.Equals(historicalChecksum, checksum, StringComparison.OrdinalIgnoreCase));

    private static (IReadOnlyList<SchemaMigrationDefinition> Migrations, string? Error) LoadMigrations(
        string migrationsPath)
    {
        if (!Directory.Exists(migrationsPath))
        {
            return ([], $"The packaged migrations directory was not found at '{migrationsPath}'.");
        }

        var files = Directory.GetFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            return ([], $"No packaged migration files were found at '{migrationsPath}'.");
        }

        var migrations = new List<SchemaMigrationDefinition>(files.Length);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!MigrationFileNamePattern().IsMatch(fileName))
            {
                return (migrations, $"Packaged migration '{fileName}' does not use the required naming convention.");
            }

            var migrationId = Path.GetFileNameWithoutExtension(file);
            if (!ids.Add(migrationId))
            {
                return (migrations, $"Packaged migration id '{migrationId}' is duplicated.");
            }

            var sqlBytes = File.ReadAllBytes(file);
            var sql = System.Text.Encoding.UTF8.GetString(sqlBytes);
            if (sql.StartsWith('\uFEFF'))
            {
                sql = sql[1..];
            }

            var separatorIndex = migrationId.IndexOf("__", StringComparison.Ordinal);
            migrations.Add(new SchemaMigrationDefinition(
                migrationId,
                Convert.ToHexString(SHA256.HashData(sqlBytes)).ToLowerInvariant(),
                migrationId[(separatorIndex + 2)..].Replace('_', ' '),
                sql,
                file));
        }

        if (migrations[0].Id != "V0001__migration_ledger")
        {
            return (migrations, "The first packaged migration must be V0001__migration_ledger.");
        }

        return (migrations, null);
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

    [GeneratedRegex("^V\\d{4}__[A-Za-z0-9_-]+\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationFileNamePattern();
}

public sealed record SchemaMigrationDefinition(
    string Id,
    string ChecksumSha256,
    string Description,
    string Sql,
    string Path);
