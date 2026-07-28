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
public sealed record InventoryReceiptCostLayer(Guid LayerId, Guid SourceTransactionId, Guid ReceiptId, int LotId, int ItemId, int FacilityId, decimal ReceivedQuantity, decimal RemainingQuantity, decimal UnitCost, string Currency, Guid? PolicyId, int? PolicyRevision, string? Method, string Status, string CreatedAt, string CreatedBy);
public sealed record InventoryReceiptCostLayerApplication(Guid ApplicationId, Guid LayerId, Guid SourceTransactionId, string ApplicationType, decimal Quantity, decimal UnitCost, decimal ExtendedCost, string RoundingTrace, Guid? ReversalApplicationId, string AppliedAt, string AppliedBy);
public sealed record InventoryValuationRunCreateRequest(string AsOfAt, int? FacilityId);
public sealed record InventoryValuationRun(Guid RunId, string RequestedAt, string RequestedBy, string AsOfAt, int? FacilityId, Guid PolicyId, int PolicyRevision, string Method, string Currency, string RoundingRule, string Status, int LayerCount, int ApplicationCount, int ExceptionCount, int UnvaluedLayerCount, decimal QuantityTotal, decimal ValueTotal, string CalculationVersion, string ResultChecksum, string CompletedAt);
public sealed record InventoryValuationRunLine(Guid LayerId, int LotId, int ItemId, int FacilityId, decimal ReceivedQuantity, decimal RemainingQuantity, decimal UnitCost, decimal ValueTotal, int ApplicationCount);
public sealed record InventoryValuationRunDetailResponse(InventoryValuationRun Run, IReadOnlyList<InventoryValuationRunLine> Lines);
public sealed class InventoryValuationPolicyMissingException(string message) : Exception(message);
