// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
    int? ProviderId,
    int? FacilityId,
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
    string? Race,
    string? Ethnicity,
    string? Interpreter,
    string? FamilySize,
    string? MonthlyIncome,
    string? Homeless,
    string? FinancialReviewDate,
    string? MotherName,
    string? GuardianName,
    string? GuardianRelationship,
    string? GuardianPhone,
    string? GuardianEmail,
    string? GuardianSex,
    string? GuardianAddress,
    string? GuardianCity,
    string? GuardianState,
    string? GuardianPostalCode,
    string? GuardianCountry,
    string? GuardianWorkPhone,
    string? EmployerName,
    string? EmployerStreet,
    string? EmployerCity,
    string? EmployerState,
    string? EmployerPostalCode,
    string? EmployerCountry,
    bool PortalEnabled,
    PatientPortalAccountSummary? PortalAccount,
    string RegistrationDate,
    string? DeceasedDate,
    string? DeceasedReason,
    string LifecycleStatus,
    string? RetiredAt,
    string? RetiredBy,
    string? RetirementReason,
    int? ProviderId,
    int? FacilityId,
    string? FacilityName,
    string? PrimaryProviderName,
    PatientCareTeamSummary? CareTeam,
    IReadOnlyList<PatientInsuranceItem> Insurance,
    PatientHistorySummary? History,
    IReadOnlyList<PatientDuplicateCandidate> DuplicateCandidates,
    PatientActivityCounts Counts,
    PatientTimelineItem? NextAppointment,
    PatientTimelineItem? LatestEncounter);

public sealed record PatientPortalAccountSummary(
    bool PortalEnabled,
    string AccessStatusLabel,
    string? CmsPortalLogin,
    bool HasAccount,
    string? PortalUsername,
    string? PortalLoginUsername,
    int? PasswordStatus,
    string PasswordStatusLabel,
    bool OneTimeLinkPending,
    string ResetStatusLabel);

public sealed record PatientPortalAccountResetRequest(
    bool OneTimeLinkPending);

public sealed record PatientPortalAccountAccessRequest(
    bool PortalEnabled);

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

public sealed record PatientDuplicateReviewItem(string TargetPatientId,string SourcePatientId,string TargetDisplayName,string SourceDisplayName,string DateOfBirth,int MatchScore,IReadOnlyList<string> MatchReasons,string Status);
public sealed record PatientDuplicateReviewQueueResponse(IReadOnlyList<PatientDuplicateReviewItem> Items);
public sealed record PatientDuplicateReviewDispositionRequest(string TargetPatientId,string SourcePatientId,string Status,string? Note);

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

public sealed record PatientMergePreviewResponse(
    string DatasetId,
    string DatasetVersion,
    bool PreviewOnly,
    PatientMergePreviewPatient TargetPatient,
    PatientMergePreviewPatient SourcePatient,
    PatientActivityCounts TargetCounts,
    PatientActivityCounts SourceCounts,
    PatientActivityCounts CombinedCounts,
    int MatchScore,
    IReadOnlyList<string> MatchReasons,
    IReadOnlyList<string> Safeguards);

public sealed record PatientMergePreviewPatient(
    string CanonicalId,
    int LegacyPid,
    string Pubpid,
    string DisplayName,
    string FirstName,
    string LastName,
    string DateOfBirth,
    string? PhoneHome,
    string? PhoneCell,
    string? Email);

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
    string? Relationship,
    string? SubscriberFirstName,
    string? SubscriberMiddleName,
    string? SubscriberLastName,
    string? SubscriberDateOfBirth,
    string? SubscriberSex,
    string? SubscriberStreet,
    string? SubscriberStreetLine2,
    string? SubscriberCity,
    string? SubscriberState,
    string? SubscriberPostalCode,
    string? SubscriberCountry,
    string? SubscriberPhone,
    string? SubscriberEmployer,
    string? SubscriberEmployerStreet,
    string? SubscriberEmployerStreetLine2,
    string? SubscriberEmployerCity,
    string? SubscriberEmployerState,
    string? SubscriberEmployerPostalCode,
    string? SubscriberEmployerCountry);

