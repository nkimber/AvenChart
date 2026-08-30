# Graph Report - .  (2026-08-30)

## Corpus Check
- Large corpus: 1133 files · ~1,117,091 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 10564 nodes · 23675 edges · 551 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output
- Edge kinds: calls: 6201 · contains: 5037 · method: 3939 · MODIFIES: 3083 · imports: 2025 · ON_BRANCH: 845 · references: 742 · reads_from: 584 · imports_from: 536 · PARENT_OF: 435 · triggers: 140 · inherits: 102 · re_exports: 6


## Input Scope
- Requested: committed
- Resolved: committed (source: cli)
- Included files: 1133 · Candidates: 1562
- Excluded: 9 untracked · 49902 ignored · 1 sensitive · 0 missing committed
- Recommendation: Use --scope all or graphify.yaml inputs.corpus for a knowledge-base folder.

## Graph Freshness
- Built from Git commit: `2488353`
- Compare this hash to `git rev-parse HEAD` before trusting freshness-sensitive graph output.
## God Nodes (most connected - your core abstractions)
1. `AdministrationRepository` - 233 edges
2. `PatientPortalRepository` - 215 edges
3. `clinicianGet()` - 162 edges
4. `clinicianPost()` - 135 edges
5. `TelehealthEndpoints` - 121 edges
6. `InventoryRepository` - 110 edges
7. `json()` - 107 edges
8. `BillingRepository` - 102 edges
9. `DocumentRepository` - 101 edges
10. `PatientRepository` - 95 edges

## Surprising Connections (you probably didn't know these)
- `access_user_memberships` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 476 → community 187_
- `appointments` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 187 → community 118_
- `inventory_lots` --references--> `facilities`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 440 → community 187_
- `lab_orders` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 321 → community 118_
- `lab_orders` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 321 → community 187_

## Communities

### Community 47 - "Community 47"
Cohesion: 0.06
Nodes (13): AccessibilityFinding, clinicianFixture, codingEncounter, encounter, composeRoot, fixtureSql(), cleanupLifecycleFixture(), AvenChartUiFixtures (+5 more)

### Community 24 - "Community 24"
Cohesion: 0.04
Nodes (37): ConvertTo-RequestJson(), Invoke-Api(), Invoke-Json(), New-Field(), New-TestSchema(), Move-Definition(), Move-Instance(), 022ba1c feat(forms): adopt legacy bronchitis sinus exam (+29 more)

### Community 6 - "Community 6"
Cohesion: 0.02
Nodes (24): getCurrentSession(), logout(), AuthorizationWorkQueueFilters, AuthorizationWorkQueueResponse, getAuthorizationWorkQueue(), getProcedureReportQueue(), ClinicianSession, updateClinicianSession() (+16 more)

### Community 12 - "Community 12"
Cohesion: 0.04
Nodes (75): ReportMetricDefinition, ReportParameterDefinition, ReportSourceDatasetDefinition, ReportOutputFieldDefinition, ReportValidationFixture, GovernedReportFamily, ReportDefinitionGovernancePolicy, GovernedReportDefinitionInput (+67 more)

### Community 8 - "Community 8"
Cohesion: 0.03
Nodes (26): ClinicalAlertSeverity, ClinicalAlertSeverityPresentation, getClinicalAlertSeverity(), LabResultFlagState, NormalizedLabResultFlag, normalValues, normalizeLabResultFlag(), ReportExecutionOptions (+18 more)

### Community 150 - "Community 150"
Cohesion: 0.18
Nodes (9): deleteStaffMessageFixture(), deleteProcedureOrderFixture(), deleteClinicalListFixture(), deletePatientDocumentFixtures(), deletePrescriptionFixture(), runProviderAssignmentSql(), deleteProviderAssignmentFixtures(), deletePatientAdministrationFixtures() (+1 more)

### Community 34 - "Community 34"
Cohesion: 0.06
Nodes (32): LoginResponse, LegacyClinicalFormDisplayEndpoints, practice_setting_facility_overrides, practice_settings, facilities, practice_setting_facility_override_revisions, practice_setting_facility_overrides, practice_setting_change_requests (+24 more)

### Community 7 - "Community 7"
Cohesion: 0.04
Nodes (83): practiceContext, prospectiveApplicant, safetyApprovedApplicant, prospectiveSafetyPassed, prospectiveVisitPurposeRecorded, prospectivePracticeNetworkOptions, prospectivePracticeNetworkRecorded, prospectiveMemberInsuranceDetailsRecorded (+75 more)

### Community 329 - "Community 329"
Cohesion: 0.25
Nodes (7): distRoot, assetsRoot, initialMatch, files, initial, violations, result

### Community 13 - "Community 13"
Cohesion: 0.02
Nodes (88): EntryChooser, ClinicianLogin, PortalLogin, OidcCallback, PortalShell, PortalDashboard, PortalMessages, PortalAppointments (+80 more)

### Community 18 - "Community 18"
Cohesion: 0.03
Nodes (77): getPatientPortalAppointments(), PatientDuplicateSearchResponse, findPatientDuplicateCandidates(), InventoryAccountingIntegrationDecisionDefinition, InventoryAccountingIntegrationChangeRequest, InventoryAccountingIntegrationChangeRequestDetailResponse, getInventoryAccountingIntegrationDecision(), createInventoryAccountingIntegrationChangeRequest() (+69 more)

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (304): AuthLoginInput, AuthLoginResponse, AuthAccessFacility, AuthAccessContextResponse, AuthAccessContextGrantResponse, AuthAccessContextGrantUpdateInput, AuthSessionResponse, PatientPortalLoginInput (+296 more)

### Community 39 - "Community 39"
Cohesion: 0.08
Nodes (33): login(), loginPatientPortal(), getPatientPortalSession(), endPatientPortalSession(), PatientPortalHomeSummaryResponse, getPatientPortalHome(), BrowserOidcAudience, BrowserOidcConfiguration (+25 more)

### Community 5 - "Community 5"
Cohesion: 0.02
Nodes (126): getStaffAccessContextGrant(), clinicianGet(), getInventoryControlledSubstanceCatalog(), getAddressBook(), getTrackAnything(), getPatientTrackHistory(), getPatientEducationResources(), getBatchCommunicationCampaigns() (+118 more)

### Community 14 - "Community 14"
Cohesion: 0.02
Nodes (83): updateStaffAccessContextGrant(), clinicianPut(), updatePatientGuardianContact(), updatePatientProviderAssignment(), updatePatientPortalAccountAccess(), investigateInventoryControlledDiscrepancy(), saveEncounterLayoutForm(), archiveEncounter() (+75 more)

### Community 51 - "Community 51"
Cohesion: 0.07
Nodes (23): PatientPortalHomeAppointmentSummary, PatientPortalAppointmentRequestOptionsResponse, PatientPortalAppointmentsResponse, getPatientPortalAppointmentRequestOptions(), requestPatientPortalAppointment(), downloadPatientPortalGeneratedMedicalReportPdf(), PatientPortalAppointmentRequestHistoryEvent, PatientPortalAppointmentRequestHistoryItem (+15 more)

### Community 83 - "Community 83"
Cohesion: 0.09
Nodes (16): PatientPortalMessageItem, PatientPortalMessagesResponse, getPatientPortalMessages(), PatientPortalMessageComposeOptions, getPatientPortalMessageComposeOptions(), composePatientPortalMessage(), downloadPatientPortalMessageAttachment(), PatientPortalMessageThreadResponse (+8 more)

### Community 30 - "Community 30"
Cohesion: 0.04
Nodes (43): PatientPortalDocumentItem, PatientPortalDocumentsResponse, getPatientPortalDocuments(), downloadPatientPortalDocuments(), PatientPortalLabOrderItem, PatientPortalLabResultsResponse, getPatientPortalLabResults(), PatientPortalClinicalSummaryResponse (+35 more)

### Community 57 - "Community 57"
Cohesion: 0.06
Nodes (30): clinicianHeaders(), deletePatientAuthorizationTestFixture(), deleteAppointment(), unlinkInventoryMedicationLink(), deleteAddressBookContact(), deleteTrackAnything(), deletePatientDocument(), ProcedureLabProviderDirectoryResponse (+22 more)

### Community 37 - "Community 37"
Cohesion: 0.05
Nodes (45): clinicianPost(), InventoryControlledSubstanceCatalogResponse, InventoryControlledCountSession, InventoryControlledCountSessionSummary, InventoryControlledAttestation, getInventoryControlledCountSessions(), getInventoryControlledCountSession(), createInventoryControlledCountSession() (+37 more)

### Community 22 - "Community 22"
Cohesion: 0.04
Nodes (50): PatientListItem, searchPatients(), AppointmentListItem, searchAppointments(), getAppointmentSchedulingOptions(), updateAppointmentStatus(), AppointmentUpdateInput, updateAppointment() (+42 more)

### Community 26 - "Community 26"
Cohesion: 0.03
Nodes (57): PatientMergePreview, PatientCareTeamMember, PatientChartSummary, getPatientChartSummary(), PatientLifecycleHistoryResponse, getPatientLifecycleHistory(), transitionPatientLifecycle(), PatientDeceasedStatusHistoryResponse (+49 more)

### Community 55 - "Community 55"
Cohesion: 0.06
Nodes (28): PatientReferral, PatientReferralWorkflowEvent, ReferralWorkQueueFilters, ReferralWorkQueueResponse, getReferralWorkQueue(), getPatientReferrals(), createPatientReferral(), updatePatientReferralStatus() (+20 more)

### Community 201 - "Community 201"
Cohesion: 0.15
Nodes (8): PatientSdohDomainValue, PatientSdohAssessment, PatientSdohAssessmentInput, getPatientSdohAssessments(), createPatientSdohAssessment(), updatePatientSdohAssessment(), DOMAINS, STATUS_OPTIONS

### Community 56 - "Community 56"
Cohesion: 0.06
Nodes (26): AppointmentSearchResponse, InventoryLot, InventoryPatientSale, createInventoryPatientSale(), InventoryPatientSaleAllocation, allocateInventoryPatientSale(), InventoryPrescriptionDispense, dispenseInventoryPrescription() (+18 more)

### Community 44 - "Community 44"
Cohesion: 0.07
Nodes (38): AppointmentSchedulingOptionsResponse, EncounterDetail, getEncounterDetail(), ProcedureOrderItem, EncounterCreateInput, CompleteEncounterCreateInput, EncounterBillingLine, EncounterBillingClaim (+30 more)

### Community 17 - "Community 17"
Cohesion: 0.02
Nodes (77): InventoryMedicationLink, InventoryMedicationCatalogItem, InventoryMedicationLinkHistoryResponse, getInventoryMedicationCatalog(), updateInventoryMedicationLink(), getInventoryMedicationLinkHistory(), InventoryReplenishmentPolicyDefinition, InventoryReplenishmentPolicyChangeRequest (+69 more)

### Community 188 - "Community 188"
Cohesion: 0.15
Nodes (11): InventoryCostPolicyDefinition, InventoryCostPolicyChangeRequest, InventoryCostPolicyChangeRequestDetailResponse, getInventoryCostPolicies(), createInventoryCostPolicyChangeRequest(), getInventoryCostPolicyChangeRequest(), transitionInventoryCostPolicyChangeRequest(), Props (+3 more)

### Community 11 - "Community 11"
Cohesion: 0.02
Nodes (93): EncounterVitals, EncounterSoapNote, EncounterSoapNoteTemplate, updateEncounter(), EncounterAuditHistory, getEncounterAuditHistory(), EncounterLayoutForm, getEncounterLayoutForms() (+85 more)

### Community 74 - "Community 74"
Cohesion: 0.08
Nodes (25): MedicationVocabularyItem, searchClinicalMedicationVocabulary(), ClinicalListAuditHistoryResponse, createProblem(), deactivateProblem(), getProblemAuditHistory(), createAllergy(), deactivateAllergy() (+17 more)

### Community 385 - "Community 385"
Cohesion: 0.33
Nodes (2): PatientMessagesResponse, getPatientMessages()

### Community 33 - "Community 33"
Cohesion: 0.04
Nodes (39): StaffMessageInboxResponse, PatientDocumentOcrQueueItem, PatientDocumentOcrQueueResponse, PatientDocumentOcrQueueFilters, PatientDocumentOcrHistoryResponse, PatientDocumentRoutingQueueItem, PatientDocumentRoutingQueueResponse, PatientDocumentRoutingQueueFilters (+31 more)

### Community 84 - "Community 84"
Cohesion: 0.09
Nodes (21): DocumentTemplateItem, DocumentTemplateListResponse, getDocumentTemplates(), createDocumentTemplate(), updateDocumentTemplate(), renderDocumentTemplate(), DocumentTemplateBinaryVersion, DocumentTemplateEvent (+13 more)

### Community 127 - "Community 127"
Cohesion: 0.15
Nodes (14): TherapyGroup, TherapyGroupMember, TherapyGroupSession, TherapyGroupSessionAttendance, getTherapyGroups(), createTherapyGroup(), getTherapyGroupMembers(), addTherapyGroupMember() (+6 more)

### Community 264 - "Community 264"
Cohesion: 0.22
Nodes (8): AuthorizationPolicyGap, AuthorizationPolicyRule, AuthorizationPolicyCatalogResponse, getAuthorizationPolicyCatalog(), AsyncState, gapOptions, formatGap(), AuthorizationPolicyRegistry()

### Community 85 - "Community 85"
Cohesion: 0.08
Nodes (24): PracticeSettingItem, PracticeSettingRegistryItem, getPracticeSettingRegistry(), PracticeSettingDelegation, getPracticeSettingDelegations(), grantPracticeSettingDelegation(), EffectivePracticeSettingItem, getEffectivePracticeSettings() (+16 more)

### Community 138 - "Community 138"
Cohesion: 0.13
Nodes (16): CodingCatalogItem, CodingCatalogChangeRequestStatus, CodingCatalogChangeRequestsResponse, CodingCatalogChangeRequestDetail, CodingCatalogChangeRequestAction, getCodingCatalogChangeRequests(), getCodingCatalogChangeRequest(), createCodingCatalogChangeRequest() (+8 more)

### Community 298 - "Community 298"
Cohesion: 0.22
Nodes (6): PatientPortalProfileDemographics, PatientPortalProfileResponse, getPatientPortalProfile(), PatientPortalProfileChangeInput, submitPatientPortalProfileChange(), emptyContactForm

### Community 49 - "Community 49"
Cohesion: 0.09
Nodes (35): AzureDeploymentProfileDocument, AzureDeploymentValidationIssue, AzureDeploymentProfileAssessment, AzureDeploymentProfileSummary, AzureDeploymentProfileDetail, AzureOperationsCapability, AzureAccessValidationResponse, AzureDeploymentExecutionSummary (+27 more)

