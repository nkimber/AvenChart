// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;

namespace AvenChart.Api.Features.Telehealth;

public sealed class TelehealthConversationRepository(NpgsqlDataSource dataSource)
{
    public Task<TelehealthConversationResponse?> GetForPatientAsync(
        string practiceId, string patientId, Guid requestId, CancellationToken cancellationToken) =>
        GetAsync(practiceId, requestId, patientId, physicianStaffId: null, cancellationToken);

    public Task<TelehealthConversationResponse?> GetForPhysicianAsync(
        string practiceId, int physicianStaffId, Guid consultationId, CancellationToken cancellationToken) =>
        GetAsync(practiceId, requestId: null, patientId: null, physicianStaffId, cancellationToken, consultationId);

    public Task<TelehealthConversationResponse?> AddForPatientAsync(
        string practiceId, string patientId, Guid requestId, string body, CancellationToken cancellationToken) =>
        AddAsync(practiceId, requestId, patientId, physicianStaffId: null, "patient", body, cancellationToken);

    public Task<TelehealthConversationResponse?> AddForPhysicianAsync(
        string practiceId, int physicianStaffId, Guid consultationId, string body, CancellationToken cancellationToken) =>
        AddAsync(practiceId, requestId: null, patientId: null, physicianStaffId, "physician", body, cancellationToken, consultationId);

    private async Task<TelehealthConversationResponse?> GetAsync(
        string practiceId, Guid? requestId, string? patientId, int? physicianStaffId, CancellationToken cancellationToken, Guid? consultationId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var context = await ResolveAsync(connection, null, practiceId, requestId, patientId, physicianStaffId, consultationId, cancellationToken);
        if (context is null) return null;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select message_id,sender_role,body,sent_at,legal_effect
            from telehealth_consultation_transcript_messages
            where consultation_id=@consultationId
            order by sent_at,message_id;
            """;
        command.Parameters.AddWithValue("consultationId", context.Value.ConsultationId);
        var messages = new List<TelehealthConversationMessageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new TelehealthConversationMessageResponse(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3), reader.GetBoolean(4)));
        }
        return ToResponse(context.Value, messages);
    }

    private async Task<TelehealthConversationResponse?> AddAsync(
        string practiceId, Guid? requestId, string? patientId, int? physicianStaffId, string senderRole, string body,
        CancellationToken cancellationToken, Guid? consultationId = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var context = await ResolveAsync(connection, transaction, practiceId, requestId, patientId, physicianStaffId, consultationId, cancellationToken, forUpdate: true);
        if (context is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into telehealth_consultation_transcript_messages(
                  message_id,consultation_id,request_id,practice_id,patient_id,physician_staff_id,sender_role,body,
                  synthetic_data_confirmed,legal_effect)
                values (@messageId,@consultationId,@requestId,@practiceId,@patientId,@physicianStaffId,@senderRole,@body,true,false);
                """;
            insert.Parameters.AddWithValue("messageId", Guid.NewGuid());
            insert.Parameters.AddWithValue("consultationId", context.Value.ConsultationId);
            insert.Parameters.AddWithValue("requestId", context.Value.RequestId);
            insert.Parameters.AddWithValue("practiceId", practiceId);
            insert.Parameters.AddWithValue("patientId", context.Value.PatientId);
            insert.Parameters.AddWithValue("physicianStaffId", context.Value.PhysicianStaffId);
            insert.Parameters.AddWithValue("senderRole", senderRole);
            insert.Parameters.AddWithValue("body", body);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(practiceId, context.Value.RequestId, context.Value.PatientId, null, cancellationToken);
    }

    private static async Task<ConversationContext?> ResolveAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string practiceId, Guid? requestId, string? patientId,
        int? physicianStaffId, Guid? consultationId, CancellationToken cancellationToken, bool forUpdate = false)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select context.consultation_id,context.request_id,request.patient_id,context.physician_staff_id
            from telehealth_consultation_contexts context
            join telehealth_requests request on request.request_id=context.request_id
            where context.practice_id=@practiceId and request.practice_id=@practiceId
              and context.status='Started' and request.status='InConsultation'
              and (@requestId is null or context.request_id=@requestId)
              and (@consultationId is null or context.consultation_id=@consultationId)
              and (@patientId is null or request.patient_id=@patientId)
              and (@physicianStaffId is null or context.physician_staff_id=@physicianStaffId)
            {(forUpdate ? "for update of context,request" : string.Empty)};
            """;
        command.Parameters.AddWithValue("practiceId", practiceId);
        command.Parameters.AddWithValue("requestId", requestId is null ? DBNull.Value : requestId.Value);
        command.Parameters.AddWithValue("consultationId", consultationId is null ? DBNull.Value : consultationId.Value);
        command.Parameters.AddWithValue("patientId", patientId is null ? DBNull.Value : patientId);
        command.Parameters.AddWithValue("physicianStaffId", physicianStaffId is null ? DBNull.Value : physicianStaffId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ConversationContext(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3))
            : null;
    }

    private static TelehealthConversationResponse ToResponse(
        ConversationContext context, IReadOnlyList<TelehealthConversationMessageResponse> messages) => new(
        context.ConsultationId, context.RequestId, "InConsultation", true, false, messages,
        [
            "This is a synthetic plain-text demonstration transcript, not a real messaging, video, emergency, or clinical-care channel.",
            "Authoritative refresh uses short HTTP polling; realtime delivery, attachments, recording, transcription, notifications, and external delivery are disabled.",
            "Messages have no legal or clinical effect and must not be used for patient care or real personal information."
        ]);

    private readonly record struct ConversationContext(Guid ConsultationId, Guid RequestId, string PatientId, int PhysicianStaffId);
}
