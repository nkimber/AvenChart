using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryRepository(NpgsqlDataSource dataSource)
{
    private static readonly HashSet<string> TransactionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchase", "adjustment", "consumption", "destruction", "transfer"
    };

    public async Task<InventoryResponse> GetInventoryAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var header = await GetHeaderAsync(connection, cancellationToken);
        var items = await GetItemsAsync(connection, header.BaseDate, cancellationToken);
        var transactions = await GetTransactionsAsync(connection, cancellationToken);
        var lots = items.SelectMany(item => item.Lots).ToList();

        return new InventoryResponse(
            header.DatasetId,
            header.DatasetVersion,
            header.BaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            new InventorySummary(
                ActiveItems: items.Count,
                ActiveLots: lots.Count(lot => string.Equals(lot.Status, "active", StringComparison.OrdinalIgnoreCase)),
                BelowReorderPoint: items.Count(item => item.BelowReorderPoint),
                ExpiringWithin90Days: lots.Count(lot => lot.ExpirationDate is not null
                    && DateOnly.Parse(lot.ExpirationDate, CultureInfo.InvariantCulture) <= header.BaseDate.AddDays(90)),
                InventoryValue: items.Sum(item => item.InventoryValue)),
            items,
            transactions);
    }

    public async Task<InventoryMutationResponse?> CreateTransactionAsync(
        InventoryTransactionCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (!TransactionTypes.Contains(request.TransactionType)
            || request.Quantity == 0
            || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("A supported transaction type, nonzero quantity, and authenticated user are required.");
        }

        var normalizedType = request.TransactionType.Trim().ToLowerInvariant();
        if ((normalizedType is "purchase" or "consumption" or "destruction" or "transfer") && request.Quantity < 0)
        {
            throw new ArgumentException("Only adjustments may use a negative quantity.");
        }

        var quantityDelta = normalizedType is "consumption" or "destruction" or "transfer"
            ? -request.Quantity
            : request.Quantity;
        var now = DateTimeOffset.UtcNow;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lotCommand = connection.CreateCommand();
        lotCommand.Transaction = transaction;
        lotCommand.CommandText = """
            select l.lot_id, l.item_id, f.code, f.name, l.lot_number, l.expiration_date,
              l.quantity_on_hand, l.unit_cost, l.status, i.reorder_point
            from inventory_lots l
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            where l.lot_id = @lot_id
            for update;
            """;
        lotCommand.Parameters.AddWithValue("lot_id", request.LotId);
        await using var lotReader = await lotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await lotReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var existingQuantity = lotReader.GetDecimal(6);
        var updatedQuantity = existingQuantity + quantityDelta;
        if (updatedQuantity < 0)
        {
            throw new ArgumentException("The requested inventory activity would make the lot quantity negative.");
        }

        var lot = new InventoryLot(
            lotReader.GetInt32(0), lotReader.GetString(2), lotReader.GetString(3), lotReader.GetString(4),
            lotReader.IsDBNull(5) ? null : lotReader.GetFieldValue<DateOnly>(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            updatedQuantity, lotReader.GetDecimal(7), lotReader.GetString(8));
        var itemId = lotReader.GetInt32(1);
        var reorderPoint = lotReader.GetDecimal(9);
        await lotReader.DisposeAsync();

        await using (var updateLot = connection.CreateCommand())
        {
            updateLot.Transaction = transaction;
            updateLot.CommandText = "update inventory_lots set quantity_on_hand = @quantity where lot_id = @lot_id;";
            updateLot.Parameters.AddWithValue("quantity", updatedQuantity);
            updateLot.Parameters.AddWithValue("lot_id", request.LotId);
            await updateLot.ExecuteNonQueryAsync(cancellationToken);
        }

        var transactionId = Guid.NewGuid();
        await using (var insertTransaction = connection.CreateCommand())
        {
            insertTransaction.Transaction = transaction;
            insertTransaction.CommandText = """
                insert into inventory_transactions (
                  transaction_id, lot_id, transaction_type, quantity_delta, reason, performed_by, occurred_at
                ) values (
                  @transaction_id, @lot_id, @transaction_type, @quantity_delta, @reason, @performed_by, @occurred_at
                );
                """;
            insertTransaction.Parameters.AddWithValue("transaction_id", transactionId);
            insertTransaction.Parameters.AddWithValue("lot_id", request.LotId);
            insertTransaction.Parameters.AddWithValue("transaction_type", normalizedType);
            insertTransaction.Parameters.AddWithValue("quantity_delta", quantityDelta);
            insertTransaction.Parameters.AddWithValue("reason", (object?)NormalizeOptional(request.Reason) ?? DBNull.Value);
            insertTransaction.Parameters.AddWithValue("performed_by", username);
            insertTransaction.Parameters.AddWithValue("occurred_at", now);
            await insertTransaction.ExecuteNonQueryAsync(cancellationToken);
        }

        decimal itemQuantity;
        await using (var itemQuantityCommand = connection.CreateCommand())
        {
            itemQuantityCommand.Transaction = transaction;
            itemQuantityCommand.CommandText = "select coalesce(sum(quantity_on_hand), 0) from inventory_lots where item_id = @item_id and status = 'active';";
            itemQuantityCommand.Parameters.AddWithValue("item_id", itemId);
            itemQuantity = Convert.ToDecimal(await itemQuantityCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        await transaction.CommitAsync(cancellationToken);
        return new InventoryMutationResponse(
            new InventoryTransactionItem(transactionId, request.LotId, string.Empty, string.Empty, lot.FacilityCode,
                normalizedType, quantityDelta, NormalizeOptional(request.Reason), username, now),
            lot,
            itemQuantity,
            itemQuantity <= reorderPoint);
    }

    private static async Task<InventoryHeader> GetHeaderAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select dataset_id, version, base_date from dataset_metadata order by generated_at desc limit 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Dataset metadata is required for inventory.");
        }

        return new InventoryHeader(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2));
    }

    private static async Task<IReadOnlyList<InventoryItem>> GetItemsAsync(NpgsqlConnection connection, DateOnly baseDate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select i.item_id, i.item_code, i.name, i.category, i.unit, i.reorder_point, i.preferred_quantity,
              l.lot_id, f.code, f.name, l.lot_number, l.expiration_date, l.quantity_on_hand, l.unit_cost, l.status
            from inventory_items i
            left join inventory_lots l on l.item_id = i.item_id
            left join facilities f on f.id = l.facility_id
            where i.active = true
            order by i.category, i.name, l.expiration_date nulls last, l.lot_id;
            """;
        var builders = new Dictionary<int, InventoryItemBuilder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemId = reader.GetInt32(0);
            if (!builders.TryGetValue(itemId, out var builder))
            {
                builder = new InventoryItemBuilder(itemId, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6));
                builders.Add(itemId, builder);
            }

            if (!reader.IsDBNull(7))
            {
                builder.Lots.Add(new InventoryLot(
                    reader.GetInt32(7), reader.GetString(8), reader.GetString(9), reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    reader.GetDecimal(12), reader.GetDecimal(13), reader.GetString(14)));
            }
        }

        return builders.Values.Select(builder => builder.Build()).ToList();
    }

    private static async Task<IReadOnlyList<InventoryTransactionItem>> GetTransactionsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select t.transaction_id, t.lot_id, i.item_code, i.name, f.code, t.transaction_type,
              t.quantity_delta, t.reason, t.performed_by, t.occurred_at
            from inventory_transactions t
            join inventory_lots l on l.lot_id = t.lot_id
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            order by t.occurred_at desc
            limit 50;
            """;
        var entries = new List<InventoryTransactionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new InventoryTransactionItem(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetDecimal(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return entries;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record InventoryHeader(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed class InventoryItemBuilder(int itemId, string itemCode, string name, string category, string unit, decimal reorderPoint, decimal preferredQuantity)
    {
        public List<InventoryLot> Lots { get; } = [];

        public InventoryItem Build()
        {
            var quantity = Lots.Where(lot => string.Equals(lot.Status, "active", StringComparison.OrdinalIgnoreCase)).Sum(lot => lot.QuantityOnHand);
            return new InventoryItem(itemId, itemCode, name, category, unit, reorderPoint, preferredQuantity, quantity,
                Lots.Sum(lot => lot.QuantityOnHand * lot.UnitCost), quantity <= reorderPoint, Lots);
        }
    }
}
