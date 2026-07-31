// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryReplenishmentPolicyRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InventoryReplenishmentPolicyCatalogResponse> GetCatalogAsync(CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var policies = await GetActivePoliciesAsync(connection, null, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select request_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_replenishment_policy_change_requests order by updated_at desc,request_id desc limit 100;";
        var requests = new List<InventoryReplenishmentPolicyChangeRequest>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) requests.Add(ReadRequest(reader));
        return new(policies, requests);
    }

    public async Task<IReadOnlyList<InventoryReplenishmentRecommendation>> GetRecommendationsAsync(CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select p.policy_id,p.revision,p.item_id,i.item_code,i.name,i.unit,p.facility_id,f.code,f.name,
              coalesce(sum(l.quantity_on_hand),0),p.reorder_point,p.target_quantity,p.lead_time_days,p.safety_stock,
              p.preferred_vendor_id,v.name,p.pack_size,p.approval_threshold,p.effective_date,p.approval_reference
            from inventory_replenishment_policies p
            join inventory_items i on i.item_id=p.item_id and i.active=true
            join facilities f on f.id=p.facility_id
            left join inventory_lots l on l.item_id=p.item_id and l.facility_id=p.facility_id and l.status='active'
            left join inventory_vendors v on v.vendor_id=p.preferred_vendor_id
            where p.status='active' and p.effective_date <= current_date
            group by p.policy_id,p.revision,p.item_id,i.item_code,i.name,i.unit,p.facility_id,f.code,f.name,
              p.reorder_point,p.target_quantity,p.lead_time_days,p.safety_stock,p.preferred_vendor_id,v.name,
              p.pack_size,p.approval_threshold,p.effective_date,p.approval_reference
            having coalesce(sum(l.quantity_on_hand),0) <= p.reorder_point
            order by (greatest(p.target_quantity,p.reorder_point+p.safety_stock)-coalesce(sum(l.quantity_on_hand),0)) desc,
              i.item_code,f.code;
            """;
        var results = new List<InventoryReplenishmentRecommendation>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var onHand = reader.GetDecimal(9);
            var reorderPoint = reader.GetDecimal(10);
            var target = reader.GetDecimal(11);
            var safety = reader.GetDecimal(13);
            var packSize = reader.GetDecimal(16);
            var recommendationBase = decimal.Max(target, reorderPoint + safety);
            var shortfall = decimal.Max(0, recommendationBase - onHand);
            var recommended = shortfall == 0 ? 0 : decimal.Ceiling(shortfall / packSize) * packSize;
            results.Add(new(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetInt32(6), reader.GetString(7), reader.GetString(8), onHand, reorderPoint, target, reader.GetInt32(12), safety,
                reader.IsDBNull(14) ? null : reader.GetGuid(14), reader.IsDBNull(15) ? null : reader.GetString(15), packSize,
                reader.GetDecimal(17), recommended, reader.GetFieldValue<DateOnly>(18).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(19), false));
        }
        return results;
    }

    public async Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> CreateAsync(InventoryReplenishmentPolicyChangeRequestCreateRequest input, string username, CancellationToken token)
    {
        var definition = Normalize(input.ProposedDefinition);
        var reason = Required(input.Reason, "A policy rationale is required.", 1000);
        var requestId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await ValidateScopeAsync(connection, transaction, definition, token);
        var active = await GetActivePolicyAsync(connection, transaction, definition.ItemId, definition.FacilityId, token, true);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_replenishment_policy_change_requests(request_id,item_id,facility_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@item,@facility,@definition,@baselineId,@baselineRevision,@reason,'draft',0,@now,@user,@now,@user);";
            insert.Parameters.AddWithValue("id", requestId); insert.Parameters.AddWithValue("item", definition.ItemId); insert.Parameters.AddWithValue("facility", definition.FacilityId);
            insert.Parameters.AddWithValue("definition", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(definition, JsonOptions));
            insert.Parameters.AddWithValue("baselineId", (object?)active?.PolicyId ?? DBNull.Value); insert.Parameters.AddWithValue("baselineRevision", (object?)active?.Revision ?? DBNull.Value);
            insert.Parameters.AddWithValue("reason", reason); insert.Parameters.AddWithValue("now", now); insert.Parameters.AddWithValue("user", username);
            try { await insert.ExecuteNonQueryAsync(token); }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { throw new InventoryReplenishmentPolicyConflictException("An open replenishment-policy proposal already exists for this item and facility."); }
        }
        await WriteEventAsync(connection, transaction, requestId, "created", reason, username, token);
        await transaction.CommitAsync(token);
        return await GetDetailAsync(requestId, token);
    }

    public async Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> GetDetailAsync(Guid id, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var request = await GetRequestAsync(connection, null, id, token) ?? throw new ArgumentException("The replenishment-policy proposal was not found.");
        var active = await GetActivePolicyAsync(connection, null, request.ProposedDefinition.ItemId, request.ProposedDefinition.FacilityId, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select event_id,action,note,occurred_at,username from inventory_replenishment_policy_change_request_events where request_id=@id order by occurred_at desc,event_id desc;";
        command.Parameters.AddWithValue("id", id);
        var events = new List<InventoryReplenishmentPolicyChangeRequestEvent>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O", CultureInfo.InvariantCulture), reader.GetString(4)));
        return new(request, active, events);
    }

    public Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> SubmitAsync(Guid id, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft"], "submitted", input, false, user, token);
    public Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> ApproveAsync(Guid id, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "approved", input, false, user, token);
    public Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> RejectAsync(Guid id, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "rejected", input, true, user, token);
    public Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> CancelAsync(Guid id, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft", "submitted", "approved"], "cancelled", input, true, user, token);
    public Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> ActivateAsync(Guid id, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["approved"], "activated", input, false, user, token);

    private async Task<InventoryReplenishmentPolicyChangeRequestDetailResponse> TransitionAsync(Guid id, string[] allowed, string next, InventoryReplenishmentPolicyChangeRequestDecisionRequest input, bool noteRequired, string user, CancellationToken token)
    {
        var note = noteRequired ? Required(input.Note, "A decision note is required.", 1000) : Optional(input.Note, 1000);
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var request = await GetRequestAsync(connection, transaction, id, token, true) ?? throw new ArgumentException("The replenishment-policy proposal was not found.");
        if (!allowed.Contains(request.Status, StringComparer.Ordinal)) throw new InventoryReplenishmentPolicyConflictException($"The proposal is {request.Status}; it cannot move to {next}.");
        if (input.ExpectedVersion is not null && input.ExpectedVersion != request.Version) throw new InventoryReplenishmentPolicyConflictException($"The proposal changed after it was loaded. Current version is {request.Version}.");
        if (next == "activated")
        {
            var definition = request.ProposedDefinition;
            await ValidateScopeAsync(connection, transaction, definition, token);
            var active = await GetActivePolicyAsync(connection, transaction, definition.ItemId, definition.FacilityId, token, true);
            if (request.BaselinePolicyId != active?.PolicyId || request.BaselineRevision != active?.Revision) throw new InventoryReplenishmentPolicyConflictException("The active replenishment policy changed after this proposal was created.");
            if (active is not null)
            {
                await using var supersede = connection.CreateCommand(); supersede.Transaction = transaction;
                supersede.CommandText = "update inventory_replenishment_policies set status='superseded',superseded_at=now(),superseded_by=@user where policy_id=@id;";
                supersede.Parameters.AddWithValue("id", active.PolicyId); supersede.Parameters.AddWithValue("user", user); await supersede.ExecuteNonQueryAsync(token);
            }
            await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_replenishment_policies(policy_id,item_id,facility_id,reorder_point,target_quantity,lead_time_days,safety_stock,preferred_vendor_id,pack_size,approval_threshold,effective_date,approval_reference,rationale,revision,status,activated_at,activated_by) values(@id,@item,@facility,@reorder,@target,@lead,@safety,@vendor,@pack,@threshold,@effective,@approval,@rationale,@revision,'active',now(),@user);";
            insert.Parameters.AddWithValue("id", Guid.NewGuid()); insert.Parameters.AddWithValue("item", definition.ItemId); insert.Parameters.AddWithValue("facility", definition.FacilityId); insert.Parameters.AddWithValue("reorder", definition.ReorderPoint); insert.Parameters.AddWithValue("target", definition.TargetQuantity); insert.Parameters.AddWithValue("lead", definition.LeadTimeDays); insert.Parameters.AddWithValue("safety", definition.SafetyStock); insert.Parameters.AddWithValue("vendor", (object?)definition.PreferredVendorId ?? DBNull.Value); insert.Parameters.AddWithValue("pack", definition.PackSize); insert.Parameters.AddWithValue("threshold", definition.ApprovalThreshold); insert.Parameters.AddWithValue("effective", DateOnly.ParseExact(definition.EffectiveDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)); insert.Parameters.AddWithValue("approval", definition.ApprovalReference); insert.Parameters.AddWithValue("rationale", definition.Rationale); insert.Parameters.AddWithValue("revision", (active?.Revision ?? 0) + 1); insert.Parameters.AddWithValue("user", user); await insert.ExecuteNonQueryAsync(token);
        }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update inventory_replenishment_policy_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("user", user); update.Parameters.AddWithValue("id", id); await update.ExecuteNonQueryAsync(token); }
        await WriteEventAsync(connection, transaction, id, next, note, user, token);
        await transaction.CommitAsync(token);
        return await GetDetailAsync(id, token);
    }

    private static async Task ValidateScopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReplenishmentPolicyDefinition definition, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select exists(select 1 from inventory_items where item_id=@item and active=true) and exists(select 1 from facilities where id=@facility) and (@vendor is null or exists(select 1 from inventory_vendors where vendor_id=@vendor and active=true));";
        command.Parameters.AddWithValue("item", definition.ItemId); command.Parameters.AddWithValue("facility", definition.FacilityId);
        command.Parameters.Add(new NpgsqlParameter("vendor", NpgsqlDbType.Uuid) { Value = (object?)definition.PreferredVendorId ?? DBNull.Value });
        if (await command.ExecuteScalarAsync(token) is not true) throw new ArgumentException("The policy must reference an active item, facility, and (when selected) active preferred vendor.");
    }

    private static async Task WriteEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string action, string? note, string user, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into inventory_replenishment_policy_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }

    private static async Task<IReadOnlyList<InventoryReplenishmentPolicy>> GetActivePoliciesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select policy_id,item_id,facility_id,reorder_point,target_quantity,lead_time_days,safety_stock,preferred_vendor_id,pack_size,approval_threshold,effective_date,approval_reference,rationale,revision,status,activated_at,activated_by,superseded_at,superseded_by from inventory_replenishment_policies where status='active' order by item_id,facility_id;"; var values = new List<InventoryReplenishmentPolicy>(); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) values.Add(ReadPolicy(reader)); return values; }
    private static async Task<InventoryReplenishmentPolicy?> GetActivePolicyAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, int itemId, int facilityId, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select policy_id,item_id,facility_id,reorder_point,target_quantity,lead_time_days,safety_stock,preferred_vendor_id,pack_size,approval_threshold,effective_date,approval_reference,rationale,revision,status,activated_at,activated_by,superseded_at,superseded_by from inventory_replenishment_policies where item_id=@item and facility_id=@facility and status='active'" + (lockRow ? " for update" : string.Empty) + ";"; command.Parameters.AddWithValue("item", itemId); command.Parameters.AddWithValue("facility", facilityId); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadPolicy(reader) : null; }
    private static async Task<InventoryReplenishmentPolicyChangeRequest?> GetRequestAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid id, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select request_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_replenishment_policy_change_requests where request_id=@id" + (lockRow ? " for update" : string.Empty) + ";"; command.Parameters.AddWithValue("id", id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadRequest(reader) : null; }
    private static InventoryReplenishmentPolicy ReadPolicy(NpgsqlDataReader reader) => new(reader.GetGuid(0), new(reader.GetInt32(1), reader.GetInt32(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetDecimal(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetDecimal(8), reader.GetDecimal(9), reader.GetFieldValue<DateOnly>(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetString(11), reader.GetString(12)), reader.GetInt32(13), reader.GetString(14), reader.GetFieldValue<DateTimeOffset>(15).ToString("O", CultureInfo.InvariantCulture), reader.GetString(16), reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(18) ? null : reader.GetString(18));
    private static InventoryReplenishmentPolicyChangeRequest ReadRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), JsonSerializer.Deserialize<InventoryReplenishmentPolicyDefinition>(reader.GetString(1), JsonOptions) ?? throw new ArgumentException("The stored replenishment-policy definition is invalid."), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O", CultureInfo.InvariantCulture), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O", CultureInfo.InvariantCulture), reader.GetString(10));
    private static InventoryReplenishmentPolicyDefinition Normalize(InventoryReplenishmentPolicyDefinition input)
    { if (input.ItemId <= 0 || input.FacilityId <= 0) throw new ArgumentException("An item and facility are required."); if (input.ReorderPoint < 0 || input.TargetQuantity < 0 || input.SafetyStock < 0 || input.ApprovalThreshold < 0 || input.PackSize <= 0 || input.LeadTimeDays < 0) throw new ArgumentException("Reorder, target, safety, threshold, and pack inputs must be valid nonnegative values; pack size must be positive."); var effective = input.EffectiveDate?.Trim(); if (!DateOnly.TryParseExact(effective, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Effective date must use YYYY-MM-DD."); return input with { EffectiveDate = effective, ApprovalReference = Required(input.ApprovalReference, "Approval reference is required.", 160), Rationale = Required(input.Rationale, "Policy rationale is required.", 1000) }; }
    private static string Required(string? value, string error, int max) { var normalized = value?.Trim(); return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= max ? normalized : throw new ArgumentException(error); }
    private static string? Optional(string? value, int max) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) return null; return normalized.Length <= max ? normalized : throw new ArgumentException("Decision note is too long."); }
}
