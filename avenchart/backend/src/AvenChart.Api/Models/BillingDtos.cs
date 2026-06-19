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
