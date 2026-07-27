namespace AvenChart.Api.Models;

public sealed record InventoryResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    InventorySummary Summary,
    IReadOnlyList<InventoryFacility> Facilities,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<InventoryTransactionItem> RecentTransactions);

public sealed record InventorySummary(
    int ActiveItems,
    int ActiveLots,
    int BelowReorderPoint,
    int ExpiredLots,
    int ExpiringWithin90Days,
    decimal InventoryValue);

public sealed record InventoryFacility(
    int FacilityId,
    string Code,
    string Name);

public sealed record InventoryItem(
    int ItemId,
    string ItemCode,
    string Name,
    string Category,
    string Unit,
    decimal ReorderPoint,
    decimal PreferredQuantity,
    decimal QuantityOnHand,
    decimal InventoryValue,
    bool BelowReorderPoint,
    InventoryMedicationLink? MedicationLink,
    IReadOnlyList<InventoryLot> Lots);

public sealed record InventoryMedicationLink(
    int ItemId,
    string RxNormCode,
    string DrugName,
    string DisplayName,
    string LinkedBy,
    string LinkedAt);

public sealed record InventoryMedicationCatalogItem(
    string RxNormCode,
    string DrugName,
    string DisplayName,
    string Form,
    string Strength,
    string Route);

public sealed record InventoryMedicationLinkUpdateRequest(string RxNormCode);

public sealed record InventoryControlledLocation(
    Guid LocationId,
    int FacilityId,
    string FacilityCode,
    string FacilityName,
    string LocationCode,
    string DisplayName,
    bool DualAttestationRequired,
    bool Active,
    string UpdatedAt,
    string UpdatedBy);

public sealed record InventoryControlledLocationMutationRequest(
    int FacilityId,
    string LocationCode,
    string DisplayName,
    bool DualAttestationRequired,
    bool Active);

public sealed record InventoryControlledLocationEvent(long EventId, string Action, bool? PriorActive, bool ResultingActive, string OccurredAt, string Username);
public sealed record InventoryControlledLocationHistoryResponse(InventoryControlledLocation Location, IReadOnlyList<InventoryControlledLocationEvent> Events);
public sealed record InventoryControlledSubstanceItem(int ItemId, string ItemCode, string Name, string Category, string Unit, string ScheduleCode);
public sealed record InventoryControlledSubstanceCatalogResponse(IReadOnlyList<InventoryControlledLocation> Locations, IReadOnlyList<InventoryControlledSubstanceItem> Items);
public sealed record InventoryControlledSubstanceClassificationRequest(string? ScheduleCode);
public sealed record InventoryControlledSubstanceClassificationEvent(long EventId, string? PriorSchedule, string? ResultingSchedule, string OccurredAt, string Username);
public sealed record InventoryControlledSubstanceClassificationHistoryResponse(InventoryControlledSubstanceItem Item, IReadOnlyList<InventoryControlledSubstanceClassificationEvent> Events);

public sealed record InventoryControlledCustodyMovementRequest(
    string Action,
    int? LotId,
    int? ItemId,
    string? LotNumber,
    string? ExpirationDate,
    decimal? UnitCost,
    decimal Quantity,
    Guid? SourceLocationId,
    Guid? DestinationLocationId,
    string? PatientId,
    int? Encounter,
    string? Reason,
    Guid? RelatedEventId,
    string? CorrectionDirection,
    string? IdempotencyKey,
    Guid? WitnessSessionId);

public sealed record InventoryControlledCustodyEvent(
    Guid EventId,
    string Action,
    int LotId,
    int? CounterpartyLotId,
    int ItemId,
    string ItemCode,
    string ScheduleCode,
    decimal Quantity,
    decimal QuantityDelta,
    Guid? SourceLocationId,
    Guid? DestinationLocationId,
    string? PatientId,
    int? Encounter,
    string Reason,
    Guid? RelatedEventId,
    decimal? SourceQuantityBefore,
    decimal? SourceQuantityAfter,
    decimal? DestinationQuantityBefore,
    decimal? DestinationQuantityAfter,
    string PerformedBy,
    string OccurredAt,
    string? WitnessUsername,
    string? WitnessedAt);

public sealed record InventoryControlledCustodyMovementResponse(
    InventoryControlledCustodyEvent Event,
    InventoryLot Lot,
    InventoryLot? CounterpartyLot);

public sealed record InventoryControlledCustodyLotHistoryResponse(
    InventoryLot Lot,
    Guid? ControlledLocationId,
    string? ControlledLocationCode,
    string? ControlledLocationName,
    string ScheduleCode,
    IReadOnlyList<InventoryControlledCustodyEvent> Events);