public sealed record PatientCareTeamSummary(
    string TeamName,
    string TeamStatus,
    string TeamStatusDisplay,
    IReadOnlyList<PatientCareTeamMember> Members);

public sealed record PatientCareTeamMember(
    long Id,
    int? UserId,
    long? ContactId,
    string MemberType,
    string? MemberName,
    string Role,
    string RoleDisplay,
    int? FacilityId,
    string? FacilityName,
    string? ProviderSince,
    string Status,
    string StatusDisplay,
    string? Note);

public sealed record PatientHistorySummary(
    string? Coffee,
    string? Tobacco,
    string? Alcohol,
    string? SleepPatterns,
    string? ExercisePatterns,
    string? SeatbeltUse,
    string? Counseling,
    string? HazardousActivities,
    string? RecreationalDrugs,
    string? LastPhysicalExam,
    string? LastMammogram,
    string? LastProstateExam,
    string? LastColonoscopy,
    string? LastEcg,
    string? LastRetinal,
    string? LastFluvax,
    string? LastPneuvax,
    string? LastLdl,
    string? LastHemoglobin,
    string? LastPsa,
    string? LastExamResults,
    string? HistoryMother,
    string? HistoryFather,
    string? HistorySiblings,
    string? HistoryOffspring,
    string? HistorySpouse,
    string? RelativesCancer,
    string? RelativesTuberculosis,
    string? RelativesDiabetes,
    string? RelativesHighBloodPressure,
    string? RelativesHeartProblems,
    string? RelativesStroke,
    string? RelativesEpilepsy,
    string? RelativesMentalIllness,
    string? RelativesSuicide,
    string? AppendectomyDate,
    string? TonsillectomyDate,
    string? CholecystectomyDate,
    string? HeartSurgeryDate,
    string? HysterectomyDate,
    string? HerniaRepairDate,
    string? HipReplacementDate,
    string? KneeReplacementDate,
    string? AdditionalHistory,
    string? Exams,
    string? RecordedAt);

public sealed record PatientInsuranceMutationRequest(
    string Type,
    string Provider,
    string PlanName,
    string PolicyNumber,
    string GroupNumber,
    string Relationship,
    string? SubscriberFirstName,
    string? SubscriberMiddleName,
    string? SubscriberLastName,
    string? SubscriberDateOfBirth,
    string? SubscriberSex,
    string? SubscriberStreet,
    string? SubscriberStreetLine2,
    string? SubscriberCity,
    string? SubscriberState,
    string? SubscriberPostalCode,
    string? SubscriberCountry,
    string? SubscriberPhone,
    string? SubscriberEmployer,
    string? SubscriberEmployerStreet,
    string? SubscriberEmployerStreetLine2,
    string? SubscriberEmployerCity,
    string? SubscriberEmployerState,
    string? SubscriberEmployerPostalCode,
    string? SubscriberEmployerCountry);

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
    string? Occupation,
    string? Race,
    string? Ethnicity,
    string? Interpreter,
    string? FamilySize,
    string? MonthlyIncome,
    string? Homeless,
    string? FinancialReviewDate);

public sealed record PatientDeceasedStatusUpdateRequest(
    string? DeceasedDate,
    string? DeceasedReason,
    string? CorrectionReason);

public sealed record PatientDeceasedStatusHistoryItem(
    Guid EventId,
    string Action,
    string? PriorDeceasedDate,
    string? PriorDeceasedReason,
    string? ResultingDeceasedDate,
    string? ResultingDeceasedReason,
    string CorrectionReason,
    string Actor,
    string OccurredAt);

public sealed record PatientDeceasedStatusHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string? CurrentDeceasedDate,
    string? CurrentDeceasedReason,
    int EventCount,
    IReadOnlyList<PatientDeceasedStatusHistoryItem> Events);

