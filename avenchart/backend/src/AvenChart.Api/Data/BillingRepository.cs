using System.Data.Common;
using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;

namespace AvenChart.Api.Data;

public sealed class BillingRepository(NpgsqlDataSource dataSource)
{
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
            PaymentAmount: -ledgerEntries.Where(entry => entry.EntryType == "Payment").Sum(entry => entry.Amount),
            AdjustmentAmount: -ledgerEntries.Where(entry => entry.EntryType == "Adjustment").Sum(entry => entry.Amount),
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

    public async Task<BillingLineMutationResponse?> CreateLineAsync(
        BillingLineCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || string.IsNullOrWhiteSpace(request.CodeType)
            || string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || string.IsNullOrWhiteSpace(request.Justify)
            || request.Encounter <= 0
            || request.Fee < 0
            || request.Units <= 0
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

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
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
            pid = result is null ? null : Convert.ToInt32(result);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<BillingLineMutationResponse?> UpdateLineAsync(
        string billingLineId,
        BillingLineUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId)
            || string.IsNullOrWhiteSpace(request.CodeText)
            || string.IsNullOrWhiteSpace(request.Justify)
            || request.Fee < 0
            || request.Units <= 0)
        {
            return null;
        }

        int? pid = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
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
            pid = result is null ? null : Convert.ToInt32(result);
        }

        if (pid is null)
        {
            return null;
        }

        var billing = await GetForPatientAsync(pid.Value.ToString(), cancellationToken);
        return billing is null ? null : new BillingLineMutationResponse(billingLineId, billing);
    }

    public async Task<bool> DeleteLineAsync(string billingLineId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(billingLineId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
            || request.PayerId <= 0
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
        await using (var command = connection.CreateCommand())
        {
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

    public async Task<bool> DeleteClaimAsync(string claimId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from claims where id = @id;";
        command.Parameters.AddWithValue("id", claimId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<BillingPaymentMutationResponse?> CreatePaymentAsync(
        BillingPaymentCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId)
            || request.Encounter <= 0
            || request.PayerId < 0
            || request.PayerType is < 0 or > 3
            || string.IsNullOrWhiteSpace(request.Reference)
            || string.IsNullOrWhiteSpace(request.PaymentType)
            || string.IsNullOrWhiteSpace(request.PaymentMethod)
            || string.IsNullOrWhiteSpace(request.Memo)
            || request.PayAmount < 0
            || request.AdjustmentAmount < 0
            || (request.PayAmount == 0m && request.AdjustmentAmount == 0m)
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
                sessionCommand.Parameters.AddWithValue("paymentType", request.PaymentType.Trim());
                sessionCommand.Parameters.AddWithValue("description", request.Memo.Trim());
                sessionCommand.Parameters.AddWithValue("adjustmentCode", request.AdjustmentAmount > 0m ? "contractual_adjustment" : string.Empty);
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
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                update payment_activities
                set deleted = now(),
                    modified_time = now()
                where id = @id
                returning pid, session_id;
                """;
            command.Parameters.AddWithValue("id", activityId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                pid = reader.GetInt32(reader.GetOrdinal("pid"));
                sessionId = reader.GetInt32(reader.GetOrdinal("session_id"));
            }
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
            lookupCommand.CommandText = "select session_id from payment_activities where id = @id limit 1;";
            lookupCommand.Parameters.AddWithValue("id", activityId);
            var result = await lookupCommand.ExecuteScalarAsync(cancellationToken);
            sessionId = result is null ? null : Convert.ToInt32(result);
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
            AdjustmentAmount: entry.EntryType == "Adjustment" ? Math.Abs(entry.Amount) : 0m,
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
                    draftEntries.Add(new BillingLedgerDraft(
                        EntryId: $"payment-{payment.Encounter}-{payment.SequenceNo}",
                        EntryDate: paymentDate,
                        Encounter: payment.Encounter,
                        EntryType: "Payment",
                        Description: NormalizeText(payment.Memo) ?? "Payment posting",
                        Code: code,
                        Reference: reference,
                        Amount: -payment.PayAmount,
                        SortPriority: 1));
                }

                if (payment.AdjustmentAmount != 0m)
                {
                    draftEntries.Add(new BillingLedgerDraft(
                        EntryId: $"adjustment-{payment.Encounter}-{payment.SequenceNo}",
                        EntryDate: paymentDate,
                        Encounter: payment.Encounter,
                        EntryType: "Adjustment",
                        Description: NormalizeText(payment.Memo) ?? "Adjustment",
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
