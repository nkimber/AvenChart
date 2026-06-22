namespace AvenChart.Api.Models;

public sealed record HealthResponse(string Status, string Application, DateTimeOffset CheckedAtUtc);

public sealed record PatientSearchResponse(
    string DatasetId,
    string DatasetVersion,
    string? Search,
    int Limit,
    int TotalMatches,
    IReadOnlyList<PatientListItem> Patients);

public sealed record PatientListItem(
    string CanonicalId,
    int LegacyPid,
    string Pubpid,
    string DisplayName,
    string FirstName,
    string LastName,
    string? PreferredName,
    string? Sex,
    string DateOfBirth,
    int Age,
    string? Cohort,
    string? Purpose,
    string? Phone,
    string? PhoneHome,
    string? PhoneCell,
    string? Email,
    string? FacilityName,
    string? PrimaryProviderName,
    PatientActivityCounts Counts);

public sealed record PatientChartSummary(
    string CanonicalId,
    int LegacyPid,
    string Pubpid,
    string DisplayName,
    string FirstName,
    string LastName,
    string? PreferredName,
    string? Sex,
    string DateOfBirth,
    int Age,
    string? Cohort,
    string? Purpose,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? Email,
    string? Phone,
    string? PhoneHome,
    string? PhoneCell,
    string? HipaaAllowSms,
    string? HipaaAllowEmail,
    string? MaritalStatus,
    string? Occupation,
    bool PortalEnabled,
    string RegistrationDate,
    string? FacilityName,
    string? PrimaryProviderName,
    IReadOnlyList<PatientInsuranceItem> Insurance,
    IReadOnlyList<PatientDuplicateCandidate> DuplicateCandidates,
    PatientActivityCounts Counts,
    PatientTimelineItem? NextAppointment,
    PatientTimelineItem? LatestEncounter);

public sealed record PatientDuplicateSearchResponse(
    string DatasetId,
    string DatasetVersion,
    string? FirstName,
    string? LastName,
    string? DateOfBirth,
    string? Phone,
    string? Email,
    int Limit,
    int TotalCandidates,
    IReadOnlyList<PatientDuplicateCandidate> Candidates);

public sealed record PatientDuplicateCandidate(
    string CanonicalId,
    int LegacyPid,
    string Pubpid,
    string DisplayName,
    string FirstName,
    string LastName,
    string DateOfBirth,
    string? Phone,
    string? PhoneHome,
    string? PhoneCell,
    string? Email,
    int MatchScore,
    IReadOnlyList<string> MatchReasons);

public sealed record PatientActivityCounts(
    int Appointments,
    int Encounters,
    int Prescriptions,
    int BillingItems,
    int LabOrders,
    int Messages,
    int Problems,
    int Allergies,
    int Medications);

public sealed record PatientTimelineItem(
    string Id,
    string Date,
    string? Time,
    string Title,
    string? Status,
    string? ProviderName,
    string? FacilityName);

public sealed record PatientInsuranceItem(
    string Id,
    string? Type,
    string? Provider,
    string? PlanName,
    string? PolicyNumber,
    string? GroupNumber,
    string? Relationship);

public sealed record PatientInsuranceMutationRequest(
    string Type,
    string Provider,
    string PlanName,
    string PolicyNumber,
    string GroupNumber,
    string Relationship);

public sealed record PatientContactUpdateRequest(
    string? PhoneHome,
    string? PhoneCell,
    string? Email,
    string? HipaaAllowSms,
    string? HipaaAllowEmail);

public sealed record PatientDemographicsUpdateRequest(
    string? FirstName,
    string? LastName,
    string? PreferredName,
    string? Sex,
    string? DateOfBirth,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? MaritalStatus,
    string? Occupation);

public sealed record PatientRegistrationRequest(
    string? Pubpid,
    string? FirstName,
    string? LastName,
    string? PreferredName,
    string? Sex,
    string? DateOfBirth,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? MaritalStatus,
    string? Occupation,
    string? PhoneHome,
    string? PhoneCell,
    string? Email,
    string? HipaaAllowSms,
    string? HipaaAllowEmail);
