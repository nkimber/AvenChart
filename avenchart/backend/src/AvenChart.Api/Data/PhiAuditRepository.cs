using Npgsql;
using AvenChart.Api.Models;

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
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into phi_access_audit_events (
              audit_id, occurred_at, username, session_id, http_method, endpoint_name,
              required_permission, authorized, response_status
            ) values (
              @audit_id, @occurred_at, @username, @session_id, @http_method, @endpoint_name,
              @required_permission, @authorized, @response_status
            );
            """;
        command.Parameters.AddWithValue("audit_id", Guid.NewGuid());
        command.Parameters.AddWithValue("occurred_at", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("username", session.Username);
        command.Parameters.AddWithValue("session_id", (object?)session.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("http_method", httpMethod);
        command.Parameters.AddWithValue("endpoint_name", endpointName);
        command.Parameters.AddWithValue("required_permission", requiredPermission);
        command.Parameters.AddWithValue("authorized", authorized);
        command.Parameters.AddWithValue("response_status", responseStatus);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PhiAccessAuditResponse> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select audit_id, occurred_at, username, http_method, endpoint_name, required_permission,
              authorized, response_status
            from phi_access_audit_events
            order by occurred_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("limit", boundedLimit);

        var events = new List<PhiAccessAuditEventItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PhiAccessAuditEventItem(
                reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetBoolean(6), reader.GetInt32(7)));
        }

        return new PhiAccessAuditResponse(
            TotalEvents: events.Count,
            AuthorizedEvents: events.Count(entry => entry.Authorized),
            DeniedEvents: events.Count(entry => !entry.Authorized),
            Events: events);
    }
}
