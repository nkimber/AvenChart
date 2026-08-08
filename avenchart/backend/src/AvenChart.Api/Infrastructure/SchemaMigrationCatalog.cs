// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AvenChart.Api.Infrastructure;

public sealed partial class SchemaMigrationCatalog
{
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
