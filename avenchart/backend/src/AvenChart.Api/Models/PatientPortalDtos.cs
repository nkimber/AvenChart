namespace AvenChart.Api.Models;

public sealed record PatientPortalLoginRequest(
    string Username,
    string Password);

public sealed record PatientPortalLoginResponse(
    bool Authenticated,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string? FailureReason,
    Guid? SessionId,
    DateTimeOffset? SessionCreatedAt,
    DateTimeOffset? SessionExpiresAt,
    string SessionSource);

public sealed record PatientPortalSessionResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? EndedAt,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalHomeSummaryResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    PatientPortalHomeMessageSummary Messages,
    int UpcomingAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> UpcomingAppointments,
    int ImmunizationCount,
    IReadOnlyList<PatientPortalHomeImmunizationSummary> Immunizations,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalHomeMessageSummary(
    int TotalMessages,
    int NewMessages,
    int DoneMessages,
    string? LatestMessageTitle,
    string? LatestMessageDate);

public sealed record PatientPortalHomeAppointmentSummary(
    string Id,
    string Date,
    string StartTime,
    string Title,
    string? Status,
    int? CategoryId,
    string? CategoryName,
    string? ProviderName,
    string? FacilityName,
    string? Comments);

public sealed record PatientPortalHomeImmunizationSummary(
    int Id,
    string? AdministeredDate,
    string? AdministeredFormatted,
    int? ImmunizationId,
    string? CvxCode,
    string CodeText,
    string? Note,
    string? CompletionStatus,
    int AddedErroneously);

public sealed record PatientPortalAppointmentsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int UpcomingAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> UpcomingAppointments,
    int PastAppointmentCount,
    IReadOnlyList<PatientPortalHomeAppointmentSummary> PastAppointments,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalClinicalSummaryResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int ProblemCount,
    IReadOnlyList<PatientPortalProblemItem> Problems,
    int AllergyCount,
    IReadOnlyList<PatientPortalAllergyItem> Allergies,
    int MedicationCount,
    IReadOnlyList<PatientPortalMedicationItem> Medications,
    int PrescriptionCount,
    IReadOnlyList<PatientPortalPrescriptionItem> Prescriptions,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalProblemItem(
    string Id,
    string Title,
    string? ReportedDate,
    string? StartDate,
    string? EndDate);

public sealed record PatientPortalAllergyItem(
    string Id,
    string Title,
    string? ReportedDate,
    string? StartDate,
    string? EndDate,
    string? ReferredBy,
    string? Reaction,
    string? Severity);

public sealed record PatientPortalMedicationItem(
    string Id,
    string Title,
    string? StartDate,
    string? ModifiedDate,
    string? EndDate);

public sealed record PatientPortalPrescriptionItem(
    string Id,
    string Drug,
    string? StartDate,
    string? ModifiedDate,
    string? EndDate,
    string? Dosage,
    string? Quantity,
    string? Route,
    string? Note);

public sealed record PatientPortalLabResultsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int OrderCount,
    int ReportCount,
    int ResultCount,
    IReadOnlyList<PatientPortalLabOrderItem> Orders,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalLabOrderItem(
    string Id,
    string OrderDate,
    string? ProcedureCode,
    string ProcedureName,
    string? OrderStatus,
    int ReportCount,
    int ResultCount,
    IReadOnlyList<PatientPortalLabReportItem> Reports);

public sealed record PatientPortalLabReportItem(
    string Id,
    string? DateCollected,
    string? ReportDate,
    string? SpecimenNumber,
    string? ReportStatus,
    string? ReviewStatus,
    int ResultCount,
    IReadOnlyList<PatientPortalLabResultItem> Results);

public sealed record PatientPortalLabResultItem(
    string Id,
    string? ResultCode,
    string ResultName,
    string? Abnormal,
    string? Value,
    string? Range,
    string? Units,
    string? ResultStatus);

public sealed record PatientPortalMedicalReportResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int SectionCount,
    int SelectedSectionCount,
    IReadOnlyList<PatientPortalMedicalReportSection> Sections,
    int IssueCount,
    IReadOnlyList<PatientPortalMedicalReportIssue> Issues,
    int EncounterCount,
    IReadOnlyList<PatientPortalMedicalReportEncounter> Encounters,
    int ProcedureOrderCount,
    IReadOnlyList<PatientPortalMedicalReportProcedureOrder> ProcedureOrders,
    PatientPortalGeneratedMedicalReport ReportPreview,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMedicalReportSection(
    string Id,
    string Label,
    string Group,
    bool Selected);

public sealed record PatientPortalMedicalReportIssue(
    string Id,
    string Type,
    string TypeLabel,
    string Title,
    string? BeginDate,
    string? EndDate,
    string Status,
    IReadOnlyList<int> EncounterIds);

public sealed record PatientPortalMedicalReportEncounter(
    int Encounter,
    string Date,
    string Display,
    string? Reason,
    int FormCount,
    IReadOnlyList<PatientPortalMedicalReportEncounterForm> Forms);

public sealed record PatientPortalMedicalReportEncounterForm(
    string Id,
    string FormDirectory,
    string Display,
    int Encounter);

public sealed record PatientPortalMedicalReportProcedureOrder(
    string Id,
    int Encounter,
    string OrderDate,
    string? EncounterDate,
    string? ProcedureCode,
    string ProcedureName,
    string? Diagnosis,
    string? OrderStatus,
    int ReportCount,
    int ResultCount,
    IReadOnlyList<string> ResultNames);

public sealed record PatientPortalGeneratedMedicalReport(
    string Title,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> IncludedProcedureOrderIds,
    IReadOnlyList<string> IncludedEncounterFormIds,
    PatientPortalGeneratedMedicalReportTemplateMetadata TemplateMetadata,
    PatientPortalGeneratedMedicalReportPackageMetadata PackageMetadata,
    int SummaryLineCount,
    IReadOnlyList<string> SummaryLines);

public sealed record PatientPortalMedicalReportGenerationRequest(
    IReadOnlyList<string>? SectionIds,
    IReadOnlyList<string>? ProcedureOrderIds,
    IReadOnlyList<string>? IssueIds,
    IReadOnlyList<string>? EncounterFormIds);

public sealed record PatientPortalGeneratedMedicalReportResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    string Title,
    string GeneratedOn,
    PatientPortalGeneratedMedicalReportTemplateMetadata TemplateMetadata,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> IncludedProcedureOrderIds,
    IReadOnlyList<string> IncludedIssueIds,
    IReadOnlyList<string> IncludedEncounterFormIds,
    bool PrintableVersionAvailable,
    bool PdfDownloadAvailable,
    bool PackageDownloadAvailable,
    PatientPortalGeneratedMedicalReportPackageMetadata PackageMetadata,
    int ReportSectionCount,
    IReadOnlyList<PatientPortalGeneratedMedicalReportSection> ReportSections,
    int SummaryLineCount,
    IReadOnlyList<string> SummaryLines,
    int AuditEventCount,
    IReadOnlyList<PatientPortalGeneratedMedicalReportAuditEvent> AuditEvents,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalGeneratedMedicalReportAuditResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int AuditEventCount,
    IReadOnlyList<PatientPortalGeneratedMedicalReportAuditEvent> AuditEvents,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalGeneratedMedicalReportAuditEvent(
    long Id,
    string EventType,
    string EventLabel,
    string EventAt,
    string ReportTitle,
    string GeneratedOn,
    string? ArtifactName,
    string? ArtifactContentType,
    IReadOnlyList<string> IncludedSectionIds,
    IReadOnlyList<string> IncludedIssueIds,
    IReadOnlyList<string> IncludedEncounterFormIds,
    IReadOnlyList<string> IncludedProcedureOrderIds,
    string Summary,
    string EventSource);

