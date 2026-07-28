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

    public async Task<IReadOnlyList<InventoryMedicationCatalogItem>> GetMedicationCatalogAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select rx_norm_code, drug_name, display_name, form, strength, route from medication_vocabulary where active=true order by display_name, rx_norm_code;";
        var items = new List<InventoryMedicationCatalogItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new InventoryMedicationCatalogItem(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return items;
    }

    public async Task<InventoryMedicationLink?> UpdateMedicationLinkAsync(int itemId, InventoryMedicationLinkUpdateRequest request, string username, CancellationToken cancellationToken)
    {
        var rxNormCode = request.RxNormCode?.Trim();
        if (itemId <= 0 || string.IsNullOrWhiteSpace(rxNormCode) || rxNormCode.Length > 50 || string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("An inventory item, known RXCUI code, and authenticated user are required.");

        var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? priorCode;
        await using (var item = connection.CreateCommand())
        {
            item.Transaction = transaction;
            item.CommandText = "select l.rx_norm_code from inventory_items i left join inventory_item_medication_links l on l.item_id=i.item_id where i.item_id=@itemId and i.active=true for update of i;";
            item.Parameters.AddWithValue("itemId", itemId);
            await using var reader = await item.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            priorCode = reader.IsDBNull(0) ? null : reader.GetString(0);
        }
        await using (var vocabulary = connection.CreateCommand())
        {
            vocabulary.Transaction = transaction;
            vocabulary.CommandText = "select exists(select 1 from medication_vocabulary where rx_norm_code=@code and active=true);";
            vocabulary.Parameters.AddWithValue("code", rxNormCode);
            if (await vocabulary.ExecuteScalarAsync(cancellationToken) is not true)
                throw new ArgumentException("The RXCUI code is not present in the active medication vocabulary.");
        }
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction;
            duplicate.CommandText = "select exists(select 1 from inventory_item_medication_links where rx_norm_code=@code and item_id<>@itemId);";
            duplicate.Parameters.AddWithValue("code", rxNormCode);
            duplicate.Parameters.AddWithValue("itemId", itemId);
            if (await duplicate.ExecuteScalarAsync(cancellationToken) is true)
                throw new ArgumentException("That RXCUI code is already linked to another inventory item.");
        }
        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = "insert into inventory_item_medication_links (item_id,rx_norm_code,linked_by,linked_at) values (@itemId,@code,@user,@at) on conflict (item_id) do update set rx_norm_code=excluded.rx_norm_code, linked_by=excluded.linked_by, linked_at=excluded.linked_at;";
            upsert.Parameters.AddWithValue("itemId", itemId);
            upsert.Parameters.AddWithValue("code", rxNormCode);
            upsert.Parameters.AddWithValue("user", username);
            upsert.Parameters.AddWithValue("at", now);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "insert into inventory_item_medication_link_audits (audit_id,item_id,prior_rx_norm_code,new_rx_norm_code,action,changed_by,changed_at,reason) values (@id,@itemId,@prior,@code,@action,@user,@at,null);";
            audit.Parameters.AddWithValue("id", Guid.NewGuid());
            audit.Parameters.AddWithValue("itemId", itemId);
            audit.Parameters.AddWithValue("prior", (object?)priorCode ?? DBNull.Value);
            audit.Parameters.AddWithValue("code", rxNormCode);
            audit.Parameters.AddWithValue("action", priorCode is null ? "linked" : priorCode == rxNormCode ? "updated" : "updated");
            audit.Parameters.AddWithValue("user", username);
            audit.Parameters.AddWithValue("at", now);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        InventoryMedicationLink link;
        await using (var result = connection.CreateCommand())
        {
            result.Transaction = transaction;
            result.CommandText = "select l.item_id,l.rx_norm_code,v.drug_name,v.display_name,l.linked_by,l.linked_at from inventory_item_medication_links l join medication_vocabulary v on v.rx_norm_code=l.rx_norm_code where l.item_id=@itemId;";
            result.Parameters.AddWithValue("itemId", itemId);
            await using var resultReader = await result.ExecuteReaderAsync(cancellationToken);
            await resultReader.ReadAsync(cancellationToken);
            link = new InventoryMedicationLink(resultReader.GetInt32(0), resultReader.GetString(1), resultReader.GetString(2), resultReader.GetString(3), resultReader.GetString(4), resultReader.GetFieldValue<DateTimeOffset>(5).ToString("O", CultureInfo.InvariantCulture));
        }
        await transaction.CommitAsync(cancellationToken);
        return link;
    }

    public async Task<InventoryMedicationLinkHistoryResponse> GetMedicationLinkHistoryAsync(int itemId, CancellationToken cancellationToken)
    {
        if (itemId <= 0) throw new ArgumentException("An inventory item is required.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using (var item = connection.CreateCommand())
        {
            item.CommandText = "select exists(select 1 from inventory_items where item_id=@itemId);";
            item.Parameters.AddWithValue("itemId", itemId);
            if (await item.ExecuteScalarAsync(cancellationToken) is not true) throw new ArgumentException("The inventory item was not found.");
        }
        var events = new List<InventoryMedicationLinkAuditEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select audit_id,prior_rx_norm_code,new_rx_norm_code,action,changed_by,changed_at,reason from inventory_item_medication_link_audits where item_id=@itemId order by changed_at desc,audit_id desc;";
            command.Parameters.AddWithValue("itemId", itemId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                events.Add(new InventoryMedicationLinkAuditEvent(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return new InventoryMedicationLinkHistoryResponse(itemId, events);
    }

    public async Task<InventoryMedicationLinkHistoryResponse> UnlinkMedicationAsync(int itemId, InventoryMedicationLinkUnlinkRequest request, string username, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (itemId <= 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("An active inventory item, unlink reason of 500 characters or fewer, and authenticated user are required.");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string? priorCode;
        await using (var item = connection.CreateCommand())
        {
            item.Transaction = transaction;
            item.CommandText = "select l.rx_norm_code from inventory_items i left join inventory_item_medication_links l on l.item_id=i.item_id where i.item_id=@itemId and i.active=true for update of i;";
            item.Parameters.AddWithValue("itemId", itemId);
            await using var reader = await item.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The active inventory item was not found.");
            priorCode = reader.IsDBNull(0) ? null : reader.GetString(0);
        }
        if (priorCode is null) throw new ArgumentException("The inventory item does not currently have a medication link.");
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "delete from inventory_item_medication_links where item_id=@itemId;";
            delete.Parameters.AddWithValue("itemId", itemId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "insert into inventory_item_medication_link_audits (audit_id,item_id,prior_rx_norm_code,new_rx_norm_code,action,changed_by,changed_at,reason) values (@id,@itemId,@prior,null,'unlinked',@user,@at,@reason);";
            audit.Parameters.AddWithValue("id", Guid.NewGuid());
            audit.Parameters.AddWithValue("itemId", itemId);
            audit.Parameters.AddWithValue("prior", priorCode);
            audit.Parameters.AddWithValue("user", username);
            audit.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
            audit.Parameters.AddWithValue("reason", reason);
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetMedicationLinkHistoryAsync(itemId, cancellationToken);
    }

    public async Task<InventoryControlledSubstanceCatalogResponse> GetControlledSubstanceCatalogAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var locations = new List<InventoryControlledLocation>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select l.location_id,l.facility_id,f.code,f.name,l.location_code,l.display_name,l.dual_attestation_required,l.active,l.updated_at,l.updated_by from inventory_controlled_locations l join facilities f on f.id=l.facility_id order by f.code,l.location_code;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) locations.Add(ReadControlledLocation(reader));
        }
        var items = new List<InventoryControlledSubstanceItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select item_id,item_code,name,category,unit,controlled_schedule from inventory_items where active=true and controlled_schedule is not null order by controlled_schedule,name,item_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) items.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        }
        return new InventoryControlledSubstanceCatalogResponse(locations, items);
    }

    public async Task<InventoryControlledLocation> CreateControlledLocationAsync(InventoryControlledLocationMutationRequest request, string username, CancellationToken cancellationToken)
    {
        if (request.FacilityId <= 0 || string.IsNullOrWhiteSpace(request.LocationCode) || request.LocationCode.Trim().Length > 60 || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 160 || string.IsNullOrWhiteSpace(username)) throw new ArgumentException("An active facility, location code, display name, and authenticated user are required.");
        var locationId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var facility = connection.CreateCommand()) { facility.Transaction=transaction; facility.CommandText="select exists(select 1 from facilities where id=@id and inactive=false);"; facility.Parameters.AddWithValue("id",request.FacilityId); if (await facility.ExecuteScalarAsync(cancellationToken) is not true) throw new ArgumentException("The controlled location requires an active facility."); }
        try
        {
            await using var insert = connection.CreateCommand(); insert.Transaction=transaction; insert.CommandText="insert into inventory_controlled_locations(location_id,facility_id,location_code,display_name,dual_attestation_required,active,created_at,created_by,updated_at,updated_by) values(@id,@facility,@code,@name,@dual,@active,now(),@user,now(),@user); insert into inventory_controlled_location_events(location_id,action,prior_active,resulting_active,occurred_at,username) values(@id,'created',null,@active,now(),@user);"; insert.Parameters.AddWithValue("id",locationId); insert.Parameters.AddWithValue("facility",request.FacilityId); insert.Parameters.AddWithValue("code",request.LocationCode.Trim().ToUpperInvariant()); insert.Parameters.AddWithValue("name",request.DisplayName.Trim()); insert.Parameters.AddWithValue("dual",request.DualAttestationRequired); insert.Parameters.AddWithValue("active",request.Active); insert.Parameters.AddWithValue("user",username); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("The facility already has a controlled location with that code."); }
        InventoryControlledLocation location;
        await using (var query = connection.CreateCommand()) { query.Transaction=transaction; query.CommandText="select l.location_id,l.facility_id,f.code,f.name,l.location_code,l.display_name,l.dual_attestation_required,l.active,l.updated_at,l.updated_by from inventory_controlled_locations l join facilities f on f.id=l.facility_id where l.location_id=@id;"; query.Parameters.AddWithValue("id",locationId); await using var reader=await query.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken); location=ReadControlledLocation(reader); }
        await transaction.CommitAsync(cancellationToken); return location;
    }

    public async Task<InventoryControlledSubstanceClassificationHistoryResponse> UpdateControlledSubstanceClassificationAsync(int itemId, InventoryControlledSubstanceClassificationRequest request, string username, CancellationToken cancellationToken)
    {
        if (itemId <= 0 || string.IsNullOrWhiteSpace(username)) throw new ArgumentException("An inventory item and authenticated user are required.");
        var schedule = NormalizeControlledSchedule(request.ScheduleCode);
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        string? prior;
        await using (var item=connection.CreateCommand()) { item.Transaction=transaction; item.CommandText="select controlled_schedule from inventory_items where item_id=@id and active=true for update;"; item.Parameters.AddWithValue("id",itemId); var value=await item.ExecuteScalarAsync(cancellationToken); if (value is null && !await ItemExistsAsync(connection,transaction,itemId,cancellationToken)) throw new ArgumentException("The active inventory item was not found."); prior=value as string; }
        if (!string.Equals(prior,schedule,StringComparison.Ordinal)) { await using var update=connection.CreateCommand(); update.Transaction=transaction; update.CommandText="update inventory_items set controlled_schedule=@schedule where item_id=@id; insert into inventory_controlled_item_classification_events(item_id,prior_schedule,resulting_schedule,occurred_at,username) values(@id,@prior,@schedule,now(),@user);"; update.Parameters.AddWithValue("id",itemId); update.Parameters.AddWithValue("schedule",(object?)schedule??DBNull.Value); update.Parameters.AddWithValue("prior",(object?)prior??DBNull.Value); update.Parameters.AddWithValue("user",username); await update.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken); return await GetControlledSubstanceClassificationHistoryAsync(itemId,cancellationToken);
    }

    public async Task<InventoryControlledSubstanceClassificationHistoryResponse> GetControlledSubstanceClassificationHistoryAsync(int itemId, CancellationToken cancellationToken)
    {
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); InventoryControlledSubstanceItem item; await using(var command=connection.CreateCommand()){command.CommandText="select item_id,item_code,name,category,unit,controlled_schedule from inventory_items where item_id=@id;";command.Parameters.AddWithValue("id",itemId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The inventory item was not found.");item=new(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.IsDBNull(5)?string.Empty:reader.GetString(5));} var events=new List<InventoryControlledSubstanceClassificationEvent>();await using(var command=connection.CreateCommand()){command.CommandText="select event_id,prior_schedule,resulting_schedule,occurred_at,username from inventory_controlled_item_classification_events where item_id=@id order by occurred_at desc,event_id desc;";command.Parameters.AddWithValue("id",itemId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))events.Add(new(reader.GetInt64(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),reader.GetString(4)));}return new(item,events);
    }

    private static InventoryControlledLocation ReadControlledLocation(NpgsqlDataReader reader) => new(reader.GetGuid(0),reader.GetInt32(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetBoolean(6),reader.GetBoolean(7),reader.GetFieldValue<DateTimeOffset>(8).ToString("O"),reader.GetString(9));
    private static string? NormalizeControlledSchedule(string? value) { var normalized=value?.Trim().ToUpperInvariant(); if (string.IsNullOrEmpty(normalized)) return null; if (normalized is not ("II" or "III" or "IV" or "V")) throw new ArgumentException("Controlled schedule must be II, III, IV, V, or blank to remove the classification."); return normalized; }
    private static async Task<bool> ItemExistsAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,int itemId,CancellationToken cancellationToken){await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from inventory_items where item_id=@id and active=true);";command.Parameters.AddWithValue("id",itemId);return await command.ExecuteScalarAsync(cancellationToken) is true;}

    public async Task<InventoryControlledCustodyMovementResponse> CreateControlledCustodyMovementAsync(InventoryControlledCustodyMovementRequest request, string username, string? witnessUsername, CancellationToken cancellationToken)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("receipt" or "transfer" or "dispense" or "administration" or "return" or "waste" or "destruction" or "correction")
            || request.Quantity <= 0 || string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("A supported controlled-custody action, positive quantity, and authenticated user are required.");

        var reason = NormalizeOptional(request.Reason);
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 120)
            throw new ArgumentException("A reason and an idempotency key of 120 characters or fewer are required for controlled custody movements.");
        if (action == "receipt" && (request.ItemId is null || request.ItemId <= 0 || string.IsNullOrWhiteSpace(request.LotNumber) || request.LotNumber.Trim().Length > 80 || request.UnitCost is null || request.UnitCost < 0 || request.DestinationLocationId is null))
            throw new ArgumentException("A receipt requires item, lot number, nonnegative unit cost, and destination controlled location.");
        if (action != "receipt" && (request.LotId is null || request.LotId <= 0))
            throw new ArgumentException("This controlled custody action requires a source or destination lot.");
        if (action == "transfer" && (request.SourceLocationId is null || request.DestinationLocationId is null))
            throw new ArgumentException("A controlled transfer requires source and destination locations.");
        if (action is "dispense" or "administration" or "return")
        {
            if (string.IsNullOrWhiteSpace(request.PatientId) || request.Encounter is null || request.Encounter <= 0)
                throw new ArgumentException("Controlled dispense, administration, and return require a patient and encounter.");
        }
        if (action == "return" && request.RelatedEventId is null)
            throw new ArgumentException("A controlled return must reference its prior dispense or administration event.");
        if (action == "correction" && (request.RelatedEventId is null || request.CorrectionDirection?.Trim().ToLowerInvariant() is not ("increase" or "decrease")))
            throw new ArgumentException("A controlled correction requires a prior custody event and an increase or decrease direction.");

        var occurredAt = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();
        var expiration = ParseOptionalDate(request.ExpirationDate, "Lot expiration must be an ISO date.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction;
            duplicate.CommandText = "select exists(select 1 from inventory_controlled_custody_events where idempotency_key=@key);";
            duplicate.Parameters.AddWithValue("key", idempotencyKey);
            if (await duplicate.ExecuteScalarAsync(cancellationToken) is true)
                throw new ArgumentException("That controlled custody idempotency key has already been used.");
        }

        ControlledLotState primaryLot;
        ControlledLotState? counterpartyLot = null;
        ControlledLocationState? sourceLocation = null;
        ControlledLocationState? destinationLocation = null;
        decimal? sourceBefore = null;
        decimal? sourceAfter = null;
        decimal? destinationBefore = null;
        decimal? destinationAfter = null;
        decimal quantityDelta;

        if (action == "receipt")
        {
            destinationLocation = await GetControlledLocationAsync(connection, transaction, request.DestinationLocationId!.Value, cancellationToken)
                ?? throw new ArgumentException("The destination controlled location was not found or is inactive.");
            var item = await GetControlledItemAsync(connection, transaction, request.ItemId!.Value, cancellationToken)
                ?? throw new ArgumentException("The controlled inventory item was not found or is inactive.");
            if (destinationLocation.FacilityId <= 0) throw new ArgumentException("The destination controlled location is invalid.");
            var created = await GetOrCreateControlledLotAsync(connection, transaction, item, destinationLocation, request.LotNumber!.Trim(), expiration, request.UnitCost!.Value, request.Quantity, cancellationToken);
            primaryLot = created.Lot;
            destinationBefore = created.PriorQuantity;
            destinationAfter = primaryLot.QuantityOnHand;
            quantityDelta = request.Quantity;
        }
        else
        {
            primaryLot = await GetControlledLotAsync(connection, transaction, request.LotId!.Value, cancellationToken)
                ?? throw new ArgumentException("The controlled lot was not found.");
            if (primaryLot.QuantityOnHand < request.Quantity && action is not "return" && !(action == "correction" && string.Equals(request.CorrectionDirection?.Trim(), "increase", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The controlled custody movement would make the lot quantity negative.");

            if (action == "transfer")
            {
                sourceLocation = await RequireMatchingControlledLocationAsync(connection, transaction, primaryLot, request.SourceLocationId!.Value, "source", cancellationToken);
                destinationLocation = await GetControlledLocationAsync(connection, transaction, request.DestinationLocationId!.Value, cancellationToken)
                    ?? throw new ArgumentException("The destination controlled location was not found or is inactive.");
                if (sourceLocation.LocationId == destinationLocation.LocationId) throw new ArgumentException("The controlled transfer destination must differ from the source location.");
                var created = await GetOrCreateControlledLotAsync(connection, transaction, primaryLot.Item, destinationLocation, primaryLot.LotNumber, primaryLot.ExpirationDate, primaryLot.UnitCost, request.Quantity, cancellationToken);
                counterpartyLot = created.Lot;
                sourceBefore = primaryLot.QuantityOnHand;
                sourceAfter = primaryLot.QuantityOnHand - request.Quantity;
                destinationBefore = created.PriorQuantity;
                destinationAfter = counterpartyLot.QuantityOnHand;
                await UpdateLotQuantityAsync(connection, transaction, primaryLot.LotId, sourceAfter.Value, cancellationToken);
                primaryLot = primaryLot with { QuantityOnHand = sourceAfter.Value };
                quantityDelta = -request.Quantity;
            }
            else
            {
                if (action is "dispense" or "administration" or "waste" or "destruction" || (action == "correction" && string.Equals(request.CorrectionDirection?.Trim(), "decrease", StringComparison.OrdinalIgnoreCase)))
                    sourceLocation = await RequireMatchingControlledLocationAsync(connection, transaction, primaryLot, request.SourceLocationId ?? primaryLot.ControlledLocationId!.Value, "source", cancellationToken);
                else
                    destinationLocation = await RequireMatchingControlledLocationAsync(connection, transaction, primaryLot, request.DestinationLocationId ?? primaryLot.ControlledLocationId!.Value, "destination", cancellationToken);

                if (action is "dispense" or "administration" or "return")
                    await EnsureControlledEncounterAsync(connection, transaction, request.PatientId!.Trim(), request.Encounter!.Value, cancellationToken);
                if (action == "return")
                    await ValidateControlledReturnAsync(connection, transaction, request.RelatedEventId!.Value, primaryLot.LotId, request.PatientId!.Trim(), request.Encounter!.Value, cancellationToken);
                if (action == "correction")
                    await ValidateRelatedControlledEventAsync(connection, transaction, request.RelatedEventId!.Value, primaryLot.LotId, cancellationToken);

                quantityDelta = action switch
                {
                    "return" => request.Quantity,
                    "correction" when string.Equals(request.CorrectionDirection?.Trim(), "increase", StringComparison.OrdinalIgnoreCase) => request.Quantity,
                    _ => -request.Quantity
                };
                var priorQuantity = primaryLot.QuantityOnHand;
                var updatedQuantity = priorQuantity + quantityDelta;
                if (updatedQuantity < 0) throw new ArgumentException("The controlled custody movement would make the lot quantity negative.");
                await UpdateLotQuantityAsync(connection, transaction, primaryLot.LotId, updatedQuantity, cancellationToken);
                primaryLot = primaryLot with { QuantityOnHand = updatedQuantity };
                if (quantityDelta < 0) { sourceBefore = priorQuantity; sourceAfter = updatedQuantity; }
                else { destinationBefore = priorQuantity; destinationAfter = updatedQuantity; }
            }
        }

        var witnessRequired = sourceLocation?.DualAttestationRequired == true || destinationLocation?.DualAttestationRequired == true;
        await EnsureNoControlledCountLockAsync(connection, transaction, sourceLocation?.LocationId, destinationLocation?.LocationId, cancellationToken);
        if (witnessRequired && string.IsNullOrWhiteSpace(witnessUsername))
            throw new ArgumentException("This controlled location requires a separately authenticated witness before the movement can post.");
        if (!string.IsNullOrWhiteSpace(witnessUsername) && string.Equals(witnessUsername, username, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The controlled-custody witness must be a different authenticated user.");
        var witnessedAt = string.IsNullOrWhiteSpace(witnessUsername) ? (DateTimeOffset?)null : DateTimeOffset.UtcNow;

        await InsertControlledCustodyEventAsync(connection, transaction, eventId, action, primaryLot, counterpartyLot, sourceLocation, destinationLocation,
            request.PatientId?.Trim(), request.Encounter, request.Quantity, quantityDelta, reason, request.RelatedEventId, idempotencyKey,
            sourceBefore, sourceAfter, destinationBefore, destinationAfter, username, occurredAt, witnessUsername, witnessedAt, cancellationToken);

        await InsertTransactionAsync(connection, transaction, Guid.NewGuid(), primaryLot.LotId, action == "transfer" ? eventId : null, $"controlled_{action}", quantityDelta, reason, username, occurredAt, cancellationToken);
        if (counterpartyLot is not null)
            await InsertTransactionAsync(connection, transaction, Guid.NewGuid(), counterpartyLot.LotId, eventId, "controlled_transfer", request.Quantity, reason, username, occurredAt, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var custodyEvent = new InventoryControlledCustodyEvent(eventId, action, primaryLot.LotId, counterpartyLot?.LotId, primaryLot.Item.ItemId, primaryLot.Item.ItemCode,
            primaryLot.Item.ScheduleCode, request.Quantity, quantityDelta, sourceLocation?.LocationId, destinationLocation?.LocationId, request.PatientId?.Trim(), request.Encounter,
            reason, request.RelatedEventId, sourceBefore, sourceAfter, destinationBefore, destinationAfter, username, occurredAt.ToString("O", CultureInfo.InvariantCulture),
            witnessUsername, witnessedAt?.ToString("O", CultureInfo.InvariantCulture));
        return new InventoryControlledCustodyMovementResponse(custodyEvent, primaryLot.ToInventoryLot(), counterpartyLot?.ToInventoryLot());
    }

    public async Task<InventoryControlledCustodyLotHistoryResponse> GetControlledCustodyLotHistoryAsync(int lotId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        ControlledLotState lot;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            lot = await GetControlledLotAsync(connection, transaction, lotId, cancellationToken) ?? throw new ArgumentException("The controlled lot was not found.");
            await transaction.CommitAsync(cancellationToken);
        }
        var events = new List<InventoryControlledCustodyEvent>();
        await using var command = connection.CreateCommand();
        command.CommandText = "select e.event_id,e.action,e.lot_id,e.counterparty_lot_id,i.item_id,i.item_code,i.controlled_schedule,e.quantity,e.quantity_delta,e.source_location_id,e.destination_location_id,e.patient_id,e.encounter,e.reason,e.related_event_id,e.source_quantity_before,e.source_quantity_after,e.destination_quantity_before,e.destination_quantity_after,e.performed_by,e.occurred_at,e.witness_username,e.witnessed_at from inventory_controlled_custody_events e join inventory_lots l on l.lot_id=e.lot_id join inventory_items i on i.item_id=l.item_id where e.lot_id=@lotId or e.counterparty_lot_id=@lotId order by e.occurred_at desc,e.event_id desc;";
        command.Parameters.AddWithValue("lotId", lotId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new InventoryControlledCustodyEvent(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetInt32(4), reader.GetString(5), reader.GetString(6), reader.GetDecimal(7), reader.GetDecimal(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetInt32(12), reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.IsDBNull(15) ? null : reader.GetDecimal(15), reader.IsDBNull(16) ? null : reader.GetDecimal(16), reader.IsDBNull(17) ? null : reader.GetDecimal(17), reader.IsDBNull(18) ? null : reader.GetDecimal(18), reader.GetString(19), reader.GetFieldValue<DateTimeOffset>(20).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetFieldValue<DateTimeOffset>(22).ToString("O", CultureInfo.InvariantCulture)));
        }
        return new InventoryControlledCustodyLotHistoryResponse(lot.ToInventoryLot(), lot.ControlledLocationId, lot.LocationCode, lot.LocationName, lot.Item.ScheduleCode, events);
    }

    public async Task<InventoryControlledCountSession> CreateControlledCountSessionAsync(InventoryControlledCountSessionCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var countType=request.CountType?.Trim().ToLowerInvariant(); var reason=NormalizeOptional(request.Reason); var key=NormalizeOptional(request.IdempotencyKey);
        if (request.LocationId==Guid.Empty || countType is not ("opening" or "shift" or "cycle" or "closing") || string.IsNullOrWhiteSpace(reason) || reason.Length>500 || string.IsNullOrWhiteSpace(key) || key.Length>120) throw new ArgumentException("Controlled count location, type, reason, and idempotency key are required.");
        var now=DateTimeOffset.UtcNow; var id=Guid.NewGuid(); await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        var location=await GetControlledLocationAsync(connection,transaction,request.LocationId,cancellationToken) ?? throw new ArgumentException("The controlled count location was not found or is inactive.");
        await using(var duplicate=connection.CreateCommand()){duplicate.Transaction=transaction;duplicate.CommandText="select exists(select 1 from inventory_controlled_count_sessions where idempotency_key=@key);";duplicate.Parameters.AddWithValue("key",key);if(await duplicate.ExecuteScalarAsync(cancellationToken) is true)throw new ArgumentException("That controlled count idempotency key has already been used.");}
        await using(var active=connection.CreateCommand()){active.Transaction=transaction;active.CommandText="select exists(select 1 from inventory_controlled_count_sessions where location_id=@location and status='in_progress');";active.Parameters.AddWithValue("location",location.LocationId);if(await active.ExecuteScalarAsync(cancellationToken) is true)throw new ArgumentException("A controlled count is already in progress for this location.");}
        await using(var insert=connection.CreateCommand()){insert.Transaction=transaction;insert.CommandText="insert into inventory_controlled_count_sessions(session_id,location_id,count_type,status,movement_lock_active,reason,idempotency_key,started_by,started_at) values(@id,@location,@type,'in_progress',@lock,@reason,@key,@user,@at);";insert.Parameters.AddWithValue("id",id);insert.Parameters.AddWithValue("location",location.LocationId);insert.Parameters.AddWithValue("type",countType);insert.Parameters.AddWithValue("lock",request.MovementLockActive);insert.Parameters.AddWithValue("reason",reason);insert.Parameters.AddWithValue("key",key);insert.Parameters.AddWithValue("user",username);insert.Parameters.AddWithValue("at",now);await insert.ExecuteNonQueryAsync(cancellationToken);}
        await using(var snapshot=connection.CreateCommand()){snapshot.Transaction=transaction;snapshot.CommandText="insert into inventory_controlled_count_lines(line_id,session_id,lot_id,expected_quantity) select gen_random_uuid(),@session,l.lot_id,l.quantity_on_hand from inventory_lots l join inventory_items i on i.item_id=l.item_id where l.controlled_location_id=@location and l.status='active' and i.active=true and i.controlled_schedule is not null;";snapshot.Parameters.AddWithValue("session",id);snapshot.Parameters.AddWithValue("location",location.LocationId);await snapshot.ExecuteNonQueryAsync(cancellationToken);}
        await transaction.CommitAsync(cancellationToken);return await GetControlledCountSessionAsync(id,cancellationToken);
    }

    public async Task<InventoryControlledCountSession> SubmitControlledCountSessionAsync(Guid sessionId, InventoryControlledCountSubmitRequest request, string username, string counterUsername, CancellationToken cancellationToken)
    {
        var reason=NormalizeOptional(request.Reason);var key=NormalizeOptional(request.IdempotencyKey);if(sessionId==Guid.Empty || request.CounterSessionId==Guid.Empty || string.IsNullOrWhiteSpace(counterUsername)||string.Equals(username,counterUsername,StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(reason)||reason.Length>500||string.IsNullOrWhiteSpace(key)||key.Length>120||request.Observations is null)throw new ArgumentException("A different authenticated counter, reason, idempotency key, and complete observations are required.");
        var now=DateTimeOffset.UtcNow;await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        Guid locationId;int lineCount;await using(var session=connection.CreateCommand()){session.Transaction=transaction;session.CommandText="select location_id,(select count(*)::int from inventory_controlled_count_lines where session_id=s.session_id) from inventory_controlled_count_sessions s where s.session_id=@id and s.status='in_progress' for update;";session.Parameters.AddWithValue("id",sessionId);await using var reader=await session.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The controlled count is not in progress.");locationId=reader.GetGuid(0);lineCount=reader.GetInt32(1);}
        if(request.Observations.Count!=lineCount||request.Observations.Any(o=>o.LotId<=0||o.ObservedQuantity<0)||request.Observations.Select(o=>o.LotId).Distinct().Count()!=lineCount)throw new ArgumentException("The submitted count must contain one non-negative observation for every snapshotted lot.");
        await using(var duplicate=connection.CreateCommand()){duplicate.Transaction=transaction;duplicate.CommandText="select exists(select 1 from inventory_controlled_count_sessions where idempotency_key=@key);";duplicate.Parameters.AddWithValue("key",key);if(await duplicate.ExecuteScalarAsync(cancellationToken) is true)throw new ArgumentException("That controlled count idempotency key has already been used.");}
        foreach(var observation in request.Observations){await using var line=connection.CreateCommand();line.Transaction=transaction;line.CommandText="update inventory_controlled_count_lines set observed_quantity=@observed,variance_quantity=@observed-expected_quantity where session_id=@session and lot_id=@lot;";line.Parameters.AddWithValue("observed",observation.ObservedQuantity);line.Parameters.AddWithValue("session",sessionId);line.Parameters.AddWithValue("lot",observation.LotId);if(await line.ExecuteNonQueryAsync(cancellationToken)!=1)throw new ArgumentException("A submitted observation does not match this count session.");}
        int variances;await using(var count=connection.CreateCommand()){count.Transaction=transaction;count.CommandText="select count(*)::int from inventory_controlled_count_lines where session_id=@session and variance_quantity<>0;";count.Parameters.AddWithValue("session",sessionId);variances=Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken),CultureInfo.InvariantCulture);}
        var status=variances==0?"reconciled":"discrepancy_open";await using(var update=connection.CreateCommand()){update.Transaction=transaction;update.CommandText="update inventory_controlled_count_sessions set status=@status,movement_lock_active=false,reason=reason || E'\\nSubmission: ' || @reason,idempotency_key=@key,submitted_by=@user,submitted_at=@at,counter_username=@counter where session_id=@session;";update.Parameters.AddWithValue("status",status);update.Parameters.AddWithValue("reason",reason);update.Parameters.AddWithValue("key",key);update.Parameters.AddWithValue("user",username);update.Parameters.AddWithValue("at",now);update.Parameters.AddWithValue("counter",counterUsername);update.Parameters.AddWithValue("session",sessionId);await update.ExecuteNonQueryAsync(cancellationToken);}
        if(variances>0){await using var discrepancy=connection.CreateCommand();discrepancy.Transaction=transaction;discrepancy.CommandText="insert into inventory_controlled_count_discrepancies(discrepancy_id,session_id,line_id,opened_by,opened_at) select gen_random_uuid(),@session,line_id,@user,@at from inventory_controlled_count_lines where session_id=@session and variance_quantity<>0;";discrepancy.Parameters.AddWithValue("session",sessionId);discrepancy.Parameters.AddWithValue("user",username);discrepancy.Parameters.AddWithValue("at",now);await discrepancy.ExecuteNonQueryAsync(cancellationToken);}
        await transaction.CommitAsync(cancellationToken);return await GetControlledCountSessionAsync(sessionId,cancellationToken);
    }

    public async Task<InventoryControlledCountSession> GetControlledCountSessionAsync(Guid sessionId,CancellationToken cancellationToken)
    {
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);Guid locationId;string locationCode,locationName,countType,status,reason,startedBy;bool movementLock;DateTimeOffset startedAt;string? submittedBy,counter;DateTimeOffset? submittedAt;
        await using(var command=connection.CreateCommand()){command.CommandText="select s.location_id,l.location_code,l.display_name,s.count_type,s.status,s.movement_lock_active,s.reason,s.started_by,s.started_at,s.submitted_by,s.submitted_at,s.counter_username from inventory_controlled_count_sessions s join inventory_controlled_locations l on l.location_id=s.location_id where s.session_id=@id;";command.Parameters.AddWithValue("id",sessionId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The controlled count session was not found.");locationId=reader.GetGuid(0);locationCode=reader.GetString(1);locationName=reader.GetString(2);countType=reader.GetString(3);status=reader.GetString(4);movementLock=reader.GetBoolean(5);reason=reader.GetString(6);startedBy=reader.GetString(7);startedAt=reader.GetFieldValue<DateTimeOffset>(8);submittedBy=reader.IsDBNull(9)?null:reader.GetString(9);submittedAt=reader.IsDBNull(10)?null:reader.GetFieldValue<DateTimeOffset>(10);counter=reader.IsDBNull(11)?null:reader.GetString(11);}
        var lines=new List<InventoryControlledCountLine>();await using(var command=connection.CreateCommand()){command.CommandText="select c.line_id,c.lot_id,l.lot_number,i.item_code,c.expected_quantity,c.observed_quantity,c.variance_quantity,d.discrepancy_id,d.status from inventory_controlled_count_lines c join inventory_lots l on l.lot_id=c.lot_id join inventory_items i on i.item_id=l.item_id left join inventory_controlled_count_discrepancies d on d.line_id=c.line_id where c.session_id=@session order by i.item_code,l.lot_number,c.line_id;";command.Parameters.AddWithValue("session",sessionId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))lines.Add(new(reader.GetGuid(0),reader.GetInt32(1),reader.GetString(2),reader.GetString(3),reader.GetDecimal(4),reader.IsDBNull(5)?null:reader.GetDecimal(5),reader.IsDBNull(6)?null:reader.GetDecimal(6),reader.IsDBNull(7)?null:reader.GetGuid(7),reader.IsDBNull(8)?null:reader.GetString(8)));}
        return new(sessionId,locationId,locationCode,locationName,countType,status,movementLock,reason,startedBy,startedAt.ToString("O",CultureInfo.InvariantCulture),submittedBy,submittedAt?.ToString("O",CultureInfo.InvariantCulture),counter,lines);
    }

    public async Task<InventoryControlledCountSession> InvestigateControlledCountDiscrepancyAsync(Guid discrepancyId, InventoryControlledDiscrepancyInvestigationRequest request, string username, CancellationToken cancellationToken)
    {
        var notes=NormalizeOptional(request.Notes);if(discrepancyId==Guid.Empty||string.IsNullOrWhiteSpace(notes)||notes.Length>1000)throw new ArgumentException("A controlled discrepancy and investigation notes are required.");
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="update inventory_controlled_count_discrepancies set status='investigating',investigation_notes=@notes where discrepancy_id=@id and status='open' returning session_id;";command.Parameters.AddWithValue("notes",notes);command.Parameters.AddWithValue("id",discrepancyId);var session=await command.ExecuteScalarAsync(cancellationToken);if(session is null)throw new ArgumentException("The controlled discrepancy was not found or is not open.");return await GetControlledCountSessionAsync((Guid)session,cancellationToken);
    }

    public async Task<InventoryControlledCustodyMovementResponse> CorrectControlledCountDiscrepancyAsync(Guid discrepancyId, InventoryControlledDiscrepancyCorrectionRequest request, string username, string? witnessUsername, CancellationToken cancellationToken)
    {
        var notes=NormalizeOptional(request.Notes);var key=NormalizeOptional(request.IdempotencyKey);if(discrepancyId==Guid.Empty||string.IsNullOrWhiteSpace(notes)||notes.Length>1000||string.IsNullOrWhiteSpace(key)||key.Length>120)throw new ArgumentException("A controlled discrepancy, correction notes, and idempotency key are required.");
        int lotId;decimal variance;Guid locationId,relatedEventId;await using(var connection=await dataSource.OpenConnectionAsync(cancellationToken)){await using var command=connection.CreateCommand();command.CommandText="select l.lot_id,c.variance_quantity,l.controlled_location_id,(select e.event_id from inventory_controlled_custody_events e where e.lot_id=l.lot_id or e.counterparty_lot_id=l.lot_id order by e.occurred_at desc,e.event_id desc limit 1) from inventory_controlled_count_discrepancies d join inventory_controlled_count_lines c on c.line_id=d.line_id join inventory_lots l on l.lot_id=c.lot_id where d.discrepancy_id=@id and d.status='investigating' and d.correction_event_id is null;";command.Parameters.AddWithValue("id",discrepancyId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The controlled discrepancy was not found or is not ready for correction.");lotId=reader.GetInt32(0);variance=reader.GetDecimal(1);if(reader.IsDBNull(2)||reader.IsDBNull(3))throw new ArgumentException("The controlled discrepancy has no eligible custody lot/event.");locationId=reader.GetGuid(2);relatedEventId=reader.GetGuid(3);}
        var direction=variance>0?"increase":"decrease";var movement=await CreateControlledCustodyMovementAsync(new("correction",lotId,null,null,null,null,Math.Abs(variance),direction=="decrease"?locationId:null,direction=="increase"?locationId:null,null,null,$"Count discrepancy {discrepancyId}: {notes}",relatedEventId,direction,key,null),username,witnessUsername,cancellationToken);
        await using(var connection=await dataSource.OpenConnectionAsync(cancellationToken)){await using var command=connection.CreateCommand();command.CommandText="update inventory_controlled_count_discrepancies set status='corrected',correction_event_id=@event where discrepancy_id=@id and status='investigating' and correction_event_id is null;";command.Parameters.AddWithValue("event",movement.Event.EventId);command.Parameters.AddWithValue("id",discrepancyId);if(await command.ExecuteNonQueryAsync(cancellationToken)!=1)throw new ArgumentException("The correction posted but the discrepancy was concurrently changed; review the custody event before retrying.");}
        return movement;
    }

    public async Task<InventoryControlledCountSession> CloseControlledCountDiscrepancyAsync(Guid discrepancyId, InventoryControlledDiscrepancyCloseRequest request, string username, CancellationToken cancellationToken)
    {
        var notes=NormalizeOptional(request.Notes);if(discrepancyId==Guid.Empty||string.IsNullOrWhiteSpace(notes)||notes.Length>1000)throw new ArgumentException("A corrected controlled discrepancy and closure notes are required.");
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="update inventory_controlled_count_discrepancies set status='closed',closed_by=@user,closed_at=now(),investigation_notes=coalesce(investigation_notes,'') || E'\\nClosure: ' || @notes where discrepancy_id=@id and status='corrected' and correction_event_id is not null returning session_id;";command.Parameters.AddWithValue("notes",notes);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("id",discrepancyId);var session=await command.ExecuteScalarAsync(cancellationToken);if(session is null)throw new ArgumentException("The controlled discrepancy was not found or has not been corrected.");return await GetControlledCountSessionAsync((Guid)session,cancellationToken);
    }

    private sealed record ControlledItemState(int ItemId, string ItemCode, string Name, string Unit, string ScheduleCode, decimal ReorderPoint);
    private sealed record ControlledLocationState(Guid LocationId, int FacilityId, string Code, string Name, bool DualAttestationRequired);
    private sealed record ControlledLotState(int LotId, ControlledItemState Item, int FacilityId, string FacilityCode, string FacilityName, string LotNumber, DateOnly? ExpirationDate, decimal QuantityOnHand, decimal UnitCost, string Status, Guid? ControlledLocationId, string? LocationCode, string? LocationName)
    {
        public InventoryLot ToInventoryLot() => new(LotId, FacilityCode, FacilityName, LotNumber, ExpirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), QuantityOnHand, UnitCost, Status);
    }
    private sealed record ControlledLotMutation(ControlledLotState Lot, decimal PriorQuantity);

    private static async Task<ControlledItemState?> GetControlledItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int itemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select item_id,item_code,name,unit,controlled_schedule,reorder_point from inventory_items where item_id=@id and active=true and controlled_schedule is not null for update;";
        command.Parameters.AddWithValue("id", itemId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5)) : null;
    }

    private static async Task<ControlledLocationState?> GetControlledLocationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid locationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select l.location_id,l.facility_id,l.location_code,l.display_name,l.dual_attestation_required from inventory_controlled_locations l join facilities f on f.id=l.facility_id where l.location_id=@id and l.active=true and f.inactive=false for update of l;";
        command.Parameters.AddWithValue("id", locationId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)) : null;
    }

    private static async Task<ControlledLotState?> GetControlledLotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int lotId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select l.lot_id,i.item_id,i.item_code,i.name,i.unit,i.controlled_schedule,i.reorder_point,l.facility_id,f.code,f.name,l.lot_number,l.expiration_date,l.quantity_on_hand,l.unit_cost,l.status,l.controlled_location_id,cl.location_code,cl.display_name from inventory_lots l join inventory_items i on i.item_id=l.item_id join facilities f on f.id=l.facility_id left join inventory_controlled_locations cl on cl.location_id=l.controlled_location_id where l.lot_id=@lotId and i.active=true and i.controlled_schedule is not null and l.status='active' for update of l;";
        command.Parameters.AddWithValue("lotId", lotId); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        if (reader.IsDBNull(15)) throw new ArgumentException("The controlled lot must be assigned to an active controlled location before it can move.");
        return new(reader.GetInt32(0), new(reader.GetInt32(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetDecimal(6)), reader.GetInt32(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),reader.IsDBNull(11)?null:reader.GetFieldValue<DateOnly>(11),reader.GetDecimal(12),reader.GetDecimal(13),reader.GetString(14),reader.GetGuid(15),reader.IsDBNull(16)?null:reader.GetString(16),reader.IsDBNull(17)?null:reader.GetString(17));
    }

    private static async Task<ControlledLotMutation> GetOrCreateControlledLotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ControlledItemState item, ControlledLocationState location, string lotNumber, DateOnly? expirationDate, decimal unitCost, decimal quantity, CancellationToken cancellationToken)
    {
        await using var existing = connection.CreateCommand(); existing.Transaction = transaction;
        existing.CommandText = "select lot_id,expiration_date,quantity_on_hand,unit_cost,status from inventory_lots where item_id=@item and facility_id=@facility and lot_number=@lot and controlled_location_id=@location for update;";
        existing.Parameters.AddWithValue("item", item.ItemId); existing.Parameters.AddWithValue("facility", location.FacilityId); existing.Parameters.AddWithValue("lot", lotNumber); existing.Parameters.AddWithValue("location", location.LocationId);
        await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var lotId=reader.GetInt32(0); DateOnly? existingExpiration=reader.IsDBNull(1)?null:reader.GetFieldValue<DateOnly>(1); var prior=reader.GetDecimal(2); var existingCost=reader.GetDecimal(3); var status=reader.GetString(4); await reader.DisposeAsync();
            if (!string.Equals(status,"active",StringComparison.OrdinalIgnoreCase) || existingExpiration != expirationDate || existingCost != unitCost) throw new ArgumentException("The matching controlled lot is inactive or has different expiry/unit-cost metadata.");
            var updated=prior+quantity; await UpdateLotQuantityAsync(connection,transaction,lotId,updated,cancellationToken);
            var existingFacility = await GetFacilityAsync(connection, transaction, location.FacilityId, cancellationToken) ?? throw new ArgumentException("The controlled location facility was not found.");
            return new(new ControlledLotState(lotId,item,location.FacilityId,existingFacility.Code,existingFacility.Name,lotNumber,expirationDate,updated,unitCost,status,location.LocationId,location.Code,location.Name),prior);
        }
        await reader.DisposeAsync();
        await using var facility = connection.CreateCommand(); facility.Transaction=transaction; facility.CommandText="select code,name from facilities where id=@id;";facility.Parameters.AddWithValue("id",location.FacilityId);await using var facilityReader=await facility.ExecuteReaderAsync(cancellationToken);if(!await facilityReader.ReadAsync(cancellationToken))throw new ArgumentException("The controlled location facility was not found.");var facilityCode=facilityReader.GetString(0);var facilityName=facilityReader.GetString(1);await facilityReader.DisposeAsync();
        await using var insert = connection.CreateCommand(); insert.Transaction=transaction; insert.CommandText="insert into inventory_lots(item_id,facility_id,lot_number,expiration_date,quantity_on_hand,unit_cost,status,controlled_location_id) values(@item,@facility,@lot,@expiration,@quantity,@cost,'active',@location) returning lot_id;";insert.Parameters.AddWithValue("item",item.ItemId);insert.Parameters.AddWithValue("facility",location.FacilityId);insert.Parameters.AddWithValue("lot",lotNumber);insert.Parameters.AddWithValue("expiration",(object?)expirationDate??DBNull.Value);insert.Parameters.AddWithValue("quantity",quantity);insert.Parameters.AddWithValue("cost",unitCost);insert.Parameters.AddWithValue("location",location.LocationId);var newLotId=Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken),CultureInfo.InvariantCulture);
        return new(new ControlledLotState(newLotId,item,location.FacilityId,facilityCode,facilityName,lotNumber,expirationDate,quantity,unitCost,"active",location.LocationId,location.Code,location.Name),0);
    }

    private static async Task<ControlledLocationState> RequireMatchingControlledLocationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ControlledLotState lot, Guid locationId, string role, CancellationToken cancellationToken)
    {
        var location=await GetControlledLocationAsync(connection,transaction,locationId,cancellationToken) ?? throw new ArgumentException($"The {role} controlled location was not found or is inactive.");
        if (lot.ControlledLocationId != location.LocationId || lot.FacilityId != location.FacilityId) throw new ArgumentException($"The {role} controlled location does not hold the selected lot.");
        return location;
    }

    private static async Task UpdateLotQuantityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int lotId, decimal quantity, CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="update inventory_lots set quantity_on_hand=@quantity where lot_id=@lot;";command.Parameters.AddWithValue("quantity",quantity);command.Parameters.AddWithValue("lot",lotId);await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureNoControlledCountLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid? sourceLocationId, Guid? destinationLocationId, CancellationToken cancellationToken)
    {
        var locations=new[] { sourceLocationId,destinationLocationId }.Where(id=>id.HasValue).Select(id=>id!.Value).Distinct().ToArray();if(locations.Length==0)return;
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from inventory_controlled_count_sessions where status='in_progress' and movement_lock_active=true and location_id=any(@locations));";command.Parameters.AddWithValue("locations",locations);if(await command.ExecuteScalarAsync(cancellationToken) is true)throw new ArgumentException("A movement-locked controlled count is in progress for this location.");
    }

    private static async Task EnsureGeneralInventoryItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int itemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select controlled_schedule from inventory_items where item_id=@item;"; command.Parameters.AddWithValue("item", itemId);
        var schedule = await command.ExecuteScalarAsync(cancellationToken);
        if (schedule is string) throw new ArgumentException("Controlled inventory must use the controlled-custody movement workflow.");
    }

    private static async Task EnsureGeneralInventoryLotAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int lotId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select i.controlled_schedule from inventory_lots l join inventory_items i on i.item_id=l.item_id where l.lot_id=@lot;"; command.Parameters.AddWithValue("lot", lotId);
        var schedule = await command.ExecuteScalarAsync(cancellationToken);
        if (schedule is string) throw new ArgumentException("Controlled inventory must use the controlled-custody movement workflow.");
    }

    private static async Task EnsureControlledEncounterAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string patientId, int encounter, CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from encounters where encounter=@encounter and patient_id=@patient);";command.Parameters.AddWithValue("encounter",encounter);command.Parameters.AddWithValue("patient",patientId);if(await command.ExecuteScalarAsync(cancellationToken) is not true)throw new ArgumentException("The encounter must belong to the selected patient.");
    }

    private static async Task ValidateControlledReturnAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid relatedEventId, int lotId, string patientId, int encounter, CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from inventory_controlled_custody_events where event_id=@event and lot_id=@lot and action in ('dispense','administration') and patient_id=@patient and encounter=@encounter);";command.Parameters.AddWithValue("event",relatedEventId);command.Parameters.AddWithValue("lot",lotId);command.Parameters.AddWithValue("patient",patientId);command.Parameters.AddWithValue("encounter",encounter);if(await command.ExecuteScalarAsync(cancellationToken) is not true)throw new ArgumentException("The return must reference a dispense or administration event for the same lot, patient, and encounter.");
    }

    private static async Task ValidateRelatedControlledEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid relatedEventId, int lotId, CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select exists(select 1 from inventory_controlled_custody_events where event_id=@event and (lot_id=@lot or counterparty_lot_id=@lot));";command.Parameters.AddWithValue("event",relatedEventId);command.Parameters.AddWithValue("lot",lotId);if(await command.ExecuteScalarAsync(cancellationToken) is not true)throw new ArgumentException("The correction must reference a custody event for the selected lot.");
    }

    private static async Task InsertControlledCustodyEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid eventId, string action, ControlledLotState lot, ControlledLotState? counterpartyLot, ControlledLocationState? sourceLocation, ControlledLocationState? destinationLocation, string? patientId, int? encounter, decimal quantity, decimal quantityDelta, string reason, Guid? relatedEventId, string idempotencyKey, decimal? sourceBefore, decimal? sourceAfter, decimal? destinationBefore, decimal? destinationAfter, string username, DateTimeOffset occurredAt, string? witnessUsername, DateTimeOffset? witnessedAt, CancellationToken cancellationToken)
    {
        await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into inventory_controlled_custody_events(event_id,action,lot_id,counterparty_lot_id,source_location_id,destination_location_id,patient_id,encounter,quantity,quantity_delta,reason,related_event_id,idempotency_key,source_quantity_before,source_quantity_after,destination_quantity_before,destination_quantity_after,performed_by,occurred_at,entered_at,witness_username,witnessed_at) values(@id,@action,@lot,@counterparty,@source,@destination,@patient,@encounter,@quantity,@delta,@reason,@related,@key,@sourceBefore,@sourceAfter,@destinationBefore,@destinationAfter,@user,@occurred,now(),@witness,@witnessedAt);";
        command.Parameters.AddWithValue("id",eventId);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("lot",lot.LotId);command.Parameters.AddWithValue("counterparty",(object?)counterpartyLot?.LotId??DBNull.Value);command.Parameters.AddWithValue("source",(object?)sourceLocation?.LocationId??DBNull.Value);command.Parameters.AddWithValue("destination",(object?)destinationLocation?.LocationId??DBNull.Value);command.Parameters.AddWithValue("patient",(object?)patientId??DBNull.Value);command.Parameters.AddWithValue("encounter",(object?)encounter??DBNull.Value);command.Parameters.AddWithValue("quantity",quantity);command.Parameters.AddWithValue("delta",quantityDelta);command.Parameters.AddWithValue("reason",reason);command.Parameters.AddWithValue("related",(object?)relatedEventId??DBNull.Value);command.Parameters.AddWithValue("key",idempotencyKey);command.Parameters.AddWithValue("sourceBefore",(object?)sourceBefore??DBNull.Value);command.Parameters.AddWithValue("sourceAfter",(object?)sourceAfter??DBNull.Value);command.Parameters.AddWithValue("destinationBefore",(object?)destinationBefore??DBNull.Value);command.Parameters.AddWithValue("destinationAfter",(object?)destinationAfter??DBNull.Value);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("occurred",occurredAt);command.Parameters.AddWithValue("witness",(object?)witnessUsername??DBNull.Value);command.Parameters.AddWithValue("witnessedAt",(object?)witnessedAt??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<InventoryPrescriptionDispenseResponse> DispensePrescriptionAsync(InventoryPrescriptionDispenseRequest request, string username, CancellationToken cancellationToken)
    {
        var prescriptionId = request.PrescriptionId?.Trim();
        if (string.IsNullOrWhiteSpace(prescriptionId) || request.Quantity <= 0 || request.Fee < 0 || request.Notes?.Trim().Length > 250 || string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Prescription, positive quantity, nonnegative fee, and valid dispense details are required.");
        var saleDate = ParseOptionalDate(request.SaleDate, "Sale date must be an ISO date.") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (saleDate < new DateOnly(2000, 1, 1) || saleDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("Sale date cannot be in the future or before 2000-01-01.");
        var now = DateTimeOffset.UtcNow; var notes = NormalizeOptional(request.Notes); var saleId = Guid.NewGuid(); var transactionId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string patientId; int encounter; string rxNormCode;
        await using (var prescription = connection.CreateCommand())
        {
            prescription.Transaction = transaction;
            prescription.CommandText = "select patient_id,encounter,rx_norm_code from prescriptions where id=@id and active=1 for update;";
            prescription.Parameters.AddWithValue("id", prescriptionId);
            await using var reader = await prescription.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The prescription was not found or is inactive.");
            patientId = reader.GetString(0); encounter = reader.IsDBNull(1) ? 0 : reader.GetInt32(1); rxNormCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim();
        }
        if (encounter <= 0 || string.IsNullOrWhiteSpace(rxNormCode)) throw new ArgumentException("The active prescription requires an encounter and RXCUI code before it can be dispensed.");
        int itemId;
        await using (var link = connection.CreateCommand())
        {
            link.Transaction = transaction;
            link.CommandText = "select i.item_id from inventory_item_medication_links l join inventory_items i on i.item_id=l.item_id where l.rx_norm_code=@code and i.active=true;";
            link.Parameters.AddWithValue("code", rxNormCode);
            var linkedItem = await link.ExecuteScalarAsync(cancellationToken);
            if (linkedItem is null) throw new ArgumentException("No active inventory item is linked to this prescription's RXCUI code.");
            itemId = Convert.ToInt32(linkedItem, CultureInfo.InvariantCulture);
        }
        await EnsureGeneralInventoryItemAsync(connection, transaction, itemId, cancellationToken);
        int lotId; string facilityCode; string facilityName; string lotNumber; DateOnly? expiration; decimal onHand; decimal unitCost; decimal reorderPoint;
        await using (var lot = connection.CreateCommand())
        {
            lot.Transaction = transaction;
            lot.CommandText = "select l.lot_id,f.code,f.name,l.lot_number,l.expiration_date,l.quantity_on_hand,l.unit_cost,i.reorder_point from inventory_lots l join facilities f on f.id=l.facility_id join inventory_items i on i.item_id=l.item_id where l.item_id=@itemId and l.status='active' and l.quantity_on_hand>=@quantity and (l.expiration_date is null or l.expiration_date>@saleDate) order by l.expiration_date nulls last,l.lot_number,l.lot_id limit 1 for update;";
            lot.Parameters.AddWithValue("itemId", itemId); lot.Parameters.AddWithValue("quantity", request.Quantity); lot.Parameters.AddWithValue("saleDate", saleDate);
            await using var reader = await lot.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("No single eligible lot can fulfill this prescription. Prescription dispensing cannot combine lots.");
            lotId=reader.GetInt32(0); facilityCode=reader.GetString(1); facilityName=reader.GetString(2); lotNumber=reader.GetString(3); expiration=reader.IsDBNull(4)?null:reader.GetFieldValue<DateOnly>(4); onHand=reader.GetDecimal(5); unitCost=reader.GetDecimal(6); reorderPoint=reader.GetDecimal(7);
        }
        var updatedQuantity=onHand-request.Quantity;
        await using (var update=connection.CreateCommand()) { update.Transaction=transaction; update.CommandText="update inventory_lots set quantity_on_hand=@quantity where lot_id=@lotId;"; update.Parameters.AddWithValue("quantity",updatedQuantity); update.Parameters.AddWithValue("lotId",lotId); await update.ExecuteNonQueryAsync(cancellationToken); }
        await using (var ledger=connection.CreateCommand()) { ledger.Transaction=transaction; ledger.CommandText="insert into inventory_transactions (transaction_id,lot_id,transaction_type,quantity_delta,reason,performed_by,occurred_at) values (@id,@lotId,'sale',@quantity,@reason,@user,@at);"; ledger.Parameters.AddWithValue("id",transactionId);ledger.Parameters.AddWithValue("lotId",lotId);ledger.Parameters.AddWithValue("quantity",-request.Quantity);ledger.Parameters.AddWithValue("reason",(object?)notes??DBNull.Value);ledger.Parameters.AddWithValue("user",username);ledger.Parameters.AddWithValue("at",now);await ledger.ExecuteNonQueryAsync(cancellationToken); }
        await using (var sale=connection.CreateCommand()) { sale.Transaction=transaction; sale.CommandText="insert into inventory_patient_sales (sale_id,lot_id,patient_id,encounter,sale_date,quantity,fee,notes,transaction_id,sold_by,sold_at,prescription_id) values (@saleId,@lotId,@patientId,@encounter,@saleDate,@quantity,@fee,@notes,@transactionId,@user,@at,@prescriptionId);"; sale.Parameters.AddWithValue("saleId",saleId);sale.Parameters.AddWithValue("lotId",lotId);sale.Parameters.AddWithValue("patientId",patientId);sale.Parameters.AddWithValue("encounter",encounter);sale.Parameters.AddWithValue("saleDate",saleDate);sale.Parameters.AddWithValue("quantity",request.Quantity);sale.Parameters.AddWithValue("fee",request.Fee);sale.Parameters.AddWithValue("notes",(object?)notes??DBNull.Value);sale.Parameters.AddWithValue("transactionId",transactionId);sale.Parameters.AddWithValue("user",username);sale.Parameters.AddWithValue("at",now);sale.Parameters.AddWithValue("prescriptionId",prescriptionId);await sale.ExecuteNonQueryAsync(cancellationToken); }
        await using var total=connection.CreateCommand(); total.Transaction=transaction; total.CommandText="select coalesce(sum(quantity_on_hand),0) from inventory_lots where item_id=@itemId and status='active';"; total.Parameters.AddWithValue("itemId",itemId); var itemQuantity=Convert.ToDecimal(await total.ExecuteScalarAsync(cancellationToken),CultureInfo.InvariantCulture);
        await transaction.CommitAsync(cancellationToken);
        var inventoryLot=new InventoryLot(lotId,facilityCode,facilityName,lotNumber,expiration?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),updatedQuantity,unitCost,"active");
        var mutation=new InventoryMutationResponse(new InventoryTransactionItem(transactionId,lotId,string.Empty,string.Empty,facilityCode,"sale",-request.Quantity,notes,username,now,null,null),inventoryLot,itemQuantity,itemQuantity<=reorderPoint);
        var saleResponse=new InventoryPatientSaleResponse(saleId,patientId,encounter,saleDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),request.Quantity,request.Fee,notes,username,now.ToString("O",CultureInfo.InvariantCulture),mutation);
        return new InventoryPrescriptionDispenseResponse(prescriptionId,itemId,patientId,encounter,rxNormCode,saleResponse);
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
        var lotStatus = lotReader.GetString(8);
        if (!string.Equals(lotStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only active inventory lots can receive stock activity.");
        }
        var updatedQuantity = existingQuantity + quantityDelta;
        if (updatedQuantity < 0)
        {
            throw new ArgumentException("The requested inventory activity would make the lot quantity negative.");
        }

        var lot = new InventoryLot(
            lotReader.GetInt32(0), lotReader.GetString(2), lotReader.GetString(3), lotReader.GetString(4),
            lotReader.IsDBNull(5) ? null : lotReader.GetFieldValue<DateOnly>(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            updatedQuantity, lotReader.GetDecimal(7), lotStatus);
        var itemId = lotReader.GetInt32(1);
        var reorderPoint = lotReader.GetDecimal(9);
        await lotReader.DisposeAsync();
        await EnsureGeneralInventoryLotAsync(connection, transaction, request.LotId, cancellationToken);

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

    public async Task<InventoryPatientSaleResponse?> CreatePatientSaleAsync(
        InventoryPatientSaleCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (request.LotId <= 0 || string.IsNullOrWhiteSpace(request.PatientId) || request.Encounter <= 0
            || request.Quantity <= 0 || request.Fee < 0 || request.Notes?.Trim().Length > 250 || string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Lot, patient, encounter, positive quantity, nonnegative fee, and valid sale details are required.");
        var saleDate = ParseOptionalDate(request.SaleDate, "Sale date must be an ISO date.") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (saleDate < new DateOnly(2000, 1, 1) || saleDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Sale date cannot be in the future or before 2000-01-01.");
        var now = DateTimeOffset.UtcNow;
        var saleId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var encounter = connection.CreateCommand())
        {
            encounter.Transaction = transaction;
            encounter.CommandText = "select exists(select 1 from encounters where encounter = @encounter and patient_id = @patientId);";
            encounter.Parameters.AddWithValue("encounter", request.Encounter);
            encounter.Parameters.AddWithValue("patientId", request.PatientId.Trim());
            if (await encounter.ExecuteScalarAsync(cancellationToken) is not true)
                throw new ArgumentException("The encounter must belong to the selected patient.");
        }
        await using var lotCommand = connection.CreateCommand();
        lotCommand.Transaction = transaction;
        lotCommand.CommandText = """
            select l.item_id, f.code, f.name, l.lot_number, l.expiration_date, l.quantity_on_hand, l.unit_cost, l.status, i.reorder_point
            from inventory_lots l join inventory_items i on i.item_id = l.item_id join facilities f on f.id = l.facility_id
            where l.lot_id = @lotId for update;
            """;
        lotCommand.Parameters.AddWithValue("lotId", request.LotId);
        await using var reader = await lotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var itemId = reader.GetInt32(0); var facilityCode = reader.GetString(1); var facilityName = reader.GetString(2); var lotNumber = reader.GetString(3);
        var expiration = reader.IsDBNull(4) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(4); var onHand = reader.GetDecimal(5); var cost = reader.GetDecimal(6); var status = reader.GetString(7); var reorderPoint = reader.GetDecimal(8);
        await reader.DisposeAsync();
        await EnsureGeneralInventoryLotAsync(connection, transaction, request.LotId, cancellationToken);
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only active inventory lots can be sold.");
        if (expiration is not null && expiration <= saleDate) throw new ArgumentException("Expired inventory lots cannot be sold.");
        if (onHand < request.Quantity) throw new ArgumentException("The requested sale would make the lot quantity negative.");
        var updatedQuantity = onHand - request.Quantity; var notes = NormalizeOptional(request.Notes);
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update inventory_lots set quantity_on_hand = @quantity where lot_id = @lotId;"; update.Parameters.AddWithValue("quantity", updatedQuantity); update.Parameters.AddWithValue("lotId", request.LotId); await update.ExecuteNonQueryAsync(cancellationToken); }
        await using (var ledger = connection.CreateCommand()) { ledger.Transaction = transaction; ledger.CommandText = "insert into inventory_transactions (transaction_id, lot_id, transaction_type, quantity_delta, reason, performed_by, occurred_at) values (@id, @lotId, 'sale', @quantity, @reason, @user, @at);"; ledger.Parameters.AddWithValue("id", transactionId); ledger.Parameters.AddWithValue("lotId", request.LotId); ledger.Parameters.AddWithValue("quantity", -request.Quantity); ledger.Parameters.AddWithValue("reason", (object?)notes ?? DBNull.Value); ledger.Parameters.AddWithValue("user", username); ledger.Parameters.AddWithValue("at", now); await ledger.ExecuteNonQueryAsync(cancellationToken); }
        await using (var sale = connection.CreateCommand()) { sale.Transaction = transaction; sale.CommandText = "insert into inventory_patient_sales (sale_id, lot_id, patient_id, encounter, sale_date, quantity, fee, notes, transaction_id, sold_by, sold_at) values (@saleId, @lotId, @patientId, @encounter, @saleDate, @quantity, @fee, @notes, @transactionId, @user, @at);"; sale.Parameters.AddWithValue("saleId", saleId); sale.Parameters.AddWithValue("lotId", request.LotId); sale.Parameters.AddWithValue("patientId", request.PatientId.Trim()); sale.Parameters.AddWithValue("encounter", request.Encounter); sale.Parameters.AddWithValue("saleDate", saleDate); sale.Parameters.AddWithValue("quantity", request.Quantity); sale.Parameters.AddWithValue("fee", request.Fee); sale.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value); sale.Parameters.AddWithValue("transactionId", transactionId); sale.Parameters.AddWithValue("user", username); sale.Parameters.AddWithValue("at", now); await sale.ExecuteNonQueryAsync(cancellationToken); }
        await using var total = connection.CreateCommand(); total.Transaction = transaction; total.CommandText = "select coalesce(sum(quantity_on_hand), 0) from inventory_lots where item_id = @itemId and status = 'active';"; total.Parameters.AddWithValue("itemId", itemId); var itemQuantity = Convert.ToDecimal(await total.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await transaction.CommitAsync(cancellationToken);
        var lot = new InventoryLot(request.LotId, facilityCode, facilityName, lotNumber, expiration?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedQuantity, cost, status);
        var mutation = new InventoryMutationResponse(new InventoryTransactionItem(transactionId, request.LotId, string.Empty, string.Empty, facilityCode, "sale", -request.Quantity, notes, username, now, null, null), lot, itemQuantity, itemQuantity <= reorderPoint);
        return new InventoryPatientSaleResponse(saleId, request.PatientId.Trim(), request.Encounter, saleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), request.Quantity, request.Fee, notes, username, now.ToString("O", CultureInfo.InvariantCulture), mutation);
    }

    public async Task<InventoryPatientSaleAllocationResponse> CreatePatientSaleAllocationAsync(InventoryPatientSaleAllocationCreateRequest request, string username, CancellationToken cancellationToken)
    {
        if (request.ItemId <= 0 || string.IsNullOrWhiteSpace(request.PatientId) || request.Encounter <= 0 || request.Quantity <= 0 || request.Fee < 0 || request.Notes?.Trim().Length > 250 || string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Item, patient, encounter, positive quantity, nonnegative fee, and valid sale details are required.");
        var saleDate = ParseOptionalDate(request.SaleDate, "Sale date must be an ISO date.") ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (saleDate < new DateOnly(2000, 1, 1) || saleDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("Sale date cannot be in the future or before 2000-01-01.");
        var now = DateTimeOffset.UtcNow; var batchId = Guid.NewGuid(); var patientId = request.PatientId.Trim(); var notes = NormalizeOptional(request.Notes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureGeneralInventoryItemAsync(connection, transaction, request.ItemId, cancellationToken);
        await using (var patient = connection.CreateCommand()) { patient.Transaction = transaction; patient.CommandText = "select exists(select 1 from encounters where encounter=@encounter and patient_id=@patientId);"; patient.Parameters.AddWithValue("encounter", request.Encounter); patient.Parameters.AddWithValue("patientId", patientId); if (await patient.ExecuteScalarAsync(cancellationToken) is not true) throw new ArgumentException("The encounter must belong to the selected patient."); }
        var lots = new List<(int Id, string Number, decimal OnHand)>();
        await using (var command = connection.CreateCommand()) { command.Transaction = transaction; command.CommandText = "select lot_id, lot_number, quantity_on_hand from inventory_lots where item_id=@itemId and status='active' and quantity_on_hand > 0 and (expiration_date is null or expiration_date > @saleDate) order by expiration_date nulls last, lot_number, lot_id for update;"; command.Parameters.AddWithValue("itemId", request.ItemId); command.Parameters.AddWithValue("saleDate", saleDate); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) lots.Add((reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2))); }
        if (lots.Sum(lot => lot.OnHand) < request.Quantity) throw new ArgumentException("Eligible inventory lots cannot fulfill the requested sale quantity.");
        await using (var batch = connection.CreateCommand()) { batch.Transaction = transaction; batch.CommandText = "insert into inventory_patient_sale_batches (sale_batch_id,item_id,patient_id,encounter,sale_date,quantity,fee,notes,sold_by,sold_at) values (@id,@item,@patient,@encounter,@date,@quantity,@fee,@notes,@user,@at);"; batch.Parameters.AddWithValue("id",batchId); batch.Parameters.AddWithValue("item",request.ItemId); batch.Parameters.AddWithValue("patient",patientId); batch.Parameters.AddWithValue("encounter",request.Encounter); batch.Parameters.AddWithValue("date",saleDate); batch.Parameters.AddWithValue("quantity",request.Quantity); batch.Parameters.AddWithValue("fee",request.Fee); batch.Parameters.AddWithValue("notes",(object?)notes??DBNull.Value); batch.Parameters.AddWithValue("user",username); batch.Parameters.AddWithValue("at",now); await batch.ExecuteNonQueryAsync(cancellationToken); }
        var remaining = request.Quantity; var remainingFee = request.Fee; var lines = new List<InventoryPatientSaleAllocationLine>();
        foreach (var lot in lots) { if (remaining <= 0) break; var quantity = Math.Min(remaining, lot.OnHand); remaining -= quantity; var fee = remaining == 0 ? remainingFee : Math.Round(request.Fee * quantity / request.Quantity, 2, MidpointRounding.AwayFromZero); remainingFee -= fee; var saleId=Guid.NewGuid(); var transactionId=Guid.NewGuid();
            await using (var update=connection.CreateCommand()) { update.Transaction=transaction; update.CommandText="update inventory_lots set quantity_on_hand=quantity_on_hand-@quantity where lot_id=@lotId;"; update.Parameters.AddWithValue("quantity",quantity); update.Parameters.AddWithValue("lotId",lot.Id); await update.ExecuteNonQueryAsync(cancellationToken); }
            await using (var ledger=connection.CreateCommand()) { ledger.Transaction=transaction; ledger.CommandText="insert into inventory_transactions (transaction_id,lot_id,transaction_type,quantity_delta,reason,performed_by,occurred_at) values (@id,@lot,'sale',@quantity,@reason,@user,@at);"; ledger.Parameters.AddWithValue("id",transactionId);ledger.Parameters.AddWithValue("lot",lot.Id);ledger.Parameters.AddWithValue("quantity",-quantity);ledger.Parameters.AddWithValue("reason",(object?)notes??DBNull.Value);ledger.Parameters.AddWithValue("user",username);ledger.Parameters.AddWithValue("at",now);await ledger.ExecuteNonQueryAsync(cancellationToken); }
            await using (var sale=connection.CreateCommand()) { sale.Transaction=transaction; sale.CommandText="insert into inventory_patient_sales (sale_id,lot_id,patient_id,encounter,sale_date,quantity,fee,notes,transaction_id,sold_by,sold_at,sale_batch_id) values (@id,@lot,@patient,@encounter,@date,@quantity,@fee,@notes,@transaction,@user,@at,@batch);";sale.Parameters.AddWithValue("id",saleId);sale.Parameters.AddWithValue("lot",lot.Id);sale.Parameters.AddWithValue("patient",patientId);sale.Parameters.AddWithValue("encounter",request.Encounter);sale.Parameters.AddWithValue("date",saleDate);sale.Parameters.AddWithValue("quantity",quantity);sale.Parameters.AddWithValue("fee",fee);sale.Parameters.AddWithValue("notes",(object?)notes??DBNull.Value);sale.Parameters.AddWithValue("transaction",transactionId);sale.Parameters.AddWithValue("user",username);sale.Parameters.AddWithValue("at",now);sale.Parameters.AddWithValue("batch",batchId);await sale.ExecuteNonQueryAsync(cancellationToken); }
            lines.Add(new(saleId,lot.Id,lot.Number,quantity,fee,transactionId)); }
        await transaction.CommitAsync(cancellationToken); return new(batchId,request.ItemId,patientId,request.Encounter,saleDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),request.Quantity,request.Fee,lines);
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
        await EnsureGeneralInventoryLotAsync(connection, transaction, sourceLotId, cancellationToken);

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
        await EnsureGeneralInventoryLotAsync(connection, transaction, lotId, cancellationToken);

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

    public async Task<InventoryLotDestructionResponse?> DestroyLotAsync(
        int lotId,
        InventoryLotDestructionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (lotId <= 0 || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(request.Method)
            || string.IsNullOrWhiteSpace(request.Witness)
            || string.IsNullOrWhiteSpace(request.Notes)
            || request.Method?.Trim().Length > 250
            || request.Witness?.Trim().Length > 250
            || request.Notes?.Trim().Length > 250)
        {
            throw new ArgumentException("A valid lot, destruction method, witness, notes, authenticated user, and details of 250 characters or fewer are required.");
        }

        var destructionDate = ParseOptionalDate(request.DestructionDate, "Destruction date must be an ISO date.")
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (destructionDate < new DateOnly(2000, 1, 1) || destructionDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Destruction date cannot be in the future or before 2000-01-01.");
        }
        var method = NormalizeOptional(request.Method);
        var witness = NormalizeOptional(request.Witness);
        var notes = NormalizeOptional(request.Notes);
        var recordedAt = DateTimeOffset.UtcNow;
        var destructionId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var lotCommand = connection.CreateCommand();
        lotCommand.Transaction = transaction;
        lotCommand.CommandText = """
            select i.item_code, i.name, f.code, f.name, l.lot_number, l.expiration_date,
              l.quantity_on_hand, l.unit_cost, l.status
            from inventory_lots l
            join inventory_items i on i.item_id = l.item_id
            join facilities f on f.id = l.facility_id
            where l.lot_id = @lotId
            for update;
            """;
        lotCommand.Parameters.AddWithValue("lotId", lotId);
        await using var reader = await lotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var itemCode = reader.GetString(0);
        var itemName = reader.GetString(1);
        var facilityCode = reader.GetString(2);
        var facilityName = reader.GetString(3);
        var lotNumber = reader.GetString(4);
        var expirationDate = reader.IsDBNull(5) ? (DateOnly?)null : reader.GetFieldValue<DateOnly>(5);
        var quantityOnHand = reader.GetDecimal(6);
        var unitCost = reader.GetDecimal(7);
        var status = reader.GetString(8);
        await reader.DisposeAsync();
        await EnsureGeneralInventoryLotAsync(connection, transaction, lotId, cancellationToken);
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The inventory lot has already been destroyed or is inactive.");
        }
        if (quantityOnHand <= 0)
        {
            throw new ArgumentException("Only an active inventory lot with quantity on hand can be destroyed.");
        }

        await using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "update inventory_lots set status = 'inactive', quantity_on_hand = 0 where lot_id = @lotId;";
            updateCommand.Parameters.AddWithValue("lotId", lotId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var ledgerCommand = connection.CreateCommand())
        {
            ledgerCommand.Transaction = transaction;
            ledgerCommand.CommandText = """
                insert into inventory_transactions (
                  transaction_id, lot_id, transaction_type, quantity_delta, reason, performed_by, occurred_at
                ) values (
                  @transactionId, @lotId, 'destruction', @quantityDelta, @reason, @user, @recordedAt
                );
                """;
            ledgerCommand.Parameters.AddWithValue("transactionId", transactionId);
            ledgerCommand.Parameters.AddWithValue("lotId", lotId);
            ledgerCommand.Parameters.AddWithValue("quantityDelta", -quantityOnHand);
            ledgerCommand.Parameters.AddWithValue("reason", notes!);
            ledgerCommand.Parameters.AddWithValue("user", username);
            ledgerCommand.Parameters.AddWithValue("recordedAt", recordedAt);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.Transaction = transaction;
            auditCommand.CommandText = "insert into inventory_lot_destructions (destruction_id, lot_id, destruction_date, destruction_method, destruction_witness, destruction_notes, destroyed_by, recorded_at) values (@id, @lotId, @date, @method, @witness, @notes, @user, @recordedAt);";
            auditCommand.Parameters.AddWithValue("id", destructionId);
            auditCommand.Parameters.AddWithValue("lotId", lotId);
            auditCommand.Parameters.AddWithValue("date", destructionDate);
            auditCommand.Parameters.AddWithValue("method", (object?)method ?? DBNull.Value);
            auditCommand.Parameters.AddWithValue("witness", (object?)witness ?? DBNull.Value);
            auditCommand.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
            auditCommand.Parameters.AddWithValue("user", username);
            auditCommand.Parameters.AddWithValue("recordedAt", recordedAt);
            await auditCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var lot = new InventoryLot(lotId, facilityCode, facilityName, lotNumber,
            expirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0, unitCost, "inactive");
        var ledgerEntry = new InventoryTransactionItem(
            transactionId, lotId, itemCode, itemName, facilityCode, "destruction", -quantityOnHand, notes,
            username, recordedAt, null, null);
        return new InventoryLotDestructionResponse(
            destructionId,
            lot,
            quantityOnHand,
            ledgerEntry,
            destructionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), method, witness, notes, username,
            recordedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    public async Task<InventoryExpiryDispositionResponse?> CreateExpiryDispositionAsync(int lotId, InventoryExpiryDispositionRequest request, string username, CancellationToken cancellationToken)
    {
        var disposition = request.Disposition?.Trim().ToLowerInvariant();
        if (lotId <= 0 || string.IsNullOrWhiteSpace(username) || disposition is not ("quarantine" or "return" or "destroy") || string.IsNullOrWhiteSpace(request.Notes)
            || (disposition == "destroy" && (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(request.Witness)))
            || request.Notes.Trim().Length > 500 || request.Method?.Trim().Length > 250 || request.Witness?.Trim().Length > 250)
            throw new ArgumentException("An expired lot, quarantine/return/destroy decision, required notes, authenticated user, and valid method and witness for destruction are required.");
        var notes = request.Notes.Trim(); var method = NormalizeOptional(request.Method); var witness = NormalizeOptional(request.Witness); var now = DateTimeOffset.UtcNow; var dispositionId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        int itemId; string itemCode; string itemName; string facilityCode; string facilityName; string lotNumber; DateOnly? expirationDate; decimal quantity; decimal unitCost; string status; decimal reorderPoint;
        await using (var lotCommand = connection.CreateCommand())
        {
            lotCommand.Transaction = transaction;
            lotCommand.CommandText = "select l.item_id,i.item_code,i.name,f.code,f.name,l.lot_number,l.expiration_date,l.quantity_on_hand,l.unit_cost,l.status,i.reorder_point from inventory_lots l join inventory_items i on i.item_id=l.item_id join facilities f on f.id=l.facility_id where l.lot_id=@lotId for update;";
            lotCommand.Parameters.AddWithValue("lotId", lotId); await using var reader = await lotCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            itemId=reader.GetInt32(0); itemCode=reader.GetString(1); itemName=reader.GetString(2); facilityCode=reader.GetString(3); facilityName=reader.GetString(4); lotNumber=reader.GetString(5); expirationDate=reader.IsDBNull(6)?null:reader.GetFieldValue<DateOnly>(6); quantity=reader.GetDecimal(7); unitCost=reader.GetDecimal(8); status=reader.GetString(9); reorderPoint=reader.GetDecimal(10);
        }
        await EnsureGeneralInventoryLotAsync(connection, transaction, lotId, cancellationToken);
        if (expirationDate is null || expirationDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("Only expired inventory lots can receive an expiry disposition.");
        if (disposition == "quarantine" && !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only active expired lots can be quarantined.");
        if (disposition != "quarantine" && status is not ("active" or "quarantined")) throw new ArgumentException("Only active or quarantined expired lots can be returned or destroyed.");
        InventoryTransactionItem? ledgerEntry = null; Guid? transactionId = null; Guid? destructionId = null; var resultingStatus = disposition == "quarantine" ? "quarantined" : "inactive";
        if ((disposition is "return" or "destroy") && quantity > 0)
        {
            transactionId = Guid.NewGuid();
            await using var ledger = connection.CreateCommand(); ledger.Transaction=transaction;
            ledger.CommandText="insert into inventory_transactions (transaction_id,lot_id,transaction_type,quantity_delta,reason,performed_by,occurred_at) values (@id,@lot,@type,@quantity,@reason,@user,@at);";
            ledger.Parameters.AddWithValue("id",transactionId.Value); ledger.Parameters.AddWithValue("lot",lotId); ledger.Parameters.AddWithValue("type",disposition); ledger.Parameters.AddWithValue("quantity",-quantity); ledger.Parameters.AddWithValue("reason",notes); ledger.Parameters.AddWithValue("user",username); ledger.Parameters.AddWithValue("at",now); await ledger.ExecuteNonQueryAsync(cancellationToken);
            ledgerEntry = new InventoryTransactionItem(transactionId.Value, lotId, itemCode, itemName, facilityCode, disposition, -quantity, notes, username, now, null, null);
        }
        if (disposition == "destroy")
        {
            destructionId = Guid.NewGuid();
            await using var destruction = connection.CreateCommand(); destruction.Transaction=transaction;
            destruction.CommandText="insert into inventory_lot_destructions (destruction_id,lot_id,destruction_date,destruction_method,destruction_witness,destruction_notes,destroyed_by,recorded_at) values (@id,@lot,@date,@method,@witness,@notes,@user,@at);";
            destruction.Parameters.AddWithValue("id",destructionId.Value); destruction.Parameters.AddWithValue("lot",lotId); destruction.Parameters.AddWithValue("date",DateOnly.FromDateTime(DateTime.UtcNow)); destruction.Parameters.AddWithValue("method",(object?)method??DBNull.Value); destruction.Parameters.AddWithValue("witness",(object?)witness??DBNull.Value); destruction.Parameters.AddWithValue("notes",notes); destruction.Parameters.AddWithValue("user",username); destruction.Parameters.AddWithValue("at",now); await destruction.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var update = connection.CreateCommand()) { update.Transaction=transaction; update.CommandText="update inventory_lots set status=@status, quantity_on_hand=@quantity where lot_id=@lotId;"; update.Parameters.AddWithValue("status",resultingStatus); update.Parameters.AddWithValue("quantity",disposition == "quarantine" ? quantity : 0); update.Parameters.AddWithValue("lotId",lotId); await update.ExecuteNonQueryAsync(cancellationToken); }
        await using (var audit = connection.CreateCommand()) { audit.Transaction=transaction; audit.CommandText="insert into inventory_lot_expiry_dispositions (disposition_id,lot_id,disposition,quantity_affected,notes,method,witness,transaction_id,destruction_id,disposed_by,disposed_at) values (@id,@lot,@disposition,@quantity,@notes,@method,@witness,@transaction,@destruction,@user,@at);"; audit.Parameters.AddWithValue("id",dispositionId); audit.Parameters.AddWithValue("lot",lotId); audit.Parameters.AddWithValue("disposition",disposition); audit.Parameters.AddWithValue("quantity",quantity); audit.Parameters.AddWithValue("notes",notes); audit.Parameters.AddWithValue("method",(object?)method??DBNull.Value); audit.Parameters.AddWithValue("witness",(object?)witness??DBNull.Value); audit.Parameters.AddWithValue("transaction",(object?)transactionId??DBNull.Value); audit.Parameters.AddWithValue("destruction",(object?)destructionId??DBNull.Value); audit.Parameters.AddWithValue("user",username); audit.Parameters.AddWithValue("at",now); await audit.ExecuteNonQueryAsync(cancellationToken); }
        await transaction.CommitAsync(cancellationToken);
        var lot = new InventoryLot(lotId,facilityCode,facilityName,lotNumber,expirationDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),disposition == "quarantine" ? quantity : 0,unitCost,resultingStatus);
        return new InventoryExpiryDispositionResponse(dispositionId,disposition,lot,quantity,notes,method,witness,username,now.ToString("O",CultureInfo.InvariantCulture),ledgerEntry,destructionId);
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

    public async Task<IReadOnlyList<InventoryPurchaseRequisition>> GetPurchaseRequisitionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select requisition_id from inventory_purchase_requisitions order by requested_at desc, requisition_id desc limit 100;";
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
        await reader.DisposeAsync();
        var result = new List<InventoryPurchaseRequisition>();
        foreach (var id in ids)
        {
            var requisition = await GetPurchaseRequisitionAsync(connection, id, cancellationToken);
            if (requisition is not null) result.Add(requisition);
        }
        return result;
    }

    public async Task<InventoryPurchaseRequisition?> CreatePurchaseRequisitionAsync(InventoryPurchaseRequisitionCreateRequest request, string username, CancellationToken cancellationToken)
    {
        ValidatePurchaseRequisition(request, username);
        var now = DateTimeOffset.UtcNow; var requisitionId = Guid.NewGuid(); var notes = NormalizeOptional(request.Notes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var facility = await GetFacilityAsync(connection, transaction, request.FacilityId, cancellationToken);
        if (facility is null) throw new ArgumentException("The requisition facility was not found.");
        if (request.VendorId is { } vendorId && await GetActiveVendorAsync(connection, transaction, vendorId, cancellationToken) is null) throw new ArgumentException("The requisition vendor was not found or is inactive.");
        foreach (var line in request.Lines)
            if (await GetActiveItemAsync(connection, transaction, line.ItemId, cancellationToken) is null) throw new ArgumentException("Each requisition line must reference an active inventory item.");
        await using (var header = connection.CreateCommand())
        {
            header.Transaction = transaction;
            header.CommandText = "insert into inventory_purchase_requisitions (requisition_id,facility_id,vendor_id,status,notes,requested_by,requested_at) values (@id,@facility,@vendor,'draft',@notes,@user,@at);";
            header.Parameters.AddWithValue("id", requisitionId); header.Parameters.AddWithValue("facility", request.FacilityId); header.Parameters.AddWithValue("vendor", (object?)request.VendorId ?? DBNull.Value); header.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value); header.Parameters.AddWithValue("user", username); header.Parameters.AddWithValue("at", now);
            await header.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var line in request.Lines)
        {
            await using var insertLine = connection.CreateCommand(); insertLine.Transaction = transaction;
            insertLine.CommandText = "insert into inventory_purchase_requisition_lines (requisition_line_id,requisition_id,item_id,requested_quantity) values (@lineId,@requisitionId,@itemId,@quantity);";
            insertLine.Parameters.AddWithValue("lineId", Guid.NewGuid()); insertLine.Parameters.AddWithValue("requisitionId", requisitionId); insertLine.Parameters.AddWithValue("itemId", line.ItemId); insertLine.Parameters.AddWithValue("quantity", line.Quantity);
            await insertLine.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertPurchaseRequisitionEventAsync(connection, transaction, requisitionId, "created", notes, username, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPurchaseRequisitionAsync(connection, requisitionId, cancellationToken);
    }

    public async Task<InventoryPurchaseRequisition?> SubmitPurchaseRequisitionAsync(Guid requisitionId, string username, CancellationToken cancellationToken)
        => await ChangePurchaseRequisitionStatusAsync(requisitionId, "submitted", null, username, cancellationToken);

    public async Task<InventoryPurchaseRequisition?> DecidePurchaseRequisitionAsync(Guid requisitionId, bool approved, InventoryPurchaseRequisitionDecisionRequest request, string username, CancellationToken cancellationToken)
    {
        var note = NormalizeOptional(request.Notes);
        if (!approved && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A rejection reason is required.");
        return await ChangePurchaseRequisitionStatusAsync(requisitionId, approved ? "approved" : "rejected", note, username, cancellationToken);
    }

    private async Task<InventoryPurchaseRequisition?> ChangePurchaseRequisitionStatusAsync(Guid requisitionId, string nextStatus, string? note, string username, CancellationToken cancellationToken)
    {
        if (requisitionId == Guid.Empty || string.IsNullOrWhiteSpace(username)) throw new ArgumentException("A requisition and authenticated user are required.");
        var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var expectedStatus = nextStatus == "submitted" ? "draft" : "submitted";
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = nextStatus == "submitted"
                ? "update inventory_purchase_requisitions set status='submitted',submitted_by=@user,submitted_at=@at where requisition_id=@id and status='draft';"
                : "update inventory_purchase_requisitions set status=@status,decided_by=@user,decided_at=@at,decision_notes=@note where requisition_id=@id and status='submitted';";
            update.Parameters.AddWithValue("id", requisitionId); update.Parameters.AddWithValue("user", username); update.Parameters.AddWithValue("at", now);
            if (nextStatus != "submitted") { update.Parameters.AddWithValue("status", nextStatus); update.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); }
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                var exists = await PurchaseRequisitionExistsAsync(connection, transaction, requisitionId, cancellationToken);
                if (!exists) return null;
                throw new ArgumentException($"Only {expectedStatus} purchase requisitions can be {nextStatus}.");
            }
        }
        await InsertPurchaseRequisitionEventAsync(connection, transaction, requisitionId, nextStatus, note, username, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPurchaseRequisitionAsync(connection, requisitionId, cancellationToken);
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
        await EnsureGeneralInventoryItemAsync(connection, transaction, item.ItemId, cancellationToken);
        var requisitionReconciliation = request.RequisitionId is { } requisitionId
            ? await GetReceiptReconciliationContextAsync(connection, transaction, requisitionId, vendor.VendorId, facility.FacilityId, item.ItemId, request.Quantity, cancellationToken)
            : null;

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
        InventoryPurchaseReceiptReconciliation? reconciliation = null;
        if (requisitionReconciliation is not null)
        {
            var reconciliationId = Guid.NewGuid();
            await using (var reconciliationCommand = connection.CreateCommand())
            {
                reconciliationCommand.Transaction = transaction;
                reconciliationCommand.CommandText = "insert into inventory_purchase_requisition_receipts (reconciliation_id,requisition_id,requisition_line_id,receipt_id,received_quantity,reconciled_by,reconciled_at) values (@id,@requisitionId,@lineId,@receiptId,@quantity,@user,@at);";
                reconciliationCommand.Parameters.AddWithValue("id", reconciliationId); reconciliationCommand.Parameters.AddWithValue("requisitionId", requisitionReconciliation.RequisitionId); reconciliationCommand.Parameters.AddWithValue("lineId", requisitionReconciliation.RequisitionLineId); reconciliationCommand.Parameters.AddWithValue("receiptId", receiptId); reconciliationCommand.Parameters.AddWithValue("quantity", request.Quantity); reconciliationCommand.Parameters.AddWithValue("user", username); reconciliationCommand.Parameters.AddWithValue("at", now);
                await reconciliationCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertPurchaseRequisitionEventAsync(connection, transaction, requisitionReconciliation.RequisitionId, "receipt_reconciled", $"Receipt {receiptId} reconciled: {request.Quantity.ToString(CultureInfo.InvariantCulture)}", username, now, cancellationToken);
            reconciliation = new InventoryPurchaseReceiptReconciliation(reconciliationId, requisitionReconciliation.RequisitionId, requisitionReconciliation.RequisitionLineId, request.Quantity, username, now.ToString("O", CultureInfo.InvariantCulture));
        }
        await transaction.CommitAsync(cancellationToken);
        var ledgerEntry = new InventoryTransactionItem(transactionId, lot.LotId, item.ItemCode, item.Name, facility.Code, "purchase", request.Quantity,
            request.Notes.Trim(), username, now, null, null, receiptId, referenceNumber);
        return new InventoryPurchaseReceiptResponse(receiptId, vendor, facility.Code, facility.Name, referenceNumber, now.ToString("O", CultureInfo.InvariantCulture), username,
            request.Notes.Trim(), lot, ledgerEntry, itemQuantity, itemQuantity <= item.ReorderPoint, reconciliation);
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
        await EnsureGeneralInventoryLotAsync(connection, transaction, request.LotId, cancellationToken);
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

    private static async Task<InventoryPurchaseRequisition?> GetPurchaseRequisitionAsync(NpgsqlConnection connection, Guid requisitionId, CancellationToken cancellationToken)
    {
        await using var header = connection.CreateCommand();
        header.CommandText = "select r.requisition_id,r.facility_id,f.code,f.name,r.vendor_id,v.name,r.status,r.notes,r.requested_by,r.requested_at,r.submitted_by,r.submitted_at,r.decided_by,r.decided_at,r.decision_notes from inventory_purchase_requisitions r join facilities f on f.id=r.facility_id left join inventory_vendors v on v.vendor_id=r.vendor_id where r.requisition_id=@id;";
        header.Parameters.AddWithValue("id", requisitionId);
        await using var reader = await header.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var result = new InventoryPurchaseRequisition(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(14) ? null : reader.GetString(14), "pending", [], []);
        await reader.DisposeAsync();
        var lines = new List<InventoryPurchaseRequisitionLine>();
        await using (var lineCommand = connection.CreateCommand())
        {
            lineCommand.CommandText = "select l.requisition_line_id,l.item_id,i.item_code,i.name,l.requested_quantity,coalesce(sum(rr.received_quantity),0),i.unit from inventory_purchase_requisition_lines l join inventory_items i on i.item_id=l.item_id left join inventory_purchase_requisition_receipts rr on rr.requisition_line_id=l.requisition_line_id where l.requisition_id=@id group by l.requisition_line_id,l.item_id,i.item_code,i.name,l.requested_quantity,i.unit order by i.name,l.requisition_line_id;";
            lineCommand.Parameters.AddWithValue("id", requisitionId);
            await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await lineReader.ReadAsync(cancellationToken))
            {
                var requestedQuantity = lineReader.GetDecimal(4); var receivedQuantity = lineReader.GetDecimal(5);
                lines.Add(new(lineReader.GetGuid(0), lineReader.GetInt32(1), lineReader.GetString(2), lineReader.GetString(3), requestedQuantity, receivedQuantity, requestedQuantity - receivedQuantity, lineReader.GetString(6)));
            }
        }
        var events = new List<InventoryPurchaseRequisitionEvent>();
        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.CommandText = "select event_id,action,note,actor,occurred_at from inventory_purchase_requisition_events where requisition_id=@id order by occurred_at,event_id;";
            eventCommand.Parameters.AddWithValue("id", requisitionId);
            await using var eventReader = await eventCommand.ExecuteReaderAsync(cancellationToken);
            while (await eventReader.ReadAsync(cancellationToken)) events.Add(new(eventReader.GetGuid(0), eventReader.GetString(1), eventReader.IsDBNull(2) ? null : eventReader.GetString(2), eventReader.GetString(3), eventReader.GetFieldValue<DateTimeOffset>(4).ToString("O", CultureInfo.InvariantCulture)));
        }
        var receiptStatus = lines.All(line => line.OutstandingQuantity == 0) ? "complete" : lines.Any(line => line.ReceivedQuantity > 0) ? "partial" : "pending";
        return result with { ReceiptStatus = receiptStatus, Lines = lines, Events = events };
    }

    private sealed record ReceiptReconciliationContext(Guid RequisitionId, Guid RequisitionLineId);

    private static async Task<ReceiptReconciliationContext> GetReceiptReconciliationContextAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requisitionId, Guid vendorId, int facilityId, int itemId, decimal quantity, CancellationToken cancellationToken)
    {
        if (requisitionId == Guid.Empty) throw new ArgumentException("The purchase requisition is invalid.");
        Guid? requisitionVendorId; int requisitionFacilityId; string status;
        await using (var requisitionCommand = connection.CreateCommand())
        {
            requisitionCommand.Transaction = transaction;
            requisitionCommand.CommandText = "select vendor_id,facility_id,status from inventory_purchase_requisitions where requisition_id=@id for update;";
            requisitionCommand.Parameters.AddWithValue("id", requisitionId);
            await using var reader = await requisitionCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The selected purchase requisition was not found.");
            requisitionVendorId = reader.IsDBNull(0) ? null : reader.GetGuid(0); requisitionFacilityId = reader.GetInt32(1); status = reader.GetString(2);
        }
        if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Only approved purchase requisitions can be reconciled with a receipt.");
        if (requisitionFacilityId != facilityId) throw new ArgumentException("The receipt facility must match the purchase requisition.");
        if (requisitionVendorId is { } expectedVendorId && expectedVendorId != vendorId) throw new ArgumentException("The receipt vendor must match the purchase requisition vendor.");
        await using var lineCommand = connection.CreateCommand();
        lineCommand.Transaction = transaction;
        lineCommand.CommandText = "select l.requisition_line_id,l.requested_quantity,coalesce(sum(rr.received_quantity),0) from inventory_purchase_requisition_lines l left join inventory_purchase_requisition_receipts rr on rr.requisition_line_id=l.requisition_line_id where l.requisition_id=@requisitionId and l.item_id=@itemId group by l.requisition_line_id,l.requested_quantity;";
        lineCommand.Parameters.AddWithValue("requisitionId", requisitionId); lineCommand.Parameters.AddWithValue("itemId", itemId);
        await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken);
        if (!await lineReader.ReadAsync(cancellationToken)) throw new ArgumentException("The receipt item is not requested by the selected purchase requisition.");
        var outstandingQuantity = lineReader.GetDecimal(1) - lineReader.GetDecimal(2);
        if (quantity > outstandingQuantity) throw new ArgumentException("The receipt quantity exceeds the outstanding requisition quantity.");
        return new ReceiptReconciliationContext(requisitionId, lineReader.GetGuid(0));
    }

    private static async Task<bool> PurchaseRequisitionExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requisitionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from inventory_purchase_requisitions where requisition_id=@id);"; command.Parameters.AddWithValue("id", requisitionId);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task InsertPurchaseRequisitionEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requisitionId, string action, string? note, string actor, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "insert into inventory_purchase_requisition_events (event_id,requisition_id,action,note,actor,occurred_at) values (@id,@requisitionId,@action,@note,@actor,@at);";
        command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("requisitionId", requisitionId); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
        Guid? transferId,
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
        command.Parameters.AddWithValue("transfer_id", (object?)transferId ?? DBNull.Value);
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
              ml.rx_norm_code, mv.drug_name, mv.display_name, ml.linked_by, ml.linked_at,
              l.lot_id, f.code, f.name, l.lot_number, l.expiration_date, l.quantity_on_hand, l.unit_cost, l.status
            from inventory_items i
            left join inventory_item_medication_links ml on ml.item_id = i.item_id
            left join medication_vocabulary mv on mv.rx_norm_code = ml.rx_norm_code
            left join inventory_lots l on l.item_id = i.item_id and l.status = 'active'
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
                var medicationLink = reader.IsDBNull(7) ? null : new InventoryMedicationLink(
                    itemId, reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10),
                    reader.GetFieldValue<DateTimeOffset>(11).ToString("O", CultureInfo.InvariantCulture));
                builder = new InventoryItemBuilder(itemId, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), medicationLink);
                builders.Add(itemId, builder);
            }

            if (!reader.IsDBNull(12))
            {
                builder.Lots.Add(new InventoryLot(
                    reader.GetInt32(12), reader.GetString(13), reader.GetString(14), reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetFieldValue<DateOnly>(16).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    reader.GetDecimal(17), reader.GetDecimal(18), reader.GetString(19),
                    GetExpiryStatus(reader.IsDBNull(16) ? null : reader.GetFieldValue<DateOnly>(16), baseDate)));
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

    private static void ValidatePurchaseRequisition(InventoryPurchaseRequisitionCreateRequest request, string username)
    {
        if (request.FacilityId <= 0 || request.Notes?.Trim().Length > 500 || string.IsNullOrWhiteSpace(username)
            || request.Lines is null || request.Lines.Count is < 1 or > 25
            || request.Lines.Any(line => line.ItemId <= 0 || line.Quantity <= 0)
            || request.Lines.Select(line => line.ItemId).Distinct().Count() != request.Lines.Count)
        {
            throw new ArgumentException("A facility, one to 25 distinct active items with positive quantities, and valid requisition details are required.");
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

    private sealed class InventoryItemBuilder(int itemId, string itemCode, string name, string category, string unit, decimal reorderPoint, decimal preferredQuantity, InventoryMedicationLink? medicationLink)
    {
        public List<InventoryLot> Lots { get; } = [];

        public InventoryItem Build()
        {
            var quantity = Lots.Where(lot => string.Equals(lot.Status, "active", StringComparison.OrdinalIgnoreCase)).Sum(lot => lot.QuantityOnHand);
            return new InventoryItem(itemId, itemCode, name, category, unit, reorderPoint, preferredQuantity, quantity,
                Lots.Sum(lot => lot.QuantityOnHand * lot.UnitCost), quantity <= reorderPoint, medicationLink, Lots);
        }
    }
}