public sealed record InventoryControlledCountSessionCreateRequest(Guid LocationId, string CountType, bool MovementLockActive, string Reason, string IdempotencyKey);
public sealed record InventoryControlledCountObservation(int LotId, decimal ObservedQuantity);
public sealed record InventoryControlledCountSubmitRequest(Guid CounterSessionId, string Reason, string IdempotencyKey, IReadOnlyList<InventoryControlledCountObservation> Observations);
public sealed record InventoryControlledCountLine(Guid LineId, int LotId, string LotNumber, string ItemCode, decimal ExpectedQuantity, decimal? ObservedQuantity, decimal? VarianceQuantity, Guid? DiscrepancyId, string? DiscrepancyStatus);
public sealed record InventoryControlledCountSession(Guid SessionId, Guid LocationId, string LocationCode, string LocationName, string CountType, string Status, bool MovementLockActive, string Reason, string StartedBy, string StartedAt, string? SubmittedBy, string? SubmittedAt, string? CounterUsername, IReadOnlyList<InventoryControlledCountLine> Lines);
public sealed record InventoryControlledDiscrepancyInvestigationRequest(string Notes);
public sealed record InventoryControlledDiscrepancyCorrectionRequest(string Notes, string IdempotencyKey, Guid? WitnessSessionId);
public sealed record InventoryControlledDiscrepancyCloseRequest(string Notes);

public sealed record InventoryPrescriptionDispenseRequest(
    string PrescriptionId,
    decimal Quantity,
    decimal Fee,
    string? SaleDate,
    string? Notes);

public sealed record InventoryPrescriptionDispenseResponse(
    string PrescriptionId,
    int ItemId,
    string PatientId,
    int Encounter,
    string RxNormCode,
    InventoryPatientSaleResponse Sale);

public sealed record InventoryLot(
    int LotId,
    string FacilityCode,
    string FacilityName,
    string LotNumber,
    string? ExpirationDate,
    decimal QuantityOnHand,
    decimal UnitCost,
    string Status,
    string? ExpiryStatus = null);

public sealed record InventoryTransactionItem(
    Guid TransactionId,
    int LotId,
    string ItemCode,
    string ItemName,
    string FacilityCode,
    string TransactionType,
    decimal QuantityDelta,
    string? Reason,
    string PerformedBy,
    DateTimeOffset OccurredAt,
    Guid? TransferId,
    string? CounterpartyFacilityCode,
    Guid? ReceiptId = null,
    string? ReceiptReference = null,
    Guid? ReconciliationId = null);

public sealed record InventoryTransactionCreateRequest(
    int LotId,
    string TransactionType,
    decimal Quantity,
    string? Reason);

public sealed record InventoryTransferCreateRequest(
    int SourceLotId,
    int DestinationFacilityId,
    decimal Quantity,
    string? Reason);

public sealed record InventoryMutationResponse(
    InventoryTransactionItem Transaction,
    InventoryLot Lot,
    decimal ItemQuantityOnHand,
    bool BelowReorderPoint,
    InventoryLot? CounterpartyLot = null,
    Guid? TransferId = null);

public sealed record InventoryLotMetadataUpdateRequest(
    string LotNumber,
    string? ExpirationDate);

public sealed record InventoryLotMetadataUpdateResponse(
    Guid AuditId,
    InventoryLot Lot,
    string ChangedBy,
    string ChangedAt);

public sealed record InventoryLotMetadataAuditItem(
    Guid AuditId,
    string PriorLotNumber,
    string NewLotNumber,
    string? PriorExpirationDate,
    string? NewExpirationDate,
    string ChangedBy,
    string ChangedAt);

public sealed record InventoryLotDestructionRequest(
    string? DestructionDate,
    string? Method,
    string? Witness,
    string? Notes);

public sealed record InventoryLotDestructionResponse(
    Guid DestructionId,
    InventoryLot Lot,
    string DestructionDate,
    string? Method,
    string? Witness,
    string? Notes,
    string DestroyedBy,
    string RecordedAt);

public sealed record InventoryExpiryDispositionRequest(
    string Disposition,
    string Notes,
    string? Method,
    string? Witness);

public sealed record InventoryExpiryDispositionResponse(
    Guid DispositionId,
    string Disposition,
    InventoryLot Lot,
    decimal QuantityAffected,
    string Notes,
    string? Method,
    string? Witness,
    string DisposedBy,
    string DisposedAt,
    InventoryTransactionItem? Transaction,
    Guid? DestructionId);

public sealed record InventoryPatientSaleCreateRequest(
    int LotId,
    string PatientId,
    int Encounter,
    string? SaleDate,
    decimal Quantity,
    decimal Fee,
    string? Notes);

