namespace AvenChart.Api.Models;
public sealed record ConfigurationCatalogItem(string Key, string Family, string Classification, string Authority, string Validation, string MutationState);
public sealed record ConfigurationCatalogResponse(IReadOnlyList<ConfigurationCatalogItem> Settings);
