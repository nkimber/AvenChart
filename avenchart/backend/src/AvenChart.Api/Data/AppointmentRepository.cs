using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AppointmentRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;
    private const int MaximumExpandedOccurrencesPerAppointment = 366;
    private const string VirtualOccurrenceSeparator = "::occurs::";
    private const int SpecificWeekdaysRecurrenceType = 3;
    private const int SpecificWeekdaysRepeatUnit = 6;

    public async Task<AppointmentSearchResponse> SearchAsync(
        string? patientId,
        string? from,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, MaximumSearchLimit);
        var metadata = await GetMetadataAsync(cancellationToken);
        var normalizedPatientId = Normalize(patientId);
        var fromDate = ParseDateOrDefault(from, metadata.BaseDate);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $$"""
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
                a.category_id,
                a.title,
                a.status,
                a.room,
                a.comments,
                a.recurrence_type,
                a.repeat_frequency,
                a.repeat_unit,
                a.recurrence_end_date,
                a.recurrence_days,
                a.recurrence_exdates,
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
            where {{AppointmentSearchPredicate}}
            order by a.appointment_date, a.start_time, a.id
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate);

        var expandedAppointments = new List<AppointmentListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            expandedAppointments.AddRange(ExpandAppointmentListItem(ReadListItem(reader), fromDate));
        }

        var appointments = expandedAppointments
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
            Limit: safeLimit,
            TotalMatches: expandedAppointments.Count,
            Appointments: appointments);
    }

    public async Task<AppointmentDetail?> GetByIdAsync(string appointmentId, CancellationToken cancellationToken)
    {
        var occurrenceReference = ParseOccurrenceReference(appointmentId);
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
                p.sex,
                p.date_of_birth,
                p.purpose,
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
                a.recurrence_end_date,
                a.recurrence_days,
                a.recurrence_exdates,
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
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
        var appointmentDate = ReadDate(reader, "appointment_date");
        var occurrenceDate = occurrenceReference.OccurrenceDate;
        if (occurrenceDate is not null
            && !IsValidOccurrenceDate(appointmentDate, recurrenceType, repeatFrequency, repeatUnit, recurrenceDays, recurrenceEndDate, recurrenceExdateSet, occurrenceDate.Value))
        {
            return null;
        }

        var occurrenceNumber = recurrenceType > 0
            ? CalculateOccurrenceNumber(appointmentDate, recurrenceType, repeatFrequency, repeatUnit, recurrenceDays, recurrenceEndDate, occurrenceDate?.ToString("yyyy-MM-dd") ?? appointmentDate)
            : null;
        var isVirtualOccurrence = occurrenceDate is not null && occurrenceDate.Value.ToString("yyyy-MM-dd") != appointmentDate;
        var responseDate = occurrenceDate?.ToString("yyyy-MM-dd") ?? appointmentDate;
        var responseId = isVirtualOccurrence
            ? BuildOccurrenceId(occurrenceReference.RootAppointmentId, occurrenceDate!.Value)
            : occurrenceReference.RootAppointmentId;

        return new AppointmentDetail(
            Id: responseId,
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
            RecurrenceDays: recurrenceDays,
            RecurrenceEndDate: recurrenceEndDate,
            RecurrenceExdates: recurrenceExdates,
            RecurrenceExceptionCount: recurrenceExdates.Count,
            RecurrenceLabel: BuildRecurrenceLabel(recurrenceType, repeatFrequency, repeatUnit, recurrenceDays, recurrenceEndDate),
            PatientPurpose: ReadNullableString(reader, "purpose"));
    }

    public async Task<AppointmentDetail?> CreateAsync(
        AppointmentCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var startTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            with patient_match as (
                select canonical_id, legacy_pid, provider_id, facility_id
                from patients
                where lower(canonical_id) = lower(@patientId)
                   or lower(pubpid) = lower(@patientId)
                   or legacy_pid::text = @patientId
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
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            )
            select
                @id,
                canonical_id,
                legacy_pid,
                coalesce((select id from staff where id = @providerId), provider_id),
                coalesce((select id from facilities where id = @facilityId), facility_id),
                coalesce((select id from facilities where id = @billingLocationId), (select id from facilities where id = @facilityId), facility_id),
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
        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.Add("billingLocationId", NpgsqlDbType.Integer).Value = request.BillingLocationId is null ? DBNull.Value : request.BillingLocationId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = startTime;
        command.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        command.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        command.Parameters.AddWithValue("title", NormalizeText(request.Title) ?? "Appointment");
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;
        command.Parameters.Add("comments", NpgsqlDbType.Text).Value = NormalizeText(request.Comments) ?? (object)DBNull.Value;
        AddRecurrenceParameters(command, request.RecurrenceType, request.RepeatFrequency, request.RepeatUnit, request.RecurrenceDays, request.RecurrenceEndDate, request.RecurrenceExdates);

        var insertedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return insertedId is null ? null : await GetByIdAsync(insertedId, cancellationToken);
    }

    public async Task<AppointmentDetail?> UpdateStatusAsync(
        string appointmentId,
        AppointmentStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var rootAppointmentId = ParseOccurrenceReference(appointmentId).RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update appointments
            set status = @status,
                title = coalesce(@title, title)
            where id = @appointmentId
            returning id;
            """;
        command.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        command.Parameters.AddWithValue("status", NormalizeText(request.Status) ?? "-");
        command.Parameters.Add("title", NpgsqlDbType.Text).Value = NormalizeText(request.Title) ?? (object)DBNull.Value;

        var updatedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return updatedId is null ? null : await GetByIdAsync(updatedId, cancellationToken);
    }

    public async Task<AppointmentDetail?> UpdateAsync(
        string appointmentId,
        AppointmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(request.Date, out var appointmentDate)
            || !TimeOnly.TryParse(request.StartTime, out var startTime)
            || request.DurationMinutes <= 0)
        {
            return null;
        }

        var rootAppointmentId = ParseOccurrenceReference(appointmentId).RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
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
                recurrence_end_date = @recurrenceEndDate,
                recurrence_days = @recurrenceDays,
                recurrence_exdates = @recurrenceExdates
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
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = NormalizeText(request.Status) ?? (object)DBNull.Value;
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;
        command.Parameters.Add("comments", NpgsqlDbType.Text).Value = NormalizeText(request.Comments) ?? (object)DBNull.Value;
        AddRecurrenceParameters(command, request.RecurrenceType, request.RepeatFrequency, request.RepeatUnit, request.RecurrenceDays, request.RecurrenceEndDate, request.RecurrenceExdates);

        var updatedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return updatedId is null ? null : await GetByIdAsync(updatedId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string appointmentId, CancellationToken cancellationToken)
    {
        var occurrenceReference = ParseOccurrenceReference(appointmentId);
        if (occurrenceReference.OccurrenceDate is not null)
        {
            return await AddRecurrenceExceptionAsync(
                occurrenceReference.RootAppointmentId,
                occurrenceReference.OccurrenceDate.Value,
                cancellationToken);
        }

        var rootAppointmentId = occurrenceReference.RootAppointmentId;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from appointments where id = @appointmentId;";
        command.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<AppointmentDetail?> RestoreRecurrenceExceptionAsync(
        string appointmentId,
        string occurrenceDateText,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(occurrenceDateText, out var occurrenceDate))
        {
            return null;
        }

        var occurrenceReference = ParseOccurrenceReference(appointmentId);
        var rootAppointmentId = occurrenceReference.RootAppointmentId;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var readCommand = connection.CreateCommand();
        readCommand.CommandText = """
            select appointment_date,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var appointmentDate = ReadDate(reader, "appointment_date");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
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
                recurrenceDays,
                recurrenceEndDate,
                updatedExdateSet,
                occurrenceDate))
        {
            return null;
        }

        var normalizedExdates = NormalizeRecurrenceExdates(updatedExdates);

        await reader.DisposeAsync();
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates
            where id = @appointmentId;
            """;
        updateCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        updateCommand.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates)
            ? DBNull.Value
            : (object)normalizedExdates;

        return await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0
            ? await GetByIdAsync(rootAppointmentId, cancellationToken)
            : null;
    }

    public async Task<AppointmentDetail?> RescheduleOccurrenceAsync(
        string appointmentId,
        string occurrenceDateText,
        AppointmentOccurrenceRescheduleRequest request,
        CancellationToken cancellationToken)
    {
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
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        AppointmentRescheduleSource source;
        await using (var reader = await readCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var appointmentDate = ReadDate(reader, "appointment_date");
            var recurrenceType = ReadRecurrenceType(reader);
            var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
            var repeatUnit = ReadNullableInt(reader, "repeat_unit");
            var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
            var recurrenceDays = ReadIntList(reader, "recurrence_days");
            var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
            var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
            if (!IsValidOccurrenceDate(
                    appointmentDate,
                    recurrenceType,
                    repeatFrequency,
                    repeatUnit,
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

        var updatedExdates = source.RecurrenceExdates
            .Concat(new[] { occurrenceDate.ToString("yyyy-MM-dd") })
            .ToList();
        var normalizedExdates = NormalizeRecurrenceExdates(updatedExdates);

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates
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
        var rescheduledTitle = NormalizeText(request.Title) ?? source.Title ?? "Appointment";
        var rescheduledStatus = NormalizeText(request.Status) ?? source.Status ?? "-";
        var rescheduledRoom = request.Room is null ? source.Room : NormalizeText(request.Room);
        var rescheduledComments = request.Comments is null ? source.Comments : NormalizeText(request.Comments);

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
                null
            )
            returning id;
            """;
        insertCommand.Parameters.AddWithValue("id", rescheduledAppointmentId);
        insertCommand.Parameters.AddWithValue("patientId", source.PatientId);
        insertCommand.Parameters.AddWithValue("pid", source.Pid);
        insertCommand.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        insertCommand.Parameters.Add("sourceProviderId", NpgsqlDbType.Integer).Value = source.ProviderId is null ? DBNull.Value : source.ProviderId.Value;
        insertCommand.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
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
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var readCommand = connection.CreateCommand();
        readCommand.CommandText = """
            select appointment_date,
                recurrence_type,
                repeat_frequency,
                repeat_unit,
                recurrence_end_date,
                recurrence_days,
                recurrence_exdates
            from appointments
            where id = @appointmentId
            limit 1;
            """;
        readCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);

        await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var appointmentDate = ReadDate(reader, "appointment_date");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");
        var recurrenceExdateSet = ParseRecurrenceExdateSet(recurrenceExdates);
        if (!IsValidOccurrenceDate(
                appointmentDate,
                recurrenceType,
                repeatFrequency,
                repeatUnit,
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
        updateCommand.CommandText = """
            update appointments
            set recurrence_exdates = @recurrenceExdates
            where id = @appointmentId;
            """;
        updateCommand.Parameters.AddWithValue("appointmentId", rootAppointmentId);
        updateCommand.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates)
            ? DBNull.Value
            : (object)normalizedExdates;
        return await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
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

    private const string AppointmentSearchPredicate = """
        (@patientId is null
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
        """;

    private static void AddSearchParameters(NpgsqlCommand command, string? patientId, DateOnly fromDate)
    {
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId is null ? DBNull.Value : patientId;
        command.Parameters.Add("fromDate", NpgsqlDbType.Date).Value = fromDate;
    }

    private static AppointmentListItem ReadListItem(DbDataReader reader)
    {
        var categoryId = ReadNullableInt(reader, "category_id");
        var recurrenceType = ReadRecurrenceType(reader);
        var repeatFrequency = ReadNullableInt(reader, "repeat_frequency");
        var repeatUnit = ReadNullableInt(reader, "repeat_unit");
        var recurrenceEndDate = ReadNullableDate(reader, "recurrence_end_date");
        var recurrenceDays = ReadIntList(reader, "recurrence_days");
        var recurrenceExdates = ReadDateList(reader, "recurrence_exdates");

        return new AppointmentListItem(
            Id: reader.GetString(reader.GetOrdinal("id")),
            SeriesRootId: reader.GetString(reader.GetOrdinal("id")),
            IsRecurringSeries: recurrenceType > 0,
            IsVirtualOccurrence: false,
            OccurrenceNumber: recurrenceType > 0 ? 1 : null,
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            PatientDisplayName: BuildDisplayName(reader),
            Date: ReadDate(reader, "appointment_date"),
            StartTime: ReadTime(reader, "start_time"),
            DurationMinutes: reader.GetInt32(reader.GetOrdinal("duration_minutes")),
            Title: ReadNullableString(reader, "title") ?? "Appointment",
            Status: ReadNullableString(reader, "status"),
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
            RecurrenceDays: recurrenceDays,
            RecurrenceEndDate: recurrenceEndDate,
            RecurrenceExdates: recurrenceExdates,
            RecurrenceExceptionCount: recurrenceExdates.Count,
            RecurrenceLabel: BuildRecurrenceLabel(recurrenceType, repeatFrequency, repeatUnit, recurrenceDays, recurrenceEndDate));
    }

    private static IEnumerable<AppointmentListItem> ExpandAppointmentListItem(AppointmentListItem appointment, DateOnly fromDate)
    {
        if (!DateOnly.TryParse(appointment.Date, out var anchorDate))
        {
            yield return appointment;
            yield break;
        }

        if (appointment.RecurrenceType <= 0)
        {
            if (anchorDate >= fromDate)
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

        if (appointment.RecurrenceType == SpecificWeekdaysRecurrenceType)
        {
            var selectedDays = ParseRecurrenceDaySet(appointment.RecurrenceDays);
            if (selectedDays.Count == 0)
            {
                yield break;
            }

            var currentDate = anchorDate;
            var occurrenceNumber = 0;
            while (currentDate <= recurrenceEndDate && occurrenceNumber < MaximumExpandedOccurrencesPerAppointment)
            {
                if (selectedDays.Contains(GetLegacyEhrWeekday(currentDate)))
                {
                    occurrenceNumber++;
                    if (currentDate >= fromDate && !recurrenceExdates.Contains(currentDate))
                    {
                        var isVirtualOccurrence = currentDate != anchorDate;
                        yield return appointment with
                        {
                            Id = isVirtualOccurrence ? BuildOccurrenceId(appointment.SeriesRootId, currentDate) : appointment.SeriesRootId,
                            Date = currentDate.ToString("yyyy-MM-dd"),
                            IsRecurringSeries = true,
                            IsVirtualOccurrence = isVirtualOccurrence,
                            OccurrenceNumber = occurrenceNumber
                        };
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            yield break;
        }

        var occurrenceDate = anchorDate;
        for (var occurrenceNumber = 1;
             occurrenceDate <= recurrenceEndDate && occurrenceNumber <= MaximumExpandedOccurrencesPerAppointment;
             occurrenceNumber++)
        {
            if (occurrenceDate >= fromDate && !recurrenceExdates.Contains(occurrenceDate))
            {
                var isVirtualOccurrence = occurrenceDate != anchorDate;
                yield return appointment with
                {
                    Id = isVirtualOccurrence ? BuildOccurrenceId(appointment.SeriesRootId, occurrenceDate) : appointment.SeriesRootId,
                    Date = occurrenceDate.ToString("yyyy-MM-dd"),
                    IsRecurringSeries = true,
                    IsVirtualOccurrence = isVirtualOccurrence,
                    OccurrenceNumber = occurrenceNumber
                };
            }

            var nextOccurrenceDate = GetNextOccurrenceDate(occurrenceDate, repeatFrequency, appointment.RepeatUnit);
            if (nextOccurrenceDate <= occurrenceDate)
            {
                yield break;
            }

            occurrenceDate = nextOccurrenceDate;
        }
    }

    private static AppointmentOccurrenceReference ParseOccurrenceReference(string appointmentId)
    {
        var separatorIndex = appointmentId.IndexOf(VirtualOccurrenceSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return new AppointmentOccurrenceReference(appointmentId, null);
        }

        var rootAppointmentId = appointmentId[..separatorIndex];
        var occurrenceDateText = appointmentId[(separatorIndex + VirtualOccurrenceSeparator.Length)..];
        return DateOnly.TryParse(occurrenceDateText, out var occurrenceDate)
            ? new AppointmentOccurrenceReference(rootAppointmentId, occurrenceDate)
            : new AppointmentOccurrenceReference(appointmentId, null);
    }

    private static string BuildOccurrenceId(string rootAppointmentId, DateOnly occurrenceDate) =>
        $"{rootAppointmentId}{VirtualOccurrenceSeparator}{occurrenceDate:yyyy-MM-dd}";

    private static bool IsValidOccurrenceDate(
        string anchorDateText,
        int recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
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

        return CalculateOccurrenceNumber(anchorDateText, recurrenceType, repeatFrequency, repeatUnit, recurrenceDays, recurrenceEndDateText, occurrenceDate.ToString("yyyy-MM-dd")) is not null;
    }

    private static int? CalculateOccurrenceNumber(
        string anchorDateText,
        int recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        IReadOnlyList<int> recurrenceDays,
        string? recurrenceEndDateText,
        string occurrenceDateText)
    {
        if (!DateOnly.TryParse(anchorDateText, out var anchorDate)
            || !DateOnly.TryParse(occurrenceDateText, out var occurrenceDate))
        {
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
                if (selectedDays.Contains(GetLegacyEhrWeekday(selectedDateCursor)))
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

    private static int GetLegacyEhrWeekday(DateOnly date) => (int)date.DayOfWeek + 1;

    private static string? GetAppointmentCategoryName(int? categoryId) => categoryId switch
    {
        9 => "Established Patient",
        10 => "New Patient",
        13 => "Preventive Care Services",
        null => null,
        _ => $"Category {categoryId.Value}"
    };

    private static void AddRecurrenceParameters(
        NpgsqlCommand command,
        int? recurrenceType,
        int? repeatFrequency,
        int? repeatUnit,
        IReadOnlyList<int>? recurrenceDays,
        string? recurrenceEndDate,
        IReadOnlyList<string>? recurrenceExdates)
    {
        var normalizedType = recurrenceType.GetValueOrDefault();
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
        command.Parameters.Add("repeatFrequency", NpgsqlDbType.Integer).Value = normalizedType > 0 && !isSpecificWeekdays
            ? repeatFrequency.GetValueOrDefault(1)
            : (object)DBNull.Value;
        command.Parameters.Add("repeatUnit", NpgsqlDbType.Integer).Value = isSpecificWeekdays
            ? (object)SpecificWeekdaysRepeatUnit
            : normalizedType > 0 ? (object)repeatUnit.GetValueOrDefault(1) : DBNull.Value;
        command.Parameters.Add("recurrenceEndDate", NpgsqlDbType.Date).Value = parsedEndDate is null ? DBNull.Value : (object)parsedEndDate.Value;
        command.Parameters.Add("recurrenceDays", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedDays) ? DBNull.Value : normalizedDays;
        command.Parameters.Add("recurrenceExdates", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(normalizedExdates) ? DBNull.Value : normalizedExdates;
    }

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

    private static string BuildRecurrenceLabel(int recurrenceType, int? repeatFrequency, int? repeatUnit, IReadOnlyList<int> recurrenceDays, string? recurrenceEndDate)
    {
        if (recurrenceType <= 0)
        {
            return "Does not repeat";
        }

        if (recurrenceType == SpecificWeekdaysRecurrenceType)
        {
            var dayLabels = recurrenceDays
                .Where(value => value is >= 1 and <= 7)
                .Distinct()
                .Order()
                .Select(GetLegacyEhrWeekdayLabel)
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

    private static string GetLegacyEhrWeekdayLabel(int value) => value switch
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

    private static string BuildDisplayName(DbDataReader reader)
    {
        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");
        return string.IsNullOrWhiteSpace(preferredName)
            ? $"{lastName}, {firstName}"
            : $"{lastName}, {firstName} ({preferredName})";
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

    private sealed record AppointmentOccurrenceReference(string RootAppointmentId, DateOnly? OccurrenceDate);

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