public sealed record InventoryPatientSaleResponse(
    Guid SaleId,
    string PatientId,
    int Encounter,
    string SaleDate,
    decimal Quantity,
    decimal Fee,
    string? Notes,
    string SoldBy,
    string SoldAt,
    InventoryMutationResponse InventoryMutation);

public sealed record InventoryPatientSaleAllocationCreateRequest(
    int ItemId,
    string PatientId,
    int Encounter,
    string? SaleDate,
    decimal Quantity,
    decimal Fee,
    string? Notes);

public sealed record InventoryPatientSaleAllocationLine(
    Guid SaleId,
    int LotId,
    string LotNumber,
    decimal Quantity,
    decimal Fee,
    Guid TransactionId);

public sealed record InventoryPatientSaleAllocationResponse(
    Guid SaleBatchId,
    int ItemId,
    string PatientId,
    int Encounter,
    string SaleDate,
    decimal Quantity,
    decimal Fee,
    IReadOnlyList<InventoryPatientSaleAllocationLine> Allocations);

public sealed record InventoryActivityReportResponse(
    string DatasetId,
    string DatasetVersion,
    string? FromDate,
    string? ToDate,
    int? FacilityId,
    int TotalEntries,
    IReadOnlyList<InventoryTransactionItem> Entries);

public sealed record InventoryVendor(
    Guid VendorId,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    bool Active);

public sealed record InventoryVendorListResponse(IReadOnlyList<InventoryVendor> Vendors);

public sealed record InventoryVendorCreateRequest(
    string Name,
    string? ContactName,
    string? Phone,
    string? Email);

public sealed record InventoryPurchaseReceiptCreateRequest(
    Guid VendorId,
    int FacilityId,
    int ItemId,
    string LotNumber,
    string? ExpirationDate,
    decimal Quantity,
    decimal UnitCost,
    string? ReferenceNumber,
    string Notes,
    Guid? RequisitionId = null);

public sealed record InventoryPurchaseReceiptReconciliation(
    Guid ReconciliationId,
    Guid RequisitionId,
    Guid RequisitionLineId,
    decimal ReceivedQuantity,
    string ReconciledBy,
    string ReconciledAt);

public sealed record InventoryPurchaseReceiptResponse(
    Guid ReceiptId,
    InventoryVendor Vendor,
    string FacilityCode,
    string FacilityName,
    string? ReferenceNumber,
    string ReceivedAt,
    string ReceivedBy,
    string Notes,
    InventoryLot Lot,
    InventoryTransactionItem Transaction,
    decimal ItemQuantityOnHand,
    bool BelowReorderPoint,
    InventoryPurchaseReceiptReconciliation? RequisitionReconciliation = null);

public sealed record InventoryPurchaseRequisitionLineCreateRequest(int ItemId, decimal Quantity);

public sealed record InventoryPurchaseRequisitionCreateRequest(
    int FacilityId,
    Guid? VendorId,
    string? Notes,
    IReadOnlyList<InventoryPurchaseRequisitionLineCreateRequest> Lines);

public sealed record InventoryPurchaseRequisitionDecisionRequest(string? Notes);

public sealed record InventoryPurchaseRequisitionLine(
    Guid RequisitionLineId,
    int ItemId,
    string ItemCode,
    string ItemName,
    decimal RequestedQuantity,
    decimal ReceivedQuantity,
    decimal OutstandingQuantity,
    string Unit);

public sealed record InventoryPurchaseRequisitionEvent(
    Guid EventId,
    string Action,
    string? Note,
    string Actor,
    string OccurredAt);

public sealed record InventoryPurchaseRequisition(
    Guid RequisitionId,
    int FacilityId,
    string FacilityCode,
    string FacilityName,
    Guid? VendorId,
    string? VendorName,
    string Status,
    string? Notes,
    string RequestedBy,
    string RequestedAt,
    string? SubmittedBy,
    string? SubmittedAt,
    string? DecidedBy,
    string? DecidedAt,
    string? DecisionNotes,
    string ReceiptStatus,
    IReadOnlyList<InventoryPurchaseRequisitionLine> Lines,
    IReadOnlyList<InventoryPurchaseRequisitionEvent> Events);

public sealed record InventoryCountReconciliationCreateRequest(
    int LotId,
    decimal CountedQuantity,
    string Notes);

public sealed record InventoryCountReconciliationResponse(
    Guid ReconciliationId,
    int LotId,
    decimal ExpectedQuantity,
    decimal CountedQuantity,
    decimal QuantityDelta,
    string Notes,
    string CountedBy,
    string CountedAt,
    InventoryLot Lot,
    InventoryTransactionItem Transaction,
    decimal ItemQuantityOnHand,
    bool BelowReorderPoint);
