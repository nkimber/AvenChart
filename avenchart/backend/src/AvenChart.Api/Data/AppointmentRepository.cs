// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data.Common;
using System.Globalization;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AppointmentConcurrencyException : Exception;

public sealed class AppointmentMutationNotAllowedException(string message) : Exception(message);

public sealed class AppointmentRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 500;
    private const int MaximumExpandedOccurrencesPerAppointment = 366;
    private const string VirtualOccurrenceSeparator = "::occurs::";
    private const int RepeatOnRecurrenceType = 2;
    private const int SpecificWeekdaysRecurrenceType = 3;
    private const int SpecificWeekdaysRepeatUnit = 6;
    private const int ReminderWindowDays = 7;

    public async Task<AppointmentSearchResponse> SearchAsync(
        string? patientId,
        string? from,
        string? to,
        int limit,
        int facilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(facilityId);
        var safeLimit = Math.Clamp(limit, 1, MaximumSearchLimit);
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedPatientId = Normalize(patientId);
        var fromDate = ParseDateOrDefault(from, metadata.BaseDate);
        var toDate = ParseOptionalDate(to);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $$"""
            select
                a.id,
                a.row_version,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.email,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                a.appointment_date,
                a.start_time,
                a.duration_minutes,
                a.category_id,
                a.title,
                a.status,
                a.room,
                a.comments,
                a.recurrence_type,
                a.repeat_frequency,
                a.repeat_unit,
                a.repeat_on_num,
                a.repeat_on_day,
                a.repeat_on_frequency,
                a.recurrence_end_date,
                a.recurrence_days,
                a.recurrence_exdates,
                ce.encounter as converted_encounter_id,
                ce.encounter_date as converted_encounter_date,
                coalesce(ce.converted_billing_line_count, 0) as converted_billing_line_count,
                a.provider_id,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                a.facility_id,
                f.name as facility_name,
                a.billing_location_id,
                bf.name as billing_location_name
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            left join facilities bf on bf.id = a.billing_location_id
            left join lateral (
                select
                    e.encounter,
                    e.encounter_date,
                    (
                        select count(*)
                        from billing b
                        where b.pid = e.pid
                          and b.encounter = e.encounter
                          and b.activity = 1
                    )::int as converted_billing_line_count
                from encounters e
                where e.source_appointment_id = a.id
                order by e.encounter desc
                limit 1
            ) ce on true
            where {{AppointmentSearchPredicate}}
            order by a.appointment_date, a.start_time, a.id
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate, toDate, facilityId);

        var expandedAppointments = new List<AppointmentListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            expandedAppointments.AddRange(ExpandAppointmentListItem(
                ReadListItem(reader, metadata.BaseDate),
                fromDate,
                toDate,
                metadata.BaseDate));
        }

        var appointments = AnnotateAppointmentOverlaps(expandedAppointments)
            .OrderBy(appointment => DateOnly.Parse(appointment.Date))
            .ThenBy(appointment => TimeOnly.Parse(appointment.StartTime))
            .ThenBy(appointment => appointment.SeriesRootId)
            .ThenBy(appointment => appointment.OccurrenceNumber.GetValueOrDefault())
            .Take(safeLimit)
            .ToList();

        return new AppointmentSearchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patientId,
            FromDate: fromDate.ToString("yyyy-MM-dd"),
            ToDate: toDate?.ToString("yyyy-MM-dd"),
            Limit: safeLimit,
            TotalMatches: expandedAppointments.Count,
            Appointments: appointments);
    }

    public async Task<AppointmentSchedulingOptionsResponse> GetSchedulingOptionsAsync(
        int facilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(facilityId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var providersCommand = connection.CreateCommand();
        providersCommand.CommandText = """
            select
                s.id,
                trim(concat(s.last_name, ', ', s.first_name)) as display_name,
                s.facility_id,
                f.name as facility_name
            from staff s
            left join facilities f on f.id = s.facility_id
            where s.active = true
              and s.calendar = true
              and s.username <> ''
              and s.facility_id = @facilityId
            order by s.last_name, s.first_name, s.id;
            """;
        providersCommand.Parameters.AddWithValue("facilityId", facilityId);

        var providers = new List<AppointmentSchedulingProviderOption>();
        await using (var reader = await providersCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                providers.Add(new AppointmentSchedulingProviderOption(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    DisplayName: ReadNullableString(reader, "display_name") ?? string.Empty,
                    FacilityId: ReadNullableInt(reader, "facility_id"),
                    FacilityName: ReadNullableString(reader, "facility_name")));
            }
        }

        await using var facilitiesCommand = connection.CreateCommand();
        facilitiesCommand.CommandText = """
            select id, name, code
            from facilities
            where id = @facilityId
              and inactive = false
            order by name, id;
            """;
        facilitiesCommand.Parameters.AddWithValue("facilityId", facilityId);

        var facilities = new List<AppointmentSchedulingFacilityOption>();
        await using (var reader = await facilitiesCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                facilities.Add(new AppointmentSchedulingFacilityOption(
                    Id: reader.GetInt32(reader.GetOrdinal("id")),
                    Name: ReadNullableString(reader, "name") ?? string.Empty,
                    Code: ReadNullableString(reader, "code")));
            }
        }

        return new AppointmentSchedulingOptionsResponse(providers, facilities);
    }

    public async Task<AppointmentDetail?> GetByIdAsync(string appointmentId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        var occurrenceReference = ParseOccurrenceReference(appointmentId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                a.id,
                a.row_version,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.sex,
                p.date_of_birth,
                p.purpose,
                p.email,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                a.appointment_date,
                a.start_time,
                a.duration_minutes,
                a.category_id,
                a.title,
                a.status,
                a.room,
                a.comments,
                a.recurrence_type,
                a.repeat_frequency,
                a.repeat_unit,
                a.repeat_on_num,
                a.repeat_on_day,
                a.repeat_on_frequency,
                a.recurrence_end_date,
                a.recurrence_days,
                a.recurrence_exdates,
                ce.encounter as converted_encounter_id,
                ce.encounter_date as converted_encounter_date,
                coalesce(ce.converted_billing_line_count, 0) as converted_billing_line_count,
                a.provider_id,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                a.facility_id,
                f.name as facility_name,
                a.billing_location_id,
                bf.name as billing_location_name
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            left join facilities bf on bf.id = a.billing_location_id
            left join lateral (
                select
                    e.encounter,
                    e.encounter_date,
                    (
                        select count(*)
                        from billing b
                        where b.pid = e.pid
                          and b.encounter = e.encounter
                          and b.activity = 1
                    )::int as converted_billing_line_count
                from encounters e
                where e.source_appointment_id = a.id
                order by e.encounter desc
                limit 1
            ) ce on true
            where a.id = @appointmentId;
            """;
        command.Parameters.AddWithValue("appointmentId", occurrenceReference.RootAppointmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var repeatOnNum = ReadNullableInt(reader, "repeat_on_num");
        var repeatOnDay = ReadNullableInt(reader, "repeat_on_day");
        var repeatOnFrequency = ReadNullableInt(reader, "repeat_on_frequency");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
        var appointmentDate = ReadDate(reader, "appointment_date");
        var occurrenceDate = occurrenceReference.OccurrenceDate;
        if (occurrenceDate is not null
            && !IsValidOccurrenceDate(
                appointmentDate,
                recurrenceType,
                repeatFrequency,
                repeatUnit,
                repeatOnNum,
                repeatOnDay,
                repeatOnFrequency,
                recurrenceDays,
                recurrenceEndDate,
                recurrenceExdateSet,
                occurrenceDate.Value))
        {
            return null;
        }

        var occurrenceNumber = recurrenceType > 0
            ? CalculateOccurrenceNumber(
                appointmentDate,
                recurrenceType,
                repeatFrequency,
                repeatUnit,
                repeatOnNum,
                repeatOnDay,
                repeatOnFrequency,
                recurrenceDays,
                recurrenceEndDate,
                occurrenceDate?.ToString("yyyy-MM-dd") ?? appointmentDate)
            : null;
        var isVirtualOccurrence = occurrenceDate is not null && occurrenceDate.Value.ToString("yyyy-MM-dd") != appointmentDate;
        var responseDate = occurrenceDate?.ToString("yyyy-MM-dd") ?? appointmentDate;
        var responseId = isVirtualOccurrence
            ? BuildOccurrenceId(occurrenceReference.RootAppointmentId, occurrenceDate!.Value)
            : occurrenceReference.RootAppointmentId;
        var reminder = BuildAppointmentReminder(
            responseDate,
            ReadNullableString(reader, "status"),
            ReadNullableString(reader, "email"),
            ReadNullableString(reader, "phone"),
            ReadNullableString(reader, "phone_home"),
            ReadNullableString(reader, "phone_cell"),
            ReadNullableString(reader, "hipaa_allow_sms"),
            ReadNullableString(reader, "hipaa_allow_email"),
            metadata.BaseDate);

        var detail = new AppointmentDetail(
            Id: responseId,
            RowVersion: reader.GetInt32(reader.GetOrdinal("row_version")),
            SeriesRootId: occurrenceReference.RootAppointmentId,
            IsRecurringSeries: recurrenceType > 0,
            IsVirtualOccurrence: isVirtualOccurrence,
            OccurrenceNumber: occurrenceNumber,
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            PatientDisplayName: BuildDisplayName(reader),
            FirstName: reader.GetString(reader.GetOrdinal("first_name")),
            LastName: reader.GetString(reader.GetOrdinal("last_name")),
            Sex: ReadNullableString(reader, "sex"),
            DateOfBirth: ReadDate(reader, "date_of_birth"),
            Date: responseDate,
            StartTime: ReadTime(reader, "start_time"),
            DurationMinutes: reader.GetInt32(reader.GetOrdinal("duration_minutes")),
            Title: ReadNullableString(reader, "title") ?? "Appointment",
            Status: ReadNullableString(reader, "status"),
            Room: ReadNullableString(reader, "room"),
            CategoryId: ReadNullableInt(reader, "category_id"),
            CategoryName: GetAppointmentCategoryName(ReadNullableInt(reader, "category_id")),
            ProviderId: ReadNullableInt(reader, "provider_id"),
            ProviderName: ReadNullableString(reader, "provider_name"),
            FacilityId: ReadNullableInt(reader, "facility_id"),
            FacilityName: ReadNullableString(reader, "facility_name"),
            BillingLocationId: ReadNullableInt(reader, "billing_location_id"),
            BillingLocationName: ReadNullableString(reader, "billing_location_name"),
            Comments: ReadNullableString(reader, "comments"),
            RecurrenceType: recurrenceType,
            RepeatFrequency: repeatFrequency,
            RepeatUnit: repeatUnit,
            RepeatOnNum: repeatOnNum,
            RepeatOnDay: repeatOnDay,
            RepeatOnFrequency: repeatOnFrequency,
            RecurrenceDays: recurrenceDays,
            RecurrenceEndDate: recurrenceEndDate,
            RecurrenceExdates: recurrenceExdates,
            RecurrenceExceptionCount: recurrenceExdates.Count,
            RecurrenceLabel: BuildRecurrenceLabel(recurrenceType, repeatFrequency, repeatUnit, repeatOnNum, repeatOnDay, repeatOnFrequency, recurrenceDays, recurrenceEndDate),
            PatientPurpose: ReadNullableString(reader, "purpose"),
            ProviderOverlapCount: 0,
            ProviderOverlapAppointmentIds: Array.Empty<string>(),
            PatientOverlapCount: 0,
            PatientOverlapAppointmentIds: Array.Empty<string>(),
            RoomOverlapCount: 0,
            RoomOverlapAppointmentIds: Array.Empty<string>(),
            ReminderDue: reminder.Due,
            ReminderStatus: reminder.Status,
            ReminderChannel: reminder.Channel,
            ReminderContact: reminder.Contact,
            ReminderLeadDays: reminder.LeadDays,
            ConvertedEncounterId: ReadNullableInt(reader, "converted_encounter_id"),
            ConvertedEncounterDate: ReadNullableDate(reader, "converted_encounter_date"),
            ConvertedBillingLineCount: reader.GetInt32(reader.GetOrdinal("converted_billing_line_count")));

        var providerOverlapSummary = await GetProviderOverlapSummaryAsync(detail, cancellationToken);
        var patientOverlapSummary = await GetPatientOverlapSummaryAsync(detail, cancellationToken);
        var roomOverlapSummary = await GetRoomOverlapSummaryAsync(detail, cancellationToken);
        return detail with
        {
            ProviderOverlapCount = providerOverlapSummary.Count,
            ProviderOverlapAppointmentIds = providerOverlapSummary.AppointmentIds,
            PatientOverlapCount = patientOverlapSummary.Count,
            PatientOverlapAppointmentIds = patientOverlapSummary.AppointmentIds,
            RoomOverlapCount = roomOverlapSummary.Count,
            RoomOverlapAppointmentIds = roomOverlapSummary.AppointmentIds
        };
    }

    public async Task<AppointmentDetail?> CreateAsync(
        AppointmentCreateRequest request,
        int facilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(facilityId);
        EnsureRequestedFacilityMatchesAccessContext(request.FacilityId, facilityId);
        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var startTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var patient = await EnsurePatientActiveForSchedulingAsync(
            connection,
            transaction,
            request.PatientId,
            cancellationToken,
            facilityId);
        await ValidateActiveSchedulingReferencesAsync(
            connection,
            request.ProviderId,
            facilityId,
            request.BillingLocationId,
            cancellationToken);
        await EnsureProviderBelongsToFacilityAsync(
            connection,
            request.ProviderId ?? patient.ProviderId,
            facilityId,
            cancellationToken);

        if (request.EnforceConflictPolicy)
        {
            await AcquireSchedulingConflictLocksAsync(
                connection,
                transaction,
                appointmentDate,
                patient.LegacyPid,
                request.ProviderId ?? patient.ProviderId,
                NormalizeText(request.Room),
                cancellationToken);

            var validation = await ValidateAvailabilityAsync(
                new AppointmentAvailabilityValidationRequest(
                    PatientId: patient.CanonicalId,
                    ProviderId: request.ProviderId,
                    Date: request.Date,
                    StartTime: request.StartTime,
                    DurationMinutes: request.DurationMinutes,
                    FacilityId: facilityId,
                    Room: request.Room,
                    ExcludeAppointmentId: null),
                facilityId,
                cancellationToken);
            if (validation is null)
            {
                throw new ArgumentException("Appointment availability could not be validated from the supplied patient, date, time, and duration.");
            }

            if (!validation.Available)
            {
                throw new AppointmentAvailabilityConflictException(validation);
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            with patient_match as (
                select canonical_id, legacy_pid, provider_id, facility_id
                from patients
                where facility_id = @facilityId
                  and (lower(canonical_id) = lower(@patientId)
                       or lower(pubpid) = lower(@patientId)
                       or legacy_pid::text = @patientId)
                limit 1
            )
            insert into appointments (
                id,
                patient_id,
                pid,
                provider_id,
                facility_id,
                billing_location_id,
                appointment_date,
                start_time,
                duration_minutes,
                category_id,
                title,
                status,
                room,
                comments,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                repeat_on_num,
                repeat_on_day,
                repeat_on_frequency,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            )
            select
                @id,
                canonical_id,
                legacy_pid,
                coalesce((select id from staff where id = @providerId), provider_id),
                @facilityId,
                coalesce((select id from facilities where id = @billingLocationId), @facilityId),
                @appointmentDate,
                @startTime,
                @durationMinutes,
                coalesce(@categoryId, 9),
                @title,
                '-',
                @room,
                @comments,
                @recurrenceType,
                @repeatFrequency,
                @repeatUnit,
                @repeatOnNum,
                @repeatOnDay,
                @repeatOnFrequency,
                @recurrenceEndDate,
                @recurrenceDays,
                @recurrenceExdates
            from patient_match
            returning id;
            """;
        var appointmentId = $"APPT-MODERN-{Guid.NewGuid():N}";
        command.Parameters.AddWithValue("id", appointmentId);
        command.Parameters.AddWithValue("patientId", request.PatientId.Trim());
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.Add("billingLocationId", NpgsqlDbType.Integer).Value = request.BillingLocationId is null ? DBNull.Value : request.BillingLocationId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = startTime;
        command.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        command.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        command.Parameters.AddWithValue("title", NormalizeText(request.Title) ?? "Appointment");
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;
        command.Parameters.Add("comments", NpgsqlDbType.Text).Value = NormalizeText(request.Comments) ?? (object)DBNull.Value;
        AddRecurrenceParameters(
            command,
            request.RecurrenceType,
            request.RepeatFrequency,
            request.RepeatUnit,
            request.RepeatOnNum,
            request.RepeatOnDay,
            request.RepeatOnFrequency,
            request.RecurrenceDays,
            request.RecurrenceEndDate,
            request.RecurrenceExdates);

        var insertedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return insertedId is null ? null : await GetByIdAsync(insertedId, cancellationToken);
    }

    public async Task<AppointmentWaitlistResponse> GetWaitlistAsync(
        int facilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(facilityId);
        var metadata = await GetMetadataAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                a.id,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                a.appointment_date,
                a.start_time,
                a.duration_minutes,
                a.title,
                a.status,
                a.category_id,
                a.room,
                a.comments,
                a.provider_id,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                a.facility_id,
                f.name as facility_name,
                m.id as reminder_id,
                m.status as reminder_status,
                m.assigned_to as reminder_assigned_to,
                m.body as reminder_body
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            left join lateral (
                select id, status, assigned_to, body
                from messages
                where pid = a.pid
                  and deleted = 0
                  and activity = 1
                  and title = 'Patient Reminders'
                  and portal_relation = concat('portal:appointment-request:', a.id)
                order by message_date desc, id desc
                limit 1
            ) m on true
            where a.status = '^'
              and a.appointment_date >= @baseDate
              and p.facility_id = @facilityId
            order by a.appointment_date, a.start_time, p.last_name, p.first_name, a.id;
            """;
        command.Parameters.Add("baseDate", NpgsqlDbType.Date).Value = metadata.BaseDate;
        command.Parameters.AddWithValue("facilityId", facilityId);

        var items = new List<AppointmentWaitlistItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var appointmentDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date"));
            var startTime = reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time"));
            var durationMinutes = reader.GetInt32(reader.GetOrdinal("duration_minutes"));
            var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));
            var daysUntilRequestedSlot = appointmentDate.DayNumber - metadata.BaseDate.DayNumber;
            var categoryId = ReadNullableInt(reader, "category_id");
            var patientId = reader.GetString(reader.GetOrdinal("patient_id"));
            var firstName = ReadNullableString(reader, "first_name");
            var lastName = ReadNullableString(reader, "last_name");
            var preferredName = ReadNullableString(reader, "preferred_name");
            var displayName = !string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {preferredName}".Trim(' ', ',')
                : NormalizePatientName(firstName, lastName, patientId);
            var reminderId = ReadNullableString(reader, "reminder_id");

            items.Add(new AppointmentWaitlistItem(
                AppointmentId: reader.GetString(reader.GetOrdinal("id")),
                PatientId: patientId,
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                Pubpid: ReadNullableString(reader, "pubpid") ?? patientId,
                PatientDisplayName: displayName,
                Date: appointmentDate.ToString("yyyy-MM-dd"),
                StartTime: startTime.ToString("HH:mm"),
                EndTime: endTime.ToString("HH:mm"),
                DurationMinutes: durationMinutes,
                Title: ReadNullableString(reader, "title") ?? "Appointment request",
                Status: ReadNullableString(reader, "status"),
                CategoryId: categoryId,
                CategoryName: GetAppointmentCategoryName(categoryId),
                ProviderId: ReadNullableInt(reader, "provider_id"),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                Room: ReadNullableString(reader, "room"),
                Reason: ReadNullableString(reader, "comments"),
                DaysUntilRequestedSlot: daysUntilRequestedSlot,
                Priority: GetWaitlistPriority(daysUntilRequestedSlot, reminderId),
                ReminderCreated: reminderId is not null,
                ReminderId: reminderId,
                ReminderStatus: ReadNullableString(reader, "reminder_status"),
                ReminderAssignedTo: ReadNullableString(reader, "reminder_assigned_to"),
                ReminderBody: ReadNullableString(reader, "reminder_body")));
        }

        return new AppointmentWaitlistResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            TotalWaiting: items.Count,
            Items: items);
    }

    public async Task<AppointmentReminderDispatchResponse?> DispatchReminderAsync(
        string appointmentId,
        string? templateId,
        CancellationToken cancellationToken)
    {
        if (!await IsAppointmentReminderRuleActiveAsync(cancellationToken))
        {
            return null;
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        var dispatch = await BuildReminderDispatchAsync(appointmentId, metadata, templateId, cancellationToken);
        if (dispatch is null)
        {
            return null;
        }

        await SaveReminderDispatchAsync(dispatch, metadata.BaseDate, cancellationToken);
        return dispatch;
    }

    public async Task<AppointmentReminderTemplateCatalogResponse> GetReminderTemplateCatalogAsync(CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        return new AppointmentReminderTemplateCatalogResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            Templates: ReminderTemplateOptions);
    }

    public async Task<AppointmentReminderDispatchResponse?> RetryReminderDispatchAsync(
        string appointmentId,
        CancellationToken cancellationToken)
    {
        if (!await IsAppointmentReminderRuleActiveAsync(cancellationToken))
        {
            return null;
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        var dispatch = await BuildReminderDispatchAsync(appointmentId, metadata, null, cancellationToken);
        if (dispatch is null)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = """
            select
              dispatch_id,
              coalesce(retry_attempt, 0) as retry_attempt,
              (
                select count(*)
                from appointment_reminder_dispatch_audit retry_rows
                where retry_rows.appointment_id = @appointmentId
                  and retry_rows.retry_of_dispatch_id is not null
              ) as retry_count
            from appointment_reminder_dispatch_audit
            where appointment_id = @appointmentId
            order by created_at desc, audit_id desc
            limit 1;
            """;
        lookup.Parameters.AddWithValue("appointmentId", dispatch.AppointmentId);

        string? priorDispatchId = null;
        var retryAttempt = 0;
        await using (var reader = await lookup.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            priorDispatchId = reader.GetString(reader.GetOrdinal("dispatch_id"));
            retryAttempt = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("retry_count"))) + 1;
        }

        var retryDispatchId = $"{dispatch.DispatchId}-RETRY-{retryAttempt}";
        var retryTimestamp = DateTime.Parse($"{metadata.BaseDate:yyyy-MM-dd}T12:10:00", CultureInfo.InvariantCulture)
            .AddMinutes(retryAttempt * 10);
        var retryDispatch = dispatch with
        {
            DispatchId = retryDispatchId,
            AuditId = $"AUD-{retryDispatchId}",
            DispatchedAt = $"{retryTimestamp:yyyy-MM-dd'T'HH:mm:ss'Z'}",
            DispatchStatus = $"{dispatch.ReminderChannel} retry queued",
            ExternalReference = $"{dispatch.ExternalReference}-RETRY-{retryAttempt}",
            MessagePreview = $"{dispatch.MessagePreview} Retry attempt {retryAttempt} after {priorDispatchId}.",
            RetryOfDispatchId = priorDispatchId,
            RetryAttempt = retryAttempt
        };

        await SaveReminderDispatchAsync(retryDispatch, metadata.BaseDate, cancellationToken);
        return retryDispatch;
    }

    private async Task SaveReminderDispatchAsync(
        AppointmentReminderDispatchResponse dispatch,
        DateOnly baseDate,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into appointment_reminder_dispatch_audit (
              audit_id,
              dataset_id,
              dataset_version,
              as_of_date,
              appointment_id,
              dispatch_id,
              dispatched_at,
              patient_id,
              legacy_pid,
              pubpid,
              patient_display_name,
              appointment_date,
              start_time,
              end_time,
              title,
              reminder_status,
              reminder_channel,
              reminder_contact,
              reminder_lead_days,
              queue_name,
              dispatch_status,
              external_reference,
              template_name,
              message_preview,
              retry_of_dispatch_id,
              retry_attempt,
              created_at
            )
            values (
              @auditId,
              @datasetId,
              @datasetVersion,
              @asOfDate,
              @appointmentId,
              @dispatchId,
              @dispatchedAt,
              @patientId,
              @legacyPid,
              @pubpid,
              @patientDisplayName,
              @appointmentDate,
              @startTime,
              @endTime,
              @title,
              @reminderStatus,
              @reminderChannel,
              @reminderContact,
              @reminderLeadDays,
              @queueName,
              @dispatchStatus,
              @externalReference,
              @templateName,
              @messagePreview,
              @retryOfDispatchId,
              @retryAttempt,
              @createdAt
            )
            on conflict (audit_id) do update set
              dataset_id = excluded.dataset_id,
              dataset_version = excluded.dataset_version,
              as_of_date = excluded.as_of_date,
              appointment_id = excluded.appointment_id,
              dispatch_id = excluded.dispatch_id,
              dispatched_at = excluded.dispatched_at,
              patient_id = excluded.patient_id,
              legacy_pid = excluded.legacy_pid,
              pubpid = excluded.pubpid,
              patient_display_name = excluded.patient_display_name,
              appointment_date = excluded.appointment_date,
              start_time = excluded.start_time,
              end_time = excluded.end_time,
              title = excluded.title,
              reminder_status = excluded.reminder_status,
              reminder_channel = excluded.reminder_channel,
              reminder_contact = excluded.reminder_contact,
              reminder_lead_days = excluded.reminder_lead_days,
              queue_name = excluded.queue_name,
              dispatch_status = excluded.dispatch_status,
              external_reference = excluded.external_reference,
              template_name = excluded.template_name,
              message_preview = excluded.message_preview,
              retry_of_dispatch_id = excluded.retry_of_dispatch_id,
              retry_attempt = excluded.retry_attempt,
              created_at = excluded.created_at;
            """;
        AddReminderDispatchParameters(command, dispatch, baseDate);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<bool> IsAppointmentReminderRuleActiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select exists(select 1 from clinical_alert_rules where rule_key='APPOINTMENT_REMINDER' and active=true and trigger_type='appointment' and target_type='reminder');";
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<AppointmentReminderDispatchHistoryResponse> GetReminderDispatchHistoryAsync(
        string? appointmentId,
        int limit,
        int facilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(facilityId);
        var metadata = await GetMetadataAsync(cancellationToken);
        var safeLimit = Math.Clamp(limit, 1, 50);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              audit_id,
              dataset_id,
              dataset_version,
              as_of_date,
              appointment_id,
              dispatch_id,
              to_char(dispatched_at, 'YYYY-MM-DD"T"HH24:MI:SS"Z"') as dispatched_at_text,
              patient_id,
              legacy_pid,
              pubpid,
              patient_display_name,
              appointment_date,
              start_time,
              end_time,
              title,
              reminder_status,
              reminder_channel,
              reminder_contact,
              reminder_lead_days,
              queue_name,
              dispatch_status,
              external_reference,
              template_name,
              message_preview,
              retry_of_dispatch_id,
              coalesce(retry_attempt, 0) as retry_attempt
            from appointment_reminder_dispatch_audit
            where exists (
                    select 1
                    from patients patient
                    where patient.legacy_pid = appointment_reminder_dispatch_audit.legacy_pid
                      and patient.facility_id = @facilityId)
              and (@appointmentId is null or appointment_id = @appointmentId)
            order by created_at desc, audit_id
            limit @limit;
            """;
        command.Parameters.Add("appointmentId", NpgsqlDbType.Text).Value = NormalizeText(appointmentId) ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("facilityId", facilityId);
        command.Parameters.AddWithValue("limit", safeLimit);

        var entries = new List<AppointmentReminderDispatchResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AppointmentReminderDispatchResponse(
                DatasetId: reader.GetString(reader.GetOrdinal("dataset_id")),
                DatasetVersion: reader.GetString(reader.GetOrdinal("dataset_version")),
                AsOfDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("as_of_date")).ToString("yyyy-MM-dd"),
                AppointmentId: reader.GetString(reader.GetOrdinal("appointment_id")),
                DispatchId: reader.GetString(reader.GetOrdinal("dispatch_id")),
                AuditId: reader.GetString(reader.GetOrdinal("audit_id")),
                DispatchedAt: reader.GetString(reader.GetOrdinal("dispatched_at_text")),
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                AppointmentDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date")).ToString("yyyy-MM-dd"),
                StartTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time")).ToString("HH:mm"),
                EndTime: reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("end_time")).ToString("HH:mm"),
                Title: reader.GetString(reader.GetOrdinal("title")),
                ReminderStatus: reader.GetString(reader.GetOrdinal("reminder_status")),
                ReminderChannel: reader.GetString(reader.GetOrdinal("reminder_channel")),
                ReminderContact: ReadNullableString(reader, "reminder_contact"),
                ReminderLeadDays: ReadNullableInt(reader, "reminder_lead_days"),
                QueueName: reader.GetString(reader.GetOrdinal("queue_name")),
                DispatchStatus: reader.GetString(reader.GetOrdinal("dispatch_status")),
                ExternalReference: reader.GetString(reader.GetOrdinal("external_reference")),
                TemplateName: reader.GetString(reader.GetOrdinal("template_name")),
                MessagePreview: reader.GetString(reader.GetOrdinal("message_preview")),
                RetryOfDispatchId: ReadNullableString(reader, "retry_of_dispatch_id"),
                RetryAttempt: reader.GetInt32(reader.GetOrdinal("retry_attempt"))));
        }

        return new AppointmentReminderDispatchHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            EventCount: entries.Count,
            Entries: entries);
    }

    public async Task<AppointmentAvailabilityValidationResponse?> ValidateAvailabilityAsync(
        AppointmentAvailabilityValidationRequest request,
        int accessFacilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(accessFacilityId);
        EnsureRequestedFacilityMatchesAccessContext(request.FacilityId, accessFacilityId);
        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var startTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        var startMinutes = startTime.Hour * 60 + startTime.Minute;
        var endMinutes = startMinutes + request.DurationMinutes;
        if (endMinutes > 24 * 60)
        {
            return null;
        }

        var endTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(endMinutes));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with patient_match as (
                select canonical_id, legacy_pid, pubpid, first_name, last_name, provider_id, facility_id
                from patients
                where facility_id = @accessFacilityId
                  and (lower(canonical_id) = lower(@patientId)
                       or lower(pubpid) = lower(@patientId)
                       or legacy_pid::text = @patientId)
                limit 1
            ),
            selected as (
                select
                    canonical_id,
                    legacy_pid,
                    pubpid,
                    first_name,
                    last_name,
                    coalesce(@providerId, provider_id) as provider_id,
                    @accessFacilityId as facility_id
                from patient_match
            )
            select
                selected.canonical_id,
                selected.legacy_pid,
                selected.pubpid,
                selected.first_name,
                selected.last_name,
                selected.provider_id,
                trim(concat(coalesce(staff.first_name, ''), ' ', coalesce(staff.last_name, ''))) as provider_name,
                staff.active as provider_active,
                selected.facility_id,
                facilities.name as facility_name,
                (facilities.id is not null and facilities.inactive = false) as facility_active
            from selected
            left join staff on staff.id = selected.provider_id
              and staff.facility_id = @accessFacilityId
            left join facilities on facilities.id = selected.facility_id;
            """;
        command.Parameters.AddWithValue("patientId", request.PatientId.Trim());
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.AddWithValue("accessFacilityId", accessFacilityId);

        string? patientCanonicalId = null;
        int? legacyPid = null;
        string? patientDisplayName = null;
        int? providerId = null;
        string? providerName = null;
        var providerActive = false;
        int? facilityId = null;
        string? facilityName = null;
        var facilityActive = false;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                patientCanonicalId = ReadNullableString(reader, "canonical_id");
                legacyPid = reader.GetInt32(reader.GetOrdinal("legacy_pid"));
                var firstName = ReadNullableString(reader, "first_name");
                var lastName = ReadNullableString(reader, "last_name");
                patientDisplayName = NormalizePatientName(firstName, lastName, patientCanonicalId);
                providerId = ReadNullableInt(reader, "provider_id");
                providerName = NormalizeText(ReadNullableString(reader, "provider_name"));
                providerActive = !reader.IsDBNull(reader.GetOrdinal("provider_active")) && reader.GetBoolean(reader.GetOrdinal("provider_active"));
                facilityId = ReadNullableInt(reader, "facility_id");
                facilityName = NormalizeText(ReadNullableString(reader, "facility_name"));
                facilityActive = !reader.IsDBNull(reader.GetOrdinal("facility_active")) && reader.GetBoolean(reader.GetOrdinal("facility_active"));
            }
        }

        if (patientCanonicalId is null || legacyPid is null)
        {
            return new AppointmentAvailabilityValidationResponse(
                Available: false,
                ValidationStatus: "patient-not-found",
                Date: appointmentDate.ToString("yyyy-MM-dd"),
                StartTime: startTime.ToString("HH:mm"),
                EndTime: endTime.ToString("HH:mm"),
                DurationMinutes: request.DurationMinutes,
                PatientKnown: false,
                ProviderId: null,
                ProviderName: null,
                ProviderAvailable: false,
                FacilityId: null,
                FacilityName: null,
                FacilityAvailable: false,
                WithinBusinessHours: false,
                ConflictCount: 0,
                Conflicts: Array.Empty<AppointmentAvailabilityConflict>(),
                Messages: new[] { "Patient was not found for appointment availability validation." });
        }

        var weekday = appointmentDate.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        var withinBusinessHours = weekday && startMinutes >= 8 * 60 && endMinutes <= 17 * 60;
        var messages = new List<string>();
        if (!withinBusinessHours)
        {
            messages.Add("Requested time is outside the shared provider/facility bookable window of Monday-Friday 08:00-17:00.");
        }

        if (providerId is null)
        {
            messages.Add("No provider could be resolved from the request or patient default.");
        }
        else if (!providerActive)
        {
            messages.Add("Resolved provider is inactive or was not found.");
        }

        if (facilityId is null)
        {
            messages.Add("No facility could be resolved from the request or patient default.");
        }
        else if (!facilityActive)
        {
            messages.Add("Resolved facility is inactive or was not found.");
        }

        var conflicts = await GetAvailabilityConflictsAsync(
            connection,
            appointmentDate,
            startTime,
            endTime,
            legacyPid.Value,
            providerId,
            NormalizeText(request.Room),
            NormalizeText(request.ExcludeAppointmentId),
            cancellationToken);

        if (conflicts.Count > 0)
        {
            messages.Add($"Requested time has {conflicts.Count} active scheduling conflict(s).");
        }

        var providerAvailable = providerId is not null && providerActive && withinBusinessHours;
        var facilityAvailable = facilityId is not null && facilityActive && withinBusinessHours;
        var available = providerAvailable && facilityAvailable && conflicts.Count == 0;
        if (available)
        {
            messages.Add("Requested time is available for the resolved provider and facility.");
        }

        return new AppointmentAvailabilityValidationResponse(
            Available: available,
            ValidationStatus: available ? "available" : "unavailable",
            Date: appointmentDate.ToString("yyyy-MM-dd"),
            StartTime: startTime.ToString("HH:mm"),
            EndTime: endTime.ToString("HH:mm"),
            DurationMinutes: request.DurationMinutes,
            PatientKnown: true,
            ProviderId: providerId,
            ProviderName: providerName,
            ProviderAvailable: providerAvailable,
            FacilityId: facilityId,
            FacilityName: facilityName,
            FacilityAvailable: facilityAvailable,
            WithinBusinessHours: withinBusinessHours,
            ConflictCount: conflicts.Count,
            Conflicts: conflicts,
            Messages: messages);
    }

    public async Task<AppointmentDetail?> UpdateStatusAsync(
        string appointmentId,
        AppointmentStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var rootAppointmentId = ParseOccurrenceReference(appointmentId).RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetMutationStateForUpdateAsync(connection, transaction, rootAppointmentId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        EnsureExpectedVersion(current.RowVersion, request.ExpectedVersion);
        var nextStatus = NormalizeAppointmentStatus(request.Status);
        EnsureValidAppointmentStatusTransition(current.Status, nextStatus);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update appointments
            set status = @status,
                title = coalesce(@title, title),
                row_version = row_version + 1
            where id = @appointmentId
            returning id;
            """;
        command.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        command.Parameters.AddWithValue("status", nextStatus);
        command.Parameters.Add("title", NpgsqlDbType.Text).Value = NormalizeText(request.Title) ?? (object)DBNull.Value;

        var updatedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updatedId is null ? null : await GetByIdAsync(updatedId, cancellationToken);
    }

    public async Task<AppointmentDetail?> UpdateAsync(
        string appointmentId,
        AppointmentUpdateRequest request,
        int accessFacilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(accessFacilityId);
        EnsureRequestedFacilityMatchesAccessContext(request.FacilityId, accessFacilityId);
        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var startTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        var rootAppointmentId = ParseOccurrenceReference(appointmentId).RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetMutationStateForUpdateAsync(connection, transaction, rootAppointmentId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        EnsureExpectedVersion(current.RowVersion, request.ExpectedVersion);
        EnsureSchedulingFactsRemainMutable(current.Status);

        var requestedStatus = request.Status is null ? null : NormalizeAppointmentStatus(request.Status);
        if (requestedStatus is not null)
        {
            EnsureValidAppointmentStatusTransition(current.Status, requestedStatus);
        }
        await EnsurePatientActiveForSchedulingAsync(connection, transaction, current.PatientId, cancellationToken);
        await ValidateActiveSchedulingReferencesAsync(
            connection,
            request.ProviderId,
            accessFacilityId,
            request.BillingLocationId,
            cancellationToken);
        await EnsureProviderBelongsToFacilityAsync(
            connection,
            request.ProviderId,
            accessFacilityId,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update appointments
            set provider_id = coalesce((select id from staff where id = @providerId), provider_id),
                facility_id = coalesce((select id from facilities where id = @facilityId), facility_id),
                billing_location_id = coalesce((select id from facilities where id = @billingLocationId), billing_location_id),
                appointment_date = @appointmentDate,
                start_time = @startTime,
                duration_minutes = @durationMinutes,
                category_id = coalesce(@categoryId, category_id),
                title = @title,
                status = coalesce(@status, status),
                room = @room,
                comments = @comments,
                recurrence_type = @recurrenceType,
                repeat_frequency = @repeatFrequency,
                repeat_unit = @repeatUnit,
                repeat_on_num = @repeatOnNum,
                repeat_on_day = @repeatOnDay,
                repeat_on_frequency = @repeatOnFrequency,
                recurrence_end_date = @recurrenceEndDate,
                recurrence_days = @recurrenceDays,
                recurrence_exdates = @recurrenceExdates,
                row_version = row_version + 1
            where id = @appointmentId
            returning id;
            """;
        command.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.Add("billingLocationId", NpgsqlDbType.Integer).Value = request.BillingLocationId is null ? DBNull.Value : request.BillingLocationId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = startTime;
        command.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        command.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        command.Parameters.AddWithValue("title", NormalizeText(request.Title) ?? "Appointment");
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = requestedStatus ?? (object)DBNull.Value;
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;
        command.Parameters.Add("comments", NpgsqlDbType.Text).Value = NormalizeText(request.Comments) ?? (object)DBNull.Value;
        AddRecurrenceParameters(
            command,
            request.RecurrenceType,
            request.RepeatFrequency,
            request.RepeatUnit,
            request.RepeatOnNum,
            request.RepeatOnDay,
            request.RepeatOnFrequency,
            request.RecurrenceDays,
            request.RecurrenceEndDate,
            request.RecurrenceExdates);

        var updatedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updatedId is null ? null : await GetByIdAsync(updatedId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string appointmentId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var occurrenceReference = ParseOccurrenceReference(appointmentId);
        if (occurrenceReference.OccurrenceDate is not null)
        {
            return await AddRecurrenceExceptionAsync(
                occurrenceReference.RootAppointmentId,
                occurrenceReference.OccurrenceDate.Value,
                expectedVersion,
                cancellationToken);
        }

        throw new AppointmentMutationNotAllowedException(
            "Appointments are retained as scheduling evidence. Cancel the appointment instead of deleting it.");
    }

    public async Task<AppointmentDetail?> RestoreRecurrenceExceptionAsync(
        string appointmentId,
        string occurrenceDateText,
        AppointmentRecurrenceExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(occurrenceDateText, out var occurrenceDate))
        {
            return null;
        }

        var occurrenceReference = ParseOccurrenceReference(appointmentId);
        var rootAppointmentId = occurrenceReference.RootAppointmentId;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = """
            select patient_id,
                row_version,
                appointment_date,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                repeat_on_num,
                repeat_on_day,
                repeat_on_frequency,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1
            for update;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        EnsureExpectedVersion(reader.GetInt32(reader.GetOrdinal("row_version")), request.ExpectedVersion);
        var patientId = reader.GetString(reader.GetOrdinal("patient_id"));
        var appointmentDate = ReadDate(reader, "appointment_date");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var repeatOnNum = ReadNullableInt(reader, "repeat_on_num");
        var repeatOnDay = ReadNullableInt(reader, "repeat_on_day");
        var repeatOnFrequency = ReadNullableInt(reader, "repeat_on_frequency");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
        if (!recurrenceExdateSet.Contains(occurrenceDate))
        {
            return null;
        }

        var updatedExdates = recurrenceExdates
            .Where(date => !DateOnly.TryParse(date, out var parsedDate) || parsedDate != occurrenceDate)
            .ToList();
        var updatedExdateSet = ParseRecurrenceExdateSet(updatedExdates);
        if (!IsValidOccurrenceDate(
                appointmentDate,
                recurrenceType,
                repeatFrequency,
                repeatUnit,
                repeatOnNum,
                repeatOnDay,
                repeatOnFrequency,
                recurrenceDays,
                recurrenceEndDate,
                updatedExdateSet,
                occurrenceDate))
        {
            return null;
        }

        var normalizedExdates = NormalizeRecurrenceExdates(updatedExdates);

        await reader.DisposeAsync();
        await EnsurePatientActiveForSchedulingAsync(connection, transaction, patientId, cancellationToken);
        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates,
                row_version = row_version + 1
            where id = @appointmentId;
            """;
        updateCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        updateCommand.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates)
            ? DBNull.Value
            : (object)normalizedExdates;

        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(rootAppointmentId, cancellationToken);
    }

    public async Task<AppointmentDetail?> RescheduleOccurrenceAsync(
        string appointmentId,
        string occurrenceDateText,
        AppointmentOccurrenceRescheduleRequest request,
        int accessFacilityId,
        CancellationToken cancellationToken)
    {
        EnsureFacilityId(accessFacilityId);
        EnsureRequestedFacilityMatchesAccessContext(request.FacilityId, accessFacilityId);
        if (!DateOnly.TryParse(occurrenceDateText, out var occurrenceDate)
            || !DateOnly.TryParse(request.Date, out var rescheduledDate)
            || !TimeOnly.TryParse(request.StartTime, out var rescheduledStartTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        var rootAppointmentId = ParseOccurrenceReference(appointmentId).RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = """
            select id,
                patient_id,
                row_version,
                pid,
                provider_id,
                facility_id,
                billing_location_id,
                appointment_date,
                duration_minutes,
                category_id,
                title,
                status,
                room,
                comments,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                repeat_on_num,
                repeat_on_day,
                repeat_on_frequency,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1
            for update;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        AppointmentRescheduleSource source;
        await using (var reader = await readCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            EnsureExpectedVersion(reader.GetInt32(reader.GetOrdinal("row_version")), request.ExpectedVersion);
            var appointmentDate = ReadDate(reader, "appointment_date");
            var recurrenceType = ReadRecurrenceType(reader);
            var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
            var repeatUnit = ReadNullableInt(reader, "repeat_unit");
            var repeatOnNum = ReadNullableInt(reader, "repeat_on_num");
            var repeatOnDay = ReadNullableInt(reader, "repeat_on_day");
            var repeatOnFrequency = ReadNullableInt(reader, "repeat_on_frequency");
            var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
            var recurrenceDays = ReadIntList(reader, "recurrence_days");
            var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
            var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
            if (!IsValidOccurrenceDate(
                    appointmentDate,
                    recurrenceType,
                    repeatFrequency,
                    repeatUnit,
                    repeatOnNum,
                    repeatOnDay,
                    repeatOnFrequency,
                    recurrenceDays,
                    recurrenceEndDate,
                    recurrenceExdateSet,
                    occurrenceDate))
            {
                return null;
            }

            source = new AppointmentRescheduleSource(
                PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                Pid: reader.GetInt32(reader.GetOrdinal("pid")),
                ProviderId: ReadNullableInt(reader, "provider_id"),
                FacilityId: ReadNullableInt(reader, "facility_id"),
                BillingLocationId: ReadNullableInt(reader, "billing_location_id"),
                AppointmentDate: appointmentDate,
                CategoryId: ReadNullableInt(reader, "category_id"),
                Title: ReadNullableString(reader, "title"),
                Status: ReadNullableString(reader, "status"),
                Room: ReadNullableString(reader, "room"),
                Comments: ReadNullableString(reader, "comments"),
                RecurrenceType: recurrenceType,
                RepeatFrequency: repeatFrequency,
                RepeatUnit: repeatUnit,
                RecurrenceEndDate: recurrenceEndDate,
                RecurrenceExdates: recurrenceExdates);
        }

        var rescheduledTitle = NormalizeText(request.Title) ?? source.Title ?? "Appointment";
        var rescheduledStatus = request.Status is null
            ? NormalizeText(source.Status) ?? "-"
            : NormalizeAppointmentStatus(request.Status);
        if (request.Status is not null)
        {
            EnsureValidAppointmentStatusTransition(source.Status, rescheduledStatus);
        }
        var rescheduledRoom = request.Room is null ? source.Room : NormalizeText(request.Room);
        var rescheduledComments = request.Comments is null ? source.Comments : NormalizeText(request.Comments);
        var effectiveProviderId = request.ProviderId ?? source.ProviderId;

        await EnsurePatientActiveForSchedulingAsync(connection, transaction, source.PatientId, cancellationToken, accessFacilityId);
        await ValidateActiveSchedulingReferencesAsync(
            connection,
            effectiveProviderId,
            accessFacilityId,
            request.BillingLocationId,
            cancellationToken);
        await EnsureProviderBelongsToFacilityAsync(
            connection,
            effectiveProviderId,
            accessFacilityId,
            cancellationToken);
        await AcquireSchedulingConflictLocksAsync(
            connection,
            transaction,
            rescheduledDate,
            source.Pid,
            effectiveProviderId,
            rescheduledRoom,
            cancellationToken);
        var availability = await ValidateAvailabilityAsync(
            new AppointmentAvailabilityValidationRequest(
                source.PatientId,
                effectiveProviderId,
                request.Date,
                request.StartTime,
                request.DurationMinutes,
                accessFacilityId,
                rescheduledRoom,
                rootAppointmentId),
            accessFacilityId,
            cancellationToken);
        if (availability is null)
        {
            return null;
        }
        if (!availability.Available)
        {
            throw new AppointmentAvailabilityConflictException(availability);
        }

        var updatedExdates = source.RecurrenceExdates
            .Concat(new[] { occurrenceDate.ToString("yyyy-MM-dd") })
            .ToList();
        var normalizedExdates = NormalizeRecurrenceExdates(updatedExdates);

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates,
                row_version = row_version + 1
            where id = @appointmentId;
            """;
        updateCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        updateCommand.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates)
            ? DBNull.Value
            : (object)normalizedExdates;

        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        var rescheduledAppointmentId = $"APPT-MODERN-OCCURRENCE-{Guid.NewGuid():N}";
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            insert into appointments (
                id,
                patient_id,
                pid,
                provider_id,
                facility_id,
                billing_location_id,
                appointment_date,
                start_time,
                duration_minutes,
                category_id,
                title,
                status,
                room,
                comments,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                repeat_on_num,
                repeat_on_day,
                repeat_on_frequency,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            )
            values (
                @id,
                @patientId,
                @pid,
                coalesce((select id from staff where id = @providerId), @sourceProviderId),
                coalesce((select id from facilities where id = @facilityId), @sourceFacilityId),
                coalesce((select id from facilities where id = @billingLocationId), @sourceBillingLocationId),
                @appointmentDate,
                @startTime,
                @durationMinutes,
                coalesce(@categoryId, @sourceCategoryId),
                @title,
                @status,
                @room,
                @comments,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            )
            returning id;
            """;
        insertCommand.Parameters.AddWithValue("id", rescheduledAppointmentId);
        insertCommand.Parameters.AddWithValue("patientId", source.PatientId);
        insertCommand.Parameters.AddWithValue("pid", source.Pid);
        insertCommand.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = effectiveProviderId is null ? DBNull.Value : effectiveProviderId.Value;
        insertCommand.Parameters.Add("sourceProviderId", NpgsqlDbType.Integer).Value = source.ProviderId is null ? DBNull.Value : source.ProviderId.Value;
        insertCommand.Parameters.AddWithValue("facilityId", accessFacilityId);
        insertCommand.Parameters.Add("sourceFacilityId", NpgsqlDbType.Integer).Value = source.FacilityId is null ? DBNull.Value : source.FacilityId.Value;
        insertCommand.Parameters.Add("billingLocationId", NpgsqlDbType.Integer).Value = request.BillingLocationId is null ? DBNull.Value : request.BillingLocationId.Value;
        insertCommand.Parameters.Add("sourceBillingLocationId", NpgsqlDbType.Integer).Value = source.BillingLocationId is null ? DBNull.Value : source.BillingLocationId.Value;
        insertCommand.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = rescheduledDate;
        insertCommand.Parameters.Add("startTime", NpgsqlDbType.Time).Value = rescheduledStartTime;
        insertCommand.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        insertCommand.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        insertCommand.Parameters.Add("sourceCategoryId", NpgsqlDbType.Integer).Value = source.CategoryId is null ? DBNull.Value : source.CategoryId.Value;
        insertCommand.Parameters.AddWithValue("title", rescheduledTitle);
        insertCommand.Parameters.AddWithValue("status", rescheduledStatus);
        insertCommand.Parameters.Add("room", NpgsqlDbType.Text).Value = rescheduledRoom is null ? DBNull.Value : rescheduledRoom;
        insertCommand.Parameters.Add("comments", NpgsqlDbType.Text).Value = rescheduledComments is null ? DBNull.Value : rescheduledComments;

        var insertedId = (string?)await insertCommand.ExecuteScalarAsync(cancellationToken);
        if (insertedId is null)
        {
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(insertedId, cancellationToken);
    }

    private async Task<bool> AddRecurrenceExceptionAsync(
        string rootAppointmentId,
        DateOnly occurrenceDate,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = """
            select row_version,
                appointment_date,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                repeat_on_num,
                repeat_on_day,
                repeat_on_frequency,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1
            for update;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        EnsureExpectedVersion(reader.GetInt32(reader.GetOrdinal("row_version")), expectedVersion);
        var appointmentDate = ReadDate(reader, "appointment_date");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var repeatOnNum = ReadNullableInt(reader, "repeat_on_num");
        var repeatOnDay = ReadNullableInt(reader, "repeat_on_day");
        var repeatOnFrequency = ReadNullableInt(reader, "repeat_on_frequency");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
        if (!IsValidOccurrenceDate(
                appointmentDate,
                recurrenceType,
                repeatFrequency,
                repeatUnit,
                repeatOnNum,
                repeatOnDay,
                repeatOnFrequency,
                recurrenceDays,
                recurrenceEndDate,
                recurrenceExdateSet,
                occurrenceDate))
        {
            return false;
        }

        var updatedExdates = recurrenceExdates
            .Concat(new[] { occurrenceDate.ToString("yyyy-MM-dd") })
            .ToList();
        var normalizedExdates = NormalizeRecurrenceExdates(updatedExdates);

        await reader.DisposeAsync();
        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates,
                row_version = row_version + 1
            where id = @appointmentId;
            """;
        updateCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        updateCommand.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates)
            ? DBNull.Value
            : (object)normalizedExdates;
        if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
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

    private async Task<AppointmentReminderDispatchResponse?> BuildReminderDispatchAsync(
        string appointmentId,
        DatasetMetadata metadata,
        string? requestedTemplateId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                a.id,
                p.canonical_id as patient_id,
                p.legacy_pid,
                p.pubpid,
                p.first_name,
                p.last_name,
                p.preferred_name,
                p.email,
                p.phone,
                p.phone_home,
                p.phone_cell,
                p.hipaa_allow_sms,
                p.hipaa_allow_email,
                a.appointment_date,
                a.start_time,
                a.duration_minutes,
                a.title,
                a.status
            from appointments a
            join patients p on p.legacy_pid = a.pid
            where a.id = @appointmentId
            limit 1;
            """;
        command.Parameters.AddWithValue("appointmentId", appointmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var appointmentDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("appointment_date"));
        var startTime = reader.GetFieldValue<TimeOnly>(reader.GetOrdinal("start_time"));
        var durationMinutes = reader.GetInt32(reader.GetOrdinal("duration_minutes"));
        var endTime = startTime.Add(TimeSpan.FromMinutes(durationMinutes));
        var reminder = BuildAppointmentReminder(
            appointmentDate.ToString("yyyy-MM-dd"),
            ReadNullableString(reader, "status"),
            ReadNullableString(reader, "email"),
            ReadNullableString(reader, "phone"),
            ReadNullableString(reader, "phone_home"),
            ReadNullableString(reader, "phone_cell"),
            ReadNullableString(reader, "hipaa_allow_sms"),
            ReadNullableString(reader, "hipaa_allow_email"),
            metadata.BaseDate);
        if (!reminder.Due)
        {
            return null;
        }

        var template = ResolveReminderTemplate(reminder.Channel, requestedTemplateId);
        if (template is null)
        {
            throw new ArgumentException($"Appointment reminder template '{requestedTemplateId}' is not valid for channel '{reminder.Channel}'.", nameof(requestedTemplateId));
        }

        var actualAppointmentId = reader.GetString(reader.GetOrdinal("id"));
        var compactDate = metadata.BaseDate.ToString("yyyyMMdd");
        var dispatchId = $"APPT-REMINDER-DISPATCH-{compactDate}-{SanitizeIdentifier(actualAppointmentId)}";
        var auditId = $"AUD-{dispatchId}";
        var channelCode = reminder.Channel.Replace(" + ", "-", StringComparison.Ordinal).ToUpperInvariant();
        var externalReference = $"LOCAL-{channelCode}-{SanitizeIdentifier(actualAppointmentId)}";
        var title = ReadNullableString(reader, "title") ?? "Appointment";
        var patientId = reader.GetString(reader.GetOrdinal("patient_id"));
        var firstName = ReadNullableString(reader, "first_name");
        var lastName = ReadNullableString(reader, "last_name");
        var preferredName = ReadNullableString(reader, "preferred_name");
        var patientDisplayName = !string.IsNullOrWhiteSpace(preferredName)
            ? $"{lastName}, {preferredName}".Trim(' ', ',')
            : NormalizePatientName(firstName, lastName, patientId);

        return new AppointmentReminderDispatchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            AppointmentId: actualAppointmentId,
            DispatchId: dispatchId,
            AuditId: auditId,
            DispatchedAt: $"{metadata.BaseDate:yyyy-MM-dd}T12:10:00Z",
            PatientId: patientId,
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: ReadNullableString(reader, "pubpid") ?? patientId,
            PatientDisplayName: patientDisplayName,
            AppointmentDate: appointmentDate.ToString("yyyy-MM-dd"),
            StartTime: startTime.ToString("HH:mm"),
            EndTime: endTime.ToString("HH:mm"),
            Title: title,
            ReminderStatus: reminder.Status,
            ReminderChannel: reminder.Channel,
            ReminderContact: reminder.Contact,
            ReminderLeadDays: reminder.LeadDays,
            QueueName: GetReminderQueueName(reminder.Channel),
            DispatchStatus: $"{reminder.Channel} queued",
            ExternalReference: externalReference,
            TemplateName: template.TemplateId,
            MessagePreview: BuildReminderMessagePreview(patientDisplayName, appointmentDate, startTime, title, reminder, template));
    }

    private static void AddReminderDispatchParameters(
        NpgsqlCommand command,
        AppointmentReminderDispatchResponse dispatch,
        DateOnly baseDate)
    {
        command.Parameters.AddWithValue("auditId", dispatch.AuditId);
        command.Parameters.AddWithValue("datasetId", dispatch.DatasetId);
        command.Parameters.AddWithValue("datasetVersion", dispatch.DatasetVersion);
        command.Parameters.Add("asOfDate", NpgsqlDbType.Date).Value = baseDate;
        command.Parameters.AddWithValue("appointmentId", dispatch.AppointmentId);
        command.Parameters.AddWithValue("dispatchId", dispatch.DispatchId);
        command.Parameters.Add("dispatchedAt", NpgsqlDbType.Timestamp).Value = DateTime.Parse(dispatch.DispatchedAt.Replace("Z", string.Empty, StringComparison.Ordinal));
        command.Parameters.AddWithValue("patientId", dispatch.PatientId);
        command.Parameters.AddWithValue("legacyPid", dispatch.LegacyPid);
        command.Parameters.AddWithValue("pubpid", dispatch.Pubpid);
        command.Parameters.AddWithValue("patientDisplayName", dispatch.PatientDisplayName);
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = DateOnly.Parse(dispatch.AppointmentDate);
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = TimeOnly.Parse(dispatch.StartTime);
        command.Parameters.Add("endTime", NpgsqlDbType.Time).Value = TimeOnly.Parse(dispatch.EndTime);
        command.Parameters.AddWithValue("title", dispatch.Title);
        command.Parameters.AddWithValue("reminderStatus", dispatch.ReminderStatus);
        command.Parameters.AddWithValue("reminderChannel", dispatch.ReminderChannel);
        command.Parameters.Add("reminderContact", NpgsqlDbType.Text).Value = dispatch.ReminderContact is null ? DBNull.Value : dispatch.ReminderContact;
        command.Parameters.Add("reminderLeadDays", NpgsqlDbType.Integer).Value = dispatch.ReminderLeadDays is null ? DBNull.Value : dispatch.ReminderLeadDays.Value;
        command.Parameters.AddWithValue("queueName", dispatch.QueueName);
        command.Parameters.AddWithValue("dispatchStatus", dispatch.DispatchStatus);
        command.Parameters.AddWithValue("externalReference", dispatch.ExternalReference);
        command.Parameters.AddWithValue("templateName", dispatch.TemplateName);
        command.Parameters.AddWithValue("messagePreview", dispatch.MessagePreview);
        command.Parameters.Add("retryOfDispatchId", NpgsqlDbType.Text).Value = dispatch.RetryOfDispatchId is null ? DBNull.Value : dispatch.RetryOfDispatchId;
        command.Parameters.AddWithValue("retryAttempt", dispatch.RetryAttempt);
        command.Parameters.Add("createdAt", NpgsqlDbType.Timestamp).Value = DateTime.Parse(dispatch.DispatchedAt.Replace("Z", string.Empty, StringComparison.Ordinal));
    }

    private const string AppointmentSearchPredicate = """
        p.facility_id = @facilityId
        and (@patientId is null
         or lower(p.canonical_id) = @patientId
         or lower(p.pubpid) = @patientId
         or p.legacy_pid::text = @patientId)
        and (
            a.appointment_date >= @fromDate
            or (
                a.recurrence_type > 0
                and a.recurrence_end_date is not null
                and a.recurrence_end_date >= @fromDate
            )
        )
        and (@toDate is null or a.appointment_date <= @toDate)
        """;

    private static void AddSearchParameters(
        NpgsqlCommand command,
        string? patientId,
        DateOnly fromDate,
        DateOnly? toDate,
        int facilityId)
    {
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId is null ? DBNull.Value : patientId;
        command.Parameters.Add("fromDate", NpgsqlDbType.Date).Value = fromDate;
        command.Parameters.Add("toDate", NpgsqlDbType.Date).Value = toDate is null ? DBNull.Value : toDate.Value;
        command.Parameters.AddWithValue("facilityId", facilityId);
    }

    private static void EnsureFacilityId(int facilityId)
    {
        if (facilityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(facilityId));
        }
    }

    private static void EnsureRequestedFacilityMatchesAccessContext(
        int? requestedFacilityId,
        int accessFacilityId)
    {
        if (requestedFacilityId is not null && requestedFacilityId != accessFacilityId)
        {
            throw new ArgumentException("Appointments may be scheduled only for the selected facility.");
        }
    }

    private static AppointmentListItem ReadListItem(DbDataReader reader, DateOnly baseDate)
    {
        var categoryId = ReadNullableInt(reader, "category_id");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var repeatOnNum = ReadNullableInt(reader, "repeat_on_num");
        var repeatOnDay = ReadNullableInt(reader, "repeat_on_day");
        var repeatOnFrequency = ReadNullableInt(reader, "repeat_on_frequency");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var appointmentDate = ReadDate(reader, "appointment_date");
        var status = ReadNullableString(reader, "status");
        var reminder = BuildAppointmentReminder(
            appointmentDate,
            status,
            ReadNullableString(reader, "email"),
            ReadNullableString(reader, "phone"),
            ReadNullableString(reader, "phone_home"),
            ReadNullableString(reader, "phone_cell"),
            ReadNullableString(reader, "hipaa_allow_sms"),
            ReadNullableString(reader, "hipaa_allow_email"),
            baseDate);

        return new AppointmentListItem(
            Id: reader.GetString(reader.GetOrdinal("id")),
            RowVersion: reader.GetInt32(reader.GetOrdinal("row_version")),
            SeriesRootId: reader.GetString(reader.GetOrdinal("id")),
            IsRecurringSeries: recurrenceType > 0,
            IsVirtualOccurrence: false,
            OccurrenceNumber: recurrenceType > 0 ? 1 : null,
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            PatientDisplayName: BuildDisplayName(reader),
            Date: appointmentDate,
            StartTime: ReadTime(reader, "start_time"),
            DurationMinutes: reader.GetInt32(reader.GetOrdinal("duration_minutes")),
            Title: ReadNullableString(reader, "title") ?? "Appointment",
            Status: status,
            Room: ReadNullableString(reader, "room"),
            CategoryId: categoryId,
            CategoryName: GetAppointmentCategoryName(categoryId),
            ProviderId: ReadNullableInt(reader, "provider_id"),
            ProviderName: ReadNullableString(reader, "provider_name"),
            FacilityId: ReadNullableInt(reader, "facility_id"),
            FacilityName: ReadNullableString(reader, "facility_name"),
            BillingLocationId: ReadNullableInt(reader, "billing_location_id"),
            BillingLocationName: ReadNullableString(reader, "billing_location_name"),
            Comments: ReadNullableString(reader, "comments"),
            RecurrenceType: recurrenceType,
            RepeatFrequency: repeatFrequency,
            RepeatUnit: repeatUnit,
            RepeatOnNum: repeatOnNum,
            RepeatOnDay: repeatOnDay,
            RepeatOnFrequency: repeatOnFrequency,
            RecurrenceDays: recurrenceDays,
            RecurrenceEndDate: recurrenceEndDate,
            RecurrenceExdates: recurrenceExdates,
            RecurrenceExceptionCount: recurrenceExdates.Count,
            RecurrenceLabel: BuildRecurrenceLabel(recurrenceType, repeatFrequency, repeatUnit, repeatOnNum, repeatOnDay, repeatOnFrequency, recurrenceDays, recurrenceEndDate),
            ProviderOverlapCount: 0,
            ProviderOverlapAppointmentIds: Array.Empty<string>(),
            PatientOverlapCount: 0,
            PatientOverlapAppointmentIds: Array.Empty<string>(),
            RoomOverlapCount: 0,
            RoomOverlapAppointmentIds: Array.Empty<string>(),
            ReminderDue: reminder.Due,
            ReminderStatus: reminder.Status,
            ReminderChannel: reminder.Channel,
            ReminderContact: reminder.Contact,
            ReminderLeadDays: reminder.LeadDays,
            ConvertedEncounterId: ReadNullableInt(reader, "converted_encounter_id"),
            ConvertedEncounterDate: ReadNullableDate(reader, "converted_encounter_date"),
            ConvertedBillingLineCount: reader.GetInt32(reader.GetOrdinal("converted_billing_line_count")));
    }

    private static IEnumerable<AppointmentListItem> ExpandAppointmentListItem(
        AppointmentListItem appointment,
        DateOnly fromDate,
        DateOnly? toDate,
        DateOnly baseDate)
    {
        if (!DateOnly.TryParse(appointment.Date, out var anchorDate))
        {
            yield return appointment;
            yield break;
        }

        if (appointment.RecurrenceType <= 0)
        {
            if (IsWithinSearchRange(anchorDate, fromDate, toDate))
            {
                yield return appointment;
            }

            yield break;
        }

        var repeatFrequency = Math.Max(1, appointment.RepeatFrequency.GetValueOrDefault(1));
        var recurrenceExdates = ParseRecurrenceExdateSet(appointment.RecurrenceExdates);
        var recurrenceEndDate = DateOnly.TryParse(appointment.RecurrenceEndDate, out var parsedEndDate)
            ? parsedEndDate
            : anchorDate;

        if (appointment.RecurrenceType == RepeatOnRecurrenceType)
        {
            var repeatOnNum = appointment.RepeatOnNum.GetValueOrDefault();
            var repeatOnDay = appointment.RepeatOnDay.GetValueOrDefault(-1);
            var repeatOnFrequency = Math.Max(1, appointment.RepeatOnFrequency.GetValueOrDefault(1));
            if (repeatOnNum is < 1 or > 5 || repeatOnDay is < 0 or > 6)
            {
                yield break;
            }

            var anchorMonth = new DateOnly(anchorDate.Year, anchorDate.Month, 1);
            var occurrenceNumber = 0;
            for (var monthOffset = 0; occurrenceNumber < MaximumExpandedOccurrencesPerAppointment; monthOffset += repeatOnFrequency)
            {
                var occurrenceMonth = anchorMonth.AddMonths(monthOffset);
                if (occurrenceMonth > recurrenceEndDate)
                {
                    yield break;
                }

                var repeatOnDate = GetRepeatOnOccurrenceDate(occurrenceMonth.Year, occurrenceMonth.Month, repeatOnNum, repeatOnDay);
                if (repeatOnDate is null)
                {
                    continue;
                }

                if (repeatOnDate.Value < anchorDate)
                {
                    continue;
                }

                if (toDate is not null && repeatOnDate.Value > toDate.Value)
                {
                    yield break;
                }

                occurrenceNumber++;
                if (repeatOnDate.Value > recurrenceEndDate)
                {
                    yield break;
                }

                if (IsWithinSearchRange(repeatOnDate.Value, fromDate, toDate)
                    && !recurrenceExdates.Contains(repeatOnDate.Value))
                {
                    var isVirtualOccurrence = repeatOnDate.Value != anchorDate;
                    yield return WithReminder(appointment with
                    {
                        Id = isVirtualOccurrence ? BuildOccurrenceId(appointment.SeriesRootId, repeatOnDate.Value) : appointment.SeriesRootId,
                        Date = repeatOnDate.Value.ToString("yyyy-MM-dd"),
                        IsRecurringSeries = true,
                        IsVirtualOccurrence = isVirtualOccurrence,
                        OccurrenceNumber = occurrenceNumber
                    }, baseDate);
                }
            }

            yield break;
        }

        if (appointment.RecurrenceType == SpecificWeekdaysRecurrenceType)
        {
            var selectedDays = ParseRecurrenceDaySet(appointment.RecurrenceDays);
            if (selectedDays.Count == 0)
            {
                yield break;
            }

            var currentDate = anchorDate;
            var occurrenceNumber = 0;
            while (currentDate <= recurrenceEndDate
                   && (toDate is null || currentDate <= toDate.Value)
                   && occurrenceNumber < MaximumExpandedOccurrencesPerAppointment)
            {
                if (selectedDays.Contains(GetAvenChartWeekday(currentDate)))
                {
                    occurrenceNumber++;
                    if (IsWithinSearchRange(currentDate, fromDate, toDate)
                        && !recurrenceExdates.Contains(currentDate))
                    {
                        var isVirtualOccurrence = currentDate != anchorDate;
                        yield return WithReminder(appointment with
                        {
                            Id = isVirtualOccurrence ? BuildOccurrenceId(appointment.SeriesRootId, currentDate) : appointment.SeriesRootId,
                            Date = currentDate.ToString("yyyy-MM-dd"),
                            IsRecurringSeries = true,
                            IsVirtualOccurrence = isVirtualOccurrence,
                            OccurrenceNumber = occurrenceNumber
                        }, baseDate);
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            yield break;
        }

        var occurrenceDate = anchorDate;
        for (var occurrenceNumber = 1;
             occurrenceDate <= recurrenceEndDate
             && (toDate is null || occurrenceDate <= toDate.Value)
             && occurrenceNumber <= MaximumExpandedOccurrencesPerAppointment;
             occurrenceNumber++)
        {
            if (IsWithinSearchRange(occurrenceDate, fromDate, toDate)
                && !recurrenceExdates.Contains(occurrenceDate))
            {
                var isVirtualOccurrence = occurrenceDate != anchorDate;
                yield return WithReminder(appointment with
                {
                    Id = isVirtualOccurrence ? BuildOccurrenceId(appointment.SeriesRootId, occurrenceDate) : appointment.SeriesRootId,
                    Date = occurrenceDate.ToString("yyyy-MM-dd"),
                    IsRecurringSeries = true,
                    IsVirtualOccurrence = isVirtualOccurrence,
                    OccurrenceNumber = occurrenceNumber
                }, baseDate);
            }

            var nextOccurrenceDate = GetNextOccurrenceDate(occurrenceDate, repeatFrequency, appointment.RepeatUnit);
            if (nextOccurrenceDate <= occurrenceDate)
            {
                yield break;
            }

            occurrenceDate = nextOccurrenceDate;
        }
    }

    private static AppointmentListItem WithReminder(AppointmentListItem appointment, DateOnly baseDate)
    {
        var reminder = BuildAppointmentReminderForExistingChannel(appointment, baseDate);
        return appointment with
        {
            ReminderDue = reminder.Due,
            ReminderStatus = reminder.Status,
            ReminderChannel = reminder.Channel,
            ReminderContact = reminder.Contact,
            ReminderLeadDays = reminder.LeadDays
        };
    }

    private static AppointmentReminder BuildAppointmentReminderForExistingChannel(
        AppointmentListItem appointment,
        DateOnly baseDate)
    {
        if (!DateOnly.TryParse(appointment.Date, out var appointmentDate))
        {
            return new AppointmentReminder(false, "No reminder - date unavailable", "None", null, null);
        }

        if (!IsActiveAppointmentStatus(appointment.Status))
        {
            return new AppointmentReminder(false, "No reminder - inactive appointment", "None", null, null);
        }

        var leadDays = appointmentDate.DayNumber - baseDate.DayNumber;
        if (leadDays <= 0)
        {
            return new AppointmentReminder(
                false,
                "No reminder - appointment is not future",
                appointment.ReminderChannel,
                appointment.ReminderContact,
                leadDays);
        }

        if (leadDays > ReminderWindowDays)
        {
            return new AppointmentReminder(
                false,
                $"Not due - {leadDays} days out",
                appointment.ReminderChannel,
                appointment.ReminderContact,
                leadDays);
        }

        return new AppointmentReminder(true, "Due now", appointment.ReminderChannel, appointment.ReminderContact, leadDays);
    }

    private static AppointmentReminder BuildAppointmentReminder(
        string appointmentDateText,
        string? status,
        string? email,
        string? phone,
        string? phoneHome,
        string? phoneCell,
        string? hipaaAllowSms,
        string? hipaaAllowEmail,
        DateOnly baseDate)
    {
        if (!DateOnly.TryParse(appointmentDateText, out var appointmentDate))
        {
            return new AppointmentReminder(false, "No reminder - date unavailable", "None", null, null);
        }

        if (!IsActiveAppointmentStatus(status))
        {
            return new AppointmentReminder(false, "No reminder - inactive appointment", "None", null, null);
        }

        var leadDays = appointmentDate.DayNumber - baseDate.DayNumber;
        if (leadDays <= 0)
        {
            return new AppointmentReminder(false, "No reminder - appointment is not future", "None", null, leadDays);
        }

        var smsContact = AllowsContact(hipaaAllowSms)
            ? FirstNonEmpty(phoneCell, phone, phoneHome)
            : null;
        var emailContact = AllowsContact(hipaaAllowEmail)
            ? NormalizeText(email)
            : null;
        var phoneContact = FirstNonEmpty(phoneHome, phone, phoneCell);
        var channel = GetReminderChannel(smsContact, emailContact, phoneContact);
        var contact = GetReminderContact(smsContact, emailContact, phoneContact);

        if (leadDays > ReminderWindowDays)
        {
            return new AppointmentReminder(false, $"Not due - {leadDays} days out", channel, contact, leadDays);
        }

        return new AppointmentReminder(true, "Due now", channel, contact, leadDays);
    }

    private static string GetReminderChannel(string? smsContact, string? emailContact, string? phoneContact)
    {
        if (smsContact is not null && emailContact is not null)
        {
            return "SMS + Email";
        }

        if (smsContact is not null)
        {
            return "SMS";
        }

        if (emailContact is not null)
        {
            return "Email";
        }

        return phoneContact is not null ? "Phone" : "Print";
    }

    private static string? GetReminderContact(string? smsContact, string? emailContact, string? phoneContact)
    {
        if (smsContact is not null && emailContact is not null)
        {
            return $"{smsContact} / {emailContact}";
        }

        return smsContact ?? emailContact ?? phoneContact;
    }

    private static string GetReminderQueueName(string channel) => channel switch
    {
        "SMS + Email" => "appointment-reminder-sms-email",
        "SMS" => "appointment-reminder-sms",
        "Email" => "appointment-reminder-email",
        "Phone" => "appointment-reminder-phone",
        _ => "appointment-reminder-print"
    };

    private static readonly IReadOnlyList<AppointmentReminderTemplateOption> ReminderTemplateOptions =
    [
        new(
            TemplateId: "appointment-reminder-sms-email-v1",
            Name: "Standard SMS and email reminder",
            Channel: "SMS + Email",
            QueueName: "appointment-reminder-sms-email",
            Description: "Default combined SMS and email appointment reminder.",
            IsDefault: true),
        new(
            TemplateId: "appointment-reminder-sms-email-arrival-v1",
            Name: "Arrival instructions SMS and email reminder",
            Channel: "SMS + Email",
            QueueName: "appointment-reminder-sms-email",
            Description: "Combined SMS and email reminder with arrival and check-in instructions.",
            IsDefault: false),
        new(
            TemplateId: "appointment-reminder-sms-v1",
            Name: "Standard SMS reminder",
            Channel: "SMS",
            QueueName: "appointment-reminder-sms",
            Description: "Default SMS appointment reminder.",
            IsDefault: true),
        new(
            TemplateId: "appointment-reminder-email-v1",
            Name: "Standard email reminder",
            Channel: "Email",
            QueueName: "appointment-reminder-email",
            Description: "Default email appointment reminder.",
            IsDefault: true),
        new(
            TemplateId: "appointment-reminder-email-arrival-v1",
            Name: "Arrival instructions email reminder",
            Channel: "Email",
            QueueName: "appointment-reminder-email",
            Description: "Email reminder with arrival and check-in instructions.",
            IsDefault: false),
        new(
            TemplateId: "appointment-reminder-phone-v1",
            Name: "Phone call reminder",
            Channel: "Phone",
            QueueName: "appointment-reminder-phone",
            Description: "Default phone-call reminder work item.",
            IsDefault: true),
        new(
            TemplateId: "appointment-reminder-print-v1",
            Name: "Printed reminder",
            Channel: "Print",
            QueueName: "appointment-reminder-print",
            Description: "Default printed reminder work item.",
            IsDefault: true)
    ];

    private static AppointmentReminderTemplateOption? ResolveReminderTemplate(string channel, string? requestedTemplateId)
    {
        var normalizedTemplateId = NormalizeText(requestedTemplateId);
        if (normalizedTemplateId is null)
        {
            return ReminderTemplateOptions.First(template =>
                template.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase) && template.IsDefault);
        }

        return ReminderTemplateOptions.FirstOrDefault(template =>
            template.TemplateId.Equals(normalizedTemplateId, StringComparison.OrdinalIgnoreCase)
            && template.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildReminderMessagePreview(
        string patientDisplayName,
        DateOnly appointmentDate,
        TimeOnly startTime,
        string title,
        AppointmentReminder reminder,
        AppointmentReminderTemplateOption template)
    {
        var contact = reminder.Contact is null ? "no direct contact" : reminder.Contact;
        var preview = $"Reminder for {patientDisplayName}: {title} on {appointmentDate:yyyy-MM-dd} at {startTime:HH:mm}. Channel {reminder.Channel}; contact {contact}.";
        return template.TemplateId.Contains("arrival", StringComparison.OrdinalIgnoreCase)
            ? $"{preview} Please arrive 15 minutes early and bring insurance plus photo ID."
            : preview;
    }

    private static bool AllowsContact(string? value) =>
        string.Equals(NormalizeText(value), "YES", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(NormalizeText).FirstOrDefault(value => value is not null);

    private static IReadOnlyList<AppointmentListItem> AnnotateAppointmentOverlaps(IReadOnlyList<AppointmentListItem> appointments)
    {
        return appointments
            .Select(appointment =>
            {
                var providerOverlapIds = GetProviderOverlapIds(appointment, appointments);
                var patientOverlapIds = GetPatientOverlapIds(appointment, appointments);
                var roomOverlapIds = GetRoomOverlapIds(appointment, appointments);
                return appointment with
                {
                    ProviderOverlapCount = providerOverlapIds.Count,
                    ProviderOverlapAppointmentIds = providerOverlapIds,
                    PatientOverlapCount = patientOverlapIds.Count,
                    PatientOverlapAppointmentIds = patientOverlapIds,
                    RoomOverlapCount = roomOverlapIds.Count,
                    RoomOverlapAppointmentIds = roomOverlapIds
                };
            })
            .ToList();
    }

    private static IReadOnlyList<string> GetProviderOverlapIds(
        AppointmentListItem appointment,
        IReadOnlyList<AppointmentListItem> appointments)
    {
        if (appointment.ProviderId is null
            || !DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return Array.Empty<string>();
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        return appointments
            .Where(candidate => candidate.Id != appointment.Id
                && candidate.ProviderId == appointment.ProviderId
                && IsActiveAppointmentStatus(candidate.Status)
                && DateOnly.TryParse(candidate.Date, out var candidateDate)
                && candidateDate == appointmentDate
                && TimeOnly.TryParse(candidate.StartTime, out var candidateStart)
                && candidate.DurationMinutes > 0
                && TimeWindowsOverlap(
                    appointmentStart,
                    appointmentEnd,
                    candidateStart,
                    candidateStart.AddMinutes(candidate.DurationMinutes)))
            .OrderBy(candidate => candidate.StartTime)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => candidate.Id)
            .ToList();
    }

    private static IReadOnlyList<string> GetPatientOverlapIds(
        AppointmentListItem appointment,
        IReadOnlyList<AppointmentListItem> appointments)
    {
        if (!DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return Array.Empty<string>();
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        return appointments
            .Where(candidate => candidate.Id != appointment.Id
                && candidate.LegacyPid == appointment.LegacyPid
                && IsActiveAppointmentStatus(candidate.Status)
                && DateOnly.TryParse(candidate.Date, out var candidateDate)
                && candidateDate == appointmentDate
                && TimeOnly.TryParse(candidate.StartTime, out var candidateStart)
                && candidate.DurationMinutes > 0
                && TimeWindowsOverlap(
                    appointmentStart,
                    appointmentEnd,
                    candidateStart,
                    candidateStart.AddMinutes(candidate.DurationMinutes)))
            .OrderBy(candidate => candidate.StartTime)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => candidate.Id)
            .ToList();
    }

    private static IReadOnlyList<string> GetRoomOverlapIds(
        AppointmentListItem appointment,
        IReadOnlyList<AppointmentListItem> appointments)
    {
        var room = NormalizeText(appointment.Room);
        if (room is null
            || !DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return Array.Empty<string>();
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        return appointments
            .Where(candidate => candidate.Id != appointment.Id
                && string.Equals(NormalizeText(candidate.Room), room, StringComparison.OrdinalIgnoreCase)
                && IsActiveAppointmentStatus(candidate.Status)
                && DateOnly.TryParse(candidate.Date, out var candidateDate)
                && candidateDate == appointmentDate
                && TimeOnly.TryParse(candidate.StartTime, out var candidateStart)
                && candidate.DurationMinutes > 0
                && TimeWindowsOverlap(
                    appointmentStart,
                    appointmentEnd,
                    candidateStart,
                    candidateStart.AddMinutes(candidate.DurationMinutes)))
            .OrderBy(candidate => candidate.StartTime)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => candidate.Id)
            .ToList();
    }

    private async Task<ProviderOverlapSummary> GetProviderOverlapSummaryAsync(
        AppointmentDetail appointment,
        CancellationToken cancellationToken)
    {
        if (appointment.ProviderId is null
            || !DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return new ProviderOverlapSummary(0, Array.Empty<string>());
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id
            from appointments
            where provider_id = @providerId
              and appointment_date = @appointmentDate
              and id <> @appointmentId
              and coalesce(status, '-') <> 'x'
              and start_time < @appointmentEnd
              and (start_time + make_interval(mins => duration_minutes))::time > @appointmentStart
            order by start_time, id;
            """;
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = appointment.ProviderId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.AddWithValue("appointmentId", appointment.SeriesRootId);
        command.Parameters.Add("appointmentStart", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.Add("appointmentEnd", NpgsqlDbType.Time).Value = appointmentEnd;

        var overlapIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overlapIds.Add(reader.GetString(reader.GetOrdinal("id")));
        }

        return new ProviderOverlapSummary(overlapIds.Count, overlapIds);
    }

    private static async Task AcquireSchedulingConflictLocksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateOnly appointmentDate,
        int patientPid,
        int? providerId,
        string? room,
        CancellationToken cancellationToken)
    {
        var lockKeys = new List<string>
        {
            $"appointment-conflict|{appointmentDate:yyyy-MM-dd}|patient|{patientPid}"
        };
        if (providerId is not null)
        {
            lockKeys.Add($"appointment-conflict|{appointmentDate:yyyy-MM-dd}|provider|{providerId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(room))
        {
            lockKeys.Add($"appointment-conflict|{appointmentDate:yyyy-MM-dd}|room|{room.Trim().ToLowerInvariant()}");
        }

        foreach (var lockKey in lockKeys.Order(StringComparer.Ordinal))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "select pg_advisory_xact_lock(hashtextextended(@lockKey, 0));";
            command.Parameters.AddWithValue("lockKey", lockKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ValidateActiveSchedulingReferencesAsync(
        NpgsqlConnection connection,
        int? providerId,
        int? facilityId,
        int? billingLocationId,
        CancellationToken cancellationToken)
    {
        await ValidateActiveSchedulingReferenceAsync(
            connection,
            providerId,
            "provider",
            "staff",
            "active = true",
            cancellationToken);
        await ValidateActiveSchedulingReferenceAsync(
            connection,
            facilityId,
            "facility",
            "facilities",
            "inactive = false",
            cancellationToken);
        await ValidateActiveSchedulingReferenceAsync(
            connection,
            billingLocationId,
            "billing location",
            "facilities",
            "inactive = false",
            cancellationToken);
    }

    private static async Task EnsureProviderBelongsToFacilityAsync(
        NpgsqlConnection connection,
        int? providerId,
        int facilityId,
        CancellationToken cancellationToken)
    {
        if (providerId is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists(
                select 1
                from staff
                where id = @providerId
                  and facility_id = @facilityId
                  and active = true);
            """;
        command.Parameters.AddWithValue("providerId", providerId.Value);
        command.Parameters.AddWithValue("facilityId", facilityId);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new ArgumentException("The selected provider is not active at the selected facility.");
        }
    }

    private static async Task<SchedulingPatient> EnsurePatientActiveForSchedulingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string patientId,
        CancellationToken cancellationToken,
        int? accessFacilityId = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid, provider_id, facility_id, lifecycle_status, deceased_date, merged_into_patient_id
            from patients
            where (@accessFacilityId is null or facility_id = @accessFacilityId)
              and (lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId)
            limit 1
            for update;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        command.Parameters.Add("accessFacilityId", NpgsqlDbType.Integer).Value = accessFacilityId is null ? DBNull.Value : accessFacilityId.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new ArgumentException("The patient was not found.");
        }

        var status = reader.GetString(reader.GetOrdinal("lifecycle_status"));
        var mergedIntoPatientId = ReadNullableString(reader, "merged_into_patient_id");

        if (mergedIntoPatientId is not null)
        {
            throw new ArgumentException("A merged patient cannot receive a new or rescheduled appointment. Use the surviving patient chart.");
        }

        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A retired patient cannot receive a new or rescheduled appointment until reactivated.");
        }

        if (!reader.IsDBNull(reader.GetOrdinal("deceased_date")))
        {
            throw new ArgumentException("A deceased patient cannot receive a new or rescheduled appointment.");
        }

        return new SchedulingPatient(
            CanonicalId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            ProviderId: ReadNullableInt(reader, "provider_id"),
            FacilityId: ReadNullableInt(reader, "facility_id"));
    }

    private static async Task ValidateActiveSchedulingReferenceAsync(
        NpgsqlConnection connection,
        int? referenceId,
        string referenceName,
        string tableName,
        string activePredicate,
        CancellationToken cancellationToken)
    {
        if (referenceId is null)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"select exists(select 1 from {tableName} where id = @referenceId and {activePredicate});";
        command.Parameters.AddWithValue("referenceId", referenceId.Value);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new ArgumentException($"Selected {referenceName} is inactive or was not found.");
        }
    }

    private static async Task<IReadOnlyList<AppointmentAvailabilityConflict>> GetAvailabilityConflictsAsync(
        NpgsqlConnection connection,
        DateOnly appointmentDate,
        TimeOnly appointmentStart,
        TimeOnly appointmentEnd,
        int patientPid,
        int? providerId,
        string? room,
        string? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                appointments.id,
                appointments.patient_id,
                trim(concat(coalesce(patients.last_name, ''), ', ', coalesce(patients.first_name, ''))) as patient_display_name,
                appointments.appointment_date,
                appointments.start_time,
                (appointments.start_time + make_interval(mins => appointments.duration_minutes))::time as end_time,
                appointments.title,
                array_remove(array[
                    case when @providerId is not null and appointments.provider_id = @providerId then 'provider' end,
                    case when appointments.pid = @patientPid then 'patient' end,
                    case when @room is not null and lower(trim(coalesce(appointments.room, ''))) = lower(@room) then 'room' end
                ], null) as conflict_types
            from appointments
            join patients on patients.legacy_pid = appointments.pid
            where appointments.appointment_date = @appointmentDate
              and coalesce(appointments.status, '-') <> 'x'
              and (@excludeAppointmentId is null or appointments.id <> @excludeAppointmentId)
              and appointments.start_time < @appointmentEnd
              and (appointments.start_time + make_interval(mins => appointments.duration_minutes))::time > @appointmentStart
              and (
                (@providerId is not null and appointments.provider_id = @providerId)
                or appointments.pid = @patientPid
                or (@room is not null and lower(trim(coalesce(appointments.room, ''))) = lower(@room))
              )
            order by appointments.start_time, appointments.id;
            """;
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = providerId is null ? DBNull.Value : providerId.Value;
        command.Parameters.Add("patientPid", NpgsqlDbType.Integer).Value = patientPid;
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = room ?? (object)DBNull.Value;
        command.Parameters.Add("excludeAppointmentId", NpgsqlDbType.Text).Value = excludeAppointmentId ?? (object)DBNull.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("appointmentStart", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.Add("appointmentEnd", NpgsqlDbType.Time).Value = appointmentEnd;

        var conflicts = new List<AppointmentAvailabilityConflict>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var conflictTypes = reader.GetFieldValue<string[]>(reader.GetOrdinal("conflict_types"));
            foreach (var conflictType in conflictTypes)
            {
                conflicts.Add(new AppointmentAvailabilityConflict(
                    AppointmentId: reader.GetString(reader.GetOrdinal("id")),
                    ConflictType: conflictType,
                    PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
                    PatientDisplayName: NormalizeText(ReadNullableString(reader, "patient_display_name")) ?? "Unknown patient",
                    Date: ReadDate(reader, "appointment_date"),
                    StartTime: ReadTime(reader, "start_time"),
                    EndTime: ReadTime(reader, "end_time"),
                    Title: reader.GetString(reader.GetOrdinal("title"))));
            }
        }

        return conflicts;
    }

    private async Task<AppointmentOverlapSummary> GetPatientOverlapSummaryAsync(
        AppointmentDetail appointment,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return new AppointmentOverlapSummary(0, Array.Empty<string>());
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id
            from appointments
            where pid = @patientPid
              and appointment_date = @appointmentDate
              and id <> @appointmentId
              and coalesce(status, '-') <> 'x'
              and start_time < @appointmentEnd
              and (start_time + make_interval(mins => duration_minutes))::time > @appointmentStart
            order by start_time, id;
            """;
        command.Parameters.Add("patientPid", NpgsqlDbType.Integer).Value = appointment.LegacyPid;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.AddWithValue("appointmentId", appointment.SeriesRootId);
        command.Parameters.Add("appointmentStart", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.Add("appointmentEnd", NpgsqlDbType.Time).Value = appointmentEnd;

        var overlapIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overlapIds.Add(reader.GetString(reader.GetOrdinal("id")));
        }

        return new AppointmentOverlapSummary(overlapIds.Count, overlapIds);
    }

    private async Task<AppointmentOverlapSummary> GetRoomOverlapSummaryAsync(
        AppointmentDetail appointment,
        CancellationToken cancellationToken)
    {
        var room = NormalizeText(appointment.Room);
        if (room is null
            || !DateOnly.TryParse(appointment.Date, out var appointmentDate)
            || !TimeOnly.TryParse(appointment.StartTime, out var appointmentStart)
            || appointment.DurationMinutes <= 0
            || !IsActiveAppointmentStatus(appointment.Status))
        {
            return new AppointmentOverlapSummary(0, Array.Empty<string>());
        }

        var appointmentEnd = appointmentStart.AddMinutes(appointment.DurationMinutes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id
            from appointments
            where lower(trim(room)) = lower(@room)
              and appointment_date = @appointmentDate
              and id <> @appointmentId
              and coalesce(status, '-') <> 'x'
              and start_time < @appointmentEnd
              and (start_time + make_interval(mins => duration_minutes))::time > @appointmentStart
            order by start_time, id;
            """;
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = room;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.AddWithValue("appointmentId", appointment.SeriesRootId);
        command.Parameters.Add("appointmentStart", NpgsqlDbType.Time).Value = appointmentStart;
        command.Parameters.Add("appointmentEnd", NpgsqlDbType.Time).Value = appointmentEnd;

        var overlapIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            overlapIds.Add(reader.GetString(reader.GetOrdinal("id")));
        }

        return new AppointmentOverlapSummary(overlapIds.Count, overlapIds);
    }

    private static bool TimeWindowsOverlap(
        TimeOnly firstStart,
        TimeOnly firstEnd,
        TimeOnly secondStart,
        TimeOnly secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    private static bool IsActiveAppointmentStatus(string? status) =>
        !string.Equals(status, "x", StringComparison.OrdinalIgnoreCase);

    private static async Task<AppointmentMutationState?> GetMutationStateForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string appointmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select coalesce(status, '-') as status, row_version, patient_id
            from appointments
            where id = @appointmentId
            for update;
            """;
        command.Parameters.AddWithValue("appointmentId", appointmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AppointmentMutationState(
                reader.GetString(reader.GetOrdinal("status")),
                reader.GetInt32(reader.GetOrdinal("row_version")),
                reader.GetString(reader.GetOrdinal("patient_id")))
            : null;
    }

    private static void EnsureExpectedVersion(int currentVersion, int expectedVersion)
    {
        if (expectedVersion <= 0 || expectedVersion != currentVersion)
        {
            throw new AppointmentConcurrencyException();
        }
    }

    private static void EnsureSchedulingFactsRemainMutable(string? status)
    {
        var normalized = NormalizeAppointmentStatus(status);
        if (normalized is "<" or "?" or "x")
        {
            throw new AppointmentMutationNotAllowedException(
                "Completed, no-show, and cancelled appointments retain their scheduling facts. Restore a cancelled or no-show appointment before editing it.");
        }
    }

    private static string NormalizeAppointmentStatus(string? value)
    {
        var normalized = NormalizeText(value)?.ToLowerInvariant();
        return normalized switch
        {
            null or "" or "-" or "scheduled" or "booked" or "confirmed" => "-",
            "~" or "pending" or "requested" or "request" => "~",
            "!" or "follow-up needed" or "left-message" or "left message" => "!",
            "@" or "arrived" or "checked-in" or "checked in" => "@",
            ">" or "in-room" or "in room" or "roomed" => ">",
            "<" or "checked-out" or "checked out" or "complete" or "completed" => "<",
            "?" or "no-show" or "no show" or "noshow" => "?",
            "x" or "cancelled" or "canceled" or "cancel" or "cancelled by patient" or "canceled by patient" => "x",
            _ => throw new ArgumentException("Select a supported appointment status.")
        };
    }

    private static void EnsureValidAppointmentStatusTransition(string? currentValue, string next)
    {
        string? current = null;
        try { current = NormalizeAppointmentStatus(currentValue); }
        catch (ArgumentException) { /* Unknown historical status may be corrected to any supported state. */ }
        if (current is null || current == next)
        {
            return;
        }

        var allowed = current switch
        {
            "-" => new[] { "~", "@", "?", "x" },
            "~" or "!" => new[] { "-", "@", "?", "x" },
            "@" => new[] { ">", "?", "x" },
            ">" => new[] { "<", "x" },
            "<" => Array.Empty<string>(),
            "?" or "x" => new[] { "-" },
            _ => Array.Empty<string>()
        };
        if (!allowed.Contains(next, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Appointment status {current} cannot move directly to {next}.");
        }
    }

    private static AppointmentOccurrenceReference ParseOccurrenceReference(string appointmentId)
    {
        // Browser clients encode the composite virtual-occurrence identifier when
        // placing it in a path segment.  Normalize it before recognising the
        // separator so an encoded occurrence cannot be mistaken for a root ID.
        var normalizedAppointmentId = Uri.UnescapeDataString(appointmentId);
        var separatorIndex = normalizedAppointmentId.IndexOf(VirtualOccurrenceSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return new AppointmentOccurrenceReference(normalizedAppointmentId, null);
        }

        var rootAppointmentId = normalizedAppointmentId[..separatorIndex];
        var occurrenceDateText = normalizedAppointmentId[(separatorIndex + VirtualOccurrenceSeparator.Length)..];
        return DateOnly.TryParse(occurrenceDateText, out var occurrenceDate)
            ? new AppointmentOccurrenceReference(rootAppointmentId, occurrenceDate)
            : new AppointmentOccurrenceReference(normalizedAppointmentId, null);
    }

    private static string BuildOccurrenceId(string rootAppointmentId, DateOnly occurrenceDate) =>
        $"{rootAppointmentId}{VirtualOccurrenceSeparator}{occurrenceDate:yyyy-MM-dd}";

    private static bool IsValidOccurrenceDate(
        string anchorDateText,
        int recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        int? repeatOnNum,
        int? repeatOnDay,
        int? repeatOnFrequency,
        IReadOnlyList<int> recurrenceDays,
        string? recurrenceEndDateText,
        IReadOnlySet<DateOnly> recurrenceExdates,
        DateOnly occurrenceDate)
    {
        if (recurrenceType <= 0 || !DateOnly.TryParse(anchorDateText, out var anchorDate))
        {
            return false;
        }

        var recurrenceEndDate = DateOnly.TryParse(recurrenceEndDateText, out var parsedEndDate)
            ? parsedEndDate
            : anchorDate;
        if (occurrenceDate < anchorDate || occurrenceDate > recurrenceEndDate)
        {
            return false;
        }
        if (recurrenceExdates.Contains(occurrenceDate))
        {
            return false;
        }

        return CalculateOccurrenceNumber(
            anchorDateText,
            recurrenceType,
            repeatFrequency,
            repeatUnit,
            repeatOnNum,
            repeatOnDay,
            repeatOnFrequency,
            recurrenceDays,
            recurrenceEndDateText,
            occurrenceDate.ToString("yyyy-MM-dd")) is not null;
    }

    private static int? CalculateOccurrenceNumber(
        string anchorDateText,
        int recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        int? repeatOnNum,
        int? repeatOnDay,
        int? repeatOnFrequency,
        IReadOnlyList<int> recurrenceDays,
        string? recurrenceEndDateText,
        string occurrenceDateText)
    {
        if (!DateOnly.TryParse(anchorDateText, out var anchorDate)
            || !DateOnly.TryParse(occurrenceDateText, out var occurrenceDate))
        {
            return null;
        }

        if (recurrenceType == RepeatOnRecurrenceType)
        {
            var normalizedRepeatOnNum = repeatOnNum.GetValueOrDefault();
            var normalizedRepeatOnDay = repeatOnDay.GetValueOrDefault(-1);
            var normalizedRepeatOnFrequency = Math.Max(1, repeatOnFrequency.GetValueOrDefault(1));
            if (normalizedRepeatOnNum is < 1 or > 5 || normalizedRepeatOnDay is < 0 or > 6)
            {
                return null;
            }

            var recurrenceEndDate = DateOnly.TryParse(recurrenceEndDateText, out var parsedEndDate)
                ? parsedEndDate
                : anchorDate;
            if (occurrenceDate < anchorDate || occurrenceDate > recurrenceEndDate)
            {
                return null;
            }

            var anchorMonth = new DateOnly(anchorDate.Year, anchorDate.Month, 1);
            var occurrenceNumber = 0;
            for (var monthOffset = 0; occurrenceNumber < MaximumExpandedOccurrencesPerAppointment; monthOffset += normalizedRepeatOnFrequency)
            {
                var occurrenceMonth = anchorMonth.AddMonths(monthOffset);
                if (occurrenceMonth > recurrenceEndDate)
                {
                    return null;
                }

                var candidate = GetRepeatOnOccurrenceDate(occurrenceMonth.Year, occurrenceMonth.Month, normalizedRepeatOnNum, normalizedRepeatOnDay);
                if (candidate is null || candidate.Value < anchorDate)
                {
                    continue;
                }

                occurrenceNumber++;
                if (candidate.Value == occurrenceDate)
                {
                    return occurrenceNumber;
                }

                if (candidate.Value > occurrenceDate)
                {
                    return null;
                }
            }

            return null;
        }

        if (recurrenceType == SpecificWeekdaysRecurrenceType)
        {
            var selectedDays = ParseRecurrenceDaySet(recurrenceDays);
            if (selectedDays.Count == 0)
            {
                return null;
            }

            var recurrenceEndDate = DateOnly.TryParse(recurrenceEndDateText, out var parsedEndDate)
                ? parsedEndDate
                : anchorDate;
            if (occurrenceDate < anchorDate || occurrenceDate > recurrenceEndDate)
            {
                return null;
            }

            var selectedDateCursor = anchorDate;
            var occurrenceNumber = 0;
            while (selectedDateCursor <= occurrenceDate && occurrenceNumber < MaximumExpandedOccurrencesPerAppointment)
            {
                if (selectedDays.Contains(GetAvenChartWeekday(selectedDateCursor)))
                {
                    occurrenceNumber++;
                    if (selectedDateCursor == occurrenceDate)
                    {
                        return occurrenceNumber;
                    }
                }

                selectedDateCursor = selectedDateCursor.AddDays(1);
            }

            return null;
        }

        var frequency = Math.Max(1, repeatFrequency.GetValueOrDefault(1));
        var currentDate = anchorDate;
        for (var occurrenceNumber = 1; occurrenceNumber <= MaximumExpandedOccurrencesPerAppointment; occurrenceNumber++)
        {
            if (currentDate == occurrenceDate)
            {
                return occurrenceNumber;
            }

            if (currentDate > occurrenceDate)
            {
                return null;
            }

            var nextDate = GetNextOccurrenceDate(currentDate, frequency, repeatUnit);
            if (nextDate <= currentDate)
            {
                return null;
            }

            currentDate = nextDate;
        }

        return null;
    }

    private static DateOnly? GetRepeatOnOccurrenceDate(int year, int month, int repeatOnNum, int repeatOnDay)
    {
        if (repeatOnNum is < 1 or > 5 || repeatOnDay is < 0 or > 6)
        {
            return null;
        }

        if (repeatOnNum == 5)
        {
            var lastDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            while ((int)lastDate.DayOfWeek != repeatOnDay)
            {
                lastDate = lastDate.AddDays(-1);
            }

            return lastDate;
        }

        var firstDate = new DateOnly(year, month, 1);
        while ((int)firstDate.DayOfWeek != repeatOnDay)
        {
            firstDate = firstDate.AddDays(1);
        }

        var candidate = firstDate.AddDays((repeatOnNum - 1) * 7);
        return candidate.Month == month ? candidate : null;
    }

    private static DateOnly GetNextOccurrenceDate(DateOnly occurrenceDate, int repeatFrequency, int? repeatUnit) => repeatUnit switch
    {
        0 => occurrenceDate.AddDays(repeatFrequency),
        2 => occurrenceDate.AddMonths(repeatFrequency),
        3 => occurrenceDate.AddYears(repeatFrequency),
        4 => AddWorkdays(occurrenceDate, repeatFrequency),
        _ => occurrenceDate.AddDays(repeatFrequency * 7)
    };

    private static DateOnly AddWorkdays(DateOnly date, int workdays)
    {
        var result = date;
        var added = 0;
        while (added < workdays)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                added++;
            }
        }

        return result;
    }

    private static int GetAvenChartWeekday(DateOnly date) => (int)date.DayOfWeek + 1;

    private static string? GetAppointmentCategoryName(int? categoryId) => categoryId switch
    {
        9 => "Established Patient",
        10 => "New Patient",
        13 => "Preventive Care Services",
        null => null,
        _ => $"Category {categoryId.Value}"
    };

    private static string GetWaitlistPriority(int daysUntilRequestedSlot, string? reminderId)
    {
        if (daysUntilRequestedSlot <= 2)
        {
            return "urgent";
        }

        if (string.IsNullOrWhiteSpace(reminderId))
        {
            return "needs-reminder";
        }

        return daysUntilRequestedSlot <= 7 ? "soon" : "standard";
    }

    private static void AddRecurrenceParameters(
        NpgsqlCommand command,
        int? recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        int? repeatOnNum,
        int? repeatOnDay,
        int? repeatOnFrequency,
        IReadOnlyList<int>? recurrenceDays,
        string? recurrenceEndDate,
        IReadOnlyList<string>? recurrenceExdates)
    {
        var normalizedType = recurrenceType.GetValueOrDefault();
        var isRepeatOn = normalizedType == RepeatOnRecurrenceType;
        var isSpecificWeekdays = normalizedType == SpecificWeekdaysRecurrenceType;
        DateOnly? parsedEndDate = null;
        if (normalizedType > 0 && DateOnly.TryParse(recurrenceEndDate, out var endDate))
        {
            parsedEndDate = endDate;
        }
        var normalizedDays = isSpecificWeekdays
            ? NormalizeRecurrenceDays(recurrenceDays)
            : null;
        var normalizedExdates = normalizedType > 0
            ? NormalizeRecurrenceExdates(recurrenceExdates)
            : null;

        command.Parameters.AddWithValue("recurrenceType", Math.Max(0, normalizedType));
        command.Parameters.Add("repeatFrequency", NpgsqlDbType.Integer).Value = normalizedType > 0 && !isSpecificWeekdays && !isRepeatOn
            ? repeatFrequency.GetValueOrDefault(1)
            : (object)DBNull.Value;
        command.Parameters.Add("repeatUnit", NpgsqlDbType.Integer).Value = isSpecificWeekdays
            ? (object)SpecificWeekdaysRepeatUnit
            : normalizedType > 0 && !isRepeatOn ? (object)repeatUnit.GetValueOrDefault(1) : DBNull.Value;
        command.Parameters.Add("repeatOnNum", NpgsqlDbType.Integer).Value = isRepeatOn
            ? (object)NormalizeRepeatOnNum(repeatOnNum)
            : DBNull.Value;
        command.Parameters.Add("repeatOnDay", NpgsqlDbType.Integer).Value = isRepeatOn
            ? (object)NormalizeRepeatOnDay(repeatOnDay)
            : DBNull.Value;
        command.Parameters.Add("repeatOnFrequency", NpgsqlDbType.Integer).Value = isRepeatOn
            ? (object)Math.Max(1, repeatOnFrequency.GetValueOrDefault(1))
            : DBNull.Value;
        command.Parameters.Add("recurrenceEndDate", NpgsqlDbType.Date).Value = parsedEndDate is null ? DBNull.Value : (object)parsedEndDate.Value;
        command.Parameters.Add("recurrenceDays", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedDays) ? DBNull.Value : normalizedDays;
        command.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates) ? DBNull.Value : normalizedExdates;
    }

    private static int NormalizeRepeatOnNum(int? repeatOnNum) =>
        repeatOnNum is >= 1 and <= 5 ? repeatOnNum.Value : 1;

    private static int NormalizeRepeatOnDay(int? repeatOnDay) =>
        repeatOnDay is >= 0 and <= 6 ? repeatOnDay.Value : 0;

    private static int ReadRecurrenceType(DbDataReader reader) => ReadNullableInt(reader, "recurrence_type").GetValueOrDefault();

    private static IReadOnlyList<string> ReadDateList(DbDataReader reader, string columnName)
    {
        var rawValue = ReadNullableString(reader, columnName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<string>();
        }

        return rawValue
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => DateOnly.TryParse(value, out _))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<int> ReadIntList(DbDataReader reader, string columnName)
    {
        var rawValue = ReadNullableString(reader, columnName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<int>();
        }

        return rawValue
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var parsed) ? parsed : (int?)null)
            .Where(value => value is >= 1 and <= 7)
            .Select(value => value!.Value)
            .Distinct()
            .Order()
            .ToList();
    }

    private static IReadOnlySet<DateOnly> ParseRecurrenceExdateSet(IReadOnlyList<string> recurrenceExdates) =>
        recurrenceExdates
            .Select(value => DateOnly.TryParse(value, out var parsed) ? parsed : (DateOnly?)null)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToHashSet();

    private static IReadOnlySet<int> ParseRecurrenceDaySet(IReadOnlyList<int> recurrenceDays) =>
        recurrenceDays
            .Where(value => value is >= 1 and <= 7)
            .ToHashSet();

    private static string? NormalizeRecurrenceDays(IReadOnlyList<int>? recurrenceDays)
    {
        if (recurrenceDays is null || recurrenceDays.Count == 0)
        {
            return null;
        }

        var normalized = recurrenceDays
            .Where(value => value is >= 1 and <= 7)
            .Distinct()
            .Order()
            .ToList();
        return normalized.Count == 0 ? null : string.Join(",", normalized);
    }

    private static string? NormalizeRecurrenceExdates(IReadOnlyList<string>? recurrenceExdates)
    {
        if (recurrenceExdates is null || recurrenceExdates.Count == 0)
        {
            return null;
        }

        var normalized = recurrenceExdates
            .Select(value => DateOnly.TryParse(value, out var parsed) ? parsed.ToString("yyyy-MM-dd") : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        return normalized.Count == 0 ? null : string.Join(",", normalized);
    }

    private static string BuildRecurrenceLabel(
        int recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        int? repeatOnNum,
        int? repeatOnDay,
        int? repeatOnFrequency,
        IReadOnlyList<int> recurrenceDays,
        string? recurrenceEndDate)
    {
        if (recurrenceType <= 0)
        {
            return "Does not repeat";
        }

        if (recurrenceType == RepeatOnRecurrenceType)
        {
            var repeatOnMonthFrequency = Math.Max(1, repeatOnFrequency.GetValueOrDefault(1));
            var repeatOnCadencePrefix = repeatOnMonthFrequency == 1 ? "Every month" : $"Every {repeatOnMonthFrequency} months";
            var ordinal = GetRepeatOnOrdinalLabel(repeatOnNum.GetValueOrDefault());
            var weekday = GetRepeatOnWeekdayLabel(repeatOnDay.GetValueOrDefault(-1));
            var repeatOnCadence = $"{repeatOnCadencePrefix} on the {ordinal} {weekday}";
            return string.IsNullOrWhiteSpace(recurrenceEndDate) ? repeatOnCadence : $"{repeatOnCadence} until {recurrenceEndDate}";
        }

        if (recurrenceType == SpecificWeekdaysRecurrenceType)
        {
            var dayLabels = recurrenceDays
                .Where(value => value is >= 1 and <= 7)
                .Distinct()
                .Order()
                .Select(GetAvenChartWeekdayLabel)
                .ToList();
            var weekdayCadence = dayLabels.Count == 0
                ? "Every week on selected days"
                : $"Every week on {string.Join(", ", dayLabels)}";
            return string.IsNullOrWhiteSpace(recurrenceEndDate) ? weekdayCadence : $"{weekdayCadence} until {recurrenceEndDate}";
        }

        var frequency = Math.Max(1, repeatFrequency.GetValueOrDefault(1));
        var unit = repeatUnit switch
        {
            0 => frequency == 1 ? "day" : "days",
            4 => frequency == 1 ? "workday" : "workdays",
            2 => frequency == 1 ? "month" : "months",
            3 => frequency == 1 ? "year" : "years",
            _ => frequency == 1 ? "week" : "weeks"
        };
        var cadence = frequency == 1 ? $"Every {unit}" : $"Every {frequency} {unit}";
        return string.IsNullOrWhiteSpace(recurrenceEndDate) ? cadence : $"{cadence} until {recurrenceEndDate}";
    }

    private static string GetRepeatOnOrdinalLabel(int value) => value switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        4 => "4th",
        5 => "Last",
        _ => $"#{value}"
    };

    private static string GetRepeatOnWeekdayLabel(int value) => value switch
    {
        0 => "Sun",
        1 => "Mon",
        2 => "Tue",
        3 => "Wed",
        4 => "Thu",
        5 => "Fri",
        6 => "Sat",
        _ => $"Day {value}"
    };

    private static string GetAvenChartWeekdayLabel(int value) => value switch
    {
        1 => "Sun",
        2 => "Mon",
        3 => "Tue",
        4 => "Wed",
        5 => "Thu",
        6 => "Fri",
        7 => "Sat",
        _ => $"Day {value}"
    };

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static DateOnly ParseDateOrDefault(string? value, DateOnly defaultDate) =>
        DateOnly.TryParse(value, out var parsed) ? parsed : defaultDate;

    private static DateOnly? ParseOptionalDate(string? value) =>
        DateOnly.TryParse(value, out var parsed) ? parsed : null;

    private static bool IsWithinSearchRange(DateOnly value, DateOnly fromDate, DateOnly? toDate) =>
        value >= fromDate && (toDate is null || value <= toDate.Value);

    private static string BuildDisplayName(DbDataReader reader)
    {
        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");
        return string.IsNullOrWhiteSpace(preferredName)
            ? $"{lastName}, {firstName}"
            : $"{lastName}, {firstName} ({preferredName})";
    }

    private static string NormalizePatientName(string? firstName, string? lastName, string? fallback) =>
        NormalizeText($"{lastName}, {firstName}") ?? fallback ?? "Unknown patient";

    private static string SanitizeIdentifier(string value)
    {
        var cleaned = new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray())
            .Trim('-')
            .ToUpperInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }

    private static string ReadDate(DbDataReader reader, string columnName) =>
        reader.GetFieldValue<DateOnly>(reader.GetOrdinal(columnName)).ToString("yyyy-MM-dd");

    private static string ReadTime(DbDataReader reader, string columnName) =>
        reader.GetFieldValue<TimeOnly>(reader.GetOrdinal(columnName)).ToString("HH:mm");

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

    private sealed record SchedulingPatient(
        string CanonicalId,
        int LegacyPid,
        int? ProviderId,
        int? FacilityId);

    private sealed record AppointmentOccurrenceReference(string RootAppointmentId, DateOnly? OccurrenceDate);

    private sealed record AppointmentMutationState(string Status, int RowVersion, string PatientId);

    private sealed record ProviderOverlapSummary(int Count, IReadOnlyList<string> AppointmentIds);

    private sealed record AppointmentOverlapSummary(int Count, IReadOnlyList<string> AppointmentIds);

    private sealed record AppointmentReminder(
        bool Due,
        string Status,
        string Channel,
        string? Contact,
        int? LeadDays);

    private sealed record AppointmentRescheduleSource(
        string PatientId,
        int Pid,
        int? ProviderId,
        int? FacilityId,
        int? BillingLocationId,
        string AppointmentDate,
        int? CategoryId,
        string? Title,
        string? Status,
        string? Room,
        string? Comments,
        int RecurrenceType,
        int? RepeatFrequency,
        int? RepeatUnit,
        string? RecurrenceEndDate,
        IReadOnlyList<string> RecurrenceExdates);
}
