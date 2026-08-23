// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class InventoryValuationRepository(NpgsqlDataSource dataSource)
{
    private const string CalculationVersion = "receipt-layer-as-of-v1";

    public async Task<InventoryValuationRunDetailResponse> CreateAsync(InventoryValuationRunCreateRequest input, string username, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("An authenticated user is required.");
        if (!DateTimeOffset.TryParse(input.AsOfAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var asOf))
            throw new ArgumentException("As-of time must be an ISO 8601 timestamp with an offset.");
        asOf = asOf.ToUniversalTime();
        var now = DateTimeOffset.UtcNow;
        if (asOf > now) throw new ArgumentException("An inventory valuation cannot be run for a future time.");
        if (input.FacilityId is <= 0) throw new ArgumentException("Facility must be a positive identifier when supplied.");

        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        if (input.FacilityId is { } facilityId && !await FacilityExistsAsync(connection, transaction, facilityId, token))
            throw new ArgumentException("The requested facility was not found.");

        var policy = await GetPolicyForAsOfAsync(connection, transaction, asOf, token)
            ?? throw new InventoryValuationPolicyMissingException("No approved inventory cost policy was effective at the requested as-of time. Valuation was not run.");
        var lines = await GetLinesAsync(connection, transaction, policy.PolicyId, policy.Revision, asOf, input.FacilityId, token);
        var exceptionCount = await GetExceptionCountAsync(connection, transaction, asOf, input.FacilityId, token);
        var unvaluedLayerCount = await GetUnvaluedLayerCountAsync(connection, transaction, asOf, input.FacilityId, token);
        var quantityTotal = lines.Sum(line => Math.Max(line.RemainingQuantity, 0m));
        var valueTotal = lines.Sum(line => line.ValueTotal);
        var applicationCount = lines.Sum(line => line.ApplicationCount);
        var status = exceptionCount > 0 || unvaluedLayerCount > 0 ? "completed_with_exceptions" : "completed";
        var runId = Guid.NewGuid();
        var checksum = Checksum(policy, asOf, input.FacilityId, lines, exceptionCount, unvaluedLayerCount);

        await InsertRunAsync(connection, transaction, new InventoryValuationRun(runId, now.ToString("O", CultureInfo.InvariantCulture), username, asOf.ToString("O", CultureInfo.InvariantCulture), input.FacilityId, policy.PolicyId, policy.Revision, policy.Method, policy.Currency, policy.RoundingRule, status, lines.Count, applicationCount, exceptionCount, unvaluedLayerCount, quantityTotal, valueTotal, CalculationVersion, checksum, now.ToString("O", CultureInfo.InvariantCulture)), token);
        foreach (var line in lines) await InsertLineAsync(connection, transaction, runId, line, token);
        await transaction.CommitAsync(token);
        return new(await GetRunAsync(runId, token) ?? throw new InvalidOperationException("The valuation run was not persisted."), lines);
    }

    public async Task<IReadOnlyList<InventoryValuationRun>> GetRunsAsync(int limit, CancellationToken token)
    {
        if (limit is < 1 or > 100) throw new ArgumentException("Limit must be between 1 and 100.");
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "select run_id,requested_at,requested_by,as_of_at,facility_id,policy_id,policy_revision,method,currency,rounding_rule,status,layer_count,application_count,exception_count,unvalued_layer_count,quantity_total,value_total,calculation_version,result_checksum,completed_at from inventory_valuation_runs order by as_of_at desc,run_id desc limit @limit;";
        command.Parameters.AddWithValue("limit", limit);
        var result = new List<InventoryValuationRun>(); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadRun(reader));
        return result;
    }

    public async Task<InventoryValuationRunDetailResponse?> GetDetailAsync(Guid runId, CancellationToken token)
    {
        var run = await GetRunAsync(runId, token); if (run is null) return null;
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var command = connection.CreateCommand();
        command.CommandText = "select layer_id,lot_id,item_id,facility_id,received_quantity,remaining_quantity,unit_cost,value_total,application_count from inventory_valuation_run_lines where run_id=@id order by layer_id;"; command.Parameters.AddWithValue("id", runId);
        var lines = new List<InventoryValuationRunLine>(); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) lines.Add(ReadLine(reader));
        return new(run, lines);
    }

    private async Task<InventoryValuationRun?> GetRunAsync(Guid runId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var command = connection.CreateCommand();
        command.CommandText = "select run_id,requested_at,requested_by,as_of_at,facility_id,policy_id,policy_revision,method,currency,rounding_rule,status,layer_count,application_count,exception_count,unvalued_layer_count,quantity_total,value_total,calculation_version,result_checksum,completed_at from inventory_valuation_runs where run_id=@id;"; command.Parameters.AddWithValue("id", runId);
        await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadRun(reader) : null;
    }

    private static async Task<bool> FacilityExistsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int facilityId, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select exists(select 1 from facilities where id=@id);"; command.Parameters.AddWithValue("id", facilityId); return await command.ExecuteScalarAsync(token) is true; }

    private static async Task<PolicySnapshot?> GetPolicyForAsOfAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset asOf, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "select policy_id,revision,method,currency,rounding_rule from inventory_cost_policies where activated_at <= @asOf and effective_date <= @asOfDate and (superseded_at is null or superseded_at > @asOf) order by activated_at desc limit 1;";
        command.Parameters.AddWithValue("asOf", asOf); command.Parameters.AddWithValue("asOfDate", DateOnly.FromDateTime(asOf.UtcDateTime));
        await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)) : null;
    }

    private static async Task<List<InventoryValuationRunLine>> GetLinesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid policyId, int revision, DateTimeOffset asOf, int? facilityId, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            select l.layer_id,l.lot_id,l.item_id,l.facility_id,l.received_quantity,
              l.received_quantity + coalesce(sum(a.quantity) filter (where a.applied_at <= @asOf),0) as remaining_quantity,
              l.unit_cost,
              (l.received_quantity + coalesce(sum(a.quantity) filter (where a.applied_at <= @asOf),0)) * l.unit_cost as value_total,
              (count(a.application_id) filter (where a.applied_at <= @asOf))::integer as application_count
            from inventory_cost_layers l
            left join inventory_cost_layer_applications a on a.layer_id=l.layer_id
            where l.policy_id=@policyId and l.policy_revision=@revision and l.created_at <= @asOf
              and (@facilityId is null or l.facility_id=@facilityId)
            group by l.layer_id,l.lot_id,l.item_id,l.facility_id,l.received_quantity,l.unit_cost
            order by l.layer_id;
            """;
        command.Parameters.AddWithValue("policyId", policyId); command.Parameters.AddWithValue("revision", revision); command.Parameters.AddWithValue("asOf", asOf); command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = (object?)facilityId ?? DBNull.Value;
        var result = new List<InventoryValuationRunLine>(); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(ReadLine(reader));
        return result;
    }

    private static async Task<int> GetExceptionCountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset asOf, int? facilityId, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select count(*) from inventory_costing_exceptions e join inventory_lots l on l.lot_id=e.lot_id where e.created_at <= @asOf and (@facilityId is null or l.facility_id=@facilityId);"; command.Parameters.AddWithValue("asOf", asOf); command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = (object?)facilityId ?? DBNull.Value; return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture); }

    private static async Task<int> GetUnvaluedLayerCountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset asOf, int? facilityId, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select count(*) from inventory_cost_layers where created_at <= @asOf and status='pending_policy' and (@facilityId is null or facility_id=@facilityId);"; command.Parameters.AddWithValue("asOf", asOf); command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = (object?)facilityId ?? DBNull.Value; return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture); }

    private static async Task InsertRunAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryValuationRun run, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into inventory_valuation_runs(run_id,requested_at,requested_by,as_of_at,facility_id,policy_id,policy_revision,method,currency,rounding_rule,status,layer_count,application_count,exception_count,unvalued_layer_count,quantity_total,value_total,calculation_version,result_checksum,completed_at) values(@id,@requestedAt,@requestedBy,@asOf,@facility,@policy,@revision,@method,@currency,@rounding,@status,@layers,@applications,@exceptions,@unvalued,@quantity,@value,@version,@checksum,@completed);"; command.Parameters.AddWithValue("id", run.RunId); command.Parameters.AddWithValue("requestedAt", DateTimeOffset.Parse(run.RequestedAt, CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("requestedBy", run.RequestedBy); command.Parameters.AddWithValue("asOf", DateTimeOffset.Parse(run.AsOfAt, CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("facility", (object?)run.FacilityId ?? DBNull.Value); command.Parameters.AddWithValue("policy", run.PolicyId); command.Parameters.AddWithValue("revision", run.PolicyRevision); command.Parameters.AddWithValue("method", run.Method); command.Parameters.AddWithValue("currency", run.Currency); command.Parameters.AddWithValue("rounding", run.RoundingRule); command.Parameters.AddWithValue("status", run.Status); command.Parameters.AddWithValue("layers", run.LayerCount); command.Parameters.AddWithValue("applications", run.ApplicationCount); command.Parameters.AddWithValue("exceptions", run.ExceptionCount); command.Parameters.AddWithValue("unvalued", run.UnvaluedLayerCount); command.Parameters.AddWithValue("quantity", run.QuantityTotal); command.Parameters.AddWithValue("value", run.ValueTotal); command.Parameters.AddWithValue("version", run.CalculationVersion); command.Parameters.AddWithValue("checksum", run.ResultChecksum); command.Parameters.AddWithValue("completed", DateTimeOffset.Parse(run.CompletedAt, CultureInfo.InvariantCulture)); await command.ExecuteNonQueryAsync(token); }

    private static async Task InsertLineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, InventoryValuationRunLine line, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into inventory_valuation_run_lines(run_id,layer_id,lot_id,item_id,facility_id,received_quantity,remaining_quantity,unit_cost,value_total,application_count) values(@run,@layer,@lot,@item,@facility,@received,@remaining,@unitCost,@value,@applications);"; command.Parameters.AddWithValue("run", runId); command.Parameters.AddWithValue("layer", line.LayerId); command.Parameters.AddWithValue("lot", line.LotId); command.Parameters.AddWithValue("item", line.ItemId); command.Parameters.AddWithValue("facility", line.FacilityId); command.Parameters.AddWithValue("received", line.ReceivedQuantity); command.Parameters.AddWithValue("remaining", line.RemainingQuantity); command.Parameters.AddWithValue("unitCost", line.UnitCost); command.Parameters.AddWithValue("value", line.ValueTotal); command.Parameters.AddWithValue("applications", line.ApplicationCount); await command.ExecuteNonQueryAsync(token); }

    private static string Checksum(PolicySnapshot policy, DateTimeOffset asOf, int? facilityId, IReadOnlyList<InventoryValuationRunLine> lines, int exceptionCount, int unvaluedLayerCount)
    {
        var material = new StringBuilder($"{CalculationVersion}|{policy.PolicyId:D}|{policy.Revision}|{policy.Method}|{policy.Currency}|{policy.RoundingRule}|{asOf:O}|{facilityId?.ToString(CultureInfo.InvariantCulture) ?? "all"}|{exceptionCount}|{unvaluedLayerCount}");
        foreach (var line in lines) material.Append($"\n{line.LayerId:D}|{line.LotId}|{line.ItemId}|{line.FacilityId}|{line.ReceivedQuantity.ToString("0.00", CultureInfo.InvariantCulture)}|{line.RemainingQuantity.ToString("0.00", CultureInfo.InvariantCulture)}|{line.UnitCost.ToString("0.0000", CultureInfo.InvariantCulture)}|{line.ValueTotal.ToString("0.0000", CultureInfo.InvariantCulture)}|{line.ApplicationCount}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())));
    }

    private static InventoryValuationRun ReadRun(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1).ToString("O", CultureInfo.InvariantCulture), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O", CultureInfo.InvariantCulture), reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.GetGuid(5), reader.GetInt32(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13), reader.GetInt32(14), reader.GetDecimal(15), reader.GetDecimal(16), reader.GetString(17), reader.GetString(18), reader.GetFieldValue<DateTimeOffset>(19).ToString("O", CultureInfo.InvariantCulture));
    private static InventoryValuationRunLine ReadLine(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetInt32(8));
    private sealed record PolicySnapshot(Guid PolicyId, int Revision, string Method, string Currency, string RoundingRule);
}
