// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Features.Telehealth;

namespace AvenChart.Api.Tests.Telehealth;

public sealed class SyntheticTelehealthPharmacyDirectoryTests
{
    private readonly SyntheticTelehealthPharmacyDirectory _directory = new();

    [Fact]
    public void DirectoryIsExplicitlyNonProductionAndVersioned()
    {
        Assert.Equal("NON_PRODUCTION", _directory.AdapterMode);
        Assert.Equal("avenchart-synthetic-pharmacy-directory", _directory.DatasetId);
        Assert.Equal("2026.08.27.1", _directory.DatasetVersion);
    }

    [Fact]
    public void SearchUsesNeutralStableOrderingWithoutAnOrigin()
    {
        var results = _directory.Search(new(null, null, null, null, false, 25));

        Assert.Equal(6, results.Count);
        Assert.Equal(results.Select(item => item.Entry.Name).Order(StringComparer.Ordinal), results.Select(item => item.Entry.Name));
        Assert.All(results, item => Assert.Null(item.ApproximateDistanceMiles));
        Assert.All(results, item => Assert.Equal("NON_PRODUCTION_ONLY", item.Entry.ElectronicRoutingCapability));
        Assert.All(results, item => Assert.Null(item.Entry.NcpdpId));
        Assert.All(results, item => Assert.Null(item.Entry.Npi));
    }

    [Fact]
    public void SearchFiltersBySupportedDirectoryFacts()
    {
        var results = _directory.Search(new("community", "GA", "303", null, false, 25));

        var result = Assert.Single(results);
        Assert.Equal("Atlanta", result.Entry.City);
        Assert.Equal("GA", result.Entry.State);
        Assert.Equal("30303", result.Entry.PostalCode);
    }

    [Fact]
    public void PostalOriginRequiresAcknowledgment()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _directory.Search(new(null, "GA", null, "30303", false, 25)));

        Assert.Contains("acknowledged", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AcknowledgedPostalOriginCalculatesDeterministicApproximateDistance()
    {
        var first = _directory.Search(new(null, "GA", null, "30303", true, 25));
        var second = _directory.Search(new(null, "GA", null, "30303", true, 25));

        Assert.Equal(first, second);
        Assert.Equal("Atlanta", first[0].Entry.City);
        Assert.NotNull(first[0].ApproximateDistanceMiles);
        Assert.True(first[0].ApproximateDistanceMiles < first[1].ApproximateDistanceMiles);
    }

    [Fact]
    public void FindReturnsOnlyAStableKnownEntry()
    {
        var known = _directory.Search(new("Miami", null, null, null, false, 1)).Single().Entry;

        Assert.Equal(known, _directory.Find(known.DirectoryEntryId));
        Assert.Null(_directory.Find(Guid.NewGuid()));
    }
}
