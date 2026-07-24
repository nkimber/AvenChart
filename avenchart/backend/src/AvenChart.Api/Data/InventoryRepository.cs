using System.Globalization;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryRepository(NpgsqlDataSource dataSource)
{
    private static readonly HashSet<string> TransactionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchase", "adjustment", "consumption", "destruction"
    };

    public async Task<InventoryResponse> GetInventoryAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var header = await GetHeaderAsync(connection, cancellationToken);
        var facilities = await GetFacilitiesAsync(connection, cancellationToken);
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
            facilities,
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
        if ((normalizedType is "purchase" or "consumption" or "destruction") && request.Quantity < 0)
        {
            throw new ArgumentException("Only adjustments may use a negative quantity.");
        }

        var quantityDelta = normalizedType is "consumption" or "destruction"
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
                normalizedType, quantityDelta, NormalizeOptional(request.Reason), username, now, null, null),
            lot,
            itemQuantity,
            itemQuantity <= reorderPoint);
    }

    public async Task<InventoryMutationResponse?> CreateTransferAsync(
        InventoryTransferCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (request.SourceLotId <= 0 || request.DestinationFacilityId <= 0 || request.Quantity <= 0 || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("A source lot, a destination facility, a positive quantity, and an authenticated user are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var transferId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var sourceCommand = connection.CreateCommand();
        sourceCommand.Transaction = transaction;
        sourceCommand.CommandText = """
            select l.lot_id, l.item_id, l.facility_id, f.code, f.name, l.lot_number, l.expiration_date,
              l.quantity_on_hand, l.unit_cost, l.status, i.reorder_point
            from inventory_lots l
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            where l.lot_id = @lot_id
            for update;
            """;
        sourceCommand.Parameters.AddWithValue("lot_id", request.SourceLotId);
        await using var sourceReader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
        if (!await sourceReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var sourceLotId = sourceReader.GetInt32(0);
        var itemId = sourceReader.GetInt32(1);
        var sourceFacilityId = sourceReader.GetInt32(2);
        var sourceFacilityCode = sourceReader.GetString(3);
        var sourceFacilityName = sourceReader.GetString(4);
        var lotNumber = sourceReader.GetString(5);
        var expirationDate = sourceReader.IsDBNull(6) ? (DateOnly?)null : sourceReader.GetFieldValue<DateOnly>(6);
        var sourceQuantity = sourceReader.GetDecimal(7);
        var unitCost = sourceReader.GetDecimal(8);
        var lotStatus = sourceReader.GetString(9);
        var reorderPoint = sourceReader.GetDecimal(10);
        await sourceReader.DisposeAsync();

        if (!string.Equals(lotStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only active inventory lots can be transferred.");
        }
        if (sourceFacilityId == request.DestinationFacilityId)
        {
            throw new ArgumentException("The destination facility must differ from the source facility.");
        }
        if (sourceQuantity < request.Quantity)
        {
            throw new ArgumentException("The requested transfer quantity exceeds the source lot quantity on hand.");
        }

        var destinationFacility = await GetFacilityAsync(connection, transaction, request.DestinationFacilityId, cancellationToken);
        if (destinationFacility is null)
        {
            throw new ArgumentException("The destination facility was not found.");
        }

        var destinationLot = await GetOrCreateDestinationLotAsync(
            connection, transaction, itemId, destinationFacility, lotNumber, expirationDate, unitCost, request.Quantity, cancellationToken);
        var updatedSourceQuantity = sourceQuantity - request.Quantity;
        var sourceLot = new InventoryLot(sourceLotId, sourceFacilityCode, sourceFacilityName, lotNumber,
            expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedSourceQuantity, unitCost, lotStatus);

        await using (var updateSource = connection.CreateCommand())
        {
            updateSource.Transaction = transaction;
            updateSource.CommandText = "update inventory_lots set quantity_on_hand = @quantity where lot_id = @lot_id;";
            updateSource.Parameters.AddWithValue("quantity", updatedSourceQuantity);
            updateSource.Parameters.AddWithValue("lot_id", sourceLotId);
            await updateSource.ExecuteNonQueryAsync(cancellationToken);
        }

        var sourceTransactionId = Guid.NewGuid();
        await InsertTransactionAsync(connection, transaction, sourceTransactionId, sourceLotId, transferId, "transfer", -request.Quantity,
            request.Reason, username, now, cancellationToken);
        await InsertTransactionAsync(connection, transaction, Guid.NewGuid(), destinationLot.LotId, transferId, "transfer", request.Quantity,
            request.Reason, username, now, cancellationToken);

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
            new InventoryTransactionItem(sourceTransactionId, sourceLotId, string.Empty, string.Empty, sourceFacilityCode, "transfer",
                -request.Quantity, NormalizeOptional(request.Reason), username, now, transferId, destinationFacility.Code),
            sourceLot,
            itemQuantity,
            itemQuantity <= reorderPoint,
            destinationLot,
            transferId);
    }

    private static async Task<IReadOnlyList<InventoryFacility>> GetFacilitiesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select id, code, name from facilities order by name, id;";
        var facilities = new List<InventoryFacility>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facilities.Add(new InventoryFacility(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return facilities;
    }

    private static async Task<InventoryFacility?> GetFacilityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int facilityId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id, code, name from facilities where id = @facility_id;";
        command.Parameters.AddWithValue("facility_id", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new InventoryFacility(reader.GetInt32(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    private static async Task<InventoryLot> GetOrCreateDestinationLotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int itemId,
        InventoryFacility destinationFacility,
        string lotNumber,
        DateOnly? expirationDate,
        decimal unitCost,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        await using var existingCommand = connection.CreateCommand();
        existingCommand.Transaction = transaction;
        existingCommand.CommandText = """
            select lot_id, quantity_on_hand, expiration_date, unit_cost, status
            from inventory_lots
            where item_id = @item_id and facility_id = @facility_id and lot_number = @lot_number
            for update;
            """;
        existingCommand.Parameters.AddWithValue("item_id", itemId);
        existingCommand.Parameters.AddWithValue("facility_id", destinationFacility.FacilityId);
        existingCommand.Parameters.AddWithValue("lot_number", lotNumber);
        await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var lotId = reader.GetInt32(0);
            var updatedQuantity = reader.GetDecimal(1) + quantity;
            var existingExpiration = reader.IsDBNull(2) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(2);
            var existingCost = reader.GetDecimal(3);
            var status = reader.GetString(4);
            await reader.DisposeAsync();
            if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The matching destination lot is not active.");
            }
            if (existingExpiration != expirationDate || existingCost != unitCost)
            {
                throw new ArgumentException("The matching destination lot has different expiry or unit-cost metadata.");
            }

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "update inventory_lots set quantity_on_hand = @quantity where lot_id = @lot_id;";
            update.Parameters.AddWithValue("quantity", updatedQuantity);
            update.Parameters.AddWithValue("lot_id", lotId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            return new InventoryLot(lotId, destinationFacility.Code, destinationFacility.Name, lotNumber,
                expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedQuantity, unitCost, status);
        }
        await reader.DisposeAsync();

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            insert into inventory_lots (item_id, facility_id, lot_number, expiration_date, quantity_on_hand, unit_cost, status)
            values (@item_id, @facility_id, @lot_number, @expiration_date, @quantity, @unit_cost, 'active')
            returning lot_id;
            """;
        insert.Parameters.AddWithValue("item_id", itemId);
        insert.Parameters.AddWithValue("facility_id", destinationFacility.FacilityId);
        insert.Parameters.AddWithValue("lot_number", lotNumber);
        insert.Parameters.AddWithValue("expiration_date", (object?)expirationDate ?? DBNull.Value);
        insert.Parameters.AddWithValue("quantity", quantity);
        insert.Parameters.AddWithValue("unit_cost", unitCost);
        var newLotId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        return new InventoryLot(newLotId, destinationFacility.Code, destinationFacility.Name, lotNumber,
            expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), quantity, unitCost, "active");
    }

    private static async Task InsertTransactionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid transactionId,
        int lotId,
        Guid transferId,
        string transactionType,
        decimal quantityDelta,
        string? reason,
        string username,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into inventory_transactions (
              transaction_id, lot_id, transfer_id, transaction_type, quantity_delta, reason, performed_by, occurred_at
            ) values (
              @transaction_id, @lot_id, @transfer_id, @transaction_type, @quantity_delta, @reason, @performed_by, @occurred_at
            );
            """;
        command.Parameters.AddWithValue("transaction_id", transactionId);
        command.Parameters.AddWithValue("lot_id", lotId);
        command.Parameters.AddWithValue("transfer_id", transferId);
        command.Parameters.AddWithValue("transaction_type", transactionType);
        command.Parameters.AddWithValue("quantity_delta", quantityDelta);
        command.Parameters.AddWithValue("reason", (object?)NormalizeOptional(reason) ?? DBNull.Value);
        command.Parameters.AddWithValue("performed_by", username);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
              t.quantity_delta, t.reason, t.performed_by, t.occurred_at, t.transfer_id, counterpart_facility.code
            from inventory_transactions t
            join inventory_lots l on l.lot_id = t.lot_id
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            left join lateral (
              select paired_facility.code
              from inventory_transactions paired
              join inventory_lots paired_lot on paired_lot.lot_id = paired.lot_id
              join facilities paired_facility on paired_facility.id = paired_lot.facility_id
              where paired.transfer_id = t.transfer_id and paired.transaction_id <> t.transaction_id
              limit 1
            ) counterpart_facility on t.transfer_id is not null
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
                reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
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
