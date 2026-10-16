// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthDemoRepository(NpgsqlDataSource source)
{
    public async Task<TelehealthDemoCreateResponse> CreateAsync(string practice, int facility, string commandId,
        string observerKey, string transport, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select pg_advisory_xact_lock(hashtext(@practice));";
        command.Parameters.AddWithValue("practice", $"telehealth-demo:{practice}:{facility}");
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.Clear();
        command.CommandText = """
            select run_id,expires_at,observer_key_hash,transport from telehealth_demo_runs
            where practice_id=@practice and facility_id=@facility and command_id=@command;
            """;
        command.Parameters.AddWithValue("practice", practice); command.Parameters.AddWithValue("facility", facility);
        command.Parameters.AddWithValue("command", commandId);
        Guid runId; DateTimeOffset expires;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(2) != TelehealthDemoPolicy.Hash(observerKey))
                    throw TelehealthProblem.Conflict("telehealth_demo_retry_conflict", "This demo command belongs to another launcher.");
                runId = reader.GetGuid(0); expires = reader.GetFieldValue<DateTimeOffset>(1); transport = reader.GetString(3);
            }
            else { runId = Guid.Empty; expires = default; }
        }
        if (runId == Guid.Empty)
        {
            command.CommandText = "select count(*) from telehealth_demo_runs where practice_id=@practice and facility_id=@facility and status='Active' and expires_at>now();";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(ct)) >= 50)
                throw TelehealthProblem.Conflict("telehealth_demo_capacity", "All synthetic demo rooms are in use. End an existing demo or wait for its expiry.");
            runId = Guid.NewGuid();
            command.CommandText = """
                insert into telehealth_demo_runs(run_id,practice_id,facility_id,observer_key_hash,command_id,transport)
                values(@run,@practice,@facility,@observer,@command,@transport) returning expires_at;
                """;
            command.Parameters.AddWithValue("run", runId); command.Parameters.AddWithValue("observer", TelehealthDemoPolicy.Hash(observerKey));
            command.Parameters.AddWithValue("transport", transport);
            expires = new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(ct))!);
            foreach (var role in new[] { "patient", "physician" })
            {
                await using var participant = connection.CreateCommand(); participant.Transaction = transaction;
                participant.CommandText = "insert into telehealth_demo_participants(participant_id,run_id,role,invitation_hash) values(@id,@run,@role,@invite);";
                participant.Parameters.AddWithValue("id", Guid.NewGuid()); participant.Parameters.AddWithValue("run", runId);
                participant.Parameters.AddWithValue("role", role); participant.Parameters.AddWithValue("invite", TelehealthDemoPolicy.Hash(TelehealthDemoPolicy.Invitation(observerKey, runId, role)));
                await participant.ExecuteNonQueryAsync(ct);
            }
        }
        await transaction.CommitAsync(ct);
        return new(runId, expires, TelehealthDemoPolicy.Invitation(observerKey, runId, "patient"), TelehealthDemoPolicy.Invitation(observerKey, runId, "physician"), transport);
    }

    public async Task<TelehealthDemoParticipant> RedeemAsync(string practice, int facility, Guid runId, string role,
        string invitation, string sessionKey, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            update telehealth_demo_participants participant set session_key_hash=@key,redeemed_at=coalesce(redeemed_at,now()),
              stage=case when redeemed_at is null then 'Opened' else stage end,last_seen_at=now()
            from telehealth_demo_runs run
            where participant.run_id=run.run_id and run.run_id=@run and run.practice_id=@practice and run.facility_id=@facility
              and run.status='Active' and run.expires_at>now() and participant.role=@role and participant.invitation_hash=@invitation
              and (participant.session_key_hash is null or participant.session_key_hash=@key)
            returning participant.participant_id,run.expires_at,run.transport;
            """);
        AddScope(command, practice, facility, runId); command.Parameters.AddWithValue("role", TelehealthDemoPolicy.RequireRole(role));
        command.Parameters.AddWithValue("invitation", TelehealthDemoPolicy.Hash(invitation)); command.Parameters.AddWithValue("key", TelehealthDemoPolicy.Hash(sessionKey));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw TelehealthProblem.NotFound();
        return new(reader.GetGuid(0), runId, role, reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2));
    }

    public async Task<TelehealthDemoParticipant> AuthorizeAsync(string practice, int facility, Guid runId, string role, string key, CancellationToken ct, bool active = true)
    {
        await using var command = source.CreateCommand("""
            select participant.participant_id,run.expires_at,run.transport from telehealth_demo_participants participant
            join telehealth_demo_runs run on run.run_id=participant.run_id
            where run.run_id=@run and run.practice_id=@practice and run.facility_id=@facility
              and participant.role=@role and participant.session_key_hash=@key
              and (not @active or (run.status='Active' and run.expires_at>now()));
            """);
        AddScope(command, practice, facility, runId); command.Parameters.AddWithValue("role", TelehealthDemoPolicy.RequireRole(role));
        command.Parameters.AddWithValue("key", TelehealthDemoPolicy.Hash(key)); command.Parameters.AddWithValue("active", active);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw TelehealthProblem.NotFound();
        return new(reader.GetGuid(0), runId, role, reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2));
    }

    public async Task<TelehealthDemoStatus> StatusAsync(string practice, int facility, Guid runId, string key, CancellationToken ct)
    {
        await using var command = source.CreateCommand("""
            select case when run.status='Ended' then 'Ended' when run.expires_at<=now() then 'Expired' else 'Active' end,
              run.expires_at,run.transport,
              case when patient.last_seen_at<now()-interval '20 seconds' then 'Offline' else patient.stage end,
              case when physician.last_seen_at<now()-interval '20 seconds' then 'Offline' else physician.stage end
            from telehealth_demo_runs run
            join telehealth_demo_participants patient on patient.run_id=run.run_id and patient.role='patient'
            join telehealth_demo_participants physician on physician.run_id=run.run_id and physician.role='physician'
            where run.run_id=@run and run.practice_id=@practice and run.facility_id=@facility
              and (run.observer_key_hash=@key or patient.session_key_hash=@key or physician.session_key_hash=@key);
            """);
        AddScope(command, practice, facility, runId); command.Parameters.AddWithValue("key", TelehealthDemoPolicy.Hash(key));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw TelehealthProblem.NotFound();
        return new(runId, reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(3), reader.GetString(4), reader.GetString(2));
    }

    public async Task HeartbeatAsync(Guid participantId, string stage, CancellationToken ct)
    {
        if (stage is not ("Opened" or "DevicesReady" or "Connected")) throw TelehealthProblem.BadRequest("telehealth_demo_stage", "Choose a valid synthetic device status.");
        await using var command = source.CreateCommand("update telehealth_demo_participants set stage=@stage,last_seen_at=now() where participant_id=@id;");
        command.Parameters.AddWithValue("stage", stage); command.Parameters.AddWithValue("id", participantId); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task EndAsync(Guid runId, CancellationToken ct)
    {
        await using var command = source.CreateCommand("update telehealth_demo_runs set status='Ended',ended_at=now() where run_id=@run and status='Active';");
        command.Parameters.AddWithValue("run", runId); await command.ExecuteNonQueryAsync(ct);
    }

    private static void AddScope(NpgsqlCommand command, string practice, int facility, Guid runId)
    {
        command.Parameters.AddWithValue("practice", practice); command.Parameters.AddWithValue("facility", facility); command.Parameters.AddWithValue("run", runId);
    }
}
