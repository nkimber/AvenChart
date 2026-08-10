// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Infrastructure;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AzureOperationsRepository(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AzureDeploymentProfileSummary>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select profile_id, name, document, version, updated_by, updated_at
            from azure_deployment_profiles
            where archived_at is null
            order by lower(name), profile_id;
            """;
        var profiles = new List<AzureDeploymentProfileSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var document = DeserializeDocument(reader.GetString(2));
            var assessment = AzureDeploymentProfilePolicy.Assess(document);
            profiles.Add(new AzureDeploymentProfileSummary(
                reader.GetGuid(0), reader.GetString(1), document.EnvironmentKind, document.Location,
                document.ResourceGroupName, reader.GetInt32(3), reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5), assessment.DeploymentReady, assessment.Issues.Count));
        }
        return profiles;
    }

    public async Task<AzureDeploymentProfileDetail> GetProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select profile_id, name, document, version, created_by, created_at, updated_by, updated_at
            from azure_deployment_profiles
            where profile_id = @profileId and archived_at is null;
            """;
        command.Parameters.AddWithValue("profileId", profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Azure deployment profile was not found.");
        return ReadProfile(reader);
    }

    public async Task<AzureDeploymentProfileDetail> CreateProfileAsync(
        AzureDeploymentProfileCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var profileId = Guid.NewGuid();
        var documentJson = JsonSerializer.Serialize(request.Document, JsonOptions);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into azure_deployment_profiles
                      (profile_id, name, document, version, created_by, updated_by)
                    values (@profileId, @name, @document, 1, @username, @username);
                    """;
                insert.Parameters.AddWithValue("profileId", profileId);
                insert.Parameters.AddWithValue("name", name);
                insert.Parameters.Add("document", NpgsqlDbType.Jsonb).Value = documentJson;
                insert.Parameters.AddWithValue("username", username);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await InsertRevisionAsync(connection, transaction, profileId, 1, "created", documentJson, username, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new AzureDeploymentProfileConflictException("An active Azure deployment profile already uses that name.");
        }
        return await GetProfileAsync(profileId, cancellationToken);
    }

    public async Task<AzureDeploymentProfileDetail> UpdateProfileAsync(
        Guid profileId,
        AzureDeploymentProfileUpdateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        var documentJson = JsonSerializer.Serialize(request.Document, JsonOptions);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        int newVersion;
        try
        {
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                update azure_deployment_profiles
                set name = @name,
                    document = @document,
                    version = version + 1,
                    updated_by = @username,
                    updated_at = now()
                where profile_id = @profileId
                  and archived_at is null
                  and version = @expectedVersion
                returning version;
                """;
            update.Parameters.AddWithValue("profileId", profileId);
            update.Parameters.AddWithValue("name", name);
            update.Parameters.Add("document", NpgsqlDbType.Jsonb).Value = documentJson;
            update.Parameters.AddWithValue("username", username);
            update.Parameters.AddWithValue("expectedVersion", request.ExpectedVersion);
            var result = await update.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                throw new AzureDeploymentProfileConflictException("The deployment profile changed after it was loaded. Reload before saving.");
            }
            newVersion = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
            await InsertRevisionAsync(connection, transaction, profileId, newVersion, "updated", documentJson, username, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new AzureDeploymentProfileConflictException("An active Azure deployment profile already uses that name.");
        }
        return await GetProfileAsync(profileId, cancellationToken);
    }

    public async Task ArchiveProfileAsync(Guid profileId, int expectedVersion, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update azure_deployment_profiles
            set archived_at = now(), updated_at = now(), updated_by = @username, version = version + 1
            where profile_id = @profileId and version = @expectedVersion and archived_at is null
              and not exists (
                select 1 from azure_deployment_executions execution
                where execution.profile_id = @profileId
                  and execution.status in ('queued', 'running', 'cancelling'));
            """;
        command.Parameters.AddWithValue("profileId", profileId);
        command.Parameters.AddWithValue("expectedVersion", expectedVersion);
        command.Parameters.AddWithValue("username", username);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AzureDeploymentProfileConflictException("The profile changed, is already archived, or has an active operation.");
    }

    public async Task<AzureDeploymentProfileHistoryResponse> GetProfileHistoryAsync(Guid profileId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select revision_id, version, action, changed_by, changed_at
            from azure_deployment_profile_revisions
            where profile_id = @profileId
            order by version desc;
            """;
        command.Parameters.AddWithValue("profileId", profileId);
        var revisions = new List<AzureDeploymentProfileRevision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            revisions.Add(new(reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
        if (revisions.Count == 0) throw new ArgumentException("Azure deployment profile was not found.");
        return new(profileId, revisions);
    }

    public async Task<AzureDeploymentExecutionSummary> CreateExecutionAsync(
        Guid profileId,
        string kind,
        int expectedProfileVersion,
        string username,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileAsync(profileId, cancellationToken);
        if (profile.Version != expectedProfileVersion)
            throw new AzureDeploymentProfileConflictException("The deployment profile changed after it was reviewed. Reload it before continuing.");
        if (!profile.Assessment.DeploymentReady)
            throw new AzureDeploymentProfileValidationException(profile.Assessment);
        var executionId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                with queued as (
                  insert into azure_deployment_executions
                    (execution_id, profile_id, profile_version, execution_kind, status, phase,
                     requested_by, profile_snapshot)
                  select @executionId, profile.profile_id, profile.version, @kind, 'queued', 'queued',
                         @username, profile.document
                  from azure_deployment_profiles profile
                  where profile.profile_id = @profileId
                    and profile.version = @profileVersion
                    and profile.archived_at is null
                  returning execution_id
                )
                insert into azure_deployment_execution_events
                  (execution_id, level, phase, message)
                select execution_id, 'information', 'queued', @message
                from queued
                returning execution_id;
                """;
            command.Parameters.AddWithValue("executionId", executionId);
            command.Parameters.AddWithValue("profileId", profileId);
            command.Parameters.AddWithValue("profileVersion", profile.Version);
            command.Parameters.AddWithValue("kind", kind);
            command.Parameters.AddWithValue("username", username);
            command.Parameters.AddWithValue("message", $"{kind} operation queued by {username}.");
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
                throw new AzureDeploymentProfileConflictException(
                    "The deployment profile changed after it was reviewed. Reload it before continuing.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new AzureDeploymentProfileConflictException("This deployment profile already has an active operation.");
        }
        return (await GetExecutionAsync(executionId, cancellationToken)).Execution;
    }

    public async Task<AzureDeploymentExecutionWorkItem> GetExecutionWorkItemAsync(Guid executionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select execution_id, profile_id, profile_version, execution_kind, status, profile_snapshot
            from azure_deployment_executions where execution_id = @executionId;
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Azure deployment execution was not found.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), DeserializeDocument(reader.GetString(5)));
    }

    public async Task<AzureDeploymentExecutionDetail> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        AzureDeploymentExecutionSummary summary;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select execution_id, profile_id, profile_version, execution_kind, status, phase,
                       requested_by, requested_at, started_at, completed_at, summary, error,
                       application_url, azure_deployment_name, cancellation_requested_at is not null
                from azure_deployment_executions where execution_id = @executionId;
                """;
            command.Parameters.AddWithValue("executionId", executionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Azure deployment execution was not found.");
            summary = ReadExecution(reader);
        }
        var events = new List<AzureDeploymentExecutionEvent>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select event_id, level, phase, message, occurred_at
                from azure_deployment_execution_events
                where execution_id = @executionId order by event_id;
                """;
            command.Parameters.AddWithValue("executionId", executionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return new(summary, events);
    }

    public async Task<AzureDeploymentExecutionListResponse> GetExecutionsAsync(Guid? profileId, int limit, CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = "select count(*)::int from azure_deployment_executions where (@profileId is null or profile_id = @profileId);";
        count.Parameters.Add("profileId", NpgsqlDbType.Uuid).Value = (object?)profileId ?? DBNull.Value;
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select execution_id, profile_id, profile_version, execution_kind, status, phase,
                   requested_by, requested_at, started_at, completed_at, summary, error,
                   application_url, azure_deployment_name, cancellation_requested_at is not null
            from azure_deployment_executions
            where (@profileId is null or profile_id = @profileId)
            order by requested_at desc limit @limit;
            """;
        command.Parameters.Add("profileId", NpgsqlDbType.Uuid).Value = (object?)profileId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", safeLimit);
        var executions = new List<AzureDeploymentExecutionSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) executions.Add(ReadExecution(reader));
        return new(total, executions);
    }

    public async Task StartExecutionAsync(Guid executionId, string phase, CancellationToken cancellationToken)
    {
        await UpdateExecutionStateAsync(executionId, "running", phase, setStarted: true, null, null, null, null, cancellationToken);
    }

    public async Task SetExecutionPhaseAsync(Guid executionId, string phase, string message, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "update azure_deployment_executions set phase = @phase where execution_id = @executionId and status in ('running', 'cancelling');";
        command.Parameters.AddWithValue("phase", phase);
        command.Parameters.AddWithValue("executionId", executionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await AddExecutionEventAsync(executionId, "information", phase, message, cancellationToken);
    }

    public Task CompleteExecutionAsync(Guid executionId, string summary, string? applicationUrl, string? deploymentName, CancellationToken cancellationToken) =>
        UpdateExecutionStateAsync(executionId, "succeeded", "complete", false, summary, null, applicationUrl, deploymentName, cancellationToken);

    public Task FailExecutionAsync(Guid executionId, string phase, string error, CancellationToken cancellationToken) =>
        UpdateExecutionStateAsync(executionId, "failed", phase, false, null, error, null, null, cancellationToken);

    public Task MarkExecutionCancelledAsync(Guid executionId, string phase, CancellationToken cancellationToken) =>
        UpdateExecutionStateAsync(executionId, "cancelled", phase, false, "Operation cancelled by an administrator.", null, null, null, cancellationToken);

    public async Task RequestCancellationAsync(Guid executionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update azure_deployment_executions
            set status = 'cancelling', cancellation_requested_at = now()
            where execution_id = @executionId and status in ('queued', 'running');
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new AzureDeploymentProfileConflictException("The operation is no longer cancellable.");
        await AddExecutionEventAsync(executionId, "warning", "cancelling", "Cancellation requested. The current Azure operation may finish before cancellation takes effect.", cancellationToken);
    }

    public async Task AddExecutionEventAsync(Guid executionId, string level, string phase, string message, CancellationToken cancellationToken)
    {
        var safeMessage = message.Length <= 4000 ? message : message[..4000];
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "insert into azure_deployment_execution_events (execution_id, level, phase, message) values (@executionId, @level, @phase, @message);";
        command.Parameters.AddWithValue("executionId", executionId);
        command.Parameters.AddWithValue("level", level);
        command.Parameters.AddWithValue("phase", phase);
        command.Parameters.AddWithValue("message", safeMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> FailInterruptedExecutionsAsync(CancellationToken cancellationToken)
    {
        const string message = "The operator host restarted while this operation was active. Inspect Azure activity and deployment history before retrying.";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var executionIds = new List<Guid>();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update azure_deployment_executions
                set status = 'failed',
                    phase = 'operator-restarted',
                    completed_at = now(),
                    error = @message
                where status in ('queued', 'running', 'cancelling')
                returning execution_id;
                """;
            update.Parameters.AddWithValue("message", message);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) executionIds.Add(reader.GetGuid(0));
        }
        foreach (var executionId in executionIds)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into azure_deployment_execution_events (execution_id, level, phase, message)
                values (@executionId, 'error', 'operator-restarted', @message);
                """;
            insert.Parameters.AddWithValue("executionId", executionId);
            insert.Parameters.AddWithValue("message", message);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return executionIds.Count;
    }

    private async Task UpdateExecutionStateAsync(Guid executionId, string status, string phase, bool setStarted, string? summary, string? error, string? applicationUrl, string? deploymentName, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update azure_deployment_executions
            set status = @status,
                phase = @phase,
                started_at = case when @setStarted then coalesce(started_at, now()) else started_at end,
                completed_at = case when @status in ('succeeded', 'failed', 'cancelled') then now() else completed_at end,
                summary = coalesce(@summary, summary),
                error = coalesce(@error, error),
                application_url = coalesce(@applicationUrl, application_url),
                azure_deployment_name = coalesce(@deploymentName, azure_deployment_name)
            where execution_id = @executionId;
            """;
        command.Parameters.AddWithValue("executionId", executionId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("phase", phase);
        command.Parameters.AddWithValue("setStarted", setStarted);
        command.Parameters.Add("summary", NpgsqlDbType.Text).Value = (object?)summary ?? DBNull.Value;
        command.Parameters.Add("error", NpgsqlDbType.Text).Value = (object?)error ?? DBNull.Value;
        command.Parameters.Add("applicationUrl", NpgsqlDbType.Text).Value = (object?)applicationUrl ?? DBNull.Value;
        command.Parameters.Add("deploymentName", NpgsqlDbType.Text).Value = (object?)deploymentName ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
        var level = status == "failed" ? "error" : status == "cancelled" ? "warning" : "information";
        await AddExecutionEventAsync(executionId, level, phase, error ?? summary ?? $"Operation state changed to {status}.", cancellationToken);
    }

    private static async Task InsertRevisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid profileId, int version, string action, string snapshot, string username, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into azure_deployment_profile_revisions (profile_id, version, action, snapshot, changed_by) values (@profileId, @version, @action, @snapshot, @username);";
        command.Parameters.AddWithValue("profileId", profileId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("snapshot", NpgsqlDbType.Jsonb).Value = snapshot;
        command.Parameters.AddWithValue("username", username);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AzureDeploymentProfileDetail ReadProfile(NpgsqlDataReader reader)
    {
        var document = DeserializeDocument(reader.GetString(2));
        return new(reader.GetGuid(0), reader.GetString(1), document, reader.GetInt32(3), reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), AzureDeploymentProfilePolicy.Assess(document));
    }

    private static AzureDeploymentExecutionSummary ReadExecution(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
        reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9), reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13), reader.GetBoolean(14));

    private static AzureDeploymentProfileDocument DeserializeDocument(string json) =>
        JsonSerializer.Deserialize<AzureDeploymentProfileDocument>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored Azure deployment profile JSON is invalid.");

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 3 or > 120) throw new ArgumentException("Profile name must be between 3 and 120 characters.");
        return normalized;
    }
}

public sealed record AzureDeploymentExecutionWorkItem(
    Guid ExecutionId,
    Guid ProfileId,
    int ProfileVersion,
    string Kind,
    string Status,
    AzureDeploymentProfileDocument Document);

public sealed class AzureDeploymentProfileValidationException(AzureDeploymentProfileAssessment assessment)
    : Exception("The Azure deployment profile is not valid.")
{
    public AzureDeploymentProfileAssessment Assessment { get; } = assessment;
}

public sealed class AzureDeploymentProfileConflictException(string message) : Exception(message);
