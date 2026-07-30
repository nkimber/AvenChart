using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class ReferralRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<ReferralItem>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, patient_id, encounter_id, destination, reason, status, external_reference, notes, requested_at, created_at, updated_at
            from referrals where lower(patient_id) = lower(@patientId) order by requested_at desc, created_at desc;
            """;
        command.Parameters.AddWithValue("patientId", await ResolvePatientIdAsync(connection, patientId, cancellationToken));
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<ReferralItem> CreateAsync(string patientId, ReferralCreateRequest request, CancellationToken cancellationToken)
    {
        var destination = request.Destination?.Trim(); var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(destination) || destination.Length > 240) throw new ArgumentException("Referral destination is required and must be 240 characters or fewer.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new ArgumentException("Referral reason is required and must be 1000 characters or fewer.");
        if (!TryParseRequestedAt(request.RequestedAt, out var requestedAt)) throw new ArgumentException("Requested date must be a valid ISO date or date-time.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var canonicalId = await ResolvePatientIdAsync(connection, patientId, cancellationToken);
        if (request.EncounterId is not null)
        {
            await using var encounterCommand = connection.CreateCommand(); encounterCommand.CommandText = "select exists(select 1 from encounters where encounter = @encounterId and lower(patient_id) = lower(@patientId));";
            encounterCommand.Parameters.AddWithValue("encounterId", request.EncounterId.Value); encounterCommand.Parameters.AddWithValue("patientId", canonicalId);
            if (!(bool)(await encounterCommand.ExecuteScalarAsync(cancellationToken) ?? false)) throw new ArgumentException("Referral encounter does not belong to this patient.");
            if (await IsEncounterLockedAsync(connection, request.EncounterId.Value, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }
        }
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into referrals (id, patient_id, encounter_id, destination, reason, status, external_reference, notes, requested_at, created_at, updated_at)
            values (@id, @patientId, @encounterId, @destination, @reason, 'draft', @externalReference, @notes, @requestedAt, @now, @now)
            returning id, patient_id, encounter_id, destination, reason, status, external_reference, notes, requested_at, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("patientId", canonicalId); command.Parameters.AddWithValue("encounterId", (object?)request.EncounterId ?? DBNull.Value); command.Parameters.AddWithValue("destination", destination); command.Parameters.AddWithValue("reason", reason); command.Parameters.AddWithValue("externalReference", (object?)TrimToNull(request.ExternalReference) ?? DBNull.Value); command.Parameters.AddWithValue("notes", (object?)TrimToNull(request.Notes) ?? DBNull.Value); command.Parameters.AddWithValue("requestedAt", requestedAt); command.Parameters.AddWithValue("now", now);
        var results = await ReadAsync(command, cancellationToken); return results.Single();
    }

    public async Task<ReferralItem> UpdateStatusAsync(string patientId, Guid referralId, ReferralStatusRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("sent" or "received" or "closed" or "cancelled")) throw new ArgumentException("Referral status must be sent, received, closed, or cancelled.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var canonicalId = await ResolvePatientIdAsync(connection, patientId, cancellationToken);
        var encounter = await GetEncounterIdAsync(connection, canonicalId, referralId, cancellationToken);
        if (encounter is not null && await IsEncounterLockedAsync(connection, encounter.Value, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update referrals set status = @status, updated_at = @now
            where id = @id and patient_id = @patientId
              and ((status = 'draft' and @status in ('sent', 'cancelled')) or (status = 'sent' and @status in ('received', 'cancelled')) or (status = 'received' and @status = 'closed'))
            returning id, patient_id, encounter_id, destination, reason, status, external_reference, notes, requested_at, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("status", status); command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow); command.Parameters.AddWithValue("id", referralId); command.Parameters.AddWithValue("patientId", canonicalId);
        var results = await ReadAsync(command, cancellationToken); if (results.Count == 0) throw new ArgumentException("Referral was not found or cannot make that status transition."); return results.Single();
    }

    private static async Task<string> ResolvePatientIdAsync(NpgsqlConnection connection, string patientId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "select canonical_id from patients where lower(canonical_id) = lower(@patientId) or lower(pubpid) = lower(@patientId) limit 1;"; command.Parameters.AddWithValue("patientId", patientId.Trim());
        return await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new ArgumentException("Patient was not found.");
    }

    private static async Task<int?> GetEncounterIdAsync(NpgsqlConnection connection, string patientId, Guid referralId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select encounter_id from referrals where id = @referralId and patient_id = @patientId;";
        command.Parameters.AddWithValue("referralId", referralId); command.Parameters.AddWithValue("patientId", patientId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : (int)result;
    }

    private static async Task<bool> IsEncounterLockedAsync(NpgsqlConnection connection, int encounter, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from encounter_signatures where encounter = @encounter and is_lock);";
        command.Parameters.AddWithValue("encounter", encounter);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<IReadOnlyList<ReferralItem>> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var referrals = new List<ReferralItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) referrals.Add(new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8).ToString("O"), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetFieldValue<DateTimeOffset>(10).ToString("O")));
        return referrals;
    }

    private static bool TryParseRequestedAt(string? value, out DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(value)) { requestedAt = DateTimeOffset.UtcNow; return true; }
        return DateTimeOffset.TryParse(value, out requestedAt);
    }
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "create table if not exists referrals (id uuid primary key, patient_id text not null references patients(canonical_id), encounter_id integer, destination text not null, reason text not null, status text not null, external_reference text, notes text, requested_at timestamptz not null, created_at timestamptz not null, updated_at timestamptz not null);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
