namespace AvenChart.Api.Models;

public sealed record ProcedureResultsResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string FirstName,
    string LastName,
    ProcedureOrderCounts Counts,
    IReadOnlyList<ProcedureOrderItem> Orders);

public sealed record ProcedureOrderCounts(
    int Orders,
    int CompletedOrders,
    int ScheduledOrders,
    int ReportlessOrders,
    int FutureScheduledOrders,
    int Reports,
    int Results,
    int FinalResults);

public sealed record ProcedureOrderItem(
    int Id,
    int? Encounter,
    string? ProviderName,
    string OrderDate,
    string? OrderPriority,
    string? Code,
    string? Name,
    string? ProcedureType,
    string? Diagnosis,
    string? Instructions,
    string? OrderStatus,
    IReadOnlyList<ProcedureReportItem> Reports);

public sealed record ProcedureReportItem(
    int Id,
    string ReportDate,
    string? Status,
    string? ReviewStatus,
    string? Notes,
    IReadOnlyList<ProcedureResultItem> Results);

public sealed record ProcedureResultItem(
    int Id,
    string? Code,
    string? Text,
    string? Units,
    string? Result,
    string? Range,
    string? Abnormal,
    string ResultDate,
    string? ResultStatus);

public sealed record ProcedureOrderCreateRequest(
    string PatientId,
    int? ProviderId,
    int EncounterId,
    string DateOrdered,
    string Priority,
    string Status,
    string ProcedureCode,
    string ProcedureName,
    string ProcedureType,
    string Diagnosis,
    string Instructions);

public sealed record ProcedureOrderStatusUpdateRequest(
    string Status);

public sealed record ProcedureReportCreateRequest(
    int OrderId,
    string DateCollected,
    string DateReport,
    string SpecimenNumber,
    string ReportStatus,
    string ReviewStatus,
    string Notes);

public sealed record ProcedureResultCreateRequest(
    int ReportId,
    string ResultCode,
    string ResultText,
    string DateTime,
    string Facility,
    string Units,
    string Result,
    string Range,
    string Abnormal,
    string Comments,
    string Status);

public sealed record ProcedureMutationResponse(
    int Id,
    ProcedureResultsResponse Detail);
