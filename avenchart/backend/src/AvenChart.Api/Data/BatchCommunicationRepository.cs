using System.Text.Json;
using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class BatchCommunicationRepository(NpgsqlDataSource dataSource)
{
    public async Task<BatchCommunicationPreview> PreviewAsync(BatchCommunicationPreviewRequest request, CancellationToken cancellationToken)
    {
        var filter = ValidateFilter(request.Filter);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return new(filter, await SelectRecipientsAsync(connection, filter, null, null, cancellationToken));
    }

    public async Task<BatchCommunicationCampaignDetail> CreateAsync(BatchCommunicationCampaignCreateRequest request, CancellationToken cancellationToken)
    {
        var filter = ValidateFilter(request.Filter);
        if (filter.ProcessType == "email" && (string.IsNullOrWhiteSpace(request.EmailSender) || string.IsNullOrWhiteSpace(request.EmailSubject) || string.IsNullOrWhiteSpace(request.EmailBody)))
            throw new ArgumentException("Email sender, subject, and body are required for an email campaign.");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var recipients = await SelectRecipientsAsync(connection, filter, request.EmailSubject, request.EmailBody, cancellationToken);
        var id = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "insert into batch_communication_campaigns(id,process_type,filter_json,email_sender,email_subject,email_body,recipient_count) values(@id,@type,cast(@filter as jsonb),@sender,@subject,@body,@count);";
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("type", filter.ProcessType);
            command.Parameters.AddWithValue("filter", JsonSerializer.Serialize(filter));
            command.Parameters.AddWithValue("sender", (object?)request.EmailSender?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("subject", (object?)request.EmailSubject?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("body", (object?)request.EmailBody?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("count", recipients.Count);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var recipient in recipients)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "insert into batch_communication_recipients(campaign_id,patient_id,display_name,email,phone_home,phone_cell,postal_code,next_appointment_date,last_appointment_date,last_visit_date,rendered_subject,rendered_body) values(@campaign,@patient,@name,@email,@home,@cell,@postal,@next,@lastAppointment,@lastVisit,@subject,@body);";
            command.Parameters.AddWithValue("campaign", id); command.Parameters.AddWithValue("patient", recipient.PatientId); command.Parameters.AddWithValue("name", recipient.DisplayName);
            command.Parameters.AddWithValue("email", (object?)recipient.Email ?? DBNull.Value); command.Parameters.AddWithValue("home", (object?)recipient.PhoneHome ?? DBNull.Value); command.Parameters.AddWithValue("cell", (object?)recipient.PhoneCell ?? DBNull.Value); command.Parameters.AddWithValue("postal", (object?)recipient.PostalCode ?? DBNull.Value);
            command.Parameters.AddWithValue("next", ParseDate(recipient.NextAppointmentDate)); command.Parameters.AddWithValue("lastAppointment", ParseDate(recipient.LastAppointmentDate)); command.Parameters.AddWithValue("lastVisit", ParseDate(recipient.LastVisitDate));
            command.Parameters.AddWithValue("subject", (object?)recipient.RenderedSubject ?? DBNull.Value); command.Parameters.AddWithValue("body", (object?)recipient.RenderedBody ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(new(id, filter, filter.ProcessType, request.EmailSender?.Trim(), request.EmailSubject?.Trim(), request.EmailBody?.Trim(), recipients.Count, DateTimeOffset.UtcNow.ToString("O")), recipients);
    }

    public async Task<IReadOnlyList<BatchCommunicationCampaign>> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "select id,process_type,filter_json,email_sender,email_subject,email_body,recipient_count,created_at from batch_communication_campaigns order by created_at desc;";
        var campaigns = new List<BatchCommunicationCampaign>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) campaigns.Add(ReadCampaign(reader)); return campaigns;
    }

    public async Task<BatchCommunicationCampaignDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "select id,process_type,filter_json,email_sender,email_subject,email_body,recipient_count,created_at from batch_communication_campaigns where id=@id;"; command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null; var campaign = ReadCampaign(reader); await reader.CloseAsync();
        await using var recipientsCommand = connection.CreateCommand(); recipientsCommand.CommandText = "select patient_id,display_name,email,phone_home,phone_cell,postal_code,next_appointment_date,last_appointment_date,last_visit_date,rendered_subject,rendered_body from batch_communication_recipients where campaign_id=@id order by display_name;"; recipientsCommand.Parameters.AddWithValue("id", id);
        var recipients = new List<BatchCommunicationRecipient>(); await using var recipientsReader = await recipientsCommand.ExecuteReaderAsync(cancellationToken); while (await recipientsReader.ReadAsync(cancellationToken)) recipients.Add(ReadRecipient(recipientsReader)); return new(campaign, recipients);
    }

    static BatchCommunicationFilter ValidateFilter(BatchCommunicationFilter filter)
    {
        var processType = filter.ProcessType?.Trim().ToLowerInvariant(); if (processType is not ("csv" or "email" or "phone")) throw new ArgumentException("Process type must be csv, email, or phone.");
        var gender = string.IsNullOrWhiteSpace(filter.Gender) ? "any" : filter.Gender.Trim().ToLowerInvariant(); if (gender is not ("any" or "male" or "female")) throw new ArgumentException("Gender must be any, male, or female.");
        var sortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "lastName" : filter.SortBy.Trim(); if (sortBy is not ("zipCode" or "lastName" or "appointmentDate")) throw new ArgumentException("Sort by must be zipCode, lastName, or appointmentDate.");
        if (filter.AgeFrom is < 0 or > 130 || filter.AgeTo is < 0 or > 130 || filter.AgeFrom > filter.AgeTo) throw new ArgumentException("Age filters must be between 0 and 130 and in ascending order.");
        return filter with { ProcessType = processType, Gender = gender, SortBy = sortBy };
    }

    static async Task<IReadOnlyList<BatchCommunicationRecipient>> SelectRecipientsAsync(NpgsqlConnection connection, BatchCommunicationFilter filter, string? subject, string? body, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var orderBy = filter.SortBy switch { "zipCode" => "p.postal_code,p.last_name,p.first_name", "appointmentDate" => "next_appointment_date nulls last,p.last_name,p.first_name", _ => "p.last_name,p.first_name" };
        command.CommandText = $"select p.canonical_id,p.first_name,p.last_name,p.email,p.phone_home,p.phone_cell,p.postal_code,next_appointment_date,last_appointment_date,last_visit_date from patients p left join lateral(select min(appointment_date) as next_appointment_date from appointments where patient_id=p.canonical_id and appointment_date>=current_date) next on true left join lateral(select max(appointment_date) as last_appointment_date from appointments where patient_id=p.canonical_id and appointment_date<current_date) last_appt on true left join lateral(select max(encounter_date) as last_visit_date from encounters where patient_id=p.canonical_id and encounter_date<=current_date) last_visit on true where p.merged_into_patient_id is null and (@gender='any' or lower(p.sex)=@gender) and (@consent=false or lower(coalesce(p.hipaa_allow_email,'')) in ('yes','true','1')) and (cast(@ageFrom as integer) is null or date_part('year',age(current_date,p.date_of_birth))>=cast(@ageFrom as integer)) and (cast(@ageTo as integer) is null or date_part('year',age(current_date,p.date_of_birth))<=cast(@ageTo as integer)) and (cast(@appointmentStart as date) is null or next_appointment_date>=cast(@appointmentStart as date)) and (cast(@appointmentEnd as date) is null or next_appointment_date<=cast(@appointmentEnd as date)) and (cast(@seenSince as date) is null or last_visit_date>=cast(@seenSince as date)) and (cast(@seenBefore as date) is null or last_visit_date<=cast(@seenBefore as date)) and (@type<>'email' or coalesce(trim(p.email),'')<>'') order by {orderBy} limit 5000;";
        command.Parameters.AddWithValue("gender", filter.Gender!); command.Parameters.AddWithValue("consent", filter.RequireConsent); command.Parameters.AddWithValue("ageFrom", (object?)filter.AgeFrom ?? DBNull.Value); command.Parameters.AddWithValue("ageTo", (object?)filter.AgeTo ?? DBNull.Value); command.Parameters.AddWithValue("appointmentStart", (object?)filter.AppointmentStart ?? DBNull.Value); command.Parameters.AddWithValue("appointmentEnd", (object?)filter.AppointmentEnd ?? DBNull.Value); command.Parameters.AddWithValue("seenSince", (object?)filter.SeenSince ?? DBNull.Value); command.Parameters.AddWithValue("seenBefore", (object?)filter.SeenBefore ?? DBNull.Value); command.Parameters.AddWithValue("type", filter.ProcessType);
        var recipients = new List<BatchCommunicationRecipient>(); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) { var name = $"{reader.GetString(1)} {reader.GetString(2)}"; recipients.Add(new(reader.GetString(0), name, Text(reader, 3), Text(reader, 4), Text(reader, 5), Text(reader, 6), Date(reader, 7), Date(reader, 8), Date(reader, 9), Render(subject, name), Render(body, name))); }
        return recipients;
    }

    static BatchCommunicationCampaign ReadCampaign(NpgsqlDataReader reader) => new(reader.GetGuid(0), JsonSerializer.Deserialize<BatchCommunicationFilter>(reader.GetString(2))!, reader.GetString(1), Text(reader, 3), Text(reader, 4), Text(reader, 5), reader.GetInt32(6), reader.GetFieldValue<DateTimeOffset>(7).ToString("O"));
    static BatchCommunicationRecipient ReadRecipient(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(1), Text(reader, 2), Text(reader, 3), Text(reader, 4), Text(reader, 5), Date(reader, 6), Date(reader, 7), Date(reader, 8), Text(reader, 9), Text(reader, 10));
    static string? Text(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    static string? Date(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal).ToString("yyyy-MM-dd");
    static object ParseDate(string? value) => value is null ? DBNull.Value : DateOnly.Parse(value);
    static string? Render(string? template, string name) => template?.Replace("***NAME***", name, StringComparison.Ordinal);
}