### Community 23 - "Community 23"
Cohesion: 0.05
Nodes (73): schema, ClinicalFormOption, ClinicalFormOptionListReference, ClinicalFormOptionListCatalogItem, ClinicalFormOptionListCatalog, ClinicalFormCondition, ClinicalFormSectionLocalization, ClinicalFormRuleLocalization (+65 more)

### Community 107 - "Community 107"
Cohesion: 0.15
Nodes (20): ClinicalFormSection, ClinicalFormSchema, field(), schema(), ClinicalFormImpactSeverity, ClinicalFormImpactItem, ClinicalFormChangeImpact, severityRank (+12 more)

### Community 35 - "Community 35"
Cohesion: 0.10
Nodes (37): ClinicalFormField, ClinicalFormCalculation, ClinicalFormRule, ClinicalFormPolicy, ClinicalFormCalculationTemplate, Props, fields, CalculationAuthoringIssue (+29 more)

### Community 179 - "Community 179"
Cohesion: 0.25
Nodes (12): ClinicalFormFieldLocalization, ClinicalFormLocalization, ClinicalFormDefinitionSummary, field(), schema(), flattenFields(), flattenRules(), synchronizeLocalization() (+4 more)

### Community 145 - "Community 145"
Cohesion: 0.18
Nodes (14): response, ExperienceBaselineCounts, ExperienceRole, ExperienceEnvironment, ExperienceTask, ExperienceCriterion, ExperienceAnalyticsEvent, ExperienceGap (+6 more)

### Community 162 - "Community 162"
Cohesion: 0.17
Nodes (12): readiness, IdentityProviderReadinessCounts, IdentityAdapterContract, IdentityTypeReadiness, IdentityBoundaryControl, IdentityVerification, IdentityProviderGap, IdentityProviderReadiness (+4 more)

### Community 62 - "Community 62"
Cohesion: 0.10
Nodes (22): item, ManagedRecordPolicy, ManagedRecordItem, ManagedRecordList, ManagedRecordEvent, ManagedRecordHistory, ManagedRecordCreateInput, headers() (+14 more)

### Community 63 - "Community 63"
Cohesion: 0.13
Nodes (28): PatientDisclosureOption, PatientDisclosureScopeOption, PatientDisclosurePolicy, PatientDisclosureAuthority, PatientDisclosureAuthorityEvent, PatientDisclosureRequest, PatientDisclosureRequestEvent, PatientDisclosureAuthorityInput (+20 more)

### Community 25 - "Community 25"
Cohesion: 0.05
Nodes (43): ApiRequestError, requestHeaderNames, materializeRequestHeaders(), announceInvalidSession(), parseProblemDetails(), requireSuccessfulResponse(), apiFetch(), isRequestCancellation() (+35 more)

### Community 235 - "Community 235"
Cohesion: 0.22
Nodes (5): Props, State, createErrorReference(), AppErrorBoundary, Component

### Community 50 - "Community 50"
Cohesion: 0.08
Nodes (37): ReviewDraft, PromotionDraft, SyntheticPromotionDraft, PracticeReviewClaimDraft, PracticeReviewAuthorizationDraft, QueueAuthorizationDraft, Props, DraftFields (+29 more)

### Community 1 - "Community 1"
Cohesion: 0.02
Nodes (260): FormValues, initialValues, PendingCreate, PendingVerification, PendingSafetyTriage, PendingVisitPurpose, PendingPracticeNetworkPrecheck, PendingMemberInsuranceDetails (+252 more)

### Community 304 - "Community 304"
Cohesion: 0.33
Nodes (7): Props, TelehealthPharmacyChoicePanel(), formatAddress(), TelehealthPharmacyChoiceDraft, TelehealthPharmacyChoiceWorkspace, getTelehealthPharmacyChoices(), recordTelehealthPharmacyChoice()

### Community 274 - "Community 274"
Cohesion: 0.31
Nodes (7): Props, TelehealthSafetyDispositionPanel(), humanize(), TelehealthSafetyDispositionDraft, TelehealthSafetyDispositionWorkspace, getTelehealthSafetyDispositionDraft(), recordTelehealthSafetyDispositionDraft()

### Community 4 - "Community 4"
Cohesion: 0.01
Nodes (38): TelehealthApplicantSession, createApplicantAccessKey(), loadApplicantSession(), saveApplicantSession(), clearApplicantSession(), ITelehealthTriageEvaluator, SyntheticTelehealthTriageEvaluator, TelehealthApplicantClinicalInformationSummaryPolicy (+30 more)

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (8): lifecycleDomains, LifecycleDomain, FixtureId, FixtureCleanup, FixtureReset, LifecycleRecord, CreateOptions, LifecycleFixtureRegistry

### Community 66 - "Community 66"
Cohesion: 0.07
Nodes (22): Microsoft.NET.Sdk.Web, net10.0, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Microsoft.IdentityModel.Protocols.OpenIdConnect, Firely.Fhir.Validation.R4, Hl7.Fhir.R4, Hl7.Fhir.Specification.Data.R4 (+14 more)

### Community 568 - "Community 568"
Cohesion: 1.00
Nodes (1): AzureOperationsOptions

### Community 569 - "Community 569"
Cohesion: 1.00
Nodes (1): DatabaseConnectionOptions

### Community 41 - "Community 41"
Cohesion: 0.06
Nodes (24): IdentityProviderOptions, StaffAuthenticationEndpoints, IdentityProviderCatalog, OidcStaffIdentityAdapter, IStaffIdentityAdapter, TestOidcStaffIdentityAdapter, OidcIdentityAdapterHelpers, IPatientPortalIdentityAdapter (+16 more)

### Community 322 - "Community 322"
Cohesion: 0.29
Nodes (1): RuntimeSafetyPolicy

### Community 292 - "Community 292"
Cohesion: 0.36
Nodes (1): AddressBookRepository

### Community 128 - "Community 128"
Cohesion: 0.21
Nodes (1): AdministrationDirectoryRepository

### Community 31 - "Community 31"
Cohesion: 0.07
Nodes (1): AdministrationRepository

### Community 3 - "Community 3"
Cohesion: 0.04
Nodes (150): RecallEntity, RecallLifecycleEventEntity, 0220d35 fix(auth): scope scheduling and encounter workflows to facility, 0239c9f fix(labs): invalidate stale critical result queue, 05beda1 refactor(api): isolate encounter endpoints, 05fa4fb fix(patients): make administration updates atomic, 07e8fdd fix(sdoh): anchor generated goals to assessment date, 083e6b1 fix(scheduling): enforce appointment concurrency (+142 more)

### Community 20 - "Community 20"
Cohesion: 0.07
Nodes (1): AppointmentRepository

### Community 130 - "Community 130"
Cohesion: 0.21
Nodes (2): AuthRepository, ToResponse()

### Community 9 - "Community 9"
Cohesion: 0.04
Nodes (100): 0316b13 feat(procedures): protect locked encounter order entry, 03db92b fix(forms): validate legacy ROS compatibility sections, 04b5c83 feat(therapy): require recorded group attendance, 05ca2d7 feat(encounters): type locked track catalog, 0fb4655 feat(labs): govern specimen lifecycle, 10b1502 feat(ui): add referral work queue, 10f5fc4 feat(billing): protect locked charge mutations, 1764980 feat(encounters): show locked track state (+92 more)

### Community 87 - "Community 87"
Cohesion: 0.21
Nodes (1): AuthorizationRepository

### Community 217 - "Community 217"
Cohesion: 0.24
Nodes (1): AzureOperationsAccessRepository

### Community 38 - "Community 38"
Cohesion: 0.05
Nodes (32): AzureDeploymentProfileValidationException, Exception, DocumentVersionConflictException, DocumentReviewConflictException, DocumentArchiveConflictException, DocumentRoutingConflictException, DocumentOcrConflictException, FieldDefinition (+24 more)

### Community 93 - "Community 93"
Cohesion: 0.15
Nodes (1): AzureOperationsRepository

### Community 218 - "Community 218"
Cohesion: 0.33
Nodes (1): BatchCommunicationRepository

### Community 15 - "Community 15"
Cohesion: 0.06
Nodes (1): BillingRepository

### Community 349 - "Community 349"
Cohesion: 0.48
Nodes (1): ChartTrackerRepository

### Community 236 - "Community 236"
Cohesion: 0.44
Nodes (1): ClinicalAlertEvaluationRepository

### Community 29 - "Community 29"
Cohesion: 0.09
Nodes (1): ClinicalFormRepository

### Community 32 - "Community 32"
Cohesion: 0.09
Nodes (1): ClinicalFormRuntime

### Community 45 - "Community 45"
Cohesion: 0.06
Nodes (22): PrescriptionContinuationBlockedException, FlowBoardRepository, InvalidOperationException, AppointmentAvailabilityConflictException, PatientDisclosureConcurrencyException, PatientAdministrationVersionConflictException, ClinicalListAuditEventEntity, patient_lifecycle_events (+14 more)

### Community 46 - "Community 46"
Cohesion: 0.15
Nodes (1): ClinicalListRepository

### Community 94 - "Community 94"
Cohesion: 0.28
Nodes (1): ClinicalListStateRepository

### Community 16 - "Community 16"
Cohesion: 0.07
Nodes (1): DocumentRepository

### Community 109 - "Community 109"
Cohesion: 0.16
Nodes (1): DocumentTemplateRepository

### Community 237 - "Community 237"
Cohesion: 0.36
Nodes (1): EncounterLayoutFormRepository

### Community 27 - "Community 27"
Cohesion: 0.09
Nodes (2): EncounterRepository, DiagnosisAccumulator

### Community 164 - "Community 164"
Cohesion: 0.32
Nodes (1): EncounterStateRepository

### Community 238 - "Community 238"
Cohesion: 0.40
Nodes (1): ExternalIdentityMappingRepository

### Community 53 - "Community 53"
Cohesion: 0.15
Nodes (23): ExternalLaboratoryIntakeRepository, Matches(), ExternalLaboratoryFhirValidationException, ArgumentException, Parse(), ParseObservation(), ReadObservationValue(), ReadReferenceRange() (+15 more)

### Community 86 - "Community 86"
Cohesion: 0.10
Nodes (8): FhirResults, external_laboratory_sources, external_laboratory_source_events, trg_external_laboratory_source_events_immutable, 2a53ba9 feat(labs): scope external sources to facilities, 32d97a0 feat(labs): ingest profiled FHIR laboratory results, a9eec9f fix(fhir): make search contract pageable and typed, bc6cc4d feat(labs): govern external laboratory source credentials

### Community 139 - "Community 139"
Cohesion: 0.28
Nodes (1): ExternalLaboratorySourceRepository

### Community 95 - "Community 95"
Cohesion: 0.23
Nodes (1): FhirRepository

### Community 202 - "Community 202"
Cohesion: 0.24
Nodes (8): integration_outbox_events, integration_outbox, integration_inbox_events, integration_inbox, 2b91a4a feat(integrations): govern outbox recovery, 66cc16f feat(integrations): govern inbox reconciliation, 9fd53c1 feat(integrations): recover expired dispatch leases, d420fed feat(integrations): expose inbox decision history

### Community 88 - "Community 88"
Cohesion: 0.18
Nodes (1): IntegrationIdempotencyConflictException

### Community 140 - "Community 140"
Cohesion: 0.27
Nodes (1): InventoryAccountingIntegrationRepository

### Community 131 - "Community 131"
Cohesion: 0.25
Nodes (1): InventoryCostPolicyRepository

### Community 113 - "Community 113"
Cohesion: 0.22
Nodes (1): InventoryReplenishmentPolicyRepository

### Community 216 - "Community 216"
Cohesion: 0.26
Nodes (9): inventory_costing_exceptions, inventory_transactions, inventory_lots, 74ad7be feat(inventory): record costing exceptions, 9ea2933 feat(inventory): show cost layer applications, a006584 feat(inventory): cost patient sale movements, b593a6b feat(inventory): flag unallocated transfer costs, cdf5f2b feat(inventory): expose cost layer applications (+1 more)

### Community 59 - "Community 59"
Cohesion: 0.09
Nodes (2): InventoryRepository, InventoryItemBuilder

### Community 147 - "Community 147"
Cohesion: 0.14
Nodes (1): ToInventoryLot()

### Community 180 - "Community 180"
Cohesion: 0.27
Nodes (1): InventoryValuationRepository

### Community 219 - "Community 219"
Cohesion: 0.38
Nodes (1): LegacyClinicalFormDisplayRepository

### Community 64 - "Community 64"
Cohesion: 0.15
Nodes (1): ManagedRecordRepository

### Community 54 - "Community 54"
Cohesion: 0.13
Nodes (1): MessageRepository

### Community 323 - "Community 323"
Cohesion: 0.43
Nodes (1): OfficeNoteRepository

### Community 89 - "Community 89"
Cohesion: 0.22
Nodes (1): PatientDisclosureRepository

### Community 110 - "Community 110"
Cohesion: 0.22
Nodes (1): PatientMergeExecutionRepository

### Community 239 - "Community 239"
Cohesion: 0.40
Nodes (1): PatientPortalExternalIdentityMappingRepository

### Community 28 - "Community 28"
Cohesion: 0.05
Nodes (1): PatientPortalRepository

### Community 148 - "Community 148"
Cohesion: 0.14
Nodes (1): ToResponse()

### Community 240 - "Community 240"
Cohesion: 0.51
Nodes (1): PatientPrintRepository

### Community 386 - "Community 386"
Cohesion: 0.67
Nodes (1): PatientRecordRequestRepository

### Community 19 - "Community 19"
Cohesion: 0.06
Nodes (1): PatientRepository

### Community 191 - "Community 191"
Cohesion: 0.35
Nodes (1): PatientSdohRepository

### Community 241 - "Community 241"
Cohesion: 0.44
Nodes (1): PatientXmlExchangeRepository

### Community 186 - "Community 186"
Cohesion: 0.15
Nodes (5): PhiAuditResourceContext, PhiAuditedResult, IResult, PhiAuditResourceContextTests, f95ef06 feat(audit): correlate direct PHI access resources

### Community 77 - "Community 77"
Cohesion: 0.08
Nodes (12): LabOrderCatalogConfiguration, LabOrderReferenceConfiguration, LabProviderAddressBookConfiguration, LabProviderConfiguration, LabOrderCatalogEntity, LabOrderReferenceEntity, LabProviderAddressBookEntity, LabProviderEntity (+4 more)

### Community 133 - "Community 133"
Cohesion: 0.26
Nodes (1): ProcedureDirectoryRepository

