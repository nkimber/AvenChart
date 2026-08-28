# Graph Report - .  (2026-08-28)

## Corpus Check
- Large corpus: 788 files · ~739,611 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 6742 nodes · 15988 edges · 309 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output
- Edge kinds: calls: 4305 · contains: 3157 · MODIFIES: 2386 · method: 2385 · imports: 1571 · ON_BRANCH: 803 · imports_from: 458 · PARENT_OF: 393 · references: 378 · inherits: 87 · reads_from: 39 · triggers: 20 · re_exports: 6


## Input Scope
- Requested: committed
- Resolved: committed (source: cli)
- Included files: 788 · Candidates: 1000
- Excluded: 384 untracked · 48989 ignored · 1 sensitive · 0 missing committed
- Recommendation: Use --scope all or graphify.yaml inputs.corpus for a knowledge-base folder.

## Graph Freshness
- Built from Git commit: `ccb790f`
- Compare this hash to `git rev-parse HEAD` before trusting freshness-sensitive graph output.
## God Nodes (most connected - your core abstractions)
1. `AdministrationRepository` - 233 edges
2. `PatientPortalRepository` - 215 edges
3. `clinicianGet()` - 162 edges
4. `clinicianPost()` - 135 edges
5. `InventoryRepository` - 110 edges
6. `BillingRepository` - 102 edges
7. `DocumentRepository` - 101 edges
8. `PatientRepository` - 95 edges
9. `AppointmentRepository` - 94 edges
10. `clinicianPut()` - 88 edges

## Surprising Connections (you probably didn't know these)
- `access_user_memberships` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 237 → community 128_
- `appointments` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 128 → community 90_
- `inventory_lots` --references--> `facilities`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 208 → community 128_
- `lab_orders` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 170 → community 90_
- `lab_orders` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 170 → community 128_

## Communities

### Community 39 - "Community 39"
Cohesion: 0.06
Nodes (13): AccessibilityFinding, clinicianFixture, codingEncounter, encounter, composeRoot, fixtureSql(), cleanupLifecycleFixture(), AvenChartUiFixtures (+5 more)

### Community 19 - "Community 19"
Cohesion: 0.04
Nodes (39): ConvertTo-RequestJson(), Invoke-Api(), Invoke-Json(), New-Field(), New-TestSchema(), Move-Definition(), Move-Instance(), 022ba1c feat(forms): adopt legacy bronchitis sinus exam (+31 more)

### Community 16 - "Community 16"
Cohesion: 0.02
Nodes (1): af0f321 fix(validation): harden runtime workflow evidence

### Community 34 - "Community 34"
Cohesion: 0.06
Nodes (19): OperationalReportsResponse, getOperationalReports(), AsyncState, ReportExecutionOptions, ReportExecutionWorker, BackgroundService, saved_report_run_events, saved_report_runs (+11 more)

### Community 6 - "Community 6"
Cohesion: 0.03
Nodes (19): ClinicalAlertSeverity, ClinicalAlertSeverityPresentation, getClinicalAlertSeverity(), LabResultFlagState, NormalizedLabResultFlag, normalValues, normalizeLabResultFlag(), FieldDefinition (+11 more)

### Community 119 - "Community 119"
Cohesion: 0.18
Nodes (9): deleteStaffMessageFixture(), deleteProcedureOrderFixture(), deleteClinicalListFixture(), deletePatientDocumentFixtures(), deletePrescriptionFixture(), runProviderAssignmentSql(), deleteProviderAssignmentFixtures(), deletePatientAdministrationFixtures() (+1 more)

### Community 107 - "Community 107"
Cohesion: 0.14
Nodes (8): LoginResponse, LegacyClinicalFormDisplayEndpoints, clinical_form_migration_manifests, 0e8f4e9 feat(forms): adopt legacy review systems genitourinary, 41fb6b1 feat(forms): display legacy soap snapshots, 8c59573 feat(forms): add clinic note migration manifest, ada3b3e feat(forms): display legacy clinic note snapshots, d681a73 feat(forms): display legacy clinical instructions

### Community 48 - "Community 48"
Cohesion: 0.07
Nodes (17): readiness, IdentityProviderReadinessCounts, IdentityAdapterContract, IdentityTypeReadiness, IdentityBoundaryControl, IdentityVerification, IdentityProviderGap, IdentityProviderReadiness (+9 more)

### Community 177 - "Community 177"
Cohesion: 0.25
Nodes (7): distRoot, assetsRoot, initialMatch, files, initial, violations, result

### Community 21 - "Community 21"
Cohesion: 0.03
Nodes (65): EntryChooser, ClinicianLogin, PortalLogin, OidcCallback, PortalShell, PortalDashboard, PortalMessages, PortalAppointments (+57 more)

### Community 2 - "Community 2"
Cohesion: 0.02
Nodes (97): getPatientPortalAppointments(), PatientDuplicateSearchResponse, findPatientDuplicateCandidates(), InventoryAccountingIntegrationDecisionDefinition, InventoryAccountingIntegrationChangeRequest, InventoryAccountingIntegrationChangeRequestDetailResponse, getInventoryAccountingIntegrationDecision(), createInventoryAccountingIntegrationChangeRequest() (+89 more)

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (321): AuthLoginInput, AuthLoginResponse, AuthAccessFacility, AuthAccessContextResponse, AuthAccessContextGrantResponse, AuthAccessContextGrantUpdateInput, PatientPortalLoginInput, PatientPortalLoginResponse (+313 more)

### Community 24 - "Community 24"
Cohesion: 0.06
Nodes (37): AuthSessionResponse, PatientPortalSessionResponse, login(), loginPatientPortal(), getPatientPortalSession(), endPatientPortalSession(), PatientPortalHomeSummaryResponse, getPatientPortalHome() (+29 more)

### Community 25 - "Community 25"
Cohesion: 0.04
Nodes (35): getCurrentSession(), logout(), searchPatients(), AuthorizationWorkQueueFilters, AuthorizationWorkQueueResponse, getAuthorizationWorkQueue(), PatientTrackHistoryTrack, getPatientTrackHistory() (+27 more)

### Community 10 - "Community 10"
Cohesion: 0.02
Nodes (86): getStaffAccessContextGrant(), clinicianGet(), getInventoryControlledCountSessions(), getPendingInventoryControlledDiscrepancyCorrectionAttestations(), getAddressBook(), getPatientEducationResources(), getBatchCommunicationCampaigns(), ChartTrackerEvent (+78 more)

### Community 12 - "Community 12"
Cohesion: 0.02
Nodes (84): updateStaffAccessContextGrant(), clinicianHeaders(), clinicianPut(), updatePatientEmployer(), updatePatientPortalAccountReset(), unlinkInventoryMedicationLink(), investigateInventoryControlledDiscrepancy(), saveEncounterLayoutForm() (+76 more)

### Community 64 - "Community 64"
Cohesion: 0.13
Nodes (18): PatientPortalHomeAppointmentSummary, PatientPortalAppointmentRequestOptionsResponse, PatientPortalAppointmentsResponse, getPatientPortalAppointmentRequestOptions(), requestPatientPortalAppointment(), PatientPortalAppointmentRequestHistoryEvent, PatientPortalAppointmentRequestHistoryItem, PatientPortalAppointmentsWithRequestHistoryResponse (+10 more)

### Community 63 - "Community 63"
Cohesion: 0.09
Nodes (16): PatientPortalMessageItem, PatientPortalMessagesResponse, getPatientPortalMessages(), PatientPortalMessageComposeOptions, getPatientPortalMessageComposeOptions(), composePatientPortalMessage(), downloadPatientPortalMessageAttachment(), PatientPortalMessageThreadResponse (+8 more)

### Community 18 - "Community 18"
Cohesion: 0.03
Nodes (57): PatientPortalDocumentItem, PatientPortalDocumentsResponse, getPatientPortalDocuments(), downloadPatientPortalDocuments(), PatientPortalLabOrderItem, PatientPortalLabResultsResponse, getPatientPortalLabResults(), PatientPortalClinicalSummaryResponse (+49 more)

### Community 96 - "Community 96"
Cohesion: 0.12
Nodes (8): downloadPatientPortalGeneratedMedicalReportPdf(), AppointmentStatusBadge(), PALETTE, ProviderEntry, isoDate(), WEEKDAYS, ClinicianCalendar(), AsyncState

### Community 28 - "Community 28"
Cohesion: 0.04
Nodes (50): clinicianPost(), InventoryControlledSubstanceCatalogResponse, InventoryControlledCountSession, InventoryControlledCountSessionSummary, InventoryControlledAttestation, getInventoryControlledSubstanceCatalog(), getInventoryControlledCountSession(), createInventoryControlledCountSession() (+42 more)

