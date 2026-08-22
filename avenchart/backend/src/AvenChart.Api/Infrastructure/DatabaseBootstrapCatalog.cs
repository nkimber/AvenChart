// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace AvenChart.Api.Infrastructure;

/// <summary>
/// Loads the versioned base schema used only when an otherwise empty database
/// is provisioned. Fixture data is deliberately excluded: synthetic data is
/// supplied by the separate gold-seed workflow, then the same migration ledger
/// advances it to the current application shape.
/// </summary>
public sealed class DatabaseBootstrapCatalog
{
    private static readonly Regex ForbiddenBootstrapStatementPattern = new(
        @"^\s*(?:copy\b|drop\s+(?:table|sequence)\b)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly string[] RequiredTables =
    [
        "dataset_metadata",
        "facilities",
        "staff",
        "auth_accounts",
        "auth_sessions",
        "access_groups",
        "patients",
        "patient_portal_accounts",
        "patient_portal_sessions",
        "appointments",
        "encounters",
        "inventory_items",
        "billing",
        "lab_orders",
        "lab_reports",
        "lab_results",
    ];

    public DatabaseBootstrapCatalog(IConfiguration configuration, SchemaMigrationCatalog migrations)
    {
        Path = ResolvePath(configuration["DatabaseSchema:BootstrapPath"], migrations.MigrationsPath);
        if (!File.Exists(Path))
        {
            Error = $"The packaged base schema was not found at '{Path}'.";
            Sql = string.Empty;
            ChecksumSha256 = string.Empty;
            return;
        }

        var bytes = File.ReadAllBytes(Path);
        Sql = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        ChecksumSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(Sql))
        {
            Error = $"The packaged base schema at '{Path}' is empty.";
        }
        else if (ForbiddenBootstrapStatementPattern.IsMatch(Sql))
        {
            Error = $"The packaged base schema at '{Path}' contains fixture or destructive reset SQL.";
        }
    }

    public string Path { get; }
    public string Sql { get; }
    public string ChecksumSha256 { get; }
    public string? Error { get; }

    public async Task<DatabaseBootstrapPresence> GetPresenceAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select relation.relname
            from pg_class relation
            join pg_namespace schema on schema.oid=relation.relnamespace
            where schema.nspname=current_schema()
              and relation.relkind in ('r','p')
              and relation.relname = any(@tableNames)
            order by relation.relname;
            """;
        command.Parameters.AddWithValue("tableNames", RequiredTables);
        var present = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            present.Add(reader.GetString(0));
        }

        var missing = RequiredTables.Where(table => !present.Contains(table)).ToArray();
        return new DatabaseBootstrapPresence(
            IsEmpty: present.Count == 0,
            IsComplete: missing.Length == 0,
            Present: present.Order(StringComparer.Ordinal).ToArray(),
            Missing: missing);
    }

    private static string ResolvePath(string? configuredPath, string migrationsPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return System.IO.Path.GetFullPath(configuredPath);
        }

        var migrationsDirectory = new DirectoryInfo(migrationsPath);
        var databaseDirectory = migrationsDirectory.Parent;
        return System.IO.Path.Combine(databaseDirectory?.FullName ?? AppContext.BaseDirectory, "bootstrap", "base-schema.sql");
    }
}

public sealed record DatabaseBootstrapPresence(
    bool IsEmpty,
    bool IsComplete,
    IReadOnlyList<string> Present,
    IReadOnlyList<string> Missing);