### Community 36 - "Community 36"
Cohesion: 0.05
Nodes (37): CriticalLabResultFollowUpLifecycle, CriticalLabResultFollowUpLifecycleTests, lab_report_review_events, lab_reports, critical_lab_result_acknowledgements, lab_results, critical_lab_result_acknowledgement_events, __dirname (+29 more)

### Community 21 - "Community 21"
Cohesion: 0.07
Nodes (1): ProcedureRepository

### Community 325 - "Community 325"
Cohesion: 0.39
Nodes (1): RecallRepository

### Community 102 - "Community 102"
Cohesion: 0.24
Nodes (1): ReferralRepository

### Community 70 - "Community 70"
Cohesion: 0.15
Nodes (1): ReportDefinitionRepository

### Community 111 - "Community 111"
Cohesion: 0.20
Nodes (2): ReportExecutionQueueRepository, WorkerCancellationState

### Community 43 - "Community 43"
Cohesion: 0.11
Nodes (1): ReportExecutionRepository

### Community 67 - "Community 67"
Cohesion: 0.12
Nodes (1): ReportRepository

### Community 2 - "Community 2"
Cohesion: 0.01
Nodes (82): AvenChartDbContext, DbContext, AccessGroupConfiguration, IEntityTypeConfiguration, AccessGroupPermissionConfiguration, AccessPermissionConfiguration, AccessUserMembershipConfiguration, AddressBookContactConfiguration (+74 more)

### Community 149 - "Community 149"
Cohesion: 0.25
Nodes (1): TherapyGroupRepository

### Community 96 - "Community 96"
Cohesion: 0.14
Nodes (3): TrackAnythingRepository, PatientTrackAccumulator, PatientEncounterAccumulator

### Community 305 - "Community 305"
Cohesion: 0.44
Nodes (2): ISyntheticTelehealthComplaintTriageEvaluator, SyntheticTelehealthComplaintTriageEvaluator

### Community 373 - "Community 373"
Cohesion: 0.33
Nodes (3): ITelehealthCoverageGateway, SyntheticTelehealthCoverageGateway, SyntheticTelehealthAcknowledgment

### Community 374 - "Community 374"
Cohesion: 0.48
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkCatalog

### Community 335 - "Community 335"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantAllergyCatalog, TelehealthApplicantAllergyInformationPolicy

### Community 154 - "Community 154"
Cohesion: 0.24
Nodes (1): TelehealthApplicantAllergyInformationRepository

### Community 407 - "Community 407"
Cohesion: 0.60
Nodes (1): TelehealthApplicantAllergyInformationService

### Community 408 - "Community 408"
Cohesion: 0.47
Nodes (1): TelehealthApplicantClinicalInformationInventoryPolicy

### Community 169 - "Community 169"
Cohesion: 0.25
Nodes (1): TelehealthApplicantClinicalInformationInventoryRepository

### Community 409 - "Community 409"
Cohesion: 0.60
Nodes (1): TelehealthApplicantClinicalInformationInventoryService

### Community 155 - "Community 155"
Cohesion: 0.24
Nodes (1): TelehealthApplicantClinicalInformationSummaryRepository

### Community 410 - "Community 410"
Cohesion: 0.60
Nodes (1): TelehealthApplicantClinicalInformationSummaryService

### Community 156 - "Community 156"
Cohesion: 0.24
Nodes (1): TelehealthApplicantCommunicationAccessRepository

### Community 411 - "Community 411"
Cohesion: 0.60
Nodes (1): TelehealthApplicantCommunicationAccessService

### Community 558 - "Community 558"
Cohesion: 0.67
Nodes (1): TelehealthApplicantConnectionPolicy

### Community 170 - "Community 170"
Cohesion: 0.25
Nodes (1): TelehealthApplicantDevicePreparationRepository

### Community 412 - "Community 412"
Cohesion: 0.60
Nodes (1): TelehealthApplicantDevicePreparationService

### Community 336 - "Community 336"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantHealthHistoryTopicCatalog, TelehealthApplicantHealthHistoryInformationPolicy

### Community 157 - "Community 157"
Cohesion: 0.24
Nodes (1): TelehealthApplicantHealthHistoryInformationRepository

### Community 413 - "Community 413"
Cohesion: 0.60
Nodes (1): TelehealthApplicantHealthHistoryInformationService

### Community 308 - "Community 308"
Cohesion: 0.39
Nodes (1): TelehealthApplicantIdentityReviewRepository

### Community 309 - "Community 309"
Cohesion: 0.42
Nodes (1): TelehealthApplicantIdentityReviewService

### Community 461 - "Community 461"
Cohesion: 0.50
Nodes (1): TelehealthApplicantInsuranceHandoffPolicy

### Community 182 - "Community 182"
Cohesion: 0.27
Nodes (1): TelehealthApplicantInsuranceHandoffRepository

### Community 414 - "Community 414"
Cohesion: 0.60
Nodes (1): TelehealthApplicantInsuranceHandoffService

### Community 337 - "Community 337"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantMedicationCatalog, TelehealthApplicantMedicationInformationPolicy

### Community 158 - "Community 158"
Cohesion: 0.24
Nodes (1): TelehealthApplicantMedicationInformationRepository

### Community 415 - "Community 415"
Cohesion: 0.60
Nodes (1): TelehealthApplicantMedicationInformationService

### Community 310 - "Community 310"
Cohesion: 0.44
Nodes (1): TelehealthApplicantNoticeRepository

### Community 416 - "Community 416"
Cohesion: 0.60
Nodes (1): TelehealthApplicantNoticeService

### Community 417 - "Community 417"
Cohesion: 0.53
Nodes (1): TelehealthApplicantPracticeReviewAuthorizationRepository

### Community 338 - "Community 338"
Cohesion: 0.50
Nodes (1): TelehealthApplicantPracticeReviewClaimRepository

### Community 462 - "Community 462"
Cohesion: 0.60
Nodes (1): TelehealthApplicantPracticeReviewInboxService

### Community 377 - "Community 377"
Cohesion: 0.57
Nodes (1): TelehealthApplicantPracticeReviewSubmissionService

### Community 117 - "Community 117"
Cohesion: 0.20
Nodes (1): TelehealthApplicantPreRequestReadinessRepository

### Community 418 - "Community 418"
Cohesion: 0.60
Nodes (1): TelehealthApplicantPreRequestReadinessService

### Community 281 - "Community 281"
Cohesion: 0.36
Nodes (1): TelehealthApplicantPromotionAuthorizationRepository

### Community 311 - "Community 311"
Cohesion: 0.42
Nodes (1): TelehealthApplicantPromotionAuthorizationService

### Community 282 - "Community 282"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRegistrationDetailsRepository

### Community 419 - "Community 419"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRegistrationDetailsService

### Community 227 - "Community 227"
Cohesion: 0.20
Nodes (1): TelehealthApplicantRequestComplaintTriagePolicy

### Community 173 - "Community 173"
Cohesion: 0.34
Nodes (1): TelehealthApplicantRequestComplaintTriageRepository

### Community 463 - "Community 463"
Cohesion: 0.80
Nodes (1): TelehealthApplicantRequestComplaintTriageService

### Community 560 - "Community 560"
Cohesion: 0.67
Nodes (1): TelehealthApplicantRequestCreationPolicy

### Community 254 - "Community 254"
Cohesion: 0.44
Nodes (1): TelehealthApplicantRequestCreationRepository

### Community 421 - "Community 421"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestCreationService

### Community 498 - "Community 498"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestEligibilityPolicy

### Community 174 - "Community 174"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestEligibilityRepository

### Community 229 - "Community 229"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestEligibilityService

### Community 499 - "Community 499"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestInsuranceSourcePolicy

### Community 183 - "Community 183"
Cohesion: 0.36
Nodes (1): TelehealthApplicantRequestInsuranceSourceRepository

### Community 422 - "Community 422"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestInsuranceSourceService

### Community 464 - "Community 464"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestIntakePolicy

### Community 175 - "Community 175"
Cohesion: 0.34
Nodes (1): TelehealthApplicantRequestIntakeRepository

### Community 423 - "Community 423"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestIntakeService

### Community 500 - "Community 500"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestLocationPolicy

### Community 210 - "Community 210"
Cohesion: 0.42
Nodes (1): TelehealthApplicantRequestLocationRepository

### Community 424 - "Community 424"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestLocationService

### Community 501 - "Community 501"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionPolicy

### Community 184 - "Community 184"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionRepository

### Community 425 - "Community 425"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionService

### Community 426 - "Community 426"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestParticipationContextPolicy

### Community 159 - "Community 159"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestParticipationContextRepository

### Community 427 - "Community 427"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestParticipationContextService

### Community 465 - "Community 465"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRequestParticipationEvaluationPolicy

### Community 144 - "Community 144"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestParticipationEvaluationRepository

### Community 428 - "Community 428"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestParticipationEvaluationService

### Community 502 - "Community 502"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestPracticeNetworkPolicy

### Community 176 - "Community 176"
Cohesion: 0.32
Nodes (1): TelehealthApplicantRequestPracticeNetworkRepository

### Community 288 - "Community 288"
Cohesion: 0.36
Nodes (1): TelehealthApplicantRequestPracticeNetworkService

### Community 503 - "Community 503"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestQueueAuthorizationPolicy

### Community 211 - "Community 211"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRequestQueueAuthorizationRepository

### Community 429 - "Community 429"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestQueueAuthorizationService

### Community 561 - "Community 561"
Cohesion: 0.67
Nodes (1): TelehealthApplicantRequestQueueStatusPolicy

### Community 466 - "Community 466"
Cohesion: 0.70
Nodes (1): TelehealthApplicantRequestQueueStatusRepository

### Community 562 - "Community 562"
Cohesion: 1.00
Nodes (1): TelehealthApplicantRequestQueueStatusService

### Community 430 - "Community 430"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestRenderingCandidatePolicy

### Community 160 - "Community 160"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestRenderingCandidateRepository

### Community 431 - "Community 431"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestRenderingCandidateService

### Community 259 - "Community 259"
Cohesion: 0.18
Nodes (1): TelehealthApplicantRequestUniversalSafetyPolicy

### Community 177 - "Community 177"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestUniversalSafetyRepository

### Community 432 - "Community 432"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestUniversalSafetyService

### Community 433 - "Community 433"
Cohesion: 0.33
Nodes (1): TelehealthApplicantSyntheticPromotionPolicy

### Community 212 - "Community 212"
Cohesion: 0.28
Nodes (1): TelehealthApplicantSyntheticPromotionRepository

### Community 315 - "Community 315"
Cohesion: 0.42
Nodes (1): TelehealthApplicantSyntheticPromotionService

### Community 125 - "Community 125"
Cohesion: 0.19
Nodes (1): TelehealthConsultationRepository

### Community 178 - "Community 178"
Cohesion: 0.23
Nodes (1): TelehealthConsultationService

### Community 213 - "Community 213"
Cohesion: 0.28
Nodes (2): TelehealthSafetyDispositionConflictException, TelehealthDispositionRepository

### Community 185 - "Community 185"
Cohesion: 0.14
Nodes (3): TelehealthRequestStateMachine, TelehealthProblem, TelehealthCommandFingerprint

### Community 504 - "Community 504"
Cohesion: 0.50
Nodes (1): TelehealthAuthorizationPolicy

### Community 467 - "Community 467"
Cohesion: 0.70
Nodes (1): TelehealthEncounterFinalizationRepository

### Community 563 - "Community 563"
Cohesion: 0.67
Nodes (1): TelehealthEncounterFinalizationService

### Community 10 - "Community 10"
Cohesion: 0.06
Nodes (1): TelehealthEndpoints

### Community 230 - "Community 230"
Cohesion: 0.32
Nodes (1): TelehealthFinalClinicalReviewRepository

### Community 468 - "Community 468"
Cohesion: 0.60
Nodes (1): TelehealthFinalClinicalReviewService

### Community 341 - "Community 341"
Cohesion: 0.39
Nodes (1): TelehealthOpenApi

### Community 434 - "Community 434"
Cohesion: 0.40
Nodes (2): TelehealthRuntimeSafetyPolicy, TelehealthServiceRegistration

### Community 231 - "Community 231"
Cohesion: 0.24
Nodes (2): IPharmacyDirectory, SyntheticTelehealthPharmacyDirectory

### Community 232 - "Community 232"
Cohesion: 0.33
Nodes (1): TelehealthPharmacyRepository

### Community 316 - "Community 316"
Cohesion: 0.28
Nodes (4): ITelehealthPrescriptionSafetyGateway, SyntheticTelehealthPrescriptionSafetyGateway, IEPrescriptionGateway, SyntheticEPrescriptionGateway

### Community 137 - "Community 137"
Cohesion: 0.23
Nodes (1): TelehealthPrescriptionRepository

### Community 342 - "Community 342"
Cohesion: 0.46
Nodes (1): TelehealthPrescriptionService

### Community 470 - "Community 470"
Cohesion: 0.50
Nodes (2): IProfessionalClaimGateway, SyntheticProfessionalClaimGateway

### Community 564 - "Community 564"
Cohesion: 0.67
Nodes (1): TelehealthProfessionalClaimPreparationRepository

### Community 565 - "Community 565"
Cohesion: 0.67
Nodes (1): TelehealthProfessionalClaimPreparationService

### Community 260 - "Community 260"
Cohesion: 0.24
Nodes (1): TelehealthProspectiveApplicantPolicy

### Community 161 - "Community 161"
Cohesion: 0.27
Nodes (1): TelehealthProspectiveApplicantRepository

### Community 379 - "Community 379"
Cohesion: 0.62
Nodes (1): TelehealthProspectiveApplicantService

### Community 435 - "Community 435"
Cohesion: 0.47
Nodes (2): ITelehealthProspectiveEligibilityGateway, SyntheticTelehealthProspectiveEligibilityGateway

### Community 343 - "Community 343"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveEligibilityRepository

### Community 261 - "Community 261"
Cohesion: 0.31
Nodes (1): TelehealthProspectiveEligibilityService

### Community 436 - "Community 436"
Cohesion: 0.47
Nodes (2): ITelehealthProspectiveIdentityProofingGateway, SyntheticTelehealthProspectiveIdentityProofingGateway

### Community 290 - "Community 290"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveIdentityProofingRepository

### Community 380 - "Community 380"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveIdentityProofingService

### Community 344 - "Community 344"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsPolicy

### Community 471 - "Community 471"
Cohesion: 0.50
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsProtector

### Community 345 - "Community 345"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsRepository

### Community 472 - "Community 472"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsService

### Community 381 - "Community 381"
Cohesion: 0.48
Nodes (2): ITelehealthProspectivePracticeNetworkGateway, SyntheticTelehealthProspectivePracticeNetworkGateway

