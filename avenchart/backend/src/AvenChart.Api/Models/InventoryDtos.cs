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
    string Notes);

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
    bool BelowReorderPoint);

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
