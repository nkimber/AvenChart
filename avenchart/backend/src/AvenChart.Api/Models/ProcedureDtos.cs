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
    int? LabFilter,
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
    int? LabId,
    string? LabName,
    string? ProcedureCode,
    string? ProcedureName,
    string ReportDate,
    string? ReportStatus,
    string? ReviewStatus,
    string? ReviewedBy,
    string? ReviewedAt,
    int ReviewVersion,
    int ReviewHistoryCount,
    string? SpecimenNumber,
    string? Notes);

public sealed record ProcedureOrderQueueResponse(
    string DatasetId,
    string DatasetVersion,
    string StatusFilter,
    string? PatientFilter,
    int? ProviderFilter,
    int? LabFilter,
    string? FromDate,
    string? ToDate,
    int Limit,
    int TotalOrders,
    int ReadyToSendOrders,
    int TransmittedPendingOrders,
    int ReportedOrders,
    int ScheduledOrders,
    int CompletedOrders,
    IReadOnlyList<ProcedureOrderQueueItem> Orders);

public sealed record ProcedureOrderQueueItem(
    int OrderId,
    string PatientId,
    int LegacyPid,
    string Pubpid,
    string PatientDisplayName,
    int? EncounterId,
    string OrderDate,
    int? ProviderId,
    string? ProviderName,
    int? LabId,
    string? LabName,
    string? ProcedureCode,
    string? ProcedureName,
    string? ProcedureType,
    string? OrderPriority,
    string? OrderStatus,
    string? DateTransmitted,
    int ReportCount,
    int ResultCount,
    int SpecimenCount,
    bool CanTransmit,
    string QueueState,
    string? Instructions);

public sealed record ProcedureLabProviderDirectoryResponse(
    string DatasetId,
    string DatasetVersion,
    bool IncludeInactive,
    int TotalProviders,
    int ActiveProviders,
    int InactiveProviders,
    IReadOnlyList<ProcedureLabProviderItem> Providers);

public sealed record ProcedureLabProviderItem(
    int Id,
    string Name,
    int? LabDirectorId,
    string? LabDirectorName,
    string? LabDirectorType,
    string? Npi,
    string? Protocol,
    string? Usage,
    string? Direction,
    string? Notes,
    bool Active,
    int OrderCount,
    int ReportCount,
    int FutureOrderCount);

public sealed record ProcedureLabProviderMutationRequest(
    string Name,
    int? LabDirectorId,
    string? Npi,
    string? Protocol,
    string? Usage,
    string? Direction,
    string? SendApplicationId,
    string? SendFacilityId,
    string? ReceiveApplicationId,
    string? ReceiveFacilityId,
    string? RemoteHost,
    string? Login,
    string? Password,
    string? OrdersPath,
    string? ResultsPath,
    string? Notes,
    bool Active);

public sealed record ProcedureLabProviderMutationResponse(
    int Id,
    ProcedureLabProviderDirectoryResponse Directory);

public sealed record ProcedureLabProviderAddressBookResponse(
    string DatasetId,
    string DatasetVersion,
    IReadOnlyList<ProcedureLabProviderAddressBookItem> Organizations);

public sealed record ProcedureLabProviderAddressBookItem(
    int Id,
    string Organization,
    string Type,
    bool Active);

public sealed record ProcedureLabProviderAddressBookMutationRequest(
    string Organization,
    string? Type,
    bool Active);

public sealed record ProcedureLabProviderAddressBookMutationResponse(
    int Id,
    ProcedureLabProviderAddressBookResponse AddressBook);

public sealed record ProcedureOrderCatalogResponse(
    string DatasetId,
    string DatasetVersion,
    int TotalItems,
    int GroupCount,
    int OrderCount,
    int LabProviderCount,
    IReadOnlyList<ProcedureOrderCatalogItem> Items);

public sealed record ProcedureOrderCatalogItem(
    int Id,
    int? ParentId,
    int? LabId,
    string? LabName,
    string Name,
    string? Code,
    string ItemType,
    string? ProcedureTypeName,
    string? Description,
    string? Specimen,
    string? StandardCode,
    int Sequence,
    bool Active,
    int ChildCount);

public sealed record ProcedureOrderCatalogMutationRequest(
    int? ParentId,
    int? LabId,
    string Name,
    string? Code,
    string? ItemType,
    string? ProcedureTypeName,
    string? Description,
    string? Specimen,
    string? StandardCode,
    int? Sequence,
    bool Active);