### Community 291 - "Community 291"
Cohesion: 0.40
Nodes (1): TelehealthProspectivePracticeNetworkPrecheckRepository

### Community 382 - "Community 382"
Cohesion: 0.48
Nodes (1): TelehealthProspectivePracticeNetworkPrecheckService

### Community 318 - "Community 318"
Cohesion: 0.39
Nodes (1): TelehealthProspectivePracticeNetworkRepository

### Community 319 - "Community 319"
Cohesion: 0.36
Nodes (1): TelehealthProspectivePracticeNetworkService

### Community 437 - "Community 437"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveSafetyTriagePolicy

### Community 383 - "Community 383"
Cohesion: 0.48
Nodes (1): TelehealthProspectiveSafetyTriageRepository

### Community 473 - "Community 473"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveSafetyTriageService

### Community 384 - "Community 384"
Cohesion: 0.48
Nodes (1): TelehealthProspectiveVisitPurposeRepository

### Community 474 - "Community 474"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveVisitPurposeService

### Community 351 - "Community 351"
Cohesion: 0.29
Nodes (4): TelehealthReadinessHealthCheck, IHealthCheck, PostgresReadinessHealthCheck, SchemaMigrationReadinessHealthCheck

### Community 42 - "Community 42"
Cohesion: 0.09
Nodes (1): TelehealthRepository

### Community 106 - "Community 106"
Cohesion: 0.22
Nodes (1): TelehealthService

### Community 439 - "Community 439"
Cohesion: 0.47
Nodes (2): ITelehealthVideoProvider, SyntheticTelehealthVideoProvider

### Community 214 - "Community 214"
Cohesion: 0.28
Nodes (1): TelehealthVideoRepository

### Community 233 - "Community 233"
Cohesion: 0.38
Nodes (1): TelehealthVideoService

### Community 505 - "Community 505"
Cohesion: 0.67
Nodes (1): AdministrationEndpoints

### Community 506 - "Community 506"
Cohesion: 0.67
Nodes (1): AdministrativeReferenceEndpoints

### Community 507 - "Community 507"
Cohesion: 0.67
Nodes (1): AppointmentEndpoints

### Community 192 - "Community 192"
Cohesion: 0.36
Nodes (1): AvenChartOpenApi

### Community 97 - "Community 97"
Cohesion: 0.16
Nodes (1): AzureDeploymentProfilePolicy

### Community 121 - "Community 121"
Cohesion: 0.19
Nodes (6): AzureOperationsAccessService, AzureOperationsAccessFilter, IEndpointFilter, AzureOperationsEnabledFilter, AzureOperationsAccessLockedException, UnauthorizedAccessException

### Community 387 - "Community 387"
Cohesion: 0.67
Nodes (1): AzureOperationsEndpoints

### Community 40 - "Community 40"
Cohesion: 0.08
Nodes (6): AzureCliRunner, AzureOperationsService, AzureDeploymentCoordinator, IHostedService, IDisposable, TemporaryParameterFile

### Community 508 - "Community 508"
Cohesion: 0.67
Nodes (1): BillingEndpoints

### Community 509 - "Community 509"
Cohesion: 0.67
Nodes (1): ClinicalFormEndpoints

### Community 510 - "Community 510"
Cohesion: 0.67
Nodes (1): ClinicalListEndpoints

### Community 511 - "Community 511"
Cohesion: 0.67
Nodes (1): ClinicalWorkflowEndpoints

### Community 512 - "Community 512"
Cohesion: 0.67
Nodes (1): ConfigurationEndpoints

### Community 75 - "Community 75"
Cohesion: 0.10
Nodes (12): DatabaseBootstrapCatalog, SchemaMigrationFaultInjectionException, SchemaMigrationCatalog, SchemaMigrationState, Valid(), Invalid(), vitals, 58f7374 Harden migrations and add review assessments (+4 more)

### Community 266 - "Community 266"
Cohesion: 0.38
Nodes (1): DatabaseSchemaMigrator

### Community 441 - "Community 441"
Cohesion: 0.60
Nodes (1): DevelopmentTestIdentityProviderEndpoints

### Community 513 - "Community 513"
Cohesion: 0.67
Nodes (1): DocumentEndpoints

### Community 514 - "Community 514"
Cohesion: 0.67
Nodes (1): DocumentTemplateEndpoints

### Community 515 - "Community 515"
Cohesion: 0.67
Nodes (1): EncounterEndpoints

### Community 220 - "Community 220"
Cohesion: 0.30
Nodes (1): EndpointAccessPolicies

### Community 479 - "Community 479"
Cohesion: 0.67
Nodes (1): ExternalLaboratoryFhirIntakeEndpoints

### Community 480 - "Community 480"
Cohesion: 0.83
Nodes (1): FhirR4Endpoints

### Community 442 - "Community 442"
Cohesion: 0.80
Nodes (1): FhirR4ValidationService

### Community 481 - "Community 481"
Cohesion: 0.67
Nodes (1): IntegrationEndpoints

### Community 516 - "Community 516"
Cohesion: 0.67
Nodes (1): InventoryEndpoints

### Community 517 - "Community 517"
Cohesion: 0.67
Nodes (1): ManagedRecordEndpoints

### Community 518 - "Community 518"
Cohesion: 0.67
Nodes (1): MessageEndpoints

### Community 519 - "Community 519"
Cohesion: 0.67
Nodes (1): OfficeNoteEndpoints

### Community 443 - "Community 443"
Cohesion: 0.60
Nodes (1): PatientEndpoints

### Community 520 - "Community 520"
Cohesion: 0.67
Nodes (1): PatientEngagementEndpoints

### Community 521 - "Community 521"
Cohesion: 0.67
Nodes (1): PatientPortalEndpoints

### Community 522 - "Community 522"
Cohesion: 0.67
Nodes (1): ProcedureEndpoints

### Community 523 - "Community 523"
Cohesion: 0.67
Nodes (1): ReportEndpoints

### Community 524 - "Community 524"
Cohesion: 0.67
Nodes (1): TherapyGroupEndpoints

### Community 348 - "Community 348"
Cohesion: 0.38
Nodes (4): medication_list_lifecycle_events, medications, f4268e0 feat(clinical): add medication lifecycle restore history, f858e05 feat(clinical): add medication content edit history

### Community 79 - "Community 79"
Cohesion: 0.07
Nodes (11): AllergyConfiguration, ImmunizationConfiguration, MedicationConfiguration, MedicationLifecycleEventConfiguration, ProblemConfiguration, AllergyEntity, ImmunizationEntity, MedicationEntity (+3 more)

### Community 459 - "Community 459"
Cohesion: 0.50
Nodes (1): AuthorizationPolicyCatalog

### Community 69 - "Community 69"
Cohesion: 0.15
Nodes (1): BrowserOidcSessionService

### Community 405 - "Community 405"
Cohesion: 0.53
Nodes (1): TestIdentityProviderService

### Community 78 - "Community 78"
Cohesion: 0.14
Nodes (3): StaffAccessContextService, Allowed(), Denied()

### Community 406 - "Community 406"
Cohesion: 0.47
Nodes (2): IStaffIdentityAdapter, LocalDevelopmentStaffIdentityAdapter

### Community 346 - "Community 346"
Cohesion: 0.46
Nodes (1): ClinicalWorkflowPolicyCatalog

### Community 585 - "Community 585"
Cohesion: 1.00
Nodes (1): AvenChart.Api.csproj

### Community 347 - "Community 347"
Cohesion: 0.57
Nodes (1): DatabaseBootstrapCatalogTests

### Community 263 - "Community 263"
Cohesion: 0.31
Nodes (1): FhirR4ValidationServiceTests

### Community 475 - "Community 475"
Cohesion: 0.50
Nodes (1): StaffAccessContextServiceTests

### Community 460 - "Community 460"
Cohesion: 0.60
Nodes (1): SyntheticProfessionalClaimGatewayTests

### Community 275 - "Community 275"
Cohesion: 0.38
Nodes (1): SyntheticTelehealthComplaintTriageEvaluatorTests

### Community 331 - "Community 331"
Cohesion: 0.39
Nodes (1): SyntheticTelehealthCoverageGatewayTests

### Community 332 - "Community 332"
Cohesion: 0.25
Nodes (1): SyntheticTelehealthPharmacyDirectoryTests

### Community 333 - "Community 333"
Cohesion: 0.43
Nodes (1): SyntheticTelehealthProspectiveEligibilityGatewayTests

### Community 306 - "Community 306"
Cohesion: 0.39
Nodes (1): SyntheticTelehealthProspectiveIdentityProofingGatewayTests

### Community 307 - "Community 307"
Cohesion: 0.22
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkCatalogTests

### Community 334 - "Community 334"
Cohesion: 0.43
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkGatewayTests

### Community 168 - "Community 168"
Cohesion: 0.13
Nodes (1): TelehealthApplicantAllergyInformationPolicyTests

### Community 276 - "Community 276"
Cohesion: 0.20
Nodes (1): TelehealthApplicantClinicalInformationInventoryPolicyTests

### Community 277 - "Community 277"
Cohesion: 0.22
Nodes (1): TelehealthApplicantClinicalInformationSummaryPolicyTests

### Community 278 - "Community 278"
Cohesion: 0.33
Nodes (1): TelehealthApplicantCommunicationAccessPolicyTests

### Community 559 - "Community 559"
Cohesion: 0.67
Nodes (1): TelehealthApplicantConnectionPolicyTests

### Community 251 - "Community 251"
Cohesion: 0.31
Nodes (1): TelehealthApplicantDevicePreparationPolicyTests

### Community 171 - "Community 171"
Cohesion: 0.13
Nodes (1): TelehealthApplicantHealthHistoryInformationPolicyTests

### Community 375 - "Community 375"
Cohesion: 0.29
Nodes (1): TelehealthApplicantIdentityReviewPolicyTests

### Community 279 - "Community 279"
Cohesion: 0.31
Nodes (1): TelehealthApplicantInsuranceHandoffPolicyTests

### Community 172 - "Community 172"
Cohesion: 0.13
Nodes (1): TelehealthApplicantMedicationInformationPolicyTests

### Community 280 - "Community 280"
Cohesion: 0.29
Nodes (1): TelehealthApplicantNoticePolicyTests

### Community 376 - "Community 376"
Cohesion: 0.29
Nodes (1): TelehealthApplicantPracticeReviewInboxPolicyTests

### Community 339 - "Community 339"
Cohesion: 0.39
Nodes (1): TelehealthApplicantPracticeReviewPacketPolicyTests

### Community 252 - "Community 252"
Cohesion: 0.22
Nodes (1): TelehealthApplicantPracticeReviewSubmissionPolicyTests

### Community 253 - "Community 253"
Cohesion: 0.20
Nodes (1): TelehealthApplicantPreRequestReadinessPolicyTests

### Community 378 - "Community 378"
Cohesion: 0.29
Nodes (1): TelehealthApplicantPromotionAuthorizationPolicyTests

### Community 312 - "Community 312"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRegistrationDetailsPolicyTests

### Community 228 - "Community 228"
Cohesion: 0.27
Nodes (1): TelehealthApplicantRequestComplaintTriagePolicyTests

### Community 420 - "Community 420"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestCreationPolicyTests

### Community 283 - "Community 283"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestEligibilityPolicyTests

### Community 284 - "Community 284"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestInsuranceSourcePolicyTests

### Community 285 - "Community 285"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestIntakePolicyTests

### Community 286 - "Community 286"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestLocationPolicyTests

### Community 313 - "Community 313"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionPolicyTests

### Community 255 - "Community 255"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestParticipationContextPolicyTests

### Community 256 - "Community 256"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestParticipationEvaluationPolicyTests

### Community 287 - "Community 287"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestPracticeNetworkPolicyTests

### Community 257 - "Community 257"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestQueueAuthorizationPolicyTests

### Community 289 - "Community 289"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestQueueStatusPolicyTests

### Community 258 - "Community 258"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestRenderingCandidatePolicyTests

### Community 314 - "Community 314"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestUniversalSafetyPolicyTests

### Community 340 - "Community 340"
Cohesion: 0.25
Nodes (1): TelehealthApplicantSyntheticPromotionPolicyTests

### Community 73 - "Community 73"
Cohesion: 0.23
Nodes (1): TelehealthConsultationServiceTests

### Community 469 - "Community 469"
Cohesion: 0.70
Nodes (1): TelehealthPatientQueueStatusProjectorTests

### Community 198 - "Community 198"
Cohesion: 0.38
Nodes (1): TelehealthPrescriptionServiceTests

### Community 317 - "Community 317"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveApplicantPolicyTests

### Community 105 - "Community 105"
Cohesion: 0.23
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsPolicyTests

### Community 320 - "Community 320"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveSafetyTriagePolicyTests

### Community 438 - "Community 438"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveVisitPurposePolicyTests

### Community 199 - "Community 199"
Cohesion: 0.32
Nodes (1): TelehealthRuntimeSafetyPolicyTests

### Community 262 - "Community 262"
Cohesion: 0.33
Nodes (1): TelehealthSafetyDispositionRulesTests

### Community 566 - "Community 566"
Cohesion: 0.67
Nodes (1): TelehealthStateMachineTests

### Community 58 - "Community 58"
Cohesion: 0.08
Nodes (35): dataset_metadata, practice_settings, coding_catalogs, coding_catalog_audit_events, form_layouts, form_option_lists, form_option_values, clinical_alert_rules (+27 more)

### Community 187 - "Community 187"
Cohesion: 0.22
Nodes (14): facilities, staff, auth_accounts, auth_sessions, patient_related_contacts, patient_care_teams, patient_care_team_members, appointments (+6 more)

### Community 476 - "Community 476"
Cohesion: 0.67
Nodes (4): access_groups, access_permissions, access_group_permissions, access_user_memberships

### Community 118 - "Community 118"
Cohesion: 0.13
Nodes (20): patients, patient_record_requests, patient_sdoh_assessments, patient_portal_accounts, patient_portal_sessions, patient_portal_profile_change_requests, patient_portal_report_audit_events, patient_portal_message_audit_events (+12 more)

### Community 477 - "Community 477"
Cohesion: 0.50
Nodes (4): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_requests, patient_disclosure_request_events

### Community 567 - "Community 567"
Cohesion: 1.00
Nodes (2): pharmacies, prescriptions

### Community 440 - "Community 440"
Cohesion: 0.40
Nodes (5): inventory_items, inventory_lots, inventory_vendors, inventory_purchase_receipts, inventory_transactions

### Community 321 - "Community 321"
Cohesion: 0.25
Nodes (8): lab_orders, lab_reports, lab_report_review_events, lab_specimens, lab_results, critical_lab_result_acknowledgements, critical_lab_result_acknowledgement_events, procedure_result_versions

