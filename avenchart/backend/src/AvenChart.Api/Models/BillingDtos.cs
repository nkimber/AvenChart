namespace AvenChart.Api.Models;

public sealed record PatientBillingResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    BillingAccountSummary AccountSummary,
    BillingAgingSummary AgingSummary,
    BillingLedgerSummary LedgerSummary,
    BillingStatementSummary StatementSummary,
    BillingStatementDocument StatementDocument,
    IReadOnlyList<BillingLedgerEntry> LedgerEntries,
    IReadOnlyList<BillingEncounterItem> Encounters);

public sealed record BillingAccountSummary(
    decimal ChargeAmount,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    decimal BalanceAmount);

public sealed record BillingAgingSummary(
    string AsOfDate,
    decimal CurrentAmount,
    decimal Days31To60Amount,
    decimal Days61To90Amount,
    decimal Over90Amount,
    decimal TotalBalanceAmount);

public sealed record BillingLedgerSummary(
    int EntryCount,
    string? FirstEntryDate,
    string? LastEntryDate,
    decimal ChargeAmount,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    decimal EndingBalanceAmount);

public sealed record BillingLedgerEntry(
    string EntryId,
    string EntryDate,
    int Encounter,
    string EntryType,
    string Description,
    string? Code,
    string? Reference,
    decimal Amount,
    decimal RunningBalanceAmount);

public sealed record BillingStatementSummary(
    string StatementStatus,
    string StatementPeriodStart,
    string StatementPeriodEnd,
    string StatementDate,
    string DueDate,
    string RecipientName,
    string MailingAddressLine1,
    string MailingAddressLine2,
    string? Email,
    string? Phone,
    int OpenEncounterCount,
    int LedgerEntryCount,
    int OldestOpenAgeDays,
    string OldestOpenDate,
    decimal ChargeAmount,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    decimal CurrentDueAmount,
    decimal PastDueAmount,
    decimal BalanceDueAmount);

public sealed record BillingStatementDocument(
    string StatementNumber,
    string Title,
    string StatementStatus,
    string StatementDate,
    string DueDate,
    string StatementPeriodStart,
    string StatementPeriodEnd,
    string RecipientName,
    string MailingAddressLine1,
    string MailingAddressLine2,
    string? Email,
    string? Phone,
    string PaymentInstructions,
    string GeneratedText,
    decimal ChargeAmount,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    decimal CurrentDueAmount,
    decimal PastDueAmount,
    decimal BalanceDueAmount,
    IReadOnlyList<BillingStatementLineItem> LineItems);

public sealed record BillingStatementLineItem(
    int LineNumber,
    string EntryDate,
    int Encounter,
    string EntryType,
    string Description,
    string? Code,
    string? Reference,
    decimal ChargeAmount,
    decimal PaymentAmount,
    decimal RefundAmount,
    decimal AdjustmentAmount,
    decimal BalanceAmount);

public sealed record StatementBatchResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int CandidateCount,
    decimal TotalBalanceAmount,
    decimal TotalPastDueAmount,
    decimal TotalCurrentDueAmount,
    IReadOnlyList<StatementBatchCandidate> Candidates);

public sealed record StatementBatchCandidate(
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string StatementNumber,
    string StatementStatus,
    string StatementDate,
    string DueDate,
    decimal BalanceDueAmount,
    decimal PastDueAmount,
    decimal CurrentDueAmount,
    int OpenEncounterCount,
    int LedgerEntryCount,
    int OldestOpenAgeDays,
    string OldestOpenDate,
    string DeliveryMethod);

public sealed record CollectionsWorkQueueResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int AccountCount,
    int HighPriorityCount,
    decimal TotalBalanceAmount,
    decimal TotalPastDueAmount,
    decimal TotalOver90Amount,
    IReadOnlyList<CollectionsWorkQueueItem> Items);

public sealed record CollectionsWorkQueueItem(
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string StatementNumber,
    string StatementDate,
    string DueDate,
    decimal BalanceDueAmount,
    decimal PastDueAmount,
    decimal Over90Amount,
    decimal CurrentDueAmount,
    int OpenEncounterCount,
    int LedgerEntryCount,
    int OldestOpenAgeDays,
    string OldestOpenDate,
    string CollectionTier,
    string RecommendedAction,
    string ContactMethod,
    string? Email,
    string? Phone);

public sealed record CollectionsFollowUpCreateRequest(
    string PatientId,
    string? AssignedTo,
    string? Action,
    string? Note);

public sealed record CollectionsFollowUpTask(
    string Id,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string StatementNumber,
    string Title,
    string Body,
    string Status,
    string AssignedTo,
    string Action,
    string CollectionTier,
    decimal PastDueAmount,
    decimal Over90Amount);

public sealed record CollectionsFollowUpMutationResponse(
    string Id,
    CollectionsFollowUpTask Task,
    PatientBillingResponse Detail);

public sealed record BillingEncounterItem(
    int Id,
    int Encounter,
    string Date,
    string? Reason,
    string? DiagnosisCode,
    string? DiagnosisText,
    string? ProviderName,
    string? FacilityName,
    decimal TotalFee,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    decimal BalanceAmount,
    int AgeDays,
    string AgingBucket,
    IReadOnlyList<BillingLineItem> Lines,
    IReadOnlyList<BillingClaimItem> Claims,
    IReadOnlyList<BillingPaymentItem> Payments);