public sealed record ProcedureOrderCatalogMutationResponse(
    int Id,
    ProcedureOrderCatalogResponse Catalog);

public sealed record ProcedureOrderCatalogImportRequest(
    string VendorFormat,
    int ParentId,
    int LabId,
    string CsvText);

public sealed record ProcedureOrderCatalogImportItem(
    int Id,
    int? ParentId,
    string Code,
    string Name,
    string ItemType,
    bool Created,
    bool Reactivated);

public sealed record ProcedureOrderCatalogImportResponse(
    string VendorFormat,
    int ParentId,
    int LabId,
    int ImportedOrderCount,
    int CreatedOrderCount,
    int UpdatedOrderCount,
    int ReactivatedOrderCount,
    int DeactivatedOrderCount,
    int ImportedResultCount,
    int CreatedResultCount,
    int UpdatedResultCount,
    int ReactivatedResultCount,
    IReadOnlyList<ProcedureOrderCatalogImportItem> ImportedItems,
    ProcedureOrderCatalogResponse Catalog);

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
    int ReviewVersion,
    int ReviewHistoryCount,
    string? Notes,
    IReadOnlyList<ProcedureResultItem> Results);

public sealed record ProcedureReportReviewHistoryResponse(
    int ReportId,
    int ReviewVersion,
    IReadOnlyList<ProcedureReportReviewEventItem> Events);

public sealed record CriticalLabResultAcknowledgementRequest(
    int ExpectedVersion,
    string Reason);

public sealed record CriticalLabResultQueueItem(
    int ResultId,
    int ReportId,
    string PatientId,
    string PatientDisplayName,
    string? Code,
    string? Text,
    string? Result,
    string? Units,
    string? Abnormal,
    string ResultDate,
    string AcknowledgementStatus,
    int AcknowledgementVersion,
    string? AcknowledgedBy,
    string? AcknowledgedAt);

public sealed record CriticalLabResultQueueResponse(
    int TotalOpen,
    IReadOnlyList<CriticalLabResultQueueItem> Results);

public sealed record ProcedureReportReviewEventItem(
    long EventId,
    string Action,
    string? PreviousStatus,
    string CurrentStatus,
    string? AssignedTo,
    string Actor,
    string? Reason,
    int ExpectedVersion,
    int ResultingVersion,
    string OccurredAt);

public sealed record ProcedureResultItem(
    int Id,
    string? Code,
    string? Text,
    string? Units,
    string? Result,
    string? Range,
    string? Abnormal,
    string ResultDate,
    string? ResultStatus,
    int CurrentVersion,
    string VersionLabel,
    int VersionHistoryCount,
    bool HasPriorVersions,
    IReadOnlyList<ProcedureResultVersionItem> VersionHistory);

public sealed record ProcedureResultVersionItem(
    int Version,
    string VersionLabel,
    string VersionStatus,
    string CapturedAt,
    string? Code,
    string? Text,
    string? Units,
    string? Result,
    string? Range,
    string? Abnormal,
    string ResultDate,
    string? ResultStatus,
    string? CorrectionActor,
    string? CorrectionReason,
    int? ResultingVersion);

public sealed record ProcedureOrderCreateRequest(
    string PatientId,
    int? ProviderId,
    int? LabId,
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

public sealed record ProcedureOrderTransmitRequest(
    string? TransmittedAt);

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
    int ExpectedReviewVersion,
    string Reason);

public sealed record ProcedureReportReviewAssignmentRequest(
    string AssignedTo,
    int ExpectedReviewVersion,
    string Reason);

public sealed record ProcedureReportReviewDecisionRequest(
    int ExpectedReviewVersion,
    string Reason);

public sealed record ProcedureReportBulkSignRequest(
    IReadOnlyList<ProcedureReportBulkSignItem> Reports,
    string Reason);

public sealed record ProcedureReportBulkSignItem(
    int ReportId,
    int ExpectedReviewVersion);

public sealed record ProcedureReportBulkSignResponse(
    int RequestedCount,
    int SignedCount,
    IReadOnlyList<int> SignedReportIds,
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
    string Status,
    int ExpectedVersion,
    string Reason);

public sealed class ProcedureResultCorrectionConflictException(
    int expectedVersion,
    int currentVersion,
    string reviewStatus,
    string message) : Exception(message)
{
    public int ExpectedVersion { get; } = expectedVersion;
    public int CurrentVersion { get; } = currentVersion;
    public string ReviewStatus { get; } = reviewStatus;
}

public sealed record ProcedureMutationResponse(
    int Id,
    ProcedureResultsResponse Detail);
