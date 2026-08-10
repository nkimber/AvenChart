// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Data;

public sealed class AzureOperationsAccessRepository(NpgsqlDataSource dataSource)
{
    public async Task<AzureOperationsAccessCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select code_salt, code_hash, hash_iterations, code_version, requires_change, changed_at
            from azure_operations_access_config
            where config_id = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Azure Operations access-code configuration is unavailable.");
        return new(
            reader.GetFieldValue<byte[]>(0),
            reader.GetFieldValue<byte[]>(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetBoolean(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    public async Task<DateTimeOffset?> GetLockoutAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select locked_until
            from azure_operations_unlock_attempts
            where session_id = @sessionId and locked_until > now();
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : (DateTimeOffset)result;
    }

    public async Task<AzureOperationsUnlockFailure> RecordFailedUnlockAsync(
        Guid sessionId,
        string username,
        int maximumFailures,
        TimeSpan failureWindow,
        TimeSpan lockoutDuration,
        string? sourceIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var count = 0;
        var windowStartedAt = now;
        DateTimeOffset? lockedUntil = null;

        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select failure_count, window_started_at, locked_until
                from azure_operations_unlock_attempts
                where session_id = @sessionId
                for update;
                """;
            select.Parameters.AddWithValue("sessionId", sessionId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                count = reader.GetInt32(0);
                windowStartedAt = reader.GetFieldValue<DateTimeOffset>(1);
                lockedUntil = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
            }
        }

        if (lockedUntil is { } activeLockout && activeLockout > now)
        {
            await InsertAuditAsync(connection, transaction, "unlock_locked", username, sessionId, false,
                sourceIp, userAgent, "An unlock request was rejected during an active lockout.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(count, activeLockout);
        }

        if (now - windowStartedAt >= failureWindow)
        {
            count = 0;
            windowStartedAt = now;
            lockedUntil = null;
        }

        count++;
        if (count >= maximumFailures) lockedUntil = now.Add(lockoutDuration);

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                insert into azure_operations_unlock_attempts
                  (session_id, username, failure_count, window_started_at, locked_until, updated_at)
                values
                  (@sessionId, @username, @failureCount, @windowStartedAt, @lockedUntil, now())
                on conflict (session_id) do update
                set username = excluded.username,
                    failure_count = excluded.failure_count,
                    window_started_at = excluded.window_started_at,
                    locked_until = excluded.locked_until,
                    updated_at = now();
                """;
            upsert.Parameters.AddWithValue("sessionId", sessionId);
            upsert.Parameters.AddWithValue("username", username);
            upsert.Parameters.AddWithValue("failureCount", count);
            upsert.Parameters.AddWithValue("windowStartedAt", windowStartedAt);
            upsert.Parameters.AddWithValue("lockedUntil", (object?)lockedUntil ?? DBNull.Value);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuditAsync(connection, transaction, lockedUntil is null ? "unlock_failed" : "unlock_locked",
            username, sessionId, false, sourceIp, userAgent,
            lockedUntil is null ? "The supplied Operations access code was invalid." : "Unlock failures triggered a temporary lockout.",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(count, lockedUntil);
    }

    public async Task CreateGrantAsync(
        Guid grantId,
        byte[] tokenHash,
        Guid sessionId,
        string username,
        int codeVersion,
        DateTimeOffset expiresAt,
        string? sourceIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into azure_operations_access_grants
                  (grant_id, token_hash, session_id, username, code_version, expires_at)
                values
                  (@grantId, @tokenHash, @sessionId, @username, @codeVersion, @expiresAt);
                """;
            insert.Parameters.AddWithValue("grantId", grantId);
            insert.Parameters.Add("tokenHash", NpgsqlDbType.Bytea).Value = tokenHash;
            insert.Parameters.AddWithValue("sessionId", sessionId);
            insert.Parameters.AddWithValue("username", username);
            insert.Parameters.AddWithValue("codeVersion", codeVersion);
            insert.Parameters.AddWithValue("expiresAt", expiresAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var reset = connection.CreateCommand())
        {
            reset.Transaction = transaction;
            reset.CommandText = "delete from azure_operations_unlock_attempts where session_id = @sessionId;";
            reset.Parameters.AddWithValue("sessionId", sessionId);
            await reset.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertAuditAsync(connection, transaction, "unlock_succeeded", username, sessionId, true,
            sourceIp, userAgent, "A short-lived Operations access grant was issued.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AzureOperationsGrantValidation?> ValidateGrantAsync(
        byte[] tokenHash,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update azure_operations_access_grants access_grant
            set last_used_at = now()
            from azure_operations_access_config config,
                 auth_sessions session
            where access_grant.token_hash = @tokenHash
              and access_grant.session_id = @sessionId
              and access_grant.revoked_at is null
              and access_grant.expires_at > now()
              and access_grant.code_version = config.code_version
              and config.config_id = 1
              and session.id = access_grant.session_id
              and session.ended_at is null
              and session.expires_at > now()
            returning access_grant.grant_id, access_grant.expires_at, config.requires_change, config.code_version;
            """;
        command.Parameters.Add("tokenHash", NpgsqlDbType.Bytea).Value = tokenHash;
        command.Parameters.AddWithValue("sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetBoolean(2), reader.GetInt32(3))
            : null;
    }

    public async Task RevokeGrantAsync(
        byte[] tokenHash,
        Guid sessionId,
        string username,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update azure_operations_access_grants
            set revoked_at = coalesce(revoked_at, now()), revoke_reason = coalesce(revoke_reason, 'operator-lock')
            where token_hash = @tokenHash and session_id = @sessionId;
            insert into azure_operations_access_audit
              (event_type, username, session_id, success, detail)
            values
              ('grant_locked', @username, @sessionId, true, 'The operator locked the Operations workspace.');
            """;
        command.Parameters.Add("tokenHash", NpgsqlDbType.Bytea).Value = tokenHash;
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DateTimeOffset> ChangeCodeAsync(
        int expectedVersion,
        byte[] salt,
        byte[] hash,
        int iterations,
        Guid sessionId,
        string username,
        string? sourceIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        DateTimeOffset changedAt;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update azure_operations_access_config
                set code_salt = @salt,
                    code_hash = @hash,
                    hash_iterations = @iterations,
                    code_version = code_version + 1,
                    requires_change = false,
                    changed_by = @username,
                    changed_at = now()
                where config_id = 1 and code_version = @expectedVersion
                returning changed_at;
                """;
            update.Parameters.Add("salt", NpgsqlDbType.Bytea).Value = salt;
            update.Parameters.Add("hash", NpgsqlDbType.Bytea).Value = hash;
            update.Parameters.AddWithValue("iterations", iterations);
            update.Parameters.AddWithValue("username", username);
            update.Parameters.AddWithValue("expectedVersion", expectedVersion);
            var result = await update.ExecuteScalarAsync(cancellationToken);
            if (result is null) throw new AzureOperationsAccessConflictException("The Operations access code changed during this request. Unlock again.");
            changedAt = result switch
            {
                DateTimeOffset value => value,
                DateTime value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException("The Operations access-code change timestamp was invalid.")
            };
        }
        await using (var revoke = connection.CreateCommand())
        {
            revoke.Transaction = transaction;
            revoke.CommandText = """
                update azure_operations_access_grants
                set revoked_at = coalesce(revoked_at, now()), revoke_reason = coalesce(revoke_reason, 'code-changed')
                where revoked_at is null;
                delete from azure_operations_unlock_attempts;
                """;
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertAuditAsync(connection, transaction, "code_changed", username, sessionId, true,
            sourceIp, userAgent, "The Operations access code was changed and all grants were revoked.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changedAt;
    }

    public async Task RecordRejectedGrantAsync(
        Guid sessionId,
        string username,
        string? sourceIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into azure_operations_access_audit
              (event_type, username, session_id, success, source_ip, user_agent, detail)
            values
              ('grant_rejected', @username, @sessionId, false, @sourceIp, @userAgent,
               'A protected Operations request did not include a valid access grant.');
            """;
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("sourceIp", (object?)Limit(sourceIp, 255) ?? DBNull.Value);
        command.Parameters.AddWithValue("userAgent", (object?)Limit(userAgent, 512) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string eventType,
        string username,
        Guid sessionId,
        bool success,
        string? sourceIp,
        string? userAgent,
        string detail,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into azure_operations_access_audit
              (event_type, username, session_id, success, source_ip, user_agent, detail)
            values
              (@eventType, @username, @sessionId, @success, @sourceIp, @userAgent, @detail);
            """;
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("success", success);
        command.Parameters.AddWithValue("sourceIp", (object?)Limit(sourceIp, 255) ?? DBNull.Value);
        command.Parameters.AddWithValue("userAgent", (object?)Limit(userAgent, 512) ?? DBNull.Value);
        command.Parameters.AddWithValue("detail", detail);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Limit(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maximumLength)];
}

public sealed record AzureOperationsAccessCredential(
    byte[] Salt,
    byte[] Hash,
    int Iterations,
    int Version,
    bool RequiresChange,
    DateTimeOffset ChangedAt);

public sealed record AzureOperationsUnlockFailure(int FailureCount, DateTimeOffset? LockedUntil);

public sealed record AzureOperationsGrantValidation(
    Guid GrantId,
    DateTimeOffset ExpiresAt,
    bool RequiresCodeChange,
    int CodeVersion);

public sealed class AzureOperationsAccessConflictException(string message) : InvalidOperationException(message);
