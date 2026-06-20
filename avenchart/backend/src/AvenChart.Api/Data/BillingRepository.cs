using System.Data.Common;
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
            LedgerEntries: ledgerEntries,
            Encounters: encounterSummaries);
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
            select encounter, version, payer_id, payer_name, payer_type, status, bill_process,
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

    private static DateOnly ReadDateOnly(string value, DateOnly fallback)
    {
        return TryReadDate(value, out var date) ? date : fallback;
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

    private sealed record DatasetMetadata(string DatasetId, string DatasetVersion, DateOnly BaseDate);

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