public sealed record PatientPortalGeneratedMedicalReportPdfPackage(
    bool Downloadable,
    string FileName,
    string ContentType,
    byte[] Content,
    int ContentLength,
    PatientPortalGeneratedMedicalReportResponse? Report,
    string? FailureReason);

public sealed record PatientPortalGeneratedMedicalReportPackageDownload(
    bool Downloadable,
    string FileName,
    string ContentType,
    byte[] Content,
    int ContentLength,
    PatientPortalGeneratedMedicalReportResponse? Report,
    string? FailureReason);

public sealed record PatientPortalGeneratedMedicalReportSection(
    string Id,
    string Title,
    int LineCount,
    IReadOnlyList<string> Lines);

public sealed record PatientPortalGeneratedMedicalReportTemplateMetadata(
    string FacilityName,
    string FacilityStreet,
    string FacilityCityStatePostal,
    string FacilityPhone,
    string PrintablePatientName,
    string PatientHeaderLine,
    string GeneratedOnLabel,
    bool SignatureLineAvailable);

public sealed record PatientPortalGeneratedMedicalReportPackageMetadata(
    string FileName,
    string ContentType,
    IReadOnlyList<string> EntryNames,
    bool ManifestAvailable,
    bool PdfAvailable,
    bool SummaryAvailable);

public sealed record PatientPortalAppointmentRequestOptionsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    IReadOnlyList<PatientPortalAppointmentCategoryOption> Categories,
    IReadOnlyList<PatientPortalAppointmentProviderOption> Providers,
    IReadOnlyList<PatientPortalAppointmentFacilityOption> Facilities,
    PatientPortalAppointmentRequestDefaults Defaults,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalAppointmentCategoryOption(
    int Id,
    string Name,
    string ConstantId,
    int DurationMinutes);

public sealed record PatientPortalAppointmentProviderOption(
    int Id,
    string Username,
    string DisplayName,
    int? FacilityId,
    string? FacilityName);

public sealed record PatientPortalAppointmentFacilityOption(
    int Id,
    string Name,
    string? Code);

public sealed record PatientPortalAppointmentRequestDefaults(
    int? CategoryId,
    int? ProviderId,
    int? FacilityId,
    int DurationMinutes,
    string Date,
    string StartTime);

public sealed record PatientPortalAppointmentRequest(
    int? ProviderId,
    int? FacilityId,
    int? CategoryId,
    string? Date,
    string? StartTime,
    int? DurationMinutes,
    string? Reason);