### Community 113 - "Community 113"
Cohesion: 0.14
Nodes (10): PatientListItem, getAppointmentSchedulingOptions(), AppointmentAvailabilityValidationResponse, createAppointment(), validateAppointmentAvailability(), DURATION_OPTIONS, AppointmentPatient, PatientSearchState (+2 more)

### Community 22 - "Community 22"
Cohesion: 0.03
Nodes (57): PatientMergePreview, PatientCareTeamMember, PatientChartSummary, getPatientChartSummary(), PatientLifecycleHistoryResponse, getPatientLifecycleHistory(), transitionPatientLifecycle(), PatientDeceasedStatusHistoryResponse (+49 more)

### Community 29 - "Community 29"
Cohesion: 0.04
Nodes (39): PatientReferral, PatientReferralWorkflowEvent, ReferralWorkQueueFilters, ReferralWorkQueueResponse, getReferralWorkQueue(), getPatientReferrals(), createPatientReferral(), updatePatientReferralStatus() (+31 more)

### Community 60 - "Community 60"
Cohesion: 0.10
Nodes (19): AppointmentListItem, AppointmentSchedulingOptionsResponse, searchAppointments(), AppointmentUpdateInput, updateAppointment(), AppointmentOccurrenceRescheduleInput, rescheduleAppointmentOccurrence(), restoreAppointmentOccurrence() (+11 more)

### Community 45 - "Community 45"
Cohesion: 0.06
Nodes (26): AppointmentSearchResponse, InventoryLot, InventoryPatientSale, createInventoryPatientSale(), InventoryPatientSaleAllocation, allocateInventoryPatientSale(), InventoryPrescriptionDispense, dispenseInventoryPrescription() (+18 more)

### Community 159 - "Community 159"
Cohesion: 0.27
Nodes (4): updateAppointmentStatus(), FlowBoardItem, FlowBoardResponse, getAppointmentFlowBoard()

### Community 11 - "Community 11"
Cohesion: 0.02
Nodes (77): InventoryMedicationLink, InventoryMedicationCatalogItem, InventoryMedicationLinkHistoryResponse, getInventoryMedicationCatalog(), updateInventoryMedicationLink(), getInventoryMedicationLinkHistory(), InventoryReplenishmentPolicyDefinition, InventoryReplenishmentPolicyChangeRequest (+69 more)

### Community 129 - "Community 129"
Cohesion: 0.15
Nodes (11): InventoryCostPolicyDefinition, InventoryCostPolicyChangeRequest, InventoryCostPolicyChangeRequestDetailResponse, getInventoryCostPolicies(), createInventoryCostPolicyChangeRequest(), getInventoryCostPolicyChangeRequest(), transitionInventoryCostPolicyChangeRequest(), Props (+3 more)

### Community 4 - "Community 4"
Cohesion: 0.02
Nodes (92): EncounterVitals, EncounterSoapNote, EncounterSoapNoteTemplate, updateEncounter(), EncounterAuditHistory, getEncounterAuditHistory(), EncounterLayoutForm, getEncounterLayoutForms() (+84 more)

### Community 78 - "Community 78"
Cohesion: 0.11
Nodes (20): EncounterDetail, getEncounterDetail(), ProcedureOrderItem, EncounterCreateInput, CompleteEncounterCreateInput, EncounterBillingLine, EncounterBillingClaim, EncounterCodingDetail (+12 more)

### Community 58 - "Community 58"
Cohesion: 0.08
Nodes (25): MedicationVocabularyItem, searchClinicalMedicationVocabulary(), ClinicalListAuditHistoryResponse, createProblem(), deactivateProblem(), getProblemAuditHistory(), createAllergy(), deactivateAllergy() (+17 more)

### Community 141 - "Community 141"
Cohesion: 0.20
Nodes (6): PatientMessageItem, PatientMessagesResponse, getPatientMessages(), AsyncState, statusClass(), PatientMessages()

### Community 32 - "Community 32"
Cohesion: 0.04
Nodes (39): StaffMessageInboxResponse, PatientDocumentOcrQueueItem, PatientDocumentOcrQueueResponse, PatientDocumentOcrQueueFilters, PatientDocumentOcrHistoryResponse, PatientDocumentRoutingQueueItem, PatientDocumentRoutingQueueResponse, PatientDocumentRoutingQueueFilters (+31 more)

### Community 65 - "Community 65"
Cohesion: 0.09
Nodes (21): DocumentTemplateItem, DocumentTemplateListResponse, getDocumentTemplates(), createDocumentTemplate(), updateDocumentTemplate(), renderDocumentTemplate(), DocumentTemplateBinaryVersion, DocumentTemplateEvent (+13 more)

### Community 97 - "Community 97"
Cohesion: 0.15
Nodes (14): TherapyGroup, TherapyGroupMember, TherapyGroupSession, TherapyGroupSessionAttendance, getTherapyGroups(), createTherapyGroup(), getTherapyGroupMembers(), addTherapyGroupMember() (+6 more)

### Community 86 - "Community 86"
Cohesion: 0.13
Nodes (18): PatientBillingResponse, getPatientBilling(), postBillingPayment(), createBillingPatientPayment(), createBillingPatientRefund(), createBillingInsurancePayment(), createBillingInsuranceReversal(), createBillingAdjustmentReversal() (+10 more)

### Community 158 - "Community 158"
Cohesion: 0.22
Nodes (8): AuthorizationPolicyGap, AuthorizationPolicyRule, AuthorizationPolicyCatalogResponse, getAuthorizationPolicyCatalog(), AsyncState, gapOptions, formatGap(), AuthorizationPolicyRegistry()

### Community 66 - "Community 66"
Cohesion: 0.08
Nodes (24): PracticeSettingItem, PracticeSettingRegistryItem, getPracticeSettingRegistry(), PracticeSettingDelegation, getPracticeSettingDelegations(), grantPracticeSettingDelegation(), EffectivePracticeSettingItem, getEffectivePracticeSettings() (+16 more)

### Community 106 - "Community 106"
Cohesion: 0.13
Nodes (16): CodingCatalogItem, CodingCatalogChangeRequestStatus, CodingCatalogChangeRequestsResponse, CodingCatalogChangeRequestDetail, CodingCatalogChangeRequestAction, getCodingCatalogChangeRequests(), getCodingCatalogChangeRequest(), createCodingCatalogChangeRequest() (+8 more)

### Community 114 - "Community 114"
Cohesion: 0.13
Nodes (13): PatientPortalProfileDemographics, PatientPortalProfileResponse, getPatientPortalProfile(), PatientPortalProfileChangeInput, submitPatientPortalProfileChange(), ToastItem, Listener, _items (+5 more)

### Community 40 - "Community 40"
Cohesion: 0.09
Nodes (35): AzureDeploymentProfileDocument, AzureDeploymentValidationIssue, AzureDeploymentProfileAssessment, AzureDeploymentProfileSummary, AzureDeploymentProfileDetail, AzureOperationsCapability, AzureAccessValidationResponse, AzureDeploymentExecutionSummary (+27 more)

### Community 17 - "Community 17"
Cohesion: 0.05
Nodes (73): schema, ClinicalFormOption, ClinicalFormOptionListReference, ClinicalFormOptionListCatalogItem, ClinicalFormOptionListCatalog, ClinicalFormCondition, ClinicalFormSectionLocalization, ClinicalFormRuleLocalization (+65 more)

### Community 77 - "Community 77"
Cohesion: 0.13
Nodes (22): ClinicalFormSection, ClinicalFormSchema, field(), schema(), ClinicalFormImpactSeverity, ClinicalFormImpactItem, ClinicalFormChangeImpact, severityRank (+14 more)

### Community 33 - "Community 33"
Cohesion: 0.10
Nodes (37): ClinicalFormField, ClinicalFormCalculation, ClinicalFormRule, ClinicalFormPolicy, ClinicalFormCalculationTemplate, Props, fields, CalculationAuthoringIssue (+29 more)

### Community 126 - "Community 126"
Cohesion: 0.25
Nodes (12): ClinicalFormFieldLocalization, ClinicalFormLocalization, ClinicalFormDefinitionSummary, field(), schema(), flattenFields(), flattenRules(), synchronizeLocalization() (+4 more)

### Community 81 - "Community 81"
Cohesion: 0.17
Nodes (17): EncounterLifecycleDetail, EncounterSignInput, EncounterLifecycleConflictError, clinicianHeaders(), lifecycleFetch(), requireArchiveVersion(), signEncounterUnderLocalPolicy(), changeEncounterArchiveState() (+9 more)

### Community 112 - "Community 112"
Cohesion: 0.18
Nodes (14): response, ExperienceBaselineCounts, ExperienceRole, ExperienceEnvironment, ExperienceTask, ExperienceCriterion, ExperienceAnalyticsEvent, ExperienceGap (+6 more)

