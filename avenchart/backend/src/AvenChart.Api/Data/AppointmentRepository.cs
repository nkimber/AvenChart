using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class AppointmentRepository(NpgsqlDataSource dataSource)
{
    private const int MaximumSearchLimit = 100;

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
        var totalMatches = await CountMatchesAsync(connection, normalizedPatientId, fromDate, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
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
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where {AppointmentSearchPredicate}
            order by a.appointment_date, a.start_time, a.id
            limit @limit;
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate);
        command.Parameters.AddWithValue("limit", safeLimit);

        var appointments = new List<AppointmentListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            appointments.Add(ReadListItem(reader));
        }

        return new AppointmentSearchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patientId,
            FromDate: fromDate.ToString("yyyy-MM-dd"),
            Limit: safeLimit,
            TotalMatches: totalMatches,
            Appointments: appointments);
    }

    public async Task<AppointmentDetail?> GetByIdAsync(string appointmentId, CancellationToken cancellationToken)
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
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name
            from appointments a
            join patients p on p.legacy_pid = a.pid
            left join staff s on s.id = a.provider_id
            left join facilities f on f.id = a.facility_id
            where a.id = @appointmentId;
            """;
        command.Parameters.AddWithValue("appointmentId", appointmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AppointmentDetail(
            Id: reader.GetString(reader.GetOrdinal("id")),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            PatientDisplayName: BuildDisplayName(reader),
            FirstName: reader.GetString(reader.GetOrdinal("first_name")),
            LastName: reader.GetString(reader.GetOrdinal("last_name")),
            Sex: ReadNullableString(reader, "sex"),
            DateOfBirth: ReadDate(reader, "date_of_birth"),
            Date: ReadDate(reader, "appointment_date"),
            StartTime: ReadTime(reader, "start_time"),
            DurationMinutes: reader.GetInt32(reader.GetOrdinal("duration_minutes")),
            Title: ReadNullableString(reader, "title") ?? "Appointment",
            Status: ReadNullableString(reader, "status"),
            Room: ReadNullableString(reader, "room"),
            CategoryId: ReadNullableInt(reader, "category_id"),
            ProviderName: ReadNullableString(reader, "provider_name"),
            FacilityName: ReadNullableString(reader, "facility_name"),
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
                appointment_date,
                start_time,
                duration_minutes,
                category_id,
                title,
                status,
                room
            )
            select
                @id,
                canonical_id,
                legacy_pid,
                coalesce((select id from staff where id = @providerId), provider_id),
                coalesce((select id from facilities where id = @facilityId), facility_id),
                @appointmentDate,
                @startTime,
                @durationMinutes,
                coalesce(@categoryId, 9),
                @title,
                '-',
                @room
            from patient_match
            returning id;
            """;
        var appointmentId = $"APPT-MODERN-{Guid.NewGuid():N}";
        command.Parameters.AddWithValue("id", appointmentId);
        command.Parameters.AddWithValue("patientId", request.PatientId.Trim());
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = startTime;
        command.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        command.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        command.Parameters.AddWithValue("title", NormalizeText(request.Title) ?? "Appointment");
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;

        var insertedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return insertedId is null ? null : await GetByIdAsync(insertedId, cancellationToken);
    }

    public async Task<AppointmentDetail?> UpdateStatusAsync(
        string appointmentId,
        AppointmentStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update appointments
            set status = @status,
                title = coalesce(@title, title)
            where id = @appointmentId
            returning id;
            """;
        command.Parameters.AddWithValue("appointmentId", appointmentId);
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

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            update appointments
            set provider_id = coalesce((select id from staff where id = @providerId), provider_id),
                facility_id = coalesce((select id from facilities where id = @facilityId), facility_id),
                appointment_date = @appointmentDate,
                start_time = @startTime,
                duration_minutes = @durationMinutes,
                category_id = coalesce(@categoryId, category_id),
                title = @title,
                status = coalesce(@status, status),
                room = @room
            where id = @appointmentId
            returning id;
            """;
        command.Parameters.AddWithValue("appointmentId", appointmentId);
        command.Parameters.Add("providerId", NpgsqlDbType.Integer).Value = request.ProviderId is null ? DBNull.Value : request.ProviderId.Value;
        command.Parameters.Add("facilityId", NpgsqlDbType.Integer).Value = request.FacilityId is null ? DBNull.Value : request.FacilityId.Value;
        command.Parameters.Add("appointmentDate", NpgsqlDbType.Date).Value = appointmentDate;
        command.Parameters.Add("startTime", NpgsqlDbType.Time).Value = startTime;
        command.Parameters.AddWithValue("durationMinutes", request.DurationMinutes);
        command.Parameters.Add("categoryId", NpgsqlDbType.Integer).Value = request.CategoryId is null ? DBNull.Value : request.CategoryId.Value;
        command.Parameters.AddWithValue("title", NormalizeText(request.Title) ?? "Appointment");
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = NormalizeText(request.Status) ?? (object)DBNull.Value;
        command.Parameters.Add("room", NpgsqlDbType.Text).Value = NormalizeText(request.Room) ?? (object)DBNull.Value;

        var updatedId = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return updatedId is null ? null : await GetByIdAsync(updatedId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string appointmentId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from appointments where id = @appointmentId;";
        command.Parameters.AddWithValue("appointmentId", appointmentId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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

    private static async Task<int> CountMatchesAsync(
        NpgsqlConnection connection,
        string? normalizedPatientId,
        DateOnly fromDate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select count(*)
            from appointments a
            join patients p on p.legacy_pid = a.pid
            where {AppointmentSearchPredicate};
            """;
        AddSearchParameters(command, normalizedPatientId, fromDate);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private const string AppointmentSearchPredicate = """
        (@patientId is null
         or lower(p.canonical_id) = @patientId
         or lower(p.pubpid) = @patientId
         or p.legacy_pid::text = @patientId)
        and a.appointment_date >= @fromDate
        """;

    private static void AddSearchParameters(NpgsqlCommand command, string? patientId, DateOnly fromDate)
    {
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = patientId is null ? DBNull.Value : patientId;
        command.Parameters.Add("fromDate", NpgsqlDbType.Date).Value = fromDate;
    }

    private static AppointmentListItem ReadListItem(DbDataReader reader) => new(
        Id: reader.GetString(reader.GetOrdinal("id")),
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
        CategoryId: ReadNullableInt(reader, "category_id"),
        ProviderName: ReadNullableString(reader, "provider_name"),
        FacilityName: ReadNullableString(reader, "facility_name"));

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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);
}
