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
