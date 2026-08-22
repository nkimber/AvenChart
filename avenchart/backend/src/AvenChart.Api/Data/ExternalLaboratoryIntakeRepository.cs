// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvenChart.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace AvenChart.Api.Data;

/// <summary>
/// Applies the deliberately narrow FHIR R4 DiagnosticReport/Observation intake
/// profile used for synthetic external laboratories.  It owns the transaction
/// from source message identity through clinical persistence so a receipt can
/// never claim a result was applied when its patient, order, specimen, report,
/// result links, and history were not committed together.
/// </summary>
public sealed class ExternalLaboratoryIntakeRepository(NpgsqlDataSource dataSource)
{
    public async Task<ExternalLaboratoryIntakeReceipt> ReceiveAsync(
        ExternalLaboratorySourceAuthentication source,
        string? sourceMessageId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var messageId = NormalizeMessageId(sourceMessageId);
        var bundle = ExternalLaboratoryFhirBundle.Parse(payload);
        var rawPayload = payload.GetRawText();
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload));

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireMessageLockAsync(connection, transaction, source.SourceId, messageId, cancellationToken);

        var existing = await GetExistingIngestionAsync(connection, transaction, source.SourceId, messageId, cancellationToken);
        if (existing is not null)
        {
            if (CryptographicOperations.FixedTimeEquals(existing.PayloadHash, payloadHash))
            {
                await InsertIngestionEventAsync(connection, transaction, existing.IngestionId, "duplicate", "Exact source message replay accepted without clinical mutation.", cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new ExternalLaboratoryIntakeReceipt(
                    existing.IngestionId, source.SourceId, messageId, existing.Status,
                    Duplicate: true, Conflict: false, Rejected: existing.Status == "rejected",
                    existing.RejectionReason, existing.ReportId, existing.CreatedResultCount,
                    existing.UpdatedResultCount, DateTimeOffset.UtcNow.ToString("O"));
            }

            await InsertIngestionEventAsync(connection, transaction, existing.IngestionId, "replay-conflict", "The source message identifier was reused with different payload content.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ExternalLaboratoryIntakeReceipt(
                existing.IngestionId, source.SourceId, messageId, existing.Status,
                Duplicate: false, Conflict: true, Rejected: false,
                "The source message identifier is already bound to different payload content.", existing.ReportId,
                existing.CreatedResultCount, existing.UpdatedResultCount, DateTimeOffset.UtcNow.ToString("O"));
        }

        var context = await ResolveClinicalContextAsync(connection, transaction, bundle, cancellationToken);
        var rejectionReason = context is null
            ? "The FHIR laboratory bundle did not resolve to the referenced patient, order, and received specimen."
            : !source.FacilityIds.Contains(context.FacilityId)
                ? "The authenticated external laboratory source is not authorized for the facility that owns the referenced order."
                : null;
        if (rejectionReason is not null)
        {
            var ingestionId = Guid.NewGuid();
            await InsertIngestionAsync(
                connection, transaction, ingestionId, source.SourceId, messageId, rawPayload, payloadHash,
                "rejected", rejectionReason, context?.PatientId, context?.OrderId, context?.SpecimenId, null, 0, 0, cancellationToken);
            await InsertIngestionEventAsync(connection, transaction, ingestionId, "received", "Authenticated FHIR R4 laboratory message received.", cancellationToken);
            await InsertIngestionEventAsync(connection, transaction, ingestionId, "rejected", rejectionReason, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ExternalLaboratoryIntakeReceipt(
                ingestionId, source.SourceId, messageId, "rejected", Duplicate: false,
                Conflict: false, Rejected: true, rejectionReason, null, 0, 0, DateTimeOffset.UtcNow.ToString("O"));
        }

        var report = await GetOrCreateReportAsync(connection, transaction, source, bundle, context!, cancellationToken);
        var created = 0;
        var updated = 0;
        foreach (var observation in bundle.Observations)
        {
            var resultMutation = await UpsertResultAsync(connection, transaction, source, report.ReportId, observation, cancellationToken);
            created += resultMutation.Created ? 1 : 0;
            updated += resultMutation.Updated ? 1 : 0;
        }

        var reportChanged = report.Existing && (updated > 0 || !report.Matches(bundle));
        if (report.Existing)
        {
            if (reportChanged)
            {
                await CorrectReportAsync(connection, transaction, report, bundle, source.SourceId, cancellationToken);
            }
        }

        var newIngestionId = Guid.NewGuid();
        await InsertIngestionAsync(
            connection, transaction, newIngestionId, source.SourceId, messageId, rawPayload, payloadHash,
            "applied", null, context.PatientId, context.OrderId, context.SpecimenId, report.ReportId,
            created, updated, cancellationToken);
        await InsertIngestionEventAsync(connection, transaction, newIngestionId, "received", "Authenticated FHIR R4 laboratory message received.", cancellationToken);
        await InsertIngestionEventAsync(connection, transaction, newIngestionId, "applied", $"Applied DiagnosticReport/{bundle.ReportId} with {created} created and {updated} corrected result(s).", cancellationToken);
        if (updated > 0 || reportChanged)
        {
            await InsertIngestionEventAsync(connection, transaction, newIngestionId, "correction", "Inbound message corrected a report or one or more linked result values; prior values remain retained.", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        return new ExternalLaboratoryIntakeReceipt(
            newIngestionId, source.SourceId, messageId, "applied", Duplicate: false,
            Conflict: false, Rejected: false, null, report.ReportId, created, updated, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static async Task AcquireMessageLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId,
        string messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select pg_advisory_xact_lock(hashtext(@messageKey));";
        command.Parameters.AddWithValue("messageKey", $"external-laboratory:{sourceId}:{messageId}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ExternalIngestionRow?> GetExistingIngestionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceId,
        string messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select ingestion_id,payload_hash,status,rejection_reason,report_id,created_result_count,updated_result_count
            from external_laboratory_ingestions
            where source_id=@sourceId and source_message_id=@messageId
            for update;
            """;
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("messageId", messageId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ExternalIngestionRow(
            reader.GetGuid(0), reader.GetFieldValue<byte[]>(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    private static async Task<ExternalClinicalContext?> ResolveClinicalContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExternalLaboratoryFhirBundle bundle,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select patient.canonical_id, orders.id, specimens.id, encounters.facility_id,
                   coalesce(nullif(btrim(specimens.accession_identifier), ''), nullif(btrim(specimens.specimen_identifier), ''), concat('Specimen ', specimens.id))
            from patients patient
            inner join lab_orders orders on orders.id=@orderId and orders.pid=patient.legacy_pid
            inner join encounters on encounters.encounter=orders.encounter and encounters.pid=orders.pid and encounters.facility_id is not null
            inner join lab_specimens specimens on specimens.id=@specimenId and specimens.order_id=orders.id and specimens.specimen_status='received'
            where patient.canonical_id=@patientReference or patient.pubpid=@patientReference
            for update of patient,orders,specimens;
            """;
        command.Parameters.AddWithValue("patientReference", bundle.PatientReference);
        command.Parameters.AddWithValue("orderId", bundle.OrderId);
        command.Parameters.AddWithValue("specimenId", bundle.SpecimenId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ExternalClinicalContext(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetString(4));
    }

    private static async Task<ExternalReportContext> GetOrCreateReportAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExternalLaboratorySourceAuthentication source,
        ExternalLaboratoryFhirBundle bundle,
        ExternalClinicalContext context,
        CancellationToken cancellationToken)
    {
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = """
                select reports.id,reports.date_collected,reports.report_date,reports.status,
                       coalesce(reports.review_status,'received'),reports.review_version
                from external_laboratory_report_links links
                inner join lab_reports reports on reports.id=links.report_id
                where links.source_id=@sourceId and links.external_report_id=@externalReportId
                for update of reports;
                """;
            existing.Parameters.AddWithValue("sourceId", source.SourceId);
            existing.Parameters.AddWithValue("externalReportId", bundle.ReportId);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new ExternalReportContext(
                    reader.GetInt32(0), Existing: true, reader.GetDateTime(1), reader.GetDateTime(2),
                    reader.GetString(3), reader.GetString(4), reader.GetInt32(5), context.SpecimenIdentifier);
            }
        }

        int reportId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into lab_reports
                    (order_id,specimen_id,date_collected,report_date,specimen_number,status,review_status,reviewed_by,reviewed_at,review_version,notes)
                values
                    (@orderId,@specimenId,@collectedAt,@reportedAt,@specimenNumber,@status,'received',null,null,1,@notes)
                returning id;
                """;
            insert.Parameters.AddWithValue("orderId", context.OrderId);
            insert.Parameters.AddWithValue("specimenId", context.SpecimenId);
            insert.Parameters.Add("collectedAt", NpgsqlDbType.Timestamp).Value = ToDatabaseTimestamp(bundle.CollectedAt);
            insert.Parameters.Add("reportedAt", NpgsqlDbType.Timestamp).Value = ToDatabaseTimestamp(bundle.ReportedAt);
            insert.Parameters.AddWithValue("specimenNumber", context.SpecimenIdentifier);
            insert.Parameters.AddWithValue("status", bundle.ReportStatus);
            insert.Parameters.AddWithValue("notes", $"External FHIR R4 DiagnosticReport/{bundle.ReportId} received from source {source.SourceId}.");
            reportId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        await using (var link = connection.CreateCommand())
        {
            link.Transaction = transaction;
            link.CommandText = """
                insert into external_laboratory_report_links(source_id,external_report_id,report_id,linked_at)
                values(@sourceId,@externalReportId,@reportId,now());
                """;
            link.Parameters.AddWithValue("sourceId", source.SourceId);
            link.Parameters.AddWithValue("externalReportId", bundle.ReportId);
            link.Parameters.AddWithValue("reportId", reportId);
            await link.ExecuteNonQueryAsync(cancellationToken);
        }
        await InsertReportReviewEventAsync(connection, transaction, reportId, "external-received", null, "received", null,
            $"external-laboratory:{source.SourceId}", "Initial external FHIR R4 laboratory report received; clinician review remains required.", 0, 1, cancellationToken);
        return new ExternalReportContext(reportId, Existing: false, ToDatabaseTimestamp(bundle.CollectedAt), ToDatabaseTimestamp(bundle.ReportedAt), bundle.ReportStatus, "received", 1, context.SpecimenIdentifier);
    }

    private static async Task<ResultMutation> UpsertResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExternalLaboratorySourceAuthentication source,
        int reportId,
        ExternalLaboratoryObservation observation,
        CancellationToken cancellationToken)
    {
        ExternalResultContext? existing = null;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                select results.id,results.report_id,results.code,results.text,results.units,results.result,results.range,results.abnormal,results.result_date,results.result_status
                from external_laboratory_result_links links
                inner join lab_results results on results.id=links.result_id
                where links.source_id=@sourceId and links.external_result_id=@externalResultId
                for update of results;
                """;
            select.Parameters.AddWithValue("sourceId", source.SourceId);
            select.Parameters.AddWithValue("externalResultId", observation.Id);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existing = new ExternalResultContext(
                    reader.GetInt32(0), reader.GetInt32(1), ReadNullable(reader, 2), ReadNullable(reader, 3), ReadNullable(reader, 4),
                    ReadNullable(reader, 5), ReadNullable(reader, 6), ReadNullable(reader, 7), reader.GetDateTime(8), ReadNullable(reader, 9));
            }
        }

        if (existing is null)
        {
            int resultId;
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    insert into lab_results(report_id,code,text,units,result,range,abnormal,result_date,result_status)
                    values(@reportId,@code,@text,@units,@result,@range,@abnormal,@resultDate,@status)
                    returning id;
                    """;
                insert.Parameters.AddWithValue("reportId", reportId);
                ConfigureResultParameters(insert, observation);
                resultId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            }
            await using (var link = connection.CreateCommand())
            {
                link.Transaction = transaction;
                link.CommandText = """
                    insert into external_laboratory_result_links(source_id,external_result_id,result_id,linked_at)
                    values(@sourceId,@externalResultId,@resultId,now());
                    """;
                link.Parameters.AddWithValue("sourceId", source.SourceId);
                link.Parameters.AddWithValue("externalResultId", observation.Id);
                link.Parameters.AddWithValue("resultId", resultId);
                await link.ExecuteNonQueryAsync(cancellationToken);
            }
            return new ResultMutation(Created: true, Updated: false);
        }

        if (existing.ReportId != reportId)
        {
            throw new ExternalLaboratoryFhirValidationException("invalid", $"Observation/{observation.Id} is already linked to a different DiagnosticReport.");
        }
        if (existing.Matches(observation)) return new ResultMutation(Created: false, Updated: false);

        await SnapshotProcedureResultAsync(connection, transaction, existing.ResultId, cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update lab_results
                set code=@code,text=@text,units=@units,result=@result,range=@range,abnormal=@abnormal,result_date=@resultDate,result_status=@status
                where id=@resultId;
                """;
            ConfigureResultParameters(update, observation);
            update.Parameters.AddWithValue("resultId", existing.ResultId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await ReopenCriticalAcknowledgementAsync(connection, transaction, existing.ResultId, observation.Abnormal, source.SourceId, cancellationToken);
        return new ResultMutation(Created: false, Updated: true);
    }

    private static void ConfigureResultParameters(NpgsqlCommand command, ExternalLaboratoryObservation observation)
    {
        command.Parameters.AddWithValue("code", observation.Code);
        command.Parameters.AddWithValue("text", observation.Text);
        command.Parameters.AddWithValue("units", observation.Units ?? string.Empty);
        command.Parameters.AddWithValue("result", observation.Result);
        command.Parameters.AddWithValue("range", observation.Range ?? string.Empty);
        command.Parameters.AddWithValue("abnormal", observation.Abnormal ?? string.Empty);
        command.Parameters.Add("resultDate", NpgsqlDbType.Timestamp).Value = ToDatabaseTimestamp(observation.EffectiveAt);
        command.Parameters.AddWithValue("status", observation.Status);
    }

    private static async Task CorrectReportAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExternalReportContext report,
        ExternalLaboratoryFhirBundle bundle,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var nextVersion = report.ReviewVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update lab_reports
                set date_collected=@collectedAt,report_date=@reportedAt,status=@status,
                    review_status='received',reviewed_by=null,reviewed_at=null,review_version=@reviewVersion
                where id=@reportId and review_version=@expectedVersion;
                """;
            update.Parameters.Add("collectedAt", NpgsqlDbType.Timestamp).Value = ToDatabaseTimestamp(bundle.CollectedAt);
            update.Parameters.Add("reportedAt", NpgsqlDbType.Timestamp).Value = ToDatabaseTimestamp(bundle.ReportedAt);
            update.Parameters.AddWithValue("status", bundle.ReportStatus);
            update.Parameters.AddWithValue("reviewVersion", nextVersion);
            update.Parameters.AddWithValue("reportId", report.ReportId);
            update.Parameters.AddWithValue("expectedVersion", report.ReviewVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("The laboratory report review state changed while an external correction was being applied.");
            }
        }
        await InsertReportReviewEventAsync(connection, transaction, report.ReportId, "external-correction", report.ReviewStatus, "received", null,
            $"external-laboratory:{sourceId}", "External FHIR R4 report/result correction received; clinician review was reopened.", report.ReviewVersion, nextVersion, cancellationToken);
    }

    private static async Task SnapshotProcedureResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int resultId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into procedure_result_versions(result_id,version_no,captured_at,code,text,units,result,range,abnormal,result_date,result_status)
            select results.id,
                   avenchart_next_integer(concat('procedure_result_versions.version:', results.id),coalesce((select max(history.version_no) from procedure_result_versions history where history.result_id=results.id),0)),
                   current_timestamp,results.code,results.text,results.units,results.result,results.range,results.abnormal,results.result_date,results.result_status
            from lab_results results
            where results.id=@resultId;
            """;
        command.Parameters.AddWithValue("resultId", resultId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReopenCriticalAcknowledgementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int resultId,
        string? abnormal,
        string sourceId,
        CancellationToken cancellationToken)
    {
        if (!IsCritical(abnormal)) return;
        string? priorStatus = null;
        var priorVersion = 0;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "select status,version from critical_lab_result_acknowledgements where result_id=@resultId for update;";
            select.Parameters.AddWithValue("resultId", resultId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                priorStatus = reader.GetString(0);
                priorVersion = reader.GetInt32(1);
            }
        }
        if (!string.Equals(priorStatus, "acknowledged", StringComparison.Ordinal)) return;

        var nextVersion = priorVersion + 1;
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update critical_lab_result_acknowledgements
                set status='open',version=@nextVersion,acknowledged_by=null,acknowledged_at=null,acknowledgement_reason=null
                where result_id=@resultId and status='acknowledged' and version=@priorVersion;
                """;
            update.Parameters.AddWithValue("nextVersion", nextVersion);
            update.Parameters.AddWithValue("resultId", resultId);
            update.Parameters.AddWithValue("priorVersion", priorVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("The critical-result acknowledgement changed while an external correction was being applied.");
            }
        }
        await using var history = connection.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = """
            insert into critical_lab_result_acknowledgement_events(result_id,action,previous_status,current_status,actor,reason,expected_version,resulting_version,occurred_at)
            values(@resultId,'reopened','acknowledged','open',@actor,@reason,@expectedVersion,@resultingVersion,current_timestamp);
            """;
        history.Parameters.AddWithValue("resultId", resultId);
        history.Parameters.AddWithValue("actor", $"external-laboratory:{sourceId}");
        history.Parameters.AddWithValue("reason", "A corrected critical external laboratory result requires a fresh acknowledgement.");
        history.Parameters.AddWithValue("expectedVersion", priorVersion);
        history.Parameters.AddWithValue("resultingVersion", nextVersion);
        await history.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIngestionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid ingestionId,
        string sourceId,
        string messageId,
        string payload,
        byte[] payloadHash,
        string status,
        string? rejectionReason,
        string? patientId,
        int? orderId,
        int? specimenId,
        int? reportId,
        int createdResultCount,
        int updatedResultCount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into external_laboratory_ingestions
                (ingestion_id,source_id,source_message_id,fhir_version,payload,payload_hash,status,rejection_reason,patient_id,order_id,specimen_id,report_id,created_result_count,updated_result_count,received_at,processed_at)
            values
                (@ingestionId,@sourceId,@messageId,'4.0.1',@payload,@payloadHash,@status,@rejectionReason,@patientId,@orderId,@specimenId,@reportId,@createdCount,@updatedCount,now(),now());
            """;
        command.Parameters.AddWithValue("ingestionId", ingestionId);
        command.Parameters.AddWithValue("sourceId", sourceId);
        command.Parameters.AddWithValue("messageId", messageId);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = payload;
        command.Parameters.Add("payloadHash", NpgsqlDbType.Bytea).Value = payloadHash;
        command.Parameters.AddWithValue("status", status);
        command.Parameters.Add("rejectionReason", NpgsqlDbType.Text).Value = (object?)rejectionReason ?? DBNull.Value;
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = (object?)patientId ?? DBNull.Value;
        command.Parameters.Add("orderId", NpgsqlDbType.Integer).Value = (object?)orderId ?? DBNull.Value;
        command.Parameters.Add("specimenId", NpgsqlDbType.Integer).Value = (object?)specimenId ?? DBNull.Value;
        command.Parameters.Add("reportId", NpgsqlDbType.Integer).Value = (object?)reportId ?? DBNull.Value;
        command.Parameters.AddWithValue("createdCount", createdResultCount);
        command.Parameters.AddWithValue("updatedCount", updatedResultCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIngestionEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid ingestionId, string action, string? detail, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into external_laboratory_ingestion_events(ingestion_id,action,detail,occurred_at) values(@ingestionId,@action,@detail,now());";
        command.Parameters.AddWithValue("ingestionId", ingestionId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertReportReviewEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int reportId, string action, string? previousStatus, string currentStatus, string? assignedTo, string actor, string reason, int expectedVersion, int resultingVersion, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into lab_report_review_events(report_id,action,previous_status,current_status,assigned_to,actor,reason,expected_version,resulting_version,occurred_at)
            values(@reportId,@action,@previousStatus,@currentStatus,@assignedTo,@actor,@reason,@expectedVersion,@resultingVersion,current_timestamp);
            """;
        command.Parameters.AddWithValue("reportId", reportId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.Add("previousStatus", NpgsqlDbType.Text).Value = (object?)previousStatus ?? DBNull.Value;
        command.Parameters.AddWithValue("currentStatus", currentStatus);
        command.Parameters.Add("assignedTo", NpgsqlDbType.Text).Value = (object?)assignedTo ?? DBNull.Value;
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("expectedVersion", expectedVersion);
        command.Parameters.AddWithValue("resultingVersion", resultingVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeMessageId(string? value)
    {
        var messageId = value?.Trim();
        if (string.IsNullOrWhiteSpace(messageId) || messageId.Length is < 3 or > 160 || !messageId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-'))
        {
            throw new ExternalLaboratoryFhirValidationException("invalid", "X-AvenChart-Lab-Message-Id must contain 3-160 letters, digits, '.', '_', ':', or '-'.");
        }
        return messageId;
    }

    private static DateTime ToDatabaseTimestamp(DateTimeOffset value) => DateTime.SpecifyKind(value.UtcDateTime, DateTimeKind.Unspecified);
    private static string? ReadNullable(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static bool IsCritical(string? abnormal) => abnormal?.Trim().ToLowerInvariant() is "critical" or "panic" or "hh" or "ll";

    private sealed record ExternalIngestionRow(Guid IngestionId, byte[] PayloadHash, string Status, string? RejectionReason, int? ReportId, int CreatedResultCount, int UpdatedResultCount);
    private sealed record ExternalClinicalContext(string PatientId, int OrderId, int SpecimenId, int FacilityId, string SpecimenIdentifier);
    private sealed record ExternalReportContext(int ReportId, bool Existing, DateTime CollectedAt, DateTime ReportedAt, string Status, string ReviewStatus, int ReviewVersion, string SpecimenIdentifier)
    {
        public bool Matches(ExternalLaboratoryFhirBundle bundle) =>
            CollectedAt == ToDatabaseTimestamp(bundle.CollectedAt)
            && ReportedAt == ToDatabaseTimestamp(bundle.ReportedAt)
            && string.Equals(Status, bundle.ReportStatus, StringComparison.OrdinalIgnoreCase);
    }
    private sealed record ExternalResultContext(int ResultId, int ReportId, string? Code, string? Text, string? Units, string? Result, string? Range, string? Abnormal, DateTime ResultDate, string? Status)
    {
        public bool Matches(ExternalLaboratoryObservation observation) =>
            string.Equals(Code, observation.Code, StringComparison.Ordinal)
            && string.Equals(Text, observation.Text, StringComparison.Ordinal)
            && string.Equals(Units ?? string.Empty, observation.Units ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(Result, observation.Result, StringComparison.Ordinal)
            && string.Equals(Range ?? string.Empty, observation.Range ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(Abnormal ?? string.Empty, observation.Abnormal ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && ResultDate == ToDatabaseTimestamp(observation.EffectiveAt)
            && string.Equals(Status, observation.Status, StringComparison.OrdinalIgnoreCase);
    }
    private sealed record ResultMutation(bool Created, bool Updated);
}

public sealed class ExternalLaboratoryFhirValidationException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}

internal sealed record ExternalLaboratoryFhirBundle(
    string ReportId,
    string ReportStatus,
    string PatientReference,
    int OrderId,
    int SpecimenId,
    DateTimeOffset CollectedAt,
    DateTimeOffset ReportedAt,
    IReadOnlyList<ExternalLaboratoryObservation> Observations)
{
    public static ExternalLaboratoryFhirBundle Parse(JsonElement root)
    {
        RequireResource(root, "Bundle", "The request body must be a FHIR R4 Bundle.");
        if (!string.Equals(ReadRequiredString(root, "type", "Bundle.type is required."), "collection", StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid("The external laboratory profile accepts Bundle.type 'collection' only.");
        }
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 2 or > 101)
        {
            throw Invalid("Bundle.entry must contain the DiagnosticReport and between one and 100 referenced Observations.");
        }

        var resources = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        JsonElement? report = null;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("Every Bundle.entry must contain a resource object.");
            }
            var resourceType = ReadRequiredString(resource, "resourceType", "Each Bundle resource requires resourceType.");
            var id = ReadResourceId(resource, resourceType);
            var key = $"{resourceType}/{id}";
            if (!resources.TryAdd(key, resource)) throw Invalid($"Bundle contains duplicate resource {key}.");
            if (string.Equals(resourceType, "DiagnosticReport", StringComparison.Ordinal))
            {
                if (report is not null) throw Invalid("Bundle must contain exactly one DiagnosticReport.");
                report = resource;
            }
        }
        if (report is null) throw Invalid("Bundle must contain exactly one DiagnosticReport.");

        var diagnosticReport = report.Value;
        var reportId = ReadResourceId(diagnosticReport, "DiagnosticReport");
        var reportStatus = ReadAllowedStatus(diagnosticReport, "DiagnosticReport");
        var patientReference = ParseReference(RequireNestedReference(diagnosticReport, "subject", "Patient", "DiagnosticReport.subject must reference Patient/{id}."), "Patient", "DiagnosticReport.subject");
        var orderId = ParsePositiveIntReference(FirstReference(diagnosticReport, "basedOn", "ServiceRequest", "DiagnosticReport.basedOn must reference ServiceRequest/{orderId}."), "ServiceRequest", "DiagnosticReport.basedOn");
        var specimenId = ParsePositiveIntReference(FirstReference(diagnosticReport, "specimen", "Specimen", "DiagnosticReport.specimen must reference Specimen/{specimenId}."), "Specimen", "DiagnosticReport.specimen");
        var collectedAt = ReadDateTime(diagnosticReport, "effectiveDateTime", "DiagnosticReport.effectiveDateTime is required.");
        var reportedAt = ReadDateTime(diagnosticReport, "issued", "DiagnosticReport.issued is required.");

        if (!diagnosticReport.TryGetProperty("result", out var resultReferences) || resultReferences.ValueKind != JsonValueKind.Array || resultReferences.GetArrayLength() is < 1 or > 100)
        {
            throw Invalid("DiagnosticReport.result must reference between one and 100 Observations.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var observations = new List<ExternalLaboratoryObservation>();
        foreach (var referenceElement in resultReferences.EnumerateArray())
        {
            var reference = RequireReferenceValue(referenceElement, "reference", "Observation", "DiagnosticReport.result references must use Observation/{id}.");
            var observationId = ParseReference(reference, "Observation", "DiagnosticReport.result");
            if (!seen.Add(observationId)) throw Invalid("DiagnosticReport.result must not reference an Observation more than once.");
            if (!resources.TryGetValue($"Observation/{observationId}", out var observation)) throw Invalid($"DiagnosticReport.result references Observation/{observationId}, which is absent from the Bundle.");
            observations.Add(ParseObservation(observation, patientReference));
        }
        return new ExternalLaboratoryFhirBundle(reportId, reportStatus, patientReference, orderId, specimenId, collectedAt, reportedAt, observations);
    }

    private static ExternalLaboratoryObservation ParseObservation(JsonElement observation, string patientReference)
    {
        RequireResource(observation, "Observation", "DiagnosticReport.result must resolve to Observation resources.");
        var id = ReadResourceId(observation, "Observation");
        var status = ReadAllowedStatus(observation, "Observation");
        var subject = ParseReference(RequireNestedReference(observation, "subject", "Patient", "Observation.subject must reference Patient/{id}."), "Patient", "Observation.subject");
        if (!string.Equals(subject, patientReference, StringComparison.Ordinal)) throw Invalid($"Observation/{id} references a different patient than the DiagnosticReport.");

        if (!observation.TryGetProperty("code", out var codeConcept) || codeConcept.ValueKind != JsonValueKind.Object || !codeConcept.TryGetProperty("coding", out var coding) || coding.ValueKind != JsonValueKind.Array)
        {
            throw Invalid($"Observation/{id}.code requires a LOINC coding.");
        }
        string? code = null;
        string? text = null;
        foreach (var item in coding.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var system = ReadOptionalString(item, "system");
            var candidate = ReadOptionalString(item, "code");
            if (string.Equals(system, "http://loinc.org", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate))
            {
                code = RequireBounded(candidate, "Observation LOINC code", 64);
                text = ReadOptionalString(item, "display");
                break;
            }
        }
        if (code is null) throw Invalid($"Observation/{id}.code requires a coding with system http://loinc.org and a code.");
        text = RequireBounded(text ?? ReadOptionalString(codeConcept, "text") ?? code, "Observation display text", 500);
        var effectiveAt = observation.TryGetProperty("effectiveDateTime", out _) ? ReadDateTime(observation, "effectiveDateTime", $"Observation/{id}.effectiveDateTime is invalid.") : ReadDateTime(observation, "issued", $"Observation/{id} requires effectiveDateTime or issued.");
        var (value, units) = ReadObservationValue(observation, id);
        var range = ReadReferenceRange(observation);
        var abnormal = ReadInterpretation(observation);
        return new ExternalLaboratoryObservation(id, code, text, value, units, range, abnormal, effectiveAt, status);
    }

    private static (string Value, string? Units) ReadObservationValue(JsonElement observation, string id)
    {
        if (observation.TryGetProperty("valueQuantity", out var quantity) && quantity.ValueKind == JsonValueKind.Object)
        {
            if (!quantity.TryGetProperty("value", out var rawValue) || rawValue.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)) throw Invalid($"Observation/{id}.valueQuantity.value is required.");
            var value = rawValue.ValueKind == JsonValueKind.Number ? rawValue.GetRawText() : rawValue.GetString();
            return (RequireBounded(value, "Observation value", 120), ReadOptionalString(quantity, "unit") is { } unit ? RequireBounded(unit, "Observation unit", 64) : null);
        }
        if (observation.TryGetProperty("valueString", out var valueString) && valueString.ValueKind == JsonValueKind.String)
        {
            return (RequireBounded(valueString.GetString(), "Observation value", 120), null);
        }
        throw Invalid($"Observation/{id} requires valueQuantity or valueString.");
    }

    private static string? ReadReferenceRange(JsonElement observation)
    {
        if (!observation.TryGetProperty("referenceRange", out var ranges) || ranges.ValueKind != JsonValueKind.Array || ranges.GetArrayLength() == 0) return null;
        var first = ranges[0];
        if (first.ValueKind != JsonValueKind.Object) return null;
        var text = ReadOptionalString(first, "text");
        if (!string.IsNullOrWhiteSpace(text)) return RequireBounded(text, "Observation reference range", 160);
        var low = ReadQuantityValue(first, "low");
        var high = ReadQuantityValue(first, "high");
        return low is null && high is null ? null : $"{low ?? string.Empty}-{high ?? string.Empty}";
    }

    private static string? ReadQuantityValue(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var quantity) || quantity.ValueKind != JsonValueKind.Object || !quantity.TryGetProperty("value", out var value)) return null;
        return value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? ReadInterpretation(JsonElement observation)
    {
        if (!observation.TryGetProperty("interpretation", out var interpretations) || interpretations.ValueKind != JsonValueKind.Array) return null;
        foreach (var concept in interpretations.EnumerateArray())
        {
            if (!concept.TryGetProperty("coding", out var coding) || coding.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in coding.EnumerateArray())
            {
                var code = ReadOptionalString(item, "code");
                if (string.IsNullOrWhiteSpace(code)) continue;
                var normalized = code.Trim().ToUpperInvariant();
                return normalized is "HH" or "LL" or "AA" or "CRITICAL" or "PANIC" ? "critical" : normalized.ToLowerInvariant();
            }
        }
        return null;
    }

    private static void RequireResource(JsonElement resource, string expectedResourceType, string message)
    {
        if (!string.Equals(ReadOptionalString(resource, "resourceType"), expectedResourceType, StringComparison.Ordinal)) throw Invalid(message);
    }
    private static string ReadResourceId(JsonElement resource, string resourceType) => RequireBounded(ReadOptionalString(resource, "id"), $"{resourceType}.id", 120);
    private static string ReadAllowedStatus(JsonElement resource, string resourceType)
    {
        var status = RequireBounded(ReadOptionalString(resource, "status"), $"{resourceType}.status", 32).ToLowerInvariant();
        return status is "final" or "amended" or "corrected" ? status : throw Invalid($"{resourceType}.status must be final, amended, or corrected.");
    }
    private static string RequireNestedReference(JsonElement objectElement, string property, string expectedType, string message)
    {
        if (!objectElement.TryGetProperty(property, out var reference) || reference.ValueKind != JsonValueKind.Object) throw Invalid(message);
        return RequireReferenceValue(reference, "reference", expectedType, message);
    }
    private static string RequireReferenceValue(JsonElement objectElement, string property, string expectedType, string message)
    {
        var reference = ReadOptionalString(objectElement, property);
        if (string.IsNullOrWhiteSpace(reference)) throw Invalid(message);
        _ = ParseReference(reference, expectedType, property);
        return reference;
    }
    private static string FirstReference(JsonElement resource, string property, string expectedType, string message)
    {
        if (!resource.TryGetProperty(property, out var references) || references.ValueKind != JsonValueKind.Array || references.GetArrayLength() == 0) throw Invalid(message);
        return RequireReferenceValue(references[0], "reference", expectedType, message);
    }
    private static string ParseReference(string reference, string expectedType, string field)
    {
        var prefix = expectedType + "/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal) || reference.Length <= prefix.Length) throw Invalid($"{field} must be a relative {expectedType}/{{id}} reference.");
        return RequireBounded(reference[prefix.Length..], $"{field} identifier", 120);
    }
    private static int ParsePositiveIntReference(string reference, string expectedType, string field)
    {
        var id = ParseReference(reference, expectedType, field);
        return int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : throw Invalid($"{field} must reference a positive integer {expectedType} identifier.");
    }
    private static DateTimeOffset ReadDateTime(JsonElement resource, string property, string message)
    {
        var raw = ReadOptionalString(resource, property);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : throw Invalid(message);
    }
    private static string ReadRequiredString(JsonElement resource, string property, string message) => ReadOptionalString(resource, property) is { Length: > 0 } value ? value : throw Invalid(message);
    private static string? ReadOptionalString(JsonElement resource, string property) => resource.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
    private static string RequireBounded(string? value, string field, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength) throw Invalid($"{field} is required and may not exceed {maximumLength} characters.");
        return normalized;
    }
    private static ExternalLaboratoryFhirValidationException Invalid(string message) => new("invalid", message);
}

internal sealed record ExternalLaboratoryObservation(string Id, string Code, string Text, string Result, string? Units, string? Range, string? Abnormal, DateTimeOffset EffectiveAt, string Status);
