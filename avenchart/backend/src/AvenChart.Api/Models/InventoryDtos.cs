namespace AvenChart.Api.Models;

public sealed record InventoryResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    InventorySummary Summary,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<InventoryTransactionItem> RecentTransactions);

public sealed record InventorySummary(
    int ActiveItems,
    int ActiveLots,
    int BelowReorderPoint,
    int ExpiringWithin90Days,
    decimal InventoryValue);

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
    string Status);

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
    DateTimeOffset OccurredAt);

public sealed record InventoryTransactionCreateRequest(
    int LotId,
    string TransactionType,
    decimal Quantity,
    string? Reason);

public sealed record InventoryMutationResponse(
    InventoryTransactionItem Transaction,
    InventoryLot Lot,
    decimal ItemQuantityOnHand,
    bool BelowReorderPoint);
