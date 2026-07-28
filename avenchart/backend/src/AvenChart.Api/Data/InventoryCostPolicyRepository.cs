using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryCostPolicyRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InventoryCostPolicyCatalogResponse> GetCatalogAsync(CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var active = await GetActiveAsync(connection, null, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select request_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_cost_policy_change_requests order by updated_at desc,request_id desc limit 50;";
        var requests = new List<InventoryCostPolicyChangeRequest>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) requests.Add(ReadRequest(reader));
        return new(active, requests);
    }

    public async Task<InventoryCostPolicyChangeRequestDetailResponse> CreateAsync(InventoryCostPolicyChangeRequestCreateRequest input, string username, CancellationToken token)
    {
        var definition = Normalize(input.ProposedDefinition);
        var reason = Required(input.Reason, "A policy rationale is required.", 1000);
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        var active = await GetActiveAsync(connection, transaction, token, true);
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction; existing.CommandText = "select exists(select 1 from inventory_cost_policy_change_requests where status in ('draft','submitted','approved'));";
            if (await existing.ExecuteScalarAsync(token) is true) throw new InventoryCostPolicyChangeRequestConflictException("An inventory cost-policy proposal is already open.");
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_cost_policy_change_requests(request_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@definition,@baselineId,@baselineRevision,@reason,'draft',0,@now,@user,@now,@user);";
            insert.Parameters.AddWithValue("id", id); insert.Parameters.AddWithValue("definition", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(definition, JsonOptions)); insert.Parameters.AddWithValue("baselineId", (object?)active?.PolicyId ?? DBNull.Value); insert.Parameters.AddWithValue("baselineRevision", (object?)active?.Revision ?? DBNull.Value); insert.Parameters.AddWithValue("reason", reason); insert.Parameters.AddWithValue("now", now); insert.Parameters.AddWithValue("user", username); await insert.ExecuteNonQueryAsync(token);
        }
        await WriteEventAsync(connection, transaction, id, "created", reason, username, token); await transaction.CommitAsync(token);
        return await GetDetailAsync(id, token);
    }

    public async Task<InventoryCostPolicyChangeRequestDetailResponse> GetDetailAsync(Guid id, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var request = await GetRequestAsync(connection, null, id, token) ?? throw new ArgumentException("The inventory cost-policy proposal was not found.");
        var active = await GetActiveAsync(connection, null, token);
        await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from inventory_cost_policy_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", id);
        var events = new List<InventoryCostPolicyChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O", CultureInfo.InvariantCulture), reader.GetString(4)));
        return new(request, active, events);
    }

    public Task<InventoryCostPolicyChangeRequestDetailResponse> SubmitAsync(Guid id, InventoryCostPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft"], "submitted", input, false, user, token);
    public Task<InventoryCostPolicyChangeRequestDetailResponse> ApproveAsync(Guid id, InventoryCostPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "approved", input, false, user, token);
    public Task<InventoryCostPolicyChangeRequestDetailResponse> RejectAsync(Guid id, InventoryCostPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "rejected", input, true, user, token);
    public Task<InventoryCostPolicyChangeRequestDetailResponse> CancelAsync(Guid id, InventoryCostPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft", "submitted", "approved"], "cancelled", input, true, user, token);
    public Task<InventoryCostPolicyChangeRequestDetailResponse> ActivateAsync(Guid id, InventoryCostPolicyChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["approved"], "activated", input, false, user, token);

    private async Task<InventoryCostPolicyChangeRequestDetailResponse> TransitionAsync(Guid id, string[] allowed, string next, InventoryCostPolicyChangeRequestDecisionRequest input, bool noteRequired, string user, CancellationToken token)
    {
        var note = noteRequired ? Required(input.Note, "A decision note is required.", 1000) : Optional(input.Note, 1000);
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        var request = await GetRequestAsync(connection, transaction, id, token, true) ?? throw new ArgumentException("The inventory cost-policy proposal was not found.");
        if (!allowed.Contains(request.Status, StringComparer.Ordinal)) throw new InventoryCostPolicyChangeRequestConflictException($"The proposal is {request.Status}; it cannot move to {next}.");
        if (input.ExpectedVersion is not null && input.ExpectedVersion != request.Version) throw new InventoryCostPolicyChangeRequestConflictException($"The proposal changed after it was loaded. Current version is {request.Version}.");
        if (next == "activated")
        {
            var active = await GetActiveAsync(connection, transaction, token, true);
            if (request.BaselinePolicyId != active?.PolicyId || request.BaselineRevision != active?.Revision) throw new InventoryCostPolicyChangeRequestConflictException("The active policy changed after this proposal was created.");
            if (active is not null)
            {
                await using var supersede = connection.CreateCommand(); supersede.Transaction = transaction; supersede.CommandText = "update inventory_cost_policies set status='superseded',superseded_at=now(),superseded_by=@user where policy_id=@id;"; supersede.Parameters.AddWithValue("id", active.PolicyId); supersede.Parameters.AddWithValue("user", user); await supersede.ExecuteNonQueryAsync(token);
            }
            await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_cost_policies(policy_id,scope_type,method,currency,tax_treatment,freight_treatment,landed_cost_treatment,rounding_rule,backdated_entry_rule,effective_date,approval_reference,rationale,revision,status,activated_at,activated_by) values(@id,'organization',@method,@currency,@tax,@freight,@landed,@rounding,@backdated,@effective,@approval,@rationale,@revision,'active',now(),@user);";
            insert.Parameters.AddWithValue("id", Guid.NewGuid()); insert.Parameters.AddWithValue("method", request.ProposedDefinition.Method); insert.Parameters.AddWithValue("currency", request.ProposedDefinition.Currency); insert.Parameters.AddWithValue("tax", request.ProposedDefinition.TaxTreatment); insert.Parameters.AddWithValue("freight", request.ProposedDefinition.FreightTreatment); insert.Parameters.AddWithValue("landed", request.ProposedDefinition.LandedCostTreatment); insert.Parameters.AddWithValue("rounding", request.ProposedDefinition.RoundingRule); insert.Parameters.AddWithValue("backdated", request.ProposedDefinition.BackdatedEntryRule); insert.Parameters.AddWithValue("effective", DateOnly.ParseExact(request.ProposedDefinition.EffectiveDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)); insert.Parameters.AddWithValue("approval", request.ProposedDefinition.ApprovalReference); insert.Parameters.AddWithValue("rationale", request.ProposedDefinition.Rationale); insert.Parameters.AddWithValue("revision", (active?.Revision ?? 0) + 1); insert.Parameters.AddWithValue("user", user); await insert.ExecuteNonQueryAsync(token);
        }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update inventory_cost_policy_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("user", user); update.Parameters.AddWithValue("id", id); await update.ExecuteNonQueryAsync(token); }
        await WriteEventAsync(connection, transaction, id, next, note, user, token); await transaction.CommitAsync(token); return await GetDetailAsync(id, token);
    }

    private static async Task WriteEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string action, string? note, string user, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into inventory_cost_policy_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }

    private static async Task<InventoryCostPolicy?> GetActiveAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select policy_id,scope_type,method,currency,tax_treatment,freight_treatment,landed_cost_treatment,rounding_rule,backdated_entry_rule,effective_date,approval_reference,rationale,revision,status,activated_at,activated_by,superseded_at,superseded_by from inventory_cost_policies where status='active'" + (lockRow ? " for update" : string.Empty) + ";"; await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadPolicy(reader) : null; }
    private static async Task<InventoryCostPolicyChangeRequest?> GetRequestAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid id, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select request_id,proposed_definition,baseline_policy_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_cost_policy_change_requests where request_id=@id" + (lockRow ? " for update" : string.Empty) + ";"; command.Parameters.AddWithValue("id", id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadRequest(reader) : null; }
    private static InventoryCostPolicy ReadPolicy(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), new(reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetFieldValue<DateOnly>(9).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetString(10), reader.GetString(11)), reader.GetInt32(12), reader.GetString(13), reader.GetFieldValue<DateTimeOffset>(14).ToString("O", CultureInfo.InvariantCulture), reader.GetString(15), reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(17) ? null : reader.GetString(17));
    private static InventoryCostPolicyChangeRequest ReadRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), JsonSerializer.Deserialize<InventoryCostPolicyDefinition>(reader.GetString(1), JsonOptions) ?? throw new ArgumentException("The stored cost-policy definition is invalid."), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O", CultureInfo.InvariantCulture), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O", CultureInfo.InvariantCulture), reader.GetString(10));
    private static InventoryCostPolicyDefinition Normalize(InventoryCostPolicyDefinition input)
    { var method = input.Method?.Trim().ToLowerInvariant(); if (method is not ("fifo" or "weighted_average" or "specific_identification" or "practice_specific")) throw new ArgumentException("Select FIFO, weighted average, specific identification, or a documented practice-specific method."); var currency = input.Currency?.Trim().ToUpperInvariant(); if (currency is null || currency.Length != 3 || !currency.All(char.IsAsciiLetter)) throw new ArgumentException("Currency must be a three-letter ISO code."); var effective = input.EffectiveDate?.Trim(); if (!DateOnly.TryParseExact(effective, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Effective date must use YYYY-MM-DD."); return new(method, currency, Required(input.TaxTreatment, "Tax treatment is required.", 160), Required(input.FreightTreatment, "Freight treatment is required.", 160), Required(input.LandedCostTreatment, "Landed-cost treatment is required.", 160), Choice(input.RoundingRule, ["half_up", "half_even", "truncate"], "rounding rule"), Choice(input.BackdatedEntryRule, ["prohibited", "restatement"], "backdated-entry rule"), effective, Required(input.ApprovalReference, "Approval reference is required.", 160), Required(input.Rationale, "Policy rationale is required.", 1000)); }
    private static string Choice(string? value, string[] choices, string label) { var normalized = value?.Trim().ToLowerInvariant(); return choices.Contains(normalized) ? normalized! : throw new ArgumentException($"Select a supported {label}."); }
    private static string Required(string? value, string error, int max) { var normalized = value?.Trim(); return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= max ? normalized : throw new ArgumentException(error); }
    private static string? Optional(string? value, int max) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) return null; return normalized.Length <= max ? normalized : throw new ArgumentException("Decision note is too long."); }
}
