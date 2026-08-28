// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data;
using System.Globalization;
using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthPharmacyChoiceConflictException(string message) : Exception(message);

public sealed class TelehealthPharmacyRepository(NpgsqlDataSource dataSource)
{
    public async Task<TelehealthPharmacyChoiceWorkspaceResponse?> GetWorkspaceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        TelehealthPharmacyDirectorySearch search,
        IPharmacyDirectory directory,
        CancellationToken cancellationToken)
    {
        var matches = directory.Search(search);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);

        var owner = await ReadOwnedWrapUpAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            lockRows: false,
            cancellationToken);
        if (owner is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var preferredIds = await ReadPreferredDirectoryEntryIdsAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            owner.PatientId,
            directory.DatasetId,
            directory.DatasetVersion,
            cancellationToken);
        var currentChoice = await ReadCurrentChoiceAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToWorkspace(
            consultationId,
            owner,
            directory,
            search,
            matches,
            preferredIds,
            currentChoice);
    }

    public async Task<TelehealthPharmacyChoiceDraftResponse?> RecordChoiceAsync(
        string practiceId,
        int facilityId,
        int physicianStaffId,
        Guid consultationId,
        RecordTelehealthPharmacyChoiceRequest request,
        string idempotencyKey,
        string commandFingerprint,
        IPharmacyDirectory directory,
        CancellationToken cancellationToken)
    {
        var entry = directory.Find(request.DirectoryEntryId);
        if (entry is null)
        {
            throw new ArgumentException("The selected synthetic pharmacy is not active in the current directory dataset.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var owner = await ReadOwnedWrapUpAsync(
            connection,
            transaction,
            practiceId,
            facilityId,
            physicianStaffId,
            consultationId,
            lockRows: true,
            cancellationToken);
        if (owner is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var replay = await ReadReplayAsync(
            connection,
            transaction,
            consultationId,
            idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.CommandFingerprint, commandFingerprint, StringComparison.Ordinal))
            {
                throw new TelehealthPharmacyChoiceConflictException(
                    "The idempotency key was already used for different pharmacy-choice content.");
            }
            await transaction.CommitAsync(cancellationToken);
            return replay.Choice;
        }

        var current = await ReadCurrentChoiceAsync(
            connection,
            transaction,
            consultationId,
            cancellationToken);
        var currentVersion = current?.Version ?? 0;
        if (request.ExpectedVersion != currentVersion)
        {
            throw new TelehealthPharmacyChoiceConflictException(
                $"The current pharmacy-choice version is {currentVersion}. Reload before recording another destination.");
        }

        var nextVersion = checked(currentVersion + 1);
        var choiceVersionId = Guid.NewGuid();
        var action = currentVersion == 0 ? "DestinationRecorded" : "DestinationChanged";

        await using (var insertChoice = connection.CreateCommand())
        {
            insertChoice.Transaction = transaction;
            insertChoice.CommandText = """
                insert into telehealth_consultation_pharmacy_choice_versions(
                  choice_version_id,consultation_id,version,directory_entry_id,directory_source,directory_version,
                  pharmacy_name,address_line1,address_line2,city,state_code,postal_code,country_code,phone,
                  ncpdp_id,npi,electronic_routing_capability,choice_basis,patient_choice_confirmed,
                  selected_at,selected_by_staff_id)
                values(
                  @choiceVersionId,@consultationId,@version,@directoryEntryId,@directorySource,@directoryVersion,
                  @name,@line1,@line2,@city,@state,@postalCode,@country,@phone,
                  @ncpdp,@npi,@routing,'PatientConfirmedDuringConsultation',true,
                  @selectedAt,@physician);
                """;
            insertChoice.Parameters.AddWithValue("choiceVersionId", choiceVersionId);
            insertChoice.Parameters.AddWithValue("consultationId", consultationId);
            insertChoice.Parameters.AddWithValue("version", nextVersion);
            insertChoice.Parameters.AddWithValue("directoryEntryId", entry.DirectoryEntryId);
            insertChoice.Parameters.AddWithValue("directorySource", directory.DatasetId);
            insertChoice.Parameters.AddWithValue("directoryVersion", directory.DatasetVersion);
            insertChoice.Parameters.AddWithValue("name", entry.Name);
            insertChoice.Parameters.AddWithValue("line1", entry.AddressLine1);
            insertChoice.Parameters.AddWithValue("line2", (object?)entry.AddressLine2 ?? DBNull.Value);
            insertChoice.Parameters.AddWithValue("city", entry.City);
            insertChoice.Parameters.AddWithValue("state", entry.State);
            insertChoice.Parameters.AddWithValue("postalCode", entry.PostalCode);
            insertChoice.Parameters.AddWithValue("country", entry.Country);
            insertChoice.Parameters.AddWithValue("phone", entry.Phone);
            insertChoice.Parameters.AddWithValue("ncpdp", (object?)entry.NcpdpId ?? DBNull.Value);
            insertChoice.Parameters.AddWithValue("npi", (object?)entry.Npi ?? DBNull.Value);
            insertChoice.Parameters.AddWithValue("routing", entry.ElectronicRoutingCapability);
            insertChoice.Parameters.AddWithValue("selectedAt", owner.DatabaseNow);
            insertChoice.Parameters.AddWithValue("physician", physicianStaffId);
            await insertChoice.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertEvent = connection.CreateCommand())
        {
            insertEvent.Transaction = transaction;
            insertEvent.CommandText = """
                insert into telehealth_consultation_pharmacy_choice_events(
                  event_id,consultation_id,choice_version_id,aggregate_version,action,actor_type,actor_id,
                  idempotency_key,command_fingerprint,occurred_at)
                values(@eventId,@consultationId,@choiceVersionId,@version,@action,'physician',@actorId,
                       @idempotencyKey,@fingerprint,@occurredAt);
                """;
            insertEvent.Parameters.AddWithValue("eventId", Guid.NewGuid());
            insertEvent.Parameters.AddWithValue("consultationId", consultationId);
            insertEvent.Parameters.AddWithValue("choiceVersionId", choiceVersionId);
            insertEvent.Parameters.AddWithValue("version", nextVersion);
            insertEvent.Parameters.AddWithValue("action", action);
            insertEvent.Parameters.AddWithValue("actorId", physicianStaffId.ToString(CultureInfo.InvariantCulture));
            insertEvent.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
            insertEvent.Parameters.AddWithValue("fingerprint", commandFingerprint);
            insertEvent.Parameters.AddWithValue("occurredAt", owner.DatabaseNow);
            await insertEvent.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ToChoice(nextVersion, entry, directory.DatasetId, directory.DatasetVersion, owner.DatabaseNow);
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
            select request.patient_id,context.status,now()
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
              and context.status='MediaEnded'
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
            ? new OwnedWrapUp(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2))
            : null;
    }

    private static async Task<HashSet<Guid>> ReadPreferredDirectoryEntryIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string practiceId,
        int facilityId,
        string patientId,
        string directorySource,
        string directoryVersion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select directory_entry_id
            from (
              select distinct on (directory_entry_id) directory_entry_id,preference_status
              from telehealth_patient_pharmacy_preferences
              where practice_id=@practiceId and facility_id=@facilityId and patient_id=@patientId
                and directory_source=@directorySource and directory_version=@directoryVersion
              order by directory_entry_id,recorded_at desc,preference_id desc
            ) latest
            where preference_status='Added'
            order by directory_entry_id;
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.AddWithValue("directorySource", directorySource);
        command.Parameters.AddWithValue("directoryVersion", directoryVersion);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetGuid(0));
        }
        return result;
    }

    private static async Task<TelehealthPharmacyChoiceDraftResponse?> ReadCurrentChoiceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid consultationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ChoiceSelect + " from telehealth_consultation_pharmacy_choice_versions choice where choice.consultation_id=@consultationId order by choice.version desc limit 1;";
        command.Parameters.AddWithValue("consultationId", consultationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadChoice(reader) : null;
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
        command.CommandText = ChoiceSelect + """
            ,event.command_fingerprint
            from telehealth_consultation_pharmacy_choice_versions choice
            join telehealth_consultation_pharmacy_choice_events event
              on event.choice_version_id=choice.choice_version_id
            where choice.consultation_id=@consultationId and event.idempotency_key=@idempotencyKey;
            """;
        command.Parameters.AddWithValue("consultationId", consultationId);
        command.Parameters.AddWithValue("idempotencyKey", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new Replay(ReadChoice(reader), reader.GetString(17))
            : null;
    }

    private const string ChoiceSelect = """
        select choice.version,choice.directory_entry_id,choice.pharmacy_name,choice.address_line1,choice.address_line2,
               choice.city,choice.state_code,choice.postal_code,choice.country_code,choice.phone,choice.ncpdp_id,
               choice.npi,choice.electronic_routing_capability,choice.directory_source,choice.directory_version,
               choice.choice_basis,choice.selected_at
        """;

    private static TelehealthPharmacyChoiceDraftResponse ReadChoice(NpgsqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetGuid(1),
        reader.GetString(2),
        new TelehealthPharmacyAddressResponse(
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8)),
        reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.GetString(12),
        reader.GetString(13),
        reader.GetString(14),
        reader.GetString(15),
        PatientChoiceConfirmed: true,
        reader.GetFieldValue<DateTimeOffset>(16),
        PrescriptionCreated: false,
        Transmitted: false);

    private static TelehealthPharmacyChoiceWorkspaceResponse ToWorkspace(
        Guid consultationId,
        OwnedWrapUp owner,
        IPharmacyDirectory directory,
        TelehealthPharmacyDirectorySearch search,
        IReadOnlyList<TelehealthPharmacyDirectoryMatch> matches,
        HashSet<Guid> preferredIds,
        TelehealthPharmacyChoiceDraftResponse? currentChoice) => new(
            consultationId,
            owner.ConsultationStatus,
            directory.AdapterMode,
            directory.DatasetId,
            directory.DatasetVersion,
            owner.DatabaseNow,
            search.State,
            search.PostalCode,
            string.IsNullOrEmpty(search.OriginPostalCode) ? null : "EnteredPostalCode",
            search.LocationSearchAcknowledged,
            preferredIds.Count,
            matches.Select(match => ToEntry(match, preferredIds.Contains(match.Entry.DirectoryEntryId))).ToArray(),
            currentChoice,
            PrescriptionEnabled: false,
            TransmissionEnabled: false,
            Limitations:
            [
                "Synthetic choices are neutral directory facts, not endorsements, network participation, availability, fill, or distance guarantees.",
                "Approximate distance is calculated only from an explicitly entered supported postal origin; no precise coordinates or external geocoder are used.",
                "A recorded destination is an unsigned patient-confirmed planning draft. No medication, prescription, signature, transmission, claim, or completion is created."
            ]);

    private static TelehealthPharmacyDirectoryEntryResponse ToEntry(
        TelehealthPharmacyDirectoryMatch match,
        bool isPreferred) => new(
            match.Entry.DirectoryEntryId,
            match.Entry.Name,
            new TelehealthPharmacyAddressResponse(
                match.Entry.AddressLine1,
                match.Entry.AddressLine2,
                match.Entry.City,
                match.Entry.State,
                match.Entry.PostalCode,
                match.Entry.Country),
            match.Entry.Phone,
            match.Entry.NcpdpId,
            match.Entry.Npi,
            match.Entry.ElectronicRoutingCapability,
            isPreferred,
            match.ApproximateDistanceMiles);

    private static TelehealthPharmacyChoiceDraftResponse ToChoice(
        int version,
        TelehealthPharmacyDirectoryEntry entry,
        string directorySource,
        string directoryVersion,
        DateTimeOffset selectedAt) => new(
            version,
            entry.DirectoryEntryId,
            entry.Name,
            new TelehealthPharmacyAddressResponse(
                entry.AddressLine1,
                entry.AddressLine2,
                entry.City,
                entry.State,
                entry.PostalCode,
                entry.Country),
            entry.Phone,
            entry.NcpdpId,
            entry.Npi,
            entry.ElectronicRoutingCapability,
            directorySource,
            directoryVersion,
            "PatientConfirmedDuringConsultation",
            PatientChoiceConfirmed: true,
            selectedAt,
            PrescriptionCreated: false,
            Transmitted: false);

    private sealed record OwnedWrapUp(string PatientId, string ConsultationStatus, DateTimeOffset DatabaseNow);
    private sealed record Replay(TelehealthPharmacyChoiceDraftResponse Choice, string CommandFingerprint);
}
