using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryRepository(NpgsqlDataSource dataSource)
{
    private static readonly HashSet<string> TransactionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "adjustment", "consumption", "destruction", "return"
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
                ExpiredLots: lots.Count(lot => lot.ExpiryStatus == "expired"),
                ExpiringWithin90Days: lots.Count(lot => lot.ExpiryStatus == "expiring"),
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
        if ((normalizedType is "consumption" or "destruction" or "return") && request.Quantity < 0)
        {
            throw new ArgumentException("Only adjustments may use a negative quantity.");
        }

        if (normalizedType == "return" && string.IsNullOrWhiteSpace(NormalizeOptional(request.Reason)))
        {
            throw new ArgumentException("A reason is required for an inventory return.");
        }

        var quantityDelta = normalizedType is "consumption" or "destruction" or "return"
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

    public async Task<InventoryLotMetadataUpdateResponse?> UpdateLotMetadataAsync(
        int lotId,
        InventoryLotMetadataUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var lotNumber = request.LotNumber?.Trim();
        if (lotId <= 0 || string.IsNullOrWhiteSpace(lotNumber) || lotNumber.Length > 80 || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("A lot identifier of 80 characters or fewer and an authenticated user are required.");
        }

        var expirationDate = ParseOptionalDate(request.ExpirationDate, "Lot expiration must be an ISO date.");
        var changedAt = DateTimeOffset.UtcNow;
        var auditId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lotCommand = connection.CreateCommand();
        lotCommand.Transaction = transaction;
        lotCommand.CommandText = """
            select l.item_id, l.facility_id, f.code, f.name, l.lot_number, l.expiration_date, l.quantity_on_hand, l.unit_cost, l.status
            from inventory_lots l
            join facilities f on f.id = l.facility_id
            where l.lot_id = @lotId
            for update;
            """;
        lotCommand.Parameters.AddWithValue("lotId", lotId);
        await using var reader = await lotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var itemId = reader.GetInt32(0);
        var facilityId = reader.GetInt32(1);
        var facilityCode = reader.GetString(2);
        var facilityName = reader.GetString(3);
        var priorLotNumber = reader.GetString(4);
        var priorExpirationDate = reader.IsDBNull(5) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(5);
        var quantityOnHand = reader.GetDecimal(6);
        var unitCost = reader.GetDecimal(7);
        var status = reader.GetString(8);
        await reader.DisposeAsync();

        await using (var duplicateCommand = connection.CreateCommand())
        {
            duplicateCommand.Transaction = transaction;
            duplicateCommand.CommandText = "select exists(select 1 from inventory_lots where item_id = @itemId and facility_id = @facilityId and lot_number = @lotNumber and lot_id <> @lotId);";
            duplicateCommand.Parameters.AddWithValue("itemId", itemId);
            duplicateCommand.Parameters.AddWithValue("facilityId", facilityId);
            duplicateCommand.Parameters.AddWithValue("lotNumber", lotNumber);
            duplicateCommand.Parameters.AddWithValue("lotId", lotId);
            if (await duplicateCommand.ExecuteScalarAsync(cancellationToken) is true)
            {
                throw new ArgumentException("A lot with that identifier already exists for this item and facility.");
            }
        }

        if (priorLotNumber != lotNumber || priorExpirationDate != expirationDate)
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "update inventory_lots set lot_number = @lotNumber, expiration_date = @expirationDate where lot_id = @lotId;";
            updateCommand.Parameters.AddWithValue("lotNumber", lotNumber);
            updateCommand.Parameters.AddWithValue("expirationDate", (object?)expirationDate ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("lotId", lotId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var auditCommand = connection.CreateCommand();
            auditCommand.Transaction = transaction;
            auditCommand.CommandText = "insert into inventory_lot_metadata_audits (audit_id, lot_id, prior_lot_number, new_lot_number, prior_expiration_date, new_expiration_date, changed_by, changed_at) values (@auditId, @lotId, @priorLotNumber, @newLotNumber, @priorExpirationDate, @newExpirationDate, @changedBy, @changedAt);";
            auditCommand.Parameters.AddWithValue("auditId", auditId);
            auditCommand.Parameters.AddWithValue("lotId", lotId);
            auditCommand.Parameters.AddWithValue("priorLotNumber", priorLotNumber);
            auditCommand.Parameters.AddWithValue("newLotNumber", lotNumber);
            auditCommand.Parameters.AddWithValue("priorExpirationDate", (object?)priorExpirationDate ?? DBNull.Value);
            auditCommand.Parameters.AddWithValue("newExpirationDate", (object?)expirationDate ?? DBNull.Value);
            auditCommand.Parameters.AddWithValue("changedBy", username);
            auditCommand.Parameters.AddWithValue("changedAt", changedAt);
            await auditCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new InventoryLotMetadataUpdateResponse(
            auditId,
            new InventoryLot(lotId, facilityCode, facilityName, lotNumber, expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), quantityOnHand, unitCost, status),
            username,
            changedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<InventoryLotMetadataAuditItem>?> GetLotMetadataHistoryAsync(int lotId, CancellationToken cancellationToken)
    {
        if (lotId <= 0) return null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = "select exists(select 1 from inventory_lots where lot_id = @lotId);";
        existsCommand.Parameters.AddWithValue("lotId", lotId);
        if (await existsCommand.ExecuteScalarAsync(cancellationToken) is not true) return null;

        await using var command = connection.CreateCommand();
        command.CommandText = "select audit_id, prior_lot_number, new_lot_number, prior_expiration_date, new_expiration_date, changed_by, changed_at from inventory_lot_metadata_audits where lot_id = @lotId order by changed_at desc, audit_id desc limit 50;";
        command.Parameters.AddWithValue("lotId", lotId);
        var entries = new List<InventoryLotMetadataAuditItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new InventoryLotMetadataAuditItem(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6).ToString("O", CultureInfo.InvariantCulture)));
        }
        return entries;
    }

    public async Task<InventoryActivityReportResponse> GetActivityReportAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? facilityId,
        CancellationToken cancellationToken)
    {
        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            throw new ArgumentException("The activity report start date cannot be after its end date.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var header = await GetHeaderAsync(connection, cancellationToken);
        var totalEntries = await CountActivityEntriesAsync(connection, fromDate, toDate, facilityId, cancellationToken);
        var entries = await GetActivityEntriesAsync(connection, fromDate, toDate, facilityId, 500, cancellationToken);
        return new InventoryActivityReportResponse(
            header.DatasetId,
            header.DatasetVersion,
            fromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            toDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            facilityId,
            totalEntries,
            entries);
    }

    public async Task<string> GetActivityReportCsvAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? facilityId,
        CancellationToken cancellationToken)
    {
        var report = await GetActivityReportAsync(fromDate, toDate, facilityId, cancellationToken);
        var csv = new StringBuilder();
        AppendCsvRow(csv, "Occurred At", "Item Code", "Item Name", "Facility", "Transaction Type", "Quantity Delta", "Counterparty Facility", "Reason", "Performed By", "Transfer ID", "Receipt ID", "Receipt Reference");
        foreach (var entry in report.Entries)
        {
            AppendCsvRow(csv,
                entry.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                entry.ItemCode,
                entry.ItemName,
                entry.FacilityCode,
                entry.TransactionType,
                entry.QuantityDelta.ToString(CultureInfo.InvariantCulture),
                entry.CounterpartyFacilityCode ?? string.Empty,
                entry.Reason ?? string.Empty,
                entry.PerformedBy,
                entry.TransferId?.ToString() ?? string.Empty,
                entry.ReceiptId?.ToString() ?? string.Empty,
                entry.ReceiptReference ?? string.Empty);
        }
        return csv.ToString();
    }

    public async Task<InventoryVendorListResponse> GetVendorsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select vendor_id, name, contact_name, phone, email, active from inventory_vendors where active = true order by name, vendor_id;";
        var vendors = new List<InventoryVendor>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            vendors.Add(ReadVendor(reader));
        }
        return new InventoryVendorListResponse(vendors);
    }

    public async Task<InventoryVendor> CreateVendorAsync(InventoryVendorCreateRequest request, string username, CancellationToken cancellationToken)
    {
        ValidateVendor(request);
        var vendor = new InventoryVendor(Guid.NewGuid(), request.Name.Trim(), NormalizeOptional(request.ContactName), NormalizeOptional(request.Phone), NormalizeOptional(request.Email), true);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "insert into inventory_vendors (vendor_id, name, contact_name, phone, email, active, created_by) values (@id, @name, @contact, @phone, @email, true, @user);";
        command.Parameters.AddWithValue("id", vendor.VendorId);
        command.Parameters.AddWithValue("name", vendor.Name);
        command.Parameters.AddWithValue("contact", (object?)vendor.ContactName ?? DBNull.Value);
        command.Parameters.AddWithValue("phone", (object?)vendor.Phone ?? DBNull.Value);
        command.Parameters.AddWithValue("email", (object?)vendor.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("user", username);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            throw new ArgumentException("A vendor with that name already exists.");
        }
        return vendor;
    }

    public async Task<InventoryPurchaseReceiptResponse> CreatePurchaseReceiptAsync(
        InventoryPurchaseReceiptCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        ValidatePurchaseReceipt(request, username);
        var expirationDate = ParseOptionalDate(request.ExpirationDate, "Lot expiration must be an ISO date.");
        var referenceNumber = NormalizeOptional(request.ReferenceNumber);
        var now = DateTimeOffset.UtcNow;
        var receiptId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var vendor = await GetActiveVendorAsync(connection, transaction, request.VendorId, cancellationToken)
            ?? throw new ArgumentException("The selected vendor was not found or is inactive.");
        var facility = await GetFacilityAsync(connection, transaction, request.FacilityId, cancellationToken)
            ?? throw new ArgumentException("The selected facility was not found.");
        var item = await GetActiveItemAsync(connection, transaction, request.ItemId, cancellationToken)
            ?? throw new ArgumentException("The selected inventory item was not found or is inactive.");

        await using (var receiptCommand = connection.CreateCommand())
        {
            receiptCommand.Transaction = transaction;
            receiptCommand.CommandText = "insert into inventory_purchase_receipts (receipt_id, vendor_id, facility_id, reference_number, received_at, received_by, notes) values (@id, @vendor, @facility, @reference, @received, @receivedBy, @notes);";
            receiptCommand.Parameters.AddWithValue("id", receiptId);
            receiptCommand.Parameters.AddWithValue("vendor", vendor.VendorId);
            receiptCommand.Parameters.AddWithValue("facility", facility.FacilityId);
            receiptCommand.Parameters.AddWithValue("reference", (object?)referenceNumber ?? DBNull.Value);
            receiptCommand.Parameters.AddWithValue("received", now);
            receiptCommand.Parameters.AddWithValue("receivedBy", username);
            receiptCommand.Parameters.AddWithValue("notes", request.Notes.Trim());
            try
            {
                await receiptCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == "23505")
            {
                throw new ArgumentException("That vendor reference number has already been received.");
            }
        }

        var lot = await GetOrCreatePurchasedLotAsync(connection, transaction, item, facility, request.LotNumber.Trim(), expirationDate, request.Quantity, request.UnitCost, cancellationToken);
        var transactionId = Guid.NewGuid();
        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText = "insert into inventory_transactions (transaction_id, lot_id, receipt_id, transaction_type, quantity_delta, reason, performed_by, occurred_at) values (@id, @lot, @receipt, 'purchase', @quantity, @reason, @user, @occurred);";
            ledgerCommand.Parameters.AddWithValue("id", transactionId);
            ledgerCommand.Parameters.AddWithValue("lot", lot.LotId);
            ledgerCommand.Parameters.AddWithValue("receipt", receiptId);
            ledgerCommand.Parameters.AddWithValue("quantity", request.Quantity);
            ledgerCommand.Parameters.AddWithValue("reason", request.Notes.Trim());
            ledgerCommand.Parameters.AddWithValue("user", username);
            ledgerCommand.Parameters.AddWithValue("occurred", now);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var itemQuantity = await GetItemQuantityAsync(connection, transaction, item.ItemId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var ledgerEntry = new InventoryTransactionItem(transactionId, lot.LotId, item.ItemCode, item.Name, facility.Code, "purchase", request.Quantity,
            request.Notes.Trim(), username, now, null, null, receiptId, referenceNumber);
        return new InventoryPurchaseReceiptResponse(receiptId, vendor, facility.Code, facility.Name, referenceNumber, now.ToString("O", CultureInfo.InvariantCulture), username,
            request.Notes.Trim(), lot, ledgerEntry, itemQuantity, itemQuantity <= item.ReorderPoint);
    }

    public async Task<InventoryCountReconciliationResponse?> CreateCountReconciliationAsync(
        InventoryCountReconciliationCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (request.LotId <= 0 || request.CountedQuantity < 0 || string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Trim().Length > 500 || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("A lot, non-negative counted quantity, required count notes, and authenticated user are required.");
        }

        var now = DateTimeOffset.UtcNow;
        var reconciliationId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lotCommand = connection.CreateCommand();
        lotCommand.Transaction = transaction;
        lotCommand.CommandText = "select l.lot_id, l.item_id, f.code, f.name, l.lot_number, l.expiration_date, l.quantity_on_hand, l.unit_cost, l.status, i.item_code, i.name, i.reorder_point from inventory_lots l join inventory_items i on i.item_id=l.item_id join facilities f on f.id=l.facility_id where l.lot_id=@lot for update;";
        lotCommand.Parameters.AddWithValue("lot", request.LotId);
        await using var reader = await lotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var itemId = reader.GetInt32(1);
        var facilityCode = reader.GetString(2);
        var facilityName = reader.GetString(3);
        var lotNumber = reader.GetString(4);
        var expirationDate = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedQuantity = reader.GetDecimal(6);
        var unitCost = reader.GetDecimal(7);
        var status = reader.GetString(8);
        var itemCode = reader.GetString(9);
        var itemName = reader.GetString(10);
        var reorderPoint = reader.GetDecimal(11);
        await reader.DisposeAsync();
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only active inventory lots can be reconciled.");

        var quantityDelta = request.CountedQuantity - expectedQuantity;
        var lot = new InventoryLot(request.LotId, facilityCode, facilityName, lotNumber, expirationDate, request.CountedQuantity, unitCost, status);
        await using (var reconciliationCommand = connection.CreateCommand())
        {
            reconciliationCommand.Transaction = transaction;
            reconciliationCommand.CommandText = "insert into inventory_count_reconciliations (reconciliation_id, lot_id, expected_quantity, counted_quantity, notes, counted_by, counted_at) values (@id, @lot, @expected, @counted, @notes, @user, @at);";
            reconciliationCommand.Parameters.AddWithValue("id", reconciliationId);
            reconciliationCommand.Parameters.AddWithValue("lot", request.LotId);
            reconciliationCommand.Parameters.AddWithValue("expected", expectedQuantity);
            reconciliationCommand.Parameters.AddWithValue("counted", request.CountedQuantity);
            reconciliationCommand.Parameters.AddWithValue("notes", request.Notes.Trim());
            reconciliationCommand.Parameters.AddWithValue("user", username);
            reconciliationCommand.Parameters.AddWithValue("at", now);
            await reconciliationCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var updateLotCommand = connection.CreateCommand())
        {
            updateLotCommand.Transaction = transaction;
            updateLotCommand.CommandText = "update inventory_lots set quantity_on_hand=@quantity where lot_id=@lot;";
            updateLotCommand.Parameters.AddWithValue("quantity", request.CountedQuantity);
            updateLotCommand.Parameters.AddWithValue("lot", request.LotId);
            await updateLotCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText = "insert into inventory_transactions (transaction_id, lot_id, reconciliation_id, transaction_type, quantity_delta, reason, performed_by, occurred_at) values (@id, @lot, @reconciliation, 'adjustment', @delta, @notes, @user, @at);";
            ledgerCommand.Parameters.AddWithValue("id", transactionId);
            ledgerCommand.Parameters.AddWithValue("lot", request.LotId);
            ledgerCommand.Parameters.AddWithValue("reconciliation", reconciliationId);
            ledgerCommand.Parameters.AddWithValue("delta", quantityDelta);
            ledgerCommand.Parameters.AddWithValue("notes", request.Notes.Trim());
            ledgerCommand.Parameters.AddWithValue("user", username);
            ledgerCommand.Parameters.AddWithValue("at", now);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        var itemQuantity = await GetItemQuantityAsync(connection, transaction, itemId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var ledgerEntry = new InventoryTransactionItem(transactionId, request.LotId, itemCode, itemName, facilityCode, "adjustment", quantityDelta,
            request.Notes.Trim(), username, now, null, null, null, null, reconciliationId);
        return new InventoryCountReconciliationResponse(reconciliationId, request.LotId, expectedQuantity, request.CountedQuantity, quantityDelta, request.Notes.Trim(), username,
            now.ToString("O", CultureInfo.InvariantCulture), lot, ledgerEntry, itemQuantity, itemQuantity <= reorderPoint);
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

    private static async Task<InventoryVendor?> GetActiveVendorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select vendor_id, name, contact_name, phone, email, active from inventory_vendors where vendor_id = @id and active = true for update;";
        command.Parameters.AddWithValue("id", vendorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadVendor(reader) : null;
    }

    private static async Task<InventoryItemIdentity?> GetActiveItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int itemId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select item_id, item_code, name, reorder_point from inventory_items where item_id = @id and active = true for update;";
        command.Parameters.AddWithValue("id", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new InventoryItemIdentity(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3))
            : null;
    }

    private static async Task<decimal> GetItemQuantityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int itemId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(sum(quantity_on_hand), 0) from inventory_lots where item_id = @item_id and status = 'active';";
        command.Parameters.AddWithValue("item_id", itemId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<InventoryLot> GetOrCreatePurchasedLotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        InventoryItemIdentity item,
        InventoryFacility facility,
        string lotNumber,
        DateOnly? expirationDate,
        decimal quantity,
        decimal unitCost,
        CancellationToken cancellationToken)
    {
        await using var existingCommand = connection.CreateCommand();
        existingCommand.Transaction = transaction;
        existingCommand.CommandText = "select lot_id, expiration_date, quantity_on_hand, status from inventory_lots where item_id = @item and facility_id = @facility and lot_number = @lot for update;";
        existingCommand.Parameters.AddWithValue("item", item.ItemId);
        existingCommand.Parameters.AddWithValue("facility", facility.FacilityId);
        existingCommand.Parameters.AddWithValue("lot", lotNumber);
        await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var lotId = reader.GetInt32(0);
            var existingExpiration = reader.IsDBNull(1) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(1);
            var updatedQuantity = reader.GetDecimal(2) + quantity;
            var status = reader.GetString(3);
            await reader.DisposeAsync();
            if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The matching inventory lot is inactive and cannot receive a purchase.");
            }
            if (existingExpiration != expirationDate)
            {
                throw new ArgumentException("The matching inventory lot has different expiry metadata. Use the correct lot number.");
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "update inventory_lots set quantity_on_hand = @quantity, unit_cost = @unit_cost where lot_id = @id;";
            updateCommand.Parameters.AddWithValue("quantity", updatedQuantity);
            updateCommand.Parameters.AddWithValue("unit_cost", unitCost);
            updateCommand.Parameters.AddWithValue("id", lotId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return new InventoryLot(lotId, facility.Code, facility.Name, lotNumber, expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedQuantity, unitCost, status);
        }
        await reader.DisposeAsync();

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = "insert into inventory_lots (item_id, facility_id, lot_number, expiration_date, quantity_on_hand, unit_cost, status) values (@item, @facility, @lot, @expiration, @quantity, @unit_cost, 'active') returning lot_id;";
        insertCommand.Parameters.AddWithValue("item", item.ItemId);
        insertCommand.Parameters.AddWithValue("facility", facility.FacilityId);
        insertCommand.Parameters.AddWithValue("lot", lotNumber);
        insertCommand.Parameters.AddWithValue("expiration", (object?)expirationDate ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("quantity", quantity);
        insertCommand.Parameters.AddWithValue("unit_cost", unitCost);
        var newLotId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        return new InventoryLot(newLotId, facility.Code, facility.Name, lotNumber, expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), quantity, unitCost, "active");
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
                    reader.GetDecimal(12), reader.GetDecimal(13), reader.GetString(14),
                    GetExpiryStatus(reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11), baseDate)));
            }
        }

        return builders.Values.Select(builder => builder.Build()).ToList();
    }

    private static string GetExpiryStatus(DateOnly? expirationDate, DateOnly asOfDate)
    {
        if (expirationDate is null)
        {
            return "not-tracked";
        }

        if (expirationDate <= asOfDate)
        {
            return "expired";
        }

        return expirationDate <= asOfDate.AddDays(90) ? "expiring" : "current";
    }

    private static async Task<IReadOnlyList<InventoryTransactionItem>> GetTransactionsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        return await GetActivityEntriesAsync(connection, null, null, null, 50, cancellationToken);
    }

    private static async Task<int> CountActivityEntriesAsync(
        NpgsqlConnection connection,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? facilityId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*)
            from inventory_transactions t
            join inventory_lots l on l.lot_id = t.lot_id
            where (@from_date is null or t.occurred_at >= @from_date)
              and (@to_date is null or t.occurred_at < @to_date)
              and (@facility_id is null or l.facility_id = @facility_id);
            """;
        AddActivityFilterParameters(command, fromDate, toDate, facilityId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<InventoryTransactionItem>> GetActivityEntriesAsync(
        NpgsqlConnection connection,
        DateOnly? fromDate,
        DateOnly? toDate,
        int? facilityId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select t.transaction_id, t.lot_id, i.item_code, i.name, f.code, t.transaction_type,
              t.quantity_delta, t.reason, t.performed_by, t.occurred_at, t.transfer_id, counterpart_facility.code,
              t.receipt_id, receipt.reference_number, t.reconciliation_id
            from inventory_transactions t
            join inventory_lots l on l.lot_id = t.lot_id
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            left join inventory_purchase_receipts receipt on receipt.receipt_id = t.receipt_id
            left join lateral (
              select paired_facility.code
              from inventory_transactions paired
              join inventory_lots paired_lot on paired_lot.lot_id = paired.lot_id
              join facilities paired_facility on paired_facility.id = paired_lot.facility_id
              where paired.transfer_id = t.transfer_id and paired.transaction_id <> t.transaction_id
              limit 1
            ) counterpart_facility on t.transfer_id is not null
            where (@from_date is null or t.occurred_at >= @from_date)
              and (@to_date is null or t.occurred_at < @to_date)
              and (@facility_id is null or l.facility_id = @facility_id)
            order by t.occurred_at desc
            limit @limit;
            """;
        AddActivityFilterParameters(command, fromDate, toDate, facilityId);
        command.Parameters.AddWithValue("limit", limit);
        var entries = new List<InventoryTransactionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new InventoryTransactionItem(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetDecimal(6), reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetGuid(12),
                reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetGuid(14)));
        }

        return entries;
    }

    private static void AddActivityFilterParameters(NpgsqlCommand command, DateOnly? fromDate, DateOnly? toDate, int? facilityId)
    {
        DateTimeOffset? fromTimestamp = fromDate is null ? null : new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        DateTimeOffset? toTimestamp = toDate is null ? null : new DateTimeOffset(toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        command.Parameters.Add(new NpgsqlParameter("from_date", NpgsqlDbType.TimestampTz) { Value = (object?)fromTimestamp ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("to_date", NpgsqlDbType.TimestampTz) { Value = (object?)toTimestamp ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("facility_id", NpgsqlDbType.Integer) { Value = (object?)facilityId ?? DBNull.Value });
    }

    private static void AppendCsvRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(',', values.Select(value => $"\"{value.Replace("\"", "\"\"")}\"")));
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseOptionalDate(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new ArgumentException(errorMessage);
    }

    private static void ValidateVendor(InventoryVendorCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160
            || request.ContactName?.Trim().Length > 160 || request.Phone?.Trim().Length > 80 || request.Email?.Trim().Length > 254)
        {
            throw new ArgumentException("Vendor name or contact details are invalid.");
        }
    }

    private static void ValidatePurchaseReceipt(InventoryPurchaseReceiptCreateRequest request, string username)
    {
        if (request.VendorId == Guid.Empty || request.FacilityId <= 0 || request.ItemId <= 0 || string.IsNullOrWhiteSpace(request.LotNumber)
            || request.LotNumber.Trim().Length > 80 || request.Quantity <= 0 || request.UnitCost < 0 || request.ReferenceNumber?.Trim().Length > 120
            || string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Trim().Length > 500 || string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Vendor, facility, item, lot, positive quantity, unit cost, and receipt notes are required.");
        }
    }

    private static InventoryVendor ReadVendor(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetBoolean(5));

    private sealed record InventoryHeader(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record InventoryItemIdentity(int ItemId, string ItemCode, string Name, decimal ReorderPoint);

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
