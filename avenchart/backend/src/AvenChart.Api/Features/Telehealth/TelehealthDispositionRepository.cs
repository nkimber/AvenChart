// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Globalization;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthSafetyDispositionConflictException(string message) : Exception(message);

public static class TelehealthSafetyDispositionRules
{
    public static readonly IReadOnlyList<TelehealthSafetyDispositionOptionResponse> Options =
    [
        new("TreatedTelehealth", "Treated by telehealth", true, false, false, false),
        new("NoTreatmentNeeded", "No treatment needed", true, false, false, false),
        new("TestingOrReferralRequired", "Testing or referral required", true, false, false, false),
        new("UrgentInPerson", "Urgent in-person evaluation", true, true, false, false),
        new("EmergencyTransferRecommended", "Emergency transfer recommended", true, true, true, false),
        new("TechnicalAbort", "Technical abort", false, false, false, true),
        new("PatientLeft", "Patient left", false, false, false, true),
        new("ClinicianUnableToComplete", "Clinician unable to complete", false, false, false, true)
    ];

    public static readonly IReadOnlyList<string> FollowUpOwners =
        ["Patient", "Practice", "TreatingPhysician", "EmergencyServices", "ExternalClinician", "NoneClinicallyRequired"];
    public static readonly IReadOnlyList<string> CommunicationMethods =
        ["DiscussedDuringSyntheticConsultation", "SyntheticCallback", "NotYetCommunicated"];
    public static readonly IReadOnlyList<string> EmergencyHandoffStatuses =
        ["RecommendedOnly", "PatientCalling", "PracticeCalling", "Connected", "UnableToConfirm"];

    public static RecordTelehealthSafetyDispositionDraftRequest Normalize(
        RecordTelehealthSafetyDispositionDraftRequest request)
    {
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentException("ExpectedVersion cannot be negative.");
        }
        if (!request.SyntheticDataConfirmed)
        {
            throw new ArgumentException("Confirm that the disposition draft contains synthetic-only demonstration data.");
        }

        var disposition = Required(request.DispositionCode, 64, "DispositionCode");
        var option = Options.SingleOrDefault(item => string.Equals(item.Code, disposition, StringComparison.Ordinal))
            ?? throw new ArgumentException("Select one supported safety disposition.");
        var owner = Required(request.FollowUpOwner, 64, "FollowUpOwner");
        if (!FollowUpOwners.Contains(owner, StringComparer.Ordinal))
        {
            throw new ArgumentException("Select one supported follow-up owner.");
        }
        var method = Required(request.CommunicationMethod, 64, "CommunicationMethod");
        if (!CommunicationMethods.Contains(method, StringComparer.Ordinal))
        {
            throw new ArgumentException("Select one supported communication method.");
        }

        var timeframe = Required(request.FollowUpTimeframe, 160, "FollowUpTimeframe");
        var nextSteps = Required(request.NextStepInstructions, 2000, "NextStepInstructions");
        var warnings = Required(request.WarningEscalationInstructions, 2000, "WarningEscalationInstructions");
        var handoff = Optional(request.EmergencyHandoffStatus, 64, "EmergencyHandoffStatus");
        var contact = Optional(request.ContactAttemptSummary, 2000, "ContactAttemptSummary");

        if (option.RequiresAdequateEvaluation && !request.AdequateEvaluationCompleted)
        {
            throw new ArgumentException("This disposition requires confirmation that the available evaluation was adequate.");
        }
        if (option.RequiresLocationCallbackReconfirmation && !request.LocationCallbackReconfirmed)
        {
            throw new ArgumentException("Reconfirm the current location and callback number for this disposition.");
        }
        if (option.RequiresEmergencyFacts)
        {
            if (!request.EmergencyInstructionProvided)
            {
                throw new ArgumentException("Confirm that emergency instructions were provided for this draft.");
            }
            if (handoff is null || !EmergencyHandoffStatuses.Contains(handoff, StringComparer.Ordinal))
            {
                throw new ArgumentException("Select a factual, non-claiming emergency handoff state.");
            }
        }
        else if (request.EmergencyInstructionProvided || handoff is not null)
        {
            throw new ArgumentException("Emergency instruction and handoff facts are available only for the emergency disposition.");
        }
        if (option.RequiresContactAttemptSummary && contact is null)
        {
            throw new ArgumentException("Interrupted consultations require a contact and safety-attempt summary.");
        }
        if (!option.RequiresContactAttemptSummary && contact is not null)
        {
            throw new ArgumentException("Contact-attempt summary is available only for interrupted dispositions.");
        }
        if (request.CommunicationCompleted == string.Equals(method, "NotYetCommunicated", StringComparison.Ordinal))
        {
            throw new ArgumentException("Communication method and completion state do not agree.");
        }