### Community 570 - "Community 570"
Cohesion: 1.00
Nodes (1): schema_migrations

### Community 571 - "Community 571"
Cohesion: 1.00
Nodes (1): statement_email_outbox

### Community 525 - "Community 525"
Cohesion: 0.67
Nodes (2): integration_outbox, integration_inbox

### Community 572 - "Community 572"
Cohesion: 1.00
Nodes (1): phi_access_audit_events

### Community 444 - "Community 444"
Cohesion: 0.70
Nodes (4): inventory_items, inventory_lots, facilities, inventory_transactions

### Community 573 - "Community 573"
Cohesion: 1.00
Nodes (1): encounter_audit_events

### Community 526 - "Community 526"
Cohesion: 1.00
Nodes (2): coding_catalog_audit_events, coding_catalogs

### Community 482 - "Community 482"
Cohesion: 0.83
Nodes (3): form_layouts, form_layout_groups, form_layout_fields

### Community 574 - "Community 574"
Cohesion: 1.00
Nodes (1): clinical_alert_rules

### Community 575 - "Community 575"
Cohesion: 1.00
Nodes (1): module_catalog

### Community 576 - "Community 576"
Cohesion: 1.00
Nodes (1): api_client_registry

### Community 527 - "Community 527"
Cohesion: 1.00
Nodes (2): form_option_lists, form_option_values

### Community 483 - "Community 483"
Cohesion: 0.83
Nodes (3): encounter_layout_form_records, form_layouts, encounter_layout_form_values

### Community 528 - "Community 528"
Cohesion: 1.00
Nodes (2): encounter_clinical_alert_acknowledgments, clinical_alert_rules

### Community 445 - "Community 445"
Cohesion: 0.70
Nodes (4): patient_merge_audit_plans, patient_merge_executions, patients, patient_merge_execution_manifest_rows

### Community 529 - "Community 529"
Cohesion: 1.00
Nodes (2): patient_record_requests, patients

### Community 530 - "Community 530"
Cohesion: 1.00
Nodes (2): patient_sdoh_assessments, patients

### Community 577 - "Community 577"
Cohesion: 1.00
Nodes (1): office_notes

### Community 578 - "Community 578"
Cohesion: 1.00
Nodes (1): address_book_contacts

### Community 579 - "Community 579"
Cohesion: 2.00
Nodes (1): track_anything_types

### Community 580 - "Community 580"
Cohesion: 1.00
Nodes (1): patient_education_resources

### Community 446 - "Community 446"
Cohesion: 0.70
Nodes (4): recalls, patients, staff, facilities

### Community 531 - "Community 531"
Cohesion: 1.00
Nodes (2): recall_activity, recalls

### Community 484 - "Community 484"
Cohesion: 0.83
Nodes (3): batch_communication_campaigns, batch_communication_recipients, patients

### Community 447 - "Community 447"
Cohesion: 0.70
Nodes (4): chart_tracker_locations, chart_tracker_events, patients, staff

### Community 581 - "Community 581"
Cohesion: 1.00
Nodes (1): document_templates

### Community 532 - "Community 532"
Cohesion: 1.00
Nodes (2): patient_duplicate_review_dispositions, patients

### Community 533 - "Community 533"
Cohesion: 1.00
Nodes (2): document_template_binary_versions, document_templates

### Community 534 - "Community 534"
Cohesion: 1.00
Nodes (2): patient_xml_exchange_audits, patients

### Community 485 - "Community 485"
Cohesion: 0.83
Nodes (3): inventory_vendors, inventory_purchase_receipts, facilities

### Community 535 - "Community 535"
Cohesion: 1.00
Nodes (2): inventory_count_reconciliations, inventory_lots

### Community 388 - "Community 388"
Cohesion: 0.67
Nodes (5): encounter_track_records, encounters, track_anything_types, encounter_track_readings, encounter_track_reading_values

### Community 536 - "Community 536"
Cohesion: 1.33
Nodes (2): practice_setting_revisions, practice_settings

### Community 537 - "Community 537"
Cohesion: 1.33
Nodes (2): coding_catalog_revisions, coding_catalogs

### Community 538 - "Community 538"
Cohesion: 1.33
Nodes (2): form_option_list_revisions, form_option_lists

### Community 539 - "Community 539"
Cohesion: 1.33
Nodes (2): form_layout_revisions, form_layouts

### Community 540 - "Community 540"
Cohesion: 1.33
Nodes (2): clinical_alert_rule_revisions, clinical_alert_rules

### Community 541 - "Community 541"
Cohesion: 1.33
Nodes (2): module_catalog_revisions, module_catalog

### Community 542 - "Community 542"
Cohesion: 1.33
Nodes (2): api_client_registry_revisions, api_client_registry

### Community 543 - "Community 543"
Cohesion: 1.00
Nodes (2): inventory_lot_metadata_audits, inventory_lots

### Community 544 - "Community 544"
Cohesion: 1.00
Nodes (2): inventory_lot_destructions, inventory_lots

### Community 389 - "Community 389"
Cohesion: 0.60
Nodes (5): inventory_patient_sales, inventory_lots, patients, encounters, inventory_transactions

### Community 448 - "Community 448"
Cohesion: 0.70
Nodes (4): inventory_patient_sale_batches, inventory_items, patients, encounters

### Community 449 - "Community 449"
Cohesion: 0.80
Nodes (4): inventory_item_medication_links, inventory_items, medication_vocabulary, inventory_item_medication_link_audits

### Community 352 - "Community 352"
Cohesion: 0.52
Nodes (6): inventory_purchase_requisitions, facilities, inventory_vendors, inventory_purchase_requisition_lines, inventory_items, inventory_purchase_requisition_events

### Community 450 - "Community 450"
Cohesion: 0.70
Nodes (4): inventory_purchase_requisition_receipts, inventory_purchase_requisitions, inventory_purchase_requisition_lines, inventory_purchase_receipts

### Community 451 - "Community 451"
Cohesion: 0.70
Nodes (4): inventory_lot_expiry_dispositions, inventory_lots, inventory_transactions, inventory_lot_destructions

### Community 486 - "Community 486"
Cohesion: 0.83
Nodes (3): practice_setting_change_requests, practice_settings, practice_setting_change_request_events

### Community 390 - "Community 390"
Cohesion: 0.53
Nodes (5): inventory_controlled_locations, facilities, inventory_controlled_location_events, inventory_controlled_item_classification_events, inventory_items

### Community 391 - "Community 391"
Cohesion: 0.67
Nodes (5): inventory_controlled_custody_events, inventory_lots, inventory_controlled_locations, patients, encounters

### Community 353 - "Community 353"
Cohesion: 0.57
Nodes (6): inventory_controlled_count_sessions, inventory_controlled_locations, inventory_controlled_count_lines, inventory_lots, inventory_controlled_count_discrepancies, inventory_controlled_custody_events

### Community 545 - "Community 545"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_runs, inventory_controlled_locations

### Community 546 - "Community 546"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_exports, inventory_controlled_report_runs

### Community 487 - "Community 487"
Cohesion: 0.83
Nodes (3): document_template_events, document_templates, document_template_binary_versions

### Community 452 - "Community 452"
Cohesion: 0.70
Nodes (4): referrals, patients, authorizations, clinical_workflow_events

### Community 547 - "Community 547"
Cohesion: 1.00
Nodes (2): coding_catalog_change_requests, coding_catalog_change_request_events

### Community 548 - "Community 548"
Cohesion: 1.00
Nodes (2): form_layout_change_requests, form_layout_change_request_events

### Community 549 - "Community 549"
Cohesion: 1.00
Nodes (2): form_option_list_change_requests, form_option_list_change_request_events

### Community 550 - "Community 550"
Cohesion: 1.00
Nodes (2): clinical_alert_rule_change_requests, clinical_alert_rule_change_request_events

### Community 551 - "Community 551"
Cohesion: 1.00
Nodes (2): module_change_requests, module_change_request_events

### Community 552 - "Community 552"
Cohesion: 1.00
Nodes (2): api_client_change_requests, api_client_change_request_events

### Community 488 - "Community 488"
Cohesion: 0.83
Nodes (3): inventory_cost_policies, inventory_cost_policy_change_requests, inventory_cost_policy_change_request_events

### Community 392 - "Community 392"
Cohesion: 0.67
Nodes (5): patient_disclosure_authorities, patients, patient_disclosure_authority_events, patient_disclosure_requests, patient_disclosure_request_events

### Community 295 - "Community 295"
Cohesion: 0.47
Nodes (8): inventory_cost_layers, inventory_transactions, inventory_purchase_receipts, inventory_lots, inventory_items, facilities, inventory_cost_policies, inventory_cost_layer_applications

### Community 181 - "Community 181"
Cohesion: 0.26
Nodes (14): patient_document_versions, patient_documents, patient_document_content_events, patient_document_review_events, patient_document_archive_events, patient_document_metadata_events, patient_document_ocr_tasks, patient_document_ocr_events (+6 more)

### Community 326 - "Community 326"
Cohesion: 0.50
Nodes (7): inventory_valuation_runs, facilities, inventory_cost_policies, inventory_valuation_run_lines, inventory_cost_layers, inventory_lots, inventory_items

### Community 354 - "Community 354"
Cohesion: 0.62
Nodes (6): inventory_replenishment_policies, inventory_items, facilities, inventory_vendors, inventory_replenishment_policy_change_requests, inventory_replenishment_policy_change_request_events

### Community 453 - "Community 453"
Cohesion: 0.90
Nodes (4): saved_report_definitions, saved_report_runs, saved_report_definition_revisions, saved_report_definition_events

### Community 489 - "Community 489"
Cohesion: 0.83
Nodes (3): inventory_accounting_integration_decisions, inventory_accounting_integration_change_requests, inventory_accounting_integration_change_request_events

### Community 327 - "Community 327"
Cohesion: 0.57
Nodes (7): clinical_form_definitions, clinical_form_revisions, clinical_form_definition_events, clinical_form_instances, patients, clinical_form_signatures, clinical_form_instance_events

### Community 234 - "Community 234"
Cohesion: 0.18
Nodes (4): 18dd71c feat(forms): adopt legacy speech dictation, 3fbf1fd feat(forms): adopt legacy phq9 screening, 7ad81c7 feat(forms): adopt legacy gad7 screening, aebec7a feat(forms): adopt legacy transfer summary

### Community 265 - "Community 265"
Cohesion: 0.20
Nodes (3): 10b94e5 feat(forms): adopt legacy ankle assessment, 9d6a4b8 feat(forms): adopt legacy treatment plan, da2d960 feat(forms): adopt legacy physical exam lines

### Community 393 - "Community 393"
Cohesion: 0.60
Nodes (5): practice_setting_delegations, auth_accounts, practice_settings, facilities, practice_setting_delegation_events

### Community 490 - "Community 490"
Cohesion: 0.83
Nodes (3): message_assignment_events, messages, patients

### Community 491 - "Community 491"
Cohesion: 0.83
Nodes (3): staff_message_attachments, messages, patients

### Community 492 - "Community 492"
Cohesion: 0.83
Nodes (3): message_correction_events, messages, patients

### Community 493 - "Community 493"
Cohesion: 0.83
Nodes (3): message_retention_events, messages, patients

### Community 151 - "Community 151"
Cohesion: 0.24
Nodes (16): patient_portal_appointment_requests, appointments, patient_portal_appointment_request_events, trg_capture_patient_portal_appointment_request, capture_patient_portal_appointment_request(), new.appointment_date, new.start_time, new.duration_minutes (+8 more)

### Community 296 - "Community 296"
Cohesion: 0.50
Nodes (8): therapy_groups, staff, therapy_group_members, patients, therapy_group_sessions, therapy_group_session_participants, therapy_group_session_encounters, therapy_group_session_attendance

### Community 553 - "Community 553"
Cohesion: 1.00
Nodes (2): procedure_specimen_events, lab_specimens

### Community 394 - "Community 394"
Cohesion: 0.33
Nodes (5): operations.operator_credentials, operations.sessions, operations.audit_events, operations.usage_events, operations.runtime_state

### Community 454 - "Community 454"
Cohesion: 0.70
Nodes (4): azure_deployment_profiles, azure_deployment_profile_revisions, azure_deployment_executions, azure_deployment_execution_events

### Community 395 - "Community 395"
Cohesion: 0.47
Nodes (5): azure_operations_access_config, azure_operations_access_grants, auth_sessions, azure_operations_unlock_attempts, azure_operations_access_audit

### Community 65 - "Community 65"
Cohesion: 0.10
Nodes (33): appointment_reminder_dispatch_audit, statement_delivery_audit_events, medication_vocabulary, prescription_audit_events, prescription_refill_request_lifecycle, patient_provider_assignment_events, patient_administration_audit_events, patient_portal_message_attachments (+25 more)

### Community 554 - "Community 554"
Cohesion: 1.00
Nodes (2): recall_lifecycle_events, recalls

### Community 555 - "Community 555"
Cohesion: 1.00
Nodes (2): lab_reports, lab_specimens

### Community 556 - "Community 556"
Cohesion: 1.00
Nodes (2): patient_registration_duplicate_reviews, patients

### Community 582 - "Community 582"
Cohesion: 1.00
Nodes (1): inventory_controlled_action_attestations

### Community 267 - "Community 267"
Cohesion: 0.33
Nodes (9): patient_allergy_review_states, trg_patients_initialize_allergy_review_state, patients, trg_allergies_advance_review_state, allergies, avenchart_initialize_allergy_review_state(), avenchart_advance_allergy_review_state(), can (+1 more)

### Community 396 - "Community 396"
Cohesion: 0.67
Nodes (5): auth_principal_facility_grants, auth_accounts, facilities, auth_principal_purpose_of_use_grants, auth_access_context_grant_events

### Community 193 - "Community 193"
Cohesion: 0.22
Nodes (13): trg_allergies_require_active_patient_for_new_content, allergies, trg_problems_require_active_patient_for_new_content, problems, trg_medications_require_active_patient_for_new_content, medications, trg_immunizations_require_active_patient_for_new_content, immunizations (+5 more)

### Community 397 - "Community 397"
Cohesion: 0.47
Nodes (4): trg_prescription_audit_events_immutable, prescription_audit_events, trg_prescriptions_retained, prescriptions

### Community 398 - "Community 398"
Cohesion: 0.53
Nodes (5): trg_prescriptions_require_active_patient_for_continuation, prescriptions, avenchart_require_active_patient_for_prescription_continuation(), patient_record, patients

### Community 203 - "Community 203"
Cohesion: 0.29
Nodes (11): external_laboratory_ingestions, external_laboratory_sources, patients, lab_orders, lab_specimens, lab_reports, external_laboratory_ingestion_events, external_laboratory_report_links (+3 more)

