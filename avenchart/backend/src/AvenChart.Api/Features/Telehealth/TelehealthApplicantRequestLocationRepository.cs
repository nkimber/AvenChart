// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed record TelehealthApplicantRequestLocationRecord(
    Guid ApplicantId,
    int ApplicantVersion,
    string ApplicantStatus,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string ContextSnapshotFingerprint,
    string CurrentLocationStateCode,
    string CallbackPhoneLast4,
    bool LocationConfirmed,
    DateTimeOffset? ConfirmedAt);

internal sealed record TelehealthApplicantRequestLocationApplicant(
    Guid ApplicantId,
    int Version,
    string Status,
    string AccessKeyHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset DatabaseNow);

internal sealed record TelehealthApplicantRequestLocationContext(
    Guid ApplicantId,
    int ApplicantVersion,
    Guid RequestCreationId,
    Guid RequestId,
    int RequestVersion,
    string RequestStatus,
    string CanonicalPatientId,
    Guid CommunicationReadinessId,
    string CurrentLocationStateCode,
    string CallbackPhoneLast4,
    int LocationCount,
    int ReceiptCount,
    int TriageCount,
    int QueueCount);

public sealed class TelehealthApplicantRequestLocationRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthApplicantRequestLocationRecord> GetAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireApplicant(applicant);

        var context = await LoadContextAsync(
            connection, null, practiceId, facilityId, applicantId, false, cancellationToken)
            ?? throw ProvenanceConflict();
        var completed = await LoadConfirmationAsync(
            connection, null, practiceId, facilityId, applicantId, null, cancellationToken);
        if (completed is not null)
        {
            RequireCompletedContext(context);
            return completed.Value.Record;
        }

        RequireReadyContext(context);
        var snapshot = CreateSnapshot(context);
        return new(
            applicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestLocationPolicy.ApplicantStatus,
            context.RequestId,
            context.RequestVersion,
            context.RequestStatus,
            snapshot.Fingerprint,
            snapshot.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            false,
            null);
    }

    public async Task<TelehealthApplicantRequestLocationRecord> ConfirmAsync(
        string practiceId,
        int facilityId,
        Guid applicantId,
        string accessKeyHash,
        NormalizedTelehealthApplicantRequestLocationConfirmation confirmation,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var applicant = await LoadApplicantAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw TelehealthProblem.ApplicantNotFound();
        RequireAccess(applicant, accessKeyHash);
        RequireApplicant(applicant);

        var context = await LoadContextAsync(
            connection, transaction, practiceId, facilityId, applicantId, true, cancellationToken)
            ?? throw ProvenanceConflict();
        var replay = await LoadConfirmationAsync(
            connection, transaction, practiceId, facilityId, applicantId, idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_location_idempotency_conflict",
                    "The idempotency key was already used with different confirmation content.");
            }
            RequireCompletedContext(context);
            await transaction.CommitAsync(cancellationToken);
            return replay.Value.Record;
        }

        if (context.ReceiptCount != 0)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_location_already_completed",
                "The request location was already confirmed. Reload the current state.");
        }
        RequireReadyContext(context);
        if (confirmation.ExpectedRequestVersion != context.RequestVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_location_version_conflict",
                "The request changed. Reload the location and callback step before retrying.");
        }

        var snapshot = CreateSnapshot(context);
        if (!string.Equals(
                confirmation.ContextSnapshotFingerprint,
                snapshot.Fingerprint,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_location_snapshot_conflict",
                "The location or callback context changed. Reload before continuing.");
        }
        if (!string.Equals(
                confirmation.CurrentLocationStateCode,
                context.CurrentLocationStateCode,
                StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_location_changed",
                "The selected location differs from the earlier confirmed state. Stop and restart or request review; this path cannot continue with changed location evidence.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.Draft,
            TelehealthRequestStatus.LocationConfirmed);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update telehealth_requests
                set status='LocationConfirmed',version=2,updated_at=now()
                where request_id=@requestId and practice_id=@practiceId and facility_id=@facilityId
                  and source_applicant_id=@applicantId and status='Draft' and version=1
                  and triage_outcome is null and ready_at is null;
                """;
            update.Parameters.AddWithValue("requestId", context.RequestId);
            update.Parameters.AddWithValue("practiceId", practiceId);
            update.Parameters.AddWithValue("facilityId", facilityId);
            update.Parameters.AddWithValue("applicantId", applicantId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_applicant_request_location_version_conflict",
                    "The request changed. Reload the location and callback step before retrying.");
            }
        }

        var locationId = Guid.NewGuid();
        DateTimeOffset confirmedAt;
        await using (var location = connection.CreateCommand())
        {
            location.Transaction = transaction;
            location.CommandText = """
                insert into telehealth_patient_locations(
                  location_id,request_id,state_code,request_version,idempotency_key,command_fingerprint)
                values(@locationId,@requestId,@stateCode,2,@idempotencyKey,@commandFingerprint)
                returning attested_at;
                """;
            location.Parameters.AddWithValue("locationId", locationId);
            location.Parameters.AddWithValue("requestId", context.RequestId);
            location.Parameters.AddWithValue("stateCode", confirmation.CurrentLocationStateCode);
            location.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            location.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            await using var reader = await location.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Synthetic request location confirmation time was not returned.");
            }
            confirmedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }

        await InsertRequestEventAsync(
            connection, transaction, context.RequestId, applicantId, idempotencyKey,
            commandFingerprint, cancellationToken);

        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText = """
                insert into telehealth_applicant_request_location_confirmations(
                  confirmation_id,location_id,request_id,applicant_id,request_creation_id,
                  communication_readiness_id,practice_id,facility_id,canonical_patient_id,
                  applicant_version,source_request_version,resulting_request_version,
                  resulting_request_status,current_location_state_code,callback_phone_last4,
                  context_snapshot_fingerprint,current_location_confirmed,
                  callback_number_confirmed,changed_location_requires_restart_acknowledged,
                  urgent_or_worsening_action_acknowledged,policy_key,policy_version,
                  evidence_type,idempotency_key,command_fingerprint,confirmed_at)
                values(@confirmationId,@locationId,@requestId,@applicantId,@requestCreationId,
                  @communicationReadinessId,@practiceId,@facilityId,@patientId,@applicantVersion,
                  1,2,'LocationConfirmed',@stateCode,@callbackLast4,@snapshotFingerprint,true,
                  true,true,true,@policyKey,@policyVersion,@evidenceType,@idempotencyKey,
                  @commandFingerprint,@confirmedAt);
                """;
            receipt.Parameters.AddWithValue("confirmationId", Guid.NewGuid());
            receipt.Parameters.AddWithValue("locationId", locationId);
            receipt.Parameters.AddWithValue("requestId", context.RequestId);
            receipt.Parameters.AddWithValue("applicantId", applicantId);
            receipt.Parameters.AddWithValue("requestCreationId", context.RequestCreationId);
            receipt.Parameters.AddWithValue("communicationReadinessId", context.CommunicationReadinessId);
            receipt.Parameters.AddWithValue("practiceId", practiceId);
            receipt.Parameters.AddWithValue("facilityId", facilityId);
            receipt.Parameters.AddWithValue("patientId", context.CanonicalPatientId);
            receipt.Parameters.AddWithValue("applicantVersion", context.ApplicantVersion);
            receipt.Parameters.AddWithValue("stateCode", context.CurrentLocationStateCode);
            receipt.Parameters.AddWithValue("callbackLast4", context.CallbackPhoneLast4);
            receipt.Parameters.AddWithValue("snapshotFingerprint", snapshot.Fingerprint);
            receipt.Parameters.AddWithValue("policyKey", TelehealthApplicantRequestLocationPolicy.PolicyKey);
            receipt.Parameters.AddWithValue("policyVersion", TelehealthApplicantRequestLocationPolicy.PolicyVersion);
            receipt.Parameters.AddWithValue("evidenceType", TelehealthApplicantRequestLocationPolicy.EvidenceType);
            receipt.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            receipt.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
            receipt.Parameters.AddWithValue("confirmedAt", confirmedAt);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(
            applicantId,
            context.ApplicantVersion,
            TelehealthApplicantRequestLocationPolicy.ApplicantStatus,
            context.RequestId,
            TelehealthApplicantRequestLocationPolicy.ResultingRequestVersion,
            TelehealthApplicantRequestLocationPolicy.ResultingRequestStatus,
            snapshot.Fingerprint,
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4,
            true,
            confirmedAt);
    }

    private static TelehealthApplicantRequestLocationSnapshot CreateSnapshot(
        TelehealthApplicantRequestLocationContext context) =>
        TelehealthApplicantRequestLocationPolicy.Snapshot(
            context.RequestId,
            context.RequestCreationId,
            context.CommunicationReadinessId,
            TelehealthApplicantRequestLocationPolicy.EntryRequestVersion,
            context.CurrentLocationStateCode,
            context.CallbackPhoneLast4);

    private static async Task<TelehealthApplicantRequestLocationApplicant?> LoadApplicantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select applicant_id,version,status,access_key_hash,expires_at,now()
            from telehealth_prospective_applicants
            where applicant_id=@applicantId and practice_id=@practiceId and facility_id=@facilityId
            {(forUpdate ? "for update" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
                reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5))
            : null;
    }

    private static async Task<TelehealthApplicantRequestLocationContext?> LoadContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select a.applicant_id,a.version,c.creation_id,r.request_id,r.version,r.status,
                   c.canonical_patient_id,communication.readiness_id,
                   communication.current_location_state_code,communication.callback_phone_last4,
                   (select count(*) from telehealth_patient_locations x where x.request_id=r.request_id),
                   (select count(*) from telehealth_applicant_request_location_confirmations x
                    where x.request_id=r.request_id),
                   (select count(*) from telehealth_triage_assessments x where x.request_id=r.request_id),
                   (select count(*) from telehealth_queue_entries x where x.request_id=r.request_id)
            from telehealth_prospective_applicants a
            join telehealth_applicant_request_creations c
              on c.applicant_id=a.applicant_id and c.practice_id=a.practice_id
             and c.facility_id=a.facility_id and c.resulting_applicant_version=a.version
             and c.resulting_applicant_status=a.status
            join telehealth_requests r
              on r.request_id=c.request_id and r.practice_id=a.practice_id
             and r.facility_id=a.facility_id and r.patient_id=c.canonical_patient_id
             and r.source_applicant_id=a.applicant_id and r.source_promotion_id=c.promotion_id
             and r.source_practice_review_case_id=c.practice_review_case_id
             and r.source_practice_review_authorization_id=c.practice_review_authorization_id
            join telehealth_applicant_communication_access_readiness communication
              on communication.applicant_id=a.applicant_id
             and communication.practice_id=a.practice_id
             and communication.facility_id=a.facility_id
             and communication.canonical_patient_id=c.canonical_patient_id
            join patients patient
              on patient.canonical_id=c.canonical_patient_id and patient.facility_id=a.facility_id
             and not patient.portal_enabled and patient.merged_into_patient_id is null
             and patient.lifecycle_status='active'
             and patient.first_name=a.legal_first_name and patient.last_name=a.legal_last_name
             and patient.date_of_birth=a.date_of_birth and patient.email=a.email
             and coalesce(nullif(patient.phone_cell,''),nullif(patient.phone_home,''),patient.phone)=a.phone
             and patient.state=a.residence_state_code and patient.postal_code=a.postal_code
            where a.applicant_id=@applicantId and a.practice_id=@practiceId
              and a.facility_id=@facilityId and a.status='SyntheticRequestCreated'
              and a.version=26 and a.expires_at>now()
              and c.request_status='Draft' and c.request_version=1
              and c.policy_key='SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION'
              and c.policy_version=1 and c.telehealth_request_created
              and not c.patient_contacted and not c.patient_care_queue_entered
              and not c.clinician_queue_entered and not c.doctor_search_started
              and not c.queue_position_assigned and not c.appointment_created
              and not c.encounter_created and not c.consent_created and not c.care_authorized
              and not c.prescribing_enabled and not c.billing_enabled and not c.claim_created
              and not c.integration_enabled and not c.external_call_performed
              and r.status in ('Draft','LocationConfirmed') and r.version in (1,2)
              and r.triage_outcome is null and r.ready_at is null
              and communication.current_location_state_code in ('GA','CA','FL')
              and communication.callback_phone_last4=right(regexp_replace(a.phone,'[^0-9]','','g'),4)
              and communication.current_location_confirmed and communication.callback_number_confirmed
              and communication.policy_key='SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
              and communication.policy_version=1
              and not exists(select 1 from insurance_records x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from medications x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from prescriptions x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from allergies x
                             where lower(x.patient_id)=lower(patient.canonical_id))
              and not exists(select 1 from problems x
                             where lower(x.patient_id)=lower(patient.canonical_id))
            {(forUpdate ? "for update of a,r,patient" : string.Empty)};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(
                reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetGuid(2),
                reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
                reader.GetString(6), reader.GetGuid(7), reader.GetString(8), reader.GetString(9),
                Convert.ToInt32(reader.GetInt64(10)), Convert.ToInt32(reader.GetInt64(11)),
                Convert.ToInt32(reader.GetInt64(12)), Convert.ToInt32(reader.GetInt64(13)))
            : null;
    }

    private static async Task<(TelehealthApplicantRequestLocationRecord Record,
        string CommandFingerprint)?> LoadConfirmationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string practiceId,
        int facilityId,
        Guid applicantId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select confirmation.applicant_id,confirmation.applicant_version,
                   a.status,confirmation.request_id,r.version,r.status,
                   confirmation.context_snapshot_fingerprint,
                   confirmation.current_location_state_code,
                   confirmation.callback_phone_last4,confirmation.confirmed_at,
                   confirmation.command_fingerprint
            from telehealth_applicant_request_location_confirmations confirmation
            join telehealth_prospective_applicants a on a.applicant_id=confirmation.applicant_id
            join telehealth_requests r on r.request_id=confirmation.request_id
            where confirmation.applicant_id=@applicantId
              and confirmation.practice_id=@practiceId and confirmation.facility_id=@facilityId
              {(idempotencyKey is null ? string.Empty : "and confirmation.idempotency_key=@idempotencyKey")};
            """;
        command.Parameters.AddWithValue("applicantId", applicantId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (idempotencyKey is not null)
        {
            command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return (new(
            reader.GetGuid(0), Convert.ToInt32(reader.GetInt64(1)), reader.GetString(2),
            reader.GetGuid(3), Convert.ToInt32(reader.GetInt64(4)), reader.GetString(5),
            reader.GetString(6), reader.GetString(7), reader.GetString(8), true,
            reader.GetFieldValue<DateTimeOffset>(9)), reader.GetString(10));
    }

    private static async Task InsertRequestEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid requestId,
        Guid applicantId,
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
            values(@eventId,@requestId,2,'location-confirmed','Draft','LocationConfirmed',
                   'applicant',@actorId,@idempotencyKey,@commandFingerprint);
            """;
        command.Parameters.AddWithValue("eventId", Guid.NewGuid());
        command.Parameters.AddWithValue("requestId", requestId);
        command.Parameters.AddWithValue("actorId", applicantId.ToString("D"));
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        command.Parameters.AddWithValue("commandFingerprint", commandFingerprint);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RequireAccess(
        TelehealthApplicantRequestLocationApplicant applicant,
        string accessKeyHash)
    {
        if (!TelehealthProspectiveApplicantPolicy.FixedTimeHashEquals(
                applicant.AccessKeyHash,
                accessKeyHash))
        {
            throw TelehealthProblem.ApplicantNotFound();
        }
    }

    private static void RequireApplicant(TelehealthApplicantRequestLocationApplicant applicant)
    {
        if (applicant.ExpiresAt <= applicant.DatabaseNow)
        {
            throw TelehealthProblem.Gone(
                "telehealth_applicant_expired",
                "This synthetic applicant session expired. Start again.");
        }
        if (applicant.Status != TelehealthApplicantRequestLocationPolicy.ApplicantStatus
            || applicant.Version != 26)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_applicant_request_location_state_conflict",
                "The applicant is not eligible for this request location-confirmation step.");
        }
    }

    private static void RequireReadyContext(TelehealthApplicantRequestLocationContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestLocationPolicy.EntryRequestStatus
            || context.RequestVersion != TelehealthApplicantRequestLocationPolicy.EntryRequestVersion
            || context.LocationCount != 0
            || context.ReceiptCount != 0
            || context.TriageCount != 0
            || context.QueueCount != 0)
        {
            throw ProvenanceConflict();
        }
    }

    private static void RequireCompletedContext(TelehealthApplicantRequestLocationContext context)
    {
        if (context.RequestStatus != TelehealthApplicantRequestLocationPolicy.ResultingRequestStatus
            || context.RequestVersion != TelehealthApplicantRequestLocationPolicy.ResultingRequestVersion
            || context.LocationCount != 1
            || context.ReceiptCount != 1
            || context.TriageCount != 0
            || context.QueueCount != 0)
        {
            throw ProvenanceConflict();
        }
    }

    private static TelehealthProblem ProvenanceConflict() => TelehealthProblem.Conflict(
        "telehealth_applicant_request_location_provenance_conflict",
        "The request location or its authorized source evidence is unavailable or changed.");
}