        return request with
        {
            DispositionCode = disposition,
            FollowUpOwner = owner,
            FollowUpTimeframe = timeframe,
            NextStepInstructions = nextSteps,
            WarningEscalationInstructions = warnings,
            CommunicationMethod = method,
            EmergencyHandoffStatus = handoff,
            ContactAttemptSummary = contact
        };
    }

    private static string Required(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} must contain between 1 and {maxLength} characters.");
        }
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string field)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.");
        }
        return normalized;
    }
}

public sealed class TelehealthDispositionRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthSafetyDispositionWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var owner = await ReadOwnedWrapUpAsync(
            connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, false, cancellationToken);
        if (owner is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var current = await ReadCurrentAsync(connection, transaction, consultationId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TelehealthSafetyDispositionWorkspaceResponse(
            consultationId,
            owner.ConsultationStatus,
            owner.DatabaseNow,
            TelehealthSafetyDispositionRules.Options,
            TelehealthSafetyDispositionRules.FollowUpOwners,
            TelehealthSafetyDispositionRules.CommunicationMethods,
            TelehealthSafetyDispositionRules.EmergencyHandoffStatuses,
            current,
            SigningEnabled: false,
            PatientDeliveryEnabled: false,
            CompletionEnabled: false,
            Limitations:
            [
                "This is an unsigned physician-authored synthetic safety draft. The application supplies no clinical instruction or recommendation.",
                "No patient delivery, AVS, signature, finalization, order, referral, prescription, claim, completion, or external handoff is created.",
                "An entered emergency handoff state is not external verification that a transfer or connection occurred."
            ]);
    }

    public async Task<TelehealthSafetyDispositionDraftResponse?> RecordAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        RecordTelehealthSafetyDispositionDraftRequest request,
        string idempotencyKey,
        string commandFingerprint,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var owner = await ReadOwnedWrapUpAsync(
            connection, transaction, practiceId, facilityId, physicianStaffId, consultationId, true, cancellationToken);
        if (owner is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var replay = await ReadReplayAsync(connection, transaction, consultationId, idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw new TelehealthSafetyDispositionConflictException(
                    "The idempotency key was already used for different safety-disposition content.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Draft;
        }

        var current = await ReadCurrentAsync(connection, transaction, consultationId, cancellationToken);
        var currentVersion = current?.Version ?? 0;
        if (request.ExpectedVersion != currentVersion)
        {
            throw new TelehealthSafetyDispositionConflictException(
                $"The current safety-disposition draft is version {currentVersion}. Reload before recording another version.");
        }

        var nextVersion = checked(currentVersion + 1);
        var versionId = Guid.NewGuid();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_consultation_disposition_draft_versions(
                  disposition_version_id,consultation_id,encounter_id,version,disposition_code,
                  adequate_evaluation_completed,follow_up_owner,follow_up_timeframe,next_step_instructions,
                  warning_escalation_instructions,communication_method,communication_completed,
                  location_callback_reconfirmed,emergency_instruction_provided,emergency_handoff_status,
                  contact_attempt_summary,legal_effect,recorded_at,recorded_by_staff_id)
                values(@versionId,@consultationId,@encounterId,@version,@disposition,@adequate,@owner,@timeframe,
                  @nextSteps,@warnings,@communicationMethod,@communicationCompleted,@locationReconfirmed,
                  @emergencyProvided,@handoff,@contact,false,@recordedAt,@physician);
                """;
            insert.Parameters.AddWithValue("versionId", versionId);
            insert.Parameters.AddWithValue("consultationId", consultationId);
            insert.Parameters.AddWithValue("encounterId", owner.EncounterId);
            insert.Parameters.AddWithValue("version", nextVersion);
            insert.Parameters.AddWithValue("disposition", request.DispositionCode);
            insert.Parameters.AddWithValue("adequate", request.AdequateEvaluationCompleted);
            insert.Parameters.AddWithValue("owner", request.FollowUpOwner);
            insert.Parameters.AddWithValue("timeframe", request.FollowUpTimeframe);
            insert.Parameters.AddWithValue("nextSteps", request.NextStepInstructions);
            insert.Parameters.AddWithValue("warnings", request.WarningEscalationInstructions);
            insert.Parameters.AddWithValue("communicationMethod", request.CommunicationMethod);
            insert.Parameters.AddWithValue("communicationCompleted", request.CommunicationCompleted);
            insert.Parameters.AddWithValue("locationReconfirmed", request.LocationCallbackReconfirmed);
            insert.Parameters.AddWithValue("emergencyProvided", request.EmergencyInstructionProvided);
            insert.Parameters.AddWithValue("handoff", (object?)request.EmergencyHandoffStatus ?? DBNull.Value);
            insert.Parameters.AddWithValue("contact", (object?)request.ContactAttemptSummary ?? DBNull.Value);
            insert.Parameters.AddWithValue("recordedAt", owner.DatabaseNow);
            insert.Parameters.AddWithValue("physician", physicianStaffId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.Transaction = transaction;
            insertEvent.CommandText = """
                insert into telehealth_consultation_disposition_draft_events(
                  event_id,consultation_id,disposition_version_id,aggregate_version,action,actor_type,actor_id,
                  idempotency_key,command_fingerprint,occurred_at)
                values(@eventId,@consultationId,@versionId,@version,@action,'physician',@actorId,
                  @idempotencyKey,@fingerprint,@occurredAt);
                """;
            insertEvent.Parameters.AddWithValue("eventId", Guid.NewGuid());
            insertEvent.Parameters.AddWithValue("consultationId", consultationId);
            insertEvent.Parameters.AddWithValue("versionId", versionId);
            insertEvent.Parameters.AddWithValue("version", nextVersion);
            insertEvent.Parameters.AddWithValue("action", currentVersion == 0 ? "DraftRecorded" : "DraftRevised");
            insertEvent.Parameters.AddWithValue("actorId", physicianStaffId.ToString(CultureInfo.InvariantCulture));
            insertEvent.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insertEvent.Parameters.AddWithValue("fingerprint", commandFingerprint);
            insertEvent.Parameters.AddWithValue("occurredAt", owner.DatabaseNow);
            await insertEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToDraft(nextVersion, request, owner.DatabaseNow);
    }

    private static async Task<OwnedWrapUp?> ReadOwnedWrapUpAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        bool lockRows,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select context.encounter_id,context.status,now()
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
              and context.physician_staff_id=@physician and context.status='MediaEnded'
              and request.practice_id=@practiceId and request.facility_id=@facilityId and request.status='WrapUp'
              and reservation.clinician_staff_id=@physician and reservation.status='Released'
              and shift.clinician_staff_id=@physician and shift.status='WrapUp'
              and session.status='Ended' and appointment.status='>'
              and encounter.provider_id=@physician and encounter.facility_id=@facilityId
              and encounter.source_appointment_id=context.appointment_id
              and not exists(select 1 from encounter_signatures signature where signature.encounter=encounter.encounter and signature.is_lock)
              and patient.facility_id=@facilityId and patient.merged_into_patient_id is null
              and patient.lifecycle_status='active'
              and patient.date_of_birth between current_date - interval '120 years'
                                                and current_date - interval '18 years'
            """ + (lockRows
                ? " for update of context,request,reservation,shift,session,appointment,encounter,patient;"
                : ";");
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("physician", physicianStaffId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OwnedWrapUp(reader.GetInt32(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<TelehealthSafetyDispositionDraftResponse?> ReadCurrentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = DraftSelect + " from telehealth_consultation_disposition_draft_versions draft where draft.consultation_id=@consultationId order by draft.version desc limit 1;";
        command.Parameters.AddWithValue("consultationId", consultationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDraft(reader) : null;
    }

    private static async Task<Replay?> ReadReplayAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = DraftSelect + """
            ,event.command_fingerprint
            from telehealth_consultation_disposition_draft_versions draft
            join telehealth_consultation_disposition_draft_events event
              on event.disposition_version_id=draft.disposition_version_id
            where draft.consultation_id=@consultationId and event.idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Replay(ReadDraft(reader), reader.GetString(15))
            : null;
    }

    private const string DraftSelect = """
        select draft.version,draft.disposition_code,draft.adequate_evaluation_completed,draft.follow_up_owner,
               draft.follow_up_timeframe,draft.next_step_instructions,draft.warning_escalation_instructions,
               draft.communication_method,draft.communication_completed,draft.location_callback_reconfirmed,
               draft.emergency_instruction_provided,draft.emergency_handoff_status,draft.contact_attempt_summary,
               draft.recorded_at,draft.legal_effect
        """;

    private static TelehealthSafetyDispositionDraftResponse ReadDraft(NpgsqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetBoolean(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetBoolean(8),
        reader.GetBoolean(9),
        reader.GetBoolean(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.GetFieldValue<DateTimeOffset>(13),
        reader.GetBoolean(14),
        Signed: false,
        Finalized: false,
        PatientDelivered: false);

    private static TelehealthSafetyDispositionDraftResponse ToDraft(
        int version,
        RecordTelehealthSafetyDispositionDraftRequest request,
        DateTimeOffset recordedAt) => new(
            version,
            request.DispositionCode,
            request.AdequateEvaluationCompleted,
            request.FollowUpOwner,
            request.FollowUpTimeframe,
            request.NextStepInstructions,
            request.WarningEscalationInstructions,
            request.CommunicationMethod,
            request.CommunicationCompleted,
            request.LocationCallbackReconfirmed,
            request.EmergencyInstructionProvided,
            request.EmergencyHandoffStatus,
            request.ContactAttemptSummary,
            recordedAt,
            LegalEffect: false,
            Signed: false,
            Finalized: false,
            PatientDelivered: false);

    private sealed record OwnedWrapUp(int EncounterId, string ConsultationStatus, DateTimeOffset DatabaseNow);
    private sealed record Replay(TelehealthSafetyDispositionDraftResponse Draft, string CommandFingerprint);
}