### Community 41 - "Community 41"
Cohesion: 0.08
Nodes (36): item, ManagedRecordPolicy, ManagedRecordItem, ManagedRecordList, ManagedRecordEvent, ManagedRecordHistory, ManagedRecordCreateInput, headers() (+28 more)

### Community 49 - "Community 49"
Cohesion: 0.13
Nodes (28): PatientDisclosureOption, PatientDisclosureScopeOption, PatientDisclosurePolicy, PatientDisclosureAuthority, PatientDisclosureAuthorityEvent, PatientDisclosureRequest, PatientDisclosureRequestEvent, PatientDisclosureAuthorityInput (+20 more)

### Community 20 - "Community 20"
Cohesion: 0.06
Nodes (61): ReportMetricDefinition, ReportParameterDefinition, ReportSourceDatasetDefinition, ReportOutputFieldDefinition, ReportValidationFixture, GovernedReportFamily, ReportDefinitionGovernancePolicy, GovernedReportDefinitionInput (+53 more)

### Community 149 - "Community 149"
Cohesion: 0.22
Nodes (5): Props, State, createErrorReference(), AppErrorBoundary, Component

### Community 160 - "Community 160"
Cohesion: 0.31
Nodes (8): AppointmentSemanticStatus, AppointmentStatusDefinition, APPOINTMENT_STATUSES, allowedTransitions, unknownStatus, getAppointmentStatus(), getAppointmentStatusOptions(), isCancelledAppointment()

### Community 95 - "Community 95"
Cohesion: 0.15
Nodes (8): lifecycleDomains, LifecycleDomain, FixtureId, FixtureCleanup, FixtureReset, LifecycleRecord, CreateOptions, LifecycleFixtureRegistry

### Community 105 - "Community 105"
Cohesion: 0.12
Nodes (16): Microsoft.NET.Sdk.Web, net10.0, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Microsoft.IdentityModel.Protocols.OpenIdConnect, Firely.Fhir.Validation.R4, Hl7.Fhir.R4, Hl7.Fhir.Specification.Data.R4 (+8 more)

### Community 317 - "Community 317"
Cohesion: 1.00
Nodes (1): AzureOperationsOptions

### Community 318 - "Community 318"
Cohesion: 1.00
Nodes (1): DatabaseConnectionOptions

### Community 52 - "Community 52"
Cohesion: 0.08
Nodes (16): IdentityProviderOptions, DatabaseBootstrapCatalog, SchemaMigrationFaultInjectionException, SchemaMigrationState, Valid(), Invalid(), vitals, Get-QueryValues() (+8 more)

### Community 140 - "Community 140"
Cohesion: 0.21
Nodes (4): RuntimeSafetyOptions, RuntimeSafetyPolicyTests, 7be4153 feat(runtime): fail closed for production hosting, 7f7a66f fix(billing): isolate generated financial fixtures

### Community 171 - "Community 171"
Cohesion: 0.29
Nodes (1): RuntimeSafetyPolicy

### Community 165 - "Community 165"
Cohesion: 0.36
Nodes (1): AddressBookRepository

### Community 99 - "Community 99"
Cohesion: 0.21
Nodes (1): AdministrationDirectoryRepository

### Community 7 - "Community 7"
Cohesion: 0.05
Nodes (86): configuration_package_events, 0316b13 feat(procedures): protect locked encounter order entry, 05ca2d7 feat(encounters): type locked track catalog, 07ec116 feat(admin): govern facility settings, 0fb4655 feat(labs): govern specimen lifecycle, 10b1502 feat(ui): add referral work queue, 10f5fc4 feat(billing): protect locked charge mutations, 1cc9676 feat(admin): resolve facility settings (+78 more)

### Community 30 - "Community 30"
Cohesion: 0.07
Nodes (1): AdministrationRepository

### Community 1 - "Community 1"
Cohesion: 0.04
Nodes (135): 0220d35 fix(auth): scope scheduling and encounter workflows to facility, 0239c9f fix(labs): invalidate stale critical result queue, 05beda1 refactor(api): isolate encounter endpoints, 05fa4fb fix(patients): make administration updates atomic, 07e8fdd fix(sdoh): anchor generated goals to assessment date, 083e6b1 fix(scheduling): enforce appointment concurrency, 088b2d8 fix(auth): revoke disabled account sessions, 0905df1 test(insurance): select fixture facility context (+127 more)

### Community 14 - "Community 14"
Cohesion: 0.07
Nodes (1): AppointmentRepository

### Community 101 - "Community 101"
Cohesion: 0.21
Nodes (2): AuthRepository, ToResponse()

### Community 67 - "Community 67"
Cohesion: 0.21
Nodes (1): AuthorizationRepository

### Community 143 - "Community 143"
Cohesion: 0.24
Nodes (1): AzureOperationsAccessRepository

### Community 42 - "Community 42"
Cohesion: 0.06
Nodes (31): AzureDeploymentProfileValidationException, Exception, DocumentVersionConflictException, DocumentReviewConflictException, DocumentArchiveConflictException, DocumentRoutingConflictException, DocumentOcrConflictException, ManagedRecordConflictException (+23 more)

### Community 72 - "Community 72"
Cohesion: 0.15
Nodes (1): AzureOperationsRepository

### Community 144 - "Community 144"
Cohesion: 0.33
Nodes (1): BatchCommunicationRepository

### Community 8 - "Community 8"
Cohesion: 0.06
Nodes (1): BillingRepository

### Community 180 - "Community 180"
Cohesion: 0.48
Nodes (1): ChartTrackerRepository

### Community 150 - "Community 150"
Cohesion: 0.44
Nodes (1): ClinicalAlertEvaluationRepository

### Community 27 - "Community 27"
Cohesion: 0.09
Nodes (1): ClinicalFormRepository

### Community 31 - "Community 31"
Cohesion: 0.09
Nodes (1): ClinicalFormRuntime

### Community 37 - "Community 37"
Cohesion: 0.06
Nodes (24): PrescriptionContinuationBlockedException, InvalidOperationException, AppointmentAvailabilityConflictException, PatientDisclosureConcurrencyException, PatientAdministrationVersionConflictException, ClinicalListAuditEventConfiguration, ClinicalListAuditEventEntity, patient_lifecycle_events (+16 more)

### Community 38 - "Community 38"
Cohesion: 0.15
Nodes (1): ClinicalListRepository

### Community 3 - "Community 3"
Cohesion: 0.02
Nodes (66): AvenChartDbContext, DbContext, AccessGroupEntity, AccessGroupPermissionEntity, AccessPermissionEntity, AccessUserMembershipEntity, AddressBookContactEntity, AllergyEntity (+58 more)

### Community 73 - "Community 73"
Cohesion: 0.28
Nodes (1): ClinicalListStateRepository

### Community 9 - "Community 9"
Cohesion: 0.07
Nodes (1): DocumentRepository

### Community 83 - "Community 83"
Cohesion: 0.16
Nodes (1): DocumentTemplateRepository

### Community 151 - "Community 151"
Cohesion: 0.36
Nodes (1): EncounterLayoutFormRepository

### Community 59 - "Community 59"
Cohesion: 0.10
Nodes (14): EncounterAuditEventConfiguration, EncounterConfiguration, EncounterAuditEventEntity, EncounterEntity, VitalEntity, clinical_notes, 0a3a419 Split encounter state mutations into EF Core, 21f29da fix(encounters): reject stale summary updates (+6 more)

### Community 23 - "Community 23"
Cohesion: 0.09
Nodes (2): EncounterRepository, DiagnosisAccumulator

### Community 124 - "Community 124"
Cohesion: 0.32
Nodes (1): EncounterStateRepository

### Community 152 - "Community 152"
Cohesion: 0.40
Nodes (1): ExternalIdentityMappingRepository

### Community 43 - "Community 43"
Cohesion: 0.15
Nodes (23): ExternalLaboratoryIntakeRepository, Matches(), ExternalLaboratoryFhirValidationException, ArgumentException, Parse(), ParseObservation(), ReadObservationValue(), ReadReferenceRange() (+15 more)

### Community 70 - "Community 70"
Cohesion: 0.11
Nodes (7): FhirResults, 06cf8a3 feat(fhir): validate external laboratory R4 profiles, 2a53ba9 feat(labs): scope external sources to facilities, 32d97a0 feat(labs): ingest profiled FHIR laboratory results, a9eec9f fix(fhir): make search contract pageable and typed, bc6cc4d feat(labs): govern external laboratory source credentials, e474fa1 fix(fhir): omit empty optional repeat fields

### Community 108 - "Community 108"
Cohesion: 0.28
Nodes (1): ExternalLaboratorySourceRepository

### Community 74 - "Community 74"
Cohesion: 0.23
Nodes (1): FhirRepository

### Community 209 - "Community 209"
Cohesion: 0.70
Nodes (1): FlowBoardRepository