public sealed record PatientPortalAppointmentRequestResponse(
    bool Authenticated,
    bool Created,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    PatientPortalHomeAppointmentSummary? Appointment,
    PatientPortalAppointmentReminder? Reminder,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalAppointmentReminder(
    string Id,
    string Title,
    string Body,
    string AssignedTo,
    string Status);

public sealed record PatientPortalMessagesResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int MessageCount,
    IReadOnlyList<PatientPortalMessageItem> Messages,
    int SentMessageCount,
    IReadOnlyList<PatientPortalMessageItem> SentMessages,
    int AllMessageCount,
    IReadOnlyList<PatientPortalMessageItem> AllMessages,
    int DeletedMessageCount,
    IReadOnlyList<PatientPortalMessageItem> DeletedMessages,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageRecipientsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int RecipientCount,
    IReadOnlyList<PatientPortalMessageRecipientOption> Recipients,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageComposeOptionsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    string DefaultSubject,
    int SubjectCount,
    IReadOnlyList<PatientPortalMessageSubjectOption> SubjectOptions,
    int RecipientCount,
    IReadOnlyList<PatientPortalMessageRecipientOption> Recipients,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageSubjectOption(
    string Value,
    string Label,
    bool Default);

public sealed record PatientPortalMessageRecipientOption(
    string Id,
    string DisplayName,
    string Type,
    bool Active,
    bool Fallback);

public sealed record PatientPortalDocumentsResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentCategory> Categories,
    IReadOnlyList<PatientPortalDocumentItem> Documents,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalDocumentCategory(
    int CategoryId,
    string CategoryName,
    string DisplayPath,
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentItem> Documents);

public sealed record PatientPortalDocumentItem(
    int Id,
    string DocumentKey,
    int CategoryId,
    string CategoryName,
    string DisplayPath,
    string Name,
    string DocDate,
    string UploadedAt,
    string? Mimetype,
    string FileName,
    int? SizeBytes,
    string? StorageMethod,
    bool CanDownload);

public sealed record PatientPortalDocumentsDownloadRequest(
    IReadOnlyList<int>? DocumentIds);

public sealed record PatientPortalDocumentsDownloadPackage(
    bool Downloadable,
    string FileName,
    string ContentType,
    byte[] Content,
    int DocumentCount,
    IReadOnlyList<PatientPortalDocumentItem> Documents,
    string? FailureReason);

public sealed record PatientPortalMessageThreadResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    string MessageId,
    int ThreadId,
    PatientPortalMessageItem? AnchorMessage,
    int ThreadMessageCount,
    IReadOnlyList<PatientPortalMessageItem> ThreadMessages,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageItem(
    string Id,
    string Date,
    string Title,
    string Body,
    string Status,
    string AssignedTo,
    string SenderId,
    string SenderName,
    string RecipientId,
    string RecipientName,
    int MailChain,
    int ReplyMailChain,
    string? PortalRelation,
    bool IsEncrypted);

public sealed record PatientPortalComposeMessageRequest(
    string? RecipientId,
    string? Title,
    string? Body);

public sealed record PatientPortalComposeMessageResponse(
    bool Authenticated,
    bool Created,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string RecipientId,
    string RecipientName,
    PatientPortalMessageItem? SentMessage,
    PatientPortalMessageItem? RecipientMessage,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalReplyMessageRequest(
    string? Body);

public sealed record PatientPortalReplyMessageResponse(
    bool Authenticated,
    bool Created,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string OriginalMessageId,
    PatientPortalMessageItem? OriginalMessage,
    PatientPortalMessageItem? SentMessage,
    PatientPortalMessageItem? RecipientMessage,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalForwardMessageRequest(
    string? Body,
    string? AssignedTo);

public sealed record PatientPortalForwardMessageResponse(
    bool Authenticated,
    bool Forwarded,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string OriginalMessageId,
    PatientPortalMessageItem? OriginalMessage,
    PatientMessageItem? ForwardedPatientMessage,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalReadMessageResponse(
    bool Authenticated,
    bool MarkedRead,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string MessageId,
    PatientPortalMessageItem? Message,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalDeleteMessageResponse(
    bool Authenticated,
    bool Deleted,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string MessageId,
    PatientPortalMessageItem? DeletedMessage,
    int DeletedMessageCount,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalArchiveMessagesRequest(
    IReadOnlyList<int>? MessageIds);

public sealed record PatientPortalArchiveMessagesResponse(
    bool Authenticated,
    bool Archived,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    IReadOnlyList<string> MessageIds,
    IReadOnlyList<PatientPortalMessageItem> ArchivedMessages,
    int ArchivedMessageCount,
    int MessageCount,
    int SentMessageCount,
    string? FailureReason,
    string SessionSource);

public sealed record PatientPortalMessageAuditEvent(
    long Id,
    string EventType,
    string EventLabel,
    string EventAt,
    string MessageId,
    IReadOnlyList<string> RelatedMessageIds,
    string MessageTitle,
    string MessageStatus,
    string? RecipientId,
    string? RecipientName,
    int ThreadId,
    int ArchivedMessageCount,
    string Summary,
    string EventSource);

public sealed record PatientPortalMessageAuditResponse(
    bool Authenticated,
    Guid? SessionId,
    string Username,
    string PortalUsername,
    string CanonicalId,
    int? LegacyPid,
    string Pubpid,
    string DisplayName,
    string DatasetId,
    string DatasetVersion,
    string AsOfDate,
    int AuditEventCount,
    IReadOnlyList<PatientPortalMessageAuditEvent> AuditEvents,
    string? FailureReason,
    string SessionSource);
