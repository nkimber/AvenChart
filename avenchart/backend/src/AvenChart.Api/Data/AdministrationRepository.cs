using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AdministrationRepository(NpgsqlDataSource dataSource)
{
    private const string DefaultFacilityColor = "#246b73";
    private const string DefaultUserEmailDomain = "example.test";
    private static readonly JsonSerializerOptions PortalProfileChangeJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ValidAccessReturnValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "addonly",
        "view",
        "write",
        "wsome"
    };

    public async Task<PracticeSettingsResponse> GetPracticeSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select setting_key, setting_value, value_type, updated_at, updated_by from practice_settings order by setting_key;";
        var settings = new List<PracticeSettingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) settings.Add(new(reader.GetString(0), reader.GetString(0) switch { "practice.name" => "Practice name", "practice.default-facility-id" => "Default facility", _ => "Time zone" }, reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4)));
        return new PracticeSettingsResponse(settings);
    }

    public async Task<EffectivePracticeSettingsResponse> GetEffectivePracticeSettingsAsync(int? facilityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (facilityId is not null)
        {
            await using var facility = connection.CreateCommand();
            facility.CommandText = "select exists(select 1 from facilities where id=@facilityId and inactive=false);";
            facility.Parameters.AddWithValue("facilityId", facilityId.Value);
            if (!(bool)(await facility.ExecuteScalarAsync(cancellationToken) ?? false)) throw new ArgumentException("The requested active facility was not found.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select setting.setting_key, setting.setting_value, setting.value_type, setting.updated_at, setting.updated_by,
                   facility_override.setting_value, facility_override.updated_at, facility_override.updated_by
            from practice_settings setting
            left join practice_setting_facility_overrides facility_override
              on facility_override.setting_key=setting.setting_key and facility_override.facility_id=@facilityId
            order by setting.setting_key;
            """;
        command.Parameters.AddWithValue("facilityId", (object?)facilityId ?? DBNull.Value);
        var settings = new List<EffectivePracticeSettingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var overridden = !reader.IsDBNull(5);
            var key = reader.GetString(0);
            settings.Add(new(
                key,
                key switch { "practice.name" => "Practice name", "practice.default-facility-id" => "Default facility", _ => "Time zone" },
                overridden ? reader.GetString(5) : reader.GetString(1),
                reader.GetString(2),
                overridden ? "facility" : "system",
                overridden ? facilityId : null,
                (overridden ? reader.GetFieldValue<DateTimeOffset>(6) : reader.GetFieldValue<DateTimeOffset>(3)).ToString("O"),
                overridden ? reader.GetString(7) : reader.GetString(4),
                facilityId is not null));
        }
        return new(facilityId, settings);
    }

    public async Task<CodingCatalogResponse> GetCodingCatalogsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select catalog_key, display_name, sequence, active, claim_enabled, fee_enabled, modifier_length, updated_at, updated_by from coding_catalogs order by sequence, catalog_key;";
        var catalogs = new List<CodingCatalogItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) catalogs.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O"), reader.GetString(8)));
        return new CodingCatalogResponse(catalogs);
    }

    public async Task<CodingCatalogResponse> UpdateCodingCatalogAsync(string key, CodingCatalogUpdateRequest request, string username, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(key);
        ValidateCodingCatalog(request.DisplayName, request.Sequence, request.ModifierLength);
        var displayName = request.DisplayName.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        bool changed;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "select display_name,sequence,active,claim_enabled,fee_enabled,modifier_length from coding_catalogs where catalog_key=@key for update;";
            existing.Parameters.AddWithValue("key", catalogKey);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Catalog was not found.");
            changed = reader.GetString(0) != displayName || reader.GetInt32(1) != request.Sequence || reader.GetBoolean(2) != request.Active || reader.GetBoolean(3) != request.ClaimEnabled || reader.GetBoolean(4) != request.FeeEnabled || reader.GetInt32(5) != request.ModifierLength;
        }
        if (changed)
        {
            try { await WriteCodingCatalogRevisionAsync(connection, transaction, catalogKey, new(displayName, request.Sequence, request.Active, request.ClaimEnabled, request.FeeEnabled, request.ModifierLength), username, "updated", null, cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Catalog key and sequence must be unique."); }
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetCodingCatalogsAsync(cancellationToken);
    }

    public async Task<CodingCatalogResponse> CreateCodingCatalogAsync(CodingCatalogCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(request.Key);
        ValidateCodingCatalog(request.DisplayName, request.Sequence, request.ModifierLength);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "insert into coding_catalogs(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,updated_at,updated_by) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,now(),@user); insert into coding_catalog_audit_events(event_id,catalog_key,action,occurred_at,username) values(@eventId,@key,'created',now(),@user); insert into coding_catalog_revisions(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,action,occurred_at,username) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,'created',now(),@user);";
        command.Parameters.AddWithValue("key", catalogKey); command.Parameters.AddWithValue("name", request.DisplayName.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("claim", request.ClaimEnabled); command.Parameters.AddWithValue("fee", request.FeeEnabled); command.Parameters.AddWithValue("modifier", request.ModifierLength); command.Parameters.AddWithValue("user", username); command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        try { await command.ExecuteNonQueryAsync(cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Catalog key and sequence must be unique."); }
        await transaction.CommitAsync(cancellationToken);
        return await GetCodingCatalogsAsync(cancellationToken);
    }

    public async Task<CodingCatalogHistoryResponse> GetCodingCatalogHistoryAsync(string key, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(key); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var catalog = await GetCodingCatalogAsync(connection, catalogKey, cancellationToken) ?? throw new ArgumentException("Catalog was not found.");
        await using var command = connection.CreateCommand(); command.CommandText = "select revision_id,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,action,restored_from_revision_id,occurred_at,username from coding_catalog_revisions where catalog_key=@key order by occurred_at desc,revision_id desc;"; command.Parameters.AddWithValue("key", catalogKey);
        var revisions = new List<CodingCatalogRevision>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetInt32(2),reader.GetBoolean(3),reader.GetBoolean(4),reader.GetBoolean(5),reader.GetInt32(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetInt64(8),reader.GetFieldValue<DateTimeOffset>(9).ToString("O"),reader.GetString(10)));
        return new(catalog,revisions);
    }

    public async Task<CodingCatalogHistoryResponse> RollbackCodingCatalogAsync(string key, long revisionId, string username, CancellationToken cancellationToken)
    {
        var catalogKey = NormalizeCatalogKey(key); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var target = await GetCodingCatalogRevisionAsync(connection, transaction, catalogKey, revisionId, cancellationToken) ?? throw new ArgumentException("The requested revision was not found for this catalog.");
        ValidateCodingCatalog(target.DisplayName,target.Sequence,target.ModifierLength);
        var current = await GetCodingCatalogForUpdateAsync(connection, transaction, catalogKey, cancellationToken) ?? throw new ArgumentException("Catalog was not found.");
        if (current != target) try { await WriteCodingCatalogRevisionAsync(connection, transaction, catalogKey, target, username, "rolled-back", revisionId, cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Catalog key and sequence must be unique."); }
        await transaction.CommitAsync(cancellationToken); return await GetCodingCatalogHistoryAsync(catalogKey,cancellationToken);
    }

    public async Task<CodingCatalogChangeRequestsResponse> GetCodingCatalogChangeRequestsAsync(string? status, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("Change-request paging is invalid.");
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("all" or "open" or "draft" or "submitted" or "approved" or "rejected" or "activated" or "cancelled")) throw new ArgumentException("Change-request status is not supported.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from coding_catalog_change_requests;";
        countCommand.Parameters.AddWithValue("status", normalizedStatus);
        var counts = new CodingCatalogChangeRequestCounts(0, 0, 0, 0, 0, 0); var total = 0;
        await using (var reader = await countCommand.ExecuteReaderAsync(cancellationToken)) if (await reader.ReadAsync(cancellationToken)) { counts = new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)); total = reader.GetInt32(6); }
        await using var command = connection.CreateCommand();
        command.CommandText = "select request_id,catalog_key,change_kind,proposed_display_name,proposed_sequence,proposed_active,proposed_claim_enabled,proposed_fee_enabled,proposed_modifier_length,baseline_display_name,baseline_sequence,baseline_active,baseline_claim_enabled,baseline_fee_enabled,baseline_modifier_length,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from coding_catalog_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc offset @offset limit @limit;";
        command.Parameters.AddWithValue("status", normalizedStatus); command.Parameters.AddWithValue("offset", offset); command.Parameters.AddWithValue("limit", limit);
        var items = new List<CodingCatalogChangeRequestItem>(); await using var result = await command.ExecuteReaderAsync(cancellationToken); while (await result.ReadAsync(cancellationToken)) items.Add(ReadCodingCatalogChangeRequest(result));
        return new(items, total, items.Count, offset, limit, normalizedStatus, counts);
    }

    public async Task<CodingCatalogChangeRequestDetailResponse> CreateCodingCatalogChangeRequestAsync(CodingCatalogChangeRequestCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var key = NormalizeCatalogKey(request.Key); ValidateCodingCatalog(request.DisplayName, request.Sequence, request.ModifierLength); var reason = ValidateChangeRequestReason(request.Reason, true)!;
        var proposed = new CodingCatalogSnapshot(request.DisplayName.Trim(), request.Sequence, request.Active, request.ClaimEnabled, request.FeeEnabled, request.ModifierLength); var requestId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var baseline = await GetCodingCatalogForUpdateAsync(connection, transaction, key, cancellationToken);
        if (baseline == proposed) throw new ArgumentException("The proposed catalog must differ from the active catalog.");
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction; duplicate.CommandText = "select exists(select 1 from coding_catalog_change_requests where catalog_key=@key and status in ('draft','submitted','approved'));"; duplicate.Parameters.AddWithValue("key", key);
            if ((bool)(await duplicate.ExecuteScalarAsync(cancellationToken) ?? false)) throw new CodingCatalogChangeRequestConflictException("An open change request already exists for this catalog.");
        }
        DateTimeOffset? baselineUpdatedAt = null;
        if (baseline is not null) { await using var baselineCommand = connection.CreateCommand(); baselineCommand.Transaction = transaction; baselineCommand.CommandText = "select updated_at from coding_catalogs where catalog_key=@key;"; baselineCommand.Parameters.AddWithValue("key", key); baselineUpdatedAt = AsUtcTimestamp(await baselineCommand.ExecuteScalarAsync(cancellationToken)); }
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction; create.CommandText = "insert into coding_catalog_change_requests(request_id,catalog_key,change_kind,proposed_display_name,proposed_sequence,proposed_active,proposed_claim_enabled,proposed_fee_enabled,proposed_modifier_length,baseline_display_name,baseline_sequence,baseline_active,baseline_claim_enabled,baseline_fee_enabled,baseline_modifier_length,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@kind,@name,@sequence,@active,@claim,@fee,@modifier,@baselineName,@baselineSequence,@baselineActive,@baselineClaim,@baselineFee,@baselineModifier,@baselineUpdated,@reason,'draft',0,now(),@user,now(),@user);";
            create.Parameters.AddWithValue("id", requestId); create.Parameters.AddWithValue("key", key); create.Parameters.AddWithValue("kind", baseline is null ? "create" : "update"); AddCodingCatalogSnapshotParameters(create, "", proposed); AddCodingCatalogSnapshotParameters(create, "baseline", baseline); create.Parameters.AddWithValue("baselineUpdated", (object?)baselineUpdatedAt ?? DBNull.Value); create.Parameters.AddWithValue("reason", reason); create.Parameters.AddWithValue("user", username); await create.ExecuteNonQueryAsync(cancellationToken);
        }
        await WriteCodingCatalogChangeRequestEventAsync(connection, transaction, requestId, "created", reason, username, cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetCodingCatalogChangeRequestAsync(requestId, cancellationToken);
    }

    public async Task<CodingCatalogChangeRequestDetailResponse> GetCodingCatalogChangeRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var request = await GetCodingCatalogChangeRequestAsync(connection, requestId, cancellationToken) ?? throw new ArgumentException("The requested coding-catalog change request was not found."); var active = await GetCodingCatalogAsync(connection, request.CatalogKey, cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from coding_catalog_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", requestId); var events = new List<CodingCatalogChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) events.Add(new(reader.GetInt64(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),reader.GetString(4))); return new(request,active,events);
    }

    public Task<CodingCatalogChangeRequestDetailResponse> SubmitCodingCatalogChangeRequestAsync(Guid requestId, string? note, int? expectedVersion, string username, CancellationToken cancellationToken) => TransitionCodingCatalogChangeRequestAsync(requestId,["draft"],"submitted",note,false,expectedVersion,username,cancellationToken);
    public Task<CodingCatalogChangeRequestDetailResponse> ApproveCodingCatalogChangeRequestAsync(Guid requestId, string? note, int? expectedVersion, string username, CancellationToken cancellationToken) => TransitionCodingCatalogChangeRequestAsync(requestId,["submitted"],"approved",note,false,expectedVersion,username,cancellationToken);
    public Task<CodingCatalogChangeRequestDetailResponse> RejectCodingCatalogChangeRequestAsync(Guid requestId, string? note, int? expectedVersion, string username, CancellationToken cancellationToken) => TransitionCodingCatalogChangeRequestAsync(requestId,["submitted"],"rejected",note,true,expectedVersion,username,cancellationToken);
    public Task<CodingCatalogChangeRequestDetailResponse> ActivateCodingCatalogChangeRequestAsync(Guid requestId, string? note, int? expectedVersion, string username, CancellationToken cancellationToken) => TransitionCodingCatalogChangeRequestAsync(requestId,["approved"],"activated",note,false,expectedVersion,username,cancellationToken);
    public Task<CodingCatalogChangeRequestDetailResponse> CancelCodingCatalogChangeRequestAsync(Guid requestId, string? note, int? expectedVersion, string username, CancellationToken cancellationToken) => TransitionCodingCatalogChangeRequestAsync(requestId,["draft","submitted","approved"],"cancelled",note,true,expectedVersion,username,cancellationToken);

    private async Task<CodingCatalogChangeRequestDetailResponse> TransitionCodingCatalogChangeRequestAsync(Guid requestId, string[] expectedStatuses, string nextStatus, string? note, bool noteRequired, int? expectedVersion, string username, CancellationToken cancellationToken)
    {
        var normalizedNote = ValidateChangeRequestReason(note, noteRequired); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        CodingCatalogChangeRequestItem request;
        await using (var current = connection.CreateCommand()) { current.Transaction = transaction; current.CommandText = "select request_id,catalog_key,change_kind,proposed_display_name,proposed_sequence,proposed_active,proposed_claim_enabled,proposed_fee_enabled,proposed_modifier_length,baseline_display_name,baseline_sequence,baseline_active,baseline_claim_enabled,baseline_fee_enabled,baseline_modifier_length,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from coding_catalog_change_requests where request_id=@id for update;"; current.Parameters.AddWithValue("id", requestId); await using var reader = await current.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The requested coding-catalog change request was not found."); request = ReadCodingCatalogChangeRequest(reader); }
        if (!expectedStatuses.Contains(request.Status, StringComparer.Ordinal)) throw new CodingCatalogChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {nextStatus}.");
        if (expectedVersion is not null && expectedVersion != request.Version) throw new CodingCatalogChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");
        if (nextStatus == "activated")
        {
            var proposed = new CodingCatalogSnapshot(request.ProposedDisplayName,request.ProposedSequence,request.ProposedActive,request.ProposedClaimEnabled,request.ProposedFeeEnabled,request.ProposedModifierLength); var current = await GetCodingCatalogForUpdateAsync(connection,transaction,request.CatalogKey,cancellationToken);
            if (request.ChangeKind == "create")
            {
                if (current is not null) throw new CodingCatalogChangeRequestConflictException("The catalog was created after this request was drafted. Cancel this stale request and create a new proposal.");
                await using var create = connection.CreateCommand(); create.Transaction=transaction; create.CommandText="insert into coding_catalogs(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,updated_at,updated_by) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,now(),@user); insert into coding_catalog_audit_events(event_id,catalog_key,action,occurred_at,username) values(@eventId,@key,'created',now(),@user); insert into coding_catalog_revisions(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,action,occurred_at,username) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,'created',now(),@user);"; create.Parameters.AddWithValue("key",request.CatalogKey); AddCodingCatalogSnapshotParameters(create,"",proposed); create.Parameters.AddWithValue("user",username); create.Parameters.AddWithValue("eventId",Guid.NewGuid()); try { await create.ExecuteNonQueryAsync(cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new CodingCatalogChangeRequestConflictException("Catalog key and sequence must be unique."); }
            }
            else
            {
                var baseline = new CodingCatalogSnapshot(request.BaselineDisplayName!,request.BaselineSequence!.Value,request.BaselineActive!.Value,request.BaselineClaimEnabled!.Value,request.BaselineFeeEnabled!.Value,request.BaselineModifierLength!.Value);
                if (current != baseline) throw new CodingCatalogChangeRequestConflictException("The active catalog changed after this request was created. Cancel this stale request and create a new proposal.");
                await using var baselineCommand = connection.CreateCommand(); baselineCommand.Transaction=transaction; baselineCommand.CommandText="select updated_at from coding_catalogs where catalog_key=@key;"; baselineCommand.Parameters.AddWithValue("key",request.CatalogKey); var updatedAt=AsUtcTimestamp(await baselineCommand.ExecuteScalarAsync(cancellationToken)); if (updatedAt?.ToUniversalTime()!=DateTimeOffset.Parse(request.BaselineUpdatedAt!).ToUniversalTime()) throw new CodingCatalogChangeRequestConflictException("The active catalog changed after this request was created. Cancel this stale request and create a new proposal.");
                try { await WriteCodingCatalogRevisionAsync(connection,transaction,request.CatalogKey,proposed,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new CodingCatalogChangeRequestConflictException("Catalog key and sequence must be unique."); }
            }
        }
        await using (var update = connection.CreateCommand()) { update.Transaction=transaction; update.CommandText="update coding_catalog_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("id",requestId); update.Parameters.AddWithValue("status",nextStatus); update.Parameters.AddWithValue("user",username); await update.ExecuteNonQueryAsync(cancellationToken); }
        await WriteCodingCatalogChangeRequestEventAsync(connection,transaction,requestId,nextStatus,normalizedNote,username,cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetCodingCatalogChangeRequestAsync(requestId,cancellationToken);
    }

    private static void AddCodingCatalogSnapshotParameters(NpgsqlCommand command, string prefix, CodingCatalogSnapshot? snapshot)
    { command.Parameters.AddWithValue(prefix + "Name", (object?)snapshot?.DisplayName ?? DBNull.Value); command.Parameters.AddWithValue(prefix + "Sequence", (object?)snapshot?.Sequence ?? DBNull.Value); command.Parameters.AddWithValue(prefix + "Active", (object?)snapshot?.Active ?? DBNull.Value); command.Parameters.AddWithValue(prefix + "Claim", (object?)snapshot?.ClaimEnabled ?? DBNull.Value); command.Parameters.AddWithValue(prefix + "Fee", (object?)snapshot?.FeeEnabled ?? DBNull.Value); command.Parameters.AddWithValue(prefix + "Modifier", (object?)snapshot?.ModifierLength ?? DBNull.Value); }
    private static DateTimeOffset? AsUtcTimestamp(object? value) => value is null or DBNull ? null : value is DateTimeOffset offset ? offset : new DateTimeOffset(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));
    private static CodingCatalogChangeRequestItem ReadCodingCatalogChangeRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetBoolean(5),reader.GetBoolean(6),reader.GetBoolean(7),reader.GetInt32(8),reader.IsDBNull(9)?null:reader.GetString(9),reader.IsDBNull(10)?null:reader.GetInt32(10),reader.IsDBNull(11)?null:reader.GetBoolean(11),reader.IsDBNull(12)?null:reader.GetBoolean(12),reader.IsDBNull(13)?null:reader.GetBoolean(13),reader.IsDBNull(14)?null:reader.GetInt32(14),reader.IsDBNull(15)?null:reader.GetFieldValue<DateTimeOffset>(15).ToString("O"),reader.GetString(16),reader.GetString(17),reader.GetInt32(18),reader.GetFieldValue<DateTimeOffset>(19).ToString("O"),reader.GetString(20),reader.GetFieldValue<DateTimeOffset>(21).ToString("O"),reader.GetString(22));
    private static async Task<CodingCatalogChangeRequestItem?> GetCodingCatalogChangeRequestAsync(NpgsqlConnection connection, Guid requestId, CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand(); command.CommandText="select request_id,catalog_key,change_kind,proposed_display_name,proposed_sequence,proposed_active,proposed_claim_enabled,proposed_fee_enabled,proposed_modifier_length,baseline_display_name,baseline_sequence,baseline_active,baseline_claim_enabled,baseline_fee_enabled,baseline_modifier_length,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from coding_catalog_change_requests where request_id=@id;"; command.Parameters.AddWithValue("id",requestId); await using var reader=await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken)?ReadCodingCatalogChangeRequest(reader):null; }
    private static async Task WriteCodingCatalogChangeRequestEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requestId, string action, string? note, string username, CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand(); command.Transaction=transaction; command.CommandText="insert into coding_catalog_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id",requestId); command.Parameters.AddWithValue("action",action); command.Parameters.AddWithValue("note",(object?)note??DBNull.Value); command.Parameters.AddWithValue("user",username); await command.ExecuteNonQueryAsync(cancellationToken); }

    private sealed record CodingCatalogSnapshot(string DisplayName,int Sequence,bool Active,bool ClaimEnabled,bool FeeEnabled,int ModifierLength);
    private static async Task<CodingCatalogItem?> GetCodingCatalogAsync(NpgsqlConnection connection,string key,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.CommandText="select catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,updated_at,updated_by from coding_catalogs where catalog_key=@key;";command.Parameters.AddWithValue("key",key);await using var reader=await command.ExecuteReaderAsync(cancellationToken);return await reader.ReadAsync(cancellationToken)?new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),reader.GetBoolean(3),reader.GetBoolean(4),reader.GetBoolean(5),reader.GetInt32(6),reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),reader.GetString(8)):null; }
    private static async Task<CodingCatalogSnapshot?> GetCodingCatalogForUpdateAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select display_name,sequence,active,claim_enabled,fee_enabled,modifier_length from coding_catalogs where catalog_key=@key for update;";command.Parameters.AddWithValue("key",key);await using var reader=await command.ExecuteReaderAsync(cancellationToken);return await reader.ReadAsync(cancellationToken)?new(reader.GetString(0),reader.GetInt32(1),reader.GetBoolean(2),reader.GetBoolean(3),reader.GetBoolean(4),reader.GetInt32(5)):null; }
    private static async Task<CodingCatalogSnapshot?> GetCodingCatalogRevisionAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,long revisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="select display_name,sequence,active,claim_enabled,fee_enabled,modifier_length from coding_catalog_revisions where catalog_key=@key and revision_id=@revision;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("revision",revisionId);await using var reader=await command.ExecuteReaderAsync(cancellationToken);return await reader.ReadAsync(cancellationToken)?new(reader.GetString(0),reader.GetInt32(1),reader.GetBoolean(2),reader.GetBoolean(3),reader.GetBoolean(4),reader.GetInt32(5)):null; }
    private static async Task WriteCodingCatalogRevisionAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,CodingCatalogSnapshot value,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="update coding_catalogs set display_name=@name,sequence=@sequence,active=@active,claim_enabled=@claim,fee_enabled=@fee,modifier_length=@modifier,updated_at=now(),updated_by=@user where catalog_key=@key; insert into coding_catalog_audit_events(event_id,catalog_key,action,occurred_at,username) values(@eventId,@key,@action,now(),@user); insert into coding_catalog_revisions(catalog_key,display_name,sequence,active,claim_enabled,fee_enabled,modifier_length,action,restored_from_revision_id,occurred_at,username) values(@key,@name,@sequence,@active,@claim,@fee,@modifier,@action,@restored,now(),@user);";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("name",value.DisplayName);command.Parameters.AddWithValue("sequence",value.Sequence);command.Parameters.AddWithValue("active",value.Active);command.Parameters.AddWithValue("claim",value.ClaimEnabled);command.Parameters.AddWithValue("fee",value.FeeEnabled);command.Parameters.AddWithValue("modifier",value.ModifierLength);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("eventId",Guid.NewGuid());command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken); }

    private static void ValidateCodingCatalog(string displayName, int sequence, int modifierLength)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120 || sequence < 0 || modifierLength is < 0 or > 12)
            throw new ArgumentException("Catalog label, sequence, and modifier length are invalid.");
    }

    private static string NormalizeCatalogKey(string key)
    {
        var normalized = key.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 32 || !normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new ArgumentException("Catalog key must be 2-32 uppercase letters, numbers, or underscores.");
        return normalized;
    }

    public async Task<FormLayoutCatalogResponse> GetFormLayoutsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "select layout_key,title,mapping,sequence,active,updated_at,updated_by from form_layouts order by sequence,layout_key;";
        var layouts = new List<FormLayoutItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) layouts.Add(ReadLayout(reader));
        return new FormLayoutCatalogResponse(layouts);
    }

    public async Task<FormLayoutDetailResponse> GetFormLayoutAsync(string key, CancellationToken cancellationToken)
    {
        var layoutKey = NormalizeCatalogKey(key); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var layoutCommand = connection.CreateCommand(); layoutCommand.CommandText = "select layout_key,title,mapping,sequence,active,updated_at,updated_by from form_layouts where layout_key=@key;"; layoutCommand.Parameters.AddWithValue("key", layoutKey);
        await using var layoutReader = await layoutCommand.ExecuteReaderAsync(cancellationToken); if (!await layoutReader.ReadAsync(cancellationToken)) throw new ArgumentException("Layout was not found."); var layout = ReadLayout(layoutReader); await layoutReader.CloseAsync();
        await using var groupCommand = connection.CreateCommand(); groupCommand.CommandText = "select group_key,title,sequence,active,updated_at,updated_by from form_layout_groups where layout_key=@key order by sequence,group_key;"; groupCommand.Parameters.AddWithValue("key", layoutKey);
        var groups = new List<FormLayoutGroupItem>(); await using (var groupReader = await groupCommand.ExecuteReaderAsync(cancellationToken)) while (await groupReader.ReadAsync(cancellationToken)) groups.Add(new(groupReader.GetString(0), groupReader.GetString(1), groupReader.GetInt32(2), groupReader.GetBoolean(3), groupReader.GetFieldValue<DateTimeOffset>(4).ToString("O"), groupReader.GetString(5)));
        await using var fieldCommand = connection.CreateCommand(); fieldCommand.CommandText = "select field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by from form_layout_fields where layout_key=@key order by group_key,sequence,field_key;"; fieldCommand.Parameters.AddWithValue("key", layoutKey);
        var fields = new List<FormLayoutFieldItem>(); await using var fieldReader = await fieldCommand.ExecuteReaderAsync(cancellationToken); while (await fieldReader.ReadAsync(cancellationToken)) fields.Add(new(fieldReader.GetString(0), fieldReader.GetString(1), fieldReader.GetString(2), fieldReader.GetString(3), fieldReader.GetInt32(4), fieldReader.GetBoolean(5), fieldReader.GetBoolean(6), fieldReader.GetInt32(7), fieldReader.IsDBNull(8) ? "" : fieldReader.GetString(8), fieldReader.IsDBNull(9) ? "" : fieldReader.GetString(9), fieldReader.GetFieldValue<DateTimeOffset>(10).ToString("O"), fieldReader.GetString(11)));
        return new FormLayoutDetailResponse(layout, groups, fields);
    }

    public async Task<FormLayoutDetailResponse> UpsertFormLayoutAsync(string key, FormLayoutMutationRequest request, string username, CancellationToken cancellationToken)
    {
        var layoutKey = NormalizeCatalogKey(key); ValidateLayoutText(request.Title, request.Mapping); if (request.Sequence < 0) throw new ArgumentException("Layout sequence must be non-negative.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText = "insert into form_layouts(layout_key,title,mapping,sequence,active,updated_at,updated_by) values(@key,@title,@mapping,@sequence,@active,now(),@user) on conflict(layout_key) do update set title=excluded.title,mapping=excluded.mapping,sequence=excluded.sequence,active=excluded.active,updated_at=now(),updated_by=excluded.updated_by;";
        command.Parameters.AddWithValue("key", layoutKey); command.Parameters.AddWithValue("title", request.Title.Trim()); command.Parameters.AddWithValue("mapping", request.Mapping.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("user", username); try { await command.ExecuteNonQueryAsync(cancellationToken); await SnapshotFormLayoutAsync(connection,transaction,layoutKey,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Layout sequence must be unique."); } await transaction.CommitAsync(cancellationToken);
        return await GetFormLayoutAsync(layoutKey, cancellationToken);
    }

    public async Task<FormLayoutDetailResponse> UpsertFormLayoutGroupAsync(string layoutKey, string groupKey, FormLayoutGroupMutationRequest request, string username, CancellationToken cancellationToken)
    {
        layoutKey = NormalizeCatalogKey(layoutKey); groupKey = NormalizeLayoutPartKey(groupKey); ValidateLayoutText(request.Title, "x"); if (request.Sequence < 0) throw new ArgumentException("Group sequence must be non-negative.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.Transaction=transaction; command.CommandText = "insert into form_layout_groups(layout_key,group_key,title,sequence,active,updated_at,updated_by) values(@layout,@key,@title,@sequence,@active,now(),@user) on conflict(layout_key,group_key) do update set title=excluded.title,sequence=excluded.sequence,active=excluded.active,updated_at=now(),updated_by=excluded.updated_by;"; command.Parameters.AddWithValue("layout", layoutKey); command.Parameters.AddWithValue("key", groupKey); command.Parameters.AddWithValue("title", request.Title.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("user", username); try { await command.ExecuteNonQueryAsync(cancellationToken); await SnapshotFormLayoutAsync(connection,transaction,layoutKey,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23503") { throw new ArgumentException("Layout was not found."); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Group sequence must be unique within a layout."); } await transaction.CommitAsync(cancellationToken); return await GetFormLayoutAsync(layoutKey, cancellationToken);
    }

    public async Task<FormLayoutDetailResponse> UpsertFormLayoutFieldAsync(string layoutKey, string fieldKey, FormLayoutFieldMutationRequest request, string username, CancellationToken cancellationToken)
    {
        layoutKey = NormalizeCatalogKey(layoutKey); fieldKey = NormalizeLayoutPartKey(fieldKey); var groupKey = NormalizeLayoutPartKey(request.GroupKey); if (string.IsNullOrWhiteSpace(request.Label) || request.Label.Trim().Length > 120 || request.Sequence < 0 || request.MaxLength is < 0 or > 4096 || request.FieldType is not ("text" or "date" or "select" or "textarea" or "checkbox" or "number")) throw new ArgumentException("Field definition is invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.Transaction=transaction; command.CommandText = "insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) values(@layout,@key,@group,@label,@type,@sequence,@required,@active,@length,@list,@default,now(),@user) on conflict(layout_key,field_key) do update set group_key=excluded.group_key,label=excluded.label,field_type=excluded.field_type,sequence=excluded.sequence,required=excluded.required,active=excluded.active,max_length=excluded.max_length,list_id=excluded.list_id,default_value=excluded.default_value,updated_at=now(),updated_by=excluded.updated_by;"; command.Parameters.AddWithValue("layout", layoutKey); command.Parameters.AddWithValue("key", fieldKey); command.Parameters.AddWithValue("group", groupKey); command.Parameters.AddWithValue("label", request.Label.Trim()); command.Parameters.AddWithValue("type", request.FieldType); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("required", request.Required); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("length", request.MaxLength); command.Parameters.AddWithValue("list", request.ListId?.Trim() ?? ""); command.Parameters.AddWithValue("default", request.DefaultValue?.Trim() ?? ""); command.Parameters.AddWithValue("user", username); try { await command.ExecuteNonQueryAsync(cancellationToken); await SnapshotFormLayoutAsync(connection,transaction,layoutKey,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23503") { throw new ArgumentException("Group was not found in the selected layout."); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Field sequence must be unique within a group."); } await transaction.CommitAsync(cancellationToken); return await GetFormLayoutAsync(layoutKey, cancellationToken);
    }

    private static FormLayoutItem ReadLayout(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4), reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetString(6));
    private static async Task SnapshotFormLayoutAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into form_layout_revisions(layout_key,title,mapping,sequence,active,groups,fields,action,restored_from_revision_id,occurred_at,username) select l.layout_key,l.title,l.mapping,l.sequence,l.active,coalesce((select jsonb_agg(jsonb_build_object('key',g.group_key,'title',g.title,'sequence',g.sequence,'active',g.active) order by g.sequence,g.group_key) from form_layout_groups g where g.layout_key=l.layout_key),'[]'::jsonb),coalesce((select jsonb_agg(jsonb_build_object('key',f.field_key,'groupKey',f.group_key,'label',f.label,'fieldType',f.field_type,'sequence',f.sequence,'required',f.required,'active',f.active,'maxLength',f.max_length,'listId',f.list_id,'defaultValue',f.default_value) order by f.group_key,f.sequence,f.field_key) from form_layout_fields f where f.layout_key=l.layout_key),'[]'::jsonb),@action,@restored,now(),@user from form_layouts l where l.layout_key=@key;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken); }
    public async Task<FormLayoutHistoryResponse> GetFormLayoutHistoryAsync(string key,CancellationToken cancellationToken)
    { var layoutKey=NormalizeCatalogKey(key);var detail=await GetFormLayoutAsync(layoutKey,cancellationToken);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select revision_id,title,mapping,sequence,active,jsonb_array_length(groups),jsonb_array_length(fields),action,restored_from_revision_id,occurred_at,username from form_layout_revisions where layout_key=@key order by occurred_at desc,revision_id desc;";command.Parameters.AddWithValue("key",layoutKey);var revisions=new List<FormLayoutRevision>();await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3),reader.GetBoolean(4),reader.GetInt32(5),reader.GetInt32(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetInt64(8),reader.GetFieldValue<DateTimeOffset>(9).ToString("O"),reader.GetString(10)));return new(detail,revisions); }
    public async Task<FormLayoutHistoryResponse> RollbackFormLayoutAsync(string key,long revisionId,string username,CancellationToken cancellationToken)
    { var layoutKey=NormalizeCatalogKey(key);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);await using(var check=connection.CreateCommand()){check.Transaction=transaction;check.CommandText="select exists(select 1 from form_layout_revisions where layout_key=@key and revision_id=@revision);";check.Parameters.AddWithValue("key",layoutKey);check.Parameters.AddWithValue("revision",revisionId);if(!(bool)(await check.ExecuteScalarAsync(cancellationToken)??false))throw new ArgumentException("The requested revision was not found for this layout.");}await using(var restore=connection.CreateCommand()){restore.Transaction=transaction;restore.CommandText="delete from form_layout_fields where layout_key=@key; delete from form_layout_groups where layout_key=@key; update form_layouts set title=(select title from form_layout_revisions where layout_key=@key and revision_id=@revision),mapping=(select mapping from form_layout_revisions where layout_key=@key and revision_id=@revision),sequence=(select sequence from form_layout_revisions where layout_key=@key and revision_id=@revision),active=(select active from form_layout_revisions where layout_key=@key and revision_id=@revision),updated_at=now(),updated_by=@user where layout_key=@key; insert into form_layout_groups(layout_key,group_key,title,sequence,active,updated_at,updated_by) select @key,item.key,item.title,item.sequence,item.active,now(),@user from jsonb_to_recordset((select groups from form_layout_revisions where layout_key=@key and revision_id=@revision)) as item(key text,title text,sequence integer,active boolean); insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) select @key,item.key,item.\"groupKey\",item.label,item.\"fieldType\",item.sequence,item.required,item.active,item.\"maxLength\",item.\"listId\",item.\"defaultValue\",now(),@user from jsonb_to_recordset((select fields from form_layout_revisions where layout_key=@key and revision_id=@revision)) as item(key text,\"groupKey\" text,label text,\"fieldType\" text,sequence integer,required boolean,active boolean,\"maxLength\" integer,\"listId\" text,\"defaultValue\" text);";restore.Parameters.AddWithValue("key",layoutKey);restore.Parameters.AddWithValue("revision",revisionId);restore.Parameters.AddWithValue("user",username);await restore.ExecuteNonQueryAsync(cancellationToken);}await SnapshotFormLayoutAsync(connection,transaction,layoutKey,username,"rolled-back",revisionId,cancellationToken);await transaction.CommitAsync(cancellationToken);return await GetFormLayoutHistoryAsync(layoutKey,cancellationToken); }

    public async Task<FormLayoutChangeRequestsResponse> GetFormLayoutChangeRequestsAsync(string? status, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("Change-request paging is invalid.");
        var normalizedStatus = NormalizeFormLayoutChangeRequestStatus(status);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = "select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from form_layout_change_requests;";
        count.Parameters.AddWithValue("status", normalizedStatus);
        var counts = new FormLayoutChangeRequestCounts(0, 0, 0, 0, 0, 0); var total = 0;
        await using (var reader = await count.ExecuteReaderAsync(cancellationToken)) if (await reader.ReadAsync(cancellationToken)) { counts = new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)); total = reader.GetInt32(6); }
        await using var command = connection.CreateCommand();
        command.CommandText = "select request_id,layout_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_layout_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc offset @offset limit @limit;";
        command.Parameters.AddWithValue("status", normalizedStatus); command.Parameters.AddWithValue("offset", offset); command.Parameters.AddWithValue("limit", limit);
        var items = new List<FormLayoutChangeRequestItem>(); await using var result = await command.ExecuteReaderAsync(cancellationToken); while (await result.ReadAsync(cancellationToken)) items.Add(ReadFormLayoutChangeRequest(result));
        return new(items, total, items.Count, offset, limit, normalizedStatus, counts);
    }

    public async Task<FormLayoutChangeRequestDetailResponse> CreateFormLayoutChangeRequestAsync(FormLayoutChangeRequestCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var proposed = NormalizeFormLayoutDefinition(request); var reason = ValidateChangeRequestReason(request.Reason, true)!; var requestId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var baseline = await GetFormLayoutDefinitionForUpdateAsync(connection, transaction, proposed.Key, cancellationToken);
        if (baseline is not null && SerializeFormLayoutDefinition(baseline.Definition) == SerializeFormLayoutDefinition(proposed)) throw new ArgumentException("The proposed form layout must differ from the active layout.");
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction; duplicate.CommandText = "select exists(select 1 from form_layout_change_requests where layout_key=@key and status in ('draft','submitted','approved'));"; duplicate.Parameters.AddWithValue("key", proposed.Key);
            if ((bool)(await duplicate.ExecuteScalarAsync(cancellationToken) ?? false)) throw new FormLayoutChangeRequestConflictException("An open change request already exists for this form layout.");
        }
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction; create.CommandText = "insert into form_layout_change_requests(request_id,layout_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@kind,@proposed,@baseline,@baselineUpdated,@reason,'draft',0,now(),@user,now(),@user);";
            create.Parameters.AddWithValue("id", requestId); create.Parameters.AddWithValue("key", proposed.Key); create.Parameters.AddWithValue("kind", baseline is null ? "create" : "update"); create.Parameters.Add("proposed", NpgsqlDbType.Jsonb).Value = SerializeFormLayoutDefinition(proposed); create.Parameters.Add("baseline", NpgsqlDbType.Jsonb).Value = baseline is null ? DBNull.Value : SerializeFormLayoutDefinition(baseline.Definition); create.Parameters.AddWithValue("baselineUpdated", (object?)baseline?.UpdatedAt ?? DBNull.Value); create.Parameters.AddWithValue("reason", reason); create.Parameters.AddWithValue("user", username); await create.ExecuteNonQueryAsync(cancellationToken);
        }
        await WriteFormLayoutChangeRequestEventAsync(connection, transaction, requestId, "created", reason, username, cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetFormLayoutChangeRequestAsync(requestId, cancellationToken);
    }

    public async Task<FormLayoutChangeRequestDetailResponse> GetFormLayoutChangeRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var request = await GetFormLayoutChangeRequestAsync(connection, requestId, cancellationToken) ?? throw new ArgumentException("The requested form-layout change request was not found.");
        FormLayoutDetailResponse? active = null; try { active = await GetFormLayoutAsync(request.LayoutKey, cancellationToken); } catch (ArgumentException) { }
        await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from form_layout_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", requestId);
        var events = new List<FormLayoutChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4)));
        return new(request, active, events);
    }

    public Task<FormLayoutChangeRequestDetailResponse> SubmitFormLayoutChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormLayoutChangeRequestAsync(id, ["draft"], "submitted", note, false, version, user, token);
    public Task<FormLayoutChangeRequestDetailResponse> ApproveFormLayoutChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormLayoutChangeRequestAsync(id, ["submitted"], "approved", note, false, version, user, token);
    public Task<FormLayoutChangeRequestDetailResponse> RejectFormLayoutChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormLayoutChangeRequestAsync(id, ["submitted"], "rejected", note, true, version, user, token);
    public Task<FormLayoutChangeRequestDetailResponse> ActivateFormLayoutChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormLayoutChangeRequestAsync(id, ["approved"], "activated", note, false, version, user, token);
    public Task<FormLayoutChangeRequestDetailResponse> CancelFormLayoutChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormLayoutChangeRequestAsync(id, ["draft", "submitted", "approved"], "cancelled", note, true, version, user, token);

    private async Task<FormLayoutChangeRequestDetailResponse> TransitionFormLayoutChangeRequestAsync(Guid requestId, string[] expectedStatuses, string nextStatus, string? note, bool noteRequired, int? expectedVersion, string username, CancellationToken cancellationToken)
    {
        var normalizedNote = ValidateChangeRequestReason(note, noteRequired); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        FormLayoutChangeRequestItem request;
        await using (var current = connection.CreateCommand()) { current.Transaction = transaction; current.CommandText = "select request_id,layout_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_layout_change_requests where request_id=@id for update;"; current.Parameters.AddWithValue("id", requestId); await using var reader = await current.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("The requested form-layout change request was not found."); request = ReadFormLayoutChangeRequest(reader); }
        if (!expectedStatuses.Contains(request.Status, StringComparer.Ordinal)) throw new FormLayoutChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {nextStatus}.");
        if (expectedVersion is not null && expectedVersion != request.Version) throw new FormLayoutChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");
        if (nextStatus == "activated")
        {
            var current = await GetFormLayoutDefinitionForUpdateAsync(connection, transaction, request.LayoutKey, cancellationToken);
            if (request.ChangeKind == "create") { if (current is not null) throw new FormLayoutChangeRequestConflictException("The form layout was created after this request was drafted. Cancel this stale request and create a new proposal."); }
            else if (current is null || request.BaselineDefinition is null || SerializeFormLayoutDefinition(current.Definition) != SerializeFormLayoutDefinition(request.BaselineDefinition) || current.UpdatedAt.ToUniversalTime() != DateTimeOffset.Parse(request.BaselineUpdatedAt!).ToUniversalTime()) throw new FormLayoutChangeRequestConflictException("The active form layout changed after this request was created. Cancel this stale request and create a new proposal.");
            try { await ApplyFormLayoutDefinitionAsync(connection, transaction, request.ProposedDefinition, username, request.ChangeKind == "create", cancellationToken); await SnapshotFormLayoutAsync(connection, transaction, request.LayoutKey, username, "activated", null, cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FormLayoutChangeRequestConflictException("The form layout conflicts with an active layout, group, or field sequence."); }
        }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update form_layout_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("id", requestId); update.Parameters.AddWithValue("status", nextStatus); update.Parameters.AddWithValue("user", username); await update.ExecuteNonQueryAsync(cancellationToken); }
        await WriteFormLayoutChangeRequestEventAsync(connection, transaction, requestId, nextStatus, normalizedNote, username, cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetFormLayoutChangeRequestAsync(requestId, cancellationToken);
    }

    private static string NormalizeFormLayoutChangeRequestStatus(string? status)
    { var normalized = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant(); if (normalized is not ("all" or "open" or "draft" or "submitted" or "approved" or "rejected" or "activated" or "cancelled")) throw new ArgumentException("Change-request status is not supported."); return normalized; }
    private static FormLayoutDefinition NormalizeFormLayoutDefinition(FormLayoutChangeRequestCreateRequest request)
    {
        var key = NormalizeCatalogKey(request.Key); ValidateLayoutText(request.Title, request.Mapping); if (request.Sequence < 0) throw new ArgumentException("Layout sequence must be non-negative.");
        var groups = (request.Groups ?? []).Select(group => new FormLayoutDefinitionGroup(NormalizeLayoutPartKey(group.Key), ValidateFormLayoutLabel(group.Title, "Group title"), group.Sequence, group.Active)).OrderBy(group => group.Sequence).ThenBy(group => group.Key, StringComparer.Ordinal).ToArray();
        if (groups.GroupBy(group => group.Key, StringComparer.Ordinal).Any(group => group.Count() > 1) || groups.GroupBy(group => group.Sequence).Any(group => group.Count() > 1)) throw new ArgumentException("Form-layout group keys and sequences must be unique.");
        var groupKeys = groups.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var fields = (request.Fields ?? []).Select(field => new FormLayoutDefinitionField(NormalizeLayoutPartKey(field.Key), NormalizeLayoutPartKey(field.GroupKey), ValidateFormLayoutLabel(field.Label, "Field label"), ValidateFormLayoutFieldType(field.FieldType), field.Sequence, field.Required, field.Active, ValidateFormLayoutMaxLength(field.MaxLength), field.ListId?.Trim() ?? "", field.DefaultValue?.Trim() ?? "")).OrderBy(field => field.GroupKey, StringComparer.Ordinal).ThenBy(field => field.Sequence).ThenBy(field => field.Key, StringComparer.Ordinal).ToArray();
        if (fields.Any(field => !groupKeys.Contains(field.GroupKey))) throw new ArgumentException("Every form-layout field must belong to a proposed group.");
        if (fields.GroupBy(field => field.Key, StringComparer.Ordinal).Any(group => group.Count() > 1) || fields.GroupBy(field => (field.GroupKey, field.Sequence)).Any(group => group.Count() > 1)) throw new ArgumentException("Form-layout field keys and sequences must be unique.");
        return new(key, request.Title.Trim(), request.Mapping.Trim(), request.Sequence, request.Active, groups, fields);
    }
    private static string ValidateFormLayoutLabel(string value, string label) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 120) throw new ArgumentException($"{label} is invalid."); return value.Trim(); }
    private static string ValidateFormLayoutFieldType(string value) { if (value is not ("text" or "date" or "select" or "textarea" or "checkbox" or "number")) throw new ArgumentException("Field type is invalid."); return value; }
    private static int ValidateFormLayoutMaxLength(int value) { if (value is < 0 or > 4096) throw new ArgumentException("Field maximum length is invalid."); return value; }
    private static string SerializeFormLayoutDefinition(FormLayoutDefinition definition) => JsonSerializer.Serialize(definition, PortalProfileChangeJsonOptions);
    private static FormLayoutDefinition ReadFormLayoutDefinition(string json) => JsonSerializer.Deserialize<FormLayoutDefinition>(json, PortalProfileChangeJsonOptions) ?? throw new InvalidOperationException("The stored form-layout definition is invalid.");
    private static FormLayoutChangeRequestItem ReadFormLayoutChangeRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), ReadFormLayoutDefinition(reader.GetString(3)), reader.IsDBNull(4) ? null : ReadFormLayoutDefinition(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11).ToString("O"), reader.GetString(12));
    private static async Task<FormLayoutChangeRequestItem?> GetFormLayoutChangeRequestAsync(NpgsqlConnection connection, Guid requestId, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.CommandText = "select request_id,layout_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_layout_change_requests where request_id=@id;"; command.Parameters.AddWithValue("id", requestId); await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? ReadFormLayoutChangeRequest(reader) : null; }
    private static async Task WriteFormLayoutChangeRequestEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requestId, string action, string? note, string username, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_layout_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", requestId); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", username); await command.ExecuteNonQueryAsync(cancellationToken); }
    private sealed record FormLayoutCurrent(FormLayoutDefinition Definition, DateTimeOffset UpdatedAt);
    private static async Task<FormLayoutCurrent?> GetFormLayoutDefinitionForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var parent = connection.CreateCommand(); parent.Transaction = transaction; parent.CommandText = "select title,mapping,sequence,active,updated_at from form_layouts where layout_key=@key for update;"; parent.Parameters.AddWithValue("key", key); await using var parentReader = await parent.ExecuteReaderAsync(cancellationToken); if (!await parentReader.ReadAsync(cancellationToken)) return null; var title = parentReader.GetString(0); var mapping = parentReader.GetString(1); var sequence = parentReader.GetInt32(2); var active = parentReader.GetBoolean(3); var updatedAt = parentReader.GetFieldValue<DateTimeOffset>(4); await parentReader.DisposeAsync();
        var groups = new List<FormLayoutDefinitionGroup>(); await using (var groupCommand = connection.CreateCommand()) { groupCommand.Transaction = transaction; groupCommand.CommandText = "select group_key,title,sequence,active from form_layout_groups where layout_key=@key order by sequence,group_key;"; groupCommand.Parameters.AddWithValue("key", key); await using var reader = await groupCommand.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) groups.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3))); }
        var fields = new List<FormLayoutDefinitionField>(); await using (var fieldCommand = connection.CreateCommand()) { fieldCommand.Transaction = transaction; fieldCommand.CommandText = "select field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value from form_layout_fields where layout_key=@key order by group_key,sequence,field_key;"; fieldCommand.Parameters.AddWithValue("key", key); await using var reader = await fieldCommand.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) fields.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5), reader.GetBoolean(6), reader.GetInt32(7), reader.IsDBNull(8) ? "" : reader.GetString(8), reader.IsDBNull(9) ? "" : reader.GetString(9))); }
        return new(new(key, title, mapping, sequence, active, groups, fields), updatedAt);
    }
    private static async Task ApplyFormLayoutDefinitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, FormLayoutDefinition definition, string username, bool create, CancellationToken cancellationToken)
    {
        await using (var parent = connection.CreateCommand()) { parent.Transaction = transaction; parent.CommandText = create ? "insert into form_layouts(layout_key,title,mapping,sequence,active,updated_at,updated_by) values(@key,@title,@mapping,@sequence,@active,now(),@user);" : "delete from form_layout_fields where layout_key=@key; delete from form_layout_groups where layout_key=@key; update form_layouts set title=@title,mapping=@mapping,sequence=@sequence,active=@active,updated_at=now(),updated_by=@user where layout_key=@key;"; parent.Parameters.AddWithValue("key", definition.Key); parent.Parameters.AddWithValue("title", definition.Title); parent.Parameters.AddWithValue("mapping", definition.Mapping); parent.Parameters.AddWithValue("sequence", definition.Sequence); parent.Parameters.AddWithValue("active", definition.Active); parent.Parameters.AddWithValue("user", username); await parent.ExecuteNonQueryAsync(cancellationToken); }
        foreach (var group in definition.Groups) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_layout_groups(layout_key,group_key,title,sequence,active,updated_at,updated_by) values(@layout,@key,@title,@sequence,@active,now(),@user);"; command.Parameters.AddWithValue("layout", definition.Key); command.Parameters.AddWithValue("key", group.Key); command.Parameters.AddWithValue("title", group.Title); command.Parameters.AddWithValue("sequence", group.Sequence); command.Parameters.AddWithValue("active", group.Active); command.Parameters.AddWithValue("user", username); await command.ExecuteNonQueryAsync(cancellationToken); }
        foreach (var field in definition.Fields) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) values(@layout,@key,@group,@label,@type,@sequence,@required,@active,@length,@list,@default,now(),@user);"; command.Parameters.AddWithValue("layout", definition.Key); command.Parameters.AddWithValue("key", field.Key); command.Parameters.AddWithValue("group", field.GroupKey); command.Parameters.AddWithValue("label", field.Label); command.Parameters.AddWithValue("type", field.FieldType); command.Parameters.AddWithValue("sequence", field.Sequence); command.Parameters.AddWithValue("required", field.Required); command.Parameters.AddWithValue("active", field.Active); command.Parameters.AddWithValue("length", field.MaxLength); command.Parameters.AddWithValue("list", field.ListId ?? ""); command.Parameters.AddWithValue("default", field.DefaultValue ?? ""); command.Parameters.AddWithValue("user", username); await command.ExecuteNonQueryAsync(cancellationToken); }
    }
    private static void ValidateLayoutText(string title, string mapping) { if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120 || string.IsNullOrWhiteSpace(mapping) || mapping.Trim().Length > 64) throw new ArgumentException("Layout title or mapping is invalid."); }
    private static string NormalizeLayoutPartKey(string key) { var normalized=key.Trim(); if (normalized.Length is < 1 or > 64 || !normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')) throw new ArgumentException("Layout group and field keys must be 1-64 letters, numbers, or underscores."); return normalized; }

    public async Task<FormOptionListCatalogResponse> GetFormOptionListsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "select l.list_key,l.title,l.active,count(v.option_key),l.updated_at,l.updated_by from form_option_lists l left join form_option_values v on v.list_key=l.list_key group by l.list_key,l.title,l.active,l.updated_at,l.updated_by order by l.title,l.list_key;";
        var lists = new List<FormOptionListItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) lists.Add(new(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), Convert.ToInt32(reader.GetInt64(3)), reader.GetFieldValue<DateTimeOffset>(4).ToString("O"), reader.GetString(5)));
        return new(lists);
    }

    public async Task<FormOptionListDetailResponse> GetFormOptionListAsync(string key, CancellationToken cancellationToken)
    {
        var listKey = NormalizeFormOptionListKey(key); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var listCommand = connection.CreateCommand(); listCommand.CommandText = "select list_key,title,active,updated_at,updated_by from form_option_lists where list_key=@key;"; listCommand.Parameters.AddWithValue("key", listKey);
        await using var listReader = await listCommand.ExecuteReaderAsync(cancellationToken); if (!await listReader.ReadAsync(cancellationToken)) throw new ArgumentException("Form option list was not found.");
        var list = new FormOptionListItem(listReader.GetString(0), listReader.GetString(1), listReader.GetBoolean(2), 0, listReader.GetFieldValue<DateTimeOffset>(3).ToString("O"), listReader.GetString(4)); await listReader.CloseAsync();
        await using var optionCommand = connection.CreateCommand(); optionCommand.CommandText = "select option_key,title,sequence,is_default,active,option_value,updated_at,updated_by from form_option_values where list_key=@key order by sequence,option_key;"; optionCommand.Parameters.AddWithValue("key", listKey);
        var options = new List<FormOptionValueItem>(); await using var optionReader = await optionCommand.ExecuteReaderAsync(cancellationToken); while (await optionReader.ReadAsync(cancellationToken)) options.Add(new(optionReader.GetString(0), optionReader.GetString(1), optionReader.GetInt32(2), optionReader.GetBoolean(3), optionReader.GetBoolean(4), optionReader.GetString(5), optionReader.GetFieldValue<DateTimeOffset>(6).ToString("O"), optionReader.GetString(7)));
        return new(list with { OptionCount = options.Count }, options);
    }

    public async Task<FormOptionListDetailResponse> UpsertFormOptionListAsync(string key, FormOptionListMutationRequest request, string username, CancellationToken cancellationToken)
    {
        var listKey = NormalizeFormOptionListKey(key); if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120) throw new ArgumentException("List title is invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText = "insert into form_option_lists(list_key,title,active,updated_at,updated_by) values(@key,@title,@active,now(),@user) on conflict(list_key) do update set title=excluded.title,active=excluded.active,updated_at=now(),updated_by=excluded.updated_by;";
        command.Parameters.AddWithValue("key", listKey); command.Parameters.AddWithValue("title", request.Title.Trim()); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("user", username); await command.ExecuteNonQueryAsync(cancellationToken); await SnapshotFormOptionListAsync(connection,transaction,listKey,username,"updated",null,cancellationToken); await transaction.CommitAsync(cancellationToken);
        return await GetFormOptionListAsync(listKey, cancellationToken);
    }

    public async Task<FormOptionListDetailResponse> UpsertFormOptionValueAsync(string listKey, string optionKey, FormOptionValueMutationRequest request, string username, CancellationToken cancellationToken)
    {
        listKey = NormalizeFormOptionListKey(listKey); optionKey = NormalizeFormOptionKey(optionKey);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255 || request.Sequence < 0 || request.Value?.Trim().Length > 255) throw new ArgumentException("List option definition is invalid.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "insert into form_option_values(list_key,option_key,title,sequence,is_default,active,option_value,updated_at,updated_by) values(@list,@key,@title,@sequence,@default,@active,@value,now(),@user) on conflict(list_key,option_key) do update set title=excluded.title,sequence=excluded.sequence,is_default=excluded.is_default,active=excluded.active,option_value=excluded.option_value,updated_at=now(),updated_by=excluded.updated_by; update form_option_lists set updated_at=now(),updated_by=@user where list_key=@list;";
        command.Parameters.AddWithValue("list", listKey); command.Parameters.AddWithValue("key", optionKey); command.Parameters.AddWithValue("title", request.Title.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("default", request.IsDefault); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("value", request.Value?.Trim() ?? ""); command.Parameters.AddWithValue("user", username);
        try { if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new ArgumentException("Form option list was not found."); await SnapshotFormOptionListAsync(connection,transaction,listKey,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23503") { throw new ArgumentException("Form option list was not found."); }
        await transaction.CommitAsync(cancellationToken); return await GetFormOptionListAsync(listKey, cancellationToken);
    }

    public async Task<FormOptionListHistoryResponse> GetFormOptionListHistoryAsync(string key, CancellationToken cancellationToken)
    { var listKey=NormalizeFormOptionListKey(key); var detail=await GetFormOptionListAsync(listKey,cancellationToken); await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); await using var command=connection.CreateCommand(); command.CommandText="select revision_id,title,active,jsonb_array_length(options),action,restored_from_revision_id,occurred_at,username from form_option_list_revisions where list_key=@key order by occurred_at desc,revision_id desc;";command.Parameters.AddWithValue("key",listKey);var revisions=new List<FormOptionListRevision>();await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetBoolean(2),reader.GetInt32(3),reader.GetString(4),reader.IsDBNull(5)?null:reader.GetInt64(5),reader.GetFieldValue<DateTimeOffset>(6).ToString("O"),reader.GetString(7)));return new(detail,revisions); }

    public async Task<FormOptionListHistoryResponse> RollbackFormOptionListAsync(string key,long revisionId,string username,CancellationToken cancellationToken)
    { var listKey=NormalizeFormOptionListKey(key);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);await using(var lockCommand=connection.CreateCommand()){lockCommand.Transaction=transaction;lockCommand.CommandText="select 1 from form_option_lists where list_key=@key for update;";lockCommand.Parameters.AddWithValue("key",listKey);if(await lockCommand.ExecuteScalarAsync(cancellationToken) is null)throw new ArgumentException("Form option list was not found.");}await using(var revisionCommand=connection.CreateCommand()){revisionCommand.Transaction=transaction;revisionCommand.CommandText="select exists(select 1 from form_option_list_revisions where list_key=@key and revision_id=@revision);";revisionCommand.Parameters.AddWithValue("key",listKey);revisionCommand.Parameters.AddWithValue("revision",revisionId);if(!(bool)(await revisionCommand.ExecuteScalarAsync(cancellationToken)??false))throw new ArgumentException("The requested revision was not found for this list.");}await using(var restore=connection.CreateCommand()){restore.Transaction=transaction;restore.CommandText="with revision as (select title,active,options from form_option_list_revisions where list_key=@key and revision_id=@revision) update form_option_lists set title=(select title from revision),active=(select active from revision),updated_at=now(),updated_by=@user where list_key=@key; delete from form_option_values where list_key=@key; insert into form_option_values(list_key,option_key,title,sequence,is_default,active,option_value,updated_at,updated_by) select @key,item.key,item.title,item.sequence,item.\"isDefault\",item.active,item.value,now(),@user from jsonb_to_recordset((select options from form_option_list_revisions where list_key=@key and revision_id=@revision)) as item(key text,title text,sequence integer,\"isDefault\" boolean,active boolean,value text);";restore.Parameters.AddWithValue("key",listKey);restore.Parameters.AddWithValue("revision",revisionId);restore.Parameters.AddWithValue("user",username);await restore.ExecuteNonQueryAsync(cancellationToken);}await SnapshotFormOptionListAsync(connection,transaction,listKey,username,"rolled-back",revisionId,cancellationToken);await transaction.CommitAsync(cancellationToken);return await GetFormOptionListHistoryAsync(listKey,cancellationToken); }

    private static async Task SnapshotFormOptionListAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into form_option_list_revisions(list_key,title,active,options,action,restored_from_revision_id,occurred_at,username) select l.list_key,l.title,l.active,coalesce((select jsonb_agg(jsonb_build_object('key',v.option_key,'title',v.title,'sequence',v.sequence,'isDefault',v.is_default,'active',v.active,'value',v.option_value) order by v.sequence,v.option_key) from form_option_values v where v.list_key=l.list_key),'[]'::jsonb),@action,@restored,now(),@user from form_option_lists l where l.list_key=@key;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<FormOptionListChangeRequestsResponse> GetFormOptionListChangeRequestsAsync(string? status, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("Change-request paging is invalid."); var normalized = NormalizeOptionListChangeStatus(status);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var count = connection.CreateCommand(); count.CommandText = "select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from form_option_list_change_requests;"; count.Parameters.AddWithValue("status", normalized);
        var counts = new FormOptionListChangeRequestCounts(0, 0, 0, 0, 0, 0); var total = 0; await using (var reader = await count.ExecuteReaderAsync(cancellationToken)) if (await reader.ReadAsync(cancellationToken)) { counts = new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)); total = reader.GetInt32(6); }
        await using var command = connection.CreateCommand(); command.CommandText = "select request_id,list_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_option_list_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc offset @offset limit @limit;"; command.Parameters.AddWithValue("status", normalized); command.Parameters.AddWithValue("offset", offset); command.Parameters.AddWithValue("limit", limit); var requests = new List<FormOptionListChangeRequestItem>(); await using var result = await command.ExecuteReaderAsync(cancellationToken); while (await result.ReadAsync(cancellationToken)) requests.Add(ReadFormOptionListChangeRequest(result)); return new(requests, total, requests.Count, offset, limit, normalized, counts);
    }

    public async Task<FormOptionListChangeRequestDetailResponse> CreateFormOptionListChangeRequestAsync(FormOptionListChangeRequestCreateRequest request, string username, CancellationToken cancellationToken)
    {
        var proposed = NormalizeFormOptionListDefinition(request); var reason = ValidateChangeRequestReason(request.Reason, true)!; var id = Guid.NewGuid(); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); var baseline = await GetFormOptionListForUpdateAsync(connection, transaction, proposed.Key, cancellationToken);
        if (baseline is not null && SerializeFormOptionListDefinition(baseline.Definition) == SerializeFormOptionListDefinition(proposed)) throw new ArgumentException("The proposed option list must differ from the active list.");
        await using (var duplicate = connection.CreateCommand()) { duplicate.Transaction = transaction; duplicate.CommandText = "select exists(select 1 from form_option_list_change_requests where list_key=@key and status in ('draft','submitted','approved'));"; duplicate.Parameters.AddWithValue("key", proposed.Key); if ((bool)(await duplicate.ExecuteScalarAsync(cancellationToken) ?? false)) throw new FormOptionListChangeRequestConflictException("An open change request already exists for this option list."); }
        await using (var create = connection.CreateCommand()) { create.Transaction = transaction; create.CommandText = "insert into form_option_list_change_requests(request_id,list_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@kind,@proposed,@baseline,@updated,@reason,'draft',0,now(),@user,now(),@user);"; create.Parameters.AddWithValue("id", id); create.Parameters.AddWithValue("key", proposed.Key); create.Parameters.AddWithValue("kind", baseline is null ? "create" : "update"); create.Parameters.Add("proposed", NpgsqlDbType.Jsonb).Value = SerializeFormOptionListDefinition(proposed); create.Parameters.Add("baseline", NpgsqlDbType.Jsonb).Value = baseline is null ? DBNull.Value : SerializeFormOptionListDefinition(baseline.Definition); create.Parameters.AddWithValue("updated", (object?)baseline?.UpdatedAt ?? DBNull.Value); create.Parameters.AddWithValue("reason", reason); create.Parameters.AddWithValue("user", username); await create.ExecuteNonQueryAsync(cancellationToken); }
        await WriteFormOptionListChangeRequestEventAsync(connection, transaction, id, "created", reason, username, cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetFormOptionListChangeRequestAsync(id, cancellationToken);
    }

    public async Task<FormOptionListChangeRequestDetailResponse> GetFormOptionListChangeRequestAsync(Guid id, CancellationToken cancellationToken)
    { await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); var request = await GetFormOptionListChangeRequestAsync(connection, id, cancellationToken) ?? throw new ArgumentException("The requested option-list change request was not found."); FormOptionListDetailResponse? active = null; try { active = await GetFormOptionListAsync(request.ListKey, cancellationToken); } catch (ArgumentException) { } await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from form_option_list_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", id); var events = new List<FormOptionListChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4))); return new(request, active, events); }
    public Task<FormOptionListChangeRequestDetailResponse> SubmitFormOptionListChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormOptionListChangeRequestAsync(id, ["draft"], "submitted", note, false, version, user, token);
    public Task<FormOptionListChangeRequestDetailResponse> ApproveFormOptionListChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormOptionListChangeRequestAsync(id, ["submitted"], "approved", note, false, version, user, token);
    public Task<FormOptionListChangeRequestDetailResponse> RejectFormOptionListChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormOptionListChangeRequestAsync(id, ["submitted"], "rejected", note, true, version, user, token);
    public Task<FormOptionListChangeRequestDetailResponse> ActivateFormOptionListChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormOptionListChangeRequestAsync(id, ["approved"], "activated", note, false, version, user, token);
    public Task<FormOptionListChangeRequestDetailResponse> CancelFormOptionListChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionFormOptionListChangeRequestAsync(id, ["draft", "submitted", "approved"], "cancelled", note, true, version, user, token);
    private async Task<FormOptionListChangeRequestDetailResponse> TransitionFormOptionListChangeRequestAsync(Guid id, string[] expected, string next, string? note, bool noteRequired, int? version, string user, CancellationToken token)
    {
        var normalizedNote = ValidateChangeRequestReason(note, noteRequired); await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token); FormOptionListChangeRequestItem request; await using (var current = connection.CreateCommand()) { current.Transaction = transaction; current.CommandText = "select request_id,list_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_option_list_change_requests where request_id=@id for update;"; current.Parameters.AddWithValue("id", id); await using var reader = await current.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ArgumentException("The requested option-list change request was not found."); request = ReadFormOptionListChangeRequest(reader); }
        if (!expected.Contains(request.Status, StringComparer.Ordinal)) throw new FormOptionListChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {next}."); if (version is not null && version != request.Version) throw new FormOptionListChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");
        if (next == "activated") { var current = await GetFormOptionListForUpdateAsync(connection, transaction, request.ListKey, token); if (request.ChangeKind == "create") { if (current is not null) throw new FormOptionListChangeRequestConflictException("The option list was created after this request was drafted. Cancel this stale request and create a new proposal."); } else if (current is null || request.BaselineDefinition is null || SerializeFormOptionListDefinition(current.Definition) != SerializeFormOptionListDefinition(request.BaselineDefinition) || current.UpdatedAt.ToUniversalTime() != DateTimeOffset.Parse(request.BaselineUpdatedAt!).ToUniversalTime()) throw new FormOptionListChangeRequestConflictException("The active option list changed after this request was created. Cancel this stale request and create a new proposal."); try { await ApplyFormOptionListDefinitionAsync(connection, transaction, request.ProposedDefinition, user, request.ChangeKind == "create", token); await SnapshotFormOptionListAsync(connection, transaction, request.ListKey, user, "activated", null, token); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new FormOptionListChangeRequestConflictException("The option-list definition conflicts with an active option sequence."); } }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update form_option_list_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("user", user); await update.ExecuteNonQueryAsync(token); } await WriteFormOptionListChangeRequestEventAsync(connection, transaction, id, next, normalizedNote, user, token); await transaction.CommitAsync(token); return await GetFormOptionListChangeRequestAsync(id, token);
    }
    private static string NormalizeOptionListChangeStatus(string? status) { var normalized = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant(); if (normalized is not ("all" or "open" or "draft" or "submitted" or "approved" or "rejected" or "activated" or "cancelled")) throw new ArgumentException("Change-request status is not supported."); return normalized; }
    private static FormOptionListDefinition NormalizeFormOptionListDefinition(FormOptionListChangeRequestCreateRequest request) { var key = NormalizeFormOptionListKey(request.Key); if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120) throw new ArgumentException("List title is invalid."); var options = (request.Options ?? []).Select(option => new FormOptionListDefinitionOption(NormalizeFormOptionKey(option.Key), ValidateOptionTitle(option.Title), ValidateOptionSequence(option.Sequence), option.IsDefault, option.Active, option.Value?.Trim() ?? "")).OrderBy(option => option.Sequence).ThenBy(option => option.Key, StringComparer.Ordinal).ToArray(); if (options.GroupBy(option => option.Key, StringComparer.Ordinal).Any(group => group.Count() > 1) || options.GroupBy(option => option.Sequence).Any(group => group.Count() > 1)) throw new ArgumentException("Option keys and sequences must be unique."); if (options.Count(option => option.IsDefault) > 1) throw new ArgumentException("Only one option may be the default."); return new(key, request.Title.Trim(), request.Active, options); }
    private static string ValidateOptionTitle(string value) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 255) throw new ArgumentException("List option definition is invalid."); return value.Trim(); }
    private static int ValidateOptionSequence(int value) { if (value < 0) throw new ArgumentException("List option definition is invalid."); return value; }
    private static string SerializeFormOptionListDefinition(FormOptionListDefinition definition) => JsonSerializer.Serialize(definition, PortalProfileChangeJsonOptions);
    private static FormOptionListDefinition ReadFormOptionListDefinition(string json) => JsonSerializer.Deserialize<FormOptionListDefinition>(json, PortalProfileChangeJsonOptions) ?? throw new InvalidOperationException("The stored option-list definition is invalid.");
    private static FormOptionListChangeRequestItem ReadFormOptionListChangeRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), ReadFormOptionListDefinition(reader.GetString(3)), reader.IsDBNull(4) ? null : ReadFormOptionListDefinition(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11).ToString("O"), reader.GetString(12));
    private static async Task<FormOptionListChangeRequestItem?> GetFormOptionListChangeRequestAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "select request_id,list_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from form_option_list_change_requests where request_id=@id;"; command.Parameters.AddWithValue("id", id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadFormOptionListChangeRequest(reader) : null; }
    private static async Task WriteFormOptionListChangeRequestEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string action, string? note, string user, CancellationToken token) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_option_list_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }
    private sealed record FormOptionListCurrent(FormOptionListDefinition Definition, DateTimeOffset UpdatedAt);
    private static async Task<FormOptionListCurrent?> GetFormOptionListForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken token) { await using var parent = connection.CreateCommand(); parent.Transaction = transaction; parent.CommandText = "select title,active,updated_at from form_option_lists where list_key=@key for update;"; parent.Parameters.AddWithValue("key", key); await using var parentReader = await parent.ExecuteReaderAsync(token); if (!await parentReader.ReadAsync(token)) return null; var title = parentReader.GetString(0); var active = parentReader.GetBoolean(1); var updatedAt = parentReader.GetFieldValue<DateTimeOffset>(2); await parentReader.DisposeAsync(); var options = new List<FormOptionListDefinitionOption>(); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select option_key,title,sequence,is_default,active,option_value from form_option_values where list_key=@key order by sequence,option_key;"; command.Parameters.AddWithValue("key", key); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) options.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.IsDBNull(5) ? "" : reader.GetString(5))); return new(new(key, title, active, options), updatedAt); }
    private static async Task ApplyFormOptionListDefinitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, FormOptionListDefinition definition, string user, bool create, CancellationToken token) { await using (var parent = connection.CreateCommand()) { parent.Transaction = transaction; parent.CommandText = create ? "insert into form_option_lists(list_key,title,active,updated_at,updated_by) values(@key,@title,@active,now(),@user);" : "delete from form_option_values where list_key=@key; update form_option_lists set title=@title,active=@active,updated_at=now(),updated_by=@user where list_key=@key;"; parent.Parameters.AddWithValue("key", definition.Key); parent.Parameters.AddWithValue("title", definition.Title); parent.Parameters.AddWithValue("active", definition.Active); parent.Parameters.AddWithValue("user", user); await parent.ExecuteNonQueryAsync(token); } foreach (var option in definition.Options) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_option_values(list_key,option_key,title,sequence,is_default,active,option_value,updated_at,updated_by) values(@list,@key,@title,@sequence,@default,@active,@value,now(),@user);"; command.Parameters.AddWithValue("list", definition.Key); command.Parameters.AddWithValue("key", option.Key); command.Parameters.AddWithValue("title", option.Title); command.Parameters.AddWithValue("sequence", option.Sequence); command.Parameters.AddWithValue("default", option.IsDefault); command.Parameters.AddWithValue("active", option.Active); command.Parameters.AddWithValue("value", option.Value ?? ""); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); } }

    private static string NormalizeFormOptionListKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Length is < 2 or > 64 || !normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')) throw new ArgumentException("List key must be 2-64 lowercase letters, numbers, underscores, or hyphens.");
        return normalized;
    }

    private static string NormalizeFormOptionKey(string key)
    {
        var normalized = key.Trim();
        if (normalized.Length is < 1 or > 64 || !normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')) throw new ArgumentException("Option key must be 1-64 letters, numbers, underscores, or hyphens.");
        return normalized;
    }

    public async Task<ClinicalAlertRulesResponse> GetClinicalAlertRulesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "select rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at,updated_by from clinical_alert_rules order by sequence,rule_key;"; var rules = new List<ClinicalAlertRuleItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) rules.Add(ReadClinicalAlertRule(reader)); return new(rules);
    }

    public async Task<ClinicalAlertRulesResponse> UpsertClinicalAlertRuleAsync(string key, ClinicalAlertRuleMutationRequest request, string username, CancellationToken cancellationToken)
    {
        key = NormalizeCatalogKey(key); if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message) || request.Sequence < 0 || request.TriggerType is not ("patient" or "encounter" or "appointment") || request.TargetType is not ("banner" or "reminder") || request.Severity is not ("info" or "warning" or "critical")) throw new ArgumentException("Alert rule definition is invalid."); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.Transaction=transaction; command.CommandText = "insert into clinical_alert_rules(rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at,updated_by) values(@key,@title,@trigger,@target,@severity,@message,@sequence,@active,now(),@user) on conflict(rule_key) do update set title=excluded.title,trigger_type=excluded.trigger_type,target_type=excluded.target_type,severity=excluded.severity,message=excluded.message,sequence=excluded.sequence,active=excluded.active,updated_at=now(),updated_by=excluded.updated_by;"; command.Parameters.AddWithValue("key", key); command.Parameters.AddWithValue("title", request.Title.Trim()); command.Parameters.AddWithValue("trigger", request.TriggerType); command.Parameters.AddWithValue("target", request.TargetType); command.Parameters.AddWithValue("severity", request.Severity); command.Parameters.AddWithValue("message", request.Message.Trim()); command.Parameters.AddWithValue("sequence", request.Sequence); command.Parameters.AddWithValue("active", request.Active); command.Parameters.AddWithValue("user", username); try { await command.ExecuteNonQueryAsync(cancellationToken); await SnapshotClinicalAlertRuleAsync(connection,transaction,key,username,"updated",null,cancellationToken); } catch (PostgresException exception) when (exception.SqlState == "23505") { throw new ArgumentException("Rule sequence must be unique."); } await transaction.CommitAsync(cancellationToken); return await GetClinicalAlertRulesAsync(cancellationToken);
    }

    public async Task<ClinicalAlertRuleHistoryResponse> GetClinicalAlertRuleHistoryAsync(string key, CancellationToken cancellationToken)
    { var ruleKey=NormalizeCatalogKey(key); var rule=await GetClinicalAlertRuleAsync(ruleKey,cancellationToken); await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); await using var command=connection.CreateCommand(); command.CommandText="select revision_id,title,trigger_type,target_type,severity,message,sequence,active,action,restored_from_revision_id,occurred_at,username from clinical_alert_rule_revisions where rule_key=@key order by occurred_at desc,revision_id desc;"; command.Parameters.AddWithValue("key",ruleKey); var revisions=new List<ClinicalAlertRuleRevision>(); await using var reader=await command.ExecuteReaderAsync(cancellationToken); while(await reader.ReadAsync(cancellationToken)) revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetInt32(6),reader.GetBoolean(7),reader.GetString(8),reader.IsDBNull(9)?null:reader.GetInt64(9),reader.GetFieldValue<DateTimeOffset>(10).ToString("O"),reader.GetString(11))); return new(rule,revisions); }

    public async Task<ClinicalAlertRuleHistoryResponse> RollbackClinicalAlertRuleAsync(string key,long revisionId,string username,CancellationToken cancellationToken)
    { var ruleKey=NormalizeCatalogKey(key); await using var connection=await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction=await connection.BeginTransactionAsync(cancellationToken); await using(var check=connection.CreateCommand()){check.Transaction=transaction;check.CommandText="select exists(select 1 from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision);";check.Parameters.AddWithValue("key",ruleKey);check.Parameters.AddWithValue("revision",revisionId);if(!(bool)(await check.ExecuteScalarAsync(cancellationToken)??false))throw new ArgumentException("The requested revision was not found for this alert rule.");} await using(var restore=connection.CreateCommand()){restore.Transaction=transaction;restore.CommandText="update clinical_alert_rules set title=(select title from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),trigger_type=(select trigger_type from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),target_type=(select target_type from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),severity=(select severity from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),message=(select message from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),sequence=(select sequence from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),active=(select active from clinical_alert_rule_revisions where rule_key=@key and revision_id=@revision),updated_at=now(),updated_by=@user where rule_key=@key;";restore.Parameters.AddWithValue("key",ruleKey);restore.Parameters.AddWithValue("revision",revisionId);restore.Parameters.AddWithValue("user",username);try{if(await restore.ExecuteNonQueryAsync(cancellationToken)!=1)throw new ArgumentException("The alert rule was not found.");}catch(PostgresException exception)when(exception.SqlState=="23505"){throw new ArgumentException("Rule sequence must be unique.");}} await SnapshotClinicalAlertRuleAsync(connection,transaction,ruleKey,username,"rolled-back",revisionId,cancellationToken); await transaction.CommitAsync(cancellationToken); return await GetClinicalAlertRuleHistoryAsync(ruleKey,cancellationToken); }

    private static ClinicalAlertRuleItem ReadClinicalAlertRule(NpgsqlDataReader reader) => new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetInt32(6),reader.GetBoolean(7),reader.GetFieldValue<DateTimeOffset>(8).ToString("O"),reader.GetString(9));
    private async Task<ClinicalAlertRuleItem> GetClinicalAlertRuleAsync(string key,CancellationToken cancellationToken)
    { await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at,updated_by from clinical_alert_rules where rule_key=@key;";command.Parameters.AddWithValue("key",key);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The requested alert rule was not found.");return ReadClinicalAlertRule(reader); }
    private static async Task SnapshotClinicalAlertRuleAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into clinical_alert_rule_revisions(rule_key,title,trigger_type,target_type,severity,message,sequence,active,action,restored_from_revision_id,occurred_at,username) select rule_key,title,trigger_type,target_type,severity,message,sequence,active,@action,@restored,now(),@user from clinical_alert_rules where rule_key=@key;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<ClinicalAlertRuleChangeRequestDetailResponse> CreateClinicalAlertRuleChangeRequestAsync(ClinicalAlertRuleChangeRequestCreateRequest request, string username, CancellationToken token)
    {
        var proposed = NormalizeAlertDefinition(request); var reason = ValidateChangeRequestReason(request.Reason, true)!; var id = Guid.NewGuid(); await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token); var baseline = await GetAlertDefinitionForUpdateAsync(connection, transaction, proposed.Key, token);
        if (baseline is not null && SerializeAlertDefinition(baseline.Definition) == SerializeAlertDefinition(proposed)) throw new ArgumentException("The proposed alert rule must differ from the active rule.");
        await using (var duplicate = connection.CreateCommand()) { duplicate.Transaction = transaction; duplicate.CommandText = "select exists(select 1 from clinical_alert_rule_change_requests where rule_key=@key and status in ('draft','submitted','approved'));"; duplicate.Parameters.AddWithValue("key", proposed.Key); if ((bool)(await duplicate.ExecuteScalarAsync(token) ?? false)) throw new ClinicalAlertRuleChangeRequestConflictException("An open change request already exists for this alert rule."); }
        await using (var create = connection.CreateCommand()) { create.Transaction=transaction; create.CommandText="insert into clinical_alert_rule_change_requests(request_id,rule_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@kind,@proposed,@baseline,@updated,@reason,'draft',0,now(),@user,now(),@user);"; create.Parameters.AddWithValue("id",id); create.Parameters.AddWithValue("key",proposed.Key); create.Parameters.AddWithValue("kind",baseline is null?"create":"update"); create.Parameters.Add("proposed",NpgsqlDbType.Jsonb).Value=SerializeAlertDefinition(proposed); create.Parameters.Add("baseline",NpgsqlDbType.Jsonb).Value=baseline is null?DBNull.Value:SerializeAlertDefinition(baseline.Definition); create.Parameters.AddWithValue("updated",(object?)baseline?.UpdatedAt??DBNull.Value); create.Parameters.AddWithValue("reason",reason); create.Parameters.AddWithValue("user",username); await create.ExecuteNonQueryAsync(token); }
        await WriteAlertChangeEventAsync(connection,transaction,id,"created",reason,username,token); await transaction.CommitAsync(token); return await GetClinicalAlertRuleChangeRequestAsync(id,token);
    }
    public async Task<ClinicalAlertRuleChangeRequestsResponse> GetClinicalAlertRuleChangeRequestsAsync(string? status, int offset, int limit, CancellationToken token)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("Change-request paging is invalid."); var normalized = NormalizeAlertChangeStatus(status); await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var count = connection.CreateCommand(); count.CommandText = "select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from clinical_alert_rule_change_requests;"; count.Parameters.AddWithValue("status", normalized); var counts = new ClinicalAlertRuleChangeRequestCounts(0, 0, 0, 0, 0, 0); var total = 0; await using (var reader = await count.ExecuteReaderAsync(token)) if (await reader.ReadAsync(token)) { counts = new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)); total = reader.GetInt32(6); }
        await using var command = connection.CreateCommand(); command.CommandText = "select request_id,rule_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from clinical_alert_rule_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc offset @offset limit @limit;"; command.Parameters.AddWithValue("status", normalized); command.Parameters.AddWithValue("offset", offset); command.Parameters.AddWithValue("limit", limit); var requests = new List<ClinicalAlertRuleChangeRequestItem>(); await using var result = await command.ExecuteReaderAsync(token); while (await result.ReadAsync(token)) requests.Add(ReadAlertChangeRequest(result)); return new(requests, total, requests.Count, offset, limit, normalized, counts);
    }
    public async Task<ClinicalAlertRuleChangeRequestDetailResponse> GetClinicalAlertRuleChangeRequestAsync(Guid id,CancellationToken token)
    { await using var connection=await dataSource.OpenConnectionAsync(token); var request=await GetAlertChangeRequestAsync(connection,id,token)??throw new ArgumentException("The requested alert-rule change request was not found."); ClinicalAlertRuleItem? active=null; try { active=await GetClinicalAlertRuleAsync(request.RuleKey,token); } catch(ArgumentException){} await using var command=connection.CreateCommand();command.CommandText="select event_id,action,note,occurred_at,username from clinical_alert_rule_change_request_events where request_id=@id order by occurred_at desc,event_id desc;";command.Parameters.AddWithValue("id",id);var events=new List<ClinicalAlertRuleChangeRequestEvent>();await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))events.Add(new(reader.GetInt64(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),reader.GetString(4)));return new(request,active,events); }
    public Task<ClinicalAlertRuleChangeRequestDetailResponse> SubmitClinicalAlertRuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionAlertChangeRequestAsync(id,["draft"],"submitted",note,false,version,user,token);
    public Task<ClinicalAlertRuleChangeRequestDetailResponse> ApproveClinicalAlertRuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionAlertChangeRequestAsync(id,["submitted"],"approved",note,false,version,user,token);
    public Task<ClinicalAlertRuleChangeRequestDetailResponse> RejectClinicalAlertRuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionAlertChangeRequestAsync(id,["submitted"],"rejected",note,true,version,user,token);
    public Task<ClinicalAlertRuleChangeRequestDetailResponse> ActivateClinicalAlertRuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionAlertChangeRequestAsync(id,["approved"],"activated",note,false,version,user,token);
    public Task<ClinicalAlertRuleChangeRequestDetailResponse> CancelClinicalAlertRuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionAlertChangeRequestAsync(id,["draft","submitted","approved"],"cancelled",note,true,version,user,token);
    private async Task<ClinicalAlertRuleChangeRequestDetailResponse> TransitionAlertChangeRequestAsync(Guid id,string[] expected,string next,string? note,bool noteRequired,int? version,string user,CancellationToken token)
    { var normalizedNote=ValidateChangeRequestReason(note,noteRequired);await using var connection=await dataSource.OpenConnectionAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);ClinicalAlertRuleChangeRequestItem request;await using(var current=connection.CreateCommand()){current.Transaction=transaction;current.CommandText="select request_id,rule_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from clinical_alert_rule_change_requests where request_id=@id for update;";current.Parameters.AddWithValue("id",id);await using var reader=await current.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))throw new ArgumentException("The requested alert-rule change request was not found.");request=ReadAlertChangeRequest(reader);}if(!expected.Contains(request.Status,StringComparer.Ordinal))throw new ClinicalAlertRuleChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {next}.");if(version is not null&&version!=request.Version)throw new ClinicalAlertRuleChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");if(next=="activated"){var current=await GetAlertDefinitionForUpdateAsync(connection,transaction,request.RuleKey,token);if(request.ChangeKind=="create"){if(current is not null)throw new ClinicalAlertRuleChangeRequestConflictException("The alert rule was created after this request was drafted.");}else if(current is null||request.BaselineDefinition is null||SerializeAlertDefinition(current.Definition)!=SerializeAlertDefinition(request.BaselineDefinition)||current.UpdatedAt.ToUniversalTime()!=DateTimeOffset.Parse(request.BaselineUpdatedAt!).ToUniversalTime())throw new ClinicalAlertRuleChangeRequestConflictException("The active alert rule changed after this request was created.");try{await ApplyAlertDefinitionAsync(connection,transaction,request.ProposedDefinition,user,request.ChangeKind=="create",token);await SnapshotClinicalAlertRuleAsync(connection,transaction,request.RuleKey,user,"activated",null,token);}catch(PostgresException exception)when(exception.SqlState=="23505"){throw new ClinicalAlertRuleChangeRequestConflictException("The rule sequence conflicts with an active alert rule.");}}await using(var update=connection.CreateCommand()){update.Transaction=transaction;update.CommandText="update clinical_alert_rule_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;";update.Parameters.AddWithValue("id",id);update.Parameters.AddWithValue("status",next);update.Parameters.AddWithValue("user",user);await update.ExecuteNonQueryAsync(token);}await WriteAlertChangeEventAsync(connection,transaction,id,next,normalizedNote,user,token);await transaction.CommitAsync(token);return await GetClinicalAlertRuleChangeRequestAsync(id,token); }
    private static ClinicalAlertRuleDefinition NormalizeAlertDefinition(ClinicalAlertRuleChangeRequestCreateRequest request){var key=NormalizeCatalogKey(request.Key);if(string.IsNullOrWhiteSpace(request.Title)||request.Title.Trim().Length>120||string.IsNullOrWhiteSpace(request.Message)||request.Message.Trim().Length>1000||request.Sequence<0||request.TriggerType is not("patient"or"encounter"or"appointment")||request.TargetType is not("banner"or"reminder")||request.Severity is not("info"or"warning"or"critical"))throw new ArgumentException("Alert rule definition is invalid.");return new(key,request.Title.Trim(),request.TriggerType,request.TargetType,request.Severity,request.Message.Trim(),request.Sequence,request.Active);}
    private static string NormalizeAlertChangeStatus(string? status){var normalized=string.IsNullOrWhiteSpace(status)?"all":status.Trim().ToLowerInvariant();if(normalized is not("all"or"open"or"draft"or"submitted"or"approved"or"rejected"or"activated"or"cancelled"))throw new ArgumentException("Change-request status is not supported.");return normalized;}
    private static string SerializeAlertDefinition(ClinicalAlertRuleDefinition value)=>JsonSerializer.Serialize(value,PortalProfileChangeJsonOptions);private static ClinicalAlertRuleDefinition ReadAlertDefinition(string json)=>JsonSerializer.Deserialize<ClinicalAlertRuleDefinition>(json,PortalProfileChangeJsonOptions)??throw new InvalidOperationException("Stored alert definition is invalid.");
    private static ClinicalAlertRuleChangeRequestItem ReadAlertChangeRequest(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetString(1),r.GetString(2),ReadAlertDefinition(r.GetString(3)),r.IsDBNull(4)?null:ReadAlertDefinition(r.GetString(4)),r.IsDBNull(5)?null:r.GetFieldValue<DateTimeOffset>(5).ToString("O"),r.GetString(6),r.GetString(7),r.GetInt32(8),r.GetFieldValue<DateTimeOffset>(9).ToString("O"),r.GetString(10),r.GetFieldValue<DateTimeOffset>(11).ToString("O"),r.GetString(12));
    private static async Task<ClinicalAlertRuleChangeRequestItem?> GetAlertChangeRequestAsync(NpgsqlConnection c,Guid id,CancellationToken t){await using var cmd=c.CreateCommand();cmd.CommandText="select request_id,rule_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from clinical_alert_rule_change_requests where request_id=@id;";cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(t);return await r.ReadAsync(t)?ReadAlertChangeRequest(r):null;}
    private static async Task WriteAlertChangeEventAsync(NpgsqlConnection c,NpgsqlTransaction tx,Guid id,string action,string? note,string user,CancellationToken t){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="insert into clinical_alert_rule_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);";cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("action",action);cmd.Parameters.AddWithValue("note",(object?)note??DBNull.Value);cmd.Parameters.AddWithValue("user",user);await cmd.ExecuteNonQueryAsync(t);}
    private sealed record AlertCurrent(ClinicalAlertRuleDefinition Definition,DateTimeOffset UpdatedAt);private static async Task<AlertCurrent?> GetAlertDefinitionForUpdateAsync(NpgsqlConnection c,NpgsqlTransaction tx,string key,CancellationToken t){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="select rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at from clinical_alert_rules where rule_key=@key for update;";cmd.Parameters.AddWithValue("key",key);await using var r=await cmd.ExecuteReaderAsync(t);return await r.ReadAsync(t)?new(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt32(6),r.GetBoolean(7)),r.GetFieldValue<DateTimeOffset>(8)):null;}
    private static async Task ApplyAlertDefinitionAsync(NpgsqlConnection c,NpgsqlTransaction tx,ClinicalAlertRuleDefinition d,string user,bool create,CancellationToken t){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=create?"insert into clinical_alert_rules(rule_key,title,trigger_type,target_type,severity,message,sequence,active,updated_at,updated_by) values(@key,@title,@trigger,@target,@severity,@message,@sequence,@active,now(),@user);":"update clinical_alert_rules set title=@title,trigger_type=@trigger,target_type=@target,severity=@severity,message=@message,sequence=@sequence,active=@active,updated_at=now(),updated_by=@user where rule_key=@key;";cmd.Parameters.AddWithValue("key",d.Key);cmd.Parameters.AddWithValue("title",d.Title);cmd.Parameters.AddWithValue("trigger",d.TriggerType);cmd.Parameters.AddWithValue("target",d.TargetType);cmd.Parameters.AddWithValue("severity",d.Severity);cmd.Parameters.AddWithValue("message",d.Message);cmd.Parameters.AddWithValue("sequence",d.Sequence);cmd.Parameters.AddWithValue("active",d.Active);cmd.Parameters.AddWithValue("user",user);await cmd.ExecuteNonQueryAsync(t);}

    public async Task<ModuleCatalogResponse> GetModuleCatalogAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "select module_key,display_name,category,status,description,updated_at,updated_by from module_catalog order by category,display_name;"; var modules = new List<ModuleCatalogItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) modules.Add(ReadModuleCatalog(reader)); return new(modules);
    }

    public async Task<ModuleCatalogHistoryResponse> GetModuleCatalogHistoryAsync(string key,CancellationToken cancellationToken)
    { var moduleKey=NormalizeCatalogKey(key);var module=await GetModuleCatalogItemAsync(moduleKey,cancellationToken);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select revision_id,display_name,category,status,description,action,restored_from_revision_id,occurred_at,username from module_catalog_revisions where module_key=@key order by occurred_at desc,revision_id desc;";command.Parameters.AddWithValue("key",moduleKey);var revisions=new List<ModuleCatalogRevision>();await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetInt64(6),reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),reader.GetString(8)));return new(module,revisions); }

    public async Task<ModuleCatalogHistoryResponse> UpdateModuleCatalogStatusAsync(string key,string status,string username,CancellationToken cancellationToken)
    { var moduleKey=NormalizeCatalogKey(key);var normalizedStatus=status.Trim().ToLowerInvariant();if(normalizedStatus is not ("enabled" or "disabled"))throw new ArgumentException("A local module can only be enabled or disabled.");await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);await using(var current=connection.CreateCommand()){current.Transaction=transaction;current.CommandText="select status from module_catalog where module_key=@key for update;";current.Parameters.AddWithValue("key",moduleKey);var currentStatus=await current.ExecuteScalarAsync(cancellationToken) as string;if(currentStatus is null)throw new ArgumentException("The requested module was not found.");if(moduleKey!="THERAPY_GROUPS")throw new ArgumentException("This module requires an owner or partner decision and cannot be changed locally.");if(currentStatus!=normalizedStatus){await using var update=connection.CreateCommand();update.Transaction=transaction;update.CommandText="update module_catalog set status=@status,updated_at=now(),updated_by=@user where module_key=@key;";update.Parameters.AddWithValue("key",moduleKey);update.Parameters.AddWithValue("status",normalizedStatus);update.Parameters.AddWithValue("user",username);await update.ExecuteNonQueryAsync(cancellationToken);await SnapshotModuleCatalogAsync(connection,transaction,moduleKey,username,"updated",null,cancellationToken);}}await transaction.CommitAsync(cancellationToken);return await GetModuleCatalogHistoryAsync(moduleKey,cancellationToken); }

    public async Task<ModuleCatalogHistoryResponse> RollbackModuleCatalogAsync(string key,long revisionId,string username,CancellationToken cancellationToken)
    { var moduleKey=NormalizeCatalogKey(key);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);if(moduleKey!="THERAPY_GROUPS")throw new ArgumentException("This module requires an owner or partner decision and cannot be changed locally.");await using(var check=connection.CreateCommand()){check.Transaction=transaction;check.CommandText="select exists(select 1 from module_catalog_revisions where module_key=@key and revision_id=@revision);";check.Parameters.AddWithValue("key",moduleKey);check.Parameters.AddWithValue("revision",revisionId);if(!(bool)(await check.ExecuteScalarAsync(cancellationToken)??false))throw new ArgumentException("The requested revision was not found for this module.");}await using(var restore=connection.CreateCommand()){restore.Transaction=transaction;restore.CommandText="update module_catalog set display_name=(select display_name from module_catalog_revisions where module_key=@key and revision_id=@revision),category=(select category from module_catalog_revisions where module_key=@key and revision_id=@revision),status=(select status from module_catalog_revisions where module_key=@key and revision_id=@revision),description=(select description from module_catalog_revisions where module_key=@key and revision_id=@revision),updated_at=now(),updated_by=@user where module_key=@key;";restore.Parameters.AddWithValue("key",moduleKey);restore.Parameters.AddWithValue("revision",revisionId);restore.Parameters.AddWithValue("user",username);if(await restore.ExecuteNonQueryAsync(cancellationToken)!=1)throw new ArgumentException("The module was not found.");}await SnapshotModuleCatalogAsync(connection,transaction,moduleKey,username,"rolled-back",revisionId,cancellationToken);await transaction.CommitAsync(cancellationToken);return await GetModuleCatalogHistoryAsync(moduleKey,cancellationToken); }

    private static ModuleCatalogItem ReadModuleCatalog(NpgsqlDataReader reader) => new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(0)=="THERAPY_GROUPS",reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),reader.GetString(6));
    private async Task<ModuleCatalogItem> GetModuleCatalogItemAsync(string key,CancellationToken cancellationToken)
    { await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select module_key,display_name,category,status,description,updated_at,updated_by from module_catalog where module_key=@key;";command.Parameters.AddWithValue("key",key);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The requested module was not found.");return ReadModuleCatalog(reader); }
    private static async Task SnapshotModuleCatalogAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    { await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into module_catalog_revisions(module_key,display_name,category,status,description,action,restored_from_revision_id,occurred_at,username) select module_key,display_name,category,status,description,@action,@restored,now(),@user from module_catalog where module_key=@key;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken); }

    public async Task<ModuleChangeRequestDetailResponse> CreateModuleChangeRequestAsync(ModuleChangeRequestCreateRequest request,string user,CancellationToken token)
    { var key=NormalizeCatalogKey(request.ModuleKey);var status=NormalizeLocalModuleStatus(request.Status);var reason=ValidateChangeRequestReason(request.Reason,true)!;await using var connection=await dataSource.OpenConnectionAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);var module=await GetModuleForUpdateAsync(connection,transaction,key,token)??throw new ArgumentException("The requested module was not found.");if(key!="THERAPY_GROUPS")throw new ArgumentException("This module requires an owner or partner decision and cannot be changed locally.");if(module.Status==status)throw new ArgumentException("The proposed module status must differ from the active status.");await using(var duplicate=connection.CreateCommand()){duplicate.Transaction=transaction;duplicate.CommandText="select exists(select 1 from module_change_requests where module_key=@key and status in ('draft','submitted','approved'));";duplicate.Parameters.AddWithValue("key",key);if((bool)(await duplicate.ExecuteScalarAsync(token)??false))throw new ModuleChangeRequestConflictException("An open change request already exists for this module.");}var id=Guid.NewGuid();await using(var create=connection.CreateCommand()){create.Transaction=transaction;create.CommandText="insert into module_change_requests(request_id,module_key,proposed_status,baseline_status,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@proposed,@baseline,@updated,@reason,'draft',0,now(),@user,now(),@user);";create.Parameters.AddWithValue("id",id);create.Parameters.AddWithValue("key",key);create.Parameters.AddWithValue("proposed",status);create.Parameters.AddWithValue("baseline",module.Status);create.Parameters.AddWithValue("updated",module.UpdatedAt);create.Parameters.AddWithValue("reason",reason);create.Parameters.AddWithValue("user",user);await create.ExecuteNonQueryAsync(token);}await WriteModuleChangeEventAsync(connection,transaction,id,"created",reason,user,token);await transaction.CommitAsync(token);return await GetModuleChangeRequestAsync(id,token); }
    public async Task<ModuleChangeRequestsResponse> GetModuleChangeRequestsAsync(string? status, CancellationToken token)
    { var normalized=NormalizeModuleChangeStatus(status);await using var connection=await dataSource.OpenConnectionAsync(token);await using var count=connection.CreateCommand();count.CommandText="select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from module_change_requests;";count.Parameters.AddWithValue("status",normalized);var counts=new ModuleChangeRequestCounts(0,0,0,0,0,0);var total=0;await using(var reader=await count.ExecuteReaderAsync(token))if(await reader.ReadAsync(token)){counts=new(reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4),reader.GetInt32(5));total=reader.GetInt32(6);}await using var command=connection.CreateCommand();command.CommandText="select request_id,module_key,proposed_status,baseline_status,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from module_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc;";command.Parameters.AddWithValue("status",normalized);var requests=new List<ModuleChangeRequestItem>();await using var result=await command.ExecuteReaderAsync(token);while(await result.ReadAsync(token))requests.Add(ReadModuleChangeRequest(result));return new(requests,total,normalized,counts);}
    public async Task<ModuleChangeRequestDetailResponse> GetModuleChangeRequestAsync(Guid id,CancellationToken token){await using var connection=await dataSource.OpenConnectionAsync(token);var request=await ReadModuleChangeRequestAsync(connection,id,token)??throw new ArgumentException("The requested module change request was not found.");var module=await GetModuleCatalogItemAsync(request.ModuleKey,token);await using var command=connection.CreateCommand();command.CommandText="select event_id,action,note,occurred_at,username from module_change_request_events where request_id=@id order by occurred_at desc,event_id desc;";command.Parameters.AddWithValue("id",id);var events=new List<ModuleChangeRequestEvent>();await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))events.Add(new(reader.GetInt64(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),reader.GetString(4)));return new(request,module,events);}
    public Task<ModuleChangeRequestDetailResponse> SubmitModuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionModuleChangeRequestAsync(id,["draft"],"submitted",note,false,version,user,token);public Task<ModuleChangeRequestDetailResponse> ApproveModuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionModuleChangeRequestAsync(id,["submitted"],"approved",note,false,version,user,token);public Task<ModuleChangeRequestDetailResponse> RejectModuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionModuleChangeRequestAsync(id,["submitted"],"rejected",note,true,version,user,token);public Task<ModuleChangeRequestDetailResponse> ActivateModuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionModuleChangeRequestAsync(id,["approved"],"activated",note,false,version,user,token);public Task<ModuleChangeRequestDetailResponse> CancelModuleChangeRequestAsync(Guid id,string? note,int? version,string user,CancellationToken token)=>TransitionModuleChangeRequestAsync(id,["draft","submitted","approved"],"cancelled",note,true,version,user,token);
    private async Task<ModuleChangeRequestDetailResponse> TransitionModuleChangeRequestAsync(Guid id,string[] expected,string next,string? note,bool noteRequired,int? version,string user,CancellationToken token){var normalizedNote=ValidateChangeRequestReason(note,noteRequired);await using var connection=await dataSource.OpenConnectionAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);ModuleChangeRequestItem request;await using(var current=connection.CreateCommand()){current.Transaction=transaction;current.CommandText="select request_id,module_key,proposed_status,baseline_status,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from module_change_requests where request_id=@id for update;";current.Parameters.AddWithValue("id",id);await using var reader=await current.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))throw new ArgumentException("The requested module change request was not found.");request=ReadModuleChangeRequest(reader);}if(!expected.Contains(request.Status,StringComparer.Ordinal))throw new ModuleChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {next}.");if(version is not null&&version!=request.Version)throw new ModuleChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");if(next=="activated"){var module=await GetModuleForUpdateAsync(connection,transaction,request.ModuleKey,token);if(module is null||module.Status!=request.BaselineStatus||module.UpdatedAt.ToUniversalTime()!=DateTimeOffset.Parse(request.BaselineUpdatedAt).ToUniversalTime())throw new ModuleChangeRequestConflictException("The active module changed after this request was created.");await using var updateModule=connection.CreateCommand();updateModule.Transaction=transaction;updateModule.CommandText="update module_catalog set status=@status,updated_at=now(),updated_by=@user where module_key=@key;";updateModule.Parameters.AddWithValue("key",request.ModuleKey);updateModule.Parameters.AddWithValue("status",request.ProposedStatus);updateModule.Parameters.AddWithValue("user",user);await updateModule.ExecuteNonQueryAsync(token);await SnapshotModuleCatalogAsync(connection,transaction,request.ModuleKey,user,"activated",null,token);}await using(var update=connection.CreateCommand()){update.Transaction=transaction;update.CommandText="update module_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;";update.Parameters.AddWithValue("id",id);update.Parameters.AddWithValue("status",next);update.Parameters.AddWithValue("user",user);await update.ExecuteNonQueryAsync(token);}await WriteModuleChangeEventAsync(connection,transaction,id,next,normalizedNote,user,token);await transaction.CommitAsync(token);return await GetModuleChangeRequestAsync(id,token);}
    private static string NormalizeLocalModuleStatus(string value){var status=value.Trim().ToLowerInvariant();if(status is not("enabled"or"disabled"))throw new ArgumentException("A local module can only be enabled or disabled.");return status;}private static string NormalizeModuleChangeStatus(string? value){var status=string.IsNullOrWhiteSpace(value)?"all":value.Trim().ToLowerInvariant();if(status is not("all"or"open"or"draft"or"submitted"or"approved"or"rejected"or"activated"or"cancelled"))throw new ArgumentException("Change-request status is not supported.");return status;}private static ModuleChangeRequestItem ReadModuleChangeRequest(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetFieldValue<DateTimeOffset>(4).ToString("O"),r.GetString(5),r.GetString(6),r.GetInt32(7),r.GetFieldValue<DateTimeOffset>(8).ToString("O"),r.GetString(9),r.GetFieldValue<DateTimeOffset>(10).ToString("O"),r.GetString(11));private static async Task<ModuleChangeRequestItem?> ReadModuleChangeRequestAsync(NpgsqlConnection c,Guid id,CancellationToken t){await using var cmd=c.CreateCommand();cmd.CommandText="select request_id,module_key,proposed_status,baseline_status,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from module_change_requests where request_id=@id;";cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(t);return await r.ReadAsync(t)?ReadModuleChangeRequest(r):null;}private static async Task WriteModuleChangeEventAsync(NpgsqlConnection c,NpgsqlTransaction tx,Guid id,string action,string? note,string user,CancellationToken t){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="insert into module_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);";cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("action",action);cmd.Parameters.AddWithValue("note",(object?)note??DBNull.Value);cmd.Parameters.AddWithValue("user",user);await cmd.ExecuteNonQueryAsync(t);}private sealed record ModuleCurrent(string Status,DateTimeOffset UpdatedAt);private static async Task<ModuleCurrent?> GetModuleForUpdateAsync(NpgsqlConnection c,NpgsqlTransaction tx,string key,CancellationToken t){await using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="select status,updated_at from module_catalog where module_key=@key for update;";cmd.Parameters.AddWithValue("key",key);await using var r=await cmd.ExecuteReaderAsync(t);return await r.ReadAsync(t)?new(r.GetString(0),r.GetFieldValue<DateTimeOffset>(1)):null;}

    public async Task<ApiClientRegistryResponse> GetApiClientsAsync(CancellationToken cancellationToken)
    { await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand(); command.CommandText = "select client_key,display_name,redirect_uri,scopes,active,updated_at,updated_by from api_client_registry order by client_key;"; var clients = new List<ApiClientRegistryItem>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) clients.Add(ReadApiClientRegistry(reader)); return new(clients); }
    public async Task<ApiClientRegistryResponse> UpsertApiClientAsync(string key, ApiClientRegistryMutationRequest request, string username, CancellationToken cancellationToken)
    { key = NormalizeCatalogKey(key); if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120 || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(request.Scopes) || request.Scopes.Length > 500) throw new ArgumentException("API client definition is invalid."); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken); await using var command = connection.CreateCommand();command.Transaction=transaction; command.CommandText = "insert into api_client_registry(client_key,display_name,redirect_uri,scopes,active,updated_at,updated_by) values(@key,@name,@uri,@scopes,@active,now(),@user) on conflict(client_key) do update set display_name=excluded.display_name,redirect_uri=excluded.redirect_uri,scopes=excluded.scopes,active=excluded.active,updated_at=now(),updated_by=excluded.updated_by;"; command.Parameters.AddWithValue("key",key); command.Parameters.AddWithValue("name",request.DisplayName.Trim()); command.Parameters.AddWithValue("uri",uri.AbsoluteUri); command.Parameters.AddWithValue("scopes",request.Scopes.Trim()); command.Parameters.AddWithValue("active",request.Active); command.Parameters.AddWithValue("user",username); await command.ExecuteNonQueryAsync(cancellationToken);await SnapshotApiClientRegistryAsync(connection,transaction,key,username,"updated",null,cancellationToken);await transaction.CommitAsync(cancellationToken); return await GetApiClientsAsync(cancellationToken); }
    public async Task<ApiClientRegistryHistoryResponse> GetApiClientRegistryHistoryAsync(string key,CancellationToken cancellationToken)
    {var clientKey=NormalizeCatalogKey(key);var client=await GetApiClientRegistryItemAsync(clientKey,cancellationToken);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select revision_id,display_name,redirect_uri,scopes,active,action,restored_from_revision_id,occurred_at,username from api_client_registry_revisions where client_key=@key order by occurred_at desc,revision_id desc;";command.Parameters.AddWithValue("key",clientKey);var revisions=new List<ApiClientRegistryRevision>();await using var reader=await command.ExecuteReaderAsync(cancellationToken);while(await reader.ReadAsync(cancellationToken))revisions.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetBoolean(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetInt64(6),reader.GetFieldValue<DateTimeOffset>(7).ToString("O"),reader.GetString(8)));return new(client,revisions);}
    public async Task<ApiClientRegistryHistoryResponse> RollbackApiClientRegistryAsync(string key,long revisionId,string username,CancellationToken cancellationToken)
    {var clientKey=NormalizeCatalogKey(key);await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var transaction=await connection.BeginTransactionAsync(cancellationToken);await using(var check=connection.CreateCommand()){check.Transaction=transaction;check.CommandText="select exists(select 1 from api_client_registry_revisions where client_key=@key and revision_id=@revision);";check.Parameters.AddWithValue("key",clientKey);check.Parameters.AddWithValue("revision",revisionId);if(!(bool)(await check.ExecuteScalarAsync(cancellationToken)??false))throw new ArgumentException("The requested revision was not found for this API client.");}await using(var restore=connection.CreateCommand()){restore.Transaction=transaction;restore.CommandText="update api_client_registry set display_name=(select display_name from api_client_registry_revisions where client_key=@key and revision_id=@revision),redirect_uri=(select redirect_uri from api_client_registry_revisions where client_key=@key and revision_id=@revision),scopes=(select scopes from api_client_registry_revisions where client_key=@key and revision_id=@revision),active=(select active from api_client_registry_revisions where client_key=@key and revision_id=@revision),updated_at=now(),updated_by=@user where client_key=@key;";restore.Parameters.AddWithValue("key",clientKey);restore.Parameters.AddWithValue("revision",revisionId);restore.Parameters.AddWithValue("user",username);if(await restore.ExecuteNonQueryAsync(cancellationToken)!=1)throw new ArgumentException("The API client was not found.");}await SnapshotApiClientRegistryAsync(connection,transaction,clientKey,username,"rolled-back",revisionId,cancellationToken);await transaction.CommitAsync(cancellationToken);return await GetApiClientRegistryHistoryAsync(clientKey,cancellationToken);}
    private static ApiClientRegistryItem ReadApiClientRegistry(NpgsqlDataReader reader)=>new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetBoolean(4),reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),reader.GetString(6));
    private async Task<ApiClientRegistryItem> GetApiClientRegistryItemAsync(string key,CancellationToken cancellationToken)
    {await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);await using var command=connection.CreateCommand();command.CommandText="select client_key,display_name,redirect_uri,scopes,active,updated_at,updated_by from api_client_registry where client_key=@key;";command.Parameters.AddWithValue("key",key);await using var reader=await command.ExecuteReaderAsync(cancellationToken);if(!await reader.ReadAsync(cancellationToken))throw new ArgumentException("The requested API client was not found.");return ReadApiClientRegistry(reader);}
    private static async Task SnapshotApiClientRegistryAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,string key,string username,string action,long? restoredFromRevisionId,CancellationToken cancellationToken)
    {await using var command=connection.CreateCommand();command.Transaction=transaction;command.CommandText="insert into api_client_registry_revisions(client_key,display_name,redirect_uri,scopes,active,action,restored_from_revision_id,occurred_at,username) select client_key,display_name,redirect_uri,scopes,active,@action,@restored,now(),@user from api_client_registry where client_key=@key;";command.Parameters.AddWithValue("key",key);command.Parameters.AddWithValue("user",username);command.Parameters.AddWithValue("action",action);command.Parameters.AddWithValue("restored",(object?)restoredFromRevisionId??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken);}

    public async Task<ApiClientChangeRequestDetailResponse> CreateApiClientChangeRequestAsync(ApiClientChangeRequestCreateRequest request, string username, CancellationToken token)
    {
        var proposed = NormalizeApiClientDefinition(request); var reason = ValidateChangeRequestReason(request.Reason, true)!; var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        var baseline = await GetApiClientForUpdateAsync(connection, transaction, proposed.Key, token);
        if (baseline is not null && SerializeApiClientDefinition(baseline.Definition) == SerializeApiClientDefinition(proposed)) throw new ArgumentException("The proposed API client must differ from the active client.");
        await using (var duplicate = connection.CreateCommand()) { duplicate.Transaction = transaction; duplicate.CommandText = "select exists(select 1 from api_client_change_requests where client_key=@key and status in ('draft','submitted','approved'));"; duplicate.Parameters.AddWithValue("key", proposed.Key); if ((bool)(await duplicate.ExecuteScalarAsync(token) ?? false)) throw new ApiClientChangeRequestConflictException("An open change request already exists for this API client."); }
        await using (var create = connection.CreateCommand()) { create.Transaction = transaction; create.CommandText = "insert into api_client_change_requests(request_id,client_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by) values(@id,@key,@kind,@proposed,@baseline,@updated,@reason,'draft',0,now(),@user,now(),@user);"; create.Parameters.AddWithValue("id", id); create.Parameters.AddWithValue("key", proposed.Key); create.Parameters.AddWithValue("kind", baseline is null ? "create" : "update"); create.Parameters.Add("proposed", NpgsqlDbType.Jsonb).Value = SerializeApiClientDefinition(proposed); create.Parameters.Add("baseline", NpgsqlDbType.Jsonb).Value = baseline is null ? DBNull.Value : SerializeApiClientDefinition(baseline.Definition); create.Parameters.AddWithValue("updated", (object?)baseline?.UpdatedAt ?? DBNull.Value); create.Parameters.AddWithValue("reason", reason); create.Parameters.AddWithValue("user", username); await create.ExecuteNonQueryAsync(token); }
        await WriteApiClientChangeEventAsync(connection, transaction, id, "created", reason, username, token); await transaction.CommitAsync(token); return await GetApiClientChangeRequestAsync(id, token);
    }
    public async Task<ApiClientChangeRequestsResponse> GetApiClientChangeRequestsAsync(string? status, CancellationToken token)
    {
        var normalized = NormalizeApiClientChangeStatus(status); await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var count = connection.CreateCommand(); count.CommandText = "select count(*) filter (where status='draft')::integer,count(*) filter (where status='submitted')::integer,count(*) filter (where status='approved')::integer,count(*) filter (where status='rejected')::integer,count(*) filter (where status='activated')::integer,count(*) filter (where status='cancelled')::integer,count(*) filter (where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status)::integer from api_client_change_requests;"; count.Parameters.AddWithValue("status", normalized); var counts = new ApiClientChangeRequestCounts(0, 0, 0, 0, 0, 0); var total = 0; await using (var reader = await count.ExecuteReaderAsync(token)) if (await reader.ReadAsync(token)) { counts = new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)); total = reader.GetInt32(6); }
        await using var command = connection.CreateCommand(); command.CommandText = "select request_id,client_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from api_client_change_requests where @status='all' or (@status='open' and status in ('draft','submitted','approved')) or status=@status order by updated_at desc,request_id desc;"; command.Parameters.AddWithValue("status", normalized); var requests = new List<ApiClientChangeRequestItem>(); await using var result = await command.ExecuteReaderAsync(token); while (await result.ReadAsync(token)) requests.Add(ReadApiClientChangeRequest(result)); return new(requests, total, normalized, counts);
    }
    public async Task<ApiClientChangeRequestDetailResponse> GetApiClientChangeRequestAsync(Guid id, CancellationToken token)
    { await using var connection = await dataSource.OpenConnectionAsync(token); var request = await ReadApiClientChangeRequestAsync(connection, id, token) ?? throw new ArgumentException("The requested API-client change request was not found."); ApiClientRegistryItem? active = null; try { active = await GetApiClientRegistryItemAsync(request.ClientKey, token); } catch (ArgumentException) { } await using var command = connection.CreateCommand(); command.CommandText = "select event_id,action,note,occurred_at,username from api_client_change_request_events where request_id=@id order by occurred_at desc,event_id desc;"; command.Parameters.AddWithValue("id", id); var events = new List<ApiClientChangeRequestEvent>(); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) events.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4))); return new(request, active, events); }
    public Task<ApiClientChangeRequestDetailResponse> SubmitApiClientChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionApiClientChangeRequestAsync(id, ["draft"], "submitted", note, false, version, user, token);
    public Task<ApiClientChangeRequestDetailResponse> ApproveApiClientChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionApiClientChangeRequestAsync(id, ["submitted"], "approved", note, false, version, user, token);
    public Task<ApiClientChangeRequestDetailResponse> RejectApiClientChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionApiClientChangeRequestAsync(id, ["submitted"], "rejected", note, true, version, user, token);
    public Task<ApiClientChangeRequestDetailResponse> ActivateApiClientChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionApiClientChangeRequestAsync(id, ["approved"], "activated", note, false, version, user, token);
    public Task<ApiClientChangeRequestDetailResponse> CancelApiClientChangeRequestAsync(Guid id, string? note, int? version, string user, CancellationToken token) => TransitionApiClientChangeRequestAsync(id, ["draft", "submitted", "approved"], "cancelled", note, true, version, user, token);
    private async Task<ApiClientChangeRequestDetailResponse> TransitionApiClientChangeRequestAsync(Guid id, string[] expected, string next, string? note, bool noteRequired, int? version, string user, CancellationToken token)
    {
        var normalizedNote = ValidateChangeRequestReason(note, noteRequired); await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token); ApiClientChangeRequestItem request;
        await using (var current = connection.CreateCommand()) { current.Transaction = transaction; current.CommandText = "select request_id,client_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from api_client_change_requests where request_id=@id for update;"; current.Parameters.AddWithValue("id", id); await using var reader = await current.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new ArgumentException("The requested API-client change request was not found."); request = ReadApiClientChangeRequest(reader); }
        if (!expected.Contains(request.Status, StringComparer.Ordinal)) throw new ApiClientChangeRequestConflictException($"The change request is {request.Status}; it cannot move to {next}."); if (version is not null && version != request.Version) throw new ApiClientChangeRequestConflictException($"The change request changed after it was loaded. Current version is {request.Version}.");
        if (next == "activated") { var active = await GetApiClientForUpdateAsync(connection, transaction, request.ClientKey, token); if ((request.ChangeKind == "create" && active is not null) || (request.ChangeKind == "update" && (active is null || active.UpdatedAt.ToUniversalTime() != DateTimeOffset.Parse(request.BaselineUpdatedAt!).ToUniversalTime() || SerializeApiClientDefinition(active.Definition) != SerializeApiClientDefinition(request.BaselineDefinition!)))) throw new ApiClientChangeRequestConflictException("The active API client changed after this request was created."); await ApplyApiClientDefinitionAsync(connection, transaction, request.ProposedDefinition, user, request.ChangeKind == "create", token); await SnapshotApiClientRegistryAsync(connection, transaction, request.ClientKey, user, "activated", null, token); }
        await using (var update = connection.CreateCommand()) { update.Transaction = transaction; update.CommandText = "update api_client_change_requests set status=@status,version=version+1,updated_at=now(),updated_by=@user where request_id=@id;"; update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("status", next); update.Parameters.AddWithValue("user", user); await update.ExecuteNonQueryAsync(token); } await WriteApiClientChangeEventAsync(connection, transaction, id, next, normalizedNote, user, token); await transaction.CommitAsync(token); return await GetApiClientChangeRequestAsync(id, token);
    }
    private static ApiClientRegistrationDefinition NormalizeApiClientDefinition(ApiClientChangeRequestCreateRequest request) { var key = NormalizeCatalogKey(request.Key); if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120 || !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(request.Scopes) || request.Scopes.Trim().Length > 500) throw new ArgumentException("API client definition is invalid."); return new(key, request.DisplayName.Trim(), uri.AbsoluteUri, request.Scopes.Trim(), request.Active); }
    private static string NormalizeApiClientChangeStatus(string? status) { var normalized = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant(); if (normalized is not ("all" or "open" or "draft" or "submitted" or "approved" or "rejected" or "activated" or "cancelled")) throw new ArgumentException("Change-request status is not supported."); return normalized; }
    private static string SerializeApiClientDefinition(ApiClientRegistrationDefinition definition) => JsonSerializer.Serialize(definition, PortalProfileChangeJsonOptions);
    private static ApiClientRegistrationDefinition ReadApiClientDefinition(string json) => JsonSerializer.Deserialize<ApiClientRegistrationDefinition>(json, PortalProfileChangeJsonOptions) ?? throw new InvalidOperationException("The stored API-client definition is invalid.");
    private static ApiClientChangeRequestItem ReadApiClientChangeRequest(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), ReadApiClientDefinition(reader.GetString(3)), reader.IsDBNull(4) ? null : ReadApiClientDefinition(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetFieldValue<DateTimeOffset>(9).ToString("O"), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11).ToString("O"), reader.GetString(12));
    private static async Task<ApiClientChangeRequestItem?> ReadApiClientChangeRequestAsync(NpgsqlConnection connection, Guid id, CancellationToken token) { await using var command = connection.CreateCommand(); command.CommandText = "select request_id,client_key,change_kind,proposed_definition,baseline_definition,baseline_updated_at,reason,status,version,created_at,created_by,updated_at,updated_by from api_client_change_requests where request_id=@id;"; command.Parameters.AddWithValue("id", id); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? ReadApiClientChangeRequest(reader) : null; }
    private static async Task WriteApiClientChangeEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string action, string? note, string user, CancellationToken token) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into api_client_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }
    private sealed record ApiClientCurrent(ApiClientRegistrationDefinition Definition, DateTimeOffset UpdatedAt);
    private static async Task<ApiClientCurrent?> GetApiClientForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken token) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select display_name,redirect_uri,scopes,active,updated_at from api_client_registry where client_key=@key for update;"; command.Parameters.AddWithValue("key", key); await using var reader = await command.ExecuteReaderAsync(token); return await reader.ReadAsync(token) ? new(new(key, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)), reader.GetFieldValue<DateTimeOffset>(4)) : null; }
    private static async Task ApplyApiClientDefinitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ApiClientRegistrationDefinition definition, string user, bool create, CancellationToken token) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = create ? "insert into api_client_registry(client_key,display_name,redirect_uri,scopes,active,updated_at,updated_by) values(@key,@name,@uri,@scopes,@active,now(),@user);" : "update api_client_registry set display_name=@name,redirect_uri=@uri,scopes=@scopes,active=@active,updated_at=now(),updated_by=@user where client_key=@key;"; command.Parameters.AddWithValue("key", definition.Key); command.Parameters.AddWithValue("name", definition.DisplayName); command.Parameters.AddWithValue("uri", definition.RedirectUri); command.Parameters.AddWithValue("scopes", definition.Scopes); command.Parameters.AddWithValue("active", definition.Active); command.Parameters.AddWithValue("user", user); await command.ExecuteNonQueryAsync(token); }

    public async Task<PracticeSettingsResponse> UpdatePracticeSettingAsync(string key, string value, string username, CancellationToken cancellationToken)
    {
        if (key is not ("practice.name" or "practice.default-facility-id" or "practice.time-zone")) throw new ArgumentException("The requested practice setting is not mutable.");
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A setting value is required.");
        if (key == "practice.default-facility-id" && (!int.TryParse(normalized, out var facilityId) || facilityId <= 0)) throw new ArgumentException("Default facility must be a valid facility identifier.");
        if (key == "practice.time-zone" && !TimeZoneInfo.GetSystemTimeZones().Any(zone => zone.Id == normalized)) throw new ArgumentException("Time zone must be a supported IANA or Windows time-zone identifier.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var existing = connection.CreateCommand(); existing.Transaction = transaction; existing.CommandText = "select setting_value from practice_settings where setting_key = @key for update;"; existing.Parameters.AddWithValue("key", key);
        var prior = await existing.ExecuteScalarAsync(cancellationToken) as string ?? throw new ArgumentException("The requested practice setting was not found.");
        if (prior != normalized) await WritePracticeSettingRevisionAsync(connection, transaction, key, prior, normalized, username, "updated", null, cancellationToken);
        await transaction.CommitAsync(cancellationToken); return await GetPracticeSettingsAsync(cancellationToken);
    }

    public async Task<PracticeSettingHistoryResponse> GetPracticeSettingHistoryAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var setting = await GetPracticeSettingAsync(connection, key, cancellationToken) ?? throw new ArgumentException("The requested practice setting was not found.");
        await using var command = connection.CreateCommand();
        command.CommandText = "select revision_id,value,prior_value,action,restored_from_revision_id,occurred_at,username from practice_setting_revisions where setting_key=@key order by occurred_at desc,revision_id desc;";
        command.Parameters.AddWithValue("key", key);
        var revisions = new List<PracticeSettingRevision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) revisions.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt64(4), reader.GetFieldValue<DateTimeOffset>(5).ToString("O"), reader.GetString(6)));
        return new PracticeSettingHistoryResponse(setting, revisions);
    }

    public async Task<PracticeSettingHistoryResponse> RollbackPracticeSettingAsync(string key, long revisionId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var targetValue = await GetPracticeSettingRevisionValueAsync(connection, transaction, key, revisionId, cancellationToken) ?? throw new ArgumentException("The requested revision was not found for this setting.");
        ValidatePracticeSettingValue(key, targetValue);
        var prior = await GetPracticeSettingValueForUpdateAsync(connection, transaction, key, cancellationToken) ?? throw new ArgumentException("The requested practice setting was not found.");
        if (prior != targetValue) await WritePracticeSettingRevisionAsync(connection, transaction, key, prior, targetValue, username, "rolled-back", revisionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPracticeSettingHistoryAsync(key, cancellationToken);
    }

    public async Task<PracticeSettingChangeRequestsResponse> GetPracticeSettingChangeRequestsAsync(
        string? settingKey,
        string? status,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(settingKey) ? null : settingKey.Trim();
        if (normalizedKey is not null)
        {
            ValidatePracticeSettingKey(normalizedKey);
        }

        if (offset < 0)
        {
            throw new ArgumentException("Change-request offset cannot be negative.");
        }

        if (limit is < 1 or > 100)
        {
            throw new ArgumentException("Change-request limit must be between 1 and 100.");
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? "all"
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("all" or "open" or "draft" or "submitted" or "approved" or "rejected" or "activated" or "cancelled"))
        {
            throw new ArgumentException("Change-request status is not supported.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var counts = await GetPracticeSettingChangeRequestCountsAsync(
            connection,
            normalizedKey,
            cancellationToken);

        await using var totalCommand = connection.CreateCommand();
        totalCommand.CommandText =
            """
            select count(*)::integer
            from practice_setting_change_requests
            where (@key is null or setting_key = @key)
              and (
                @status = 'all'
                or (@status = 'open' and status in ('draft', 'submitted', 'approved'))
                or status = @status
              );
            """;
        totalCommand.Parameters.Add("key", NpgsqlDbType.Text).Value =
            (object?)normalizedKey ?? DBNull.Value;
        totalCommand.Parameters.AddWithValue("status", normalizedStatus);
        var total = (int)(await totalCommand.ExecuteScalarAsync(cancellationToken) ?? 0);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
              request_id,
              setting_key,
              facility_id,
              proposed_value,
              baseline_value,
              baseline_updated_at,
              reason,
              status,
              version,
              created_at,
              created_by,
              updated_at,
              updated_by
            from practice_setting_change_requests
            where (@key is null or setting_key = @key)
              and (
                @status = 'all'
                or (@status = 'open' and status in ('draft', 'submitted', 'approved'))
                or status = @status
              )
            order by updated_at desc, request_id desc
            offset @offset
            limit @limit;
            """;
        command.Parameters.Add("key", NpgsqlDbType.Text).Value =
            (object?)normalizedKey ?? DBNull.Value;
        command.Parameters.AddWithValue("status", normalizedStatus);
        command.Parameters.AddWithValue("offset", offset);
        command.Parameters.AddWithValue("limit", limit);
        var requests = new List<PracticeSettingChangeRequestItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(ReadPracticeSettingChangeRequest(reader));
        }

        return new PracticeSettingChangeRequestsResponse(
            requests,
            total,
            requests.Count,
            offset,
            limit,
            normalizedStatus,
            normalizedKey,
            counts);
    }

    public async Task<PracticeSettingChangeRequestDetailResponse> CreatePracticeSettingChangeRequestAsync(
        string key,
        PracticeSettingChangeRequestCreateRequest request,
        string username,
        CancellationToken cancellationToken)
    {
        ValidatePracticeSettingValue(key, request.Value);
        var proposedValue = request.Value.Trim();
        var reason = ValidateChangeRequestReason(request.Reason, required: true)!;
        var requestId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var facilityId = request.FacilityId;
        var baseline = await GetPracticeSettingScopeValueAsync(
            connection,
            transaction,
            key,
            facilityId,
            cancellationToken);
        var baselineValue = baseline.Value;
        var baselineUpdatedAt = baseline.UpdatedAt;

        if (string.Equals(baselineValue, proposedValue, StringComparison.Ordinal))
        {
            throw new ArgumentException("The proposed value must differ from the active setting.");
        }

        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = transaction;
            duplicate.CommandText =
                """
                select exists(
                  select 1
                  from practice_setting_change_requests
                  where setting_key = @key
                    and facility_id is not distinct from @facilityId
                    and proposed_value = @value
                    and status in ('draft', 'submitted', 'approved')
                );
                """;
            duplicate.Parameters.AddWithValue("key", key);
            duplicate.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = (object?)facilityId ?? DBNull.Value;
            duplicate.Parameters.AddWithValue("value", proposedValue);
            if ((bool)(await duplicate.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                throw new PracticeSettingChangeRequestConflictException(
                    "An open change request already proposes this value.");
            }
        }

        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText =
                """
                insert into practice_setting_change_requests(
                  request_id,
                  setting_key,
                  facility_id,
                  proposed_value,
                  baseline_value,
                  baseline_updated_at,
                  reason,
                  status,
                  version,
                  created_at,
                  created_by,
                  updated_at,
                  updated_by)
                values(
                  @id,
                  @key,
                  @facilityId,
                  @value,
                  @baselineValue,
                  @baselineUpdatedAt,
                  @reason,
                  'draft',
                  0,
                  now(),
                  @user,
                  now(),
                  @user);
                """;
            create.Parameters.AddWithValue("id", requestId);
            create.Parameters.AddWithValue("key", key);
            create.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = (object?)facilityId ?? DBNull.Value;
            create.Parameters.AddWithValue("value", proposedValue);
            create.Parameters.AddWithValue("baselineValue", baselineValue);
            create.Parameters.AddWithValue("baselineUpdatedAt", baselineUpdatedAt);
            create.Parameters.AddWithValue("reason", reason);
            create.Parameters.AddWithValue("user", username);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await WritePracticeSettingChangeRequestEventAsync(
            connection,
            transaction,
            requestId,
            "created",
            reason,
            username,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPracticeSettingChangeRequestAsync(requestId, cancellationToken);
    }

    public async Task<PracticeSettingChangeRequestDetailResponse> GetPracticeSettingChangeRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var request = await GetPracticeSettingChangeRequestAsync(
            connection,
            requestId,
            cancellationToken) ?? throw new ArgumentException(
                "The requested practice-setting change request was not found.");
        var setting = await GetPracticeSettingAsync(
            connection,
            request.SettingKey,
            cancellationToken) ?? throw new ArgumentException(
                "The requested practice setting was not found.");
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select event_id, action, note, occurred_at, username
            from practice_setting_change_request_events
            where request_id = @id
            order by occurred_at desc, event_id desc;
            """;
        command.Parameters.AddWithValue("id", requestId);
        var events = new List<PracticeSettingChangeRequestEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new PracticeSettingChangeRequestEvent(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),
                reader.GetString(4)));
        }

        return new PracticeSettingChangeRequestDetailResponse(
            request,
            setting,
            events);
    }

    public Task<PracticeSettingChangeRequestDetailResponse> SubmitPracticeSettingChangeRequestAsync(
        Guid requestId,
        string? note,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken) =>
        TransitionPracticeSettingChangeRequestAsync(
            requestId,
            ["draft"],
            "submitted",
            note,
            noteRequired: false,
            expectedVersion,
            username,
            cancellationToken);

    public Task<PracticeSettingChangeRequestDetailResponse> ApprovePracticeSettingChangeRequestAsync(
        Guid requestId,
        string? note,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken) =>
        TransitionPracticeSettingChangeRequestAsync(
            requestId,
            ["submitted"],
            "approved",
            note,
            noteRequired: false,
            expectedVersion,
            username,
            cancellationToken);

    public Task<PracticeSettingChangeRequestDetailResponse> RejectPracticeSettingChangeRequestAsync(
        Guid requestId,
        string? note,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken) =>
        TransitionPracticeSettingChangeRequestAsync(
            requestId,
            ["submitted"],
            "rejected",
            note,
            noteRequired: true,
            expectedVersion,
            username,
            cancellationToken);

    public Task<PracticeSettingChangeRequestDetailResponse> ActivatePracticeSettingChangeRequestAsync(
        Guid requestId,
        string? note,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken) =>
        TransitionPracticeSettingChangeRequestAsync(
            requestId,
            ["approved"],
            "activated",
            note,
            noteRequired: false,
            expectedVersion,
            username,
            cancellationToken);

    public Task<PracticeSettingChangeRequestDetailResponse> CancelPracticeSettingChangeRequestAsync(
        Guid requestId,
        string? note,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken) =>
        TransitionPracticeSettingChangeRequestAsync(
            requestId,
            ["draft", "submitted", "approved"],
            "cancelled",
            note,
            noteRequired: true,
            expectedVersion,
            username,
            cancellationToken);

    private async Task<PracticeSettingChangeRequestDetailResponse> TransitionPracticeSettingChangeRequestAsync(
        Guid requestId,
        string[] expectedStatuses,
        string nextStatus,
        string? note,
        bool noteRequired,
        int? expectedVersion,
        string username,
        CancellationToken cancellationToken)
    {
        var normalizedNote = ValidateChangeRequestReason(note, noteRequired);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string settingKey;
        int? facilityId;
        string proposedValue;
        string baselineValue;
        DateTimeOffset baselineUpdatedAt;
        int currentVersion;

        await using (var current = connection.CreateCommand())
        {
            current.Transaction = transaction;
            current.CommandText =
                """
                select
                  setting_key,
                  facility_id,
                  proposed_value,
                  baseline_value,
                  baseline_updated_at,
                  status,
                  version
                from practice_setting_change_requests
                where request_id = @id
                for update;
                """;
            current.Parameters.AddWithValue("id", requestId);
            await using var reader = await current.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException(
                    "The requested practice-setting change request was not found.");
            }

            settingKey = reader.GetString(0);
            facilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            proposedValue = reader.GetString(2);
            baselineValue = reader.GetString(3);
            baselineUpdatedAt = reader.GetFieldValue<DateTimeOffset>(4);
            var currentStatus = reader.GetString(5);
            currentVersion = reader.GetInt32(6);
            if (!expectedStatuses.Contains(currentStatus, StringComparer.Ordinal))
            {
                throw new PracticeSettingChangeRequestConflictException(
                    $"The change request is {currentStatus}; it cannot move to {nextStatus}.");
            }

            if (expectedVersion is not null && expectedVersion.Value != currentVersion)
            {
                throw new PracticeSettingChangeRequestConflictException(
                    $"The change request changed after it was loaded. Current version is {currentVersion}.");
            }
        }

        ValidatePracticeSettingValue(settingKey, proposedValue);
        if (nextStatus == "activated")
        {
            var current = await GetPracticeSettingScopeValueAsync(
                connection,
                transaction,
                settingKey,
                facilityId,
                cancellationToken);
            var currentValue = current.Value;
            var currentUpdatedAt = current.UpdatedAt;

            if (!string.Equals(currentValue, baselineValue, StringComparison.Ordinal)
                || currentUpdatedAt.ToUniversalTime() != baselineUpdatedAt.ToUniversalTime())
            {
                throw new PracticeSettingChangeRequestConflictException(
                    "The active practice setting changed after this request was created. Cancel this stale request and create a new proposal.");
            }

            if (facilityId is null)
            {
                await WritePracticeSettingRevisionAsync(
                    connection,
                    transaction,
                    settingKey,
                    currentValue,
                    proposedValue,
                    username,
                    "activated",
                    null,
                    cancellationToken);
            }
            else
            {
                await WritePracticeSettingFacilityOverrideRevisionAsync(
                    connection,
                    transaction,
                    settingKey,
                    facilityId.Value,
                    currentValue,
                    proposedValue,
                    username,
                    cancellationToken);
            }
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                update practice_setting_change_requests
                set status = @status,
                    version = version + 1,
                    updated_at = now(),
                    updated_by = @user
                where request_id = @id;
                """;
            update.Parameters.AddWithValue("id", requestId);
            update.Parameters.AddWithValue("status", nextStatus);
            update.Parameters.AddWithValue("user", username);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await WritePracticeSettingChangeRequestEventAsync(
            connection,
            transaction,
            requestId,
            nextStatus,
            normalizedNote,
            username,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetPracticeSettingChangeRequestAsync(requestId, cancellationToken);
    }

    public async Task<bool> DeletePracticeSettingChangeRequestTestFixtureAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string proposedValue;
        string reason;
        string currentValue;

        await using (var current = connection.CreateCommand())
        {
            current.Transaction = transaction;
            current.CommandText =
                """
                select request.proposed_value, request.reason, setting.setting_value
                from practice_setting_change_requests request
                join practice_settings setting on setting.setting_key = request.setting_key
                where request.request_id = @id
                for update of request;
                """;
            current.Parameters.AddWithValue("id", requestId);
            await using var reader = await current.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            proposedValue = reader.GetString(0);
            reason = reader.GetString(1);
            currentValue = reader.GetString(2);
        }

        const string fixturePrefix = "TMP-ADM-SETTING-";
        if (!proposedValue.StartsWith(fixturePrefix, StringComparison.Ordinal)
            || !reason.StartsWith(fixturePrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only prefix-constrained ADM-01 development fixtures can be deleted.");
        }

        if (string.Equals(currentValue, proposedValue, StringComparison.Ordinal))
        {
            throw new PracticeSettingChangeRequestConflictException(
                "Restore the active practice setting before deleting its change-request fixture.");
        }

        await using (var deleteEvents = connection.CreateCommand())
        {
            deleteEvents.Transaction = transaction;
            deleteEvents.CommandText =
                "delete from practice_setting_change_request_events where request_id = @id;";
            deleteEvents.Parameters.AddWithValue("id", requestId);
            await deleteEvents.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteRequest = connection.CreateCommand())
        {
            deleteRequest.Transaction = transaction;
            deleteRequest.CommandText =
                "delete from practice_setting_change_requests where request_id = @id;";
            deleteRequest.Parameters.AddWithValue("id", requestId);
            await deleteRequest.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<PracticeSettingItem?> GetPracticeSettingAsync(NpgsqlConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "select setting_key,setting_value,value_type,updated_at,updated_by from practice_settings where setting_key=@key;"; command.Parameters.AddWithValue("key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPracticeSetting(reader) : null;
    }

    private static async Task<(string Value, DateTimeOffset UpdatedAt)> GetPracticeSettingScopeValueAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string key,
        int? facilityId,
        CancellationToken cancellationToken)
    {
        if (facilityId is not null)
        {
            await using var facility = connection.CreateCommand();
            facility.Transaction = transaction;
            facility.CommandText = "select exists(select 1 from facilities where id = @facilityId and inactive = false);";
            facility.Parameters.AddWithValue("facilityId", facilityId.Value);
            if (!(bool)(await facility.ExecuteScalarAsync(cancellationToken) ?? false))
            {
                throw new ArgumentException("The requested active facility was not found.");
            }
        }

        string systemValue;
        DateTimeOffset systemUpdatedAt;
        await using (var setting = connection.CreateCommand())
        {
            setting.Transaction = transaction;
            setting.CommandText = "select setting_value, updated_at from practice_settings where setting_key = @key for update;";
            setting.Parameters.AddWithValue("key", key);
            await using var reader = await setting.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("The requested practice setting was not found.");
            }
            systemValue = reader.GetString(0);
            systemUpdatedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }

        if (facilityId is null)
        {
            return (systemValue, systemUpdatedAt);
        }

        await using var overrideCommand = connection.CreateCommand();
        overrideCommand.Transaction = transaction;
        overrideCommand.CommandText = "select setting_value, updated_at from practice_setting_facility_overrides where setting_key = @key and facility_id = @facilityId for update;";
        overrideCommand.Parameters.AddWithValue("key", key);
        overrideCommand.Parameters.AddWithValue("facilityId", facilityId.Value);
        await using var overrideReader = await overrideCommand.ExecuteReaderAsync(cancellationToken);
        return await overrideReader.ReadAsync(cancellationToken)
            ? (overrideReader.GetString(0), overrideReader.GetFieldValue<DateTimeOffset>(1))
            : (systemValue, systemUpdatedAt);
    }

    private static async Task<string?> GetPracticeSettingValueForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select setting_value from practice_settings where setting_key=@key for update;"; command.Parameters.AddWithValue("key", key); return await command.ExecuteScalarAsync(cancellationToken) as string; }
    private static async Task<string?> GetPracticeSettingRevisionValueAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, long revisionId, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "select value from practice_setting_revisions where setting_key=@key and revision_id=@revision;"; command.Parameters.AddWithValue("key", key); command.Parameters.AddWithValue("revision", revisionId); return await command.ExecuteScalarAsync(cancellationToken) as string; }
    private static async Task WritePracticeSettingRevisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, string prior, string value, string username, string action, long? restoredFromRevisionId, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "update practice_settings set setting_value=@value,updated_at=now(),updated_by=@username where setting_key=@key; insert into practice_setting_audit_events(event_id,setting_key,prior_value,new_value,occurred_at,username) values(@eventId,@key,@prior,@value,now(),@username); insert into practice_setting_revisions(setting_key,value,prior_value,action,restored_from_revision_id,occurred_at,username) values(@key,@value,@prior,@action,@restored,now(),@username);"; command.Parameters.AddWithValue("key", key); command.Parameters.AddWithValue("value", value); command.Parameters.AddWithValue("prior", prior); command.Parameters.AddWithValue("username", username); command.Parameters.AddWithValue("eventId", Guid.NewGuid()); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("restored", (object?)restoredFromRevisionId ?? DBNull.Value); await command.ExecuteNonQueryAsync(cancellationToken); }
    private static async Task WritePracticeSettingFacilityOverrideRevisionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string key, int facilityId, string priorEffectiveValue, string value, string username, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into practice_setting_facility_overrides(setting_key,facility_id,setting_value,updated_at,updated_by) values(@key,@facilityId,@value,now(),@username) on conflict(setting_key,facility_id) do update set setting_value=excluded.setting_value,updated_at=excluded.updated_at,updated_by=excluded.updated_by; insert into practice_setting_facility_override_revisions(setting_key,facility_id,value,prior_effective_value,action,occurred_at,username) values(@key,@facilityId,@value,@prior,'activated',now(),@username);"; command.Parameters.AddWithValue("key", key); command.Parameters.AddWithValue("facilityId", facilityId); command.Parameters.AddWithValue("value", value); command.Parameters.AddWithValue("prior", priorEffectiveValue); command.Parameters.AddWithValue("username", username); await command.ExecuteNonQueryAsync(cancellationToken); }
    private static PracticeSettingChangeRequestItem ReadPracticeSettingChangeRequest(
        NpgsqlDataReader reader) => new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5).ToString("O"),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetFieldValue<DateTimeOffset>(9).ToString("O"),
            reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11).ToString("O"),
            reader.GetString(12));

    private static async Task<PracticeSettingChangeRequestItem?> GetPracticeSettingChangeRequestAsync(NpgsqlConnection connection, Guid requestId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
              request_id,
              setting_key,
              facility_id,
              proposed_value,
              baseline_value,
              baseline_updated_at,
              reason,
              status,
              version,
              created_at,
              created_by,
              updated_at,
              updated_by
            from practice_setting_change_requests
            where request_id = @id;
            """;
        command.Parameters.AddWithValue("id", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadPracticeSettingChangeRequest(reader)
            : null;
    }

    private static async Task<PracticeSettingChangeRequestCounts> GetPracticeSettingChangeRequestCountsAsync(
        NpgsqlConnection connection,
        string? settingKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
              count(*) filter (where status = 'draft')::integer,
              count(*) filter (where status = 'submitted')::integer,
              count(*) filter (where status = 'approved')::integer,
              count(*) filter (where status = 'rejected')::integer,
              count(*) filter (where status = 'activated')::integer,
              count(*) filter (where status = 'cancelled')::integer
            from practice_setting_change_requests
            where (@key is null or setting_key = @key);
            """;
        command.Parameters.Add("key", NpgsqlDbType.Text).Value =
            (object?)settingKey ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new PracticeSettingChangeRequestCounts(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    private static async Task WritePracticeSettingChangeRequestEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid requestId, string action, string? note, string username, CancellationToken cancellationToken)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into practice_setting_change_request_events(request_id,action,note,occurred_at,username) values(@id,@action,@note,now(),@user);"; command.Parameters.AddWithValue("id", requestId); command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value); command.Parameters.AddWithValue("user", username); await command.ExecuteNonQueryAsync(cancellationToken); }
    private static string? ValidateChangeRequestReason(string? value, bool required)
    { var normalized = value?.Trim(); if (required && string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A change-request reason is required."); if (normalized?.Length > 1000) throw new ArgumentException("A change-request note may not exceed 1000 characters."); return normalized; }
    private static PracticeSettingItem ReadPracticeSetting(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(0) switch { "practice.name" => "Practice name", "practice.default-facility-id" => "Default facility", _ => "Time zone" }, reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3).ToString("O"), reader.GetString(4));
    private static void ValidatePracticeSettingKey(string key)
    {
        if (key is not ("practice.name" or "practice.default-facility-id" or "practice.time-zone"))
        {
            throw new ArgumentException("The requested practice setting is not mutable.");
        }
    }

    private static void ValidatePracticeSettingValue(string key, string value)
    { ValidatePracticeSettingKey(key); var normalized = value.Trim(); if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A setting value is required."); if (key == "practice.default-facility-id" && (!int.TryParse(normalized, out var facilityId) || facilityId <= 0)) throw new ArgumentException("Default facility must be a valid facility identifier."); if (key == "practice.time-zone" && !TimeZoneInfo.GetSystemTimeZones().Any(zone => zone.Id == normalized)) throw new ArgumentException("Time zone must be a supported IANA or Windows time-zone identifier."); }

    public async Task<AdministrationDirectoryResponse> GetDirectoryAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await GetUsersAsync(connection, cancellationToken);
        var facilities = await GetFacilitiesAsync(connection, cancellationToken);
        var accessControl = await GetAccessControlAsync(connection, cancellationToken);
        var portalActivity = await GetPortalActivityAsync(connection, cancellationToken);

        return new AdministrationDirectoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            Counts: new AdministrationDirectoryCounts(
                Users: users.Count,
                Providers: users.Count(user => string.Equals(user.Role, "provider", StringComparison.OrdinalIgnoreCase)),
                CalendarUsers: users.Count(user => user.Calendar),
                Facilities: facilities.Count,
                AccessGroups: accessControl.Groups.Count,
                AccessPermissions: accessControl.Permissions.Count,
                AccessGroupPermissions: accessControl.GroupPermissions.Count,
                AccessUserMemberships: accessControl.UserMemberships.Count,
                WaitingPortalAudits: portalActivity.WaitingAuditCount,
                WaitingProfileReviews: portalActivity.WaitingProfileReviewCount),
            Users: users,
            Facilities: facilities,
            AccessControl: accessControl,
            PortalActivity: portalActivity);
    }

    public async Task<AdministrationPortalProfileReviewMutationResponse?> AcceptPortalProfileReviewAsync(
        long requestId,
        string actionUser,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText = """
            select
                id::text,
                patient_id,
                pid,
                requested_changes::text as requested_changes
            from patient_portal_profile_change_requests
            where id = @id
              and status = 'waiting'
              and activity = 'profile'
              and require_audit = 1
              and pending_action = 'review'
            for update;
            """;
        findCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;

        string id;
        string patientId;
        int legacyPid;
        PatientPortalProfileDemographics requestedDemographics;
        await using (var reader = await findCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetString(reader.GetOrdinal("id"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            requestedDemographics = JsonSerializer.Deserialize<PatientPortalProfileDemographics>(
                reader.GetString(reader.GetOrdinal("requested_changes")),
                PortalProfileChangeJsonOptions) ?? EmptyRequestedDemographics();
        }

        await using (var updatePatientCommand = connection.CreateCommand())
        {
            updatePatientCommand.Transaction = transaction;
            updatePatientCommand.CommandText = """
                update patients
                set first_name = @first_name,
                    last_name = @last_name,
                    preferred_name = @preferred_name,
                    date_of_birth = coalesce(@date_of_birth, date_of_birth),
                    sex = @sex,
                    email = @email,
                    street = @street,
                    city = @city,
                    state = @state,
                    postal_code = @postal_code,
                    phone_home = @phone_home,
                    phone_cell = @phone_cell,
                    phone = @phone_contact,
                    hipaa_allow_sms = @hipaa_allow_sms,
                    hipaa_allow_email = @hipaa_allow_email,
                    guardian_relationship = @guardian_relationship,
                    mother_name = @mother_name,
                    guardian_name = @guardian_name,
                    guardian_phone = @guardian_phone,
                    guardian_email = @guardian_email
                where canonical_id = @patient_id;
                """;
            updatePatientCommand.Parameters.Add("patient_id", NpgsqlDbType.Text).Value = patientId;
            updatePatientCommand.Parameters.Add("first_name", NpgsqlDbType.Text).Value =
                NormalizeRequired(requestedDemographics.FirstName, "First name");
            updatePatientCommand.Parameters.Add("last_name", NpgsqlDbType.Text).Value =
                NormalizeRequired(requestedDemographics.LastName, "Last name");
            AddNullableText(updatePatientCommand, "preferred_name", requestedDemographics.PreferredName);
            AddNullableDate(updatePatientCommand, "date_of_birth", requestedDemographics.DateOfBirth);
            AddNullableText(updatePatientCommand, "sex", requestedDemographics.Sex);
            AddNullableText(updatePatientCommand, "email", requestedDemographics.Email);
            AddNullableText(updatePatientCommand, "street", requestedDemographics.Street);
            AddNullableText(updatePatientCommand, "city", requestedDemographics.City);
            AddNullableText(updatePatientCommand, "state", requestedDemographics.State);
            AddNullableText(updatePatientCommand, "postal_code", requestedDemographics.PostalCode);
            AddNullableText(updatePatientCommand, "phone_home", requestedDemographics.PhoneHome);
            AddNullableText(updatePatientCommand, "phone_cell", requestedDemographics.PhoneCell);
            AddNullableText(updatePatientCommand, "phone_contact", requestedDemographics.PhoneContact);
            AddNullableText(updatePatientCommand, "hipaa_allow_sms", NormalizePermission(requestedDemographics.HipaaAllowSms));
            AddNullableText(updatePatientCommand, "hipaa_allow_email", NormalizePermission(requestedDemographics.HipaaAllowEmail));
            AddNullableText(
                updatePatientCommand,
                "guardian_relationship",
                requestedDemographics.GuardianRelationship ?? requestedDemographics.ContactRelationship);
            AddNullableText(updatePatientCommand, "mother_name", requestedDemographics.MotherName);
            AddNullableText(updatePatientCommand, "guardian_name", requestedDemographics.GuardianName);
            AddNullableText(updatePatientCommand, "guardian_phone", requestedDemographics.GuardianPhone);
            AddNullableText(updatePatientCommand, "guardian_email", requestedDemographics.GuardianEmail);

            if (await updatePatientCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        const string acceptedStatus = "closed";
        const string acceptedPendingAction = "completed";
        const string acceptedActionTaken = "accept";
        const string acceptedNarrative = "Changes reviewed and committed to demographics.";
        const string acceptedTableAction = "update";
        string acceptedAt;
        await using (var updateRequestCommand = connection.CreateCommand())
        {
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.CommandText = """
                update patient_portal_profile_change_requests
                set pending_action = @pending_action,
                    action_taken = @action_taken,
                    status = @status,
                    narrative = @narrative,
                    table_action = @table_action,
                    action_user = @action_user,
                    action_taken_at = now(),
                    updated_at = now()
                where id = @id
                returning to_char(action_taken_at, 'YYYY-MM-DD HH24:MI:SS') as action_taken_at;
                """;
            updateRequestCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;
            updateRequestCommand.Parameters.Add("pending_action", NpgsqlDbType.Text).Value = acceptedPendingAction;
            updateRequestCommand.Parameters.Add("action_taken", NpgsqlDbType.Text).Value = acceptedActionTaken;
            updateRequestCommand.Parameters.Add("status", NpgsqlDbType.Text).Value = acceptedStatus;
            updateRequestCommand.Parameters.Add("narrative", NpgsqlDbType.Text).Value = acceptedNarrative;
            updateRequestCommand.Parameters.Add("table_action", NpgsqlDbType.Text).Value = acceptedTableAction;
            updateRequestCommand.Parameters.Add("action_user", NpgsqlDbType.Text).Value = NormalizeRequired(actionUser, "Action user");
            acceptedAt = (string)(await updateRequestCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Profile review accept did not return an action timestamp."));
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationPortalProfileReviewMutationResponse(
            Id: id,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Status: acceptedStatus,
            PendingAction: acceptedPendingAction,
            ActionTaken: acceptedActionTaken,
            Narrative: acceptedNarrative,
            TableAction: acceptedTableAction,
            ActionUser: NormalizeRequired(actionUser, "Action user"),
            ActionTakenAt: acceptedAt,
            RequestedDemographics: requestedDemographics,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationPortalProfileReviewMutationResponse?> RevertPortalProfileReviewAsync(
        long requestId,
        string actionUser,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText = """
            select
                r.id::text,
                r.patient_id,
                r.pid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.date_of_birth,
                p.sex,
                p.email,
                p.street,
                p.city,
                p.state,
                p.postal_code,
                p.phone_home,
                p.phone_cell,
                p.phone as phone_contact,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                p.guardian_relationship as contact_relationship,
                p.mother_name,
                p.guardian_name,
                p.guardian_relationship,
                p.guardian_phone,
                p.guardian_email
            from patient_portal_profile_change_requests r
            join patients p on p.canonical_id = r.patient_id
            where r.id = @id
              and r.status = 'waiting'
              and r.activity = 'profile'
              and r.require_audit = 1
              and r.pending_action = 'review'
            for update;
            """;
        findCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;

        string id;
        string patientId;
        int legacyPid;
        PatientPortalProfileDemographics chartDemographics;
        await using (var reader = await findCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = reader.GetString(reader.GetOrdinal("id"));
            patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            chartDemographics = new PatientPortalProfileDemographics(
                FirstName: reader.GetString(reader.GetOrdinal("first_name")),
                LastName: reader.GetString(reader.GetOrdinal("last_name")),
                PreferredName: ReadNullableString(reader, "preferred_name"),
                DateOfBirth: ReadNullableDate(reader, "date_of_birth"),
                Sex: ReadNullableString(reader, "sex"),
                Email: ReadNullableString(reader, "email"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                PhoneHome: ReadNullableString(reader, "phone_home"),
                PhoneCell: ReadNullableString(reader, "phone_cell"),
                PhoneContact: ReadNullableString(reader, "phone_contact"),
                HipaaAllowSms: ReadNullableString(reader, "hipaa_allow_sms"),
                HipaaAllowEmail: ReadNullableString(reader, "hipaa_allow_email"),
                ContactRelationship: ReadNullableString(reader, "contact_relationship"),
                MotherName: ReadNullableString(reader, "mother_name"),
                GuardianName: ReadNullableString(reader, "guardian_name"),
                GuardianRelationship: ReadNullableString(reader, "guardian_relationship"),
                GuardianPhone: ReadNullableString(reader, "guardian_phone"),
                GuardianEmail: ReadNullableString(reader, "guardian_email"));
        }

        const string resolvedStatus = "closed";
        const string resolvedPendingAction = "completed";
        const string resolvedActionTaken = "accept";
        const string resolvedNarrative = "Changes reviewed and committed to demographics.";
        const string resolvedTableAction = "update";
        string resolvedAt;
        await using (var updateRequestCommand = connection.CreateCommand())
        {
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.CommandText = """
                update patient_portal_profile_change_requests
                set requested_changes = @requested_changes::jsonb,
                    pending_action = @pending_action,
                    action_taken = @action_taken,
                    status = @status,
                    narrative = @narrative,
                    table_action = @table_action,
                    action_user = @action_user,
                    action_taken_at = now(),
                    updated_at = now()
                where id = @id
                returning to_char(action_taken_at, 'YYYY-MM-DD HH24:MI:SS') as action_taken_at;
                """;
            updateRequestCommand.Parameters.Add("id", NpgsqlDbType.Bigint).Value = requestId;
            updateRequestCommand.Parameters.Add("requested_changes", NpgsqlDbType.Text).Value =
                JsonSerializer.Serialize(chartDemographics, PortalProfileChangeJsonOptions);
            updateRequestCommand.Parameters.Add("pending_action", NpgsqlDbType.Text).Value = resolvedPendingAction;
            updateRequestCommand.Parameters.Add("action_taken", NpgsqlDbType.Text).Value = resolvedActionTaken;
            updateRequestCommand.Parameters.Add("status", NpgsqlDbType.Text).Value = resolvedStatus;
            updateRequestCommand.Parameters.Add("narrative", NpgsqlDbType.Text).Value = resolvedNarrative;
            updateRequestCommand.Parameters.Add("table_action", NpgsqlDbType.Text).Value = resolvedTableAction;
            updateRequestCommand.Parameters.Add("action_user", NpgsqlDbType.Text).Value = NormalizeRequired(actionUser, "Action user");
            resolvedAt = (string)(await updateRequestCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Profile review revert did not return an action timestamp."));
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationPortalProfileReviewMutationResponse(
            Id: id,
            PatientId: patientId,
            LegacyPid: legacyPid,
            Status: resolvedStatus,
            PendingAction: resolvedPendingAction,
            ActionTaken: resolvedActionTaken,
            Narrative: resolvedNarrative,
            TableAction: resolvedTableAction,
            ActionUser: NormalizeRequired(actionUser, "Action user"),
            ActionTakenAt: resolvedAt,
            RequestedDemographics: chartDemographics,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse> CreateUserAsync(
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextStaffIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into staff
              (id, username, first_name, last_name, role, calendar, facility_id, email, npi, active)
            values
              (@id, @username, @firstName, @lastName, @role, @calendar, @facilityId, @email, @npi, @active)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddUserParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User create did not return an ID."));

        return new AdministrationUserMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationUserMutationResponse?> UpdateUserAsync(
        int userId,
        AdministrationUserMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update staff
            set username = @username,
                first_name = @firstName,
                last_name = @lastName,
                role = @role,
                calendar = @calendar,
                facility_id = @facilityId,
                email = @email,
                npi = @npi,
                active = @active
            where id = @userId
            returning id;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        AddUserParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationUserMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var membershipCommand = connection.CreateCommand())
        {
            membershipCommand.Transaction = transaction;
            membershipCommand.CommandText = """
                delete from access_user_memberships
                where staff_id = @userId;
                """;
            membershipCommand.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
            await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            delete from staff
            where id = @userId;
            """;

        command.Parameters.Add("userId", NpgsqlDbType.Integer).Value = userId;
        var deleted = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        await transaction.CommitAsync(cancellationToken);
        return deleted;
    }

    public async Task<AdministrationFacilityMutationResponse> CreateFacilityAsync(
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await GetNextFacilityIdAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into facilities
              (id, code, name, phone, street, city, state, postal_code, color, inactive)
            values
              (@id, @code, @name, @phone, @street, @city, @state, @postalCode, @color, @inactive)
            returning id;
            """;

        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = id;
        AddFacilityParameters(command, request, defaultActive: true);

        var insertedId = (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility create did not return an ID."));

        return new AdministrationFacilityMutationResponse(insertedId, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationFacilityMutationResponse?> UpdateFacilityAsync(
        int facilityId,
        AdministrationFacilityMutationRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update facilities
            set code = @code,
                name = @name,
                phone = @phone,
                street = @street,
                city = @city,
                state = @state,
                postal_code = @postalCode,
                color = @color,
                inactive = @inactive
            where id = @facilityId
            returning id;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        AddFacilityParameters(command, request, defaultActive: true);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
        {
            return null;
        }

        return new AdministrationFacilityMutationResponse((int)result, await GetDirectoryAsync(cancellationToken));
    }

    public async Task<bool> DeleteFacilityAsync(int facilityId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from facilities
            where id = @facilityId;
            """;

        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = facilityId;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<AdministrationAccessPermissionMutationResponse> GrantAccessGroupPermissionAsync(
        AdministrationAccessPermissionMutationRequest request,
        CancellationToken cancellationToken)
    {
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");
        var sectionValue = NormalizeAccessToken(request.SectionValue, "Permission section");
        var permissionValue = NormalizeAccessToken(request.PermissionValue, "Permission");
        var returnValue = NormalizeAccessReturnValue(request.ReturnValue);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await AccessGroupExistsAsync(connection, groupValue, cancellationToken))
        {
            throw new ArgumentException($"Access group '{groupValue}' was not found.");
        }

        var permissionName = await GetAccessPermissionNameAsync(connection, sectionValue, permissionValue, cancellationToken)
            ?? throw new ArgumentException($"Access permission '{sectionValue}:{permissionValue}' was not found.");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = """
                delete from access_group_permissions
                where group_value = @groupValue
                  and section_value = @sectionValue
                  and permission_value = @permissionValue;
                """;
            AddAccessAssignmentKeys(deleteCommand, groupValue, sectionValue, permissionValue);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                insert into access_group_permissions
                  (group_value, section_value, permission_value, permission_name, return_value)
                values
                  (@groupValue, @sectionValue, @permissionValue, @permissionName, @returnValue);
                """;
            AddAccessAssignmentKeys(insertCommand, groupValue, sectionValue, permissionValue);
            insertCommand.Parameters.Add("permissionName", NpgsqlDbType.Text).Value = permissionName;
            insertCommand.Parameters.Add("returnValue", NpgsqlDbType.Text).Value = returnValue;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new AdministrationAccessPermissionMutationResponse(
            GroupValue: groupValue,
            SectionValue: sectionValue,
            PermissionValue: permissionValue,
            ReturnValue: returnValue,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessPermissionMutationResponse?> RevokeAccessGroupPermissionAsync(
        string groupValue,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken)
    {
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");
        var normalizedSection = NormalizeAccessToken(sectionValue, "Permission section");
        var normalizedPermission = NormalizeAccessToken(permissionValue, "Permission");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from access_group_permissions
            where group_value = @groupValue
              and section_value = @sectionValue
              and permission_value = @permissionValue;
            """;
        AddAccessAssignmentKeys(command, normalizedGroup, normalizedSection, normalizedPermission);

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (deleted == 0)
        {
            return null;
        }

        return new AdministrationAccessPermissionMutationResponse(
            GroupValue: normalizedGroup,
            SectionValue: normalizedSection,
            PermissionValue: normalizedPermission,
            ReturnValue: null,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse> GrantAccessUserMembershipAsync(
        AdministrationAccessUserMembershipMutationRequest request,
        CancellationToken cancellationToken)
    {
        var userValue = NormalizeAccessToken(request.UserValue, "User");
        var groupValue = NormalizeAccessToken(request.GroupValue, "Group");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var groupName = await GetAccessGroupNameAsync(connection, groupValue, cancellationToken)
            ?? throw new ArgumentException($"Access group '{groupValue}' was not found.");
        var staff = await GetStaffAccessUserAsync(connection, userValue, cancellationToken)
            ?? throw new ArgumentException($"User '{userValue}' was not found.");

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into access_user_memberships
              (user_value, user_name, group_value, group_name, staff_id)
            values
              (@userValue, @userName, @groupValue, @groupName, @staffId)
            on conflict (user_value, group_value) do update
            set user_name = excluded.user_name,
                group_name = excluded.group_name,
                staff_id = excluded.staff_id;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = staff.UserValue;
        command.Parameters.Add("userName", NpgsqlDbType.Text).Value = staff.UserName;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        command.Parameters.Add("groupName", NpgsqlDbType.Text).Value = groupName;
        command.Parameters.Add("staffId", NpgsqlDbType.Integer).Value = staff.StaffId;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new AdministrationAccessUserMembershipMutationResponse(
            UserValue: staff.UserValue,
            GroupValue: groupValue,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    public async Task<AdministrationAccessUserMembershipMutationResponse?> RevokeAccessUserMembershipAsync(
        string userValue,
        string groupValue,
        CancellationToken cancellationToken)
    {
        var normalizedUser = NormalizeAccessToken(userValue, "User");
        var normalizedGroup = NormalizeAccessToken(groupValue, "Group");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from access_user_memberships
            where user_value = @userValue
              and group_value = @groupValue;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = normalizedUser;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = normalizedGroup;

        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (deleted == 0)
        {
            return null;
        }

        return new AdministrationAccessUserMembershipMutationResponse(
            UserValue: normalizedUser,
            GroupValue: normalizedGroup,
            Detail: await GetDirectoryAsync(cancellationToken));
    }

    private async Task<DatasetMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select dataset_id, version, base_date
            from dataset_metadata
            order by generated_at desc
            limit 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new DatasetMetadata("unseeded", "unknown", DateOnly.FromDateTime(DateTime.UtcNow));
        }

        return new DatasetMetadata(
            reader.GetString(reader.GetOrdinal("dataset_id")),
            reader.GetString(reader.GetOrdinal("version")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("base_date")));
    }

    private static async Task<IReadOnlyList<AdministrationUserItem>> GetUsersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                s.id,
                s.username,
                s.first_name,
                s.last_name,
                s.role,
                s.active,
                s.calendar,
                s.facility_id,
                f.name as facility_name,
                s.email,
                s.npi
            from staff s
            left join facilities f on f.id = s.facility_id
            order by s.id;
            """;

        var users = new List<AdministrationUserItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(reader.GetOrdinal("id"));
            var username = reader.GetString(reader.GetOrdinal("username"));
            var firstName = reader.GetString(reader.GetOrdinal("first_name"));
            var lastName = reader.GetString(reader.GetOrdinal("last_name"));
            var role = reader.GetString(reader.GetOrdinal("role"));
            var isProvider = string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);

            users.Add(new AdministrationUserItem(
                Id: id,
                Username: username,
                FirstName: firstName,
                LastName: lastName,
                DisplayName: $"{lastName}, {firstName}",
                Role: role,
                Authorized: isProvider,
                Active: reader.GetBoolean(reader.GetOrdinal("active")),
                Calendar: reader.GetBoolean(reader.GetOrdinal("calendar")),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Email: ReadNullableString(reader, "email") ?? $"{username}@{DefaultUserEmailDomain}",
                Npi: ReadNullableString(reader, "npi") ?? (isProvider ? $"18888{id}" : null)));
        }

        return users;
    }

    private static async Task<IReadOnlyList<AdministrationFacilityItem>> GetFacilitiesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, code, name, phone, street, city, state, postal_code, color, inactive
            from facilities
            order by id;
            """;

        var facilities = new List<AdministrationFacilityItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facilities.Add(new AdministrationFacilityItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Code: reader.GetString(reader.GetOrdinal("code")),
                Name: reader.GetString(reader.GetOrdinal("name")),
                Active: !reader.GetBoolean(reader.GetOrdinal("inactive")),
                Phone: ReadNullableString(reader, "phone"),
                Street: ReadNullableString(reader, "street"),
                City: ReadNullableString(reader, "city"),
                State: ReadNullableString(reader, "state"),
                PostalCode: ReadNullableString(reader, "postal_code"),
                Color: ReadNullableString(reader, "color")));
        }

        return facilities;
    }

    private static async Task<AdministrationAccessControlSummary> GetAccessControlAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var groups = new List<AdministrationAccessGroupItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    g.id,
                    g.value,
                    g.name,
                    g.parent_id,
                    count(gp.*) as permission_count
                from access_groups g
                left join access_group_permissions gp on gp.group_value = g.value
                group by g.id, g.value, g.name, g.parent_id
                order by g.id;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groups.Add(new AdministrationAccessGroupItem(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name")),
                    ParentId: ReadNullableInt(reader, "parent_id"),
                    PermissionCount: (int)reader.GetInt64(reader.GetOrdinal("permission_count"))));
            }
        }

        var permissions = new List<AdministrationAccessPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select section_value, value, name
                from access_permissions
                order by section_value, value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new AdministrationAccessPermissionItem(
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    Value: reader.GetString(reader.GetOrdinal("value")),
                    Name: reader.GetString(reader.GetOrdinal("name"))));
            }
        }

        var groupPermissions = new List<AdministrationAccessGroupPermissionItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select group_value, section_value, permission_value, permission_name, return_value
                from access_group_permissions
                order by group_value, section_value, permission_value, return_value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                groupPermissions.Add(new AdministrationAccessGroupPermissionItem(
                    GroupValue: reader.GetString(reader.GetOrdinal("group_value")),
                    SectionValue: reader.GetString(reader.GetOrdinal("section_value")),
                    PermissionValue: reader.GetString(reader.GetOrdinal("permission_value")),
                    PermissionName: reader.GetString(reader.GetOrdinal("permission_name")),
                    ReturnValue: reader.GetString(reader.GetOrdinal("return_value"))));
            }
        }

        var userMemberships = new List<AdministrationAccessUserMembershipItem>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select user_value, user_name, group_value, group_name, staff_id
                from access_user_memberships
                order by user_value, group_value;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                userMemberships.Add(new AdministrationAccessUserMembershipItem(
                    UserValue: reader.GetString(reader.GetOrdinal("user_value")),
                    UserName: reader.GetString(reader.GetOrdinal("user_name")),
                    GroupValue: reader.GetString(reader.GetOrdinal("group_value")),
                    GroupName: reader.GetString(reader.GetOrdinal("group_name")),
                    StaffId: ReadNullableInt(reader, "staff_id")));
            }
        }

        return new AdministrationAccessControlSummary(groups, permissions, groupPermissions, userMemberships);
    }

    private static async Task<AdministrationPortalActivitySummary> GetPortalActivityAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var waitingAuditCount = 0;
        var waitingProfileReviewCount = 0;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = """
                select
                    count(*) filter (where status = 'waiting') as waiting_audit_count,
                    count(*) filter (
                        where status = 'waiting'
                          and activity = 'profile'
                          and require_audit = 1
                          and pending_action = 'review'
                    ) as waiting_profile_review_count
                from patient_portal_profile_change_requests;
                """;

            await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                waitingAuditCount = (int)reader.GetInt64(reader.GetOrdinal("waiting_audit_count"));
                waitingProfileReviewCount = (int)reader.GetInt64(reader.GetOrdinal("waiting_profile_review_count"));
            }
        }

        var requests = new List<AdministrationPortalProfileReviewRequest>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select
                    r.id::text,
                    to_char(r.created_at, 'YYYY-MM-DD HH24:MI:SS') as requested_at,
                    r.patient_id,
                    r.pid,
                    p.pubpid,
                    p.first_name,
                    '' as middle_name,
                    p.last_name,
                    r.activity,
                    r.require_audit,
                    r.pending_action,
                    r.action_taken,
                    r.status,
                    r.narrative,
                    r.table_action,
                    nullif(r.action_user, '') as action_user,
                    case
                        when r.action_taken_at is null then null
                        else to_char(r.action_taken_at, 'YYYY-MM-DD HH24:MI:SS')
                    end as action_taken_at,
                    r.checksum,
                    r.requested_changes::text as requested_changes
                from patient_portal_profile_change_requests r
                join patients p on p.canonical_id = r.patient_id
                where r.status = 'waiting'
                  and r.activity = 'profile'
                  and r.require_audit = 1
                  and r.pending_action = 'review'
                order by r.created_at desc, r.id desc;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var requestedChanges = reader.GetString(reader.GetOrdinal("requested_changes"));
                var demographics = JsonSerializer.Deserialize<PatientPortalProfileDemographics>(
                    requestedChanges,
                    PortalProfileChangeJsonOptions) ?? EmptyRequestedDemographics();
                var firstName = reader.GetString(reader.GetOrdinal("first_name"));
                var lastName = reader.GetString(reader.GetOrdinal("last_name"));
                var middleName = reader.GetString(reader.GetOrdinal("middle_name"));
                var patientName = string.Join(
                    " ",
                    new[] { firstName, middleName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

                requests.Add(new AdministrationPortalProfileReviewRequest(
                    Id: reader.GetString(reader.GetOrdinal("id")),
                    RequestedAt: reader.GetString(reader.GetOrdinal("requested_at")),
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("pid")),
                    Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                    FirstName: firstName,
                    MiddleName: middleName,
                    LastName: lastName,
                    PatientName: patientName,
                    Activity: reader.GetString(reader.GetOrdinal("activity")),
                    RequireAudit: reader.GetInt32(reader.GetOrdinal("require_audit")),
                    PendingAction: reader.GetString(reader.GetOrdinal("pending_action")),
                    ActionTaken: reader.GetString(reader.GetOrdinal("action_taken")),
                    Status: reader.GetString(reader.GetOrdinal("status")),
                    Narrative: reader.GetString(reader.GetOrdinal("narrative")),
                    TableAction: reader.GetString(reader.GetOrdinal("table_action")),
                    ActionUser: ReadNullableString(reader, "action_user"),
                    ActionTakenAt: ReadNullableString(reader, "action_taken_at"),
                    Checksum: reader.GetString(reader.GetOrdinal("checksum")),
                    RequestedDemographics: demographics));
            }
        }

        return new AdministrationPortalActivitySummary(
            WaitingAuditCount: waitingAuditCount,
            WaitingProfileReviewCount: waitingProfileReviewCount,
            ProfileReviewRequests: requests);
    }

    private async Task<int> GetNextStaffIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from staff;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("User ID allocation failed."));
    }

    private async Task<int> GetNextFacilityIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select coalesce(max(id), 0) + 1 from facilities;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Facility ID allocation failed."));
    }

    private static void AddUserParameters(
        NpgsqlCommand command,
        AdministrationUserMutationRequest request,
        bool defaultActive)
    {
        var username = NormalizeRequired(request.Username, "Username");
        var role = NormalizeRequired(request.Role, "Role").ToLowerInvariant();
        command.Parameters.Add("username", NpgsqlDbType.Text).Value = username;
        command.Parameters.Add("firstName", NpgsqlDbType.Text).Value = NormalizeRequired(request.FirstName, "First name");
        command.Parameters.Add("lastName", NpgsqlDbType.Text).Value = NormalizeRequired(request.LastName, "Last name");
        command.Parameters.Add("role", NpgsqlDbType.Text).Value = role;
        command.Parameters.Add("calendar", NpgsqlDbType.Boolean).Value = request.Calendar ?? string.Equals(role, "provider", StringComparison.OrdinalIgnoreCase);
        AddNullableInt(command, "facilityId", request.FacilityId);
        AddNullableText(command, "email", request.Email ?? $"{username}@{DefaultUserEmailDomain}");
        AddNullableText(command, "npi", request.Npi);
        command.Parameters.Add("active", NpgsqlDbType.Boolean).Value = request.Active ?? defaultActive;
    }

    private static void AddFacilityParameters(
        NpgsqlCommand command,
        AdministrationFacilityMutationRequest request,
        bool defaultActive)
    {
        command.Parameters.Add("code", NpgsqlDbType.Text).Value = NormalizeRequired(request.Code, "Facility code");
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = NormalizeRequired(request.Name, "Facility name");
        AddNullableText(command, "phone", request.Phone);
        AddNullableText(command, "street", request.Street);
        AddNullableText(command, "city", request.City);
        AddNullableText(command, "state", request.State);
        AddNullableText(command, "postalCode", request.PostalCode);
        command.Parameters.Add("color", NpgsqlDbType.Text).Value = NormalizeOptional(request.Color) ?? DefaultFacilityColor;
        command.Parameters.Add("inactive", NpgsqlDbType.Boolean).Value = !(request.Active ?? defaultActive);
    }

    private static async Task<bool> AccessGroupExistsAsync(
        NpgsqlConnection connection,
        string groupValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
                select 1
                from access_groups
                where value = @groupValue
            );
            """;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<string?> GetAccessGroupNameAsync(
        NpgsqlConnection connection,
        string groupValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select name
            from access_groups
            where value = @groupValue
            limit 1;
            """;
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<AccessStaffUser?> GetStaffAccessUserAsync(
        NpgsqlConnection connection,
        string userValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, username, first_name, last_name
            from staff
            where lower(username) = @userValue
            limit 1;
            """;
        command.Parameters.Add("userValue", NpgsqlDbType.Text).Value = userValue;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        return new AccessStaffUser(
            StaffId: reader.GetInt32(reader.GetOrdinal("id")),
            UserValue: reader.GetString(reader.GetOrdinal("username")),
            UserName: $"{lastName}, {firstName}");
    }

    private static async Task<string?> GetAccessPermissionNameAsync(
        NpgsqlConnection connection,
        string sectionValue,
        string permissionValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select name
            from access_permissions
            where section_value = @sectionValue
              and value = @permissionValue
            limit 1;
            """;
        command.Parameters.Add("sectionValue", NpgsqlDbType.Text).Value = sectionValue;
        command.Parameters.Add("permissionValue", NpgsqlDbType.Text).Value = permissionValue;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static void AddAccessAssignmentKeys(
        NpgsqlCommand command,
        string groupValue,
        string sectionValue,
        string permissionValue)
    {
        command.Parameters.Add("groupValue", NpgsqlDbType.Text).Value = groupValue;
        command.Parameters.Add("sectionValue", NpgsqlDbType.Text).Value = sectionValue;
        command.Parameters.Add("permissionValue", NpgsqlDbType.Text).Value = permissionValue;
    }

    private static string NormalizeAccessToken(string? value, string label)
    {
        return NormalizeRequired(value, label).ToLowerInvariant();
    }

    private static string NormalizeAccessReturnValue(string? value)
    {
        var returnValue = NormalizeAccessToken(value, "Return value");
        return ValidAccessReturnValues.Contains(returnValue)
            ? returnValue
            : throw new ArgumentException($"Return value '{returnValue}' is not supported.");
    }

    private static PatientPortalProfileDemographics EmptyRequestedDemographics() =>
        new(
            FirstName: string.Empty,
            LastName: string.Empty,
            PreferredName: null,
            DateOfBirth: null,
            Sex: null,
            Email: null,
            Street: null,
            City: null,
            State: null,
            PostalCode: null,
            PhoneHome: null,
            PhoneCell: null,
            PhoneContact: null,
            HipaaAllowSms: null,
            HipaaAllowEmail: null,
            ContactRelationship: null,
            MotherName: null,
            GuardianName: null,
            GuardianRelationship: null,
            GuardianPhone: null,
            GuardianEmail: null);

    private static string? NormalizePermission(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) ? "YES" : "NO";
    }

    private static string NormalizeRequired(string? value, string label)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{label} is required.")
            : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = NormalizeOptional(value) is { } normalized
            ? normalized
            : DBNull.Value;
    }

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Integer).Value = value is { } integer
            ? integer
            : DBNull.Value;
    }

    private static void AddNullableDate(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Date).Value =
            DateOnly.TryParse(value, out var date) ? date : (object)DBNull.Value;
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ReadNullableDate(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record AccessStaffUser(int StaffId, string UserValue, string UserName);
}
