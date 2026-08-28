// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public interface IPharmacyDirectory
{
    string AdapterMode { get; }
    string DatasetId { get; }
    string DatasetVersion { get; }
    TelehealthPharmacyDirectoryEntry? Find(Guid directoryEntryId);
    IReadOnlyList<TelehealthPharmacyDirectoryMatch> Search(TelehealthPharmacyDirectorySearch search);
}

public sealed record TelehealthPharmacyDirectorySearch(
    string? Query,
    string? State,
    string? PostalCode,
    string? OriginPostalCode,
    bool LocationSearchAcknowledged,
    int Limit);

public sealed record TelehealthPharmacyDirectoryEntry(
    Guid DirectoryEntryId,
    bool Active,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone,
    string? NcpdpId,
    string? Npi,
    string ElectronicRoutingCapability,
    double Latitude,
    double Longitude);

public sealed record TelehealthPharmacyDirectoryMatch(
    TelehealthPharmacyDirectoryEntry Entry,
    decimal? ApproximateDistanceMiles);

public sealed class SyntheticTelehealthPharmacyDirectory : IPharmacyDirectory
{
    public const string Mode = "NON_PRODUCTION";
    public const string SourceId = "avenchart-synthetic-pharmacy-directory";
    public const string SourceVersion = "2026.08.27.1";

    private static readonly IReadOnlyDictionary<string, (double Latitude, double Longitude)> PostalCentroids =
        new Dictionary<string, (double Latitude, double Longitude)>(StringComparer.Ordinal)
        {
            ["30303"] = (33.7529, -84.3925),
            ["31401"] = (32.0809, -81.0912),
            ["90012"] = (34.0614, -118.2385),
            ["92101"] = (32.7198, -117.1628),
            ["33130"] = (25.7680, -80.2057),
            ["32801"] = (28.5421, -81.3728)
        };

    private static readonly IReadOnlyList<TelehealthPharmacyDirectoryEntry> Entries =
    [
        Entry("00000000-0000-4000-8000-000000001001", "Atlanta Synthetic Community Pharmacy", "100 Synthetic Peachtree Way", "Atlanta", "GA", "30303", "404-555-0101", 33.7522, -84.3908),
        Entry("00000000-0000-4000-8000-000000001002", "Savannah Synthetic Neighborhood Pharmacy", "200 Demonstration Bay Street", "Savannah", "GA", "31401", "912-555-0102", 32.0815, -81.0920),
        Entry("00000000-0000-4000-8000-000000001003", "Los Angeles Synthetic Care Pharmacy", "300 Example Grand Avenue", "Los Angeles", "CA", "90012", "213-555-0103", 34.0589, -118.2392),
        Entry("00000000-0000-4000-8000-000000001004", "San Diego Synthetic Community Pharmacy", "400 Demonstration Harbor Drive", "San Diego", "CA", "92101", "619-555-0104", 32.7167, -117.1694),
        Entry("00000000-0000-4000-8000-000000001005", "Miami Synthetic Neighborhood Pharmacy", "500 Example Brickell Avenue", "Miami", "FL", "33130", "305-555-0105", 25.7667, -80.1918),
        Entry("00000000-0000-4000-8000-000000001006", "Orlando Synthetic Community Pharmacy", "600 Demonstration Central Boulevard", "Orlando", "FL", "32801", "407-555-0106", 28.5424, -81.3761)
    ];

    public string AdapterMode => Mode;
    public string DatasetId => SourceId;
    public string DatasetVersion => SourceVersion;

    public TelehealthPharmacyDirectoryEntry? Find(Guid directoryEntryId) =>
        Entries.SingleOrDefault(entry => entry.DirectoryEntryId == directoryEntryId && entry.Active);

    public IReadOnlyList<TelehealthPharmacyDirectoryMatch> Search(TelehealthPharmacyDirectorySearch search)
    {
        if (search.Limit is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(nameof(search), "The pharmacy search limit must be between 1 and 25.");
        }
        if (!string.IsNullOrEmpty(search.OriginPostalCode) && !search.LocationSearchAcknowledged)
        {
            throw new ArgumentException("Location search must be acknowledged before using a postal origin.", nameof(search));
        }

        var query = Normalize(search.Query);
        var state = Normalize(search.State)?.ToUpperInvariant();
        var postalCode = Normalize(search.PostalCode);
        var originPostalCode = Normalize(search.OriginPostalCode);
        (double Latitude, double Longitude) origin = default;
        var hasOrigin = originPostalCode is not null
            && PostalCentroids.TryGetValue(originPostalCode, out origin);

        var matches = Entries
            .Where(entry => entry.Active)
            .Where(entry => state is null || string.Equals(entry.State, state, StringComparison.Ordinal))
            .Where(entry => postalCode is null || entry.PostalCode.StartsWith(postalCode, StringComparison.Ordinal))
            .Where(entry => query is null || Contains(entry, query))
            .Select(entry => new TelehealthPharmacyDirectoryMatch(
                entry,
                hasOrigin ? CalculateMiles(origin, entry) : null));

        return (hasOrigin
                ? matches.OrderBy(match => match.ApproximateDistanceMiles)
                    .ThenBy(match => match.Entry.Name, StringComparer.Ordinal)
                    .ThenBy(match => match.Entry.DirectoryEntryId)
                : matches.OrderBy(match => match.Entry.Name, StringComparer.Ordinal)
                    .ThenBy(match => match.Entry.DirectoryEntryId))
            .Take(search.Limit)
            .ToArray();
    }

    private static TelehealthPharmacyDirectoryEntry Entry(
        string id,
        string name,
        string address,
        string city,
        string state,
        string postalCode,
        string phone,
        double latitude,
        double longitude) => new(
            Guid.Parse(id),
            Active: true,
            name,
            address,
            AddressLine2: null,
            city,
            state,
            postalCode,
            Country: "US",
            phone,
            NcpdpId: null,
            Npi: null,
            ElectronicRoutingCapability: "NON_PRODUCTION_ONLY",
            latitude,
            longitude);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Contains(TelehealthPharmacyDirectoryEntry entry, string query) =>
        entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.City.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.State.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.PostalCode.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static decimal CalculateMiles(
        (double Latitude, double Longitude) origin,
        TelehealthPharmacyDirectoryEntry entry)
    {
        const double earthRadiusMiles = 3958.7613;
        var latitudeDelta = DegreesToRadians(entry.Latitude - origin.Latitude);
        var longitudeDelta = DegreesToRadians(entry.Longitude - origin.Longitude);
        var originLatitude = DegreesToRadians(origin.Latitude);
        var destinationLatitude = DegreesToRadians(entry.Latitude);
        var value = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(originLatitude) * Math.Cos(destinationLatitude)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var miles = earthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(value), Math.Sqrt(1 - value));
        return decimal.Round((decimal)miles, 1, MidpointRounding.AwayFromZero);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
