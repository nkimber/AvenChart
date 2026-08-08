// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class PatientRecordRequestRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<PatientRecordRequestResponse>> GetAsync(string patientId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken)
            ?? throw new ArgumentException("The patient does not exist.");

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select request_id, patient_id, pid, requested_at, requested_by, completed_at, completed_by
            from patient_record_requests
            where patient_id = @patientId
            order by requested_at desc, request_id desc;
            """;
        command.Parameters.AddWithValue("patientId", patient.CanonicalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var requests = new List<PatientRecordRequestResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            requests.Add(ToResponse(reader));
        }

        return requests;
    }

    public async Task<PatientRecordRequestResponse> CreateAsync(string patientId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken, transaction)
            ?? throw new ArgumentException("The patient does not exist.");

        var requestId = Guid.NewGuid();
        try
        {
            PatientRecordRequestResponse response;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into patient_record_requests (request_id, patient_id, pid, requested_at, requested_by)
                    values (@requestId, @patientId, @pid, now(), @username)
                    returning request_id, patient_id, pid, requested_at, requested_by, completed_at, completed_by;
                    """;
                command.Parameters.AddWithValue("requestId", requestId);
                command.Parameters.AddWithValue("patientId", patient.CanonicalId);
                command.Parameters.AddWithValue("pid", patient.LegacyPid);
                command.Parameters.AddWithValue("username", username);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("The record request could not be created.");
                }

                response = ToResponse(reader);
            }

            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("There is already an open patient record request.");
        }
    }

    public async Task<PatientRecordRequestResponse> CompleteAsync(string patientId, Guid requestId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var patient = await ResolvePatientAsync(connection, patientId, cancellationToken, transaction)
            ?? throw new ArgumentException("The patient does not exist.");

        PatientRecordRequestResponse response;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update patient_record_requests
                set completed_at = now(), completed_by = @username
                where request_id = @requestId
                  and patient_id = @patientId
                  and completed_at is null
                returning request_id, patient_id, pid, requested_at, requested_by, completed_at, completed_by;
                """;
            command.Parameters.AddWithValue("requestId", requestId);
            command.Parameters.AddWithValue("patientId", patient.CanonicalId);
            command.Parameters.AddWithValue("username", username);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Only an open patient record request can be completed.");
            }

            response = ToResponse(reader);
        }

        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static async Task<PatientIdentity?> ResolvePatientAsync(NpgsqlConnection connection, string patientId, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select canonical_id, legacy_pid
            from patients
            where (lower(canonical_id) = lower(@patientId)
                or lower(pubpid) = lower(@patientId)
                or legacy_pid::text = @patientId)
              and merged_into_patient_id is null
            for update;
            """;
        command.Parameters.AddWithValue("patientId", patientId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PatientIdentity(reader.GetString(0), reader.GetInt32(1))
            : null;
    }

    private static PatientRecordRequestResponse ToResponse(NpgsqlDataReader reader)
    {
        var completedAt = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5).ToString("O");
        return new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            completedAt is null ? "Open" : "Completed",
            reader.GetFieldValue<DateTimeOffset>(3).ToString("O"),
            reader.GetString(4),
            completedAt,
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private sealed record PatientIdentity(string CanonicalId, int LegacyPid);
}