### Community 355 - "Community 355"
Cohesion: 0.48
Nodes (5): external_laboratory_source_facility_grants, external_laboratory_sources, facilities, external_laboratory_source_facility_events, trg_external_laboratory_source_facility_events_immutable

### Community 399 - "Community 399"
Cohesion: 0.53
Nodes (4): auth_external_identity_mappings, auth_accounts, auth_external_identity_mapping_events, trg_auth_external_identity_mapping_events_immutable

### Community 400 - "Community 400"
Cohesion: 0.53
Nodes (4): patient_portal_external_identity_mappings, patients, patient_portal_external_identity_mapping_events, trg_patient_portal_external_identity_mapping_events_immutable

### Community 204 - "Community 204"
Cohesion: 0.28
Nodes (12): trg_lab_report_review_event_content, lab_report_review_events, avenchart_capture_lab_report_review_content(), the, an, old.content_revision, old.content_checksum, old.content_manifest (+4 more)

### Community 242 - "Community 242"
Cohesion: 0.35
Nodes (10): avenchart_reject_locked_encounter_mutation(), target_encounter, lab_orders, lab_reports, encounter_track_records, until, encounter_signatures, is_locked (+2 more)

### Community 401 - "Community 401"
Cohesion: 0.53
Nodes (4): critical_lab_result_follow_ups, lab_results, critical_lab_result_follow_up_events, trg_critical_follow_up_events_append_only

### Community 455 - "Community 455"
Cohesion: 0.60
Nodes (4): procedure_order_events, lab_orders, procedure_result_events, lab_results

### Community 494 - "Community 494"
Cohesion: 0.83
Nodes (3): integration_idempotency_conflicts, integration_outbox, integration_inbox

### Community 456 - "Community 456"
Cohesion: 0.60
Nodes (3): integration_outbox_provenance_events, integration_outbox, trg_integration_outbox_provenance_events_immutable

### Community 152 - "Community 152"
Cohesion: 0.25
Nodes (15): telehealth_requests, facilities, patients, telehealth_protocol_versions, telehealth_patient_locations, telehealth_triage_assessments, telehealth_request_events, telehealth_queue_entries (+7 more)

### Community 205 - "Community 205"
Cohesion: 0.32
Nodes (12): telehealth_patient_confirmations, telehealth_requests, telehealth_intake_snapshots, telehealth_demonstration_acknowledgments, telehealth_coverage_selections, insurance_records, telehealth_coverage_verifications, trg_telehealth_patient_confirmations_append_only (+4 more)

### Community 243 - "Community 243"
Cohesion: 0.33
Nodes (9): telehealth_prospective_applicants, facilities, telehealth_applicant_contact_challenges, telehealth_applicant_verification_attempts, telehealth_applicant_events, trg_telehealth_applicant_challenges_append_only, trg_telehealth_applicant_attempts_append_only, trg_telehealth_applicant_events_append_only (+1 more)

### Community 206 - "Community 206"
Cohesion: 0.31
Nodes (11): telehealth_video_sessions, telehealth_requests, facilities, telehealth_reservations, telehealth_video_preflights, telehealth_video_participant_grants, telehealth_video_events, trg_telehealth_video_preflights_append_only (+3 more)

### Community 207 - "Community 207"
Cohesion: 0.33
Nodes (12): telehealth_consultation_contexts, telehealth_requests, telehealth_clinician_shifts, telehealth_video_sessions, appointments, encounters, facilities, staff (+4 more)

### Community 244 - "Community 244"
Cohesion: 0.36
Nodes (10): telehealth_patient_pharmacy_preferences, facilities, patients, telehealth_consultation_pharmacy_choice_versions, telehealth_consultation_contexts, staff, telehealth_consultation_pharmacy_choice_events, trg_telehealth_patient_pharmacy_preferences_append_only (+2 more)

### Community 328 - "Community 328"
Cohesion: 0.50
Nodes (7): telehealth_consultation_disposition_draft_versions, telehealth_consultation_contexts, encounters, staff, telehealth_consultation_disposition_draft_events, trg_telehealth_disposition_versions_append_only, trg_telehealth_disposition_events_append_only

### Community 208 - "Community 208"
Cohesion: 0.31
Nodes (12): telehealth_consultation_prescription_draft_versions, telehealth_consultation_contexts, encounters, medication_vocabulary, staff, telehealth_consultation_pharmacy_choice_versions, telehealth_consultation_prescription_draft_events, trg_telehealth_prescription_draft_catalog (+4 more)

### Community 221 - "Community 221"
Cohesion: 0.32
Nodes (11): telehealth_applicant_identity_review_decisions, telehealth_prospective_applicants, facilities, staff, trg_telehealth_applicant_identity_decision_guard, trg_telehealth_applicant_identity_decisions_append_only, enforce_telehealth_applicant_identity_review_decision(), applicant_row (+3 more)

### Community 268 - "Community 268"
Cohesion: 0.40
Nodes (9): telehealth_applicant_safety_triage_evaluations, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, trg_telehealth_applicant_safety_triage_guard, trg_telehealth_applicant_safety_triage_append_only, enforce_telehealth_applicant_safety_triage_evaluation(), applicant_row (+1 more)

### Community 222 - "Community 222"
Cohesion: 0.35
Nodes (11): telehealth_applicant_visit_purposes, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, trg_telehealth_applicant_visit_purpose_guard, trg_telehealth_applicant_visit_purpose_append_only, enforce_telehealth_applicant_visit_purpose() (+3 more)

### Community 194 - "Community 194"
Cohesion: 0.31
Nodes (13): telehealth_applicant_practice_network_prechecks, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, trg_telehealth_applicant_network_precheck_guard, trg_telehealth_applicant_network_precheck_append_only (+5 more)

### Community 166 - "Community 166"
Cohesion: 0.28
Nodes (15): telehealth_applicant_member_insurance_details, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, telehealth_applicant_practice_network_prechecks, trg_telehealth_applicant_member_details_guard (+7 more)

### Community 134 - "Community 134"
Cohesion: 0.23
Nodes (18): telehealth_applicant_eligibility_results, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, telehealth_applicant_practice_network_prechecks, telehealth_applicant_member_insurance_details (+10 more)

### Community 122 - "Community 122"
Cohesion: 0.23
Nodes (19): telehealth_applicant_practice_network_determinations, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, telehealth_applicant_practice_network_prechecks, telehealth_applicant_member_insurance_details (+11 more)

### Community 167 - "Community 167"
Cohesion: 0.25
Nodes (15): telehealth_applicant_identity_proofing_results, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, telehealth_applicant_practice_network_prechecks, telehealth_applicant_member_insurance_details (+7 more)

### Community 143 - "Community 143"
Cohesion: 0.22
Nodes (17): telehealth_applicant_promotion_authorization_decisions, telehealth_prospective_applicants, facilities, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes, telehealth_applicant_practice_network_prechecks, telehealth_applicant_member_insurance_details (+9 more)

### Community 195 - "Community 195"
Cohesion: 0.30
Nodes (13): telehealth_applicant_synthetic_promotions, telehealth_prospective_applicants, facilities, telehealth_applicant_promotion_authorization_decisions, patients, staff, trg_telehealth_applicant_synthetic_promotion_guard, trg_telehealth_applicant_synthetic_promotions_append_only (+5 more)

### Community 196 - "Community 196"
Cohesion: 0.31
Nodes (13): telehealth_applicant_notice_acknowledgments, telehealth_prospective_applicants, facilities, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_synthetic_promotions, patients, trg_telehealth_applicant_notice_acknowledgment_guard, trg_telehealth_applicant_notice_acknowledgments_append_only (+5 more)

### Community 197 - "Community 197"
Cohesion: 0.31
Nodes (13): telehealth_applicant_registration_details_confirmations, telehealth_prospective_applicants, facilities, telehealth_applicant_notice_acknowledgments, telehealth_applicant_synthetic_promotions, patients, trg_telehealth_registration_details_confirmation_guard, trg_telehealth_registration_details_confirmations_append_only (+5 more)

### Community 103 - "Community 103"
Cohesion: 0.19
Nodes (22): telehealth_applicant_insurance_handoff_confirmations, telehealth_prospective_applicants, facilities, telehealth_applicant_registration_details_confirmations, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_member_insurance_details, telehealth_applicant_eligibility_results (+14 more)

### Community 135 - "Community 135"
Cohesion: 0.23
Nodes (18): telehealth_applicant_communication_access_readiness, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_registration_details_confirmations, telehealth_applicant_insurance_handoff_confirmations, telehealth_applicant_safety_triage_evaluations (+10 more)

### Community 114 - "Community 114"
Cohesion: 0.21
Nodes (20): telehealth_applicant_device_preparations, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_registration_details_confirmations, telehealth_applicant_insurance_handoff_confirmations, telehealth_applicant_safety_triage_evaluations (+12 more)

### Community 136 - "Community 136"
Cohesion: 0.22
Nodes (18): telehealth_applicant_clinical_information_inventories, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_registration_details_confirmations, telehealth_applicant_insurance_handoff_confirmations, telehealth_applicant_safety_triage_evaluations (+10 more)

### Community 71 - "Community 71"
Cohesion: 0.14
Nodes (30): telehealth_applicant_medication_information_receipts, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_clinical_information_inventories, telehealth_applicant_registration_details_confirmations, telehealth_applicant_insurance_handoff_confirmations (+22 more)

### Community 60 - "Community 60"
Cohesion: 0.12
Nodes (35): telehealth_applicant_allergy_information_receipts, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_clinical_information_inventories, telehealth_applicant_medication_information_receipts, telehealth_applicant_registration_details_confirmations (+27 more)

### Community 52 - "Community 52"
Cohesion: 0.11
Nodes (40): telehealth_applicant_health_history_information_receipts, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_clinical_information_inventories, telehealth_applicant_medication_information_receipts, telehealth_applicant_allergy_information_receipts (+32 more)

### Community 72 - "Community 72"
Cohesion: 0.14
Nodes (30): telehealth_applicant_clinical_information_summary_confirmations, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_clinical_information_inventories, telehealth_applicant_medication_information_receipts, telehealth_applicant_allergy_information_receipts (+22 more)

### Community 76 - "Community 76"
Cohesion: 0.16
Nodes (28): telehealth_applicant_pre_request_readiness_acknowledgments, telehealth_prospective_applicants, facilities, telehealth_applicant_synthetic_promotions, patients, telehealth_applicant_registration_details_confirmations, telehealth_applicant_insurance_handoff_confirmations, telehealth_applicant_communication_access_readiness (+20 more)

### Community 123 - "Community 123"
Cohesion: 0.23
Nodes (19): telehealth_prospective_practice_review_cases, telehealth_prospective_applicants, facilities, patients, telehealth_applicant_pre_request_readiness_acknowledgments, telehealth_applicant_practice_review_submissions, trg_enforce_telehealth_applicant_practice_review_submission, trg_telehealth_practice_review_cases_append_only (+11 more)

### Community 269 - "Community 269"
Cohesion: 0.38
Nodes (9): telehealth_practice_review_claims, telehealth_prospective_practice_review_cases, facilities, trg_enforce_telehealth_practice_review_claim, trg_telehealth_practice_review_claims_append_only, enforce_telehealth_practice_review_claim(), case_row, applicant_row (+1 more)

### Community 153 - "Community 153"
Cohesion: 0.25
Nodes (16): telehealth_practice_review_authorizations, telehealth_prospective_practice_review_cases, telehealth_prospective_applicants, facilities, patients, telehealth_applicant_practice_review_submissions, telehealth_applicant_pre_request_readiness_acknowledgments, telehealth_practice_review_claims (+8 more)

### Community 115 - "Community 115"
Cohesion: 0.19
Nodes (20): telehealth_applicant_request_creations, telehealth_requests, telehealth_prospective_applicants, facilities, patients, telehealth_applicant_synthetic_promotions, telehealth_prospective_practice_review_cases, telehealth_practice_review_authorizations (+12 more)

### Community 124 - "Community 124"
Cohesion: 0.22
Nodes (19): telehealth_applicant_request_location_confirmations, telehealth_patient_locations, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_creations, telehealth_applicant_communication_access_readiness, facilities, patients (+11 more)

### Community 90 - "Community 90"
Cohesion: 0.17
Nodes (25): telehealth_applicant_request_universal_safety_assessments, telehealth_triage_assessments, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_creations, telehealth_applicant_request_location_confirmations, telehealth_patient_locations, telehealth_applicant_safety_triage_evaluations (+17 more)

### Community 81 - "Community 81"
Cohesion: 0.17
Nodes (26): telehealth_applicant_request_complaint_triage_assessments, telehealth_triage_assessments, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_creations, telehealth_applicant_request_location_confirmations, telehealth_patient_locations, telehealth_applicant_request_universal_safety_assessments (+18 more)

### Community 48 - "Community 48"
Cohesion: 0.10
Nodes (45): telehealth_applicant_request_intake_snapshots, telehealth_intake_snapshots, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_creations, telehealth_applicant_request_location_confirmations, telehealth_patient_locations, telehealth_applicant_request_universal_safety_assessments (+37 more)

### Community 61 - "Community 61"
Cohesion: 0.12
Nodes (35): telehealth_applicant_request_insurance_source_confirmations, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_intake_snapshots, telehealth_applicant_request_creations, telehealth_applicant_insurance_handoff_confirmations, telehealth_applicant_member_insurance_details, telehealth_applicant_eligibility_results (+27 more)

### Community 100 - "Community 100"
Cohesion: 0.18
Nodes (23): telehealth_applicant_request_eligibility_verifications, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_insurance_source_confirmations, telehealth_applicant_member_insurance_details, facilities, patients, trg_th_app_request_eligibility_guard (+15 more)

### Community 116 - "Community 116"
Cohesion: 0.20
Nodes (20): telehealth_applicant_request_practice_network_verifications, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_eligibility_verifications, facilities, patients, trg_th_app_request_practice_network_guard, trg_th_app_request_practice_network_append (+12 more)

### Community 98 - "Community 98"
Cohesion: 0.17
Nodes (24): telehealth_applicant_request_rendering_candidate_selections, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_eligibility_verifications, telehealth_applicant_request_practice_network_verifications, facilities, patients, staff (+16 more)

### Community 82 - "Community 82"
Cohesion: 0.16
Nodes (26): telehealth_applicant_request_participation_contexts, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_eligibility_verifications, telehealth_applicant_request_practice_network_verifications, telehealth_applicant_request_rendering_candidate_selections, facilities, patients (+18 more)

