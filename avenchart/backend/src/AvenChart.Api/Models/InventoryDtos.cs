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
    IReadOnlyList<InventoryLot> Lots);

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