### Community 87 - "Community 87"
Cohesion: 0.13
Nodes (10): LocalDeterministicIntegrationTransport, IIntegrationTransport, integration_outbox_events, integration_outbox, integration_inbox_events, integration_inbox, 2b91a4a feat(integrations): govern outbox recovery, 66cc16f feat(integrations): govern inbox reconciliation (+2 more)

### Community 68 - "Community 68"
Cohesion: 0.18
Nodes (1): IntegrationIdempotencyConflictException

### Community 109 - "Community 109"
Cohesion: 0.27
Nodes (1): InventoryAccountingIntegrationRepository

### Community 102 - "Community 102"
Cohesion: 0.25
Nodes (1): InventoryCostPolicyRepository

### Community 89 - "Community 89"
Cohesion: 0.22
Nodes (1): InventoryReplenishmentPolicyRepository

### Community 98 - "Community 98"
Cohesion: 0.18
Nodes (15): inventory_costing_exceptions, inventory_transactions, inventory_lots, 2e2dbd9 feat(inventory): support specific identification costing, 6066464 feat(inventory): restore linked cost layers, 6268ce2 feat(inventory): support specific transfer costing, 69844bd feat(inventory): reallocate FIFO transfer costs, 7002695 feat(inventory): reallocate weighted transfer costs (+7 more)

### Community 47 - "Community 47"
Cohesion: 0.09
Nodes (2): InventoryRepository, InventoryItemBuilder

### Community 116 - "Community 116"
Cohesion: 0.14
Nodes (1): ToInventoryLot()

### Community 127 - "Community 127"
Cohesion: 0.27
Nodes (1): InventoryValuationRepository

### Community 145 - "Community 145"
Cohesion: 0.38
Nodes (1): LegacyClinicalFormDisplayRepository

### Community 50 - "Community 50"
Cohesion: 0.15
Nodes (1): ManagedRecordRepository

### Community 44 - "Community 44"
Cohesion: 0.13
Nodes (1): MessageRepository

### Community 172 - "Community 172"
Cohesion: 0.43
Nodes (1): OfficeNoteRepository

### Community 69 - "Community 69"
Cohesion: 0.22
Nodes (1): PatientDisclosureRepository

### Community 240 - "Community 240"
Cohesion: 0.83
Nodes (1): PatientMergeAuditRepository

### Community 84 - "Community 84"
Cohesion: 0.22
Nodes (1): PatientMergeExecutionRepository

### Community 153 - "Community 153"
Cohesion: 0.40
Nodes (1): PatientPortalExternalIdentityMappingRepository

### Community 26 - "Community 26"
Cohesion: 0.05
Nodes (1): PatientPortalRepository

### Community 117 - "Community 117"
Cohesion: 0.14
Nodes (1): ToResponse()

### Community 154 - "Community 154"
Cohesion: 0.51
Nodes (1): PatientPrintRepository

### Community 188 - "Community 188"
Cohesion: 0.67
Nodes (1): PatientRecordRequestRepository

### Community 13 - "Community 13"
Cohesion: 0.06
Nodes (1): PatientRepository

### Community 134 - "Community 134"
Cohesion: 0.35
Nodes (1): PatientSdohRepository

### Community 155 - "Community 155"
Cohesion: 0.44
Nodes (1): PatientXmlExchangeRepository

### Community 210 - "Community 210"
Cohesion: 0.60
Nodes (1): PhiAuditRepository

### Community 121 - "Community 121"
Cohesion: 0.13
Nodes (6): PhiAuditResourceContext, PhiAuditedResult, IResult, PhiAuditResourceContextTests, 5178d56 fix(reports): pin queued execution source snapshots, f95ef06 feat(audit): correlate direct PHI access resources

### Community 104 - "Community 104"
Cohesion: 0.26
Nodes (1): ProcedureDirectoryRepository

### Community 15 - "Community 15"
Cohesion: 0.07
Nodes (1): ProcedureRepository

### Community 91 - "Community 91"
Cohesion: 0.13
Nodes (11): RecallConfiguration, RecallLifecycleEventConfiguration, RecallEntity, RecallLifecycleEventEntity, recall_lifecycle_events, recalls, lab_reports, lab_specimens (+3 more)

### Community 174 - "Community 174"
Cohesion: 0.39
Nodes (1): RecallRepository

### Community 80 - "Community 80"
Cohesion: 0.24
Nodes (1): ReferralRepository

### Community 56 - "Community 56"
Cohesion: 0.15
Nodes (1): ReportDefinitionRepository

### Community 85 - "Community 85"
Cohesion: 0.20
Nodes (2): ReportExecutionQueueRepository, WorkerCancellationState

### Community 36 - "Community 36"
Cohesion: 0.11
Nodes (1): ReportExecutionRepository

### Community 53 - "Community 53"
Cohesion: 0.12
Nodes (1): ReportRepository

### Community 118 - "Community 118"
Cohesion: 0.25
Nodes (1): TherapyGroupRepository

### Community 131 - "Community 131"
Cohesion: 0.16
Nodes (6): PatientReadingAccumulator, Invoke-JsonRequest(), Get-HttpStatus(), Invoke-StatusRequest(), Get-EncounterDetail(), 1764980 feat(encounters): show locked track state

### Community 75 - "Community 75"
Cohesion: 0.14
Nodes (3): TrackAnythingRepository, PatientTrackAccumulator, PatientEncounterAccumulator

### Community 261 - "Community 261"
Cohesion: 0.67
Nodes (1): AdministrationEndpoints

### Community 262 - "Community 262"
Cohesion: 0.67
Nodes (1): AdministrativeReferenceEndpoints

### Community 263 - "Community 263"
Cohesion: 0.67
Nodes (1): AppointmentEndpoints

### Community 135 - "Community 135"
Cohesion: 0.36
Nodes (1): AvenChartOpenApi

### Community 76 - "Community 76"
Cohesion: 0.16
Nodes (1): AzureDeploymentProfilePolicy

### Community 94 - "Community 94"
Cohesion: 0.19
Nodes (6): AzureOperationsAccessService, AzureOperationsAccessFilter, IEndpointFilter, AzureOperationsEnabledFilter, AzureOperationsAccessLockedException, UnauthorizedAccessException

### Community 189 - "Community 189"
Cohesion: 0.67
Nodes (1): AzureOperationsEndpoints

### Community 35 - "Community 35"
Cohesion: 0.08
Nodes (6): AzureCliRunner, AzureOperationsService, AzureDeploymentCoordinator, IHostedService, IDisposable, TemporaryParameterFile

### Community 264 - "Community 264"
Cohesion: 0.67
Nodes (1): BillingEndpoints

### Community 265 - "Community 265"
Cohesion: 0.67
Nodes (1): ClinicalFormEndpoints

### Community 266 - "Community 266"
Cohesion: 0.67
Nodes (1): ClinicalListEndpoints

### Community 267 - "Community 267"
Cohesion: 0.67
Nodes (1): ClinicalWorkflowEndpoints

### Community 268 - "Community 268"
Cohesion: 0.67
Nodes (1): ConfigurationEndpoints

### Community 161 - "Community 161"
Cohesion: 0.38
Nodes (1): DatabaseSchemaMigrator

### Community 211 - "Community 211"
Cohesion: 0.60
Nodes (1): DevelopmentTestIdentityProviderEndpoints

### Community 269 - "Community 269"
Cohesion: 0.67
Nodes (1): DocumentEndpoints

### Community 270 - "Community 270"
Cohesion: 0.67
Nodes (1): DocumentTemplateEndpoints

### Community 271 - "Community 271"
Cohesion: 0.67
Nodes (1): EncounterEndpoints

### Community 146 - "Community 146"
Cohesion: 0.30
Nodes (1): EndpointAccessPolicies

### Community 241 - "Community 241"
Cohesion: 0.67
Nodes (1): ExternalLaboratoryFhirIntakeEndpoints

### Community 212 - "Community 212"
Cohesion: 0.60
Nodes (1): FhirR4Endpoints

### Community 213 - "Community 213"
Cohesion: 0.80
Nodes (1): FhirR4ValidationService

### Community 242 - "Community 242"
Cohesion: 0.67
Nodes (1): IntegrationEndpoints

### Community 272 - "Community 272"
Cohesion: 0.67
Nodes (1): InventoryEndpoints

### Community 273 - "Community 273"
Cohesion: 0.67
Nodes (1): ManagedRecordEndpoints

### Community 274 - "Community 274"
Cohesion: 0.67
Nodes (1): MessageEndpoints

### Community 275 - "Community 275"
Cohesion: 0.67
Nodes (1): OfficeNoteEndpoints

### Community 214 - "Community 214"
Cohesion: 0.60
Nodes (1): PatientEndpoints

### Community 276 - "Community 276"
Cohesion: 0.67
Nodes (1): PatientEngagementEndpoints

