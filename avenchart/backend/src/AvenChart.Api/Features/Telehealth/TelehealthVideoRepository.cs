// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthVideoContextRecord(
    Guid SessionId,
    Guid RequestId,
    Guid ReservationId,
    int RequestVersion,
    string RequestStatus,
    DateTimeOffset SessionExpiresAt,
    DateTimeOffset GrantExpiresAt);

public sealed record TelehealthVideoGrantRecord(
    Guid SessionId,
    Guid GrantId,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string ParticipantRole,
    DateTimeOffset ExpiresAt);

public sealed class TelehealthVideoRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthVideoContextRecord> PreparePatientContextAsync(
        string practiceId,
        int facilityId,
        string patientId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        await PrepareContextAsync(
            practiceId, facilityId, requestId, patientId, null, null, cancellationToken);

    public async Task<TelehealthVideoContextRecord> PrepareApplicantContextAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        Guid requestId,
        CancellationToken cancellationToken) =>
        await PrepareContextAsync(
            practiceId,
            facilityId,
            requestId,
            null,
            (applicantId, accessKeyHash),
            null,
            cancellationToken);

    public async Task<TelehealthVideoContextRecord> PreparePhysicianContextAsync(
        string practiceId,
        int facilityId,
        int clinicianStaffId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        await PrepareContextAsync(
            practiceId,
            facilityId,
            null,
            null,
            null,
            (reservationId, clinicianStaffId),
            cancellationToken);

    public async Task<TelehealthVideoGrantRecord> IssueGrantAsync(
        TelehealthVideoContextRecord context,
        int expectedVersion,
        string participantRole,
        string participantSubjectHash,
        PrepareTelehealthConnectionRequest request,
        Guid grantId,
        TelehealthVideoProvision provision,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LoadContextForUpdateAsync(
            connection, transaction, context.SessionId, context.ReservationId, cancellationToken)
            ?? throw TelehealthProblem.Conflict(
                "telehealth_video_context_stale",
                "The reserved connection context is no longer available. Refresh before trying again.");

        if (context.GrantExpiresAt > current.SessionExpiresAt
            || context.GrantExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(1))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_reservation_expired",
                "The physician reservation expired before the connection grant could be issued.");
        }

        var prior = await LoadGrantByIdempotencyAsync(
            connection,
            transaction,
            context.SessionId,
            participantRole,
            participantSubjectHash,
            idempotencyKey,
            cancellationToken);
        if (prior is not null)
        {
            if (!string.Equals(prior.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal)
                || !string.Equals(prior.Value.ProviderInstanceId, provision.ProviderInstanceId, StringComparison.Ordinal)
                || !string.Equals(prior.Value.CredentialHash, provision.JoinCredentialHash, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_video_idempotency_conflict",
                    "The connection command cannot be replayed with changed content or after the simulator process changed.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new TelehealthVideoGrantRecord(
                context.SessionId,
                prior.Value.GrantId,
                current.RequestId,
                current.RequestVersion,
                current.RequestStatus,
                participantRole,
                prior.Value.ExpiresAt);
        }

        if (current.RequestStatus == TelehealthRequestStatus.Reserved.ToString()
            && current.RequestVersion != expectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_version_conflict",
                "The request changed before the connection room could be prepared. Refresh and try again.");
        }
        if (current.RequestStatus is not ("Reserved" or "Connecting"))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_state_invalid",
                "A connection grant requires a reserved or connecting request.");
        }

        await ExpirePriorGrantsAsync(
            connection, transaction, context.SessionId, participantRole, participantSubjectHash, cancellationToken);

        var preflightId = Guid.NewGuid();
        await using (var preflight = connection.CreateCommand())
        {
            preflight.Transaction = transaction;
            preflight.CommandText = """
                insert into telehealth_video_preflights(
                  preflight_id,session_id,participant_role,participant_subject_hash,
                  browser_supported,camera_available,microphone_available,speaker_available,
                  network_quality,synthetic_data_confirmed,idempotency_key,command_fingerprint)
                values(@preflightId,@sessionId,@role,@subjectHash,
                       @browser,@camera,@microphone,@speaker,
                       @network,@synthetic,@key,@fingerprint);
                """;
            preflight.Parameters.AddWithValue("preflightId", preflightId);
            preflight.Parameters.AddWithValue("sessionId", context.SessionId);
            preflight.Parameters.AddWithValue("role", participantRole);
            preflight.Parameters.AddWithValue("subjectHash", participantSubjectHash);
            preflight.Parameters.AddWithValue("browser", request.BrowserSupported);
            preflight.Parameters.AddWithValue("camera", request.CameraAvailable);
            preflight.Parameters.AddWithValue("microphone", request.MicrophoneAvailable);
            preflight.Parameters.AddWithValue("speaker", request.SpeakerAvailable);
            preflight.Parameters.AddWithValue("network", request.NetworkQuality);
            preflight.Parameters.AddWithValue("synthetic", request.SyntheticDataConfirmed);
            preflight.Parameters.AddWithValue("key", idempotencyKey);
            preflight.Parameters.AddWithValue("fingerprint", commandFingerprint);
            await preflight.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var grant = connection.CreateCommand())
        {
            grant.Transaction = transaction;
            grant.CommandText = """
                insert into telehealth_video_participant_grants(
                  grant_id,session_id,preflight_id,participant_role,participant_subject_hash,
                  provider_instance_id,credential_hash,status,expires_at,idempotency_key,command_fingerprint)
                values(@grantId,@sessionId,@preflightId,@role,@subjectHash,
                       @providerInstance,@credentialHash,'Issued',@expiresAt,@key,@fingerprint);
                """;
            grant.Parameters.AddWithValue("grantId", grantId);
            grant.Parameters.AddWithValue("sessionId", context.SessionId);
            grant.Parameters.AddWithValue("preflightId", preflightId);
            grant.Parameters.AddWithValue("role", participantRole);
            grant.Parameters.AddWithValue("subjectHash", participantSubjectHash);
            grant.Parameters.AddWithValue("providerInstance", provision.ProviderInstanceId);
            grant.Parameters.AddWithValue("credentialHash", provision.JoinCredentialHash);
            grant.Parameters.AddWithValue("expiresAt", context.GrantExpiresAt);
            grant.Parameters.AddWithValue("key", idempotencyKey);
            grant.Parameters.AddWithValue("fingerprint", commandFingerprint);
            await grant.ExecuteNonQueryAsync(cancellationToken);
        }

        var requestVersion = current.RequestVersion;
        var requestStatus = current.RequestStatus;
        if (current.RequestStatus == TelehealthRequestStatus.Reserved.ToString())
        {
            TelehealthRequestStateMachine.RequireTransition(
                TelehealthRequestStatus.Reserved, TelehealthRequestStatus.Connecting);
            requestVersion++;
            requestStatus = TelehealthRequestStatus.Connecting.ToString();
            await using var updateRequest = connection.CreateCommand();
            updateRequest.Transaction = transaction;
            updateRequest.CommandText = """
                update telehealth_requests
                set status='Connecting',version=@newVersion,updated_at=now()
                where request_id=@requestId and status='Reserved' and version=@expectedVersion;
                """;
            updateRequest.Parameters.AddWithValue("newVersion", requestVersion);
            updateRequest.Parameters.AddWithValue("requestId", current.RequestId);
            updateRequest.Parameters.AddWithValue("expectedVersion", expectedVersion);
            if (await updateRequest.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_video_state_conflict",
                    "The request changed while the connection grant was being issued.");
            }

            await InsertRequestEventAsync(
                connection,
                transaction,
                current.RequestId,
                requestVersion,
                participantRole,
                participantSubjectHash,
                idempotencyKey,
                commandFingerprint,
                cancellationToken);
        }

        var sessionVersion = await AdvanceSessionAsync(connection, transaction, context.SessionId, cancellationToken);
        await InsertVideoEventAsync(
            connection,
            transaction,
            context.SessionId,
            sessionVersion,
            participantRole,
            participantSubjectHash,
            idempotencyKey,
            commandFingerprint,
            cancellationToken);

        if (string.Equals(participantRole, "patient", StringComparison.Ordinal))
        {
            await using var arriveAppointment = connection.CreateCommand();
            arriveAppointment.Transaction = transaction;
            arriveAppointment.CommandText = """
                update appointments set status='@',row_version=row_version+1
                where id=(select appointment_id from telehealth_requests where request_id=@requestId)
                  and coalesce(status,'-')='-';
                """;
            arriveAppointment.Parameters.AddWithValue("requestId", current.RequestId);
            await arriveAppointment.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new TelehealthVideoGrantRecord(
            context.SessionId,
            grantId,
            current.RequestId,
            requestVersion,
            requestStatus,
            participantRole,
            context.GrantExpiresAt);
    }

    private async Task<TelehealthVideoContextRecord> PrepareContextAsync(
        string practiceId,
        int facilityId,
        Guid? requestId,
        string? patientId,
        (Guid ApplicantId, string AccessKeyHash)? applicant,
        (Guid ReservationId, int ClinicianStaffId)? physician,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = applicant is not null
            ? """
                select r.request_id,r.version,r.status,reservation.reservation_id,
                       reservation.lease_expires_at,now()
                from telehealth_prospective_applicants applicant
                join telehealth_applicant_request_creations creation
                  on creation.applicant_id=applicant.applicant_id
                 and creation.practice_id=applicant.practice_id
                 and creation.facility_id=applicant.facility_id
                join telehealth_requests r
                  on r.request_id=creation.request_id
                 and r.source_applicant_id=applicant.applicant_id
                 and r.patient_id=creation.canonical_patient_id
                 and r.practice_id=applicant.practice_id
                 and r.facility_id=applicant.facility_id
                join patients patient
                  on patient.canonical_id=r.patient_id and patient.facility_id=r.facility_id
                join telehealth_queue_entries queue_entry
                  on queue_entry.request_id=r.request_id and queue_entry.status='Reserved'
                join telehealth_reservations reservation
                  on reservation.request_id=r.request_id
                 and reservation.queue_entry_id=queue_entry.queue_entry_id
                 and reservation.status='Active'
                 and reservation.lease_expires_at>now()
                join telehealth_clinician_shifts shift
                  on shift.shift_id=reservation.shift_id
                 and shift.practice_id=r.practice_id
                 and shift.facility_id=r.facility_id
                 and shift.clinician_staff_id=reservation.clinician_staff_id
                 and shift.status='Active'
                join telehealth_applicant_request_queue_authorizations queue_authorization
                  on queue_authorization.request_id=r.request_id
                 and queue_authorization.applicant_id=applicant.applicant_id
                 and queue_authorization.practice_id=r.practice_id
                 and queue_authorization.facility_id=r.facility_id
                 and queue_authorization.canonical_patient_id=r.patient_id
                 and queue_authorization.candidate_staff_id=reservation.clinician_staff_id
                join appointments appointment
                  on appointment.id=r.appointment_id
                 and appointment.patient_id=r.patient_id
                 and appointment.facility_id=r.facility_id
                 and appointment.provider_id=reservation.clinician_staff_id
                where applicant.applicant_id=@applicantId
                  and applicant.practice_id=@practiceId
                  and applicant.facility_id=@facilityId
                  and applicant.access_key_hash=@accessKeyHash
                  and applicant.status='SyntheticRequestCreated'
                  and applicant.version=26
                  and applicant.expires_at>now()
                  and r.request_id=@requestId
                  and r.triage_outcome='TelehealthEligible'
                  and r.status in ('Reserved','Connecting')
                  and not patient.portal_enabled
                  and patient.merged_into_patient_id is null
                  and coalesce(lower(patient.lifecycle_status),'active')='active'
                  and patient.deceased_date is null
                  and queue_authorization.resulting_request_status='Queued'
                  and queue_authorization.resulting_request_version=13
                  and queue_authorization.policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                  and queue_authorization.policy_version=1
                  and queue_authorization.evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                  and queue_authorization.source_mode='NON_PRODUCTION'
                  and queue_authorization.compatibility_target='AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1'
                  and queue_authorization.business_outcome='SyntheticRequestAuthorizedToQueue'
                  and queue_authorization.practice_accepted
                  and queue_authorization.patient_care_queue_entered
                  and queue_authorization.clinician_queue_entered
                  and queue_authorization.doctor_search_started
                  and queue_authorization.appointment_created
                  and not queue_authorization.rendering_physician_assigned
                  and not queue_authorization.coverage_verified
                  and not queue_authorization.financial_route_created
                  and not queue_authorization.queue_position_assigned
                  and not queue_authorization.encounter_created
                  and not queue_authorization.consent_created
                  and not queue_authorization.care_authorized
                  and not queue_authorization.integration_enabled
                  and not queue_authorization.external_call_performed
                  and reservation.reserved_at>=queue_authorization.authorized_at
                  and reservation.reserved_at<queue_authorization.result_valid_through
                  and now()<queue_authorization.result_valid_through
                for update of r,reservation;
                """
            : physician is null
            ? """
                select r.request_id,r.version,r.status,reservation.reservation_id,
                       reservation.lease_expires_at,now()
                from telehealth_requests r
                join telehealth_reservations reservation
                  on reservation.request_id=r.request_id and reservation.status='Active'
                join telehealth_clinician_shifts shift
                  on shift.shift_id=reservation.shift_id and shift.status='Active'
                where r.practice_id=@practiceId and r.facility_id=@facilityId
                  and r.request_id=@requestId and r.patient_id=@patientId
                for update of r,reservation;
                """
            : """
                select r.request_id,r.version,r.status,reservation.reservation_id,
                       reservation.lease_expires_at,now()
                from telehealth_requests r
                join telehealth_reservations reservation
                  on reservation.request_id=r.request_id and reservation.status='Active'
                join telehealth_clinician_shifts shift
                  on shift.shift_id=reservation.shift_id and shift.status='Active'
                where r.practice_id=@practiceId and r.facility_id=@facilityId
                  and reservation.reservation_id=@reservationId
                  and reservation.clinician_staff_id=@clinicianStaffId
                  and shift.clinician_staff_id=@clinicianStaffId
                for update of r,reservation;
                """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (applicant is not null)
        {
            command.Parameters.AddWithValue("requestId", requestId!.Value);
            command.Parameters.AddWithValue("applicantId", applicant.Value.ApplicantId);
            command.Parameters.AddWithValue("accessKeyHash", applicant.Value.AccessKeyHash);
        }
        else if (physician is null)
        {
            command.Parameters.AddWithValue("requestId", requestId!.Value);
            command.Parameters.AddWithValue("patientId", patientId!);
        }
        else
        {
            command.Parameters.AddWithValue("reservationId", physician.Value.ReservationId);
            command.Parameters.AddWithValue("clinicianStaffId", physician.Value.ClinicianStaffId);
        }

        Guid currentRequestId;
        int currentVersion;
        string currentStatus;
        Guid currentReservationId;
        DateTimeOffset leaseExpiresAt;
        DateTimeOffset databaseNow;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw TelehealthProblem.NotFound();
            }
            currentRequestId = reader.GetGuid(0);
            currentVersion = checked((int)reader.GetInt64(1));
            currentStatus = reader.GetString(2);
            currentReservationId = reader.GetGuid(3);
            leaseExpiresAt = reader.GetFieldValue<DateTimeOffset>(4);
            databaseNow = reader.GetFieldValue<DateTimeOffset>(5);
        }

        if (currentStatus is not ("Reserved" or "Connecting"))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_state_invalid",
                "A connection room requires a reserved or connecting request.");
        }
        if (leaseExpiresAt <= databaseNow.AddSeconds(5))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_reservation_expired",
                "The physician reservation is expired or too close to expiry. Refresh the queue.");
        }

        var session = await LoadSessionAsync(
            connection, transaction, currentRequestId, cancellationToken);
        if (session is null)
        {
            var sessionId = Guid.NewGuid();
            var expiresAt = leaseExpiresAt < databaseNow.AddMinutes(30)
                ? leaseExpiresAt
                : databaseNow.AddMinutes(30);
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_video_sessions(
                  session_id,request_id,reservation_id,practice_id,facility_id,
                  adapter_mode,provider_session_reference,status,expires_at)
                values(@sessionId,@requestId,@reservationId,@practiceId,@facilityId,
                       'NON_PRODUCTION',@providerReference,'Prepared',@expiresAt)
                on conflict(request_id) do nothing;
                """;
            insert.Parameters.AddWithValue("sessionId", sessionId);
            insert.Parameters.AddWithValue("requestId", currentRequestId);
            insert.Parameters.AddWithValue("reservationId", currentReservationId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("facilityId", facilityId);
            insert.Parameters.AddWithValue(
                "providerReference", TelehealthCommandFingerprint.Create("opaque-video-session", sessionId));
            insert.Parameters.AddWithValue("expiresAt", expiresAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            session = await LoadSessionAsync(connection, transaction, currentRequestId, cancellationToken);
        }

        if (session is null || session.Value.ReservationId != currentReservationId)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_video_session_conflict",
                "The request connection room is bound to a different reservation.");
        }

        var grantExpiresAt = session.Value.ExpiresAt < databaseNow.AddMinutes(5)
            ? session.Value.ExpiresAt
            : databaseNow.AddMinutes(5);
        await transaction.CommitAsync(cancellationToken);
        return new TelehealthVideoContextRecord(
            session.Value.SessionId,
            currentRequestId,
            currentReservationId,
            currentVersion,
            currentStatus,
            session.Value.ExpiresAt,
            grantExpiresAt);
    }

    private static async Task<ConnectionRow?> LoadContextForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select r.request_id,r.version,r.status,session.expires_at,
                   least(session.expires_at,now() + interval '5 minutes')
            from telehealth_video_sessions session
            join telehealth_requests r on r.request_id=session.request_id
            join telehealth_reservations reservation
              on reservation.reservation_id=session.reservation_id
             and reservation.request_id=session.request_id
             and reservation.status='Active'
             and reservation.lease_expires_at > now()
            join telehealth_clinician_shifts shift
              on shift.shift_id=reservation.shift_id and shift.status='Active'
            where session.session_id=@sessionId and session.reservation_id=@reservationId
              and session.status in ('Prepared','WaitingRoom') and session.expires_at > now()
            for update of session,r,reservation;
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("reservationId", reservationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ConnectionRow(
                reader.GetGuid(0),
                checked((int)reader.GetInt64(1)),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    private static async Task<(Guid SessionId, Guid ReservationId, DateTimeOffset ExpiresAt)?> LoadSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select session_id,reservation_id,expires_at
            from telehealth_video_sessions where request_id=@requestId;
            """;
        command.Parameters.AddWithValue("requestId", requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<(Guid GrantId, string ProviderInstanceId, string CredentialHash, string CommandFingerprint, DateTimeOffset ExpiresAt)?> LoadGrantByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        string participantRole,
        string participantSubjectHash,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select grant_id,provider_instance_id,credential_hash,command_fingerprint,expires_at
            from telehealth_video_participant_grants
            where session_id=@sessionId and participant_role=@role
              and participant_subject_hash=@subjectHash and idempotency_key=@key;
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("role", participantRole);
        command.Parameters.AddWithValue("subjectHash", participantSubjectHash);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    private static async Task ExpirePriorGrantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        string participantRole,
        string participantSubjectHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update telehealth_video_participant_grants
            set status=case when expires_at <= now() then 'Expired' else 'Revoked' end
            where session_id=@sessionId and participant_role=@role
              and participant_subject_hash=@subjectHash and status='Issued';
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("role", participantRole);
        command.Parameters.AddWithValue("subjectHash", participantSubjectHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> AdvanceSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update telehealth_video_sessions
            set status='WaitingRoom',version=version+1
            where session_id=@sessionId
            returning version;
            """;
        command.Parameters.AddWithValue("sessionId", sessionId);
        return checked((int)(long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The video session could not be advanced.")));
    }

    private static async Task InsertRequestEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        int requestVersion,
        string actorType,
        string actorSubjectHash,
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
            values(@eventId,@requestId,@version,'connection-room-entered','Reserved','Connecting',
                   @actorType,@actorId,@key,@fingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("version", requestVersion);
        command.Parameters.AddWithValue("actorType", actorType);
        command.Parameters.AddWithValue("actorId", actorSubjectHash);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertVideoEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        int aggregateVersion,
        string actorType,
        string actorSubjectHash,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into telehealth_video_events(
              event_id,session_id,aggregate_version,action,actor_type,actor_subject_hash,
              idempotency_key,command_fingerprint)
            values(@eventId,@sessionId,@version,'participant-grant-issued',@actorType,@actorHash,
                   @key,@fingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("version", aggregateVersion);
        command.Parameters.AddWithValue("actorType", actorType);
        command.Parameters.AddWithValue("actorHash", actorSubjectHash);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ConnectionRow(
        Guid RequestId,
        int RequestVersion,
        string RequestStatus,
        DateTimeOffset SessionExpiresAt,
        DateTimeOffset GrantExpiresAt);
}
