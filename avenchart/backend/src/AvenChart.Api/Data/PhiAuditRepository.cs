// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Data;

public sealed class PhiAuditRepository(NpgsqlDataSource dataSource)
{
    public async Task RecordAccessDecisionAsync(
        AuthSessionResponse session,
        string httpMethod,
        string endpointName,
        string requiredPermission,
        bool authorized,
        int responseStatus,
        StaffAccessContext? accessContext,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into phi_access_audit_events (
              audit_id, occurred_at, username, session_id, http_method, endpoint_name,
              required_permission, facility_id, facility_code, purpose_of_use, authorized, response_status
            ) values (
              @audit_id, @occurred_at, @username, @session_id, @http_method, @endpoint_name,
              @required_permission, @facility_id, @facility_code, @purpose_of_use, @authorized, @response_status
            );
            """;
        command.Parameters.AddWithValue("audit_id", Guid.NewGuid());
        command.Parameters.AddWithValue("occurred_at", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("username", session.Username);
        command.Parameters.AddWithValue("session_id", (object?)session.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("http_method", httpMethod);
        command.Parameters.AddWithValue("endpoint_name", endpointName);
        command.Parameters.AddWithValue("required_permission", requiredPermission);
        command.Parameters.Add("facility_id", NpgsqlDbType.Integer).Value = (object?)accessContext?.FacilityId ?? DBNull.Value;
        command.Parameters.Add("facility_code", NpgsqlDbType.Text).Value = (object?)accessContext?.FacilityCode ?? DBNull.Value;
        command.Parameters.Add("purpose_of_use", NpgsqlDbType.Text).Value = (object?)accessContext?.PurposeOfUse ?? DBNull.Value;
        command.Parameters.AddWithValue("authorized", authorized);
        command.Parameters.AddWithValue("response_status", responseStatus);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PhiAccessAuditResponse> GetRecentAsync(int limit, string? username, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 200);
        if (from is not null && to is not null && from > to) throw new ArgumentException("Audit start date cannot be after its end date.");
        var normalizedUsername = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        if (normalizedUsername?.Length > 128) throw new ArgumentException("Audit username filter is too long.");
        var fromAt = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toAtExclusive = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select audit_id, occurred_at, username, http_method, endpoint_name, required_permission,
              facility_id, facility_code, purpose_of_use, authorized, response_status
            from phi_access_audit_events
            where (@username is null or username=@username)
              and (@from_at is null or occurred_at>=@from_at)
              and (@to_at_exclusive is null or occurred_at<@to_at_exclusive)
            order by occurred_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("limit", boundedLimit);
        command.Parameters.AddWithValue("username", NpgsqlDbType.Text, (object?)normalizedUsername ?? DBNull.Value);
        command.Parameters.AddWithValue("from_at", NpgsqlDbType.TimestampTz, (object?)fromAt ?? DBNull.Value);
        command.Parameters.AddWithValue("to_at_exclusive", NpgsqlDbType.TimestampTz, (object?)toAtExclusive ?? DBNull.Value);

        var events = new List<PhiAccessAuditEventItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PhiAccessAuditEventItem(
                reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetBoolean(9), reader.GetInt32(10)));
        }

        return new PhiAccessAuditResponse(
            TotalEvents: events.Count,
            AuthorizedEvents: events.Count(entry => entry.Authorized),
            DeniedEvents: events.Count(entry => !entry.Authorized),
            Events: events);
    }

    public async Task<string> GetCsvAsync(int limit, string? username, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var response = await GetRecentAsync(limit, username, from, to, cancellationToken);
        var csv = new System.Text.StringBuilder("Occurred At,Username,Method,Endpoint,Required Permission,Facility ID,Facility Code,Purpose Of Use,Decision,Response Status\n");
        foreach (var entry in response.Events)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                EscapeCsv(entry.OccurredAt.ToString("O")), EscapeCsv(entry.Username), EscapeCsv(entry.HttpMethod),
                EscapeCsv(entry.EndpointName), EscapeCsv(entry.RequiredPermission), EscapeCsv(entry.FacilityId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
                EscapeCsv(entry.FacilityCode ?? string.Empty), EscapeCsv(entry.PurposeOfUse ?? string.Empty), EscapeCsv(entry.Authorized ? "allowed" : "denied"),
                EscapeCsv(entry.ResponseStatus.ToString(System.Globalization.CultureInfo.InvariantCulture))
            }));
        }
        return csv.ToString();
    }

    private static string EscapeCsv(string value)
    {
        var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }
}
