namespace AvenChart.Api.Models;

public sealed record AuthorizationItem(Guid Id, string PatientId, Guid? ReferralId, string Payer, string Service, string Status, string? AuthorizationNumber, string RequestedAt, string? ExpiresAt, string CreatedAt, string UpdatedAt);
public sealed record AuthorizationCreateRequest(Guid? ReferralId, string Payer, string Service, string? RequestedAt, string? ExpiresAt);
public sealed record AuthorizationStatusRequest(string Status, string? AuthorizationNumber);