public sealed record PatientLifecycleTransitionRequest(string? Reason);

public sealed record PatientLifecycleHistoryItem(
    Guid EventId,
    string Action,
    string PriorStatus,
    string ResultingStatus,
    string Reason,
    string Actor,
    string OccurredAt);

public sealed record PatientLifecycleHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    string CurrentStatus,
    string? RetiredAt,
    string? RetiredBy,
    string? RetirementReason,
    int EventCount,
    IReadOnlyList<PatientLifecycleHistoryItem> Events);

public sealed record PatientGuardianContactUpdateRequest(
    string? MotherName,
    string? GuardianName,
    string? GuardianRelationship,
    string? GuardianPhone,
    string? GuardianEmail,
    string? GuardianSex,
    string? GuardianAddress,
    string? GuardianCity,
    string? GuardianState,
    string? GuardianPostalCode,
    string? GuardianCountry,
    string? GuardianWorkPhone);

public sealed record PatientEmployerUpdateRequest(
    string? EmployerName,
    string? EmployerStreet,
    string? EmployerCity,
    string? EmployerState,
    string? EmployerPostalCode,
    string? EmployerCountry);

public sealed record PatientProviderAssignmentOptionsResponse(
    string DatasetId,
    string DatasetVersion,
    IReadOnlyList<PatientProviderAssignmentOption> Providers);

public sealed record PatientProviderAssignmentOption(
    int Id,
    string DisplayName,
    int? FacilityId,
    string? FacilityName);

public sealed record PatientCareTeamContactOption(
    long Id,
    string DisplayName,
    string? Relationship,
    string? Phone,
    string? Email);

public sealed record PatientCareTeamOptionsResponse(
    string DatasetId,
    string DatasetVersion,
    IReadOnlyList<PatientProviderAssignmentOption> Providers,
    IReadOnlyList<PatientCareTeamContactOption> Contacts);

public sealed record PatientProviderAssignmentUpdateRequest(
    int? ProviderId,
    string? Reason = null);

public sealed record PatientProviderAssignmentHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    int? CurrentProviderId,
    string? CurrentProviderName,
    int? CurrentFacilityId,
    string? CurrentFacilityName,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientProviderAssignmentHistoryItem> Events);

public sealed record PatientProviderAssignmentHistoryItem(
    Guid EventId,
    int? FromProviderId,
    string? FromProviderName,
    int? FromFacilityId,
    string? FromFacilityName,
    int? ToProviderId,
    string? ToProviderName,
    int? ToFacilityId,
    string? ToFacilityName,
    string Reason,
    string Actor,
    string OccurredAt);

public sealed record PatientAdministrationHistoryResponse(
    string DatasetId,
    string DatasetVersion,
    string PatientId,
    int LegacyPid,
    int EventCount,
    int ReturnedCount,
    int ResultLimit,
    IReadOnlyList<PatientAdministrationHistoryItem> Events);

public sealed record PatientAdministrationHistoryItem(
    Guid EventId,
    string Area,
    string Action,
    string? EntityId,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyDictionary<string, string?> BeforeValues,
    IReadOnlyDictionary<string, string?> AfterValues,
    string Actor,
    string OccurredAt);

public sealed record PatientCareTeamMemberUpdateRequest(
    int? UserId,
    long? ContactId,
    string? Role,
    int? FacilityId,
    string? ProviderSince,
    string? Status,
    string? Note);

public sealed record PatientCareTeamUpdateRequest(
    string? TeamName,
    string? TeamStatus,
    int? UserId,
    string? Role,
    int? FacilityId,
    string? ProviderSince,
    string? Status,
    string? Note,
    IReadOnlyList<PatientCareTeamMemberUpdateRequest>? Members = null);

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

public sealed record PatientRegistrationMutationResult(
    PatientChartSummary? Patient,
    IReadOnlyList<PatientRegistrationValidationIssue> ValidationIssues);

public sealed record PatientRegistrationValidationIssue(
    string Field,
    string Code,
    string Message);
