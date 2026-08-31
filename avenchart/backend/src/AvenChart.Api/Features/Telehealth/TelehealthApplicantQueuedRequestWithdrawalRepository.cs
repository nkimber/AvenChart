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
            return CreateResponse(source.RequestId, source.RequestVersion, source.DatabaseNow);
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
        return CreateResponse(requestId, nextVersion, withdrawnAt);
    }

    private static TelehealthApplicantQueuedRequestWithdrawalResponse CreateResponse(
        Guid requestId,
        int requestVersion,
        DateTimeOffset withdrawnAt) => new(
            requestId,
            requestVersion,
            TelehealthRequestStatus.Cancelled.ToString(),
            "NON_PRODUCTION",
            QueueEntryRemoved: true,
            ProvisionalAppointmentCancelled: true,
            ReservationCreated: false,
            ConnectionCreated: false,
            ConsultationCreated: false,
            ExternalActionPerformed: false,
            withdrawnAt,
            [
                "This is an access-key-owner synthetic withdrawal only.",
                "The request was removed only before a clinician reservation or connection.",
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
}