### Community 91 - "Community 91"
Cohesion: 0.16
Nodes (25): telehealth_applicant_request_participation_evaluations, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_participation_contexts, telehealth_applicant_request_eligibility_verifications, telehealth_applicant_request_practice_network_verifications, telehealth_applicant_request_rendering_candidate_selections, facilities (+17 more)

### Community 104 - "Community 104"
Cohesion: 0.19
Nodes (22): telehealth_applicant_request_operational_review_submissions, telehealth_requests, telehealth_prospective_applicants, telehealth_applicant_request_participation_evaluations, facilities, patients, staff, trg_th_app_request_op_review_submission_guard (+14 more)

### Community 99 - "Community 99"
Cohesion: 0.17
Nodes (24): telehealth_applicant_request_queue_authorizations, telehealth_requests, telehealth_applicant_request_operational_review_submissions, telehealth_prospective_applicants, facilities, patients, staff, trg_th_app_request_queue_auth_guard (+16 more)

### Community 270 - "Community 270"
Cohesion: 0.38
Nodes (9): telehealth_consultation_prescription_orders, telehealth_consultation_contexts, prescriptions, telehealth_consultation_prescription_draft_versions, staff, telehealth_consultation_pharmacy_choice_versions, trg_telehealth_prescription_orders_append_only, trg_prescriptions_reject_signed_telehealth_mutation (+1 more)

### Community 297 - "Community 297"
Cohesion: 0.44
Nodes (8): telehealth_consultation_final_clinical_review_versions, telehealth_consultation_contexts, encounters, telehealth_consultation_prescription_orders, staff, telehealth_consultation_final_clinical_review_events, trg_telehealth_final_clinical_review_versions_append_only, trg_telehealth_final_clinical_review_events_append_only

### Community 271 - "Community 271"
Cohesion: 0.20
Nodes (10): Get-AdministrationHeaders(), Set-AdministrationFacilityContext(), Cancel-AppointmentTestFixture(), Archive-EncounterTestFixture(), Archive-DocumentTestFixture(), Archive-MessageTestFixture(), New-ReceivedProcedureSpecimen(), Test-ProcedureOrderRetention() (+2 more)

### Community 402 - "Community 402"
Cohesion: 0.40
Nodes (2): Invoke-Api(), Start-TestApi()

### Community 403 - "Community 403"
Cohesion: 0.40
Nodes (2): Invoke-JsonRequest(), Get-EncounterDetail()

### Community 299 - "Community 299"
Cohesion: 0.28
Nodes (4): Invoke-JsonRequest(), Get-HttpStatus(), Invoke-StatusRequest(), Get-EncounterDetail()

### Community 356 - "Community 356"
Cohesion: 0.33
Nodes (2): Get-PropertyValue(), Get-PathOperation()

### Community 457 - "Community 457"
Cohesion: 0.50
Nodes (2): Invoke-FixtureSql(), Set-FixturePortalState()

### Community 272 - "Community 272"
Cohesion: 0.27
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-VerifiedApplicant()

### Community 300 - "Community 300"
Cohesion: 0.28
Nodes (3): Authorization-Path(), Invoke-Authorization(), Get-AuthorizationStatus()

### Community 362 - "Community 362"
Cohesion: 0.38
Nodes (3): Claim-Path(), Invoke-Claim(), Get-ClaimStatus()

### Community 404 - "Community 404"
Cohesion: 0.47
Nodes (3): Packet-Path(), Get-Packet(), Get-PacketStatus()

### Community 273 - "Community 273"
Cohesion: 0.27
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-ProofedApplicant()

### Community 209 - "Community 209"
Cohesion: 0.18
Nodes (3): New-MigraineAnswers(), New-SleepAnswers(), New-ComplaintBody()

### Community 301 - "Community 301"
Cohesion: 0.39
Nodes (5): Request-CreationPath(), Applicant-Headers(), Invoke-RequestCreation(), Get-RequestCreation(), Get-RequestCreationStatus()

### Community 365 - "Community 365"
Cohesion: 0.33
Nodes (2): Eligibility-Path(), Invoke-ContendedEligibilityPosts()

### Community 366 - "Community 366"
Cohesion: 0.33
Nodes (2): Source-Path(), Invoke-ContendedSourcePosts()

### Community 367 - "Community 367"
Cohesion: 0.33
Nodes (2): Intake-Path(), Invoke-ContendedIntakePosts()

### Community 302 - "Community 302"
Cohesion: 0.31
Nodes (4): Request-LocationPath(), Invoke-RequestLocation(), Get-RequestLocation(), Get-RequestLocationStatus()

### Community 368 - "Community 368"
Cohesion: 0.33
Nodes (2): Submission-Path(), Invoke-ContendedSubmissionPosts()

### Community 369 - "Community 369"
Cohesion: 0.33
Nodes (2): Participation-Path(), Invoke-ContendedParticipationPosts()

### Community 370 - "Community 370"
Cohesion: 0.33
Nodes (2): Evaluation-Path(), Invoke-ContendedEvaluationPosts()

### Community 371 - "Community 371"
Cohesion: 0.33
Nodes (2): PracticeNetwork-Path(), Invoke-ContendedPracticeNetworkPosts()

### Community 245 - "Community 245"
Cohesion: 0.24
Nodes (5): Queue-Authorization-Path(), Applicant-Queue-Status-Path(), Get-Applicant-Queue-Status(), Post-Queue-Authorization(), Invoke-ContendedQueueAuthorizations()

### Community 372 - "Community 372"
Cohesion: 0.33
Nodes (2): Candidate-Path(), Invoke-ContendedCandidatePosts()

### Community 303 - "Community 303"
Cohesion: 0.31
Nodes (4): Request-SafetyPath(), Invoke-RequestSafety(), Get-RequestSafety(), Get-RequestSafetyStatus()

### Community 246 - "Community 246"
Cohesion: 0.24
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-AuthorizedApplicant()

### Community 247 - "Community 247"
Cohesion: 0.24
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-EligibilityReadyApplicant()

### Community 330 - "Community 330"
Cohesion: 0.29
Nodes (2): Invoke-Scalar(), Get-CanonicalCounts()

### Community 248 - "Community 248"
Cohesion: 0.24
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-NetworkedApplicant()

### Community 223 - "Community 223"
Cohesion: 0.21
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-PrecheckedApplicant()

### Community 249 - "Community 249"
Cohesion: 0.24
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-NetworkReadyApplicant()

### Community 224 - "Community 224"
Cohesion: 0.21
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-VisitPurposeApplicant()

### Community 225 - "Community 225"
Cohesion: 0.21
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-ApprovedApplicant()

### Community 226 - "Community 226"
Cohesion: 0.21
Nodes (5): New-Secret(), New-Key(), Invoke-Scalar(), Get-Counts(), New-SafetyPassedApplicant()

### Community 250 - "Community 250"
Cohesion: 0.20
Nodes (2): Scalar(), Sql-Fails()

### Community 215 - "Community 215"
Cohesion: 0.30
Nodes (2): 0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts, 286a7d3 Add local Docker management scripts

### Community 478 - "Community 478"
Cohesion: 0.50
Nodes (3): 1b2ad1b docs(phase-2): record Graphify index evidence, c3a55ee chore(tooling): add Graphify code-navigation index, ccb790f docs(phase-2): record Graphify supply-chain residual

### Community 200 - "Community 200"
Cohesion: 0.14
Nodes (12): here, repositoryRoot, outputPath, historyBasePath, historyRef, sourceRevision, log, commits (+4 more)