public sealed record BillingLineItem(
    string Id,
    int Encounter,
    string BillingDate,
    string? CodeType,
    string? Code,
    string? Modifier,
    string? CodeText,
    decimal? Fee,
    string? Justify,
    int Units,
    int Billed,
    int Activity);

public sealed record BillingClaimItem(
    string Id,
    int Encounter,
    int Version,
    int PayerId,
    string? PayerName,
    int PayerType,
    int Status,
    string StatusLabel,
    int BillProcess,
    string? BillTime,
    string? ProcessTime,
    string? ProcessFile,
    string? Target,
    string? SubmittedClaim);

public sealed record BillingPaymentItem(
    string ActivityId,
    int Encounter,
    int SequenceNo,
    int SessionId,
    string? Reference,
    string? PayerName,
    int PayerType,
    string? PaymentType,
    string? PaymentMethod,
    string? CheckDate,
    string? DepositDate,
    string? PostDate,
    string PostTime,
    string? CodeType,
    string? Code,
    string? Modifier,
    string? Memo,
    decimal PayAmount,
    decimal AdjustmentAmount,
    string? AccountCode,
    string? ReasonCode,
    string? PayerClaimNumber);

public sealed record BillingPaymentReceiptDocument(
    string ReceiptNumber,
    string Title,
    string PatientDisplayName,
    string Pubpid,
    int LegacyPid,
    int Encounter,
    string PostedDate,
    string? Reference,
    string? PayerName,
    string PayerTypeLabel,
    string? PaymentType,
    string? PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string? Memo,
    decimal PaymentAmount,
    decimal AdjustmentAmount,
    string? AccountCode,
    string? ReasonCode,
    string? PayerClaimNumber,
    string GeneratedText);

public sealed record BillingLineCreateRequest(
    string PatientId,
    int? ProviderId,
    int Encounter,
    string BillingDate,
    string CodeType,
    string Code,
    string? Modifier,
    string CodeText,
    decimal Fee,
    int Units,
    string Justify);

public sealed record BillingLineUpdateRequest(
    string CodeText,
    string? Modifier,
    decimal Fee,
    int Units,
    string Justify);

public sealed record BillingLineStatusUpdateRequest(
    int Billed,
    int Activity);

public sealed record BillingLineMutationResponse(
    string Id,
    PatientBillingResponse Detail);

public sealed record BillingChargeTemplateResponse(
    string Id,
    string Label,
    string Code,
    string Modifier,
    string Description,
    string Fee,
    int Units,
    string Justify);

public sealed record BillingClaimCreateRequest(
    string PatientId,
    int Encounter,
    int PayerId,
    string? PayerName,
    int PayerType,
    int Status,
    int BillProcess,
    string? BillTime,
    string? ProcessTime,
    string? ProcessFile,
    string? Target,
    int? X12PartnerId,
    string? SubmittedClaim);

public sealed record BillingClaimStatusUpdateRequest(
    int Status,
    int BillProcess,
    string? ProcessTime,
    string? ProcessFile,
    string? Target,
    int? X12PartnerId,
    string? SubmittedClaim);

public sealed record BillingClaimMutationResponse(
    string Id,
    PatientBillingResponse Detail);

public sealed record BillingPaymentCreateRequest(
    string PatientId,
    int Encounter,
    int PayerId,
    string? PayerName,
    int PayerType,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentType,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal PayAmount,
    decimal AdjustmentAmount,
    string? AccountCode,
    string? ReasonCode,
    string? PayerClaimNumber);

public sealed record BillingPatientPaymentCreateRequest(
    string PatientId,
    int Encounter,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal PayAmount);

public sealed record BillingPatientRefundCreateRequest(
    string PatientId,
    int Encounter,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal RefundAmount);

public sealed record BillingInsurancePaymentCreateRequest(
    string PatientId,
    int Encounter,
    int PayerId,
    string PayerName,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal PayAmount,
    decimal AdjustmentAmount,
    string ReasonCode,
    string? PayerClaimNumber);

public sealed record BillingInsuranceReversalCreateRequest(
    string PatientId,
    int Encounter,
    int PayerId,
    string PayerName,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal ReversalAmount,
    string? PayerClaimNumber);

public sealed record BillingAdjustmentReversalCreateRequest(
    string PatientId,
    int Encounter,
    int PayerId,
    string PayerName,
    string Reference,
    string PostDate,
    string? CheckDate,
    string? DepositDate,
    string PaymentMethod,
    string? CodeType,
    string? Code,
    string? Modifier,
    string Memo,
    decimal AdjustmentAmount,
    string? PayerClaimNumber);

public sealed record BillingEobBatchImportRequest(
    string PatientId);

public sealed record BillingPaymentMutationResponse(
    string Id,
    int SessionId,
    PatientBillingResponse Detail);

public sealed record BillingEobBatchImportResponse(
    IReadOnlyList<string> Ids,
    IReadOnlyList<int> SessionIds,
    PatientBillingResponse Detail);
