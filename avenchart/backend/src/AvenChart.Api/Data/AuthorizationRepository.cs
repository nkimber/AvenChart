using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AuthorizationRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<AuthorizationItem>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var canonicalId = await ResolvePatientIdAsync(connection, patientId, cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "select id, patient_id, referral_id, payer, service, status, authorization_number, requested_at, expires_at, created_at, updated_at from authorizations where patient_id = @patientId order by requested_at desc, created_at desc;"; command.Parameters.AddWithValue("patientId", canonicalId);
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<AuthorizationItem> CreateAsync(string patientId, AuthorizationCreateRequest request, CancellationToken cancellationToken)
    {
        var payer = request.Payer?.Trim(); var service = request.Service?.Trim();
        if (string.IsNullOrWhiteSpace(payer) || payer.Length > 240) throw new ArgumentException("Payer is required and must be 240 characters or fewer.");
        if (string.IsNullOrWhiteSpace(service) || service.Length > 500) throw new ArgumentException("Service is required and must be 500 characters or fewer.");
        if (!TryParse(request.RequestedAt, out var requestedAt) || !TryParseNullable(request.ExpiresAt, out var expiresAt)) throw new ArgumentException("Requested and expiry dates must be valid ISO dates or date-times.");
        if (expiresAt is not null && expiresAt < requestedAt) throw new ArgumentException("Expiry cannot be before the requested date.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var canonicalId = await ResolvePatientIdAsync(connection, patientId, cancellationToken);
        if (request.ReferralId is not null)
        {
            await using var referralCommand = connection.CreateCommand(); referralCommand.CommandText = "select exists(select 1 from referrals where id = @referralId and patient_id = @patientId);"; referralCommand.Parameters.AddWithValue("referralId", request.ReferralId.Value); referralCommand.Parameters.AddWithValue("patientId", canonicalId);
            if (!(bool)(await referralCommand.ExecuteScalarAsync(cancellationToken) ?? false)) throw new ArgumentException("Authorization referral does not belong to this patient.");
        }
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into authorizations (id, patient_id, referral_id, payer, service, status, authorization_number, requested_at, expires_at, created_at, updated_at)
            values (@id, @patientId, @referralId, @payer, @service, 'draft', null, @requestedAt, @expiresAt, @now, @now)
            returning id, patient_id, referral_id, payer, service, status, authorization_number, requested_at, expires_at, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("patientId", canonicalId); command.Parameters.AddWithValue("referralId", (object?)request.ReferralId ?? DBNull.Value); command.Parameters.AddWithValue("payer", payer); command.Parameters.AddWithValue("service", service); command.Parameters.AddWithValue("requestedAt", requestedAt); command.Parameters.AddWithValue("expiresAt", (object?)expiresAt ?? DBNull.Value); command.Parameters.AddWithValue("now", now);
        return (await ReadAsync(command, cancellationToken)).Single();
    }

    public async Task<AuthorizationItem> UpdateStatusAsync(string patientId, Guid authorizationId, AuthorizationStatusRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant(); if (status is not ("submitted" or "approved" or "denied" or "expired" or "cancelled")) throw new ArgumentException("Authorization status must be submitted, approved, denied, expired, or cancelled.");
        if (status == "approved" && string.IsNullOrWhiteSpace(request.AuthorizationNumber)) throw new ArgumentException("An approval requires an authorization number.");
        await EnsureSchemaAsync(cancellationToken); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var canonicalId = await ResolvePatientIdAsync(connection, patientId, cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = """
            update authorizations set status = @status, authorization_number = coalesce(@authorizationNumber, authorization_number), updated_at = @now
            where id = @id and patient_id = @patientId
              and ((status = 'draft' and @status in ('submitted', 'cancelled')) or (status = 'submitted' and @status in ('approved', 'denied', 'cancelled')) or (status = 'approved' and @status = 'expired'))
            returning id, patient_id, referral_id, payer, service, status, authorization_number, requested_at, expires_at, created_at, updated_at;
            """;
        command.Parameters.AddWithValue("status", status); command.Parameters.AddWithValue("authorizationNumber", (object?)TrimToNull(request.AuthorizationNumber) ?? DBNull.Value); command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow); command.Parameters.AddWithValue("id", authorizationId); command.Parameters.AddWithValue("patientId", canonicalId);
        var results = await ReadAsync(command, cancellationToken); if (results.Count == 0) throw new ArgumentException("Authorization was not found or cannot make that status transition."); return results.Single();
    }

    private static async Task<string> ResolvePatientIdAsync(NpgsqlConnection connection, string patientId, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "select canonical_id from patients where lower(canonical_id) = lower(@patientId) or lower(pubpid) = lower(@patientId) limit 1;"; command.Parameters.AddWithValue("patientId", patientId.Trim()); return await command.ExecuteScalarAsync(cancellationToken) as string ?? throw new ArgumentException("Patient was not found."); }
    private static async Task<IReadOnlyList<AuthorizationItem>> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken) { var values = new List<AuthorizationItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) values.Add(new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O"), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8).ToString("O"), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetFieldValue<DateTimeOffset>(10).ToString("O"))); return values; }
    private static bool TryParse(string? value, out DateTimeOffset result) { if (string.IsNullOrWhiteSpace(value)) { result = DateTimeOffset.UtcNow; return true; } return DateTimeOffset.TryParse(value, out result); }
    private static bool TryParseNullable(string? value, out DateTimeOffset? result) { if (string.IsNullOrWhiteSpace(value)) { result = null; return true; } if (DateTimeOffset.TryParse(value, out var parsed)) { result = parsed; return true; } result = null; return false; }
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken) { await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "create table if not exists authorizations (id uuid primary key, patient_id text not null references patients(canonical_id), referral_id uuid references referrals(id), payer text not null, service text not null, status text not null, authorization_number text, requested_at timestamptz not null, expires_at timestamptz, created_at timestamptz not null, updated_at timestamptz not null);"; await command.ExecuteNonQueryAsync(cancellationToken); }
}
