namespace AvenChart.Api.Models;

public sealed record OperationalReportsResponse(
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int CurrentYear,
    OperationalReportCounts Counts,
    IReadOnlyList<ProviderActivityReportItem> ProviderActivity,
    IReadOnlyList<FacilityActivityReportItem> FacilityActivity,
    IReadOnlyList<ClinicalConditionReportItem> ClinicalConditions);

public sealed record OperationalReportCounts(
    int Patients,
    int PortalPatients,
    int Appointments,
    int FutureAppointments,
    int CurrentYearAppointments,
    int Encounters,
    int CurrentYearEncounters,
    int BillingLines,
    decimal BillingTotal,
    int LabReports,
    int PatientDocuments,
    int Messages,
    int NewMessages,
    int DoneMessages,
    int Facilities,
    int Providers);

public sealed record ProviderActivityReportItem(
    string Username,
    string FirstName,
    string LastName,
    string DisplayName,
    int Encounters,
    int BillingLines,
    decimal BillingTotal);

public sealed record FacilityActivityReportItem(
    string Code,
    string Name,
    int Appointments,
    int Encounters,
    int BillingLines,
    decimal BillingTotal);

public sealed record ClinicalConditionReportItem(
    string Title,
    string Diagnosis,
    int Patients);

public sealed record ReportFamilyItem(string Key, string Name, string Description, bool SupportsDateRange);
public sealed record SavedReportDefinitionRequest(string Name, string Schedule, bool Active, string? ReportType = null);

public sealed record SavedReportDefinitionItem(
    Guid Id,
    string Name,
    string ReportType,
    string Schedule,
    bool Active,
    string CreatedBy,
    string CreatedAt,
    string? LastRunAt,
    int RunCount);

public sealed record SavedReportDefinitionsResponse(
    IReadOnlyList<SavedReportDefinitionItem> Definitions);

public sealed record SavedReportRunResponse(
    Guid DefinitionId,
    string RunId,
    string RanAt,
    string RanBy,
    string ReportType,
    string OutputFormat,
    int RowCount);

public sealed record ControlledInventoryReportRequest(DateOnly? AsOfDate, Guid? LocationId);

public sealed record ControlledInventoryReportLine(
    int LotId,
    string ItemCode,
    string ScheduleCode,
    string FacilityCode,
    string LocationCode,
    string LotNumber,
    decimal QuantityOnHand);

public sealed record ControlledInventoryReportRun(
    Guid RunId,
    string ReportKey,
    string AsOfDate,
    Guid? LocationId,
    string RequestedBy,
    string RequestedAt,
    int RowCount,
    string ResultChecksum);

public sealed record ControlledInventoryReportResponse(
    ControlledInventoryReportRun Run,
    IReadOnlyList<ControlledInventoryReportLine> Lines);

public sealed record ControlledInventoryActivityReportRequest(
    string ReportType,
    DateOnly? FromDate,
    DateOnly? ToDate,
    Guid? LocationId,
    string? PatientId);

public sealed record ControlledInventoryActivityReportLine(
    Guid EventId,
    string Action,
    int LotId,
    string ItemCode,
    string ScheduleCode,
    string FacilityCode,
    string LotNumber,
    string? SourceLocationCode,
    string? DestinationLocationCode,
    string? PatientId,
    int? Encounter,
    decimal Quantity,
    decimal QuantityDelta,
    string Reason,
    Guid? RelatedEventId,
    string PerformedBy,
    string OccurredAt,
    string? WitnessUsername,
    string? WitnessedAt);

public sealed record ControlledInventoryActivityReportRun(
    Guid RunId,
    string ReportKey,
    string ReportType,
    string FromDate,
    string ToDate,
    Guid? LocationId,
    string? PatientId,
    string RequestedBy,
    string RequestedAt,
    int RowCount,
    string ResultChecksum);

public sealed record ControlledInventoryActivityReportResponse(
    ControlledInventoryActivityReportRun Run,
    IReadOnlyList<ControlledInventoryActivityReportLine> Lines);

public sealed record ControlledCountVarianceReportRequest(DateOnly? FromDate, DateOnly? ToDate, Guid? LocationId);
public sealed record ControlledCountVarianceReportLine(Guid SessionId, Guid DiscrepancyId, string LocationCode, string CountType, string SessionStatus, int LotId, string ItemCode, string LotNumber, decimal ExpectedQuantity, decimal ObservedQuantity, decimal VarianceQuantity, string DiscrepancyStatus, Guid? CorrectionEventId, string StartedAt, string? SubmittedAt);
public sealed record ControlledCountVarianceReportRun(Guid RunId, string ReportKey, string FromDate, string ToDate, Guid? LocationId, string RequestedBy, string RequestedAt, int RowCount, string ResultChecksum);
public sealed record ControlledCountVarianceReportResponse(ControlledCountVarianceReportRun Run, IReadOnlyList<ControlledCountVarianceReportLine> Lines);