### Community 277 - "Community 277"
Cohesion: 0.67
Nodes (1): PatientPortalEndpoints

### Community 182 - "Community 182"
Cohesion: 0.29
Nodes (3): PostgresReadinessHealthCheck, IHealthCheck, SchemaMigrationReadinessHealthCheck

### Community 278 - "Community 278"
Cohesion: 0.67
Nodes (1): ProcedureEndpoints

### Community 279 - "Community 279"
Cohesion: 0.67
Nodes (1): ReportEndpoints

### Community 215 - "Community 215"
Cohesion: 0.50
Nodes (1): SchemaMigrationCatalog

### Community 280 - "Community 280"
Cohesion: 0.67
Nodes (1): StaffAuthenticationEndpoints

### Community 281 - "Community 281"
Cohesion: 0.67
Nodes (1): TherapyGroupEndpoints

### Community 5 - "Community 5"
Cohesion: 0.02
Nodes (39): AccessGroupConfiguration, IEntityTypeConfiguration, AccessGroupPermissionConfiguration, AccessPermissionConfiguration, AccessUserMembershipConfiguration, AddressBookContactConfiguration, AllergyConfiguration, AuthAccountConfiguration (+31 more)

### Community 235 - "Community 235"
Cohesion: 0.50
Nodes (1): AuthorizationPolicyCatalog

### Community 55 - "Community 55"
Cohesion: 0.15
Nodes (1): BrowserOidcSessionService

### Community 162 - "Community 162"
Cohesion: 0.33
Nodes (4): OidcStaffIdentityAdapter, IStaffIdentityAdapter, TestOidcStaffIdentityAdapter, OidcIdentityAdapterHelpers

### Community 207 - "Community 207"
Cohesion: 0.53
Nodes (1): TestIdentityProviderService

### Community 147 - "Community 147"
Cohesion: 0.24
Nodes (5): IPatientPortalIdentityAdapter, LocalPatientPortalIdentityAdapter, OidcPatientPortalIdentityAdapter, TestOidcPatientPortalIdentityAdapter, PatientPortalIdentityAdapterHelpers

### Community 61 - "Community 61"
Cohesion: 0.14
Nodes (3): StaffAccessContextService, Allowed(), Denied()

### Community 178 - "Community 178"
Cohesion: 0.46
Nodes (1): ClinicalWorkflowPolicyCatalog

### Community 332 - "Community 332"
Cohesion: 1.00
Nodes (1): AvenChart.Api.csproj

### Community 179 - "Community 179"
Cohesion: 0.57
Nodes (1): DatabaseBootstrapCatalogTests

### Community 157 - "Community 157"
Cohesion: 0.31
Nodes (1): FhirR4ValidationServiceTests

### Community 236 - "Community 236"
Cohesion: 0.50
Nodes (1): StaffAccessContextServiceTests

### Community 46 - "Community 46"
Cohesion: 0.08
Nodes (35): dataset_metadata, practice_settings, coding_catalogs, coding_catalog_audit_events, form_layouts, form_option_lists, form_option_values, clinical_alert_rules (+27 more)

### Community 128 - "Community 128"
Cohesion: 0.22
Nodes (14): facilities, staff, auth_accounts, auth_sessions, patient_related_contacts, patient_care_teams, patient_care_team_members, appointments (+6 more)

### Community 237 - "Community 237"
Cohesion: 0.67
Nodes (4): access_groups, access_permissions, access_group_permissions, access_user_memberships

### Community 90 - "Community 90"
Cohesion: 0.13
Nodes (20): patients, patient_record_requests, patient_sdoh_assessments, patient_portal_accounts, patient_portal_sessions, patient_portal_profile_change_requests, patient_portal_report_audit_events, patient_portal_message_audit_events (+12 more)

### Community 238 - "Community 238"
Cohesion: 0.50
Nodes (4): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_requests, patient_disclosure_request_events

### Community 316 - "Community 316"
Cohesion: 1.00
Nodes (2): pharmacies, prescriptions

### Community 208 - "Community 208"
Cohesion: 0.40
Nodes (5): inventory_items, inventory_lots, inventory_vendors, inventory_purchase_receipts, inventory_transactions

### Community 170 - "Community 170"
Cohesion: 0.25
Nodes (8): lab_orders, lab_reports, lab_report_review_events, lab_specimens, lab_results, critical_lab_result_acknowledgements, critical_lab_result_acknowledgement_events, procedure_result_versions

### Community 319 - "Community 319"
Cohesion: 1.00
Nodes (1): schema_migrations

### Community 320 - "Community 320"
Cohesion: 1.00
Nodes (1): statement_email_outbox

### Community 282 - "Community 282"
Cohesion: 0.67
Nodes (2): integration_outbox, integration_inbox

### Community 321 - "Community 321"
Cohesion: 1.00
Nodes (1): phi_access_audit_events

### Community 216 - "Community 216"
Cohesion: 0.70
Nodes (4): inventory_items, inventory_lots, facilities, inventory_transactions

### Community 322 - "Community 322"
Cohesion: 1.00
Nodes (1): encounter_audit_events

### Community 283 - "Community 283"
Cohesion: 0.67
Nodes (2): practice_settings, practice_setting_audit_events

### Community 284 - "Community 284"
Cohesion: 1.00
Nodes (2): coding_catalog_audit_events, coding_catalogs

### Community 243 - "Community 243"
Cohesion: 0.83
Nodes (3): form_layouts, form_layout_groups, form_layout_fields

### Community 323 - "Community 323"
Cohesion: 1.00
Nodes (1): clinical_alert_rules

### Community 324 - "Community 324"
Cohesion: 1.00
Nodes (1): module_catalog

### Community 325 - "Community 325"
Cohesion: 1.00
Nodes (1): api_client_registry

### Community 285 - "Community 285"
Cohesion: 1.00
Nodes (2): form_option_lists, form_option_values

### Community 244 - "Community 244"
Cohesion: 0.83
Nodes (3): encounter_layout_form_records, form_layouts, encounter_layout_form_values

### Community 286 - "Community 286"
Cohesion: 1.00
Nodes (2): encounter_clinical_alert_acknowledgments, clinical_alert_rules

### Community 217 - "Community 217"
Cohesion: 0.70
Nodes (4): patient_merge_audit_plans, patient_merge_executions, patients, patient_merge_execution_manifest_rows

### Community 287 - "Community 287"
Cohesion: 1.00
Nodes (2): patient_record_requests, patients

### Community 288 - "Community 288"
Cohesion: 1.00
Nodes (2): patient_sdoh_assessments, patients

### Community 326 - "Community 326"
Cohesion: 1.00
Nodes (1): office_notes

### Community 327 - "Community 327"
Cohesion: 1.00
Nodes (1): address_book_contacts

### Community 328 - "Community 328"
Cohesion: 2.00
Nodes (1): track_anything_types

### Community 329 - "Community 329"
Cohesion: 1.00
Nodes (1): patient_education_resources

### Community 218 - "Community 218"
Cohesion: 0.70
Nodes (4): recalls, patients, staff, facilities

### Community 289 - "Community 289"
Cohesion: 1.00
Nodes (2): recall_activity, recalls

### Community 245 - "Community 245"
Cohesion: 0.83
Nodes (3): batch_communication_campaigns, batch_communication_recipients, patients

### Community 219 - "Community 219"
Cohesion: 0.70
Nodes (4): chart_tracker_locations, chart_tracker_events, patients, staff

### Community 330 - "Community 330"
Cohesion: 1.00
Nodes (1): document_templates

### Community 290 - "Community 290"
Cohesion: 1.00
Nodes (2): patient_duplicate_review_dispositions, patients

### Community 291 - "Community 291"
Cohesion: 1.00
Nodes (2): document_template_binary_versions, document_templates

### Community 292 - "Community 292"
Cohesion: 1.00
Nodes (2): patient_xml_exchange_audits, patients

### Community 246 - "Community 246"
Cohesion: 0.83
Nodes (3): inventory_vendors, inventory_purchase_receipts, facilities

### Community 293 - "Community 293"
Cohesion: 1.00
Nodes (2): inventory_count_reconciliations, inventory_lots

### Community 190 - "Community 190"
Cohesion: 0.67
Nodes (5): encounter_track_records, encounters, track_anything_types, encounter_track_readings, encounter_track_reading_values

### Community 294 - "Community 294"
Cohesion: 1.33
Nodes (2): practice_setting_revisions, practice_settings

### Community 295 - "Community 295"
Cohesion: 1.33
Nodes (2): coding_catalog_revisions, coding_catalogs

### Community 296 - "Community 296"
Cohesion: 1.33
Nodes (2): form_option_list_revisions, form_option_lists

### Community 297 - "Community 297"
Cohesion: 1.33
Nodes (2): form_layout_revisions, form_layouts

