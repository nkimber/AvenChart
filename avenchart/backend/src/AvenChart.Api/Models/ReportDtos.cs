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
