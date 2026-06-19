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
    IReadOnlyList<ProcedureOrderItem> Orders);

public sealed record ProcedureOrderItem(
    int Id,
    int? Encounter,
    string? ProviderName,
    string OrderDate,
    string? Code,
    string? Name,
    string? Diagnosis,
    string? OrderStatus,
    IReadOnlyList<ProcedureReportItem> Reports);

public sealed record ProcedureReportItem(
    int Id,
    string ReportDate,
    string? Status,
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