### Community 298 - "Community 298"
Cohesion: 1.33
Nodes (2): clinical_alert_rule_revisions, clinical_alert_rules

### Community 299 - "Community 299"
Cohesion: 1.33
Nodes (2): module_catalog_revisions, module_catalog

### Community 300 - "Community 300"
Cohesion: 1.33
Nodes (2): api_client_registry_revisions, api_client_registry

### Community 301 - "Community 301"
Cohesion: 1.00
Nodes (2): inventory_lot_metadata_audits, inventory_lots

### Community 302 - "Community 302"
Cohesion: 1.00
Nodes (2): inventory_lot_destructions, inventory_lots

### Community 191 - "Community 191"
Cohesion: 0.60
Nodes (5): inventory_patient_sales, inventory_lots, patients, encounters, inventory_transactions

### Community 220 - "Community 220"
Cohesion: 0.70
Nodes (4): inventory_patient_sale_batches, inventory_items, patients, encounters

### Community 221 - "Community 221"
Cohesion: 0.80
Nodes (4): inventory_item_medication_links, inventory_items, medication_vocabulary, inventory_item_medication_link_audits

### Community 183 - "Community 183"
Cohesion: 0.52
Nodes (6): inventory_purchase_requisitions, facilities, inventory_vendors, inventory_purchase_requisition_lines, inventory_items, inventory_purchase_requisition_events

### Community 222 - "Community 222"
Cohesion: 0.70
Nodes (4): inventory_purchase_requisition_receipts, inventory_purchase_requisitions, inventory_purchase_requisition_lines, inventory_purchase_receipts

### Community 223 - "Community 223"
Cohesion: 0.70
Nodes (4): inventory_lot_expiry_dispositions, inventory_lots, inventory_transactions, inventory_lot_destructions

### Community 247 - "Community 247"
Cohesion: 0.83
Nodes (3): practice_setting_change_requests, practice_settings, practice_setting_change_request_events

### Community 192 - "Community 192"
Cohesion: 0.53
Nodes (5): inventory_controlled_locations, facilities, inventory_controlled_location_events, inventory_controlled_item_classification_events, inventory_items

### Community 193 - "Community 193"
Cohesion: 0.67
Nodes (5): inventory_controlled_custody_events, inventory_lots, inventory_controlled_locations, patients, encounters

### Community 184 - "Community 184"
Cohesion: 0.57
Nodes (6): inventory_controlled_count_sessions, inventory_controlled_locations, inventory_controlled_count_lines, inventory_lots, inventory_controlled_count_discrepancies, inventory_controlled_custody_events

### Community 303 - "Community 303"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_runs, inventory_controlled_locations

### Community 304 - "Community 304"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_exports, inventory_controlled_report_runs

### Community 248 - "Community 248"
Cohesion: 0.83
Nodes (3): document_template_events, document_templates, document_template_binary_versions

### Community 224 - "Community 224"
Cohesion: 0.70
Nodes (4): referrals, patients, authorizations, clinical_workflow_events

### Community 305 - "Community 305"
Cohesion: 1.00
Nodes (2): coding_catalog_change_requests, coding_catalog_change_request_events

### Community 306 - "Community 306"
Cohesion: 1.00
Nodes (2): form_layout_change_requests, form_layout_change_request_events

### Community 307 - "Community 307"
Cohesion: 1.00
Nodes (2): form_option_list_change_requests, form_option_list_change_request_events

### Community 308 - "Community 308"
Cohesion: 1.00
Nodes (2): clinical_alert_rule_change_requests, clinical_alert_rule_change_request_events

### Community 309 - "Community 309"
Cohesion: 1.00
Nodes (2): module_change_requests, module_change_request_events

### Community 310 - "Community 310"
Cohesion: 1.00
Nodes (2): api_client_change_requests, api_client_change_request_events

### Community 249 - "Community 249"
Cohesion: 0.83
Nodes (3): inventory_cost_policies, inventory_cost_policy_change_requests, inventory_cost_policy_change_request_events

### Community 194 - "Community 194"
Cohesion: 0.67
Nodes (5): patient_disclosure_authorities, patients, patient_disclosure_authority_events, patient_disclosure_requests, patient_disclosure_request_events

### Community 168 - "Community 168"
Cohesion: 0.47
Nodes (8): inventory_cost_layers, inventory_transactions, inventory_purchase_receipts, inventory_lots, inventory_items, facilities, inventory_cost_policies, inventory_cost_layer_applications

### Community 175 - "Community 175"
Cohesion: 0.50
Nodes (7): inventory_valuation_runs, facilities, inventory_cost_policies, inventory_valuation_run_lines, inventory_cost_layers, inventory_lots, inventory_items

### Community 185 - "Community 185"
Cohesion: 0.62
Nodes (6): inventory_replenishment_policies, inventory_items, facilities, inventory_vendors, inventory_replenishment_policy_change_requests, inventory_replenishment_policy_change_request_events

### Community 225 - "Community 225"
Cohesion: 0.90
Nodes (4): saved_report_definitions, saved_report_runs, saved_report_definition_revisions, saved_report_definition_events

### Community 250 - "Community 250"
Cohesion: 0.83
Nodes (3): inventory_accounting_integration_decisions, inventory_accounting_integration_change_requests, inventory_accounting_integration_change_request_events

### Community 176 - "Community 176"
Cohesion: 0.57
Nodes (7): clinical_form_definitions, clinical_form_revisions, clinical_form_definition_events, clinical_form_instances, patients, clinical_form_signatures, clinical_form_instance_events

### Community 148 - "Community 148"
Cohesion: 0.18
Nodes (4): 18dd71c feat(forms): adopt legacy speech dictation, 3fbf1fd feat(forms): adopt legacy phq9 screening, 7ad81c7 feat(forms): adopt legacy gad7 screening, aebec7a feat(forms): adopt legacy transfer summary

### Community 130 - "Community 130"
Cohesion: 0.14
Nodes (5): 10b94e5 feat(forms): adopt legacy ankle assessment, 21a1e03 feat(forms): adopt legacy prior authorization, 9d6a4b8 feat(forms): adopt legacy treatment plan, d0ab407 feat(forms): adopt legacy work school note, da2d960 feat(forms): adopt legacy physical exam lines

### Community 251 - "Community 251"
Cohesion: 0.83
Nodes (3): practice_setting_facility_overrides, practice_settings, facilities

### Community 252 - "Community 252"
Cohesion: 0.83
Nodes (3): practice_setting_facility_override_revisions, practice_setting_facility_overrides, practice_setting_change_requests

### Community 195 - "Community 195"
Cohesion: 0.60
Nodes (5): practice_setting_delegations, auth_accounts, practice_settings, facilities, practice_setting_delegation_events

### Community 311 - "Community 311"
Cohesion: 1.00
Nodes (2): configuration_package_import_requests, configuration_package_import_request_events

### Community 122 - "Community 122"
Cohesion: 0.18
Nodes (9): 03db92b fix(forms): validate legacy ROS compatibility sections, 193f79b feat(forms): adopt legacy ROS breast pulmonary section, 22c8408 feat(forms): adopt legacy ROS cardiovascular section, 2f3adbd feat(forms): adopt legacy ROS eyes section, 3f1b80a feat(forms): adopt legacy ROS general section, 5545c62 feat(forms): adopt legacy ROS ear nose throat section, 683c9ca feat(forms): adopt legacy ROS gastrointestinal section, 7988299 feat(forms): adopt legacy ROS urinary section (+1 more)

### Community 253 - "Community 253"
Cohesion: 0.83
Nodes (3): legacy_clinical_form_snapshots, patients, encounters

### Community 312 - "Community 312"
Cohesion: 1.00
Nodes (2): clinical_form_migration_manifest_events, clinical_form_migration_manifests

### Community 254 - "Community 254"
Cohesion: 0.83
Nodes (3): message_assignment_events, messages, patients

### Community 255 - "Community 255"
Cohesion: 0.83
Nodes (3): staff_message_attachments, messages, patients

### Community 256 - "Community 256"
Cohesion: 0.83
Nodes (3): message_correction_events, messages, patients

### Community 257 - "Community 257"
Cohesion: 0.83
Nodes (3): message_retention_events, messages, patients

### Community 313 - "Community 313"
Cohesion: 1.00
Nodes (2): message_escalation_events, messages

### Community 120 - "Community 120"
Cohesion: 0.24
Nodes (16): patient_portal_appointment_requests, appointments, patient_portal_appointment_request_events, trg_capture_patient_portal_appointment_request, capture_patient_portal_appointment_request(), new.appointment_date, new.start_time, new.duration_minutes (+8 more)

### Community 258 - "Community 258"
Cohesion: 0.83
Nodes (3): critical_lab_result_acknowledgements, lab_results, critical_lab_result_acknowledgement_events

