// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryAccountingIntegrationRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InventoryAccountingIntegrationCatalogResponse> GetCatalogAsync(CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var active = await GetActiveAsync(connection, null, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select request_id,proposed_definition,baseline_decision_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_accounting_integration_change_requests order by updated_at desc,request_id desc limit 50;";
        var requests = new List<InventoryAccountingIntegrationChangeRequest>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) requests.Add(ReadRequest(reader));
        return new(active, requests);
    }

    public async Task<InventoryAccountingIntegrationChangeRequestDetailResponse> CreateAsync(InventoryAccountingIntegrationChangeRequestCreateRequest input, string username, CancellationToken token)
    {
        var definition = Normalize(input.ProposedDefinition); var reason = Required(input.Reason, "A decision rationale is required.", 1000);
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        var active = await GetActiveAsync(connection, transaction, token, true);
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction; existing.CommandText = "select exists(select 1 from inventory_accounting_integration_change_requests where status in ('draft','submitted','approved'));";
            if (await existing.ExecuteScalarAsync(token) is true) throw new InventoryAccountingIntegrationConflictException("An accounting-integration decision proposal is already open.");
        }
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_accounting_integration_change_requests(request_id,proposed_definition,baseline_decision_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@definition,@baselineId,@baselineRevision,@reason,'draft',0,@now,@user,@now,@user);";
            insert.Parameters.AddWithValue("id", id); insert.Parameters.AddWithValue("definition", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(definition, JsonOptions)); insert.Parameters.AddWithValue("baselineId", (object?)active?.DecisionId ?? DBNull.Value); insert.Parameters.AddWithValue("baselineRevision", (object?)active?.Revision ?? DBNull.Value); insert.Parameters.AddWithValue("reason", reason); insert.Parameters.AddWithValue("now", now); insert.Parameters.AddWithValue("user", username); await insert.ExecuteNonQueryAsync(token);
        }
        await WriteEventAsync(connection, transaction, id, "created", reason, username, token); await transaction.CommitAsync(token); return await GetDetailAsync(id, token);
    }

    public async Task<InventoryAccountingIntegrationChangeRequestDetailResponse> GetDetailAsync(Guid id, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var request = await GetRequestAsync(connection, null, id, token) ?? throw new ArgumentException("The accounting-integration decision proposal was not found.");
        var active = await GetActiveAsync(connection, null, token);
        await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from inventory_accounting_integration_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", id);
        var events = new List<InventoryAccountingIntegrationChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O", CultureInfo.InvariantCulture), reader.GetString(4)));
        return new(request, active, events);
    }

    public Task<InventoryAccountingIntegrationChangeRequestDetailResponse> SubmitAsync(Guid id, InventoryAccountingIntegrationChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft"], "submitted", input, false, user, token);
    public Task<InventoryAccountingIntegrationChangeRequestDetailResponse> ApproveAsync(Guid id, InventoryAccountingIntegrationChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "approved", input, false, user, token);
    public Task<InventoryAccountingIntegrationChangeRequestDetailResponse> RejectAsync(Guid id, InventoryAccountingIntegrationChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["submitted"], "rejected", input, true, user, token);
    public Task<InventoryAccountingIntegrationChangeRequestDetailResponse> CancelAsync(Guid id, InventoryAccountingIntegrationChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["draft", "submitted", "approved"], "cancelled", input, true, user, token);
    public Task<InventoryAccountingIntegrationChangeRequestDetailResponse> ActivateAsync(Guid id, InventoryAccountingIntegrationChangeRequestDecisionRequest input, string user, CancellationToken token) => TransitionAsync(id, ["approved"], "activated", input, false, user, token);

    private async Task<InventoryAccountingIntegrationChangeRequestDetailResponse> TransitionAsync(Guid id, string[] allowed, string next, InventoryAccountingIntegrationChangeRequestDecisionRequest input, bool noteRequired, string user, CancellationToken token)
    {
        var note = noteRequired ? Required(input.Note, "A decision note is required.", 1000) : Optional(input.Note, 1000);
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        var request = await GetRequestAsync(connection, transaction, id, token, true) ?? throw new ArgumentException("The accounting-integration decision proposal was not found.");
        if (!allowed.Contains(request.Status, StringComparer.Ordinal)) throw new InventoryAccountingIntegrationConflictException($"The proposal is {request.Status}; it cannot move to {next}.");
        if (input.ExpectedVersion is not null && input.ExpectedVersion != request.Version) throw new InventoryAccountingIntegrationConflictException($"The proposal changed after it was loaded. Current version is {request.Version}.");
        if (next == "activated")
        {
            var active = await GetActiveAsync(connection, transaction, token, true);
            if (request.BaselineDecisionId != active?.DecisionId || request.BaselineRevision != active?.Revision) throw new InventoryAccountingIntegrationConflictException("The active accounting-integration decision changed after this proposal was created.");
            if (active is not null)
            {
                await using var supersede = connection.CreateCommand(); supersede.Transaction = transaction; supersede.CommandText = "update inventory_accounting_integration_decisions set status='superseded',superseded_at=now(),superseded_by=@user where decision_id=@id;"; supersede.Parameters.AddWithValue("id", active.DecisionId); supersede.Parameters.AddWithValue("user", user); await supersede.ExecuteNonQueryAsync(token);
            }
            var definition = request.ProposedDefinition;
            await using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "insert into inventory_accounting_integration_decisions(decision_id,mode,finance_owner,effective_date,mapping_reference,reconciliation_reference,rationale,revision,status,activated_at,activated_by) values(@id,@mode,@owner,@effective,@mapping,@reconciliation,@rationale,@revision,'active',now(),@user);";
            insert.Parameters.AddWithValue("id", Guid.NewGuid()); insert.Parameters.AddWithValue("mode", definition.Mode); insert.Parameters.AddWithValue("owner", definition.FinanceOwner); insert.Parameters.AddWithValue("effective", DateOnly.ParseExact(definition.EffectiveDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)); insert.Parameters.AddWithValue("mapping", (object?)definition.MappingReference ?? DBNull.Value); insert.Parameters.AddWithValue("reconciliation", (object?)definition.ReconciliationReference ?? DBNull.Value); insert.Parameters.AddWithValue("rationale", definition.Rationale); insert.Parameters.AddWithValue("revision", (active?.Revision ?? 0) + 1); insert.Parameters.AddWithValue("user", user); await insert.ExecuteNonQueryAsync(token);
        }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update inventory_accounting_integration_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("user", user); update.Parameters.AddWithValue("id", id); await update.ExecuteNonQueryAsync(token); }
        await WriteEventAsync(connection, transaction, id, next, note, user, token); await transaction.CommitAsync(token); return await GetDetailAsync(id, token);
    }

    private static async Task WriteEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string action, string? note, string user, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into inventory_accounting_integration_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }
    private static async Task<InventoryAccountingIntegrationDecision?> GetActiveAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select decision_id,mode,finance_owner,effective_date,mapping_reference,reconciliation_reference,rationale,revision,status,activated_at,activated_by,superseded_at,superseded_by from inventory_accounting_integration_decisions where status='active'" + (lockRow ? " for update" : string.Empty) + ";"; await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadDecision(reader) : null; }
    private static async Task<InventoryAccountingIntegrationChangeRequest?> GetRequestAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid id, CancellationToken token, bool lockRow = false)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select request_id,proposed_definition,baseline_decision_id,baseline_revision,reason,status,version,created_at,created_by,updated_at,updated_by from inventory_accounting_integration_change_requests where request_id=@id" + (lockRow ? " for update" : string.Empty) + ";"; command.Parameters.AddWithValue("id", id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadRequest(reader) : null; }
    private static InventoryAccountingIntegrationDecision ReadDecision(NpgsqlDataReader reader) => new(reader.GetGuid(0), new(reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateOnly>(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6)), reader.GetInt32(7), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O", CultureInfo.InvariantCulture), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(12) ? null : reader.GetString(12));
    private static InventoryAccountingIntegrationChangeRequest ReadRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), JsonSerializer.Deserialize<InventoryAccountingIntegrationDecisionDefinition>(reader.GetString(1), JsonOptions) ?? throw new ArgumentException("The stored accounting-integration decision is invalid."), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O", CultureInfo.InvariantCulture), reader.GetString(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O", CultureInfo.InvariantCulture), reader.GetString(10));
    private static InventoryAccountingIntegrationDecisionDefinition Normalize(InventoryAccountingIntegrationDecisionDefinition input)
    { var mode = input.Mode?.Trim().ToLowerInvariant(); if (mode is not ("external" or "integration_accepted")) throw new ArgumentException("Select external or integration accepted."); var effective = input.EffectiveDate?.Trim(); if (!DateOnly.TryParseExact(effective, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Effective date must use YYYY-MM-DD."); var mapping = Optional(input.MappingReference, 500); var reconciliation = Optional(input.ReconciliationReference, 500); if (mode == "external") { mapping = null; reconciliation = null; } else if (mapping is null || reconciliation is null) throw new ArgumentException("Accepted integration requires both a mapping and reconciliation reference."); return new(mode, Required(input.FinanceOwner, "A finance owner is required.", 160), effective!, mapping, reconciliation, Required(input.Rationale, "Decision rationale is required.", 1000)); }
    private static string Required(string? value, string error, int max) { var normalized = value?.Trim(); return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= max ? normalized : throw new ArgumentException(error); }
    private static string? Optional(string? value, int max) { var normalized = value?.Trim(); if (string.IsNullOrWhiteSpace(normalized)) return null; return normalized.Length <= max ? normalized : throw new ArgumentException("Decision input is too long."); }
}
