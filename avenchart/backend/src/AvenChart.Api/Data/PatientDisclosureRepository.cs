// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;
using AvenChart.Api.Security;

namespace AvenChart.Api.Data;

public sealed class PatientDisclosureRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<PatientDisclosureAuthorityResponse>> GetAuthoritiesAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {AuthoritySelect}
            where authority.patient_id = @patient_id
            order by authority.created_at desc, authority.authority_id desc
            limit 100;
            """;
        command.Parameters.AddWithValue("patient_id", canonicalId);

        var authorities = new List<PatientDisclosureAuthorityResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            authorities.Add(ReadAuthority(reader));
        }

        return authorities;
    }

    public async Task<PatientDisclosureAuthorityResponse> CreateAuthorityAsync(
        string patientId,
        PatientDisclosureAuthorityCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var authorityType = NormalizeChoice(
            request.AuthorityType,
            ["patient", "proxy"],
            "Authority type");
        var proxyName = NormalizeOptional(request.ProxyName, 120, "Proxy name");
        var proxyRelationship = NormalizeOptional(
            request.ProxyRelationship,
            80,
            "Proxy relationship");
        if (authorityType == "patient" && (proxyName is not null || proxyRelationship is not null))
        {
            throw new ArgumentException(
                "Patient authority cannot include proxy identity fields.");
        }

        if (authorityType == "proxy" && (proxyName is null || proxyRelationship is null))
        {
            throw new ArgumentException(
                "Proxy name and relationship are required for proxy authority.");
        }

        var purpose = NormalizeRequired(request.Purpose, 120, "Purpose");
        var recipient = NormalizeRequired(request.Recipient, 160, "Recipient");
        var scopeKeys = NormalizeScope(request.ScopeKeys);
        var verificationMethod = NormalizeChoice(
            request.VerificationMethod,
            PatientDisclosurePolicyCatalog.VerificationMethods,
            "Verification method");
        var verificationReference = NormalizeRequired(
            request.VerificationReference,
            160,
            "Verification reference");
        var reason = NormalizeRequired(request.Reason, 500, "Creation reason");
        if (request.ExpiresAt <= request.EffectiveFrom)
        {
            throw new ArgumentException(
                "Authority expiration must be after its effective start.");
        }

        if (request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException(
                "Authority expiration must be in the future.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction,
            lockPatient: true)
            ?? throw new ArgumentException("The patient does not exist.");

        var authorityId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_disclosure_authorities
                  (authority_id, patient_id, authority_type, proxy_name,
                   proxy_relationship, purpose, recipient, scope_keys,
                   effective_from, expires_at, verification_method,
                   verification_reference, policy_revision, status, version,
                   created_at, created_by, updated_at, updated_by)
                values
                  (@authority_id, @patient_id, @authority_type, @proxy_name,
                   @proxy_relationship, @purpose, @recipient, @scope_keys,
                   @effective_from, @expires_at, @verification_method,
                   @verification_reference, @policy_revision, 'pending', 0,
                   now(), @username, now(), @username);
                """;
            command.Parameters.AddWithValue("authority_id", authorityId);
            command.Parameters.AddWithValue("patient_id", canonicalId);
            command.Parameters.AddWithValue("authority_type", authorityType);
            command.Parameters.AddWithValue(
                "proxy_name",
                (object?)proxyName ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "proxy_relationship",
                (object?)proxyRelationship ?? DBNull.Value);
            command.Parameters.AddWithValue("purpose", purpose);
            command.Parameters.AddWithValue("recipient", recipient);
            AddScopeParameter(command, scopeKeys);
            command.Parameters.AddWithValue("effective_from", request.EffectiveFrom);
            command.Parameters.AddWithValue("expires_at", request.ExpiresAt);
            command.Parameters.AddWithValue(
                "verification_method",
                verificationMethod);
            command.Parameters.AddWithValue(
                "verification_reference",
                verificationReference);
            command.Parameters.AddWithValue(
                "policy_revision",
                PatientDisclosurePolicyCatalog.Revision);
            command.Parameters.AddWithValue("username", username);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertAuthorityEventAsync(
            connection,
            transaction,
            authorityId,
            "created",
            null,
            "pending",
            version: 0,
            reason,
            username,
            cancellationToken);
        var response = await GetAuthorityByIdAsync(
            connection,
            transaction,
            canonicalId,
            authorityId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The disclosure authority could not be reloaded.");
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<PatientDisclosureAuthorityResponse> TransitionAuthorityAsync(
        string patientId,
        Guid authorityId,
        string action,
        PatientDisclosureAuthorityTransitionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "Expected authority version cannot be negative.");
        }

        var normalizedAction = NormalizeChoice(
            action,
            ["activate", "revoke"],
            "Authority action");
        var reason = NormalizeRequired(request.Reason, 500, "Transition reason");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction,
            lockPatient: true)
            ?? throw new ArgumentException("The patient does not exist.");

        var current = await GetAuthorityByIdAsync(
            connection,
            transaction,
            canonicalId,
            authorityId,
            cancellationToken,
            forUpdate: true)
            ?? throw new KeyNotFoundException(
                "The disclosure authority does not exist for this patient.");
        if (current.Version != request.ExpectedVersion)
        {
            throw new PatientDisclosureConcurrencyException(
                "The disclosure authority changed after it was loaded.",
                request.ExpectedVersion,
                current.Version);
        }

        var now = DateTimeOffset.UtcNow;
        string nextStatus;
        if (normalizedAction == "activate")
        {
            if (current.Status != "pending")
            {
                throw new InvalidOperationException(
                    "Only pending disclosure authority can be activated.");
            }

            if (now < current.EffectiveFrom)
            {
                throw new InvalidOperationException(
                    "Disclosure authority cannot be activated before its effective start.");
            }

            if (now >= current.ExpiresAt)
            {
                throw new InvalidOperationException(
                    "Expired disclosure authority cannot be activated.");
            }

            nextStatus = "active";
        }
        else
        {
            if (current.Status is not ("pending" or "active"))
            {
                throw new InvalidOperationException(
                    "Only pending or active disclosure authority can be revoked.");
            }

            if (now >= current.ExpiresAt)
            {
                throw new InvalidOperationException(
                    "Expired disclosure authority is already ineffective.");
            }

            nextStatus = "revoked";
        }

        var nextVersion = current.Version + 1;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patient_disclosure_authorities
                set status = @status,
                    version = @next_version,
                    updated_at = now(),
                    updated_by = @username
                where authority_id = @authority_id
                  and patient_id = @patient_id
                  and version = @expected_version;
                """;
            command.Parameters.AddWithValue("status", nextStatus);
            command.Parameters.AddWithValue("next_version", nextVersion);
            command.Parameters.AddWithValue("username", username);
            command.Parameters.AddWithValue("authority_id", authorityId);
            command.Parameters.AddWithValue("patient_id", canonicalId);
            command.Parameters.AddWithValue(
                "expected_version",
                request.ExpectedVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new PatientDisclosureConcurrencyException(
                    "The disclosure authority changed during the transition.",
                    request.ExpectedVersion,
                    current.Version);
            }
        }

        await InsertAuthorityEventAsync(
            connection,
            transaction,
            authorityId,
            normalizedAction == "activate" ? "activated" : "revoked",
            current.Status,
            nextStatus,
            nextVersion,
            reason,
            username,
            cancellationToken);
        var response = await GetAuthorityByIdAsync(
            connection,
            transaction,
            canonicalId,
            authorityId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The disclosure authority could not be reloaded.");
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<PatientDisclosureAuthorityEventResponse>> GetAuthorityHistoryAsync(
        string patientId,
        Guid authorityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        if (await GetAuthorityByIdAsync(
                connection,
                transaction: null,
                canonicalId,
                authorityId,
                cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                "The disclosure authority does not exist for this patient.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, authority_id, action, from_status, to_status,
                   version, reason, occurred_at, username, policy_revision
            from patient_disclosure_authority_events
            where authority_id = @authority_id
            order by event_id desc
            limit 100;
            """;
        command.Parameters.AddWithValue("authority_id", authorityId);
        var events = new List<PatientDisclosureAuthorityEventResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PatientDisclosureAuthorityEventResponse(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetString(8),
                reader.GetString(9)));
        }

        return events;
    }

    public async Task<IReadOnlyList<PatientDisclosureRequestResponse>> GetRequestsAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {RequestSelect}
            where disclosure.patient_id = @patient_id
            order by disclosure.requested_at desc, disclosure.request_id desc
            limit 100;
            """;
        command.Parameters.AddWithValue("patient_id", canonicalId);
        var requests = new List<PatientDisclosureRequestResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(ReadRequest(reader));
        }

        return requests;
    }

    public async Task<PatientDisclosureRequestResponse> CreateRequestAsync(
        string patientId,
        PatientDisclosureRequestCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        var purpose = NormalizeRequired(request.Purpose, 120, "Purpose");
        var recipient = NormalizeRequired(request.Recipient, 160, "Recipient");
        var scopeKeys = NormalizeScope(request.ScopeKeys);
        var reason = NormalizeRequired(request.Reason, 500, "Request reason");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction,
            lockPatient: true)
            ?? throw new ArgumentException("The patient does not exist.");
        var authority = await GetAuthorityByIdAsync(
            connection,
            transaction,
            canonicalId,
            request.AuthorityId,
            cancellationToken,
            forUpdate: true)
            ?? throw new ArgumentException(
                "The selected disclosure authority does not exist for this patient.");
        ValidateAuthorityMatch(authority, purpose, recipient, scopeKeys);

        var requestId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into patient_disclosure_requests
                  (request_id, patient_id, authority_id, purpose, recipient,
                   scope_keys, status, version, policy_revision, requested_at,
                   requested_by)
                values
                  (@request_id, @patient_id, @authority_id, @purpose, @recipient,
                   @scope_keys, 'requested', 0, @policy_revision, now(),
                   @username);
                """;
            command.Parameters.AddWithValue("request_id", requestId);
            command.Parameters.AddWithValue("patient_id", canonicalId);
            command.Parameters.AddWithValue("authority_id", request.AuthorityId);
            command.Parameters.AddWithValue("purpose", purpose);
            command.Parameters.AddWithValue("recipient", recipient);
            AddScopeParameter(command, scopeKeys);
            command.Parameters.AddWithValue(
                "policy_revision",
                PatientDisclosurePolicyCatalog.Revision);
            command.Parameters.AddWithValue("username", username);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertRequestEventAsync(
            connection,
            transaction,
            requestId,
            "requested",
            null,
            "requested",
            version: 0,
            reason,
            username,
            authority,
            cancellationToken);
        var response = await GetRequestByIdAsync(
            connection,
            transaction,
            canonicalId,
            requestId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The disclosure request could not be reloaded.");
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<PatientDisclosureRequestResponse> DecideRequestAsync(
        string patientId,
        Guid requestId,
        PatientDisclosureDecisionRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "Expected disclosure-request version cannot be negative.");
        }

        var action = NormalizeChoice(
            request.Action,
            ["approve", "deny"],
            "Disclosure action");
        var reason = NormalizeRequired(request.Reason, 500, "Decision reason");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction,
            lockPatient: true)
            ?? throw new ArgumentException("The patient does not exist.");
        var current = await GetRequestByIdAsync(
            connection,
            transaction,
            canonicalId,
            requestId,
            cancellationToken,
            forUpdate: true)
            ?? throw new KeyNotFoundException(
                "The disclosure request does not exist for this patient.");
        if (current.Version != request.ExpectedVersion)
        {
            throw new PatientDisclosureConcurrencyException(
                "The disclosure request changed after it was loaded.",
                request.ExpectedVersion,
                current.Version);
        }

        if (current.Status != "requested")
        {
            throw new InvalidOperationException(
                "Only a requested disclosure can be approved or denied.");
        }

        var authority = await GetAuthorityByIdAsync(
            connection,
            transaction,
            canonicalId,
            current.AuthorityId,
            cancellationToken,
            forUpdate: true)
            ?? throw new InvalidOperationException(
                "The disclosure authority is no longer available.");
        if (action == "approve")
        {
            ValidateAuthorityMatch(
                authority,
                current.Purpose,
                current.Recipient,
                current.ScopeKeys);
        }

        var nextStatus = action == "approve" ? "approved" : "denied";
        var nextVersion = current.Version + 1;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patient_disclosure_requests
                set status = @status,
                    version = @next_version,
                    decided_at = now(),
                    decided_by = @username,
                    decision_reason = @reason
                where request_id = @request_id
                  and patient_id = @patient_id
                  and version = @expected_version;
                """;
            command.Parameters.AddWithValue("status", nextStatus);
            command.Parameters.AddWithValue("next_version", nextVersion);
            command.Parameters.AddWithValue("username", username);
            command.Parameters.AddWithValue("reason", reason);
            command.Parameters.AddWithValue("request_id", requestId);
            command.Parameters.AddWithValue("patient_id", canonicalId);
            command.Parameters.AddWithValue(
                "expected_version",
                request.ExpectedVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new PatientDisclosureConcurrencyException(
                    "The disclosure request changed during the decision.",
                    request.ExpectedVersion,
                    current.Version);
            }
        }

        await InsertRequestEventAsync(
            connection,
            transaction,
            requestId,
            action == "approve" ? "approved" : "denied",
            current.Status,
            nextStatus,
            nextVersion,
            reason,
            username,
            authority,
            cancellationToken);
        var response = await GetRequestByIdAsync(
            connection,
            transaction,
            canonicalId,
            requestId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The disclosure request could not be reloaded.");
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<PatientDisclosureRequestEventResponse>> GetRequestHistoryAsync(
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");
        if (await GetRequestByIdAsync(
                connection,
                transaction: null,
                canonicalId,
                requestId,
                cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                "The disclosure request does not exist for this patient.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select event_id, request_id, action, from_status, to_status,
                   version, reason, occurred_at, username, authority_id,
                   authority_version, authority_effective_status,
                   policy_revision
            from patient_disclosure_request_events
            where request_id = @request_id
            order by event_id desc
            limit 100;
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        var events = new List<PatientDisclosureRequestEventResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PatientDisclosureRequestEventResponse(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetString(8),
                reader.GetGuid(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetString(12)));
        }

        return events;
    }

    public async Task<bool> DeleteFixtureAsync(
        string patientId,
        Guid authorityId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var canonicalId = await ResolvePatientAsync(
            connection,
            patientId,
            cancellationToken,
            transaction,
            lockPatient: true)
            ?? throw new ArgumentException("The patient does not exist.");
        await using (var requestCommand = connection.CreateCommand())
        {
            requestCommand.Transaction = transaction;
            requestCommand.CommandText = """
                delete from patient_disclosure_requests disclosure
                where disclosure.authority_id = @authority_id
                  and disclosure.patient_id = @patient_id
                  and exists (
                    select 1
                    from patient_disclosure_authorities authority
                    where authority.authority_id = disclosure.authority_id
                      and authority.patient_id = disclosure.patient_id
                      and authority.verification_reference like 'TMP-DISCLOSURE-%'
                  );
                """;
            requestCommand.Parameters.AddWithValue("authority_id", authorityId);
            requestCommand.Parameters.AddWithValue("patient_id", canonicalId);
            await requestCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var authorityCommand = connection.CreateCommand();
        authorityCommand.Transaction = transaction;
        authorityCommand.CommandText = """
            delete from patient_disclosure_authorities
            where authority_id = @authority_id
              and patient_id = @patient_id
              and verification_reference like 'TMP-DISCLOSURE-%';
            """;
        authorityCommand.Parameters.AddWithValue("authority_id", authorityId);
        authorityCommand.Parameters.AddWithValue("patient_id", canonicalId);
        var deleted = await authorityCommand.ExecuteNonQueryAsync(
            cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    private static async Task<string?> ResolvePatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken,
        NpgsqlTransaction? transaction = null,
        bool lockPatient = false)
    {
        var normalized = NormalizeRequired(patientId, 80, "Patient identifier");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select canonical_id
            from patients
            where (lower(canonical_id) = lower(@patient_id)
                or lower(pubpid) = lower(@patient_id)
                or legacy_pid::text = @patient_id)
              and merged_into_patient_id is null
            limit 1
            {(lockPatient ? "for update" : string.Empty)};
            """;
        command.Parameters.AddWithValue("patient_id", normalized);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<PatientDisclosureAuthorityResponse?> GetAuthorityByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string canonicalId,
        Guid authorityId,
        CancellationToken cancellationToken,
        bool forUpdate = false)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            {AuthoritySelect}
            where authority.patient_id = @patient_id
              and authority.authority_id = @authority_id
            limit 1
            {(forUpdate ? "for update of authority" : string.Empty)};
            """;
        command.Parameters.AddWithValue("patient_id", canonicalId);
        command.Parameters.AddWithValue("authority_id", authorityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadAuthority(reader)
            : null;
    }

    private static async Task<PatientDisclosureRequestResponse?> GetRequestByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string canonicalId,
        Guid requestId,
        CancellationToken cancellationToken,
        bool forUpdate = false)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            {RequestSelect}
            where disclosure.patient_id = @patient_id
              and disclosure.request_id = @request_id
            limit 1
            {(forUpdate ? "for update of disclosure" : string.Empty)};
            """;
        command.Parameters.AddWithValue("patient_id", canonicalId);
        command.Parameters.AddWithValue("request_id", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRequest(reader)
            : null;
    }

    private static PatientDisclosureAuthorityResponse ReadAuthority(
        NpgsqlDataReader reader)
    {
        var status = reader.GetString(reader.GetOrdinal("status"));
        var effectiveFrom = reader.GetFieldValue<DateTimeOffset>(
            reader.GetOrdinal("effective_from"));
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(
            reader.GetOrdinal("expires_at"));
        var effectiveStatus = EffectiveStatus(
            status,
            effectiveFrom,
            expiresAt,
            DateTimeOffset.UtcNow);
        return new PatientDisclosureAuthorityResponse(
            reader.GetGuid(reader.GetOrdinal("authority_id")),
            reader.GetString(reader.GetOrdinal("patient_id")),
            reader.GetString(reader.GetOrdinal("authority_type")),
            ReadNullableString(reader, "proxy_name"),
            ReadNullableString(reader, "proxy_relationship"),
            reader.GetString(reader.GetOrdinal("purpose")),
            reader.GetString(reader.GetOrdinal("recipient")),
            reader.GetFieldValue<string[]>(
                reader.GetOrdinal("scope_keys")),
            effectiveFrom,
            expiresAt,
            reader.GetString(reader.GetOrdinal("verification_method")),
            reader.GetString(reader.GetOrdinal("verification_reference")),
            reader.GetString(reader.GetOrdinal("policy_revision")),
            status,
            effectiveStatus,
            reader.GetInt32(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("created_at")),
            reader.GetString(reader.GetOrdinal("created_by")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("updated_at")),
            reader.GetString(reader.GetOrdinal("updated_by")),
            AuthorityAllowedActions(status, effectiveStatus));
    }

    private static PatientDisclosureRequestResponse ReadRequest(
        NpgsqlDataReader reader)
    {
        var status = reader.GetString(reader.GetOrdinal("status"));
        var authorityStatus = reader.GetString(
            reader.GetOrdinal("authority_status"));
        var authorityEffectiveStatus = EffectiveStatus(
            authorityStatus,
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("authority_effective_from")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("authority_expires_at")),
            DateTimeOffset.UtcNow);
        return new PatientDisclosureRequestResponse(
            reader.GetGuid(reader.GetOrdinal("request_id")),
            reader.GetString(reader.GetOrdinal("patient_id")),
            reader.GetGuid(reader.GetOrdinal("authority_id")),
            reader.GetString(reader.GetOrdinal("purpose")),
            reader.GetString(reader.GetOrdinal("recipient")),
            reader.GetFieldValue<string[]>(reader.GetOrdinal("scope_keys")),
            status,
            reader.GetInt32(reader.GetOrdinal("version")),
            reader.GetString(reader.GetOrdinal("policy_revision")),
            reader.GetFieldValue<DateTimeOffset>(
                reader.GetOrdinal("requested_at")),
            reader.GetString(reader.GetOrdinal("requested_by")),
            reader.IsDBNull(reader.GetOrdinal("decided_at"))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(
                    reader.GetOrdinal("decided_at")),
            ReadNullableString(reader, "decided_by"),
            ReadNullableString(reader, "decision_reason"),
            authorityEffectiveStatus,
            reader.GetInt32(reader.GetOrdinal("authority_version")),
            status == "requested" ? ["approve", "deny"] : []);
    }

    private static void ValidateAuthorityMatch(
        PatientDisclosureAuthorityResponse authority,
        string purpose,
        string recipient,
        IReadOnlyList<string> scopeKeys)
    {
        if (authority.EffectiveStatus != "active")
        {
            throw new InvalidOperationException(
                $"Disclosure authority is {authority.EffectiveStatus} and cannot authorize this request.");
        }

        if (!string.Equals(
                authority.Purpose,
                purpose,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Disclosure purpose does not match the selected authority.");
        }

        if (!string.Equals(
                authority.Recipient,
                recipient,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Disclosure recipient does not match the selected authority.");
        }

        var authorityScopes = authority.ScopeKeys.ToHashSet(
            StringComparer.Ordinal);
        if (scopeKeys.Any(scope => !authorityScopes.Contains(scope)))
        {
            throw new ArgumentException(
                "Disclosure scope exceeds the selected authority.");
        }
    }

    private static string EffectiveStatus(
        string status,
        DateTimeOffset effectiveFrom,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (status == "revoked")
        {
            return "revoked";
        }

        if (now >= expiresAt)
        {
            return "expired";
        }

        if (status == "pending")
        {
            return "pending";
        }

        return now < effectiveFrom ? "not-yet-effective" : "active";
    }

    private static IReadOnlyList<string> AuthorityAllowedActions(
        string status,
        string effectiveStatus)
    {
        if (effectiveStatus == "expired" || status == "revoked")
        {
            return [];
        }

        return status == "pending" ? ["activate", "revoke"] : ["revoke"];
    }

    private static async Task InsertAuthorityEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid authorityId,
        string action,
        string? fromStatus,
        string toStatus,
        int version,
        string reason,
        string username,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_disclosure_authority_events
              (authority_id, action, from_status, to_status, version, reason,
               occurred_at, username, policy_revision)
            values
              (@authority_id, @action, @from_status, @to_status, @version,
               @reason, now(), @username, @policy_revision);
            """;
        command.Parameters.AddWithValue("authority_id", authorityId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue(
            "from_status",
            (object?)fromStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("to_status", toStatus);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue(
            "policy_revision",
            PatientDisclosurePolicyCatalog.Revision);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRequestEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string action,
        string? fromStatus,
        string toStatus,
        int version,
        string reason,
        string username,
        PatientDisclosureAuthorityResponse authority,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_disclosure_request_events
              (request_id, action, from_status, to_status, version, reason,
               occurred_at, username, authority_id, authority_version,
               authority_effective_status, policy_revision)
            values
              (@request_id, @action, @from_status, @to_status, @version,
               @reason, now(), @username, @authority_id, @authority_version,
               @authority_effective_status, @policy_revision);
            """;
        command.Parameters.AddWithValue("request_id", requestId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue(
            "from_status",
            (object?)fromStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("to_status", toStatus);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("authority_id", authority.AuthorityId);
        command.Parameters.AddWithValue(
            "authority_version",
            authority.Version);
        command.Parameters.AddWithValue(
            "authority_effective_status",
            authority.EffectiveStatus);
        command.Parameters.AddWithValue(
            "policy_revision",
            PatientDisclosurePolicyCatalog.Revision);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string[] NormalizeScope(IReadOnlyList<string>? scopeKeys)
    {
        if (scopeKeys is null || scopeKeys.Count == 0)
        {
            throw new ArgumentException(
                "At least one disclosure scope is required.");
        }

        var normalized = scopeKeys
            .Select(scope => scope?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(scope => scope.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "At least one disclosure scope is required.");
        }

        var unsupported = normalized
            .Where(scope =>
                !PatientDisclosurePolicyCatalog.ScopeKeys.Contains(
                    scope,
                    StringComparer.Ordinal))
            .ToArray();
        if (unsupported.Length > 0)
        {
            throw new ArgumentException(
                $"Disclosure scope is not supported: {string.Join(", ", unsupported)}.");
        }

        return PatientDisclosurePolicyCatalog.ScopeKeys
            .Where(normalized.Contains)
            .ToArray();
    }

    private static string NormalizeChoice(
        string? value,
        IReadOnlyList<string> choices,
        string label)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!choices.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"{label} must be one of: {string.Join(", ", choices)}.");
        }

        return normalized;
    }

    private static string NormalizeRequired(
        string? value,
        int maximumLength,
        string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{label} is required.");
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{label} must be {maximumLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{label} must be {maximumLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? ReadNullableString(
        NpgsqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddScopeParameter(
        NpgsqlCommand command,
        IReadOnlyList<string> scopeKeys)
    {
        command.Parameters.Add(
            new NpgsqlParameter<string[]>(
                "scope_keys",
                NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                TypedValue = scopeKeys.ToArray(),
            });
    }

    private const string AuthoritySelect = """
        select authority.authority_id, authority.patient_id,
               authority.authority_type, authority.proxy_name,
               authority.proxy_relationship, authority.purpose,
               authority.recipient, authority.scope_keys,
               authority.effective_from, authority.expires_at,
               authority.verification_method,
               authority.verification_reference,
               authority.policy_revision, authority.status,
               authority.version, authority.created_at,
               authority.created_by, authority.updated_at,
               authority.updated_by
        from patient_disclosure_authorities authority
        """;

    private const string RequestSelect = """
        select disclosure.request_id, disclosure.patient_id,
               disclosure.authority_id, disclosure.purpose,
               disclosure.recipient, disclosure.scope_keys,
               disclosure.status, disclosure.version,
               disclosure.policy_revision, disclosure.requested_at,
               disclosure.requested_by, disclosure.decided_at,
               disclosure.decided_by, disclosure.decision_reason,
               authority.status as authority_status,
               authority.version as authority_version,
               authority.effective_from as authority_effective_from,
               authority.expires_at as authority_expires_at
        from patient_disclosure_requests disclosure
        join patient_disclosure_authorities authority
          on authority.authority_id = disclosure.authority_id
        """;
}