### Community 169 - "Community 169"
Cohesion: 0.50
Nodes (8): therapy_groups, staff, therapy_group_members, patients, therapy_group_sessions, therapy_group_session_participants, therapy_group_session_encounters, therapy_group_session_attendance

### Community 314 - "Community 314"
Cohesion: 1.00
Nodes (2): procedure_specimen_events, lab_specimens

### Community 196 - "Community 196"
Cohesion: 0.33
Nodes (5): operations.operator_credentials, operations.sessions, operations.audit_events, operations.usage_events, operations.runtime_state

### Community 226 - "Community 226"
Cohesion: 0.70
Nodes (4): azure_deployment_profiles, azure_deployment_profile_revisions, azure_deployment_executions, azure_deployment_execution_events

### Community 197 - "Community 197"
Cohesion: 0.47
Nodes (5): azure_operations_access_config, azure_operations_access_grants, auth_sessions, azure_operations_unlock_attempts, azure_operations_access_audit

### Community 51 - "Community 51"
Cohesion: 0.10
Nodes (33): appointment_reminder_dispatch_audit, statement_delivery_audit_events, medication_vocabulary, prescription_audit_events, prescription_refill_request_lifecycle, patient_provider_assignment_events, patient_administration_audit_events, patient_portal_message_attachments (+25 more)

### Community 315 - "Community 315"
Cohesion: 1.00
Nodes (2): patient_registration_duplicate_reviews, patients

### Community 331 - "Community 331"
Cohesion: 1.00
Nodes (1): inventory_controlled_action_attestations

### Community 163 - "Community 163"
Cohesion: 0.33
Nodes (9): patient_allergy_review_states, trg_patients_initialize_allergy_review_state, patients, trg_allergies_advance_review_state, allergies, avenchart_initialize_allergy_review_state(), avenchart_advance_allergy_review_state(), can (+1 more)

### Community 198 - "Community 198"
Cohesion: 0.67
Nodes (5): auth_principal_facility_grants, auth_accounts, facilities, auth_principal_purpose_of_use_grants, auth_access_context_grant_events

### Community 136 - "Community 136"
Cohesion: 0.22
Nodes (13): trg_allergies_require_active_patient_for_new_content, allergies, trg_problems_require_active_patient_for_new_content, problems, trg_medications_require_active_patient_for_new_content, medications, trg_immunizations_require_active_patient_for_new_content, immunizations (+5 more)

### Community 227 - "Community 227"
Cohesion: 0.60
Nodes (3): clinical_list_audit_events, patients, trg_clinical_list_audit_events_immutable

### Community 199 - "Community 199"
Cohesion: 0.47
Nodes (4): trg_prescription_audit_events_immutable, prescription_audit_events, trg_prescriptions_retained, prescriptions

### Community 200 - "Community 200"
Cohesion: 0.53
Nodes (5): trg_prescriptions_require_active_patient_for_continuation, prescriptions, avenchart_require_active_patient_for_prescription_continuation(), patient_record, patients

### Community 228 - "Community 228"
Cohesion: 0.60
Nodes (3): external_laboratory_sources, external_laboratory_source_events, trg_external_laboratory_source_events_immutable

### Community 138 - "Community 138"
Cohesion: 0.29
Nodes (11): external_laboratory_ingestions, external_laboratory_sources, patients, lab_orders, lab_specimens, lab_reports, external_laboratory_ingestion_events, external_laboratory_report_links (+3 more)

### Community 186 - "Community 186"
Cohesion: 0.48
Nodes (5): external_laboratory_source_facility_grants, external_laboratory_sources, facilities, external_laboratory_source_facility_events, trg_external_laboratory_source_facility_events_immutable

### Community 201 - "Community 201"
Cohesion: 0.53
Nodes (4): auth_external_identity_mappings, auth_accounts, auth_external_identity_mapping_events, trg_auth_external_identity_mapping_events_immutable

### Community 202 - "Community 202"
Cohesion: 0.53
Nodes (4): patient_portal_external_identity_mappings, patients, patient_portal_external_identity_mapping_events, trg_patient_portal_external_identity_mapping_events_immutable

### Community 139 - "Community 139"
Cohesion: 0.28
Nodes (12): trg_lab_report_review_event_content, lab_report_review_events, avenchart_capture_lab_report_review_content(), the, an, old.content_revision, old.content_checksum, old.content_manifest (+4 more)

### Community 156 - "Community 156"
Cohesion: 0.35
Nodes (10): avenchart_reject_locked_encounter_mutation(), target_encounter, lab_orders, lab_reports, encounter_track_records, until, encounter_signatures, is_locked (+2 more)

### Community 259 - "Community 259"
Cohesion: 0.83
Nodes (3): message_content_events, messages, patients

### Community 203 - "Community 203"
Cohesion: 0.53
Nodes (4): critical_lab_result_follow_ups, lab_results, critical_lab_result_follow_up_events, trg_critical_follow_up_events_append_only

### Community 229 - "Community 229"
Cohesion: 0.60
Nodes (4): procedure_order_events, lab_orders, procedure_result_events, lab_results

### Community 260 - "Community 260"
Cohesion: 0.83
Nodes (3): integration_idempotency_conflicts, integration_outbox, integration_inbox

### Community 230 - "Community 230"
Cohesion: 0.60
Nodes (3): integration_outbox_provenance_events, integration_outbox, trg_integration_outbox_provenance_events_immutable

### Community 164 - "Community 164"
Cohesion: 0.20
Nodes (10): Get-AdministrationHeaders(), Set-AdministrationFacilityContext(), Cancel-AppointmentTestFixture(), Archive-EncounterTestFixture(), Archive-DocumentTestFixture(), Archive-MessageTestFixture(), New-ReceivedProcedureSpecimen(), Test-ProcedureOrderRetention() (+2 more)

### Community 204 - "Community 204"
Cohesion: 0.40
Nodes (2): Invoke-Api(), Start-TestApi()

### Community 205 - "Community 205"
Cohesion: 0.40
Nodes (2): Invoke-JsonRequest(), Get-EncounterDetail()

### Community 187 - "Community 187"
Cohesion: 0.33
Nodes (2): Get-PropertyValue(), Get-PathOperation()

### Community 232 - "Community 232"
Cohesion: 0.50
Nodes (2): Invoke-FixtureSql(), Set-FixturePortalState()

### Community 206 - "Community 206"
Cohesion: 0.40
Nodes (2): Assert-GeneratedAppointmentId(), New-PortalRequest()

### Community 57 - "Community 57"
Cohesion: 0.06
Nodes (25): __dirname, solutionRoot, workspaceRoot, bootstrapOnly, verifyBootstrap, datasetPath, outputDir, bootstrapPath (+17 more)

### Community 142 - "Community 142"
Cohesion: 0.30
Nodes (2): 0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts, 286a7d3 Add local Docker management scripts

### Community 239 - "Community 239"
Cohesion: 0.50
Nodes (3): 1b2ad1b docs(phase-2): record Graphify index evidence, c3a55ee chore(tooling): add Graphify code-navigation index, ccb790f docs(phase-2): record Graphify supply-chain residual

### Community 137 - "Community 137"
Cohesion: 0.14
Nodes (12): here, repositoryRoot, outputPath, historyBasePath, historyRef, sourceRevision, log, commits (+4 more)

