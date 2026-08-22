// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using Npgsql;

namespace AvenChart.Api.Data;

/// <summary>
/// Governs external OIDC subject bindings for the patient portal. Each active
/// binding is one-to-one within a provider so an external identity cannot be
/// redirected to another patient's portal record by client input.
/// </summary>
public sealed class PatientPortalExternalIdentityMappingRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<PatientPortalExternalIdentityMappingItem>> GetMappingsAsync(
        string? providerId,
        CancellationToken cancellationToken)
    {
        var normalizedProviderId = string.IsNullOrWhiteSpace(providerId)
            ? null
            : NormalizeProviderId(providerId);
        var mappings = new List<PatientPortalExternalIdentityMappingItem>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select mapping.mapping_id,mapping.provider_id,mapping.external_subject,mapping.patient_id,mapping.active,
                   mapping.created_at,mapping.created_by,mapping.deactivated_at,mapping.deactivated_by,mapping.deactivation_reason
            from patient_portal_external_identity_mappings mapping
            where @providerId is null or mapping.provider_id=@providerId
            order by mapping.active desc,mapping.provider_id,mapping.patient_id,mapping.created_at desc,mapping.mapping_id;
            """;
        command.Parameters.AddWithValue("providerId", (object?)normalizedProviderId ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            mappings.Add(ReadMapping(reader));
        }

        return mappings;
    }

    public async Task<PatientPortalExternalIdentityMappingItem> CreateAsync(
        PatientPortalExternalIdentityMappingCreateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var providerId = NormalizeProviderId(request.ProviderId);
        var externalSubject = NormalizeExternalSubject(request.ExternalSubject);
        var patientId = NormalizeRequired(request.PatientId, "Patient ID", 128);
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalPatientId = await ResolvePortalPatientAsync(connection, transaction, patientId, cancellationToken);
        var mappingId = Guid.NewGuid();
        PatientPortalExternalIdentityMappingItem mapping;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_portal_external_identity_mappings(
                  mapping_id,provider_id,external_subject,patient_id,active,created_at,created_by)
                values(@mappingId,@providerId,@externalSubject,@patientId,true,now(),@actor)
                returning mapping_id,provider_id,external_subject,patient_id,active,
                          created_at,created_by,deactivated_at,deactivated_by,deactivation_reason;
                """;
            command.Parameters.AddWithValue("mappingId", mappingId);
            command.Parameters.AddWithValue("providerId", providerId);
            command.Parameters.AddWithValue("externalSubject", externalSubject);
            command.Parameters.AddWithValue("patientId", canonicalPatientId);
            command.Parameters.AddWithValue("actor", normalizedActor);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The patient portal external identity mapping could not be created.");
            }

            mapping = ReadMapping(reader);
        }

        await InsertEventAsync(connection, transaction, mappingId, "created", normalizedActor, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mapping;
    }

    public async Task<PatientPortalExternalIdentityMappingItem?> DeactivateAsync(
        Guid mappingId,
        PatientPortalExternalIdentityMappingDeactivateRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeRequired(request.Reason, "Deactivation reason", 500);
        var normalizedActor = NormalizeRequired(actor, "Authenticated actor", 120);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        PatientPortalExternalIdentityMappingItem? mapping;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patient_portal_external_identity_mappings
                set active=false,
                    deactivated_at=now(),
                    deactivated_by=@actor,
                    deactivation_reason=@reason
                where mapping_id=@mappingId
                  and active=true
                returning mapping_id,provider_id,external_subject,patient_id,active,
                          created_at,created_by,deactivated_at,deactivated_by,deactivation_reason;
                """;
            command.Parameters.AddWithValue("mappingId", mappingId);
            command.Parameters.AddWithValue("actor", normalizedActor);
            command.Parameters.AddWithValue("reason", reason);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            mapping = await reader.ReadAsync(cancellationToken) ? ReadMapping(reader) : null;
        }

        if (mapping is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await InsertEventAsync(connection, transaction, mappingId, "deactivated", normalizedActor, reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mapping;
    }

    private static async Task<string> ResolvePortalPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string requestedPatientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select patient.canonical_id
            from patients patient
            join patient_portal_accounts account on account.patient_id=patient.canonical_id
            where patient.canonical_id=@patientId
              and patient.portal_enabled=true
              and nullif(btrim(account.portal_username),'') is not null
              and nullif(btrim(account.portal_login_username),'') is not null
            limit 1
            for update;
            """;
        command.Parameters.AddWithValue("patientId", requestedPatientId);
        return await command.ExecuteScalarAsync(cancellationToken) as string
            ?? throw new ArgumentException("The selected patient does not have an active, configured portal account.");
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid mappingId,
        string action,
        string actor,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_portal_external_identity_mapping_events(event_id,mapping_id,action,actor,reason,occurred_at)
            values(@eventId,@mappingId,@action,@actor,@reason,now());
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("mappingId", mappingId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static PatientPortalExternalIdentityMappingItem ReadMapping(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetBoolean(4),
        reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),
        reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9));

    private static string NormalizeProviderId(string? providerId)
    {
        var normalized = NormalizeRequired(providerId, "Provider ID", 80).ToLowerInvariant();
        if (normalized.Length < 2 || !char.IsLetterOrDigit(normalized[0]) || !char.IsLetterOrDigit(normalized[^1]) ||
            normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Provider ID must contain 2-80 lowercase letters, digits, periods, underscores, or hyphens and start and end with a letter or digit.");
        }

        return normalized;
    }

    private static string NormalizeExternalSubject(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 512 || subject != subject.Trim() || subject.Any(char.IsControl))
        {
            throw new ArgumentException("External subject must be 1-512 non-control characters without leading or trailing whitespace.");
        }

        return subject;
    }

    private static string NormalizeRequired(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{label} is required and may not exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
