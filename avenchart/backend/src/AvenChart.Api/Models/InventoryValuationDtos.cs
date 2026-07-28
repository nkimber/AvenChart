namespace AvenChart.Api.Models;

public sealed record InventoryCostPolicyDefinition(string Method, string Currency, string TaxTreatment, string FreightTreatment, string LandedCostTreatment, string RoundingRule, string BackdatedEntryRule, string EffectiveDate, string ApprovalReference, string Rationale);
public sealed record InventoryCostPolicy(Guid PolicyId, string ScopeType, InventoryCostPolicyDefinition Definition, int Revision, string Status, string ActivatedAt, string ActivatedBy, string? SupersededAt, string? SupersededBy);
public sealed record InventoryCostPolicyChangeRequest(Guid RequestId, InventoryCostPolicyDefinition ProposedDefinition, Guid? BaselinePolicyId, int? BaselineRevision, string Reason, string Status, int Version, string CreatedAt, string CreatedBy, string UpdatedAt, string UpdatedBy);
public sealed record InventoryCostPolicyChangeRequestEvent(long EventId, string Action, string? Note, string OccurredAt, string Username);
public sealed record InventoryCostPolicyCatalogResponse(InventoryCostPolicy? ActivePolicy, IReadOnlyList<InventoryCostPolicyChangeRequest> Requests);
public sealed record InventoryCostPolicyChangeRequestDetailResponse(InventoryCostPolicyChangeRequest Request, InventoryCostPolicy? ActivePolicy, IReadOnlyList<InventoryCostPolicyChangeRequestEvent> Events);
public sealed record InventoryCostPolicyChangeRequestCreateRequest(InventoryCostPolicyDefinition ProposedDefinition, string Reason);
public sealed record InventoryCostPolicyChangeRequestDecisionRequest(int? ExpectedVersion, string? Note);
public sealed class InventoryCostPolicyChangeRequestConflictException(string message) : Exception(message);