## Knowledge Gaps
- **841 isolated node(s):** `AccessibilityFinding`, `clinicianFixture`, `codingEncounter`, `encounter`, `composeRoot` (+836 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 16`** (1 nodes): `af0f321 fix(validation): harden runtime workflow evidence`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 317`** (1 nodes): `AzureOperationsOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 318`** (1 nodes): `DatabaseConnectionOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 171`** (1 nodes): `RuntimeSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 165`** (1 nodes): `AddressBookRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 99`** (1 nodes): `AdministrationDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 30`** (1 nodes): `AdministrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 14`** (1 nodes): `AppointmentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 101`** (2 nodes): `AuthRepository`, `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 67`** (1 nodes): `AuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 143`** (1 nodes): `AzureOperationsAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 72`** (1 nodes): `AzureOperationsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 144`** (1 nodes): `BatchCommunicationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 8`** (1 nodes): `BillingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 180`** (1 nodes): `ChartTrackerRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 150`** (1 nodes): `ClinicalAlertEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (1 nodes): `ClinicalFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 31`** (1 nodes): `ClinicalFormRuntime`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 38`** (1 nodes): `ClinicalListRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 73`** (1 nodes): `ClinicalListStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 9`** (1 nodes): `DocumentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 83`** (1 nodes): `DocumentTemplateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 151`** (1 nodes): `EncounterLayoutFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 23`** (2 nodes): `EncounterRepository`, `DiagnosisAccumulator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 124`** (1 nodes): `EncounterStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 152`** (1 nodes): `ExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 108`** (1 nodes): `ExternalLaboratorySourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 74`** (1 nodes): `FhirRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 209`** (1 nodes): `FlowBoardRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 68`** (1 nodes): `IntegrationIdempotencyConflictException`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 109`** (1 nodes): `InventoryAccountingIntegrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 102`** (1 nodes): `InventoryCostPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 89`** (1 nodes): `InventoryReplenishmentPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 47`** (2 nodes): `InventoryRepository`, `InventoryItemBuilder`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 116`** (1 nodes): `ToInventoryLot()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 127`** (1 nodes): `InventoryValuationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 145`** (1 nodes): `LegacyClinicalFormDisplayRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 50`** (1 nodes): `ManagedRecordRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 44`** (1 nodes): `MessageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 172`** (1 nodes): `OfficeNoteRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 69`** (1 nodes): `PatientDisclosureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 240`** (1 nodes): `PatientMergeAuditRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 84`** (1 nodes): `PatientMergeExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 153`** (1 nodes): `PatientPortalExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (1 nodes): `PatientPortalRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 117`** (1 nodes): `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 154`** (1 nodes): `PatientPrintRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 188`** (1 nodes): `PatientRecordRequestRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 13`** (1 nodes): `PatientRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 134`** (1 nodes): `PatientSdohRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 155`** (1 nodes): `PatientXmlExchangeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 210`** (1 nodes): `PhiAuditRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 104`** (1 nodes): `ProcedureDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (1 nodes): `ProcedureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 174`** (1 nodes): `RecallRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 80`** (1 nodes): `ReferralRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 56`** (1 nodes): `ReportDefinitionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 85`** (2 nodes): `ReportExecutionQueueRepository`, `WorkerCancellationState`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 36`** (1 nodes): `ReportExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 53`** (1 nodes): `ReportRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 118`** (1 nodes): `TherapyGroupRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 261`** (1 nodes): `AdministrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 262`** (1 nodes): `AdministrativeReferenceEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 263`** (1 nodes): `AppointmentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 135`** (1 nodes): `AvenChartOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 76`** (1 nodes): `AzureDeploymentProfilePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 189`** (1 nodes): `AzureOperationsEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 264`** (1 nodes): `BillingEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 265`** (1 nodes): `ClinicalFormEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 266`** (1 nodes): `ClinicalListEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 267`** (1 nodes): `ClinicalWorkflowEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 268`** (1 nodes): `ConfigurationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 161`** (1 nodes): `DatabaseSchemaMigrator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 211`** (1 nodes): `DevelopmentTestIdentityProviderEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 269`** (1 nodes): `DocumentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 270`** (1 nodes): `DocumentTemplateEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 271`** (1 nodes): `EncounterEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 146`** (1 nodes): `EndpointAccessPolicies`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 241`** (1 nodes): `ExternalLaboratoryFhirIntakeEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 212`** (1 nodes): `FhirR4Endpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 213`** (1 nodes): `FhirR4ValidationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 242`** (1 nodes): `IntegrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 272`** (1 nodes): `InventoryEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 273`** (1 nodes): `ManagedRecordEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 274`** (1 nodes): `MessageEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 275`** (1 nodes): `OfficeNoteEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 214`** (1 nodes): `PatientEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 276`** (1 nodes): `PatientEngagementEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 277`** (1 nodes): `PatientPortalEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 278`** (1 nodes): `ProcedureEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 279`** (1 nodes): `ReportEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 215`** (1 nodes): `SchemaMigrationCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 280`** (1 nodes): `StaffAuthenticationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 281`** (1 nodes): `TherapyGroupEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 235`** (1 nodes): `AuthorizationPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 55`** (1 nodes): `BrowserOidcSessionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 207`** (1 nodes): `TestIdentityProviderService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 178`** (1 nodes): `ClinicalWorkflowPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 332`** (1 nodes): `AvenChart.Api.csproj`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 179`** (1 nodes): `DatabaseBootstrapCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 157`** (1 nodes): `FhirR4ValidationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 236`** (1 nodes): `StaffAccessContextServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 316`** (2 nodes): `pharmacies`, `prescriptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 319`** (1 nodes): `schema_migrations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 320`** (1 nodes): `statement_email_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 282`** (2 nodes): `integration_outbox`, `integration_inbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 321`** (1 nodes): `phi_access_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 322`** (1 nodes): `encounter_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 283`** (2 nodes): `practice_settings`, `practice_setting_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 284`** (2 nodes): `coding_catalog_audit_events`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 323`** (1 nodes): `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 324`** (1 nodes): `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 325`** (1 nodes): `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 285`** (2 nodes): `form_option_lists`, `form_option_values`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 286`** (2 nodes): `encounter_clinical_alert_acknowledgments`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 287`** (2 nodes): `patient_record_requests`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 288`** (2 nodes): `patient_sdoh_assessments`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 326`** (1 nodes): `office_notes`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 327`** (1 nodes): `address_book_contacts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 328`** (1 nodes): `track_anything_types`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 329`** (1 nodes): `patient_education_resources`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 289`** (2 nodes): `recall_activity`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 330`** (1 nodes): `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 290`** (2 nodes): `patient_duplicate_review_dispositions`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 291`** (2 nodes): `document_template_binary_versions`, `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 292`** (2 nodes): `patient_xml_exchange_audits`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 293`** (2 nodes): `inventory_count_reconciliations`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 294`** (2 nodes): `practice_setting_revisions`, `practice_settings`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 295`** (2 nodes): `coding_catalog_revisions`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 296`** (2 nodes): `form_option_list_revisions`, `form_option_lists`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 297`** (2 nodes): `form_layout_revisions`, `form_layouts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 298`** (2 nodes): `clinical_alert_rule_revisions`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 299`** (2 nodes): `module_catalog_revisions`, `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 300`** (2 nodes): `api_client_registry_revisions`, `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 301`** (2 nodes): `inventory_lot_metadata_audits`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 302`** (2 nodes): `inventory_lot_destructions`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 303`** (2 nodes): `inventory_controlled_report_runs`, `inventory_controlled_locations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 304`** (2 nodes): `inventory_controlled_report_exports`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 305`** (2 nodes): `coding_catalog_change_requests`, `coding_catalog_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 306`** (2 nodes): `form_layout_change_requests`, `form_layout_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 307`** (2 nodes): `form_option_list_change_requests`, `form_option_list_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 308`** (2 nodes): `clinical_alert_rule_change_requests`, `clinical_alert_rule_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 309`** (2 nodes): `module_change_requests`, `module_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 310`** (2 nodes): `api_client_change_requests`, `api_client_change_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 311`** (2 nodes): `configuration_package_import_requests`, `configuration_package_import_request_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 312`** (2 nodes): `clinical_form_migration_manifest_events`, `clinical_form_migration_manifests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 313`** (2 nodes): `message_escalation_events`, `messages`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 314`** (2 nodes): `procedure_specimen_events`, `lab_specimens`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 315`** (2 nodes): `patient_registration_duplicate_reviews`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 331`** (1 nodes): `inventory_controlled_action_attestations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 204`** (2 nodes): `Invoke-Api()`, `Start-TestApi()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 205`** (2 nodes): `Invoke-JsonRequest()`, `Get-EncounterDetail()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 187`** (2 nodes): `Get-PropertyValue()`, `Get-PathOperation()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 232`** (2 nodes): `Invoke-FixtureSql()`, `Set-FixturePortalState()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 206`** (2 nodes): `Assert-GeneratedAppointmentId()`, `New-PortalRequest()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 142`** (2 nodes): `0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts`, `286a7d3 Add local Docker management scripts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AdministrationRepository` connect `Community 30` to `Community 7`, `Community 115`, `Community 123`, `Community 92`, `Community 79`, `Community 100`, `Community 132`, `Community 71`, `Community 82`, `Community 88`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **Why does `PatientPortalRepository` connect `Community 26` to `Community 3`, `Community 110`, `Community 111`, `Community 125`, `Community 62`, `Community 54`, `Community 166`, `Community 167`, `Community 117`, `Community 173`?**
  _High betweenness centrality (0.060) - this node is a cross-community bridge._
- **Why does `InventoryRepository` connect `Community 47` to `Community 98`, `Community 181`, `Community 103`, `Community 93`, `Community 133`, `Community 116`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **What connects `AccessibilityFinding`, `clinicianFixture`, `codingEncounter` to the rest of the system?**
  _841 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 39` be split into smaller, more focused modules?**
  _Cohesion score 0.057971014492753624 - nodes in this community are weakly interconnected._
- **Should `Community 19` be split into smaller, more focused modules?**
  _Cohesion score 0.04195804195804196 - nodes in this community are weakly interconnected._
- **Should `Community 16` be split into smaller, more focused modules?**
  _Cohesion score 0.023809523809523808 - nodes in this community are weakly interconnected._