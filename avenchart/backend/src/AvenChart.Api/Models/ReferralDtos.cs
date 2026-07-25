namespace AvenChart.Api.Models;

public sealed record ReferralItem(Guid Id, string PatientId, int? EncounterId, string Destination, string Reason, string Status, string? ExternalReference, string? Notes, string RequestedAt, string CreatedAt, string UpdatedAt);
public sealed record ReferralCreateRequest(int? EncounterId, string Destination, string Reason, string? ExternalReference, string? Notes, string? RequestedAt);
public sealed record ReferralStatusRequest(string Status);
