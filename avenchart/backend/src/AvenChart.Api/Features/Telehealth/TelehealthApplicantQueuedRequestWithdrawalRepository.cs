// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

internal sealed record TelehealthApplicantQueuedRequestWithdrawalSource(
    string AccessKeyHash,
    string ApplicantStatus,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    TelehealthRequestStatus RequestStatus,
    int RequestVersion,
    string QueueStatus,
    string AppointmentStatus);

internal sealed record TelehealthApplicantPreAuthorizationRequestWithdrawalSource(
    string AccessKeyHash,
    string ApplicantStatus,
    int ApplicantVersion,
    DateTimeOffset ApplicantExpiresAt,
    DateTimeOffset DatabaseNow,
    Guid RequestId,
    TelehealthRequestStatus RequestStatus,
    int RequestVersion,
    int AuthorizationCount,
    int QueueEntryCount,
    int AppointmentCount,
    int ReservationCount,
    int ConnectionSessionCount,
    int ConsultationCount);

internal sealed record TelehealthApplicantRequestWithdrawalRoute(
    TelehealthRequestStatus RequestStatus,
    string? PriorAction);

public sealed class TelehealthApplicantQueuedRequestWithdrawalRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantQueuedRequestWithdrawalResponse> WithdrawAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        string participantSubjectHash,
        Guid requestId,
        int expectedRequestVersion,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        var route = await GetWithdrawalRouteAsync(
            practiceId, facilityId, applicantId, requestId, idempotencyKey, cancellationToken);
        return route is { RequestStatus: TelehealthRequestStatus.Queued }
            || route?.PriorAction == "synthetic-applicant-request-withdrawn-from-queue"
            ? await WithdrawQueuedAsync(
                practiceId, facilityId, applicantId, accessKeyHash, participantSubjectHash,
                requestId, expectedRequestVersion, idempotencyKey, commandFingerprint, cancellationToken)
            : await WithdrawBeforeQueueAuthorizationAsync(
                practiceId, facilityId, applicantId, accessKeyHash, participantSubjectHash,
                requestId, expectedRequestVersion, idempotencyKey, commandFingerprint, cancellationToken);
    }

    private async Task<TelehealthApplicantQueuedRequestWithdrawalResponse> WithdrawQueuedAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        string participantSubjectHash,
        Guid requestId,
        int expectedRequestVersion,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var source = await LoadForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, requestId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(source, accessKeyHash);
        RequireApplicant(source);

        var replay = await LoadEventFingerprintAsync(
            connection, transaction, requestId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_queued_withdrawal_idempotency_conflict",
                    "The idempotency key was already used with different command content.");
            }

            if (source.RequestStatus != TelehealthRequestStatus.Cancelled)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_queued_withdrawal_replay_conflict",
                    "The prior synthetic withdrawal did not produce the expected terminal request state.");
            }

            await transaction.CommitAsync(cancellationToken);
            return CreateResponse(source.RequestId, source.RequestVersion, source.DatabaseNow, queueEntryRemoved: true, provisionalAppointmentCancelled: true);
        }

        if (source.RequestVersion != expectedRequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_queued_withdrawal_version_conflict",
                "The queued synthetic request changed. Reload before withdrawing it.");
        }
        if (source.RequestStatus != TelehealthRequestStatus.Queued
            || source.QueueStatus != "Ready"
            || source.AppointmentStatus != "-")
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_queued_withdrawal_unavailable",
                "Only a ready queued synthetic request with an unstarted provisional appointment can be withdrawn.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.Queued, TelehealthRequestStatus.Cancelled);
        var nextVersion = source.RequestVersion + 1;
        DateTimeOffset withdrawnAt;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update telehealth_queue_entries
                set status='Removed', version=version+1, updated_at=now()
                where request_id=@requestId and status='Ready';
                update appointments
                set provider_id=null, status='x', row_version=row_version+1
                where id=(select appointment_id from telehealth_requests where request_id=@requestId)
                  and coalesce(status, '-')='-';
                update telehealth_requests
                set status='Cancelled', version=@nextVersion, updated_at=now()
                where request_id=@requestId and source_applicant_id=@applicantId
                  and status='Queued' and version=@expectedVersion
                returning updated_at;
                """;
            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("applicantId", applicantId);
            command.Parameters.AddWithValue("nextVersion", nextVersion);
            command.Parameters.AddWithValue("expectedVersion", expectedRequestVersion);
            var value = await command.ExecuteScalarAsync(cancellationToken)
                ?? throw TelehealthProblem.Conflict(
                    "telehealth_applicant_queued_withdrawal_version_conflict",
                    "The queued synthetic request changed. Reload before withdrawing it.");
            withdrawnAt = value switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException("The synthetic withdrawal time had an unexpected database type.")
            };
        }

        await using (var eventCommand = connection.CreateCommand())
        {
            eventCommand.Transaction = transaction;
            eventCommand.CommandText = """
                insert into telehealth_request_events(
                  event_id,request_id,aggregate_version,action,from_status,to_status,
                  actor_type,actor_id,idempotency_key,command_fingerprint)
                values(
                  @eventId,@requestId,@nextVersion,'synthetic-applicant-request-withdrawn-from-queue',
                  'Queued','Cancelled','patient',@participantSubjectHash,@idempotencyKey,@commandFingerprint);
                """;
            eventCommand.Parameters.AddWithValue("eventId", Guid.NewGuid());
            eventCommand.Parameters.AddWithValue("requestId", requestId);
            eventCommand.Parameters.AddWithValue("nextVersion", nextVersion);
            eventCommand.Parameters.AddWithValue("participantSubjectHash", participantSubjectHash);
            eventCommand.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            eventCommand.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await eventCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return CreateResponse(requestId, nextVersion, withdrawnAt, queueEntryRemoved: true, provisionalAppointmentCancelled: true);
    }

    private async Task<TelehealthApplicantQueuedRequestWithdrawalResponse> WithdrawBeforeQueueAuthorizationAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        string participantSubjectHash,
        Guid requestId,
        int expectedRequestVersion,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var source = await LoadPreAuthorizationForUpdateAsync(
            connection, transaction, practiceId, facilityId, applicantId, requestId, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(source, accessKeyHash);
        RequireApplicant(source);

        var replay = await LoadEventFingerprintAsync(
            connection, transaction, requestId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_pre_authorization_withdrawal_idempotency_conflict",
                    "The idempotency key was already used with different command content.");
            }
            if (source.RequestStatus != TelehealthRequestStatus.Cancelled)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_pre_authorization_withdrawal_replay_conflict",
                    "The prior synthetic withdrawal did not produce the expected terminal request state.");
            }

            await transaction.CommitAsync(cancellationToken);
            return CreateResponse(source.RequestId, source.RequestVersion, source.DatabaseNow, queueEntryRemoved: false, provisionalAppointmentCancelled: false);
        }

        if (source.RequestVersion != expectedRequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_authorization_withdrawal_version_conflict",
                "The operational-review synthetic request changed. Reload before withdrawing it.");
        }
        if (source.RequestStatus != TelehealthRequestStatus.OperationalReview
            || source.RequestVersion != 12
            || source.AuthorizationCount != 0
            || source.QueueEntryCount != 0
            || source.AppointmentCount != 0
            || source.ReservationCount != 0
            || source.ConnectionSessionCount != 0
            || source.ConsultationCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_authorization_withdrawal_unavailable",
                "Only an operational-review synthetic request before practice queue authorization can be withdrawn.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.OperationalReview, TelehealthRequestStatus.Cancelled);
        var nextVersion = source.RequestVersion + 1;
        DateTimeOffset withdrawnAt;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update telehealth_requests
                set status='Cancelled', version=@nextVersion, updated_at=now()
                where request_id=@requestId and source_applicant_id=@applicantId
                  and status='OperationalReview' and version=12
                returning updated_at;
                """;
            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("applicantId", applicantId);
            command.Parameters.AddWithValue("nextVersion", nextVersion);
            var value = await command.ExecuteScalarAsync(cancellationToken)
                ?? throw TelehealthProblem.Conflict(
                    "telehealth_applicant_pre_authorization_withdrawal_version_conflict",
                    "The operational-review synthetic request changed. Reload before withdrawing it.");
            withdrawnAt = value switch
            {
                DateTimeOffset offset => offset,
                DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
                _ => throw new InvalidOperationException("The synthetic withdrawal time had an unexpected database type.")
            };
        }

        await InsertWithdrawalEventAsync(
            connection, transaction, requestId, nextVersion,
            "synthetic-applicant-request-withdrawn-before-queue-authorization",
            "OperationalReview", participantSubjectHash, idempotencyKey, commandFingerprint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateResponse(requestId, nextVersion, withdrawnAt, queueEntryRemoved: false, provisionalAppointmentCancelled: false);
    }

    private static TelehealthApplicantQueuedRequestWithdrawalResponse CreateResponse(
        Guid requestId,
        int requestVersion,
        DateTimeOffset withdrawnAt,
        bool queueEntryRemoved,
        bool provisionalAppointmentCancelled) => new(
            requestId,
            requestVersion,
            TelehealthRequestStatus.Cancelled.ToString(),
            "NON_PRODUCTION",
            QueueEntryRemoved: queueEntryRemoved,
            ProvisionalAppointmentCancelled: provisionalAppointmentCancelled,
            ReservationCreated: false,
            ConnectionCreated: false,
            ConsultationCreated: false,
            ExternalActionPerformed: false,
            withdrawnAt,
            [
                "This is an access-key-owner synthetic withdrawal only.",
                "The request was withdrawn only before a clinician reservation or connection.",
                queueEntryRemoved
                    ? "The ready synthetic queue entry and provisional appointment were removed before clinician work started."
                    : "Practice queue authorization, a queue entry, and a provisional appointment were not created.",
                "No consultation, care, prescription, billing, claim, integration, notification, or external action occurred."
            ]);

    private static async Task<TelehealthApplicantQueuedRequestWithdrawalSource?> LoadForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.access_key_hash,a.status,a.version,a.expires_at,now(),
                   r.request_id,r.status,r.version,q.status,coalesce(appointment.status,'-')
            from telehealth_prospective_applicants a
            join telehealth_requests r
              on r.source_applicant_id=a.applicant_id and r.practice_id=a.practice_id
             and r.facility_id=a.facility_id
            join telehealth_queue_entries q
              on q.request_id=r.request_id and q.practice_id=r.practice_id and q.facility_id=r.facility_id
            join appointments appointment
              on appointment.id=r.appointment_id and appointment.patient_id=r.patient_id
             and appointment.facility_id=r.facility_id
            where a.applicant_id=@applicantId and a.practice_id=@practiceId and a.facility_id=@facilityId
              and r.request_id=@requestId
            for update of a,r,q,appointment;
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("requestId", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetString(0), reader.GetString(1), checked((int)reader.GetInt64(2)),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetGuid(5), Enum.Parse<TelehealthRequestStatus>(reader.GetString(6)),
                checked((int)reader.GetInt64(7)), reader.GetString(8), reader.GetString(9))
            : null;
    }

    private static async Task<TelehealthApplicantPreAuthorizationRequestWithdrawalSource?> LoadPreAuthorizationForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select a.access_key_hash,a.status,a.version,a.expires_at,now(),
                   r.request_id,r.status,r.version,
                   (select count(*)::int from telehealth_applicant_request_queue_authorizations authorization
                     where authorization.request_id=r.request_id),
                   (select count(*)::int from telehealth_queue_entries entry
                     where entry.request_id=r.request_id),
                   (select count(*)::int from appointments appointment
                     where appointment.id=r.appointment_id and appointment.patient_id=r.patient_id
                       and appointment.facility_id=r.facility_id),
                   (select count(*)::int from telehealth_reservations reservation
                     where reservation.request_id=r.request_id),
                   (select count(*)::int from telehealth_video_sessions session
                     where session.request_id=r.request_id),
                   (select count(*)::int from telehealth_consultation_contexts consultation
                     where consultation.request_id=r.request_id)
            from telehealth_requests r
            join telehealth_prospective_applicants a
              on a.applicant_id=r.source_applicant_id and a.practice_id=r.practice_id
             and a.facility_id=r.facility_id
            where r.request_id=@requestId and r.practice_id=@practiceId and r.facility_id=@facilityId
              and a.applicant_id=@applicantId
            for update of r,a;
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetString(0), reader.GetString(1), checked((int)reader.GetInt64(2)),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetGuid(5), Enum.Parse<TelehealthRequestStatus>(reader.GetString(6)),
                checked((int)reader.GetInt64(7)), reader.GetInt32(8), reader.GetInt32(9),
                reader.GetInt32(10), reader.GetInt32(11), reader.GetInt32(12), reader.GetInt32(13))
            : null;
    }

    private async Task<TelehealthApplicantRequestWithdrawalRoute?> GetWithdrawalRouteAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        Guid requestId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select r.status,
                   (select event.action from telehealth_request_events event
                     where event.request_id=r.request_id and event.idempotency_key=@idempotencyKey)
            from telehealth_requests r
            join telehealth_prospective_applicants a
              on a.applicant_id=r.source_applicant_id and a.practice_id=r.practice_id
             and a.facility_id=r.facility_id
            where r.request_id=@requestId and r.practice_id=@practiceId and r.facility_id=@facilityId
              and a.applicant_id=@applicantId;
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                Enum.Parse<TelehealthRequestStatus>(reader.GetString(0)),
                reader.IsDBNull(1) ? null : reader.GetString(1))
            : null;
    }

    private static async Task InsertWithdrawalEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        int nextVersion,
        string action,
        string fromStatus,
        string participantSubjectHash,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_request_events(
              event_id,request_id,aggregate_version,action,from_status,to_status,
              actor_type,actor_id,idempotency_key,command_fingerprint)
            values(
              @eventId,@requestId,@nextVersion,@action,@fromStatus,'Cancelled',
              'patient',@participantSubjectHash,@idempotencyKey,@commandFingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("nextVersion", nextVersion);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("fromStatus", fromStatus);
        command.Parameters.AddWithValue("participantSubjectHash", participantSubjectHash);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> LoadEventFingerprintAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select command_fingerprint from telehealth_request_events
            where request_id=@requestId and idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static void RequireAccess(TelehealthApplicantQueuedRequestWithdrawalSource source, string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(source.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireAccess(TelehealthApplicantPreAuthorizationRequestWithdrawalSource source, string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(source.AccessKeyHash, accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantQueuedRequestWithdrawalSource source)
    {
        if (source.ApplicantExpiresAt <= source.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (source.ApplicantStatus != TelehealthApplicantRequestQueueStatusPolicy.ApplicantStatus
            || source.ApplicantVersion != TelehealthApplicantRequestQueueStatusPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_queued_withdrawal_state_conflict",
                "This applicant cannot withdraw the synthetic request in its current state.");
        }
    }

    private static void RequireApplicant(TelehealthApplicantPreAuthorizationRequestWithdrawalSource source)
    {
        if (source.ApplicantExpiresAt <= source.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (source.ApplicantStatus != TelehealthApplicantRequestQueueStatusPolicy.ApplicantStatus
            || source.ApplicantVersion != TelehealthApplicantRequestQueueStatusPolicy.ApplicantVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_pre_authorization_withdrawal_state_conflict",
                "This applicant cannot withdraw the synthetic request in its current state.");
        }
    }
}
