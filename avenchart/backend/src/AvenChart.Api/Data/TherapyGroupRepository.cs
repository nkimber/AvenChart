// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using AvenChart.Api.Models;
using AvenChart.Api.Persistence;
using AvenChart.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvenChart.Api.Data;

public sealed class TherapyGroupRepository(
    NpgsqlDataSource dataSource,
    AvenChartDbContext dbContext)
{
    public async Task<TherapyGroupsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var groups = await dbContext.TherapyGroups
            .AsNoTracking()
            .OrderByDescending(group => group.CreatedAt)
            .ToListAsync(cancellationToken);
        return new TherapyGroupsResponse(groups.Select(ToItem).ToList());
    }

    public async Task<TherapyGroupItem> CreateAsync(
        TherapyGroupCreateRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            throw new ArgumentException("Group name is required and must be 120 characters or fewer.");
        }

        await EnsureModuleEnabledAsync(cancellationToken);
        var group = new TherapyGroupEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = "active",
            FacilitatorId = request.FacilitatorId,
            Description = request.Description?.Trim(),
            Capacity = Math.Clamp(request.Capacity, 1, 200),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.TherapyGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(group);
    }

    public async Task<IReadOnlyList<TherapyGroupMemberItem>> GetMembersAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TherapyGroupMembers
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.JoinedAt)
            .Select(member => new
            {
                Member = member,
                member.Patient.LegacyPid,
                member.Patient.PreferredName,
                member.Patient.LastName
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TherapyGroupMemberItem(
            row.Member.GroupId,
            row.Member.PatientId,
            row.LegacyPid,
            DisplayName(row.PreferredName, row.LastName, row.Member.PatientId),
            row.Member.JoinedAt.ToString("O"))).ToList();
    }

    public async Task<TherapyGroupMemberItem> AddMemberAsync(
        Guid groupId,
        TherapyGroupMemberRequest request,
        CancellationToken cancellationToken)
    {
        var patientId = request.PatientId?.Trim();
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient identifier is required.");
        }

        await EnsureModuleEnabledAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var group = await dbContext.TherapyGroups
            .FromSqlInterpolated($"select * from therapy_groups where id = {groupId} for update")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException("Therapy group was not found.");
        if (!string.Equals(group.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Members can only be added to an active therapy group.");
        }

        var currentCount = await dbContext.TherapyGroupMembers.CountAsync(
            member => member.GroupId == groupId,
            cancellationToken);
        if (currentCount >= group.Capacity)
        {
            throw new ArgumentException("The therapy group is at capacity.");
        }

        var normalizedPatientId = patientId.ToLowerInvariant();
        var patient = await dbContext.Patients
            .AsNoTracking()
            .Where(candidate =>
                candidate.CanonicalId.ToLower() == normalizedPatientId ||
                candidate.PublicId.ToLower() == normalizedPatientId)
            .Select(candidate => new
            {
                candidate.CanonicalId,
                candidate.LegacyPid,
                candidate.PreferredName,
                candidate.LastName
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException("Patient was not found.");
        var joinedAt = DateTimeOffset.UtcNow;
        var member = new TherapyGroupMemberEntity
        {
            GroupId = groupId,
            PatientId = patient.CanonicalId,
            JoinedAt = joinedAt
        };
        dbContext.TherapyGroupMembers.Add(member);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ArgumentException("Patient is already a member of this therapy group.");
        }

        return new TherapyGroupMemberItem(
            groupId,
            patient.CanonicalId,
            patient.LegacyPid,
            DisplayName(patient.PreferredName, patient.LastName, patient.CanonicalId),
            joinedAt.ToString("O"));
    }

    public async Task<IReadOnlyList<TherapyGroupSessionItem>> GetSessionsAsync(
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.TherapyGroupSessions
            .AsNoTracking()
            .Where(session => session.GroupId == groupId)
            .OrderByDescending(session => session.StartsAt)
            .ToListAsync(cancellationToken);
        return sessions.Select(ToItem).ToList();
    }

    public async Task<TherapyGroupSessionItem> CreateSessionAsync(
        Guid groupId,
        TherapyGroupSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(request.StartsAt, out var startsAt))
        {
            throw new ArgumentException("A valid session start date and time is required.");
        }

        if (request.DurationMinutes is < 15 or > 480)
        {
            throw new ArgumentException("Session duration must be between 15 and 480 minutes.");
        }

        var topic = request.Topic?.Trim();
        if (topic?.Length > 400)
        {
            throw new ArgumentException("Session topic must be 400 characters or fewer.");
        }

        await EnsureModuleEnabledAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var groupStatus = await dbContext.TherapyGroups
            .AsNoTracking()
            .Where(group => group.Id == groupId)
            .Select(group => group.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (groupStatus is null)
        {
            throw new ArgumentException("Therapy group was not found.");
        }

        if (!string.Equals(groupStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Sessions can only be scheduled for an active therapy group.");
        }

        var session = new TherapyGroupSessionEntity
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            StartsAt = startsAt,
            DurationMinutes = request.DurationMinutes,
            Topic = topic,
            Status = "scheduled",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.TherapyGroupSessions.Add(session);
        var patientIds = await dbContext.TherapyGroupMembers
            .AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .Select(member => member.PatientId)
            .ToListAsync(cancellationToken);
        dbContext.TherapyGroupSessionAttendance.AddRange(patientIds.Select(patientId =>
            new TherapyGroupSessionAttendanceEntity
            {
                SessionId = session.Id,
                PatientId = patientId,
                AttendanceStatus = "unrecorded"
            }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToItem(session);
    }

    public async Task<TherapyGroupSessionAttendanceResponse> GetSessionAttendanceAsync(
        Guid groupId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.TherapyGroupSessions.AsNoTracking().AnyAsync(
            session => session.Id == sessionId && session.GroupId == groupId,
            cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Therapy-group session was not found.");
        }

        return new TherapyGroupSessionAttendanceResponse(
            sessionId,
            await ReadSessionAttendanceAsync(sessionId, cancellationToken));
    }

    public async Task<TherapyGroupSessionAttendanceItem> RecordSessionAttendanceAsync(
        Guid groupId,
        Guid sessionId,
        string patientId,
        TherapyGroupSessionAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("present" or "absent" or "excused"))
        {
            throw new ArgumentException("Attendance status must be present, absent, or excused.");
        }

        var note = request.Note?.Trim();
        if (note?.Length > 500)
        {
            throw new ArgumentException("Attendance note must be 500 characters or fewer.");
        }

        await EnsureModuleEnabledAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        _ = await GetScheduledSessionForUpdateAsync(groupId, sessionId, cancellationToken)
            ?? throw new ArgumentException("Scheduled therapy-group session was not found.");
        var attendance = await dbContext.TherapyGroupSessionAttendance
            .Include(item => item.Patient)
            .SingleOrDefaultAsync(
                item =>
                    item.SessionId == sessionId &&
                    item.PatientId == patientId,
                cancellationToken)
            ?? throw new ArgumentException("Scheduled session attendance participant was not found.");
        attendance.AttendanceStatus = status;
        attendance.Note = note;
        attendance.RecordedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToItem(attendance);
    }

    public async Task<TherapyGroupSessionItem> UpdateSessionStatusAsync(
        Guid groupId,
        Guid sessionId,
        TherapyGroupSessionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("completed" or "cancelled"))
        {
            throw new ArgumentException("Session status must be completed or cancelled.");
        }

        await EnsureModuleEnabledAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await GetScheduledSessionForUpdateAsync(groupId, sessionId, cancellationToken)
            ?? throw new ArgumentException("Scheduled therapy-group session was not found.");
        if (status == "completed" && await dbContext.TherapyGroupSessionAttendance.AsNoTracking().AnyAsync(
                attendance =>
                    attendance.SessionId == sessionId &&
                    attendance.AttendanceStatus == "unrecorded",
                cancellationToken))
        {
            throw new ArgumentException("Record attendance for every session participant before completing the session.");
        }

        session.Status = status;
        if (status == "completed")
        {
            var patientIds = await dbContext.TherapyGroupSessionAttendance
                .AsNoTracking()
                .Where(attendance =>
                    attendance.SessionId == sessionId &&
                    attendance.AttendanceStatus == "present")
                .Select(attendance => attendance.PatientId)
                .ToListAsync(cancellationToken);
            dbContext.TherapyGroupSessionParticipants.AddRange(patientIds.Select(patientId =>
                new TherapyGroupSessionParticipantEntity
                {
                    SessionId = sessionId,
                    PatientId = patientId
                }));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ArgumentException("The therapy-group session status changed before this update could be saved.");
        }

        return ToItem(session);
    }

    public async Task<IReadOnlyList<TherapyGroupSessionEncounterItem>> GetSessionEncountersAsync(
        Guid groupId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TherapyGroupSessionEncounters
            .AsNoTracking()
            .Where(encounter =>
                encounter.SessionId == sessionId &&
                dbContext.TherapyGroupSessions.Any(session =>
                    session.Id == encounter.SessionId && session.GroupId == groupId))
            .OrderBy(encounter => encounter.Patient.LastName)
            .ThenBy(encounter => encounter.Patient.FirstName)
            .Select(encounter => new
            {
                Encounter = encounter,
                encounter.Patient.LegacyPid,
                encounter.Patient.PreferredName,
                encounter.Patient.LastName
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TherapyGroupSessionEncounterItem(
            row.Encounter.SessionId,
            row.Encounter.PatientId,
            row.LegacyPid,
            DisplayName(row.PreferredName, row.LastName, row.Encounter.PatientId),
            row.Encounter.EncounterId,
            "existing")).ToList();
    }

    // Encounter creation reuses the session transaction so an encounter and its durable
    // therapy-session link either commit together or both roll back.
    public async Task<TherapyGroupSessionEncounterResponse> CreateSessionEncountersAsync(
        Guid groupId,
        Guid sessionId,
        TherapyGroupSessionEncounterRequest request,
        EncounterRepository encounterRepository,
        CancellationToken cancellationToken)
    {
        await EnsureModuleEnabledAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var sessionCommand = connection.CreateCommand();
        sessionCommand.Transaction = transaction;
        sessionCommand.CommandText = """
            select s.starts_at, coalesce(nullif(s.topic, ''), g.name)
            from therapy_group_sessions s inner join therapy_groups g on g.id = s.group_id
            where s.id = @sessionId and s.group_id = @groupId and s.status = 'completed' for update;
            """;
        sessionCommand.Parameters.AddWithValue("sessionId", sessionId);
        sessionCommand.Parameters.AddWithValue("groupId", groupId);
        await using var sessionReader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await sessionReader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException("Completed therapy-group session was not found.");
        }

        var sessionStartsAt = sessionReader.GetFieldValue<DateTimeOffset>(0).ToString("O");
        var sessionTopic = sessionReader.GetString(1);
        await sessionReader.DisposeAsync();
        await using var participantCommand = connection.CreateCommand();
        participantCommand.Transaction = transaction;
        participantCommand.CommandText = """
            select p.canonical_id, p.legacy_pid,
              coalesce(nullif(trim(concat_ws(' ', p.preferred_name, p.last_name)), ''), p.canonical_id), e.encounter_id
            from therapy_group_session_participants sp
            inner join patients p on p.canonical_id = sp.patient_id
            left join therapy_group_session_encounters e on e.session_id = sp.session_id and e.patient_id = sp.patient_id
            where sp.session_id = @sessionId order by p.last_name, p.first_name;
            """;
        participantCommand.Parameters.AddWithValue("sessionId", sessionId);
        var participants = new List<(string PatientId, int LegacyPid, string DisplayName, int? Encounter)>();
        await using var participantReader = await participantCommand.ExecuteReaderAsync(cancellationToken);
        while (await participantReader.ReadAsync(cancellationToken))
        {
            participants.Add((
                participantReader.GetString(0),
                participantReader.GetInt32(1),
                participantReader.GetString(2),
                participantReader.IsDBNull(3) ? null : participantReader.GetInt32(3)));
        }

        await participantReader.DisposeAsync();
        if (participants.Count == 0)
        {
            throw new ArgumentException("The completed session has no enrolled participant snapshot.");
        }

        var results = new List<TherapyGroupSessionEncounterItem>();
        foreach (var participant in participants)
        {
            if (participant.Encounter is not null)
            {
                results.Add(new TherapyGroupSessionEncounterItem(
                    sessionId,
                    participant.PatientId,
                    participant.LegacyPid,
                    participant.DisplayName,
                    participant.Encounter,
                    "existing"));
                continue;
            }

            var encounterId = await encounterRepository.CreateInTransactionAsync(
                connection,
                transaction,
                new EncounterCreateRequest(
                    participant.PatientId,
                    request.ProviderId,
                    sessionStartsAt,
                    $"Group therapy: {sessionTopic}",
                    request.FacilityId,
                    request.BillingFacilityId,
                    request.Sensitivity,
                    request.ReferralSource,
                    null,
                    request.PosCode,
                    request.BillingNote,
                    null),
                cancellationToken);
            if (encounterId is null)
            {
                results.Add(new TherapyGroupSessionEncounterItem(
                    sessionId,
                    participant.PatientId,
                    participant.LegacyPid,
                    participant.DisplayName,
                    null,
                    "failed"));
                continue;
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                insert into therapy_group_session_encounters
                  (session_id, patient_id, encounter_id, created_at)
                values (@sessionId, @patientId, @encounterId, @createdAt);
                """;
            insertCommand.Parameters.AddWithValue("sessionId", sessionId);
            insertCommand.Parameters.AddWithValue("patientId", participant.PatientId);
            insertCommand.Parameters.AddWithValue("encounterId", encounterId.Value);
            insertCommand.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            results.Add(new TherapyGroupSessionEncounterItem(
                sessionId,
                participant.PatientId,
                participant.LegacyPid,
                participant.DisplayName,
                encounterId.Value,
                "created"));
        }

        await transaction.CommitAsync(cancellationToken);
        return new TherapyGroupSessionEncounterResponse(sessionId, results);
    }

    private async Task<IReadOnlyList<TherapyGroupSessionAttendanceItem>> ReadSessionAttendanceAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TherapyGroupSessionAttendance
            .AsNoTracking()
            .Where(attendance => attendance.SessionId == sessionId)
            .OrderBy(attendance => attendance.Patient.LastName)
            .ThenBy(attendance => attendance.Patient.FirstName)
            .Select(attendance => new
            {
                Attendance = attendance,
                attendance.Patient.LegacyPid,
                attendance.Patient.PreferredName,
                attendance.Patient.LastName
            })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new TherapyGroupSessionAttendanceItem(
            row.Attendance.SessionId,
            row.Attendance.PatientId,
            row.LegacyPid,
            DisplayName(row.PreferredName, row.LastName, row.Attendance.PatientId),
            row.Attendance.AttendanceStatus,
            row.Attendance.Note,
            row.Attendance.RecordedAt?.ToString("O"))).ToList();
    }

    private Task<TherapyGroupSessionEntity?> GetScheduledSessionForUpdateAsync(
        Guid groupId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        dbContext.TherapyGroupSessions
            .FromSqlInterpolated($"""
                select *
                from therapy_group_sessions
                where id = {sessionId}
                  and group_id = {groupId}
                  and status = 'scheduled'
                for update
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task EnsureModuleEnabledAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
              select 1 from module_catalog
              where module_key = 'THERAPY_GROUPS' and status = 'enabled');
            """;
        if (!(bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
        {
            throw new ArgumentException("The Therapy Groups module is disabled.");
        }
    }

    private static TherapyGroupItem ToItem(TherapyGroupEntity group) =>
        new(
            group.Id,
            group.Name,
            group.Status,
            group.FacilitatorId,
            group.Description,
            group.Capacity,
            group.CreatedAt.ToString("O"));

    private static TherapyGroupSessionItem ToItem(TherapyGroupSessionEntity session) =>
        new(
            session.Id,
            session.GroupId,
            session.StartsAt.ToString("O"),
            session.DurationMinutes,
            session.Topic,
            session.Status,
            session.CreatedAt.ToString("O"));

    private static TherapyGroupSessionAttendanceItem ToItem(
        TherapyGroupSessionAttendanceEntity attendance) =>
        new(
            attendance.SessionId,
            attendance.PatientId,
            attendance.Patient.LegacyPid,
            DisplayName(
                attendance.Patient.PreferredName,
                attendance.Patient.LastName,
                attendance.PatientId),
            attendance.AttendanceStatus,
            attendance.Note,
            attendance.RecordedAt?.ToString("O"));

    private static string DisplayName(string? preferredName, string lastName, string patientId)
    {
        var displayName = $"{preferredName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? patientId : displayName;
    }
}
