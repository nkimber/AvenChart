// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using AvenChart.Api.Data;
using AvenChart.Api.Models;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthConsultationRepository(
    NpgsqlDataSource dataSource,
    EncounterRepository encounterRepository)
{
    public async Task<TelehealthConsultationWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead,
            cancellationToken);

        WorkspaceContext? workspace = null;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select context.consultation_id,context.started_at,now(),context.modality,
                       context.patient_location_state,context.encounter_id,
                       trim(concat(coalesce(nullif(trim(patient.preferred_name),''),patient.first_name),' ',patient.last_name)),
                       patient.date_of_birth,
                       extract(year from age(current_date,patient.date_of_birth))::integer,
                       nullif(trim(patient.sex),''),
                       coalesce(nullif(trim(patient.phone_cell),''),nullif(trim(patient.phone),''),nullif(trim(patient.phone_home),'')),
                       request.complaint_category,intake.complaint_summary,intake.symptom_duration,request.triage_outcome,
                       patient.canonical_id,context.version::integer,context.media_ended_at,request.status
                from telehealth_consultation_contexts context
                join telehealth_requests request on request.request_id=context.request_id
                join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
                join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
                join telehealth_video_sessions session on session.session_id=context.session_id
                join appointments appointment on appointment.id=context.appointment_id
                join encounters encounter on encounter.encounter=context.encounter_id
                join patients patient on patient.canonical_id=request.patient_id
                join lateral (
                  select complaint_summary,symptom_duration
                  from telehealth_intake_snapshots
                  where request_id=request.request_id
                  order by captured_at desc,intake_id desc limit 1
                ) intake on true
                where context.consultation_id=@consultationId
                  and context.practice_id=@practiceId and context.facility_id=@facilityId
                  and context.physician_staff_id=@physician
                  and request.practice_id=@practiceId and request.facility_id=@facilityId
                  and reservation.clinician_staff_id=@physician and reservation.status='Released'
                  and shift.clinician_staff_id=@physician
                  and ((context.status='Started' and request.status='InConsultation' and shift.status='Busy')
                    or (context.status='MediaEnded' and request.status='WrapUp' and shift.status='WrapUp'))
                  and session.status='Ended' and appointment.status='>'
                  and encounter.provider_id=@physician and encounter.facility_id=@facilityId
                  and encounter.source_appointment_id=context.appointment_id
                  and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
                  and patient.lifecycle_status='active'
                  and patient.date_of_birth between current_date - interval '120 years'
                                                    and current_date - interval '18 years';
                """;
            command.Parameters.AddWithValue("consultationId", consultationId);
            command.Parameters.AddWithValue("practiceId", practiceId);
            command.Parameters.AddWithValue("facilityId", facilityId);
            command.Parameters.AddWithValue("physician", physicianStaffId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                workspace = new WorkspaceContext(
                    reader.GetGuid(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetInt32(5),
                    reader.GetString(6),
                    reader.GetFieldValue<DateOnly>(7),
                    reader.GetInt32(8),
                    ReadNullableString(reader, 9),
                    ReadNullableString(reader, 10),
                    reader.GetString(11),
                    reader.GetString(12),
                    reader.GetString(13),
                    reader.GetString(14),
                    reader.GetString(15),
                    reader.GetInt32(16),
                    reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
                    reader.GetString(18));
            }
        }

        if (workspace is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var allergies = await ReadAllergiesAsync(connection, transaction, workspace.PatientId, cancellationToken);
        var medications = await ReadMedicationsAsync(connection, transaction, workspace.PatientId, cancellationToken);
        var problems = await ReadProblemsAsync(connection, transaction, workspace.PatientId, cancellationToken);
        var documentation = await ReadDocumentationDraftAsync(
            connection,
            transaction,
            workspace.EncounterId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TelehealthConsultationWorkspaceResponse(
            workspace.ConsultationId,
            workspace.RequestStatus,
            workspace.ConsultationVersion,
            workspace.MediaEndedAt,
            workspace.Modality,
            workspace.StartedAt,
            workspace.AsOf,
            ReadOnly: true,
            new TelehealthConsultationPatientResponse(
                workspace.DisplayName,
                workspace.DateOfBirth.ToString("yyyy-MM-dd"),
                workspace.Age,
                workspace.RecordedSex,
                workspace.CallbackPhone),
            new TelehealthConsultationVisitResponse(
                workspace.PatientLocationState,
                workspace.ComplaintCategory,
                workspace.ComplaintSummary,
                workspace.SymptomDuration,
                workspace.TriageOutcome),
            allergies,
            medications,
            problems,
            documentation,
            DocumentationEnabled: true,
            PrescribingEnabled: false,
            ClaimsEnabled: false,
            CompletionEnabled: false,
            Limitations:
            [
                "The chart projection is read-only; verify identity, callback, allergies, medications, problems, and visit facts with the patient.",
                "An empty list means no active entry was returned, not a confirmed negative history.",
                "Only an explicit, unsigned SOAP draft save is enabled. Diagnosis, orders, signing, prescribing, claims, and completion are unavailable."
            ]);
    }

    public async Task<TelehealthConsultationDocumentationDraftResponse?> SaveDocumentationDraftAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        TelehealthConsultationDocumentationDraftRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var encounterId = await ResolveOwnedEncounterAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            cancellationToken);
        if (encounterId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await EncounterRepository.AppendSoapNoteVersionAsync(
            connection,
            transaction,
            encounterId.Value,
            new EncounterSoapNoteCreateRequest(
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                request.Subjective,
                request.Objective,
                request.Assessment,
                request.Plan,
                request.ExpectedVersion),
            actor,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var detail = await encounterRepository.GetByEncounterAsync(encounterId.Value, cancellationToken);
        return detail?.SoapNote is { } note ? ToDocumentationDraft(note) : null;
    }

    private static async Task<int?> ResolveOwnedEncounterAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.encounter_id
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            join patients patient on patient.canonical_id=request.patient_id
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician
              and request.practice_id=@practiceId and request.facility_id=@facilityId
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician
              and ((context.status='Started' and request.status='InConsultation' and shift.status='Busy')
                or (context.status='MediaEnded' and request.status='WrapUp' and shift.status='WrapUp'))
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
            for update of context,request,reservation,shift,session,appointment,encounter,patient;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<TelehealthConsultationDocumentationDraftResponse> ReadDocumentationDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int encounterId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select note.version,note.saved_at,note.saved_by,note.subjective,note.objective,note.assessment,note.plan,
                   exists(select 1 from encounter_signatures signature where signature.encounter=@encounter and signature.is_lock)
            from (select @encounter::integer as encounter) target
            left join lateral (
              select version,saved_at,saved_by,subjective,objective,assessment,plan
              from clinical_notes
              where encounter=@encounter
              order by version desc,id desc limit 1
            ) note on true;
            """;
        command.Parameters.AddWithValue("encounter", encounterId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return EmptyDocumentationDraft(isLocked: false);
        }
        if (reader.IsDBNull(0))
        {
            return EmptyDocumentationDraft(reader.GetBoolean(7));
        }

        return new TelehealthConsultationDocumentationDraftResponse(
            reader.GetInt32(0),
            DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            ReadNullableString(reader, 2),
            reader.GetBoolean(7),
            IsSigned: false,
            IsFinal: false,
            ReadNullableString(reader, 3),
            ReadNullableString(reader, 4),
            ReadNullableString(reader, 5),
            ReadNullableString(reader, 6));
    }

    private static TelehealthConsultationDocumentationDraftResponse EmptyDocumentationDraft(bool isLocked) => new(
        Version: 0,
        SavedAt: null,
        SavedBy: null,
        IsLocked: isLocked,
        IsSigned: false,
        IsFinal: false,
        Subjective: null,
        Objective: null,
        Assessment: null,
        Plan: null);

    private static TelehealthConsultationDocumentationDraftResponse ToDocumentationDraft(EncounterSoapNote note) => new(
        note.Version,
        note.SavedAt,
        note.SavedBy,
        note.IsLocked,
        IsSigned: false,
        IsFinal: false,
        note.Subjective,
        note.Objective,
        note.Assessment,
        note.Plan);

    public async Task<TelehealthConsultationWrapUpResponse?> EnterWrapUpAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        EnterTelehealthConsultationWrapUpRequest request,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await LoadWrapUpContextAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            cancellationToken);
        if (context is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var actorHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{practiceId}:{physicianStaffId}")));
        var replay = await FindWrapUpReplayAsync(
            connection,
            transaction,
            consultationId,
            actorHash,
            idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.Action, "consultation-wrap-up-entered", StringComparison.Ordinal)
                || !string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_idempotency_conflict",
                    "The consultation wrap-up idempotency key was reused with different content.");
            }
            if (!string.Equals(context.ConsultationStatus, "MediaEnded", StringComparison.Ordinal)
                || context.MediaEndedAt is null)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_consultation_state_invalid",
                    "The replayed wrap-up command no longer matches the current consultation state.");
            }

            await transaction.CommitAsync(cancellationToken);
            return ToWrapUpResponse(context);
        }

        if (context.ConsultationVersion != request.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_version_conflict",
                "The consultation changed before wrap-up. Reload the current workspace before trying again.");
        }
        if (!string.Equals(context.ConsultationStatus, "Started", StringComparison.Ordinal)
            || !string.Equals(context.RequestStatus, TelehealthRequestStatus.InConsultation.ToString(), StringComparison.Ordinal)
            || !string.Equals(context.ShiftStatus, "Busy", StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_state_invalid",
                "Only a current in-consultation visit can enter wrap-up.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.InConsultation,
            TelehealthRequestStatus.WrapUp);

        var nextConsultationVersion = context.ConsultationVersion + 1;
        var nextRequestVersion = context.RequestVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                with updated_consultation as (
                  update telehealth_consultation_contexts
                  set status='MediaEnded',version=@nextConsultationVersion,media_ended_at=@now
                  where consultation_id=@consultationId and status='Started' and version=@expectedVersion
                  returning 1
                ), updated_request as (
                  update telehealth_requests
                  set status='WrapUp',version=@nextRequestVersion,updated_at=@now
                  where request_id=@requestId and status='InConsultation' and version=@requestVersion
                  returning 1
                ), updated_shift as (
                  update telehealth_clinician_shifts
                  set status='WrapUp',version=version+1
                  where shift_id=@shiftId and status='Busy' and clinician_staff_id=@physician
                  returning 1
                )
                select (select count(*) from updated_consultation),
                       (select count(*) from updated_request),
                       (select count(*) from updated_shift);
                """;
            update.Parameters.AddWithValue("consultationId", consultationId);
            update.Parameters.AddWithValue("requestId", context.RequestId);
            update.Parameters.AddWithValue("shiftId", context.ShiftId);
            update.Parameters.AddWithValue("physician", physicianStaffId);
            update.Parameters.AddWithValue("expectedVersion", request.ExpectedVersion);
            update.Parameters.AddWithValue("nextConsultationVersion", nextConsultationVersion);
            update.Parameters.AddWithValue("requestVersion", context.RequestVersion);
            update.Parameters.AddWithValue("nextRequestVersion", nextRequestVersion);
            update.Parameters.AddWithValue("now", context.DatabaseNow);
            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || reader.GetInt64(0) != 1
                || reader.GetInt64(1) != 1
                || reader.GetInt64(2) != 1)
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_consultation_state_conflict",
                    "The consultation changed while entering wrap-up. Reload the current workspace.");
            }
        }

        await using (var events = connection.CreateCommand())
        {
            events.Transaction = transaction;
            events.CommandText = """
                insert into telehealth_consultation_events(
                  event_id,consultation_id,request_id,aggregate_version,action,actor_type,
                  actor_subject_hash,idempotency_key,command_fingerprint,occurred_at)
                values(@consultationEventId,@consultationId,@requestId,@consultationVersion,
                       'consultation-wrap-up-entered','physician',@actorHash,@key,@fingerprint,@now);
                insert into telehealth_request_events(
                  event_id,request_id,aggregate_version,action,from_status,to_status,
                  actor_type,actor_id,idempotency_key,command_fingerprint,occurred_at)
                values(@requestEventId,@requestId,@requestVersion,'consultation-wrap-up-entered',
                       'InConsultation','WrapUp','physician',@actorId,@key,@fingerprint,@now);
                """;
            events.Parameters.AddWithValue("consultationEventId", Guid.NewGuid());
            events.Parameters.AddWithValue("requestEventId", Guid.NewGuid());
            events.Parameters.AddWithValue("consultationId", consultationId);
            events.Parameters.AddWithValue("requestId", context.RequestId);
            events.Parameters.AddWithValue("consultationVersion", nextConsultationVersion);
            events.Parameters.AddWithValue("requestVersion", nextRequestVersion);
            events.Parameters.AddWithValue("actorHash", actorHash);
            events.Parameters.AddWithValue("actorId", physicianStaffId.ToString(CultureInfo.InvariantCulture));
            events.Parameters.AddWithValue("key", idempotencyKey);
            events.Parameters.AddWithValue("fingerprint", commandFingerprint);
            events.Parameters.AddWithValue("now", context.DatabaseNow);
            await events.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToWrapUpResponse(context with
        {
            ConsultationStatus = "MediaEnded",
            ConsultationVersion = nextConsultationVersion,
            MediaEndedAt = context.DatabaseNow,
            RequestStatus = TelehealthRequestStatus.WrapUp.ToString(),
            RequestVersion = nextRequestVersion,
            ShiftStatus = "WrapUp"
        });
    }

    private static async Task<WrapUpContext?> LoadWrapUpContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.consultation_id,context.request_id,context.shift_id,context.status,
                   context.version::integer,context.media_ended_at,request.status,request.version,
                   shift.status,session.status,appointment.status,now()
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join telehealth_reservations reservation on reservation.reservation_id=context.reservation_id
            join telehealth_clinician_shifts shift on shift.shift_id=context.shift_id
            join telehealth_video_sessions session on session.session_id=context.session_id
            join appointments appointment on appointment.id=context.appointment_id
            join encounters encounter on encounter.encounter=context.encounter_id
            join patients patient on patient.canonical_id=request.patient_id
            where context.consultation_id=@consultationId
              and context.practice_id=@practiceId and context.facility_id=@facilityId
              and context.physician_staff_id=@physician
              and request.practice_id=@practiceId and request.facility_id=@facilityId
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
            for update of context,request,reservation,shift,session,appointment,encounter,patient;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new WrapUpContext(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetString(6),
            reader.GetInt32(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }

    private static async Task<(string Action, string CommandFingerprint)?> FindWrapUpReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        string actorHash,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select action,command_fingerprint
            from telehealth_consultation_events
            where consultation_id=@consultationId and actor_type='physician'
              and actor_subject_hash=@actorHash and idempotency_key=@key;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("actorHash", actorHash);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static TelehealthConsultationWrapUpResponse ToWrapUpResponse(WrapUpContext context) => new(
        context.ConsultationId,
        context.ConsultationVersion,
        context.ConsultationStatus,
        context.MediaEndedAt ?? context.DatabaseNow,
        context.RequestVersion,
        context.RequestStatus,
        context.ShiftStatus,
        context.AppointmentStatus,
        DocumentationEnabled: true,
        CompletionEnabled: false,
        ClinicianAvailableForNewWork: false,
        Limitations:
        [
            "Wrap-up is unfinished synthetic lifecycle state, not a final disposition or completed encounter.",
            "The owning physician remains responsible and unavailable for new work.",
            "Signing, finalization, patient delivery, prescribing, billing, and claims are unavailable."
        ]);

    private static async Task<IReadOnlyList<TelehealthConsultationAllergyResponse>> ReadAllergiesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(nullif(trim(title),''),nullif(trim(type),''),'Recorded allergy'),
                   nullif(trim(reaction),''),nullif(trim(severity),'')
            from allergies
            where patient_id=@patientId and activity=1
              and (end_date is null or end_date>=current_date)
            order by allergy_date desc nulls last,id
            limit 20;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        var items = new List<TelehealthConsultationAllergyResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TelehealthConsultationAllergyResponse(
                reader.GetString(0), ReadNullableString(reader, 1), ReadNullableString(reader, 2)));
        }
        return items;
    }

    private static async Task<IReadOnlyList<TelehealthConsultationMedicationResponse>> ReadMedicationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(nullif(trim(title),''),'Recorded medication')
            from medications
            where patient_id=@patientId and activity=1
              and (end_date is null or end_date>=current_date)
            order by medication_date desc nulls last,id
            limit 20;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        var items = new List<TelehealthConsultationMedicationResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TelehealthConsultationMedicationResponse(reader.GetString(0)));
        }
        return items;
    }

    private static async Task<IReadOnlyList<TelehealthConsultationProblemResponse>> ReadProblemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(nullif(trim(title),''),nullif(trim(diagnosis),''),'Recorded problem'),
                   nullif(trim(diagnosis),'')
            from problems
            where patient_id=@patientId and activity=1
              and (end_date is null or end_date>=current_date)
            order by problem_date desc nulls last,id
            limit 20;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        var items = new List<TelehealthConsultationProblemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TelehealthConsultationProblemResponse(
                reader.GetString(0), ReadNullableString(reader, 1)));
        }
        return items;
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public async Task<TelehealthConsultationStartResponse> StartAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid reservationId,
        StartTelehealthConsultationRequest request,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var replay = await FindReplayAsync(
            connection, transaction, physicianStaffId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.Value.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw TelehealthProblem.Conflict(
                    "telehealth_idempotency_conflict",
                    "The consultation-start idempotency key was reused with different content.");
            }

            await transaction.CommitAsync(cancellationToken);
            return ToResponse(replay.Value.Context);
        }

        var context = await LoadStartContextAsync(
            connection, transaction, practiceId, facilityId, physicianStaffId, reservationId, cancellationToken)
            ?? throw TelehealthProblem.NotFound();

        if (context.RequestVersion != request.ExpectedVersion)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_version_conflict",
                "The request changed before consultation start. Refresh and repeat the start checks.");
        }
        if (!string.Equals(context.RequestStatus, TelehealthRequestStatus.Connecting.ToString(), StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_state_invalid",
                "Consultation start requires a connecting request.");
        }
        if (!string.Equals(context.PatientLocationState, request.PatientLocationState, StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_location_reconfirmation_stale",
                "The reconfirmed patient state does not match the current request location evidence.");
        }
        if (context.ReservationExpiresAt <= context.DatabaseNow
            || context.SessionExpiresAt <= context.DatabaseNow
            || !context.PatientGrantCurrent
            || !context.PhysicianGrantCurrent)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_presence_stale",
                "The active reservation and both participant grants must remain current at consultation start.");
        }
        if (!context.FinancialEvidenceCurrent)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_financial_gate_stale",
                "The synthetic eligibility and exact rendering-candidate network evidence expired before consultation start.");
        }
        if (!string.Equals(context.AppointmentStatus, "@", StringComparison.Ordinal))
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_patient_not_arrived",
                "The patient must enter the isolated waiting room before consultation start.");
        }
        if (context.ExistingConsultationId is not null)
        {
            throw TelehealthProblem.Conflict(
                "telehealth_consultation_already_started",
                "This request already has a consultation context.");
        }

        TelehealthRequestStateMachine.RequireTransition(
            TelehealthRequestStatus.Connecting,
            TelehealthRequestStatus.InConsultation);

        var encounter = await encounterRepository.CreateInTransactionAsync(
            connection,
            transaction,
            new EncounterCreateRequest(
                PatientId: context.PatientId,
                ProviderId: physicianStaffId,
                DateTime: context.DatabaseNow.UtcDateTime.ToString("O"),
                Reason: $"Immediate telehealth - {context.ComplaintCategory}",
                FacilityId: facilityId,
                BillingFacilityId: facilityId,
                Sensitivity: null,
                ReferralSource: "Immediate telehealth synthetic",
                ExternalId: context.RequestId.ToString("D"),
                PosCode: null,
                BillingNote: null,
                SourceAppointmentId: context.AppointmentId),
            cancellationToken)
            ?? throw TelehealthProblem.Conflict(
                "telehealth_encounter_link_failed",
                "The existing AvenChart encounter could not be linked transactionally.");

        var consultationId = Guid.NewGuid();
        var requestVersion = context.RequestVersion + 1;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into telehealth_consultation_contexts(
                  consultation_id,request_id,reservation_id,shift_id,session_id,appointment_id,
                  encounter_id,practice_id,facility_id,physician_staff_id,patient_location_state,
                  modality,status,patient_identity_discussed,callback_confirmed,privacy_confirmed,
                  consent_discussed,no_concerning_symptom_change,emergency_plan_confirmed,
                  communication_sufficient,synthetic_data_confirmed,legal_effect,
                  idempotency_key,command_fingerprint)
                values(@consultationId,@requestId,@reservationId,@shiftId,@sessionId,@appointmentId,
                       @encounterId,@practiceId,@facilityId,@physician,@state,
                       'SYNTHETIC_VIDEO','Started',@identity,@callback,@privacy,
                       @consent,@symptoms,@emergency,@communication,@synthetic,false,@key,@fingerprint);

                update telehealth_requests
                set status='InConsultation',version=@requestVersion,updated_at=now()
                where request_id=@requestId and status='Connecting' and version=@expectedVersion;
                update appointments set status='>',row_version=row_version+1
                where id=@appointmentId and coalesce(status,'-')='@';
                update telehealth_queue_entries set status='Removed',version=version+1,updated_at=now()
                where request_id=@requestId and status='Reserved';
                update telehealth_reservations set status='Released',version=version+1
                where reservation_id=@reservationId and status='Active';
                update telehealth_clinician_shifts set status='Busy',version=version+1
                where shift_id=@shiftId and status='Active';
                update telehealth_video_participant_grants set status='Revoked'
                where session_id=@sessionId and status='Issued';
                update telehealth_video_sessions set status='Ended',version=version+1
                where session_id=@sessionId and status='WaitingRoom';
                """;
            command.Parameters.AddWithValue("consultationId", consultationId);
            command.Parameters.AddWithValue("requestId", context.RequestId);
            command.Parameters.AddWithValue("reservationId", reservationId);
            command.Parameters.AddWithValue("shiftId", context.ShiftId);
            command.Parameters.AddWithValue("sessionId", context.SessionId);
            command.Parameters.AddWithValue("appointmentId", context.AppointmentId);
            command.Parameters.AddWithValue("encounterId", encounter);
            command.Parameters.AddWithValue("practiceId", practiceId);
            command.Parameters.AddWithValue("facilityId", facilityId);
            command.Parameters.AddWithValue("physician", physicianStaffId);
            command.Parameters.AddWithValue("state", request.PatientLocationState);
            command.Parameters.AddWithValue("identity", request.PatientIdentityDiscussed);
            command.Parameters.AddWithValue("callback", request.CallbackConfirmed);
            command.Parameters.AddWithValue("privacy", request.PrivacyConfirmed);
            command.Parameters.AddWithValue("consent", request.ConsentDiscussed);
            command.Parameters.AddWithValue("symptoms", request.NoConcerningSymptomChange);
            command.Parameters.AddWithValue("emergency", request.EmergencyPlanConfirmed);
            command.Parameters.AddWithValue("communication", request.CommunicationSufficient);
            command.Parameters.AddWithValue("synthetic", request.SyntheticDataConfirmed);
            command.Parameters.AddWithValue("key", idempotencyKey);
            command.Parameters.AddWithValue("fingerprint", commandFingerprint);
            command.Parameters.AddWithValue("requestVersion", requestVersion);
            command.Parameters.AddWithValue("expectedVersion", context.RequestVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var actorHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{practiceId}:{physicianStaffId}")));
        await using (var events = connection.CreateCommand())
        {
            events.Transaction = transaction;
            events.CommandText = """
                insert into telehealth_consultation_events(
                  event_id,consultation_id,request_id,aggregate_version,action,actor_type,
                  actor_subject_hash,idempotency_key,command_fingerprint)
                values(@eventId,@consultationId,@requestId,1,'consultation-started','physician',
                       @actorHash,@key,@fingerprint);
                insert into telehealth_request_events(
                  event_id,request_id,aggregate_version,action,from_status,to_status,
                  actor_type,actor_id,idempotency_key,command_fingerprint)
                values(@requestEventId,@requestId,@requestVersion,'consultation-started','Connecting','InConsultation',
                       'physician',@actorId,@key,@fingerprint);
                """;
            events.Parameters.AddWithValue("eventId", Guid.NewGuid());
            events.Parameters.AddWithValue("requestEventId", Guid.NewGuid());
            events.Parameters.AddWithValue("consultationId", consultationId);
            events.Parameters.AddWithValue("requestId", context.RequestId);
            events.Parameters.AddWithValue("requestVersion", requestVersion);
            events.Parameters.AddWithValue("actorHash", actorHash);
            events.Parameters.AddWithValue("actorId", physicianStaffId.ToString());
            events.Parameters.AddWithValue("key", idempotencyKey);
            events.Parameters.AddWithValue("fingerprint", commandFingerprint);
            await events.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToResponse(new ConsultationRecord(
            consultationId,
            context.RequestId,
            requestVersion,
            TelehealthRequestStatus.InConsultation.ToString(),
            ">",
            "SYNTHETIC_VIDEO",
            context.DatabaseNow,
            false,
            context.ApplicantOriginated,
            commandFingerprint));
    }

    private static async Task<(ConsultationRecord Context, string CommandFingerprint)?> FindReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int physicianStaffId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.consultation_id,context.request_id,request.version,request.status,
                   coalesce(appointment.status,'-'),context.modality,context.started_at,
                   context.legal_effect,request.source_applicant_id is not null,context.command_fingerprint
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            join appointments appointment on appointment.id=context.appointment_id
            where context.physician_staff_id=@physician and context.idempotency_key=@key
            for update of context;
            """;
        command.Parameters.AddWithValue("physician", physicianStaffId);
        command.Parameters.AddWithValue("key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var record = new ConsultationRecord(
            reader.GetGuid(0), reader.GetGuid(1), checked((int)reader.GetInt64(2)), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetBoolean(7), reader.GetBoolean(8), reader.GetString(9));
        return (record, record.CommandFingerprint);
    }

    private static async Task<StartContext?> LoadStartContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select request.request_id,request.patient_id,request.complaint_category,request.version,request.status,
                   request.appointment_id,coalesce(appointment.status,'-'),reservation.shift_id,
                   reservation.lease_expires_at,session.session_id,session.expires_at,now(),
                   location.state_code,
                   exists(select 1 from telehealth_video_participant_grants grant_row
                          where grant_row.session_id=session.session_id and grant_row.participant_role='patient'
                            and grant_row.status='Issued' and grant_row.expires_at>now()),
                   exists(select 1 from telehealth_video_participant_grants grant_row
                          where grant_row.session_id=session.session_id and grant_row.participant_role='physician'
                            and grant_row.status='Issued' and grant_row.expires_at>now()),
                   ((request.source_applicant_id is null
                     and exists(select 1 from telehealth_coverage_verifications verification
                                where verification.request_id=request.request_id
                                  and verification.eligibility_status='Active'
                                  and verification.network_status='ConfirmedInNetwork'
                                  and verification.expires_at>now()))
                    or (request.source_applicant_id is not null and exists(
                      select 1
                      from telehealth_applicant_request_queue_authorizations queue_authorization
                      join telehealth_prospective_applicants applicant
                        on applicant.applicant_id=queue_authorization.applicant_id
                       and applicant.practice_id=queue_authorization.practice_id
                       and applicant.facility_id=queue_authorization.facility_id
                      where queue_authorization.request_id=request.request_id
                        and queue_authorization.applicant_id=request.source_applicant_id
                        and queue_authorization.practice_id=request.practice_id
                        and queue_authorization.facility_id=request.facility_id
                        and queue_authorization.canonical_patient_id=request.patient_id
                        and queue_authorization.candidate_staff_id=@physician
                        and queue_authorization.resulting_request_status='Queued'
                        and queue_authorization.resulting_request_version=13
                        and queue_authorization.result_valid_through>now()
                        and queue_authorization.source_mode='NON_PRODUCTION'
                        and queue_authorization.compatibility_target='AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1'
                        and queue_authorization.business_outcome='SyntheticRequestAuthorizedToQueue'
                        and queue_authorization.policy_key='SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.policy_version=1
                        and queue_authorization.evidence_type='APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
                        and queue_authorization.synthetic_evidence_reviewed
                        and queue_authorization.no_coverage_guarantee_acknowledged
                        and queue_authorization.practice_accepts_for_queue_acknowledged
                        and queue_authorization.queue_not_care_acknowledged
                        and queue_authorization.practice_accepted
                        and queue_authorization.patient_care_queue_entered
                        and queue_authorization.clinician_queue_entered
                        and queue_authorization.doctor_search_started
                        and queue_authorization.appointment_created
                        and not queue_authorization.real_state_authority_verified
                        and not queue_authorization.real_credentialing_verified
                        and not queue_authorization.rendering_physician_network_checked
                        and not queue_authorization.exact_network_confirmed
                        and not queue_authorization.canonical_coverage_created
                        and not queue_authorization.coverage_verified
                        and not queue_authorization.financial_route_created
                        and not queue_authorization.consent_created
                        and not queue_authorization.care_authorized
                        and not queue_authorization.prescribing_enabled
                        and not queue_authorization.billing_enabled
                        and not queue_authorization.claim_created
                        and not queue_authorization.integration_enabled
                        and not queue_authorization.external_call_performed
                        and applicant.status='SyntheticRequestCreated'
                        and applicant.version=26
                        and applicant.expires_at>now()
                    ))),
                   request.source_applicant_id is not null,
                   existing.consultation_id
            from telehealth_reservations reservation
            join telehealth_requests request on request.request_id=reservation.request_id
            join telehealth_clinician_shifts shift on shift.shift_id=reservation.shift_id
            join telehealth_video_sessions session on session.reservation_id=reservation.reservation_id
            join appointments appointment
              on appointment.id=request.appointment_id
             and appointment.patient_id=request.patient_id
             and appointment.facility_id=request.facility_id
             and appointment.provider_id=@physician
            join patients patient
              on patient.canonical_id=request.patient_id and patient.facility_id=request.facility_id
            join lateral (
              select state_code,attested_at from telehealth_patient_locations
              where request_id=request.request_id order by attested_at desc,location_id desc limit 1
            ) location on location.attested_at>now()-interval '4 hours'
            left join telehealth_consultation_contexts existing on existing.request_id=request.request_id
            where reservation.reservation_id=@reservationId and reservation.clinician_staff_id=@physician
              and reservation.status='Active' and shift.status='Active'
              and shift.clinician_staff_id=@physician
              and request.practice_id=@practiceId and request.facility_id=@facilityId
              and session.status='WaitingRoom'
              and patient.merged_into_patient_id is null
              and coalesce(lower(patient.lifecycle_status),'active')='active'
              and patient.deceased_date is null
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
            for update of request,reservation,shift,session,appointment;
            """;
        command.Parameters.AddWithValue("reservationId", reservationId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StartContext(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), checked((int)reader.GetInt64(3)),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetGuid(7),
                reader.GetFieldValue<DateTimeOffset>(8), reader.GetGuid(9), reader.GetFieldValue<DateTimeOffset>(10),
                reader.GetFieldValue<DateTimeOffset>(11), reader.GetString(12), reader.GetBoolean(13),
                reader.GetBoolean(14), reader.GetBoolean(15), reader.GetBoolean(16),
                reader.IsDBNull(17) ? null : reader.GetGuid(17))
            : null;
    }

    private static TelehealthConsultationStartResponse ToResponse(ConsultationRecord record) => new(
        record.ConsultationId,
        record.RequestId,
        record.RequestVersion,
        record.RequestStatus,
        record.AppointmentStatus,
        record.Modality,
        record.StartedAt,
        record.LegalEffect,
        ChartAccessEnabled: true,
        DocumentationEnabled: true,
        PrescribingEnabled: false,
        ClaimsEnabled: false,
        Limitations: StartLimitations(record.ApplicantOriginated));

    private static IReadOnlyList<string> StartLimitations(bool applicantOriginated) => applicantOriginated
        ?
        [
            "Synthetic lifecycle evidence only; no real consultation or media occurred.",
            "The new-patient financial gate uses current synthetic eligibility and exact rendering-candidate evidence only; it is not real coverage verification or a payment guarantee.",
            "This confirmation has no legal consent or identity-proofing effect.",
            "Only the separately audited, bounded chart projection and explicit unsigned SOAP draft are available; general chart access is not enabled.",
            "Diagnosis, orders, signing, prescribing, claims, and completion are unavailable in this slice."
        ]
        :
        [
            "Synthetic lifecycle evidence only; no real consultation or media occurred.",
            "This confirmation has no legal consent or identity-proofing effect.",
            "Only the separately audited, bounded chart projection and explicit unsigned SOAP draft are available; general chart access is not enabled.",
            "Diagnosis, orders, signing, prescribing, claims, and completion are unavailable in this slice."
        ];

    private sealed record StartContext(
        Guid RequestId,
        string PatientId,
        string ComplaintCategory,
        int RequestVersion,
        string RequestStatus,
        string AppointmentId,
        string AppointmentStatus,
        Guid ShiftId,
        DateTimeOffset ReservationExpiresAt,
        Guid SessionId,
        DateTimeOffset SessionExpiresAt,
        DateTimeOffset DatabaseNow,
        string PatientLocationState,
        bool PatientGrantCurrent,
        bool PhysicianGrantCurrent,
        bool FinancialEvidenceCurrent,
        bool ApplicantOriginated,
        Guid? ExistingConsultationId);

    private sealed record ConsultationRecord(
        Guid ConsultationId,
        Guid RequestId,
        int RequestVersion,
        string RequestStatus,
        string AppointmentStatus,
        string Modality,
        DateTimeOffset StartedAt,
        bool LegalEffect,
        bool ApplicantOriginated,
        string CommandFingerprint);

    private sealed record WorkspaceContext(
        Guid ConsultationId,
        DateTimeOffset StartedAt,
        DateTimeOffset AsOf,
        string Modality,
        string PatientLocationState,
        int EncounterId,
        string DisplayName,
        DateOnly DateOfBirth,
        int Age,
        string? RecordedSex,
        string? CallbackPhone,
        string ComplaintCategory,
        string ComplaintSummary,
        string SymptomDuration,
        string TriageOutcome,
        string PatientId,
        int ConsultationVersion,
        DateTimeOffset? MediaEndedAt,
        string RequestStatus);

    private sealed record WrapUpContext(
        Guid ConsultationId,
        Guid RequestId,
        Guid ShiftId,
        string ConsultationStatus,
        int ConsultationVersion,
        DateTimeOffset? MediaEndedAt,
        string RequestStatus,
        int RequestVersion,
        string ShiftStatus,
        string SessionStatus,
        string AppointmentStatus,
        DateTimeOffset DatabaseNow);
}
