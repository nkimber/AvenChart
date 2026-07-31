// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class BillingRepository(NpgsqlDataSource dataSource)
{
    private static readonly DateOnly ClaimScrubBusinessDate = new(2026, 6, 18);
    private static readonly HashSet<string> AllowedClaimModifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "25",
        "59",
        "76",
        "77",
        "95"
    };

    public async Task<PatientBillingResponse?> GetForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, patientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var encounters = await GetBillingEncountersAsync(connection, patient.LegacyPid, cancellationToken);
        var encounterNumbers = encounters.Select(encounter => encounter.Encounter).ToArray();
        var lines = await GetBillingLinesAsync(connection, patient.LegacyPid, encounterNumbers, cancellationToken);
        var claims = await GetClaimsAsync(connection, patient.LegacyPid, encounterNumbers, cancellationToken);
        var payments = await GetPaymentsAsync(connection, patient.LegacyPid, encounterNumbers, cancellationToken);
        var linesByEncounter = lines.GroupBy(line => line.Encounter).ToDictionary(group => group.Key, group => group.ToList());
        var claimsByEncounter = claims.GroupBy(claim => claim.Encounter).ToDictionary(group => group.Key, group => group.ToList());
        var paymentsByEncounter = payments.GroupBy(payment => payment.Encounter).ToDictionary(group => group.Key, group => group.ToList());
        var encounterSummaries = encounters.Select(encounter =>
        {
            var encounterLines = linesByEncounter.GetValueOrDefault(encounter.Encounter, []);
            var encounterPayments = paymentsByEncounter.GetValueOrDefault(encounter.Encounter, []);
            var chargeAmount = encounterLines.Sum(line => line.Fee ?? 0m);
            var paymentAmount = encounterPayments.Sum(payment => payment.PayAmount);
            var adjustmentAmount = encounterPayments.Sum(payment => payment.AdjustmentAmount);
            var lastBillingDate = encounterLines
                .Select(line => ReadDateOnly(line.BillingDate, metadata.BaseDate))
                .DefaultIfEmpty(ReadDateOnly(encounter.Date, metadata.BaseDate))
                .Max();
            var ageDays = Math.Max(0, metadata.BaseDate.DayNumber - lastBillingDate.DayNumber);
            var agingBucket = AgingBucketLabel(ageDays);

            return encounter with
            {
                TotalFee = chargeAmount,
                PaymentAmount = paymentAmount,
                AdjustmentAmount = adjustmentAmount,
                BalanceAmount = chargeAmount - paymentAmount - adjustmentAmount,
                AgeDays = ageDays,
                AgingBucket = agingBucket,
                Lines = encounterLines,
                Claims = claimsByEncounter.GetValueOrDefault(encounter.Encounter, []),
                Payments = encounterPayments
            };
        }).ToList();
        var accountSummary = new BillingAccountSummary(
            ChargeAmount: encounterSummaries.Sum(encounter => encounter.TotalFee),
            PaymentAmount: encounterSummaries.Sum(encounter => encounter.PaymentAmount),
            AdjustmentAmount: encounterSummaries.Sum(encounter => encounter.AdjustmentAmount),
            BalanceAmount: encounterSummaries.Sum(encounter => encounter.BalanceAmount));
        var agingSummary = new BillingAgingSummary(
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            CurrentAmount: SumBalanceForBucket(encounterSummaries, "Current"),
            Days31To60Amount: SumBalanceForBucket(encounterSummaries, "31-60"),
            Days61To90Amount: SumBalanceForBucket(encounterSummaries, "61-90"),
            Over90Amount: SumBalanceForBucket(encounterSummaries, "Over 90"),
            TotalBalanceAmount: accountSummary.BalanceAmount);
        var ledgerEntries = BuildLedgerEntries(encounterSummaries, metadata.BaseDate);
        var ledgerSummary = new BillingLedgerSummary(
            EntryCount: ledgerEntries.Count,
            FirstEntryDate: ledgerEntries.FirstOrDefault()?.EntryDate,
            LastEntryDate: ledgerEntries.LastOrDefault()?.EntryDate,
            ChargeAmount: ledgerEntries.Where(entry => entry.EntryType == "Charge").Sum(entry => entry.Amount),
            PaymentAmount: -ledgerEntries
                .Where(entry => entry.EntryType is "Payment" or "Refund" or "Reversal")
                .Sum(entry => entry.Amount),
            AdjustmentAmount: -ledgerEntries
                .Where(entry => entry.EntryType is "Adjustment" or "Adjustment Reversal")
                .Sum(entry => entry.Amount),
            EndingBalanceAmount: ledgerEntries.LastOrDefault()?.RunningBalanceAmount ?? 0m);
        var statementSummary = BuildStatementSummary(
            patient,
            accountSummary,
            agingSummary,
            ledgerSummary,
            encounterSummaries,
            metadata.BaseDate);
        var statementDocument = BuildStatementDocument(
            patient,
            statementSummary,
            ledgerEntries);

        return new PatientBillingResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            PatientId: patient.PatientId,
            LegacyPid: patient.LegacyPid,
            Pubpid: patient.Pubpid,
            PatientDisplayName: patient.DisplayName,
            FirstName: patient.FirstName,
            LastName: patient.LastName,
            AccountSummary: accountSummary,
            AgingSummary: agingSummary,
            LedgerSummary: ledgerSummary,
            StatementSummary: statementSummary,
            StatementDocument: statementDocument,
            LedgerEntries: ledgerEntries,
            Encounters: encounterSummaries);
    }

    public async Task<(byte[] Content, string FileName)?> GetStatementPdfAsync(
        string patientId,
        CancellationToken cancellationToken)
    {
        var patientBilling = await GetForPatientAsync(patientId, cancellationToken);
        if (patientBilling is null)
        {
            return null;
        }

        var document = patientBilling.StatementDocument;
        return (
            Content: BuildStatementPdf(document),
            FileName: $"{document.StatementNumber}.pdf");
    }

    public async Task<(byte[] Content, string FileName, BillingPaymentReceiptDocument Document)?> GetPaymentReceiptPdfAsync(
        string activityId,
        CancellationToken cancellationToken)
    {
        var receipt = await GetPaymentReceiptDocumentAsync(activityId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        return (
            Content: BuildPaymentReceiptPdf(receipt),
            FileName: $"{receipt.ReceiptNumber}.pdf",
            Document: receipt);
    }

    public async Task<StatementBatchResponse> GetStatementBatchAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var metadata = await GetMetadataAsync(cancellationToken);
        var rollups = new List<StatementBatchRollupRow>();

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                with charges as (
                    select
                        pid,
                        encounter,
                        count(*) as line_count,
                        coalesce(sum(coalesce(fee, 0)), 0) as charge_amount,
                        max(billing_date) as last_billing_date
                    from billing
                    where activity = 1
                    group by pid, encounter
                ),
                payments as (
                    select
                        pid,
                        encounter,
                        count(*) as payment_count,
                        coalesce(sum(pay_amount), 0) as payment_amount,
                        coalesce(sum(adj_amount), 0) as adjustment_amount
                    from payment_activities
                    where deleted is null
                    group by pid, encounter
                ),
                aged as (
                    select
                        c.pid,
                        c.encounter,
                        c.line_count,
                        coalesce(p.payment_count, 0) as payment_count,
                        c.last_billing_date,
                        greatest((cast(@asOfDate as date) - c.last_billing_date)::int, 0) as age_days,
                        c.charge_amount - coalesce(p.payment_amount, 0) - coalesce(p.adjustment_amount, 0) as balance_amount
                    from charges c
                    left join payments p on p.pid = c.pid and p.encounter = c.encounter
                    union all
                    select
                        p.pid,
                        p.encounter,
                        0 as line_count,
                        p.payment_count,
                        cast(@asOfDate as date) as last_billing_date,
                        0 as age_days,
                        0 - p.payment_amount - p.adjustment_amount as balance_amount
                    from payments p
                    left join charges c on c.pid = p.pid and c.encounter = p.encounter
                    where c.encounter is null
                ),
                rollup as (
                    select
                        pid,
                        count(*) filter (where balance_amount > 0) as open_encounter_count,
                        coalesce(max(age_days) filter (where balance_amount > 0), 0) as oldest_open_age_days,
                        coalesce(min(last_billing_date) filter (where balance_amount > 0), cast(@asOfDate as date)) as oldest_open_date,
                        sum(case when age_days <= 30 then balance_amount else 0 end) as current_due_amount,
                        sum(case when age_days > 30 then balance_amount else 0 end) as past_due_amount,
                        sum(balance_amount) as balance_due_amount
                    from aged
                    group by pid
                    having sum(balance_amount) > 0
                )
                select
                    p.legacy_pid,
                    count(*) over() as candidate_count,
                    sum(r.balance_due_amount) over() as total_balance_amount,
                    sum(r.past_due_amount) over() as total_past_due_amount,
                    sum(r.current_due_amount) over() as total_current_due_amount
                from rollup r
                inner join patients p on p.legacy_pid = r.pid
                order by
                    r.past_due_amount desc,
                    r.balance_due_amount desc,
                    r.oldest_open_age_days desc,
                    p.legacy_pid
                limit @limit;
                """;
            command.Parameters.Add("asOfDate", NpgsqlDbType.Date).Value = metadata.BaseDate;
            command.Parameters.AddWithValue("limit", boundedLimit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rollups.Add(new StatementBatchRollupRow(
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    CandidateCount: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("candidate_count"))),
                    TotalBalanceAmount: reader.GetDecimal(reader.GetOrdinal("total_balance_amount")),
                    TotalPastDueAmount: reader.GetDecimal(reader.GetOrdinal("total_past_due_amount")),
                    TotalCurrentDueAmount: reader.GetDecimal(reader.GetOrdinal("total_current_due_amount"))));
            }
        }

        var candidates = new List<StatementBatchCandidate>();
        foreach (var rollup in rollups)
        {
            var billing = await GetForPatientAsync(rollup.LegacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
            if (billing is null || billing.StatementSummary.BalanceDueAmount <= 0m)
            {
                continue;
            }

            candidates.Add(BuildStatementBatchCandidate(billing));
        }

        var totals = rollups.FirstOrDefault();
        return new StatementBatchResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            CandidateCount: totals?.CandidateCount ?? candidates.Count,
            TotalBalanceAmount: totals?.TotalBalanceAmount ?? candidates.Sum(candidate => candidate.BalanceDueAmount),
            TotalPastDueAmount: totals?.TotalPastDueAmount ?? candidates.Sum(candidate => candidate.PastDueAmount),
            TotalCurrentDueAmount: totals?.TotalCurrentDueAmount ?? candidates.Sum(candidate => candidate.CurrentDueAmount),
            Candidates: candidates);
    }

    public async Task<(byte[] Content, string FileName)> GetStatementBatchPackageAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var batch = await GetStatementBatchAsync(limit, cancellationToken);
        var packageDate = batch.AsOfDate.Replace("-", string.Empty, StringComparison.Ordinal);
        var packageId = $"STMT-BATCH-{packageDate}-TOP{batch.Candidates.Count}";
        var fileName = $"statement-batch-{packageDate.ToLowerInvariant()}-top{batch.Candidates.Count}.zip";
        var timestampDate = DateOnly.ParseExact(batch.AsOfDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var timestamp = new DateTimeOffset(
            timestampDate.Year,
            timestampDate.Month,
            timestampDate.Day,
            0,
            0,
            0,
            TimeSpan.Zero);

        var manifestEntries = new List<StatementBatchPackageEntry>();
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var candidate in batch.Candidates)
            {
                var billing = await GetForPatientAsync(
                    candidate.LegacyPid.ToString(CultureInfo.InvariantCulture),
                    cancellationToken);
                if (billing is null)
                {
                    continue;
                }

                var document = billing.StatementDocument;
                var entryName = $"statements/{document.StatementNumber}.pdf";
                AddArchiveEntry(
                    archive,
                    entryName,
                    BuildStatementPdf(document),
                    timestamp);
                manifestEntries.Add(new StatementBatchPackageEntry(
                    Pubpid: candidate.Pubpid,
                    LegacyPid: candidate.LegacyPid,
                    PatientDisplayName: candidate.PatientDisplayName,
                    StatementNumber: document.StatementNumber,
                    StatementStatus: candidate.StatementStatus,
                    StatementDate: candidate.StatementDate,
                    DueDate: candidate.DueDate,
                    BalanceDueAmount: candidate.BalanceDueAmount,
                    PastDueAmount: candidate.PastDueAmount,
                    CurrentDueAmount: candidate.CurrentDueAmount,
                    DeliveryMethod: candidate.DeliveryMethod,
                    FileName: entryName));
            }

            var manifest = new StatementBatchPackageManifest(
                DatasetId: batch.DatasetId,
                DatasetVersion: batch.DatasetVersion,
                AsOfDate: batch.AsOfDate,
                PackageId: packageId,
                CandidateCount: batch.CandidateCount,
                IncludedStatementCount: manifestEntries.Count,
                TotalBalanceAmount: batch.TotalBalanceAmount,
                TotalPastDueAmount: batch.TotalPastDueAmount,
                TotalCurrentDueAmount: batch.TotalCurrentDueAmount,
                Entries: manifestEntries);
            var manifestJson = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
            AddArchiveEntry(archive, "manifest.json", Encoding.UTF8.GetBytes(manifestJson), timestamp);
            AddArchiveEntry(archive, "summary.csv", Encoding.UTF8.GetBytes(BuildStatementBatchSummaryCsv(manifestEntries)), timestamp);
        }

        return (memory.ToArray(), fileName);
    }

    public async Task<StatementBatchDeliveryResponse> PrepareStatementBatchDeliveryAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var batch = await GetStatementBatchAsync(limit, cancellationToken);
        var packageDate = batch.AsOfDate.Replace("-", string.Empty, StringComparison.Ordinal);
        var preparedAt = $"{batch.AsOfDate}T12:00:00Z";
        var entries = new List<StatementBatchDeliveryEntry>();

        foreach (var candidate in batch.Candidates)
        {
            var billing = await GetForPatientAsync(
                candidate.LegacyPid.ToString(CultureInfo.InvariantCulture),
                cancellationToken);
            if (billing is null)
            {
                continue;
            }

            var document = billing.StatementDocument;
            var summary = billing.StatementSummary;
            entries.Add(new StatementBatchDeliveryEntry(
                Pubpid: candidate.Pubpid,
                LegacyPid: candidate.LegacyPid,
                PatientDisplayName: candidate.PatientDisplayName,
                StatementNumber: document.StatementNumber,
                StatementStatus: candidate.StatementStatus,
                StatementDate: candidate.StatementDate,
                DueDate: candidate.DueDate,
                BalanceDueAmount: candidate.BalanceDueAmount,
                PastDueAmount: candidate.PastDueAmount,
                CurrentDueAmount: candidate.CurrentDueAmount,
                DeliveryMethod: candidate.DeliveryMethod,
                Destination: StatementDeliveryDestination(candidate.DeliveryMethod, summary),
                FileName: $"statements/{document.StatementNumber}.pdf",
                DeliveryStatus: "Queued"));
        }

        return new StatementBatchDeliveryResponse(
            DatasetId: batch.DatasetId,
            DatasetVersion: batch.DatasetVersion,
            AsOfDate: batch.AsOfDate,
            DeliveryId: $"STMT-DELIVERY-{packageDate}-TOP{entries.Count}",
            PreparedAt: preparedAt,
            CandidateCount: batch.CandidateCount,
            IncludedStatementCount: entries.Count,
            EmailReadyCount: entries.Count(entry => string.Equals(entry.DeliveryMethod, "Email-ready", StringComparison.OrdinalIgnoreCase)),
            PrintReadyCount: entries.Count(entry => string.Equals(entry.DeliveryMethod, "Print", StringComparison.OrdinalIgnoreCase)),
            TotalBalanceAmount: batch.TotalBalanceAmount,
            TotalPastDueAmount: batch.TotalPastDueAmount,
            TotalCurrentDueAmount: batch.TotalCurrentDueAmount,
            Entries: entries);
    }

    public async Task<StatementBatchDispatchResponse> DispatchStatementBatchDeliveryAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var delivery = await PrepareStatementBatchDeliveryAsync(limit, cancellationToken);
        var dispatchId = delivery.DeliveryId.Replace("STMT-DELIVERY-", "STMT-DISPATCH-", StringComparison.Ordinal);
        var dispatchedAt = $"{delivery.AsOfDate}T12:05:00Z";
        var entries = delivery.Entries
            .Select((entry, index) =>
            {
                var isEmail = string.Equals(entry.DeliveryMethod, "Email-ready", StringComparison.OrdinalIgnoreCase);
                var channel = isEmail ? "EMAIL" : "PRINT";
                return new StatementBatchDispatchEntry(
                    DispatchAuditId: $"AUD-{dispatchId}-{index + 1:D4}",
                    Pubpid: entry.Pubpid,
                    LegacyPid: entry.LegacyPid,
                    PatientDisplayName: entry.PatientDisplayName,
                    StatementNumber: entry.StatementNumber,
                    StatementStatus: entry.StatementStatus,
                    StatementDate: entry.StatementDate,
                    DueDate: entry.DueDate,
                    BalanceDueAmount: entry.BalanceDueAmount,
                    PastDueAmount: entry.PastDueAmount,
                    CurrentDueAmount: entry.CurrentDueAmount,
                    DeliveryMethod: entry.DeliveryMethod,
                    Destination: entry.Destination,
                    FileName: entry.FileName,
                    QueueName: isEmail ? "patient-statement-email" : "patient-statement-print",
                    DispatchStatus: isEmail ? "Email queued" : "Print queued",
                    ExternalReference: $"LOCAL-{channel}-{entry.StatementNumber}");
            })
            .ToList();

        var response = new StatementBatchDispatchResponse(
            DatasetId: delivery.DatasetId,
            DatasetVersion: delivery.DatasetVersion,
            AsOfDate: delivery.AsOfDate,
            DeliveryId: delivery.DeliveryId,
            DispatchId: dispatchId,
            DispatchedAt: dispatchedAt,
            CandidateCount: delivery.CandidateCount,
            DispatchedStatementCount: entries.Count,
            EmailQueueCount: entries.Count(entry => string.Equals(entry.QueueName, "patient-statement-email", StringComparison.OrdinalIgnoreCase)),
            PrintQueueCount: entries.Count(entry => string.Equals(entry.QueueName, "patient-statement-print", StringComparison.OrdinalIgnoreCase)),
            TotalBalanceAmount: delivery.TotalBalanceAmount,
            TotalPastDueAmount: delivery.TotalPastDueAmount,
            TotalCurrentDueAmount: delivery.TotalCurrentDueAmount,
            Entries: entries);

        await SaveStatementDispatchAsync(response, cancellationToken);
        return response;
    }

    public async Task<StatementDeliveryAuditHistoryResponse> GetStatementDeliveryAuditHistoryAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var metadata = await GetMetadataAsync(cancellationToken);
        var entries = new List<StatementDeliveryAuditHistoryEntry>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureStatementDeliveryAuditTableAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
              dispatch_audit_id,
              delivery_id,
              dispatch_id,
              to_char(dispatched_at, 'YYYY-MM-DD"T"HH24:MI:SS"Z"') as dispatched_at_text,
              to_char(created_at, 'YYYY-MM-DD"T"HH24:MI:SS"Z"') as created_at_text,
              pubpid,
              legacy_pid,
              patient_display_name,
              statement_number,
              statement_status,
              statement_date::text as statement_date,
              due_date::text as due_date,
              balance_due_amount,
              past_due_amount,
              current_due_amount,
              delivery_method,
              destination,
              file_name,
              queue_name,
              dispatch_status,
              external_reference
            from statement_delivery_audit_events
            order by created_at desc, dispatch_audit_id
            limit @limit;
            """;
        command.Parameters.Add("limit", NpgsqlDbType.Integer).Value = boundedLimit;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new StatementDeliveryAuditHistoryEntry(
                DispatchAuditId: reader.GetString(reader.GetOrdinal("dispatch_audit_id")),
                DeliveryId: reader.GetString(reader.GetOrdinal("delivery_id")),
                DispatchId: reader.GetString(reader.GetOrdinal("dispatch_id")),
                DispatchedAt: reader.GetString(reader.GetOrdinal("dispatched_at_text")),
                CreatedAt: reader.GetString(reader.GetOrdinal("created_at_text")),
                Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
                LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                PatientDisplayName: reader.GetString(reader.GetOrdinal("patient_display_name")),
                StatementNumber: reader.GetString(reader.GetOrdinal("statement_number")),
                StatementStatus: reader.GetString(reader.GetOrdinal("statement_status")),
                StatementDate: reader.GetString(reader.GetOrdinal("statement_date")),
                DueDate: reader.GetString(reader.GetOrdinal("due_date")),
                BalanceDueAmount: reader.GetDecimal(reader.GetOrdinal("balance_due_amount")),
                PastDueAmount: reader.GetDecimal(reader.GetOrdinal("past_due_amount")),
                CurrentDueAmount: reader.GetDecimal(reader.GetOrdinal("current_due_amount")),
                DeliveryMethod: reader.GetString(reader.GetOrdinal("delivery_method")),
                Destination: reader.GetString(reader.GetOrdinal("destination")),
                FileName: reader.GetString(reader.GetOrdinal("file_name")),
                QueueName: reader.GetString(reader.GetOrdinal("queue_name")),
                DispatchStatus: reader.GetString(reader.GetOrdinal("dispatch_status")),
                ExternalReference: reader.GetString(reader.GetOrdinal("external_reference"))));
        }

        return new StatementDeliveryAuditHistoryResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            EventCount: entries.Count,
            Entries: entries);
    }

    public async Task<StatementPortalDeliveryResponse> DeliverStatementBatchToPortalAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var batch = await GetStatementBatchAsync(limit, cancellationToken);
        var packageDate = batch.AsOfDate.Replace("-", string.Empty, StringComparison.Ordinal);
        var portalDeliveryId = $"STMT-PORTAL-{packageDate}-TOP{batch.Candidates.Count}";
        var deliveredAt = $"{batch.AsOfDate}T12:10:00Z";
        var entries = new List<StatementPortalDeliveryEntry>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var nextDocumentId = await GetNextPortalStatementDocumentIdAsync(connection, transaction, cancellationToken);

        foreach (var candidate in batch.Candidates)
        {
            var portalAccount = await GetPortalStatementDeliveryAccountAsync(
                connection,
                candidate.LegacyPid,
                transaction,
                cancellationToken);
            if (portalAccount is null)
            {
                continue;
            }

            var patientBilling = await GetForPatientAsync(candidate.LegacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
            var document = patientBilling?.StatementDocument;
            if (document is null)
            {
                continue;
            }

            var contentBytes = BuildStatementPdf(document);
            var documentKey = $"portal-statement-{candidate.StatementNumber}";
            var documentName = $"Patient Statement {candidate.StatementNumber}";
            var fileName = $"{candidate.StatementNumber}.pdf";
            var documentId = await UpsertPortalStatementDocumentAsync(
                connection,
                transaction,
                nextDocumentId,
                portalAccount,
                candidate,
                documentKey,
                documentName,
                fileName,
                contentBytes,
                cancellationToken);
            nextDocumentId = Math.Max(nextDocumentId + 1, documentId + 1);

            entries.Add(new StatementPortalDeliveryEntry(
                DocumentId: documentId,
                DocumentKey: documentKey,
                Pubpid: candidate.Pubpid,
                LegacyPid: candidate.LegacyPid,
                PatientDisplayName: candidate.PatientDisplayName,
                PortalUsername: portalAccount.PortalUsername,
                StatementNumber: candidate.StatementNumber,
                StatementStatus: candidate.StatementStatus,
                StatementDate: candidate.StatementDate,
                DueDate: candidate.DueDate,
                BalanceDueAmount: candidate.BalanceDueAmount,
                PastDueAmount: candidate.PastDueAmount,
                CurrentDueAmount: candidate.CurrentDueAmount,
                CategoryName: "Invoices",
                DocumentName: documentName,
                FileName: fileName,
                DeliveryStatus: "Portal document published"));
        }

        await transaction.CommitAsync(cancellationToken);

        return new StatementPortalDeliveryResponse(
            DatasetId: batch.DatasetId,
            DatasetVersion: batch.DatasetVersion,
            AsOfDate: batch.AsOfDate,
            PortalDeliveryId: portalDeliveryId,
            DeliveredAt: deliveredAt,
            CandidateCount: batch.CandidateCount,
            PortalEligibleCount: entries.Count,
            DeliveredDocumentCount: entries.Count,
            SkippedCount: Math.Max(0, batch.Candidates.Count - entries.Count),
            TotalBalanceAmount: batch.TotalBalanceAmount,
            TotalPastDueAmount: batch.TotalPastDueAmount,
            TotalCurrentDueAmount: batch.TotalCurrentDueAmount,
            Entries: entries);
    }

    public async Task<StatementEmailOutboxResponse> QueueStatementBatchEmailOutboxAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var delivery = await PrepareStatementBatchDeliveryAsync(limit, cancellationToken);
        var packageDate = delivery.AsOfDate.Replace("-", string.Empty, StringComparison.Ordinal);
        var outboxBatchId = $"STMT-EMAIL-{packageDate}-TOP{delivery.Entries.Count}";
        var queuedAt = $"{delivery.AsOfDate}T12:15:00Z";
        var entries = delivery.Entries
            .Where(entry => string.Equals(entry.DeliveryMethod, "Email-ready", StringComparison.OrdinalIgnoreCase)
                && NormalizeText(entry.Destination) is not null)
            .Select((entry, index) => new StatementEmailOutboxEntry(
                OutboxMessageId: $"EMAIL-{outboxBatchId}-{index + 1:D4}",
                Pubpid: entry.Pubpid,
                LegacyPid: entry.LegacyPid,
                PatientDisplayName: entry.PatientDisplayName,
                StatementNumber: entry.StatementNumber,
                StatementStatus: entry.StatementStatus,
                StatementDate: entry.StatementDate,
                DueDate: entry.DueDate,
                BalanceDueAmount: entry.BalanceDueAmount,
                PastDueAmount: entry.PastDueAmount,
                CurrentDueAmount: entry.CurrentDueAmount,
                ToEmail: entry.Destination,
                FromEmail: "billing@example.test",
                Subject: $"Statement {entry.StatementNumber} is ready",
                BodyPreview: $"Your statement {entry.StatementNumber} dated {entry.StatementDate} has a balance of {entry.BalanceDueAmount:0.00}.",
                AttachmentFileName: entry.FileName,
                QueueName: "patient-statement-email-outbox",
                DeliveryStatus: "Email outbox queued",
                ExternalReference: $"LOCAL-EMAIL-OUTBOX-{entry.StatementNumber}"))
            .ToList();

        var response = new StatementEmailOutboxResponse(
            DatasetId: delivery.DatasetId,
            DatasetVersion: delivery.DatasetVersion,
            AsOfDate: delivery.AsOfDate,
            OutboxBatchId: outboxBatchId,
            QueuedAt: queuedAt,
            CandidateCount: delivery.CandidateCount,
            EmailEligibleCount: entries.Count,
            QueuedMessageCount: entries.Count,
            SkippedCount: Math.Max(0, delivery.Entries.Count - entries.Count),
            TotalBalanceAmount: delivery.TotalBalanceAmount,
            TotalPastDueAmount: delivery.TotalPastDueAmount,
            TotalCurrentDueAmount: delivery.TotalCurrentDueAmount,
            Entries: entries);

        await SaveStatementEmailOutboxAsync(response, cancellationToken);
        return response;
    }

    private async Task SaveStatementDispatchAsync(
        StatementBatchDispatchResponse response,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureStatementDeliveryAuditTableAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var entry in response.Entries)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into statement_delivery_audit_events (
                  dispatch_audit_id, dataset_id, dataset_version, as_of_date, delivery_id, dispatch_id,
                  dispatched_at, pubpid, legacy_pid, patient_display_name, statement_number, statement_status,
                  statement_date, due_date, balance_due_amount, past_due_amount, current_due_amount,
                  delivery_method, destination, file_name, queue_name, dispatch_status, external_reference, created_at
                )
                values (
                  @dispatchAuditId, @datasetId, @datasetVersion, @asOfDate, @deliveryId, @dispatchId,
                  @dispatchedAt, @pubpid, @legacyPid, @patientDisplayName, @statementNumber, @statementStatus,
                  @statementDate, @dueDate, @balanceDueAmount, @pastDueAmount, @currentDueAmount,
                  @deliveryMethod, @destination, @fileName, @queueName, @dispatchStatus, @externalReference, @createdAt
                )
                on conflict (dispatch_audit_id) do update set
                  dataset_id = excluded.dataset_id,
                  dataset_version = excluded.dataset_version,
                  as_of_date = excluded.as_of_date,
                  delivery_id = excluded.delivery_id,
                  dispatch_id = excluded.dispatch_id,
                  dispatched_at = excluded.dispatched_at,
                  pubpid = excluded.pubpid,
                  legacy_pid = excluded.legacy_pid,
                  patient_display_name = excluded.patient_display_name,
                  statement_number = excluded.statement_number,
                  statement_status = excluded.statement_status,
                  statement_date = excluded.statement_date,
                  due_date = excluded.due_date,
                  balance_due_amount = excluded.balance_due_amount,
                  past_due_amount = excluded.past_due_amount,
                  current_due_amount = excluded.current_due_amount,
                  delivery_method = excluded.delivery_method,
                  destination = excluded.destination,
                  file_name = excluded.file_name,
                  queue_name = excluded.queue_name,
                  dispatch_status = excluded.dispatch_status,
                  external_reference = excluded.external_reference,
                  created_at = excluded.created_at;
                """;
            command.Parameters.Add("dispatchAuditId", NpgsqlDbType.Text).Value = entry.DispatchAuditId;
            command.Parameters.Add("datasetId", NpgsqlDbType.Text).Value = response.DatasetId;
            command.Parameters.Add("datasetVersion", NpgsqlDbType.Text).Value = response.DatasetVersion;
            command.Parameters.Add("asOfDate", NpgsqlDbType.Date).Value = ReadDateOnly(response.AsOfDate, ClaimScrubBusinessDate);
            command.Parameters.Add("deliveryId", NpgsqlDbType.Text).Value = response.DeliveryId;
            command.Parameters.Add("dispatchId", NpgsqlDbType.Text).Value = response.DispatchId;
            command.Parameters.Add("dispatchedAt", NpgsqlDbType.Timestamp).Value = ReadDispatchTimestamp(response.DispatchedAt);
            command.Parameters.Add("pubpid", NpgsqlDbType.Text).Value = entry.Pubpid;
            command.Parameters.Add("legacyPid", NpgsqlDbType.Integer).Value = entry.LegacyPid;
            command.Parameters.Add("patientDisplayName", NpgsqlDbType.Text).Value = entry.PatientDisplayName;
            command.Parameters.Add("statementNumber", NpgsqlDbType.Text).Value = entry.StatementNumber;
            command.Parameters.Add("statementStatus", NpgsqlDbType.Text).Value = entry.StatementStatus;
            command.Parameters.Add("statementDate", NpgsqlDbType.Date).Value = ReadDateOnly(entry.StatementDate, ClaimScrubBusinessDate);
            command.Parameters.Add("dueDate", NpgsqlDbType.Date).Value = ReadDateOnly(entry.DueDate, ClaimScrubBusinessDate);
            command.Parameters.Add("balanceDueAmount", NpgsqlDbType.Numeric).Value = entry.BalanceDueAmount;
            command.Parameters.Add("pastDueAmount", NpgsqlDbType.Numeric).Value = entry.PastDueAmount;
            command.Parameters.Add("currentDueAmount", NpgsqlDbType.Numeric).Value = entry.CurrentDueAmount;
            command.Parameters.Add("deliveryMethod", NpgsqlDbType.Text).Value = entry.DeliveryMethod;
            command.Parameters.Add("destination", NpgsqlDbType.Text).Value = entry.Destination;
            command.Parameters.Add("fileName", NpgsqlDbType.Text).Value = entry.FileName;
            command.Parameters.Add("queueName", NpgsqlDbType.Text).Value = entry.QueueName;
            command.Parameters.Add("dispatchStatus", NpgsqlDbType.Text).Value = entry.DispatchStatus;
            command.Parameters.Add("externalReference", NpgsqlDbType.Text).Value = entry.ExternalReference;
            command.Parameters.Add("createdAt", NpgsqlDbType.Timestamp).Value = ReadDispatchTimestamp(response.DispatchedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SaveStatementEmailOutboxAsync(
        StatementEmailOutboxResponse response,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var entry in response.Entries)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into statement_email_outbox (
                  outbox_message_id, dataset_id, dataset_version, as_of_date, outbox_batch_id, queued_at,
                  pubpid, legacy_pid, patient_display_name, statement_number, statement_status,
                  statement_date, due_date, balance_due_amount, past_due_amount, current_due_amount,
                  to_email, from_email, subject, body_preview, attachment_file_name, queue_name,
                  delivery_status, external_reference, created_at
                )
                values (
                  @outboxMessageId, @datasetId, @datasetVersion, @asOfDate, @outboxBatchId, @queuedAt,
                  @pubpid, @legacyPid, @patientDisplayName, @statementNumber, @statementStatus,
                  @statementDate, @dueDate, @balanceDueAmount, @pastDueAmount, @currentDueAmount,
                  @toEmail, @fromEmail, @subject, @bodyPreview, @attachmentFileName, @queueName,
                  @deliveryStatus, @externalReference, @createdAt
                )
                on conflict (outbox_message_id) do update set
                  dataset_id = excluded.dataset_id,
                  dataset_version = excluded.dataset_version,
                  as_of_date = excluded.as_of_date,
                  outbox_batch_id = excluded.outbox_batch_id,
                  queued_at = excluded.queued_at,
                  pubpid = excluded.pubpid,
                  legacy_pid = excluded.legacy_pid,
                  patient_display_name = excluded.patient_display_name,
                  statement_number = excluded.statement_number,
                  statement_status = excluded.statement_status,
                  statement_date = excluded.statement_date,
                  due_date = excluded.due_date,
                  balance_due_amount = excluded.balance_due_amount,
                  past_due_amount = excluded.past_due_amount,
                  current_due_amount = excluded.current_due_amount,
                  to_email = excluded.to_email,
                  from_email = excluded.from_email,
                  subject = excluded.subject,
                  body_preview = excluded.body_preview,
                  attachment_file_name = excluded.attachment_file_name,
                  queue_name = excluded.queue_name,
                  delivery_status = excluded.delivery_status,
                  external_reference = excluded.external_reference,
                  created_at = excluded.created_at;
                """;
            command.Parameters.Add("outboxMessageId", NpgsqlDbType.Text).Value = entry.OutboxMessageId;
            command.Parameters.Add("datasetId", NpgsqlDbType.Text).Value = response.DatasetId;
            command.Parameters.Add("datasetVersion", NpgsqlDbType.Text).Value = response.DatasetVersion;
            command.Parameters.Add("asOfDate", NpgsqlDbType.Date).Value = ReadDateOnly(response.AsOfDate, ClaimScrubBusinessDate);
            command.Parameters.Add("outboxBatchId", NpgsqlDbType.Text).Value = response.OutboxBatchId;
            command.Parameters.Add("queuedAt", NpgsqlDbType.Timestamp).Value = ReadDispatchTimestamp(response.QueuedAt);
            command.Parameters.Add("pubpid", NpgsqlDbType.Text).Value = entry.Pubpid;
            command.Parameters.Add("legacyPid", NpgsqlDbType.Integer).Value = entry.LegacyPid;
            command.Parameters.Add("patientDisplayName", NpgsqlDbType.Text).Value = entry.PatientDisplayName;
            command.Parameters.Add("statementNumber", NpgsqlDbType.Text).Value = entry.StatementNumber;
            command.Parameters.Add("statementStatus", NpgsqlDbType.Text).Value = entry.StatementStatus;
            command.Parameters.Add("statementDate", NpgsqlDbType.Date).Value = ReadDateOnly(entry.StatementDate, ClaimScrubBusinessDate);
            command.Parameters.Add("dueDate", NpgsqlDbType.Date).Value = ReadDateOnly(entry.DueDate, ClaimScrubBusinessDate);
            command.Parameters.Add("balanceDueAmount", NpgsqlDbType.Numeric).Value = entry.BalanceDueAmount;
            command.Parameters.Add("pastDueAmount", NpgsqlDbType.Numeric).Value = entry.PastDueAmount;
            command.Parameters.Add("currentDueAmount", NpgsqlDbType.Numeric).Value = entry.CurrentDueAmount;
            command.Parameters.Add("toEmail", NpgsqlDbType.Text).Value = entry.ToEmail;
            command.Parameters.Add("fromEmail", NpgsqlDbType.Text).Value = entry.FromEmail;
            command.Parameters.Add("subject", NpgsqlDbType.Text).Value = entry.Subject;
            command.Parameters.Add("bodyPreview", NpgsqlDbType.Text).Value = entry.BodyPreview;
            command.Parameters.Add("attachmentFileName", NpgsqlDbType.Text).Value = entry.AttachmentFileName;
            command.Parameters.Add("queueName", NpgsqlDbType.Text).Value = entry.QueueName;
            command.Parameters.Add("deliveryStatus", NpgsqlDbType.Text).Value = entry.DeliveryStatus;
            command.Parameters.Add("externalReference", NpgsqlDbType.Text).Value = entry.ExternalReference;
            command.Parameters.Add("createdAt", NpgsqlDbType.Timestamp).Value = ReadDispatchTimestamp(response.QueuedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> GetNextPortalStatementDocumentIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select greatest(coalesce(max(id), 0) + 1, 8100000) from patient_documents;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<PortalStatementDeliveryAccount?> GetPortalStatementDeliveryAccountAsync(
        NpgsqlConnection connection,
        int legacyPid,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select
              p.canonical_id,
              p.legacy_pid,
              ppa.portal_username
            from patients p
            inner join patient_portal_accounts ppa on ppa.patient_id = p.canonical_id
            where p.legacy_pid = @legacyPid
              and p.portal_enabled = true
              and ppa.password_status = 1
              and coalesce(ppa.one_time_token, '') = ''
            limit 1;
            """;
        command.Parameters.Add("legacyPid", NpgsqlDbType.Integer).Value = legacyPid;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PortalStatementDeliveryAccount(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            PortalUsername: reader.GetString(reader.GetOrdinal("portal_username")));
    }

    private static async Task<int> UpsertPortalStatementDocumentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int documentId,
        PortalStatementDeliveryAccount portalAccount,
        StatementBatchCandidate candidate,
        string documentKey,
        string documentName,
        string fileName,
        byte[] contentBytes,
        CancellationToken cancellationToken)
    {
        var statementDate = ReadDateOnly(candidate.StatementDate, ClaimScrubBusinessDate);
        var uploadedAt = ReadDispatchTimestamp($"{candidate.StatementDate}T12:10:00Z");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into patient_documents (
              id, document_key, patient_id, pid, category_id, category_name, name,
              doc_date, uploaded_at, mimetype, file_name, size_bytes, pages, encounter,
              storage_method, url, hash, documentation_of, notes, review_status,
              reviewed_by, reviewed_at, content, content_bytes, deleted
            )
            values (
              @id, @documentKey, @patientId, @pid, 31, 'Invoices', @name,
              @docDate, @uploadedAt, 'application/pdf', @fileName, @sizeBytes, 1, null,
              'database', '', @hash, 'billing_statement', @notes, 'reviewed',
              'system', @uploadedAt, @content, @contentBytes, 0
            )
            on conflict (document_key) do update set
              patient_id = excluded.patient_id,
              pid = excluded.pid,
              category_id = excluded.category_id,
              category_name = excluded.category_name,
              name = excluded.name,
              doc_date = excluded.doc_date,
              uploaded_at = excluded.uploaded_at,
              mimetype = excluded.mimetype,
              file_name = excluded.file_name,
              size_bytes = excluded.size_bytes,
              pages = excluded.pages,
              encounter = excluded.encounter,
              storage_method = excluded.storage_method,
              url = excluded.url,
              hash = excluded.hash,
              documentation_of = excluded.documentation_of,
              notes = excluded.notes,
              review_status = excluded.review_status,
              reviewed_by = excluded.reviewed_by,
              reviewed_at = excluded.reviewed_at,
              content = excluded.content,
              content_bytes = excluded.content_bytes,
              deleted = 0
            returning id;
            """;
        command.Parameters.Add("id", NpgsqlDbType.Integer).Value = documentId;
        command.Parameters.Add("documentKey", NpgsqlDbType.Text).Value = documentKey;
        command.Parameters.Add("patientId", NpgsqlDbType.Text).Value = portalAccount.PatientId;
        command.Parameters.Add("pid", NpgsqlDbType.Integer).Value = portalAccount.LegacyPid;
        command.Parameters.Add("name", NpgsqlDbType.Text).Value = documentName;
        command.Parameters.Add("docDate", NpgsqlDbType.Date).Value = statementDate;
        command.Parameters.Add("uploadedAt", NpgsqlDbType.Timestamp).Value = uploadedAt;
        command.Parameters.Add("fileName", NpgsqlDbType.Text).Value = fileName;
        command.Parameters.Add("sizeBytes", NpgsqlDbType.Integer).Value = contentBytes.Length;
        command.Parameters.Add("hash", NpgsqlDbType.Text).Value = Convert.ToHexString(SHA1.HashData(contentBytes)).ToLowerInvariant();
        command.Parameters.Add("notes", NpgsqlDbType.Text).Value = $"Delivered to portal account {portalAccount.PortalUsername}.";
        command.Parameters.Add("content", NpgsqlDbType.Text).Value =
            $"Portal-delivered statement {candidate.StatementNumber} for {candidate.PatientDisplayName}; balance {candidate.BalanceDueAmount:0.00}.";
        command.Parameters.Add("contentBytes", NpgsqlDbType.Bytea).Value = contentBytes;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task EnsureStatementDeliveryAuditTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists statement_delivery_audit_events (
              dispatch_audit_id text primary key,
              dataset_id text not null,
              dataset_version text not null,
              as_of_date date not null,
              delivery_id text not null,
              dispatch_id text not null,
              dispatched_at timestamp not null,
              pubpid text not null,
              legacy_pid integer not null,
              patient_display_name text not null,
              statement_number text not null,
              statement_status text not null,
              statement_date date not null,
              due_date date not null,
              balance_due_amount numeric(12,2) not null default 0,
              past_due_amount numeric(12,2) not null default 0,
              current_due_amount numeric(12,2) not null default 0,
              delivery_method text not null,
              destination text not null,
              file_name text not null,
              queue_name text not null,
              dispatch_status text not null,
              external_reference text not null,
              created_at timestamp not null
            );
            create index if not exists idx_statement_delivery_audit_dispatch
              on statement_delivery_audit_events (dispatch_id, dispatched_at desc);
            create index if not exists idx_statement_delivery_audit_pid_created
              on statement_delivery_audit_events (legacy_pid, created_at desc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTime ReadDispatchTimestamp(string value)
    {
        var parsed = DateTime.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
    }

    public async Task<CollectionsWorkQueueResponse> GetCollectionsWorkQueueAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var metadata = await GetMetadataAsync(cancellationToken);
        var rollups = new List<CollectionsWorkQueueRollupRow>();

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                with charges as (
                    select
                        pid,
                        encounter,
                        count(*) as line_count,
                        coalesce(sum(coalesce(fee, 0)), 0) as charge_amount,
                        max(billing_date) as last_billing_date
                    from billing
                    where activity = 1
                    group by pid, encounter
                ),
                payments as (
                    select
                        pid,
                        encounter,
                        count(*) as payment_count,
                        coalesce(sum(pay_amount), 0) as payment_amount,
                        coalesce(sum(adj_amount), 0) as adjustment_amount
                    from payment_activities
                    where deleted is null
                    group by pid, encounter
                ),
                aged as (
                    select
                        c.pid,
                        c.encounter,
                        c.line_count,
                        coalesce(p.payment_count, 0) as payment_count,
                        c.last_billing_date,
                        greatest((cast(@asOfDate as date) - c.last_billing_date)::int, 0) as age_days,
                        c.charge_amount - coalesce(p.payment_amount, 0) - coalesce(p.adjustment_amount, 0) as balance_amount
                    from charges c
                    left join payments p on p.pid = c.pid and p.encounter = c.encounter
                    union all
                    select
                        p.pid,
                        p.encounter,
                        0 as line_count,
                        p.payment_count,
                        cast(@asOfDate as date) as last_billing_date,
                        0 as age_days,
                        0 - p.payment_amount - p.adjustment_amount as balance_amount
                    from payments p
                    left join charges c on c.pid = p.pid and c.encounter = p.encounter
                    where c.encounter is null
                ),
                rollup as (
                    select
                        pid,
                        count(*) filter (where balance_amount > 0) as open_encounter_count,
                        coalesce(max(age_days) filter (where balance_amount > 0), 0) as oldest_open_age_days,
                        coalesce(min(last_billing_date) filter (where balance_amount > 0), cast(@asOfDate as date)) as oldest_open_date,
                        sum(case when age_days <= 30 then balance_amount else 0 end) as current_due_amount,
                        sum(case when age_days > 30 then balance_amount else 0 end) as past_due_amount,
                        sum(case when age_days > 90 then balance_amount else 0 end) as over90_amount,
                        sum(balance_amount) as balance_due_amount
                    from aged
                    group by pid
                    having sum(balance_amount) > 0
                ),
                queue as (
                    select *
                    from rollup
                    where past_due_amount > 0
                )
                select
                    p.legacy_pid,
                    count(*) over() as account_count,
                    sum(case when q.over90_amount > 0 or q.oldest_open_age_days >= 91 then 1 else 0 end) over() as high_priority_count,
                    sum(q.balance_due_amount) over() as total_balance_amount,
                    sum(q.past_due_amount) over() as total_past_due_amount,
                    sum(q.over90_amount) over() as total_over90_amount
                from queue q
                inner join patients p on p.legacy_pid = q.pid
                order by
                    q.over90_amount desc,
                    q.past_due_amount desc,
                    q.balance_due_amount desc,
                    q.oldest_open_age_days desc,
                    p.legacy_pid
                limit @limit;
                """;
            command.Parameters.Add("asOfDate", NpgsqlDbType.Date).Value = metadata.BaseDate;
            command.Parameters.AddWithValue("limit", boundedLimit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rollups.Add(new CollectionsWorkQueueRollupRow(
                    LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
                    AccountCount: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("account_count"))),
                    HighPriorityCount: Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("high_priority_count"))),
                    TotalBalanceAmount: reader.GetDecimal(reader.GetOrdinal("total_balance_amount")),
                    TotalPastDueAmount: reader.GetDecimal(reader.GetOrdinal("total_past_due_amount")),
                    TotalOver90Amount: reader.GetDecimal(reader.GetOrdinal("total_over90_amount"))));
            }
        }

        var items = new List<CollectionsWorkQueueItem>();
        foreach (var rollup in rollups)
        {
            var billing = await GetForPatientAsync(rollup.LegacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
            if (billing is null || billing.StatementSummary.PastDueAmount <= 0m)
            {
                continue;
            }

            items.Add(BuildCollectionsWorkQueueItem(billing));
        }

        var totals = rollups.FirstOrDefault();
        return new CollectionsWorkQueueResponse(
            DatasetId: metadata.DatasetId,
            DatasetVersion: metadata.DatasetVersion,
            AsOfDate: metadata.BaseDate.ToString("yyyy-MM-dd"),
            AccountCount: totals?.AccountCount ?? items.Count,
            HighPriorityCount: totals?.HighPriorityCount ?? items.Count(item => item.CollectionTier == "High"),
            TotalBalanceAmount: totals?.TotalBalanceAmount ?? items.Sum(item => item.BalanceDueAmount),
            TotalPastDueAmount: totals?.TotalPastDueAmount ?? items.Sum(item => item.PastDueAmount),
            TotalOver90Amount: totals?.TotalOver90Amount ?? items.Sum(item => item.Over90Amount),
            Items: items);
    }

    public async Task<CollectionsFollowUpMutationResponse?> CreateCollectionsFollowUpAsync(
        CollectionsFollowUpCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            return null;
        }

        var billing = await GetForPatientAsync(request.PatientId.Trim(), cancellationToken);
        if (billing is null || billing.StatementSummary.PastDueAmount <= 0m)
        {
            return null;
        }

        var action = NormalizeText(request.Action)
            ?? CollectionRecommendedAction(billing.StatementSummary.OldestOpenAgeDays, billing.AgingSummary.Over90Amount);
        var assignedTo = NormalizeText(request.AssignedTo) ?? "billing";
        var note = NormalizeText(request.Note);
        var id = $"COLL-MODERN-{Guid.NewGuid():N}";
        var task = BuildCollectionsFollowUpTask(id, billing, assignedTo, action, note);

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                insert into messages
                    (id, patient_id, pid, message_date, title, body, status, assigned_to, deleted, activity)
                values
                    (@id, @patientId, @pid, @messageDate, @title, @body, 'New', @assignedTo, 0, 1);
                """;
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("patientId", billing.PatientId);
            command.Parameters.AddWithValue("pid", billing.LegacyPid);
            command.Parameters.Add("messageDate", NpgsqlDbType.Date).Value =
                DateOnly.ParseExact(billing.StatementSummary.StatementDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue("title", task.Title);
            command.Parameters.AddWithValue("body", task.Body);
            command.Parameters.AddWithValue("assignedTo", assignedTo);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new CollectionsFollowUpMutationResponse(id, task, billing);
    }

    public async Task<BillingLineMutationResponse?> CreateLineAsync(
        BillingLineCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.CodeType)
            || string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || request.Encounter <= 0
            || request.Fee < 0
            || request.Units < 0
            || !TryReadDate(request.BillingDate, out var billingDate))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, request.Encounter, cancellationToken);
        if (encounter is null)
        {
            return null;
        }

        if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        var id = $"BILL-MODERN-{Guid.NewGuid():N}";
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into billing
                (id, pid, provider_id, encounter, billing_date, code_type, code, code_text,
                 modifier, fee, justify, units, billed, activity)
            values
                (@id, @pid, @providerId, @encounter, @billingDate, @codeType, @code, @codeText,
                 @modifier, @fee, @justify, @units, 0, 1);
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("pid", patient.LegacyPid);
        command.Parameters.AddWithValue("providerId", request.ProviderId ?? encounter.ProviderId);
        command.Parameters.AddWithValue("encounter", encounter.Encounter);
        command.Parameters.Add("billingDate", NpgsqlDbType.Date).Value = billingDate;
        command.Parameters.AddWithValue("codeType", request.CodeType.Trim());
        command.Parameters.AddWithValue("code", request.Code.Trim());
        command.Parameters.AddWithValue("codeText", request.CodeText.Trim());
        command.Parameters.AddWithValue("modifier", NormalizeText(request.Modifier) ?? string.Empty);
        command.Parameters.AddWithValue("fee", request.Fee);
        command.Parameters.AddWithValue("justify", request.Justify.Trim());
        command.Parameters.AddWithValue("units", request.Units);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var billing = await GetForPatientAsync(patient.PatientId, cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(id, billing);
    }

    public BillingChargeTemplateResponse? GetChargeTemplate(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        return NormalizeText(templateId)?.ToLowerInvariant() switch
        {
            "office-visit" => new BillingChargeTemplateResponse(
                Id: "office-visit",
                Label: "Office visit",
                Code: "99213",
                Modifier: string.Empty,
                Description: "Established patient office visit",
                Fee: "125.00",
                Units: 1,
                Justify: "Z00.00"),
            "preventive-visit" => new BillingChargeTemplateResponse(
                Id: "preventive-visit",
                Label: "Preventive visit",
                Code: "99395",
                Modifier: string.Empty,
                Description: "Preventive medicine visit",
                Fee: "185.00",
                Units: 1,
                Justify: "Z00.00"),
            "telehealth-follow-up" => new BillingChargeTemplateResponse(
                Id: "telehealth-follow-up",
                Label: "Telehealth follow-up",
                Code: "99212",
                Modifier: "95",
                Description: "Established patient telehealth follow-up",
                Fee: "92.00",
                Units: 1,
                Justify: "Z00.00"),
            "complex-follow-up" => new BillingChargeTemplateResponse(
                Id: "complex-follow-up",
                Label: "Complex follow-up",
                Code: "99214",
                Modifier: "25",
                Description: "Complex established patient follow-up",
                Fee: "210.00",
                Units: 2,
                Justify: "K21.9"),
            _ => null
        };
    }

    public async Task<BillingLineMutationResponse?> UpdateLineStatusAsync(
        string billingLineId,
        BillingLineStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId)
            || !IsBinaryStatus(request.Billed)
            || !IsBinaryStatus(request.Activity))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var line = await GetBillingLineContextAsync(connection, billingLineId, cancellationToken);
        if (line is null)
        {
            return null;
        }

        if (await IsEncounterLockedAsync(connection, line.Value.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update billing
                set billed = @billed,
                    activity = @activity
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", billingLineId);
            command.Parameters.AddWithValue("billed", request.Billed);
            command.Parameters.AddWithValue("activity", request.Activity);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                return null;
            }
        }

        var billing = await GetForPatientAsync(line.Value.Pid.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<BillingLineMutationResponse?> UpdateLineAsync(
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || request.Fee < 0
            || request.Units < 0)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var line = await GetBillingLineContextAsync(connection, billingLineId, cancellationToken);
        if (line is null)
        {
            return null;
        }

        if (await IsEncounterLockedAsync(connection, line.Value.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update billing
                set code_text = @codeText,
                    modifier = @modifier,
                    fee = @fee,
                    units = @units,
                    justify = @justify
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", billingLineId);
            command.Parameters.AddWithValue("codeText", request.CodeText.Trim());
            command.Parameters.AddWithValue("modifier", NormalizeText(request.Modifier) ?? string.Empty);
            command.Parameters.AddWithValue("fee", request.Fee);
            command.Parameters.AddWithValue("units", request.Units);
            command.Parameters.AddWithValue("justify", request.Justify.Trim());
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                return null;
            }
        }

        var billing = await GetForPatientAsync(line.Value.Pid.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<bool> DeleteLineAsync(string billingLineId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var line = await GetBillingLineContextAsync(connection, billingLineId, cancellationToken);
        if (line is null)
        {
            return false;
        }

        if (await IsEncounterLockedAsync(connection, line.Value.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from billing
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", billingLineId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<BillingClaimMutationResponse?> CreateClaimAsync(
        BillingClaimCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || request.Encounter <= 0
            || request.PayerId < 0
            || request.PayerType <= 0
            || !IsClaimStatus(request.Status)
            || !IsBinaryStatus(request.BillProcess))
        {
            return null;
        }

        DateTime? billTime = TryReadOptionalDateTime(request.BillTime, out var parsedBillTime) ? parsedBillTime : null;
        DateTime? processTime = TryReadOptionalDateTime(request.ProcessTime, out var parsedProcessTime) ? parsedProcessTime : null;
        int version;
        int legacyPid;
        var id = $"CLAIM-MODERN-{Guid.NewGuid():N}";

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
            if (patient is null)
            {
                return null;
            }

            var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, request.Encounter, cancellationToken);
            if (encounter is null)
            {
                return null;
            }
            if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }

            legacyPid = patient.LegacyPid;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            version = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(version), 0) + 1 from claims where pid = @pid and encounter = @encounter;",
                cancellationToken,
                command =>
                {
                    command.Parameters.AddWithValue("pid", patient.LegacyPid);
                    command.Parameters.AddWithValue("encounter", encounter.Encounter);
                });

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into claims
                    (id, patient_id, pid, encounter, version, payer_id, payer_name, payer_type,
                     status, bill_process, bill_time, process_time, process_file, target,
                     x12_partner_id, submitted_claim)
                values
                    (@id, @patientId, @pid, @encounter, @version, @payerId, @payerName, @payerType,
                     @status, @billProcess, @billTime, @processTime, @processFile, @target,
                     @x12PartnerId, @submittedClaim);
                """;
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("patientId", patient.PatientId);
            command.Parameters.AddWithValue("pid", patient.LegacyPid);
            command.Parameters.AddWithValue("encounter", encounter.Encounter);
            command.Parameters.AddWithValue("version", version);
            command.Parameters.AddWithValue("payerId", request.PayerId);
            command.Parameters.AddWithValue("payerName", NormalizeText(request.PayerName) ?? string.Empty);
            command.Parameters.AddWithValue("payerType", request.PayerType);
            command.Parameters.AddWithValue("status", request.Status);
            command.Parameters.AddWithValue("billProcess", request.BillProcess);
            AddNullableTimestamp(command, "billTime", billTime);
            AddNullableTimestamp(command, "processTime", processTime);
            command.Parameters.AddWithValue("processFile", NormalizeText(request.ProcessFile) ?? string.Empty);
            command.Parameters.AddWithValue("target", NormalizeText(request.Target) ?? string.Empty);
            command.Parameters.AddWithValue("x12PartnerId", request.X12PartnerId ?? 0);
            command.Parameters.AddWithValue("submittedClaim", NormalizeText(request.SubmittedClaim) ?? string.Empty);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var billing = await GetForPatientAsync(legacyPid.ToString(), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(id, billing);
    }

    public async Task<BillingClaimMutationResponse?> UpdateClaimStatusAsync(
        string claimId,
        BillingClaimStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId)
            || !IsClaimStatus(request.Status)
            || !IsBinaryStatus(request.BillProcess))
        {
            return null;
        }

        DateTime? processTime = TryReadOptionalDateTime(request.ProcessTime, out var parsedProcessTime) ? parsedProcessTime : null;
        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            if (await GetClaimAsync(connection, claimId, cancellationToken) is null)
            {
                return null;
            }
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set status = @status,
                    bill_process = @billProcess,
                    process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            command.Parameters.AddWithValue("status", request.Status);
            command.Parameters.AddWithValue("billProcess", request.BillProcess);
            AddNullableTimestamp(command, "processTime", processTime);
            command.Parameters.AddWithValue("processFile", NormalizeText(request.ProcessFile) ?? string.Empty);
            command.Parameters.AddWithValue("target", NormalizeText(request.Target) ?? string.Empty);
            command.Parameters.AddWithValue("x12PartnerId", request.X12PartnerId ?? 0);
            command.Parameters.AddWithValue("submittedClaim", NormalizeText(request.SubmittedClaim) ?? string.Empty);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingClaimMutationResponse?> ScrubClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var lines = await GetBillingLinesAsync(connection, claim.Pid, [claim.Encounter], cancellationToken);
            var scrub = BuildClaimScrubReport(claim, lines);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            AddNullableTimestamp(command, "processTime", new DateTime(2026, 6, 18, 13, 5, 0));
            command.Parameters.AddWithValue("processFile", scrub.ProcessFile);
            command.Parameters.AddWithValue("target", NormalizeText(claim.Target) ?? "HCFA");
            command.Parameters.AddWithValue("x12PartnerId", string.Equals(claim.Target, "X12", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            command.Parameters.AddWithValue("submittedClaim", scrub.Report);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingClaimMutationResponse?> GenerateClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var generated = BuildGeneratedClaim837Payload(claim);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set status = @status,
                    bill_process = @billProcess,
                    process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            command.Parameters.AddWithValue("status", 2);
            command.Parameters.AddWithValue("billProcess", 0);
            AddNullableTimestamp(command, "processTime", new DateTime(2026, 6, 18, 14, 15, 0));
            command.Parameters.AddWithValue("processFile", generated.ProcessFile);
            command.Parameters.AddWithValue("target", "X12");
            command.Parameters.AddWithValue("x12PartnerId", 1);
            command.Parameters.AddWithValue("submittedClaim", generated.Payload);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingClaimMutationResponse?> ResubmitClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var resubmission = BuildClaimResubmissionPayload(claim);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set status = @status,
                    bill_process = @billProcess,
                    process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            command.Parameters.AddWithValue("status", 1);
            command.Parameters.AddWithValue("billProcess", 1);
            AddNullableTimestamp(command, "processTime", new DateTime(2026, 6, 18, 17, 10, 0));
            command.Parameters.AddWithValue("processFile", resubmission.ProcessFile);
            command.Parameters.AddWithValue("target", "X12");
            command.Parameters.AddWithValue("x12PartnerId", 1);
            command.Parameters.AddWithValue("submittedClaim", resubmission.Payload);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingClaimMutationResponse?> DenyClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var denial = BuildClaimDenialPayload(claim);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set status = @status,
                    bill_process = @billProcess,
                    process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            command.Parameters.AddWithValue("status", 7);
            command.Parameters.AddWithValue("billProcess", 0);
            AddNullableTimestamp(command, "processTime", new DateTime(2026, 6, 18, 15, 20, 0));
            command.Parameters.AddWithValue("processFile", denial.ProcessFile);
            command.Parameters.AddWithValue("target", "X12");
            command.Parameters.AddWithValue("x12PartnerId", 1);
            command.Parameters.AddWithValue("submittedClaim", denial.Payload);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingClaimMutationResponse?> ClearClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var payload = BuildClaimClearPayload(claim);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update claims
                set status = @status,
                    bill_process = @billProcess,
                    process_time = @processTime,
                    process_file = @processFile,
                    target = @target,
                    x12_partner_id = @x12PartnerId,
                    submitted_claim = @submittedClaim
                where id = @id
                returning pid;
                """;
            command.Parameters.AddWithValue("id", claimId);
            command.Parameters.AddWithValue("status", 3);
            command.Parameters.AddWithValue("billProcess", 0);
            AddNullableTimestamp(command, "processTime", null);
            command.Parameters.AddWithValue("processFile", string.Empty);
            command.Parameters.AddWithValue("target", "HCFA");
            command.Parameters.AddWithValue("x12PartnerId", 0);
            command.Parameters.AddWithValue("submittedClaim", payload);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            pid = result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingClaimMutationResponse(claimId, billing);
    }

    public async Task<BillingPaymentMutationResponse?> AdjudicateClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return null;
        }

        int sessionId;
        int legacyPid;
        var activityId = $"PAY-MODERN-{Guid.NewGuid():N}";

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var claim = await GetClaimAsync(connection, claimId, cancellationToken);
            if (claim is null)
            {
                return null;
            }

            var patient = await GetPatientAsync(connection, claim.Pid.ToString(CultureInfo.InvariantCulture), cancellationToken);
            if (patient is null)
            {
                return null;
            }

            var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, claim.Encounter, cancellationToken);
            if (encounter is null)
            {
                return null;
            }
            if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }

            legacyPid = patient.LegacyPid;
            var postDate = new DateOnly(2026, 6, 18);
            var postTime = postDate.ToDateTime(new TimeOnly(10, 45, 0));
            var payerName = NormalizeText(claim.PayerName) ?? $"Payer {claim.PayerId}";
            var payerType = claim.PayerType <= 0 ? 1 : claim.PayerType;
            var payerClaimNumber = BuildAdjudicatedPayerClaimNumber(claim);
            var adjudicated = BuildClaimAdjudicationPayload(claim);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            sessionId = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(id), 1200000) + 1 from payment_sessions;",
                cancellationToken);
            var sequenceNo = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(sequence_no), 0) + 1 from payment_activities where pid = @pid and encounter = @encounter;",
                cancellationToken,
                command =>
                {
                    command.Parameters.AddWithValue("pid", patient.LegacyPid);
                    command.Parameters.AddWithValue("encounter", encounter.Encounter);
                });

            await using (var sessionCommand = connection.CreateCommand())
            {
                sessionCommand.Transaction = transaction;
                sessionCommand.CommandText = """
                    insert into payment_sessions
                        (id, patient_id, pid, payer_id, payer_name, user_id, user_name, closed, reference,
                         check_date, deposit_date, pay_total, created_time, modified_time, global_amount,
                         payment_type, description, adjustment_code, post_to_date, payment_method)
                    values
                        (@id, @patientId, @pid, @payerId, @payerName, 119, 'gold-billing-01', 1, @reference,
                         @checkDate, @depositDate, @payTotal, @postTime, @postTime, 0,
                         'insurance_payment', @description, 'contractual_adjustment', @postDate, 'electronic_payment');
                    """;
                sessionCommand.Parameters.AddWithValue("id", sessionId);
                sessionCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                sessionCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                sessionCommand.Parameters.AddWithValue("payerId", claim.PayerId);
                sessionCommand.Parameters.AddWithValue("payerName", payerName);
                sessionCommand.Parameters.AddWithValue("reference", $"EOB-{claim.Encounter}-ADJUDICATED");
                sessionCommand.Parameters.Add("checkDate", NpgsqlDbType.Date).Value = postDate;
                sessionCommand.Parameters.Add("depositDate", NpgsqlDbType.Date).Value = postDate;
                sessionCommand.Parameters.AddWithValue("payTotal", 42m);
                sessionCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                sessionCommand.Parameters.AddWithValue("description", $"Adjudicated claim {claim.Encounter}");
                sessionCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var activityCommand = connection.CreateCommand())
            {
                activityCommand.Transaction = transaction;
                activityCommand.CommandText = """
                    insert into payment_activities
                        (id, session_id, patient_id, pid, encounter, sequence_no, code_type, code, modifier,
                         payer_type, post_time, post_user_id, post_user_name, memo, pay_amount, adj_amount,
                         modified_time, follow_up, follow_up_note, account_code, reason_code, deleted, post_date,
                         payer_claim_number)
                    values
                        (@id, @sessionId, @patientId, @pid, @encounter, @sequenceNo, 'CPT4', '99214', '',
                         @payerType, @postTime, 119, 'gold-billing-01', @memo, 42, 5.75,
                         @postTime, '', '', 'CO45', 'CO-45', null, @postDate, @payerClaimNumber);
                    """;
                activityCommand.Parameters.AddWithValue("id", activityId);
                activityCommand.Parameters.AddWithValue("sessionId", sessionId);
                activityCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                activityCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                activityCommand.Parameters.AddWithValue("encounter", encounter.Encounter);
                activityCommand.Parameters.AddWithValue("sequenceNo", sequenceNo);
                activityCommand.Parameters.AddWithValue("payerType", payerType);
                activityCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                activityCommand.Parameters.AddWithValue("memo", $"Adjudicated claim {claim.Encounter}");
                activityCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                activityCommand.Parameters.AddWithValue("payerClaimNumber", payerClaimNumber);
                await activityCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var claimCommand = connection.CreateCommand())
            {
                claimCommand.Transaction = transaction;
                claimCommand.CommandText = """
                    update claims
                    set status = @status,
                        bill_process = @billProcess,
                        process_time = @processTime,
                        process_file = @processFile,
                        target = @target,
                        x12_partner_id = @x12PartnerId,
                        submitted_claim = @submittedClaim
                    where id = @id;
                    """;
                claimCommand.Parameters.AddWithValue("id", claimId);
                claimCommand.Parameters.AddWithValue("status", 3);
                claimCommand.Parameters.AddWithValue("billProcess", 0);
                AddNullableTimestamp(claimCommand, "processTime", new DateTime(2026, 6, 18, 16, 5, 0));
                claimCommand.Parameters.AddWithValue("processFile", adjudicated.ProcessFile);
                claimCommand.Parameters.AddWithValue("target", "X12");
                claimCommand.Parameters.AddWithValue("x12PartnerId", 1);
                claimCommand.Parameters.AddWithValue("submittedClaim", adjudicated.Payload);
                await claimCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var billing = await GetForPatientAsync(legacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingPaymentMutationResponse(activityId, sessionId, billing);
    }

    public async Task<bool> DeleteClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await GetClaimAsync(connection, claimId, cancellationToken) is null)
        {
            return false;
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from claims where id = @id;";
        command.Parameters.AddWithValue("id", claimId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<BillingPaymentMutationResponse?> CreatePaymentAsync(
        BillingPaymentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var paymentType = NormalizeText(request.PaymentType);
        var isPatientRefund = string.Equals(paymentType, "patient_refund", StringComparison.OrdinalIgnoreCase);
        var isInsuranceReversal = string.Equals(paymentType, "insurance_reversal", StringComparison.OrdinalIgnoreCase);
        var isAdjustmentReversal = string.Equals(paymentType, "adjustment_reversal", StringComparison.OrdinalIgnoreCase);
        var allowsNegativePayment = isPatientRefund || isInsuranceReversal;
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || request.Encounter <= 0
            || request.PayerId < 0
            || request.PayerType is < 0 or > 3
            || string.IsNullOrWhiteSpace(request.Reference)
            || string.IsNullOrWhiteSpace(paymentType)
            || string.IsNullOrWhiteSpace(request.PaymentMethod)
            || string.IsNullOrWhiteSpace(request.Memo)
            || (request.AdjustmentAmount < 0m && !isAdjustmentReversal)
            || (request.PayAmount == 0m && request.AdjustmentAmount == 0m)
            || (request.PayAmount < 0m && !allowsNegativePayment)
            || (isPatientRefund && (request.PayAmount >= 0m || request.AdjustmentAmount != 0m || request.PayerType != 0 || request.PayerId != 0))
            || (isInsuranceReversal && (request.PayAmount >= 0m || request.AdjustmentAmount != 0m || request.PayerType == 0 || request.PayerId <= 0))
            || (isAdjustmentReversal && (request.PayAmount != 0m || request.AdjustmentAmount >= 0m || request.PayerType == 0 || request.PayerId <= 0))
            || !TryReadDate(request.PostDate, out var postDate))
        {
            return null;
        }

        var checkDate = TryReadOptionalDate(request.CheckDate, out var parsedCheckDate) ? parsedCheckDate : postDate;
        var depositDate = TryReadOptionalDate(request.DepositDate, out var parsedDepositDate) ? parsedDepositDate : postDate;
        var postTime = postDate.ToDateTime(new TimeOnly(10, 45, 0));
        int sessionId;
        int sequenceNo;
        int legacyPid;
        var activityId = $"PAY-MODERN-{Guid.NewGuid():N}";

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
            if (patient is null)
            {
                return null;
            }

            var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, request.Encounter, cancellationToken);
            if (encounter is null)
            {
                return null;
            }
            if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }

            legacyPid = patient.LegacyPid;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            sessionId = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(id), 1200000) + 1 from payment_sessions;",
                cancellationToken);
            sequenceNo = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(sequence_no), 0) + 1 from payment_activities where pid = @pid and encounter = @encounter;",
                cancellationToken,
                command =>
                {
                    command.Parameters.AddWithValue("pid", patient.LegacyPid);
                    command.Parameters.AddWithValue("encounter", encounter.Encounter);
                });

            await using (var sessionCommand = connection.CreateCommand())
            {
                sessionCommand.Transaction = transaction;
                sessionCommand.CommandText = """
                    insert into payment_sessions
                        (id, patient_id, pid, payer_id, payer_name, user_id, user_name, closed, reference,
                         check_date, deposit_date, pay_total, created_time, modified_time, global_amount,
                         payment_type, description, adjustment_code, post_to_date, payment_method)
                    values
                        (@id, @patientId, @pid, @payerId, @payerName, 119, 'gold-billing-01', 1, @reference,
                         @checkDate, @depositDate, @payTotal, @postTime, @postTime, 0,
                         @paymentType, @description, @adjustmentCode, @postDate, @paymentMethod);
                    """;
                sessionCommand.Parameters.AddWithValue("id", sessionId);
                sessionCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                sessionCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                sessionCommand.Parameters.AddWithValue("payerId", request.PayerId);
                sessionCommand.Parameters.AddWithValue("payerName", NormalizeText(request.PayerName) ?? string.Empty);
                sessionCommand.Parameters.AddWithValue("reference", request.Reference.Trim());
                sessionCommand.Parameters.Add("checkDate", NpgsqlDbType.Date).Value = checkDate;
                sessionCommand.Parameters.Add("depositDate", NpgsqlDbType.Date).Value = depositDate;
                sessionCommand.Parameters.AddWithValue("payTotal", request.PayAmount);
                sessionCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                sessionCommand.Parameters.AddWithValue("paymentType", paymentType);
                sessionCommand.Parameters.AddWithValue("description", request.Memo.Trim());
                sessionCommand.Parameters.AddWithValue("adjustmentCode", request.AdjustmentAmount > 0m ? "contractual_adjustment" : isAdjustmentReversal ? "adjustment_reversal" : string.Empty);
                sessionCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                sessionCommand.Parameters.AddWithValue("paymentMethod", request.PaymentMethod.Trim());
                await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var activityCommand = connection.CreateCommand())
            {
                activityCommand.Transaction = transaction;
                activityCommand.CommandText = """
                    insert into payment_activities
                        (id, session_id, patient_id, pid, encounter, sequence_no, code_type, code, modifier,
                         payer_type, post_time, post_user_id, post_user_name, memo, pay_amount, adj_amount,
                         modified_time, follow_up, follow_up_note, account_code, reason_code, deleted, post_date,
                         payer_claim_number)
                    values
                        (@id, @sessionId, @patientId, @pid, @encounter, @sequenceNo, @codeType, @code, @modifier,
                         @payerType, @postTime, 119, 'gold-billing-01', @memo, @payAmount, @adjustmentAmount,
                         @postTime, '', '', @accountCode, @reasonCode, null, @postDate, @payerClaimNumber);
                    """;
                activityCommand.Parameters.AddWithValue("id", activityId);
                activityCommand.Parameters.AddWithValue("sessionId", sessionId);
                activityCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                activityCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                activityCommand.Parameters.AddWithValue("encounter", encounter.Encounter);
                activityCommand.Parameters.AddWithValue("sequenceNo", sequenceNo);
                activityCommand.Parameters.AddWithValue("codeType", NormalizeText(request.CodeType) ?? string.Empty);
                activityCommand.Parameters.AddWithValue("code", NormalizeText(request.Code) ?? string.Empty);
                activityCommand.Parameters.AddWithValue("modifier", NormalizeText(request.Modifier) ?? string.Empty);
                activityCommand.Parameters.AddWithValue("payerType", request.PayerType);
                activityCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                activityCommand.Parameters.AddWithValue("memo", request.Memo.Trim());
                activityCommand.Parameters.AddWithValue("payAmount", request.PayAmount);
                activityCommand.Parameters.AddWithValue("adjustmentAmount", request.AdjustmentAmount);
                activityCommand.Parameters.AddWithValue("accountCode", NormalizeText(request.AccountCode) ?? string.Empty);
                activityCommand.Parameters.AddWithValue("reasonCode", NormalizeText(request.ReasonCode) ?? string.Empty);
                activityCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                activityCommand.Parameters.AddWithValue("payerClaimNumber", NormalizeText(request.PayerClaimNumber) ?? string.Empty);
                await activityCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var billing = await GetForPatientAsync(legacyPid.ToString(), cancellationToken);
        return billing is null ? null : new BillingPaymentMutationResponse(activityId, sessionId, billing);
    }

    public Task<BillingPaymentMutationResponse?> CreatePatientRefundAsync(
        BillingPatientRefundCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RefundAmount == 0m)
        {
            return Task.FromResult<BillingPaymentMutationResponse?>(null);
        }

        return CreatePaymentAsync(
            new BillingPaymentCreateRequest(
                request.PatientId,
                request.Encounter,
                0,
                string.Empty,
                0,
                request.Reference,
                request.PostDate,
                request.CheckDate,
                request.DepositDate,
                "patient_refund",
                request.PaymentMethod,
                request.CodeType,
                request.Code,
                request.Modifier,
                request.Memo,
                -Math.Abs(request.RefundAmount),
                0m,
                string.Empty,
                string.Empty,
                string.Empty),
            cancellationToken);
    }

    public Task<BillingPaymentMutationResponse?> CreatePatientPaymentAsync(
        BillingPatientPaymentCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PayAmount <= 0m)
        {
            return Task.FromResult<BillingPaymentMutationResponse?>(null);
        }

        return CreatePaymentAsync(
            new BillingPaymentCreateRequest(
                request.PatientId,
                request.Encounter,
                0,
                string.Empty,
                0,
                request.Reference,
                request.PostDate,
                request.CheckDate,
                request.DepositDate,
                "patient_payment",
                request.PaymentMethod,
                request.CodeType,
                request.Code,
                request.Modifier,
                request.Memo,
                request.PayAmount,
                0m,
                string.Empty,
                string.Empty,
                string.Empty),
            cancellationToken);
    }

    public Task<BillingPaymentMutationResponse?> CreateInsurancePaymentAsync(
        BillingInsurancePaymentCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PayerId <= 0
            || request.PayAmount <= 0m
            || request.AdjustmentAmount < 0m
            || string.IsNullOrWhiteSpace(request.PayerName)
            || string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            return Task.FromResult<BillingPaymentMutationResponse?>(null);
        }

        var reasonCode = request.ReasonCode.Trim();
        return CreatePaymentAsync(
            new BillingPaymentCreateRequest(
                request.PatientId,
                request.Encounter,
                request.PayerId,
                request.PayerName,
                1,
                request.Reference,
                request.PostDate,
                request.CheckDate,
                request.DepositDate,
                "insurance_payment",
                request.PaymentMethod,
                request.CodeType,
                request.Code,
                request.Modifier,
                request.Memo,
                request.PayAmount,
                request.AdjustmentAmount,
                reasonCode.Replace("-", string.Empty, StringComparison.Ordinal),
                reasonCode,
                request.PayerClaimNumber ?? string.Empty),
            cancellationToken);
    }

    public Task<BillingPaymentMutationResponse?> CreateInsuranceReversalAsync(
        BillingInsuranceReversalCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PayerId <= 0 || request.ReversalAmount == 0m || string.IsNullOrWhiteSpace(request.PayerName))
        {
            return Task.FromResult<BillingPaymentMutationResponse?>(null);
        }

        return CreatePaymentAsync(
            new BillingPaymentCreateRequest(
                request.PatientId,
                request.Encounter,
                request.PayerId,
                request.PayerName,
                1,
                request.Reference,
                request.PostDate,
                request.CheckDate,
                request.DepositDate,
                "insurance_reversal",
                request.PaymentMethod,
                request.CodeType,
                request.Code,
                request.Modifier,
                request.Memo,
                -Math.Abs(request.ReversalAmount),
                0m,
                string.Empty,
                string.Empty,
                request.PayerClaimNumber ?? string.Empty),
            cancellationToken);
    }

    public Task<BillingPaymentMutationResponse?> CreateAdjustmentReversalAsync(
        BillingAdjustmentReversalCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PayerId <= 0 || request.AdjustmentAmount == 0m || string.IsNullOrWhiteSpace(request.PayerName))
        {
            return Task.FromResult<BillingPaymentMutationResponse?>(null);
        }

        return CreatePaymentAsync(
            new BillingPaymentCreateRequest(
                request.PatientId,
                request.Encounter,
                request.PayerId,
                request.PayerName,
                1,
                request.Reference,
                request.PostDate,
                request.CheckDate,
                request.DepositDate,
                "adjustment_reversal",
                request.PaymentMethod,
                request.CodeType,
                request.Code,
                request.Modifier,
                request.Memo,
                0m,
                -Math.Abs(request.AdjustmentAmount),
                string.Empty,
                string.Empty,
                request.PayerClaimNumber ?? string.Empty),
            cancellationToken);
    }

    public async Task<BillingEobBatchImportResponse?> ImportEobBatchAsync(
        BillingEobBatchImportRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            return null;
        }

        var batchRows = new[]
        {
            new EobBatchImportRow(
                Reference: "EOB-BATCH-1000052-PRIMARY",
                Code: "99214",
                Memo: "Imported EOB batch primary",
                PayAmount: 28m,
                AdjustmentAmount: 4.25m,
                AccountCode: "CO45",
                ReasonCode: "CO-45",
                PayerClaimNumber: "EOB-BATCH-1000052-P1"),
            new EobBatchImportRow(
                Reference: "EOB-BATCH-1000052-SECONDARY",
                Code: "99213",
                Memo: "Imported EOB batch secondary",
                PayAmount: 11m,
                AdjustmentAmount: 1.5m,
                AccountCode: "PR2",
                ReasonCode: "PR-2",
                PayerClaimNumber: "EOB-BATCH-1000052-S1"),
        };

        var postDate = new DateOnly(2026, 6, 18);
        var postTime = postDate.ToDateTime(new TimeOnly(10, 45, 0));
        int legacyPid;
        var activityIds = new List<string>(batchRows.Length);
        var sessionIds = new List<int>(batchRows.Length);

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var patient = await GetPatientAsync(connection, request.PatientId, cancellationToken);
            if (patient is null)
            {
                return null;
            }

            var encounter = await GetEncounterForPatientAsync(connection, patient.LegacyPid, 1000052, cancellationToken);
            if (encounter is null)
            {
                return null;
            }
            if (await IsEncounterLockedAsync(connection, encounter.Encounter, cancellationToken))
            {
                throw new EncounterLockConflictException(
                    "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
            }

            legacyPid = patient.LegacyPid;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var nextSessionId = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(id), 1200000) + 1 from payment_sessions;",
                cancellationToken);
            var nextSequenceNo = await NextIntAsync(
                connection,
                transaction,
                "select coalesce(max(sequence_no), 0) + 1 from payment_activities where pid = @pid and encounter = @encounter;",
                cancellationToken,
                command =>
                {
                    command.Parameters.AddWithValue("pid", patient.LegacyPid);
                    command.Parameters.AddWithValue("encounter", encounter.Encounter);
                });

            foreach (var row in batchRows)
            {
                var sessionId = nextSessionId++;
                var sequenceNo = nextSequenceNo++;
                var activityId = $"PAY-MODERN-{Guid.NewGuid():N}";

                await using (var sessionCommand = connection.CreateCommand())
                {
                    sessionCommand.Transaction = transaction;
                    sessionCommand.CommandText = """
                        insert into payment_sessions
                            (id, patient_id, pid, payer_id, payer_name, user_id, user_name, closed, reference,
                             check_date, deposit_date, pay_total, created_time, modified_time, global_amount,
                             payment_type, description, adjustment_code, post_to_date, payment_method)
                        values
                            (@id, @patientId, @pid, 9005, 'Northstar HMO', 119, 'gold-billing-01', 1, @reference,
                             @checkDate, @depositDate, @payTotal, @postTime, @postTime, 0,
                             'insurance_payment', @description, 'contractual_adjustment', @postDate, 'electronic_payment');
                        """;
                    sessionCommand.Parameters.AddWithValue("id", sessionId);
                    sessionCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                    sessionCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                    sessionCommand.Parameters.AddWithValue("reference", row.Reference);
                    sessionCommand.Parameters.Add("checkDate", NpgsqlDbType.Date).Value = postDate;
                    sessionCommand.Parameters.Add("depositDate", NpgsqlDbType.Date).Value = postDate;
                    sessionCommand.Parameters.AddWithValue("payTotal", row.PayAmount);
                    sessionCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                    sessionCommand.Parameters.AddWithValue("description", row.Memo);
                    sessionCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                    await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var activityCommand = connection.CreateCommand())
                {
                    activityCommand.Transaction = transaction;
                    activityCommand.CommandText = """
                        insert into payment_activities
                            (id, session_id, patient_id, pid, encounter, sequence_no, code_type, code, modifier,
                             payer_type, post_time, post_user_id, post_user_name, memo, pay_amount, adj_amount,
                             modified_time, follow_up, follow_up_note, account_code, reason_code, deleted, post_date,
                             payer_claim_number)
                        values
                            (@id, @sessionId, @patientId, @pid, @encounter, @sequenceNo, 'CPT4', @code, '',
                             1, @postTime, 119, 'gold-billing-01', @memo, @payAmount, @adjustmentAmount,
                             @postTime, '', '', @accountCode, @reasonCode, null, @postDate, @payerClaimNumber);
                        """;
                    activityCommand.Parameters.AddWithValue("id", activityId);
                    activityCommand.Parameters.AddWithValue("sessionId", sessionId);
                    activityCommand.Parameters.AddWithValue("patientId", patient.PatientId);
                    activityCommand.Parameters.AddWithValue("pid", patient.LegacyPid);
                    activityCommand.Parameters.AddWithValue("encounter", encounter.Encounter);
                    activityCommand.Parameters.AddWithValue("sequenceNo", sequenceNo);
                    activityCommand.Parameters.AddWithValue("code", row.Code);
                    activityCommand.Parameters.Add("postTime", NpgsqlDbType.Timestamp).Value = postTime;
                    activityCommand.Parameters.AddWithValue("memo", row.Memo);
                    activityCommand.Parameters.AddWithValue("payAmount", row.PayAmount);
                    activityCommand.Parameters.AddWithValue("adjustmentAmount", row.AdjustmentAmount);
                    activityCommand.Parameters.AddWithValue("accountCode", row.AccountCode);
                    activityCommand.Parameters.AddWithValue("reasonCode", row.ReasonCode);
                    activityCommand.Parameters.Add("postDate", NpgsqlDbType.Date).Value = postDate;
                    activityCommand.Parameters.AddWithValue("payerClaimNumber", row.PayerClaimNumber);
                    await activityCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                activityIds.Add(activityId);
                sessionIds.Add(sessionId);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var billing = await GetForPatientAsync(legacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return billing is null ? null : new BillingEobBatchImportResponse(activityIds, sessionIds, billing);
    }

    public async Task<BillingPaymentMutationResponse?> VoidPaymentAsync(
        string activityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return null;
        }

        int? pid = null;
        int? sessionId = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await using (var lookup = connection.CreateCommand())
            {
                lookup.CommandText = "select pid, session_id, encounter from payment_activities where id = @id limit 1;";
                lookup.Parameters.AddWithValue("id", activityId);
                await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                pid = reader.GetInt32(0);
                sessionId = reader.GetInt32(1);
                var encounter = reader.GetInt32(2);
                await reader.DisposeAsync();
                if (await IsEncounterLockedAsync(connection, encounter, cancellationToken))
                {
                    throw new EncounterLockConflictException(
                        "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                update payment_activities
                set deleted = now(),
                    modified_time = now()
                where id = @id
                returning pid, session_id;
                """;
            command.Parameters.AddWithValue("id", activityId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (pid is null || sessionId is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingPaymentMutationResponse(activityId, sessionId.Value, billing);
    }

    public async Task<bool> DeletePaymentAsync(string activityId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        int? sessionId = null;
        await using (var lookupCommand = connection.CreateCommand())
        {
            lookupCommand.Transaction = transaction;
            lookupCommand.CommandText = "select session_id, encounter from payment_activities where id = @id limit 1;";
            lookupCommand.Parameters.AddWithValue("id", activityId);
            await using var reader = await lookupCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                sessionId = reader.GetInt32(0);
                var encounter = reader.GetInt32(1);
                await reader.DisposeAsync();
                if (await IsEncounterLockedAsync(connection, encounter, cancellationToken))
                {
                    throw new EncounterLockConflictException(
                        "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
                }
            }
        }

        if (sessionId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using (var activityCommand = connection.CreateCommand())
        {
            activityCommand.Transaction = transaction;
            activityCommand.CommandText = "delete from payment_activities where id = @id;";
            activityCommand.Parameters.AddWithValue("id", activityId);
            await activityCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var sessionCommand = connection.CreateCommand())
        {
            sessionCommand.Transaction = transaction;
            sessionCommand.CommandText = """
                delete from payment_sessions ps
                where ps.id = @sessionId
                  and not exists (
                    select 1
                    from payment_activities pa
                    where pa.session_id = ps.id
                  );
                """;
            sessionCommand.Parameters.AddWithValue("sessionId", sessionId.Value);
            await sessionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<BillingPaymentReceiptDocument?> GetPaymentReceiptDocumentAsync(
        string activityId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                pa.pid,
                pa.encounter,
                pa.sequence_no,
                ps.reference,
                ps.payer_name,
                pa.payer_type,
                ps.payment_type,
                ps.payment_method,
                pa.post_date::text as post_date,
                to_char(pa.post_time, 'YYYY-MM-DD HH24:MI:SS') as post_time,
                pa.code_type,
                pa.code,
                pa.modifier,
                pa.memo,
                pa.pay_amount,
                pa.adj_amount,
                pa.account_code,
                pa.reason_code,
                pa.payer_claim_number
            from payment_activities pa
            inner join payment_sessions ps on ps.id = pa.session_id
            where pa.id = @id
              and pa.deleted is null
            limit 1;
            """;
        command.Parameters.AddWithValue("id", activityId);

        int legacyPid;
        int encounter;
        int sequenceNo;
        string? reference;
        string? payerName;
        int payerType;
        string? paymentType;
        string? paymentMethod;
        string postDate;
        string? codeType;
        string? code;
        string? modifier;
        string? memo;
        decimal paymentAmount;
        decimal adjustmentAmount;
        string? accountCode;
        string? reasonCode;
        string? payerClaimNumber;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            legacyPid = reader.GetInt32(reader.GetOrdinal("pid"));
            encounter = reader.GetInt32(reader.GetOrdinal("encounter"));
            sequenceNo = reader.GetInt32(reader.GetOrdinal("sequence_no"));
            reference = ReadNullableString(reader, "reference");
            payerName = ReadNullableString(reader, "payer_name");
            payerType = reader.GetInt32(reader.GetOrdinal("payer_type"));
            paymentType = ReadNullableString(reader, "payment_type");
            paymentMethod = ReadNullableString(reader, "payment_method");
            postDate = ReadNullableString(reader, "post_date")
                ?? reader.GetString(reader.GetOrdinal("post_time"))[..10];
            codeType = ReadNullableString(reader, "code_type");
            code = ReadNullableString(reader, "code");
            modifier = ReadNullableString(reader, "modifier");
            memo = ReadNullableString(reader, "memo");
            paymentAmount = reader.GetDecimal(reader.GetOrdinal("pay_amount"));
            adjustmentAmount = reader.GetDecimal(reader.GetOrdinal("adj_amount"));
            accountCode = ReadNullableString(reader, "account_code");
            reasonCode = ReadNullableString(reader, "reason_code");
            payerClaimNumber = ReadNullableString(reader, "payer_claim_number");
        }

        var patient = await GetPatientAsync(connection, legacyPid.ToString(CultureInfo.InvariantCulture), cancellationToken);
        if (patient is null)
        {
            return null;
        }

        var receiptNumber = $"RCPT-{patient.Pubpid}-{postDate.Replace("-", string.Empty, StringComparison.Ordinal)}-{sequenceNo:000}";
        var payerTypeLabel = payerType switch
        {
            0 => "Patient",
            1 => "Primary insurance",
            2 => "Secondary insurance",
            3 => "Tertiary insurance",
            _ => $"Payer type {payerType}"
        };

        var document = new BillingPaymentReceiptDocument(
            ReceiptNumber: receiptNumber,
            Title: "Payment Receipt",
            PatientDisplayName: patient.DisplayName,
            Pubpid: patient.Pubpid,
            LegacyPid: patient.LegacyPid,
            Encounter: encounter,
            PostedDate: postDate,
            Reference: reference,
            PayerName: payerName,
            PayerTypeLabel: payerTypeLabel,
            PaymentType: paymentType,
            PaymentMethod: paymentMethod,
            CodeType: codeType,
            Code: code,
            Modifier: modifier,
            Memo: memo,
            PaymentAmount: paymentAmount,
            AdjustmentAmount: adjustmentAmount,
            AccountCode: accountCode,
            ReasonCode: reasonCode,
            PayerClaimNumber: payerClaimNumber,
            GeneratedText: string.Empty);

        var generatedText = string.Join(Environment.NewLine, BuildPaymentReceiptLines(document));
        return document with { GeneratedText = generatedText };
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

    private static async Task<BillingPatient?> GetPatientAsync(
        NpgsqlConnection connection,
        string patientId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select canonical_id, legacy_pid, pubpid, first_name, last_name, preferred_name,
                street, city, state, postal_code, email, phone, phone_home, phone_cell
            from patients
            where lower(canonical_id) = lower(@patientId)
               or lower(pubpid) = lower(@patientId)
               or legacy_pid::text = @patientId
            limit 1;
            """;
        command.Parameters.AddWithValue("patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var firstName = reader.GetString(reader.GetOrdinal("first_name"));
        var lastName = reader.GetString(reader.GetOrdinal("last_name"));
        var preferredName = ReadNullableString(reader, "preferred_name");

        return new BillingPatient(
            PatientId: reader.GetString(reader.GetOrdinal("canonical_id")),
            LegacyPid: reader.GetInt32(reader.GetOrdinal("legacy_pid")),
            Pubpid: reader.GetString(reader.GetOrdinal("pubpid")),
            FirstName: firstName,
            LastName: lastName,
            Street: ReadNullableString(reader, "street"),
            City: ReadNullableString(reader, "city"),
            State: ReadNullableString(reader, "state"),
            PostalCode: ReadNullableString(reader, "postal_code"),
            Email: ReadNullableString(reader, "email"),
            Phone: NormalizeText(ReadNullableString(reader, "phone_home"))
                ?? NormalizeText(ReadNullableString(reader, "phone"))
                ?? NormalizeText(ReadNullableString(reader, "phone_cell")),
            DisplayName: string.IsNullOrWhiteSpace(preferredName)
                ? $"{lastName}, {firstName}"
                : $"{lastName}, {firstName} ({preferredName})");
    }

    private static async Task<IReadOnlyList<BillingEncounterItem>> GetBillingEncountersAsync(
        NpgsqlConnection connection,
        int legacyPid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                e.id,
                e.encounter,
                e.encounter_date,
                e.reason,
                e.diagnosis_code,
                e.diagnosis_text,
                trim(concat(s.first_name, ' ', s.last_name)) as provider_name,
                f.name as facility_name
            from encounters e
            left join staff s on s.id = e.provider_id
            left join facilities f on f.id = e.facility_id
            where e.pid = @pid
              and exists (select 1 from billing b where b.pid = e.pid and b.encounter = e.encounter)
            order by e.encounter_date desc, e.encounter desc;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);

        var items = new List<BillingEncounterItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingEncounterItem(
                Id: reader.GetInt32(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                Date: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("encounter_date")).ToString("yyyy-MM-dd"),
                Reason: ReadNullableString(reader, "reason"),
                DiagnosisCode: ReadNullableString(reader, "diagnosis_code"),
                DiagnosisText: ReadNullableString(reader, "diagnosis_text"),
                ProviderName: ReadNullableString(reader, "provider_name"),
                FacilityName: ReadNullableString(reader, "facility_name"),
                TotalFee: 0m,
                PaymentAmount: 0m,
                AdjustmentAmount: 0m,
                BalanceAmount: 0m,
                AgeDays: 0,
                AgingBucket: "Current",
                Lines: [],
                Claims: [],
                Payments: []));
        }

        return items;
    }

    private static async Task<IReadOnlyList<BillingLineItem>> GetBillingLinesAsync(
        NpgsqlConnection connection,
        int legacyPid,
        IReadOnlyList<int> encounters,
        CancellationToken cancellationToken)
    {
        if (encounters.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, billing_date, code_type, code, modifier, code_text, fee, justify, units, billed, activity
            from billing
            where pid = @pid
              and encounter = any(@encounters)
              and activity = 1
            order by encounter desc, id;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounters", encounters.ToArray());

        var items = new List<BillingLineItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingLineItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                BillingDate: reader.GetFieldValue<DateOnly>(reader.GetOrdinal("billing_date")).ToString("yyyy-MM-dd"),
                CodeType: ReadNullableString(reader, "code_type"),
                Code: ReadNullableString(reader, "code"),
                Modifier: ReadNullableString(reader, "modifier"),
                CodeText: ReadNullableString(reader, "code_text"),
                Fee: ReadNullableDecimal(reader, "fee"),
                Justify: ReadNullableString(reader, "justify"),
                Units: ReadInt(reader, "units"),
                Billed: ReadInt(reader, "billed"),
                Activity: ReadInt(reader, "activity")));
        }

        return items;
    }

    private static async Task<IReadOnlyList<BillingClaimItem>> GetClaimsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        IReadOnlyList<int> encounters,
        CancellationToken cancellationToken)
    {
        if (encounters.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, encounter, version, payer_id, payer_name, payer_type, status, bill_process,
                   bill_time, process_time, process_file, target, submitted_claim
            from claims
            where pid = @pid
              and encounter = any(@encounters)
            order by encounter desc, version;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounters", encounters.ToArray());

        var items = new List<BillingClaimItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = ReadInt(reader, "status");
            var billProcess = ReadInt(reader, "bill_process");
            items.Add(new BillingClaimItem(
                Id: reader.GetString(reader.GetOrdinal("id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                Version: reader.GetInt32(reader.GetOrdinal("version")),
                PayerId: reader.GetInt32(reader.GetOrdinal("payer_id")),
                PayerName: ReadNullableString(reader, "payer_name"),
                PayerType: reader.GetInt32(reader.GetOrdinal("payer_type")),
                Status: status,
                StatusLabel: ClaimStatusLabel(status, billProcess),
                BillProcess: billProcess,
                BillTime: ReadNullableDateTime(reader, "bill_time"),
                ProcessTime: ReadNullableDateTime(reader, "process_time"),
                ProcessFile: ReadNullableString(reader, "process_file"),
                Target: ReadNullableString(reader, "target"),
                SubmittedClaim: ReadNullableString(reader, "submitted_claim")));
        }

        return items;
    }

    private static async Task<BillingClaimScrubContext?> GetClaimAsync(
        NpgsqlConnection connection,
        string claimId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select c.id, c.pid, coalesce(p.pubpid, c.pid::text) as patient_id, c.encounter, c.payer_id, c.payer_name, c.payer_type, c.target, c.status, c.bill_process, c.process_file, c.submitted_claim
            from claims c
            left join patients p on p.legacy_pid = c.pid
            where c.id = @id
            limit 1;
            """;
        command.Parameters.AddWithValue("id", claimId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var claim = new BillingClaimScrubContext(
            Id: reader.GetString(reader.GetOrdinal("id")),
            Pid: ReadInt(reader, "pid"),
            PatientId: reader.GetString(reader.GetOrdinal("patient_id")),
            Encounter: ReadInt(reader, "encounter"),
            PayerId: ReadInt(reader, "payer_id"),
            PayerName: ReadNullableString(reader, "payer_name"),
            PayerType: ReadInt(reader, "payer_type"),
            Target: ReadNullableString(reader, "target"),
            Status: ReadInt(reader, "status"),
            BillProcess: ReadInt(reader, "bill_process"),
            ProcessFile: ReadNullableString(reader, "process_file"),
            SubmittedClaim: ReadNullableString(reader, "submitted_claim"));
        await reader.DisposeAsync();
        if (await IsEncounterLockedAsync(connection, claim.Encounter, cancellationToken))
        {
            throw new EncounterLockConflictException(
                "This encounter has a locking signature. Add clinical changes through the governed amendment workflow.");
        }

        return claim;
    }

    private static async Task<IReadOnlyList<BillingPaymentItem>> GetPaymentsAsync(
        NpgsqlConnection connection,
        int legacyPid,
        IReadOnlyList<int> encounters,
        CancellationToken cancellationToken)
    {
        if (encounters.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                pa.id as activity_id,
                pa.encounter,
                pa.sequence_no,
                pa.session_id,
                ps.reference,
                ps.payer_name,
                pa.payer_type,
                ps.payment_type,
                ps.payment_method,
                ps.check_date::text as check_date,
                ps.deposit_date::text as deposit_date,
                pa.post_date::text as post_date,
                to_char(pa.post_time, 'YYYY-MM-DD HH24:MI:SS') as post_time,
                pa.code_type,
                pa.code,
                pa.modifier,
                pa.memo,
                pa.pay_amount,
                pa.adj_amount,
                pa.account_code,
                pa.reason_code,
                pa.payer_claim_number
            from payment_activities pa
            inner join payment_sessions ps on ps.id = pa.session_id
            where pa.pid = @pid
              and pa.encounter = any(@encounters)
              and pa.deleted is null
            order by pa.encounter desc, pa.sequence_no;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounters", encounters.ToArray());

        var items = new List<BillingPaymentItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BillingPaymentItem(
                ActivityId: reader.GetString(reader.GetOrdinal("activity_id")),
                Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
                SequenceNo: reader.GetInt32(reader.GetOrdinal("sequence_no")),
                SessionId: reader.GetInt32(reader.GetOrdinal("session_id")),
                Reference: ReadNullableString(reader, "reference"),
                PayerName: ReadNullableString(reader, "payer_name"),
                PayerType: reader.GetInt32(reader.GetOrdinal("payer_type")),
                PaymentType: ReadNullableString(reader, "payment_type"),
                PaymentMethod: ReadNullableString(reader, "payment_method"),
                CheckDate: ReadNullableString(reader, "check_date"),
                DepositDate: ReadNullableString(reader, "deposit_date"),
                PostDate: ReadNullableString(reader, "post_date"),
                PostTime: reader.GetString(reader.GetOrdinal("post_time")),
                CodeType: ReadNullableString(reader, "code_type"),
                Code: ReadNullableString(reader, "code"),
                Modifier: ReadNullableString(reader, "modifier"),
                Memo: ReadNullableString(reader, "memo"),
                PayAmount: reader.GetDecimal(reader.GetOrdinal("pay_amount")),
                AdjustmentAmount: reader.GetDecimal(reader.GetOrdinal("adj_amount")),
                AccountCode: ReadNullableString(reader, "account_code"),
                ReasonCode: ReadNullableString(reader, "reason_code"),
                PayerClaimNumber: ReadNullableString(reader, "payer_claim_number")));
        }

        return items;
    }

    private static async Task<bool> IsEncounterLockedAsync(
        NpgsqlConnection connection,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from encounter_signatures where encounter = @encounter and is_lock;";
        command.Parameters.AddWithValue("encounter", encounter);
        var lockCount = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(lockCount ?? 0, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<(int Pid, int Encounter)?> GetBillingLineContextAsync(
        NpgsqlConnection connection,
        string billingLineId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select pid, encounter from billing where id = @id limit 1;";
        command.Parameters.AddWithValue("id", billingLineId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? (reader.GetInt32(0), reader.GetInt32(1))
            : null;
    }

    private static async Task<BillingEncounterMutationContext?> GetEncounterForPatientAsync(
        NpgsqlConnection connection,
        int legacyPid,
        int encounter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select encounter, provider_id
            from encounters
            where pid = @pid and encounter = @encounter
            limit 1;
            """;
        command.Parameters.AddWithValue("pid", legacyPid);
        command.Parameters.AddWithValue("encounter", encounter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingEncounterMutationContext(
            Encounter: reader.GetInt32(reader.GetOrdinal("encounter")),
            ProviderId: reader.GetInt32(reader.GetOrdinal("provider_id")));
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        return reader.GetInt32(reader.GetOrdinal(columnName));
    }

    private static bool TryReadDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", out date)
            || DateOnly.TryParse(value, out date);
    }

    private static bool TryReadOptionalDate(string? value, out DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        return TryReadDate(value, out date);
    }

    private static bool TryReadOptionalDateTime(string? value, out DateTime dateTime)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            dateTime = default;
            return false;
        }

        return DateTime.TryParse(value, out dateTime);
    }

    private static void AddNullableTimestamp(NpgsqlCommand command, string name, DateTime? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Timestamp);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
    }

    private static DateOnly ReadDateOnly(string value, DateOnly fallback)
    {
        return TryReadDate(value, out var date) ? date : fallback;
    }

    private static async Task<int> NextIntAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        Action<NpgsqlCommand>? configure = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        configure?.Invoke(command);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? 1 : Convert.ToInt32(result);
    }

    private static string AgingBucketLabel(int ageDays)
    {
        if (ageDays <= 30)
        {
            return "Current";
        }

        if (ageDays <= 60)
        {
            return "31-60";
        }

        if (ageDays <= 90)
        {
            return "61-90";
        }

        return "Over 90";
    }

    private static decimal SumBalanceForBucket(IEnumerable<BillingEncounterItem> encounters, string bucket)
    {
        return encounters
            .Where(encounter => encounter.AgingBucket == bucket)
            .Sum(encounter => encounter.BalanceAmount);
    }

    private static BillingStatementSummary BuildStatementSummary(
        BillingPatient patient,
        BillingAccountSummary accountSummary,
        BillingAgingSummary agingSummary,
        BillingLedgerSummary ledgerSummary,
        IReadOnlyList<BillingEncounterItem> encounters,
        DateOnly fallbackDate)
    {
        var statementDate = ReadDateOnly(ledgerSummary.LastEntryDate ?? agingSummary.AsOfDate, fallbackDate);
        var periodStart = ledgerSummary.FirstEntryDate ?? agingSummary.AsOfDate;
        var openEncounters = encounters
            .Where(encounter => encounter.BalanceAmount > 0m)
            .ToList();
        var oldestOpen = openEncounters
            .OrderByDescending(encounter => encounter.AgeDays)
            .FirstOrDefault();
        var pastDueAmount = agingSummary.Days31To60Amount
            + agingSummary.Days61To90Amount
            + agingSummary.Over90Amount;
        var statementStatus = accountSummary.BalanceAmount <= 0m
            ? "No balance due"
            : pastDueAmount > 0m
                ? "Past due review"
                : "Ready for statement";

        return new BillingStatementSummary(
            StatementStatus: statementStatus,
            StatementPeriodStart: periodStart,
            StatementPeriodEnd: ledgerSummary.LastEntryDate ?? agingSummary.AsOfDate,
            StatementDate: statementDate.ToString("yyyy-MM-dd"),
            DueDate: statementDate.AddDays(30).ToString("yyyy-MM-dd"),
            RecipientName: $"{patient.FirstName} {patient.LastName}",
            MailingAddressLine1: NormalizeText(patient.Street) ?? string.Empty,
            MailingAddressLine2: BuildMailingAddressLine2(patient),
            Email: NormalizeText(patient.Email),
            Phone: NormalizeText(patient.Phone),
            OpenEncounterCount: openEncounters.Count,
            LedgerEntryCount: ledgerSummary.EntryCount,
            OldestOpenAgeDays: oldestOpen?.AgeDays ?? 0,
            OldestOpenDate: oldestOpen?.Lines
                .Select(line => line.BillingDate)
                .OrderBy(date => date)
                .FirstOrDefault() ?? periodStart,
            ChargeAmount: accountSummary.ChargeAmount,
            PaymentAmount: accountSummary.PaymentAmount,
            AdjustmentAmount: accountSummary.AdjustmentAmount,
            CurrentDueAmount: agingSummary.CurrentAmount,
            PastDueAmount: pastDueAmount,
            BalanceDueAmount: accountSummary.BalanceAmount);
    }

    private static string BuildMailingAddressLine2(BillingPatient patient)
    {
        var cityState = string.Join(", ", new[] { NormalizeText(patient.City), NormalizeText(patient.State) }
            .Where(value => value is not null));
        return string.Join(" ", new[] { NormalizeText(cityState), NormalizeText(patient.PostalCode) }
            .Where(value => value is not null));
    }

    private static BillingStatementDocument BuildStatementDocument(
        BillingPatient patient,
        BillingStatementSummary statementSummary,
        IReadOnlyList<BillingLedgerEntry> ledgerEntries)
    {
        const string title = "Patient Statement";
        var statementNumber = $"STMT-{patient.Pubpid}-{statementSummary.StatementDate.Replace("-", string.Empty)}";
        var paymentInstructions = statementSummary.BalanceDueAmount > 0m
            ? $"Please pay {FormatMoney(statementSummary.BalanceDueAmount)} by {statementSummary.DueDate}."
            : "No payment is due for this statement.";
        var lineItems = ledgerEntries
            .Select((entry, index) => BuildStatementLineItem(index + 1, entry))
            .ToList();
        var generatedLines = new List<string>
        {
            $"{title} {statementNumber}",
            statementSummary.RecipientName,
            statementSummary.MailingAddressLine1,
            statementSummary.MailingAddressLine2,
            $"Period {statementSummary.StatementPeriodStart} to {statementSummary.StatementPeriodEnd}",
            $"Statement date {statementSummary.StatementDate}",
            $"Due date {statementSummary.DueDate}",
            $"Total charges {FormatMoney(statementSummary.ChargeAmount)}",
            $"Payments {FormatMoney(statementSummary.PaymentAmount)}",
            $"Adjustments {FormatMoney(statementSummary.AdjustmentAmount)}",
            $"Current due {FormatMoney(statementSummary.CurrentDueAmount)}",
            $"Past due {FormatMoney(statementSummary.PastDueAmount)}",
            $"Balance due {FormatMoney(statementSummary.BalanceDueAmount)}",
            paymentInstructions
        };

        if (!string.IsNullOrWhiteSpace(statementSummary.Email))
        {
            generatedLines.Insert(4, $"Email {statementSummary.Email}");
        }

        if (!string.IsNullOrWhiteSpace(statementSummary.Phone))
        {
            generatedLines.Insert(5, $"Phone {statementSummary.Phone}");
        }

        return new BillingStatementDocument(
            StatementNumber: statementNumber,
            Title: title,
            StatementStatus: statementSummary.StatementStatus,
            StatementDate: statementSummary.StatementDate,
            DueDate: statementSummary.DueDate,
            StatementPeriodStart: statementSummary.StatementPeriodStart,
            StatementPeriodEnd: statementSummary.StatementPeriodEnd,
            RecipientName: statementSummary.RecipientName,
            MailingAddressLine1: statementSummary.MailingAddressLine1,
            MailingAddressLine2: statementSummary.MailingAddressLine2,
            Email: statementSummary.Email,
            Phone: statementSummary.Phone,
            PaymentInstructions: paymentInstructions,
            GeneratedText: string.Join('\n', generatedLines.Where(line => !string.IsNullOrWhiteSpace(line))),
            ChargeAmount: statementSummary.ChargeAmount,
            PaymentAmount: statementSummary.PaymentAmount,
            AdjustmentAmount: statementSummary.AdjustmentAmount,
            CurrentDueAmount: statementSummary.CurrentDueAmount,
            PastDueAmount: statementSummary.PastDueAmount,
            BalanceDueAmount: statementSummary.BalanceDueAmount,
            LineItems: lineItems);
    }

    private static StatementBatchCandidate BuildStatementBatchCandidate(PatientBillingResponse billing)
    {
        var summary = billing.StatementSummary;
        var document = billing.StatementDocument;

        return new StatementBatchCandidate(
            PatientId: billing.PatientId,
            LegacyPid: billing.LegacyPid,
            Pubpid: billing.Pubpid,
            PatientDisplayName: billing.PatientDisplayName,
            StatementNumber: document.StatementNumber,
            StatementStatus: summary.StatementStatus,
            StatementDate: summary.StatementDate,
            DueDate: summary.DueDate,
            BalanceDueAmount: summary.BalanceDueAmount,
            PastDueAmount: summary.PastDueAmount,
            CurrentDueAmount: summary.CurrentDueAmount,
            OpenEncounterCount: summary.OpenEncounterCount,
            LedgerEntryCount: summary.LedgerEntryCount,
            OldestOpenAgeDays: summary.OldestOpenAgeDays,
            OldestOpenDate: summary.OldestOpenDate,
            DeliveryMethod: NormalizeText(summary.Email) is null ? "Print" : "Email-ready");
    }

    private static string StatementDeliveryDestination(
        string deliveryMethod,
        BillingStatementSummary summary)
    {
        if (string.Equals(deliveryMethod, "Email-ready", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(summary.Email))
        {
            return summary.Email.Trim();
        }

        return string.IsNullOrWhiteSpace(summary.MailingAddressLine2)
            ? summary.MailingAddressLine1
            : $"{summary.MailingAddressLine1}, {summary.MailingAddressLine2}";
    }

    private static CollectionsWorkQueueItem BuildCollectionsWorkQueueItem(PatientBillingResponse billing)
    {
        var summary = billing.StatementSummary;
        var document = billing.StatementDocument;
        var over90Amount = billing.AgingSummary.Over90Amount;

        return new CollectionsWorkQueueItem(
            PatientId: billing.PatientId,
            LegacyPid: billing.LegacyPid,
            Pubpid: billing.Pubpid,
            PatientDisplayName: billing.PatientDisplayName,
            StatementNumber: document.StatementNumber,
            StatementDate: summary.StatementDate,
            DueDate: summary.DueDate,
            BalanceDueAmount: summary.BalanceDueAmount,
            PastDueAmount: summary.PastDueAmount,
            Over90Amount: over90Amount,
            CurrentDueAmount: summary.CurrentDueAmount,
            OpenEncounterCount: summary.OpenEncounterCount,
            LedgerEntryCount: summary.LedgerEntryCount,
            OldestOpenAgeDays: summary.OldestOpenAgeDays,
            OldestOpenDate: summary.OldestOpenDate,
            CollectionTier: CollectionTier(summary.OldestOpenAgeDays, over90Amount),
            RecommendedAction: CollectionRecommendedAction(summary.OldestOpenAgeDays, over90Amount),
            ContactMethod: CollectionContactMethod(summary.Email, summary.Phone),
            Email: NormalizeText(summary.Email),
            Phone: NormalizeText(summary.Phone));
    }

    private static CollectionsFollowUpTask BuildCollectionsFollowUpTask(
        string id,
        PatientBillingResponse billing,
        string assignedTo,
        string action,
        string? note)
    {
        var summary = billing.StatementSummary;
        var document = billing.StatementDocument;
        var over90Amount = billing.AgingSummary.Over90Amount;
        var tier = CollectionTier(summary.OldestOpenAgeDays, over90Amount);
        var title = $"Collections follow-up: {document.StatementNumber}";

        return new CollectionsFollowUpTask(
            Id: id,
            PatientId: billing.PatientId,
            LegacyPid: billing.LegacyPid,
            Pubpid: billing.Pubpid,
            PatientDisplayName: billing.PatientDisplayName,
            StatementNumber: document.StatementNumber,
            Title: title,
            Body: BuildCollectionsFollowUpBody(billing, action, tier, note),
            Status: "New",
            AssignedTo: assignedTo,
            Action: action,
            CollectionTier: tier,
            PastDueAmount: summary.PastDueAmount,
            Over90Amount: over90Amount);
    }

    private static string BuildCollectionsFollowUpBody(
        PatientBillingResponse billing,
        string action,
        string tier,
        string? note)
    {
        var summary = billing.StatementSummary;
        var document = billing.StatementDocument;
        var lines = new List<string>
        {
            "Collections follow-up created from the work queue.",
            $"Patient: {billing.PatientDisplayName} ({billing.Pubpid})",
            $"Statement: {document.StatementNumber}",
            $"Action: {action}",
            $"Priority: {tier}",
            $"Past due: {FormatMoney(summary.PastDueAmount)}",
            $"Over 90: {FormatMoney(billing.AgingSummary.Over90Amount)}",
            $"Balance: {FormatMoney(summary.BalanceDueAmount)}",
            $"Oldest open: {summary.OldestOpenDate} ({summary.OldestOpenAgeDays} days)",
            $"Due date: {summary.DueDate}"
        };

        if (!string.IsNullOrWhiteSpace(note))
        {
            lines.Add($"Note: {note}");
        }

        return string.Join('\n', lines);
    }

    private static string CollectionTier(int oldestOpenAgeDays, decimal over90Amount)
    {
        if (over90Amount > 0m || oldestOpenAgeDays >= 91)
        {
            return "High";
        }

        if (oldestOpenAgeDays >= 61)
        {
            return "Medium";
        }

        return "Early";
    }

    private static string CollectionRecommendedAction(int oldestOpenAgeDays, decimal over90Amount)
    {
        if (over90Amount > 0m || oldestOpenAgeDays >= 181)
        {
            return "Final notice review";
        }

        if (oldestOpenAgeDays >= 91)
        {
            return "Phone outreach";
        }

        if (oldestOpenAgeDays >= 61)
        {
            return "Second reminder";
        }

        return "First reminder";
    }

    private static string CollectionContactMethod(string? email, string? phone)
    {
        if (NormalizeText(email) is not null)
        {
            return "Email-ready";
        }

        return NormalizeText(phone) is null ? "Print" : "Phone";
    }

    private static BillingStatementLineItem BuildStatementLineItem(int lineNumber, BillingLedgerEntry entry)
    {
        return new BillingStatementLineItem(
            LineNumber: lineNumber,
            EntryDate: entry.EntryDate,
            Encounter: entry.Encounter,
            EntryType: entry.EntryType,
            Description: entry.Description,
            Code: entry.Code,
            Reference: entry.Reference,
            ChargeAmount: entry.EntryType == "Charge" ? entry.Amount : 0m,
            PaymentAmount: entry.EntryType == "Payment" ? Math.Abs(entry.Amount) : 0m,
            RefundAmount: entry.EntryType is "Refund" or "Reversal" ? entry.Amount : 0m,
            AdjustmentAmount: entry.EntryType is "Adjustment" or "Adjustment Reversal" ? Math.Abs(entry.Amount) : 0m,
            BalanceAmount: entry.RunningBalanceAmount);
    }

    private static string FormatMoney(decimal amount)
    {
        return string.Create(CultureInfo.InvariantCulture, $"${amount:0.00}");
    }

    private static byte[] BuildStatementPdf(BillingStatementDocument document)
    {
        var lines = new List<string>
        {
            $"{document.Title} {document.StatementNumber}",
            document.RecipientName,
            document.MailingAddressLine1,
            document.MailingAddressLine2,
            $"Statement status {document.StatementStatus}",
            $"Period {document.StatementPeriodStart} to {document.StatementPeriodEnd}",
            $"Statement date {document.StatementDate}",
            $"Due date {document.DueDate}",
            $"Total charges {FormatMoney(document.ChargeAmount)}",
            $"Payments {FormatMoney(document.PaymentAmount)}",
            $"Adjustments {FormatMoney(document.AdjustmentAmount)}",
            $"Current due {FormatMoney(document.CurrentDueAmount)}",
            $"Past due {FormatMoney(document.PastDueAmount)}",
            $"Balance due {FormatMoney(document.BalanceDueAmount)}",
            document.PaymentInstructions,
            string.Empty,
            "Statement lines"
        };

        lines.AddRange(document.LineItems.Select(line =>
            $"{line.LineNumber}. {line.EntryDate} Encounter {line.Encounter} {line.EntryType} {line.Description} "
            + $"Code {line.Code ?? "None"} Reference {line.Reference ?? "None"} "
            + $"Charge {FormatMoney(line.ChargeAmount)} Payment {FormatMoney(line.PaymentAmount)} "
            + $"Refund {FormatMoney(line.RefundAmount)} "
            + $"Adjustment {FormatMoney(line.AdjustmentAmount)} Balance {FormatMoney(line.BalanceAmount)}"));

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("50 760 Td");
        foreach (var line in lines)
        {
            contentBuilder.Append('(');
            contentBuilder.Append(EscapePdfText(line));
            contentBuilder.AppendLine(") Tj");
            contentBuilder.AppendLine("0 -14 Td");
        }
        contentBuilder.AppendLine("ET");

        var content = contentBuilder.ToString();
        var contentLength = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentLength} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int>();
        pdf.AppendLine("%PDF-1.4");
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1);
            pdf.AppendLine(" 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Length + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static IReadOnlyList<string> BuildPaymentReceiptLines(BillingPaymentReceiptDocument document)
    {
        var code = string.IsNullOrWhiteSpace(document.Code)
            ? "None"
            : $"{document.CodeType ?? "Code"} {document.Code}{(string.IsNullOrWhiteSpace(document.Modifier) ? string.Empty : $":{document.Modifier}")}";
        return
        [
            $"{document.Title} {document.ReceiptNumber}",
            document.PatientDisplayName,
            $"Patient ID {document.Pubpid}",
            $"PID {document.LegacyPid}",
            $"Encounter {document.Encounter}",
            $"Posted date {document.PostedDate}",
            $"Payer {(string.IsNullOrWhiteSpace(document.PayerName) ? document.PayerTypeLabel : document.PayerName)}",
            $"Payer type {document.PayerTypeLabel}",
            $"Reference {document.Reference ?? "None"}",
            $"Payment type {document.PaymentType ?? "None"}",
            $"Payment method {document.PaymentMethod ?? "None"}",
            $"Code {code}",
            $"Memo {document.Memo ?? "None"}",
            $"Payment amount {FormatMoney(document.PaymentAmount)}",
            $"Adjustment amount {FormatMoney(document.AdjustmentAmount)}",
            $"Account code {document.AccountCode ?? "None"}",
            $"Reason code {document.ReasonCode ?? "None"}",
            $"Payer claim number {document.PayerClaimNumber ?? "None"}"
        ];
    }

    private static byte[] BuildPaymentReceiptPdf(BillingPaymentReceiptDocument document)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 11 Tf");
        contentBuilder.AppendLine("50 760 Td");
        foreach (var line in BuildPaymentReceiptLines(document))
        {
            contentBuilder.Append('(');
            contentBuilder.Append(EscapePdfText(line));
            contentBuilder.AppendLine(") Tj");
            contentBuilder.AppendLine("0 -16 Td");
        }

        contentBuilder.AppendLine("ET");

        var content = contentBuilder.ToString();
        var contentLength = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentLength} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int>();
        pdf.AppendLine("%PDF-1.4");
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(i + 1);
            pdf.AppendLine(" 0 obj");
            pdf.AppendLine(objects[i]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Length + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Length + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string BuildStatementBatchSummaryCsv(IReadOnlyList<StatementBatchPackageEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("StatementNumber,Pubpid,PatientDisplayName,StatementStatus,StatementDate,DueDate,BalanceDueAmount,PastDueAmount,CurrentDueAmount,DeliveryMethod,FileName");
        foreach (var entry in entries)
        {
            builder.AppendLine(string.Join(
                ',',
                new[]
                {
                    EscapeCsv(entry.StatementNumber),
                    EscapeCsv(entry.Pubpid),
                    EscapeCsv(entry.PatientDisplayName),
                    EscapeCsv(entry.StatementStatus),
                    EscapeCsv(entry.StatementDate),
                    EscapeCsv(entry.DueDate),
                    EscapeCsv(entry.BalanceDueAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(entry.PastDueAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(entry.CurrentDueAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                    EscapeCsv(entry.DeliveryMethod),
                    EscapeCsv(entry.FileName)
                }));
        }

        return builder.ToString();
    }

    private static void AddArchiveEntry(
        ZipArchive archive,
        string entryName,
        byte[] content,
        DateTimeOffset timestamp)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        entry.LastWriteTime = timestamp;
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains('"', StringComparison.Ordinal)
            && !value.Contains(',', StringComparison.Ordinal)
            && !value.Contains('\n', StringComparison.Ordinal)
            && !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static IReadOnlyList<BillingLedgerEntry> BuildLedgerEntries(
        IReadOnlyList<BillingEncounterItem> encounters,
        DateOnly fallbackDate)
    {
        var draftEntries = new List<BillingLedgerDraft>();

        foreach (var encounter in encounters)
        {
            foreach (var line in encounter.Lines)
            {
                var fee = line.Fee ?? 0m;
                if (fee == 0m)
                {
                    continue;
                }

                var code = NormalizeText(line.Code);
                var description = NormalizeText(line.CodeText)
                    ?? NormalizeText($"{line.CodeType} {line.Code}")
                    ?? "Billing charge";
                draftEntries.Add(new BillingLedgerDraft(
                    EntryId: $"charge-{line.Id}",
                    EntryDate: ReadDateOnly(line.BillingDate, fallbackDate),
                    Encounter: line.Encounter,
                    EntryType: "Charge",
                    Description: description,
                    Code: code,
                    Reference: line.Id,
                    Amount: fee,
                    SortPriority: 0));
            }

            foreach (var payment in encounter.Payments)
            {
                var paymentDate = ReadDateOnly(payment.PostDate ?? payment.PostTime[..10], fallbackDate);
                var code = NormalizeText(payment.Code);
                var reference = NormalizeText(payment.Reference) ?? $"Session {payment.SessionId}";

                if (payment.PayAmount != 0m)
                {
                    var isRefund = payment.PayAmount < 0m;
                    var isInsuranceReversal = isRefund && payment.PayerType != 0;
                    draftEntries.Add(new BillingLedgerDraft(
                        EntryId: $"{(isRefund ? isInsuranceReversal ? "reversal" : "refund" : "payment")}-{payment.Encounter}-{payment.SequenceNo}",
                        EntryDate: paymentDate,
                        Encounter: payment.Encounter,
                        EntryType: isRefund ? isInsuranceReversal ? "Reversal" : "Refund" : "Payment",
                        Description: NormalizeText(payment.Memo) ?? (isRefund ? isInsuranceReversal ? "Insurance payment reversal" : "Patient refund" : "Payment posting"),
                        Code: code,
                        Reference: reference,
                        Amount: -payment.PayAmount,
                        SortPriority: 1));
                }

                if (payment.AdjustmentAmount != 0m)
                {
                    var isAdjustmentReversal = payment.AdjustmentAmount < 0m;
                    draftEntries.Add(new BillingLedgerDraft(
                        EntryId: $"{(isAdjustmentReversal ? "adjustment-reversal" : "adjustment")}-{payment.Encounter}-{payment.SequenceNo}",
                        EntryDate: paymentDate,
                        Encounter: payment.Encounter,
                        EntryType: isAdjustmentReversal ? "Adjustment Reversal" : "Adjustment",
                        Description: NormalizeText(payment.Memo) ?? (isAdjustmentReversal ? "Adjustment reversal" : "Adjustment"),
                        Code: code,
                        Reference: reference,
                        Amount: -payment.AdjustmentAmount,
                        SortPriority: 2));
                }
            }
        }

        var runningBalance = 0m;
        return draftEntries
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.Encounter)
            .ThenBy(entry => entry.SortPriority)
            .ThenBy(entry => entry.Code)
            .ThenBy(entry => entry.Description)
            .ThenBy(entry => entry.Reference)
            .ThenBy(entry => entry.EntryId)
            .Select(entry =>
            {
                runningBalance += entry.Amount;
                return new BillingLedgerEntry(
                    EntryId: entry.EntryId,
                    EntryDate: entry.EntryDate.ToString("yyyy-MM-dd"),
                    Encounter: entry.Encounter,
                    EntryType: entry.EntryType,
                    Description: entry.Description,
                    Code: entry.Code,
                    Reference: entry.Reference,
                    Amount: entry.Amount,
                    RunningBalanceAmount: runningBalance);
            })
            .ToList();
    }

    private static BillingClaimScrubReport BuildClaimScrubReport(
        BillingClaimScrubContext claim,
        IReadOnlyList<BillingLineItem> encounterLines)
    {
        var controlNumber = new string(claim.Id.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        controlNumber = controlNumber.Length > 12 ? controlNumber[..12] : controlNumber;
        if (string.IsNullOrWhiteSpace(controlNumber))
        {
            controlNumber = "CLAIM";
        }

        var cptLines = encounterLines
            .Where(line => string.Equals(line.CodeType, "CPT4", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var invalidCptCodes = cptLines
            .Select(line => (line.Code ?? string.Empty).Trim().ToUpperInvariant())
            .Where(code => !System.Text.RegularExpressions.Regex.IsMatch(code, "^\\d{5}$"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var futureServiceDates = cptLines
            .Select(line => NormalizeBillingDate(line.BillingDate))
            .Where(billingDate => billingDate is not null && billingDate > ClaimScrubBusinessDate)
            .Select(billingDate => billingDate!.Value.ToString("yyyy-MM-dd"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var modifierTokensByLine = cptLines
            .Select(line => ParseClaimModifierTokens(line.Modifier).ToList())
            .ToList();
        var invalidModifiers = modifierTokensByLine
            .SelectMany(modifiers => modifiers)
            .Where(modifier => !AllowedClaimModifiers.Contains(modifier))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var duplicateModifiers = modifierTokensByLine
            .SelectMany(modifiers => modifiers.Where((modifier, index) => modifiers.IndexOf(modifier) != index))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var incompatibleModifierCombinations = modifierTokensByLine
            .Where(modifiers => modifiers.Contains("25", StringComparer.OrdinalIgnoreCase)
                && modifiers.Contains("59", StringComparer.OrdinalIgnoreCase))
            .Select(_ => "25+59")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var modifierCountIssues = modifierTokensByLine
            .Select(modifiers => modifiers.Count)
            .Where(count => count > 4)
            .Distinct()
            .ToList();
        var diagnosisPointerTokensByLine = cptLines
            .Select(line => ParseClaimDiagnosisPointerTokens(line.Justify).ToList())
            .ToList();
        var duplicateDiagnosisPointers = diagnosisPointerTokensByLine
            .SelectMany(pointers => pointers.Where((pointer, index) => pointers.IndexOf(pointer) != index))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var diagnosisPointerCountIssues = diagnosisPointerTokensByLine
            .Select(pointers => pointers.Count)
            .Where(count => count > 4)
            .Distinct()
            .ToList();
        var diagnosisPointers = diagnosisPointerTokensByLine
            .SelectMany(pointers => pointers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var diagnosisCodeValues = encounterLines
            .Where(line => string.Equals(line.CodeType, "ICD10", StringComparison.OrdinalIgnoreCase))
            .Select(line => (line.Code ?? string.Empty).Trim().ToUpperInvariant())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
        var invalidDiagnosisCodes = diagnosisCodeValues
            .Where(diagnosisCode => !System.Text.RegularExpressions.Regex.IsMatch(diagnosisCode, "^[A-Z][0-9][0-9A-Z](?:\\.[0-9A-Z]{1,4})?$"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var duplicateDiagnosisCodes = diagnosisCodeValues
            .Where((diagnosisCode, index) => diagnosisCodeValues.IndexOf(diagnosisCode) != index)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var diagnosisCodes = diagnosisCodeValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unsupportedDiagnosisPointers = diagnosisCodes.Count == 0
            ? []
            : diagnosisPointerTokensByLine
                .SelectMany(pointers => pointers)
                .Where(pointer => !string.IsNullOrWhiteSpace(pointer) && !diagnosisCodes.Contains(pointer))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        var issues = new List<string>();

        if (claim.PayerId <= 0 || string.IsNullOrWhiteSpace(claim.PayerName))
        {
            issues.Add("missing-payer");
        }
        if (cptLines.Count == 0)
        {
            issues.Add("missing-cpt-line");
        }
        if (invalidCptCodes.Count > 0)
        {
            issues.Add($"invalid-cpt-code:{string.Join(",", invalidCptCodes)}");
        }
        if (futureServiceDates.Count > 0)
        {
            issues.Add($"future-service-date:{string.Join(",", futureServiceDates)}");
        }
        if (cptLines.Any(line => string.IsNullOrWhiteSpace(line.Justify)))
        {
            issues.Add("missing-diagnosis-pointer");
        }
        if (diagnosisCodes.Count == 0 && cptLines.Any(line => !string.IsNullOrWhiteSpace(line.Justify)))
        {
            issues.Add("missing-diagnosis-code");
        }
        if (unsupportedDiagnosisPointers.Count > 0)
        {
            issues.Add($"invalid-diagnosis-pointer:{string.Join(",", unsupportedDiagnosisPointers)}");
        }
        if (invalidDiagnosisCodes.Count > 0)
        {
            issues.Add($"invalid-diagnosis-code:{string.Join(",", invalidDiagnosisCodes)}");
        }
        if (duplicateDiagnosisCodes.Count > 0)
        {
            issues.Add($"duplicate-diagnosis-code:{string.Join(",", duplicateDiagnosisCodes)}");
        }
        if (cptLines.Any(line => IsInvalidClaimNumber(line.Fee, 0m)))
        {
            issues.Add("invalid-fee");
        }
        if (cptLines.Any(line => IsInvalidClaimNumber(line.Units, 1)))
        {
            issues.Add("invalid-units");
        }
        if (invalidModifiers.Count > 0)
        {
            issues.Add($"invalid-modifier:{string.Join(",", invalidModifiers)}");
        }
        if (duplicateModifiers.Count > 0)
        {
            issues.Add($"duplicate-modifier:{string.Join(",", duplicateModifiers)}");
        }
        if (incompatibleModifierCombinations.Count > 0)
        {
            issues.Add($"incompatible-modifier-combination:{string.Join(",", incompatibleModifierCombinations)}");
        }
        if (modifierCountIssues.Count > 0)
        {
            issues.Add($"modifier-count-exceeded:{string.Join(",", modifierCountIssues)}");
        }
        if (diagnosisPointerCountIssues.Count > 0)
        {
            issues.Add($"diagnosis-pointer-count-exceeded:{string.Join(",", diagnosisPointerCountIssues)}");
        }
        if (duplicateDiagnosisPointers.Count > 0)
        {
            issues.Add($"duplicate-diagnosis-pointer:{string.Join(",", duplicateDiagnosisPointers)}");
        }

        var status = issues.Count == 0 ? "PASS" : "FAIL";
        var processFile = $"CLAIM-{claim.Encounter}-{controlNumber}-SCRUB.txt";
        var report = string.Join("|", [
            $"SCRUB-{status}",
            $"claim={controlNumber}",
            $"patient={claim.PatientId}",
            $"encounter={claim.Encounter}",
            $"payer={claim.PayerName ?? claim.PayerId.ToString(CultureInfo.InvariantCulture)}",
            $"cptCount={cptLines.Count}",
            $"diagnosisPointers={(diagnosisPointers.Count > 0 ? string.Join(",", diagnosisPointers) : "none")}",
            $"issues={(issues.Count > 0 ? string.Join(",", issues) : "none")}"
        ]);

        return new BillingClaimScrubReport(processFile, report);
    }

    private static BillingGeneratedClaimPayload BuildGeneratedClaim837Payload(BillingClaimScrubContext claim)
    {
        var controlNumber = new string(claim.Id.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        controlNumber = controlNumber.Length > 12 ? controlNumber[..12] : controlNumber;
        if (string.IsNullOrWhiteSpace(controlNumber))
        {
            controlNumber = "CLAIM";
        }

        var payerName = NormalizeText(claim.PayerName) ?? $"Payer {claim.PayerId}";
        var payerCode = new string((claim.PayerId == 0 ? "UNKNOWN" : claim.PayerId.ToString(CultureInfo.InvariantCulture))
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(payerCode))
        {
            payerCode = "UNKNOWN";
        }

        var isaControlNumber = controlNumber.PadLeft(9, '0');
        isaControlNumber = isaControlNumber.Length > 9 ? isaControlNumber[^9..] : isaControlNumber;
        var processFile = $"CLAIM-{claim.Encounter}-{controlNumber}-837P.txt";
        var payload = string.Concat(
            $"ISA*00*          *00*          *ZZ*LEGACY_EHR        *ZZ*PAYER{payerCode.PadRight(10, ' ')}*260618*1415*^*00501*{isaControlNumber}*0*T*:~",
            $"GS*HC*LEGACY_EHR*PAYER{payerCode}*20260618*1415*{controlNumber}*X*005010X222A1~",
            $"ST*837*{controlNumber}*005010X222A1~",
            $"BHT*0019*00*{claim.Encounter}*20260618*1415*CH~",
            $"NM1*QC*1*PATIENT*{claim.PatientId}****MI*{claim.PatientId}~",
            $"CLM*{claim.Encounter}*0***11:B:1*Y*A*Y*I~",
            $"NM1*PR*2*{payerName}*****PI*{claim.PayerId}~",
            $"SE*7*{controlNumber}~");

        return new BillingGeneratedClaimPayload(processFile, payload);
    }

    private static BillingGeneratedClaimPayload BuildClaimResubmissionPayload(BillingClaimScrubContext claim)
    {
        var controlNumber = new string(claim.Id.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        controlNumber = controlNumber.Length > 12 ? controlNumber[..12] : controlNumber;
        if (string.IsNullOrWhiteSpace(controlNumber))
        {
            controlNumber = "CLAIM";
        }

        var payer = NormalizeText(claim.PayerName) ?? claim.PayerId.ToString(CultureInfo.InvariantCulture);
        var processFile = $"CLAIM-{claim.Encounter}-{controlNumber}-RESUBMIT.txt";
        var payload = string.Join("|", [
            "RESUBMIT",
            $"claim={controlNumber}",
            $"patient={claim.PatientId}",
            $"encounter={claim.Encounter}",
            $"payer={payer}",
            $"sourceStatus={ClaimStatusLabel(claim.Status, claim.BillProcess)}",
            "target=X12",
            "reason=corrected-and-requeued"
        ]);

        return new BillingGeneratedClaimPayload(processFile, payload);
    }

    private static BillingGeneratedClaimPayload BuildClaimDenialPayload(BillingClaimScrubContext claim)
    {
        var processFile = NormalizeText(claim.ProcessFile) ?? $"CLAIM-{claim.Encounter}-DENIAL-835.txt";
        var payload = NormalizeText(claim.SubmittedClaim) ?? $"Denied claim {claim.Encounter}";

        return new BillingGeneratedClaimPayload(processFile, payload);
    }

    private static string BuildClaimClearPayload(BillingClaimScrubContext claim)
        => NormalizeText(claim.SubmittedClaim) ?? $"Cleared claim {claim.Encounter}";

    private static BillingGeneratedClaimPayload BuildClaimAdjudicationPayload(BillingClaimScrubContext claim)
    {
        var processFile = $"CLAIM-{claim.Encounter}-EOB-835.txt";
        var payload = NormalizeText(claim.SubmittedClaim) ?? $"Adjudicated claim {claim.Encounter}";

        return new BillingGeneratedClaimPayload(processFile, payload);
    }

    private static string BuildAdjudicatedPayerClaimNumber(BillingClaimScrubContext claim)
        => $"ADJ-{claim.Id}"[..Math.Min(48, $"ADJ-{claim.Id}".Length)];

    private static IEnumerable<string> ParseClaimModifierTokens(string? modifier)
    {
        var value = (modifier ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length > 2 && value.Length % 2 == 0 && value.All(char.IsLetterOrDigit))
        {
            for (var index = 0; index < value.Length; index += 2)
            {
                yield return value.Substring(index, 2);
            }

            yield break;
        }

        foreach (var token in value.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token.ToUpperInvariant();
        }
    }

    private static IEnumerable<string> ParseClaimDiagnosisPointerTokens(string? justify)
    {
        foreach (var token in (justify ?? string.Empty).Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token.ToUpperInvariant();
        }
    }

    private static bool IsInvalidClaimNumber(decimal? value, decimal fallback)
    {
        return (value ?? fallback) <= 0m;
    }

    private static DateOnly? NormalizeBillingDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 10)
        {
            return null;
        }

        return DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", out var date) ? date : null;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ClaimStatusLabel(int status, int billProcess)
    {
        if (billProcess != 0)
        {
            return "Queued for billing";
        }

        return status switch
        {
            1 => "Re-opened",
            2 or 3 => "Marked as cleared",
            4 => "Closed",
            5 => "Canceled",
            6 => "Forwarded",
            7 => "Denied",
            _ => "Unsubmitted"
        };
    }

    private static bool IsBinaryStatus(int value)
    {
        return value is 0 or 1;
    }

    private static bool IsClaimStatus(int value)
    {
        return value is >= 0 and <= 7;
    }

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

    private sealed record StatementBatchRollupRow(
        int LegacyPid,
        int CandidateCount,
        decimal TotalBalanceAmount,
        decimal TotalPastDueAmount,
        decimal TotalCurrentDueAmount);

    private sealed record CollectionsWorkQueueRollupRow(
        int LegacyPid,
        int AccountCount,
        int HighPriorityCount,
        decimal TotalBalanceAmount,
        decimal TotalPastDueAmount,
        decimal TotalOver90Amount);

    private sealed record StatementBatchPackageManifest(
        string DatasetId,
        string DatasetVersion,
        string AsOfDate,
        string PackageId,
        int CandidateCount,
        int IncludedStatementCount,
        decimal TotalBalanceAmount,
        decimal TotalPastDueAmount,
        decimal TotalCurrentDueAmount,
        IReadOnlyList<StatementBatchPackageEntry> Entries);

    private sealed record StatementBatchPackageEntry(
        string Pubpid,
        int LegacyPid,
        string PatientDisplayName,
        string StatementNumber,
        string StatementStatus,
        string StatementDate,
        string DueDate,
        decimal BalanceDueAmount,
        decimal PastDueAmount,
        decimal CurrentDueAmount,
        string DeliveryMethod,
        string FileName);

    private sealed record PortalStatementDeliveryAccount(
        string PatientId,
        int LegacyPid,
        string PortalUsername);

    private sealed record BillingPatient(
        string PatientId,
        int LegacyPid,
        string Pubpid,
        string FirstName,
        string LastName,
        string? Street,
        string? City,
        string? State,
        string? PostalCode,
        string? Email,
        string? Phone,
        string DisplayName);

    private sealed record BillingEncounterMutationContext(
        int Encounter,
        int ProviderId);

    private sealed record BillingClaimScrubContext(
        string Id,
        int Pid,
        string PatientId,
        int Encounter,
        int PayerId,
        string? PayerName,
        int PayerType,
        string? Target,
        int Status,
        int BillProcess,
        string? ProcessFile,
        string? SubmittedClaim);

    private sealed record BillingClaimScrubReport(
        string ProcessFile,
        string Report);

    private sealed record BillingGeneratedClaimPayload(
        string ProcessFile,
        string Payload);

    private sealed record EobBatchImportRow(
        string Reference,
        string Code,
        string Memo,
        decimal PayAmount,
        decimal AdjustmentAmount,
        string AccountCode,
        string ReasonCode,
        string PayerClaimNumber);

    private sealed record BillingLedgerDraft(
        string EntryId,
        DateOnly EntryDate,
        int Encounter,
        string EntryType,
        string Description,
        string? Code,
        string? Reference,
        decimal Amount,
        int SortPriority);
}