## Knowledge Gaps
- **1005 isolated node(s):** `AccessibilityFinding`, `clinicianFixture`, `codingEncounter`, `encounter`, `composeRoot` (+1000 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 385`** (2 nodes): `PatientMessagesResponse`, `getPatientMessages()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 568`** (1 nodes): `AzureOperationsOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 569`** (1 nodes): `DatabaseConnectionOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 322`** (1 nodes): `RuntimeSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 292`** (1 nodes): `AddressBookRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 128`** (1 nodes): `AdministrationDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 31`** (1 nodes): `AdministrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 20`** (1 nodes): `AppointmentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 130`** (2 nodes): `AuthRepository`, `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 87`** (1 nodes): `AuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 217`** (1 nodes): `AzureOperationsAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 93`** (1 nodes): `AzureOperationsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 218`** (1 nodes): `BatchCommunicationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (1 nodes): `BillingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 349`** (1 nodes): `ChartTrackerRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 236`** (1 nodes): `ClinicalAlertEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 29`** (1 nodes): `ClinicalFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 32`** (1 nodes): `ClinicalFormRuntime`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 46`** (1 nodes): `ClinicalListRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 94`** (1 nodes): `ClinicalListStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 16`** (1 nodes): `DocumentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 109`** (1 nodes): `DocumentTemplateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 237`** (1 nodes): `EncounterLayoutFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (2 nodes): `EncounterRepository`, `DiagnosisAccumulator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 164`** (1 nodes): `EncounterStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 238`** (1 nodes): `ExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 139`** (1 nodes): `ExternalLaboratorySourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 95`** (1 nodes): `FhirRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 88`** (1 nodes): `IntegrationIdempotencyConflictException`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 140`** (1 nodes): `InventoryAccountingIntegrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 131`** (1 nodes): `InventoryCostPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 113`** (1 nodes): `InventoryReplenishmentPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 59`** (2 nodes): `InventoryRepository`, `InventoryItemBuilder`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 147`** (1 nodes): `ToInventoryLot()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 180`** (1 nodes): `InventoryValuationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 219`** (1 nodes): `LegacyClinicalFormDisplayRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 64`** (1 nodes): `ManagedRecordRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 54`** (1 nodes): `MessageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 323`** (1 nodes): `OfficeNoteRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 89`** (1 nodes): `PatientDisclosureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 110`** (1 nodes): `PatientMergeExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 239`** (1 nodes): `PatientPortalExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 28`** (1 nodes): `PatientPortalRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 148`** (1 nodes): `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 240`** (1 nodes): `PatientPrintRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 386`** (1 nodes): `PatientRecordRequestRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 19`** (1 nodes): `PatientRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 191`** (1 nodes): `PatientSdohRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 241`** (1 nodes): `PatientXmlExchangeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 133`** (1 nodes): `ProcedureDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 21`** (1 nodes): `ProcedureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 325`** (1 nodes): `RecallRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 102`** (1 nodes): `ReferralRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 70`** (1 nodes): `ReportDefinitionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 111`** (2 nodes): `ReportExecutionQueueRepository`, `WorkerCancellationState`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 43`** (1 nodes): `ReportExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 67`** (1 nodes): `ReportRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 149`** (1 nodes): `TherapyGroupRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 305`** (2 nodes): `ISyntheticTelehealthComplaintTriageEvaluator`, `SyntheticTelehealthComplaintTriageEvaluator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 374`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 335`** (2 nodes): `SyntheticTelehealthApplicantAllergyCatalog`, `TelehealthApplicantAllergyInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 154`** (1 nodes): `TelehealthApplicantAllergyInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 407`** (1 nodes): `TelehealthApplicantAllergyInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 408`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 169`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 409`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 155`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 410`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 156`** (1 nodes): `TelehealthApplicantCommunicationAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 411`** (1 nodes): `TelehealthApplicantCommunicationAccessService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 558`** (1 nodes): `TelehealthApplicantConnectionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 170`** (1 nodes): `TelehealthApplicantDevicePreparationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 412`** (1 nodes): `TelehealthApplicantDevicePreparationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 336`** (2 nodes): `SyntheticTelehealthApplicantHealthHistoryTopicCatalog`, `TelehealthApplicantHealthHistoryInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 157`** (1 nodes): `TelehealthApplicantHealthHistoryInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 413`** (1 nodes): `TelehealthApplicantHealthHistoryInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 308`** (1 nodes): `TelehealthApplicantIdentityReviewRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 309`** (1 nodes): `TelehealthApplicantIdentityReviewService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 461`** (1 nodes): `TelehealthApplicantInsuranceHandoffPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 182`** (1 nodes): `TelehealthApplicantInsuranceHandoffRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 414`** (1 nodes): `TelehealthApplicantInsuranceHandoffService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 337`** (2 nodes): `SyntheticTelehealthApplicantMedicationCatalog`, `TelehealthApplicantMedicationInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 158`** (1 nodes): `TelehealthApplicantMedicationInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 415`** (1 nodes): `TelehealthApplicantMedicationInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 310`** (1 nodes): `TelehealthApplicantNoticeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 416`** (1 nodes): `TelehealthApplicantNoticeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 417`** (1 nodes): `TelehealthApplicantPracticeReviewAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 338`** (1 nodes): `TelehealthApplicantPracticeReviewClaimRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 462`** (1 nodes): `TelehealthApplicantPracticeReviewInboxService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 377`** (1 nodes): `TelehealthApplicantPracticeReviewSubmissionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 117`** (1 nodes): `TelehealthApplicantPreRequestReadinessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 418`** (1 nodes): `TelehealthApplicantPreRequestReadinessService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 281`** (1 nodes): `TelehealthApplicantPromotionAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 311`** (1 nodes): `TelehealthApplicantPromotionAuthorizationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 282`** (1 nodes): `TelehealthApplicantRegistrationDetailsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 419`** (1 nodes): `TelehealthApplicantRegistrationDetailsService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 227`** (1 nodes): `TelehealthApplicantRequestComplaintTriagePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 173`** (1 nodes): `TelehealthApplicantRequestComplaintTriageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 463`** (1 nodes): `TelehealthApplicantRequestComplaintTriageService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 560`** (1 nodes): `TelehealthApplicantRequestCreationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 254`** (1 nodes): `TelehealthApplicantRequestCreationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 421`** (1 nodes): `TelehealthApplicantRequestCreationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 498`** (1 nodes): `TelehealthApplicantRequestEligibilityPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 174`** (1 nodes): `TelehealthApplicantRequestEligibilityRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 229`** (1 nodes): `TelehealthApplicantRequestEligibilityService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 499`** (1 nodes): `TelehealthApplicantRequestInsuranceSourcePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 183`** (1 nodes): `TelehealthApplicantRequestInsuranceSourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 422`** (1 nodes): `TelehealthApplicantRequestInsuranceSourceService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 464`** (1 nodes): `TelehealthApplicantRequestIntakePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 175`** (1 nodes): `TelehealthApplicantRequestIntakeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 423`** (1 nodes): `TelehealthApplicantRequestIntakeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 500`** (1 nodes): `TelehealthApplicantRequestLocationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 210`** (1 nodes): `TelehealthApplicantRequestLocationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 424`** (1 nodes): `TelehealthApplicantRequestLocationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 501`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 184`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 425`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 426`** (1 nodes): `TelehealthApplicantRequestParticipationContextPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 159`** (1 nodes): `TelehealthApplicantRequestParticipationContextRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 427`** (1 nodes): `TelehealthApplicantRequestParticipationContextService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 465`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 144`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 428`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 502`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 176`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 288`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 503`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 211`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 429`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 561`** (1 nodes): `TelehealthApplicantRequestQueueStatusPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 466`** (1 nodes): `TelehealthApplicantRequestQueueStatusRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 562`** (1 nodes): `TelehealthApplicantRequestQueueStatusService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 430`** (1 nodes): `TelehealthApplicantRequestRenderingCandidatePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 160`** (1 nodes): `TelehealthApplicantRequestRenderingCandidateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 431`** (1 nodes): `TelehealthApplicantRequestRenderingCandidateService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 259`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 177`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 432`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 433`** (1 nodes): `TelehealthApplicantSyntheticPromotionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 212`** (1 nodes): `TelehealthApplicantSyntheticPromotionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 315`** (1 nodes): `TelehealthApplicantSyntheticPromotionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 125`** (1 nodes): `TelehealthConsultationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 178`** (1 nodes): `TelehealthConsultationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 213`** (2 nodes): `TelehealthSafetyDispositionConflictException`, `TelehealthDispositionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 504`** (1 nodes): `TelehealthAuthorizationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 467`** (1 nodes): `TelehealthEncounterFinalizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 563`** (1 nodes): `TelehealthEncounterFinalizationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 10`** (1 nodes): `TelehealthEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 230`** (1 nodes): `TelehealthFinalClinicalReviewRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 468`** (1 nodes): `TelehealthFinalClinicalReviewService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 341`** (1 nodes): `TelehealthOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 434`** (2 nodes): `TelehealthRuntimeSafetyPolicy`, `TelehealthServiceRegistration`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 231`** (2 nodes): `IPharmacyDirectory`, `SyntheticTelehealthPharmacyDirectory`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 232`** (1 nodes): `TelehealthPharmacyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 137`** (1 nodes): `TelehealthPrescriptionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 342`** (1 nodes): `TelehealthPrescriptionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 470`** (2 nodes): `IProfessionalClaimGateway`, `SyntheticProfessionalClaimGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 564`** (1 nodes): `TelehealthProfessionalClaimPreparationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 565`** (1 nodes): `TelehealthProfessionalClaimPreparationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 260`** (1 nodes): `TelehealthProspectiveApplicantPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 161`** (1 nodes): `TelehealthProspectiveApplicantRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 379`** (1 nodes): `TelehealthProspectiveApplicantService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 435`** (2 nodes): `ITelehealthProspectiveEligibilityGateway`, `SyntheticTelehealthProspectiveEligibilityGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 343`** (1 nodes): `TelehealthProspectiveEligibilityRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 261`** (1 nodes): `TelehealthProspectiveEligibilityService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 436`** (2 nodes): `ITelehealthProspectiveIdentityProofingGateway`, `SyntheticTelehealthProspectiveIdentityProofingGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 290`** (1 nodes): `TelehealthProspectiveIdentityProofingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 380`** (1 nodes): `TelehealthProspectiveIdentityProofingService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 344`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 471`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsProtector`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 345`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 472`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 381`** (2 nodes): `ITelehealthProspectivePracticeNetworkGateway`, `SyntheticTelehealthProspectivePracticeNetworkGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 291`** (1 nodes): `TelehealthProspectivePracticeNetworkPrecheckRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 382`** (1 nodes): `TelehealthProspectivePracticeNetworkPrecheckService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 318`** (1 nodes): `TelehealthProspectivePracticeNetworkRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 319`** (1 nodes): `TelehealthProspectivePracticeNetworkService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 437`** (1 nodes): `TelehealthProspectiveSafetyTriagePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 383`** (1 nodes): `TelehealthProspectiveSafetyTriageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 473`** (1 nodes): `TelehealthProspectiveSafetyTriageService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 384`** (1 nodes): `TelehealthProspectiveVisitPurposeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 474`** (1 nodes): `TelehealthProspectiveVisitPurposeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (1 nodes): `TelehealthRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 106`** (1 nodes): `TelehealthService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 439`** (2 nodes): `ITelehealthVideoProvider`, `SyntheticTelehealthVideoProvider`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 214`** (1 nodes): `TelehealthVideoRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 233`** (1 nodes): `TelehealthVideoService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 505`** (1 nodes): `AdministrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 506`** (1 nodes): `AdministrativeReferenceEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 507`** (1 nodes): `AppointmentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 192`** (1 nodes): `AvenChartOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 97`** (1 nodes): `AzureDeploymentProfilePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 387`** (1 nodes): `AzureOperationsEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 508`** (1 nodes): `BillingEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 509`** (1 nodes): `ClinicalFormEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 510`** (1 nodes): `ClinicalListEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 511`** (1 nodes): `ClinicalWorkflowEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 512`** (1 nodes): `ConfigurationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 266`** (1 nodes): `DatabaseSchemaMigrator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 441`** (1 nodes): `DevelopmentTestIdentityProviderEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 513`** (1 nodes): `DocumentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 514`** (1 nodes): `DocumentTemplateEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 515`** (1 nodes): `EncounterEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 220`** (1 nodes): `EndpointAccessPolicies`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 479`** (1 nodes): `ExternalLaboratoryFhirIntakeEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 480`** (1 nodes): `FhirR4Endpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 442`** (1 nodes): `FhirR4ValidationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 481`** (1 nodes): `IntegrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 516`** (1 nodes): `InventoryEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 517`** (1 nodes): `ManagedRecordEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 518`** (1 nodes): `MessageEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 519`** (1 nodes): `OfficeNoteEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 443`** (1 nodes): `PatientEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 520`** (1 nodes): `PatientEngagementEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 521`** (1 nodes): `PatientPortalEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 522`** (1 nodes): `ProcedureEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 523`** (1 nodes): `ReportEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 524`** (1 nodes): `TherapyGroupEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 459`** (1 nodes): `AuthorizationPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 69`** (1 nodes): `BrowserOidcSessionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 405`** (1 nodes): `TestIdentityProviderService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 406`** (2 nodes): `IStaffIdentityAdapter`, `LocalDevelopmentStaffIdentityAdapter`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 346`** (1 nodes): `ClinicalWorkflowPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 585`** (1 nodes): `AvenChart.Api.csproj`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 347`** (1 nodes): `DatabaseBootstrapCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 263`** (1 nodes): `FhirR4ValidationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 475`** (1 nodes): `StaffAccessContextServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 460`** (1 nodes): `SyntheticProfessionalClaimGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 275`** (1 nodes): `SyntheticTelehealthComplaintTriageEvaluatorTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 331`** (1 nodes): `SyntheticTelehealthCoverageGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 332`** (1 nodes): `SyntheticTelehealthPharmacyDirectoryTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 333`** (1 nodes): `SyntheticTelehealthProspectiveEligibilityGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 306`** (1 nodes): `SyntheticTelehealthProspectiveIdentityProofingGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 307`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 334`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 168`** (1 nodes): `TelehealthApplicantAllergyInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 276`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 277`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 278`** (1 nodes): `TelehealthApplicantCommunicationAccessPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 559`** (1 nodes): `TelehealthApplicantConnectionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 251`** (1 nodes): `TelehealthApplicantDevicePreparationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 171`** (1 nodes): `TelehealthApplicantHealthHistoryInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 375`** (1 nodes): `TelehealthApplicantIdentityReviewPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 279`** (1 nodes): `TelehealthApplicantInsuranceHandoffPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 172`** (1 nodes): `TelehealthApplicantMedicationInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 280`** (1 nodes): `TelehealthApplicantNoticePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 376`** (1 nodes): `TelehealthApplicantPracticeReviewInboxPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 339`** (1 nodes): `TelehealthApplicantPracticeReviewPacketPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 252`** (1 nodes): `TelehealthApplicantPracticeReviewSubmissionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 253`** (1 nodes): `TelehealthApplicantPreRequestReadinessPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 378`** (1 nodes): `TelehealthApplicantPromotionAuthorizationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 312`** (1 nodes): `TelehealthApplicantRegistrationDetailsPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 228`** (1 nodes): `TelehealthApplicantRequestComplaintTriagePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 420`** (1 nodes): `TelehealthApplicantRequestCreationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 283`** (1 nodes): `TelehealthApplicantRequestEligibilityPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 284`** (1 nodes): `TelehealthApplicantRequestInsuranceSourcePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 285`** (1 nodes): `TelehealthApplicantRequestIntakePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 286`** (1 nodes): `TelehealthApplicantRequestLocationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 313`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 255`** (1 nodes): `TelehealthApplicantRequestParticipationContextPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 256`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 287`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 257`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 289`** (1 nodes): `TelehealthApplicantRequestQueueStatusPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 258`** (1 nodes): `TelehealthApplicantRequestRenderingCandidatePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 314`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 340`** (1 nodes): `TelehealthApplicantSyntheticPromotionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 73`** (1 nodes): `TelehealthConsultationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 469`** (1 nodes): `TelehealthPatientQueueStatusProjectorTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 198`** (1 nodes): `TelehealthPrescriptionServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 317`** (1 nodes): `TelehealthProspectiveApplicantPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 105`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 320`** (1 nodes): `TelehealthProspectiveSafetyTriagePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 438`** (1 nodes): `TelehealthProspectiveVisitPurposePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 199`** (1 nodes): `TelehealthRuntimeSafetyPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 262`** (1 nodes): `TelehealthSafetyDispositionRulesTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 566`** (1 nodes): `TelehealthStateMachineTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 567`** (2 nodes): `pharmacies`, `prescriptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 570`** (1 nodes): `schema_migrations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 571`** (1 nodes): `statement_email_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 525`** (2 nodes): `integration_outbox`, `integration_inbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 572`** (1 nodes): `phi_access_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 573`** (1 nodes): `encounter_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 526`** (2 nodes): `coding_catalog_audit_events`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 574`** (1 nodes): `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 575`** (1 nodes): `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 576`** (1 nodes): `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 527`** (2 nodes): `form_option_lists`, `form_option_values`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 528`** (2 nodes): `encounter_clinical_alert_acknowledgments`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 529`** (2 nodes): `patient_record_requests`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 530`** (2 nodes): `patient_sdoh_assessments`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 577`** (1 nodes): `office_notes`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 578`** (1 nodes): `address_book_contacts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 579`** (1 nodes): `track_anything_types`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 580`** (1 nodes): `patient_education_resources`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 531`** (2 nodes): `recall_activity`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 581`** (1 nodes): `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 532`** (2 nodes): `patient_duplicate_review_dispositions`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 533`** (2 nodes): `document_template_binary_versions`, `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 534`** (2 nodes): `patient_xml_exchange_audits`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 535`** (2 nodes): `inventory_count_reconciliations`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 536`** (2 nodes): `practice_setting_revisions`, `practice_settings`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 537`** (2 nodes): `coding_catalog_revisions`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 538`** (2 nodes): `form_option_list_revisions`, `form_option_lists`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 539`** (2 nodes): `form_layout_revisions`, `form_layouts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 540`** (2 nodes): `clinical_alert_rule_revisions`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 541`** (2 nodes): `module_catalog_revisions`, `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 542`** (2 nodes): `api_client_registry_revisions`, `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 543`** (2 nodes): `inventory_lot_metadata_audits`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 544`** (2 nodes): `inventory_lot_destructions`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 545`** (2 nodes): `inventory_controlled_report_runs`, `inventory_controlled_locations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 546`** (2 nodes): `inventory_controlled_report_exports`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 547`** (2 nodes): `coding_catalog_change_requests`, `coding_catalog_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 548`** (2 nodes): `form_layout_change_requests`, `form_layout_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 549`** (2 nodes): `form_option_list_change_requests`, `form_option_list_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 550`** (2 nodes): `clinical_alert_rule_change_requests`, `clinical_alert_rule_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 551`** (2 nodes): `module_change_requests`, `module_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 552`** (2 nodes): `api_client_change_requests`, `api_client_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 553`** (2 nodes): `procedure_specimen_events`, `lab_specimens`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 554`** (2 nodes): `recall_lifecycle_events`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 555`** (2 nodes): `lab_reports`, `lab_specimens`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 556`** (2 nodes): `patient_registration_duplicate_reviews`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 582`** (1 nodes): `inventory_controlled_action_attestations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 402`** (2 nodes): `Invoke-Api()`, `Start-TestApi()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 403`** (2 nodes): `Invoke-JsonRequest()`, `Get-EncounterDetail()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 356`** (2 nodes): `Get-PropertyValue()`, `Get-PathOperation()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 457`** (2 nodes): `Invoke-FixtureSql()`, `Set-FixturePortalState()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 365`** (2 nodes): `Eligibility-Path()`, `Invoke-ContendedEligibilityPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 366`** (2 nodes): `Source-Path()`, `Invoke-ContendedSourcePosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 367`** (2 nodes): `Intake-Path()`, `Invoke-ContendedIntakePosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 368`** (2 nodes): `Submission-Path()`, `Invoke-ContendedSubmissionPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 369`** (2 nodes): `Participation-Path()`, `Invoke-ContendedParticipationPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 370`** (2 nodes): `Evaluation-Path()`, `Invoke-ContendedEvaluationPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 371`** (2 nodes): `PracticeNetwork-Path()`, `Invoke-ContendedPracticeNetworkPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 372`** (2 nodes): `Candidate-Path()`, `Invoke-ContendedCandidatePosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 330`** (2 nodes): `Invoke-Scalar()`, `Get-CanonicalCounts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 250`** (2 nodes): `Scalar()`, `Sql-Fails()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 215`** (2 nodes): `0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts`, `286a7d3 Add local Docker management scripts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AdministrationRepository` connect `Community 31` to `Community 34`, `Community 146`, `Community 163`, `Community 119`, `Community 101`, `Community 129`, `Community 189`, `Community 92`, `Community 108`, `Community 112`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Why does `PatientPortalRepository` connect `Community 28` to `Community 41`, `Community 141`, `Community 142`, `Community 165`, `Community 80`, `Community 68`, `Community 293`, `Community 294`, `Community 148`, `Community 324`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Why does `TelehealthEndpoints` connect `Community 10` to `Community 7`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **What connects `AccessibilityFinding`, `clinicianFixture`, `codingEncounter` to the rest of the system?**
  _1005 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 47` be split into smaller, more focused modules?**
  _Cohesion score 0.057971014492753624 - nodes in this community are weakly interconnected._
- **Should `Community 24` be split into smaller, more focused modules?**
  _Cohesion score 0.044427989633469084 - nodes in this community are weakly interconnected._
- **Should `Community 6` be split into smaller, more focused modules?**
  _Cohesion score 0.017471736896197326 - nodes in this community are weakly interconnected._