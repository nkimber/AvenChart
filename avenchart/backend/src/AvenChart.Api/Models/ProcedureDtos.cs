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

public sealed record ProcedureReportReviewQueueResponse(
    string DatasetId,
    string DatasetVersion,
    string StatusFilter,
    string? PatientFilter,
    int? ProviderFilter,
    string? FromDate,
    string? ToDate,
    int Limit,
    int TotalReports,
    int ReviewedReports,
    int UnreviewedReports,
    IReadOnlyList<ProcedureReportReviewQueueItem> Reports);

public sealed record ProcedureReportReviewQueueItem(
    int ReportId,
    int OrderId,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    string OrderDate,
    int? ProviderId,
    string? ProviderName,
    string? ProcedureCode,
    string? ProcedureName,
    string ReportDate,
    string? ReportStatus,
    string? ReviewStatus,
    string? ReviewedBy,
    string? ReviewedAt,
    string? SpecimenNumber,
    string? Notes);

public sealed record ProcedureOrderCounts(
    int Orders,
    int CompletedOrders,
    int ScheduledOrders,
    int ReportlessOrders,
    int FutureScheduledOrders,
    int Specimens,
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
    IReadOnlyList<ProcedureSpecimenItem> Specimens,
    IReadOnlyList<ProcedureReportItem> Reports);

public sealed record ProcedureSpecimenItem(
    int Id,
    string? SpecimenIdentifier,
    string? AccessionIdentifier,
    string? SpecimenTypeCode,
    string? SpecimenType,
    string? CollectionMethodCode,
    string? CollectionMethod,
    string? SpecimenLocationCode,
    string? SpecimenLocation,
    string CollectedDate,
    decimal? VolumeValue,
    string? VolumeUnit,
    string? ConditionCode,
    string? SpecimenCondition,
    string? Comments);

public sealed record ProcedureReportItem(
    int Id,
    string DateCollected,
    string ReportDate,
    string? SpecimenNumber,
    string? Status,
    string? ReviewStatus,
    string? ReviewedBy,
    string? ReviewedAt,
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

public sealed record ProcedureOrderUpdateRequest(
    string DateOrdered,
    string Priority,
    string Status,
    string ProcedureCode,
    string ProcedureName,
    string ProcedureType,
    string Diagnosis,
    string Instructions);

public sealed record ProcedureReportCreateRequest(
    int OrderId,
    string DateCollected,
    string DateReport,
    string SpecimenNumber,
    string ReportStatus,
    string ReviewStatus,
    string Notes);

public sealed record ProcedureReportUpdateRequest(
    string DateCollected,
    string DateReport,
    string SpecimenNumber,
    string ReportStatus,
    string ReviewStatus,
    string Notes);

public sealed record ProcedureReportSignRequest(
    string ReviewedBy,
    string ReviewedAt);

public sealed record ProcedureSpecimenCreateRequest(
    int OrderId,
    string SpecimenIdentifier,
    string AccessionIdentifier,
    string SpecimenTypeCode,
    string SpecimenType,
    string CollectionMethodCode,
    string CollectionMethod,
    string SpecimenLocationCode,
    string SpecimenLocation,
    string CollectedDate,
    decimal? VolumeValue,
    string VolumeUnit,
    string ConditionCode,
    string SpecimenCondition,
    string Comments);

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

public sealed record ProcedureResultUpdateRequest(
    string ResultCode,
    string ResultText,
    string DateTime,
    string Units,
    string Result,
    string Range,
    string Abnormal,
    string Status);

public sealed record ProcedureMutationResponse(
    int Id,
    ProcedureResultsResponse Detail);
