// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace AvenChart.Api.Tests;

public sealed class DatabaseBootstrapCatalogTests
{
    [Fact]
    public void ActualBootstrapSchema_DoesNotMistakeColumnNamesForCopyStatements()
    {
        var migrationsPath = FindRepositoryPath("database", "migrations");
        var configuration = BuildConfiguration(migrationsPath);

        var catalog = new SchemaMigrationCatalog(configuration);
        var bootstrap = new DatabaseBootstrapCatalog(configuration, catalog);

        Assert.Null(catalog.Error);
        Assert.Null(bootstrap.Error);
    }

    [Theory]
    [InlineData("create table example(last_colonoscopy text);", false)]
    [InlineData("copy example from stdin;", true)]
    [InlineData("drop table example;", true)]
    [InlineData("drop sequence example_id_seq;", true)]
    public void BootstrapCatalog_RejectsOnlyForbiddenSqlStatements(string sql, bool shouldReject)
    {
        var migrationsPath = FindRepositoryPath("database", "migrations");
        var directory = Path.Combine(Path.GetTempPath(), $"avanchart-bootstrap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var bootstrapPath = Path.Combine(directory, "base-schema.sql");
        File.WriteAllText(bootstrapPath, sql);

        try
        {
            var configuration = BuildConfiguration(migrationsPath, bootstrapPath);
            var catalog = new SchemaMigrationCatalog(configuration);
            var bootstrap = new DatabaseBootstrapCatalog(configuration, catalog);

            Assert.Null(catalog.Error);
            Assert.Equal(shouldReject, bootstrap.Error is not null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("V0200__legacy_clinic_note_display_adapter", "7307b31b53c0217cfc70b812ad69f5cfc3ae7ab9f8ca15d14b6d8ce7d1cd4f4d", true)]
    [InlineData("V0202__legacy_clinic_note_manifest_governance", "f238d1e23acc6e1366bc7b0aa85990776412ba47cef70e7530f0db5f8367fe9e", true)]
    [InlineData("V0203__legacy_clinical_instructions_display_adapter", "17c5adf055ab1b59642351e0be22e10f85cfd0e131b27a765693522d0c0f7dd7", true)]
    [InlineData("V0204__legacy_soap_display_adapter", "91794a8fe0d27bedf56b349e883a3a204d3a79494f88b6016bc5453be11cdc39", true)]
    [InlineData("V0200__legacy_clinic_note_display_adapter", "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", false)]
    [InlineData("V0001__migration_ledger", "7307b31b53c0217cfc70b812ad69f5cfc3ae7ab9f8ca15d14b6d8ce7d1cd4f4d", false)]
    public void MigrationCatalog_AcceptsOnlyExplicitHistoricalChecksumAmendments(
        string migrationId,
        string checksum,
        bool expectedAcceptance)
    {
        var migrationsPath = FindRepositoryPath("database", "migrations");
        var catalog = new SchemaMigrationCatalog(BuildConfiguration(migrationsPath));

        var migration = Assert.Single(catalog.Migrations, candidate => candidate.Id == migrationId);

        Assert.Equal(expectedAcceptance, catalog.IsAcceptedLedgerChecksum(migration, checksum));
    }

    private static IConfiguration BuildConfiguration(string migrationsPath, string? bootstrapPath = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["DatabaseSchema:MigrationsPath"] = migrationsPath
        };
        if (bootstrapPath is not null)
        {
            values["DatabaseSchema:BootstrapPath"] = bootstrapPath;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string FindRepositoryPath(params string[] segments)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Could not locate the repository path '{Path.Combine(segments)}'.");
    }
}
