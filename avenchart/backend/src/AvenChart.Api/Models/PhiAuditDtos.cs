namespace AvenChart.Api.Models;

public sealed record PhiAccessAuditEventItem(
    Guid AuditId,
    DateTimeOffset OccurredAt,
    string Username,
    string HttpMethod,
    string EndpointName,
    string RequiredPermission,
    bool Authorized,
    int ResponseStatus);

public sealed record PhiAccessAuditResponse(
    int TotalEvents,
    int AuthorizedEvents,
    int DeniedEvents,
    IReadOnlyList<PhiAccessAuditEventItem> Events);
